using Moq;
using TechChallenge.Oficina.OsService.Controllers.Features.Indicadores;
using TechChallenge.Oficina.OsService.UseCases.Features.Indicadores.Queries;
using TechChallenge.Oficina.OsService.UseCases.Features.Indicadores.UseCases;
using TechChallenge.Oficina.OsService.UseCases.Features.Indicadores.ViewModels;
using Xunit;

namespace TechChallenge.Oficina.OsService.Controllers.Tests.Features.Indicadores;

public sealed class IndicadoresControllerTests
{
    [Fact]
    public async Task Get_DeveObterIndicadoresEAdaptar()
    {
        var useCasesMock = new Mock<IIndicadorUseCases>();
        var adapterMock = new Mock<IIndicadoresAdapter>();
        var indicador = new IndicadorViewModel { TempoMedioExecucao = TimeSpan.FromHours(1), TempoMedioEntrega = TimeSpan.FromHours(3) };
        var resultadoAdaptado = new object();

        useCasesMock.Setup(useCases => useCases.ObterAsync(It.IsAny<ObterIndicadoresQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync(indicador);
        adapterMock.Setup(adapter => adapter.Adapt(It.Is<IndicadoresResult<IndicadorViewModel, Exception>>(r => r.Value == indicador))).Returns(resultadoAdaptado);

        var resultado = await new IndicadoresController(useCasesMock.Object, adapterMock.Object).Get(CancellationToken.None);

        Assert.Same(resultadoAdaptado, resultado);
        useCasesMock.Verify(useCases => useCases.ObterAsync(It.IsAny<ObterIndicadoresQuery>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void IndicadoresResult_DeveGuardarValorOuErro()
    {
        var sucesso = IndicadoresResult.From(new IndicadorViewModel());
        var erro = IndicadoresResult.FromError<IndicadorViewModel>(new InvalidOperationException("erro"));

        Assert.NotNull(sucesso.Value);
        Assert.Null(sucesso.Error);
        Assert.Null(erro.Value);
        Assert.IsType<InvalidOperationException>(erro.Error);
    }
}
