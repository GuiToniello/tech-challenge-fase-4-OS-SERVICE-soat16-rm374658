using Amazon.SQS;
using Amazon.SQS.Model;
using Moq;
using TechChallenge.Oficina.OsService.Messaging.Sqs;
using Xunit;

namespace TechChallenge.Oficina.OsService.Messaging.Tests.Sqs;

public sealed class SqsClientAdapterTests
{
    private const string UrlFila = "http://elasticmq:9324/000000000000/techchallenge-oficina-management.fifo";

    private readonly Mock<IAmazonSQS> _sqsMock = new();

    private SqsClientAdapter CriarAdapter() => new(_sqsMock.Object);

    [Fact]
    public async Task GetQueueUrlAsync_DeveRetornarUrlDaFilaPeloNome()
    {
        _sqsMock
            .Setup(s => s.GetQueueUrlAsync(It.Is<GetQueueUrlRequest>(r => r.QueueName == "techchallenge-oficina-management.fifo"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetQueueUrlResponse { QueueUrl = UrlFila });

        var url = await CriarAdapter().GetQueueUrlAsync("techchallenge-oficina-management.fifo");

        Assert.Equal(UrlFila, url);
    }

    [Fact]
    public async Task SendAsync_DeveEnviarComGrupoDeduplicacaoEAtributoTipo()
    {
        SendMessageRequest? enviada = null;
        _sqsMock
            .Setup(s => s.SendMessageAsync(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .Callback<SendMessageRequest, CancellationToken>((request, _) => enviada = request)
            .ReturnsAsync(new SendMessageResponse());

        await CriarAdapter().SendAsync(UrlFila, "{\"tipo\":\"OrdemServicoAberta\"}", "OrdemServicoAberta", "grupo-os", "dedup-envelope");

        Assert.NotNull(enviada);
        Assert.Equal(UrlFila, enviada.QueueUrl);
        Assert.Equal("{\"tipo\":\"OrdemServicoAberta\"}", enviada.MessageBody);
        Assert.Equal("grupo-os", enviada.MessageGroupId);
        Assert.Equal("dedup-envelope", enviada.MessageDeduplicationId);
        var atributo = Assert.Single(enviada.MessageAttributes);
        Assert.Equal("tipo", atributo.Key);
        Assert.Equal("String", atributo.Value.DataType);
        Assert.Equal("OrdemServicoAberta", atributo.Value.StringValue);
    }

    [Fact]
    public async Task ReceiveAsync_DeveUsarLongPollingEConverterMensagens()
    {
        ReceiveMessageRequest? recebida = null;
        _sqsMock
            .Setup(s => s.ReceiveMessageAsync(It.IsAny<ReceiveMessageRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ReceiveMessageRequest, CancellationToken>((request, _) => recebida = request)
            .ReturnsAsync(new ReceiveMessageResponse
            {
                Messages = [new Message { MessageId = "m1", ReceiptHandle = "r1", Body = "{}" }]
            });

        var mensagens = await CriarAdapter().ReceiveAsync(UrlFila, 10, 20);

        var mensagem = Assert.Single(mensagens);
        Assert.Equal(new MensagemSqs("m1", "r1", "{}"), mensagem);
        Assert.NotNull(recebida);
        Assert.Equal(UrlFila, recebida.QueueUrl);
        Assert.Equal(10, recebida.MaxNumberOfMessages);
        Assert.Equal(20, recebida.WaitTimeSeconds);
        Assert.Contains("All", recebida.MessageAttributeNames);
    }

    [Fact]
    public async Task ReceiveAsync_DeveRetornarVazio_QuandoRespostaSemMensagens()
    {
        _sqsMock
            .Setup(s => s.ReceiveMessageAsync(It.IsAny<ReceiveMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReceiveMessageResponse { Messages = null });

        var mensagens = await CriarAdapter().ReceiveAsync(UrlFila, 10, 20);

        Assert.Empty(mensagens);
    }

    [Fact]
    public async Task DeleteAsync_DeveExcluirPeloReceiptHandle()
    {
        await CriarAdapter().DeleteAsync(UrlFila, "recibo-1");

        _sqsMock.Verify(s => s.DeleteMessageAsync(
            It.Is<DeleteMessageRequest>(r => r.QueueUrl == UrlFila && r.ReceiptHandle == "recibo-1"),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
