using System.Security.Cryptography;
using System.Text;
using UrlShortener.Api.Data;
using UrlShortener.Api.Domain;

namespace UrlShortener.Api.Application;

public sealed record CreateLinkResult(string? Code, string? ManagementKey, string? Error, bool IsConflict = false);

public sealed class LinkService(
    ILinkRepository repository,
    Func<string> generateCode,
    TargetUrlValidator validator)
{
    public async Task<CreateLinkResult> CreateAsync(string? targetUrl, CancellationToken cancellationToken = default)
        => await CreateAsync(targetUrl, null, cancellationToken);

    public async Task<CreateLinkResult> CreateAsync(string? targetUrl, string? code,
        CancellationToken cancellationToken = default)
    {
        if (code is not null && (code.Length is < 4 or > 20 ||
            code.Any(character => character is not (>= 'A' and <= 'Z') and not (>= '1' and <= '9'))))
            return new(null, null, "O código deve ter de 4 a 20 caracteres, usando apenas letras maiúsculas de A a Z e números de 1 a 9.");

        var error = await validator.ValidateAsync(targetUrl, cancellationToken);
        if (error is not null)
            return new(null, null, error);

        if (code is not null && await repository.CodeExistsAsync(code, cancellationToken))
            return new(null, null, "O código informado já está em uso.", IsConflict: true);

        var managementKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(managementKey)));
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var link = new Link
            {
                Code = code ?? generateCode(),
                TargetUrl = targetUrl!,
                CreatedAtUtc = DateTime.UtcNow,
                ManagementKeyHash = hash
            };
            if (await repository.TryAddAsync(link, cancellationToken))
                return new(link.Code, managementKey, null);
            if (code is not null)
                return new(null, null, "O código informado já está em uso.", IsConflict: true);
        }
    }
}
