namespace TechChallenge.Oficina.OsService.UseCases.Features.Eventos.Interfaces;

// Servico que publicou o evento consumido (atributo Origem do item no DynamoDB), fixo pelo contrato de eventos
public static class OrigensHistoricoSaga
{
    public const string Management = "management";
    public const string Billing = "billing";
    public const string Execution = "execution";
}
