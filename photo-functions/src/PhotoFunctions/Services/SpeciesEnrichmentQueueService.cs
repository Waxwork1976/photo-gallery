using System.Text.Json;
using Azure.Storage;
using Azure.Storage.Queues;
using Microsoft.Extensions.Options;
using PhotoFunctions.Configuration;
using PhotoFunctions.Models;

namespace PhotoFunctions.Services;

public sealed class SpeciesEnrichmentQueueService : ISpeciesEnrichmentQueueService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly QueueClient _queueClient;

    public SpeciesEnrichmentQueueService(
        IOptions<AzureStorageOptions> storageOptions,
        IOptions<SpeciesEnrichmentOptions> enrichmentOptions)
    {
        var storage = storageOptions.Value;
        var queueName = NormalizeQueueName(enrichmentOptions.Value.QueueName);
        var credential = new StorageSharedKeyCredential(storage.AccountName, storage.AccountKey);
        var queueUri = new Uri($"https://{storage.AccountName}.queue.core.windows.net/{queueName}");
        _queueClient = new QueueClient(queueUri, credential);
    }

    public async Task EnqueueAsync(SpeciesEnrichmentQueueMessage message, CancellationToken cancellationToken = default)
    {
        await _queueClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        var payload = JsonSerializer.Serialize(message with
        {
            EnqueuedAtUtc = message.EnqueuedAtUtc ?? DateTimeOffset.UtcNow.ToString("o")
        }, JsonOptions);

        await _queueClient.SendMessageAsync(payload, cancellationToken: cancellationToken);
    }

    private static string NormalizeQueueName(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        return string.IsNullOrWhiteSpace(normalized)
            ? "species-enrichment-queue"
            : normalized;
    }
}
