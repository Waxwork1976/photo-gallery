using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using PhotoFunctions.Models;
using PhotoFunctions.Services;

namespace PhotoFunctions.Functions;

/// <summary>
/// GET /api/manage-search-suggestions?q=<term>&locale=<locale>&limit=<n>
/// Protected suggestions endpoint for manage search.
/// </summary>
public sealed class ManageSearchSuggestionsFunction
{
    private readonly IJwtValidationService _jwtService;
    private readonly IPhotoTableService _tableService;

    public ManageSearchSuggestionsFunction(
        IJwtValidationService jwtService,
        IPhotoTableService tableService)
    {
        _jwtService = jwtService;
        _tableService = tableService;
    }

    [Function("manage-search-suggestions")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "manage-search-suggestions")] HttpRequest req)
    {
        var (_, authError) = await AuthorizeAsync(req);
        if (authError is not null)
            return authError;

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

        var entities = await _tableService.ListAllAsync();
        var suggestions = SearchSuggestionsBuilder.Build(entities, query, locale, limit);
        return new OkObjectResult(new SearchSuggestionsResponse(
            Query: query,
            Locale: locale,
            Suggestions: suggestions));
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
