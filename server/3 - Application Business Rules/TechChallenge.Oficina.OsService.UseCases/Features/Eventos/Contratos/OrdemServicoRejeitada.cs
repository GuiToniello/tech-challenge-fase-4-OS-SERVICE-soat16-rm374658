namespace TechChallenge.Oficina.OsService.UseCases.Features.Eventos.Contratos;

// Consumido: a Management rejeitou a OS (cliente, veiculo ou servico inexistente). Compensacao: a OS e cancelada com o motivo.
public sealed record OrdemServicoRejeitada(Guid OrdemServicoId, string? Motivo);
