using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using UrlShortener.Api.Domain;

namespace UrlShortener.Api.Data;

public sealed class LinkRepository(AppDbContext context) : ILinkRepository
{
    public async Task<bool> TryAddAsync(Link link, CancellationToken cancellationToken)
    {
        context.Links.Add(link);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is SqliteException { SqliteExtendedErrorCode: 2067 })
        {
            context.Entry(link).State = EntityState.Detached;
            return false;
        }
    }
}
