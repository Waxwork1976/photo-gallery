using System.Net.Mail;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using PhotoFunctions.Models;
using PhotoFunctions.Services;

namespace PhotoFunctions.Functions;

/// <summary>
/// POST /api/taxonomy-suggestions
/// Stores a taxonomy change suggestion for manager review.
/// Public endpoint (guest or authenticated user can submit).
/// </summary>
public sealed class CreateTaxonomySuggestionFunction
{
    private readonly ITaxonomySuggestionService _taxonomySuggestionService;
    private readonly ILogger<CreateTaxonomySuggestionFunction> _logger;

    public CreateTaxonomySuggestionFunction(
        ITaxonomySuggestionService taxonomySuggestionService,
        ILogger<CreateTaxonomySuggestionFunction> logger)
    {
        _taxonomySuggestionService = taxonomySuggestionService;
        _logger = logger;
    }

    [Function("taxonomy-suggestions")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "taxonomy-suggestions")] HttpRequest req)
    {
        CreateTaxonomySuggestionRequest? body;
        try
        {
            body = await req.ReadFromJsonAsync<CreateTaxonomySuggestionRequest>();
        }
        catch
        {
            return new BadRequestObjectResult(new ErrorResponse("Invalid request body"));
        }

        if (body is null
            || string.IsNullOrWhiteSpace(body.ImageId)
            || body.SourceTaxonomy is null
            || body.SuggestedTaxonomy is null
            || body.Requester is null)
        {
            return new BadRequestObjectResult(new ErrorResponse("Missing required suggestion fields"));
        }

        var requesterEmail = body.Requester.Email?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(requesterEmail))
            return new BadRequestObjectResult(new ErrorResponse("Requester email is required"));
        if (!IsValidEmail(requesterEmail))
            return new BadRequestObjectResult(new ErrorResponse("Requester email is invalid"));

        var entity = new TaxonomySuggestionEntity
        {
            ImageId = body.ImageId.Trim(),
            ImageTitle = body.ImageTitle?.Trim() ?? string.Empty,

            SourceOrder = body.SourceTaxonomy.Order?.Trim() ?? string.Empty,
            SourceFamily = body.SourceTaxonomy.Family?.Trim() ?? string.Empty,
            SourceGenus = body.SourceTaxonomy.Genus?.Trim() ?? string.Empty,
            SourceScientificName = body.SourceTaxonomy.ScientificName?.Trim() ?? string.Empty,
            SourceCommonName = body.SourceTaxonomy.CommonName?.Trim() ?? string.Empty,

            SuggestedOrder = body.SuggestedTaxonomy.Order?.Trim() ?? string.Empty,
            SuggestedFamily = body.SuggestedTaxonomy.Family?.Trim() ?? string.Empty,
            SuggestedGenus = body.SuggestedTaxonomy.Genus?.Trim() ?? string.Empty,
            SuggestedScientificName = body.SuggestedTaxonomy.ScientificName?.Trim() ?? string.Empty,
            SuggestedCommonName = body.SuggestedTaxonomy.CommonName?.Trim() ?? string.Empty,
            Note = body.Note?.Trim() ?? string.Empty,

            RequesterAuthenticated = body.Requester.Authenticated,
            RequesterName = body.Requester.Name?.Trim() ?? string.Empty,
            RequesterEmail = requesterEmail,
            RequesterLocale = NormalizeLocale(body.Requester.Locale),
            CaptchaToken = body.CaptchaToken?.Trim() ?? string.Empty,
            Status = "pending",
            CreatedAt = DateTimeOffset.UtcNow.ToString("o"),
        };

        var inserted = await _taxonomySuggestionService.InsertAsync(entity);

        _logger.LogInformation("Stored taxonomy suggestion {SuggestionId} for image {ImageId}", inserted.RowKey, inserted.ImageId);
        return new OkObjectResult(new CreateTaxonomySuggestionResponse(
            Success: true,
            Id: inserted.RowKey,
            CreatedAt: inserted.CreatedAt));
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

    private static bool IsValidEmail(string email)
    {
        try
        {
            _ = new MailAddress(email);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
