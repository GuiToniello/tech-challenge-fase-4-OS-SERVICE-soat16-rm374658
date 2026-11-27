using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TechChallenge.Oficina.OsService.Entities.Features.OrdensServico;
using TechChallenge.Oficina.OsService.Entities.Features.OrdensServico.Enums;
using TechChallenge.Oficina.OsService.UseCases.Features.Eventos;
using TechChallenge.Oficina.OsService.UseCases.Features.Eventos.Interfaces;
using TechChallenge.Oficina.OsService.UseCases.Features.OrdensServico.Mappings;
using TechChallenge.Oficina.OsService.UseCases.Features.OrdensServico.ViewModels;
using Xunit;

namespace TechChallenge.Oficina.OsService.UseCases.Tests.Features.OrdensServico.Mappings;

public sealed class OrdemServicoProfileTests
{
    private readonly IMapper _mapper;

    public OrdemServicoProfileTests()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddAutoMapper(_ => { }, typeof(OrdemServicoProfile).Assembly);

        using var serviceProvider = services.BuildServiceProvider();
        _mapper = serviceProvider.GetRequiredService<IMapper>();
    }

    [Fact]
    public void Map_OrdemServicoParaOrdemServicoViewModel_DeveMapearStatusEServicos()
    {
        var servicoId = Guid.NewGuid();
        var ordemServico = OrdemServico.Criar(Guid.NewGuid(), Guid.NewGuid(), [servicoId]);

        var viewModel = _mapper.Map<OrdemServicoViewModel>(ordemServico);

        Assert.Equal(ordemServico.Id, viewModel.Id);
        Assert.Equal(ordemServico.ClienteId, viewModel.ClienteId);
        Assert.Equal(ordemServico.VeiculoId, viewModel.VeiculoId);
        Assert.Equal([servicoId], viewModel.ServicoIds);
        Assert.Equal(1, viewModel.Status);
        Assert.Equal("Recebida", viewModel.StatusDescricao);
        Assert.Null(viewModel.ValorOrcamento);
        Assert.Null(viewModel.MotivoCancelamento);
    }

    [Fact]
    public void Map_OrdemServicoCancelada_DeveMapearValorOrcamentoEMotivo()
    {
        var ordemServico = OrdemServico.Criar(Guid.NewGuid(), Guid.NewGuid(), [Guid.NewGuid()]);
        ordemServico.RegistrarOrcamentoGerado(99.90m);
        ordemServico.Cancelar("Veiculo nao encontrado.");

        var viewModel = _mapper.Map<OrdemServicoViewModel>(ordemServico);

        Assert.Equal(8, viewModel.Status);
        Assert.Equal("Cancelada", viewModel.StatusDescricao);
        Assert.Equal(99.90m, viewModel.ValorOrcamento);
        Assert.Equal("Veiculo nao encontrado.", viewModel.MotivoCancelamento);
    }

    [Fact]
    public void Map_OrdemServicoParaAcompanhamento_DeveMapearHistoricoEmOrdemCronologica()
    {
        var ordemServico = OrdemServico.Criar(Guid.NewGuid(), Guid.NewGuid(), [Guid.NewGuid()], new DateTime(2024, 01, 01, 8, 00, 00, DateTimeKind.Utc));
        ordemServico.AlterarParaEmDiagnostico(new DateTime(2024, 01, 01, 9, 00, 00, DateTimeKind.Utc));
        ordemServico.RegistrarOrcamentoGerado(120m, new DateTime(2024, 01, 01, 10, 00, 00, DateTimeKind.Utc));

        var viewModel = _mapper.Map<AcompanhamentoOrdemServicoViewModel>(ordemServico);

        Assert.Equal(ordemServico.Id, viewModel.Id);
        Assert.Equal(3, viewModel.Status);
        Assert.Equal("Aguardando aprovação", viewModel.StatusDescricao);
        Assert.Equal(120m, viewModel.ValorOrcamento);
        Assert.Equal(
            [StatusOrdemServico.Recebida, StatusOrdemServico.EmDiagnostico, StatusOrdemServico.AguardandoAprovacao],
            viewModel.HistoricoStatus.Select(historico => historico.Status));
    }

    [Fact]
    public void Map_OrdemServicoParaOrdenadas_DeveUsarDataDeEntradaDaOrdem()
    {
        var dataCadastro = new DateTime(2024, 01, 01, 8, 00, 00, DateTimeKind.Utc);
        var ordemServico = OrdemServico.Criar(Guid.NewGuid(), Guid.NewGuid(), [Guid.NewGuid()], dataCadastro);
        ordemServico.AlterarParaEmDiagnostico(new DateTime(2024, 01, 01, 9, 00, 00, DateTimeKind.Utc));

        var viewModel = _mapper.Map<OrdemServicoOrdenadasViewModel>(ordemServico);

        Assert.Equal(ordemServico.Id, viewModel.Id);
        Assert.Equal(2, viewModel.Status);
        Assert.Equal("Em diagnóstico", viewModel.StatusDescricao);
        Assert.Equal(dataCadastro, viewModel.DataAlteracao);
        Assert.Equal(ordemServico.ServicoIds, viewModel.ServicoIds);
    }

    [Fact]
    public void Map_HistoricoSagaParaViewModel_DeveCopiarOsCamposComOResultadoEmTexto()
    {
        // Mesmo mapeamento de colecao usado na consulta do historico da saga
        var ocorridoEm = new DateTime(2026, 11, 27, 10, 15, 30, 123, DateTimeKind.Utc);
        var aplicado = new EventoHistoricoSaga
        {
            OrdemServicoId = Guid.NewGuid(),
            OcorridoEm = ocorridoEm,
            Tipo = "OrdemServicoRejeitada",
            Origem = "management",
            Resultado = ResultadoProcessamentoEvento.Processado,
            StatusAnterior = 1,
            StatusNovo = 8
        };
        var semOrdem = aplicado with
        {
            OcorridoEm = ocorridoEm.AddSeconds(1),
            Resultado = ResultadoProcessamentoEvento.Ignorado,
            StatusAnterior = null,
            StatusNovo = null,
            Motivo = "Ordem de servico nao encontrada."
        };

        var viewModels = _mapper.Map<IReadOnlyCollection<EventoHistoricoSagaViewModel>>(new[] { aplicado, semOrdem }).ToArray();

        Assert.Equal(2, viewModels.Length);
        Assert.Equal(ocorridoEm, viewModels[0].OcorridoEm);
        Assert.Equal("OrdemServicoRejeitada", viewModels[0].Tipo);
        Assert.Equal("management", viewModels[0].Origem);
        Assert.Equal("Processado", viewModels[0].Resultado);
        Assert.Equal(1, viewModels[0].StatusAnterior);
        Assert.Equal(8, viewModels[0].StatusNovo);
        Assert.Null(viewModels[0].Motivo);
        Assert.Equal("Ignorado", viewModels[1].Resultado);
        Assert.Null(viewModels[1].StatusAnterior);
        Assert.Null(viewModels[1].StatusNovo);
        Assert.Equal("Ordem de servico nao encontrada.", viewModels[1].Motivo);
    }
}
