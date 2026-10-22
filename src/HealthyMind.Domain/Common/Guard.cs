namespace HealthyMind.Domain.Common;

internal static class Guard
{
    public static void NotEmpty(Guid value, string field)
    {
        if (value == Guid.Empty)
        {
            throw new DomainException($"El {field} es obligatorio.");
        }
    }

    public static string Text(string? value, string field, int maxLength)
    {
        var normalized = value?.Trim();

        if (string.IsNullOrEmpty(normalized))
        {
            throw new DomainException($"El campo {field} es obligatorio.");
        }

        if (normalized.Length > maxLength)
        {
            throw new DomainException($"El campo {field} no puede superar los {maxLength} caracteres.");
        }

        return normalized;
    }

    public static string? OptionalText(string? value, string field, int maxLength)
    {
        var normalized = value?.Trim();

        if (string.IsNullOrEmpty(normalized))
        {
            return null;
        }

        if (normalized.Length > maxLength)
        {
            throw new DomainException($"El campo {field} no puede superar los {maxLength} caracteres.");
        }

        return normalized;
    }

    public static DateTimeOffset Utc(DateTimeOffset value) => value.ToUniversalTime();
}
