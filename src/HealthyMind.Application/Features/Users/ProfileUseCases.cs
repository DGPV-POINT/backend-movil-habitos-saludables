using FluentValidation;
using HealthyMind.Application.Common;
using HealthyMind.Application.Features.Auth;
using HealthyMind.Application.Ports.In;
using HealthyMind.Application.Ports.Out;
using HealthyMind.Domain.Users;

namespace HealthyMind.Application.Features.Users;

internal sealed class ProfileUseCases(
    IUserRepository users,
    IPasswordHasher passwordHasher,
    SessionIssuer sessions,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    IValidator<UpdateProfileRequest> updateValidator,
    IValidator<ChangePasswordRequest> passwordValidator) : IProfileUseCases
{
    public async Task<UserProfileResponse> GetAsync(Guid userId, CancellationToken cancellationToken) =>
        UserProfileResponse.From(await GetUserAsync(userId, cancellationToken));

    public async Task<UserProfileResponse> UpdateAsync(
        Guid userId,
        UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        await updateValidator.ValidateAndThrowAsync(request, cancellationToken);
        var user = await GetUserAsync(userId, cancellationToken);

        if (await users.EmailExistsAsync(User.NormalizeEmail(request.Email), user.Id, cancellationToken))
        {
            throw new ConflictException("El correo ya está registrado.");
        }

        user.UpdateProfile(request.FirstName, request.LastName, request.Email, request.TimeZoneId, timeProvider.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return UserProfileResponse.From(user);
    }

    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        await passwordValidator.ValidateAndThrowAsync(request, cancellationToken);
        var user = await GetUserAsync(userId, cancellationToken);

        if (!passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
        {
            throw new ForbiddenException("La contraseña actual no es correcta.");
        }

        user.ChangePasswordHash(passwordHasher.Hash(request.NewPassword), timeProvider.GetUtcNow());
        await sessions.RevokeAllAsync(user.Id, RevocationReasons.PasswordChanged, cancellationToken, request.DeviceId);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<User> GetUserAsync(Guid userId, CancellationToken cancellationToken) =>
        await users.GetByIdAsync(userId, cancellationToken) ?? throw new NotFoundException("Usuario no encontrado.");
}
