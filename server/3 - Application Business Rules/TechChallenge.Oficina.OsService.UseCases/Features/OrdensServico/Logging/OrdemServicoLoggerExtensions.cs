using Microsoft.Extensions.Logging;
using TechChallenge.Oficina.OsService.Entities.Features.OrdensServico;
using TechChallenge.Oficina.OsService.Entities.Features.OrdensServico.Enums;

namespace TechChallenge.Oficina.OsService.UseCases.Features.OrdensServico.Logging;

public static class OrdemServicoLoggerExtensions
{
    // Usado pelas mudanças de status vindas do HTTP e dos eventos: o template e as propriedades do os.status_alterado ficam num só lugar
    public static void RegistrarStatusAlterado(
        this ILogger logger,
        OrdemServico ordemServico,
        string etapa,
        StatusOrdemServico statusAnterior,
        DateTime dataEntradaStatusAnterior,
        DateTime dataAlteracao)
    {
        logger.LogInformation(
            OrdemServicoLogEventos.OsStatusAlterado,
            "Ordem de servico {OrdemServicoId} alterada de {StatusAnterior} para {StatusNovo} na etapa {Etapa} apos {TempoNoStatusAnteriorSegundos} segundos no status anterior.",
            ordemServico.Id,
            OrdemServicoLogEventos.StatusParaLog(statusAnterior),
            OrdemServicoLogEventos.StatusParaLog(ordemServico.Status),
            etapa,
            (dataAlteracao - dataEntradaStatusAnterior).TotalSeconds);
    }
}
