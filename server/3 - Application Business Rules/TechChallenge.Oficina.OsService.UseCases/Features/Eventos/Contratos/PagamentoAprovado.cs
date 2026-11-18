namespace TechChallenge.Oficina.OsService.UseCases.Features.Eventos.Contratos;

// Consumido: o Billing confirmou o pagamento do orcamento aprovado. A OS passa para em execucao (pagamentoId e valor sao so informativos aqui).
public sealed record PagamentoAprovado(Guid OrdemServicoId, Guid PagamentoId, decimal Valor);
