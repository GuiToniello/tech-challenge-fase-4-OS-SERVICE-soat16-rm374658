using Microsoft.AspNetCore.Http.HttpResults;
using Moq;
using TechChallenge.Oficina.OsService.Entities.Features.OrdensServico.Enums;
using TechChallenge.Oficina.OsService.Integration.Tests.Infrastructure;
using TechChallenge.Oficina.OsService.UseCases.Features.Eventos;
using TechChallenge.Oficina.OsService.UseCases.Features.Eventos.Contratos;
using TechChallenge.Oficina.OsService.UseCases.Features.OrdensServico.Commands;
using TechChallenge.Oficina.OsService.UseCases.Features.OrdensServico.ViewModels;
using Xunit;

namespace TechChallenge.Oficina.OsService.Integration.Tests.Features.Eventos;

// Saga do lado do OS Service: eventos da Management (JSON como chegam da fila) aplicados pela EventosController
public sealed class EventosIntegrationTests : IDisposable
{
    private readonly IntegrationTestFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    private async Task<Guid> CriarOrdemAsync()
    {
        var command = new CriarOrdemServicoCommand { ClienteId = Guid.NewGuid(), VeiculoId = Guid.NewGuid(), ServicoIds = [Guid.NewGuid()] };
        var created = Assert.IsType<CreatedAtRoute<OrdemServicoViewModel>>(await _fixture.CriarOrdensServicoEndpoints().Post(command, CancellationToken.None));
        return created.Value!.Id;
    }

    private Task<ResultadoProcessamentoEvento> ProcessarAsync(string tipo, string dadosJson) =>
        _fixture.CriarEventosController().ProcessarAsync(tipo, dadosJson, CancellationToken.None);

    private async Task<AcompanhamentoOrdemServicoViewModel> ObterAcompanhamentoAsync(Guid ordemServicoId)
    {
        var ok = Assert.IsType<Ok<AcompanhamentoOrdemServicoViewModel>>(await _fixture.CriarOrdensServicoEndpoints().GetAcompanhamento(ordemServicoId, CancellationToken.None));
        return ok.Value!;
    }

    [Fact]
    public async Task OrcamentoGeradoEAprovado_DeveLevarOrdemParaEmExecucaoComValorEHistorico()
    {
        var ordemId = await CriarOrdemAsync();

        Assert.Equal(ResultadoProcessamentoEvento.Processado, await ProcessarAsync("OrcamentoGerado", $$"""{"ordemServicoId":"{{ordemId}}","valorTotal":389.90}"""));
        var aguardando = await ObterAcompanhamentoAsync(ordemId);
        Assert.Equal((int)StatusOrdemServico.AguardandoAprovacao, aguardando.Status);
        Assert.Equal(389.90m, aguardando.ValorOrcamento);

        Assert.Equal(ResultadoProcessamentoEvento.Processado, await ProcessarAsync("OrcamentoAprovado", $$"""{"ordemServicoId":"{{ordemId}}"}"""));
        var emExecucao = await ObterAcompanhamentoAsync(ordemId);
        Assert.Equal((int)StatusOrdemServico.EmExecucao, emExecucao.Status);
        Assert.Equal(
            [StatusOrdemServico.Recebida, StatusOrdemServico.AguardandoAprovacao, StatusOrdemServico.EmExecucao],
            emExecucao.HistoricoStatus.Select(historico => historico.Status));

        _fixture.EventosPublisherMock.Verify(p => p.PublicarAsync(
            It.Is<OrdemServicoStatusAlterado>(evento => evento.StatusAnterior == 1 && evento.StatusNovo == 3),
            ordemId,
            It.IsAny<CancellationToken>()), Times.Once);
        _fixture.EventosPublisherMock.Verify(p => p.PublicarAsync(
            It.Is<OrdemServicoStatusAlterado>(evento => evento.StatusAnterior == 3 && evento.StatusNovo == 4),
            ordemId,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OrcamentoRecusado_DeveFinalizarOrdem()
    {
        var ordemId = await CriarOrdemAsync();
        await ProcessarAsync("OrcamentoGerado", $$"""{"ordemServicoId":"{{ordemId}}","valorTotal":100}""");

        var resultado = await ProcessarAsync("OrcamentoRecusado", $$"""{"ordemServicoId":"{{ordemId}}"}""");

        Assert.Equal(ResultadoProcessamentoEvento.Processado, resultado);
        Assert.Equal((int)StatusOrdemServico.Finalizada, (await ObterAcompanhamentoAsync(ordemId)).Status);
    }

    [Fact]
    public async Task OrdemServicoRejeitada_DeveCancelarOrdemComMotivo()
    {
        var ordemId = await CriarOrdemAsync();

        var resultado = await ProcessarAsync("OrdemServicoRejeitada", $$"""{"ordemServicoId":"{{ordemId}}","motivo":"Cliente nao encontrado."}""");

        Assert.Equal(ResultadoProcessamentoEvento.Processado, resultado);
        var cancelada = await ObterAcompanhamentoAsync(ordemId);
        Assert.Equal((int)StatusOrdemServico.Cancelada, cancelada.Status);
        Assert.Equal("Cancelada", cancelada.StatusDescricao);
        Assert.Equal("Cliente nao encontrado.", cancelada.MotivoCancelamento);
        _fixture.EventosPublisherMock.Verify(p => p.PublicarAsync(
            It.Is<OrdemServicoStatusAlterado>(evento => evento.StatusAnterior == 1 && evento.StatusNovo == 8),
            ordemId,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EventoRepetido_DeveSerIgnoradoSemNovaPublicacao()
    {
        var ordemId = await CriarOrdemAsync();
        await ProcessarAsync("OrcamentoGerado", $$"""{"ordemServicoId":"{{ordemId}}","valorTotal":100}""");
        await ProcessarAsync("OrcamentoAprovado", $$"""{"ordemServicoId":"{{ordemId}}"}""");

        var repetido = await ProcessarAsync("OrcamentoAprovado", $$"""{"ordemServicoId":"{{ordemId}}"}""");

        Assert.Equal(ResultadoProcessamentoEvento.Ignorado, repetido);
        Assert.Equal(3, (await ObterAcompanhamentoAsync(ordemId)).HistoricoStatus.Count);
        _fixture.EventosPublisherMock.Verify(p => p.PublicarAsync(It.IsAny<OrdemServicoStatusAlterado>(), ordemId, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task EventoDeOrdemInexistente_DeveSerIgnorado()
    {
        var resultado = await ProcessarAsync("OrdemServicoRejeitada", $$"""{"ordemServicoId":"{{Guid.NewGuid()}}","motivo":"Cliente nao encontrado."}""");

        Assert.Equal(ResultadoProcessamentoEvento.Ignorado, resultado);
    }

    [Fact]
    public async Task OrdemCancelada_NaoDeveAceitarMudancaDeStatusPeloHttp()
    {
        var ordemId = await CriarOrdemAsync();
        await ProcessarAsync("OrdemServicoRejeitada", $$"""{"ordemServicoId":"{{ordemId}}","motivo":"Veiculo nao encontrado."}""");

        Assert.IsType<BadRequest<Dictionary<string, string?>>>(await _fixture.CriarOrdensServicoEndpoints().AlterarParaEmDiagnostico(ordemId, CancellationToken.None));
    }
}
