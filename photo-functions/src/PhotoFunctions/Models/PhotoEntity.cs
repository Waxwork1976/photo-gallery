using Azure;
using Azure.Data.Tables;

namespace PhotoFunctions.Models;

/// <summary>
/// Represents a photo entity in Azure Table Storage.
/// PartitionKey: "gallery" (single partition for low-volume personal gallery).
/// RowKey: GUID (unique image identifier).
/// </summary>
public sealed class PhotoEntity : ITableEntity
{
    // ----- Table Storage required fields -----
    public string PartitionKey { get; set; } = "gallery";
    public string RowKey { get; set; } = Guid.NewGuid().ToString();
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    // ----- Blob reference -----
    public string BlobName { get; set; } = string.Empty;
    public string BlobUrl { get; set; } = string.Empty;
    public string ThumbnailBlobName { get; set; } = string.Empty;
    public string ThumbnailBlobUrl { get; set; } = string.Empty;
    public string OriginalFilename { get; set; } = string.Empty;

    // ----- User-provided metadata -----
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    /// <summary>Comma-separated tags (Table Storage has no native array type).</summary>
    public string Tags { get; set; } = string.Empty;

    // ----- System metadata -----
    public string UploadedAt { get; set; } = DateTimeOffset.UtcNow.ToString("o");
    public string UploadedBy { get; set; } = string.Empty;

    // ----- Image metadata -----
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }

    // ----- Status flags -----
    public bool IsPublic { get; set; } = true;
    public bool Featured { get; set; }
    public int SortOrder { get; set; }
}
