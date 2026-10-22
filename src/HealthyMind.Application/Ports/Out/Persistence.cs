using HealthyMind.Application.Common;
using HealthyMind.Domain.Habits;
using HealthyMind.Domain.Recommendations;
using HealthyMind.Domain.Users;

namespace HealthyMind.Application.Ports.Out;

// Puertos de salida hacia la base de datos. Las implementaciones viven en Infrastructure.
// Salvo que se diga lo contrario, las consultas excluyen lo eliminado lógicamente.

public interface IUnitOfWork
{
    /// <exception cref="ConflictException">Se violó una restricción de unicidad.</exception>
    Task SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>Descarta lo pendiente para seguir trabajando después de un fallo al grabar.</summary>
    void DiscardChanges();
}

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Busca por usuario o por correo, sin distinguir mayúsculas.</summary>
    Task<User?> GetByLoginAsync(string usernameOrEmail, CancellationToken cancellationToken);

    Task<bool> UsernameExistsAsync(string normalizedUsername, CancellationToken cancellationToken);

    Task<bool> EmailExistsAsync(string email, Guid? excludingUserId, CancellationToken cancellationToken);

    Task<bool> AnyAdminAsync(CancellationToken cancellationToken);

    Task<bool> IsActiveAsync(Guid id, CancellationToken cancellationToken);

    Task<PagedResult<User>> SearchAsync(
        string? search,
        UserStatus? status,
        PageRequest page,
        CancellationToken cancellationToken);

    void Add(User user);
}

public interface IRefreshTokenRepository
{
    /// <summary>Incluye tokens revocados: se necesitan para detectar reutilización.</summary>
    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken);

    Task<IReadOnlyList<RefreshToken>> ListActiveByUserAsync(
        Guid userId,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<RefreshToken>> ListActiveByDeviceAsync(
        Guid userId,
        string deviceId,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    void Add(RefreshToken token);
}

public sealed record HabitFilter(HabitCategory? Category = null, string? Search = null, bool? IsActive = null);

public interface IHabitRepository
{
    /// <summary>Incluye eliminados: la sincronización necesita verlos.</summary>
    Task<Habit?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Habit>> ListByUserAsync(Guid userId, HabitFilter filter, CancellationToken cancellationToken);

    Task<bool> NameExistsAsync(Guid userId, string name, Guid? excludingHabitId, CancellationToken cancellationToken);

    /// <summary>Sin <paramref name="since"/>: todo lo vigente. Con él: lo cambiado después, eliminados incluidos.</summary>
    Task<IReadOnlyList<Habit>> ListChangedSinceAsync(
        Guid userId,
        DateTimeOffset? since,
        CancellationToken cancellationToken);

    void Add(Habit habit);
}

public interface IHabitRecordRepository
{
    /// <summary>Incluye eliminados: la sincronización necesita verlos.</summary>
    Task<HabitRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<HabitRecord?> GetByHabitAndDateAsync(Guid habitId, DateOnly date, CancellationToken cancellationToken);

    /// <summary>Más recientes primero.</summary>
    Task<IReadOnlyList<HabitRecord>> ListByHabitAsync(
        Guid habitId,
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<HabitRecord>> ListByUserAsync(
        Guid userId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken);

    /// <summary>Días distintos en los que el usuario cumplió al menos un hábito.</summary>
    Task<IReadOnlyList<DateOnly>> ListCompletedDatesAsync(Guid userId, CancellationToken cancellationToken);

    /// <inheritdoc cref="IHabitRepository.ListChangedSinceAsync"/>
    Task<IReadOnlyList<HabitRecord>> ListChangedSinceAsync(
        Guid userId,
        DateTimeOffset? since,
        CancellationToken cancellationToken);

    void Add(HabitRecord record);
}

public interface IRecommendationRepository
{
    Task<AiRecommendation?> GetLatestAsync(Guid userId, CancellationToken cancellationToken);

    Task<AiRecommendation?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<PagedResult<AiRecommendation>> ListByUserAsync(Guid userId, PageRequest page, CancellationToken cancellationToken);

    /// <summary>Más recientes primero.</summary>
    Task<IReadOnlyList<AiRecommendation>> ListCreatedSinceAsync(
        Guid userId,
        DateTimeOffset? since,
        int limit,
        CancellationToken cancellationToken);

    /// <summary>
    /// Usuarios activos con hábitos cuya última recomendación es anterior a <paramref name="generatedBefore"/>
    /// (o que no tienen ninguna) y que registraron actividad desde entonces.
    /// </summary>
    Task<IReadOnlyList<Guid>> ListUsersDueAsync(
        DateTimeOffset generatedBefore,
        int limit,
        CancellationToken cancellationToken);

    void Add(AiRecommendation recommendation);
}
