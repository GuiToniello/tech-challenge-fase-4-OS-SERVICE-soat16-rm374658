using Microsoft.EntityFrameworkCore;
using TechChallenge.Oficina.OsService.DB.Data.Context;
using TechChallenge.Oficina.OsService.DB.Data.Features.OrdensServico;
using TechChallenge.Oficina.OsService.Entities.Features.OrdensServico;
using TechChallenge.Oficina.OsService.Entities.Features.OrdensServico.Enums;
using Xunit;

namespace TechChallenge.Oficina.OsService.DB.Data.Tests.Features.OrdensServico;

public sealed class OrdemServicoGatewayTests
{
    private readonly DbContextOptions<OsServiceDbContext> _options = new DbContextOptionsBuilder<OsServiceDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options;

    private OsServiceDbContext CriarContexto() => new(_options);

    private static DateTime Hora(int hora) => new(2024, 01, 01, hora, 00, 00, DateTimeKind.Utc);

    private static OrdemServico CriarOrdem(Guid? clienteId = null, DateTime? dataCadastro = null) =>
        OrdemServico.Criar(clienteId ?? Guid.NewGuid(), Guid.NewGuid(), [Guid.NewGuid(), Guid.NewGuid()], dataCadastro);

    private async Task AdicionarAsync(params OrdemServico[] ordensServico)
    {
        await using var context = CriarContexto();
        var gateway = new OrdemServicoGateway(context);

        foreach (var ordemServico in ordensServico)
        {
            await gateway.AdicionarAsync(ordemServico);
        }
    }

    [Fact]
    public async Task AdicionarAsync_DevePersistirOrdemServicoComServicosEHistorico()
    {
        var ordemServico = CriarOrdem();

        await AdicionarAsync(ordemServico);

        await using var context = CriarContexto();
        var salvo = await context.OrdensServico.Include(o => o.HistoricoStatus).SingleAsync();
        Assert.Equal(ordemServico.Id, salvo.Id);
        Assert.Equal(ordemServico.ServicoIds, salvo.ServicoIds);
        Assert.Equal(StatusOrdemServico.Recebida, salvo.Status);
        Assert.Single(salvo.HistoricoStatus);
    }

    [Fact]
    public async Task ObterPorIdAsync_DeveRetornarOrdemServicoComHistorico_QuandoExiste()
    {
        var ordemServico = CriarOrdem();
        await AdicionarAsync(ordemServico);

        await using var context = CriarContexto();
        var encontrado = await new OrdemServicoGateway(context).ObterPorIdAsync(ordemServico.Id);

        Assert.NotNull(encontrado);
        Assert.Equal(ordemServico.ClienteId, encontrado.ClienteId);
        Assert.Equal(ordemServico.VeiculoId, encontrado.VeiculoId);
        Assert.Equal(ordemServico.ServicoIds, encontrado.ServicoIds);
        Assert.Single(encontrado.HistoricoStatus);
    }

    [Fact]
    public async Task ObterPorIdAsync_DeveRetornarNulo_QuandoNaoExiste()
    {
        await using var context = CriarContexto();

        var encontrado = await new OrdemServicoGateway(context).ObterPorIdAsync(Guid.NewGuid());

        Assert.Null(encontrado);
    }

    [Fact]
    public async Task AtualizarAsync_DevePersistirServicosValorMotivoEHistorico()
    {
        var ordemServico = CriarOrdem();
        await AdicionarAsync(ordemServico);
        var novoServicoId = Guid.NewGuid();

        await using (var context = CriarContexto())
        {
            var gateway = new OrdemServicoGateway(context);
            var existente = (await gateway.ObterPorIdAsync(ordemServico.Id))!;
            existente.Atualizar(existente.ClienteId, existente.VeiculoId, [novoServicoId]);
            existente.RegistrarOrcamentoGerado(250.50m);
            existente.Cancelar("Cliente nao encontrado.");
            await gateway.AtualizarAsync(existente);
        }

        await using var contextoLeitura = CriarContexto();
        var salvo = (await new OrdemServicoGateway(contextoLeitura).ObterPorIdAsync(ordemServico.Id))!;
        Assert.Equal([novoServicoId], salvo.ServicoIds);
        Assert.Equal(250.50m, salvo.ValorOrcamento);
        Assert.Equal("Cliente nao encontrado.", salvo.MotivoCancelamento);
        Assert.Equal(StatusOrdemServico.Cancelada, salvo.Status);
        Assert.Equal(
            [StatusOrdemServico.Recebida, StatusOrdemServico.AguardandoAprovacao, StatusOrdemServico.Cancelada],
            salvo.HistoricoStatus.OrderBy(historico => historico.DataAlteracao).Select(historico => historico.Status));
    }

    [Fact]
    public async Task ListarAsync_DeveRetornarTodasAsOrdens()
    {
        await AdicionarAsync(CriarOrdem(), CriarOrdem());

        await using var context = CriarContexto();
        var ordens = await new OrdemServicoGateway(context).ListarAsync();

        Assert.Equal(2, ordens.Count);
    }

    [Fact]
    public async Task ListarPorClienteAsync_DeveRetornarSomenteOrdensDoClienteInformado()
    {
        var clienteA = Guid.NewGuid();
        await AdicionarAsync(CriarOrdem(clienteA), CriarOrdem(clienteA), CriarOrdem());

        await using var context = CriarContexto();
        var ordens = await new OrdemServicoGateway(context).ListarPorClienteAsync(clienteA);

        Assert.Equal(2, ordens.Count);
        Assert.All(ordens, ordem => Assert.Equal(clienteA, ordem.ClienteId));
    }

    [Fact]
    public async Task ListarPorClienteAsync_DeveRetornarVazio_QuandoClienteNaoPossuiOrdens()
    {
        await AdicionarAsync(CriarOrdem());

        await using var context = CriarContexto();
        var ordens = await new OrdemServicoGateway(context).ListarPorClienteAsync(Guid.NewGuid());

        Assert.Empty(ordens);
    }

    [Fact]
    public async Task ListarPorStatusAsync_DeveRetornarSomenteOrdensNoStatusInformado()
    {
        var entregue = CriarOrdem(dataCadastro: Hora(8));
        entregue.AlterarParaEmDiagnostico(Hora(9));
        entregue.RegistrarOrcamentoGerado(100m, Hora(10));
        entregue.RegistrarPagamentoAprovado(Hora(10));
        entregue.AlterarParaFinalizada(Hora(11));
        entregue.AlterarParaEntregue(Hora(12));
        await AdicionarAsync(entregue, CriarOrdem());

        await using var context = CriarContexto();
        var ordens = await new OrdemServicoGateway(context).ListarPorStatusAsync(StatusOrdemServico.Entregue);

        var ordem = Assert.Single(ordens);
        Assert.Equal(entregue.Id, ordem.Id);
        Assert.Equal(6, ordem.HistoricoStatus.Count);
    }

    [Fact]
    public async Task ListarOrdenadasAsync_DeveOrdenarPorPrioridadeEDataEExcluirOrdensEncerradas()
    {
        var recebidaAntiga = CriarOrdem(dataCadastro: Hora(1));
        var recebidaNova = CriarOrdem(dataCadastro: Hora(5));

        var emDiagnostico = CriarOrdem(dataCadastro: Hora(2));
        emDiagnostico.AlterarParaEmDiagnostico(Hora(6));

        var aguardando = CriarOrdem(dataCadastro: Hora(3));
        aguardando.RegistrarOrcamentoGerado(100m, Hora(7));

        var emExecucao = CriarOrdem(dataCadastro: Hora(4));
        emExecucao.RegistrarOrcamentoGerado(100m, Hora(8));
        emExecucao.RegistrarPagamentoAprovado(Hora(9));

        var finalizada = CriarOrdem(dataCadastro: Hora(1));
        finalizada.RegistrarOrcamentoGerado(100m, Hora(2));
        finalizada.RegistrarOrcamentoRecusado(Hora(3));

        var entregue = CriarOrdem(dataCadastro: Hora(1));
        entregue.AlterarParaEmDiagnostico(Hora(2));
        entregue.RegistrarOrcamentoGerado(100m, Hora(3));
        entregue.RegistrarPagamentoAprovado(Hora(3));
        entregue.AlterarParaFinalizada(Hora(4));
        entregue.AlterarParaEntregue(Hora(5));

        var cancelada = CriarOrdem(dataCadastro: Hora(1));
        cancelada.Cancelar("Cliente nao encontrado.", Hora(2));

        await AdicionarAsync(recebidaNova, finalizada, emDiagnostico, cancelada, recebidaAntiga, entregue, aguardando, emExecucao);

        await using var context = CriarContexto();
        var ordens = await new OrdemServicoGateway(context).ListarOrdenadasAsync();

        Assert.Equal(
            [emExecucao.Id, aguardando.Id, emDiagnostico.Id, recebidaAntiga.Id, recebidaNova.Id],
            ordens.Select(ordem => ordem.Id));
    }

    [Fact]
    public async Task RemoverAsync_DeveExcluirOrdemServicoEHistorico()
    {
        var ordemServico = CriarOrdem();
        await AdicionarAsync(ordemServico);

        await using (var context = CriarContexto())
        {
            var gateway = new OrdemServicoGateway(context);
            var existente = (await gateway.ObterPorIdAsync(ordemServico.Id))!;
            await gateway.RemoverAsync(existente);
        }

        await using var contextoLeitura = CriarContexto();
        Assert.Empty(await contextoLeitura.OrdensServico.ToListAsync());
    }
}
