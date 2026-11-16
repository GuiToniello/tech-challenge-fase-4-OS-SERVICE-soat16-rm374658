using TechChallenge.Oficina.OsService.UseCases.Features.Indicadores.ViewModels;

namespace TechChallenge.Oficina.OsService.Controllers.Features.Indicadores
{
    public interface IIndicadoresAdapter
    {
        object Adapt(IndicadoresResult<IndicadorViewModel, Exception> result);

        object Adapt(IndicadoresResult<IReadOnlyCollection<IndicadorViewModel>, Exception> result);

        object Adapt(IndicadoresResult<bool, Exception> result);

        object Empty();
    }
}
