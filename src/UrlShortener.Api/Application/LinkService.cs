using System.Security.Cryptography;
using System.Text;
using UrlShortener.Api.Data;
using UrlShortener.Api.Domain;

namespace UrlShortener.Api.Application;

public sealed record CreateLinkResult(string? Code, string? ManagementKey, string? Error);

public sealed class LinkService(
    ILinkRepository repository,
    Func<string> generateCode,
    TargetUrlValidator validator)
{
    public async Task<CreateLinkResult> CreateAsync(string? targetUrl, CancellationToken cancellationToken = default)
    {
        var error = await validator.ValidateAsync(targetUrl, cancellationToken);
        if (error is not null)
            return new(null, null, error);

        var managementKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(managementKey)));
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var link = new Link
            {
                Code = generateCode(),
                TargetUrl = targetUrl!,
                CreatedAtUtc = DateTime.UtcNow,
                ManagementKeyHash = hash
            };
            if (await repository.TryAddAsync(link, cancellationToken))
                return new(link.Code, managementKey, null);
        }
    }
}
