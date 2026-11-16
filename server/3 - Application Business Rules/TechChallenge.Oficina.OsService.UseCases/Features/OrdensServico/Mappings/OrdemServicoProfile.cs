using AutoMapper;
using TechChallenge.Oficina.OsService.Entities.Features.OrdensServico;
using TechChallenge.Oficina.OsService.Entities.Features.OrdensServico.Enums;
using TechChallenge.Oficina.OsService.Entities.Features.OrdensServico.VOs;
using TechChallenge.Oficina.OsService.UseCases.Features.OrdensServico.ViewModels;

namespace TechChallenge.Oficina.OsService.UseCases.Features.OrdensServico.Mappings;

public sealed class OrdemServicoProfile : Profile
{
    public OrdemServicoProfile()
    {
        CreateMap<HistoricoStatusOrdemServico, HistoricoStatusOrdemServicoViewModel>();

        CreateMap<OrdemServico, AcompanhamentoOrdemServicoViewModel>()
            .ForMember(destino => destino.Status, configuracao => configuracao.MapFrom(origem => (int)origem.Status))
            .ForMember(destino => destino.StatusDescricao, configuracao => configuracao.MapFrom(origem => origem.Status.ObterDescricao()))
            .ForMember(destino => destino.HistoricoStatus, configuracao => configuracao.MapFrom(origem => origem.HistoricoStatus.OrderBy(historico => historico.DataAlteracao)));

        CreateMap<OrdemServico, OrdemServicoViewModel>()
            .ForMember(destino => destino.Status, configuracao => configuracao.MapFrom(origem => (int)origem.Status))
            .ForMember(destino => destino.StatusDescricao, configuracao => configuracao.MapFrom(origem => origem.Status.ObterDescricao()));

        CreateMap<OrdemServico, OrdemServicoOrdenadasViewModel>()
            .ForMember(destino => destino.Status, configuracao => configuracao.MapFrom(origem => (int)origem.Status))
            .ForMember(destino => destino.StatusDescricao, configuracao => configuracao.MapFrom(origem => origem.Status.ObterDescricao()))
            .ForMember(destino => destino.DataAlteracao, configuracao => configuracao.MapFrom(origem =>
                origem.HistoricoStatus.Count > 0
                    ? origem.HistoricoStatus.Min(historico => historico.DataAlteracao)
                    : default(DateTime)));
    }
}
