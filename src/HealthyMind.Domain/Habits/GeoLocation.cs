using HealthyMind.Domain.Common;

namespace HealthyMind.Domain.Habits;

/// <summary>Ubicación opcional donde se realizó la actividad (RF03).</summary>
public sealed record GeoLocation
{
    private GeoLocation(double latitude, double longitude)
    {
        Latitude = latitude;
        Longitude = longitude;
    }

    public double Latitude { get; }

    public double Longitude { get; }

    /// <summary>Ambas coordenadas o ninguna.</summary>
    public static GeoLocation? From(double? latitude, double? longitude)
    {
        if (latitude is null && longitude is null)
        {
            return null;
        }

        if (latitude is null || longitude is null)
        {
            throw new DomainException("La ubicación necesita latitud y longitud.");
        }

        if (latitude is < -90 or > 90 || longitude is < -180 or > 180)
        {
            throw new DomainException("La ubicación no es válida.");
        }

        return new GeoLocation(latitude.Value, longitude.Value);
    }
}
