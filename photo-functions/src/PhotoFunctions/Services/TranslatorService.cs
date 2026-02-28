using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PhotoFunctions.Configuration;

namespace PhotoFunctions.Services;

public sealed class TranslatorService : ITranslatorService
{
    private readonly HttpClient _httpClient;
    private readonly TranslatorOptions _options;
    private readonly ILogger<TranslatorService> _logger;

    public TranslatorService(
        HttpClient httpClient,
        IOptions<TranslatorOptions> options,
        ILogger<TranslatorService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<string>> TranslateBatchAsync(
        string sourceLocale,
        string targetLocale,
        IReadOnlyList<string> texts)
    {
        if (texts.Count == 0)
            return [];

        ValidateConfiguration();

        var endpoint = _options.Endpoint.TrimEnd('/');
        var uri = $"{endpoint}/translate?api-version=3.0&from={Uri.EscapeDataString(sourceLocale)}&to={Uri.EscapeDataString(targetLocale)}";
        var body = texts.Select(text => new TranslationInput(text)).ToArray();

        using var request = new HttpRequestMessage(HttpMethod.Post, uri);
        request.Headers.Add("Ocp-Apim-Subscription-Key", _options.ApiKey);
        if (!string.IsNullOrWhiteSpace(_options.Region))
            request.Headers.Add("Ocp-Apim-Subscription-Region", _options.Region);
        request.Content = JsonContent.Create(body);

        var response = await _httpClient.SendAsync(request);
        var payload = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Translator API returned {StatusCode}: {Body}",
                (int)response.StatusCode,
                payload);
            throw new HttpRequestException(
                $"Translator API returned {(int)response.StatusCode}: {payload}",
                null,
                response.StatusCode);
        }

        var translated = JsonSerializer.Deserialize<TranslationOutput[]>(payload, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? [];

        if (translated.Length != texts.Count)
        {
            throw new InvalidOperationException(
                $"Translator returned {translated.Length} result(s) for {texts.Count} input text(s).");
        }

        return translated
            .Select(item => item.Translations.FirstOrDefault()?.Text ?? string.Empty)
            .ToArray();
    }

    private void ValidateConfiguration()
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
            throw new InvalidOperationException("Translator configuration is missing ApiKey.");

        if (string.IsNullOrWhiteSpace(_options.Endpoint))
            throw new InvalidOperationException("Translator configuration is missing Endpoint.");
    }

    private sealed record TranslationInput(string Text);

    private sealed record TranslationOutput(
        TranslationItem[] Translations);

    private sealed record TranslationItem(
        string Text,
        string To);
}
