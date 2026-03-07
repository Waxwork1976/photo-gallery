using PhotoFunctions.Models;

namespace PhotoFunctions.Services;

public interface ITaxonomySuggestionService
{
    Task<TaxonomySuggestionEntity> InsertAsync(TaxonomySuggestionEntity entity);
    Task<IReadOnlyList<TaxonomySuggestionEntity>> ListPendingAsync();
    Task<int> CountPendingAsync();
    Task<TaxonomySuggestionEntity?> GetAsync(string rowKey);
    Task DeleteAsync(string rowKey);
    Task<int> DeleteByImageIdAsync(string imageId);
}
