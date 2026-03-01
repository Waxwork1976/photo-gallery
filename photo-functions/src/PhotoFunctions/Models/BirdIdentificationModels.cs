namespace PhotoFunctions.Models;

public sealed record BirdPredictionItem(
    string ScientificName,
    double Probability,
    Dictionary<string, string>? CommonNames = null,
    string TaxonomyOrder = "",
    string TaxonomyFamily = "",
    string TaxonomyGenus = ""
);

public sealed record BirdIdentificationResponse(
    double MinProbability,
    bool Accepted,
    BirdPredictionItem? TopResult,
    BirdPredictionItem[] Results
);
