using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using TechChallenge.Oficina.OsService.Adapters.Features.Indicadores;
using TechChallenge.Oficina.OsService.Controllers.Features.Indicadores;
using TechChallenge.Oficina.OsService.Entities.Exceptions;
using TechChallenge.Oficina.OsService.UseCases.Features.Indicadores.ViewModels;
using Xunit;

namespace TechChallenge.Oficina.OsService.Adapters.Tests.Features.Indicadores;

public sealed class IndicadoresAdapterTests
{
    private readonly IndicadoresAdapter _adapter = new();

    [Fact]
    public void Adapt_DeveRetornarOk_QuandoIndicadorSucesso()
    {
        var indicador = new IndicadorViewModel { TempoMedioExecucao = TimeSpan.FromHours(1), TempoMedioEntrega = TimeSpan.FromHours(2) };

        var adaptado = _adapter.Adapt(IndicadoresResult.From(indicador));

        var ok = Assert.IsType<Ok<IndicadorViewModel>>(adaptado);
        Assert.Equal(indicador, ok.Value);
    }

    [Fact]
    public void Adapt_DeveRetornarOk_QuandoListaSucesso()
    {
        IReadOnlyCollection<IndicadorViewModel> indicadores = [new IndicadorViewModel()];

        var adaptado = _adapter.Adapt(IndicadoresResult.From(indicadores));

        Assert.IsType<Ok<IReadOnlyCollection<IndicadorViewModel>>>(adaptado);
    }

    [Fact]
    public void Adapt_DeveRetornarOk_QuandoBooleanoVerdadeiro()
    {
        var adaptado = _adapter.Adapt(IndicadoresResult.From(true));

        Assert.IsType<Ok<bool>>(adaptado);
    }

    [Fact]
    public void Adapt_DeveRetornarBadRequest_QuandoDomainException()
    {
        var adaptado = _adapter.Adapt(IndicadoresResult.FromError<IndicadorViewModel>(new DomainException("erro")));

        var statusCode = Assert.IsAssignableFrom<IStatusCodeHttpResult>(adaptado);
        Assert.Equal(StatusCodes.Status400BadRequest, statusCode.StatusCode);
    }

    [Fact]
    public void Adapt_DeveRetornarNotFound_QuandoKeyNotFoundException()
    {
        var adaptado = _adapter.Adapt(IndicadoresResult.FromError<IndicadorViewModel>(new KeyNotFoundException("nao encontrado")));

        var statusCode = Assert.IsAssignableFrom<IStatusCodeHttpResult>(adaptado);
        Assert.Equal(StatusCodes.Status404NotFound, statusCode.StatusCode);
    }

    [Fact]
    public void Adapt_DeveRetornarProblem_QuandoOutraExcecao()
    {
        var adaptado = _adapter.Adapt(IndicadoresResult.FromError<bool>(new InvalidOperationException("falha")));

        var statusCode = Assert.IsAssignableFrom<IStatusCodeHttpResult>(adaptado);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusCode.StatusCode);
    }

    [Fact]
    public void Empty_DeveRetornarNoContent()
    {
        Assert.IsType<NoContent>(_adapter.Empty());
    }
}
