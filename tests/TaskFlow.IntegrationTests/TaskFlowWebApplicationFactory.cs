using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

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

        // Falls back to the same values used by docker-compose.yml/.env locally — only applied if
        // User Secrets hasn't already set a real one, so a developer's `dotnet user-secrets set`
        // still takes priority — checked by actually reading User Secrets here, not just "is the
        // env var already set", because environment variables are added AFTER User Secrets in
        // WebApplicationBuilder's default configuration order and would otherwise silently win
        // and override it.
        SetIfUnconfigured("Sql:Password", "Sql__Password", Environment.GetEnvironmentVariable("MSSQL_SA_PASSWORD") ?? "TaskFlow_Dev_Pwd#2026");
        SetIfUnconfigured("Jwt:Secret", "Jwt__Secret", "local-integration-tests-signing-key-at-least-32-chars-long");
    }

    private static void SetIfUnconfigured(string configKey, string envKey, string fallbackValue)
    {
        // An env var here (e.g. a CI-provided real secret) must win outright — respect it as-is
        // without even probing User Secrets.
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(envKey)))
        {
            return;
        }

        var userSecrets = new ConfigurationBuilder().AddUserSecrets<Program>(optional: true).Build();
        if (string.IsNullOrEmpty(userSecrets[configKey]))
        {
            Environment.SetEnvironmentVariable(envKey, fallbackValue);
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
