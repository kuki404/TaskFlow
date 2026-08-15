using System.Linq.Expressions;
using TaskFlow.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace TaskFlow.Infrastructure.Query;

/// <summary>
/// Runs a COUNT + a projected, paged SELECT — never a tracked/materialized entity set — so every
/// list endpoint only ever pulls the columns and rows it will actually return.
/// </summary>
public static class QueryExtensions
{
    public static async Task<PagedResult<TDto>> ToPagedResultAsync<TEntity, TDto>(
        this IQueryable<TEntity> query,
        Expression<Func<TEntity, TDto>> projection,
        PagedRequest paging,
        CancellationToken cancellationToken)
    {
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip(paging.Skip)
            .Take(paging.PageSize)
            .Select(projection)
            .ToListAsync(cancellationToken);

        return new PagedResult<TDto>(items, totalCount, paging.Page, paging.PageSize);
    }
}
