using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using PhotoFunctions.Models;
using PhotoFunctions.Services;

namespace PhotoFunctions.Functions;

/// <summary>
/// GET /api/search-suggestions?q=<term>&locale=<locale>&limit=<n>
/// Public suggestions endpoint for gallery search.
/// </summary>
public sealed class SearchSuggestionsFunction
{
    private readonly IPhotoTableService _tableService;

    public SearchSuggestionsFunction(IPhotoTableService tableService)
    {
        _tableService = tableService;
    }

    [Function("search-suggestions")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "search-suggestions")] HttpRequest req)
    {
        var query = req.Query["q"].FirstOrDefault() ?? string.Empty;
        var locale = req.Query["locale"].FirstOrDefault() ?? "en";
        _ = int.TryParse(req.Query["limit"], out var parsedLimit);
        var limit = parsedLimit <= 0 ? 12 : parsedLimit;

        if (query.Trim().Length < 3)
        {
            return new OkObjectResult(new SearchSuggestionsResponse(
                Query: query,
                Locale: locale,
                Suggestions: []));
        }

        var entities = await _tableService.ListAsync();
        var suggestions = SearchSuggestionsBuilder.Build(entities, query, locale, limit);
        return new OkObjectResult(new SearchSuggestionsResponse(
            Query: query,
            Locale: locale,
            Suggestions: suggestions));
    }
}
