using System.Net;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using PhotoFunctions.Models;
using PhotoFunctions.Services;

namespace PhotoFunctions.Functions;

/// <summary>
/// POST /api/identify-plant
/// Pass-through to the PlantNet identification API.
/// Accepts multipart/form-data with one or more images and optional organs.
/// Protected: requires a valid JWT from an authorized user.
/// </summary>
public sealed class IdentifyPlantFunction
{
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
    };

    private static readonly HashSet<string> ValidOrgans = new(StringComparer.OrdinalIgnoreCase)
    {
        "auto", "leaf", "flower", "fruit", "bark",
    };

    private readonly IJwtValidationService _jwtService;
    private readonly IPlantIdentificationService _plantService;
    private readonly ISpeciesCommonNameService _commonNameService;
    private readonly ILogger<IdentifyPlantFunction> _logger;

    public IdentifyPlantFunction(
        IJwtValidationService jwtService,
        IPlantIdentificationService plantService,
        ISpeciesCommonNameService commonNameService,
        ILogger<IdentifyPlantFunction> logger)
    {
        _jwtService = jwtService;
        _plantService = plantService;
        _commonNameService = commonNameService;
        _logger = logger;
    }

    [Function("identify-plant")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "identify-plant")] HttpRequest req)
    {
        var (_, authError) = await AuthorizeAsync(req);
        if (authError is not null)
            return authError;

        if (!req.HasFormContentType)
            return new BadRequestObjectResult(
                new ErrorResponse("Request must be multipart/form-data"));

        var form = await req.ReadFormAsync();
        var files = form.Files.GetFiles("images");

        if (files.Count == 0)
            return new BadRequestObjectResult(
                new ErrorResponse("At least one image is required (field name: 'images')"));

        if (files.Count > 5)
            return new BadRequestObjectResult(
                new ErrorResponse("A maximum of 5 images is allowed"));

        foreach (var file in files)
        {
            if (!AllowedContentTypes.Contains(file.ContentType))
                return new BadRequestObjectResult(
                    new ErrorResponse($"Content type '{file.ContentType}' is not allowed. Allowed: {string.Join(", ", AllowedContentTypes)}"));
        }

        var organValues = form.ContainsKey("organs")
            ? form["organs"].ToArray()
            : null;

        List<string>? organs = null;
        if (organValues is { Length: > 0 })
        {
            if (organValues.Length != files.Count)
                return new BadRequestObjectResult(
                    new ErrorResponse($"Number of organ values ({organValues.Length}) must match number of images ({files.Count})"));

            organs = new List<string>(organValues.Length);
            foreach (var o in organValues)
            {
                var val = o ?? "auto";
                if (!ValidOrgans.Contains(val))
                    return new BadRequestObjectResult(
                        new ErrorResponse($"Invalid organ value '{val}'. Allowed: {string.Join(", ", ValidOrgans)}"));
                organs.Add(val);
            }
        }

        var imageStreams = new List<(Stream, string, string)>();
        try
        {
            foreach (var file in files)
                imageStreams.Add((file.OpenReadStream(), file.FileName, file.ContentType));

            var result = await _plantService.IdentifyAsync(imageStreams, organs);
            var enriched = await EnrichWithCommonNamesAsync(result);
            return new OkObjectResult(enriched);
        }
        catch (HttpRequestException ex) when (ex.StatusCode is not null)
        {
            _logger.LogWarning(ex, "PlantNet API error");
            return new ObjectResult(
                new ErrorResponse("PlantNet API error", ex.Message))
            {
                StatusCode = (int)ex.StatusCode,
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "PlantNet API call failed");
            return new ObjectResult(
                new ErrorResponse("Failed to call PlantNet API", ex.Message))
            {
                StatusCode = StatusCodes.Status502BadGateway,
            };
        }
        finally
        {
            foreach (var (stream, _, _) in imageStreams)
                await stream.DisposeAsync();
        }
    }

    private async Task<JsonNode> EnrichWithCommonNamesAsync(JsonElement result)
    {
        JsonNode? rootNode;
        try
        {
            rootNode = JsonNode.Parse(result.GetRawText());
        }
        catch
        {
            rootNode = null;
        }

        if (rootNode is not JsonObject root)
            return JsonNode.Parse(result.GetRawText())!;

        var results = root["results"] as JsonArray;
        if (results is null)
            return root;

        var cache = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in results.Take(5))
        {
            if (item is not JsonObject resultObj)
                continue;
            if (resultObj["species"] is not JsonObject speciesObj)
                continue;

            var scientificName = speciesObj["scientificName"]?.GetValue<string>()?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(scientificName))
                continue;

            if (!cache.TryGetValue(scientificName, out var names))
            {
                var order = speciesObj["order"]?["scientificNameWithoutAuthor"]?.GetValue<string>()
                            ?? speciesObj["order"]?["scientificName"]?.GetValue<string>();
                var family = speciesObj["family"]?["scientificNameWithoutAuthor"]?.GetValue<string>()
                             ?? speciesObj["family"]?["scientificName"]?.GetValue<string>();
                var genus = speciesObj["genus"]?["scientificNameWithoutAuthor"]?.GetValue<string>()
                            ?? speciesObj["genus"]?["scientificName"]?.GetValue<string>();

                names = await _commonNameService.GetCommonNamesAsync(
                    speciesType: "plant",
                    scientificName: scientificName,
                    taxonomyOrder: order,
                    taxonomyFamily: family,
                    taxonomyGenus: genus);
                cache[scientificName] = names;
            }

            speciesObj["commonNamesByLocale"] = JsonSerializer.SerializeToNode(names);
        }

        return root;
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
