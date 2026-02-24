using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger<SaveMetadataFunction> _logger;

    public SaveMetadataFunction(
        IJwtValidationService jwtService,
        IPhotoTableService tableService,
        IBlobStorageService blobService,
        ILogger<SaveMetadataFunction> logger)
    {
        _jwtService = jwtService;
        _tableService = tableService;
        _blobService = blobService;
        _logger = logger;
    }

    [Function("save-metadata")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "save-metadata")] HttpRequest req)
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
            return new BadRequestObjectResult(new ErrorResponse("Invalid request body"));
        }

        if (body is null
            || string.IsNullOrWhiteSpace(body.BlobName)
            || string.IsNullOrWhiteSpace(body.OriginalFilename)
            || string.IsNullOrWhiteSpace(body.ContentType))
        {
            return new BadRequestObjectResult(
                new ErrorResponse("Missing required fields: blobName, originalFilename, contentType"));
        }

        // 3. Create lightweight thumbnail and build entity
        var fullUrl = _blobService.GetPublicBlobUrl(body.BlobName);
        var thumbnail = await _blobService.CreateThumbnailAsync(body.BlobName, body.ContentType);

        var entity = new PhotoEntity
        {
            RowKey = Guid.NewGuid().ToString(),
            BlobName = body.BlobName,
            BlobUrl = fullUrl,
            ThumbnailBlobName = thumbnail.BlobName,
            ThumbnailBlobUrl = thumbnail.Url,
            OriginalFilename = body.OriginalFilename,
            Title = string.IsNullOrWhiteSpace(body.Title) ? body.OriginalFilename : body.Title,
            Description = body.Description ?? string.Empty,
            Tags = body.Tags is { Length: > 0 } ? string.Join(",", body.Tags) : string.Empty,
            UploadedAt = DateTimeOffset.UtcNow.ToString("o"),
            UploadedBy = oid!,
            ContentType = body.ContentType,
            SizeBytes = body.SizeBytes,
            Width = body.Width ?? thumbnail.Width,
            Height = body.Height ?? thumbnail.Height,
            IsPublic = true,
            Featured = false,
            SortOrder = 0,
        };

        // 4. Insert into Table Storage
        var inserted = await _tableService.InsertAsync(entity);

        _logger.LogInformation("Saved metadata for blob {BlobName} as entity {RowKey}",
            body.BlobName, inserted.RowKey);

        return new ObjectResult(new SaveMetadataResponse(true, inserted.RowKey, fullUrl, thumbnail.Url))
        {
            StatusCode = StatusCodes.Status201Created
        };
    }

    // ----- Helpers -----

    private async Task<(string? oid, IActionResult? error)> AuthorizeAsync(HttpRequest req)
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

        return (oid, null);
    }
}
