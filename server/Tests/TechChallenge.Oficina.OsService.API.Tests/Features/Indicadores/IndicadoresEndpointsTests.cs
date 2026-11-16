using Microsoft.AspNetCore.Http.HttpResults;
using Moq;
using TechChallenge.Oficina.OsService.Adapters.Features.Indicadores;
using TechChallenge.Oficina.OsService.Controllers.Features.Indicadores;
using TechChallenge.Oficina.OsService.UseCases.Features.Indicadores.Queries;
using TechChallenge.Oficina.OsService.UseCases.Features.Indicadores.UseCases;
using TechChallenge.Oficina.OsService.UseCases.Features.Indicadores.ViewModels;
using Xunit;

namespace TechChallenge.Oficina.OsService.API.Tests.Features.Indicadores;

public sealed class IndicadoresEndpointsTests
{
    [Fact]
    public async Task MapIndicadoresEndpoints_Get_DeveRetornar200ComIndicadores()
    {
        var useCasesMock = new Mock<IIndicadorUseCases>();
        var indicador = new IndicadorViewModel { TempoMedioExecucao = TimeSpan.FromHours(2), TempoMedioEntrega = TimeSpan.FromHours(5) };
        useCasesMock.Setup(s => s.ObterAsync(It.IsAny<ObterIndicadoresQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync(indicador);

        var resultado = await new IndicadoresController(useCasesMock.Object, new IndicadoresAdapter()).Get(CancellationToken.None);

        var ok = Assert.IsType<Ok<IndicadorViewModel>>(resultado);
        Assert.Equal(indicador, ok.Value);
    }
}
