using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using HealthyMind.Application.Ports.Out;
using HealthyMind.Domain.Recommendations;
using Microsoft.Extensions.Options;

namespace HealthyMind.Infrastructure.Ai;

/// <summary>Adaptador de salida hacia Groq (API compatible con OpenAI) con el modelo de Groq:Model.</summary>
internal sealed class GroqRecommendationProvider(HttpClient httpClient, IOptions<GroqOptions> options)
    : IAiRecommendationProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<RecommendationContent> GenerateAsync(
        HabitActivitySnapshot snapshot,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (!settings.IsConfigured)
        {
            throw new AiProviderUnavailableException("La API key de Groq no está configurada (Groq:ApiKey).");
        }

        var request = new ChatCompletionRequest(
            settings.Model,
            [
                new ChatMessage("system", GroqPromptBuilder.SystemPrompt),
                new ChatMessage("user", GroqPromptBuilder.BuildUserPrompt(snapshot)),
            ],
            settings.Temperature,
            settings.MaxCompletionTokens,
            new ResponseFormat("json_object"),
            string.IsNullOrWhiteSpace(settings.ReasoningEffort) ? null : settings.ReasoningEffort);

        try
        {
            // Cuerpo con Content-Length (no chunked): es lo que mejor tolera cualquier proxy delante de la API.
            using var body = new StringContent(JsonSerializer.Serialize(request, JsonOptions), Encoding.UTF8, "application/json");
            using var response = await httpClient.PostAsync("chat/completions", body, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var reason = await ReadErrorMessageAsync(response, cancellationToken);
                throw new AiProviderUnavailableException($"Groq respondió con estado HTTP {(int)response.StatusCode}: {reason}");
            }

            var completion = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(JsonOptions, cancellationToken);
            var content = completion?.Choices is { Count: > 0 } choices ? choices[0].Message?.Content : null;
            return Parse(content, settings.Model);
        }
        catch (Exception exception)
            when ((exception is HttpRequestException or JsonException or TaskCanceledException)
                  && !cancellationToken.IsCancellationRequested)
        {
            throw new AiProviderUnavailableException("No fue posible obtener la recomendación de Groq.", exception);
        }
    }

    private static RecommendationContent Parse(string? content, string model)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new AiProviderUnavailableException("Groq no devolvió contenido.");
        }

        var parsed = JsonSerializer.Deserialize<RecommendationJson>(content, JsonOptions);
        if (parsed is null || string.IsNullOrWhiteSpace(parsed.Hallazgo) || string.IsNullOrWhiteSpace(parsed.Recomendacion))
        {
            throw new AiProviderUnavailableException("La respuesta de Groq no tiene el formato esperado.");
        }

        return new RecommendationContent(
            Fit(string.IsNullOrWhiteSpace(parsed.Titulo) ? "Tu análisis de hábitos" : parsed.Titulo, AiRecommendation.TitleMaxLength),
            Fit(parsed.Hallazgo, AiRecommendation.TextMaxLength),
            string.IsNullOrWhiteSpace(parsed.HabitoAtencion) ? null : Fit(parsed.HabitoAtencion, AiRecommendation.FocusHabitMaxLength),
            Fit(parsed.Recomendacion, AiRecommendation.TextMaxLength),
            model);
    }

    /// <summary>Groq explica el motivo en <c>error.message</c> (p. ej. modelo retirado); sin él, un 404 no dice nada.</summary>
    private static async Task<string> ReadErrorMessageAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var error = await response.Content.ReadFromJsonAsync<ErrorEnvelope>(JsonOptions, cancellationToken);
            return error?.Error?.Message ?? "sin detalle.";
        }
        catch (JsonException)
        {
            return "sin detalle.";
        }
    }

    private static string Fit(string value, int maxLength)
    {
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..(maxLength - 1)] + "…";
    }

    private sealed record ChatCompletionRequest(
        string Model,
        IReadOnlyList<ChatMessage> Messages,
        double Temperature,
        [property: JsonPropertyName("max_completion_tokens")] int MaxCompletionTokens,
        [property: JsonPropertyName("response_format")] ResponseFormat ResponseFormat,
        [property: JsonPropertyName("reasoning_effort"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        string? ReasoningEffort);

    private sealed record ChatMessage(string Role, string Content);

    private sealed record ResponseFormat(string Type);

    private sealed record ChatCompletionResponse(IReadOnlyList<ChatChoice>? Choices);

    private sealed record ChatChoice(ChatMessage? Message);

    private sealed record ErrorEnvelope(ErrorDetail? Error);

    private sealed record ErrorDetail(string? Message);

    private sealed record RecommendationJson(string? Titulo, string? Hallazgo, string? HabitoAtencion, string? Recomendacion);
}
