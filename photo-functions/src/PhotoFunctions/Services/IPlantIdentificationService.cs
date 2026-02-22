using System.Text.Json;

namespace PhotoFunctions.Services;

public interface IPlantIdentificationService
{
    /// <summary>
    /// Identifies a plant from one or more images by calling the PlantNet API.
    /// Returns the raw JSON response from PlantNet.
    /// </summary>
    Task<JsonElement> IdentifyAsync(
        IReadOnlyList<(Stream Content, string FileName, string ContentType)> images,
        IReadOnlyList<string>? organs = null);
}
