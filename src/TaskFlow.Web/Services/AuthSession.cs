using System.Security.Cryptography;
using TaskFlow.Application.Dtos;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace TaskFlow.Web.Services;

/// <summary>Holds the current user's tokens for the circuit's lifetime, mirrored into ProtectedSessionStorage so an F5 refresh (which tears down and rebuilds the circuit) doesn't silently log the user out.</summary>
public class AuthSession(ProtectedSessionStorage storage)
{
    private const string StorageKey = "taskflow-auth";

    public AuthResponse? Current { get; private set; }

    public event Action? Changed;

    public async Task RestoreAsync()
    {
        ProtectedBrowserStorageResult<AuthResponse> result;
        try
        {
            result = await storage.GetAsync<AuthResponse>(StorageKey);
        }
        catch (CryptographicException)
        {
            // The value was encrypted by a Data Protection key this instance no longer has (e.g.
            // the container restarted without a persisted key ring, or keys rotated) — an
            // undecryptable stored session is no different from no session, not a crash.
            return;
        }

        if (result is { Success: true, Value.AccessTokenExpiresAtUtc: var expiry } && expiry > DateTime.UtcNow)
        {
            Current = result.Value;
            Changed?.Invoke();
        }
    }

    public async Task SetAsync(AuthResponse response)
    {
        Current = response;
        await storage.SetAsync(StorageKey, response);
        Changed?.Invoke();
    }

    public async Task ClearAsync()
    {
        Current = null;
        await storage.DeleteAsync(StorageKey);
        Changed?.Invoke();
    }
}
