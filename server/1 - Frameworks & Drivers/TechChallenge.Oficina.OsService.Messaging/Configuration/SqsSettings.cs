namespace TechChallenge.Oficina.OsService.Messaging.Configuration;

public sealed class SqsSettings
{
    public const string SectionName = "SqsSettings";

    // Fila FIFO de onde o OS Service consome os eventos da Management, do Billing e da Execucao. Vazia: o consumidor nao e iniciado.
    public string FilaConsumo { get; set; } = string.Empty;

    // Filas FIFO onde o OS Service publica os eventos da ordem de servico (fan-out): a da Management, a do Billing e a da Execucao.
    // Cada evento vai, com o mesmo envelope, para todas as filas da lista; quem nao trata o tipo o ignora.
    public string[] FilasPublicacao { get; set; } = [];

    // Endpoint do emulador local (ElasticMQ). Vazio na AWS: regiao e credenciais vem do ambiente.
    public string ServiceUrl { get; set; } = string.Empty;
}
