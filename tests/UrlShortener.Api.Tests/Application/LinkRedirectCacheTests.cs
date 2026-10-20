using UrlShortener.Api.Application;

namespace UrlShortener.Api.Tests.Application;

public sealed class LinkRedirectCacheTests
{
    [Fact]
    public void EvictsLeastRecentlyUsedLinkAtCapacity()
    {
        var cache = new LinkRedirectCache(2);
        var now = DateTimeOffset.UtcNow;
        var redirect = new CachedRedirect(42, "https://8.8.8.8", null);
        cache.Set("HOT", redirect, now);
        cache.Set("COLD", redirect, now);
        Assert.Equal(redirect, cache.Get("HOT", now));

        cache.Set("NEW", redirect, now);

        Assert.Null(cache.Get("COLD", now));
        Assert.Equal(redirect, cache.Get("HOT", now));
        Assert.Equal(redirect, cache.Get("NEW", now));
        Assert.Null(cache.Get("hot", now));
    }

    [Fact]
    public void EntryExpiresAfterFiveMinutesEvenWhenFrequentlyRead()
    {
        var cache = new LinkRedirectCache();
        var now = DateTimeOffset.UtcNow;
        var redirect = new CachedRedirect(42, "https://8.8.8.8", null);
        cache.Set("HOT", redirect, now);

        Assert.Equal(redirect, cache.Get("HOT", now.AddMinutes(4)));
        Assert.Null(cache.Get("HOT", now.AddMinutes(5)));
    }
}
