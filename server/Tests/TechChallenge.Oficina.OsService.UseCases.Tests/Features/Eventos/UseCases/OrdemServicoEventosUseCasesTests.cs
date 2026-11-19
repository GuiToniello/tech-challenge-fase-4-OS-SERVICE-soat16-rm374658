using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TechChallenge.Oficina.OsService.Entities.Features.OrdensServico;
using TechChallenge.Oficina.OsService.Entities.Features.OrdensServico.Enums;
using TechChallenge.Oficina.OsService.UseCases.Features.Eventos;
using TechChallenge.Oficina.OsService.UseCases.Features.Eventos.Contratos;
using TechChallenge.Oficina.OsService.UseCases.Features.Eventos.Interfaces;
using TechChallenge.Oficina.OsService.UseCases.Features.Eventos.UseCases;
using TechChallenge.Oficina.OsService.UseCases.Features.OrdensServico.Logging;
using TechChallenge.Oficina.OsService.UseCases.Features.OrdensServico.UseCases;
using Xunit;

namespace TechChallenge.Oficina.OsService.UseCases.Tests.Features.Eventos.UseCases;

public sealed class OrdemServicoEventosUseCasesTests
{
    private readonly Mock<IOrdemServicoGateway> _ordemServicoGatewayMock = new();
    private readonly Mock<IEventosPublisher> _eventosPublisherMock = new();

    private OrdemServicoEventosUseCases CriarUseCases(ILogger<OrdemServicoEventosUseCases>? logger = null)
    {
        return new OrdemServicoEventosUseCases(
            _ordemServicoGatewayMock.Object,
            _eventosPublisherMock.Object,
            logger ?? NullLogger<OrdemServicoEventosUseCases>.Instance);
    }

    private static OrdemServico CriarOrdemNoStatus(StatusOrdemServico status)
    {
        var ordemServico = OrdemServico.Criar(Guid.NewGuid(), Guid.NewGuid(), [Guid.NewGuid()], DateTime.UtcNow.AddHours(-1));

        switch (status)
        {
            case StatusOrdemServico.EmDiagnostico:
                ordemServico.AlterarParaEmDiagnostico();
                break;
            case StatusOrdemServico.AguardandoAprovacao:
                ordemServico.RegistrarOrcamentoGerado(100m);
                break;
            case StatusOrdemServico.EmExecucao:
                ordemServico.RegistrarOrcamentoGerado(100m);
                ordemServico.RegistrarPagamentoAprovado();
                break;
            case StatusOrdemServico.Finalizada:
                ordemServico.RegistrarOrcamentoGerado(100m);
                ordemServico.RegistrarPagamentoAprovado();
                ordemServico.AlterarParaFinalizada();
                break;
            case StatusOrdemServico.Entregue:
                ordemServico.AlterarParaEmDiagnostico();
                ordemServico.RegistrarOrcamentoGerado(100m);
                ordemServico.RegistrarPagamentoAprovado();
                ordemServico.AlterarParaFinalizada();
                ordemServico.AlterarParaEntregue();
                break;
            case StatusOrdemServico.Cancelada:
                ordemServico.Cancelar("Cliente inexistente.");
                break;
        }

        return ordemServico;
    }

    private void ConfigurarOrdemExistente(OrdemServico ordemServico) =>
        _ordemServicoGatewayMock.Setup(g => g.ObterPorIdAsync(ordemServico.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ordemServico);

    private void VerificarStatusAlteradoPublicado(OrdemServico ordemServico, StatusOrdemServico statusAnterior, StatusOrdemServico statusNovo)
    {
        _eventosPublisherMock.Verify(p => p.PublicarAsync(
            It.Is<OrdemServicoStatusAlterado>(evento =>
                evento.OrdemServicoId == ordemServico.Id &&
                evento.ClienteId == ordemServico.ClienteId &&
                evento.StatusAnterior == (int)statusAnterior &&
                evento.StatusNovo == (int)statusNovo),
            ordemServico.Id,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private void VerificarIgnoradoSemGravarNemPublicar()
    {
        _ordemServicoGatewayMock.Verify(g => g.AtualizarAsync(It.IsAny<OrdemServico>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Empty(_eventosPublisherMock.Invocations);
    }

    [Theory]
    [InlineData(StatusOrdemServico.Recebida)]
    [InlineData(StatusOrdemServico.EmDiagnostico)]
    [InlineData(StatusOrdemServico.AguardandoAprovacao)]
    [InlineData(StatusOrdemServico.EmExecucao)]
    public async Task ProcessarRejeitadaAsync_DeveCancelarGravarEPublicarStatusAlterado(StatusOrdemServico statusAtual)
    {
        var useCases = CriarUseCases();
        var ordemServico = CriarOrdemNoStatus(statusAtual);
        ConfigurarOrdemExistente(ordemServico);

        var resultado = await useCases.ProcessarRejeitadaAsync(new OrdemServicoRejeitada(ordemServico.Id, "Cliente nao encontrado."));

        Assert.Equal(ResultadoProcessamentoEvento.Processado, resultado);
        Assert.Equal(StatusOrdemServico.Cancelada, ordemServico.Status);
        Assert.Equal("Cliente nao encontrado.", ordemServico.MotivoCancelamento);
        _ordemServicoGatewayMock.Verify(g => g.AtualizarAsync(ordemServico, It.IsAny<CancellationToken>()), Times.Once);
        VerificarStatusAlteradoPublicado(ordemServico, statusAtual, StatusOrdemServico.Cancelada);
    }

    [Fact]
    public async Task ProcessarRejeitadaAsync_DeveIgnorar_QuandoOrdemNaoExiste()
    {
        var useCases = CriarUseCases();

        var resultado = await useCases.ProcessarRejeitadaAsync(new OrdemServicoRejeitada(Guid.NewGuid(), "Cliente nao encontrado."));

        Assert.Equal(ResultadoProcessamentoEvento.Ignorado, resultado);
        VerificarIgnoradoSemGravarNemPublicar();
    }

    [Theory]
    [InlineData(StatusOrdemServico.Cancelada)]
    [InlineData(StatusOrdemServico.Entregue)]
    public async Task ProcessarRejeitadaAsync_DeveIgnorar_QuandoOrdemJaEncerrada(StatusOrdemServico statusAtual)
    {
        var useCases = CriarUseCases();
        var ordemServico = CriarOrdemNoStatus(statusAtual);
        ConfigurarOrdemExistente(ordemServico);

        var resultado = await useCases.ProcessarRejeitadaAsync(new OrdemServicoRejeitada(ordemServico.Id, "Veiculo nao encontrado."));

        Assert.Equal(ResultadoProcessamentoEvento.Ignorado, resultado);
        Assert.Equal(statusAtual, ordemServico.Status);
        VerificarIgnoradoSemGravarNemPublicar();
    }

    [Theory]
    [InlineData(StatusOrdemServico.Recebida)]
    [InlineData(StatusOrdemServico.EmDiagnostico)]
    public async Task ProcessarOrcamentoGeradoAsync_DeveAlterarParaAguardandoAprovacaoComValor(StatusOrdemServico statusAtual)
    {
        var useCases = CriarUseCases();
        var ordemServico = CriarOrdemNoStatus(statusAtual);
        ConfigurarOrdemExistente(ordemServico);

        var resultado = await useCases.ProcessarOrcamentoGeradoAsync(new OrcamentoGerado(ordemServico.Id, 389.90m));

        Assert.Equal(ResultadoProcessamentoEvento.Processado, resultado);
        Assert.Equal(StatusOrdemServico.AguardandoAprovacao, ordemServico.Status);
        Assert.Equal(389.90m, ordemServico.ValorOrcamento);
        _ordemServicoGatewayMock.Verify(g => g.AtualizarAsync(ordemServico, It.IsAny<CancellationToken>()), Times.Once);
        VerificarStatusAlteradoPublicado(ordemServico, statusAtual, StatusOrdemServico.AguardandoAprovacao);
    }

    [Fact]
    public async Task ProcessarOrcamentoGeradoAsync_QuandoOrcamentoRegerado_DeveGravarValorSemPublicarStatus()
    {
        var useCases = CriarUseCases();
        var ordemServico = CriarOrdemNoStatus(StatusOrdemServico.AguardandoAprovacao);
        ConfigurarOrdemExistente(ordemServico);

        var resultado = await useCases.ProcessarOrcamentoGeradoAsync(new OrcamentoGerado(ordemServico.Id, 150m));

        Assert.Equal(ResultadoProcessamentoEvento.Processado, resultado);
        Assert.Equal(150m, ordemServico.ValorOrcamento);
        _ordemServicoGatewayMock.Verify(g => g.AtualizarAsync(ordemServico, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Empty(_eventosPublisherMock.Invocations);
    }

    [Theory]
    [InlineData(StatusOrdemServico.EmExecucao)]
    [InlineData(StatusOrdemServico.Cancelada)]
    public async Task ProcessarOrcamentoGeradoAsync_DeveIgnorar_QuandoStatusNaoPermite(StatusOrdemServico statusAtual)
    {
        var useCases = CriarUseCases();
        var ordemServico = CriarOrdemNoStatus(statusAtual);
        ConfigurarOrdemExistente(ordemServico);

        var resultado = await useCases.ProcessarOrcamentoGeradoAsync(new OrcamentoGerado(ordemServico.Id, 150m));

        Assert.Equal(ResultadoProcessamentoEvento.Ignorado, resultado);
        Assert.Equal(statusAtual, ordemServico.Status);
        VerificarIgnoradoSemGravarNemPublicar();
    }

    private static PagamentoAprovado CriarPagamentoAprovado(Guid ordemServicoId) => new(ordemServicoId, Guid.NewGuid(), 389.90m);

    [Fact]
    public async Task ProcessarPagamentoAprovadoAsync_DeveAlterarParaEmExecucaoGravarEPublicar()
    {
        var useCases = CriarUseCases();
        var ordemServico = CriarOrdemNoStatus(StatusOrdemServico.AguardandoAprovacao);
        ConfigurarOrdemExistente(ordemServico);

        var resultado = await useCases.ProcessarPagamentoAprovadoAsync(CriarPagamentoAprovado(ordemServico.Id));

        Assert.Equal(ResultadoProcessamentoEvento.Processado, resultado);
        Assert.Equal(StatusOrdemServico.EmExecucao, ordemServico.Status);
        _ordemServicoGatewayMock.Verify(g => g.AtualizarAsync(ordemServico, It.IsAny<CancellationToken>()), Times.Once);
        VerificarStatusAlteradoPublicado(ordemServico, StatusOrdemServico.AguardandoAprovacao, StatusOrdemServico.EmExecucao);
    }

    [Fact]
    public async Task ProcessarPagamentoAprovadoAsync_DeveIgnorar_QuandoEventoRepetido()
    {
        var useCases = CriarUseCases();
        var ordemServico = CriarOrdemNoStatus(StatusOrdemServico.EmExecucao);
        ConfigurarOrdemExistente(ordemServico);
        var quantidadeHistorico = ordemServico.HistoricoStatus.Count;

        var resultado = await useCases.ProcessarPagamentoAprovadoAsync(CriarPagamentoAprovado(ordemServico.Id));

        Assert.Equal(ResultadoProcessamentoEvento.Ignorado, resultado);
        Assert.Equal(quantidadeHistorico, ordemServico.HistoricoStatus.Count);
        VerificarIgnoradoSemGravarNemPublicar();
    }

    [Theory]
    [InlineData(StatusOrdemServico.Recebida)]
    [InlineData(StatusOrdemServico.EmDiagnostico)]
    [InlineData(StatusOrdemServico.Cancelada)]
    public async Task ProcessarPagamentoAprovadoAsync_DeveIgnorar_QuandoNaoEstiverAguardandoAprovacao(StatusOrdemServico statusAtual)
    {
        var useCases = CriarUseCases();
        var ordemServico = CriarOrdemNoStatus(statusAtual);
        ConfigurarOrdemExistente(ordemServico);

        var resultado = await useCases.ProcessarPagamentoAprovadoAsync(CriarPagamentoAprovado(ordemServico.Id));

        Assert.Equal(ResultadoProcessamentoEvento.Ignorado, resultado);
        Assert.Equal(statusAtual, ordemServico.Status);
        VerificarIgnoradoSemGravarNemPublicar();
    }

    [Fact]
    public async Task ProcessarPagamentoAprovadoAsync_DeveIgnorar_QuandoOrdemNaoExiste()
    {
        var useCases = CriarUseCases();

        var resultado = await useCases.ProcessarPagamentoAprovadoAsync(CriarPagamentoAprovado(Guid.NewGuid()));

        Assert.Equal(ResultadoProcessamentoEvento.Ignorado, resultado);
        VerificarIgnoradoSemGravarNemPublicar();
    }

    [Fact]
    public async Task ProcessarPagamentoAprovadoAsync_DeveRegistrarStatusAlteradoNaEtapaOrcamentoAprovacao()
    {
        var loggerMock = new Mock<ILogger<OrdemServicoEventosUseCases>>();
        var useCases = CriarUseCases(loggerMock.Object);
        var ordemServico = CriarOrdemNoStatus(StatusOrdemServico.AguardandoAprovacao);
        ConfigurarOrdemExistente(ordemServico);

        await useCases.ProcessarPagamentoAprovadoAsync(CriarPagamentoAprovado(ordemServico.Id));

        var invocacao = Assert.Single(loggerMock.Invocations, chamada => chamada.Method.Name == nameof(ILogger.Log) && ((EventId)chamada.Arguments[1]).Id == OrdemServicoLogEventos.OsStatusAlterado.Id);
        var propriedades = ((IEnumerable<KeyValuePair<string, object?>>)invocacao.Arguments[2]).ToDictionary(propriedade => propriedade.Key, propriedade => propriedade.Value);
        Assert.Equal("aguardando_aprovacao", propriedades["StatusAnterior"]);
        Assert.Equal("em_execucao", propriedades["StatusNovo"]);
        Assert.Equal("orcamento_aprovacao", propriedades["Etapa"]);
    }

    [Fact]
    public async Task ProcessarOrcamentoRecusadoAsync_DeveAlterarParaFinalizadaEPublicar()
    {
        var useCases = CriarUseCases();
        var ordemServico = CriarOrdemNoStatus(StatusOrdemServico.AguardandoAprovacao);
        ConfigurarOrdemExistente(ordemServico);

        var resultado = await useCases.ProcessarOrcamentoRecusadoAsync(new OrcamentoRecusado(ordemServico.Id));

        Assert.Equal(ResultadoProcessamentoEvento.Processado, resultado);
        Assert.Equal(StatusOrdemServico.Finalizada, ordemServico.Status);
        VerificarStatusAlteradoPublicado(ordemServico, StatusOrdemServico.AguardandoAprovacao, StatusOrdemServico.Finalizada);
    }

    [Fact]
    public async Task ProcessarOrcamentoRecusadoAsync_DeveIgnorar_QuandoNaoEstiverAguardandoAprovacao()
    {
        var useCases = CriarUseCases();
        var ordemServico = CriarOrdemNoStatus(StatusOrdemServico.Recebida);
        ConfigurarOrdemExistente(ordemServico);

        var resultado = await useCases.ProcessarOrcamentoRecusadoAsync(new OrcamentoRecusado(ordemServico.Id));

        Assert.Equal(ResultadoProcessamentoEvento.Ignorado, resultado);
        VerificarIgnoradoSemGravarNemPublicar();
    }

    [Fact]
    public async Task ProcessarDiagnosticoIniciadoAsync_DeveAlterarParaEmDiagnosticoGravarEPublicar()
    {
        var useCases = CriarUseCases();
        var ordemServico = CriarOrdemNoStatus(StatusOrdemServico.Recebida);
        ConfigurarOrdemExistente(ordemServico);

        var resultado = await useCases.ProcessarDiagnosticoIniciadoAsync(new DiagnosticoIniciado(ordemServico.Id));

        Assert.Equal(ResultadoProcessamentoEvento.Processado, resultado);
        Assert.Equal(StatusOrdemServico.EmDiagnostico, ordemServico.Status);
        _ordemServicoGatewayMock.Verify(g => g.AtualizarAsync(ordemServico, It.IsAny<CancellationToken>()), Times.Once);
        VerificarStatusAlteradoPublicado(ordemServico, StatusOrdemServico.Recebida, StatusOrdemServico.EmDiagnostico);
    }

    [Fact]
    public async Task ProcessarDiagnosticoIniciadoAsync_DeveIgnorar_QuandoEventoRepetido()
    {
        var useCases = CriarUseCases();
        var ordemServico = CriarOrdemNoStatus(StatusOrdemServico.EmDiagnostico);
        ConfigurarOrdemExistente(ordemServico);
        var quantidadeHistorico = ordemServico.HistoricoStatus.Count;

        var resultado = await useCases.ProcessarDiagnosticoIniciadoAsync(new DiagnosticoIniciado(ordemServico.Id));

        Assert.Equal(ResultadoProcessamentoEvento.Ignorado, resultado);
        Assert.Equal(quantidadeHistorico, ordemServico.HistoricoStatus.Count);
        VerificarIgnoradoSemGravarNemPublicar();
    }

    [Theory]
    [InlineData(StatusOrdemServico.AguardandoAprovacao)]
    [InlineData(StatusOrdemServico.EmExecucao)]
    [InlineData(StatusOrdemServico.Cancelada)]
    public async Task ProcessarDiagnosticoIniciadoAsync_DeveIgnorar_QuandoNaoEstiverRecebida(StatusOrdemServico statusAtual)
    {
        var useCases = CriarUseCases();
        var ordemServico = CriarOrdemNoStatus(statusAtual);
        ConfigurarOrdemExistente(ordemServico);

        var resultado = await useCases.ProcessarDiagnosticoIniciadoAsync(new DiagnosticoIniciado(ordemServico.Id));

        Assert.Equal(ResultadoProcessamentoEvento.Ignorado, resultado);
        Assert.Equal(statusAtual, ordemServico.Status);
        VerificarIgnoradoSemGravarNemPublicar();
    }

    [Fact]
    public async Task ProcessarDiagnosticoIniciadoAsync_DeveIgnorar_QuandoOrdemNaoExiste()
    {
        var useCases = CriarUseCases();

        var resultado = await useCases.ProcessarDiagnosticoIniciadoAsync(new DiagnosticoIniciado(Guid.NewGuid()));

        Assert.Equal(ResultadoProcessamentoEvento.Ignorado, resultado);
        VerificarIgnoradoSemGravarNemPublicar();
    }

    [Fact]
    public async Task ProcessarDiagnosticoIniciadoAsync_DeveRegistrarStatusAlteradoNaEtapaDiagnostico()
    {
        var loggerMock = new Mock<ILogger<OrdemServicoEventosUseCases>>();
        var useCases = CriarUseCases(loggerMock.Object);
        var ordemServico = CriarOrdemNoStatus(StatusOrdemServico.Recebida);
        ConfigurarOrdemExistente(ordemServico);

        await useCases.ProcessarDiagnosticoIniciadoAsync(new DiagnosticoIniciado(ordemServico.Id));

        var invocacao = Assert.Single(loggerMock.Invocations, chamada => chamada.Method.Name == nameof(ILogger.Log) && ((EventId)chamada.Arguments[1]).Id == OrdemServicoLogEventos.OsStatusAlterado.Id);
        var propriedades = ((IEnumerable<KeyValuePair<string, object?>>)invocacao.Arguments[2]).ToDictionary(propriedade => propriedade.Key, propriedade => propriedade.Value);
        Assert.Equal("recebida", propriedades["StatusAnterior"]);
        Assert.Equal("em_diagnostico", propriedades["StatusNovo"]);
        Assert.Equal("diagnostico", propriedades["Etapa"]);
    }

    [Fact]
    public async Task ProcessarExecucaoFinalizadaAsync_DeveAlterarParaFinalizadaGravarEPublicar()
    {
        var useCases = CriarUseCases();
        var ordemServico = CriarOrdemNoStatus(StatusOrdemServico.EmExecucao);
        ConfigurarOrdemExistente(ordemServico);

        var resultado = await useCases.ProcessarExecucaoFinalizadaAsync(new ExecucaoFinalizada(ordemServico.Id));

        Assert.Equal(ResultadoProcessamentoEvento.Processado, resultado);
        Assert.Equal(StatusOrdemServico.Finalizada, ordemServico.Status);
        _ordemServicoGatewayMock.Verify(g => g.AtualizarAsync(ordemServico, It.IsAny<CancellationToken>()), Times.Once);
        VerificarStatusAlteradoPublicado(ordemServico, StatusOrdemServico.EmExecucao, StatusOrdemServico.Finalizada);
    }

    [Fact]
    public async Task ProcessarExecucaoFinalizadaAsync_DeveIgnorar_QuandoEventoRepetido()
    {
        var useCases = CriarUseCases();
        var ordemServico = CriarOrdemNoStatus(StatusOrdemServico.Finalizada);
        ConfigurarOrdemExistente(ordemServico);
        var quantidadeHistorico = ordemServico.HistoricoStatus.Count;

        var resultado = await useCases.ProcessarExecucaoFinalizadaAsync(new ExecucaoFinalizada(ordemServico.Id));

        Assert.Equal(ResultadoProcessamentoEvento.Ignorado, resultado);
        Assert.Equal(quantidadeHistorico, ordemServico.HistoricoStatus.Count);
        VerificarIgnoradoSemGravarNemPublicar();
    }

    // Aguardando aprovacao: sem o pagamento aprovado no Billing a OS nao entrou em execucao, entao nao ha o que finalizar
    [Theory]
    [InlineData(StatusOrdemServico.Recebida)]
    [InlineData(StatusOrdemServico.EmDiagnostico)]
    [InlineData(StatusOrdemServico.AguardandoAprovacao)]
    [InlineData(StatusOrdemServico.Cancelada)]
    public async Task ProcessarExecucaoFinalizadaAsync_DeveIgnorar_QuandoNaoEstiverEmExecucao(StatusOrdemServico statusAtual)
    {
        var useCases = CriarUseCases();
        var ordemServico = CriarOrdemNoStatus(statusAtual);
        ConfigurarOrdemExistente(ordemServico);

        var resultado = await useCases.ProcessarExecucaoFinalizadaAsync(new ExecucaoFinalizada(ordemServico.Id));

        Assert.Equal(ResultadoProcessamentoEvento.Ignorado, resultado);
        Assert.Equal(statusAtual, ordemServico.Status);
        VerificarIgnoradoSemGravarNemPublicar();
    }

    [Fact]
    public async Task ProcessarExecucaoFinalizadaAsync_DeveIgnorar_QuandoOrdemNaoExiste()
    {
        var useCases = CriarUseCases();

        var resultado = await useCases.ProcessarExecucaoFinalizadaAsync(new ExecucaoFinalizada(Guid.NewGuid()));

        Assert.Equal(ResultadoProcessamentoEvento.Ignorado, resultado);
        VerificarIgnoradoSemGravarNemPublicar();
    }

    [Fact]
    public async Task ProcessarExecucaoFinalizadaAsync_DeveRegistrarStatusAlteradoNaEtapaFinalizacao()
    {
        var loggerMock = new Mock<ILogger<OrdemServicoEventosUseCases>>();
        var useCases = CriarUseCases(loggerMock.Object);
        var ordemServico = CriarOrdemNoStatus(StatusOrdemServico.EmExecucao);
        ConfigurarOrdemExistente(ordemServico);

        await useCases.ProcessarExecucaoFinalizadaAsync(new ExecucaoFinalizada(ordemServico.Id));

        var invocacao = Assert.Single(loggerMock.Invocations, chamada => chamada.Method.Name == nameof(ILogger.Log) && ((EventId)chamada.Arguments[1]).Id == OrdemServicoLogEventos.OsStatusAlterado.Id);
        var propriedades = ((IEnumerable<KeyValuePair<string, object?>>)invocacao.Arguments[2]).ToDictionary(propriedade => propriedade.Key, propriedade => propriedade.Value);
        Assert.Equal("em_execucao", propriedades["StatusAnterior"]);
        Assert.Equal("finalizada", propriedades["StatusNovo"]);
        Assert.Equal("finalizacao", propriedades["Etapa"]);
    }

    [Fact]
    public async Task ProcessarRejeitadaAsync_DeveRegistrarStatusAlteradoNaEtapaCancelamento()
    {
        var loggerMock = new Mock<ILogger<OrdemServicoEventosUseCases>>();
        var useCases = CriarUseCases(loggerMock.Object);
        var ordemServico = CriarOrdemNoStatus(StatusOrdemServico.Recebida);
        ConfigurarOrdemExistente(ordemServico);

        await useCases.ProcessarRejeitadaAsync(new OrdemServicoRejeitada(ordemServico.Id, "Servico nao encontrado."));

        var invocacao = Assert.Single(loggerMock.Invocations, chamada => chamada.Method.Name == nameof(ILogger.Log) && ((EventId)chamada.Arguments[1]).Id == OrdemServicoLogEventos.OsStatusAlterado.Id);
        var propriedades = ((IEnumerable<KeyValuePair<string, object?>>)invocacao.Arguments[2]).ToDictionary(propriedade => propriedade.Key, propriedade => propriedade.Value);
        Assert.Equal(LogLevel.Information, (LogLevel)invocacao.Arguments[0]);
        Assert.Equal(ordemServico.Id, propriedades["OrdemServicoId"]);
        Assert.Equal("recebida", propriedades["StatusAnterior"]);
        Assert.Equal("cancelada", propriedades["StatusNovo"]);
        Assert.Equal("cancelamento", propriedades["Etapa"]);
        Assert.InRange((double)propriedades["TempoNoStatusAnteriorSegundos"]!, 3500d, 3700d);
    }

    [Fact]
    public async Task Processar_DevePropagarFalhaDePublicacao_ParaAMensagemSerReprocessada()
    {
        var useCases = CriarUseCases();
        var ordemServico = CriarOrdemNoStatus(StatusOrdemServico.AguardandoAprovacao);
        ConfigurarOrdemExistente(ordemServico);

        _eventosPublisherMock
            .Setup(p => p.PublicarAsync(It.IsAny<OrdemServicoStatusAlterado>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SQS indisponivel"));

        var action = async () => await useCases.ProcessarPagamentoAprovadoAsync(CriarPagamentoAprovado(ordemServico.Id));

        await Assert.ThrowsAsync<InvalidOperationException>(action);
        _ordemServicoGatewayMock.Verify(g => g.AtualizarAsync(ordemServico, It.IsAny<CancellationToken>()), Times.Once);
    }
}
