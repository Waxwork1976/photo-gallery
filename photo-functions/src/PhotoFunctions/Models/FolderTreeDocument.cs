namespace PhotoFunctions.Models;

public sealed class FolderTreeDocument
{
    public string Root { get; set; } = "photos";
    public List<string> FolderPaths { get; set; } = [];
    public Dictionary<string, string> TagRules { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
