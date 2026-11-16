namespace TechChallenge.Oficina.OsService.UseCases.Features.Eventos.Contratos;

// Publicado pelo OS Service a cada mudanca de status, vinda do HTTP ou de um evento. Status com os valores de StatusOrdemServico (1 a 8).
public sealed record OrdemServicoStatusAlterado(Guid OrdemServicoId, Guid ClienteId, int StatusAnterior, int StatusNovo);
