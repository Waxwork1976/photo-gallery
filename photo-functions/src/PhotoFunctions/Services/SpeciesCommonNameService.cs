using System.Text;
using System.Text.Json;
using PhotoFunctions.Configuration;

namespace PhotoFunctions.Services;

public sealed class SpeciesCommonNameService : ISpeciesCommonNameService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly GeminiOptions _options;

    public SpeciesCommonNameService(HttpClient httpClient, Microsoft.Extensions.Options.IOptions<GeminiOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<Dictionary<string, string>> GetCommonNamesAsync(
        string speciesType,
        string scientificName,
        string? taxonomyOrder = null,
        string? taxonomyFamily = null,
        string? taxonomyGenus = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(scientificName))
            return EmptyMap();
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
            return EmptyMap();

        var endpoint =
            $"{_options.BaseUrl.TrimEnd('/')}/v1beta/models/{Uri.EscapeDataString(_options.Model)}:generateContent?key={Uri.EscapeDataString(_options.ApiKey)}";

        var prompt = BuildPrompt(speciesType, scientificName, taxonomyOrder, taxonomyFamily, taxonomyGenus);
        var body = JsonSerializer.Serialize(new
        {
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[] { new { text = prompt } }
                }
            }
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

        using var timeoutCts = _options.TimeoutSeconds > 0
            ? new CancellationTokenSource(TimeSpan.FromSeconds(_options.TimeoutSeconds))
            : null;
        using var linkedCts = timeoutCts is null
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
            : CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        using var response = await _httpClient.SendAsync(request, linkedCts.Token);
        if (!response.IsSuccessStatusCode)
            return EmptyMap();

        var payload = await response.Content.ReadAsStringAsync(linkedCts.Token);
        var modelText = ExtractModelText(payload);
        if (string.IsNullOrWhiteSpace(modelText))
            return EmptyMap();

        var json = ExtractJson(modelText);
        if (string.IsNullOrWhiteSpace(json))
            return EmptyMap();

        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions)
                         ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["en"] = Cleanup(parsed.GetValueOrDefault("en")),
                ["fr"] = Cleanup(parsed.GetValueOrDefault("fr")),
                ["de"] = Cleanup(parsed.GetValueOrDefault("de")),
                ["it"] = Cleanup(parsed.GetValueOrDefault("it")),
            };
        }
        catch
        {
            return EmptyMap();
        }
    }

    private static string BuildPrompt(
        string speciesType,
        string scientificName,
        string? taxonomyOrder,
        string? taxonomyFamily,
        string? taxonomyGenus) =>
        string.Join('\n',
            $"Species type: {(speciesType ?? "unknown").Trim().ToLowerInvariant()}",
            $"Scientific name: {scientificName.Trim()}",
            $"Taxonomy order: {(taxonomyOrder ?? string.Empty).Trim()}",
            $"Taxonomy family: {(taxonomyFamily ?? string.Empty).Trim()}",
            $"Taxonomy genus: {(taxonomyGenus ?? string.Empty).Trim()}",
            "Return ONLY valid JSON in this exact shape:",
            "{",
            "  \"en\": \"string\",",
            "  \"fr\": \"string\",",
            "  \"de\": \"string\",",
            "  \"it\": \"string\"",
            "}",
            "Rules:",
            "- Values must be common names only (no labels/prefixes, no taxonomy words).",
            "- If a locale has no known common name, return empty string for that locale.",
            "- Do not include markdown.");

    private static Dictionary<string, string> EmptyMap() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["en"] = string.Empty,
        ["fr"] = string.Empty,
        ["de"] = string.Empty,
        ["it"] = string.Empty,
    };

    private static string Cleanup(string? value)
    {
        var text = (value ?? string.Empty).Trim();
        text = text.Trim('"', '\'', '`', ' ');
        var separators = new[] { ":", "=" };
        foreach (var separator in separators)
        {
            var index = text.IndexOf(separator, StringComparison.Ordinal);
            if (index > 0 && index <= 20)
            {
                var left = text[..index].Trim().ToLowerInvariant();
                if (left is "en" or "fr" or "de" or "it" or "name" or "common name")
                    text = text[(index + 1)..].Trim();
            }
        }
        return text.Trim('"', '\'', '`', ' ');
    }

    private static string? ExtractModelText(string payload)
    {
        using var doc = JsonDocument.Parse(payload);
        if (!doc.RootElement.TryGetProperty("candidates", out var candidates)
            || candidates.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var candidate in candidates.EnumerateArray())
        {
            if (!candidate.TryGetProperty("content", out var content)
                || !content.TryGetProperty("parts", out var parts)
                || parts.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var part in parts.EnumerateArray())
            {
                if (!part.TryGetProperty("text", out var textProp))
                    continue;
                var text = textProp.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                    return text;
            }
        }

        return null;
    }

    private static string? ExtractJson(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var startFenceEnd = trimmed.IndexOf('\n');
            if (startFenceEnd >= 0)
            {
                var withoutStartFence = trimmed[(startFenceEnd + 1)..];
                var endFence = withoutStartFence.LastIndexOf("```", StringComparison.Ordinal);
                if (endFence > 0)
                    trimmed = withoutStartFence[..endFence].Trim();
            }
        }

        var start = trimmed.IndexOf('{');
        var end = trimmed.LastIndexOf('}');
        if (start < 0 || end <= start)
            return null;

        return trimmed.Substring(start, end - start + 1);
    }
}
