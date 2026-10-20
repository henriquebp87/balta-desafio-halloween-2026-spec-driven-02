using UrlShortener.Api.Domain;

namespace UrlShortener.Api.Data;

public interface ILinkRepository
{
    Task IncrementAccessCountAsync(long linkId, CancellationToken cancellationToken);
    Task<Link?> FindByCodeAsync(string code, CancellationToken cancellationToken);
    Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken);
    Task<bool> TryAddAsync(Link link, CancellationToken cancellationToken);
}
