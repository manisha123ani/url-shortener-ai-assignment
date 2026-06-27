using System.Security.Cryptography;

namespace UrlShortener.Api.Services;

public interface IShortCodeGenerator
{
    /// <summary>Deterministic base62 encoding of an internal id (used as the primary strategy).</summary>
    string FromId(long id);

    /// <summary>Random base62 code, used as a fallback when a custom alias collides or for pre-generation.</summary>
    string RandomCode(int length = 7);
}

/// <summary>
/// Base62 ([0-9a-zA-Z]) short code generation.
/// Primary strategy: encode the DB-assigned identity column. This guarantees uniqueness
/// without needing a distributed counter or collision-retry loop, at the cost of codes
/// being sequential/guessable (acceptable for this prototype — see ARCHITECTURE.md "Trade-offs").
/// </summary>
public class Base62ShortCodeGenerator : IShortCodeGenerator
{
    private const string Alphabet = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";

    public string FromId(long id)
    {
        if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id), "Id must be positive.");

        var chars = new Stack<char>();
        var value = id;
        while (value > 0)
        {
            chars.Push(Alphabet[(int)(value % 62)]);
            value /= 62;
        }
        return new string(chars.ToArray());
    }

    public string RandomCode(int length = 7)
    {
        var bytes = RandomNumberGenerator.GetBytes(length);
        var chars = new char[length];
        for (int i = 0; i < length; i++)
        {
            chars[i] = Alphabet[bytes[i] % Alphabet.Length];
        }
        return new string(chars);
    }
}
