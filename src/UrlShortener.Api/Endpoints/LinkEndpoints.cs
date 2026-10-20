using UrlShortener.Api.Application;

namespace UrlShortener.Api.Endpoints;

public sealed record CreateLinkRequest(string? TargetUrl, string? Code = null);
public sealed record CreateLinkResponse(string ShortUrl, string Code, string ManagementKey);
public sealed record LinkStatsResponse(long AccessCount);

public static class LinkEndpoints
{
    public static void MapLinkEndpoints(this WebApplication app)
    {
        app.MapGet("/api/links/{code}/stats", async (string code,
            LinkService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetStatsAsync(code, cancellationToken);
            return result.Status switch
            {
                LinkStatsStatus.Found => Results.Ok(new LinkStatsResponse(result.AccessCount!.Value)),
                _ => Results.Problem(statusCode: 404, title: "Link não encontrado.")
            };
        });

        app.MapGet("/{code}", async (string code, LinkService service, CancellationToken cancellationToken) =>
        {
            var result = await service.ResolveAsync(code, cancellationToken);
            return result.Status switch
            {
                ResolveLinkStatus.Found => Results.Redirect(result.TargetUrl!),
                ResolveLinkStatus.Expired => Results.Problem(statusCode: 410, title: "Link expirado."),
                _ => Results.Problem(statusCode: 404, title: "Link não encontrado.")
            };
        });

        app.MapPost("/api/links", async (CreateLinkRequest request, LinkService service,
            HttpRequest httpRequest, CancellationToken cancellationToken) =>
        {
            var result = await service.CreateAsync(request.TargetUrl, request.Code, cancellationToken);
            if (result.IsConflict)
                return Results.Problem(statusCode: 409, title: "Código já em uso.", detail: result.Error);
            if (result.Error is not null)
                return Results.Problem(statusCode: 400, title: "Dados de criação inválidos.", detail: result.Error);

            var shortUrl = $"{httpRequest.Scheme}://{httpRequest.Host}{httpRequest.PathBase}/{result.Code}";
            return Results.Created(shortUrl, new CreateLinkResponse(shortUrl, result.Code!, result.ManagementKey!));
        });
    }
}
