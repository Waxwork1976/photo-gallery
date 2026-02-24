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
/// GET /api/list-images?featured=true|false
/// Lists all public images (optionally filtered to featured only).
/// This endpoint is PUBLIC — no authentication required.
/// </summary>
public sealed class ListImagesFunction
{
    private readonly IPhotoTableService _tableService;
    private readonly IBlobStorageService _blobService;
    private readonly AzureStorageOptions _storageOptions;
    private readonly ILogger<ListImagesFunction> _logger;

    public ListImagesFunction(
        IPhotoTableService tableService,
        IBlobStorageService blobService,
        IOptions<AzureStorageOptions> storageOptions,
        ILogger<ListImagesFunction> logger)
    {
        _tableService = tableService;
        _blobService = blobService;
        _storageOptions = storageOptions.Value;
        _logger = logger;
    }

    [Function("list-images")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "list-images")] HttpRequest req)
    {
        var featuredOnly = string.Equals(
            req.Query["featured"], "true", StringComparison.OrdinalIgnoreCase);

        var entities = await _tableService.ListAsync(featuredOnly);

        var images = entities.Select(e =>
        {
            // Resolve image URL depending on public vs private container
            var url = _storageOptions.UsePrivateContainer
                ? _blobService.GenerateReadSasUrl(e.BlobName, TimeSpan.FromHours(1))
                : _blobService.GetPublicBlobUrl(e.BlobName);

            return new ImageDto(
                Id: e.RowKey,
                BlobName: e.BlobName,
                Url: url,
                Title: e.Title,
                Description: e.Description,
                Tags: e.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries),
                PrimaryFolderPath: string.IsNullOrWhiteSpace(e.PrimaryFolderPath) ? "photos" : e.PrimaryFolderPath,
                FolderPaths: string.IsNullOrWhiteSpace(e.FolderPathsCsv) ? ["photos"] : e.FolderPathsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries),
                UploadedAt: e.UploadedAt,
                Width: e.Width,
                Height: e.Height,
                SizeBytes: e.SizeBytes,
                Featured: e.Featured);
        }).ToArray();

        _logger.LogInformation("Returned {Count} images (featuredOnly={Featured})",
            images.Length, featuredOnly);

        return new OkObjectResult(new ListImagesResponse(images));
    }
}
