using FluentValidation;
using HealthyMind.Application.Features.Users;
using HealthyMind.Domain.Users;

namespace HealthyMind.Application.Features.Auth;

/// <param name="DeviceId">Identificador estable de la instalación en el móvil; ata la sesión a ese equipo.</param>
public sealed record RegisterRequest(
    string Username,
    string Email,
    string FirstName,
    string LastName,
    string Password,
    string DeviceId,
    string? DeviceName,
    string? TimeZoneId);

public sealed record LoginRequest(string UsernameOrEmail, string Password, string DeviceId, string? DeviceName);

public sealed record RefreshRequest(string RefreshToken, string DeviceId);

public sealed record LogoutRequest(string RefreshToken);

/// <summary>
/// El móvil guarda <see cref="RefreshToken"/> en almacenamiento seguro protegido por huella;
/// <see cref="AccessToken"/> dura minutos y se renueva con <c>/api/auth/refresh</c>.
/// </summary>
public sealed record AuthResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    UserProfileResponse User);

public sealed record SessionResponse(
    Guid Id,
    string DeviceId,
    string? DeviceName,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt)
{
    public static SessionResponse From(RefreshToken token) =>
        new(token.Id, token.DeviceId, token.DeviceName, token.CreatedAt, token.ExpiresAt);
}

public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>
    /// Cuánto puede pasar sin abrir la app antes de pedir contraseña otra vez.
    /// Cada renovación reinicia el plazo.
    /// </summary>
    public int RefreshTokenLifetimeDays { get; set; } = 60;
}

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Username).ValidUsername();
        RuleFor(x => x.Email).ValidEmail();
        RuleFor(x => x.FirstName).ValidPersonName("nombre");
        RuleFor(x => x.LastName).ValidPersonName("apellido");
        RuleFor(x => x.Password).StrongPassword();
        RuleFor(x => x.DeviceId).ValidDeviceId();
        RuleFor(x => x.DeviceName).MaximumLength(RefreshToken.DeviceNameMaxLength);
        RuleFor(x => x.TimeZoneId).ValidTimeZone();
    }
}

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.UsernameOrEmail).NotEmpty().WithMessage("El usuario o correo es obligatorio.")
            .MaximumLength(User.EmailMaxLength);
        RuleFor(x => x.Password).NotEmpty().WithMessage("La contraseña es obligatoria.")
            .MaximumLength(UserRules.PasswordMaxLength);
        RuleFor(x => x.DeviceId).ValidDeviceId();
        RuleFor(x => x.DeviceName).MaximumLength(RefreshToken.DeviceNameMaxLength);
    }
}

public sealed class RefreshRequestValidator : AbstractValidator<RefreshRequest>
{
    public RefreshRequestValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty().WithMessage("El refresh token es obligatorio.").MaximumLength(200);
        RuleFor(x => x.DeviceId).ValidDeviceId();
    }
}

public sealed class LogoutRequestValidator : AbstractValidator<LogoutRequest>
{
    public LogoutRequestValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty().WithMessage("El refresh token es obligatorio.").MaximumLength(200);
    }
}
