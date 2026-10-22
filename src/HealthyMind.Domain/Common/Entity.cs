namespace HealthyMind.Domain.Common;

public abstract class Entity
{
    protected Entity()
    {
    }

    protected Entity(Guid id)
    {
        Guard.NotEmpty(id, "identificador");
        Id = id;
    }

    public Guid Id { get; private init; }
}
