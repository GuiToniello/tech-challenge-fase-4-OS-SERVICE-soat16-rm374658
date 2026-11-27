using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace TechChallenge.Oficina.OsService.NoSql.DynamoDb;

internal sealed class DynamoDbClientAdapter : IDynamoDbClient
{
    private readonly IAmazonDynamoDB _dynamoDb;

    public DynamoDbClientAdapter(IAmazonDynamoDB dynamoDb)
    {
        _dynamoDb = dynamoDb;
    }

    public async Task PutItemAsync(string tabela, Dictionary<string, AttributeValue> item, CancellationToken cancellationToken = default)
    {
        await _dynamoDb.PutItemAsync(new PutItemRequest { TableName = tabela, Item = item }, cancellationToken);
    }

    public async Task<IReadOnlyList<Dictionary<string, AttributeValue>>> QueryAsync(string tabela, string chaveParticao, string valorParticao, CancellationToken cancellationToken = default)
    {
        var itens = new List<Dictionary<string, AttributeValue>>();
        Dictionary<string, AttributeValue>? inicio = null;

        do
        {
            // Leitura consistente: a consulta logo depois de um registro ja o enxerga
            var response = await _dynamoDb.QueryAsync(new QueryRequest
            {
                TableName = tabela,
                KeyConditionExpression = "#particao = :particao",
                ExpressionAttributeNames = new Dictionary<string, string> { ["#particao"] = chaveParticao },
                ExpressionAttributeValues = new Dictionary<string, AttributeValue> { [":particao"] = new AttributeValue { S = valorParticao } },
                ScanIndexForward = true,
                ConsistentRead = true,
                ExclusiveStartKey = inicio
            }, cancellationToken);

            // No AWSSDK v4 as colecoes vazias da resposta chegam como null
            itens.AddRange(response.Items ?? []);
            inicio = response.LastEvaluatedKey is { Count: > 0 } ? response.LastEvaluatedKey : null;
        }
        while (inicio is not null);

        return itens;
    }

    public async Task<bool> CriarTabelaSeNaoExisteAsync(string tabela, string chaveParticao, string chaveOrdenacao, string atributoTtl, CancellationToken cancellationToken = default)
    {
        try
        {
            await _dynamoDb.CreateTableAsync(new CreateTableRequest
            {
                TableName = tabela,
                BillingMode = BillingMode.PAY_PER_REQUEST,
                AttributeDefinitions =
                [
                    new AttributeDefinition { AttributeName = chaveParticao, AttributeType = ScalarAttributeType.S },
                    new AttributeDefinition { AttributeName = chaveOrdenacao, AttributeType = ScalarAttributeType.S }
                ],
                KeySchema =
                [
                    new KeySchemaElement { AttributeName = chaveParticao, KeyType = KeyType.HASH },
                    new KeySchemaElement { AttributeName = chaveOrdenacao, KeyType = KeyType.RANGE }
                ]
            }, cancellationToken);
        }
        catch (ResourceInUseException)
        {
            // Tabela ja existe (ex.: a API reiniciou com o DynamoDB Local no ar)
            return false;
        }

        await _dynamoDb.UpdateTimeToLiveAsync(new UpdateTimeToLiveRequest
        {
            TableName = tabela,
            TimeToLiveSpecification = new TimeToLiveSpecification { AttributeName = atributoTtl, Enabled = true }
        }, cancellationToken);

        return true;
    }
}
