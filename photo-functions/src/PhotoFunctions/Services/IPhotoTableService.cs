using PhotoFunctions.Models;

namespace PhotoFunctions.Services;

public interface IPhotoTableService
{
    /// <summary>
    /// Inserts a new photo entity into Table Storage.
    /// </summary>
    Task<PhotoEntity> InsertAsync(PhotoEntity entity);

    /// <summary>
    /// Lists photos, optionally filtered to featured-only.
    /// Returns entities sorted newest-first.
    /// </summary>
    Task<IReadOnlyList<PhotoEntity>> ListAsync(bool featuredOnly = false);

    /// <summary>
    /// Gets a single photo by its RowKey.
    /// </summary>
    Task<PhotoEntity?> GetAsync(string rowKey);
}
