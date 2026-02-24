namespace PhotoFunctions.Services;

public interface IBlobStorageService
{
    /// <summary>
    /// Result of creating a thumbnail variant for an existing source blob.
    /// </summary>
    public sealed record ThumbnailResult(
        string BlobName,
        string Url,
        int Width,
        int Height,
        long SizeBytes
    );

    /// <summary>
    /// Generates a SAS URL with write-only permission for uploading a blob.
    /// Returns the SAS URL and the unique blob name.
    /// </summary>
    (string SasUrl, string BlobName) GenerateUploadSasUrl(string originalFilename, string contentType);

    /// <summary>
    /// Generates a SAS URL with read-only permission for downloading/viewing a blob.
    /// </summary>
    string GenerateReadSasUrl(string blobName, TimeSpan? expiry = null);

    /// <summary>
    /// Returns the public URL for a blob (no SAS token).
    /// </summary>
    string GetPublicBlobUrl(string blobName);

    /// <summary>
    /// Deletes a blob from the photo container.
    /// </summary>
    Task DeleteBlobAsync(string blobName);

    /// <summary>
    /// Creates and uploads a lightweight thumbnail for an existing blob.
    /// </summary>
    Task<ThumbnailResult> CreateThumbnailAsync(string sourceBlobName, string contentType);
}
