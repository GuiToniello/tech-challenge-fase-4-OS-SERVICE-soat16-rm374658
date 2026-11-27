using AutoMapper;
using Microsoft.Extensions.Logging;
using TechChallenge.Oficina.OsService.Entities.Features.OrdensServico;
using TechChallenge.Oficina.OsService.Entities.Features.OrdensServico.Enums;
using TechChallenge.Oficina.OsService.UseCases.Features.Eventos.Contratos;
using TechChallenge.Oficina.OsService.UseCases.Features.Eventos.Interfaces;
using TechChallenge.Oficina.OsService.UseCases.Features.Indicadores.UseCases;
using TechChallenge.Oficina.OsService.UseCases.Features.OrdensServico.Commands;
using TechChallenge.Oficina.OsService.UseCases.Features.OrdensServico.Logging;
using TechChallenge.Oficina.OsService.UseCases.Features.OrdensServico.Queries;
using TechChallenge.Oficina.OsService.UseCases.Features.OrdensServico.ViewModels;

namespace TechChallenge.Oficina.OsService.UseCases.Features.OrdensServico.UseCases;

// Toda alteracao e gravada antes de publicar o evento. Se a publicacao falhar, a excecao sobe (o middleware responde 500) e a alteracao ja gravada permanece.
// Depois de gravar, a publicacao nao e cancelada junto com a requisicao (CancellationToken.None): o evento precisa sair para a Management, o Billing e a Execucao.
public sealed class OrdemServicoUseCases : IOrdemServicoUseCases
{
    private readonly IMapper _mapper;
    private readonly IOrdemServicoGateway _ordemServicoGateway;
    private readonly IIndicadorUseCases _indicadorUseCases;
    private readonly IEventosPublisher _eventosPublisher;
    private readonly IHistoricoSagaGateway _historicoSagaGateway;
    private readonly ILogger<OrdemServicoUseCases> _logger;

    public OrdemServicoUseCases(
        IMapper mapper,
        IOrdemServicoGateway ordemServicoGateway,
        IIndicadorUseCases indicadorUseCases,
        IEventosPublisher eventosPublisher,
        IHistoricoSagaGateway historicoSagaGateway,
        ILogger<OrdemServicoUseCases> logger)
    {
        _mapper = mapper;
        _ordemServicoGateway = ordemServicoGateway;
        _indicadorUseCases = indicadorUseCases;
        _eventosPublisher = eventosPublisher;
        _historicoSagaGateway = historicoSagaGateway;
        _logger = logger;
    }

    public async Task<OrdemServicoViewModel> CriarAsync(CriarOrdemServicoCommand command, CancellationToken cancellationToken = default)
    {
        // Cliente, veiculo e servicos pertencem a Management: a existencia deles e validada la, a partir do evento OrdemServicoAberta
        var ordemServico = OrdemServico.Criar(command.ClienteId, command.VeiculoId, command.ServicoIds);

        await _ordemServicoGateway.AdicionarAsync(ordemServico, cancellationToken);

        _logger.LogInformation(
            OrdemServicoLogEventos.OsCriada,
            "Ordem de servico {OrdemServicoId} criada para o cliente {ClienteId} e o veiculo {VeiculoId} com {QuantidadeServicos} servicos.",
            ordemServico.Id,
            ordemServico.ClienteId,
            ordemServico.VeiculoId,
            ordemServico.ServicoIds.Count);

        await _eventosPublisher.PublicarAsync(
            new OrdemServicoAberta(ordemServico.Id, ordemServico.ClienteId, ordemServico.VeiculoId, ordemServico.ServicoIds.ToArray()),
            ordemServico.Id,
            CancellationToken.None);

        return _mapper.Map<OrdemServicoViewModel>(ordemServico);
    }

    public async Task<OrdemServicoViewModel> AtualizarAsync(AtualizarOrdemServicoCommand command, CancellationToken cancellationToken = default)
    {
        var ordemServico = await ObterOrdemServicoExistenteAsync(command.Id, cancellationToken);

        ordemServico.Atualizar(command.ClienteId, command.VeiculoId, command.ServicoIds);

        await _ordemServicoGateway.AtualizarAsync(ordemServico, cancellationToken);

        await _eventosPublisher.PublicarAsync(
            new OrdemServicoAtualizada(ordemServico.Id, ordemServico.ClienteId, ordemServico.VeiculoId, ordemServico.ServicoIds.ToArray()),
            ordemServico.Id,
            CancellationToken.None);

        return _mapper.Map<OrdemServicoViewModel>(ordemServico);
    }

    public async Task<OrdemServicoViewModel> ObterPorIdAsync(ObterOrdemServicoPorIdQuery query, CancellationToken cancellationToken = default)
    {
        var ordemServico = await ObterOrdemServicoExistenteAsync(query.Id, cancellationToken);
        return _mapper.Map<OrdemServicoViewModel>(ordemServico);
    }

    public async Task<IReadOnlyCollection<OrdemServicoViewModel>> ListarAsync(ListarOrdensServicoQuery query, CancellationToken cancellationToken = default)
    {
        var ordensServico = await _ordemServicoGateway.ListarAsync(cancellationToken);
        return _mapper.Map<IReadOnlyCollection<OrdemServicoViewModel>>(ordensServico);
    }

    public async Task<IReadOnlyCollection<OrdemServicoOrdenadasViewModel>> ListarOrdenadasAsync(ListarOrdensServicoOrdenadasQuery query, CancellationToken cancellationToken = default)
    {
        var ordensServico = await _ordemServicoGateway.ListarOrdenadasAsync(cancellationToken);
        return _mapper.Map<IReadOnlyCollection<OrdemServicoOrdenadasViewModel>>(ordensServico);
    }

    public async Task ExcluirAsync(ExcluirOrdemServicoCommand command, CancellationToken cancellationToken = default)
    {
        var ordemServico = await ObterOrdemServicoExistenteAsync(command.Id, cancellationToken);

        await _ordemServicoGateway.RemoverAsync(ordemServico, cancellationToken);

        await _eventosPublisher.PublicarAsync(new OrdemServicoExcluida(ordemServico.Id), ordemServico.Id, CancellationToken.None);
    }

    public async Task<AcompanhamentoOrdemServicoViewModel> ObterAcompanhamentoAsync(ObterAcompanhamentoOrdemServicoPorIdQuery query, CancellationToken cancellationToken = default)
    {
        var ordemServico = await ObterOrdemServicoExistenteAsync(query.Id, cancellationToken);
        return _mapper.Map<AcompanhamentoOrdemServicoViewModel>(ordemServico);
    }

    public async Task<IReadOnlyCollection<AcompanhamentoOrdemServicoViewModel>> ListarPorClienteAsync(ListarOrdensServicoPorClienteQuery query, CancellationToken cancellationToken = default)
    {
        // O cadastro de clientes fica na Management: cliente sem ordens (ou inexistente) resulta em lista vazia
        var ordensServico = await _ordemServicoGateway.ListarPorClienteAsync(query.ClienteId, cancellationToken);
        return _mapper.Map<IReadOnlyCollection<AcompanhamentoOrdemServicoViewModel>>(ordensServico);
    }

    public async Task<IReadOnlyCollection<EventoHistoricoSagaViewModel>> ListarHistoricoSagaAsync(ListarHistoricoSagaQuery query, CancellationToken cancellationToken = default)
    {
        // So o DynamoDB, sem consultar o PostgreSQL: OS excluida ou desconhecida retorna o que houver no historico (ou lista vazia)
        var historico = await _historicoSagaGateway.ListarAsync(query.Id, cancellationToken);
        return _mapper.Map<IReadOnlyCollection<EventoHistoricoSagaViewModel>>(historico.OrderBy(evento => evento.OcorridoEm).ToArray());
    }

    // Diagnostico (1->2) e finalizacao (4->5) chegam por evento da Execucao e Producao (OrdemServicoEventosUseCases); pelo HTTP so a entrega
    public async Task<OrdemServicoViewModel> AlterarStatusParaEntregueAsync(AlterarStatusOrdemServicoCommand command, CancellationToken cancellationToken = default)
    {
        var (ordemServico, statusAnterior) = await AlterarStatusAsync(
            command.Id,
            OrdemServicoLogEventos.Etapas.Entrega,
            (ordem, dataAlteracao) => ordem.AlterarParaEntregue(dataAlteracao),
            cancellationToken);

        _logger.LogInformation(
            OrdemServicoLogEventos.OsEncerrada,
            "Ordem de servico {OrdemServicoId} encerrada apos {TempoTotalSegundos} segundos desde o recebimento.",
            ordemServico.Id,
            ordemServico.ObterTempoEntrega().TotalSeconds);

        await PublicarStatusAlteradoAsync(ordemServico, statusAnterior);
        await _indicadorUseCases.AtualizarAsync(cancellationToken);

        return _mapper.Map<OrdemServicoViewModel>(ordemServico);
    }

    private async Task<OrdemServico> ObterOrdemServicoExistenteAsync(Guid id, CancellationToken cancellationToken)
    {
        var ordemServico = await _ordemServicoGateway.ObterPorIdAsync(id, cancellationToken);

        if (ordemServico is null)
        {
            throw new KeyNotFoundException("Ordem de servico nao encontrada.");
        }

        return ordemServico;
    }

    private async Task<(OrdemServico OrdemServico, StatusOrdemServico StatusAnterior)> AlterarStatusAsync(
        Guid id,
        string etapa,
        Action<OrdemServico, DateTime> alteracao,
        CancellationToken cancellationToken)
    {
        var ordemServico = await ObterOrdemServicoExistenteAsync(id, cancellationToken);
        var statusAnterior = ordemServico.Status;
        var dataEntradaStatusAnterior = ordemServico.HistoricoStatus.Max(historico => historico.DataAlteracao);
        var agora = DateTime.UtcNow;

        alteracao(ordemServico, agora);
        await _ordemServicoGateway.AtualizarAsync(ordemServico, cancellationToken);
        _logger.RegistrarStatusAlterado(ordemServico, etapa, statusAnterior, dataEntradaStatusAnterior, agora);

        return (ordemServico, statusAnterior);
    }

    private Task PublicarStatusAlteradoAsync(OrdemServico ordemServico, StatusOrdemServico statusAnterior)
    {
        return _eventosPublisher.PublicarAsync(
            new OrdemServicoStatusAlterado(ordemServico.Id, ordemServico.ClienteId, (int)statusAnterior, (int)ordemServico.Status),
            ordemServico.Id,
            CancellationToken.None);
    }
}
