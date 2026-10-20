using System.Security.Cryptography;
using System.Text;
using UrlShortener.Api.Application;
using UrlShortener.Api.Data;
using UrlShortener.Api.Domain;

namespace UrlShortener.Api.Tests.Application;

public sealed class LinkServiceTests
{
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

    private sealed class RecordingRepository : ILinkRepository
    {
        public List<Link> Attempts { get; } = [];
        public int Collisions { get; init; }

        public Task<bool> TryAddAsync(Link link, CancellationToken cancellationToken)
        {
            Attempts.Add(link);
            return Task.FromResult(Attempts.Count > Collisions);
        }
    }
}
