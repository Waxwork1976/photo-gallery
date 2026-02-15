using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
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

    private const long MaxFileSizeBytes = 20 * 1024 * 1024; // 20 MB (we can't enforce here, but document it)

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
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "generate-sas")] HttpRequestData req)
    {
        // 1. Authenticate & authorize
        var (principal, errorResponse) = await AuthorizeAsync(req);
        if (errorResponse is not null)
            return errorResponse;

        // 2. Parse request body
        GenerateSasRequest? body;
        try
        {
            body = await req.ReadFromJsonAsync<GenerateSasRequest>();
        }
        catch
        {
            return await CreateJsonResponse(req, HttpStatusCode.BadRequest,
                new ErrorResponse("Invalid request body"));
        }

        if (body is null || string.IsNullOrWhiteSpace(body.Filename) || string.IsNullOrWhiteSpace(body.ContentType))
        {
            return await CreateJsonResponse(req, HttpStatusCode.BadRequest,
                new ErrorResponse("Missing filename or contentType"));
        }

        // 3. Validate content type
        if (!AllowedContentTypes.Contains(body.ContentType))
        {
            return await CreateJsonResponse(req, HttpStatusCode.BadRequest,
                new ErrorResponse($"Content type '{body.ContentType}' is not allowed. " +
                                  $"Allowed: {string.Join(", ", AllowedContentTypes)}"));
        }

        // 4. Generate SAS URL
        var (sasUrl, blobName) = _blobService.GenerateUploadSasUrl(body.Filename, body.ContentType);

        _logger.LogInformation("Generated upload SAS for blob {BlobName} (original: {Filename})",
            blobName, body.Filename);

        return await CreateJsonResponse(req, HttpStatusCode.OK,
            new GenerateSasResponse(sasUrl, blobName, ExpiresInSeconds: 600));
    }

    // ----- Helpers -----

    private async Task<(System.Security.Claims.ClaimsPrincipal? principal, HttpResponseData? error)>
        AuthorizeAsync(HttpRequestData req)
    {
        var token = _jwtService.ExtractBearerToken(
            req.Headers.TryGetValues("Authorization", out var values)
                ? values.FirstOrDefault()
                : null);

        if (token is null)
        {
            var resp = await CreateJsonResponse(req, HttpStatusCode.Unauthorized,
                new ErrorResponse("No token provided"));
            return (null, resp);
        }

        var principal = await _jwtService.ValidateTokenAsync(token);
        if (principal is null)
        {
            var resp = await CreateJsonResponse(req, HttpStatusCode.Unauthorized,
                new ErrorResponse("Invalid token"));
            return (null, resp);
        }

        var oid = principal.FindFirst("oid")?.Value
                  ?? principal.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;

        if (string.IsNullOrEmpty(oid) || !_jwtService.IsAuthorizedUser(oid))
        {
            var resp = await CreateJsonResponse(req, HttpStatusCode.Forbidden,
                new ErrorResponse("User not authorized"));
            return (null, resp);
        }

        return (principal, null);
    }

    private static async Task<HttpResponseData> CreateJsonResponse<T>(
        HttpRequestData req, HttpStatusCode statusCode, T body)
    {
        var response = req.CreateResponse(statusCode);
        await response.WriteAsJsonAsync(body);
        return response;
    }
}
