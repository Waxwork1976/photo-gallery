using PhotoFunctions.Models;

namespace PhotoFunctions.Services;

public interface ISlideshowSettingsService
{
    Task<SlideshowSettingsDocument> GetDocumentAsync();
    Task SaveDocumentAsync(SlideshowSettingsDocument document);
}
