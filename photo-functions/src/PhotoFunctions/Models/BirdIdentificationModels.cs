namespace PhotoFunctions.Models;

public sealed record BirdPredictionItem(
    string ScientificName,
    double Probability
);

public sealed record BirdIdentificationResponse(
    double MinProbability,
    bool Accepted,
    BirdPredictionItem? TopResult,
    BirdPredictionItem[] Results
);
