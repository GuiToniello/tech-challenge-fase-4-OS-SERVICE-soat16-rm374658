using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TechChallenge.Oficina.OsService.Entities.Features.OrdensServico;

namespace TechChallenge.Oficina.OsService.DB.Data.Features.OrdensServico;

public sealed class OrdemServicoConfiguration : IEntityTypeConfiguration<OrdemServico>
{
    private const string OrdemServicoIdColumnName = "ordem_servico_id";

    public void Configure(EntityTypeBuilder<OrdemServico> builder)
    {
        builder.ToTable("ordens_servico");

        builder.HasKey(ordemServico => ordemServico.Id);

        builder.Property(ordemServico => ordemServico.Id)
            .HasColumnName("id");

        builder.Property(ordemServico => ordemServico.ClienteId)
            .HasColumnName("cliente_id")
            .IsRequired();

        builder.Property(ordemServico => ordemServico.VeiculoId)
            .HasColumnName("veiculo_id")
            .IsRequired();

        builder.Property(ordemServico => ordemServico.Status)
            .HasColumnName("status")
            .HasColumnType("integer")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(ordemServico => ordemServico.ValorOrcamento)
            .HasColumnName("valor_orcamento")
            .HasColumnType("numeric(18,2)");

        builder.Property(ordemServico => ordemServico.MotivoCancelamento)
            .HasColumnName("motivo_cancelamento")
            .HasMaxLength(OrdemServico.TamanhoMaximoMotivoCancelamento);

        // Cliente, veiculo e servicos pertencem a Management (outro banco): ficam so os ids, sem chave estrangeira.
        // Os servicos viram uma coluna uuid[] (primitive collection do Npgsql) lida e gravada pelo campo _servicoIds.
        builder.PrimitiveCollection(ordemServico => ordemServico.ServicoIds)
            .HasField("_servicoIds")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasColumnName("servico_ids")
            .IsRequired();

        builder.HasIndex(ordemServico => ordemServico.ClienteId);
        builder.HasIndex(ordemServico => ordemServico.VeiculoId);
        builder.HasIndex(ordemServico => ordemServico.Status);

        builder.OwnsMany(ordemServico => ordemServico.HistoricoStatus, historicoBuilder =>
        {
            historicoBuilder.ToTable("ordens_servico_historico_status");
            historicoBuilder.WithOwner().HasForeignKey(OrdemServicoIdColumnName);
            historicoBuilder.Property<int>("id").ValueGeneratedOnAdd();
            historicoBuilder.HasKey("id");
            historicoBuilder.Property(historico => historico.Status)
                .HasColumnName("status")
                .HasColumnType("integer")
                .HasConversion<int>()
                .IsRequired();
            historicoBuilder.Property(historico => historico.DataAlteracao)
                .HasColumnName("data_alteracao")
                .HasColumnType("timestamp with time zone")
                .IsRequired();
            historicoBuilder.HasIndex(OrdemServicoIdColumnName);
            historicoBuilder.HasIndex(historico => historico.DataAlteracao);
        });
    }
}
