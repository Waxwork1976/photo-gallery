using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PhotoFunctions.Configuration;
using PhotoFunctions.Models;
using PhotoFunctions.Services;

namespace PhotoFunctions.Functions;

/// <summary>
/// POST /api/identify-bird
/// Pass-through to the RapidAPI bird-classifier endpoint.
/// Protected: requires a valid JWT from an authorized user.
/// </summary>
public sealed class IdentifyBirdFunction
{
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp",
    };

    private readonly IJwtValidationService _jwtService;
    private readonly IBirdIdentificationService _birdService;
    private readonly ISpeciesCommonNameService _commonNameService;
    private readonly BirdApiOptions _birdOptions;
    private readonly ILogger<IdentifyBirdFunction> _logger;

    public IdentifyBirdFunction(
        IJwtValidationService jwtService,
        IBirdIdentificationService birdService,
        ISpeciesCommonNameService commonNameService,
        IOptions<BirdApiOptions> birdOptions,
        ILogger<IdentifyBirdFunction> logger)
    {
        _jwtService = jwtService;
        _birdService = birdService;
        _commonNameService = commonNameService;
        _birdOptions = birdOptions.Value;
        _logger = logger;
    }

    [Function("identify-bird")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "identify-bird")] HttpRequest req)
    {
        var (_, authError) = await AuthorizeAsync(req);
        if (authError is not null)
            return authError;

        if (!req.HasFormContentType)
            return new BadRequestObjectResult(
                new ErrorResponse("Request must be multipart/form-data"));

        var form = await req.ReadFormAsync();
        var files = form.Files.GetFiles("image");

        if (files.Count == 0)
            return new BadRequestObjectResult(
                new ErrorResponse("An image is required (field name: 'image')"));

        if (files.Count > 1)
            return new BadRequestObjectResult(
                new ErrorResponse("Only one image is allowed"));

        var file = files[0];
        if (!AllowedContentTypes.Contains(file.ContentType))
            return new BadRequestObjectResult(
                new ErrorResponse($"Content type '{file.ContentType}' is not allowed. Allowed: {string.Join(", ", AllowedContentTypes)}"));

        await using var stream = file.OpenReadStream();
        try
        {
            var results = await _birdService.IdentifyAsync(stream, file.FileName, file.ContentType);
            var topResult = results.FirstOrDefault();
            Dictionary<string, string>? topCommonNames = null;
            if (topResult is not null)
            {
                topCommonNames = await _commonNameService.GetCommonNamesAsync(
                    "bird",
                    topResult.ScientificName);
            }

            var enrichedResults = results.Select(item =>
            {
                if (topResult is null || !string.Equals(item.ScientificName, topResult.ScientificName, StringComparison.OrdinalIgnoreCase))
                    return item;

                return item with
                {
                    CommonNames = topCommonNames
                };
            }).ToArray();

            var enrichedTopResult = topResult is null
                ? null
                : topResult with { CommonNames = topCommonNames };

            var accepted = enrichedTopResult is not null && enrichedTopResult.Probability >= _birdOptions.MinProbability;

            return new OkObjectResult(new BirdIdentificationResponse(
                MinProbability: _birdOptions.MinProbability,
                Accepted: accepted,
                TopResult: enrichedTopResult,
                Results: enrichedResults));
        }
        catch (HttpRequestException ex) when (ex.StatusCode is not null)
        {
            _logger.LogWarning(ex, "Bird API error");
            return new ObjectResult(
                new ErrorResponse("Bird API error", ex.Message))
            {
                StatusCode = (int)ex.StatusCode,
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Bird API call failed");
            return new ObjectResult(
                new ErrorResponse("Failed to call Bird API", ex.Message))
            {
                StatusCode = StatusCodes.Status502BadGateway,
            };
        }
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
