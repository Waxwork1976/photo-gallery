using Azure.Data.Tables;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PhotoFunctions.Configuration;
using PhotoFunctions.Models;

namespace PhotoFunctions.Services;

/// <summary>
/// Handles CRUD operations on the photos Azure Table.
/// </summary>
public sealed class PhotoTableService : IPhotoTableService
{
    private const string DefaultPartitionKey = "gallery";

    private readonly TableClient _tableClient;
    private readonly ILogger<PhotoTableService> _logger;

    public PhotoTableService(
        IOptions<AzureStorageOptions> options,
        ILogger<PhotoTableService> logger)
    {
        _logger = logger;

        var storageOptions = options.Value;
        var credential = new TableSharedKeyCredential(
            storageOptions.AccountName,
            storageOptions.AccountKey);

        _tableClient = new TableClient(
            new Uri($"{storageOptions.TableServiceUri}/{storageOptions.PhotoTableName}"),
            storageOptions.PhotoTableName,
            credential);
    }

    /// <inheritdoc />
    public async Task<PhotoEntity> InsertAsync(PhotoEntity entity)
    {
        entity.PartitionKey = DefaultPartitionKey;
        if (string.IsNullOrEmpty(entity.RowKey))
            entity.RowKey = Guid.NewGuid().ToString();

        await _tableClient.AddEntityAsync(entity);

        _logger.LogInformation("Inserted photo entity {RowKey} for blob {BlobName}",
            entity.RowKey, entity.BlobName);

        return entity;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PhotoEntity>> ListAsync(bool featuredOnly = false)
    {
        string filter = featuredOnly
            ? $"PartitionKey eq '{DefaultPartitionKey}' and IsPublic eq true and Featured eq true"
            : $"PartitionKey eq '{DefaultPartitionKey}' and IsPublic eq true";

        var entities = new List<PhotoEntity>();

        await foreach (var entity in _tableClient.QueryAsync<PhotoEntity>(filter))
        {
            entities.Add(entity);
        }

        // Sort newest first
        entities.Sort((a, b) =>
            string.Compare(b.UploadedAt, a.UploadedAt, StringComparison.Ordinal));

        _logger.LogInformation("Listed {Count} photo entities (featuredOnly={FeaturedOnly})",
            entities.Count, featuredOnly);

        return entities;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PhotoEntity>> ListAllAsync()
    {
        string filter = $"PartitionKey eq '{DefaultPartitionKey}'";

        var entities = new List<PhotoEntity>();
        await foreach (var entity in _tableClient.QueryAsync<PhotoEntity>(filter))
        {
            entities.Add(entity);
        }

        entities.Sort((a, b) =>
            string.Compare(b.UploadedAt, a.UploadedAt, StringComparison.Ordinal));

        _logger.LogInformation("Listed all {Count} photo entities (admin)", entities.Count);
        return entities;
    }

    /// <inheritdoc />
    public async Task<PhotoEntity?> GetAsync(string rowKey)
    {
        try
        {
            var response = await _tableClient.GetEntityAsync<PhotoEntity>(DefaultPartitionKey, rowKey);
            return response.Value;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<PhotoEntity> UpdateAsync(PhotoEntity entity)
    {
        await _tableClient.UpdateEntityAsync(entity, entity.ETag, TableUpdateMode.Merge);
        _logger.LogInformation("Updated photo entity {RowKey}", entity.RowKey);
        return entity;
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string rowKey)
    {
        await _tableClient.DeleteEntityAsync(DefaultPartitionKey, rowKey);
        _logger.LogInformation("Deleted photo entity {RowKey}", rowKey);
    }
}
