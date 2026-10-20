using UrlShortener.Api.Application;

namespace UrlShortener.Api.Tests.Application;

public sealed class LinkCodeGeneratorTests
{
    [Fact]
    public void GenerateReturnsSevenCharactersFromUnambiguousAlphabet()
    {
        var generator = new LinkCodeGenerator();
        const string allowedCharacters = "ABCDEFGHJKLMNPQRSTUVWXYZ123456789";

        for (var sample = 0; sample < 1_000; sample++)
        {
            var code = generator.Generate();

            Assert.Equal(7, code.Length);
            Assert.Matches("^[A-Z1-9]{7}$", code);
            Assert.All(code, character => Assert.Contains(character, allowedCharacters));
        }
    }
}
