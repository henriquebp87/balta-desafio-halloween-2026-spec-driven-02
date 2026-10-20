using System.Net;
using System.Net.Sockets;
using UrlShortener.Api.Application;

namespace UrlShortener.Api.Tests.Application;

public sealed class TargetUrlValidatorTests
{
    private static TargetUrlValidator PublicDestination() =>
        new((_, _) => Task.FromResult(new[] { IPAddress.Parse("8.8.8.8") }));

    [Theory]
    [InlineData("http://example.com")]
    [InlineData("https://example.com/path?key=value#section")]
    [InlineData("HTTPS://example.com")]
    [InlineData("https://8.8.8.8")]
    [InlineData("https://[2606:4700:4700::1111]")]
    public async Task ValidateAcceptsHttpAndHttpsPublicDestinations(string url)
    {
        Assert.Null(await PublicDestination().ValidateAsync(url));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("example.com")]
    [InlineData("/relative")]
    [InlineData("ftp://example.com")]
    [InlineData("javascript:alert(1)")]
    [InlineData("https://")]
    [InlineData(" https://example.com")]
    [InlineData("https://example.com\n")]
    public async Task ValidateRejectsInvalidUrlsWithoutResolvingDns(string? url)
    {
        var validator = new TargetUrlValidator((_, _) => throw new InvalidOperationException("Unexpected DNS lookup"));
        Assert.NotNull(await validator.ValidateAsync(url));
    }

    [Theory]
    [InlineData(2047, true)]
    [InlineData(2048, true)]
    [InlineData(2049, false)]
    public async Task ValidateEnforcesLengthBoundary(int length, bool valid)
    {
        const string prefix = "https://example.com/";
        var error = await PublicDestination().ValidateAsync(prefix + new string('a', length - prefix.Length));
        Assert.Equal(valid, error is null);
    }

    [Theory]
    [InlineData("0.0.0.0")]
    [InlineData("127.0.0.1")]
    [InlineData("10.0.0.1")]
    [InlineData("100.64.0.1")]
    [InlineData("169.254.169.254")]
    [InlineData("172.16.0.1")]
    [InlineData("192.168.1.1")]
    [InlineData("192.0.2.1")]
    [InlineData("198.18.0.1")]
    [InlineData("224.0.0.1")]
    [InlineData("255.255.255.255")]
    [InlineData("[::1]")]
    [InlineData("[::]")]
    [InlineData("[fc00::1]")]
    [InlineData("[fe80::1]")]
    [InlineData("[ff02::1]")]
    [InlineData("[::ffff:127.0.0.1]")]
    [InlineData("[2001:db8::1]")]
    public async Task ValidateRejectsNonPublicIpLiterals(string host)
    {
        Assert.NotNull(await PublicDestination().ValidateAsync($"http://{host}/"));
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("192.168.1.1")]
    [InlineData("::1")]
    public async Task ValidateRejectsHostnameWithAnyNonPublicAddress(string privateAddress)
    {
        var validator = new TargetUrlValidator((_, _) => Task.FromResult(new[]
        {
            IPAddress.Parse("8.8.8.8"), IPAddress.Parse(privateAddress)
        }));
        Assert.NotNull(await validator.ValidateAsync("https://example.com"));
    }

    [Fact]
    public async Task ValidateRejectsUnresolvedHostname()
    {
        var validator = new TargetUrlValidator((_, _) => throw new SocketException());
        Assert.NotNull(await validator.ValidateAsync("https://unknown.invalid"));
    }

    [Fact]
    public async Task ValidateRejectsHostnameWithoutAddresses()
    {
        var validator = new TargetUrlValidator((_, _) => Task.FromResult(Array.Empty<IPAddress>()));
        Assert.NotNull(await validator.ValidateAsync("https://example.com"));
    }

    [Fact]
    public async Task ValidatePropagatesCancellationToDnsResolver()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        var validator = new TargetUrlValidator((_, token) => Task.FromCanceled<IPAddress[]>(token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            validator.ValidateAsync("https://example.com", source.Token));
    }
}
