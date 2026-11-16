namespace TechChallenge.Oficina.OsService.Entities.Features.OrdensServico.Enums;

// Os valores numéricos fazem parte do contrato dos eventos (statusAnterior/statusNovo): não renumerar.
public enum StatusOrdemServico
{
    Recebida = 1,
    EmDiagnostico = 2,
    AguardandoAprovacao = 3,
    EmExecucao = 4,
    Finalizada = 5,
    Entregue = 6,
    Encerrada = 7,
    Cancelada = 8
}
