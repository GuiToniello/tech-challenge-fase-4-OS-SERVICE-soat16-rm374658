namespace TechChallenge.Oficina.OsService.UseCases.Features.OrdensServico.ViewModels;

public sealed class AcompanhamentoOrdemServicoViewModel
{
    public Guid Id { get; set; }
    public int Status { get; set; }
    public string StatusDescricao { get; set; } = string.Empty;
    public decimal? ValorOrcamento { get; set; }
    public string? MotivoCancelamento { get; set; }
    public IReadOnlyCollection<HistoricoStatusOrdemServicoViewModel> HistoricoStatus { get; set; } = [];
}
