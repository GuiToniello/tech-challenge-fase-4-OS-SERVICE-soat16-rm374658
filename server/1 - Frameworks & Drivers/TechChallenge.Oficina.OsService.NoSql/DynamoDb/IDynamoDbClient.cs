using Amazon.DynamoDBv2.Model;

namespace TechChallenge.Oficina.OsService.NoSql.DynamoDb;

// Recorte do SDK do DynamoDB usado pelo servico: isola o AWSSDK e simplifica os testes do gateway do historico da saga.
internal interface IDynamoDbClient
{
    Task PutItemAsync(string tabela, Dictionary<string, AttributeValue> item, CancellationToken cancellationToken = default);

    // Todos os itens da particao, em ordem crescente da chave de ordenacao (percorre as paginas do Query)
    Task<IReadOnlyList<Dictionary<string, AttributeValue>>> QueryAsync(string tabela, string chaveParticao, string valorParticao, CancellationToken cancellationToken = default);

    // So para o DynamoDB Local: cria a tabela (chaves string, sob demanda) com TTL. Retorna false quando a tabela ja existia.
    Task<bool> CriarTabelaSeNaoExisteAsync(string tabela, string chaveParticao, string chaveOrdenacao, string atributoTtl, CancellationToken cancellationToken = default);
}
