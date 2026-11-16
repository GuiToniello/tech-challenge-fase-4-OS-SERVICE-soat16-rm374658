using TechChallenge.Oficina.OsService.Controllers.Features.Indicadores;
using TechChallenge.Oficina.OsService.UseCases.Features.Indicadores.ViewModels;

namespace TechChallenge.Oficina.OsService.API.Features.Indicadores
{
    public static class IndicadoresEndpoints
    {
        public static RouteGroupBuilder MapIndicadoresEndpoints(this IEndpointRouteBuilder routes)
        {
            var group = routes
                .MapGroup("/api/indicadores")
                .WithTags("Indicadores");

            group.MapGet(
                string.Empty,
                (
                    IIndicadoresController controller,
                    CancellationToken cancellationToken
                ) => controller.Get(cancellationToken))
                .Produces<IndicadorViewModel>(StatusCodes.Status200OK);

            return group;
        }
    }
}
