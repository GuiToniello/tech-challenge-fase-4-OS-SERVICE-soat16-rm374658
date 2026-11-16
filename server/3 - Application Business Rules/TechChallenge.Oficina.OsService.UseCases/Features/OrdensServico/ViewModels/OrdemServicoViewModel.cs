namespace TechChallenge.Oficina.OsService.UseCases.Features.OrdensServico.ViewModels;

public sealed class OrdemServicoViewModel
{
    public Guid Id { get; set; }
    public Guid ClienteId { get; set; }
    public Guid VeiculoId { get; set; }
    public IReadOnlyCollection<Guid> ServicoIds { get; set; } = [];
    public int Status { get; set; }
    public string StatusDescricao { get; set; } = string.Empty;
    public decimal? ValorOrcamento { get; set; }
    public string? MotivoCancelamento { get; set; }
}
