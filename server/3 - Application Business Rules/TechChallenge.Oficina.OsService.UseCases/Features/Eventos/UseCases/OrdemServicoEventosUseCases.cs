using Microsoft.Extensions.Logging;
using TechChallenge.Oficina.OsService.Entities.Exceptions;
using TechChallenge.Oficina.OsService.Entities.Features.OrdensServico;
using TechChallenge.Oficina.OsService.UseCases.Features.Eventos.Contratos;
using TechChallenge.Oficina.OsService.UseCases.Features.Eventos.Interfaces;
using TechChallenge.Oficina.OsService.UseCases.Features.OrdensServico.Logging;
using TechChallenge.Oficina.OsService.UseCases.Features.OrdensServico.UseCases;

namespace TechChallenge.Oficina.OsService.UseCases.Features.Eventos.UseCases;

// Processa os eventos da Management, do Billing e da Execucao e Producao sobre a OS (saga coreografada). E idempotente: a fila entrega pelo menos uma vez,
// entao OS inexistente ou transicao nao permitida (evento repetido ou fora de ordem) resulta em Ignorado, sem excecao e sem publicar nada.
// Cada evento tratado vai para o historico da saga (DynamoDB) com o veredito, por ultimo: best-effort (o gateway nao lanca) e sem o token do consumidor.
public sealed class OrdemServicoEventosUseCases : IOrdemServicoEventosUseCases
{
    private readonly IOrdemServicoGateway _ordemServicoGateway;
    private readonly IEventosPublisher _eventosPublisher;
    private readonly IHistoricoSagaGateway _historicoSagaGateway;
    private readonly ILogger<OrdemServicoEventosUseCases> _logger;

    public OrdemServicoEventosUseCases(
        IOrdemServicoGateway ordemServicoGateway,
        IEventosPublisher eventosPublisher,
        IHistoricoSagaGateway historicoSagaGateway,
        ILogger<OrdemServicoEventosUseCases> logger)
    {
        _ordemServicoGateway = ordemServicoGateway;
        _eventosPublisher = eventosPublisher;
        _historicoSagaGateway = historicoSagaGateway;
        _logger = logger;
    }

    public Task<ResultadoProcessamentoEvento> ProcessarRejeitadaAsync(OrdemServicoRejeitada evento, CancellationToken cancellationToken = default)
    {
        return ProcessarAsync(
            evento.OrdemServicoId,
            nameof(OrdemServicoRejeitada),
            OrigensHistoricoSaga.Management,
            OrdemServicoLogEventos.Etapas.Cancelamento,
            (ordemServico, dataAlteracao) => ordemServico.Cancelar(evento.Motivo, dataAlteracao),
            cancellationToken);
    }

    public Task<ResultadoProcessamentoEvento> ProcessarOrcamentoGeradoAsync(OrcamentoGerado evento, CancellationToken cancellationToken = default)
    {
        return ProcessarAsync(
            evento.OrdemServicoId,
            nameof(OrcamentoGerado),
            OrigensHistoricoSaga.Billing,
            OrdemServicoLogEventos.Etapas.OrcamentoGeracao,
            (ordemServico, dataAlteracao) => ordemServico.RegistrarOrcamentoGerado(evento.ValorTotal, dataAlteracao),
            cancellationToken);
    }

    public Task<ResultadoProcessamentoEvento> ProcessarPagamentoAprovadoAsync(PagamentoAprovado evento, CancellationToken cancellationToken = default)
    {
        // Mesma etapa do log de antes (orcamento_aprovacao): a aprovacao do orcamento agora se concretiza com o pagamento aprovado no Billing
        return ProcessarAsync(
            evento.OrdemServicoId,
            nameof(PagamentoAprovado),
            OrigensHistoricoSaga.Billing,
            OrdemServicoLogEventos.Etapas.OrcamentoAprovacao,
            (ordemServico, dataAlteracao) => ordemServico.RegistrarPagamentoAprovado(dataAlteracao),
            cancellationToken);
    }

    public Task<ResultadoProcessamentoEvento> ProcessarOrcamentoRecusadoAsync(OrcamentoRecusado evento, CancellationToken cancellationToken = default)
    {
        return ProcessarAsync(
            evento.OrdemServicoId,
            nameof(OrcamentoRecusado),
            OrigensHistoricoSaga.Billing,
            OrdemServicoLogEventos.Etapas.OrcamentoRecusa,
            (ordemServico, dataAlteracao) => ordemServico.RegistrarOrcamentoRecusado(dataAlteracao),
            cancellationToken);
    }

    public Task<ResultadoProcessamentoEvento> ProcessarDiagnosticoIniciadoAsync(DiagnosticoIniciado evento, CancellationToken cancellationToken = default)
    {
        return ProcessarAsync(
            evento.OrdemServicoId,
            nameof(DiagnosticoIniciado),
            OrigensHistoricoSaga.Execution,
            OrdemServicoLogEventos.Etapas.Diagnostico,
            (ordemServico, dataAlteracao) => ordemServico.AlterarParaEmDiagnostico(dataAlteracao),
            cancellationToken);
    }

    public Task<ResultadoProcessamentoEvento> ProcessarExecucaoFinalizadaAsync(ExecucaoFinalizada evento, CancellationToken cancellationToken = default)
    {
        return ProcessarAsync(
            evento.OrdemServicoId,
            nameof(ExecucaoFinalizada),
            OrigensHistoricoSaga.Execution,
            OrdemServicoLogEventos.Etapas.Finalizacao,
            (ordemServico, dataAlteracao) => ordemServico.AlterarParaFinalizada(dataAlteracao),
            cancellationToken);
    }

    // Tipo (nome do contrato) e origem (servico que publicou) identificam o evento no historico da saga
    private async Task<ResultadoProcessamentoEvento> ProcessarAsync(
        Guid ordemServicoId,
        string tipo,
        string origem,
        string etapa,
        Action<OrdemServico, DateTime> alteracao,
        CancellationToken cancellationToken)
    {
        var ordemServico = await _ordemServicoGateway.ObterPorIdAsync(ordemServicoId, cancellationToken);

        if (ordemServico is null)
        {
            _logger.LogInformation(
                "Evento da etapa {Etapa} ignorado: ordem de servico {OrdemServicoId} nao encontrada.",
                etapa,
                ordemServicoId);
            await _historicoSagaGateway.RegistrarAsync(
                new EventoHistoricoSaga
                {
                    OrdemServicoId = ordemServicoId,
                    OcorridoEm = DateTime.UtcNow,
                    Tipo = tipo,
                    Origem = origem,
                    Resultado = ResultadoProcessamentoEvento.Ignorado,
                    Motivo = "Ordem de servico nao encontrada."
                },
                CancellationToken.None);
            return ResultadoProcessamentoEvento.Ignorado;
        }

        var statusAnterior = ordemServico.Status;
        var dataEntradaStatusAnterior = ordemServico.HistoricoStatus.Max(historico => historico.DataAlteracao);
        var agora = DateTime.UtcNow;

        try
        {
            alteracao(ordemServico, agora);
        }
        catch (DomainException exception)
        {
            _logger.LogInformation(
                "Evento da etapa {Etapa} ignorado para a ordem de servico {OrdemServicoId} no status {StatusAtual}: {Motivo}",
                etapa,
                ordemServico.Id,
                OrdemServicoLogEventos.StatusParaLog(ordemServico.Status),
                exception.Message);
            // A transicao foi recusada antes de qualquer alteracao: a OS segue no status em que estava
            await _historicoSagaGateway.RegistrarAsync(
                new EventoHistoricoSaga
                {
                    OrdemServicoId = ordemServico.Id,
                    OcorridoEm = agora,
                    Tipo = tipo,
                    Origem = origem,
                    Resultado = ResultadoProcessamentoEvento.Ignorado,
                    StatusAnterior = (int)statusAnterior,
                    StatusNovo = (int)statusAnterior,
                    Motivo = exception.Message
                },
                CancellationToken.None);
            return ResultadoProcessamentoEvento.Ignorado;
        }

        await _ordemServicoGateway.AtualizarAsync(ordemServico, cancellationToken);

        // Orcamento regerado com a OS ja aguardando aprovacao: so o valor muda, sem mudanca de status para registrar ou publicar
        if (ordemServico.Status != statusAnterior)
        {
            _logger.RegistrarStatusAlterado(ordemServico, etapa, statusAnterior, dataEntradaStatusAnterior, agora);

            // Ja gravado: a publicacao nao e cancelada com o encerramento do consumidor, senao o evento se perderia
            // (a reentrega da mensagem seria ignorada, pois a transicao ja foi aplicada)
            await _eventosPublisher.PublicarAsync(
                new OrdemServicoStatusAlterado(ordemServico.Id, ordemServico.ClienteId, (int)statusAnterior, (int)ordemServico.Status),
                ordemServico.Id,
                CancellationToken.None);
        }

        // Depois da gravacao e da publicacao: se a publicacao falhar, a mensagem volta para a fila sem registro no historico
        await _historicoSagaGateway.RegistrarAsync(
            new EventoHistoricoSaga
            {
                OrdemServicoId = ordemServico.Id,
                OcorridoEm = agora,
                Tipo = tipo,
                Origem = origem,
                Resultado = ResultadoProcessamentoEvento.Processado,
                StatusAnterior = (int)statusAnterior,
                StatusNovo = (int)ordemServico.Status
            },
            CancellationToken.None);

        return ResultadoProcessamentoEvento.Processado;
    }
}
