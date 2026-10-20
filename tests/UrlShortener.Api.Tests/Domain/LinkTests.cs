using UrlShortener.Api.Domain;

namespace UrlShortener.Api.Tests.Domain;

public sealed class LinkTests
{
    [Fact]
    public void StoresPlannedDataModel()
    {
        var createdAtUtc = new DateTime(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc);
        var expiresAtUtc = createdAtUtc.AddDays(1);

        var link = new Link
        {
            Id = 1,
            Code = "ABC1234",
            TargetUrl = "https://example.com",
            CreatedAtUtc = createdAtUtc,
            ExpiresAtUtc = expiresAtUtc,
            AccessCount = 3,
            ManagementKeyHash = "hash"
        };

        Assert.Equal(1, link.Id);
        Assert.Equal("ABC1234", link.Code);
        Assert.Equal("https://example.com", link.TargetUrl);
        Assert.Equal(createdAtUtc, link.CreatedAtUtc);
        Assert.Equal(expiresAtUtc, link.ExpiresAtUtc);
        Assert.Equal(3, link.AccessCount);
        Assert.Equal("hash", link.ManagementKeyHash);
    }
}
