using System.Text.Json;
using Moq;
using TechChallenge.Oficina.OsService.Controllers.Features.Eventos;
using TechChallenge.Oficina.OsService.UseCases.Features.Eventos;
using TechChallenge.Oficina.OsService.UseCases.Features.Eventos.Contratos;
using TechChallenge.Oficina.OsService.UseCases.Features.Eventos.UseCases;
using Xunit;

namespace TechChallenge.Oficina.OsService.Controllers.Tests.Features.Eventos;

public sealed class EventosControllerTests
{
    private readonly Mock<IOrdemServicoEventosUseCases> _useCasesMock = new();
    private readonly Guid _ordemServicoId = Guid.NewGuid();

    private EventosController CriarController() => new(_useCasesMock.Object);

    [Fact]
    public async Task ProcessarAsync_DeveDespacharOrdemServicoRejeitada_ComDadosEmCamelCase()
    {
        _useCasesMock.Setup(useCases => useCases.ProcessarRejeitadaAsync(It.IsAny<OrdemServicoRejeitada>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResultadoProcessamentoEvento.Processado);

        var resultado = await CriarController().ProcessarAsync(
            "OrdemServicoRejeitada",
            $$"""{"ordemServicoId":"{{_ordemServicoId}}","motivo":"Cliente nao encontrado."}""",
            CancellationToken.None);

        Assert.Equal(ResultadoProcessamentoEvento.Processado, resultado);
        _useCasesMock.Verify(useCases => useCases.ProcessarRejeitadaAsync(
            It.Is<OrdemServicoRejeitada>(evento => evento.OrdemServicoId == _ordemServicoId && evento.Motivo == "Cliente nao encontrado."),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessarAsync_DeveDespacharOrcamentoGerado_ComValorTotal()
    {
        _useCasesMock.Setup(useCases => useCases.ProcessarOrcamentoGeradoAsync(It.IsAny<OrcamentoGerado>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResultadoProcessamentoEvento.Processado);

        var resultado = await CriarController().ProcessarAsync(
            "OrcamentoGerado",
            $$"""{"ordemServicoId":"{{_ordemServicoId}}","valorTotal":389.90}""",
            CancellationToken.None);

        Assert.Equal(ResultadoProcessamentoEvento.Processado, resultado);
        _useCasesMock.Verify(useCases => useCases.ProcessarOrcamentoGeradoAsync(
            It.Is<OrcamentoGerado>(evento => evento.OrdemServicoId == _ordemServicoId && evento.ValorTotal == 389.90m),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessarAsync_DeveDespacharPagamentoAprovado_ComPagamentoEValor()
    {
        var pagamentoId = Guid.NewGuid();
        _useCasesMock.Setup(useCases => useCases.ProcessarPagamentoAprovadoAsync(It.IsAny<PagamentoAprovado>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResultadoProcessamentoEvento.Processado);

        var resultado = await CriarController().ProcessarAsync(
            "PagamentoAprovado",
            $$"""{"ordemServicoId":"{{_ordemServicoId}}","pagamentoId":"{{pagamentoId}}","valor":389.90}""",
            CancellationToken.None);

        Assert.Equal(ResultadoProcessamentoEvento.Processado, resultado);
        _useCasesMock.Verify(useCases => useCases.ProcessarPagamentoAprovadoAsync(
            It.Is<PagamentoAprovado>(evento => evento.OrdemServicoId == _ordemServicoId && evento.PagamentoId == pagamentoId && evento.Valor == 389.90m),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessarAsync_DeveDespacharOrcamentoRecusado()
    {
        _useCasesMock.Setup(useCases => useCases.ProcessarOrcamentoRecusadoAsync(It.IsAny<OrcamentoRecusado>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResultadoProcessamentoEvento.Processado);

        var resultado = await CriarController().ProcessarAsync("OrcamentoRecusado", $$"""{"ordemServicoId":"{{_ordemServicoId}}"}""", CancellationToken.None);

        Assert.Equal(ResultadoProcessamentoEvento.Processado, resultado);
        _useCasesMock.Verify(useCases => useCases.ProcessarOrcamentoRecusadoAsync(It.Is<OrcamentoRecusado>(evento => evento.OrdemServicoId == _ordemServicoId), It.IsAny<CancellationToken>()), Times.Once);
    }

    // OrcamentoAprovado saiu do contrato (a OS so entra em execucao com o PagamentoAprovado); OrdemServicoValidada e de interesse do Billing
    [Theory]
    [InlineData("OrdemServicoAberta")]
    [InlineData("OrdemServicoValidada")]
    [InlineData("OrcamentoAprovado")]
    [InlineData("EventoDesconhecido")]
    [InlineData("")]
    public async Task ProcessarAsync_DeveIgnorarSemChamarUseCases_QuandoTipoNaoTratado(string tipo)
    {
        var resultado = await CriarController().ProcessarAsync(tipo, $$"""{"ordemServicoId":"{{_ordemServicoId}}"}""", CancellationToken.None);

        Assert.Equal(ResultadoProcessamentoEvento.Ignorado, resultado);
        Assert.Empty(_useCasesMock.Invocations);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("""{"ordemServicoId":"nao-e-guid"}""")]
    [InlineData("nao e json")]
    public async Task ProcessarAsync_DeveLancarJsonException_QuandoDadosInvalidos(string dadosJson)
    {
        var action = async () => await CriarController().ProcessarAsync("PagamentoAprovado", dadosJson, CancellationToken.None);

        await Assert.ThrowsAsync<JsonException>(action);
        Assert.Empty(_useCasesMock.Invocations);
    }

    [Fact]
    public async Task ProcessarAsync_DevePropagarExcecaoDoUseCase()
    {
        _useCasesMock.Setup(useCases => useCases.ProcessarPagamentoAprovadoAsync(It.IsAny<PagamentoAprovado>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Banco indisponivel"));

        var action = async () => await CriarController().ProcessarAsync(
            "PagamentoAprovado",
            $$"""{"ordemServicoId":"{{_ordemServicoId}}","pagamentoId":"{{Guid.NewGuid()}}","valor":100}""",
            CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(action);
    }
}
