namespace PhotoFunctions.Models;

public static class SearchSuggestionsBuilder
{
    public static SearchSuggestionDto[] Build(
        IEnumerable<PhotoEntity> entities,
        string query,
        string locale,
        int limit)
    {
        var normalizedQuery = (query ?? string.Empty).Trim().ToLowerInvariant();
        if (normalizedQuery.Length < 3)
            return [];

        var preferredLocale = NormalizeLocale(locale);
        var candidates = new List<string>();

        foreach (var entity in entities)
        {
            candidates.AddRange((entity.Tags ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

            if (!string.IsNullOrWhiteSpace(entity.ScientificName))
                candidates.Add(entity.ScientificName);
            if (!string.IsNullOrWhiteSpace(entity.TaxonomyOrder))
                candidates.Add(entity.TaxonomyOrder);
            if (!string.IsNullOrWhiteSpace(entity.TaxonomyFamily))
                candidates.Add(entity.TaxonomyFamily);
            if (!string.IsNullOrWhiteSpace(entity.TaxonomyGenus))
                candidates.Add(entity.TaxonomyGenus);

            var commonNames = SpeciesMetadata.DeserializeCommonNames(entity.CommonNamesJson);
            var preferred = commonNames.GetValueOrDefault(preferredLocale);
            if (!string.IsNullOrWhiteSpace(preferred))
                candidates.Add(preferred);
            var en = commonNames.GetValueOrDefault("en");
            if (!string.IsNullOrWhiteSpace(en))
                candidates.Add(en);
            candidates.AddRange(commonNames.Values.Where(v => !string.IsNullOrWhiteSpace(v)));
        }

        var dedup = candidates
            .Select(v => (v ?? string.Empty).Trim())
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var startsWith = dedup
            .Where(v => v.StartsWith(normalizedQuery, StringComparison.OrdinalIgnoreCase))
            .Select(v => new SearchSuggestionDto(v, "startsWith"));
        var contains = dedup
            .Where(v => !v.StartsWith(normalizedQuery, StringComparison.OrdinalIgnoreCase))
            .Where(v => v.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
            .Select(v => new SearchSuggestionDto(v, "contains"));

        return startsWith
            .Concat(contains)
            .Take(Math.Clamp(limit, 1, 50))
            .ToArray();
    }

    private static string NormalizeLocale(string locale)
    {
        var normalized = (locale ?? "en").Trim().ToLowerInvariant();
        return normalized is "fr" or "de" or "it" ? normalized : "en";
    }
}
