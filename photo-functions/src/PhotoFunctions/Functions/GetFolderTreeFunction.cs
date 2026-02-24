using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using PhotoFunctions.Models;
using PhotoFunctions.Services;

namespace PhotoFunctions.Functions;

public sealed class GetFolderTreeFunction
{
    private readonly IFolderTreeService _folderTreeService;

    public GetFolderTreeFunction(IFolderTreeService folderTreeService)
    {
        _folderTreeService = folderTreeService;
    }

    [Function("folder-tree")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "folder-tree")] HttpRequest req)
    {
        var document = await _folderTreeService.GetDocumentAsync();
        var response = new FolderTreeResponse(
            Root: document.Root,
            TagRules: document.TagRules,
            Tree: _folderTreeService.BuildTree(document));
        return new OkObjectResult(response);
    }
}
