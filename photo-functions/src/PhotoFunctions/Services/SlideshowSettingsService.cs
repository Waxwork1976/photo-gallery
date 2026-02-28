using System.Text.Json;
using PhotoFunctions.Models;

namespace PhotoFunctions.Services;

public sealed class SlideshowSettingsService : ISlideshowSettingsService
{
    private const string ContainerName = "app-config";
    private const string BlobName = "settings/slideshow.json";
    private const int DefaultPhotoCount = 8;
    private const int DefaultIntervalSeconds = 4;
    private const int MinPhotoCount = 1;
    private const int MaxPhotoCount = 100;
    private const int MinIntervalSeconds = 1;
    private const int MaxIntervalSeconds = 120;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private readonly IBlobStorageService _blobStorage;

    public SlideshowSettingsService(IBlobStorageService blobStorage)
    {
        _blobStorage = blobStorage;
    }

    public async Task<SlideshowSettingsDocument> GetDocumentAsync()
    {
        var text = await _blobStorage.ReadTextBlobAsync(ContainerName, BlobName);
        if (string.IsNullOrWhiteSpace(text))
        {
            var defaultDoc = CreateDefaultDocument();
            await SaveDocumentAsync(defaultDoc);
            return defaultDoc;
        }

        var parsed = JsonSerializer.Deserialize<SlideshowSettingsDocument>(text, JsonOptions) ?? CreateDefaultDocument();
        NormalizeDocument(parsed);
        return parsed;
    }

    public async Task SaveDocumentAsync(SlideshowSettingsDocument document)
    {
        NormalizeDocument(document);
        var json = JsonSerializer.Serialize(document, JsonOptions);
        await _blobStorage.WriteTextBlobAsync(ContainerName, BlobName, json);
    }

    private static SlideshowSettingsDocument CreateDefaultDocument() =>
        new()
        {
            PhotoCount = DefaultPhotoCount,
            IntervalSeconds = DefaultIntervalSeconds,
        };

    private static void NormalizeDocument(SlideshowSettingsDocument document)
    {
        document.PhotoCount = Math.Clamp(document.PhotoCount, MinPhotoCount, MaxPhotoCount);
        document.IntervalSeconds = Math.Clamp(document.IntervalSeconds, MinIntervalSeconds, MaxIntervalSeconds);
    }
}
