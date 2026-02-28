using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using PhotoFunctions.Models;
using PhotoFunctions.Services;

namespace PhotoFunctions.Functions;

public sealed class ManageTranslationsFunctions
{
    private static readonly string[] SupportedLocales = ["en", "fr", "de", "it"];
    private readonly IJwtValidationService _jwtService;
    private readonly ITranslationService _translationService;
    private readonly ITranslatorService _translatorService;

    public ManageTranslationsFunctions(
        IJwtValidationService jwtService,
        ITranslationService translationService,
        ITranslatorService translatorService)
    {
        _jwtService = jwtService;
        _translationService = translationService;
        _translatorService = translatorService;
    }

    [Function("manage-translations-get")]
    public async Task<IActionResult> Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "manage-translations")] HttpRequest req)
    {
        var (_, authError) = await AuthorizeAsync(req);
        if (authError is not null)
            return authError;

        var document = await _translationService.GetDocumentAsync();
        return new OkObjectResult(new TranslationsResponse(document.Locales));
    }

    [Function("manage-translations-put")]
    public async Task<IActionResult> Put(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "manage-translations")] HttpRequest req)
    {
        var (_, authError) = await AuthorizeAsync(req);
        if (authError is not null)
            return authError;

        ManageTranslationsRequest? body;
        try
        {
            body = await req.ReadFromJsonAsync<ManageTranslationsRequest>();
        }
        catch
        {
            return new BadRequestObjectResult(new ErrorResponse("Invalid request body"));
        }

        if (body?.Locales is null)
            return new BadRequestObjectResult(new ErrorResponse("Request body is required"));

        var document = new TranslationDocument
        {
            Locales = body.Locales
        };
        await _translationService.SaveDocumentAsync(document);

        var persisted = await _translationService.GetDocumentAsync();
        return new OkObjectResult(new TranslationsResponse(persisted.Locales));
    }

    [Function("manage-translations-translate-all")]
    public async Task<IActionResult> TranslateAll(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "manage-translations/translate-all")] HttpRequest req)
    {
        var (_, authError) = await AuthorizeAsync(req);
        if (authError is not null)
            return authError;

        TranslateAllTranslationsRequest? body;
        try
        {
            body = await req.ReadFromJsonAsync<TranslateAllTranslationsRequest>();
        }
        catch
        {
            return new BadRequestObjectResult(new ErrorResponse("Invalid request body"));
        }

        if (body?.Locales is null || string.IsNullOrWhiteSpace(body.SourceLocale))
            return new BadRequestObjectResult(new ErrorResponse("Request body is required"));

        var sourceLocale = body.SourceLocale.Trim().ToLowerInvariant();
        if (!SupportedLocales.Contains(sourceLocale))
            return new BadRequestObjectResult(new ErrorResponse($"Unsupported source locale '{sourceLocale}'"));

        var targetLocales = ResolveTargetLocales(sourceLocale, body.TargetLocales);
        if (targetLocales.Count == 0)
            return new BadRequestObjectResult(new ErrorResponse("At least one target locale is required"));

        EnsureLocaleDictionaries(body.Locales);
        var sourceDictionary = body.Locales[sourceLocale];
        var sourceItems = sourceDictionary
            .Where(kv => !string.IsNullOrWhiteSpace(kv.Key) && !string.IsNullOrWhiteSpace(kv.Value))
            .Select(kv => (Key: kv.Key.Trim(), Value: kv.Value))
            .ToArray();

        if (sourceItems.Length == 0)
            return new BadRequestObjectResult(new ErrorResponse("No non-empty source translations found"));

        var keys = sourceItems.Select(item => item.Key).ToArray();
        var sourceTexts = sourceItems.Select(item => item.Value).ToArray();

        try
        {
            foreach (var targetLocale in targetLocales)
            {
                var translatedTexts = await _translatorService.TranslateBatchAsync(
                    sourceLocale,
                    targetLocale,
                    sourceTexts);

                for (var i = 0; i < keys.Length; i++)
                    body.Locales[targetLocale][keys[i]] = translatedTexts[i];
            }

            return new OkObjectResult(new TranslationsResponse(body.Locales));
        }
        catch (InvalidOperationException ex)
        {
            return new ObjectResult(new ErrorResponse("Translator is not configured", ex.Message))
            {
                StatusCode = StatusCodes.Status500InternalServerError
            };
        }
        catch (HttpRequestException ex)
        {
            return new ObjectResult(new ErrorResponse("Failed to call Translator API", ex.Message))
            {
                StatusCode = (int?)ex.StatusCode ?? StatusCodes.Status502BadGateway
            };
        }
    }

    [Function("manage-translations-translate-key")]
    public async Task<IActionResult> TranslateKey(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "manage-translations/translate-key")] HttpRequest req)
    {
        var (_, authError) = await AuthorizeAsync(req);
        if (authError is not null)
            return authError;

        TranslateSingleTranslationRequest? body;
        try
        {
            body = await req.ReadFromJsonAsync<TranslateSingleTranslationRequest>();
        }
        catch
        {
            return new BadRequestObjectResult(new ErrorResponse("Invalid request body"));
        }

        if (body?.Locales is null
            || string.IsNullOrWhiteSpace(body.SourceLocale)
            || string.IsNullOrWhiteSpace(body.Key))
        {
            return new BadRequestObjectResult(new ErrorResponse("Request body is required"));
        }

        var sourceLocale = body.SourceLocale.Trim().ToLowerInvariant();
        if (!SupportedLocales.Contains(sourceLocale))
            return new BadRequestObjectResult(new ErrorResponse($"Unsupported source locale '{sourceLocale}'"));

        var key = body.Key.Trim();
        var targetLocales = ResolveTargetLocales(sourceLocale, body.TargetLocales);
        if (targetLocales.Count == 0)
            return new BadRequestObjectResult(new ErrorResponse("At least one target locale is required"));

        EnsureLocaleDictionaries(body.Locales);
        var sourceDictionary = body.Locales[sourceLocale];
        var sourceText = sourceDictionary.GetValueOrDefault(key)?.Trim();
        if (string.IsNullOrWhiteSpace(sourceText))
            return new BadRequestObjectResult(new ErrorResponse($"Source text is empty for key '{key}'"));

        try
        {
            foreach (var targetLocale in targetLocales)
            {
                var translated = await _translatorService.TranslateBatchAsync(
                    sourceLocale,
                    targetLocale,
                    [sourceText]);
                body.Locales[targetLocale][key] = translated[0];
            }

            return new OkObjectResult(new TranslationsResponse(body.Locales));
        }
        catch (InvalidOperationException ex)
        {
            return new ObjectResult(new ErrorResponse("Translator is not configured", ex.Message))
            {
                StatusCode = StatusCodes.Status500InternalServerError
            };
        }
        catch (HttpRequestException ex)
        {
            return new ObjectResult(new ErrorResponse("Failed to call Translator API", ex.Message))
            {
                StatusCode = (int?)ex.StatusCode ?? StatusCodes.Status502BadGateway
            };
        }
    }

    private static List<string> ResolveTargetLocales(string sourceLocale, string[]? requestedTargetLocales)
    {
        if (requestedTargetLocales is null || requestedTargetLocales.Length == 0)
        {
            return SupportedLocales
                .Where(locale => !string.Equals(locale, sourceLocale, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        return requestedTargetLocales
            .Where(locale => !string.IsNullOrWhiteSpace(locale))
            .Select(locale => locale.Trim().ToLowerInvariant())
            .Where(SupportedLocales.Contains)
            .Where(locale => !string.Equals(locale, sourceLocale, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void EnsureLocaleDictionaries(Dictionary<string, Dictionary<string, string>> locales)
    {
        foreach (var locale in SupportedLocales)
        {
            if (!locales.TryGetValue(locale, out var dictionary) || dictionary is null)
                locales[locale] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private async Task<(ClaimsPrincipal? principal, IActionResult? error)> AuthorizeAsync(HttpRequest req)
    {
        var token = _jwtService.ExtractBearerToken(
            req.Headers.Authorization.FirstOrDefault());

        if (token is null)
            return (null, new UnauthorizedObjectResult(new ErrorResponse("No token provided")));

        var principal = await _jwtService.ValidateTokenAsync(token);
        if (principal is null)
            return (null, new UnauthorizedObjectResult(new ErrorResponse("Invalid token")));

        var oid = principal.FindFirst("oid")?.Value
                  ?? principal.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;

        if (string.IsNullOrEmpty(oid) || !_jwtService.IsAuthorizedUser(oid))
            return (null, new ObjectResult(new ErrorResponse("User not authorized")) { StatusCode = StatusCodes.Status403Forbidden });

        return (principal, null);
    }
}
