using UrlShortener.Api.Domain;

namespace UrlShortener.Api.Data;

public interface ILinkRepository
{
    Task<bool> TryAddAsync(Link link, CancellationToken cancellationToken);
}
