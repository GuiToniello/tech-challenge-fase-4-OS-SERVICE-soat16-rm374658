using Microsoft.AspNetCore.Http.HttpResults;
using TechChallenge.Oficina.OsService.Integration.Tests.Infrastructure;
using TechChallenge.Oficina.OsService.UseCases.Features.Indicadores.ViewModels;
using TechChallenge.Oficina.OsService.UseCases.Features.OrdensServico.Commands;
using TechChallenge.Oficina.OsService.UseCases.Features.OrdensServico.ViewModels;
using Xunit;

namespace TechChallenge.Oficina.OsService.Integration.Tests.Features.Indicadores;

public sealed class IndicadoresControllerIntegrationTests : IDisposable
{
    private readonly IntegrationTestFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    private async Task<Guid> CriarOrdemServicoAsync()
    {
        var command = new CriarOrdemServicoCommand { ClienteId = Guid.NewGuid(), VeiculoId = Guid.NewGuid(), ServicoIds = [Guid.NewGuid()] };
        var created = Assert.IsType<CreatedAtRoute<OrdemServicoViewModel>>(await _fixture.CriarOrdensServicoEndpoints().Post(command, CancellationToken.None));
        return created.Value!.Id;
    }

    private async Task<Guid> CriarOrdemComOrcamentoRecusadoAsync()
    {
        var ordemId = await CriarOrdemServicoAsync();
        var eventos = _fixture.CriarEventosController();

        await eventos.ProcessarAsync("OrcamentoGerado", $$"""{"ordemServicoId":"{{ordemId}}","valorTotal":120}""", CancellationToken.None);
        await eventos.ProcessarAsync("OrcamentoRecusado", $$"""{"ordemServicoId":"{{ordemId}}"}""", CancellationToken.None);

        return ordemId;
    }

    [Fact]
    public async Task Get_SemOrdensEntregues_DeveRetornar200ComTemposZerados()
    {
        var indicadores = Assert.IsType<Ok<IndicadorViewModel>>(await _fixture.CriarIndicadoresEndpoints().Get(CancellationToken.None));

        Assert.Equal(TimeSpan.Zero, indicadores.Value!.TempoMedioExecucao);
        Assert.Equal(TimeSpan.Zero, indicadores.Value.TempoMedioEntrega);
    }

    [Fact]
    public async Task AlterarParaEntregue_OrdemComOrcamentoRecusado_DeveAtualizarSomenteTempoDeEntrega()
    {
        var ordemRecusadaId = await CriarOrdemComOrcamentoRecusadoAsync();

        var entregue = Assert.IsType<Ok<OrdemServicoViewModel>>(await _fixture.CriarOrdensServicoEndpoints().AlterarParaEntregue(ordemRecusadaId, CancellationToken.None));
        Assert.Equal(6, entregue.Value!.Status);

        var indicadores = Assert.IsType<Ok<IndicadorViewModel>>(await _fixture.CriarIndicadoresEndpoints().Get(CancellationToken.None));
        Assert.True(indicadores.Value!.TempoMedioEntrega > TimeSpan.Zero);
        Assert.Equal(TimeSpan.Zero, indicadores.Value.TempoMedioExecucao);
    }

    [Fact]
    public async Task Get_AposEntregarOrdemRecusadaEOrdemExecutada_DeveRetornarTemposMedios()
    {
        var ordemRecusadaId = await CriarOrdemComOrcamentoRecusadoAsync();
        var ordemExecutadaId = await CriarOrdemServicoAsync();
        var endpoints = _fixture.CriarOrdensServicoEndpoints();

        Assert.IsType<Ok<OrdemServicoViewModel>>(await endpoints.AlterarParaEntregue(ordemRecusadaId, CancellationToken.None));

        Assert.IsType<Ok<OrdemServicoViewModel>>(await endpoints.AlterarParaEmDiagnostico(ordemExecutadaId, CancellationToken.None));
        await _fixture.CriarEventosController().ProcessarAsync("OrcamentoGerado", $$"""{"ordemServicoId":"{{ordemExecutadaId}}","valorTotal":80}""", CancellationToken.None);
        await _fixture.CriarEventosController().ProcessarAsync("OrcamentoAprovado", $$"""{"ordemServicoId":"{{ordemExecutadaId}}"}""", CancellationToken.None);
        Assert.IsType<Ok<OrdemServicoViewModel>>(await _fixture.CriarOrdensServicoEndpoints().AlterarParaFinalizada(ordemExecutadaId, CancellationToken.None));
        var entregue = Assert.IsType<Ok<OrdemServicoViewModel>>(await _fixture.CriarOrdensServicoEndpoints().AlterarParaEntregue(ordemExecutadaId, CancellationToken.None));
        Assert.Equal(6, entregue.Value!.Status);

        var indicadores = Assert.IsType<Ok<IndicadorViewModel>>(await _fixture.CriarIndicadoresEndpoints().Get(CancellationToken.None));
        Assert.True(indicadores.Value!.TempoMedioEntrega > TimeSpan.Zero);
        Assert.True(indicadores.Value.TempoMedioExecucao > TimeSpan.Zero);
    }
}
