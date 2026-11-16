using System.Text.Json;
using TechChallenge.Oficina.OsService.UseCases.Features.Eventos;
using TechChallenge.Oficina.OsService.UseCases.Features.Eventos.Contratos;
using TechChallenge.Oficina.OsService.UseCases.Features.Eventos.UseCases;

namespace TechChallenge.Oficina.OsService.Controllers.Features.Eventos
{
    public sealed class EventosController : IEventosController
    {
        // Os dados do envelope chegam em camelCase (contrato entre os servicos)
        private static readonly JsonSerializerOptions OpcoesJson = new(JsonSerializerDefaults.Web);

        private readonly IOrdemServicoEventosUseCases _ordemServicoEventosUseCases;

        public EventosController(IOrdemServicoEventosUseCases ordemServicoEventosUseCases)
        {
            _ordemServicoEventosUseCases = ordemServicoEventosUseCases;
        }

        // Tipo desconhecido e ignorado (a mensagem sai da fila). Dados invalidos lancam JsonException: a mensagem fica na fila e, apos as retentativas, vai para a DLQ.
        public async Task<ResultadoProcessamentoEvento> ProcessarAsync(string tipo, string dadosJson, CancellationToken cancellationToken)
        {
            return tipo switch
            {
                nameof(OrdemServicoRejeitada) => await _ordemServicoEventosUseCases.ProcessarRejeitadaAsync(Desserializar<OrdemServicoRejeitada>(dadosJson), cancellationToken),
                nameof(OrcamentoGerado) => await _ordemServicoEventosUseCases.ProcessarOrcamentoGeradoAsync(Desserializar<OrcamentoGerado>(dadosJson), cancellationToken),
                nameof(OrcamentoAprovado) => await _ordemServicoEventosUseCases.ProcessarOrcamentoAprovadoAsync(Desserializar<OrcamentoAprovado>(dadosJson), cancellationToken),
                nameof(OrcamentoRecusado) => await _ordemServicoEventosUseCases.ProcessarOrcamentoRecusadoAsync(Desserializar<OrcamentoRecusado>(dadosJson), cancellationToken),
                _ => ResultadoProcessamentoEvento.Ignorado
            };
        }

        private static TEvento Desserializar<TEvento>(string dadosJson) where TEvento : class
        {
            return JsonSerializer.Deserialize<TEvento>(dadosJson, OpcoesJson)
                ?? throw new JsonException($"Os dados do evento {typeof(TEvento).Name} nao foram informados.");
        }
    }
}
