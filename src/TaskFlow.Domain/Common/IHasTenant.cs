namespace TaskFlow.Domain.Common;

/// <summary>
/// Marker for every entity scoped to a single tenant. TenantId is denormalized directly onto
/// Board/CardList/Card (not just Project) so the EF Core global query filter
/// (TaskFlowDbContext.OnModelCreating) can filter each table on its own TenantId column without a
/// join back to Project on every query — trades a few extra bytes per row for filters that stay
/// cheap and index-friendly as the hierarchy gets deeper.
/// </summary>
public interface IHasTenant
{
    Guid TenantId { get; }
}
