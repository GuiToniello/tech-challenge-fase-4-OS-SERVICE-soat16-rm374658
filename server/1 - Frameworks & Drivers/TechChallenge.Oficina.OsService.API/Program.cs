using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using TechChallenge.Oficina.OsService.Adapters.Features.Indicadores;
using TechChallenge.Oficina.OsService.Adapters.Features.OrdensServico;
using TechChallenge.Oficina.OsService.API.Extensions;
using TechChallenge.Oficina.OsService.API.Features.Indicadores;
using TechChallenge.Oficina.OsService.API.Features.OrdensServico;
using TechChallenge.Oficina.OsService.API.Middleware;
using TechChallenge.Oficina.OsService.API.Settings;
using TechChallenge.Oficina.OsService.Controllers.Features.Eventos;
using TechChallenge.Oficina.OsService.Controllers.Features.Indicadores;
using TechChallenge.Oficina.OsService.Controllers.Features.OrdensServico;
using TechChallenge.Oficina.OsService.DB.Data;
using TechChallenge.Oficina.OsService.Messaging;
using TechChallenge.Oficina.OsService.Messaging.Configuration;
using TechChallenge.Oficina.OsService.UseCases.Features.Eventos.UseCases;
using TechChallenge.Oficina.OsService.UseCases.Features.Indicadores.UseCases;
using TechChallenge.Oficina.OsService.UseCases.Features.OrdensServico.Mappings;
using TechChallenge.Oficina.OsService.UseCases.Features.OrdensServico.UseCases;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddLoggingConfiguration(builder.Configuration);
builder.Services.AddSwaggerConfiguration();
builder.Services.AddAuthConfiguration(builder.Configuration);

var databaseSettings = builder.Configuration
    .GetSection(DatabaseSettings.SectionName)
    .Get<DatabaseSettings>()
    ?? throw new InvalidOperationException("A configuração de banco de dados é obrigatória.");

if (string.IsNullOrWhiteSpace(databaseSettings.ConnectionString))
{
    throw new InvalidOperationException("A connection string do banco de dados é obrigatória.");
}

var sqsSettings = builder.Configuration
    .GetSection(SqsSettings.SectionName)
    .Get<SqsSettings>()
    ?? new SqsSettings();

// Fan-out: cada evento da OS é publicado em todas as filas da lista (Management e Billing)
if (sqsSettings.FilasPublicacao.Length == 0 || sqsSettings.FilasPublicacao.Any(string.IsNullOrWhiteSpace))
{
    throw new InvalidOperationException("SqsSettings:FilasPublicacao é obrigatório: informe ao menos uma fila, sem itens vazios.");
}

builder.Services.AddAutoMapper(_ => { }, typeof(OrdemServicoProfile).Assembly);
builder.Services.AddScoped<IIndicadorUseCases, IndicadorUseCases>();
builder.Services.AddScoped<IOrdemServicoUseCases, OrdemServicoUseCases>();
builder.Services.AddScoped<IOrdemServicoEventosUseCases, OrdemServicoEventosUseCases>();
builder.Services.AddInfraData(databaseSettings.ConnectionString);
builder.Services.AddInfraMessaging(sqsSettings);

builder.Services.AddScoped<IOrdensServicoController, OrdensServicoController>();
builder.Services.AddScoped<IOrdensServicoAdapter, OrdensServicoAdapter>();
builder.Services.AddScoped<IIndicadoresAdapter, IndicadoresAdapter>();
builder.Services.AddScoped<IIndicadoresController, IndicadoresController>();
builder.Services.AddScoped<IEventosController, EventosController>();

var app = builder.Build();

try
{
    app.Services.ApplyMigrations();
}
catch (Exception ex)
{
    app.Logger.LogCritical(new EventId(9002, "app.migracao_falhou"), ex, "Falha ao aplicar as migrations do banco");
    Environment.ExitCode = 1;
    return;
}

app.UseLoggingConfiguration();
app.UseMiddleware<GlobalExceptionHandlerMiddleware>();

app.UseSwaggerConfiguration();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") }).AllowAnonymous();
app.MapIndicadoresEndpoints();
app.MapOrdensServicoEndpoints();

await app.RunAsync();
