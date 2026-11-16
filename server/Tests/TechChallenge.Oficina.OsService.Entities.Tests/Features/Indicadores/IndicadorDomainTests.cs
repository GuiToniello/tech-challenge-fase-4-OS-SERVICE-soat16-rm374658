using TechChallenge.Oficina.OsService.Entities.Exceptions;
using TechChallenge.Oficina.OsService.Entities.Features.Indicadores;
using Xunit;

namespace TechChallenge.Oficina.OsService.Entities.Tests.Features.Indicadores;

public sealed class IndicadorDomainTests
{
    [Fact]
    public void Criar_DeveDefinirIdUnicoETempos()
    {
        var indicador = Indicador.Criar(TimeSpan.FromHours(2), TimeSpan.FromHours(5));

        Assert.Equal(1, indicador.Id);
        Assert.Equal(TimeSpan.FromHours(2), indicador.TempoMedioExecucao);
        Assert.Equal(TimeSpan.FromHours(5), indicador.TempoMedioEntrega);
    }

    [Fact]
    public void Atualizar_DeveSubstituirTempos()
    {
        var indicador = Indicador.Criar(TimeSpan.Zero, TimeSpan.Zero);

        indicador.Atualizar(TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(90));

        Assert.Equal(TimeSpan.FromMinutes(30), indicador.TempoMedioExecucao);
        Assert.Equal(TimeSpan.FromMinutes(90), indicador.TempoMedioEntrega);
    }

    [Fact]
    public void Atualizar_DeveLancar_QuandoTempoMedioExecucaoForNegativo()
    {
        var indicador = Indicador.Criar(TimeSpan.Zero, TimeSpan.Zero);

        var action = () => indicador.Atualizar(TimeSpan.FromMinutes(-1), TimeSpan.Zero);

        var exception = Assert.Throws<DomainException>(action);
        Assert.Equal("O tempo medio de execucao da ordem de servico deve ser valido.", exception.Message);
    }

    [Fact]
    public void Atualizar_DeveLancar_QuandoTempoMedioEntregaForNegativo()
    {
        var indicador = Indicador.Criar(TimeSpan.Zero, TimeSpan.Zero);

        var action = () => indicador.Atualizar(TimeSpan.Zero, TimeSpan.FromMinutes(-1));

        var exception = Assert.Throws<DomainException>(action);
        Assert.Equal("O tempo medio de entrega da ordem de servico deve ser valido.", exception.Message);
    }
}
