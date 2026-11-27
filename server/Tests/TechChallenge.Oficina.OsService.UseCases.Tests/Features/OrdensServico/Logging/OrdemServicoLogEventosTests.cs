using TechChallenge.Oficina.OsService.Entities.Features.OrdensServico.Enums;
using TechChallenge.Oficina.OsService.UseCases.Features.OrdensServico.Logging;
using Xunit;

namespace TechChallenge.Oficina.OsService.UseCases.Tests.Features.OrdensServico.Logging;

public sealed class OrdemServicoLogEventosTests
{
    [Theory]
    [InlineData(StatusOrdemServico.Recebida, "recebida")]
    [InlineData(StatusOrdemServico.EmDiagnostico, "em_diagnostico")]
    [InlineData(StatusOrdemServico.AguardandoAprovacao, "aguardando_aprovacao")]
    [InlineData(StatusOrdemServico.EmExecucao, "em_execucao")]
    [InlineData(StatusOrdemServico.Finalizada, "finalizada")]
    [InlineData(StatusOrdemServico.Entregue, "entregue")]
    [InlineData(StatusOrdemServico.Encerrada, "encerrada")]
    [InlineData(StatusOrdemServico.Cancelada, "cancelada")]
    public void StatusParaLog_DeveRetornarValorDoContrato(StatusOrdemServico status, string valorEsperado)
    {
        Assert.Equal(valorEsperado, OrdemServicoLogEventos.StatusParaLog(status));
    }

    [Fact]
    public void StatusParaLog_DeveLancar_QuandoStatusInvalido()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => OrdemServicoLogEventos.StatusParaLog((StatusOrdemServico)99));
    }

    [Fact]
    public void Eventos_DevemManterNomesEIdsDoContrato()
    {
        Assert.Equal((1001, "os.criada"), (OrdemServicoLogEventos.OsCriada.Id, OrdemServicoLogEventos.OsCriada.Name));
        Assert.Equal((1002, "os.status_alterado"), (OrdemServicoLogEventos.OsStatusAlterado.Id, OrdemServicoLogEventos.OsStatusAlterado.Name));
        Assert.Equal((1003, "os.encerrada"), (OrdemServicoLogEventos.OsEncerrada.Id, OrdemServicoLogEventos.OsEncerrada.Name));
        Assert.Equal((2001, "os.falha"), (OrdemServicoLogEventos.OsFalha.Id, OrdemServicoLogEventos.OsFalha.Name));
        Assert.Equal((3001, "integracao.falha"), (OrdemServicoLogEventos.IntegracaoFalha.Id, OrdemServicoLogEventos.IntegracaoFalha.Name));
        Assert.Equal((4001, "evento.publicado"), (OrdemServicoLogEventos.EventoPublicado.Id, OrdemServicoLogEventos.EventoPublicado.Name));
        Assert.Equal((4002, "evento.consumido"), (OrdemServicoLogEventos.EventoConsumido.Id, OrdemServicoLogEventos.EventoConsumido.Name));
        Assert.Equal((4003, "evento.ignorado"), (OrdemServicoLogEventos.EventoIgnorado.Id, OrdemServicoLogEventos.EventoIgnorado.Name));
    }

    [Fact]
    public void Etapas_TiposIntegracoesEOperacoes_DevemManterValoresDoContrato()
    {
        Assert.Equal(
            ["abertura", "atualizacao", "exclusao", "diagnostico", "orcamento_geracao", "orcamento_aprovacao", "orcamento_recusa", "finalizacao", "entrega", "cancelamento"],
            new[]
            {
                OrdemServicoLogEventos.Etapas.Abertura, OrdemServicoLogEventos.Etapas.Atualizacao, OrdemServicoLogEventos.Etapas.Exclusao,
                OrdemServicoLogEventos.Etapas.Diagnostico, OrdemServicoLogEventos.Etapas.OrcamentoGeracao, OrdemServicoLogEventos.Etapas.OrcamentoAprovacao,
                OrdemServicoLogEventos.Etapas.OrcamentoRecusa, OrdemServicoLogEventos.Etapas.Finalizacao, OrdemServicoLogEventos.Etapas.Entrega,
                OrdemServicoLogEventos.Etapas.Cancelamento
            });
        Assert.Equal(
            ["regra_negocio", "nao_encontrado"],
            new[] { OrdemServicoLogEventos.TiposFalha.RegraNegocio, OrdemServicoLogEventos.TiposFalha.NaoEncontrado });
        Assert.Equal(
            ["postgres", "sqs", "dynamodb"],
            new[] { OrdemServicoLogEventos.Integracoes.Postgres, OrdemServicoLogEventos.Integracoes.Sqs, OrdemServicoLogEventos.Integracoes.DynamoDb });
        Assert.Equal(
            ["publicacao", "consumo", "registro_historico", "consulta_historico"],
            new[]
            {
                OrdemServicoLogEventos.Operacoes.Publicacao, OrdemServicoLogEventos.Operacoes.Consumo,
                OrdemServicoLogEventos.Operacoes.RegistroHistorico, OrdemServicoLogEventos.Operacoes.ConsultaHistorico
            });
    }
}
