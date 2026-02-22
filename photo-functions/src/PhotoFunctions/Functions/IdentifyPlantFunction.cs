using System.Net;
using System.Security.Claims;
using System.Text.Json;
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
    private readonly ILogger<IdentifyPlantFunction> _logger;

    public IdentifyPlantFunction(
        IJwtValidationService jwtService,
        IPlantIdentificationService plantService,
        ILogger<IdentifyPlantFunction> logger)
    {
        _jwtService = jwtService;
        _plantService = plantService;
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

            return new OkObjectResult(result);
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
