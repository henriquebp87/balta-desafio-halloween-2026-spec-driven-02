using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using UrlShortener.Api.Data;
using UrlShortener.Api.Domain;

namespace UrlShortener.Api.Tests.Data;

public sealed class LinkPersistenceTests
{
    [Fact]
    public async Task MigrationCreatesSchemaAndPersistsLink()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var context = new AppDbContext(options))
        {
            await context.Database.MigrateAsync();
            context.Links.Add(new Link
            {
                Code = "ABC1234",
                TargetUrl = "https://example.com",
                CreatedAtUtc = new DateTime(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc),
                ExpiresAtUtc = null,
                ManagementKeyHash = "hash"
            });
            await context.SaveChangesAsync();
        }

        await using var verificationContext = new AppDbContext(options);
        var persistedLink = await verificationContext.Links.SingleAsync();

        Assert.Equal("ABC1234", persistedLink.Code);
        Assert.Equal("https://example.com", persistedLink.TargetUrl);
        Assert.Equal(0, persistedLink.AccessCount);
    }
}
