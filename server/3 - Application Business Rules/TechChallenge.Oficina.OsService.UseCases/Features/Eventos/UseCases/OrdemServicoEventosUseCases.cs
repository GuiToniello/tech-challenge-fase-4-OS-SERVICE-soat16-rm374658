using Microsoft.Extensions.Logging;
using TechChallenge.Oficina.OsService.Entities.Exceptions;
using TechChallenge.Oficina.OsService.Entities.Features.OrdensServico;
using TechChallenge.Oficina.OsService.UseCases.Features.Eventos.Contratos;
using TechChallenge.Oficina.OsService.UseCases.Features.Eventos.Interfaces;
using TechChallenge.Oficina.OsService.UseCases.Features.OrdensServico.Logging;
using TechChallenge.Oficina.OsService.UseCases.Features.OrdensServico.UseCases;

namespace TechChallenge.Oficina.OsService.UseCases.Features.Eventos.UseCases;

// Processa os eventos da Management sobre a OS (saga coreografada). E idempotente: a fila entrega pelo menos uma vez,
// entao OS inexistente ou transicao nao permitida (evento repetido ou fora de ordem) resulta em Ignorado, sem excecao e sem publicar nada.
public sealed class OrdemServicoEventosUseCases : IOrdemServicoEventosUseCases
{
    private readonly IOrdemServicoGateway _ordemServicoGateway;
    private readonly IEventosPublisher _eventosPublisher;
    private readonly ILogger<OrdemServicoEventosUseCases> _logger;

    public OrdemServicoEventosUseCases(
        IOrdemServicoGateway ordemServicoGateway,
        IEventosPublisher eventosPublisher,
        ILogger<OrdemServicoEventosUseCases> logger)
    {
        _ordemServicoGateway = ordemServicoGateway;
        _eventosPublisher = eventosPublisher;
        _logger = logger;
    }

    public Task<ResultadoProcessamentoEvento> ProcessarRejeitadaAsync(OrdemServicoRejeitada evento, CancellationToken cancellationToken = default)
    {
        return ProcessarAsync(
            evento.OrdemServicoId,
            OrdemServicoLogEventos.Etapas.Cancelamento,
            (ordemServico, dataAlteracao) => ordemServico.Cancelar(evento.Motivo, dataAlteracao),
            cancellationToken);
    }

    public Task<ResultadoProcessamentoEvento> ProcessarOrcamentoGeradoAsync(OrcamentoGerado evento, CancellationToken cancellationToken = default)
    {
        return ProcessarAsync(
            evento.OrdemServicoId,
            OrdemServicoLogEventos.Etapas.OrcamentoGeracao,
            (ordemServico, dataAlteracao) => ordemServico.RegistrarOrcamentoGerado(evento.ValorTotal, dataAlteracao),
            cancellationToken);
    }

    public Task<ResultadoProcessamentoEvento> ProcessarOrcamentoAprovadoAsync(OrcamentoAprovado evento, CancellationToken cancellationToken = default)
    {
        return ProcessarAsync(
            evento.OrdemServicoId,
            OrdemServicoLogEventos.Etapas.OrcamentoAprovacao,
            (ordemServico, dataAlteracao) => ordemServico.RegistrarOrcamentoAprovado(dataAlteracao),
            cancellationToken);
    }

    public Task<ResultadoProcessamentoEvento> ProcessarOrcamentoRecusadoAsync(OrcamentoRecusado evento, CancellationToken cancellationToken = default)
    {
        return ProcessarAsync(
            evento.OrdemServicoId,
            OrdemServicoLogEventos.Etapas.OrcamentoRecusa,
            (ordemServico, dataAlteracao) => ordemServico.RegistrarOrcamentoRecusado(dataAlteracao),
            cancellationToken);
    }

    private async Task<ResultadoProcessamentoEvento> ProcessarAsync(
        Guid ordemServicoId,
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

        return ResultadoProcessamentoEvento.Processado;
    }
}
