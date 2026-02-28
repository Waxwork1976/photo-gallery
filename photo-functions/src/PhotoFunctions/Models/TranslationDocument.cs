namespace PhotoFunctions.Models;

public sealed class TranslationEntry
{
    public string Value { get; set; } = string.Empty;
    public bool AutoTranslate { get; set; } = true;
}

public sealed class TranslationDocument
{
    public Dictionary<string, Dictionary<string, TranslationEntry>> Locales { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
