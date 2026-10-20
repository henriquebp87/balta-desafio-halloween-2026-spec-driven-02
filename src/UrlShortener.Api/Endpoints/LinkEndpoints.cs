using UrlShortener.Api.Application;

namespace UrlShortener.Api.Endpoints;

public sealed record CreateLinkRequest(string? TargetUrl, string? Code = null);
public sealed record CreateLinkResponse(string ShortUrl, string Code, string ManagementKey);

public static class LinkEndpoints
{
    public static void MapLinkEndpoints(this WebApplication app)
    {
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
