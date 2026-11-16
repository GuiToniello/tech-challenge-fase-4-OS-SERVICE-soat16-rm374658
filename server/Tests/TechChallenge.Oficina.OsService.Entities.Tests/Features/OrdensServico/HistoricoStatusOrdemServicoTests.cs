using TechChallenge.Oficina.OsService.Entities.Exceptions;
using TechChallenge.Oficina.OsService.Entities.Features.OrdensServico.Enums;
using TechChallenge.Oficina.OsService.Entities.Features.OrdensServico.VOs;
using Xunit;

namespace TechChallenge.Oficina.OsService.Entities.Tests.Features.OrdensServico;

public sealed class HistoricoStatusOrdemServicoTests
{
    [Fact]
    public void Criar_DeveLancar_QuandoDataNaoInformada()
    {
        var action = () => HistoricoStatusOrdemServico.Criar(StatusOrdemServico.Recebida, default);

        var exception = Assert.Throws<DomainException>(action);
        Assert.Equal("A data do historico de status da ordem de servico e obrigatoria.", exception.Message);
    }

    [Fact]
    public void Equals_DeveCompararPorStatusEData()
    {
        var data = new DateTime(2024, 01, 01, 10, 00, 00, DateTimeKind.Utc);
        var historico = HistoricoStatusOrdemServico.Criar(StatusOrdemServico.Cancelada, data);
        var igual = HistoricoStatusOrdemServico.Criar(StatusOrdemServico.Cancelada, data);
        var diferente = HistoricoStatusOrdemServico.Criar(StatusOrdemServico.Recebida, data);

        Assert.Equal(historico, igual);
        Assert.Equal(historico.GetHashCode(), igual.GetHashCode());
        Assert.NotEqual(historico, diferente);
        Assert.False(historico.Equals((object?)null));
    }
}
