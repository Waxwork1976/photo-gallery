using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using PhotoFunctions.Models;
using PhotoFunctions.Services;

namespace PhotoFunctions.Functions;

/// <summary>
/// POST /api/identify-insect
/// Identifies the insect shown in a single uploaded image via Gemini.
/// Protected: requires a valid JWT from an authorized user.
/// </summary>
public sealed class IdentifyInsectFunction
{
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp",
    };

    private static readonly HashSet<string> SupportedLocales = new(StringComparer.OrdinalIgnoreCase)
    {
        "en",
        "fr",
        "de",
        "it",
    };

    private const int MaxFileSizeBytes = 10 * 1024 * 1024;

    private readonly IJwtValidationService _jwtService;
    private readonly IInsectIdentificationService _insectService;
    private readonly ILogger<IdentifyInsectFunction> _logger;

    public IdentifyInsectFunction(
        IJwtValidationService jwtService,
        IInsectIdentificationService insectService,
        ILogger<IdentifyInsectFunction> logger)
    {
        _jwtService = jwtService;
        _insectService = insectService;
        _logger = logger;
    }

    [Function("identify-insect")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "identify-insect")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var (_, authError) = await AuthorizeAsync(req);
        if (authError is not null)
            return authError;

        if (!req.HasFormContentType)
            return new BadRequestObjectResult(
                new ErrorResponse("Request must be multipart/form-data"));

        var form = await req.ReadFormAsync(cancellationToken);
        var files = form.Files.GetFiles("image");
        if (files.Count == 0)
            return new BadRequestObjectResult(
                new ErrorResponse("An image is required (field name: 'image')"));

        if (files.Count > 1)
            return new BadRequestObjectResult(
                new ErrorResponse("Only one image is allowed"));

        var file = files[0];
        if (!AllowedContentTypes.Contains(file.ContentType))
            return new BadRequestObjectResult(
                new ErrorResponse($"Content type '{file.ContentType}' is not allowed. Allowed: {string.Join(", ", AllowedContentTypes)}"));

        if (file.Length <= 0 || file.Length > MaxFileSizeBytes)
            return new BadRequestObjectResult(
                new ErrorResponse($"Image size must be between 1 byte and {MaxFileSizeBytes} bytes"));

        var localeRaw = form["locale"].FirstOrDefault();
        var locale = string.IsNullOrWhiteSpace(localeRaw) ? "en" : localeRaw.Trim().ToLowerInvariant();
        if (!SupportedLocales.Contains(locale))
            return new BadRequestObjectResult(
                new ErrorResponse($"Unsupported locale '{locale}'. Allowed: {string.Join(", ", SupportedLocales)}"));

        await using var stream = file.OpenReadStream();
        try
        {
            var result = await _insectService.IdentifyAsync(
                stream,
                file.FileName,
                file.ContentType,
                locale,
                cancellationToken);

            return new OkObjectResult(result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Gemini configuration is invalid");
            return new ObjectResult(
                new ErrorResponse("Gemini configuration error", ex.Message))
            {
                StatusCode = StatusCodes.Status500InternalServerError
            };
        }
        catch (HttpRequestException ex) when (ex.StatusCode is not null)
        {
            _logger.LogWarning(ex, "Gemini API error");
            return new ObjectResult(
                new ErrorResponse("Gemini API error", ex.Message))
            {
                StatusCode = (int)ex.StatusCode
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Gemini API call failed");
            return new ObjectResult(
                new ErrorResponse("Failed to call Gemini API", ex.Message))
            {
                StatusCode = StatusCodes.Status502BadGateway
            };
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
