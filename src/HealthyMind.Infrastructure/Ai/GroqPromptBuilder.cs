using System.Globalization;
using System.Text;
using HealthyMind.Application.Ports.Out;

namespace HealthyMind.Infrastructure.Ai;

/// <summary>Convierte el resumen de actividad en el prompt para el modelo de Groq.</summary>
internal static class GroqPromptBuilder
{
    public const string SystemPrompt = """
        Eres un coach de hábitos saludables dentro de la app móvil HealthyMind.
        Respondes siempre en español neutro, con tono cercano, breve y práctico.
        Solo usas los datos que te entregan: no inventes hábitos, cifras ni información personal.
        No das diagnósticos ni indicaciones médicas; si algo lo amerita, sugiere consultar a un profesional.
        Tu respuesta es exclusivamente un objeto JSON válido, sin texto antes ni después.
        """;

    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-EC");

    public static string BuildUserPrompt(HabitActivitySnapshot snapshot)
    {
        var prompt = new StringBuilder();
        prompt.AppendLine(Spanish, $"Periodo analizado: del {snapshot.From:yyyy-MM-dd} al {snapshot.To:yyyy-MM-dd}.");
        prompt.AppendLine(Spanish, $"Cumplimiento general: {snapshot.OverallPercentage:0.##}%.");
        prompt.AppendLine(Spanish, $"Racha actual: {snapshot.CurrentStreak} día(s). Mejor racha: {snapshot.BestStreak} día(s).");
        prompt.AppendLine();
        prompt.AppendLine("Hábitos (nombre | categoría | meta diaria | estado | días esperados | días cumplidos | % | valor promedio registrado):");

        foreach (var habit in snapshot.Habits)
        {
            var average = habit.AverageValue is null
                ? "sin registros"
                : string.Create(Spanish, $"{habit.AverageValue:0.##} {habit.Unit}");

            prompt.AppendLine(
                Spanish,
                $"- {habit.Name} | {habit.Category} | {habit.Goal} {habit.Unit} | {(habit.IsActive ? "activo" : "pausado")} | {habit.Expected} | {habit.Completed} | {habit.Percentage:0.##}% | {average}");
        }

        prompt.AppendLine();
        prompt.AppendLine("Cumplimiento por día de la semana:");
        foreach (var day in snapshot.Weekdays)
        {
            prompt.AppendLine(Spanish, $"- {Spanish.DateTimeFormat.GetDayName(day.Day)}: {day.Percentage:0.##}% ({day.Completed}/{day.Expected})");
        }

        prompt.AppendLine();
        prompt.AppendLine("""
            Devuelve un JSON con exactamente estas claves:
            {
              "titulo": "frase corta y motivadora (máx. 80 caracteres)",
              "hallazgo": "el hallazgo principal del periodo en 2 o 3 oraciones, citando cifras reales",
              "habitoAtencion": "nombre exacto del hábito que más necesita atención, o null si todos van bien",
              "recomendacion": "3 acciones concretas para los próximos 7 días, separadas por saltos de línea y empezando con '• '"
            }
            """);

        return prompt.ToString();
    }
}
