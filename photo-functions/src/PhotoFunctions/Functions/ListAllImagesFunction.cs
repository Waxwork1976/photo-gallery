using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PhotoFunctions.Configuration;
using PhotoFunctions.Models;
using PhotoFunctions.Services;

namespace PhotoFunctions.Functions;

/// <summary>
/// GET /api/admin/list-images
/// Lists all images regardless of visibility status. For the management page.
/// Protected: requires a valid JWT from an authorized user.
/// </summary>
public sealed class ListAllImagesFunction
{
    private readonly IJwtValidationService _jwtService;
    private readonly IPhotoTableService _tableService;
    private readonly IBlobStorageService _blobService;
    private readonly AzureStorageOptions _storageOptions;
    private readonly ILogger<ListAllImagesFunction> _logger;

    public ListAllImagesFunction(
        IJwtValidationService jwtService,
        IPhotoTableService tableService,
        IBlobStorageService blobService,
        IOptions<AzureStorageOptions> storageOptions,
        ILogger<ListAllImagesFunction> logger)
    {
        _jwtService = jwtService;
        _tableService = tableService;
        _blobService = blobService;
        _storageOptions = storageOptions.Value;
        _logger = logger;
    }

    [Function("manage-images")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "manage-images")] HttpRequest req)
    {
        var (_, authError) = await AuthorizeAsync(req);
        if (authError is not null)
            return authError;

        var entities = await _tableService.ListAllAsync();

        var images = entities.Select(e =>
        {
            var fullUrl = _storageOptions.UsePrivateContainer
                ? _blobService.GenerateReadSasUrl(e.BlobName, TimeSpan.FromHours(1))
                : _blobService.GetPublicBlobUrl(e.BlobName);
            var thumbnailBlobName = string.IsNullOrWhiteSpace(e.ThumbnailBlobName) ? e.BlobName : e.ThumbnailBlobName;
            var thumbnailUrl = _storageOptions.UsePrivateContainer
                ? _blobService.GenerateReadSasUrl(thumbnailBlobName, TimeSpan.FromHours(1))
                : _blobService.GetPublicBlobUrl(thumbnailBlobName);

            return new ImageDto(
                Id: e.RowKey,
                BlobName: e.BlobName,
                FullUrl: fullUrl,
                ThumbnailUrl: thumbnailUrl,
                Title: e.Title,
                Description: e.Description,
                Tags: e.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries),
                SpeciesType: e.SpeciesType ?? string.Empty,
                ScientificName: e.ScientificName ?? string.Empty,
                TaxonomyOrder: e.TaxonomyOrder ?? string.Empty,
                TaxonomyFamily: e.TaxonomyFamily ?? string.Empty,
                TaxonomyGenus: e.TaxonomyGenus ?? string.Empty,
                CommonNames: SpeciesMetadata.DeserializeCommonNames(e.CommonNamesJson),
                PrimaryFolderPath: string.IsNullOrWhiteSpace(e.PrimaryFolderPath) ? "photos" : e.PrimaryFolderPath,
                FolderPaths: string.IsNullOrWhiteSpace(e.FolderPathsCsv) ? ["photos"] : e.FolderPathsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries),
                UploadedAt: e.UploadedAt,
                Width: e.Width,
                Height: e.Height,
                Latitude: e.Latitude,
                Longitude: e.Longitude,
                SizeBytes: e.SizeBytes,
                Featured: e.Featured);
        }).ToArray();

        _logger.LogInformation("Admin listed {Count} images", images.Length);

        return new OkObjectResult(new ListImagesResponse(images));
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
