using Microsoft.AspNetCore.Http;
using TaskFlow.Application.Abstractions;

namespace TaskFlow.Infrastructure.Multitenancy;

/// <summary>
/// The ONLY implementation of ICurrentTenantProvider — reads the "tenant_id" claim off the
/// authenticated JWT via IHttpContextAccessor. Deliberately does NOT read any header, query
/// string, or route value: those are attacker-controlled input, and if this ever read tenant
/// scope from them, any authenticated user could pass a different tenant's id and read that
/// tenant's boards/cards straight through the EF Core query filter below.
/// </summary>
public class CurrentTenantProvider(IHttpContextAccessor httpContextAccessor) : ICurrentTenantProvider
{
    public Guid? TenantId
    {
        get
        {
            var claim = httpContextAccessor.HttpContext?.User.FindFirst("tenant_id")?.Value;
            return Guid.TryParse(claim, out var tenantId) ? tenantId : null;
        }
    }
}
