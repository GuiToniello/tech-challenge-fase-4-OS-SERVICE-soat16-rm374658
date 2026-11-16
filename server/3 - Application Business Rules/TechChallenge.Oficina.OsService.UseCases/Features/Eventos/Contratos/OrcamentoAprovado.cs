namespace TechChallenge.Oficina.OsService.UseCases.Features.Eventos.Contratos;

// Consumido: o cliente aprovou o orcamento na Management. A OS passa para em execucao.
public sealed record OrcamentoAprovado(Guid OrdemServicoId);
