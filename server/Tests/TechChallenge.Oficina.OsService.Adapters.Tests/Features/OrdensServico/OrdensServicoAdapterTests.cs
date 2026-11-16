using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using TechChallenge.Oficina.OsService.Adapters.Features.OrdensServico;
using TechChallenge.Oficina.OsService.Controllers.Features.OrdensServico;
using TechChallenge.Oficina.OsService.Entities.Exceptions;
using TechChallenge.Oficina.OsService.UseCases.Features.OrdensServico.ViewModels;
using Xunit;

namespace TechChallenge.Oficina.OsService.Adapters.Tests.Features.OrdensServico;

public sealed class OrdensServicoAdapterTests
{
    private readonly OrdensServicoAdapter _adapter = new();

    [Fact]
    public void Adapt_DeveRetornarCreatedAtRoute_QuandoOrdemCriada()
    {
        var ordem = new OrdemServicoViewModel { Id = Guid.NewGuid() };
        var result = new OrdensServicoResult<OrdemServicoViewModel, Exception>(ordem);

        var adaptado = _adapter.Adapt(result, true);

        var createdAtRoute = Assert.IsType<CreatedAtRoute<OrdemServicoViewModel>>(adaptado);
        Assert.Equal(ordem, createdAtRoute.Value);
        Assert.Equal("GetOrdemServicoById", createdAtRoute.RouteName);
        Assert.Equal(ordem.Id, createdAtRoute.RouteValues["id"]);
    }

    [Fact]
    public void Adapt_DeveRetornarOk_QuandoOrdemSemCriacao()
    {
        var ordem = new OrdemServicoViewModel { Id = Guid.NewGuid() };

        var adaptado = _adapter.Adapt(new OrdensServicoResult<OrdemServicoViewModel, Exception>(ordem));

        var ok = Assert.IsType<Ok<OrdemServicoViewModel>>(adaptado);
        Assert.Equal(ordem, ok.Value);
    }

    [Fact]
    public void Adapt_DeveRetornarBadRequestComMensagem_QuandoDomainException()
    {
        var result = new OrdensServicoResult<OrdemServicoViewModel, Exception>(new DomainException("erro de regra"));

        var adaptado = _adapter.Adapt(result);

        var badRequest = Assert.IsType<BadRequest<Dictionary<string, string?>>>(adaptado);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
        Assert.Equal("erro de regra", badRequest.Value!["message"]);
    }

    [Fact]
    public void Adapt_DeveRetornarNotFoundComMensagem_QuandoKeyNotFoundException()
    {
        var result = new OrdensServicoResult<OrdemServicoViewModel, Exception>(new KeyNotFoundException("nao encontrado"));

        var adaptado = _adapter.Adapt(result);

        var notFound = Assert.IsType<NotFound<Dictionary<string, string?>>>(adaptado);
        Assert.Equal(StatusCodes.Status404NotFound, notFound.StatusCode);
        Assert.Equal("nao encontrado", notFound.Value!["message"]);
    }

    [Fact]
    public void Adapt_DeveRetornarProblem_QuandoOutraExcecao()
    {
        var result = new OrdensServicoResult<OrdemServicoViewModel, Exception>(new InvalidOperationException("falha"));

        var adaptado = _adapter.Adapt(result);

        var problem = Assert.IsType<ProblemHttpResult>(adaptado);
        Assert.Equal(StatusCodes.Status500InternalServerError, problem.StatusCode);
    }

    [Fact]
    public void Adapt_DeveRetornarOk_QuandoListaOrdemServicoSucesso()
    {
        IReadOnlyCollection<OrdemServicoViewModel> ordens = [new OrdemServicoViewModel { Id = Guid.NewGuid() }];

        var adaptado = _adapter.Adapt(new OrdensServicoResult<IReadOnlyCollection<OrdemServicoViewModel>, Exception>(ordens));

        var ok = Assert.IsType<Ok<IReadOnlyCollection<OrdemServicoViewModel>>>(adaptado);
        Assert.Equal(ordens, ok.Value);
    }

    [Fact]
    public void Adapt_DeveRetornarOk_QuandoListaOrdenadaSucesso()
    {
        IReadOnlyCollection<OrdemServicoOrdenadasViewModel> ordens = [new OrdemServicoOrdenadasViewModel { Id = Guid.NewGuid() }];

        var adaptado = _adapter.Adapt(new OrdensServicoResult<IReadOnlyCollection<OrdemServicoOrdenadasViewModel>, Exception>(ordens));

        var ok = Assert.IsType<Ok<IReadOnlyCollection<OrdemServicoOrdenadasViewModel>>>(adaptado);
        Assert.Equal(ordens, ok.Value);
    }

    [Fact]
    public void Adapt_DeveRetornarOk_QuandoAcompanhamentoSucesso()
    {
        var acompanhamento = new AcompanhamentoOrdemServicoViewModel { Id = Guid.NewGuid(), Status = 8, MotivoCancelamento = "Cliente inexistente." };

        var adaptado = _adapter.Adapt(new OrdensServicoResult<AcompanhamentoOrdemServicoViewModel, Exception>(acompanhamento));

        var ok = Assert.IsType<Ok<AcompanhamentoOrdemServicoViewModel>>(adaptado);
        Assert.Equal(acompanhamento, ok.Value);
    }

    [Fact]
    public void Adapt_DeveRetornarNotFound_QuandoAcompanhamentoNaoEncontrado()
    {
        var adaptado = _adapter.Adapt(new OrdensServicoResult<AcompanhamentoOrdemServicoViewModel, Exception>(new KeyNotFoundException("nao encontrado")));

        Assert.IsType<NotFound<Dictionary<string, string?>>>(adaptado);
    }

    [Fact]
    public void Adapt_DeveRetornarOk_QuandoListaAcompanhamentoSucesso()
    {
        IReadOnlyCollection<AcompanhamentoOrdemServicoViewModel> acompanhamentos = [new AcompanhamentoOrdemServicoViewModel { Id = Guid.NewGuid(), Status = 1 }];

        var adaptado = _adapter.Adapt(new OrdensServicoResult<IReadOnlyCollection<AcompanhamentoOrdemServicoViewModel>, Exception>(acompanhamentos));

        var ok = Assert.IsType<Ok<IReadOnlyCollection<AcompanhamentoOrdemServicoViewModel>>>(adaptado);
        Assert.Equal(acompanhamentos, ok.Value);
    }

    [Fact]
    public void Adapt_DeveRetornarOk_QuandoResultadoBooleanoVerdadeiro()
    {
        var adaptado = _adapter.Adapt(new OrdensServicoResult<bool, Exception>(true));

        var ok = Assert.IsType<Ok<bool>>(adaptado);
        Assert.True(ok.Value);
    }

    [Fact]
    public void Adapt_DeveRetornarNotFound_QuandoResultadoBooleanoComErro()
    {
        var adaptado = _adapter.Adapt(new OrdensServicoResult<bool, Exception>(new KeyNotFoundException("nao encontrado")));

        Assert.IsType<NotFound<Dictionary<string, string?>>>(adaptado);
    }

    [Fact]
    public void Empty_DeveRetornarNoContent()
    {
        Assert.IsType<NoContent>(_adapter.Empty());
    }
}
