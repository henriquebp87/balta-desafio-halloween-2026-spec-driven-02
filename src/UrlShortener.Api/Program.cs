using Microsoft.EntityFrameworkCore;
using UrlShortener.Api.Data;
using UrlShortener.Api.Application;
using UrlShortener.Api.Endpoints;

var app = Program.CreateApp(args);
app.Run();

public partial class Program
{
    public static WebApplication CreateApp(string[] args, Action<WebApplicationBuilder>? configure = null)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));
        builder.Services.AddScoped<ILinkRepository, LinkRepository>();
        builder.Services.AddSingleton<LinkCodeGenerator>();
        builder.Services.AddSingleton<TargetUrlValidator>();
        builder.Services.AddSingleton<LinkRedirectCache>();
        builder.Services.AddScoped<LinkService>(services => new LinkService(
            services.GetRequiredService<ILinkRepository>(),
            services.GetRequiredService<LinkCodeGenerator>().Generate,
            services.GetRequiredService<TargetUrlValidator>(),
            redirectCache: services.GetRequiredService<LinkRedirectCache>()));
        configure?.Invoke(builder);

        var app = builder.Build();
        app.MapLinkEndpoints();
        return app;
    }
}
