namespace PhotoFunctions.Models;

/// <summary>
/// Queue contract for asynchronous species common-name enrichment.
/// </summary>
public sealed record SpeciesEnrichmentQueueMessage(
    string ImageId,
    string SpeciesType,
    string ScientificName,
    string? TaxonomyOrder = null,
    string? TaxonomyFamily = null,
    string? TaxonomyGenus = null,
    string? CorrelationId = null,
    string? EnqueuedAtUtc = null
);
