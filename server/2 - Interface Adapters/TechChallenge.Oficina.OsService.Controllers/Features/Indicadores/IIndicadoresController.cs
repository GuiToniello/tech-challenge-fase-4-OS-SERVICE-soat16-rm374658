namespace TechChallenge.Oficina.OsService.Controllers.Features.Indicadores
{
    public interface IIndicadoresController
    {
        Task<object> Get(CancellationToken cancellationToken);
    }
}
