namespace TechChallenge.Oficina.OsService.UseCases.Features.OrdensServico.ViewModels;

public sealed class EventoHistoricoSagaViewModel
{
    public DateTime OcorridoEm { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public string Origem { get; set; } = string.Empty;
    public string Resultado { get; set; } = string.Empty;
    public int? StatusAnterior { get; set; }
    public int? StatusNovo { get; set; }
    public string? Motivo { get; set; }
}
