using System.Text.Json;
using PhotoFunctions.Models;

namespace PhotoFunctions.Services;

public sealed class TranslationService : ITranslationService
{
    private const string ContainerName = "app-config";
    private const string BlobName = "translations/messages.json";
    private static readonly string[] SupportedLocales = ["en", "fr", "de", "it"];
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

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

        var parsed = JsonSerializer.Deserialize<TranslationDocument>(text, JsonOptions) ?? CreateDefaultDocument();
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
            doc.Locales[locale] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
        return doc;
    }

    private static void NormalizeDocument(TranslationDocument document)
    {
        document.Locales ??= new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var locale in SupportedLocales)
        {
            if (!document.Locales.TryGetValue(locale, out var dictionary) || dictionary is null)
            {
                document.Locales[locale] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                continue;
            }

            var normalized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in dictionary)
            {
                var key = kv.Key?.Trim();
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                normalized[key] = kv.Value ?? string.Empty;
            }
            document.Locales[locale] = normalized;
        }
    }
}
