namespace TechChallenge.Oficina.OsService.UseCases.Features.Eventos.Contratos;

// Consumido: a Management gerou (ou regerou) o orcamento da OS. A OS passa para aguardando aprovacao com o valor total.
public sealed record OrcamentoGerado(Guid OrdemServicoId, decimal ValorTotal);
