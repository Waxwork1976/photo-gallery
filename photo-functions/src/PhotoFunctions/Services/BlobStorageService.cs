using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.Extensions.Options;
using PhotoFunctions.Configuration;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace PhotoFunctions.Services;

/// <summary>
/// Handles Azure Blob Storage operations: SAS URL generation for upload and read.
/// </summary>
public sealed class BlobStorageService : IBlobStorageService
{
    private const int ThumbnailMaxWidth = 1200;
    private const int ThumbnailJpegQuality = 72;

    private readonly AzureStorageOptions _options;
    private readonly StorageSharedKeyCredential _credential;
    private readonly BlobServiceClient _blobServiceClient;

    public BlobStorageService(IOptions<AzureStorageOptions> options)
    {
        _options = options.Value;

        _credential = new StorageSharedKeyCredential(
            _options.AccountName,
            _options.AccountKey);

        _blobServiceClient = new BlobServiceClient(
            new Uri(_options.BlobServiceUri),
            _credential);
    }

    /// <inheritdoc />
    public (string SasUrl, string BlobName) GenerateUploadSasUrl(string originalFilename, string contentType)
    {
        // Build a unique blob name: <guid>.<original-extension>
        var extension = Path.GetExtension(originalFilename)?.TrimStart('.') ?? "jpg";
        var blobName = $"{Guid.NewGuid()}.{extension}";

        var containerClient = _blobServiceClient.GetBlobContainerClient(_options.PhotoContainerName);
        var blobClient = containerClient.GetBlobClient(blobName);

        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = _options.PhotoContainerName,
            BlobName = blobName,
            Resource = "b",                       // "b" = blob level
            StartsOn = DateTimeOffset.UtcNow.AddMinutes(-1),
            ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(10),
            ContentType = contentType,
        };
        sasBuilder.SetPermissions(BlobSasPermissions.Write);

        var sasToken = sasBuilder.ToSasQueryParameters(_credential).ToString();
        var sasUrl = $"{blobClient.Uri}?{sasToken}";

        return (sasUrl, blobName);
    }

    /// <inheritdoc />
    public string GenerateReadSasUrl(string blobName, TimeSpan? expiry = null)
    {
        expiry ??= TimeSpan.FromHours(1);

        var containerClient = _blobServiceClient.GetBlobContainerClient(_options.PhotoContainerName);
        var blobClient = containerClient.GetBlobClient(blobName);

        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = _options.PhotoContainerName,
            BlobName = blobName,
            Resource = "b",
            StartsOn = DateTimeOffset.UtcNow.AddMinutes(-1),
            ExpiresOn = DateTimeOffset.UtcNow.Add(expiry.Value),
        };
        sasBuilder.SetPermissions(BlobSasPermissions.Read);

        var sasToken = sasBuilder.ToSasQueryParameters(_credential).ToString();
        return $"{blobClient.Uri}?{sasToken}";
    }

    /// <inheritdoc />
    public string GetPublicBlobUrl(string blobName)
    {
        return $"{_options.BlobServiceUri}/{_options.PhotoContainerName}/{blobName}";
    }

    /// <inheritdoc />
    public async Task DeleteBlobAsync(string blobName)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(_options.PhotoContainerName);
        await containerClient.DeleteBlobIfExistsAsync(blobName);
    }

    public async Task<string?> ReadTextBlobAsync(string containerName, string blobName)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        var blobClient = containerClient.GetBlobClient(blobName);

        var exists = await blobClient.ExistsAsync();
        if (!exists.Value)
            return null;

        var response = await blobClient.DownloadContentAsync();
        return response.Value.Content.ToString();
    }

    public async Task WriteTextBlobAsync(string containerName, string blobName, string content, string contentType = "application/json")
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        await containerClient.CreateIfNotExistsAsync();

        var blobClient = containerClient.GetBlobClient(blobName);
        var options = new Azure.Storage.Blobs.Models.BlobUploadOptions
        {
            HttpHeaders = new Azure.Storage.Blobs.Models.BlobHttpHeaders
            {
                ContentType = contentType,
            },
        };

        await blobClient.UploadAsync(BinaryData.FromString(content), options);
    }

    /// <inheritdoc />
    public async Task<IBlobStorageService.ThumbnailResult> CreateThumbnailAsync(string sourceBlobName, string contentType)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(_options.PhotoContainerName);
        var sourceBlobClient = containerClient.GetBlobClient(sourceBlobName);
        var thumbnailBlobName = BuildThumbnailBlobName(sourceBlobName);
        var thumbnailBlobClient = containerClient.GetBlobClient(thumbnailBlobName);

        await using var sourceBuffer = new MemoryStream();
        await sourceBlobClient.DownloadToAsync(sourceBuffer);
        sourceBuffer.Position = 0;

        using var image = await Image.LoadAsync(sourceBuffer);
        image.Mutate(ctx => ctx.Resize(new ResizeOptions
        {
            Mode = ResizeMode.Max,
            Size = new Size(ThumbnailMaxWidth, ThumbnailMaxWidth)
        }));

        await using var thumbnailBuffer = new MemoryStream();
        var jpegEncoder = new JpegEncoder
        {
            Quality = ThumbnailJpegQuality
        };
        await image.SaveAsync(thumbnailBuffer, jpegEncoder);
        var thumbnailSizeBytes = thumbnailBuffer.Length;
        thumbnailBuffer.Position = 0;

        await thumbnailBlobClient.UploadAsync(
            thumbnailBuffer,
            new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = "image/jpeg"
                }
            });

        return new IBlobStorageService.ThumbnailResult(
            BlobName: thumbnailBlobName,
            Url: GetPublicBlobUrl(thumbnailBlobName),
            Width: image.Width,
            Height: image.Height,
            SizeBytes: thumbnailSizeBytes);
    }

    private static string BuildThumbnailBlobName(string sourceBlobName)
    {
        var filename = Path.GetFileNameWithoutExtension(sourceBlobName);
        return $"{filename}-thumb.jpg";
    }
}
