using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using PhotoFunctions.Models;
using PhotoFunctions.Services;

namespace PhotoFunctions.Functions;

public sealed class GetSlideshowSettingsFunction
{
    private readonly ISlideshowSettingsService _slideshowSettingsService;

    public GetSlideshowSettingsFunction(ISlideshowSettingsService slideshowSettingsService)
    {
        _slideshowSettingsService = slideshowSettingsService;
    }

    [Function("slideshow-settings")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "slideshow-settings")] HttpRequest req)
    {
        var document = await _slideshowSettingsService.GetDocumentAsync();
        return new OkObjectResult(new SlideshowSettingsResponse(
            PhotoCount: document.PhotoCount,
            IntervalSeconds: document.IntervalSeconds,
            TransitionSeconds: document.TransitionSeconds));
    }
}
