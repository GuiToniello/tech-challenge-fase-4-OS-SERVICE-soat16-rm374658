using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TechChallenge.Oficina.OsService.Messaging.Configuration;
using TechChallenge.Oficina.OsService.Messaging.Features.Eventos;
using TechChallenge.Oficina.OsService.Messaging.Sqs;
using TechChallenge.Oficina.OsService.UseCases.Features.Eventos.Contratos;
using TechChallenge.Oficina.OsService.UseCases.Features.OrdensServico.Logging;
using Xunit;

namespace TechChallenge.Oficina.OsService.Messaging.Tests.Features.Eventos;

public sealed class EventosPublisherTests
{
    private const string FilaManagement = "techchallenge-oficina-management.fifo";
    private const string FilaBilling = "techchallenge-oficina-billing.fifo";
    private const string UrlFilaManagement = "http://elasticmq:9324/000000000000/techchallenge-oficina-management.fifo";
    private const string UrlFilaBilling = "http://elasticmq:9324/000000000000/techchallenge-oficina-billing.fifo";

    private readonly Mock<ISqsClient> _sqsClientMock = new();
    private readonly SqsSettings _sqsSettings = new() { FilasPublicacao = [FilaManagement, FilaBilling], FilaConsumo = "techchallenge-oficina-os-service.fifo" };
    private readonly List<(string Url, string Corpo, string Tipo, string Grupo, string Deduplicacao)> _enviadas = [];

    public EventosPublisherTests()
    {
        _sqsClientMock.Setup(c => c.GetQueueUrlAsync(FilaManagement, It.IsAny<CancellationToken>())).ReturnsAsync(UrlFilaManagement);
        _sqsClientMock.Setup(c => c.GetQueueUrlAsync(FilaBilling, It.IsAny<CancellationToken>())).ReturnsAsync(UrlFilaBilling);
        _sqsClientMock
            .Setup(c => c.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, string, string, CancellationToken>((url, corpo, tipo, grupo, deduplicacao, _) => _enviadas.Add((url, corpo, tipo, grupo, deduplicacao)))
            .Returns(Task.CompletedTask);
    }

    private EventosPublisher CriarPublisher(ILogger<EventosPublisher>? logger = null) =>
        new(_sqsClientMock.Object, _sqsSettings, logger ?? NullLogger<EventosPublisher>.Instance);

    private void ConfigurarFalhaNoEnvio(string urlFila, Exception falha) =>
        _sqsClientMock
            .Setup(c => c.SendAsync(urlFila, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(falha);

    private static IReadOnlyList<IReadOnlyDictionary<string, object?>> ObterPropriedadesDosLogs(Mock<ILogger<EventosPublisher>> loggerMock, EventId evento) =>
        loggerMock.Invocations
            .Where(chamada => chamada.Method.Name == nameof(ILogger.Log) && ((EventId)chamada.Arguments[1]).Id == evento.Id)
            .Select(chamada => (IReadOnlyDictionary<string, object?>)((IEnumerable<KeyValuePair<string, object?>>)chamada.Arguments[2]).ToDictionary(propriedade => propriedade.Key, propriedade => propriedade.Value))
            .ToList();

    [Fact]
    public async Task PublicarAsync_DeveEnviarEnvelopeEmCamelCase()
    {
        var ordemServicoId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        var veiculoId = Guid.NewGuid();
        var servicoId = Guid.NewGuid();

        await CriarPublisher().PublicarAsync(new OrdemServicoAberta(ordemServicoId, clienteId, veiculoId, [servicoId]), ordemServicoId);

        var (_, corpo, tipo, grupo, deduplicacao) = _enviadas[0];
        Assert.Equal("OrdemServicoAberta", tipo);

        using var json = JsonDocument.Parse(corpo);
        var envelope = json.RootElement;
        Assert.Equal(["id", "tipo", "versao", "ocorridoEm", "origem", "dados"], envelope.EnumerateObject().Select(propriedade => propriedade.Name));
        Assert.Equal("OrdemServicoAberta", envelope.GetProperty("tipo").GetString());
        Assert.Equal(1, envelope.GetProperty("versao").GetInt32());
        Assert.Equal("os-service", envelope.GetProperty("origem").GetString());

        var ocorridoEm = envelope.GetProperty("ocorridoEm").GetString()!;
        Assert.EndsWith("Z", ocorridoEm);
        Assert.InRange(DateTime.Parse(ocorridoEm, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal), DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(1));

        var dados = envelope.GetProperty("dados");
        Assert.Equal(["ordemServicoId", "clienteId", "veiculoId", "servicoIds"], dados.EnumerateObject().Select(propriedade => propriedade.Name));
        Assert.Equal(ordemServicoId, dados.GetProperty("ordemServicoId").GetGuid());
        Assert.Equal(clienteId, dados.GetProperty("clienteId").GetGuid());
        Assert.Equal(veiculoId, dados.GetProperty("veiculoId").GetGuid());
        Assert.Equal([servicoId], dados.GetProperty("servicoIds").EnumerateArray().Select(item => item.GetGuid()));

        // Grupo FIFO = ordem de servico; deduplicacao = id do envelope
        Assert.Equal(ordemServicoId.ToString("D"), grupo);
        Assert.Equal(envelope.GetProperty("id").GetGuid().ToString("D"), deduplicacao);
    }

    [Fact]
    public async Task PublicarAsync_DeveEnviarOMesmoEnvelopeParaCadaFilaDaLista()
    {
        var ordemServicoId = Guid.NewGuid();

        await CriarPublisher().PublicarAsync(new OrdemServicoStatusAlterado(ordemServicoId, Guid.NewGuid(), 3, 4), ordemServicoId);

        // Fan-out na ordem da lista: Management e Billing recebem o mesmo corpo, com o mesmo id de envelope (deduplicacao), tipo e grupo
        Assert.Equal([UrlFilaManagement, UrlFilaBilling], _enviadas.Select(enviada => enviada.Url));
        Assert.Single(_enviadas.Select(enviada => enviada.Corpo).Distinct());
        Assert.Single(_enviadas.Select(enviada => enviada.Deduplicacao).Distinct());
        Assert.All(_enviadas, enviada =>
        {
            Assert.Equal("OrdemServicoStatusAlterado", enviada.Tipo);
            Assert.Equal(ordemServicoId.ToString("D"), enviada.Grupo);
        });

        using var json = JsonDocument.Parse(_enviadas[0].Corpo);
        Assert.Equal(json.RootElement.GetProperty("id").GetGuid().ToString("D"), _enviadas[0].Deduplicacao);
    }

    [Fact]
    public async Task PublicarAsync_DeveSerializarStatusComoNumeros()
    {
        var ordemServicoId = Guid.NewGuid();

        await CriarPublisher().PublicarAsync(new OrdemServicoStatusAlterado(ordemServicoId, Guid.NewGuid(), 3, 4), ordemServicoId);

        var (_, corpo, tipo, _, _) = _enviadas[0];
        Assert.Equal("OrdemServicoStatusAlterado", tipo);
        using var json = JsonDocument.Parse(corpo);
        var dados = json.RootElement.GetProperty("dados");
        Assert.Equal(JsonValueKind.Number, dados.GetProperty("statusAnterior").ValueKind);
        Assert.Equal(3, dados.GetProperty("statusAnterior").GetInt32());
        Assert.Equal(4, dados.GetProperty("statusNovo").GetInt32());
    }

    [Fact]
    public async Task PublicarAsync_DeveGerarIdDeEnvelopeDiferentePorEvento()
    {
        var publisher = CriarPublisher();
        var ordemServicoId = Guid.NewGuid();

        await publisher.PublicarAsync(new OrdemServicoExcluida(ordemServicoId), ordemServicoId);
        await publisher.PublicarAsync(new OrdemServicoExcluida(ordemServicoId), ordemServicoId);

        // 2 eventos x 2 filas: um id de envelope por evento, repetido nas duas filas
        Assert.Equal(4, _enviadas.Count);
        Assert.Equal(2, _enviadas.Select(enviada => enviada.Deduplicacao).Distinct().Count());
        Assert.All(_enviadas, enviada => Assert.Equal(ordemServicoId.ToString("D"), enviada.Grupo));
    }

    [Fact]
    public async Task PublicarAsync_DeveObterUrlDeCadaFilaUmaVez()
    {
        var publisher = CriarPublisher();
        var ordemServicoId = Guid.NewGuid();

        await publisher.PublicarAsync(new OrdemServicoExcluida(ordemServicoId), ordemServicoId);
        await publisher.PublicarAsync(new OrdemServicoExcluida(ordemServicoId), ordemServicoId);

        _sqsClientMock.Verify(c => c.GetQueueUrlAsync(FilaManagement, It.IsAny<CancellationToken>()), Times.Once);
        _sqsClientMock.Verify(c => c.GetQueueUrlAsync(FilaBilling, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PublicarAsync_DeveRegistrarEventoPublicadoPorFila()
    {
        var loggerMock = new Mock<ILogger<EventosPublisher>>();
        var ordemServicoId = Guid.NewGuid();

        await CriarPublisher(loggerMock.Object).PublicarAsync(new OrdemServicoExcluida(ordemServicoId), ordemServicoId);

        var registros = ObterPropriedadesDosLogs(loggerMock, OrdemServicoLogEventos.EventoPublicado);
        Assert.Equal([FilaManagement, FilaBilling], registros.Select(propriedades => propriedades["Fila"]));
        Assert.All(registros, propriedades =>
        {
            Assert.Equal("OrdemServicoExcluida", propriedades["Tipo"]);
            Assert.Equal(ordemServicoId, propriedades["OrdemServicoId"]);
        });
        Assert.Single(registros.Select(propriedades => propriedades["EventoId"]).Distinct());
        Assert.All(
            loggerMock.Invocations.Where(chamada => chamada.Method.Name == nameof(ILogger.Log)),
            chamada => Assert.Equal(LogLevel.Information, (LogLevel)chamada.Arguments[0]));
    }

    [Fact]
    public async Task PublicarAsync_DeveRegistrarFalhaDeIntegracaoERelancar_QuandoEnvioFalhar()
    {
        var loggerMock = new Mock<ILogger<EventosPublisher>>();
        var falha = new InvalidOperationException("SQS indisponivel");
        var ordemServicoId = Guid.NewGuid();
        ConfigurarFalhaNoEnvio(UrlFilaBilling, falha);

        var action = async () => await CriarPublisher(loggerMock.Object).PublicarAsync(new OrdemServicoExcluida(ordemServicoId), ordemServicoId);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(action);
        Assert.Same(falha, exception);

        // A fila anterior (Management) ja recebeu o evento; a falha e registrada para a fila do Billing
        var publicado = Assert.Single(ObterPropriedadesDosLogs(loggerMock, OrdemServicoLogEventos.EventoPublicado));
        Assert.Equal(FilaManagement, publicado["Fila"]);

        var invocacao = Assert.Single(loggerMock.Invocations, chamada => chamada.Method.Name == nameof(ILogger.Log) && ((EventId)chamada.Arguments[1]).Id == OrdemServicoLogEventos.IntegracaoFalha.Id);
        var propriedades = ((IEnumerable<KeyValuePair<string, object?>>)invocacao.Arguments[2]).ToDictionary(propriedade => propriedade.Key, propriedade => propriedade.Value);
        Assert.Equal(LogLevel.Error, (LogLevel)invocacao.Arguments[0]);
        Assert.Same(falha, invocacao.Arguments[3]);
        Assert.Equal("sqs", propriedades["Integracao"]);
        Assert.Equal("publicacao", propriedades["Operacao"]);
        Assert.Equal("OrdemServicoExcluida", propriedades["Tipo"]);
        Assert.Equal(FilaBilling, propriedades["Fila"]);
    }

    [Fact]
    public async Task PublicarAsync_DeveInterromperNasDemaisFilas_QuandoEnvioNaPrimeiraFalhar()
    {
        var loggerMock = new Mock<ILogger<EventosPublisher>>();
        var ordemServicoId = Guid.NewGuid();
        ConfigurarFalhaNoEnvio(UrlFilaManagement, new InvalidOperationException("SQS indisponivel"));

        var action = async () => await CriarPublisher(loggerMock.Object).PublicarAsync(new OrdemServicoExcluida(ordemServicoId), ordemServicoId);

        await Assert.ThrowsAsync<InvalidOperationException>(action);
        _sqsClientMock.Verify(c => c.SendAsync(UrlFilaBilling, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Empty(ObterPropriedadesDosLogs(loggerMock, OrdemServicoLogEventos.EventoPublicado));
        Assert.Equal(FilaManagement, Assert.Single(ObterPropriedadesDosLogs(loggerMock, OrdemServicoLogEventos.IntegracaoFalha))["Fila"]);
    }

    [Fact]
    public async Task PublicarAsync_DeveRelancarSemRegistrarFalha_QuandoCancelado()
    {
        var loggerMock = new Mock<ILogger<EventosPublisher>>();
        using var cancelamento = new CancellationTokenSource();
        cancelamento.Cancel();
        _sqsClientMock
            .Setup(c => c.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException(cancelamento.Token));
        var ordemServicoId = Guid.NewGuid();

        var action = async () => await CriarPublisher(loggerMock.Object).PublicarAsync(new OrdemServicoExcluida(ordemServicoId), ordemServicoId, cancelamento.Token);

        await Assert.ThrowsAsync<OperationCanceledException>(action);
        Assert.DoesNotContain(loggerMock.Invocations, chamada => chamada.Method.Name == nameof(ILogger.Log) && ((EventId)chamada.Arguments[1]).Id == OrdemServicoLogEventos.IntegracaoFalha.Id);
    }
}
