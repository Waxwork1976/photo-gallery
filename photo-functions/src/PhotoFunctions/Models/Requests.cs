namespace PhotoFunctions.Models;

/// <summary>Request body for generating a SAS upload URL.</summary>
public sealed record GenerateSasRequest(
    string Filename,
    string ContentType
);

/// <summary>Request body for saving image metadata after upload.</summary>
public sealed record SaveMetadataRequest(
    string BlobName,
    string OriginalFilename,
    string Title,
    string Description,
    string[] Tags,
    string ContentType,
    long SizeBytes,
    int? Width = null,
    int? Height = null
);
