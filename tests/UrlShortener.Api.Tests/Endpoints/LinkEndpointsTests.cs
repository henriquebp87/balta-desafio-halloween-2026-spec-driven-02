using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UrlShortener.Api.Data;
using UrlShortener.Api.Domain;
using UrlShortener.Api.Endpoints;

namespace UrlShortener.Api.Tests.Endpoints;

public sealed class LinkEndpointsTests
{
    [Fact]
    public async Task PostCreatesLinkReturns201AndPersistsOnlyManagementKeyHash()
    {
        await WithApi(async (client, options) =>
        {
            using var response = await client.PostAsJsonAsync("/api/links", new { targetUrl = "https://8.8.8.8/path" });

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<CreateLinkResponse>();
            Assert.NotNull(body);
            Assert.Matches("^[A-HJ-NP-Z1-9]{7}$", body.Code);
            Assert.Equal($"{client.BaseAddress}{body.Code}", body.ShortUrl);
            Assert.Equal(body.ShortUrl, response.Headers.Location!.ToString());

            await using var verification = new AppDbContext(options);
            var link = await verification.Links.SingleAsync();
            Assert.Equal(body.Code, link.Code);
            Assert.Equal("https://8.8.8.8/path", link.TargetUrl);
            Assert.Equal(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(body.ManagementKey))), link.ManagementKeyHash);
            Assert.Equal(0, link.AccessCount);
            Assert.Null(link.ExpiresAtUtc);
        });
    }

    [Fact]
    public async Task PostInvalidTargetReturns400ProblemDetailsWithoutPersisting()
    {
        await WithApi(async (client, options) =>
        {
            using var response = await client.PostAsJsonAsync("/api/links", new { targetUrl = "ftp://8.8.8.8" });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
            var problem = await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>();
            Assert.Equal(400, problem!.Status);
            Assert.Contains("http ou https", problem.Detail);
            await using var verification = new AppDbContext(options);
            Assert.Empty(await verification.Links.ToListAsync());
        });
    }

    [Fact]
    public async Task RepositoryReportsCollisionAndAllowsRetry()
    {
        await WithApi(async (_, options) =>
        {
            await using var context = new AppDbContext(options);
            var repository = new LinkRepository(context);
            Link NewLink(string code) => new()
            {
                Code = code, TargetUrl = "https://8.8.8.8", CreatedAtUtc = DateTime.UtcNow, ManagementKeyHash = "hash"
            };
            Assert.True(await repository.TryAddAsync(NewLink("ABC1234"), default));
            Assert.False(await repository.TryAddAsync(NewLink("ABC1234"), default));
            Assert.True(await repository.TryAddAsync(NewLink("XYZ5678"), default));
            Assert.Equal(2, await context.Links.CountAsync());
        });
    }

    private static async Task WithApi(Func<HttpClient, DbContextOptions<AppDbContext>, Task> test)
    {
        var connectionString = $"Data Source=T05-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connectionString).Options;
        await using (var context = new AppDbContext(options))
            await context.Database.MigrateAsync();

        await using var app = Program.CreateApp([], builder =>
        {
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connectionString
            });
            builder.WebHost.UseUrls("http://127.0.0.1:0");
        });
        await app.StartAsync();
        try
        {
            var address = app.Services.GetRequiredService<IServer>().Features
                .Get<IServerAddressesFeature>()!.Addresses.Single();
            using var client = new HttpClient { BaseAddress = new Uri(address) };
            await test(client, options);
        }
        finally
        {
            await app.StopAsync();
        }
    }
}
