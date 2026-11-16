using System.Text.Json;
using Microsoft.Extensions.Logging;
using TechChallenge.Oficina.OsService.Messaging.Configuration;
using TechChallenge.Oficina.OsService.Messaging.Sqs;
using TechChallenge.Oficina.OsService.UseCases.Features.Eventos.Interfaces;
using TechChallenge.Oficina.OsService.UseCases.Features.OrdensServico.Logging;

namespace TechChallenge.Oficina.OsService.Messaging.Features.Eventos;

public sealed class EventosPublisher : IEventosPublisher
{
    public const string Origem = "os-service";
    public const int Versao = 1;

    private readonly ISqsClient _sqsClient;
    private readonly SqsSettings _sqsSettings;
    private readonly ILogger<EventosPublisher> _logger;
    private string? _urlFilaPublicacao;

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

        try
        {
            _urlFilaPublicacao ??= await _sqsClient.GetQueueUrlAsync(_sqsSettings.FilaPublicacao, cancellationToken);

            // Grupo = ordem de servico (ordem garantida por OS na fila FIFO); deduplicacao = id do envelope
            await _sqsClient.SendAsync(
                _urlFilaPublicacao,
                corpo,
                envelope.Tipo,
                ordemServicoId.ToString("D"),
                envelope.Id.ToString("D"),
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(
                OrdemServicoLogEventos.IntegracaoFalha,
                exception,
                "Falha na integracao {Integracao} durante a operacao {Operacao} do evento {Tipo} da ordem de servico {OrdemServicoId}.",
                OrdemServicoLogEventos.Integracoes.Sqs,
                OrdemServicoLogEventos.Operacoes.Publicacao,
                envelope.Tipo,
                ordemServicoId);
            throw;
        }

        _logger.LogInformation(
            OrdemServicoLogEventos.EventoPublicado,
            "Evento {Tipo} {EventoId} publicado na fila {Fila} para a ordem de servico {OrdemServicoId}.",
            envelope.Tipo,
            envelope.Id,
            _sqsSettings.FilaPublicacao,
            ordemServicoId);
    }
}
