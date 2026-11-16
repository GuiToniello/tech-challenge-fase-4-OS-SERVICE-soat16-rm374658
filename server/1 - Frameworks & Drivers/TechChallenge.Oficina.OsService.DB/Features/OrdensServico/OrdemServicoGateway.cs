using Microsoft.EntityFrameworkCore;
using TechChallenge.Oficina.OsService.DB.Data.Context;
using TechChallenge.Oficina.OsService.Entities.Features.OrdensServico;
using TechChallenge.Oficina.OsService.Entities.Features.OrdensServico.Enums;
using TechChallenge.Oficina.OsService.UseCases.Features.OrdensServico.UseCases;

namespace TechChallenge.Oficina.OsService.DB.Data.Features.OrdensServico;

public sealed class OrdemServicoGateway : IOrdemServicoGateway
{
    private static readonly Dictionary<StatusOrdemServico, int> PrioridadeStatus = new()
    {
        { StatusOrdemServico.EmExecucao, 1 },
        { StatusOrdemServico.AguardandoAprovacao, 2 },
        { StatusOrdemServico.EmDiagnostico, 3 },
        { StatusOrdemServico.Recebida, 4 }
    };

    private readonly OsServiceDbContext _context;

    public OrdemServicoGateway(OsServiceDbContext context)
    {
        _context = context;
    }

    public async Task AdicionarAsync(OrdemServico ordemServico, CancellationToken cancellationToken = default)
    {
        await _context.OrdensServico.AddAsync(ordemServico, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task AtualizarAsync(OrdemServico ordemServico, CancellationToken cancellationToken = default)
    {
        _context.OrdensServico.Update(ordemServico);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<OrdemServico?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await CriarConsultaCompleta()
            .FirstOrDefaultAsync(ordemServico => ordemServico.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyCollection<OrdemServico>> ListarAsync(CancellationToken cancellationToken = default)
    {
        return await CriarConsultaCompleta()
            .OrderBy(ordemServico => ordemServico.Id)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<OrdemServico>> ListarOrdenadasAsync(CancellationToken cancellationToken = default)
    {
        // Fora da fila de trabalho: finalizadas, entregues, encerradas e canceladas
        var ordensAtivas = await CriarConsultaCompleta()
            .Where(ordemServico => ordemServico.Status != StatusOrdemServico.Finalizada &&
                                   ordemServico.Status != StatusOrdemServico.Entregue &&
                                   ordemServico.Status != StatusOrdemServico.Encerrada &&
                                   ordemServico.Status != StatusOrdemServico.Cancelada)
            .ToArrayAsync(cancellationToken);

        // Ordena por prioridade de status e depois por data de criação (primeira entrada do histórico)
        return ordensAtivas
            .OrderBy(ordemServico => PrioridadeStatus.TryGetValue(ordemServico.Status, out var prioridade) ? prioridade : int.MaxValue)
            .ThenBy(ordemServico => ordemServico.HistoricoStatus.Count > 0 ? ordemServico.HistoricoStatus.Min(historico => historico.DataAlteracao) : DateTime.MaxValue)
            .ToArray();
    }

    public async Task<IReadOnlyCollection<OrdemServico>> ListarPorClienteAsync(Guid clienteId, CancellationToken cancellationToken = default)
    {
        return await CriarConsultaCompleta()
            .Where(ordemServico => ordemServico.ClienteId == clienteId)
            .OrderBy(ordemServico => ordemServico.Id)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<OrdemServico>> ListarPorStatusAsync(StatusOrdemServico status, CancellationToken cancellationToken = default)
    {
        return await CriarConsultaCompleta()
            .Where(ordemServico => ordemServico.Status == status)
            .OrderBy(ordemServico => ordemServico.Id)
            .ToArrayAsync(cancellationToken);
    }

    public async Task RemoverAsync(OrdemServico ordemServico, CancellationToken cancellationToken = default)
    {
        _context.OrdensServico.Remove(ordemServico);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private IQueryable<OrdemServico> CriarConsultaCompleta()
    {
        return _context.OrdensServico
            .Include(ordemServico => ordemServico.HistoricoStatus);
    }
}
