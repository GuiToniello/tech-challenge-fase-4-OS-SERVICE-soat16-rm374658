using TechChallenge.Oficina.OsService.UseCases.Features.Eventos.Interfaces;

namespace TechChallenge.Oficina.OsService.Integration.Tests.Infrastructure;

/// <summary>
/// Historico da saga em memoria no lugar do DynamoDB: uma lista com os eventos na ordem de registro.
/// </summary>
public sealed class HistoricoSagaEmMemoria : IHistoricoSagaGateway
{
    private readonly List<EventoHistoricoSaga> _eventos = [];

    public IReadOnlyList<EventoHistoricoSaga> Eventos => _eventos;

    public Task RegistrarAsync(EventoHistoricoSaga evento, CancellationToken cancellationToken)
    {
        _eventos.Add(evento);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyCollection<EventoHistoricoSaga>> ListarAsync(Guid ordemServicoId, CancellationToken cancellationToken)
    {
        IReadOnlyCollection<EventoHistoricoSaga> eventos = _eventos
            .Where(evento => evento.OrdemServicoId == ordemServicoId)
            .OrderBy(evento => evento.OcorridoEm)
            .ToArray();

        return Task.FromResult(eventos);
    }
}
