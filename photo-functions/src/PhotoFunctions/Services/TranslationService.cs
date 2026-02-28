using System.Text.Json;
using PhotoFunctions.Models;

namespace PhotoFunctions.Services;

public sealed class TranslationService : ITranslationService
{
    private const string ContainerName = "app-config";
    private const string BlobName = "translations/messages.json";
    private static readonly string[] SupportedLocales = ["en", "fr", "de", "it"];
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly IBlobStorageService _blobStorage;

    public TranslationService(IBlobStorageService blobStorage)
    {
        _blobStorage = blobStorage;
    }

    public async Task<TranslationDocument> GetDocumentAsync()
    {
        var text = await _blobStorage.ReadTextBlobAsync(ContainerName, BlobName);
        if (string.IsNullOrWhiteSpace(text))
        {
            var defaultDoc = CreateDefaultDocument();
            await SaveDocumentAsync(defaultDoc);
            return defaultDoc;
        }

        var parsed = ParseDocumentFromJson(text);
        NormalizeDocument(parsed);
        return parsed;
    }

    public async Task SaveDocumentAsync(TranslationDocument document)
    {
        NormalizeDocument(document);
        var json = JsonSerializer.Serialize(document, JsonOptions);
        await _blobStorage.WriteTextBlobAsync(ContainerName, BlobName, json);
    }

    private static TranslationDocument CreateDefaultDocument()
    {
        var doc = new TranslationDocument();
        foreach (var locale in SupportedLocales)
        {
            doc.Locales[locale] = new Dictionary<string, TranslationEntry>(StringComparer.OrdinalIgnoreCase);
        }
        return doc;
    }

    private static void NormalizeDocument(TranslationDocument document)
    {
        document.Locales ??= new Dictionary<string, Dictionary<string, TranslationEntry>>(StringComparer.OrdinalIgnoreCase);

        foreach (var locale in SupportedLocales)
        {
            if (!document.Locales.TryGetValue(locale, out var dictionary) || dictionary is null)
            {
                document.Locales[locale] = new Dictionary<string, TranslationEntry>(StringComparer.OrdinalIgnoreCase);
                continue;
            }

            var normalized = new Dictionary<string, TranslationEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in dictionary)
            {
                var key = kv.Key?.Trim();
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                var entry = kv.Value ?? new TranslationEntry();
                normalized[key] = new TranslationEntry
                {
                    Value = entry.Value ?? string.Empty,
                    AutoTranslate = entry.AutoTranslate
                };
            }
            document.Locales[locale] = normalized;
        }
    }

    private static TranslationDocument ParseDocumentFromJson(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return CreateDefaultDocument();

        try
        {
            using var json = JsonDocument.Parse(text);
            if (!json.RootElement.TryGetProperty("locales", out var localesElement)
                && !json.RootElement.TryGetProperty("Locales", out localesElement))
            {
                return CreateDefaultDocument();
            }

            var document = new TranslationDocument();
            foreach (var localeProperty in localesElement.EnumerateObject())
            {
                if (localeProperty.Value.ValueKind != JsonValueKind.Object)
                    continue;

                var entries = new Dictionary<string, TranslationEntry>(StringComparer.OrdinalIgnoreCase);
                foreach (var keyProperty in localeProperty.Value.EnumerateObject())
                {
                    var key = keyProperty.Name?.Trim();
                    if (string.IsNullOrWhiteSpace(key))
                        continue;

                    entries[key] = ParseTranslationEntryElement(keyProperty.Value);
                }

                document.Locales[localeProperty.Name] = entries;
            }

            return document;
        }
        catch (JsonException)
        {
            return CreateDefaultDocument();
        }
    }

    private static TranslationEntry ParseTranslationEntryElement(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            return new TranslationEntry
            {
                Value = element.GetString() ?? string.Empty,
                AutoTranslate = true
            };
        }

        if (element.ValueKind != JsonValueKind.Object)
            return new TranslationEntry();

        var value = string.Empty;
        if (element.TryGetProperty("value", out var valueElement)
            || element.TryGetProperty("Value", out valueElement))
        {
            value = valueElement.GetString() ?? string.Empty;
        }

        var autoTranslate = true;
        if (element.TryGetProperty("autoTranslate", out var autoTranslateElement)
            || element.TryGetProperty("AutoTranslate", out autoTranslateElement))
        {
            autoTranslate = autoTranslateElement.ValueKind == JsonValueKind.True
                || (autoTranslateElement.ValueKind != JsonValueKind.False && autoTranslate);
        }

        return new TranslationEntry
        {
            Value = value,
            AutoTranslate = autoTranslate
        };
    }
}
