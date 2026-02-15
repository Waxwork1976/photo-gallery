using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using PhotoFunctions.Models;
using PhotoFunctions.Services;

namespace PhotoFunctions.Functions;

/// <summary>
/// POST /api/generate-sas
/// Generates a time-limited SAS URL for uploading a single image to Blob Storage.
/// Protected: requires a valid JWT from an authorized user.
/// </summary>
public sealed class GenerateSasFunction
{
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp",
        "image/gif",
    };

    private readonly IJwtValidationService _jwtService;
    private readonly IBlobStorageService _blobService;
    private readonly ILogger<GenerateSasFunction> _logger;

    public GenerateSasFunction(
        IJwtValidationService jwtService,
        IBlobStorageService blobService,
        ILogger<GenerateSasFunction> logger)
    {
        _jwtService = jwtService;
        _blobService = blobService;
        _logger = logger;
    }

    [Function("generate-sas")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "generate-sas")] HttpRequest req)
    {
        // 1. Authenticate & authorize
        var (_, authError) = await AuthorizeAsync(req);
        if (authError is not null)
            return authError;

        // 2. Parse request body
        GenerateSasRequest? body;
        try
        {
            body = await req.ReadFromJsonAsync<GenerateSasRequest>();
        }
        catch
        {
            return new BadRequestObjectResult(new ErrorResponse("Invalid request body"));
        }

        if (body is null || string.IsNullOrWhiteSpace(body.Filename) || string.IsNullOrWhiteSpace(body.ContentType))
        {
            return new BadRequestObjectResult(new ErrorResponse("Missing filename or contentType"));
        }

        // 3. Validate content type
        if (!AllowedContentTypes.Contains(body.ContentType))
        {
            return new BadRequestObjectResult(
                new ErrorResponse($"Content type '{body.ContentType}' is not allowed. " +
                                  $"Allowed: {string.Join(", ", AllowedContentTypes)}"));
        }

        // 4. Generate SAS URL
        var (sasUrl, blobName) = _blobService.GenerateUploadSasUrl(body.Filename, body.ContentType);

        _logger.LogInformation("Generated upload SAS for blob {BlobName} (original: {Filename})",
            blobName, body.Filename);

        return new OkObjectResult(new GenerateSasResponse(sasUrl, blobName, ExpiresInSeconds: 600));
    }

    // ----- Helpers -----

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
