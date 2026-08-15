using TaskFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace TaskFlow.Infrastructure.Query;

/// <summary>
/// EF.CompileAsyncQuery for the full-board-load hot path: every board view (and every SignalR
/// join) re-runs this exact shape. Compiling it once skips LINQ expression-tree processing on
/// every call — worth it here because unlike most list endpoints, this query's shape never
/// varies with paging/filtering parameters.
/// </summary>
public static class CompiledQueries
{
    public static readonly Func<TaskFlowDbContext, Guid, Task<Board?>> BoardByIdWithListsAndCards =
        EF.CompileAsyncQuery((TaskFlowDbContext db, Guid boardId) =>
            db.Boards
                .AsNoTracking()
                // Board -> CardLists -> Cards is a one-to-many-to-many fan-out; a single query
                // with Include would duplicate every Board/CardList column once per Card row.
                // AsSplitQuery issues one SQL query per level instead, avoiding that cartesian
                // explosion at the cost of an extra round trip.
                .AsSplitQuery()
                .Include(b => b.CardLists.OrderBy(l => l.Position))
                .ThenInclude(l => l.Cards.OrderBy(c => c.Position))
                .FirstOrDefault(b => b.Id == boardId));
}
