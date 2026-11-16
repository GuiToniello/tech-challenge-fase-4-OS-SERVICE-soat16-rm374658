using TechChallenge.Oficina.OsService.Entities.Features.Indicadores;
using TechChallenge.Oficina.OsService.UseCases.Features.Indicadores.Queries;
using TechChallenge.Oficina.OsService.UseCases.Features.Indicadores.ViewModels;

namespace TechChallenge.Oficina.OsService.UseCases.Features.Indicadores.UseCases;

public interface IIndicadorUseCases : IIndicadorService<ObterIndicadoresQuery, IndicadorViewModel>
{
}
