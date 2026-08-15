using TaskFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TaskFlow.Infrastructure.Configurations;

public class CardConfiguration : IEntityTypeConfiguration<Card>
{
    public void Configure(EntityTypeBuilder<Card> builder)
    {
        builder.ToTable("Cards");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Title).HasMaxLength(200).IsRequired();
        builder.Property(c => c.Description).HasMaxLength(2000);
        builder.Property(c => c.Priority).HasConversion<string>().HasMaxLength(20);

        // Optimistic concurrency: two concurrent PATCH/move requests on the same card will have
        // the loser fail with DbUpdateConcurrencyException instead of silently overwriting.
        builder.Property(c => c.RowVersion).IsRowVersion();

        // Backs "cards in this list, in order" — the innermost loop of every board load.
        builder.HasIndex(c => new { c.CardListId, c.Position });

        // Backs "my assigned cards" (a likely future personal-dashboard query) — filtered so it
        // only ever indexes rows that actually have an assignee.
        builder.HasIndex(c => c.AssignedUserId).HasFilter("[AssignedUserId] IS NOT NULL");
    }
}
