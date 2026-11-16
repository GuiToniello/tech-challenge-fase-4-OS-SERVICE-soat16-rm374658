using TechChallenge.Oficina.OsService.UseCases.Features.Eventos;

namespace TechChallenge.Oficina.OsService.Controllers.Features.Eventos
{
    // Porta de entrada dos eventos consumidos da fila: o consumidor entrega o tipo e os dados (JSON) do envelope.
    public interface IEventosController
    {
        Task<ResultadoProcessamentoEvento> ProcessarAsync(string tipo, string dadosJson, CancellationToken cancellationToken);
    }
}
