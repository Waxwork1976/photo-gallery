using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PhotoFunctions.Configuration;
using PhotoFunctions.Models;

namespace PhotoFunctions.Services;

public sealed class InsectIdentificationService : IInsectIdentificationService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly GeminiOptions _options;
    private readonly ILogger<InsectIdentificationService> _logger;

    public InsectIdentificationService(
        HttpClient httpClient,
        IOptions<GeminiOptions> options,
        ILogger<InsectIdentificationService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<InsectIdentificationResponse> IdentifyAsync(
        Stream content,
        string fileName,
        string contentType,
        string locale,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
            throw new InvalidOperationException("Gemini API key is not configured.");

        if (_options.TimeoutSeconds > 0)
            _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);

        var base64 = await ToBase64Async(content, cancellationToken);
        var endpoint =
            $"{_options.BaseUrl.TrimEnd('/')}/v1beta/models/{Uri.EscapeDataString(_options.Model)}:generateContent?key={Uri.EscapeDataString(_options.ApiKey)}";

        var prompt = BuildPrompt(locale);
        var body = new Dictionary<string, object?>
        {
            ["contents"] = new object[]
            {
                new Dictionary<string, object?>
                {
                    ["role"] = "user",
                    ["parts"] = new object[]
                    {
                        new Dictionary<string, object?> { ["text"] = prompt },
                        new Dictionary<string, object?>
                        {
                            ["inline_data"] = new Dictionary<string, string>
                            {
                                ["mime_type"] = contentType,
                                ["data"] = base64
                            }
                        }
                    }
                }
            }
        };

        var json = JsonSerializer.Serialize(body, JsonOptions);
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        var response = await _httpClient.SendAsync(request, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Gemini API returned {Status}: {Body} (file={FileName})",
                (int)response.StatusCode, payload, fileName);
            throw new HttpRequestException(
                $"Gemini API returned {(int)response.StatusCode}: {payload}",
                null,
                response.StatusCode);
        }

        var modelText = ExtractModelText(payload);
        if (string.IsNullOrWhiteSpace(modelText))
            return EmptyResult();

        var parsed = ParseNormalizedJson(modelText);
        if (parsed is null)
            return EmptyResult();

        return parsed;
    }

    private static async Task<string> ToBase64Async(Stream content, CancellationToken cancellationToken)
    {
        if (content.CanSeek)
            content.Position = 0;

        using var ms = new MemoryStream();
        await content.CopyToAsync(ms, cancellationToken);
        return Convert.ToBase64String(ms.ToArray());
    }

    private static string BuildPrompt(string locale)
    {
        var languageName = locale switch
        {
            "fr" => "French",
            "de" => "German",
            "it" => "Italian",
            _ => "English"
        };

        return string.Join('\n',
            "Identify the main insect visible in this image.",
            "Return ONLY valid JSON with this exact shape and no markdown:",
            "{",
            "  \"accepted\": boolean,",
            "  \"confidence\": number,",
            "  \"commonName\": \"string\",",
            "  \"scientificName\": \"string\",",
            "  \"taxonomyOrder\": \"string\",",
            "  \"taxonomyFamily\": \"string\",",
            "  \"taxonomyGenus\": \"string\"",
            "}",
            string.Empty,
            "Rules:",
            $"- commonName must be written in {languageName}.",
            "- scientificName must be Latin binomial or best known scientific name.",
            "- taxonomyOrder, taxonomyFamily, taxonomyGenus must be separate values.",
            "- Return ONLY the raw value in each field. Do NOT include labels, prefixes, or key names.",
            "- Forbidden examples inside values: 'taxonomy:order:Lepidoptera', 'order: Lepidoptera', 'family=Formicidae'.",
            "- Correct examples: 'Lepidoptera', 'Formicidae', 'Pieris brassicae'.",
            "- confidence must be between 0 and 1.",
            "- If not enough certainty, set accepted=false but still provide best-effort values (empty string allowed).");
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
                if (part.TryGetProperty("text", out var textProp))
                {
                    var text = textProp.GetString();
                    if (!string.IsNullOrWhiteSpace(text))
                        return text;
                }
            }
        }

        return null;
    }

    private static InsectIdentificationResponse? ParseNormalizedJson(string modelText)
    {
        var candidate = ExtractJson(modelText);
        if (string.IsNullOrWhiteSpace(candidate))
            return null;

        InsectIdentificationResponse? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<InsectIdentificationResponse>(candidate, JsonOptions);
        }
        catch
        {
            return null;
        }

        if (parsed is null)
            return null;

        var confidence = parsed.Confidence;
        if (confidence > 1 && confidence <= 100)
            confidence /= 100;

        confidence = Math.Clamp(confidence, 0, 1);

        var commonName = NormalizeTaxonomyValue(parsed.CommonName);
        var scientificName = NormalizeTaxonomyValue(parsed.ScientificName);
        var taxonomyOrder = NormalizeTaxonomyValue(parsed.TaxonomyOrder);
        var taxonomyFamily = NormalizeTaxonomyValue(parsed.TaxonomyFamily);
        var taxonomyGenus = NormalizeTaxonomyValue(parsed.TaxonomyGenus);

        var accepted = parsed.Accepted
                       && !string.IsNullOrWhiteSpace(scientificName)
                       && !string.IsNullOrWhiteSpace(taxonomyOrder)
                       && !string.IsNullOrWhiteSpace(taxonomyFamily)
                       && !string.IsNullOrWhiteSpace(taxonomyGenus);

        return new InsectIdentificationResponse(
            Accepted: accepted,
            Confidence: confidence,
            CommonName: commonName,
            ScientificName: scientificName,
            TaxonomyOrder: taxonomyOrder,
            TaxonomyFamily: taxonomyFamily,
            TaxonomyGenus: taxonomyGenus);
    }

    private static string NormalizeTaxonomyValue(string? value)
    {
        var normalized = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            return string.Empty;

        normalized = normalized.Trim('"', '\'', '`', ' ');

        var lower = normalized.ToLowerInvariant();
        var knownPrefixes = new[]
        {
            "taxonomy:order:",
            "taxonomy:family:",
            "taxonomy:genus:",
            "species:scientific:",
            "species:common:",
            "order:",
            "family:",
            "genus:",
            "scientificname:",
            "scientific name:",
            "commonname:",
            "common name:"
        };

        foreach (var prefix in knownPrefixes)
        {
            if (lower.StartsWith(prefix, StringComparison.Ordinal))
            {
                normalized = normalized[prefix.Length..].Trim();
                break;
            }
        }

        if (normalized.Contains('='))
        {
            var parts = normalized.Split('=', 2, StringSplitOptions.TrimEntries);
            if (parts.Length == 2)
                normalized = parts[1];
        }

        return normalized.Trim('"', '\'', '`', ' ');
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

    private static InsectIdentificationResponse EmptyResult() =>
        new(
            Accepted: false,
            Confidence: 0,
            CommonName: string.Empty,
            ScientificName: string.Empty,
            TaxonomyOrder: string.Empty,
            TaxonomyFamily: string.Empty,
            TaxonomyGenus: string.Empty);
}
