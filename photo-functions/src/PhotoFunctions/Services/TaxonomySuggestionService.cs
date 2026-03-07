using Azure.Data.Tables;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PhotoFunctions.Configuration;
using PhotoFunctions.Models;

namespace PhotoFunctions.Services;

public sealed class TaxonomySuggestionService : ITaxonomySuggestionService
{
    private const string PartitionKey = "suggestions";

    private readonly TableClient _tableClient;
    private readonly ILogger<TaxonomySuggestionService> _logger;

    public TaxonomySuggestionService(
        IOptions<AzureStorageOptions> options,
        ILogger<TaxonomySuggestionService> logger)
    {
        _logger = logger;
        var storageOptions = options.Value;

        var credential = new TableSharedKeyCredential(
            storageOptions.AccountName,
            storageOptions.AccountKey);

        _tableClient = new TableClient(
            new Uri($"{storageOptions.TableServiceUri}/{storageOptions.TaxonomySuggestionTableName}"),
            storageOptions.TaxonomySuggestionTableName,
            credential);

        _tableClient.CreateIfNotExists();
    }

    public async Task<TaxonomySuggestionEntity> InsertAsync(TaxonomySuggestionEntity entity)
    {
        entity.PartitionKey = PartitionKey;
        if (string.IsNullOrWhiteSpace(entity.RowKey))
            entity.RowKey = Guid.NewGuid().ToString("n");
        if (string.IsNullOrWhiteSpace(entity.Status))
            entity.Status = "pending";
        if (string.IsNullOrWhiteSpace(entity.CreatedAt))
            entity.CreatedAt = DateTimeOffset.UtcNow.ToString("o");

        await _tableClient.AddEntityAsync(entity);
        _logger.LogInformation("Inserted taxonomy suggestion {SuggestionId} for image {ImageId}", entity.RowKey, entity.ImageId);
        return entity;
    }

    public async Task<IReadOnlyList<TaxonomySuggestionEntity>> ListPendingAsync()
    {
        var filter = $"PartitionKey eq '{PartitionKey}' and Status eq 'pending'";
        var entities = new List<TaxonomySuggestionEntity>();
        await foreach (var entity in _tableClient.QueryAsync<TaxonomySuggestionEntity>(filter))
        {
            entities.Add(entity);
        }

        entities.Sort((a, b) => string.Compare(b.CreatedAt, a.CreatedAt, StringComparison.Ordinal));
        return entities;
    }

    public async Task<int> CountPendingAsync()
    {
        var filter = $"PartitionKey eq '{PartitionKey}' and Status eq 'pending'";
        var count = 0;
        await foreach (var _ in _tableClient.QueryAsync<TaxonomySuggestionEntity>(filter))
        {
            count++;
        }
        return count;
    }

    public async Task<TaxonomySuggestionEntity?> GetAsync(string rowKey)
    {
        try
        {
            var response = await _tableClient.GetEntityAsync<TaxonomySuggestionEntity>(PartitionKey, rowKey);
            return response.Value;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task DeleteAsync(string rowKey)
    {
        await _tableClient.DeleteEntityAsync(PartitionKey, rowKey);
        _logger.LogInformation("Deleted taxonomy suggestion {SuggestionId}", rowKey);
    }

    public async Task<int> DeleteByImageIdAsync(string imageId)
    {
        if (string.IsNullOrWhiteSpace(imageId))
            return 0;

        var normalized = imageId.Trim().Replace("'", "''");
        var filter = $"PartitionKey eq '{PartitionKey}' and ImageId eq '{normalized}'";
        var deleted = 0;

        await foreach (var entity in _tableClient.QueryAsync<TaxonomySuggestionEntity>(filter))
        {
            await _tableClient.DeleteEntityAsync(entity.PartitionKey, entity.RowKey);
            deleted++;
        }

        if (deleted > 0)
        {
            _logger.LogWarning("Deleted {Count} taxonomy suggestion(s) linked to image {ImageId}", deleted, imageId);
        }

        return deleted;
    }
}
