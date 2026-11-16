using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using TechChallenge.Oficina.OsService.DB.Data.Context;
using TechChallenge.Oficina.OsService.DB.Data.Features.Indicadores;
using TechChallenge.Oficina.OsService.DB.Data.Features.OrdensServico;
using TechChallenge.Oficina.OsService.Entities.Features.OrdensServico;
using TechChallenge.Oficina.OsService.UseCases.Features.Indicadores.UseCases;
using TechChallenge.Oficina.OsService.UseCases.Features.OrdensServico.UseCases;
using Xunit;

namespace TechChallenge.Oficina.OsService.DB.Data.Tests;

public sealed class ExtensionsTests
{
    private const string ConnectionString = "Host=localhost;Port=5433;Database=oficina_os;Username=sa;Password=senha";

    [Fact]
    public void AddInfraData_DeveRegistrarGatewaysEHealthCheckDoBanco()
    {
        var services = new ServiceCollection();

        services.AddInfraData(ConnectionString);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        Assert.IsType<OrdemServicoGateway>(scope.ServiceProvider.GetRequiredService<IOrdemServicoGateway>());
        Assert.IsType<IndicadorGateway>(scope.ServiceProvider.GetRequiredService<IIndicadorGateway>());

        var healthChecks = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations;
        var postgres = Assert.Single(healthChecks, registro => registro.Name == "postgres");
        Assert.Contains("ready", postgres.Tags);
    }

    [Fact]
    public void Modelo_DeveMapearTabelasEColunasNoPostgres()
    {
        var options = new DbContextOptionsBuilder<OsServiceDbContext>().UseNpgsql(ConnectionString).Options;
        using var context = new OsServiceDbContext(options);

        var ordemServico = context.Model.FindEntityType(typeof(OrdemServico))!;

        Assert.Equal("ordens_servico", ordemServico.GetTableName());
        Assert.Equal("uuid[]", ordemServico.FindProperty(nameof(OrdemServico.ServicoIds))!.GetColumnType());
        Assert.Equal("servico_ids", ordemServico.FindProperty(nameof(OrdemServico.ServicoIds))!.GetColumnName());
        Assert.Equal("numeric(18,2)", ordemServico.FindProperty(nameof(OrdemServico.ValorOrcamento))!.GetColumnType());
        Assert.Equal(300, ordemServico.FindProperty(nameof(OrdemServico.MotivoCancelamento))!.GetMaxLength());
        Assert.Equal("id", ordemServico.FindProperty(nameof(OrdemServico.Id))!.GetColumnName());

        var historico = ordemServico.FindNavigation(nameof(OrdemServico.HistoricoStatus))!.TargetEntityType;
        Assert.Equal("ordens_servico_historico_status", historico.GetTableName());
    }
}
