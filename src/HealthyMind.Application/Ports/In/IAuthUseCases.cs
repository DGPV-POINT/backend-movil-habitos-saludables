using HealthyMind.Application.Features.Auth;

namespace HealthyMind.Application.Ports.In;

public interface IAuthUseCases
{
    /// <summary>Crea la cuenta y envía el código de confirmación al correo. La sesión se abre al confirmarlo.</summary>
    Task<RegisterResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);

    /// <summary>Confirma el correo con el código y abre la sesión en el dispositivo.</summary>
    Task<AuthResponse> VerifyEmailAsync(VerifyEmailRequest request, CancellationToken cancellationToken);

    /// <summary>No delata si el correo existe: siempre termina bien.</summary>
    Task ResendVerificationAsync(EmailRequest request, CancellationToken cancellationToken);

    /// <summary>Envía un código para poner contraseña nueva. No delata si el correo existe.</summary>
    Task ForgotPasswordAsync(EmailRequest request, CancellationToken cancellationToken);

    /// <summary>Cambia la contraseña con el código y cierra todas las sesiones.</summary>
    Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken);

    /// <summary>Único momento en que se pide contraseña; después basta la huella + refresh.</summary>
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken);

    /// <summary>Lo que hace el móvil tras validar la huella: cambia el refresh token por uno nuevo.</summary>
    Task<AuthResponse> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken);

    Task LogoutAsync(LogoutRequest request, CancellationToken cancellationToken);
}

public interface ISessionUseCases
{
    Task<IReadOnlyList<SessionResponse>> ListAsync(Guid userId, CancellationToken cancellationToken);

    Task RevokeAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken);

    Task RevokeAllAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Se consulta en cada petición: una cuenta suspendida deja de operar al instante.</summary>
    Task<bool> IsUserActiveAsync(Guid userId, CancellationToken cancellationToken);
}
