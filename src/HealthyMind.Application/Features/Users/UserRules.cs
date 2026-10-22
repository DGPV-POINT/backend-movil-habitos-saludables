using FluentValidation;
using HealthyMind.Domain.Users;

namespace HealthyMind.Application.Features.Users;

/// <summary>Reglas de formato compartidas por registro, alta por administrador y perfil.</summary>
public static class UserRules
{
    public const int PasswordMinLength = 8;
    public const int PasswordMaxLength = 100;

    public static IRuleBuilderOptions<T, string> ValidUsername<T>(this IRuleBuilder<T, string> rule) => rule
        .NotEmpty().WithMessage("El usuario es obligatorio.")
        .Length(User.UsernameMinLength, User.UsernameMaxLength)
        .WithMessage($"El usuario debe tener entre {User.UsernameMinLength} y {User.UsernameMaxLength} caracteres.")
        .Matches("^[a-zA-Z0-9._-]+$").WithMessage("El usuario solo admite letras, números, punto, guion y guion bajo.");

    public static IRuleBuilderOptions<T, string> ValidEmail<T>(this IRuleBuilder<T, string> rule) => rule
        .NotEmpty().WithMessage("El correo es obligatorio.")
        .MaximumLength(User.EmailMaxLength).WithMessage($"El correo no puede superar los {User.EmailMaxLength} caracteres.")
        .EmailAddress().WithMessage("El correo no tiene un formato válido.");

    public static IRuleBuilderOptions<T, string> ValidPersonName<T>(this IRuleBuilder<T, string> rule, string field) => rule
        .NotEmpty().WithMessage($"El {field} es obligatorio.")
        .MaximumLength(User.NameMaxLength).WithMessage($"El {field} no puede superar los {User.NameMaxLength} caracteres.");

    public static IRuleBuilderOptions<T, string> StrongPassword<T>(this IRuleBuilder<T, string> rule) => rule
        .NotEmpty().WithMessage("La contraseña es obligatoria.")
        .Length(PasswordMinLength, PasswordMaxLength)
        .WithMessage($"La contraseña debe tener entre {PasswordMinLength} y {PasswordMaxLength} caracteres.")
        .Matches("[A-Z]").WithMessage("La contraseña debe tener al menos una mayúscula.")
        .Matches("[a-z]").WithMessage("La contraseña debe tener al menos una minúscula.")
        .Matches("[0-9]").WithMessage("La contraseña debe tener al menos un número.");

    public static IRuleBuilderOptions<T, string?> ValidTimeZone<T>(this IRuleBuilder<T, string?> rule) => rule
        .Must(zone => zone is null || User.IsValidTimeZone(zone))
        .WithMessage("La zona horaria no es válida (usa un identificador IANA, p. ej. America/Guayaquil).");

    public static IRuleBuilderOptions<T, string> ValidDeviceId<T>(this IRuleBuilder<T, string> rule) => rule
        .NotEmpty().WithMessage("El identificador del dispositivo es obligatorio.")
        .MaximumLength(RefreshToken.DeviceIdMaxLength)
        .WithMessage($"El identificador del dispositivo no puede superar los {RefreshToken.DeviceIdMaxLength} caracteres.");
}

public sealed class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(x => x.FirstName).ValidPersonName("nombre");
        RuleFor(x => x.LastName).ValidPersonName("apellido");
        RuleFor(x => x.Email).ValidEmail();
        RuleFor(x => x.TimeZoneId).ValidTimeZone();
    }
}

public sealed class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty().WithMessage("La contraseña actual es obligatoria.");
        RuleFor(x => x.NewPassword).StrongPassword()
            .NotEqual(x => x.CurrentPassword).WithMessage("La nueva contraseña debe ser distinta de la actual.");
        RuleFor(x => x.DeviceId).ValidDeviceId();
    }
}

public sealed class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(x => x.Username).ValidUsername();
        RuleFor(x => x.Email).ValidEmail();
        RuleFor(x => x.FirstName).ValidPersonName("nombre");
        RuleFor(x => x.LastName).ValidPersonName("apellido");
        RuleFor(x => x.Password).StrongPassword();
        RuleFor(x => x.Role).IsInEnum().WithMessage("El rol debe ser User o Admin.");
        RuleFor(x => x.TimeZoneId).ValidTimeZone();
    }
}
