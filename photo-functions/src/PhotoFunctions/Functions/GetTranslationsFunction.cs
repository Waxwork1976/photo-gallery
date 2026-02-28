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
        return new OkObjectResult(new TranslationsResponse(document.Locales));
    }
}
