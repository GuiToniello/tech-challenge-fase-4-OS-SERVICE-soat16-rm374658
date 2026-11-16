namespace TechChallenge.Oficina.OsService.UseCases.Features.OrdensServico.ViewModels;

public sealed class OrdemServicoOrdenadasViewModel
{
    public Guid Id { get; set; }
    public Guid ClienteId { get; set; }
    public Guid VeiculoId { get; set; }
    public IReadOnlyCollection<Guid> ServicoIds { get; set; } = [];
    public decimal? ValorOrcamento { get; set; }
    public int Status { get; set; }
    public string StatusDescricao { get; set; } = string.Empty;
    public DateTime DataAlteracao { get; set; }
}
