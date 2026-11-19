using Moq;
using TechChallenge.Oficina.OsService.Entities.Features.Indicadores;
using TechChallenge.Oficina.OsService.Entities.Features.OrdensServico;
using TechChallenge.Oficina.OsService.Entities.Features.OrdensServico.Enums;
using TechChallenge.Oficina.OsService.UseCases.Features.Indicadores.Queries;
using TechChallenge.Oficina.OsService.UseCases.Features.Indicadores.UseCases;
using TechChallenge.Oficina.OsService.UseCases.Features.OrdensServico.UseCases;
using Xunit;

namespace TechChallenge.Oficina.OsService.UseCases.Tests.Features.Indicadores.UseCases;

public sealed class IndicadorUseCasesTests
{
    private readonly Mock<IIndicadorGateway> _indicadorGatewayMock = new();
    private readonly Mock<IOrdemServicoGateway> _ordemServicoGatewayMock = new();

    private IndicadorUseCases CriarUseCases() => new(_indicadorGatewayMock.Object, _ordemServicoGatewayMock.Object);

    private static DateTime Hora(int hora) => new(2024, 01, 01, hora, 00, 00, DateTimeKind.Utc);

    [Fact]
    public async Task ObterAsync_DeveRetornarTemposZerados_QuandoNaoHaIndicador()
    {
        var resultado = await CriarUseCases().ObterAsync(new ObterIndicadoresQuery());

        Assert.Equal(TimeSpan.Zero, resultado.TempoMedioExecucao);
        Assert.Equal(TimeSpan.Zero, resultado.TempoMedioEntrega);
    }

    [Fact]
    public async Task ObterAsync_DeveRetornarTemposDoIndicador()
    {
        _indicadorGatewayMock.Setup(g => g.ObterAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Indicador.Criar(TimeSpan.FromHours(2), TimeSpan.FromHours(4)));

        var resultado = await CriarUseCases().ObterAsync(new ObterIndicadoresQuery());

        Assert.Equal(TimeSpan.FromHours(2), resultado.TempoMedioExecucao);
        Assert.Equal(TimeSpan.FromHours(4), resultado.TempoMedioEntrega);
    }

    [Fact]
    public async Task AtualizarAsync_DeveCalcularMediasDasOrdensEntregues_IgnorandoExecucaoDeOrcamentoRecusado()
    {
        var executada = OrdemServico.Criar(Guid.NewGuid(), Guid.NewGuid(), [Guid.NewGuid()], Hora(8));
        executada.AlterarParaEmDiagnostico(Hora(9));
        executada.RegistrarOrcamentoGerado(80m, Hora(10));
        executada.RegistrarPagamentoAprovado(Hora(10));
        executada.AlterarParaFinalizada(Hora(12));
        executada.AlterarParaEntregue(Hora(13));

        var recusada = OrdemServico.Criar(Guid.NewGuid(), Guid.NewGuid(), [Guid.NewGuid()], Hora(8));
        recusada.RegistrarOrcamentoGerado(50m, Hora(9));
        recusada.RegistrarOrcamentoRecusado(Hora(10));
        recusada.AlterarParaEntregue(Hora(11));

        _ordemServicoGatewayMock.Setup(g => g.ListarPorStatusAsync(StatusOrdemServico.Entregue, It.IsAny<CancellationToken>())).ReturnsAsync([executada, recusada]);

        await CriarUseCases().AtualizarAsync();

        _indicadorGatewayMock.Verify(g => g.SalvarAsync(
            It.Is<Indicador>(indicador => indicador.TempoMedioExecucao == TimeSpan.FromHours(2) && indicador.TempoMedioEntrega == TimeSpan.FromHours(4)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AtualizarAsync_DeveSalvarIndicadorZerado_QuandoNaoHaOrdensEntregues()
    {
        _ordemServicoGatewayMock.Setup(g => g.ListarPorStatusAsync(StatusOrdemServico.Entregue, It.IsAny<CancellationToken>())).ReturnsAsync([]);

        await CriarUseCases().AtualizarAsync();

        _indicadorGatewayMock.Verify(g => g.SalvarAsync(
            It.Is<Indicador>(indicador => indicador.TempoMedioExecucao == TimeSpan.Zero && indicador.TempoMedioEntrega == TimeSpan.Zero),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
