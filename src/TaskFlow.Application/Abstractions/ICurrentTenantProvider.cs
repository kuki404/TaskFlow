namespace TaskFlow.Application.Abstractions;

/// <summary>
/// Resolves the current request's tenant. The ONLY valid source is the "tenant_id" claim on the
/// authenticated JWT (set at token-issue time from the user's own TenantId column) — never a
/// client-supplied header, query string, or route parameter, which a malicious or buggy client
/// could set to any value and read another tenant's data. Implemented in Infrastructure against
/// IHttpContextAccessor and injected into TaskFlowDbContext.
/// </summary>
public interface ICurrentTenantProvider
{
    /// <summary>Null when there is no authenticated user on the current request (e.g. the login endpoint itself) — TaskFlowDbContext's query filter treats that as "match nothing" rather than "match everything".</summary>
    Guid? TenantId { get; }
}
