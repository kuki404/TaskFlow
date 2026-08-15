using TaskFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TaskFlow.Infrastructure.Configurations;

public class BoardConfiguration : IEntityTypeConfiguration<Board>
{
    public void Configure(EntityTypeBuilder<Board> builder)
    {
        builder.ToTable("Boards");
        builder.HasKey(b => b.Id);

        builder.HasMany(b => b.CardLists)
            .WithOne()
            .HasForeignKey(l => l.BoardId)
            .OnDelete(DeleteBehavior.Cascade);

        // Backs BoardService.GetByIdAsync — loading a whole board by its own id, filtered to the caller's tenant.
        builder.HasIndex(b => new { b.Id, b.TenantId });
    }
}

public class CardListConfiguration : IEntityTypeConfiguration<CardList>
{
    public void Configure(EntityTypeBuilder<CardList> builder)
    {
        builder.ToTable("CardLists");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Name).HasMaxLength(100).IsRequired();

        builder.HasMany(l => l.Cards)
            .WithOne()
            .HasForeignKey(c => c.CardListId)
            .OnDelete(DeleteBehavior.Cascade);

        // Backs "columns for this board, in order" — the core of every board load.
        builder.HasIndex(l => new { l.BoardId, l.Position });
    }
}
