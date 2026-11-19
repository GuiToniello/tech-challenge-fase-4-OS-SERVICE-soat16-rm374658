using System.Text.Json;

namespace TechChallenge.Oficina.OsService.Messaging.Features.Eventos;

// Envelope comum aos eventos trocados entre OS Service, Management, Billing e Execucao, em JSON camelCase:
// id, tipo, versao, ocorridoEm, origem e dados. Na publicacao, Dados e o contrato do evento; no consumo, e lido como JsonElement.
public sealed record EnvelopeEvento<TDados>(Guid Id, string Tipo, int Versao, DateTime OcorridoEm, string Origem, TDados Dados);

public static class SerializacaoEventos
{
    // Web: camelCase na escrita e leitura sem diferenciar maiusculas de minusculas
    public static readonly JsonSerializerOptions Opcoes = new(JsonSerializerDefaults.Web);
}
