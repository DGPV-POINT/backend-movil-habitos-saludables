using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace HealthyMind.Infrastructure.Security;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; set; } = "HealthyMind";

    [Required]
    public string Audience { get; set; } = "HealthyMind.Mobile";

    /// <summary>Clave HMAC de al menos 32 caracteres. Nunca en el repositorio: user-secrets o variable de entorno.</summary>
    [Required]
    [MinLength(32, ErrorMessage = "Jwt:SigningKey debe tener al menos 32 caracteres.")]
    public string SigningKey { get; set; } = string.Empty;

    [Range(1, 120)]
    public int AccessTokenMinutes { get; set; } = 15;

    public SymmetricSecurityKey CreateSigningKey() => new(Encoding.UTF8.GetBytes(SigningKey));
}
