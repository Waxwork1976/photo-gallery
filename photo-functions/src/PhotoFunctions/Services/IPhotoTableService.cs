using PhotoFunctions.Models;

namespace PhotoFunctions.Services;

public interface IPhotoTableService
{
    Task<PhotoEntity> InsertAsync(PhotoEntity entity);

    /// <summary>
    /// Lists public photos, optionally filtered to featured-only.
    /// </summary>
    Task<IReadOnlyList<PhotoEntity>> ListAsync(bool featuredOnly = false);

    /// <summary>
    /// Lists all photos regardless of visibility. For admin use.
    /// </summary>
    Task<IReadOnlyList<PhotoEntity>> ListAllAsync();

    Task<PhotoEntity?> GetAsync(string rowKey);

    Task<PhotoEntity> UpdateAsync(PhotoEntity entity);

    Task DeleteAsync(string rowKey);
}
