using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TechChallenge.Oficina.OsService.Adapters.Features.OrdensServico;
using TechChallenge.Oficina.OsService.Controllers.Features.OrdensServico;
using TechChallenge.Oficina.OsService.UseCases.Features.OrdensServico.Commands;
using TechChallenge.Oficina.OsService.UseCases.Features.OrdensServico.Queries;
using TechChallenge.Oficina.OsService.UseCases.Features.OrdensServico.UseCases;
using TechChallenge.Oficina.OsService.UseCases.Features.OrdensServico.ViewModels;
using Xunit;

namespace TechChallenge.Oficina.OsService.API.Tests.Features.OrdensServico;

public sealed class OrdensServicoEndpointsTests
{
    private readonly Mock<IOrdemServicoUseCases> _ordemServicoUseCasesMock = new();

    private OrdensServicoController CriarController() =>
        new(_ordemServicoUseCasesMock.Object, new OrdensServicoAdapter(), NullLogger<OrdensServicoController>.Instance);

    [Fact]
    public async Task MapOrdensServicoEndpoints_Post_DeveInvocarUseCaseERetornar201()
    {
        var command = new CriarOrdemServicoCommand { ClienteId = Guid.NewGuid(), VeiculoId = Guid.NewGuid(), ServicoIds = [Guid.NewGuid()] };
        var ordem = new OrdemServicoViewModel { Id = Guid.NewGuid(), ClienteId = command.ClienteId };
        _ordemServicoUseCasesMock.Setup(s => s.CriarAsync(command, It.IsAny<CancellationToken>())).ReturnsAsync(ordem);

        var resultado = await CriarController().Post(command, CancellationToken.None);

        var created = Assert.IsType<CreatedAtRoute<OrdemServicoViewModel>>(resultado);
        Assert.Equal(ordem, created.Value);
        _ordemServicoUseCasesMock.Verify(s => s.CriarAsync(command, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MapOrdensServicoEndpoints_GetById_DeveInvocarUseCaseComIdCorreto()
    {
        var id = Guid.NewGuid();
        _ordemServicoUseCasesMock.Setup(s => s.ObterPorIdAsync(It.Is<ObterOrdemServicoPorIdQuery>(q => q.Id == id), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrdemServicoViewModel { Id = id });

        var resultado = await CriarController().GetById(id, CancellationToken.None);

        Assert.IsType<Ok<OrdemServicoViewModel>>(resultado);
        _ordemServicoUseCasesMock.Verify(s => s.ObterPorIdAsync(It.Is<ObterOrdemServicoPorIdQuery>(q => q.Id == id), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MapOrdensServicoEndpoints_Get_DeveInvocarUseCase()
    {
        _ordemServicoUseCasesMock.Setup(s => s.ListarAsync(It.IsAny<ListarOrdensServicoQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var resultado = await CriarController().Get(CancellationToken.None);

        Assert.IsType<Ok<IReadOnlyCollection<OrdemServicoViewModel>>>(resultado);
    }

    [Fact]
    public async Task MapOrdensServicoEndpoints_GetOrdenadas_DeveInvocarUseCase()
    {
        _ordemServicoUseCasesMock.Setup(s => s.ListarOrdenadasAsync(It.IsAny<ListarOrdensServicoOrdenadasQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var resultado = await CriarController().GetOrdenadas(CancellationToken.None);

        Assert.IsType<Ok<IReadOnlyCollection<OrdemServicoOrdenadasViewModel>>>(resultado);
    }

    [Fact]
    public async Task MapOrdensServicoEndpoints_GetAcompanhamento_DeveRetornar404_QuandoOrdemNaoExiste()
    {
        _ordemServicoUseCasesMock.Setup(s => s.ObterAcompanhamentoAsync(It.IsAny<ObterAcompanhamentoOrdemServicoPorIdQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("Ordem de servico nao encontrada."));

        var resultado = await CriarController().GetAcompanhamento(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFound<Dictionary<string, string?>>>(resultado);
    }

    [Fact]
    public async Task MapOrdensServicoEndpoints_GetByCliente_DeveInvocarUseCaseComClienteId()
    {
        var clienteId = Guid.NewGuid();
        _ordemServicoUseCasesMock.Setup(s => s.ListarPorClienteAsync(It.Is<ListarOrdensServicoPorClienteQuery>(q => q.ClienteId == clienteId), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var resultado = await CriarController().GetByCliente(clienteId, CancellationToken.None);

        Assert.IsType<Ok<IReadOnlyCollection<AcompanhamentoOrdemServicoViewModel>>>(resultado);
    }

    [Fact]
    public async Task MapOrdensServicoEndpoints_GetHistoricoSaga_DeveRetornarOkComOsEventos()
    {
        var id = Guid.NewGuid();
        IReadOnlyCollection<EventoHistoricoSagaViewModel> historico =
        [
            new EventoHistoricoSagaViewModel { Tipo = "DiagnosticoIniciado", Origem = "execution", Resultado = "Processado", StatusAnterior = 1, StatusNovo = 2 },
            new EventoHistoricoSagaViewModel { Tipo = "DiagnosticoIniciado", Origem = "execution", Resultado = "Ignorado", StatusAnterior = 2, StatusNovo = 2, Motivo = "Somente ordem de servico recebida pode ser alterada para em diagnostico." }
        ];
        _ordemServicoUseCasesMock.Setup(s => s.ListarHistoricoSagaAsync(It.Is<ListarHistoricoSagaQuery>(q => q.Id == id), It.IsAny<CancellationToken>())).ReturnsAsync(historico);

        var resultado = await CriarController().GetHistoricoSaga(id, CancellationToken.None);

        var ok = Assert.IsType<Ok<IReadOnlyCollection<EventoHistoricoSagaViewModel>>>(resultado);
        Assert.Equal(["Processado", "Ignorado"], ok.Value!.Select(evento => evento.Resultado));
    }

    [Fact]
    public async Task MapOrdensServicoEndpoints_GetHistoricoSaga_DeveRetornarOkComListaVazia_QuandoSemRegistros()
    {
        _ordemServicoUseCasesMock
            .Setup(s => s.ListarHistoricoSagaAsync(It.IsAny<ListarHistoricoSagaQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<EventoHistoricoSagaViewModel>());

        var resultado = await CriarController().GetHistoricoSaga(Guid.NewGuid(), CancellationToken.None);

        Assert.Empty(Assert.IsType<Ok<IReadOnlyCollection<EventoHistoricoSagaViewModel>>>(resultado).Value!);
    }

    [Fact]
    public async Task MapOrdensServicoEndpoints_Put_DeveRepassarComandoInteiro()
    {
        var command = new AtualizarOrdemServicoCommand { Id = Guid.NewGuid(), ClienteId = Guid.NewGuid(), VeiculoId = Guid.NewGuid(), ServicoIds = [Guid.NewGuid()] };
        _ordemServicoUseCasesMock.Setup(s => s.AtualizarAsync(command, It.IsAny<CancellationToken>())).ReturnsAsync(new OrdemServicoViewModel { Id = command.Id });

        var resultado = await CriarController().Put(command, CancellationToken.None);

        Assert.IsType<Ok<OrdemServicoViewModel>>(resultado);
        _ordemServicoUseCasesMock.Verify(s => s.AtualizarAsync(command, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MapOrdensServicoEndpoints_Delete_DeveRetornar204()
    {
        var id = Guid.NewGuid();

        var resultado = await CriarController().Delete(id, CancellationToken.None);

        Assert.IsType<NoContent>(resultado);
        _ordemServicoUseCasesMock.Verify(s => s.ExcluirAsync(It.Is<ExcluirOrdemServicoCommand>(c => c.Id == id), It.IsAny<CancellationToken>()), Times.Once);
    }

    // Diagnostico e finalizacao chegam por evento da Execucao e Producao: pelo HTTP so resta a entrega
    [Fact]
    public async Task MapOrdensServicoEndpoints_Entregar_DeveInvocarUseCaseComIdCorreto()
    {
        var id = Guid.NewGuid();
        _ordemServicoUseCasesMock.Setup(s => s.AlterarStatusParaEntregueAsync(It.IsAny<AlterarStatusOrdemServicoCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(new OrdemServicoViewModel { Id = id, Status = 6 });

        var resultado = await CriarController().AlterarParaEntregue(id, CancellationToken.None);

        Assert.Equal(6, Assert.IsType<Ok<OrdemServicoViewModel>>(resultado).Value!.Status);
        _ordemServicoUseCasesMock.Verify(s => s.AlterarStatusParaEntregueAsync(It.Is<AlterarStatusOrdemServicoCommand>(c => c.Id == id), It.IsAny<CancellationToken>()), Times.Once);
    }
}
