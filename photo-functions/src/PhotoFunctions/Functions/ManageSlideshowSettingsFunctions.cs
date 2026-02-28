using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using PhotoFunctions.Models;
using PhotoFunctions.Services;

namespace PhotoFunctions.Functions;

public sealed class ManageSlideshowSettingsFunctions
{
    private readonly IJwtValidationService _jwtService;
    private readonly ISlideshowSettingsService _slideshowSettingsService;

    public ManageSlideshowSettingsFunctions(
        IJwtValidationService jwtService,
        ISlideshowSettingsService slideshowSettingsService)
    {
        _jwtService = jwtService;
        _slideshowSettingsService = slideshowSettingsService;
    }

    [Function("manage-slideshow-settings-get")]
    public async Task<IActionResult> Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "manage-slideshow-settings")] HttpRequest req)
    {
        var (_, authError) = await AuthorizeAsync(req);
        if (authError is not null)
            return authError;

        var document = await _slideshowSettingsService.GetDocumentAsync();
        return new OkObjectResult(new SlideshowSettingsResponse(
            PhotoCount: document.PhotoCount,
            IntervalSeconds: document.IntervalSeconds,
            TransitionSeconds: document.TransitionSeconds));
    }

    [Function("manage-slideshow-settings-put")]
    public async Task<IActionResult> Put(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "manage-slideshow-settings")] HttpRequest req)
    {
        var (_, authError) = await AuthorizeAsync(req);
        if (authError is not null)
            return authError;

        ManageSlideshowSettingsRequest? body;
        try
        {
            body = await req.ReadFromJsonAsync<ManageSlideshowSettingsRequest>();
        }
        catch
        {
            return new BadRequestObjectResult(new ErrorResponse("Invalid request body"));
        }

        if (body is null)
            return new BadRequestObjectResult(new ErrorResponse("Request body is required"));

        await _slideshowSettingsService.SaveDocumentAsync(new SlideshowSettingsDocument
        {
            PhotoCount = body.PhotoCount,
            IntervalSeconds = body.IntervalSeconds,
            TransitionSeconds = body.TransitionSeconds,
        });

        var persisted = await _slideshowSettingsService.GetDocumentAsync();
        return new OkObjectResult(new SlideshowSettingsResponse(
            PhotoCount: persisted.PhotoCount,
            IntervalSeconds: persisted.IntervalSeconds,
            TransitionSeconds: persisted.TransitionSeconds));
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
