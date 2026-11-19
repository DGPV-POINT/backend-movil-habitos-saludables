using FluentValidation;
using HealthyMind.Application.Common;
using HealthyMind.Application.Features.HabitRecords;
using HealthyMind.Application.Features.Habits;
using HealthyMind.Application.Features.Recommendations;
using HealthyMind.Application.Ports.In;
using HealthyMind.Application.Ports.Out;
using HealthyMind.Domain.Common;
using HealthyMind.Domain.Habits;
using HealthyMind.Domain.Users;

namespace HealthyMind.Application.Features.Sync;

/// <summary>
/// Resuelve conflictos con "la última escritura gana" según la fecha del cambio en el dispositivo.
/// Una baja es definitiva: un cambio que llega para algo ya eliminado se descarta.
/// </summary>
internal sealed class SyncUseCases(
    IHabitRepository habits,
    IHabitRecordRepository records,
    IRecommendationRepository recommendations,
    OwnedResources owned,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    IValidator<SyncPushRequest> validator) : ISyncUseCases
{
    /// <summary>
    /// El cursor que se devuelve retrocede este margen: lo que otra transacción grabó con hora anterior
    /// pero confirmó después no se pierde. A cambio, el móvil puede recibir repetido algo que ya tenía.
    /// </summary>
    public static readonly TimeSpan PullOverlap = TimeSpan.FromSeconds(30);

    private const int PullRecommendationLimit = 20;
    private const string HabitNotFound = "El hábito no existe.";
    private const string RecordNotFound = "El registro no existe.";

    public async Task<SyncPushResponse> PushAsync(Guid userId, SyncPushRequest request, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        var user = await owned.UserAsync(userId, cancellationToken);

        var habitResults = new List<SyncItemResult>();
        foreach (var change in request.Habits ?? [])
        {
            habitResults.Add(await ApplySafelyAsync(change.Id, () => ApplyHabitAsync(userId, change, cancellationToken), cancellationToken));
        }

        var recordResults = new List<SyncItemResult>();
        foreach (var change in request.Records ?? [])
        {
            recordResults.Add(await ApplySafelyAsync(change.Id, () => ApplyRecordAsync(user, change, cancellationToken), cancellationToken));
        }

        return new SyncPushResponse(timeProvider.GetUtcNow(), habitResults, recordResults);
    }

    public async Task<SyncPullResponse> PullAsync(Guid userId, DateTimeOffset? since, CancellationToken cancellationToken)
    {
        var cursor = timeProvider.GetUtcNow() - PullOverlap;
        var sinceUtc = since?.ToUniversalTime();

        var user = await owned.UserAsync(userId, cancellationToken);
        var changedHabits = await habits.ListChangedSinceAsync(userId, sinceUtc, cancellationToken);
        var changedRecords = await records.ListChangedSinceAsync(userId, sinceUtc, cancellationToken);
        var newRecommendations = await recommendations.ListCreatedSinceAsync(userId, sinceUtc, PullRecommendationLimit, cancellationToken);

        return new SyncPullResponse(
            cursor,
            since is null,
            changedHabits.Select(habit => new HabitSyncItem(HabitResponse.From(habit), habit.IsDeleted)).ToList(),
            changedRecords.Select(record => new HabitRecordSyncItem(HabitRecordResponse.From(record), record.IsDeleted)).ToList(),
            newRecommendations.Select(RecommendationResponse.From).ToList(),
            user.RecommendationsSeenAt);
    }

    /// <summary>Cada elemento se graba por separado: si uno falla, se descarta solo ese.</summary>
    private async Task<SyncItemResult> ApplySafelyAsync(
        Guid id,
        Func<Task<SyncItemResult>> apply,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await apply();
            if (result.Status == SyncItemStatus.Applied)
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }

            return result;
        }
        catch (Exception exception) when (exception is DomainException or ConflictException)
        {
            unitOfWork.DiscardChanges();
            return Rejected(id, exception.Message);
        }
    }

    private async Task<SyncItemResult> ApplyHabitAsync(Guid userId, HabitChange change, CancellationToken cancellationToken)
    {
        var at = ClampToNow(change.UpdatedAt);
        var habit = await habits.GetByIdAsync(change.Id, cancellationToken);

        if (habit is null)
        {
            if (change.IsDeleted)
            {
                return Applied(change.Id); // Se creó y se borró sin conexión: no hay nada que hacer.
            }

            await EnsureUniqueNameAsync(userId, change.Name, null, cancellationToken);
            habit = Habit.Create(change.Id, userId, change.ToDefinition(), at);
            if (!change.IsActive)
            {
                habit.SetActive(false, at);
            }

            habits.Add(habit);
            return Applied(change.Id);
        }

        if (!habit.IsOwnedBy(userId))
        {
            return Rejected(change.Id, HabitNotFound);
        }

        if (habit.IsDeleted)
        {
            return change.IsDeleted
                ? Applied(change.Id)
                : Stale(change.Id, "El hábito fue eliminado en otro dispositivo.");
        }

        if (!habit.IsOlderThan(at))
        {
            return Stale(change.Id, "Hay una versión más reciente en el servidor.");
        }

        if (change.IsDeleted)
        {
            await owned.DeleteHabitAsync(habit, at, cancellationToken);
            return Applied(change.Id);
        }

        await EnsureUniqueNameAsync(userId, change.Name, habit.Id, cancellationToken);
        habit.Update(change.ToDefinition(), at);
        habit.SetActive(change.IsActive, at);
        return Applied(change.Id);
    }

    private async Task<SyncItemResult> ApplyRecordAsync(User user, HabitRecordChange change, CancellationToken cancellationToken)
    {
        var at = ClampToNow(change.UpdatedAt);
        var record = await records.GetByIdAsync(change.Id, cancellationToken);

        if (record is not null && !record.IsOwnedBy(user.Id))
        {
            return Rejected(change.Id, RecordNotFound);
        }

        var habit = await habits.GetByIdAsync(change.HabitId, cancellationToken);
        if (habit is null || !habit.IsOwnedBy(user.Id))
        {
            return Rejected(change.Id, HabitNotFound);
        }

        if (record is null)
        {
            return await ApplyNewRecordAsync(user, habit, change, at, cancellationToken);
        }

        if (record.HabitId != change.HabitId || record.RecordDate != change.RecordDate)
        {
            return Rejected(change.Id, "Un registro no puede cambiar de hábito ni de fecha; elimínalo y crea otro.");
        }

        return ApplyToExistingRecord(record, habit, change, at, change.Id);
    }

    private async Task<SyncItemResult> ApplyNewRecordAsync(
        User user,
        Habit habit,
        HabitRecordChange change,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        if (change.IsDeleted)
        {
            return Applied(change.Id);
        }

        // Otro dispositivo ya registró ese día con otro id: se fusiona sobre el existente.
        var sameDay = await records.GetByHabitAndDateAsync(habit.Id, change.RecordDate, cancellationToken);
        if (sameDay is not null)
        {
            return ApplyToExistingRecord(sameDay, habit, change, at, change.Id);
        }

        var record = HabitRecord.Create(change.Id, habit, change.RecordDate, change.ToProgress(), user.LocalDate(timeProvider.GetUtcNow()), at);
        records.Add(record);
        return Applied(change.Id);
    }

    private static SyncItemResult ApplyToExistingRecord(
        HabitRecord record,
        Habit habit,
        HabitRecordChange change,
        DateTimeOffset at,
        Guid clientId)
    {
        var serverId = record.Id == clientId ? (Guid?)null : record.Id;

        if (record.IsDeleted)
        {
            return change.IsDeleted
                ? Applied(clientId, serverId)
                : Stale(clientId, "El registro fue eliminado en otro dispositivo.", serverId);
        }

        if (!record.IsOlderThan(at))
        {
            return Stale(clientId, "Hay una versión más reciente en el servidor.", serverId);
        }

        if (change.IsDeleted)
        {
            record.Delete(at);
        }
        else
        {
            record.Update(habit, change.ToProgress(), at);
        }

        return Applied(clientId, serverId);
    }

    private async Task EnsureUniqueNameAsync(Guid userId, string name, Guid? excludingHabitId, CancellationToken cancellationToken)
    {
        if (await habits.NameExistsAsync(userId, name.Trim(), excludingHabitId, cancellationToken))
        {
            throw new ConflictException("Ya existe un hábito con ese nombre.");
        }
    }

    /// <summary>Un reloj de teléfono adelantado no puede ganarle para siempre a los demás cambios.</summary>
    private DateTimeOffset ClampToNow(DateTimeOffset updatedAt)
    {
        var now = timeProvider.GetUtcNow();
        var utc = updatedAt.ToUniversalTime();
        return utc > now ? now : utc;
    }

    private static SyncItemResult Applied(Guid id, Guid? serverId = null) => new(id, SyncItemStatus.Applied, serverId, null);

    private static SyncItemResult Stale(Guid id, string message, Guid? serverId = null) =>
        new(id, SyncItemStatus.Stale, serverId, message);

    private static SyncItemResult Rejected(Guid id, string message) => new(id, SyncItemStatus.Rejected, null, message);
}
