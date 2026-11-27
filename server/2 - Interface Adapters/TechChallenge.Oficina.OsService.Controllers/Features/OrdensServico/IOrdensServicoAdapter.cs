using TechChallenge.Oficina.OsService.UseCases.Features.OrdensServico.ViewModels;

namespace TechChallenge.Oficina.OsService.Controllers.Features.OrdensServico
{
    public interface IOrdensServicoAdapter
    {
        object Adapt(OrdensServicoResult<OrdemServicoViewModel, Exception> result, bool created = false);

        object Adapt(OrdensServicoResult<IReadOnlyCollection<OrdemServicoViewModel>, Exception> result);

        object Adapt(OrdensServicoResult<IReadOnlyCollection<OrdemServicoOrdenadasViewModel>, Exception> result);

        object Adapt(OrdensServicoResult<AcompanhamentoOrdemServicoViewModel, Exception> result);

        object Adapt(OrdensServicoResult<IReadOnlyCollection<AcompanhamentoOrdemServicoViewModel>, Exception> result);

        object Adapt(OrdensServicoResult<IReadOnlyCollection<EventoHistoricoSagaViewModel>, Exception> result);

        object Adapt(OrdensServicoResult<bool, Exception> result);

        object Empty();
    }
}
