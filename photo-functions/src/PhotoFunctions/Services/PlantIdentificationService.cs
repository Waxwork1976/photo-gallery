using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PhotoFunctions.Configuration;

namespace PhotoFunctions.Services;

public sealed class PlantIdentificationService : IPlantIdentificationService
{
    private readonly HttpClient _httpClient;
    private readonly PlantNetOptions _options;
    private readonly ILogger<PlantIdentificationService> _logger;

    public PlantIdentificationService(
        HttpClient httpClient,
        IOptions<PlantNetOptions> options,
        ILogger<PlantIdentificationService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<JsonElement> IdentifyAsync(
        IReadOnlyList<(Stream Content, string FileName, string ContentType)> images,
        IReadOnlyList<string>? organs = null)
    {
        if (images.Count == 0)
            throw new ArgumentException("At least one image is required.", nameof(images));

        if (images.Count > 5)
            throw new ArgumentException("A maximum of 5 images is allowed.", nameof(images));

        if (organs is not null && organs.Count != images.Count)
            throw new ArgumentException(
                "Number of organ values must match number of images.", nameof(organs));

        var queryParams = new Dictionary<string, string>
        {
            ["api-key"] = _options.ApiKey,
            ["include-related-images"] = _options.IncludeRelatedImages.ToString().ToLowerInvariant(),
            ["lang"] = _options.Language,
            ["type"] = _options.Type,
        };

        if (_options.Detailed)
            queryParams["detailed"] = "true";

        var queryString = string.Join("&",
            queryParams.Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));

        var url = $"{_options.BaseUrl.TrimEnd('/')}/v2/identify/{Uri.EscapeDataString(_options.Project)}?{queryString}";

        using var content = new MultipartFormDataContent();

        for (var i = 0; i < images.Count; i++)
        {
            var img = images[i];
            var streamContent = new StreamContent(img.Content);
            streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(img.ContentType);
            content.Add(streamContent, "images", img.FileName);
        }

        if (organs is not null)
        {
            foreach (var organ in organs)
                content.Add(new StringContent(organ), "organs");
        }

        _logger.LogInformation(
            "Calling PlantNet identify API with {ImageCount} image(s), project={Project}",
            images.Count, _options.Project);

        var response = await _httpClient.PostAsync(url, content);

        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "PlantNet API returned {StatusCode}: {Body}",
                (int)response.StatusCode, responseBody);

            throw new HttpRequestException(
                $"PlantNet API returned {(int)response.StatusCode}: {responseBody}",
                null,
                response.StatusCode);
        }

        _logger.LogInformation("PlantNet identification successful");

        return JsonSerializer.Deserialize<JsonElement>(responseBody);
    }
}
