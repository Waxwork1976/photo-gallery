namespace PhotoFunctions.Services;

public interface ISpeciesCommonNameService
{
    Task<Dictionary<string, string>> GetCommonNamesAsync(
        string speciesType,
        string scientificName,
        string? taxonomyOrder = null,
        string? taxonomyFamily = null,
        string? taxonomyGenus = null,
        CancellationToken cancellationToken = default);
}
