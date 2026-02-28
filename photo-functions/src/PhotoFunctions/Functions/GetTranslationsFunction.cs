using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using PhotoFunctions.Models;
using PhotoFunctions.Services;

namespace PhotoFunctions.Functions;

public sealed class GetTranslationsFunction
{
    private readonly ITranslationService _translationService;

    public GetTranslationsFunction(ITranslationService translationService)
    {
        _translationService = translationService;
    }

    [Function("translations")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "translations")] HttpRequest req)
    {
        var document = await _translationService.GetDocumentAsync();
        var locales = document.Locales.ToDictionary(
            locale => locale.Key,
            locale => locale.Value.ToDictionary(
                entry => entry.Key,
                entry => entry.Value?.Value ?? string.Empty,
                StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);
        return new OkObjectResult(new TranslationsResponse(locales));
    }
}
