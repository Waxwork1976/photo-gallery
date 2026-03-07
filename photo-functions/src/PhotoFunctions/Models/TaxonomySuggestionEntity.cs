using Azure;
using Azure.Data.Tables;

namespace PhotoFunctions.Models;

/// <summary>
/// Represents a taxonomy change suggestion stored for manager review.
/// </summary>
public sealed class TaxonomySuggestionEntity : ITableEntity
{
    public string PartitionKey { get; set; } = "suggestions";
    public string RowKey { get; set; } = Guid.NewGuid().ToString("n");
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string ImageId { get; set; } = string.Empty;
    public string ImageTitle { get; set; } = string.Empty;

    public string SourceOrder { get; set; } = string.Empty;
    public string SourceFamily { get; set; } = string.Empty;
    public string SourceGenus { get; set; } = string.Empty;
    public string SourceScientificName { get; set; } = string.Empty;
    public string SourceCommonName { get; set; } = string.Empty;

    public string SuggestedOrder { get; set; } = string.Empty;
    public string SuggestedFamily { get; set; } = string.Empty;
    public string SuggestedGenus { get; set; } = string.Empty;
    public string SuggestedScientificName { get; set; } = string.Empty;
    public string SuggestedCommonName { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;

    public bool RequesterAuthenticated { get; set; }
    public string RequesterName { get; set; } = string.Empty;
    public string RequesterEmail { get; set; } = string.Empty;
    public string RequesterLocale { get; set; } = "en";

    public string CaptchaToken { get; set; } = string.Empty;
    public string Status { get; set; } = "pending";
    public string CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToString("o");
}
