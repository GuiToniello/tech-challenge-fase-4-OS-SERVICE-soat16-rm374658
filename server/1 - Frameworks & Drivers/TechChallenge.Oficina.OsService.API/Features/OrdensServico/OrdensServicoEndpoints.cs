using TechChallenge.Oficina.OsService.Controllers.Features.OrdensServico;
using TechChallenge.Oficina.OsService.UseCases.Features.OrdensServico.Commands;
using TechChallenge.Oficina.OsService.UseCases.Features.OrdensServico.ViewModels;

namespace TechChallenge.Oficina.OsService.API.Features.OrdensServico
{
    public static class OrdensServicoEndpoints
    {
        public static RouteGroupBuilder MapOrdensServicoEndpoints(this IEndpointRouteBuilder routes)
        {
            var group = routes
                .MapGroup("/api/ordens-servico")
                .WithTags("OrdensServico");

            group.MapPost(
                string.Empty,
                (
                    IOrdensServicoController ordensServicoController,
                    CriarOrdemServicoCommand command,
                    CancellationToken cancellationToken
                ) => ordensServicoController.Post(command, cancellationToken))
                .Produces<OrdemServicoViewModel>(StatusCodes.Status201Created)
                .Produces(StatusCodes.Status400BadRequest);

            group.MapGet(
                "/{id:guid}",
                (
                    IOrdensServicoController ordensServicoController,
                    Guid id,
                    CancellationToken cancellationToken
                ) => ordensServicoController.GetById(id, cancellationToken))
                .WithName("GetOrdemServicoById")
                .Produces<OrdemServicoViewModel>(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status404NotFound);

            group.MapGet(
                string.Empty,
                (
                    IOrdensServicoController ordensServicoController,
                    CancellationToken cancellationToken
                ) => ordensServicoController.Get(cancellationToken))
                .Produces<IReadOnlyCollection<OrdemServicoViewModel>>(StatusCodes.Status200OK);

            group.MapGet(
                "/ordenadas",
                (
                    IOrdensServicoController ordensServicoController,
                    CancellationToken cancellationToken
                ) => ordensServicoController.GetOrdenadas(cancellationToken))
                .Produces<IReadOnlyCollection<OrdemServicoOrdenadasViewModel>>(StatusCodes.Status200OK);

            group.MapGet(
                "/{id:guid}/acompanhamento",
                (
                    IOrdensServicoController ordensServicoController,
                    Guid id,
                    CancellationToken cancellationToken
                ) => ordensServicoController.GetAcompanhamento(id, cancellationToken))
                .Produces<AcompanhamentoOrdemServicoViewModel>(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status404NotFound);

            group.MapGet(
                "/cliente/{clienteId:guid}",
                (
                    IOrdensServicoController ordensServicoController,
                    Guid clienteId,
                    CancellationToken cancellationToken
                ) => ordensServicoController.GetByCliente(clienteId, cancellationToken))
                .Produces<IReadOnlyCollection<AcompanhamentoOrdemServicoViewModel>>(StatusCodes.Status200OK);

            group.MapPut(
                string.Empty,
                (
                    IOrdensServicoController ordensServicoController,
                    AtualizarOrdemServicoCommand command,
                    CancellationToken cancellationToken
                ) => ordensServicoController.Put(command, cancellationToken))
                .Produces<OrdemServicoViewModel>(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status404NotFound);

            group.MapDelete(
                "/{id:guid}",
                (
                    IOrdensServicoController ordensServicoController,
                    Guid id,
                    CancellationToken cancellationToken
                ) => ordensServicoController.Delete(id, cancellationToken))
                .Produces(StatusCodes.Status204NoContent)
                .Produces(StatusCodes.Status404NotFound);

            group.MapPost(
                "/{id:guid}/em-diagnostico",
                (
                    IOrdensServicoController ordensServicoController,
                    Guid id,
                    CancellationToken cancellationToken
                ) => ordensServicoController.AlterarParaEmDiagnostico(id, cancellationToken))
                .Produces<OrdemServicoViewModel>(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status404NotFound);

            group.MapPost(
                "/{id:guid}/em-execucao",
                (
                    IOrdensServicoController ordensServicoController,
                    Guid id,
                    CancellationToken cancellationToken
                ) => ordensServicoController.AlterarParaEmExecucao(id, cancellationToken))
                .Produces<OrdemServicoViewModel>(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status404NotFound);

            group.MapPost(
                "/{id:guid}/finalizar",
                (
                    IOrdensServicoController ordensServicoController,
                    Guid id,
                    CancellationToken cancellationToken
                ) => ordensServicoController.AlterarParaFinalizada(id, cancellationToken))
                .Produces<OrdemServicoViewModel>(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status404NotFound);

            group.MapPost(
                "/{id:guid}/entregar",
                (
                    IOrdensServicoController ordensServicoController,
                    Guid id,
                    CancellationToken cancellationToken
                ) => ordensServicoController.AlterarParaEntregue(id, cancellationToken))
                .Produces<OrdemServicoViewModel>(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status404NotFound);

            return group;
        }
    }
}
