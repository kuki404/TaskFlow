using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using TaskFlow.Application.Abstractions;

namespace TaskFlow.Infrastructure;

/// <summary>
/// Lets `dotnet ef migrations add` / `dotnet ef database update` build a DbContext without
/// spinning up the full Api host. Reads the same User Secrets store as TaskFlow.Api (shared
/// UserSecretsId), so no connection string is ever hardcoded or committed.
/// </summary>
public class TaskFlowDbContextFactory : IDesignTimeDbContextFactory<TaskFlowDbContext>
{
    /// <summary>No HTTP request exists at design time, so the tenant is always null — fine, migrations don't run queries through the model's query filters.</summary>
    private sealed class NullTenantProvider : ICurrentTenantProvider
    {
        public Guid? TenantId => null;
    }

    public TaskFlowDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<TaskFlowDbContextFactory>()
            .AddEnvironmentVariables()
            .Build();

        var connectionString = SqlConnectionStringFactory.Build(configuration);

        var optionsBuilder = new DbContextOptionsBuilder<TaskFlowDbContext>();
        optionsBuilder.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure());

        return new TaskFlowDbContext(optionsBuilder.Options, new NullTenantProvider());
    }
}
