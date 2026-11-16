namespace TechChallenge.Oficina.OsService.UseCases.Features.Eventos.Contratos;

// Publicado pelo OS Service ao atualizar cliente, veiculo ou servicos da OS (so permitido com a OS recebida).
public sealed record OrdemServicoAtualizada(Guid OrdemServicoId, Guid ClienteId, Guid VeiculoId, IReadOnlyCollection<Guid> ServicoIds);
