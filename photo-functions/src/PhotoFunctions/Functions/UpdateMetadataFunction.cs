using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using PhotoFunctions.Models;
using PhotoFunctions.Services;

namespace PhotoFunctions.Functions;

/// <summary>
/// PUT /api/update-metadata/{id}
/// Updates title, description and tags of an existing photo.
/// Protected: requires a valid JWT from an authorized user.
/// </summary>
public sealed class UpdateMetadataFunction
{
    private readonly IJwtValidationService _jwtService;
    private readonly IPhotoTableService _tableService;
    private readonly IFolderTreeService _folderTreeService;
    private readonly ISpeciesEnrichmentQueueService _speciesEnrichmentQueueService;
    private readonly ILogger<UpdateMetadataFunction> _logger;

    public UpdateMetadataFunction(
        IJwtValidationService jwtService,
        IPhotoTableService tableService,
        IFolderTreeService folderTreeService,
        ISpeciesEnrichmentQueueService speciesEnrichmentQueueService,
        ILogger<UpdateMetadataFunction> logger)
    {
        _jwtService = jwtService;
        _tableService = tableService;
        _folderTreeService = folderTreeService;
        _speciesEnrichmentQueueService = speciesEnrichmentQueueService;
        _logger = logger;
    }

    [Function("update-metadata")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "update-metadata/{id}")] HttpRequest req,
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

        UpdateMetadataRequest? body;
        try
        {
            body = await req.ReadFromJsonAsync<UpdateMetadataRequest>();
        }
        catch
        {
            return new BadRequestObjectResult(new ErrorResponse("Invalid request body"));
        }

        if (body is null)
            return new BadRequestObjectResult(new ErrorResponse("Request body is required"));

        entity.Title = body.Title;
        entity.Description = body.Description;
        entity.Tags = string.Join(",", body.Tags ?? []);
        if (body.SpeciesType is not null)
            entity.SpeciesType = SpeciesMetadata.NormalizeSpeciesType(body.SpeciesType);
        if (body.ScientificName is not null)
            entity.ScientificName = SpeciesMetadata.NormalizeText(body.ScientificName);
        if (body.TaxonomyOrder is not null)
            entity.TaxonomyOrder = SpeciesMetadata.NormalizeText(body.TaxonomyOrder);
        if (body.TaxonomyFamily is not null)
            entity.TaxonomyFamily = SpeciesMetadata.NormalizeText(body.TaxonomyFamily);
        if (body.TaxonomyGenus is not null)
            entity.TaxonomyGenus = SpeciesMetadata.NormalizeText(body.TaxonomyGenus);
        if (body.CommonNames is not null)
            entity.CommonNamesJson = SpeciesMetadata.SerializeCommonNames(body.CommonNames);
        var folderDoc = await _folderTreeService.GetDocumentAsync();
        var resolved = _folderTreeService.ResolveFoldersFromTags(body.Tags ?? [], folderDoc);
        entity.PrimaryFolderPath = resolved.PrimaryFolderPath;
        entity.FolderPathsCsv = string.Join(",", resolved.FolderPaths);

        await _tableService.UpdateAsync(entity);
        await TryEnqueuePlantEnrichmentAsync(entity, req.HttpContext.RequestAborted);

        _logger.LogInformation("Updated metadata for photo {RowKey}", id);

        return new OkObjectResult(new { success = true, id });
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

    private async Task TryEnqueuePlantEnrichmentAsync(PhotoEntity entity, CancellationToken cancellationToken)
    {
        if (!string.Equals(entity.SpeciesType, "plant", StringComparison.Ordinal))
            return;
        if (string.IsNullOrWhiteSpace(entity.ScientificName))
            return;

        var message = new SpeciesEnrichmentQueueMessage(
            ImageId: entity.RowKey,
            SpeciesType: "plant",
            ScientificName: entity.ScientificName,
            TaxonomyOrder: entity.TaxonomyOrder,
            TaxonomyFamily: entity.TaxonomyFamily,
            TaxonomyGenus: entity.TaxonomyGenus,
            CorrelationId: Guid.NewGuid().ToString("n"));

        try
        {
            await _speciesEnrichmentQueueService.EnqueueAsync(message, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to enqueue plant enrichment for image {ImageId}", entity.RowKey);
        }
    }
}
