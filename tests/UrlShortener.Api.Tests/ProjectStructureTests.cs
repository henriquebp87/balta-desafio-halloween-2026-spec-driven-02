namespace UrlShortener.Api.Tests;

public class ProjectStructureTests
{
    [Fact]
    public void ApiProjectAssemblyCanBeLoaded()
    {
        var assembly = typeof(Program).Assembly;

        Assert.Equal("UrlShortener.Api", assembly.GetName().Name);
    }
}
