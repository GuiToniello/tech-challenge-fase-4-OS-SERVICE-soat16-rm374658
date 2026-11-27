namespace TechChallenge.Oficina.OsService.UseCases.Features.Eventos.Interfaces;

// Porta de saida para o historico da saga (Amazon DynamoDB no Frameworks & Drivers): cada evento consumido com o veredito do OS Service.
// O historico e complementar e descartavel: a fonte da verdade continua no PostgreSQL e nenhuma regra le o historico para decidir algo.
public interface IHistoricoSagaGateway
{
    // Best-effort: falha de integracao nao lanca excecao (a implementacao registra integracao.falha e segue)
    Task RegistrarAsync(EventoHistoricoSaga evento, CancellationToken cancellationToken);

    // Eventos da ordem de servico em ordem cronologica; vazio quando nao ha registros
    Task<IReadOnlyCollection<EventoHistoricoSaga>> ListarAsync(Guid ordemServicoId, CancellationToken cancellationToken);
}
