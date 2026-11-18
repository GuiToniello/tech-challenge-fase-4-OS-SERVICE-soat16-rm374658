using Amazon.SQS;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TechChallenge.Oficina.OsService.Messaging.Configuration;
using TechChallenge.Oficina.OsService.Messaging.Features.Eventos;
using TechChallenge.Oficina.OsService.Messaging.Sqs;
using TechChallenge.Oficina.OsService.UseCases.Features.Eventos.Interfaces;
using Xunit;

namespace TechChallenge.Oficina.OsService.Messaging.Tests;

public sealed class ExtensionsTests
{
    private static SqsSettings CriarSettings(string filaConsumo) => new()
    {
        FilaConsumo = filaConsumo,
        FilasPublicacao = ["techchallenge-oficina-management.fifo", "techchallenge-oficina-billing.fifo"],
        ServiceUrl = "http://localhost:9325"
    };

    [Fact]
    public void AddInfraMessaging_DeveRegistrarPublisherEClienteSqsApontandoParaServiceUrl()
    {
        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        var settings = CriarSettings("techchallenge-oficina-os-service.fifo");

        services.AddInfraMessaging(settings);

        using var provider = services.BuildServiceProvider();
        Assert.IsType<EventosPublisher>(provider.GetRequiredService<IEventosPublisher>());
        Assert.IsType<SqsClientAdapter>(provider.GetRequiredService<ISqsClient>());
        Assert.Same(settings, provider.GetRequiredService<SqsSettings>());
        var clienteSqs = provider.GetRequiredService<IAmazonSQS>();
        Assert.StartsWith("http://localhost:9325", clienteSqs.Config.ServiceURL);
    }

    [Fact]
    public void AddInfraMessaging_DeveRegistrarConsumidor_QuandoFilaDeConsumoConfigurada()
    {
        var services = new ServiceCollection();

        services.AddInfraMessaging(CriarSettings("techchallenge-oficina-os-service.fifo"));

        Assert.Contains(services, descritor => descritor.ServiceType == typeof(IHostedService) && descritor.ImplementationType == typeof(SqsConsumidorHostedService));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AddInfraMessaging_NaoDeveRegistrarConsumidor_QuandoFilaDeConsumoVazia(string filaConsumo)
    {
        var services = new ServiceCollection();

        services.AddInfraMessaging(CriarSettings(filaConsumo));

        Assert.DoesNotContain(services, descritor => descritor.ServiceType == typeof(IHostedService));
        Assert.Contains(services, descritor => descritor.ServiceType == typeof(IEventosPublisher));
    }
}
