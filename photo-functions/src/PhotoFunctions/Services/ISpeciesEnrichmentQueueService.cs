using PhotoFunctions.Models;

namespace PhotoFunctions.Services;

public interface ISpeciesEnrichmentQueueService
{
    Task EnqueueAsync(SpeciesEnrichmentQueueMessage message, CancellationToken cancellationToken = default);
}
