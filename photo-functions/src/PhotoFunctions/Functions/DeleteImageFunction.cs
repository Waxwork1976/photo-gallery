using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using PhotoFunctions.Models;
using PhotoFunctions.Services;

namespace PhotoFunctions.Functions;

/// <summary>
/// DELETE /api/delete-image/{id}
/// Deletes a photo: removes the blob from storage and the entity from Table Storage.
/// Protected: requires a valid JWT from an authorized user.
/// </summary>
public sealed class DeleteImageFunction
{
    private readonly IJwtValidationService _jwtService;
    private readonly IPhotoTableService _tableService;
    private readonly IBlobStorageService _blobService;
    private readonly IFolderTreeService _folderTreeService;
    private readonly ITaxonomySuggestionService _taxonomySuggestionService;
    private readonly ILogger<DeleteImageFunction> _logger;

    public DeleteImageFunction(
        IJwtValidationService jwtService,
        IPhotoTableService tableService,
        IBlobStorageService blobService,
        IFolderTreeService folderTreeService,
        ITaxonomySuggestionService taxonomySuggestionService,
        ILogger<DeleteImageFunction> logger)
    {
        _jwtService = jwtService;
        _tableService = tableService;
        _blobService = blobService;
        _folderTreeService = folderTreeService;
        _taxonomySuggestionService = taxonomySuggestionService;
        _logger = logger;
    }

    [Function("delete-image")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "delete-image/{id}")] HttpRequest req,
        string id)
    {
        var (_, authError) = await AuthorizeAsync(req);
        if (authError is not null)
            return authError;

        if (string.IsNullOrWhiteSpace(id))
            return new BadRequestObjectResult(new ErrorResponse("Missing image id"));

        var entity = await _tableService.GetAsync(id);
        if (entity is null)
            return new NotFoundObjectResult(new ErrorResponse("Image not found"));

        if (!string.IsNullOrEmpty(entity.BlobName))
        {
            await _blobService.DeleteBlobAsync(entity.BlobName);
            _logger.LogInformation("Deleted blob {BlobName} for photo {RowKey}", entity.BlobName, id);
        }

        if (!string.IsNullOrEmpty(entity.ThumbnailBlobName))
        {
            await _blobService.DeleteBlobAsync(entity.ThumbnailBlobName);
            _logger.LogInformation("Deleted thumbnail blob {BlobName} for photo {RowKey}", entity.ThumbnailBlobName, id);
        }

        await _tableService.DeleteAsync(id);
        var deletedSuggestionCount = await _taxonomySuggestionService.DeleteByImageIdAsync(id);

        await RecomputeFolderAssignmentsBestEffortAsync();

        _logger.LogInformation("Deleted photo {RowKey}", id);

        string? warning = null;
        if (deletedSuggestionCount > 0)
        {
            warning = $"Deleted {deletedSuggestionCount} linked taxonomy suggestion(s) for this image.";
        }

        return new OkObjectResult(new
        {
            success = true,
            id,
            deletedSuggestionCount,
            warning,
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

    private async Task RecomputeFolderAssignmentsBestEffortAsync()
    {
        try
        {
            var entities = await _tableService.ListAllAsync();
            var tagsByImage = entities
                .Select(entity => (entity.Tags ?? string.Empty)
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .ToArray();

            // Keep tree/rules in sync with current tags before recomputing assignments.
            var regenerated = _folderTreeService.GenerateDocumentFromImageTags(tagsByImage);
            await _folderTreeService.SaveDocumentAsync(regenerated);
            var document = await _folderTreeService.GetDocumentAsync();

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

            _logger.LogInformation("Auto-recomputed folder assignments after delete: {Updated}/{Total}", updated, entities.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Folder assignment auto-recompute failed after delete");
        }
    }
}
