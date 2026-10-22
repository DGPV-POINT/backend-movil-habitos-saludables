namespace HealthyMind.Domain.Common;

/// <summary>
/// Una regla de negocio fue violada. El mensaje se muestra tal cual al usuario.
/// </summary>
public sealed class DomainException : Exception
{
    public DomainException(string message) : base(message)
    {
    }

    public DomainException()
    {
    }

    public DomainException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
