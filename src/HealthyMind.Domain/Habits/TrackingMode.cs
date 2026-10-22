namespace HealthyMind.Domain.Habits;

/// <summary>Cómo se llena el avance diario de un hábito.</summary>
public enum TrackingMode
{
    /// <summary>El usuario registra el valor a mano.</summary>
    Manual = 1,

    /// <summary>El móvil lo llena con el contador de pasos del dispositivo (RF06).</summary>
    StepCounter = 2,
}
