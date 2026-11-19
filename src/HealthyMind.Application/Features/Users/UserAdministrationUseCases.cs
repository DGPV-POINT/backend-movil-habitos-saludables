using FluentValidation;
using HealthyMind.Application.Common;
using HealthyMind.Application.Features.Auth;
using HealthyMind.Application.Ports.In;
using HealthyMind.Application.Ports.Out;
using HealthyMind.Domain.Users;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HealthyMind.Application.Features.Users;

internal sealed class UserAdministrationUseCases(
    IUserRepository users,
    UserRegistration registration,
    SessionIssuer sessions,
    IAdminDashboardReader dashboard,
    IOptions<AdminOptions> adminOptions,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    IValidator<CreateUserRequest> createValidator,
    ILogger<UserAdministrationUseCases> logger) : IUserAdministrationUseCases
{
    public async Task<PagedResult<UserProfileResponse>> SearchAsync(
        UserSearchRequest request,
        CancellationToken cancellationToken)
    {
        var result = await users.SearchAsync(
            request.Search,
            request.Status,
            new PageRequest(request.Page, request.PageSize),
            cancellationToken);

        return result.Map(UserProfileResponse.From);
    }

    public async Task<UserProfileResponse> GetAsync(Guid userId, CancellationToken cancellationToken) =>
        UserProfileResponse.From(await GetUserAsync(userId, cancellationToken));

    public async Task<UserProfileResponse> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken)
    {
        await createValidator.ValidateAndThrowAsync(request, cancellationToken);

        var user = await registration.RegisterAsync(
            new NewUser(
                request.Username,
                request.Email,
                request.FirstName,
                request.LastName,
                request.Password,
                request.Role,
                request.TimeZoneId),
            cancellationToken);

        // La crea el administrador: el correo se da por confirmado.
        user.MarkEmailVerified(timeProvider.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return UserProfileResponse.From(user);
    }

    public async Task<UserProfileResponse> ChangeRoleAsync(
        Guid adminId,
        Guid userId,
        ChangeRoleRequest request,
        CancellationToken cancellationToken)
    {
        var user = await GetUserAsync(userId, cancellationToken);
        if (user.Role == request.Role)
        {
            return UserProfileResponse.From(user);
        }

        if (request.Role != UserRole.Admin)
        {
            if (adminId == userId)
            {
                throw new ForbiddenException("No puedes quitarte el rol de administrador a ti mismo.");
            }

            if (adminOptions.Value.IsAdminEmail(user.Email))
            {
                throw new ForbiddenException("Este correo es administrador por configuración del servidor.");
            }

            if (user.IsActive && await users.CountActiveAdminsAsync(cancellationToken) <= 1)
            {
                throw new ForbiddenException("Debe quedar al menos un administrador activo.");
            }
        }

        user.ChangeRole(request.Role, timeProvider.GetUtcNow());

        // El rol va dentro del access token: se cierran sus sesiones para que entre con el permiso nuevo.
        await sessions.RevokeAllAsync(user.Id, RevocationReasons.RoleChanged, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        logger.LogInformation("El administrador {AdminId} cambió el rol de {UserId} a {Role}.", adminId, userId, request.Role);
        return UserProfileResponse.From(user);
    }

    public async Task<AdminDashboardResponse> GetDashboardAsync(Guid adminId, CancellationToken cancellationToken)
    {
        var admin = await GetUserAsync(adminId, cancellationToken);
        return AdminDashboardResponse.From(
            await dashboard.ReadAsync(timeProvider.GetUtcNow(), admin.GetTimeZone(), cancellationToken));
    }

    public async Task<int> PromoteConfiguredAdminsAsync(CancellationToken cancellationToken)
    {
        var emails = adminOptions.Value.Emails.Where(email => !string.IsNullOrWhiteSpace(email)).ToList();
        if (emails.Count == 0)
        {
            return 0;
        }

        var now = timeProvider.GetUtcNow();
        var promoted = 0;
        foreach (var user in await users.ListByEmailsAsync(emails, cancellationToken))
        {
            if (user.Role != UserRole.Admin)
            {
                user.ChangeRole(UserRole.Admin, now);
                promoted++;
                logger.LogInformation("Se promovió a administrador la cuenta {UserId} (configuración Admin:Emails).", user.Id);
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return promoted;
    }

    public async Task<UserProfileResponse> SuspendAsync(Guid adminId, Guid userId, CancellationToken cancellationToken)
    {
        if (adminId == userId)
        {
            throw new ForbiddenException("No puedes suspender tu propia cuenta.");
        }

        var user = await GetUserAsync(userId, cancellationToken);
        user.Suspend(timeProvider.GetUtcNow());
        await sessions.RevokeAllAsync(user.Id, RevocationReasons.AccountSuspended, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return UserProfileResponse.From(user);
    }

    public async Task<UserProfileResponse> ReactivateAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await GetUserAsync(userId, cancellationToken);
        user.Reactivate(timeProvider.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return UserProfileResponse.From(user);
    }

    public async Task<bool> EnsureAdminExistsAsync(BootstrapAdminRequest request, CancellationToken cancellationToken)
    {
        if (await users.AnyAdminAsync(cancellationToken))
        {
            return false;
        }

        await CreateAsync(
            new CreateUserRequest(request.Username, request.Email, "Administrador", "HealthyMind", request.Password, UserRole.Admin, null),
            cancellationToken);

        logger.LogInformation("Se creó el administrador inicial {Username}.", request.Username);
        return true;
    }

    private async Task<User> GetUserAsync(Guid userId, CancellationToken cancellationToken) =>
        await users.GetByIdAsync(userId, cancellationToken) ?? throw new NotFoundException("Usuario no encontrado.");
}
