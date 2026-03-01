using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using PhotoFunctions.Models;
using PhotoFunctions.Services;

namespace PhotoFunctions.Functions;

public sealed class RecomputeCommonNamesFunction
{
    private readonly IJwtValidationService _jwtService;
    private readonly IPhotoTableService _tableService;
    private readonly ISpeciesCommonNameService _commonNameService;
    private readonly ILogger<RecomputeCommonNamesFunction> _logger;

    public RecomputeCommonNamesFunction(
        IJwtValidationService jwtService,
        IPhotoTableService tableService,
        ISpeciesCommonNameService commonNameService,
        ILogger<RecomputeCommonNamesFunction> logger)
    {
        _jwtService = jwtService;
        _tableService = tableService;
        _commonNameService = commonNameService;
        _logger = logger;
    }

    [Function("recompute-common-names")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "recompute-common-names")] HttpRequest req)
    {
        var (_, authError) = await AuthorizeAsync(req);
        if (authError is not null)
            return authError;

        var entities = await _tableService.ListAllAsync();
        var updated = 0;
        var attempted = 0;

        foreach (var entity in entities)
        {
            var (speciesType, scientificName, taxonomyOrder, taxonomyFamily, taxonomyGenus) = ResolveSpeciesMetadata(entity);
            if (string.IsNullOrWhiteSpace(speciesType) || string.IsNullOrWhiteSpace(scientificName))
                continue;

            attempted++;
            var commonNames = await _commonNameService.GetCommonNamesAsync(
                speciesType: speciesType,
                scientificName: scientificName,
                taxonomyOrder: taxonomyOrder,
                taxonomyFamily: taxonomyFamily,
                taxonomyGenus: taxonomyGenus);

            var nextCommonNamesJson = SpeciesMetadata.SerializeCommonNames(commonNames);
            if (entity.SpeciesType == speciesType
                && entity.ScientificName == scientificName
                && entity.TaxonomyOrder == taxonomyOrder
                && entity.TaxonomyFamily == taxonomyFamily
                && entity.TaxonomyGenus == taxonomyGenus
                && entity.CommonNamesJson == nextCommonNamesJson)
            {
                continue;
            }

            entity.SpeciesType = speciesType;
            entity.ScientificName = scientificName;
            entity.TaxonomyOrder = taxonomyOrder;
            entity.TaxonomyFamily = taxonomyFamily;
            entity.TaxonomyGenus = taxonomyGenus;
            entity.CommonNamesJson = nextCommonNamesJson;
            await _tableService.UpdateAsync(entity);
            updated++;
        }

        _logger.LogInformation(
            "Recomputed common names for {Updated}/{Total} images (attempted={Attempted})",
            updated,
            entities.Count,
            attempted);

        return new OkObjectResult(new
        {
            success = true,
            total = entities.Count,
            attempted,
            updated,
        });
    }

    private static (string speciesType, string scientificName, string taxonomyOrder, string taxonomyFamily, string taxonomyGenus)
        ResolveSpeciesMetadata(PhotoEntity entity)
    {
        var speciesType = SpeciesMetadata.NormalizeSpeciesType(entity.SpeciesType);
        var scientificName = SpeciesMetadata.NormalizeText(entity.ScientificName);
        var taxonomyOrder = SpeciesMetadata.NormalizeText(entity.TaxonomyOrder);
        var taxonomyFamily = SpeciesMetadata.NormalizeText(entity.TaxonomyFamily);
        var taxonomyGenus = SpeciesMetadata.NormalizeText(entity.TaxonomyGenus);

        var tags = (entity.Tags ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (string.IsNullOrWhiteSpace(speciesType))
        {
            if (tags.Any(t => string.Equals(t, "plant", StringComparison.OrdinalIgnoreCase)))
                speciesType = "plant";
            else if (tags.Any(t => string.Equals(t, "bird", StringComparison.OrdinalIgnoreCase)))
                speciesType = "bird";
            else if (tags.Any(t => string.Equals(t, "insect", StringComparison.OrdinalIgnoreCase)))
                speciesType = "insect";
        }

        if (string.Equals(speciesType, "plant", StringComparison.OrdinalIgnoreCase))
        {
            var hasCultivationTag = tags.Length >= 2
                && (string.Equals(tags[1], "wildlife", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(tags[1], "cultivated", StringComparison.OrdinalIgnoreCase));

            if (hasCultivationTag)
            {
                // Current format: plant, wildlife|cultivated, family, genus, scientific
                if (string.IsNullOrWhiteSpace(taxonomyFamily) && tags.Length >= 3)
                    taxonomyFamily = tags[2];
                if (string.IsNullOrWhiteSpace(taxonomyGenus) && tags.Length >= 4)
                    taxonomyGenus = tags[3];
                if (string.IsNullOrWhiteSpace(scientificName) && tags.Length >= 5)
                    scientificName = tags[4];
            }
            else
            {
                // Legacy format: plant, family, genus, scientific
                if (string.IsNullOrWhiteSpace(taxonomyFamily) && tags.Length >= 2)
                    taxonomyFamily = tags[1];
                if (string.IsNullOrWhiteSpace(taxonomyGenus) && tags.Length >= 3)
                    taxonomyGenus = tags[2];
                if (string.IsNullOrWhiteSpace(scientificName) && tags.Length >= 4)
                    scientificName = tags[3];
            }
        }
        else if (string.Equals(speciesType, "bird", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(scientificName) && tags.Length >= 2)
                scientificName = tags[1];
        }
        else if (string.Equals(speciesType, "insect", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(taxonomyFamily) && tags.Length >= 2)
                taxonomyFamily = tags[1];
            if (string.IsNullOrWhiteSpace(taxonomyGenus) && tags.Length >= 3)
                taxonomyGenus = tags[2];
            if (string.IsNullOrWhiteSpace(scientificName) && tags.Length >= 4)
                scientificName = tags[3];
        }

        return (speciesType, scientificName, taxonomyOrder, taxonomyFamily, taxonomyGenus);
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
