namespace PhotoFunctions.Configuration;

/// <summary>Configuration for Azure Storage (Blobs + Tables).</summary>
public sealed class AzureStorageOptions
{
    public const string SectionName = "AzureStorage";

    public string AccountName { get; set; } = string.Empty;
    public string AccountKey { get; set; } = string.Empty;
    public string PhotoContainerName { get; set; } = "photos";
    public string PhotoTableName { get; set; } = "photos";

    /// <summary>
    /// When true, read SAS tokens are generated for each image URL.
    /// When false, the blob container is assumed to have public read access.
    /// </summary>
    public bool UsePrivateContainer { get; set; }

    // ----- Derived helpers -----

    public string BlobServiceUri => $"https://{AccountName}.blob.core.windows.net";
    public string TableServiceUri => $"https://{AccountName}.table.core.windows.net";
}
