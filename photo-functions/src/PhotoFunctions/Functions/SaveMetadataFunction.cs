using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PhotoFunctions.Configuration;
using PhotoFunctions.Models;
using PhotoFunctions.Services;

namespace PhotoFunctions.Functions;

/// <summary>
/// POST /api/save-metadata
/// Saves image metadata to Azure Table Storage after a successful blob upload.
/// Protected: requires a valid JWT from an authorized user.
/// </summary>
public sealed class SaveMetadataFunction
{
    private readonly IJwtValidationService _jwtService;
    private readonly IPhotoTableService _tableService;
    private readonly IBlobStorageService _blobService;
    private readonly AzureStorageOptions _storageOptions;
    private readonly ILogger<SaveMetadataFunction> _logger;

    public SaveMetadataFunction(
        IJwtValidationService jwtService,
        IPhotoTableService tableService,
        IBlobStorageService blobService,
        IOptions<AzureStorageOptions> storageOptions,
        ILogger<SaveMetadataFunction> logger)
    {
        _jwtService = jwtService;
        _tableService = tableService;
        _blobService = blobService;
        _storageOptions = storageOptions.Value;
        _logger = logger;
    }

    [Function("save-metadata")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "save-metadata")] HttpRequestData req)
    {
        // 1. Authenticate & authorize
        var (oid, authError) = await AuthorizeAsync(req);
        if (authError is not null)
            return authError;

        // 2. Parse request body
        SaveMetadataRequest? body;
        try
        {
            body = await req.ReadFromJsonAsync<SaveMetadataRequest>();
        }
        catch
        {
            return await CreateJsonResponse(req, HttpStatusCode.BadRequest,
                new ErrorResponse("Invalid request body"));
        }

        if (body is null
            || string.IsNullOrWhiteSpace(body.BlobName)
            || string.IsNullOrWhiteSpace(body.OriginalFilename)
            || string.IsNullOrWhiteSpace(body.ContentType))
        {
            return await CreateJsonResponse(req, HttpStatusCode.BadRequest,
                new ErrorResponse("Missing required fields: blobName, originalFilename, contentType"));
        }

        // 3. Build entity
        var blobUrl = _blobService.GetPublicBlobUrl(body.BlobName);

        var entity = new PhotoEntity
        {
            RowKey = Guid.NewGuid().ToString(),
            BlobName = body.BlobName,
            BlobUrl = blobUrl,
            OriginalFilename = body.OriginalFilename,
            Title = string.IsNullOrWhiteSpace(body.Title) ? body.OriginalFilename : body.Title,
            Description = body.Description ?? string.Empty,
            Tags = body.Tags is { Length: > 0 } ? string.Join(",", body.Tags) : string.Empty,
            UploadedAt = DateTimeOffset.UtcNow.ToString("o"),
            UploadedBy = oid!,
            ContentType = body.ContentType,
            SizeBytes = body.SizeBytes,
            Width = body.Width ?? 0,
            Height = body.Height ?? 0,
            IsPublic = true,
            Featured = false,
            SortOrder = 0,
        };

        // 4. Insert into Table Storage
        var inserted = await _tableService.InsertAsync(entity);

        _logger.LogInformation("Saved metadata for blob {BlobName} as entity {RowKey}",
            body.BlobName, inserted.RowKey);

        return await CreateJsonResponse(req, HttpStatusCode.Created,
            new SaveMetadataResponse(true, inserted.RowKey, blobUrl));
    }

    // ----- Helpers -----

    private async Task<(string? oid, HttpResponseData? error)> AuthorizeAsync(HttpRequestData req)
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

        return (oid, null);
    }

    private static async Task<HttpResponseData> CreateJsonResponse<T>(
        HttpRequestData req, HttpStatusCode statusCode, T body)
    {
        var response = req.CreateResponse(statusCode);
        await response.WriteAsJsonAsync(body);
        return response;
    }
}
