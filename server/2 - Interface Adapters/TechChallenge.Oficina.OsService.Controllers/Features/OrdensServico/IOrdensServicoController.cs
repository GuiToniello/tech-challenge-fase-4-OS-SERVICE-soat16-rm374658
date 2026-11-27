using TechChallenge.Oficina.OsService.UseCases.Features.OrdensServico.Commands;

namespace TechChallenge.Oficina.OsService.Controllers.Features.OrdensServico
{
    public interface IOrdensServicoController
    {
        Task<object> Post(CriarOrdemServicoCommand command, CancellationToken cancellationToken);

        Task<object> GetById(Guid id, CancellationToken cancellationToken);

        Task<object> Get(CancellationToken cancellationToken);

        Task<object> GetOrdenadas(CancellationToken cancellationToken);

        Task<object> GetAcompanhamento(Guid id, CancellationToken cancellationToken);

        Task<object> GetByCliente(Guid clienteId, CancellationToken cancellationToken);

        Task<object> GetHistoricoSaga(Guid id, CancellationToken cancellationToken);

        Task<object> Put(AtualizarOrdemServicoCommand command, CancellationToken cancellationToken);

        Task<object> Delete(Guid id, CancellationToken cancellationToken);

        Task<object> AlterarParaEntregue(Guid id, CancellationToken cancellationToken);
    }
}
