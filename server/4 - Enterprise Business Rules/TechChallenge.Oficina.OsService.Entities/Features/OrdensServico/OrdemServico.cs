using TechChallenge.Oficina.OsService.Entities.Exceptions;
using TechChallenge.Oficina.OsService.Entities.Features.OrdensServico.Enums;
using TechChallenge.Oficina.OsService.Entities.Features.OrdensServico.VOs;

namespace TechChallenge.Oficina.OsService.Entities.Features.OrdensServico;

public class OrdemServico
{
    public const int TamanhoMaximoMotivoCancelamento = 300;
    private const string MotivoCancelamentoPadrao = "Motivo nao informado.";

    private static readonly StatusOrdemServico[] StatusSemCancelamento =
    [
        StatusOrdemServico.Finalizada,
        StatusOrdemServico.Entregue,
        StatusOrdemServico.Encerrada,
        StatusOrdemServico.Cancelada
    ];

    private readonly List<HistoricoStatusOrdemServico> _historicoStatus = [];

    // Não é readonly: o EF Core materializa a coluna servico_ids atribuindo uma nova lista a este campo.
    private List<Guid> _servicoIds = [];

    public Guid Id { get; private set; }
    public Guid ClienteId { get; private set; }
    public Guid VeiculoId { get; private set; }
    public StatusOrdemServico Status { get; private set; }
    public decimal? ValorOrcamento { get; private set; }
    public string? MotivoCancelamento { get; private set; }
    public IReadOnlyCollection<Guid> ServicoIds => _servicoIds.AsReadOnly();
    public IReadOnlyCollection<HistoricoStatusOrdemServico> HistoricoStatus => _historicoStatus.AsReadOnly();

    public static OrdemServico Criar(Guid clienteId, Guid veiculoId, IReadOnlyCollection<Guid> servicoIds, DateTime? dataCadastro = null)
    {
        return new OrdemServico(clienteId, veiculoId, servicoIds, dataCadastro);
    }

    private OrdemServico()
    {
    }

    private OrdemServico(Guid clienteId, Guid veiculoId, IReadOnlyCollection<Guid> servicoIds, DateTime? dataCadastro)
    {
        Id = Guid.NewGuid();
        DefinirDados(clienteId, veiculoId, servicoIds);
        DefinirStatus(StatusOrdemServico.Recebida, dataCadastro ?? DateTime.UtcNow);
    }

    public void Atualizar(Guid clienteId, Guid veiculoId, IReadOnlyCollection<Guid> servicoIds)
    {
        // Depois do diagnóstico o orçamento já pode ter sido calculado pela Management sobre os dados atuais
        if (Status != StatusOrdemServico.Recebida)
        {
            throw new DomainException("Somente ordem de servico recebida pode ser atualizada.");
        }

        DefinirDados(clienteId, veiculoId, servicoIds);
    }

    public void AlterarParaEmDiagnostico(DateTime? dataAlteracao = null)
    {
        if (Status != StatusOrdemServico.Recebida)
        {
            throw new DomainException("Somente ordem de servico recebida pode ser alterada para em diagnostico.");
        }

        DefinirStatus(StatusOrdemServico.EmDiagnostico, dataAlteracao ?? DateTime.UtcNow);
    }

    public void AlterarParaEmExecucao(DateTime? dataAlteracao = null)
    {
        if (Status != StatusOrdemServico.EmDiagnostico && Status != StatusOrdemServico.AguardandoAprovacao)
        {
            throw new DomainException("Somente ordem de servico em diagnostico ou aguardando aprovacao pode ser alterada para em execucao.");
        }

        DefinirStatus(StatusOrdemServico.EmExecucao, dataAlteracao ?? DateTime.UtcNow);
    }

    public void AlterarParaFinalizada(DateTime? dataAlteracao = null)
    {
        if (Status != StatusOrdemServico.EmExecucao)
        {
            throw new DomainException("Somente ordem de servico em execucao pode ser alterada para finalizada.");
        }

        DefinirStatus(StatusOrdemServico.Finalizada, dataAlteracao ?? DateTime.UtcNow);
    }

    public void AlterarParaEntregue(DateTime? dataAlteracao = null)
    {
        if (Status != StatusOrdemServico.Finalizada)
        {
            throw new DomainException("Somente ordem de servico finalizada pode ser alterada para entregue.");
        }

        DefinirStatus(StatusOrdemServico.Entregue, dataAlteracao ?? DateTime.UtcNow);
    }

    public void RegistrarOrcamentoGerado(decimal valorTotal, DateTime? dataGeracao = null)
    {
        if (Status != StatusOrdemServico.Recebida && Status != StatusOrdemServico.EmDiagnostico && Status != StatusOrdemServico.AguardandoAprovacao)
        {
            throw new DomainException("Somente ordem de servico recebida, em diagnostico ou aguardando aprovacao pode ter orcamento gerado.");
        }

        if (valorTotal < 0)
        {
            throw new DomainException("O valor do orcamento da ordem de servico deve ser valido.");
        }

        ValorOrcamento = valorTotal;

        // Orçamento regerado: só atualiza o valor, sem nova entrada no histórico
        if (Status != StatusOrdemServico.AguardandoAprovacao)
        {
            DefinirStatus(StatusOrdemServico.AguardandoAprovacao, dataGeracao ?? DateTime.UtcNow);
        }
    }

    public void RegistrarOrcamentoAprovado(DateTime? dataAprovacao = null)
    {
        if (Status != StatusOrdemServico.AguardandoAprovacao)
        {
            throw new DomainException("Somente ordem de servico aguardando aprovacao pode ser aprovada.");
        }

        DefinirStatus(StatusOrdemServico.EmExecucao, dataAprovacao ?? DateTime.UtcNow);
    }

    public void RegistrarOrcamentoRecusado(DateTime? dataRecusa = null)
    {
        if (Status != StatusOrdemServico.AguardandoAprovacao)
        {
            throw new DomainException("Somente ordem de servico aguardando aprovacao pode ser recusada.");
        }

        DefinirStatus(StatusOrdemServico.Finalizada, dataRecusa ?? DateTime.UtcNow);
    }

    public void Cancelar(string? motivo, DateTime? dataCancelamento = null)
    {
        if (StatusSemCancelamento.Contains(Status))
        {
            throw new DomainException("Ordem de servico finalizada, entregue, encerrada ou cancelada nao pode ser cancelada.");
        }

        MotivoCancelamento = NormalizarMotivo(motivo);
        DefinirStatus(StatusOrdemServico.Cancelada, dataCancelamento ?? DateTime.UtcNow);
    }

    public TimeSpan ObterTempoExecucao()
    {
        var inicio = ObterDataHistoricoObrigatoria(StatusOrdemServico.EmExecucao);
        var fim = ObterDataHistoricoObrigatoria(StatusOrdemServico.Finalizada);
        return fim - inicio;
    }

    public TimeSpan ObterTempoEntrega()
    {
        var recebida = ObterDataHistoricoObrigatoria(StatusOrdemServico.Recebida);
        var entregue = ObterDataHistoricoObrigatoria(StatusOrdemServico.Entregue);
        return entregue - recebida;
    }

    private void DefinirDados(Guid clienteId, Guid veiculoId, IReadOnlyCollection<Guid> servicoIds)
    {
        // Valida tudo antes de alterar: uma atualização inválida não deixa a ordem pela metade
        if (clienteId == Guid.Empty)
        {
            throw new DomainException("O cliente da ordem de servico e obrigatorio.");
        }

        if (veiculoId == Guid.Empty)
        {
            throw new DomainException("O veiculo da ordem de servico e obrigatorio.");
        }

        if (servicoIds is null || servicoIds.Count == 0)
        {
            throw new DomainException("A ordem de servico deve possuir ao menos um servico.");
        }

        if (servicoIds.Any(servicoId => servicoId == Guid.Empty))
        {
            throw new DomainException("Todos os servicos informados devem possuir identificador valido.");
        }

        ClienteId = clienteId;
        VeiculoId = veiculoId;
        _servicoIds = servicoIds.Distinct().ToList();
    }

    private void DefinirStatus(StatusOrdemServico novoStatus, DateTime dataAlteracao)
    {
        Status = novoStatus;
        _historicoStatus.Add(HistoricoStatusOrdemServico.Criar(novoStatus, dataAlteracao));
    }

    private DateTime ObterDataHistoricoObrigatoria(StatusOrdemServico status)
    {
        var historico = _historicoStatus.LastOrDefault(item => item.Status == status);

        if (historico is null)
        {
            throw new DomainException("A ordem de servico nao possui historico suficiente para calcular os indicadores.");
        }

        return historico.DataAlteracao;
    }

    private static string NormalizarMotivo(string? motivo)
    {
        // O motivo vem do evento de rejeição da Management: vazio vira um texto padrão e o excesso é cortado no tamanho da coluna
        var motivoNormalizado = (motivo ?? string.Empty).Trim();

        if (motivoNormalizado.Length == 0)
        {
            return MotivoCancelamentoPadrao;
        }

        return motivoNormalizado.Length <= TamanhoMaximoMotivoCancelamento
            ? motivoNormalizado
            : motivoNormalizado.Substring(0, TamanhoMaximoMotivoCancelamento);
    }
}
