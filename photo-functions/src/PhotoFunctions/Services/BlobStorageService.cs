using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using Microsoft.Extensions.Options;
using PhotoFunctions.Configuration;

namespace PhotoFunctions.Services;

/// <summary>
/// Handles Azure Blob Storage operations: SAS URL generation for upload and read.
/// </summary>
public sealed class BlobStorageService : IBlobStorageService
{
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
}
