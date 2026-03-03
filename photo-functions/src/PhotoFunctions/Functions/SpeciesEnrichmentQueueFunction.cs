using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using PhotoFunctions.Models;
using PhotoFunctions.Services;

namespace PhotoFunctions.Functions;

/// <summary>
/// Queue-triggered worker that enriches multilingual common names asynchronously.
/// </summary>
public sealed class SpeciesEnrichmentQueueFunction
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IPhotoTableService _tableService;
    private readonly ISpeciesCommonNameService _commonNameService;
    private readonly ILogger<SpeciesEnrichmentQueueFunction> _logger;

    public SpeciesEnrichmentQueueFunction(
        IPhotoTableService tableService,
        ISpeciesCommonNameService commonNameService,
        ILogger<SpeciesEnrichmentQueueFunction> logger)
    {
        _tableService = tableService;
        _commonNameService = commonNameService;
        _logger = logger;
    }

    [Function("species-enrichment-worker")]
    public async Task Run(
        [QueueTrigger("%SpeciesEnrichment__QueueName%")]
        string payload,
        CancellationToken cancellationToken)
    {
        SpeciesEnrichmentQueueMessage? message;
        try
        {
            message = JsonSerializer.Deserialize<SpeciesEnrichmentQueueMessage>(payload, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Invalid species enrichment queue payload");
            return;
        }

        if (message is null || string.IsNullOrWhiteSpace(message.ImageId))
        {
            _logger.LogWarning("Species enrichment message missing image id");
            return;
        }

        var speciesType = SpeciesMetadata.NormalizeSpeciesType(message.SpeciesType);
        if (!string.Equals(speciesType, "plant", StringComparison.Ordinal))
        {
            _logger.LogInformation(
                "Skipping enrichment for image {ImageId}: unsupported species type '{SpeciesType}'",
                message.ImageId,
                message.SpeciesType);
            return;
        }

        var scientificNameFromMessage = SpeciesMetadata.NormalizeText(message.ScientificName);
        if (string.IsNullOrWhiteSpace(scientificNameFromMessage))
        {
            _logger.LogInformation(
                "Skipping enrichment for image {ImageId}: scientific name is empty",
                message.ImageId);
            return;
        }

        var entity = await _tableService.GetAsync(message.ImageId);
        if (entity is null)
        {
            _logger.LogWarning("Photo entity {ImageId} not found for enrichment", message.ImageId);
            return;
        }

        var scientificNameForLookup = FirstNonEmpty(entity.ScientificName, scientificNameFromMessage);
        var nextOrder = FirstNonEmpty(entity.TaxonomyOrder, SpeciesMetadata.NormalizeText(message.TaxonomyOrder));
        var nextFamily = FirstNonEmpty(entity.TaxonomyFamily, SpeciesMetadata.NormalizeText(message.TaxonomyFamily));
        var nextGenus = FirstNonEmpty(entity.TaxonomyGenus, SpeciesMetadata.NormalizeText(message.TaxonomyGenus));

        var names = await _commonNameService.GetCommonNamesAsync(
            speciesType: "plant",
            scientificName: scientificNameForLookup,
            taxonomyOrder: nextOrder,
            taxonomyFamily: nextFamily,
            taxonomyGenus: nextGenus,
            cancellationToken: cancellationToken);

        var nextCommonNamesJson = SpeciesMetadata.SerializeCommonNames(names);

        var changed = false;
        changed |= SetIfDifferent(entity.SpeciesType, "plant", value => entity.SpeciesType = value);
        changed |= SetIfDifferent(entity.ScientificName, scientificNameForLookup, value => entity.ScientificName = value);
        changed |= SetIfDifferent(entity.TaxonomyOrder, nextOrder, value => entity.TaxonomyOrder = value);
        changed |= SetIfDifferent(entity.TaxonomyFamily, nextFamily, value => entity.TaxonomyFamily = value);
        changed |= SetIfDifferent(entity.TaxonomyGenus, nextGenus, value => entity.TaxonomyGenus = value);
        if (!string.IsNullOrWhiteSpace(nextCommonNamesJson) || string.IsNullOrWhiteSpace(entity.CommonNamesJson))
            changed |= SetIfDifferent(entity.CommonNamesJson, nextCommonNamesJson, value => entity.CommonNamesJson = value);

        if (!changed)
        {
            _logger.LogInformation("Skipping enrichment update for image {ImageId}: no changes", message.ImageId);
            return;
        }

        await _tableService.UpdateAsync(entity);
        _logger.LogInformation(
            "Applied async enrichment for image {ImageId} (correlationId={CorrelationId})",
            message.ImageId,
            message.CorrelationId ?? string.Empty);
    }

    private static bool SetIfDifferent(string current, string next, Action<string> apply)
    {
        var normalizedNext = next ?? string.Empty;
        if (string.Equals(current ?? string.Empty, normalizedNext, StringComparison.Ordinal))
            return false;

        apply(normalizedNext);
        return true;
    }

    private static string FirstNonEmpty(string preferred, string fallback) =>
        string.IsNullOrWhiteSpace(preferred) ? (fallback ?? string.Empty) : preferred;
}
