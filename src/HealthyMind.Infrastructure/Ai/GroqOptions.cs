using System.ComponentModel.DataAnnotations;

namespace HealthyMind.Infrastructure.Ai;

public sealed class GroqOptions
{
    public const string SectionName = "Groq";

    [Required]
    [Url]
    public string BaseUrl { get; set; } = "https://api.groq.com/openai/v1/";

    /// <summary>Nunca en el repositorio: user-secrets o la variable de entorno <c>Groq__ApiKey</c>.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Groq retiró Llama 3.3 (octubre 2026). Disponibles: openai/gpt-oss-120b, openai/gpt-oss-20b, qwen/qwen3.8-27b.
    /// </summary>
    [Required]
    public string Model { get; set; } = "openai/gpt-oss-120b";

    [Range(0, 2)]
    public double Temperature { get; set; } = 0.4;

    /// <summary>En modelos que razonan (gpt-oss) incluye el razonamiento: con poco margen el JSON llega cortado.</summary>
    [Range(100, 8000)]
    public int MaxCompletionTokens { get; set; } = 2000;

    /// <summary>low | medium | high, solo para modelos que razonan (gpt-oss). Vacío: no se envía.</summary>
    [RegularExpression("^(low|medium|high)?$", ErrorMessage = "Groq:ReasoningEffort debe ser low, medium o high.")]
    public string? ReasoningEffort { get; set; } = "low";

    [Range(5, 120)]
    public int TimeoutSeconds { get; set; } = 30;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}
