using Microsoft.EntityFrameworkCore;
using TechChallenge.Oficina.OsService.DB.Data.Context;
using TechChallenge.Oficina.OsService.Entities.Features.Indicadores;
using TechChallenge.Oficina.OsService.UseCases.Features.Indicadores.UseCases;

namespace TechChallenge.Oficina.OsService.DB.Data.Features.Indicadores;

public sealed class IndicadorGateway : IIndicadorGateway
{
    private readonly OsServiceDbContext _context;

    public IndicadorGateway(OsServiceDbContext context)
    {
        _context = context;
    }

    public async Task<Indicador?> ObterAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Indicadores.FirstOrDefaultAsync(cancellationToken);
    }

    public async Task SalvarAsync(Indicador indicador, CancellationToken cancellationToken = default)
    {
        var existente = await _context.Indicadores.FirstOrDefaultAsync(cancellationToken);

        if (existente is null)
        {
            await _context.Indicadores.AddAsync(indicador, cancellationToken);
        }
        else
        {
            existente.Atualizar(indicador.TempoMedioExecucao, indicador.TempoMedioEntrega);
            _context.Indicadores.Update(existente);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
