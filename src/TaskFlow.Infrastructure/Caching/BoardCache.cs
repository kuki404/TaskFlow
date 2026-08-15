using Microsoft.Extensions.Caching.Hybrid;

namespace TaskFlow.Infrastructure.Caching;

/// <summary>
/// Thin, typed wrapper around HybridCache for board metadata (list/column names + positions —
/// changes rarely compared to cards, which move constantly and are never cached here). Tag-based
/// invalidation means every write that touches a board's lists calls InvalidateAsync once, rather
/// than every reader having to know the exact cache key shape.
/// </summary>
public class BoardCache(HybridCache cache)
{
    private static string KeyFor(Guid boardId) => $"board-meta:{boardId}";
    private static string TagFor(Guid boardId) => $"board:{boardId}";

    public ValueTask<T> GetOrCreateAsync<T>(Guid boardId, Func<CancellationToken, ValueTask<T>> factory, CancellationToken ct) =>
        cache.GetOrCreateAsync(KeyFor(boardId), factory, tags: [TagFor(boardId)], cancellationToken: ct);

    /// <summary>Called by BoardService after any structural change (list created/moved/deleted) — never after a plain card edit, which does not affect cached board metadata.</summary>
    public ValueTask InvalidateAsync(Guid boardId, CancellationToken ct) =>
        cache.RemoveByTagAsync(TagFor(boardId), ct);
}
