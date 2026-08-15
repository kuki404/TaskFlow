using Microsoft.AspNetCore.Mvc.Testing;

namespace TaskFlow.IntegrationTests;

/// <summary>
/// Boots the real Api host against a dedicated "TaskFlow_IntegrationTests" database — separate
/// from the "TaskFlow" database used for local dev, so test runs never touch dev data. The
/// override is set as a process environment variable in the static constructor (not via
/// ConfigureWebHost) because Program.cs (top-level statements) reads configuration to build the
/// connection string before WebApplicationFactory's host-building customization would apply.
/// </summary>
public class TaskFlowWebApplicationFactory : WebApplicationFactory<Program>
{
    static TaskFlowWebApplicationFactory()
    {
        Environment.SetEnvironmentVariable("Sql__Database", "TaskFlow_IntegrationTests");

        // The "auth" rate limit (5/min) exists to slow down brute-force attempts, not to survive
        // a test suite that legitimately registers/logs in many times in a few seconds.
        Environment.SetEnvironmentVariable("RateLimiting__Auth__PermitLimit", "1000");

        // Falls back to the same values used by docker-compose.yml/.env locally — only applied
        // if the environment (or User Secrets) hasn't already set them, so CI (which sets real
        // secrets via repo secrets) and a local `dotnet user-secrets set` both still take priority.
        SetIfUnset("Sql__Password", Environment.GetEnvironmentVariable("MSSQL_SA_PASSWORD") ?? "TaskFlow_Dev_Pwd#2026");
        SetIfUnset("Jwt__Secret", "local-integration-tests-signing-key-at-least-32-chars-long");
    }

    private static void SetIfUnset(string key, string value)
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
        {
            Environment.SetEnvironmentVariable(key, value);
        }
    }
}

/// <summary>
/// Every integration test class shares ONE factory instance via this collection instead of each
/// declaring its own IClassFixture — xUnit runs test classes in different collections in
/// parallel, and two hosts migrating/seeding the same shared database at the same time would race
/// each other (BookIt hit exactly this bug with two IClassFixture&lt;Factory&gt; declarations).
/// </summary>
[CollectionDefinition("Integration")]
public class IntegrationCollection : ICollectionFixture<TaskFlowWebApplicationFactory>;
