using System.Globalization;
using System.Security.Cryptography;
using HealthyMind.Application.Ports.Out;

namespace HealthyMind.Infrastructure.Security;

/// <summary>
/// PBKDF2-SHA512 con sal aleatoria (solo librería estándar de .NET).
/// Formato guardado: <c>PBKDF2-SHA512$iteraciones$sal$hash</c>, para poder subir las iteraciones sin romper lo existente.
/// </summary>
internal sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const string Scheme = "PBKDF2-SHA512";
    private const int Iterations = 210_000; // Recomendación OWASP vigente para SHA-512.
    private const int SaltSize = 16;
    private const int HashSize = 32;

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Derive(password, salt, Iterations);
        return string.Join(
            '$',
            Scheme,
            Iterations.ToString(CultureInfo.InvariantCulture),
            Convert.ToBase64String(salt),
            Convert.ToBase64String(hash));
    }

    public bool Verify(string password, string passwordHash)
    {
        var parts = passwordHash.Split('$');
        if (parts.Length != 4
            || parts[0] != Scheme
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var iterations))
        {
            return false;
        }

        try
        {
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            return CryptographicOperations.FixedTimeEquals(Derive(password, salt, iterations, expected.Length), expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static byte[] Derive(string password, byte[] salt, int iterations, int size = HashSize) =>
        Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA512, size);
}
