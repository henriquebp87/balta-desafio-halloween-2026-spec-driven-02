namespace UrlShortener.Api.Application;

public sealed record CachedRedirect(long Id, string TargetUrl, DateTime? ExpiresAtUtc);

// Bounded LRU cache: reads keep frequently accessed links in memory.
public sealed class LinkRedirectCache(int capacity = 1024)
{
    private readonly object gate = new();
    private readonly Dictionary<string, LinkedListNode<Entry>> entries = new(StringComparer.Ordinal);
    private readonly LinkedList<Entry> recent = new();
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    private sealed record Entry(string Code, CachedRedirect Redirect, DateTimeOffset CachedAt);

    public CachedRedirect? Get(string code, DateTimeOffset now)
    {
        lock (gate)
        {
            if (!entries.TryGetValue(code, out var node))
                return null;

            recent.Remove(node);
            if (now - node.Value.CachedAt >= Lifetime)
            {
                entries.Remove(code);
                return null;
            }

            recent.AddFirst(node);
            return node.Value.Redirect;
        }
    }

    public void Set(string code, CachedRedirect redirect, DateTimeOffset now)
    {
        lock (gate)
        {
            if (capacity <= 0)
                return;
            if (entries.Remove(code, out var existing))
                recent.Remove(existing);

            entries[code] = recent.AddFirst(new Entry(code, redirect, now));
            if (entries.Count > capacity)
            {
                entries.Remove(recent.Last!.Value.Code);
                recent.RemoveLast();
            }
        }
    }
}
