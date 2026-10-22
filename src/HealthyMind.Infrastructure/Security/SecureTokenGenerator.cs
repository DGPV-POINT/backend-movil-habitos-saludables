using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using HealthyMind.Application.Ports.Out;

namespace HealthyMind.Infrastructure.Security;

internal sealed class SecureTokenGenerator : ISecureTokenGenerator
{
    private const int TokenBytes = 64;

    public string Generate() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(TokenBytes));

    /// <summary>SHA-256 basta: el token ya es aleatorio de 512 bits, no hace falta un hash lento.</summary>
    public string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
