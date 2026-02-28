using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using PhotoFunctions.Models;
using PhotoFunctions.Services;

namespace PhotoFunctions.Functions;

public sealed class ManageFolderTreeFunctions
{
    private readonly IJwtValidationService _jwtService;
    private readonly IFolderTreeService _folderTreeService;

    public ManageFolderTreeFunctions(
        IJwtValidationService jwtService,
        IFolderTreeService folderTreeService)
    {
        _jwtService = jwtService;
        _folderTreeService = folderTreeService;
    }

    [Function("manage-folder-tree-get")]
    public async Task<IActionResult> Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "manage-folder-tree")] HttpRequest req)
    {
        var (_, authError) = await AuthorizeAsync(req);
        if (authError is not null)
            return authError;

        var document = await _folderTreeService.GetDocumentAsync();
        return new OkObjectResult(new ManageFolderTreeResponse(
            Root: document.Root,
            FolderPaths: document.FolderPaths.ToArray(),
            TagRules: document.TagRules,
            RootLabels: document.RootLabels));
    }

    [Function("manage-folder-tree-put")]
    public async Task<IActionResult> Put(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "manage-folder-tree")] HttpRequest req)
    {
        var (_, authError) = await AuthorizeAsync(req);
        if (authError is not null)
            return authError;

        ManageFolderTreeRequest? body;
        try
        {
            body = await req.ReadFromJsonAsync<ManageFolderTreeRequest>();
        }
        catch
        {
            return new BadRequestObjectResult(new ErrorResponse("Invalid request body"));
        }

        if (body is null)
            return new BadRequestObjectResult(new ErrorResponse("Request body is required"));

        var document = new FolderTreeDocument
        {
            Root = "photos",
            FolderPaths = body.FolderPaths?.ToList() ?? ["photos"],
            TagRules = body.TagRules ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            RootLabels = body.RootLabels ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
        };

        await _folderTreeService.SaveDocumentAsync(document);
        var persisted = await _folderTreeService.GetDocumentAsync();
        return new OkObjectResult(new ManageFolderTreeResponse(
            Root: persisted.Root,
            FolderPaths: persisted.FolderPaths.ToArray(),
            TagRules: persisted.TagRules,
            RootLabels: persisted.RootLabels));
    }

    private async Task<(ClaimsPrincipal? principal, IActionResult? error)> AuthorizeAsync(HttpRequest req)
    {
        var token = _jwtService.ExtractBearerToken(
            req.Headers.Authorization.FirstOrDefault());

        if (token is null)
            return (null, new UnauthorizedObjectResult(new ErrorResponse("No token provided")));

        var principal = await _jwtService.ValidateTokenAsync(token);
        if (principal is null)
            return (null, new UnauthorizedObjectResult(new ErrorResponse("Invalid token")));

        var oid = principal.FindFirst("oid")?.Value
                  ?? principal.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;

        if (string.IsNullOrEmpty(oid) || !_jwtService.IsAuthorizedUser(oid))
            return (null, new ObjectResult(new ErrorResponse("User not authorized")) { StatusCode = StatusCodes.Status403Forbidden });

        return (principal, null);
    }
}
