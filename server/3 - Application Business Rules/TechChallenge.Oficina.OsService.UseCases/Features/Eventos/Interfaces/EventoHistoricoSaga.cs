namespace TechChallenge.Oficina.OsService.UseCases.Features.Eventos.Interfaces;

// Um registro do historico da saga: um evento consumido pelo OS Service e o veredito dele (Resultado). Tipo e o nome do evento no contrato
// e Origem usa os valores de OrigensHistoricoSaga. Os status sao os valores numericos de StatusOrdemServico: iguais quando nada mudou
// (orcamento regerado ou evento ignorado) e vazios quando a OS nao existe. Motivo so no ignorado. Sem dados pessoais e sem o conteudo do evento.
public sealed record EventoHistoricoSaga
{
    public required Guid OrdemServicoId { get; init; }
    public required DateTime OcorridoEm { get; init; }
    public required string Tipo { get; init; }
    public required string Origem { get; init; }
    public required ResultadoProcessamentoEvento Resultado { get; init; }
    public int? StatusAnterior { get; init; }
    public int? StatusNovo { get; init; }
    public string? Motivo { get; init; }
}
