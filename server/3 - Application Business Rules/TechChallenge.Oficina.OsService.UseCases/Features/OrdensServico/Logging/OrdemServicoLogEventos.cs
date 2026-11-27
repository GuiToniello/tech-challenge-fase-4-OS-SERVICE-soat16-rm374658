using Microsoft.Extensions.Logging;
using TechChallenge.Oficina.OsService.Entities.Features.OrdensServico.Enums;

namespace TechChallenge.Oficina.OsService.UseCases.Features.OrdensServico.Logging;

// Catálogo dos eventos de log de negócio da ordem de serviço. Nomes e valores são o contrato com as métricas e monitores do Datadog: não renomear sem atualizar os dois lados.
public static class OrdemServicoLogEventos
{
    public static readonly EventId OsCriada = new(1001, "os.criada");
    public static readonly EventId OsStatusAlterado = new(1002, "os.status_alterado");
    public static readonly EventId OsEncerrada = new(1003, "os.encerrada");
    public static readonly EventId OsFalha = new(2001, "os.falha");
    public static readonly EventId IntegracaoFalha = new(3001, "integracao.falha");
    public static readonly EventId EventoPublicado = new(4001, "evento.publicado");
    public static readonly EventId EventoConsumido = new(4002, "evento.consumido");
    public static readonly EventId EventoIgnorado = new(4003, "evento.ignorado");

    public static class Etapas
    {
        public const string Abertura = "abertura";
        public const string Atualizacao = "atualizacao";
        public const string Exclusao = "exclusao";
        public const string Diagnostico = "diagnostico";
        public const string OrcamentoGeracao = "orcamento_geracao";
        public const string OrcamentoAprovacao = "orcamento_aprovacao";
        public const string OrcamentoRecusa = "orcamento_recusa";
        public const string Finalizacao = "finalizacao";
        public const string Entrega = "entrega";
        public const string Cancelamento = "cancelamento";
    }

    public static class TiposFalha
    {
        public const string RegraNegocio = "regra_negocio";
        public const string NaoEncontrado = "nao_encontrado";
    }

    public static class Integracoes
    {
        public const string Postgres = "postgres";
        public const string Sqs = "sqs";
        public const string DynamoDb = "dynamodb";
    }

    public static class Operacoes
    {
        public const string Publicacao = "publicacao";
        public const string Consumo = "consumo";
        public const string RegistroHistorico = "registro_historico";
        public const string ConsultaHistorico = "consulta_historico";
    }

    public static string StatusParaLog(StatusOrdemServico status)
    {
        return status switch
        {
            StatusOrdemServico.Recebida => "recebida",
            StatusOrdemServico.EmDiagnostico => "em_diagnostico",
            StatusOrdemServico.AguardandoAprovacao => "aguardando_aprovacao",
            StatusOrdemServico.EmExecucao => "em_execucao",
            StatusOrdemServico.Finalizada => "finalizada",
            StatusOrdemServico.Entregue => "entregue",
            StatusOrdemServico.Encerrada => "encerrada",
            StatusOrdemServico.Cancelada => "cancelada",
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Status da ordem de servico invalido.")
        };
    }
}
