using Microsoft.AspNetCore.Http.HttpResults;
using Moq;
using TechChallenge.Oficina.OsService.Entities.Features.OrdensServico.Enums;
using TechChallenge.Oficina.OsService.Integration.Tests.Infrastructure;
using TechChallenge.Oficina.OsService.UseCases.Features.Eventos;
using TechChallenge.Oficina.OsService.UseCases.Features.Eventos.Contratos;
using TechChallenge.Oficina.OsService.UseCases.Features.OrdensServico.Commands;
using TechChallenge.Oficina.OsService.UseCases.Features.OrdensServico.ViewModels;
using Xunit;

namespace TechChallenge.Oficina.OsService.Integration.Tests.Features.OrdensServico;

public sealed class OrdensServicoControllerIntegrationTests : IDisposable
{
    private readonly IntegrationTestFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    private async Task<OrdemServicoViewModel> CriarOrdemAsync(Guid? clienteId = null)
    {
        var command = new CriarOrdemServicoCommand { ClienteId = clienteId ?? Guid.NewGuid(), VeiculoId = Guid.NewGuid(), ServicoIds = [Guid.NewGuid()] };
        var created = Assert.IsType<CreatedAtRoute<OrdemServicoViewModel>>(await _fixture.CriarOrdensServicoEndpoints().Post(command, CancellationToken.None));
        return created.Value!;
    }

    // Diagnostico e finalizacao chegam por evento da Execucao e Producao; orcamento e pagamento, por evento do Billing (JSON como chegam da fila)
    private async Task ProcessarEventoAsync(string tipo, string dadosJson)
    {
        var resultado = await _fixture.CriarEventosController().ProcessarAsync(tipo, dadosJson, CancellationToken.None);
        Assert.Equal(ResultadoProcessamentoEvento.Processado, resultado);
    }

    private Task IniciarDiagnosticoAsync(Guid ordemId) =>
        ProcessarEventoAsync("DiagnosticoIniciado", $$"""{"ordemServicoId":"{{ordemId}}"}""");

    private async Task AprovarOrcamentoEPagamentoAsync(Guid ordemId)
    {
        await ProcessarEventoAsync("OrcamentoGerado", $$"""{"ordemServicoId":"{{ordemId}}","valorTotal":100}""");
        await ProcessarEventoAsync("PagamentoAprovado", $$"""{"ordemServicoId":"{{ordemId}}","pagamentoId":"{{Guid.NewGuid()}}","valor":100}""");
    }

    private Task FinalizarExecucaoAsync(Guid ordemId) =>
        ProcessarEventoAsync("ExecucaoFinalizada", $$"""{"ordemServicoId":"{{ordemId}}"}""");

    private async Task<EventoHistoricoSagaViewModel[]> ObterHistoricoSagaAsync(Guid ordemId)
    {
        var ok = Assert.IsType<Ok<IReadOnlyCollection<EventoHistoricoSagaViewModel>>>(await _fixture.CriarOrdensServicoEndpoints().GetHistoricoSaga(ordemId, CancellationToken.None));
        return ok.Value!.ToArray();
    }

    [Fact]
    public async Task Post_OrdemServicoValida_DeveRetornar201EPublicarOrdemServicoAberta()
    {
        var command = new CriarOrdemServicoCommand { ClienteId = Guid.NewGuid(), VeiculoId = Guid.NewGuid(), ServicoIds = [Guid.NewGuid(), Guid.NewGuid()] };

        var created = Assert.IsType<CreatedAtRoute<OrdemServicoViewModel>>(await _fixture.CriarOrdensServicoEndpoints().Post(command, CancellationToken.None));

        var viewModel = created.Value!;
        Assert.NotEqual(Guid.Empty, viewModel.Id);
        Assert.Equal(command.ClienteId, viewModel.ClienteId);
        Assert.Equal(command.VeiculoId, viewModel.VeiculoId);
        Assert.Equal(command.ServicoIds, viewModel.ServicoIds);
        Assert.Equal((int)StatusOrdemServico.Recebida, viewModel.Status);
        Assert.Equal("GetOrdemServicoById", created.RouteName);
        _fixture.EventosPublisherMock.Verify(p => p.PublicarAsync(
            It.Is<OrdemServicoAberta>(evento => evento.OrdemServicoId == viewModel.Id && evento.ServicoIds.Count == 2),
            viewModel.Id,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Post_SemServicos_DeveRetornar400()
    {
        var command = new CriarOrdemServicoCommand { ClienteId = Guid.NewGuid(), VeiculoId = Guid.NewGuid(), ServicoIds = [] };

        Assert.IsType<BadRequest<Dictionary<string, string?>>>(await _fixture.CriarOrdensServicoEndpoints().Post(command, CancellationToken.None));
    }

    [Fact]
    public async Task Post_SemCliente_DeveRetornar400()
    {
        var command = new CriarOrdemServicoCommand { ClienteId = Guid.Empty, VeiculoId = Guid.NewGuid(), ServicoIds = [Guid.NewGuid()] };

        var badRequest = Assert.IsType<BadRequest<Dictionary<string, string?>>>(await _fixture.CriarOrdensServicoEndpoints().Post(command, CancellationToken.None));

        Assert.Equal("O cliente da ordem de servico e obrigatorio.", badRequest.Value!["message"]);
    }

    [Fact]
    public async Task GetById_OrdemExistente_DeveRetornar200ComViewModel()
    {
        var ordem = await CriarOrdemAsync();

        var ok = Assert.IsType<Ok<OrdemServicoViewModel>>(await _fixture.CriarOrdensServicoEndpoints().GetById(ordem.Id, CancellationToken.None));

        Assert.Equal(ordem.Id, ok.Value!.Id);
        Assert.Equal(ordem.ServicoIds, ok.Value.ServicoIds);
    }

    [Fact]
    public async Task GetById_OrdemInexistente_DeveRetornar404()
    {
        Assert.IsType<NotFound<Dictionary<string, string?>>>(await _fixture.CriarOrdensServicoEndpoints().GetById(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task Get_ComOrdensServico_DeveRetornar200ComLista()
    {
        await CriarOrdemAsync();
        await CriarOrdemAsync();

        var ok = Assert.IsType<Ok<IReadOnlyCollection<OrdemServicoViewModel>>>(await _fixture.CriarOrdensServicoEndpoints().Get(CancellationToken.None));

        Assert.Equal(2, ok.Value!.Count);
    }

    [Fact]
    public async Task GetAcompanhamento_OrdemExistente_DeveRetornar200ComHistorico()
    {
        var ordem = await CriarOrdemAsync();

        var ok = Assert.IsType<Ok<AcompanhamentoOrdemServicoViewModel>>(await _fixture.CriarOrdensServicoEndpoints().GetAcompanhamento(ordem.Id, CancellationToken.None));

        Assert.Equal(ordem.Id, ok.Value!.Id);
        Assert.Equal(1, ok.Value.Status);
        Assert.Single(ok.Value.HistoricoStatus);
    }

    [Fact]
    public async Task GetAcompanhamento_OrdemInexistente_DeveRetornar404()
    {
        Assert.IsType<NotFound<Dictionary<string, string?>>>(await _fixture.CriarOrdensServicoEndpoints().GetAcompanhamento(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task GetByCliente_ClienteComOrdens_DeveRetornar200ComLista()
    {
        var clienteId = Guid.NewGuid();
        await CriarOrdemAsync(clienteId);
        await CriarOrdemAsync();

        var ok = Assert.IsType<Ok<IReadOnlyCollection<AcompanhamentoOrdemServicoViewModel>>>(await _fixture.CriarOrdensServicoEndpoints().GetByCliente(clienteId, CancellationToken.None));

        Assert.Single(ok.Value!);
    }

    [Fact]
    public async Task GetByCliente_ClienteSemOrdens_DeveRetornar200ComListaVazia()
    {
        var ok = Assert.IsType<Ok<IReadOnlyCollection<AcompanhamentoOrdemServicoViewModel>>>(await _fixture.CriarOrdensServicoEndpoints().GetByCliente(Guid.NewGuid(), CancellationToken.None));

        Assert.Empty(ok.Value!);
    }

    [Fact]
    public async Task Put_OrdemRecebida_DeveRetornar200EPublicarOrdemServicoAtualizada()
    {
        var ordem = await CriarOrdemAsync();
        var command = new AtualizarOrdemServicoCommand { Id = ordem.Id, ClienteId = ordem.ClienteId, VeiculoId = Guid.NewGuid(), ServicoIds = [Guid.NewGuid(), Guid.NewGuid()] };

        var ok = Assert.IsType<Ok<OrdemServicoViewModel>>(await _fixture.CriarOrdensServicoEndpoints().Put(command, CancellationToken.None));

        Assert.Equal(command.VeiculoId, ok.Value!.VeiculoId);
        Assert.Equal(command.ServicoIds, ok.Value.ServicoIds);
        _fixture.EventosPublisherMock.Verify(p => p.PublicarAsync(
            It.Is<OrdemServicoAtualizada>(evento => evento.OrdemServicoId == ordem.Id && evento.VeiculoId == command.VeiculoId),
            ordem.Id,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Put_AposMudancaDeStatus_DeveRetornar400SemAlterarAOrdem()
    {
        var ordem = await CriarOrdemAsync();
        await IniciarDiagnosticoAsync(ordem.Id);
        var command = new AtualizarOrdemServicoCommand { Id = ordem.Id, ClienteId = ordem.ClienteId, VeiculoId = Guid.NewGuid(), ServicoIds = [Guid.NewGuid()] };

        var badRequest = Assert.IsType<BadRequest<Dictionary<string, string?>>>(await _fixture.CriarOrdensServicoEndpoints().Put(command, CancellationToken.None));

        Assert.Equal("Somente ordem de servico recebida pode ser atualizada.", badRequest.Value!["message"]);
        var atual = Assert.IsType<Ok<OrdemServicoViewModel>>(await _fixture.CriarOrdensServicoEndpoints().GetById(ordem.Id, CancellationToken.None));
        Assert.Equal(ordem.VeiculoId, atual.Value!.VeiculoId);
    }

    [Fact]
    public async Task Put_OrdemInexistente_DeveRetornar404()
    {
        var command = new AtualizarOrdemServicoCommand { Id = Guid.NewGuid(), ClienteId = Guid.NewGuid(), VeiculoId = Guid.NewGuid(), ServicoIds = [Guid.NewGuid()] };

        Assert.IsType<NotFound<Dictionary<string, string?>>>(await _fixture.CriarOrdensServicoEndpoints().Put(command, CancellationToken.None));
    }

    [Fact]
    public async Task FluxoCompleto_DevePassarPorTodosOsStatusEPublicarCadaMudanca()
    {
        var ordem = await CriarOrdemAsync();

        await IniciarDiagnosticoAsync(ordem.Id);
        await AprovarOrcamentoEPagamentoAsync(ordem.Id);
        await FinalizarExecucaoAsync(ordem.Id);
        Assert.Equal(6, Assert.IsType<Ok<OrdemServicoViewModel>>(await _fixture.CriarOrdensServicoEndpoints().AlterarParaEntregue(ordem.Id, CancellationToken.None)).Value!.Status);

        var acompanhamento = Assert.IsType<Ok<AcompanhamentoOrdemServicoViewModel>>(await _fixture.CriarOrdensServicoEndpoints().GetAcompanhamento(ordem.Id, CancellationToken.None));
        Assert.Equal(
            [StatusOrdemServico.Recebida, StatusOrdemServico.EmDiagnostico, StatusOrdemServico.AguardandoAprovacao, StatusOrdemServico.EmExecucao, StatusOrdemServico.Finalizada, StatusOrdemServico.Entregue],
            acompanhamento.Value!.HistoricoStatus.Select(historico => historico.Status));

        // Uma publicacao por mudanca: diagnostico e finalizacao (Execucao e Producao), orcamento e pagamento (Billing) e entrega (HTTP)
        _fixture.EventosPublisherMock.Verify(p => p.PublicarAsync(It.IsAny<OrdemServicoStatusAlterado>(), ordem.Id, It.IsAny<CancellationToken>()), Times.Exactly(5));
        foreach (var (statusAnterior, statusNovo) in new[] { (1, 2), (2, 3), (3, 4), (4, 5), (5, 6) })
        {
            _fixture.EventosPublisherMock.Verify(p => p.PublicarAsync(
                It.Is<OrdemServicoStatusAlterado>(evento => evento.StatusAnterior == statusAnterior && evento.StatusNovo == statusNovo),
                ordem.Id,
                It.IsAny<CancellationToken>()), Times.Once);
        }
    }

    [Fact]
    public async Task GetHistoricoSaga_AposASaga_DeveRetornar200ComOsEventosConsumidosEmOrdem()
    {
        var ordem = await CriarOrdemAsync();
        await IniciarDiagnosticoAsync(ordem.Id);
        await ProcessarEventoAsync("OrcamentoGerado", $$"""{"ordemServicoId":"{{ordem.Id}}","valorTotal":100}""");
        // Gera o orcamento de novo (regerado: processado sem mudanca de status, 3 -> 3) e aprova o pagamento
        await AprovarOrcamentoEPagamentoAsync(ordem.Id);
        await FinalizarExecucaoAsync(ordem.Id);
        // A entrega e feita pelo HTTP, nao por evento: nao entra no historico da saga
        Assert.IsType<Ok<OrdemServicoViewModel>>(await _fixture.CriarOrdensServicoEndpoints().AlterarParaEntregue(ordem.Id, CancellationToken.None));

        var historico = await ObterHistoricoSagaAsync(ordem.Id);

        Assert.Equal(["DiagnosticoIniciado", "OrcamentoGerado", "OrcamentoGerado", "PagamentoAprovado", "ExecucaoFinalizada"], historico.Select(evento => evento.Tipo));
        Assert.Equal(["execution", "billing", "billing", "billing", "execution"], historico.Select(evento => evento.Origem));
        Assert.Equal(new int?[] { 1, 2, 3, 3, 4 }, historico.Select(evento => evento.StatusAnterior));
        Assert.Equal(new int?[] { 2, 3, 3, 4, 5 }, historico.Select(evento => evento.StatusNovo));
        Assert.All(historico, evento =>
        {
            Assert.Equal("Processado", evento.Resultado);
            Assert.Null(evento.Motivo);
        });
    }

    [Fact]
    public async Task GetHistoricoSaga_OrdemSemEventos_DeveRetornar200ComListaVazia()
    {
        var ordem = await CriarOrdemAsync();

        Assert.Empty(await ObterHistoricoSagaAsync(ordem.Id));
        Assert.Empty(await ObterHistoricoSagaAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetHistoricoSaga_OrdemExcluidaOuDesconhecida_DeveRetornar200ComOQueHouverNoHistorico()
    {
        // A consulta nao passa pelo PostgreSQL: o historico continua disponivel depois da exclusao da OS
        var ordem = await CriarOrdemAsync();
        await ProcessarEventoAsync("OrdemServicoRejeitada", $$"""{"ordemServicoId":"{{ordem.Id}}","motivo":"Cliente nao encontrado."}""");
        Assert.IsType<NoContent>(await _fixture.CriarOrdensServicoEndpoints().Delete(ordem.Id, CancellationToken.None));
        // Evento de uma OS que o OS Service nao conhece: ignorado e registrado sem status
        var desconhecidaId = Guid.NewGuid();
        var resultado = await _fixture.CriarEventosController().ProcessarAsync(
            "PagamentoAprovado",
            $$"""{"ordemServicoId":"{{desconhecidaId}}","pagamentoId":"{{Guid.NewGuid()}}","valor":100}""",
            CancellationToken.None);
        Assert.Equal(ResultadoProcessamentoEvento.Ignorado, resultado);

        var cancelada = Assert.Single(await ObterHistoricoSagaAsync(ordem.Id));
        Assert.Equal("OrdemServicoRejeitada", cancelada.Tipo);
        Assert.Equal("management", cancelada.Origem);
        Assert.Equal("Processado", cancelada.Resultado);
        Assert.Equal(1, cancelada.StatusAnterior);
        Assert.Equal(8, cancelada.StatusNovo);
        var ignorado = Assert.Single(await ObterHistoricoSagaAsync(desconhecidaId));
        Assert.Equal("PagamentoAprovado", ignorado.Tipo);
        Assert.Equal("billing", ignorado.Origem);
        Assert.Equal("Ignorado", ignorado.Resultado);
        Assert.Null(ignorado.StatusAnterior);
        Assert.Null(ignorado.StatusNovo);
        Assert.Equal("Ordem de servico nao encontrada.", ignorado.Motivo);
    }

    [Fact]
    public async Task AlterarParaEntregue_OrdemNaoFinalizada_DeveRetornar400SemAlterarAOrdem()
    {
        var ordem = await CriarOrdemAsync();
        await IniciarDiagnosticoAsync(ordem.Id);

        var badRequest = Assert.IsType<BadRequest<Dictionary<string, string?>>>(await _fixture.CriarOrdensServicoEndpoints().AlterarParaEntregue(ordem.Id, CancellationToken.None));

        Assert.Equal("Somente ordem de servico finalizada pode ser alterada para entregue.", badRequest.Value!["message"]);
        var atual = Assert.IsType<Ok<OrdemServicoViewModel>>(await _fixture.CriarOrdensServicoEndpoints().GetById(ordem.Id, CancellationToken.None));
        Assert.Equal((int)StatusOrdemServico.EmDiagnostico, atual.Value!.Status);
    }

    [Fact]
    public async Task AlterarStatus_OrdemInexistente_DeveRetornar404()
    {
        Assert.IsType<NotFound<Dictionary<string, string?>>>(await _fixture.CriarOrdensServicoEndpoints().AlterarParaEntregue(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task Delete_OrdemExistente_DeveRetornar204EPublicarOrdemServicoExcluida()
    {
        var ordem = await CriarOrdemAsync();

        Assert.IsType<NoContent>(await _fixture.CriarOrdensServicoEndpoints().Delete(ordem.Id, CancellationToken.None));

        Assert.IsType<NotFound<Dictionary<string, string?>>>(await _fixture.CriarOrdensServicoEndpoints().GetById(ordem.Id, CancellationToken.None));
        _fixture.EventosPublisherMock.Verify(p => p.PublicarAsync(It.Is<OrdemServicoExcluida>(evento => evento.OrdemServicoId == ordem.Id), ordem.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Delete_OrdemInexistente_DeveRetornar404()
    {
        Assert.IsType<NotFound<Dictionary<string, string?>>>(await _fixture.CriarOrdensServicoEndpoints().Delete(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task GetOrdenadas_DeveOrdenarPorPrioridadeEExcluirFinalizadasEntreguesECanceladas()
    {
        var recebida = await CriarOrdemAsync();
        var emDiagnostico = await CriarOrdemAsync();
        var emExecucao = await CriarOrdemAsync();
        var finalizada = await CriarOrdemAsync();
        var cancelada = await CriarOrdemAsync();

        await IniciarDiagnosticoAsync(emDiagnostico.Id);
        await IniciarDiagnosticoAsync(emExecucao.Id);
        await AprovarOrcamentoEPagamentoAsync(emExecucao.Id);
        await IniciarDiagnosticoAsync(finalizada.Id);
        await AprovarOrcamentoEPagamentoAsync(finalizada.Id);
        await FinalizarExecucaoAsync(finalizada.Id);
        await ProcessarEventoAsync("OrdemServicoRejeitada", $$"""{"ordemServicoId":"{{cancelada.Id}}","motivo":"Cliente nao encontrado."}""");

        var ok = Assert.IsType<Ok<IReadOnlyCollection<OrdemServicoOrdenadasViewModel>>>(await _fixture.CriarOrdensServicoEndpoints().GetOrdenadas(CancellationToken.None));

        Assert.Equal([emExecucao.Id, emDiagnostico.Id, recebida.Id], ok.Value!.Select(ordem => ordem.Id));
        Assert.All(ok.Value!, ordem => Assert.NotEqual(default, ordem.DataAlteracao));
    }

    [Fact]
    public async Task GetOrdenadas_SemOrdensAtivas_DeveRetornarListaVazia()
    {
        var ok = Assert.IsType<Ok<IReadOnlyCollection<OrdemServicoOrdenadasViewModel>>>(await _fixture.CriarOrdensServicoEndpoints().GetOrdenadas(CancellationToken.None));

        Assert.Empty(ok.Value!);
    }
}
