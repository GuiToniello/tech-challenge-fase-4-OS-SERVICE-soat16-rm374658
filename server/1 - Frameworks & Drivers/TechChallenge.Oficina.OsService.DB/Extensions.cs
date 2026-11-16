using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TechChallenge.Oficina.OsService.DB.Data.Context;
using TechChallenge.Oficina.OsService.DB.Data.Features.Indicadores;
using TechChallenge.Oficina.OsService.DB.Data.Features.OrdensServico;
using TechChallenge.Oficina.OsService.UseCases.Features.Indicadores.UseCases;
using TechChallenge.Oficina.OsService.UseCases.Features.OrdensServico.UseCases;

namespace TechChallenge.Oficina.OsService.DB.Data;

public static class Extensions
{
    public static IServiceCollection AddInfraData(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<OsServiceDbContext>(options => options.UseNpgsql(connectionString));
        services.AddHealthChecks().AddDbContextCheck<OsServiceDbContext>("postgres", tags: ["ready"]);
        services.AddScoped<IIndicadorGateway, IndicadorGateway>();
        services.AddScoped<IOrdemServicoGateway, OrdemServicoGateway>();
        return services;
    }

    public static void ApplyMigrations(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OsServiceDbContext>();
        dbContext.Database.Migrate();
    }
}
