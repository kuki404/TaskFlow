using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using TaskFlow.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace TaskFlow.Infrastructure.Seed;

/// <summary>
/// Applies pending migrations and seeds two separate demo tenants (each with an Owner and a
/// Member user) on startup, so `docker compose up` + a fresh clone is enough to get a reviewer
/// clicking through real multi-tenant, role-differentiated data with zero typing — see the "Demo
/// login" buttons on the Web app's Login page.
/// </summary>
public static class DbInitializer
{
    public static async Task RunAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<TaskFlowDbContext>();
        await db.Database.MigrateAsync();

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        await SeedTenantAsync(db, userManager, "Acme Inc", "acme.local", "Kanban Launch Board");
        await SeedTenantAsync(db, userManager, "Globex Corp", "globex.local", "Product Roadmap");
    }

    private static async Task SeedTenantAsync(TaskFlowDbContext db, UserManager<ApplicationUser> userManager, string tenantName, string emailDomain, string projectName)
    {
        var existingOwner = await userManager.FindByEmailAsync($"owner@{emailDomain}");
        if (existingOwner is not null)
        {
            return;
        }

        var tenant = Tenant.Create(tenantName);
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var owner = await CreateUserAsync(userManager, tenant.Id, $"owner@{emailDomain}", "Demo123!", $"Demo Owner ({tenantName})");
        var member = await CreateUserAsync(userManager, tenant.Id, $"member@{emailDomain}", "Demo123!", $"Demo Member ({tenantName})");

        var project = Project.Create(tenant.Id, projectName, description: null, ownerUserId: owner.Id);
        project.Members.Add(ProjectMember.Create(project.Id, member.Id, ProjectRole.Member));

        // A couple of sample cards so the board isn't empty on first look.
        var todo = project.Board!.CardLists.Single(l => l.Name == "To Do");
        var inProgress = project.Board!.CardLists.Single(l => l.Name == "In Progress");
        db.Cards.Add(Card.Create(tenant.Id, todo.Id, "Set up CI pipeline", "Wire up build + test on every push.", CardPriority.Medium, 0));
        db.Cards.Add(Card.Create(tenant.Id, todo.Id, "Design onboarding flow", null, CardPriority.Low, 1));
        db.Cards.Add(Card.Create(tenant.Id, inProgress.Id, "Implement card drag-and-drop", "Board page, SignalR-synced.", CardPriority.High, 0));

        db.Projects.Add(project);
        await db.SaveChangesAsync();
    }

    private static async Task<ApplicationUser> CreateUserAsync(UserManager<ApplicationUser> userManager, Guid tenantId, string email, string password, string displayName)
    {
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            DisplayName = displayName,
            TenantId = tenantId,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Failed to seed demo user {email}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }

        return user;
    }
}
