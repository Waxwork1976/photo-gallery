namespace PhotoFunctions.Configuration;

public sealed class PlantNetOptions
{
    public const string SectionName = "PlantNet";

    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://my-api.plantnet.org";
    public string Project { get; set; } = "all";
    public string Language { get; set; } = "fr";
    public string Type { get; set; } = "kt";
    public bool IncludeRelatedImages { get; set; } = false;
    public bool Detailed { get; set; } = true;
}
