namespace HealthyMind.Domain.Common;

/// <summary>
/// Entidad que el móvil puede crear o modificar sin conexión y sincronizar después.
/// </summary>
/// <remarks>
/// Maneja dos relojes a propósito:
/// <list type="bullet">
/// <item><see cref="UpdatedAt"/>: cuándo ocurrió el cambio (puede venir del dispositivo, horas después).
/// Decide quién gana en un conflicto (última escritura gana).</item>
/// <item><see cref="ServerModifiedAt"/>: cuándo lo grabó el servidor. Es el cursor de la descarga
/// incremental; con solo <see cref="UpdatedAt"/> un cambio que llega tarde quedaría detrás del cursor
/// de otro dispositivo y nunca lo vería.</item>
/// </list>
/// Las bajas son lógicas (<see cref="DeletedAt"/>) para que los demás dispositivos se enteren.
/// </remarks>
public abstract class SyncableEntity : Entity
{
    protected SyncableEntity()
    {
    }

    protected SyncableEntity(Guid id, DateTimeOffset createdAt) : base(id)
    {
        CreatedAt = Guard.Utc(createdAt);
        UpdatedAt = CreatedAt;
    }

    public DateTimeOffset CreatedAt { get; private init; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public DateTimeOffset ServerModifiedAt { get; private set; }

    public bool IsDeleted => DeletedAt is not null;

    /// <summary>Un cambio con esta fecha pisa al actual solo si es más reciente.</summary>
    public bool IsOlderThan(DateTimeOffset incomingUpdatedAt) => Guard.Utc(incomingUpdatedAt) > UpdatedAt;

    /// <summary>Lo llama la persistencia al grabar; el dominio nunca lo decide.</summary>
    public void MarkPersisted(DateTimeOffset serverTime) => ServerModifiedAt = Guard.Utc(serverTime);

    protected void Touch(DateTimeOffset at)
    {
        EnsureNotDeleted();
        UpdatedAt = Guard.Utc(at);
    }

    protected void MarkDeleted(DateTimeOffset at)
    {
        if (IsDeleted)
        {
            return;
        }

        UpdatedAt = Guard.Utc(at);
        DeletedAt = UpdatedAt;
    }

    protected abstract string DeletedMessage { get; }

    public void EnsureNotDeleted()
    {
        if (IsDeleted)
        {
            throw new DomainException(DeletedMessage);
        }
    }
}
