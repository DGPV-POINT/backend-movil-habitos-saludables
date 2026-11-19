using FluentValidation;
using HealthyMind.Application.Common;
using HealthyMind.Application.Features.Users;
using HealthyMind.Application.Ports.In;
using HealthyMind.Application.Ports.Out;
using HealthyMind.Domain.Users;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HealthyMind.Application.Features.Auth;

internal sealed class AuthUseCases(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IPasswordHasher passwordHasher,
    ISecureTokenGenerator tokenGenerator,
    UserRegistration registration,
    SessionIssuer sessions,
    AccountCodes codes,
    IOptions<AdminOptions> adminOptions,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    IValidator<RegisterRequest> registerValidator,
    IValidator<LoginRequest> loginValidator,
    IValidator<RefreshRequest> refreshValidator,
    IValidator<LogoutRequest> logoutValidator,
    IValidator<VerifyEmailRequest> verifyValidator,
    IValidator<EmailRequest> emailValidator,
    IValidator<ResetPasswordRequest> resetValidator,
    ILogger<AuthUseCases> logger) : IAuthUseCases
{
    private const string InvalidCredentials = "Usuario o contraseña incorrectos.";
    private const string InvalidSession = "La sesión no es válida. Inicia sesión con tu contraseña.";
    private const string SuspendedAccount = "La cuenta se encuentra suspendida.";
    private const string InvalidCode = "El código es incorrecto o ya venció. Pide uno nuevo.";

    // Hash de relleno: si el usuario no existe se verifica igual, para no delatar por tiempo de respuesta qué cuentas existen.
    private readonly Lazy<string> _dummyHash = new(() => passwordHasher.Hash(Guid.NewGuid().ToString()));

    public async Task<RegisterResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        await registerValidator.ValidateAndThrowAsync(request, cancellationToken);
        var role = adminOptions.Value.IsAdminEmail(request.Email) ? UserRole.Admin : UserRole.User;

        var user = await registration.RegisterAsync(
            new NewUser(
                request.Username,
                request.Email,
                request.FirstName,
                request.LastName,
                request.Password,
                role,
                request.TimeZoneId),
            cancellationToken);

        // Primero se graba la cuenta: si el correo falla, la persona puede pedir el código otra vez.
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await codes.SendAsync(user, UserCodePurpose.VerifyEmail, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new RegisterResponse(user.Email, (int)UserCode.Lifetime.TotalMinutes);
    }

    public async Task<AuthResponse> VerifyEmailAsync(VerifyEmailRequest request, CancellationToken cancellationToken)
    {
        await verifyValidator.ValidateAndThrowAsync(request, cancellationToken);
        var now = timeProvider.GetUtcNow();

        var user = await users.GetByEmailAsync(request.Email, cancellationToken)
            ?? throw new AuthenticationFailedException(InvalidCode);

        if (user.IsEmailVerified)
        {
            throw new ConflictException("Tu correo ya está confirmado. Inicia sesión con tu contraseña.");
        }

        var valid = await codes.TryUseAsync(user, UserCodePurpose.VerifyEmail, request.Code, cancellationToken);
        if (!valid)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken); // guarda el intento gastado
            throw new AuthenticationFailedException(InvalidCode);
        }

        user.MarkEmailVerified(now);
        if (!user.IsActive)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            throw new ForbiddenException(SuspendedAccount);
        }

        user.RegisterSuccessfulLogin(now);
        var response = await sessions.OpenAsync(user, request.DeviceId, request.DeviceName, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return response;
    }

    public async Task ResendVerificationAsync(EmailRequest request, CancellationToken cancellationToken)
    {
        await emailValidator.ValidateAndThrowAsync(request, cancellationToken);
        var user = await users.GetByEmailAsync(request.Email, cancellationToken);
        if (user is { IsEmailVerified: false, IsActive: true })
        {
            await codes.SendAsync(user, UserCodePurpose.VerifyEmail, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task ForgotPasswordAsync(EmailRequest request, CancellationToken cancellationToken)
    {
        await emailValidator.ValidateAndThrowAsync(request, cancellationToken);
        var user = await users.GetByEmailAsync(request.Email, cancellationToken);
        if (user is { IsActive: true })
        {
            await codes.SendAsync(user, UserCodePurpose.ResetPassword, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        await resetValidator.ValidateAndThrowAsync(request, cancellationToken);

        var user = await users.GetByEmailAsync(request.Email, cancellationToken)
            ?? throw new AuthenticationFailedException(InvalidCode);

        var valid = await codes.TryUseAsync(user, UserCodePurpose.ResetPassword, request.Code, cancellationToken);
        if (!valid || !user.IsActive)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            throw valid ? new ForbiddenException(SuspendedAccount) : new AuthenticationFailedException(InvalidCode);
        }

        user.ResetPassword(passwordHasher.Hash(request.NewPassword), timeProvider.GetUtcNow());
        await sessions.RevokeAllAsync(user.Id, RevocationReasons.PasswordChanged, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        await loginValidator.ValidateAndThrowAsync(request, cancellationToken);
        var now = timeProvider.GetUtcNow();

        var user = await users.GetByLoginAsync(request.UsernameOrEmail.Trim(), cancellationToken);
        if (user is null)
        {
            passwordHasher.Verify(request.Password, _dummyHash.Value);
            throw new AuthenticationFailedException(InvalidCredentials);
        }

        if (user.IsLockedOut(now))
        {
            var minutes = (int)Math.Ceiling((user.LockoutEndsAt!.Value - now).TotalMinutes);
            throw new AuthenticationFailedException(
                $"Cuenta bloqueada por intentos fallidos. Intenta de nuevo en {minutes} minuto(s).");
        }

        if (!passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            user.RegisterFailedLogin(now);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            throw new AuthenticationFailedException(InvalidCredentials);
        }

        // Se revisa después de la contraseña para no confirmar a un tercero que la cuenta existe.
        if (!user.IsActive)
        {
            throw new ForbiddenException(SuspendedAccount);
        }

        if (!user.IsEmailVerified)
        {
            await codes.SendAsync(user, UserCodePurpose.VerifyEmail, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            throw new EmailNotVerifiedException(user.Email);
        }

        user.RegisterSuccessfulLogin(now);
        var response = await sessions.OpenAsync(user, request.DeviceId, request.DeviceName, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return response;
    }

    public async Task<AuthResponse> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken)
    {
        await refreshValidator.ValidateAndThrowAsync(request, cancellationToken);
        var now = timeProvider.GetUtcNow();

        var token = await refreshTokens.GetByHashAsync(tokenGenerator.Hash(request.RefreshToken.Trim()), cancellationToken);
        if (token is null)
        {
            throw new AuthenticationFailedException(InvalidSession);
        }

        if (token.WasRotated)
        {
            // Un token ya cambiado volvió a usarse: alguien más lo tiene. Se corta todo el dispositivo.
            logger.LogWarning(
                "Reutilización de refresh token del usuario {UserId} en el dispositivo {DeviceId}.",
                token.UserId,
                token.DeviceId);
            await sessions.RevokeDeviceAsync(token.UserId, token.DeviceId, RevocationReasons.ReuseDetected, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            throw new AuthenticationFailedException(InvalidSession);
        }

        if (!token.IsActive(now))
        {
            throw new AuthenticationFailedException("La sesión expiró. Inicia sesión con tu contraseña.");
        }

        if (!token.BelongsToDevice(request.DeviceId))
        {
            token.Revoke(now, RevocationReasons.ReuseDetected);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            throw new AuthenticationFailedException(InvalidSession);
        }

        var user = await users.GetByIdAsync(token.UserId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            token.Revoke(now, RevocationReasons.AccountSuspended);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            throw new ForbiddenException(SuspendedAccount);
        }

        var response = sessions.Rotate(user, token);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return response;
    }

    public async Task LogoutAsync(LogoutRequest request, CancellationToken cancellationToken)
    {
        await logoutValidator.ValidateAndThrowAsync(request, cancellationToken);

        var token = await refreshTokens.GetByHashAsync(tokenGenerator.Hash(request.RefreshToken.Trim()), cancellationToken);
        if (token is null)
        {
            return;
        }

        token.Revoke(timeProvider.GetUtcNow(), RevocationReasons.Logout);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
