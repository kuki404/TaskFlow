using TaskFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TaskFlow.Infrastructure.Configurations;

public class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("Projects");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();

        builder.HasOne(p => p.Board)
            .WithOne()
            .HasForeignKey<Board>(b => b.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Members)
            .WithOne()
            .HasForeignKey(m => m.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        // Backs "projects for the current tenant" list queries — every request to ProjectsController.
        builder.HasIndex(p => p.TenantId);
    }
}

public class ProjectMemberConfiguration : IEntityTypeConfiguration<ProjectMember>
{
    public void Configure(EntityTypeBuilder<ProjectMember> builder)
    {
        builder.ToTable("ProjectMembers");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Role).HasConversion<string>().HasMaxLength(20);

        // A user appears at most once per project. Backs both "am I a member of this project"
        // membership checks (the hottest query in the whole app — every board/card operation runs
        // one) and the resource-based authorization handler.
        builder.HasIndex(m => new { m.ProjectId, m.UserId }).IsUnique();
    }
}
