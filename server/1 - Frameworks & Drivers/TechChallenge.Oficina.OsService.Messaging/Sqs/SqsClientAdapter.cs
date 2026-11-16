using Amazon.SQS;
using Amazon.SQS.Model;

namespace TechChallenge.Oficina.OsService.Messaging.Sqs;

public sealed class SqsClientAdapter : ISqsClient
{
    public const string AtributoTipo = "tipo";

    private readonly IAmazonSQS _sqs;

    public SqsClientAdapter(IAmazonSQS sqs)
    {
        _sqs = sqs;
    }

    public async Task<string> GetQueueUrlAsync(string queueName, CancellationToken cancellationToken = default)
    {
        var response = await _sqs.GetQueueUrlAsync(new GetQueueUrlRequest { QueueName = queueName }, cancellationToken);
        return response.QueueUrl;
    }

    public async Task SendAsync(string queueUrl, string body, string tipo, string groupId, string deduplicationId, CancellationToken cancellationToken = default)
    {
        await _sqs.SendMessageAsync(new SendMessageRequest
        {
            QueueUrl = queueUrl,
            MessageBody = body,
            MessageGroupId = groupId,
            MessageDeduplicationId = deduplicationId,
            MessageAttributes = new Dictionary<string, MessageAttributeValue>
            {
                [AtributoTipo] = new MessageAttributeValue { DataType = "String", StringValue = tipo }
            }
        }, cancellationToken);
    }

    public async Task<IReadOnlyCollection<MensagemSqs>> ReceiveAsync(string queueUrl, int maxMessages, int waitTimeSeconds, CancellationToken cancellationToken = default)
    {
        var response = await _sqs.ReceiveMessageAsync(new ReceiveMessageRequest
        {
            QueueUrl = queueUrl,
            MaxNumberOfMessages = maxMessages,
            WaitTimeSeconds = waitTimeSeconds,
            MessageAttributeNames = ["All"]
        }, cancellationToken);

        // No AWSSDK v4 a lista vazia da resposta chega como null
        return (response.Messages ?? [])
            .Select(mensagem => new MensagemSqs(mensagem.MessageId, mensagem.ReceiptHandle, mensagem.Body))
            .ToArray();
    }

    public async Task DeleteAsync(string queueUrl, string receiptHandle, CancellationToken cancellationToken = default)
    {
        await _sqs.DeleteMessageAsync(new DeleteMessageRequest { QueueUrl = queueUrl, ReceiptHandle = receiptHandle }, cancellationToken);
    }
}
