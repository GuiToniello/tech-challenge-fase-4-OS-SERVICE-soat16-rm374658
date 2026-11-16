namespace TechChallenge.Oficina.OsService.UseCases.Features.Eventos.Contratos;

// Consumido: o cliente recusou o orcamento na Management. A OS passa para finalizada, sem execucao.
public sealed record OrcamentoRecusado(Guid OrdemServicoId);
