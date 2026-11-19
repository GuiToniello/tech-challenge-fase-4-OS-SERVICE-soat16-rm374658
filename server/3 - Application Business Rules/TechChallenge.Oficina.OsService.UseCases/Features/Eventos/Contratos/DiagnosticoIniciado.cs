namespace TechChallenge.Oficina.OsService.UseCases.Features.Eventos.Contratos;

// Consumido: a Execucao e Producao iniciou o diagnostico da OS. A OS passa de recebida para em diagnostico.
public sealed record DiagnosticoIniciado(Guid OrdemServicoId);
