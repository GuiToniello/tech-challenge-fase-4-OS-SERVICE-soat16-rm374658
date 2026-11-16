namespace TechChallenge.Oficina.OsService.UseCases.Features.Eventos.Interfaces;

public interface IEventosPublisher
{
    // O tipo do envelope e o nome da classe do evento (typeof(TEvento).Name): os contratos em Eventos/Contratos levam o nome exato do evento.
    // A ordem de servico identifica o grupo da mensagem na fila FIFO, garantindo a ordem dos eventos de uma mesma OS.
    Task PublicarAsync<TEvento>(TEvento evento, Guid ordemServicoId, CancellationToken cancellationToken = default) where TEvento : class;
}
