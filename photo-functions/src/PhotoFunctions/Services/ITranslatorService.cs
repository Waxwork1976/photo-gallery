namespace PhotoFunctions.Services;

public interface ITranslatorService
{
    Task<IReadOnlyList<string>> TranslateBatchAsync(
        string sourceLocale,
        string targetLocale,
        IReadOnlyList<string> texts);
}
