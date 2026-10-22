using HealthyMind.Application.Features.Auth;

namespace HealthyMind.Application.Ports.In;

public interface IAuthUseCases
{
    /// <summary>Crea la cuenta (rol usuario) y deja la sesión abierta en el dispositivo.</summary>
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);

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
