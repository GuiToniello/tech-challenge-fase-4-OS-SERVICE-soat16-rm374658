using AutoMapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TechChallenge.Oficina.OsService.Entities.Exceptions;
using TechChallenge.Oficina.OsService.Entities.Features.OrdensServico;
using TechChallenge.Oficina.OsService.Entities.Features.OrdensServico.Enums;
using TechChallenge.Oficina.OsService.UseCases.Features.Eventos;
using TechChallenge.Oficina.OsService.UseCases.Features.Eventos.Contratos;
using TechChallenge.Oficina.OsService.UseCases.Features.Eventos.Interfaces;
using TechChallenge.Oficina.OsService.UseCases.Features.Indicadores.UseCases;
using TechChallenge.Oficina.OsService.UseCases.Features.OrdensServico.Commands;
using TechChallenge.Oficina.OsService.UseCases.Features.OrdensServico.Logging;
using TechChallenge.Oficina.OsService.UseCases.Features.OrdensServico.Queries;
using TechChallenge.Oficina.OsService.UseCases.Features.OrdensServico.UseCases;
using TechChallenge.Oficina.OsService.UseCases.Features.OrdensServico.ViewModels;
using Xunit;

namespace TechChallenge.Oficina.OsService.UseCases.Tests.Features.OrdensServico.UseCases;

public sealed class OrdemServicoUseCasesTests
{
    private readonly Mock<IMapper> _mapperMock = new();
    private readonly Mock<IOrdemServicoGateway> _ordemServicoGatewayMock = new();
    private readonly Mock<IIndicadorUseCases> _indicadorUseCasesMock = new();
    private readonly Mock<IEventosPublisher> _eventosPublisherMock = new();
    private readonly Mock<IHistoricoSagaGateway> _historicoSagaGatewayMock = new();

    public OrdemServicoUseCasesTests()
    {
        _mapperMock.Setup(m => m.Map<OrdemServicoViewModel>(It.IsAny<object>())).Returns((object source) => MapearViewModel((OrdemServico)source));
    }

    private OrdemServicoUseCases CriarUseCases(ILogger<OrdemServicoUseCases>? logger = null)
    {
        return new OrdemServicoUseCases(
            _mapperMock.Object,
            _ordemServicoGatewayMock.Object,
            _indicadorUseCasesMock.Object,
            _eventosPublisherMock.Object,
            _historicoSagaGatewayMock.Object,
            logger ?? NullLogger<OrdemServicoUseCases>.Instance);
    }

    private static (LogLevel Nivel, IReadOnlyDictionary<string, object?> Propriedades) ObterRegistroDeLog(Mock<ILogger<OrdemServicoUseCases>> loggerMock, EventId evento)
    {
        var invocacao = Assert.Single(loggerMock.Invocations, chamada => chamada.Method.Name == nameof(ILogger.Log) && ((EventId)chamada.Arguments[1]).Id == evento.Id);
        var propriedades = ((IEnumerable<KeyValuePair<string, object?>>)invocacao.Arguments[2]).ToDictionary(propriedade => propriedade.Key, propriedade => propriedade.Value);

        return ((LogLevel)invocacao.Arguments[0], propriedades);
    }

    private static OrdemServico CriarOrdemServico(DateTime? dataCadastro = null) =>
        OrdemServico.Criar(Guid.NewGuid(), Guid.NewGuid(), [Guid.NewGuid()], dataCadastro);

    // Diagnostico e finalizacao vem dos eventos da Execucao e Producao; a execucao, do pagamento aprovado no Billing
    private static OrdemServico CriarOrdemServicoFinalizada(DateTime dataFinalizacao)
    {
        var ordemServico = CriarOrdemServico(dataFinalizacao.AddMinutes(-40));
        ordemServico.AlterarParaEmDiagnostico(dataFinalizacao.AddMinutes(-30));
        ordemServico.RegistrarOrcamentoGerado(100m, dataFinalizacao.AddMinutes(-20));
        ordemServico.RegistrarPagamentoAprovado(dataFinalizacao.AddMinutes(-10));
        ordemServico.AlterarParaFinalizada(dataFinalizacao);
        return ordemServico;
    }

    private void ConfigurarOrdemExistente(OrdemServico ordemServico) =>
        _ordemServicoGatewayMock.Setup(g => g.ObterPorIdAsync(ordemServico.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ordemServico);

    private static OrdemServicoViewModel MapearViewModel(OrdemServico ordemServico) =>
        new()
        {
            Id = ordemServico.Id,
            ClienteId = ordemServico.ClienteId,
            VeiculoId = ordemServico.VeiculoId,
            ServicoIds = ordemServico.ServicoIds.ToArray(),
            Status = (int)ordemServico.Status,
            StatusDescricao = ordemServico.Status.ObterDescricao(),
            ValorOrcamento = ordemServico.ValorOrcamento,
            MotivoCancelamento = ordemServico.MotivoCancelamento
        };

    private static AcompanhamentoOrdemServicoViewModel MapearAcompanhamento(OrdemServico ordemServico) =>
        new()
        {
            Id = ordemServico.Id,
            Status = (int)ordemServico.Status,
            StatusDescricao = ordemServico.Status.ObterDescricao(),
            HistoricoStatus = ordemServico.HistoricoStatus.Select(historico => new HistoricoStatusOrdemServicoViewModel { Status = historico.Status, DataAlteracao = historico.DataAlteracao }).ToArray()
        };

    private void ConfigurarMapeamentoDoHistoricoSaga() =>
        _mapperMock.Setup(m => m.Map<IReadOnlyCollection<EventoHistoricoSagaViewModel>>(It.IsAny<object>()))
            .Returns((object source) => ((IEnumerable<EventoHistoricoSaga>)source).Select(evento => new EventoHistoricoSagaViewModel
            {
                OcorridoEm = evento.OcorridoEm,
                Tipo = evento.Tipo,
                Origem = evento.Origem,
                Resultado = evento.Resultado.ToString(),
                StatusAnterior = evento.StatusAnterior,
                StatusNovo = evento.StatusNovo,
                Motivo = evento.Motivo
            }).ToArray());

    [Fact]
    public async Task CriarAsync_DeveGravarPublicarOrdemServicoAbertaERetornarViewModel()
    {
        var useCases = CriarUseCases();
        var servicoId = Guid.NewGuid();
        var command = new CriarOrdemServicoCommand { ClienteId = Guid.NewGuid(), VeiculoId = Guid.NewGuid(), ServicoIds = [servicoId, servicoId] };
        OrdemServico? adicionada = null;

        _ordemServicoGatewayMock
            .Setup(g => g.AdicionarAsync(It.IsAny<OrdemServico>(), It.IsAny<CancellationToken>()))
            .Callback<OrdemServico, CancellationToken>((ordemServico, _) => adicionada = ordemServico);

        var resultado = await useCases.CriarAsync(command);

        Assert.NotNull(adicionada);
        Assert.Equal(adicionada.Id, resultado.Id);
        Assert.Equal(command.ClienteId, resultado.ClienteId);
        Assert.Equal(command.VeiculoId, resultado.VeiculoId);
        Assert.Equal([servicoId], resultado.ServicoIds);
        Assert.Equal((int)StatusOrdemServico.Recebida, resultado.Status);
        _eventosPublisherMock.Verify(p => p.PublicarAsync(
            It.Is<OrdemServicoAberta>(evento =>
                evento.OrdemServicoId == adicionada.Id &&
                evento.ClienteId == command.ClienteId &&
                evento.VeiculoId == command.VeiculoId &&
                evento.ServicoIds.SequenceEqual(new[] { servicoId })),
            adicionada.Id,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CriarAsync_DevePublicarSomenteDepoisDeGravar()
    {
        var useCases = CriarUseCases();
        var passos = new List<string>();

        _ordemServicoGatewayMock
            .Setup(g => g.AdicionarAsync(It.IsAny<OrdemServico>(), It.IsAny<CancellationToken>()))
            .Callback(() => passos.Add("gravar"))
            .Returns(Task.CompletedTask);
        _eventosPublisherMock
            .Setup(p => p.PublicarAsync(It.IsAny<OrdemServicoAberta>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Callback(() => passos.Add("publicar"))
            .Returns(Task.CompletedTask);

        await useCases.CriarAsync(new CriarOrdemServicoCommand { ClienteId = Guid.NewGuid(), VeiculoId = Guid.NewGuid(), ServicoIds = [Guid.NewGuid()] });

        Assert.Equal(["gravar", "publicar"], passos);
    }

    [Fact]
    public async Task CriarAsync_DeveRegistrarOsCriada()
    {
        var loggerMock = new Mock<ILogger<OrdemServicoUseCases>>();
        var useCases = CriarUseCases(loggerMock.Object);
        var command = new CriarOrdemServicoCommand { ClienteId = Guid.NewGuid(), VeiculoId = Guid.NewGuid(), ServicoIds = [Guid.NewGuid(), Guid.NewGuid()] };

        var resultado = await useCases.CriarAsync(command);

        var (nivel, propriedades) = ObterRegistroDeLog(loggerMock, OrdemServicoLogEventos.OsCriada);
        Assert.Equal(LogLevel.Information, nivel);
        Assert.Equal(resultado.Id, propriedades["OrdemServicoId"]);
        Assert.Equal(command.ClienteId, propriedades["ClienteId"]);
        Assert.Equal(command.VeiculoId, propriedades["VeiculoId"]);
        Assert.Equal(2, propriedades["QuantidadeServicos"]);
    }

    [Fact]
    public async Task CriarAsync_DeveLancarDomainExceptionSemGravarNemPublicar_QuandoNaoInformarServicos()
    {
        var useCases = CriarUseCases();
        var command = new CriarOrdemServicoCommand { ClienteId = Guid.NewGuid(), VeiculoId = Guid.NewGuid(), ServicoIds = [] };

        var action = async () => await useCases.CriarAsync(command);

        var exception = await Assert.ThrowsAsync<DomainException>(action);
        Assert.Equal("A ordem de servico deve possuir ao menos um servico.", exception.Message);
        _ordemServicoGatewayMock.Verify(g => g.AdicionarAsync(It.IsAny<OrdemServico>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Empty(_eventosPublisherMock.Invocations);
    }

    [Fact]
    public async Task CriarAsync_DevePropagarFalhaDePublicacao_DepoisDeGravar()
    {
        var useCases = CriarUseCases();
        var falha = new InvalidOperationException("SQS indisponivel");

        _eventosPublisherMock
            .Setup(p => p.PublicarAsync(It.IsAny<OrdemServicoAberta>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(falha);

        var action = async () => await useCases.CriarAsync(new CriarOrdemServicoCommand { ClienteId = Guid.NewGuid(), VeiculoId = Guid.NewGuid(), ServicoIds = [Guid.NewGuid()] });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(action);
        Assert.Same(falha, exception);
        _ordemServicoGatewayMock.Verify(g => g.AdicionarAsync(It.IsAny<OrdemServico>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AtualizarAsync_DeveAtualizarEPublicarOrdemServicoAtualizada()
    {
        var useCases = CriarUseCases();
        var ordemServico = CriarOrdemServico();
        var novoServicoId = Guid.NewGuid();
        var command = new AtualizarOrdemServicoCommand { Id = ordemServico.Id, ClienteId = Guid.NewGuid(), VeiculoId = Guid.NewGuid(), ServicoIds = [novoServicoId] };
        ConfigurarOrdemExistente(ordemServico);

        var resultado = await useCases.AtualizarAsync(command);

        Assert.Equal(command.ClienteId, resultado.ClienteId);
        Assert.Equal(command.VeiculoId, resultado.VeiculoId);
        Assert.Equal([novoServicoId], resultado.ServicoIds);
        Assert.Equal((int)StatusOrdemServico.Recebida, resultado.Status);
        _ordemServicoGatewayMock.Verify(g => g.AtualizarAsync(ordemServico, It.IsAny<CancellationToken>()), Times.Once);
        _eventosPublisherMock.Verify(p => p.PublicarAsync(
            It.Is<OrdemServicoAtualizada>(evento =>
                evento.OrdemServicoId == ordemServico.Id &&
                evento.ClienteId == command.ClienteId &&
                evento.VeiculoId == command.VeiculoId &&
                evento.ServicoIds.SequenceEqual(new[] { novoServicoId })),
            ordemServico.Id,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AtualizarAsync_DeveLancarDomainExceptionSemGravarNemPublicar_QuandoOrdemNaoEstaRecebida()
    {
        var useCases = CriarUseCases();
        var ordemServico = CriarOrdemServico();
        ordemServico.AlterarParaEmDiagnostico();
        ConfigurarOrdemExistente(ordemServico);

        var action = async () => await useCases.AtualizarAsync(new AtualizarOrdemServicoCommand { Id = ordemServico.Id, ClienteId = Guid.NewGuid(), VeiculoId = Guid.NewGuid(), ServicoIds = [Guid.NewGuid()] });

        var exception = await Assert.ThrowsAsync<DomainException>(action);
        Assert.Equal("Somente ordem de servico recebida pode ser atualizada.", exception.Message);
        _ordemServicoGatewayMock.Verify(g => g.AtualizarAsync(It.IsAny<OrdemServico>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Empty(_eventosPublisherMock.Invocations);
    }

    [Fact]
    public async Task AtualizarAsync_DeveLancarKeyNotFoundException_QuandoOrdemNaoExiste()
    {
        var useCases = CriarUseCases();

        var action = async () => await useCases.AtualizarAsync(new AtualizarOrdemServicoCommand { Id = Guid.NewGuid(), ClienteId = Guid.NewGuid(), VeiculoId = Guid.NewGuid(), ServicoIds = [Guid.NewGuid()] });

        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(action);
        Assert.Equal("Ordem de servico nao encontrada.", exception.Message);
        Assert.Empty(_eventosPublisherMock.Invocations);
    }

    [Fact]
    public async Task ObterPorIdAsync_DeveRetornarViewModel_QuandoOrdemServicoExiste()
    {
        var useCases = CriarUseCases();
        var ordemServico = CriarOrdemServico();
        ConfigurarOrdemExistente(ordemServico);

        var resultado = await useCases.ObterPorIdAsync(new ObterOrdemServicoPorIdQuery { Id = ordemServico.Id });

        Assert.Equal(ordemServico.Id, resultado.Id);
    }

    [Fact]
    public async Task ObterPorIdAsync_DeveLancarKeyNotFoundException_QuandoOrdemServicoNaoExiste()
    {
        var useCases = CriarUseCases();

        var action = async () => await useCases.ObterPorIdAsync(new ObterOrdemServicoPorIdQuery { Id = Guid.NewGuid() });

        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(action);
        Assert.Equal("Ordem de servico nao encontrada.", exception.Message);
    }

    [Fact]
    public async Task ListarAsync_DeveRetornarOrdensDoGateway()
    {
        var useCases = CriarUseCases();
        var ordensServico = new[] { CriarOrdemServico(), CriarOrdemServico() };

        _ordemServicoGatewayMock.Setup(g => g.ListarAsync(It.IsAny<CancellationToken>())).ReturnsAsync(ordensServico);
        _mapperMock.Setup(m => m.Map<IReadOnlyCollection<OrdemServicoViewModel>>(It.IsAny<object>()))
            .Returns((object source) => ((IEnumerable<OrdemServico>)source).Select(MapearViewModel).ToArray());

        var resultado = await useCases.ListarAsync(new ListarOrdensServicoQuery());

        Assert.Equal(2, resultado.Count);
    }

    [Fact]
    public async Task ListarOrdenadasAsync_DeveRetornarOrdensNaOrdemDoGateway()
    {
        var useCases = CriarUseCases();
        var emExecucao = CriarOrdemServico();
        emExecucao.AlterarParaEmDiagnostico();
        emExecucao.RegistrarOrcamentoGerado(100m);
        emExecucao.RegistrarPagamentoAprovado();
        var recebida = CriarOrdemServico();

        _ordemServicoGatewayMock.Setup(g => g.ListarOrdenadasAsync(It.IsAny<CancellationToken>())).ReturnsAsync([emExecucao, recebida]);
        _mapperMock.Setup(m => m.Map<IReadOnlyCollection<OrdemServicoOrdenadasViewModel>>(It.IsAny<object>()))
            .Returns((object source) => ((IEnumerable<OrdemServico>)source).Select(ordem => new OrdemServicoOrdenadasViewModel { Id = ordem.Id, Status = (int)ordem.Status }).ToArray());

        var resultado = await useCases.ListarOrdenadasAsync(new ListarOrdensServicoOrdenadasQuery());

        Assert.Equal([emExecucao.Id, recebida.Id], resultado.Select(ordem => ordem.Id));
        _ordemServicoGatewayMock.Verify(g => g.ListarOrdenadasAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ObterAcompanhamentoAsync_DeveRetornarResumoDaOrdemServico()
    {
        var useCases = CriarUseCases();
        var ordemServico = CriarOrdemServico();
        ConfigurarOrdemExistente(ordemServico);
        _mapperMock.Setup(m => m.Map<AcompanhamentoOrdemServicoViewModel>(It.IsAny<object>())).Returns((object source) => MapearAcompanhamento((OrdemServico)source));

        var resultado = await useCases.ObterAcompanhamentoAsync(new ObterAcompanhamentoOrdemServicoPorIdQuery { Id = ordemServico.Id });

        Assert.Equal(ordemServico.Id, resultado.Id);
        Assert.Equal((int)StatusOrdemServico.Recebida, resultado.Status);
        Assert.Single(resultado.HistoricoStatus);
    }

    [Fact]
    public async Task ObterAcompanhamentoAsync_DeveLancarKeyNotFoundException_QuandoOrdemNaoExiste()
    {
        var useCases = CriarUseCases();

        var action = async () => await useCases.ObterAcompanhamentoAsync(new ObterAcompanhamentoOrdemServicoPorIdQuery { Id = Guid.NewGuid() });

        await Assert.ThrowsAsync<KeyNotFoundException>(action);
    }

    [Fact]
    public async Task ListarPorClienteAsync_DeveRetornarAcompanhamentosDoCliente()
    {
        var useCases = CriarUseCases();
        var clienteId = Guid.NewGuid();
        var ordensServico = new[] { OrdemServico.Criar(clienteId, Guid.NewGuid(), [Guid.NewGuid()]) };

        _ordemServicoGatewayMock.Setup(g => g.ListarPorClienteAsync(clienteId, It.IsAny<CancellationToken>())).ReturnsAsync(ordensServico);
        _mapperMock.Setup(m => m.Map<IReadOnlyCollection<AcompanhamentoOrdemServicoViewModel>>(It.IsAny<object>()))
            .Returns((object source) => ((IEnumerable<OrdemServico>)source).Select(MapearAcompanhamento).ToArray());

        var resultado = await useCases.ListarPorClienteAsync(new ListarOrdensServicoPorClienteQuery { ClienteId = clienteId });

        Assert.Single(resultado);
    }

    [Fact]
    public async Task ListarPorClienteAsync_DeveRetornarListaVazia_QuandoClienteNaoPossuiOrdens()
    {
        var useCases = CriarUseCases();
        var clienteId = Guid.NewGuid();

        _ordemServicoGatewayMock.Setup(g => g.ListarPorClienteAsync(clienteId, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _mapperMock.Setup(m => m.Map<IReadOnlyCollection<AcompanhamentoOrdemServicoViewModel>>(It.IsAny<object>()))
            .Returns((object source) => ((IEnumerable<OrdemServico>)source).Select(MapearAcompanhamento).ToArray());

        var resultado = await useCases.ListarPorClienteAsync(new ListarOrdensServicoPorClienteQuery { ClienteId = clienteId });

        Assert.Empty(resultado);
    }

    [Fact]
    public async Task ListarHistoricoSagaAsync_DeveRetornarOsEventosEmOrdemCronologicaSemConsultarAOrdem()
    {
        var useCases = CriarUseCases();
        var ordemServicoId = Guid.NewGuid();
        var inicio = new DateTime(2026, 11, 27, 8, 00, 00, DateTimeKind.Utc);
        var diagnostico = new EventoHistoricoSaga
        {
            OrdemServicoId = ordemServicoId,
            OcorridoEm = inicio,
            Tipo = nameof(DiagnosticoIniciado),
            Origem = OrigensHistoricoSaga.Execution,
            Resultado = ResultadoProcessamentoEvento.Processado,
            StatusAnterior = 1,
            StatusNovo = 2
        };
        var repetido = diagnostico with
        {
            OcorridoEm = inicio.AddMinutes(5),
            Resultado = ResultadoProcessamentoEvento.Ignorado,
            StatusAnterior = 2,
            Motivo = "Somente ordem de servico recebida pode ser alterada para em diagnostico."
        };
        using var cancelamento = new CancellationTokenSource();
        _historicoSagaGatewayMock.Setup(h => h.ListarAsync(ordemServicoId, cancelamento.Token)).ReturnsAsync([repetido, diagnostico]);
        ConfigurarMapeamentoDoHistoricoSaga();

        var historico = await useCases.ListarHistoricoSagaAsync(new ListarHistoricoSagaQuery { Id = ordemServicoId }, cancelamento.Token);

        Assert.Equal(["Processado", "Ignorado"], historico.Select(evento => evento.Resultado));
        Assert.Equal([inicio, inicio.AddMinutes(5)], historico.Select(evento => evento.OcorridoEm));
        Assert.Equal("execution", historico.First().Origem);
        Assert.Equal(1, historico.First().StatusAnterior);
        Assert.Equal(2, historico.First().StatusNovo);
        Assert.Equal(repetido.Motivo, historico.Last().Motivo);
        // O historico e so do DynamoDB: OS excluida ou desconhecida nao impede a consulta
        Assert.Empty(_ordemServicoGatewayMock.Invocations);
    }

    [Fact]
    public async Task ListarHistoricoSagaAsync_DeveRetornarListaVazia_QuandoNaoHaHistorico()
    {
        var useCases = CriarUseCases();
        var ordemServicoId = Guid.NewGuid();
        _historicoSagaGatewayMock.Setup(h => h.ListarAsync(ordemServicoId, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        ConfigurarMapeamentoDoHistoricoSaga();

        var historico = await useCases.ListarHistoricoSagaAsync(new ListarHistoricoSagaQuery { Id = ordemServicoId });

        Assert.Empty(historico);
        Assert.Empty(_ordemServicoGatewayMock.Invocations);
    }

    [Fact]
    public async Task ExcluirAsync_DeveRemoverEPublicarOrdemServicoExcluida()
    {
        var useCases = CriarUseCases();
        var ordemServico = CriarOrdemServico();
        ConfigurarOrdemExistente(ordemServico);

        await useCases.ExcluirAsync(new ExcluirOrdemServicoCommand { Id = ordemServico.Id });

        _ordemServicoGatewayMock.Verify(g => g.RemoverAsync(ordemServico, It.IsAny<CancellationToken>()), Times.Once);
        _eventosPublisherMock.Verify(p => p.PublicarAsync(
            It.Is<OrdemServicoExcluida>(evento => evento.OrdemServicoId == ordemServico.Id),
            ordemServico.Id,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExcluirAsync_DeveLancarKeyNotFoundExceptionSemPublicar_QuandoOrdemNaoExiste()
    {
        var useCases = CriarUseCases();

        var action = async () => await useCases.ExcluirAsync(new ExcluirOrdemServicoCommand { Id = Guid.NewGuid() });

        await Assert.ThrowsAsync<KeyNotFoundException>(action);
        _ordemServicoGatewayMock.Verify(g => g.RemoverAsync(It.IsAny<OrdemServico>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Empty(_eventosPublisherMock.Invocations);
    }

    [Fact]
    public async Task AlterarStatusParaEntregueAsync_DeveAlterarPublicarEAtualizarIndicadores()
    {
        var loggerMock = new Mock<ILogger<OrdemServicoUseCases>>();
        var useCases = CriarUseCases(loggerMock.Object);
        var ordemServico = CriarOrdemServicoFinalizada(DateTime.UtcNow);
        ConfigurarOrdemExistente(ordemServico);

        var resultado = await useCases.AlterarStatusParaEntregueAsync(new AlterarStatusOrdemServicoCommand { Id = ordemServico.Id });

        Assert.Equal((int)StatusOrdemServico.Entregue, resultado.Status);
        _ordemServicoGatewayMock.Verify(g => g.AtualizarAsync(ordemServico, It.IsAny<CancellationToken>()), Times.Once);
        _indicadorUseCasesMock.Verify(i => i.AtualizarAsync(It.IsAny<CancellationToken>()), Times.Once);
        _eventosPublisherMock.Verify(p => p.PublicarAsync(
            It.Is<OrdemServicoStatusAlterado>(evento => evento.StatusAnterior == 5 && evento.StatusNovo == 6),
            ordemServico.Id,
            It.IsAny<CancellationToken>()), Times.Once);
        var (nivel, propriedades) = ObterRegistroDeLog(loggerMock, OrdemServicoLogEventos.OsEncerrada);
        Assert.Equal(LogLevel.Information, nivel);
        Assert.Equal(ordemServico.Id, propriedades["OrdemServicoId"]);
    }

    [Fact]
    public async Task AlterarStatusParaEntregueAsync_DeveLancarDomainExceptionSemAtualizarIndicadores_QuandoTransicaoInvalida()
    {
        var useCases = CriarUseCases();
        var ordemServico = CriarOrdemServico();
        ConfigurarOrdemExistente(ordemServico);

        var action = async () => await useCases.AlterarStatusParaEntregueAsync(new AlterarStatusOrdemServicoCommand { Id = ordemServico.Id });

        var exception = await Assert.ThrowsAsync<DomainException>(action);
        Assert.Equal("Somente ordem de servico finalizada pode ser alterada para entregue.", exception.Message);
        _indicadorUseCasesMock.Verify(i => i.AtualizarAsync(It.IsAny<CancellationToken>()), Times.Never);
        Assert.Empty(_eventosPublisherMock.Invocations);
    }

    [Fact]
    public async Task AlterarStatusParaEntregueAsync_DeveLancarKeyNotFoundException_QuandoOrdemNaoExiste()
    {
        var useCases = CriarUseCases();

        var action = async () => await useCases.AlterarStatusParaEntregueAsync(new AlterarStatusOrdemServicoCommand { Id = Guid.NewGuid() });

        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(action);
        Assert.Equal("Ordem de servico nao encontrada.", exception.Message);
    }

    [Fact]
    public async Task AlterarStatusParaEntregueAsync_DeveRegistrarStatusAlteradoComTempoNoStatusAnterior()
    {
        var loggerMock = new Mock<ILogger<OrdemServicoUseCases>>();
        var useCases = CriarUseCases(loggerMock.Object);
        var ordemServico = CriarOrdemServicoFinalizada(DateTime.UtcNow.AddMinutes(-10));
        ConfigurarOrdemExistente(ordemServico);

        await useCases.AlterarStatusParaEntregueAsync(new AlterarStatusOrdemServicoCommand { Id = ordemServico.Id });

        var (nivel, propriedades) = ObterRegistroDeLog(loggerMock, OrdemServicoLogEventos.OsStatusAlterado);
        Assert.Equal(LogLevel.Information, nivel);
        Assert.Equal(ordemServico.Id, propriedades["OrdemServicoId"]);
        Assert.Equal("finalizada", propriedades["StatusAnterior"]);
        Assert.Equal("entregue", propriedades["StatusNovo"]);
        Assert.Equal("entrega", propriedades["Etapa"]);
        Assert.InRange((double)propriedades["TempoNoStatusAnteriorSegundos"]!, 600d, 660d);
    }

    [Fact]
    public async Task AlterarStatus_DevePropagarFalhaDePublicacao_DepoisDeGravar()
    {
        var useCases = CriarUseCases();
        var ordemServico = CriarOrdemServicoFinalizada(DateTime.UtcNow);
        ConfigurarOrdemExistente(ordemServico);

        _eventosPublisherMock
            .Setup(p => p.PublicarAsync(It.IsAny<OrdemServicoStatusAlterado>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SQS indisponivel"));

        var action = async () => await useCases.AlterarStatusParaEntregueAsync(new AlterarStatusOrdemServicoCommand { Id = ordemServico.Id });

        await Assert.ThrowsAsync<InvalidOperationException>(action);
        _ordemServicoGatewayMock.Verify(g => g.AtualizarAsync(ordemServico, It.IsAny<CancellationToken>()), Times.Once);
    }
}
