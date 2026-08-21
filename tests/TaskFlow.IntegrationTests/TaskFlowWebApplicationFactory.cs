using System.Data.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Respawn;
using TaskFlow.Infrastructure;
using Testcontainers.MsSql;

namespace TaskFlow.IntegrationTests;

/// <summary>
/// One real SQL Server container for the whole test run (Testcontainers.MsSql), Respawn to reset
/// state between tests instead of a container per test — per dotnet-testing. Program.cs
/// (top-level statements) builds the connection string from Sql:* config before
/// WebApplicationFactory's ConfigureWebHost customization is applied, so that hook arrives too
/// late here — config is set as process env vars in InitializeAsync, before the warm-up
/// CreateClient() call that triggers host build, adapted from StaySphereWebApplicationFactory for
/// Testcontainers' dynamically assigned port. No manual `docker compose up -d db` step is needed
/// anymore: the container is started and torn down entirely by this fixture.
/// </summary>
public class TaskFlowWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string SaPassword = "IntegrationTests123!";

    private readonly MsSqlContainer sqlContainer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
        .WithPassword(SaPassword)
        .Build();

    private DbConnection connection = null!;
    private Respawner respawner = null!;

    public async ValueTask InitializeAsync()
    {
        await sqlContainer.StartAsync();

        Environment.SetEnvironmentVariable("Sql__Host", sqlContainer.Hostname);
        Environment.SetEnvironmentVariable("Sql__Port", sqlContainer.GetMappedPublicPort(1433).ToString());
        Environment.SetEnvironmentVariable("Sql__Password", SaPassword);
        Environment.SetEnvironmentVariable("Sql__Database", "TaskFlow_IntegrationTests");
        Environment.SetEnvironmentVariable("Jwt__Secret", "local-integration-tests-signing-key-at-least-32-chars-long");

        // The "auth" rate limit (5/min) exists to slow down brute-force attempts, not to survive
        // a test suite that legitimately registers/logs in many times in a few seconds.
        Environment.SetEnvironmentVariable("RateLimiting__Auth__PermitLimit", "1000");

        // Triggers host build (lazy on first Server/CreateClient access). Migration is applied
        // explicitly right after, rather than relying on any Development-only seed gate — that
        // keeps the test database's schema deterministic regardless of what ASPNETCORE_ENVIRONMENT
        // the test host happens to run under.
        using (var warmUpClient = CreateClient())
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TaskFlowDbContext>();
            await db.Database.MigrateAsync();
        }

        var sqlConnection = new SqlConnection(BuildAdoConnectionString());
        await sqlConnection.OpenAsync();

        respawner = await Respawner.CreateAsync(sqlConnection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.SqlServer,
            SchemasToInclude = ["dbo"],
            TablesToIgnore = ["__EFMigrationsHistory"]
        });

        connection = sqlConnection;
    }

    public Task ResetDatabaseAsync() => respawner.ResetAsync(connection);

    private string BuildAdoConnectionString() =>
        $"Server={sqlContainer.Hostname},{sqlContainer.GetMappedPublicPort(1433)};" +
        "Database=TaskFlow_IntegrationTests;User Id=sa;Password=" + SaPassword +
        ";TrustServerCertificate=True;Encrypt=True";

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.UseEnvironment("Development");

    public override async ValueTask DisposeAsync()
    {
        await connection.DisposeAsync();
        await sqlContainer.DisposeAsync();
        await base.DisposeAsync();
    }
}

/// <summary>
/// Every integration test class shares ONE factory instance via this collection instead of each
/// declaring its own IClassFixture — xUnit runs test classes in different collections in
/// parallel, and two hosts migrating/seeding the same shared database at the same time would race
/// each other.
/// </summary>
[CollectionDefinition("Integration")]
public class IntegrationCollection : ICollectionFixture<TaskFlowWebApplicationFactory>;
