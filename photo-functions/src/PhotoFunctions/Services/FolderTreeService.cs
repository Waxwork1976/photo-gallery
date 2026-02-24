using System.Text.Json;
using PhotoFunctions.Models;

namespace PhotoFunctions.Services;

public sealed class FolderTreeService : IFolderTreeService
{
    private const string ContainerName = "folder-structure";
    private const string BlobName = "photos/tree.json";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private readonly IBlobStorageService _blobStorage;

    public FolderTreeService(IBlobStorageService blobStorage)
    {
        _blobStorage = blobStorage;
    }

    public async Task<FolderTreeDocument> GetDocumentAsync()
    {
        var text = await _blobStorage.ReadTextBlobAsync(ContainerName, BlobName);
        if (string.IsNullOrWhiteSpace(text))
        {
            var defaultDoc = CreateDefaultDocument();
            await SaveDocumentAsync(defaultDoc);
            return defaultDoc;
        }

        var parsed = JsonSerializer.Deserialize<FolderTreeDocument>(text, JsonOptions) ?? CreateDefaultDocument();
        NormalizeDocument(parsed);
        return parsed;
    }

    public async Task SaveDocumentAsync(FolderTreeDocument document)
    {
        NormalizeDocument(document);
        var json = JsonSerializer.Serialize(document, JsonOptions);
        await _blobStorage.WriteTextBlobAsync(ContainerName, BlobName, json);
    }

    public FolderTreeNodeDto[] BuildTree(FolderTreeDocument document)
    {
        NormalizeDocument(document);
        var rootNode = new Node("photos", "photos");

        foreach (var path in document.FolderPaths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var normalizedPath = NormalizePath(path);
            if (!normalizedPath.StartsWith("photos/", StringComparison.OrdinalIgnoreCase))
                continue;

            var segments = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var current = rootNode;
            var accPath = "photos";

            for (var i = 1; i < segments.Length; i++)
            {
                var segment = segments[i];
                accPath = $"{accPath}/{segment}";

                if (!current.Children.TryGetValue(segment, out var child))
                {
                    child = new Node(segment, accPath);
                    current.Children[segment] = child;
                }
                current = child;
            }
        }

        return [Convert(rootNode)];
    }

    public (string PrimaryFolderPath, string[] FolderPaths) ResolveFoldersFromTags(string[] tags, FolderTreeDocument document)
    {
        NormalizeDocument(document);

        var matched = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawTag in tags)
        {
            var tag = rawTag?.Trim();
            if (string.IsNullOrWhiteSpace(tag))
                continue;

            if (document.TagRules.TryGetValue(tag, out var targetPath) && !string.IsNullOrWhiteSpace(targetPath))
            {
                matched.Add(NormalizePath(targetPath));
            }
        }

        if (matched.Count == 0)
            return ("photos", ["photos"]);

        var ordered = matched.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToArray();
        return (ordered[0], ordered);
    }

    private static FolderTreeDocument CreateDefaultDocument()
    {
        return new FolderTreeDocument
        {
            Root = "photos",
            FolderPaths = ["photos"],
            TagRules = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
        };
    }

    private static void NormalizeDocument(FolderTreeDocument document)
    {
        document.Root = "photos";
        document.FolderPaths ??= [];
        document.TagRules ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var normalizedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "photos" };
        foreach (var path in document.FolderPaths)
        {
            if (string.IsNullOrWhiteSpace(path))
                continue;
            normalizedPaths.Add(NormalizePath(path));
        }
        document.FolderPaths = normalizedPaths.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();

        var normalizedRules = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in document.TagRules)
        {
            if (string.IsNullOrWhiteSpace(kv.Key) || string.IsNullOrWhiteSpace(kv.Value))
                continue;
            normalizedRules[kv.Key.Trim()] = NormalizePath(kv.Value);
        }
        document.TagRules = normalizedRules;
    }

    private static string NormalizePath(string rawPath)
    {
        var p = rawPath.Trim().Replace("\\", "/");
        p = string.Join("/", p.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()));
        if (string.IsNullOrWhiteSpace(p))
            return "photos";
        if (!p.StartsWith("photos", StringComparison.OrdinalIgnoreCase))
            p = $"photos/{p}";
        return p;
    }

    private static FolderTreeNodeDto Convert(Node node)
    {
        var children = node.Children.Values
            .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .Select(Convert)
            .ToArray();
        return new FolderTreeNodeDto(node.Name, node.Path, children);
    }

    private sealed class Node
    {
        public Node(string name, string path)
        {
            Name = name;
            Path = path;
        }

        public string Name { get; }
        public string Path { get; }
        public Dictionary<string, Node> Children { get; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
