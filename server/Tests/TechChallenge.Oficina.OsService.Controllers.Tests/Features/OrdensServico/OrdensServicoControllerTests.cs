using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TechChallenge.Oficina.OsService.Controllers.Features.OrdensServico;
using TechChallenge.Oficina.OsService.Entities.Exceptions;
using TechChallenge.Oficina.OsService.UseCases.Features.OrdensServico.Commands;
using TechChallenge.Oficina.OsService.UseCases.Features.OrdensServico.Logging;
using TechChallenge.Oficina.OsService.UseCases.Features.OrdensServico.Queries;
using TechChallenge.Oficina.OsService.UseCases.Features.OrdensServico.UseCases;
using TechChallenge.Oficina.OsService.UseCases.Features.OrdensServico.ViewModels;
using Xunit;

namespace TechChallenge.Oficina.OsService.Controllers.Tests.Features.OrdensServico;

public sealed class OrdensServicoControllerTests
{
    private readonly Mock<IOrdemServicoUseCases> _useCasesMock = new();
    private readonly Mock<IOrdensServicoAdapter> _adapterMock = new();
    private readonly object _resultadoAdaptado = new();

    private OrdensServicoController CriarController(ILogger<OrdensServicoController>? logger = null)
    {
        return new OrdensServicoController(
            _useCasesMock.Object,
            _adapterMock.Object,
            logger ?? NullLogger<OrdensServicoController>.Instance);
    }

    private static (LogLevel Nivel, IReadOnlyDictionary<string, object?> Propriedades) ObterRegistroDeFalha(Mock<ILogger<OrdensServicoController>> loggerMock)
    {
        var invocacao = Assert.Single(loggerMock.Invocations, chamada => chamada.Method.Name == nameof(ILogger.Log) && ((EventId)chamada.Arguments[1]).Id == OrdemServicoLogEventos.OsFalha.Id);
        var propriedades = ((IEnumerable<KeyValuePair<string, object?>>)invocacao.Arguments[2]).ToDictionary(propriedade => propriedade.Key, propriedade => propriedade.Value);

        return ((LogLevel)invocacao.Arguments[0], propriedades);
    }

    private void ConfigurarAdapterParaOrdem() =>
        _adapterMock.Setup(adapter => adapter.Adapt(It.IsAny<OrdensServicoResult<OrdemServicoViewModel, Exception>>(), It.IsAny<bool>())).Returns(_resultadoAdaptado);

    [Fact]
    public async Task Post_DeveRetornarAdaptadoComoCriado_QuandoSucesso()
    {
        var controller = CriarController();
        var command = new CriarOrdemServicoCommand { ClienteId = Guid.NewGuid(), VeiculoId = Guid.NewGuid(), ServicoIds = [Guid.NewGuid()] };
        var ordem = new OrdemServicoViewModel { Id = Guid.NewGuid() };
        _useCasesMock.Setup(useCases => useCases.CriarAsync(command, It.IsAny<CancellationToken>())).ReturnsAsync(ordem);
        ConfigurarAdapterParaOrdem();

        var resultado = await controller.Post(command, CancellationToken.None);

        Assert.Same(_resultadoAdaptado, resultado);
        _adapterMock.Verify(adapter => adapter.Adapt(It.Is<OrdensServicoResult<OrdemServicoViewModel, Exception>>(r => r.Value == ordem), true), Times.Once);
    }

    [Fact]
    public async Task Post_DeveAdaptarErroERegistrarFalha_QuandoDomainException()
    {
        var loggerMock = new Mock<ILogger<OrdensServicoController>>();
        var controller = CriarController(loggerMock.Object);
        var command = new CriarOrdemServicoCommand();
        _useCasesMock.Setup(useCases => useCases.CriarAsync(command, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DomainException("A ordem de servico deve possuir ao menos um servico."));
        ConfigurarAdapterParaOrdem();

        var resultado = await controller.Post(command, CancellationToken.None);

        Assert.Same(_resultadoAdaptado, resultado);
        _adapterMock.Verify(adapter => adapter.Adapt(It.Is<OrdensServicoResult<OrdemServicoViewModel, Exception>>(r => r.Error is DomainException), false), Times.Once);
        var (nivel, propriedades) = ObterRegistroDeFalha(loggerMock);
        Assert.Equal(LogLevel.Warning, nivel);
        Assert.Equal("abertura", propriedades["Etapa"]);
        Assert.Null(propriedades["OrdemServicoId"]);
        Assert.Equal("regra_negocio", propriedades["TipoFalha"]);
        Assert.Equal("A ordem de servico deve possuir ao menos um servico.", propriedades["Motivo"]);
    }

    [Fact]
    public async Task GetById_DeveRepassarIdCorretamente()
    {
        var controller = CriarController();
        var id = Guid.NewGuid();
        _useCasesMock.Setup(useCases => useCases.ObterPorIdAsync(It.Is<ObterOrdemServicoPorIdQuery>(q => q.Id == id), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrdemServicoViewModel { Id = id });
        ConfigurarAdapterParaOrdem();

        var resultado = await controller.GetById(id, CancellationToken.None);

        Assert.Same(_resultadoAdaptado, resultado);
        _useCasesMock.Verify(useCases => useCases.ObterPorIdAsync(It.Is<ObterOrdemServicoPorIdQuery>(q => q.Id == id), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetById_DeveAdaptarErro_QuandoOrdemNaoExiste()
    {
        var controller = CriarController();
        _useCasesMock.Setup(useCases => useCases.ObterPorIdAsync(It.IsAny<ObterOrdemServicoPorIdQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("Ordem de servico nao encontrada."));
        ConfigurarAdapterParaOrdem();

        await controller.GetById(Guid.NewGuid(), CancellationToken.None);

        _adapterMock.Verify(adapter => adapter.Adapt(It.Is<OrdensServicoResult<OrdemServicoViewModel, Exception>>(r => r.Error is KeyNotFoundException), false), Times.Once);
    }

    [Fact]
    public async Task Get_DeveRepassarQuery()
    {
        var controller = CriarController();
        IReadOnlyCollection<OrdemServicoViewModel> ordens = [new OrdemServicoViewModel { Id = Guid.NewGuid() }];
        _useCasesMock.Setup(useCases => useCases.ListarAsync(It.IsAny<ListarOrdensServicoQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync(ordens);
        _adapterMock.Setup(adapter => adapter.Adapt(It.IsAny<OrdensServicoResult<IReadOnlyCollection<OrdemServicoViewModel>, Exception>>())).Returns(_resultadoAdaptado);

        var resultado = await controller.Get(CancellationToken.None);

        Assert.Same(_resultadoAdaptado, resultado);
        _useCasesMock.Verify(useCases => useCases.ListarAsync(It.IsAny<ListarOrdensServicoQuery>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetOrdenadas_DeveRepassarQuery()
    {
        var controller = CriarController();
        IReadOnlyCollection<OrdemServicoOrdenadasViewModel> ordens = [new OrdemServicoOrdenadasViewModel { Id = Guid.NewGuid() }];
        _useCasesMock.Setup(useCases => useCases.ListarOrdenadasAsync(It.IsAny<ListarOrdensServicoOrdenadasQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync(ordens);
        _adapterMock.Setup(adapter => adapter.Adapt(It.IsAny<OrdensServicoResult<IReadOnlyCollection<OrdemServicoOrdenadasViewModel>, Exception>>())).Returns(_resultadoAdaptado);

        var resultado = await controller.GetOrdenadas(CancellationToken.None);

        Assert.Same(_resultadoAdaptado, resultado);
    }

    [Fact]
    public async Task GetAcompanhamento_DeveRepassarIdCorretamente()
    {
        var controller = CriarController();
        var id = Guid.NewGuid();
        _useCasesMock.Setup(useCases => useCases.ObterAcompanhamentoAsync(It.Is<ObterAcompanhamentoOrdemServicoPorIdQuery>(q => q.Id == id), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AcompanhamentoOrdemServicoViewModel { Id = id });
        _adapterMock.Setup(adapter => adapter.Adapt(It.IsAny<OrdensServicoResult<AcompanhamentoOrdemServicoViewModel, Exception>>())).Returns(_resultadoAdaptado);

        var resultado = await controller.GetAcompanhamento(id, CancellationToken.None);

        Assert.Same(_resultadoAdaptado, resultado);
    }

    [Fact]
    public async Task GetAcompanhamento_DeveAdaptarErro_QuandoOrdemNaoExiste()
    {
        var controller = CriarController();
        _useCasesMock.Setup(useCases => useCases.ObterAcompanhamentoAsync(It.IsAny<ObterAcompanhamentoOrdemServicoPorIdQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("Ordem de servico nao encontrada."));
        _adapterMock.Setup(adapter => adapter.Adapt(It.IsAny<OrdensServicoResult<AcompanhamentoOrdemServicoViewModel, Exception>>())).Returns(_resultadoAdaptado);

        await controller.GetAcompanhamento(Guid.NewGuid(), CancellationToken.None);

        _adapterMock.Verify(adapter => adapter.Adapt(It.Is<OrdensServicoResult<AcompanhamentoOrdemServicoViewModel, Exception>>(r => r.Error is KeyNotFoundException)), Times.Once);
    }

    [Fact]
    public async Task GetByCliente_DeveRepassarClienteId()
    {
        var controller = CriarController();
        var clienteId = Guid.NewGuid();
        _useCasesMock.Setup(useCases => useCases.ListarPorClienteAsync(It.Is<ListarOrdensServicoPorClienteQuery>(q => q.ClienteId == clienteId), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _adapterMock.Setup(adapter => adapter.Adapt(It.IsAny<OrdensServicoResult<IReadOnlyCollection<AcompanhamentoOrdemServicoViewModel>, Exception>>())).Returns(_resultadoAdaptado);

        var resultado = await controller.GetByCliente(clienteId, CancellationToken.None);

        Assert.Same(_resultadoAdaptado, resultado);
        _useCasesMock.Verify(useCases => useCases.ListarPorClienteAsync(It.Is<ListarOrdensServicoPorClienteQuery>(q => q.ClienteId == clienteId), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetHistoricoSaga_DeveRepassarIdERetornarAdaptado()
    {
        var controller = CriarController();
        var id = Guid.NewGuid();
        using var cancelamento = new CancellationTokenSource();
        IReadOnlyCollection<EventoHistoricoSagaViewModel> historico = [new EventoHistoricoSagaViewModel { Tipo = "DiagnosticoIniciado", Resultado = "Processado" }];
        _useCasesMock.Setup(useCases => useCases.ListarHistoricoSagaAsync(It.Is<ListarHistoricoSagaQuery>(q => q.Id == id), cancelamento.Token))
            .ReturnsAsync(historico);
        _adapterMock
            .Setup(adapter => adapter.Adapt(It.Is<OrdensServicoResult<IReadOnlyCollection<EventoHistoricoSagaViewModel>, Exception>>(r => r.Value == historico)))
            .Returns(_resultadoAdaptado);

        var resultado = await controller.GetHistoricoSaga(id, cancelamento.Token);

        Assert.Same(_resultadoAdaptado, resultado);
    }

    [Fact]
    public async Task GetHistoricoSaga_DevePropagarFalhaDoDynamoDbSemRegistrarOsFalha()
    {
        // A falha ja sai como integracao.falha no gateway do historico; o tratamento global responde 500
        var loggerMock = new Mock<ILogger<OrdensServicoController>>();
        var controller = CriarController(loggerMock.Object);
        _useCasesMock.Setup(useCases => useCases.ListarHistoricoSagaAsync(It.IsAny<ListarHistoricoSagaQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DynamoDB indisponivel"));

        var action = async () => await controller.GetHistoricoSaga(Guid.NewGuid(), CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(action);
        Assert.Empty(loggerMock.Invocations);
        Assert.Empty(_adapterMock.Invocations);
    }

    [Fact]
    public async Task Put_DeveRepassarComandoInteiro()
    {
        var controller = CriarController();
        var command = new AtualizarOrdemServicoCommand { Id = Guid.NewGuid(), ClienteId = Guid.NewGuid(), VeiculoId = Guid.NewGuid(), ServicoIds = [Guid.NewGuid()] };
        _useCasesMock.Setup(useCases => useCases.AtualizarAsync(command, It.IsAny<CancellationToken>())).ReturnsAsync(new OrdemServicoViewModel { Id = command.Id });
        ConfigurarAdapterParaOrdem();

        var resultado = await controller.Put(command, CancellationToken.None);

        Assert.Same(_resultadoAdaptado, resultado);
        _useCasesMock.Verify(useCases => useCases.AtualizarAsync(command, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Put_DeveRegistrarFalhaDeRegraDeNegocio_QuandoOrdemNaoEstaRecebida()
    {
        var loggerMock = new Mock<ILogger<OrdensServicoController>>();
        var controller = CriarController(loggerMock.Object);
        var command = new AtualizarOrdemServicoCommand { Id = Guid.NewGuid() };
        _useCasesMock.Setup(useCases => useCases.AtualizarAsync(command, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DomainException("Somente ordem de servico recebida pode ser atualizada."));
        ConfigurarAdapterParaOrdem();

        await controller.Put(command, CancellationToken.None);

        var (nivel, propriedades) = ObterRegistroDeFalha(loggerMock);
        Assert.Equal(LogLevel.Warning, nivel);
        Assert.Equal("atualizacao", propriedades["Etapa"]);
        Assert.Equal(command.Id, propriedades["OrdemServicoId"]);
        Assert.Equal("regra_negocio", propriedades["TipoFalha"]);
    }

    [Fact]
    public async Task Put_DeveRegistrarFalhaDeNaoEncontrado_QuandoOrdemNaoExiste()
    {
        var loggerMock = new Mock<ILogger<OrdensServicoController>>();
        var controller = CriarController(loggerMock.Object);
        var command = new AtualizarOrdemServicoCommand { Id = Guid.NewGuid() };
        _useCasesMock.Setup(useCases => useCases.AtualizarAsync(command, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("Ordem de servico nao encontrada."));
        ConfigurarAdapterParaOrdem();

        await controller.Put(command, CancellationToken.None);

        var (_, propriedades) = ObterRegistroDeFalha(loggerMock);
        Assert.Equal("nao_encontrado", propriedades["TipoFalha"]);
        _adapterMock.Verify(adapter => adapter.Adapt(It.Is<OrdensServicoResult<OrdemServicoViewModel, Exception>>(r => r.Error is KeyNotFoundException), false), Times.Once);
    }

    [Fact]
    public async Task Delete_DeveRepassarIdERetornarVazio()
    {
        var controller = CriarController();
        var id = Guid.NewGuid();
        _adapterMock.Setup(adapter => adapter.Empty()).Returns(_resultadoAdaptado);

        var resultado = await controller.Delete(id, CancellationToken.None);

        Assert.Same(_resultadoAdaptado, resultado);
        _useCasesMock.Verify(useCases => useCases.ExcluirAsync(It.Is<ExcluirOrdemServicoCommand>(c => c.Id == id), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Delete_DeveRegistrarFalhaEAdaptarErro_QuandoOrdemNaoExiste()
    {
        var loggerMock = new Mock<ILogger<OrdensServicoController>>();
        var controller = CriarController(loggerMock.Object);
        var id = Guid.NewGuid();
        _useCasesMock.Setup(useCases => useCases.ExcluirAsync(It.IsAny<ExcluirOrdemServicoCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("Ordem de servico nao encontrada."));
        _adapterMock.Setup(adapter => adapter.Adapt(It.IsAny<OrdensServicoResult<bool, Exception>>())).Returns(_resultadoAdaptado);

        var resultado = await controller.Delete(id, CancellationToken.None);

        Assert.Same(_resultadoAdaptado, resultado);
        var (_, propriedades) = ObterRegistroDeFalha(loggerMock);
        Assert.Equal("exclusao", propriedades["Etapa"]);
        Assert.Equal(id, propriedades["OrdemServicoId"]);
        Assert.Equal("nao_encontrado", propriedades["TipoFalha"]);
    }

    [Fact]
    public async Task AlterarParaEntregue_DeveRepassarComandoComId()
    {
        var controller = CriarController();
        var id = Guid.NewGuid();
        _useCasesMock.Setup(useCases => useCases.AlterarStatusParaEntregueAsync(It.Is<AlterarStatusOrdemServicoCommand>(c => c.Id == id), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrdemServicoViewModel { Id = id, Status = 6 });
        ConfigurarAdapterParaOrdem();

        var resultado = await controller.AlterarParaEntregue(id, CancellationToken.None);

        Assert.Same(_resultadoAdaptado, resultado);
        _useCasesMock.Verify(useCases => useCases.AlterarStatusParaEntregueAsync(It.Is<AlterarStatusOrdemServicoCommand>(c => c.Id == id), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AlterarParaEntregue_DeveRegistrarFalhaDeRegraDeNegocio_QuandoTransicaoInvalida()
    {
        var loggerMock = new Mock<ILogger<OrdensServicoController>>();
        var controller = CriarController(loggerMock.Object);
        var id = Guid.NewGuid();
        _useCasesMock.Setup(useCases => useCases.AlterarStatusParaEntregueAsync(It.Is<AlterarStatusOrdemServicoCommand>(c => c.Id == id), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DomainException("Somente ordem de servico finalizada pode ser alterada para entregue."));
        ConfigurarAdapterParaOrdem();

        await controller.AlterarParaEntregue(id, CancellationToken.None);

        var (nivel, propriedades) = ObterRegistroDeFalha(loggerMock);
        Assert.Equal(LogLevel.Warning, nivel);
        Assert.Equal("entrega", propriedades["Etapa"]);
        Assert.Equal(id, propriedades["OrdemServicoId"]);
        Assert.Equal("regra_negocio", propriedades["TipoFalha"]);
        _adapterMock.Verify(adapter => adapter.Adapt(It.Is<OrdensServicoResult<OrdemServicoViewModel, Exception>>(r => r.Error is DomainException), false), Times.Once);
    }

    [Fact]
    public async Task AlterarParaEntregue_DeveRegistrarFalhaDeNaoEncontrado_QuandoOrdemNaoExiste()
    {
        var loggerMock = new Mock<ILogger<OrdensServicoController>>();
        var controller = CriarController(loggerMock.Object);
        var id = Guid.NewGuid();
        _useCasesMock.Setup(useCases => useCases.AlterarStatusParaEntregueAsync(It.Is<AlterarStatusOrdemServicoCommand>(c => c.Id == id), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("Ordem de servico nao encontrada."));
        ConfigurarAdapterParaOrdem();

        await controller.AlterarParaEntregue(id, CancellationToken.None);

        var (_, propriedades) = ObterRegistroDeFalha(loggerMock);
        Assert.Equal("entrega", propriedades["Etapa"]);
        Assert.Equal("nao_encontrado", propriedades["TipoFalha"]);
        Assert.Equal("Ordem de servico nao encontrada.", propriedades["Motivo"]);
    }

    [Fact]
    public async Task AlterarStatus_DevePropagarExcecaoNaoTratada_ParaOMiddleware()
    {
        var controller = CriarController();
        _useCasesMock.Setup(useCases => useCases.AlterarStatusParaEntregueAsync(It.IsAny<AlterarStatusOrdemServicoCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SQS indisponivel"));

        var action = async () => await controller.AlterarParaEntregue(Guid.NewGuid(), CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(action);
    }
}
