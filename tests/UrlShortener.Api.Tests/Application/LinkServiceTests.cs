using System.Security.Cryptography;
using System.Text;
using UrlShortener.Api.Application;
using UrlShortener.Api.Data;
using UrlShortener.Api.Domain;

namespace UrlShortener.Api.Tests.Application;

public sealed class LinkServiceTests
{
    [Fact]
    public async Task CachedRedirectIsSharedAcrossServicesAndStillIncrementsEveryAccess()
    {
        var repository = new RecordingRepository
        {
            StoredLink = new Link { Id = 42, Code = "ABCD", TargetUrl = "https://8.8.8.8", ManagementKeyHash = "hash" }
        };
        var cache = new LinkRedirectCache();
        var first = new LinkService(repository, () => "unused", new TargetUrlValidator(), redirectCache: cache);
        var second = new LinkService(repository, () => "unused", new TargetUrlValidator(), redirectCache: cache);

        var initial = await first.ResolveAsync("ABCD");
        var cached = await second.ResolveAsync("ABCD");
        var stats = await second.GetStatsAsync("ABCD");

        Assert.Equal(initial, cached);
        Assert.Equal(ResolveLinkStatus.Found, cached.Status);
        Assert.Equal(new long[] { 42, 42 }, repository.IncrementedLinkIds);
        Assert.Equal(2, repository.ReadCount); // One redirect lookup and one fresh stats lookup.
        Assert.Equal(LinkStatsStatus.Found, stats.Status);
    }

    [Fact]
    public async Task CachedLinkIsRecheckedForExpirationAtUtcMidnight()
    {
        var clock = new MutableTimeProvider();
        var repository = new RecordingRepository
        {
            StoredLink = new Link
            {
                Id = 42, Code = "ABCD", TargetUrl = "https://8.8.8.8", ManagementKeyHash = "hash",
                ExpiresAtUtc = clock.Now.UtcDateTime.Date
            }
        };
        var service = new LinkService(repository, () => "unused", new TargetUrlValidator(), clock, new LinkRedirectCache());

        Assert.Equal(ResolveLinkStatus.Found, (await service.ResolveAsync("ABCD")).Status);
        clock.Now = clock.Now.AddSeconds(2);
        Assert.Equal(ResolveLinkStatus.Expired, (await service.ResolveAsync("ABCD")).Status);
        Assert.Equal(1, repository.ReadCount);
        Assert.Single(repository.IncrementedLinkIds);
    }

    [Fact]
    public async Task MissingRedirectIsNotCached()
    {
        var repository = new RecordingRepository();
        var service = new LinkService(repository, () => "unused", new TargetUrlValidator(), redirectCache: new LinkRedirectCache());

        Assert.Equal(ResolveLinkStatus.NotFound, (await service.ResolveAsync("MISSING")).Status);
        Assert.Equal(ResolveLinkStatus.NotFound, (await service.ResolveAsync("MISSING")).Status);
        Assert.Equal(2, repository.ReadCount);
        Assert.Empty(repository.IncrementedLinkIds);
    }

    private sealed class MutableTimeProvider : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 19, 23, 59, 59, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    [Fact]
    public async Task StatsRequiresOnlyCodeWithoutChangingCount()
    {
        var link = new Link
        {
            Code = "ABC1234", TargetUrl = "https://8.8.8.8", AccessCount = 42,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(-1),
            ManagementKeyHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("secret")))
        };
        var repository = new RecordingRepository { StoredLink = link };
        var service = new LinkService(repository, () => throw new InvalidOperationException(), new TargetUrlValidator());

        var result = await service.GetStatsAsync(link.Code);

        Assert.Equal(LinkStatsStatus.Found, result.Status);
        Assert.Equal(42, result.AccessCount);
        Assert.Equal(link.Code, repository.RequestedCode);
        Assert.Equal(42, link.AccessCount);
        Assert.Empty(repository.IncrementedLinkIds);
        Assert.Empty(repository.Attempts);
    }

    [Fact]
    public async Task StatsForMissingCodeReturnsNotFound()
    {
        var repository = new RecordingRepository();
        var service = new LinkService(repository, () => throw new InvalidOperationException(), new TargetUrlValidator());

        var result = await service.GetStatsAsync("MISSING");

        Assert.Equal(LinkStatsStatus.NotFound, result.Status);
        Assert.Null(result.AccessCount);
        Assert.Empty(repository.IncrementedLinkIds);
    }

    [Theory]
    [InlineData(null, ResolveLinkStatus.Found)]
    [InlineData("2026-10-18T23:59:59Z", ResolveLinkStatus.Expired)]
    [InlineData("2026-10-19T00:00:00Z", ResolveLinkStatus.Found)]
    [InlineData("2026-10-20T00:00:00Z", ResolveLinkStatus.Found)]
    public async Task ResolveHonorsExpirationDateThroughEndOfUtcDay(string? expiration, ResolveLinkStatus expected)
    {
        var link = new Link
        {
            Id = 42, Code = "ABC1234", TargetUrl = "https://8.8.8.8/path", ManagementKeyHash = "hash",
            ExpiresAtUtc = expiration is null ? null : DateTimeOffset.Parse(expiration).UtcDateTime,
            AccessCount = 12
        };
        var repository = new RecordingRepository { StoredLink = link };
        var service = new LinkService(repository, () => throw new InvalidOperationException(),
            new TargetUrlValidator(), new FixedTimeProvider());

        var result = await service.ResolveAsync("ABC1234");

        Assert.Equal(expected, result.Status);
        Assert.Equal(expected == ResolveLinkStatus.Found ? link.TargetUrl : null, result.TargetUrl);
        Assert.Equal("ABC1234", repository.RequestedCode);
        Assert.Equal(12, link.AccessCount);
        Assert.Equal(expected == ResolveLinkStatus.Found ? new long[] { link.Id } : [],
            repository.IncrementedLinkIds);
        Assert.Empty(repository.Attempts);
    }

    [Fact]
    public async Task ResolveMissingCodeReturnsNotFound()
    {
        var repository = new RecordingRepository();
        var service = new LinkService(repository, () => throw new InvalidOperationException(), new TargetUrlValidator());

        var result = await service.ResolveAsync("MISSING");

        Assert.Equal(ResolveLinkStatus.NotFound, result.Status);
        Assert.Null(result.TargetUrl);
        Assert.Equal("MISSING", repository.RequestedCode);
        Assert.Empty(repository.IncrementedLinkIds);
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
            => new(2026, 10, 19, 23, 59, 59, TimeSpan.Zero);
    }

    [Fact]
    public async Task CreatesAutomaticLinkWithDefaultsAndHashedManagementKey()
    {
        var repository = new RecordingRepository();
        var service = new LinkService(repository, () => "ABC1234", new TargetUrlValidator());
        var before = DateTime.UtcNow;

        var result = await service.CreateAsync("https://8.8.8.8/path");

        Assert.Null(result.Error);
        var link = Assert.Single(repository.Attempts);
        Assert.Equal("ABC1234", result.Code);
        Assert.Equal("https://8.8.8.8/path", link.TargetUrl);
        Assert.InRange(link.CreatedAtUtc, before, DateTime.UtcNow);
        Assert.Equal(DateTimeKind.Utc, link.CreatedAtUtc.Kind);
        Assert.Null(link.ExpiresAtUtc);
        Assert.Equal(0, link.AccessCount);
        Assert.Equal(64, result.ManagementKey!.Length);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(result.ManagementKey))),
            link.ManagementKeyHash);
        Assert.NotEqual(result.ManagementKey, link.ManagementKeyHash);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("ftp://8.8.8.8")]
    [InlineData("http://127.0.0.1")]
    public async Task InvalidTargetDoesNotGenerateOrPersistLink(string? targetUrl)
    {
        var repository = new RecordingRepository();
        var service = new LinkService(repository, () => throw new InvalidOperationException(), new TargetUrlValidator());

        var result = await service.CreateAsync(targetUrl);

        Assert.NotNull(result.Error);
        Assert.Null(result.Code);
        Assert.Null(result.ManagementKey);
        Assert.Empty(repository.Attempts);
    }

    [Fact]
    public async Task GeneratesAnotherCodeWhenRepositoryReportsCollision()
    {
        var repository = new RecordingRepository { Collisions = 1 };
        var codes = new Queue<string>(["ABC1234", "XYZ5678"]);
        var service = new LinkService(repository, codes.Dequeue, new TargetUrlValidator());

        var result = await service.CreateAsync("https://8.8.8.8");

        Assert.Equal("XYZ5678", result.Code);
        Assert.Equal(new[] { "ABC1234", "XYZ5678" }, repository.Attempts.Select(link => link.Code));
    }

    [Theory]
    [InlineData("AB1I")]
    [InlineData("ABCDEFGHIJKLMNOPQRST")]
    public async Task CreatesCustomCodeWithoutGenerating(string code)
    {
        var repository = new RecordingRepository();
        var service = new LinkService(repository, () => throw new InvalidOperationException(), new TargetUrlValidator());

        var result = await service.CreateAsync("https://8.8.8.8", code);

        Assert.Null(result.Error);
        Assert.Equal(code, result.Code);
        Assert.Equal(code, Assert.Single(repository.Attempts).Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("ABC")]
    [InlineData("ABCDEFGHIJKLMNOPQRSTU")]
    [InlineData("ABC0")]
    [InlineData("abc1")]
    [InlineData("ABC ")]
    [InlineData("ABC-")]
    [InlineData("ÁBC1")]
    [InlineData("ABC\n")]
    public async Task InvalidCustomCodeDoesNotPersist(string code)
    {
        var repository = new RecordingRepository();
        var service = new LinkService(repository, () => throw new InvalidOperationException(), new TargetUrlValidator());

        var result = await service.CreateAsync("https://8.8.8.8", code);

        Assert.NotNull(result.Error);
        Assert.False(result.IsConflict);
        Assert.Empty(repository.Attempts);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DuplicateCustomCodeReturnsConflictIncludingInsertRace(bool existsBeforeInsert)
    {
        var repository = new RecordingRepository { Exists = existsBeforeInsert, Collisions = 1 };
        var service = new LinkService(repository, () => throw new InvalidOperationException(), new TargetUrlValidator());

        var result = await service.CreateAsync("https://8.8.8.8", "ABCD");

        Assert.True(result.IsConflict);
        Assert.Contains("já está em uso", result.Error);
        Assert.Null(result.Code);
        Assert.Null(result.ManagementKey);
        Assert.Equal(existsBeforeInsert ? 0 : 1, repository.Attempts.Count);
    }

    private sealed class RecordingRepository : ILinkRepository
    {
        public List<long> IncrementedLinkIds { get; } = [];

        public Task IncrementAccessCountAsync(long linkId, CancellationToken cancellationToken)
        {
            IncrementedLinkIds.Add(linkId);
            return Task.CompletedTask;
        }

        public Link? StoredLink { get; init; }
        public string? RequestedCode { get; private set; }
        public int ReadCount { get; private set; }

        public Task<Link?> FindByCodeAsync(string code, CancellationToken cancellationToken)
        {
            ReadCount++;
            RequestedCode = code;
            return Task.FromResult(StoredLink);
        }

        public List<Link> Attempts { get; } = [];
        public int Collisions { get; init; }
        public bool Exists { get; init; }

        public Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken)
            => Task.FromResult(Exists);

        public Task<bool> TryAddAsync(Link link, CancellationToken cancellationToken)
        {
            Attempts.Add(link);
            return Task.FromResult(Attempts.Count > Collisions);
        }
    }
}
