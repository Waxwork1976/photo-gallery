namespace PhotoFunctions.Configuration;

public sealed class TranslatorOptions
{
    public const string SectionName = "Translator";

    public string Endpoint { get; set; } = "https://api.cognitive.microsofttranslator.com";
    public string ApiKey { get; set; } = string.Empty;
    public string Region { get; set; } = "global";
}
