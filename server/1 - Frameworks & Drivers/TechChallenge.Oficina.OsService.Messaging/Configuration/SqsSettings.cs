namespace TechChallenge.Oficina.OsService.Messaging.Configuration;

public sealed class SqsSettings
{
    public const string SectionName = "SqsSettings";

    // Fila FIFO de onde o OS Service consome os eventos da Management. Vazia: o consumidor nao e iniciado.
    public string FilaConsumo { get; set; } = string.Empty;

    // Fila FIFO da Management, onde o OS Service publica os eventos da ordem de servico.
    public string FilaPublicacao { get; set; } = string.Empty;

    // Endpoint do emulador local (ElasticMQ). Vazio na AWS: regiao e credenciais vem do ambiente.
    public string ServiceUrl { get; set; } = string.Empty;
}
