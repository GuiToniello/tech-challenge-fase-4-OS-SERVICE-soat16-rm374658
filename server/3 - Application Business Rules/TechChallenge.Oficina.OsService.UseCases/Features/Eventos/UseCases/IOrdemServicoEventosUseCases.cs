using TechChallenge.Oficina.OsService.UseCases.Features.Eventos.Contratos;

namespace TechChallenge.Oficina.OsService.UseCases.Features.Eventos.UseCases;

public interface IOrdemServicoEventosUseCases
{
    Task<ResultadoProcessamentoEvento> ProcessarRejeitadaAsync(OrdemServicoRejeitada evento, CancellationToken cancellationToken = default);
    Task<ResultadoProcessamentoEvento> ProcessarOrcamentoGeradoAsync(OrcamentoGerado evento, CancellationToken cancellationToken = default);
    Task<ResultadoProcessamentoEvento> ProcessarPagamentoAprovadoAsync(PagamentoAprovado evento, CancellationToken cancellationToken = default);
    Task<ResultadoProcessamentoEvento> ProcessarOrcamentoRecusadoAsync(OrcamentoRecusado evento, CancellationToken cancellationToken = default);
}
