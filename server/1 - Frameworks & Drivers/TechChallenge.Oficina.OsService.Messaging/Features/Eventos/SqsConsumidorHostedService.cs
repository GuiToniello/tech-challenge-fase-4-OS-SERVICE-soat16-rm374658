using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TechChallenge.Oficina.OsService.Controllers.Features.Eventos;
using TechChallenge.Oficina.OsService.Messaging.Configuration;
using TechChallenge.Oficina.OsService.Messaging.Sqs;
using TechChallenge.Oficina.OsService.UseCases.Features.Eventos;
using TechChallenge.Oficina.OsService.UseCases.Features.OrdensServico.Logging;

namespace TechChallenge.Oficina.OsService.Messaging.Features.Eventos;

// Consome a fila FIFO do OS Service com long polling. Mensagem processada ou ignorada sai da fila;
// em caso de excecao ela fica, volta apos o visibility timeout e, depois de 5 recebimentos, vai para a DLQ.
public sealed class SqsConsumidorHostedService : BackgroundService
{
    private const int QuantidadeMaximaMensagens = 10;
    private const int TempoEsperaSegundos = 20;
    private static readonly TimeSpan IntervaloNovaTentativa = TimeSpan.FromSeconds(5);

    private readonly ISqsClient _sqsClient;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SqsSettings _sqsSettings;
    private readonly ILogger<SqsConsumidorHostedService> _logger;

    public SqsConsumidorHostedService(
        ISqsClient sqsClient,
        IServiceScopeFactory scopeFactory,
        SqsSettings sqsSettings,
        ILogger<SqsConsumidorHostedService> logger)
    {
        _sqsClient = sqsClient;
        _scopeFactory = scopeFactory;
        _sqsSettings = sqsSettings;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var urlFila = await ObterUrlFilaAsync(stoppingToken);

        if (urlFila is null)
        {
            return;
        }

        _logger.LogInformation("Consumidor da fila {Fila} iniciado.", _sqsSettings.FilaConsumo);

        while (!stoppingToken.IsCancellationRequested)
        {
            IReadOnlyCollection<MensagemSqs> mensagens;

            try
            {
                mensagens = await _sqsClient.ReceiveAsync(urlFila, QuantidadeMaximaMensagens, TempoEsperaSegundos, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    OrdemServicoLogEventos.IntegracaoFalha,
                    exception,
                    "Falha na integracao {Integracao} durante a operacao {Operacao} ao receber mensagens da fila {Fila}.",
                    OrdemServicoLogEventos.Integracoes.Sqs,
                    OrdemServicoLogEventos.Operacoes.Consumo,
                    _sqsSettings.FilaConsumo);
                await AguardarNovaTentativaAsync(stoppingToken);
                continue;
            }

            // Em sequencia, para respeitar a ordem dos eventos de cada OS na fila FIFO
            foreach (var mensagem in mensagens)
            {
                await ProcessarMensagemAsync(urlFila, mensagem, stoppingToken);
            }
        }

        _logger.LogInformation("Consumidor da fila {Fila} encerrado.", _sqsSettings.FilaConsumo);
    }

    public async Task ProcessarMensagemAsync(string urlFila, MensagemSqs mensagem, CancellationToken cancellationToken)
    {
        string? tipo = null;
        Guid? eventoId = null;

        try
        {
            var envelope = LerEnvelope(mensagem.Body);
            tipo = envelope.Tipo;
            eventoId = envelope.Id;

            var resultado = await ProcessarEventoAsync(envelope, cancellationToken);

            if (resultado == ResultadoProcessamentoEvento.Processado)
            {
                _logger.LogInformation(
                    OrdemServicoLogEventos.EventoConsumido,
                    "Evento {Tipo} {EventoId} consumido da fila {Fila}.",
                    tipo,
                    eventoId,
                    _sqsSettings.FilaConsumo);
            }
            else
            {
                _logger.LogWarning(
                    OrdemServicoLogEventos.EventoIgnorado,
                    "Evento {Tipo} {EventoId} ignorado na fila {Fila}.",
                    tipo,
                    eventoId,
                    _sqsSettings.FilaConsumo);
            }

            await _sqsClient.DeleteAsync(urlFila, mensagem.ReceiptHandle, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Servico encerrando: a mensagem nao e excluida e volta para a fila apos o visibility timeout
        }
        catch (Exception exception)
        {
            _logger.LogError(
                OrdemServicoLogEventos.IntegracaoFalha,
                exception,
                "Falha na integracao {Integracao} durante a operacao {Operacao} do evento {Tipo} {EventoId} (mensagem {MensagemId}).",
                OrdemServicoLogEventos.Integracoes.Sqs,
                OrdemServicoLogEventos.Operacoes.Consumo,
                tipo,
                eventoId,
                mensagem.MessageId);
        }
    }

    private async Task<ResultadoProcessamentoEvento> ProcessarEventoAsync(EnvelopeEvento<JsonElement> envelope, CancellationToken cancellationToken)
    {
        // Um escopo por mensagem: DbContext e use cases novos, como numa requisicao HTTP
        await using var scope = _scopeFactory.CreateAsyncScope();
        var eventosController = scope.ServiceProvider.GetRequiredService<IEventosController>();

        return await eventosController.ProcessarAsync(envelope.Tipo, envelope.Dados.GetRawText(), cancellationToken);
    }

    private async Task<string?> ObterUrlFilaAsync(CancellationToken stoppingToken)
    {
        // A fila pode ainda nao existir na subida (ex.: ElasticMQ iniciando): tenta de novo ate conseguir ou o servico parar
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                return await _sqsClient.GetQueueUrlAsync(_sqsSettings.FilaConsumo, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return null;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    OrdemServicoLogEventos.IntegracaoFalha,
                    exception,
                    "Falha na integracao {Integracao} durante a operacao {Operacao} ao obter a fila {Fila}; nova tentativa em {IntervaloSegundos} segundos.",
                    OrdemServicoLogEventos.Integracoes.Sqs,
                    OrdemServicoLogEventos.Operacoes.Consumo,
                    _sqsSettings.FilaConsumo,
                    IntervaloNovaTentativa.TotalSeconds);
                await AguardarNovaTentativaAsync(stoppingToken);
            }
        }

        return null;
    }

    private static EnvelopeEvento<JsonElement> LerEnvelope(string corpo)
    {
        var envelope = JsonSerializer.Deserialize<EnvelopeEvento<JsonElement>>(corpo, SerializacaoEventos.Opcoes);

        if (envelope is null ||
            string.IsNullOrWhiteSpace(envelope.Tipo) ||
            envelope.Dados.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            throw new JsonException("A mensagem nao segue o envelope de eventos: tipo e dados sao obrigatorios.");
        }

        return envelope;
    }

    private static async Task AguardarNovaTentativaAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(IntervaloNovaTentativa, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Servico encerrando durante a espera
        }
    }
}
