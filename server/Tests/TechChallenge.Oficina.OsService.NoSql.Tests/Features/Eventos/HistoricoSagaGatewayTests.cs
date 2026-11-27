using System.Text.RegularExpressions;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Logging;
using Moq;
using TechChallenge.Oficina.OsService.NoSql.Configuration;
using TechChallenge.Oficina.OsService.NoSql.DynamoDb;
using TechChallenge.Oficina.OsService.NoSql.Features.Eventos;
using TechChallenge.Oficina.OsService.UseCases.Features.Eventos;
using TechChallenge.Oficina.OsService.UseCases.Features.Eventos.Interfaces;
using TechChallenge.Oficina.OsService.UseCases.Features.OrdensServico.Logging;
using Xunit;

namespace TechChallenge.Oficina.OsService.NoSql.Tests.Features.Eventos;

public sealed class HistoricoSagaGatewayTests
{
    private const string Tabela = "techchallenge-oficina-os-saga-historico";

    private static readonly DateTime OcorridoEm = new(2026, 11, 27, 10, 15, 30, 123, DateTimeKind.Utc);

    private readonly Mock<IDynamoDbClient> _dynamoDbClientMock = new();
    private readonly Mock<ILogger<HistoricoSagaGateway>> _loggerMock = new();
    private readonly Guid _ordemServicoId = Guid.NewGuid();

    private HistoricoSagaGateway CriarGateway() =>
        new(_dynamoDbClientMock.Object, new DynamoDbSettings { TabelaHistoricoSaga = Tabela }, _loggerMock.Object);

    // Pagamento aprovado no Billing e aplicado: a OS passou de aguardando aprovacao (3) para em execucao (4)
    private EventoHistoricoSaga CriarEvento() => new()
    {
        OrdemServicoId = _ordemServicoId,
        OcorridoEm = OcorridoEm,
        Tipo = "PagamentoAprovado",
        Origem = OrigensHistoricoSaga.Billing,
        Resultado = ResultadoProcessamentoEvento.Processado,
        StatusAnterior = 3,
        StatusNovo = 4
    };

    // Evento repetido (reentrega da fila): a transicao foi recusada e a OS seguiu em execucao (4)
    private EventoHistoricoSaga CriarIgnoradoPorTransicao() => CriarEvento() with
    {
        Resultado = ResultadoProcessamentoEvento.Ignorado,
        StatusAnterior = 4,
        StatusNovo = 4,
        Motivo = "Somente ordem de servico aguardando aprovacao pode ter o pagamento aprovado."
    };

    // OS inexistente no OS Service: sem status para registrar
    private EventoHistoricoSaga CriarIgnoradoSemOrdem() => CriarEvento() with
    {
        Tipo = "OrdemServicoRejeitada",
        Origem = OrigensHistoricoSaga.Management,
        Resultado = ResultadoProcessamentoEvento.Ignorado,
        StatusAnterior = null,
        StatusNovo = null,
        Motivo = "Ordem de servico nao encontrada."
    };

    private (LogLevel Nivel, IReadOnlyDictionary<string, object?> Propriedades, Exception? Excecao) ObterRegistroDeFalha()
    {
        var invocacao = Assert.Single(_loggerMock.Invocations, chamada => chamada.Method.Name == nameof(ILogger.Log) && ((EventId)chamada.Arguments[1]).Id == OrdemServicoLogEventos.IntegracaoFalha.Id);
        var propriedades = ((IEnumerable<KeyValuePair<string, object?>>)invocacao.Arguments[2]).ToDictionary(propriedade => propriedade.Key, propriedade => propriedade.Value);

        return ((LogLevel)invocacao.Arguments[0], propriedades, (Exception?)invocacao.Arguments[3]);
    }

    [Fact]
    public async Task RegistrarAsync_DeveGravarItemComChavesTtlEAtributosDoContrato()
    {
        Dictionary<string, AttributeValue>? gravado = null;
        _dynamoDbClientMock
            .Setup(c => c.PutItemAsync(Tabela, It.IsAny<Dictionary<string, AttributeValue>>(), It.IsAny<CancellationToken>()))
            .Callback<string, Dictionary<string, AttributeValue>, CancellationToken>((_, item, _) => gravado = item)
            .Returns(Task.CompletedTask);

        await CriarGateway().RegistrarAsync(CriarEvento(), CancellationToken.None);

        Assert.NotNull(gravado);
        Assert.Equal(_ordemServicoId.ToString("D"), gravado["OrdemServicoId"].S);
        Assert.Matches(new Regex("^2026-11-27T10:15:30\\.123Z#[0-9a-f]{8}$"), gravado["OcorridoEm"].S);
        Assert.Equal(new DateTimeOffset(OcorridoEm).AddDays(180).ToUnixTimeSeconds().ToString(), gravado["ExpiraEm"].N);
        Assert.Equal("PagamentoAprovado", gravado["Tipo"].S);
        Assert.Equal("billing", gravado["Origem"].S);
        Assert.Equal("Processado", gravado["Resultado"].S);
        Assert.Equal("3", gravado["StatusAnterior"].N);
        Assert.Equal("4", gravado["StatusNovo"].N);
        Assert.DoesNotContain("Motivo", gravado.Keys);
        Assert.Equal(8, gravado.Count);
        Assert.Empty(_loggerMock.Invocations);
    }

    [Fact]
    public void CriarItem_DeveGravarOsStatusEOMotivo_QuandoTransicaoRecusada()
    {
        var item = HistoricoSagaGateway.CriarItem(CriarIgnoradoPorTransicao());

        Assert.Equal("Ignorado", item["Resultado"].S);
        Assert.Equal("4", item["StatusAnterior"].N);
        Assert.Equal("4", item["StatusNovo"].N);
        Assert.Equal("Somente ordem de servico aguardando aprovacao pode ter o pagamento aprovado.", item["Motivo"].S);
        Assert.Equal(9, item.Count);
    }

    [Fact]
    public void CriarItem_NaoDeveIncluirOsStatus_QuandoOrdemNaoEncontrada()
    {
        var item = HistoricoSagaGateway.CriarItem(CriarIgnoradoSemOrdem());

        Assert.DoesNotContain("StatusAnterior", item.Keys);
        Assert.DoesNotContain("StatusNovo", item.Keys);
        Assert.Equal("OrdemServicoRejeitada", item["Tipo"].S);
        Assert.Equal("management", item["Origem"].S);
        Assert.Equal("Ignorado", item["Resultado"].S);
        Assert.Equal("Ordem de servico nao encontrada.", item["Motivo"].S);
        Assert.Equal(7, item.Count);
    }

    [Fact]
    public void CriarChaveOrdenacao_DeveSerUnicaNoMesmoInstanteEOrdenarCronologicamente()
    {
        var primeira = HistoricoSagaGateway.CriarChaveOrdenacao(OcorridoEm);
        var mesmoInstante = HistoricoSagaGateway.CriarChaveOrdenacao(OcorridoEm);
        var seguinte = HistoricoSagaGateway.CriarChaveOrdenacao(OcorridoEm.AddMilliseconds(1));

        Assert.NotEqual(primeira, mesmoInstante);
        Assert.True(string.CompareOrdinal(primeira, seguinte) < 0);
        Assert.True(string.CompareOrdinal(mesmoInstante, seguinte) < 0);
    }

    [Fact]
    public void Mapear_DeveRecuperarOEventoGravado()
    {
        EventoHistoricoSaga[] eventos = [CriarEvento(), CriarIgnoradoPorTransicao(), CriarIgnoradoSemOrdem()];

        foreach (var evento in eventos)
        {
            var mapeado = HistoricoSagaGateway.Mapear(HistoricoSagaGateway.CriarItem(evento));

            Assert.Equal(evento, mapeado);
            Assert.Equal(DateTimeKind.Utc, mapeado.OcorridoEm.Kind);
        }
    }

    [Fact]
    public async Task RegistrarAsync_DeveRegistrarWarningSemLancar_QuandoDynamoDbFalha()
    {
        var falha = new InvalidOperationException("DynamoDB indisponivel");
        _dynamoDbClientMock
            .Setup(c => c.PutItemAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, AttributeValue>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(falha);

        await CriarGateway().RegistrarAsync(CriarEvento(), CancellationToken.None);

        var (nivel, propriedades, excecao) = ObterRegistroDeFalha();
        Assert.Equal(LogLevel.Warning, nivel);
        Assert.Same(falha, excecao);
        Assert.Equal("dynamodb", propriedades["Integracao"]);
        Assert.Equal("registro_historico", propriedades["Operacao"]);
        Assert.Equal("PagamentoAprovado", propriedades["Tipo"]);
        Assert.Equal(_ordemServicoId, propriedades["OrdemServicoId"]);
    }

    [Fact]
    public async Task RegistrarAsync_DeveRegistrarWarningSemLancar_QuandoTempoEsgotadoSemCancelarOConsumo()
    {
        // O caso de uso registra com CancellationToken.None: o tempo esgotado do SDK (TaskCanceledException) tambem e so um aviso
        _dynamoDbClientMock
            .Setup(c => c.PutItemAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, AttributeValue>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TaskCanceledException("Tempo esgotado"));

        await CriarGateway().RegistrarAsync(CriarEvento(), CancellationToken.None);

        var (nivel, _, excecao) = ObterRegistroDeFalha();
        Assert.Equal(LogLevel.Warning, nivel);
        Assert.IsType<TaskCanceledException>(excecao);
    }

    [Fact]
    public async Task RegistrarAsync_DevePropagarCancelamentoDoChamador()
    {
        using var cancelamento = new CancellationTokenSource();
        cancelamento.Cancel();
        _dynamoDbClientMock
            .Setup(c => c.PutItemAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, AttributeValue>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException(cancelamento.Token));

        var action = async () => await CriarGateway().RegistrarAsync(CriarEvento(), cancelamento.Token);

        await Assert.ThrowsAsync<OperationCanceledException>(action);
        Assert.Empty(_loggerMock.Invocations);
    }

    [Fact]
    public async Task ListarAsync_DeveConsultarAParticaoDaOrdemEMapearOsItens()
    {
        var aplicado = CriarEvento();
        var repetido = CriarIgnoradoPorTransicao() with { OcorridoEm = OcorridoEm.AddMinutes(2) };
        _dynamoDbClientMock
            .Setup(c => c.QueryAsync(Tabela, "OrdemServicoId", _ordemServicoId.ToString("D"), It.IsAny<CancellationToken>()))
            .ReturnsAsync([HistoricoSagaGateway.CriarItem(aplicado), HistoricoSagaGateway.CriarItem(repetido)]);

        var historico = await CriarGateway().ListarAsync(_ordemServicoId, CancellationToken.None);

        Assert.Equal([aplicado, repetido], historico);
    }

    [Fact]
    public async Task ListarAsync_DeveRegistrarErroERelancar_QuandoDynamoDbFalha()
    {
        var falha = new InvalidOperationException("DynamoDB indisponivel");
        _dynamoDbClientMock
            .Setup(c => c.QueryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(falha);

        var action = async () => await CriarGateway().ListarAsync(_ordemServicoId, CancellationToken.None);

        Assert.Same(falha, await Assert.ThrowsAsync<InvalidOperationException>(action));
        var (nivel, propriedades, excecao) = ObterRegistroDeFalha();
        Assert.Equal(LogLevel.Error, nivel);
        Assert.Same(falha, excecao);
        Assert.Equal("dynamodb", propriedades["Integracao"]);
        Assert.Equal("consulta_historico", propriedades["Operacao"]);
        Assert.Equal(_ordemServicoId, propriedades["OrdemServicoId"]);
    }

    [Fact]
    public async Task ListarAsync_DevePropagarCancelamentoDaRequisicaoSemRegistrarFalha()
    {
        // Cliente desconectou: o middleware trata o cancelamento (499), sem integracao.falha
        using var cancelamento = new CancellationTokenSource();
        cancelamento.Cancel();
        _dynamoDbClientMock
            .Setup(c => c.QueryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException(cancelamento.Token));

        var action = async () => await CriarGateway().ListarAsync(_ordemServicoId, cancelamento.Token);

        await Assert.ThrowsAsync<OperationCanceledException>(action);
        Assert.Empty(_loggerMock.Invocations);
    }
}
