namespace TechChallenge.Oficina.OsService.UseCases.Features.Eventos.Contratos;

// Publicado pelo OS Service ao criar a OS. A Management valida cliente, veiculo e servicos e, se algo nao existir, responde com OrdemServicoRejeitada.
public sealed record OrdemServicoAberta(Guid OrdemServicoId, Guid ClienteId, Guid VeiculoId, IReadOnlyCollection<Guid> ServicoIds);
