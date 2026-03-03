namespace PhotoFunctions.Configuration;

/// <summary>
/// Configuration for background species-enrichment queue processing.
/// </summary>
public sealed class SpeciesEnrichmentOptions
{
    public const string SectionName = "SpeciesEnrichment";

    public string QueueName { get; set; } = "species-enrichment-queue";
}
