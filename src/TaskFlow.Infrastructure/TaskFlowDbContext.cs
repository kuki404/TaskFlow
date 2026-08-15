using TaskFlow.Application.Abstractions;
using TaskFlow.Domain.Common;
using TaskFlow.Domain.Entities;
using TaskFlow.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace TaskFlow.Infrastructure;

public class TaskFlowDbContext(DbContextOptions<TaskFlowDbContext> options, ICurrentTenantProvider tenantProvider)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();
    public DbSet<Board> Boards => Set<Board>();
    public DbSet<CardList> CardLists => Set<CardList>();
    public DbSet<Card> Cards => Set<Card>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(TaskFlowDbContext).Assembly);

        // --- Multi-tenancy: every tenant-scoped table gets a global query filter comparing its
        // own (denormalized) TenantId column against the JWT-derived current tenant. This is the
        // single choke point that makes cross-tenant data leakage a compile-time-obvious bug
        // instead of something every query author has to remember by hand. TenantId is null (not
        // the empty guid) when there's no authenticated caller, which intentionally matches
        // nothing rather than everything. ---
        builder.Entity<Project>().HasQueryFilter(p => p.TenantId == tenantProvider.TenantId);
        builder.Entity<Board>().HasQueryFilter(b => b.TenantId == tenantProvider.TenantId);
        builder.Entity<CardList>().HasQueryFilter(l => l.TenantId == tenantProvider.TenantId);
        builder.Entity<Card>().HasQueryFilter(c => c.TenantId == tenantProvider.TenantId);
    }
}
