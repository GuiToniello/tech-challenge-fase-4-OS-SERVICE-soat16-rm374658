using Microsoft.EntityFrameworkCore;
using TechChallenge.Oficina.OsService.DB.Data.Context;
using TechChallenge.Oficina.OsService.DB.Data.Features.Indicadores;
using TechChallenge.Oficina.OsService.Entities.Features.Indicadores;
using Xunit;

namespace TechChallenge.Oficina.OsService.DB.Data.Tests.Features.Indicadores;

public sealed class IndicadorGatewayTests
{
    private readonly DbContextOptions<OsServiceDbContext> _options = new DbContextOptionsBuilder<OsServiceDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options;

    [Fact]
    public async Task ObterAsync_DeveRetornarNulo_QuandoNaoHaIndicador()
    {
        await using var context = new OsServiceDbContext(_options);

        var indicador = await new IndicadorGateway(context).ObterAsync();

        Assert.Null(indicador);
    }

    [Fact]
    public async Task SalvarAsync_DeveAdicionarEDepoisAtualizarOMesmoRegistro()
    {
        await using (var context = new OsServiceDbContext(_options))
        {
            await new IndicadorGateway(context).SalvarAsync(Indicador.Criar(TimeSpan.FromHours(1), TimeSpan.FromHours(2)));
        }

        await using (var context = new OsServiceDbContext(_options))
        {
            await new IndicadorGateway(context).SalvarAsync(Indicador.Criar(TimeSpan.FromHours(3), TimeSpan.FromHours(4)));
        }

        await using var contextoLeitura = new OsServiceDbContext(_options);
        var indicador = Assert.Single(await contextoLeitura.Indicadores.ToListAsync());
        Assert.Equal(1, indicador.Id);
        Assert.Equal(TimeSpan.FromHours(3), indicador.TempoMedioExecucao);
        Assert.Equal(TimeSpan.FromHours(4), indicador.TempoMedioEntrega);
    }
}
