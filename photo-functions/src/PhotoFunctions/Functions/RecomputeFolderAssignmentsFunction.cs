using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using PhotoFunctions.Models;
using PhotoFunctions.Services;

namespace PhotoFunctions.Functions;

public sealed class RecomputeFolderAssignmentsFunction
{
    private readonly IJwtValidationService _jwtService;
    private readonly IPhotoTableService _tableService;
    private readonly IFolderTreeService _folderTreeService;
    private readonly ILogger<RecomputeFolderAssignmentsFunction> _logger;

    public RecomputeFolderAssignmentsFunction(
        IJwtValidationService jwtService,
        IPhotoTableService tableService,
        IFolderTreeService folderTreeService,
        ILogger<RecomputeFolderAssignmentsFunction> logger)
    {
        _jwtService = jwtService;
        _tableService = tableService;
        _folderTreeService = folderTreeService;
        _logger = logger;
    }

    [Function("recompute-folder-assignments")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "recompute-folder-assignments")] HttpRequest req)
    {
        var (_, authError) = await AuthorizeAsync(req);
        if (authError is not null)
            return authError;

        var document = await _folderTreeService.GetDocumentAsync();
        var entities = await _tableService.ListAllAsync();

        var updated = 0;
        foreach (var entity in entities)
        {
            var tags = (entity.Tags ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            var resolved = _folderTreeService.ResolveFoldersFromTags(tags, document);
            var nextPrimary = resolved.PrimaryFolderPath;
            var nextCsv = string.Join(",", resolved.FolderPaths);

            if (entity.PrimaryFolderPath == nextPrimary && entity.FolderPathsCsv == nextCsv)
                continue;

            entity.PrimaryFolderPath = nextPrimary;
            entity.FolderPathsCsv = nextCsv;
            await _tableService.UpdateAsync(entity);
            updated++;
        }

        _logger.LogInformation("Recomputed folder assignments for {Updated}/{Total} images", updated, entities.Count);

        return new OkObjectResult(new
        {
            success = true,
            total = entities.Count,
            updated,
        });
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
