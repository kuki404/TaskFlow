using System.Net;
using System.Net.Http.Json;
using Shouldly;
using TaskFlow.Application.Dtos;

namespace TaskFlow.IntegrationTests;

/// <summary>
/// Mirrors BookIt's RegisterErrorMessageTests: AddIdentityCore only overrides RequiredLength and
/// RequireNonAlphanumeric, leaving Identity's digit/upper/lowercase defaults in force, but
/// Register.razor's helper text used to promise only "8 characters" and AuthService returned one
/// generic failure message for every rejected registration — so a password like "password1"
/// passed client-side validation and then failed with no useful reason.
/// </summary>
[Collection("Integration")]
public class RegisterErrorMessageTests(TaskFlowWebApplicationFactory factory)
{
    [Fact]
    public async Task Register_WithAPasswordMissingAnUppercaseLetter_ReturnsASpecificReason()
    {
        await factory.ResetDatabaseAsync();
        var client = factory.CreateClient();
        var email = $"user-{Guid.NewGuid():N}@test.local";

        var response = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, "password1", "Test User", "Test Tenant"), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldNotBe(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        // The generic "Could not create an account with the provided details." is reserved for
        // the enumeration-sensitive duplicate-email case, not a plain password-policy failure.
        body.ShouldNotContain("Could not create an account with the provided details.");
        body.ShouldContain("uppercase", Case.Insensitive);
    }

    [Fact]
    public async Task Register_WithADuplicateEmail_StillReturnsTheGenericEnumerationSafeMessage()
    {
        await factory.ResetDatabaseAsync();
        var client = factory.CreateClient();
        var email = $"user-{Guid.NewGuid():N}@test.local";
        var firstResponse = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, "Passw0rd123", "First User", "First Tenant"), TestContext.Current.CancellationToken);
        firstResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var secondResponse = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, "Passw0rd123", "Second User", "Second Tenant"), TestContext.Current.CancellationToken);

        var body = await secondResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldContain("Could not create an account with the provided details.");
    }
}
