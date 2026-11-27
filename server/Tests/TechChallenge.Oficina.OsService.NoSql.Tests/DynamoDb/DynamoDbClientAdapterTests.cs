using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Moq;
using TechChallenge.Oficina.OsService.NoSql.DynamoDb;
using Xunit;

namespace TechChallenge.Oficina.OsService.NoSql.Tests.DynamoDb;

public sealed class DynamoDbClientAdapterTests
{
    private const string Tabela = "techchallenge-oficina-os-saga-historico";

    private readonly Mock<IAmazonDynamoDB> _dynamoDbMock = new();

    private DynamoDbClientAdapter CriarAdapter() => new(_dynamoDbMock.Object);

    private static Dictionary<string, AttributeValue> CriarItem(string ocorridoEm) => new()
    {
        ["OrdemServicoId"] = new AttributeValue { S = "os-1" },
        ["OcorridoEm"] = new AttributeValue { S = ocorridoEm }
    };

    [Fact]
    public async Task PutItemAsync_DeveGravarOItemNaTabela()
    {
        var item = CriarItem("2026-11-27T10:00:00.000Z#abcd1234");

        await CriarAdapter().PutItemAsync(Tabela, item);

        _dynamoDbMock.Verify(d => d.PutItemAsync(
            It.Is<PutItemRequest>(r => r.TableName == Tabela && r.Item == item),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueryAsync_DeveConsultarAParticaoEmOrdemCrescentePercorrendoAsPaginas()
    {
        var ultimaChavePrimeiraPagina = CriarItem("2026-11-27T10:00:01.000Z#00000002");
        var requisicoes = new List<QueryRequest>();
        var respostas = new Queue<QueryResponse>(
        [
            new QueryResponse
            {
                Items = [CriarItem("2026-11-27T10:00:00.000Z#00000001"), ultimaChavePrimeiraPagina],
                LastEvaluatedKey = ultimaChavePrimeiraPagina
            },
            new QueryResponse { Items = [CriarItem("2026-11-27T10:00:02.000Z#00000003")], LastEvaluatedKey = null }
        ]);
        _dynamoDbMock
            .Setup(d => d.QueryAsync(It.IsAny<QueryRequest>(), It.IsAny<CancellationToken>()))
            .Callback<QueryRequest, CancellationToken>((request, _) => requisicoes.Add(request))
            .ReturnsAsync(() => respostas.Dequeue());

        var itens = await CriarAdapter().QueryAsync(Tabela, "OrdemServicoId", "os-1");

        Assert.Equal(
            ["2026-11-27T10:00:00.000Z#00000001", "2026-11-27T10:00:01.000Z#00000002", "2026-11-27T10:00:02.000Z#00000003"],
            itens.Select(item => item["OcorridoEm"].S));
        Assert.Equal(2, requisicoes.Count);
        Assert.All(requisicoes, request =>
        {
            Assert.Equal(Tabela, request.TableName);
            Assert.Equal("#particao = :particao", request.KeyConditionExpression);
            Assert.Equal("OrdemServicoId", request.ExpressionAttributeNames["#particao"]);
            Assert.Equal("os-1", request.ExpressionAttributeValues[":particao"].S);
            Assert.True(request.ScanIndexForward);
            Assert.True(request.ConsistentRead);
        });
        Assert.Null(requisicoes[0].ExclusiveStartKey);
        Assert.Same(ultimaChavePrimeiraPagina, requisicoes[1].ExclusiveStartKey);
    }

    [Fact]
    public async Task QueryAsync_DeveRetornarVazio_QuandoRespostaSemItens()
    {
        // No AWSSDK v4 as colecoes vazias chegam como null
        _dynamoDbMock
            .Setup(d => d.QueryAsync(It.IsAny<QueryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryResponse { Items = null, LastEvaluatedKey = null });

        var itens = await CriarAdapter().QueryAsync(Tabela, "OrdemServicoId", "os-1");

        Assert.Empty(itens);
        _dynamoDbMock.Verify(d => d.QueryAsync(It.IsAny<QueryRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CriarTabelaSeNaoExisteAsync_DeveCriarTabelaSobDemandaComChavesETtl()
    {
        CreateTableRequest? criacao = null;
        _dynamoDbMock
            .Setup(d => d.CreateTableAsync(It.IsAny<CreateTableRequest>(), It.IsAny<CancellationToken>()))
            .Callback<CreateTableRequest, CancellationToken>((request, _) => criacao = request)
            .ReturnsAsync(new CreateTableResponse());

        var criada = await CriarAdapter().CriarTabelaSeNaoExisteAsync(Tabela, "OrdemServicoId", "OcorridoEm", "ExpiraEm");

        Assert.True(criada);
        Assert.NotNull(criacao);
        Assert.Equal(Tabela, criacao.TableName);
        Assert.Equal(BillingMode.PAY_PER_REQUEST, criacao.BillingMode);
        Assert.Equal(
            [("OrdemServicoId", "S"), ("OcorridoEm", "S")],
            criacao.AttributeDefinitions.Select(atributo => (atributo.AttributeName, atributo.AttributeType.Value)));
        Assert.Equal(
            [("OrdemServicoId", "HASH"), ("OcorridoEm", "RANGE")],
            criacao.KeySchema.Select(chave => (chave.AttributeName, chave.KeyType.Value)));
        _dynamoDbMock.Verify(d => d.UpdateTimeToLiveAsync(
            It.Is<UpdateTimeToLiveRequest>(r => r.TableName == Tabela && r.TimeToLiveSpecification.AttributeName == "ExpiraEm" && r.TimeToLiveSpecification.Enabled == true),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CriarTabelaSeNaoExisteAsync_DeveRetornarFalseSemAlterarTtl_QuandoTabelaJaExiste()
    {
        _dynamoDbMock
            .Setup(d => d.CreateTableAsync(It.IsAny<CreateTableRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ResourceInUseException("Cannot create preexisting table"));

        var criada = await CriarAdapter().CriarTabelaSeNaoExisteAsync(Tabela, "OrdemServicoId", "OcorridoEm", "ExpiraEm");

        Assert.False(criada);
        _dynamoDbMock.Verify(d => d.UpdateTimeToLiveAsync(It.IsAny<UpdateTimeToLiveRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
