using PhotoFunctions.Models;

namespace PhotoFunctions.Services;

public interface IBirdIdentificationService
{
    Task<BirdPredictionItem[]> IdentifyAsync(Stream content, string fileName, string contentType);
}
