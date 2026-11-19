using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using TechChallenge.Oficina.OsService.API.Features.Indicadores;
using TechChallenge.Oficina.OsService.API.Features.OrdensServico;
using TechChallenge.Oficina.OsService.Controllers.Features.Indicadores;
using TechChallenge.Oficina.OsService.Controllers.Features.OrdensServico;
using Xunit;

namespace TechChallenge.Oficina.OsService.API.Tests.Features;

public sealed class RotasTests
{
    private static List<RouteEndpoint> MapearRotas()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddScoped(_ => Mock.Of<IOrdensServicoController>());
        builder.Services.AddScoped(_ => Mock.Of<IIndicadoresController>());
        var app = builder.Build();

        app.MapOrdensServicoEndpoints();
        app.MapIndicadoresEndpoints();

        return ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(fonte => fonte.Endpoints)
            .OfType<RouteEndpoint>()
            .ToList();
    }

    [Fact]
    public void Endpoints_DevemMapearAsRotasDeOrdensServicoEIndicadores()
    {
        // Rotas do grupo com padrao vazio terminam em "/" no RawText; o roteamento atende com e sem a barra final
        var rotas = MapearRotas()
            .Select(endpoint => $"{endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()!.HttpMethods.Single()} {endpoint.RoutePattern.RawText!.TrimEnd('/')}")
            .OrderBy(rota => rota)
            .ToArray();

        Assert.Equal(
            new[]
            {
                "DELETE /api/ordens-servico/{id:guid}",
                "GET /api/indicadores",
                "GET /api/ordens-servico",
                "GET /api/ordens-servico/cliente/{clienteId:guid}",
                "GET /api/ordens-servico/ordenadas",
                "GET /api/ordens-servico/{id:guid}",
                "GET /api/ordens-servico/{id:guid}/acompanhamento",
                "POST /api/ordens-servico",
                "POST /api/ordens-servico/{id:guid}/entregar",
                "PUT /api/ordens-servico"
            }.OrderBy(rota => rota),
            rotas);
    }

    [Fact]
    public void GetById_DeveTerORotaNomeadaUsadaNoCreatedAtRoute()
    {
        var endpoint = Assert.Single(MapearRotas(), rota => rota.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName == "GetOrdemServicoById");

        Assert.Equal("/api/ordens-servico/{id:guid}", endpoint.RoutePattern.RawText);
    }
}
