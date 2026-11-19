namespace TechChallenge.Oficina.OsService.UseCases.Features.Eventos.Contratos;

// Consumido: a Execucao e Producao finalizou a execucao da OS. A OS passa de em execucao para finalizada.
public sealed record ExecucaoFinalizada(Guid OrdemServicoId);
