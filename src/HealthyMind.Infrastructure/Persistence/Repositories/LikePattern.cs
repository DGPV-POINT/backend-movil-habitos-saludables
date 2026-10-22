namespace HealthyMind.Infrastructure.Persistence.Repositories;

/// <summary>Patrones para ILIKE de PostgreSQL con los comodines del usuario escapados.</summary>
internal static class LikePattern
{
    public static string Contains(string value) => $"%{Escape(value)}%";

    public static string Exact(string value) => Escape(value);

    private static string Escape(string value) =>
        value.Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("%", @"\%", StringComparison.Ordinal)
            .Replace("_", @"\_", StringComparison.Ordinal);
}
