using System.Security.Cryptography;

namespace UrlShortener.Api.Application;

public sealed class LinkCodeGenerator
{
    // I and O are omitted to avoid confusion with the digits 1 and 0 (D-03).
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ123456789";
    private const int CodeLength = 7;

    public string Generate()
    {
        return RandomNumberGenerator.GetString(Alphabet, CodeLength);
    }
}
