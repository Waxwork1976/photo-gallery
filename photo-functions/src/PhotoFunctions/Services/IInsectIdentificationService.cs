using PhotoFunctions.Models;

namespace PhotoFunctions.Services;

public interface IInsectIdentificationService
{
    Task<InsectIdentificationResponse> IdentifyAsync(
        Stream content,
        string fileName,
        string contentType,
        string locale,
        CancellationToken cancellationToken = default);
}
