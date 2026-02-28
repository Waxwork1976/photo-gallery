namespace PhotoFunctions.Configuration;

public sealed class BirdApiOptions
{
    public const string SectionName = "BirdApi";

    public string ApiKey { get; set; } = string.Empty;
    public string Host { get; set; } = "bird-classifier.p.rapidapi.com";
    public string BaseUrl { get; set; } = "https://bird-classifier.p.rapidapi.com";
    public string Path { get; set; } = "/BirdClassifier/prediction";
    public int ResultsCount { get; set; } = 5;
    public double MinProbability { get; set; } = 0.5;
}
