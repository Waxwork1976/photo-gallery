using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PhotoFunctions.Configuration;
using PhotoFunctions.Models;

namespace PhotoFunctions.Services;

public sealed class BirdIdentificationService : IBirdIdentificationService
{
    private readonly HttpClient _httpClient;
    private readonly BirdApiOptions _options;
    private readonly ILogger<BirdIdentificationService> _logger;

    public BirdIdentificationService(
        HttpClient httpClient,
        IOptions<BirdApiOptions> options,
        ILogger<BirdIdentificationService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<BirdPredictionItem[]> IdentifyAsync(Stream content, string fileName, string contentType)
    {
        var endpoint = $"{_options.BaseUrl.TrimEnd('/')}/{_options.Path.TrimStart('/')}?results={_options.ResultsCount}";

        using var form = new MultipartFormDataContent();
        var streamContent = new StreamContent(content);
        streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        form.Add(streamContent, "image", fileName);

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = form
        };
        request.Headers.Add("x-rapidapi-key", _options.ApiKey);
        request.Headers.Add("x-rapidapi-host", _options.Host);

        var response = await _httpClient.SendAsync(request);
        var payload = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Bird API returned {Status}: {Body}", (int)response.StatusCode, payload);
            throw new HttpRequestException(
                $"Bird API returned {(int)response.StatusCode}: {payload}",
                null,
                response.StatusCode);
        }

        var results = JsonSerializer.Deserialize<BirdPredictionItem[]>(
            payload,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? [];

        return results
            .Where(x => !string.IsNullOrWhiteSpace(x.ScientificName))
            .OrderByDescending(x => x.Probability)
            .ToArray();
    }
}
