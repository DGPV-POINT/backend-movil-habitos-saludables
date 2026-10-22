using FluentValidation;
using HealthyMind.Application.Common;
using HealthyMind.Application.Features.Auth;
using HealthyMind.Application.Ports.In;
using HealthyMind.Application.Ports.Out;
using HealthyMind.Domain.Users;
using Microsoft.Extensions.Logging;

namespace HealthyMind.Application.Features.Users;

internal sealed class UserAdministrationUseCases(
    IUserRepository users,
    UserRegistration registration,
    SessionIssuer sessions,
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

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return UserProfileResponse.From(user);
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
