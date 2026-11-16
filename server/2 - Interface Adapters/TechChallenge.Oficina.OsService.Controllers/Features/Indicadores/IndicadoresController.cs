using TechChallenge.Oficina.OsService.UseCases.Features.Indicadores.Queries;
using TechChallenge.Oficina.OsService.UseCases.Features.Indicadores.UseCases;

namespace TechChallenge.Oficina.OsService.Controllers.Features.Indicadores
{
    public sealed class IndicadoresController : IIndicadoresController
    {
        private readonly IIndicadorUseCases _indicadorUseCases;
        private readonly IIndicadoresAdapter _indicadoresAdapter;

        public IndicadoresController(IIndicadorUseCases indicadorUseCases, IIndicadoresAdapter indicadoresAdapter)
        {
            _indicadorUseCases = indicadorUseCases;
            _indicadoresAdapter = indicadoresAdapter;
        }

        public async Task<object> Get(CancellationToken cancellationToken)
        {
            var indicadores = await _indicadorUseCases.ObterAsync(new ObterIndicadoresQuery(), cancellationToken);
            var result = IndicadoresResult.From(indicadores);

            return _indicadoresAdapter.Adapt(result);
        }
    }
}
