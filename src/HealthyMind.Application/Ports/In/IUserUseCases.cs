using HealthyMind.Application.Common;
using HealthyMind.Application.Features.Users;

namespace HealthyMind.Application.Ports.In;

public interface IProfileUseCases
{
    Task<UserProfileResponse> GetAsync(Guid userId, CancellationToken cancellationToken);

    Task<UserProfileResponse> UpdateAsync(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken);

    /// <summary>Cierra las sesiones de los demás dispositivos.</summary>
    Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken);
}

/// <summary>Gestión de cuentas por parte del administrador (RF01).</summary>
public interface IUserAdministrationUseCases
{
    Task<PagedResult<UserProfileResponse>> SearchAsync(UserSearchRequest request, CancellationToken cancellationToken);

    Task<UserProfileResponse> GetAsync(Guid userId, CancellationToken cancellationToken);

    Task<UserProfileResponse> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken);

    /// <summary>Suspende la cuenta y cierra todas sus sesiones.</summary>
    Task<UserProfileResponse> SuspendAsync(Guid adminId, Guid userId, CancellationToken cancellationToken);

    Task<UserProfileResponse> ReactivateAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Crea el primer administrador si todavía no hay ninguno. Devuelve si lo creó.</summary>
    Task<bool> EnsureAdminExistsAsync(BootstrapAdminRequest request, CancellationToken cancellationToken);
}
