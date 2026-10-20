using UrlShortener.Api.Domain;

namespace UrlShortener.Api.Data;

public interface ILinkRepository
{
    Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken);
    Task<bool> TryAddAsync(Link link, CancellationToken cancellationToken);
}
