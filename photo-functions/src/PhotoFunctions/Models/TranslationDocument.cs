namespace PhotoFunctions.Models;

public sealed class TranslationDocument
{
    public Dictionary<string, Dictionary<string, string>> Locales { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
