namespace TechChallenge.Oficina.OsService.Messaging.Sqs;

// Recorte do SDK do SQS usado pelo servico: isola o AWSSDK e simplifica os testes do publisher e do consumidor.
public interface ISqsClient
{
    Task<string> GetQueueUrlAsync(string queueName, CancellationToken cancellationToken = default);

    Task SendAsync(string queueUrl, string body, string tipo, string groupId, string deduplicationId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<MensagemSqs>> ReceiveAsync(string queueUrl, int maxMessages, int waitTimeSeconds, CancellationToken cancellationToken = default);

    Task DeleteAsync(string queueUrl, string receiptHandle, CancellationToken cancellationToken = default);
}
