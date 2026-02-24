namespace PhotoFunctions.Services;

public interface IBlobStorageService
{
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

    Task<string?> ReadTextBlobAsync(string containerName, string blobName);
    Task WriteTextBlobAsync(string containerName, string blobName, string content, string contentType = "application/json");
}
