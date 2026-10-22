using HealthyMind.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace HealthyMind.Infrastructure.Persistence;

/// <summary>
/// Sella con la hora del servidor todo lo sincronizable que se graba, sea por la API o por el push del móvil.
/// Es el cursor del pull incremental.
/// </summary>
internal sealed class SyncTimestampInterceptor(TimeProvider timeProvider) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Stamp(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Stamp(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Stamp(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        foreach (var entry in context.ChangeTracker.Entries<SyncableEntity>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                entry.Entity.MarkPersisted(now);
            }
        }
    }
}
