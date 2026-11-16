using Amazon.SQS;
using Microsoft.Extensions.DependencyInjection;
using TechChallenge.Oficina.OsService.Messaging.Configuration;
using TechChallenge.Oficina.OsService.Messaging.Features.Eventos;
using TechChallenge.Oficina.OsService.Messaging.Sqs;
using TechChallenge.Oficina.OsService.UseCases.Features.Eventos.Interfaces;

namespace TechChallenge.Oficina.OsService.Messaging;

public static class Extensions
{
    public static IServiceCollection AddInfraMessaging(this IServiceCollection services, SqsSettings sqsSettings)
    {
        services.AddSingleton(sqsSettings);
        services.AddSingleton<IAmazonSQS>(_ => CriarClienteSqs(sqsSettings));
        services.AddSingleton<ISqsClient, SqsClientAdapter>();
        services.AddSingleton<IEventosPublisher, EventosPublisher>();

        // Sem fila de consumo o servico apenas publica
        if (!string.IsNullOrWhiteSpace(sqsSettings.FilaConsumo))
        {
            services.AddHostedService<SqsConsumidorHostedService>();
        }

        return services;
    }

    private static AmazonSQSClient CriarClienteSqs(SqsSettings sqsSettings)
    {
        // Local: ServiceUrl aponta para o ElasticMQ (credenciais ficticias via AWS_ACCESS_KEY_ID/AWS_SECRET_ACCESS_KEY).
        // Na AWS fica vazio: regiao (AWS_REGION) e credenciais vem do ambiente do pod.
        if (!string.IsNullOrWhiteSpace(sqsSettings.ServiceUrl))
        {
            return new AmazonSQSClient(new AmazonSQSConfig { ServiceURL = sqsSettings.ServiceUrl });
        }

        return new AmazonSQSClient();
    }
}
