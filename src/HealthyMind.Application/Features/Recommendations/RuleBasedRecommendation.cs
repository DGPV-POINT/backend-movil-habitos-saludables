using System.Globalization;
using System.Text;
using HealthyMind.Application.Ports.Out;
using HealthyMind.Domain.Recommendations;

namespace HealthyMind.Application.Features.Recommendations;

/// <summary>
/// Plan B cuando la IA no responde (clave vencida, límite agotado, Groq caído): arma una recomendación
/// con reglas fijas sobre las mismas cifras. Así el usuario nunca se queda sin su análisis.
/// </summary>
public static class RuleBasedRecommendation
{
    /// <summary>Nombre que se guarda como "modelo": la app y los reportes saben que no vino de la IA.</summary>
    public const string ModelName = "reglas-healthymind";

    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-EC");

    public static RecommendationContent Build(HabitActivitySnapshot snapshot)
    {
        var active = snapshot.Habits.Where(habit => habit.IsActive && habit.Expected > 0).ToList();
        var weakest = active.OrderBy(habit => habit.Percentage).FirstOrDefault();
        var strongest = active.OrderByDescending(habit => habit.Percentage).FirstOrDefault();
        var weakDay = snapshot.Weekdays.Where(day => day.Expected > 0).OrderBy(day => day.Percentage).FirstOrDefault();

        var title = snapshot.OverallPercentage switch
        {
            >= 80 => "¡Vas muy bien, mantén el ritmo!",
            >= 50 => "Buen avance: un pequeño ajuste y lo logras",
            _ => "Cada día cuenta: empecemos por lo simple",
        };

        var summary = new StringBuilder();
        summary.Append(string.Create(Spanish, $"Cumpliste el {snapshot.OverallPercentage:0.#}% de tus hábitos en los últimos días"));
        summary.Append(snapshot.CurrentStreak > 0
            ? string.Create(Spanish, $" y llevas una racha de {snapshot.CurrentStreak} día(s).")
            : ".");
        if (strongest is not null && weakest is not null && strongest != weakest)
        {
            summary.Append(string.Create(
                Spanish,
                $" Tu punto fuerte es {strongest.Name} ({strongest.Percentage:0.#}%) y el que más cuesta es {weakest.Name} ({weakest.Percentage:0.#}%)."));
        }

        var actions = new List<string>();
        if (weakest is not null && weakest.Percentage < 80)
        {
            actions.Add($"• Haz {weakest.Name} a la misma hora cada día y activa su recordatorio en la app.");
        }

        if (weakDay is not null && weakDay.Percentage < 70)
        {
            actions.Add($"• Los {Spanish.DateTimeFormat.GetDayName(weakDay.Day)} te cuestan más: prepara ese día con anticipación.");
        }

        actions.Add(snapshot.CurrentStreak > 0
            ? "• No rompas la racha: aunque sea, registra una parte de cada hábito."
            : "• Empieza una racha hoy: elige el hábito más fácil y cúmplelo.");
        actions.Add("• Revisa tus estadísticas al final de la semana para ver tu progreso.");

        return new RecommendationContent(
            title,
            summary.ToString(),
            weakest is not null && weakest.Percentage < 80 ? weakest.Name : null,
            string.Join('\n', actions.Take(3)),
            ModelName);
    }
}

/// <summary>Horario en que no se generan recomendaciones automáticas para que el aviso no llegue de madrugada.</summary>
public static class QuietHours
{
    /// <summary>true si <paramref name="hour"/> cae entre <paramref name="start"/> y <paramref name="end"/> (puede cruzar la medianoche).</summary>
    public static bool Contains(int hour, int start, int end) =>
        start == end ? false : start < end ? hour >= start && hour < end : hour >= start || hour < end;
}
