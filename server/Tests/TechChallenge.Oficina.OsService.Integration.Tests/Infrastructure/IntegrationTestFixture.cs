using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using TechChallenge.Oficina.OsService.Adapters.Features.Indicadores;
using TechChallenge.Oficina.OsService.Adapters.Features.OrdensServico;
using TechChallenge.Oficina.OsService.Controllers.Features.Eventos;
using TechChallenge.Oficina.OsService.Controllers.Features.Indicadores;
using TechChallenge.Oficina.OsService.Controllers.Features.OrdensServico;
using TechChallenge.Oficina.OsService.DB.Data.Context;
using TechChallenge.Oficina.OsService.DB.Data.Features.Indicadores;
using TechChallenge.Oficina.OsService.DB.Data.Features.OrdensServico;
using TechChallenge.Oficina.OsService.UseCases.Features.Eventos.Interfaces;
using TechChallenge.Oficina.OsService.UseCases.Features.Eventos.UseCases;
using TechChallenge.Oficina.OsService.UseCases.Features.Indicadores.UseCases;
using TechChallenge.Oficina.OsService.UseCases.Features.OrdensServico.Mappings;
using TechChallenge.Oficina.OsService.UseCases.Features.OrdensServico.UseCases;

namespace TechChallenge.Oficina.OsService.Integration.Tests.Infrastructure;

/// <summary>
/// Fixture de integração: DI real com EF Core InMemory, mock apenas de IEventosPublisher (a fila SQS) e o histórico da saga (DynamoDB) em memória.
/// Cada instância possui banco de dados isolado (nome único por GUID) e cada controller é obtido de um escopo novo, como numa requisição.
/// </summary>
public sealed class IntegrationTestFixture : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly List<IServiceScope> _escopos = [];

    public IntegrationTestFixture()
    {
        var services = new ServiceCollection();

        services.AddLogging();

        var dbName = Guid.NewGuid().ToString();
        services.AddDbContext<OsServiceDbContext>(options => options.UseInMemoryDatabase(dbName));

        services.AddScoped<IIndicadorGateway, IndicadorGateway>();
        services.AddScoped<IOrdemServicoGateway, OrdemServicoGateway>();

        EventosPublisherMock
            .Setup(p => p.PublicarAsync(It.IsAny<It.IsAnyType>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        services.AddSingleton(EventosPublisherMock.Object);
        services.AddSingleton<IHistoricoSagaGateway>(HistoricoSaga);

        services.AddAutoMapper(_ => { }, typeof(OrdemServicoProfile).Assembly);
        services.AddScoped<IIndicadorUseCases, IndicadorUseCases>();
        services.AddScoped<IOrdemServicoUseCases, OrdemServicoUseCases>();
        services.AddScoped<IOrdemServicoEventosUseCases, OrdemServicoEventosUseCases>();
        services.AddScoped<IOrdensServicoAdapter, OrdensServicoAdapter>();
        services.AddScoped<OrdensServicoController>();
        services.AddScoped<IIndicadoresAdapter, IndicadoresAdapter>();
        services.AddScoped<IIndicadoresController, IndicadoresController>();
        services.AddScoped<IEventosController, EventosController>();

        _serviceProvider = services.BuildServiceProvider();
    }

    public Mock<IEventosPublisher> EventosPublisherMock { get; } = new();
    public HistoricoSagaEmMemoria HistoricoSaga { get; } = new();

    private T Obter<T>() where T : notnull
    {
        var escopo = _serviceProvider.CreateScope();
        _escopos.Add(escopo);
        return escopo.ServiceProvider.GetRequiredService<T>();
    }

    public OrdensServicoController CriarOrdensServicoEndpoints()
        => Obter<OrdensServicoController>();

    public IIndicadoresController CriarIndicadoresEndpoints()
        => Obter<IIndicadoresController>();

    public IEventosController CriarEventosController()
        => Obter<IEventosController>();

    public void Dispose()
    {
        foreach (var escopo in _escopos)
        {
            escopo.Dispose();
        }

        _serviceProvider.Dispose();
    }
}
