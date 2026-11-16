using System.Reflection;
using TechChallenge.Oficina.OsService.Entities.Exceptions;
using TechChallenge.Oficina.OsService.Entities.Features.OrdensServico;
using TechChallenge.Oficina.OsService.Entities.Features.OrdensServico.Enums;
using Xunit;

namespace TechChallenge.Oficina.OsService.Entities.Tests.Features.OrdensServico;

public sealed class OrdemServicoDomainTests
{
    private static OrdemServico CriarOrdem(DateTime? dataCadastro = null) =>
        OrdemServico.Criar(Guid.NewGuid(), Guid.NewGuid(), [Guid.NewGuid()], dataCadastro);

    private static OrdemServico CriarOrdemNoStatus(StatusOrdemServico status)
    {
        var ordemServico = CriarOrdem();

        switch (status)
        {
            case StatusOrdemServico.Recebida:
                break;
            case StatusOrdemServico.EmDiagnostico:
                ordemServico.AlterarParaEmDiagnostico();
                break;
            case StatusOrdemServico.AguardandoAprovacao:
                ordemServico.RegistrarOrcamentoGerado(100m);
                break;
            case StatusOrdemServico.EmExecucao:
                ordemServico.RegistrarOrcamentoGerado(100m);
                ordemServico.RegistrarOrcamentoAprovado();
                break;
            case StatusOrdemServico.Finalizada:
                ordemServico.AlterarParaEmDiagnostico();
                ordemServico.AlterarParaEmExecucao();
                ordemServico.AlterarParaFinalizada();
                break;
            case StatusOrdemServico.Entregue:
                ordemServico.AlterarParaEmDiagnostico();
                ordemServico.AlterarParaEmExecucao();
                ordemServico.AlterarParaFinalizada();
                ordemServico.AlterarParaEntregue();
                break;
            case StatusOrdemServico.Cancelada:
                ordemServico.Cancelar("Cliente inexistente.");
                break;
            default:
                // Encerrada nao tem transicao no dominio atual: forca o status para cobrir as regras que o consideram
                typeof(OrdemServico)
                    .GetProperty(nameof(OrdemServico.Status), BindingFlags.Public | BindingFlags.Instance)!
                    .SetValue(ordemServico, status);
                break;
        }

        return ordemServico;
    }

    [Fact]
    public void Criar_DeveIniciarComStatusRecebidaEHistorico()
    {
        var clienteId = Guid.NewGuid();
        var veiculoId = Guid.NewGuid();
        var servicoId = Guid.NewGuid();

        var ordemServico = OrdemServico.Criar(clienteId, veiculoId, [servicoId], new DateTime(2024, 01, 01, 10, 00, 00, DateTimeKind.Utc));

        Assert.NotEqual(Guid.Empty, ordemServico.Id);
        Assert.Equal(clienteId, ordemServico.ClienteId);
        Assert.Equal(veiculoId, ordemServico.VeiculoId);
        Assert.Equal(StatusOrdemServico.Recebida, ordemServico.Status);
        Assert.Equal([servicoId], ordemServico.ServicoIds);
        Assert.Null(ordemServico.ValorOrcamento);
        Assert.Null(ordemServico.MotivoCancelamento);
        var historico = Assert.Single(ordemServico.HistoricoStatus);
        Assert.Equal(StatusOrdemServico.Recebida, historico.Status);
        Assert.Equal(new DateTime(2024, 01, 01, 10, 00, 00, DateTimeKind.Utc), historico.DataAlteracao);
    }

    [Fact]
    public void Criar_DeveRemoverServicosDuplicados()
    {
        var servicoId = Guid.NewGuid();
        var outroServicoId = Guid.NewGuid();

        var ordemServico = OrdemServico.Criar(Guid.NewGuid(), Guid.NewGuid(), [servicoId, outroServicoId, servicoId]);

        Assert.Equal([servicoId, outroServicoId], ordemServico.ServicoIds);
    }

    [Fact]
    public void Criar_DeveLancar_QuandoClienteForInvalido()
    {
        var action = () => OrdemServico.Criar(Guid.Empty, Guid.NewGuid(), [Guid.NewGuid()]);

        var exception = Assert.Throws<DomainException>(action);
        Assert.Equal("O cliente da ordem de servico e obrigatorio.", exception.Message);
    }

    [Fact]
    public void Criar_DeveLancar_QuandoVeiculoForInvalido()
    {
        var action = () => OrdemServico.Criar(Guid.NewGuid(), Guid.Empty, [Guid.NewGuid()]);

        var exception = Assert.Throws<DomainException>(action);
        Assert.Equal("O veiculo da ordem de servico e obrigatorio.", exception.Message);
    }

    [Fact]
    public void Criar_DeveLancar_QuandoNaoPossuirServicos()
    {
        var action = () => OrdemServico.Criar(Guid.NewGuid(), Guid.NewGuid(), []);

        var exception = Assert.Throws<DomainException>(action);
        Assert.Equal("A ordem de servico deve possuir ao menos um servico.", exception.Message);
    }

    [Fact]
    public void Criar_DeveLancar_QuandoServicosForemNulos()
    {
        var action = () => OrdemServico.Criar(Guid.NewGuid(), Guid.NewGuid(), null!);

        var exception = Assert.Throws<DomainException>(action);
        Assert.Equal("A ordem de servico deve possuir ao menos um servico.", exception.Message);
    }

    [Fact]
    public void Criar_DeveLancar_QuandoAlgumServicoTiverIdentificadorVazio()
    {
        var action = () => OrdemServico.Criar(Guid.NewGuid(), Guid.NewGuid(), [Guid.NewGuid(), Guid.Empty]);

        var exception = Assert.Throws<DomainException>(action);
        Assert.Equal("Todos os servicos informados devem possuir identificador valido.", exception.Message);
    }

    [Fact]
    public void AlteracoesDeStatus_DevemRegistrarHistorico()
    {
        var ordemServico = CriarOrdem(new DateTime(2024, 01, 01, 8, 00, 00, DateTimeKind.Utc));

        ordemServico.AlterarParaEmDiagnostico(new DateTime(2024, 01, 01, 9, 00, 00, DateTimeKind.Utc));
        ordemServico.AlterarParaEmExecucao(new DateTime(2024, 01, 01, 10, 00, 00, DateTimeKind.Utc));
        ordemServico.AlterarParaFinalizada(new DateTime(2024, 01, 01, 12, 00, 00, DateTimeKind.Utc));
        ordemServico.AlterarParaEntregue(new DateTime(2024, 01, 01, 14, 00, 00, DateTimeKind.Utc));

        Assert.Equal(5, ordemServico.HistoricoStatus.Count);
        Assert.Equal(StatusOrdemServico.Entregue, ordemServico.Status);
    }

    [Fact]
    public void DeveCalcularTempoExecucaoEEntrega()
    {
        var ordemServico = CriarOrdem(new DateTime(2024, 01, 01, 8, 00, 00, DateTimeKind.Utc));
        ordemServico.AlterarParaEmDiagnostico(new DateTime(2024, 01, 01, 9, 00, 00, DateTimeKind.Utc));
        ordemServico.AlterarParaEmExecucao(new DateTime(2024, 01, 01, 10, 00, 00, DateTimeKind.Utc));
        ordemServico.AlterarParaFinalizada(new DateTime(2024, 01, 01, 13, 00, 00, DateTimeKind.Utc));
        ordemServico.AlterarParaEntregue(new DateTime(2024, 01, 01, 14, 00, 00, DateTimeKind.Utc));

        Assert.Equal(TimeSpan.FromHours(3), ordemServico.ObterTempoExecucao());
        Assert.Equal(TimeSpan.FromHours(6), ordemServico.ObterTempoEntrega());
    }

    [Fact]
    public void ObterTempoExecucao_DeveLancar_QuandoOrdemNaoPassouPorExecucao()
    {
        var ordemServico = CriarOrdem();
        ordemServico.RegistrarOrcamentoGerado(50m);
        ordemServico.RegistrarOrcamentoRecusado();

        Action action = () => ordemServico.ObterTempoExecucao();

        var exception = Assert.Throws<DomainException>(action);
        Assert.Equal("A ordem de servico nao possui historico suficiente para calcular os indicadores.", exception.Message);
    }

    [Theory]
    [InlineData(StatusOrdemServico.Recebida, "Recebida")]
    [InlineData(StatusOrdemServico.EmDiagnostico, "Em diagnóstico")]
    [InlineData(StatusOrdemServico.AguardandoAprovacao, "Aguardando aprovação")]
    [InlineData(StatusOrdemServico.EmExecucao, "Em execução")]
    [InlineData(StatusOrdemServico.Finalizada, "Finalizada")]
    [InlineData(StatusOrdemServico.Entregue, "Entregue")]
    [InlineData(StatusOrdemServico.Encerrada, "Encerrada")]
    [InlineData(StatusOrdemServico.Cancelada, "Cancelada")]
    public void StatusOrdemServicoExtensions_ObterDescricao_DeveRetornarDescricaoEsperada(StatusOrdemServico status, string descricaoEsperada)
    {
        var descricao = status.ObterDescricao();

        Assert.Equal(descricaoEsperada, descricao);
    }

    [Fact]
    public void StatusOrdemServicoExtensions_ObterDescricao_DeveLancar_QuandoStatusInvalido()
    {
        var action = () => ((StatusOrdemServico)99).ObterDescricao();

        Assert.Throws<ArgumentOutOfRangeException>(action);
    }

    [Fact]
    public void StatusOrdemServico_DeveManterValoresDoContratoDeEventos()
    {
        Assert.Equal(1, (int)StatusOrdemServico.Recebida);
        Assert.Equal(2, (int)StatusOrdemServico.EmDiagnostico);
        Assert.Equal(3, (int)StatusOrdemServico.AguardandoAprovacao);
        Assert.Equal(4, (int)StatusOrdemServico.EmExecucao);
        Assert.Equal(5, (int)StatusOrdemServico.Finalizada);
        Assert.Equal(6, (int)StatusOrdemServico.Entregue);
        Assert.Equal(7, (int)StatusOrdemServico.Encerrada);
        Assert.Equal(8, (int)StatusOrdemServico.Cancelada);
    }

    [Fact]
    public void AlterarParaEmDiagnostico_DeveAlterarStatus_QuandoStatusForRecebida()
    {
        var ordemServico = CriarOrdem();

        ordemServico.AlterarParaEmDiagnostico();

        Assert.Equal(StatusOrdemServico.EmDiagnostico, ordemServico.Status);
    }

    [Fact]
    public void AlterarParaEmDiagnostico_DeveLancar_QuandoStatusForDiferente()
    {
        var ordemServico = CriarOrdemNoStatus(StatusOrdemServico.EmDiagnostico);

        var action = () => ordemServico.AlterarParaEmDiagnostico();

        var exception = Assert.Throws<DomainException>(action);
        Assert.Equal("Somente ordem de servico recebida pode ser alterada para em diagnostico.", exception.Message);
    }

    [Theory]
    [InlineData(StatusOrdemServico.EmDiagnostico)]
    [InlineData(StatusOrdemServico.AguardandoAprovacao)]
    public void AlterarParaEmExecucao_DeveAlterarStatus_QuandoStatusPermitir(StatusOrdemServico statusAtual)
    {
        var ordemServico = CriarOrdemNoStatus(statusAtual);

        ordemServico.AlterarParaEmExecucao();

        Assert.Equal(StatusOrdemServico.EmExecucao, ordemServico.Status);
    }

    [Fact]
    public void AlterarParaEmExecucao_DeveLancar_QuandoStatusForRecebida()
    {
        var ordemServico = CriarOrdem();

        var action = () => ordemServico.AlterarParaEmExecucao();

        var exception = Assert.Throws<DomainException>(action);
        Assert.Equal("Somente ordem de servico em diagnostico ou aguardando aprovacao pode ser alterada para em execucao.", exception.Message);
    }

    [Fact]
    public void AlterarParaFinalizada_DeveAlterarStatus_QuandoStatusForEmExecucao()
    {
        var ordemServico = CriarOrdemNoStatus(StatusOrdemServico.EmExecucao);

        ordemServico.AlterarParaFinalizada();

        Assert.Equal(StatusOrdemServico.Finalizada, ordemServico.Status);
    }

    [Fact]
    public void AlterarParaFinalizada_DeveLancar_QuandoStatusForEmDiagnostico()
    {
        var ordemServico = CriarOrdemNoStatus(StatusOrdemServico.EmDiagnostico);

        var action = () => ordemServico.AlterarParaFinalizada();

        var exception = Assert.Throws<DomainException>(action);
        Assert.Equal("Somente ordem de servico em execucao pode ser alterada para finalizada.", exception.Message);
    }

    [Fact]
    public void AlterarParaEntregue_DeveAlterarStatus_QuandoStatusForFinalizada()
    {
        var ordemServico = CriarOrdemNoStatus(StatusOrdemServico.Finalizada);

        ordemServico.AlterarParaEntregue();

        Assert.Equal(StatusOrdemServico.Entregue, ordemServico.Status);
    }

    [Fact]
    public void AlterarParaEntregue_DeveLancar_QuandoStatusForDiferente()
    {
        var ordemServico = CriarOrdem();

        var action = () => ordemServico.AlterarParaEntregue();

        var exception = Assert.Throws<DomainException>(action);
        Assert.Equal("Somente ordem de servico finalizada pode ser alterada para entregue.", exception.Message);
    }

    [Fact]
    public void Atualizar_DeveAlterarClienteVeiculoEServicos_QuandoOrdemRecebida()
    {
        var ordemServico = CriarOrdem();
        var clienteId = Guid.NewGuid();
        var veiculoId = Guid.NewGuid();
        var servicoId = Guid.NewGuid();

        ordemServico.Atualizar(clienteId, veiculoId, [servicoId, servicoId]);

        Assert.Equal(clienteId, ordemServico.ClienteId);
        Assert.Equal(veiculoId, ordemServico.VeiculoId);
        Assert.Equal([servicoId], ordemServico.ServicoIds);
        Assert.Equal(StatusOrdemServico.Recebida, ordemServico.Status);
        Assert.Single(ordemServico.HistoricoStatus);
    }

    [Theory]
    [InlineData(StatusOrdemServico.EmDiagnostico)]
    [InlineData(StatusOrdemServico.AguardandoAprovacao)]
    [InlineData(StatusOrdemServico.EmExecucao)]
    [InlineData(StatusOrdemServico.Cancelada)]
    public void Atualizar_DeveLancar_QuandoOrdemNaoEstiverRecebida(StatusOrdemServico statusAtual)
    {
        var ordemServico = CriarOrdemNoStatus(statusAtual);
        var clienteOriginal = ordemServico.ClienteId;

        var action = () => ordemServico.Atualizar(Guid.NewGuid(), Guid.NewGuid(), [Guid.NewGuid()]);

        var exception = Assert.Throws<DomainException>(action);
        Assert.Equal("Somente ordem de servico recebida pode ser atualizada.", exception.Message);
        Assert.Equal(clienteOriginal, ordemServico.ClienteId);
    }

    [Fact]
    public void Atualizar_DeveManterDadosAtuais_QuandoAlgumDadoForInvalido()
    {
        var ordemServico = CriarOrdem();
        var clienteOriginal = ordemServico.ClienteId;
        var servicosOriginais = ordemServico.ServicoIds.ToArray();

        var action = () => ordemServico.Atualizar(Guid.NewGuid(), Guid.NewGuid(), []);

        var exception = Assert.Throws<DomainException>(action);
        Assert.Equal("A ordem de servico deve possuir ao menos um servico.", exception.Message);
        Assert.Equal(clienteOriginal, ordemServico.ClienteId);
        Assert.Equal(servicosOriginais, ordemServico.ServicoIds);
    }

    [Theory]
    [InlineData(StatusOrdemServico.Recebida)]
    [InlineData(StatusOrdemServico.EmDiagnostico)]
    public void RegistrarOrcamentoGerado_DeveAlterarParaAguardandoAprovacaoComValor(StatusOrdemServico statusAtual)
    {
        var ordemServico = CriarOrdemNoStatus(statusAtual);
        var quantidadeHistorico = ordemServico.HistoricoStatus.Count;

        ordemServico.RegistrarOrcamentoGerado(250.75m, new DateTime(2024, 01, 01, 9, 00, 00, DateTimeKind.Utc));

        Assert.Equal(StatusOrdemServico.AguardandoAprovacao, ordemServico.Status);
        Assert.Equal(250.75m, ordemServico.ValorOrcamento);
        Assert.Equal(quantidadeHistorico + 1, ordemServico.HistoricoStatus.Count);
        Assert.Equal(StatusOrdemServico.AguardandoAprovacao, ordemServico.HistoricoStatus.Last().Status);
    }

    [Fact]
    public void RegistrarOrcamentoGerado_QuandoJaAguardandoAprovacao_DeveAtualizarValorSemNovoHistorico()
    {
        var ordemServico = CriarOrdem();
        ordemServico.RegistrarOrcamentoGerado(100m);

        ordemServico.RegistrarOrcamentoGerado(180m);

        Assert.Equal(StatusOrdemServico.AguardandoAprovacao, ordemServico.Status);
        Assert.Equal(180m, ordemServico.ValorOrcamento);
        Assert.Equal(2, ordemServico.HistoricoStatus.Count);
        Assert.Single(ordemServico.HistoricoStatus, historico => historico.Status == StatusOrdemServico.AguardandoAprovacao);
    }

    [Fact]
    public void RegistrarOrcamentoGerado_DevePermitirValorZero()
    {
        var ordemServico = CriarOrdem();

        ordemServico.RegistrarOrcamentoGerado(0m);

        Assert.Equal(0m, ordemServico.ValorOrcamento);
        Assert.Equal(StatusOrdemServico.AguardandoAprovacao, ordemServico.Status);
    }

    [Theory]
    [InlineData(StatusOrdemServico.EmExecucao)]
    [InlineData(StatusOrdemServico.Finalizada)]
    [InlineData(StatusOrdemServico.Cancelada)]
    public void RegistrarOrcamentoGerado_DeveLancar_QuandoStatusNaoPermitir(StatusOrdemServico statusAtual)
    {
        var ordemServico = CriarOrdemNoStatus(statusAtual);

        var action = () => ordemServico.RegistrarOrcamentoGerado(100m);

        var exception = Assert.Throws<DomainException>(action);
        Assert.Equal("Somente ordem de servico recebida, em diagnostico ou aguardando aprovacao pode ter orcamento gerado.", exception.Message);
        Assert.Equal(statusAtual, ordemServico.Status);
    }

    [Fact]
    public void RegistrarOrcamentoGerado_DeveLancar_QuandoValorForNegativo()
    {
        var ordemServico = CriarOrdem();

        var action = () => ordemServico.RegistrarOrcamentoGerado(-1m);

        var exception = Assert.Throws<DomainException>(action);
        Assert.Equal("O valor do orcamento da ordem de servico deve ser valido.", exception.Message);
        Assert.Equal(StatusOrdemServico.Recebida, ordemServico.Status);
        Assert.Null(ordemServico.ValorOrcamento);
    }

    [Fact]
    public void RegistrarOrcamentoAprovado_DeveAlterarParaEmExecucao_QuandoAguardandoAprovacao()
    {
        var ordemServico = CriarOrdemNoStatus(StatusOrdemServico.AguardandoAprovacao);

        ordemServico.RegistrarOrcamentoAprovado();

        Assert.Equal(StatusOrdemServico.EmExecucao, ordemServico.Status);
    }

    [Theory]
    [InlineData(StatusOrdemServico.Recebida)]
    [InlineData(StatusOrdemServico.EmExecucao)]
    public void RegistrarOrcamentoAprovado_DeveLancar_QuandoNaoEstiverAguardandoAprovacao(StatusOrdemServico statusAtual)
    {
        var ordemServico = CriarOrdemNoStatus(statusAtual);

        var action = () => ordemServico.RegistrarOrcamentoAprovado();

        var exception = Assert.Throws<DomainException>(action);
        Assert.Equal("Somente ordem de servico aguardando aprovacao pode ser aprovada.", exception.Message);
    }

    [Fact]
    public void RegistrarOrcamentoRecusado_DeveAlterarParaFinalizada_QuandoAguardandoAprovacao()
    {
        var ordemServico = CriarOrdemNoStatus(StatusOrdemServico.AguardandoAprovacao);

        ordemServico.RegistrarOrcamentoRecusado();

        Assert.Equal(StatusOrdemServico.Finalizada, ordemServico.Status);
    }

    [Fact]
    public void RegistrarOrcamentoRecusado_DeveLancar_QuandoNaoEstiverAguardandoAprovacao()
    {
        var ordemServico = CriarOrdem();

        var action = () => ordemServico.RegistrarOrcamentoRecusado();

        var exception = Assert.Throws<DomainException>(action);
        Assert.Equal("Somente ordem de servico aguardando aprovacao pode ser recusada.", exception.Message);
    }

    [Theory]
    [InlineData(StatusOrdemServico.Recebida)]
    [InlineData(StatusOrdemServico.EmDiagnostico)]
    [InlineData(StatusOrdemServico.AguardandoAprovacao)]
    [InlineData(StatusOrdemServico.EmExecucao)]
    public void Cancelar_DeveAlterarParaCanceladaComMotivo(StatusOrdemServico statusAtual)
    {
        var ordemServico = CriarOrdemNoStatus(statusAtual);
        var quantidadeHistorico = ordemServico.HistoricoStatus.Count;

        ordemServico.Cancelar("  Cliente nao encontrado.  ");

        Assert.Equal(StatusOrdemServico.Cancelada, ordemServico.Status);
        Assert.Equal("Cliente nao encontrado.", ordemServico.MotivoCancelamento);
        Assert.Equal(quantidadeHistorico + 1, ordemServico.HistoricoStatus.Count);
        Assert.Equal(StatusOrdemServico.Cancelada, ordemServico.HistoricoStatus.Last().Status);
    }

    [Theory]
    [InlineData(StatusOrdemServico.Finalizada)]
    [InlineData(StatusOrdemServico.Entregue)]
    [InlineData(StatusOrdemServico.Encerrada)]
    [InlineData(StatusOrdemServico.Cancelada)]
    public void Cancelar_DeveLancar_QuandoOrdemJaEstiverEncerrada(StatusOrdemServico statusAtual)
    {
        var ordemServico = CriarOrdemNoStatus(statusAtual);
        var quantidadeHistorico = ordemServico.HistoricoStatus.Count;

        var action = () => ordemServico.Cancelar("Outro motivo");

        var exception = Assert.Throws<DomainException>(action);
        Assert.Equal("Ordem de servico finalizada, entregue, encerrada ou cancelada nao pode ser cancelada.", exception.Message);
        Assert.Equal(statusAtual, ordemServico.Status);
        Assert.Equal(quantidadeHistorico, ordemServico.HistoricoStatus.Count);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Cancelar_DeveUsarMotivoPadrao_QuandoMotivoNaoInformado(string? motivo)
    {
        var ordemServico = CriarOrdem();

        ordemServico.Cancelar(motivo);

        Assert.Equal(StatusOrdemServico.Cancelada, ordemServico.Status);
        Assert.Equal("Motivo nao informado.", ordemServico.MotivoCancelamento);
    }

    [Fact]
    public void Cancelar_DeveLimitarMotivoAoTamanhoMaximo()
    {
        var ordemServico = CriarOrdem();

        ordemServico.Cancelar(new string('x', OrdemServico.TamanhoMaximoMotivoCancelamento + 50));

        Assert.Equal(OrdemServico.TamanhoMaximoMotivoCancelamento, ordemServico.MotivoCancelamento!.Length);
    }
}
