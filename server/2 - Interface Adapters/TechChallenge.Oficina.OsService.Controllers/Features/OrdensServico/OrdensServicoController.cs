using Microsoft.Extensions.Logging;
using TechChallenge.Oficina.OsService.Entities.Exceptions;
using TechChallenge.Oficina.OsService.UseCases.Features.OrdensServico.Commands;
using TechChallenge.Oficina.OsService.UseCases.Features.OrdensServico.Logging;
using TechChallenge.Oficina.OsService.UseCases.Features.OrdensServico.Queries;
using TechChallenge.Oficina.OsService.UseCases.Features.OrdensServico.UseCases;
using TechChallenge.Oficina.OsService.UseCases.Features.OrdensServico.ViewModels;

namespace TechChallenge.Oficina.OsService.Controllers.Features.OrdensServico
{
    public class OrdensServicoController : IOrdensServicoController
    {
        private readonly IOrdemServicoUseCases _ordemServicoUseCases;
        private readonly IOrdensServicoAdapter _ordensServicoAdapter;
        private readonly ILogger<OrdensServicoController> _logger;

        public OrdensServicoController(
            IOrdemServicoUseCases ordemServicoUseCases,
            IOrdensServicoAdapter ordensServicoAdapter,
            ILogger<OrdensServicoController> logger)
        {
            _ordemServicoUseCases = ordemServicoUseCases;
            _ordensServicoAdapter = ordensServicoAdapter;
            _logger = logger;
        }

        public async Task<object> Post(CriarOrdemServicoCommand command, CancellationToken cancellationToken)
        {
            try
            {
                var ordemServico = await _ordemServicoUseCases.CriarAsync(command, cancellationToken);
                var result = OrdensServicoResult.From(ordemServico);

                return _ordensServicoAdapter.Adapt(result, true);
            }
            catch (DomainException exception)
            {
                RegistrarFalha(OrdemServicoLogEventos.Etapas.Abertura, null, exception);
                var result = OrdensServicoResult.FromError<OrdemServicoViewModel>(exception);

                return _ordensServicoAdapter.Adapt(result);
            }
        }

        public async Task<object> GetById(Guid id, CancellationToken cancellationToken)
        {
            try
            {
                var query = new ObterOrdemServicoPorIdQuery { Id = id };
                var ordemServico = await _ordemServicoUseCases.ObterPorIdAsync(query, cancellationToken);
                var result = OrdensServicoResult.From(ordemServico);

                return _ordensServicoAdapter.Adapt(result);
            }
            catch (KeyNotFoundException exception)
            {
                var result = OrdensServicoResult.FromError<OrdemServicoViewModel>(exception);
                return _ordensServicoAdapter.Adapt(result);
            }
        }

        public async Task<object> Get(CancellationToken cancellationToken)
        {
            var ordensServico = await _ordemServicoUseCases.ListarAsync(new ListarOrdensServicoQuery(), cancellationToken);
            var result = OrdensServicoResult.From(ordensServico);

            return _ordensServicoAdapter.Adapt(result);
        }

        public async Task<object> GetOrdenadas(CancellationToken cancellationToken)
        {
            var ordensServico = await _ordemServicoUseCases.ListarOrdenadasAsync(new ListarOrdensServicoOrdenadasQuery(), cancellationToken);
            var result = OrdensServicoResult.From(ordensServico);

            return _ordensServicoAdapter.Adapt(result);
        }

        public async Task<object> GetAcompanhamento(Guid id, CancellationToken cancellationToken)
        {
            try
            {
                var acompanhamento = await _ordemServicoUseCases.ObterAcompanhamentoAsync(new ObterAcompanhamentoOrdemServicoPorIdQuery { Id = id }, cancellationToken);
                var result = OrdensServicoResult.From(acompanhamento);

                return _ordensServicoAdapter.Adapt(result);
            }
            catch (KeyNotFoundException exception)
            {
                var result = OrdensServicoResult.FromError<AcompanhamentoOrdemServicoViewModel>(exception);
                return _ordensServicoAdapter.Adapt(result);
            }
        }

        public async Task<object> GetByCliente(Guid clienteId, CancellationToken cancellationToken)
        {
            var acompanhamentos = await _ordemServicoUseCases.ListarPorClienteAsync(new ListarOrdensServicoPorClienteQuery { ClienteId = clienteId }, cancellationToken);
            var result = OrdensServicoResult.From(acompanhamentos);

            return _ordensServicoAdapter.Adapt(result);
        }

        public async Task<object> Put(AtualizarOrdemServicoCommand command, CancellationToken cancellationToken)
        {
            try
            {
                var ordemServico = await _ordemServicoUseCases.AtualizarAsync(command, cancellationToken);
                var result = OrdensServicoResult.From(ordemServico);

                return _ordensServicoAdapter.Adapt(result);
            }
            catch (DomainException exception)
            {
                RegistrarFalha(OrdemServicoLogEventos.Etapas.Atualizacao, command.Id, exception);
                var result = OrdensServicoResult.FromError<OrdemServicoViewModel>(exception);

                return _ordensServicoAdapter.Adapt(result);
            }
            catch (KeyNotFoundException exception)
            {
                RegistrarFalha(OrdemServicoLogEventos.Etapas.Atualizacao, command.Id, exception);
                var result = OrdensServicoResult.FromError<OrdemServicoViewModel>(exception);

                return _ordensServicoAdapter.Adapt(result);
            }
        }

        public async Task<object> Delete(Guid id, CancellationToken cancellationToken)
        {
            try
            {
                var command = new ExcluirOrdemServicoCommand { Id = id };
                await _ordemServicoUseCases.ExcluirAsync(command, cancellationToken);

                return _ordensServicoAdapter.Empty();
            }
            catch (KeyNotFoundException exception)
            {
                RegistrarFalha(OrdemServicoLogEventos.Etapas.Exclusao, id, exception);
                var result = OrdensServicoResult.FromError<bool>(exception);
                return _ordensServicoAdapter.Adapt(result);
            }
        }

        public Task<object> AlterarParaEmDiagnostico(Guid id, CancellationToken cancellationToken)
        {
            return AlterarStatusAsync(id, OrdemServicoLogEventos.Etapas.Diagnostico, _ordemServicoUseCases.AlterarStatusParaEmDiagnosticoAsync, cancellationToken);
        }

        public Task<object> AlterarParaEmExecucao(Guid id, CancellationToken cancellationToken)
        {
            return AlterarStatusAsync(id, OrdemServicoLogEventos.Etapas.Execucao, _ordemServicoUseCases.AlterarStatusParaEmExecucaoAsync, cancellationToken);
        }

        public Task<object> AlterarParaFinalizada(Guid id, CancellationToken cancellationToken)
        {
            return AlterarStatusAsync(id, OrdemServicoLogEventos.Etapas.Finalizacao, _ordemServicoUseCases.AlterarStatusParaFinalizadaAsync, cancellationToken);
        }

        public Task<object> AlterarParaEntregue(Guid id, CancellationToken cancellationToken)
        {
            return AlterarStatusAsync(id, OrdemServicoLogEventos.Etapas.Entrega, _ordemServicoUseCases.AlterarStatusParaEntregueAsync, cancellationToken);
        }

        private async Task<object> AlterarStatusAsync(
            Guid id,
            string etapa,
            Func<AlterarStatusOrdemServicoCommand, CancellationToken, Task<OrdemServicoViewModel>> alterarStatus,
            CancellationToken cancellationToken)
        {
            try
            {
                var ordemServico = await alterarStatus(new AlterarStatusOrdemServicoCommand { Id = id }, cancellationToken);
                var result = OrdensServicoResult.From(ordemServico);

                return _ordensServicoAdapter.Adapt(result);
            }
            catch (DomainException exception)
            {
                RegistrarFalha(etapa, id, exception);
                var result = OrdensServicoResult.FromError<OrdemServicoViewModel>(exception);
                return _ordensServicoAdapter.Adapt(result);
            }
            catch (KeyNotFoundException exception)
            {
                RegistrarFalha(etapa, id, exception);
                var result = OrdensServicoResult.FromError<OrdemServicoViewModel>(exception);
                return _ordensServicoAdapter.Adapt(result);
            }
        }

        private void RegistrarFalha(string etapa, Guid? ordemServicoId, Exception exception)
        {
            var tipoFalha = exception is KeyNotFoundException
                ? OrdemServicoLogEventos.TiposFalha.NaoEncontrado
                : OrdemServicoLogEventos.TiposFalha.RegraNegocio;

            // Rejeicao de regra de negocio nao e erro de aplicacao: registra so o motivo, sem stack trace
            _logger.LogWarning(
                OrdemServicoLogEventos.OsFalha,
                "Falha na etapa {Etapa} da ordem de servico {OrdemServicoId} ({TipoFalha}): {Motivo}",
                etapa,
                ordemServicoId,
                tipoFalha,
                exception.Message);
        }
    }
}
