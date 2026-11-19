using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TechChallenge.Oficina.OsService.Messaging.Configuration;
using TechChallenge.Oficina.OsService.Messaging.Sqs;
using TechChallenge.Oficina.OsService.UseCases.Features.Eventos.Interfaces;
using TechChallenge.Oficina.OsService.UseCases.Features.OrdensServico.Logging;

namespace TechChallenge.Oficina.OsService.Messaging.Features.Eventos;

// Fan-out: o envelope e montado uma vez e o mesmo (mesmo id) vai para cada fila de FilasPublicacao (Management, Billing e Execucao).
public sealed class EventosPublisher : IEventosPublisher
{
    public const string Origem = "os-service";
    public const int Versao = 1;

    private readonly ISqsClient _sqsClient;
    private readonly SqsSettings _sqsSettings;
    private readonly ILogger<EventosPublisher> _logger;

    // A URL de cada fila nao muda durante a execucao: e resolvida uma vez e reaproveitada (o publicador e singleton e atende chamadas em paralelo)
    private readonly ConcurrentDictionary<string, string> _urlsFilas = new();

    public EventosPublisher(ISqsClient sqsClient, SqsSettings sqsSettings, ILogger<EventosPublisher> logger)
    {
        _sqsClient = sqsClient;
        _sqsSettings = sqsSettings;
        _logger = logger;
    }

    public async Task PublicarAsync<TEvento>(TEvento evento, Guid ordemServicoId, CancellationToken cancellationToken = default) where TEvento : class
    {
        var envelope = new EnvelopeEvento<TEvento>(Guid.NewGuid(), typeof(TEvento).Name, Versao, DateTime.UtcNow, Origem, evento);
        var corpo = JsonSerializer.Serialize(envelope, SerializacaoEventos.Opcoes);

        // Uma fila por vez: a falha em uma interrompe a publicacao e sobe para quem chamou
        foreach (var fila in _sqsSettings.FilasPublicacao)
        {
            await PublicarNaFilaAsync(fila, envelope.Id, envelope.Tipo, corpo, ordemServicoId, cancellationToken);
        }
    }

    private async Task PublicarNaFilaAsync(string fila, Guid eventoId, string tipo, string corpo, Guid ordemServicoId, CancellationToken cancellationToken)
    {
        try
        {
            var urlFila = await ObterUrlFilaAsync(fila, cancellationToken);

            // Grupo = ordem de servico (ordem garantida por OS na fila FIFO); deduplicacao = id do envelope
            await _sqsClient.SendAsync(
                urlFila,
                corpo,
                tipo,
                ordemServicoId.ToString("D"),
                eventoId.ToString("D"),
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(
                OrdemServicoLogEventos.IntegracaoFalha,
                exception,
                "Falha na integracao {Integracao} durante a operacao {Operacao} do evento {Tipo} da ordem de servico {OrdemServicoId} na fila {Fila}.",
                OrdemServicoLogEventos.Integracoes.Sqs,
                OrdemServicoLogEventos.Operacoes.Publicacao,
                tipo,
                ordemServicoId,
                fila);
            throw;
        }

        _logger.LogInformation(
            OrdemServicoLogEventos.EventoPublicado,
            "Evento {Tipo} {EventoId} publicado na fila {Fila} para a ordem de servico {OrdemServicoId}.",
            tipo,
            eventoId,
            fila,
            ordemServicoId);
    }

    private async Task<string> ObterUrlFilaAsync(string fila, CancellationToken cancellationToken)
    {
        if (!_urlsFilas.TryGetValue(fila, out var urlFila))
        {
            urlFila = await _sqsClient.GetQueueUrlAsync(fila, cancellationToken);
            _urlsFilas[fila] = urlFila;
        }

        return urlFila;
    }
}
