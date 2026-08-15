using TaskFlow.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TaskFlow.Infrastructure.Configurations;

public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        // Backs "list members of my tenant" (AddProjectMemberRequest resolves an email to a user,
        // scoped so one tenant can never add a user from another tenant to a project).
        builder.HasIndex(u => u.TenantId);
    }
}
