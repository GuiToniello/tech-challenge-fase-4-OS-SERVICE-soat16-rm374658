using System.Globalization;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Logging;
using TechChallenge.Oficina.OsService.NoSql.Configuration;
using TechChallenge.Oficina.OsService.NoSql.DynamoDb;
using TechChallenge.Oficina.OsService.UseCases.Features.Eventos;
using TechChallenge.Oficina.OsService.UseCases.Features.Eventos.Interfaces;
using TechChallenge.Oficina.OsService.UseCases.Features.OrdensServico.Logging;

namespace TechChallenge.Oficina.OsService.NoSql.Features.Eventos;

// Historico da saga no DynamoDB: uma particao por OS (OrdemServicoId) e a chave de ordenacao "OcorridoEm#sufixo"
// (data UTC com milissegundos + id curto), em ordem cronologica e sem colisao entre eventos do mesmo instante. Cada item expira em 180 dias (TTL em ExpiraEm).
// O registro e best-effort: a falha sai como integracao.falha (Warning) e o processamento do evento segue. A consulta registra a falha e relanca.
internal sealed class HistoricoSagaGateway : IHistoricoSagaGateway
{
    public const string ChaveParticao = "OrdemServicoId";
    public const string ChaveOrdenacao = "OcorridoEm";
    public const string AtributoExpiracao = "ExpiraEm";
    public const string AtributoTipo = "Tipo";
    public const string AtributoOrigem = "Origem";
    public const string AtributoResultado = "Resultado";
    public const string AtributoStatusAnterior = "StatusAnterior";
    public const string AtributoStatusNovo = "StatusNovo";
    public const string AtributoMotivo = "Motivo";

    public static readonly TimeSpan Retencao = TimeSpan.FromDays(180);

    private const string FormatoData = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";
    private const char SeparadorChaveOrdenacao = '#';
    private const int TamanhoSufixo = 8;

    private readonly IDynamoDbClient _dynamoDbClient;
    private readonly DynamoDbSettings _settings;
    private readonly ILogger<HistoricoSagaGateway> _logger;

    public HistoricoSagaGateway(IDynamoDbClient dynamoDbClient, DynamoDbSettings settings, ILogger<HistoricoSagaGateway> logger)
    {
        _dynamoDbClient = dynamoDbClient;
        _settings = settings;
        _logger = logger;
    }

    public async Task RegistrarAsync(EventoHistoricoSaga evento, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evento);

        try
        {
            await _dynamoDbClient.PutItemAsync(_settings.TabelaHistoricoSaga, CriarItem(evento), cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // Historico complementar: o veredito do evento ja esta decidido (e gravado no PostgreSQL quando aplicado), entao a falha nao interrompe o processamento
            _logger.LogWarning(
                OrdemServicoLogEventos.IntegracaoFalha,
                exception,
                "Falha na integracao {Integracao} durante a operacao {Operacao} do evento {Tipo} da ordem de servico {OrdemServicoId}; o historico da saga fica sem este registro.",
                OrdemServicoLogEventos.Integracoes.DynamoDb,
                OrdemServicoLogEventos.Operacoes.RegistroHistorico,
                evento.Tipo,
                evento.OrdemServicoId);
        }
    }

    public async Task<IReadOnlyCollection<EventoHistoricoSaga>> ListarAsync(Guid ordemServicoId, CancellationToken cancellationToken)
    {
        try
        {
            var itens = await _dynamoDbClient.QueryAsync(_settings.TabelaHistoricoSaga, ChaveParticao, ordemServicoId.ToString("D"), cancellationToken);
            return itens.Select(Mapear).ToArray();
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(
                OrdemServicoLogEventos.IntegracaoFalha,
                exception,
                "Falha na integracao {Integracao} durante a operacao {Operacao} da ordem de servico {OrdemServicoId}.",
                OrdemServicoLogEventos.Integracoes.DynamoDb,
                OrdemServicoLogEventos.Operacoes.ConsultaHistorico,
                ordemServicoId);
            throw;
        }
    }

    // Sem dados pessoais e sem o conteudo do evento: so o identificador da OS, tipo, origem, resultado, status e motivo
    internal static Dictionary<string, AttributeValue> CriarItem(EventoHistoricoSaga evento)
    {
        var ocorridoEm = ParaUtc(evento.OcorridoEm);

        var item = new Dictionary<string, AttributeValue>
        {
            [ChaveParticao] = new AttributeValue { S = evento.OrdemServicoId.ToString("D") },
            [ChaveOrdenacao] = new AttributeValue { S = CriarChaveOrdenacao(ocorridoEm) },
            [AtributoExpiracao] = new AttributeValue { N = new DateTimeOffset(ocorridoEm).Add(Retencao).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture) },
            [AtributoTipo] = new AttributeValue { S = evento.Tipo },
            [AtributoOrigem] = new AttributeValue { S = evento.Origem },
            [AtributoResultado] = new AttributeValue { S = evento.Resultado.ToString() }
        };

        // Atributos opcionais so entram quando preenchidos (sem status quando a OS nao existe; motivo so no evento ignorado)
        if (evento.StatusAnterior is { } statusAnterior)
        {
            item[AtributoStatusAnterior] = new AttributeValue { N = statusAnterior.ToString(CultureInfo.InvariantCulture) };
        }

        if (evento.StatusNovo is { } statusNovo)
        {
            item[AtributoStatusNovo] = new AttributeValue { N = statusNovo.ToString(CultureInfo.InvariantCulture) };
        }

        if (evento.Motivo is { } motivo)
        {
            item[AtributoMotivo] = new AttributeValue { S = motivo };
        }

        return item;
    }

    internal static string CriarChaveOrdenacao(DateTime ocorridoEmUtc)
    {
        var sufixo = Guid.NewGuid().ToString("N")[..TamanhoSufixo];
        return $"{ocorridoEmUtc.ToString(FormatoData, CultureInfo.InvariantCulture)}{SeparadorChaveOrdenacao}{sufixo}";
    }

    internal static EventoHistoricoSaga Mapear(Dictionary<string, AttributeValue> item)
    {
        var data = item[ChaveOrdenacao].S.Split(SeparadorChaveOrdenacao)[0];

        return new EventoHistoricoSaga
        {
            OrdemServicoId = Guid.Parse(item[ChaveParticao].S),
            OcorridoEm = DateTime.ParseExact(data, FormatoData, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal),
            Tipo = item[AtributoTipo].S,
            Origem = item[AtributoOrigem].S,
            Resultado = Enum.Parse<ResultadoProcessamentoEvento>(item[AtributoResultado].S),
            StatusAnterior = item.TryGetValue(AtributoStatusAnterior, out var statusAnterior) ? LerNumero(statusAnterior.N) : null,
            StatusNovo = item.TryGetValue(AtributoStatusNovo, out var statusNovo) ? LerNumero(statusNovo.N) : null,
            Motivo = item.TryGetValue(AtributoMotivo, out var motivo) ? motivo.S : null
        };
    }

    private static int LerNumero(string valor)
    {
        return int.Parse(valor, NumberStyles.Integer, CultureInfo.InvariantCulture);
    }

    // Data sem fuso (Unspecified) e tratada como UTC, como o restante do servico (DateTime.UtcNow)
    private static DateTime ParaUtc(DateTime data)
    {
        return data.Kind == DateTimeKind.Local ? data.ToUniversalTime() : DateTime.SpecifyKind(data, DateTimeKind.Utc);
    }
}
