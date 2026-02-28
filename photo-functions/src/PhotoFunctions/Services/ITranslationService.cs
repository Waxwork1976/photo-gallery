using PhotoFunctions.Models;

namespace PhotoFunctions.Services;

public interface ITranslationService
{
    Task<TranslationDocument> GetDocumentAsync();
    Task SaveDocumentAsync(TranslationDocument document);
}
