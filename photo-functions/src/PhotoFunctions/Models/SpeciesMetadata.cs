using System.Text.Json;

namespace PhotoFunctions.Models;

public static class SpeciesMetadata
{
    private static readonly string[] SupportedLocales = ["en", "fr", "de", "it"];

    public static Dictionary<string, string> NormalizeCommonNames(Dictionary<string, string>? commonNames)
    {
        var normalized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (commonNames is null)
            return normalized;

        foreach (var locale in SupportedLocales)
        {
            if (!commonNames.TryGetValue(locale, out var value))
                continue;

            var cleaned = (value ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(cleaned))
                normalized[locale] = cleaned;
        }

        return normalized;
    }

    public static string SerializeCommonNames(Dictionary<string, string>? commonNames)
    {
        var normalized = NormalizeCommonNames(commonNames);
        return normalized.Count == 0 ? string.Empty : JsonSerializer.Serialize(normalized);
    }

    public static Dictionary<string, string> DeserializeCommonNames(string? commonNamesJson)
    {
        if (string.IsNullOrWhiteSpace(commonNamesJson))
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(commonNamesJson)
                         ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            return NormalizeCommonNames(parsed);
        }
        catch
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public static string NormalizeText(string? value) => (value ?? string.Empty).Trim();

    public static string NormalizeSpeciesType(string? value)
    {
        var speciesType = (value ?? string.Empty).Trim().ToLowerInvariant();
        return speciesType is "plant" or "bird" or "insect" ? speciesType : string.Empty;
    }
}
