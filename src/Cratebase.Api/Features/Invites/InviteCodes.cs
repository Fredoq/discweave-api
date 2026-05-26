using System.Security.Cryptography;
using System.Text;

namespace Cratebase.Api.Features.Invites;

internal static class InviteCodes
{
    private const string Prefix = "CRATE";
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const int RandomCharacterCount = 16;

    public static string Generate()
    {
        Span<byte> bytes = stackalloc byte[RandomCharacterCount];
        RandomNumberGenerator.Fill(bytes);

        Span<char> characters = stackalloc char[RandomCharacterCount];
        for (int index = 0; index < bytes.Length; index++)
        {
            characters[index] = Alphabet[bytes[index] % Alphabet.Length];
        }

        return string.Create(
            Prefix.Length + 4 + RandomCharacterCount,
            characters.ToString(),
            static (destination, randomCharacters) =>
            {
                Prefix.CopyTo(destination);
                destination[5] = '-';
                destination[6] = randomCharacters[0];
                destination[7] = randomCharacters[1];
                destination[8] = randomCharacters[2];
                destination[9] = randomCharacters[3];
                destination[10] = '-';
                destination[11] = randomCharacters[4];
                destination[12] = randomCharacters[5];
                destination[13] = randomCharacters[6];
                destination[14] = randomCharacters[7];
                destination[15] = '-';
                destination[16] = randomCharacters[8];
                destination[17] = randomCharacters[9];
                destination[18] = randomCharacters[10];
                destination[19] = randomCharacters[11];
                destination[20] = '-';
                destination[21] = randomCharacters[12];
                destination[22] = randomCharacters[13];
                destination[23] = randomCharacters[14];
                destination[24] = randomCharacters[15];
            });
    }

    public static string Hash(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return string.Empty;
        }

        string normalized = Normalize(code);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));

        return Convert.ToHexString(hash);
    }

    private static string Normalize(string code)
    {
        StringBuilder builder = new(code.Length);
        foreach (char character in code)
        {
            if (character is '-' || char.IsWhiteSpace(character))
            {
                continue;
            }

            _ = builder.Append(char.ToUpperInvariant(character));
        }

        return builder.ToString();
    }
}
