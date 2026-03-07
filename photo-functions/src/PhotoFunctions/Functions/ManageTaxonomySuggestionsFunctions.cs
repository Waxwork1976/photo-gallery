using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using PhotoFunctions.Models;
using PhotoFunctions.Services;

namespace PhotoFunctions.Functions;

public sealed class ManageTaxonomySuggestionsFunctions
{
    private const string ExpertEditLabel = "Edited following expert request";

    private readonly IJwtValidationService _jwtService;
    private readonly ITaxonomySuggestionService _taxonomySuggestionService;
    private readonly IPhotoTableService _photoTableService;
    private readonly IFolderTreeService _folderTreeService;
    private readonly ILogger<ManageTaxonomySuggestionsFunctions> _logger;

    public ManageTaxonomySuggestionsFunctions(
        IJwtValidationService jwtService,
        ITaxonomySuggestionService taxonomySuggestionService,
        IPhotoTableService photoTableService,
        IFolderTreeService folderTreeService,
        ILogger<ManageTaxonomySuggestionsFunctions> logger)
    {
        _jwtService = jwtService;
        _taxonomySuggestionService = taxonomySuggestionService;
        _photoTableService = photoTableService;
        _folderTreeService = folderTreeService;
        _logger = logger;
    }

    [Function("manage-taxonomy-suggestions")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "manage-taxonomy-suggestions")] HttpRequest req)
    {
        var (_, authError) = await AuthorizeAsync(req);
        if (authError is not null)
            return authError;

        var entities = await _taxonomySuggestionService.ListPendingAsync();
        var suggestions = entities.Select(ToDto).ToArray();
        return new OkObjectResult(new ListTaxonomySuggestionsResponse(suggestions));
    }

    [Function("manage-taxonomy-suggestions-count")]
    public async Task<IActionResult> Count(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "manage-taxonomy-suggestions/count")] HttpRequest req)
    {
        var (_, authError) = await AuthorizeAsync(req);
        if (authError is not null)
            return authError;

        var pendingCount = await _taxonomySuggestionService.CountPendingAsync();
        return new OkObjectResult(new TaxonomySuggestionCountResponse(pendingCount));
    }

    [Function("manage-taxonomy-suggestions-accept")]
    public async Task<IActionResult> Accept(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "manage-taxonomy-suggestions/{id}/accept")] HttpRequest req,
        string id)
    {
        var (_, authError) = await AuthorizeAsync(req);
        if (authError is not null)
            return authError;

        if (string.IsNullOrWhiteSpace(id))
            return new BadRequestObjectResult(new ErrorResponse("Missing suggestion id"));

        var suggestion = await _taxonomySuggestionService.GetAsync(id);
        if (suggestion is null || !string.Equals(suggestion.Status, "pending", StringComparison.OrdinalIgnoreCase))
            return new NotFoundObjectResult(new ErrorResponse("Suggestion not found"));

        var photo = await _photoTableService.GetAsync(suggestion.ImageId);
        if (photo is null)
            return new NotFoundObjectResult(new ErrorResponse("Target image not found"));

        photo.TaxonomyOrder = SpeciesMetadata.NormalizeText(suggestion.SuggestedOrder);
        photo.TaxonomyFamily = SpeciesMetadata.NormalizeText(suggestion.SuggestedFamily);
        photo.TaxonomyGenus = SpeciesMetadata.NormalizeText(suggestion.SuggestedGenus);
        photo.ScientificName = SpeciesMetadata.NormalizeText(suggestion.SuggestedScientificName);

        var tags = BuildUpdatedTags(photo, suggestion);
        photo.Tags = string.Join(",", tags);

        var folderDoc = await _folderTreeService.GetDocumentAsync();
        var resolved = _folderTreeService.ResolveFoldersFromTags(tags.ToArray(), folderDoc);
        photo.PrimaryFolderPath = resolved.PrimaryFolderPath;
        photo.FolderPathsCsv = string.Join(",", resolved.FolderPaths);

        if (!string.IsNullOrWhiteSpace(suggestion.SuggestedCommonName))
        {
            var commonNames = SpeciesMetadata.DeserializeCommonNames(photo.CommonNamesJson);
            var locale = NormalizeLocale(suggestion.RequesterLocale);
            commonNames[locale] = suggestion.SuggestedCommonName.Trim();
            if (!commonNames.TryGetValue("en", out var en) || string.IsNullOrWhiteSpace(en))
                commonNames["en"] = suggestion.SuggestedCommonName.Trim();
            photo.CommonNamesJson = SpeciesMetadata.SerializeCommonNames(commonNames);
        }

        if (string.IsNullOrWhiteSpace(photo.Description))
        {
            photo.Description = ExpertEditLabel;
        }
        else if (!photo.Description.Contains(ExpertEditLabel, StringComparison.OrdinalIgnoreCase))
        {
            photo.Description = $"{photo.Description.Trim()} {ExpertEditLabel}";
        }

        await _photoTableService.UpdateAsync(photo);
        await _taxonomySuggestionService.DeleteAsync(id);

        _logger.LogInformation("Accepted taxonomy suggestion {SuggestionId} for image {ImageId}", id, suggestion.ImageId);
        return new OkObjectResult(new { success = true, id });
    }

    [Function("manage-taxonomy-suggestions-reject")]
    public async Task<IActionResult> Reject(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "manage-taxonomy-suggestions/{id}/reject")] HttpRequest req,
        string id)
    {
        var (_, authError) = await AuthorizeAsync(req);
        if (authError is not null)
            return authError;

        if (string.IsNullOrWhiteSpace(id))
            return new BadRequestObjectResult(new ErrorResponse("Missing suggestion id"));

        var suggestion = await _taxonomySuggestionService.GetAsync(id);
        if (suggestion is null || !string.Equals(suggestion.Status, "pending", StringComparison.OrdinalIgnoreCase))
            return new NotFoundObjectResult(new ErrorResponse("Suggestion not found"));

        await _taxonomySuggestionService.DeleteAsync(id);
        _logger.LogInformation("Rejected taxonomy suggestion {SuggestionId}", id);

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

    private static TaxonomySuggestionDto ToDto(TaxonomySuggestionEntity entity)
    {
        return new TaxonomySuggestionDto(
            Id: entity.RowKey,
            ImageId: entity.ImageId,
            ImageTitle: entity.ImageTitle,
            SourceTaxonomy: new TaxonomySuggestionTaxonomyDto(
                Order: entity.SourceOrder,
                Family: entity.SourceFamily,
                Genus: entity.SourceGenus,
                ScientificName: entity.SourceScientificName,
                CommonName: entity.SourceCommonName),
            SuggestedTaxonomy: new TaxonomySuggestionTaxonomyDto(
                Order: entity.SuggestedOrder,
                Family: entity.SuggestedFamily,
                Genus: entity.SuggestedGenus,
                ScientificName: entity.SuggestedScientificName,
                CommonName: entity.SuggestedCommonName),
            Note: entity.Note,
            Requester: new TaxonomySuggestionRequesterDto(
                Authenticated: entity.RequesterAuthenticated,
                Name: entity.RequesterName,
                Email: entity.RequesterEmail,
                Locale: NormalizeLocale(entity.RequesterLocale)),
            CreatedAt: entity.CreatedAt);
    }

    private static string NormalizeLocale(string? locale)
    {
        var normalized = (locale ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "en" or "fr" or "de" or "it" => normalized,
            _ => "en",
        };
    }

    private static List<string> BuildUpdatedTags(PhotoEntity photo, TaxonomySuggestionEntity suggestion)
    {
        var currentTags = (photo.Tags ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        var removable = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in new[]
                 {
                     photo.ScientificName,
                     photo.TaxonomyOrder,
                     photo.TaxonomyFamily,
                     photo.TaxonomyGenus,
                     suggestion.SourceScientificName,
                     suggestion.SourceOrder,
                     suggestion.SourceFamily,
                     suggestion.SourceGenus,
                 })
        {
            if (!string.IsNullOrWhiteSpace(value))
                removable.Add(value.Trim());
        }

        var nextTags = currentTags
            .Where(tag => !removable.Contains(tag))
            .ToList();

        foreach (var value in new[]
                 {
                     suggestion.SuggestedScientificName,
                     suggestion.SuggestedOrder,
                     suggestion.SuggestedFamily,
                     suggestion.SuggestedGenus,
                 })
        {
            var normalized = (value ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalized))
                continue;
            if (nextTags.Any(tag => string.Equals(tag, normalized, StringComparison.OrdinalIgnoreCase)))
                continue;
            nextTags.Add(normalized);
        }

        return nextTags;
    }
}
