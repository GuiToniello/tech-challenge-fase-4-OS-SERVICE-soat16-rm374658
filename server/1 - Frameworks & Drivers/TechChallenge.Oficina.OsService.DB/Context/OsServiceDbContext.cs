using Microsoft.EntityFrameworkCore;
using TechChallenge.Oficina.OsService.Entities.Features.Indicadores;
using TechChallenge.Oficina.OsService.Entities.Features.OrdensServico;

namespace TechChallenge.Oficina.OsService.DB.Data.Context;

public sealed class OsServiceDbContext : DbContext
{
    public OsServiceDbContext(DbContextOptions<OsServiceDbContext> options) : base(options)
    {
    }

    public DbSet<Indicador> Indicadores => Set<Indicador>();
    public DbSet<OrdemServico> OrdensServico => Set<OrdemServico>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OsServiceDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
