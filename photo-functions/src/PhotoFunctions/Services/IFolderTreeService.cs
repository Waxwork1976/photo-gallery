using PhotoFunctions.Models;

namespace PhotoFunctions.Services;

public interface IFolderTreeService
{
    Task<FolderTreeDocument> GetDocumentAsync();
    Task SaveDocumentAsync(FolderTreeDocument document);
    FolderTreeNodeDto[] BuildTree(FolderTreeDocument document);
    (string PrimaryFolderPath, string[] FolderPaths) ResolveFoldersFromTags(string[] tags, FolderTreeDocument document);
}
