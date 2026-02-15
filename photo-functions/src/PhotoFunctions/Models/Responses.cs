namespace PhotoFunctions.Models;

/// <summary>Response from the generate-sas endpoint.</summary>
public sealed record GenerateSasResponse(
    string SasUrl,
    string BlobName,
    int ExpiresInSeconds
);

/// <summary>Response from the save-metadata endpoint.</summary>
public sealed record SaveMetadataResponse(
    bool Success,
    string Id,
    string BlobUrl
);

/// <summary>A single image in the list-images response.</summary>
public sealed record ImageDto(
    string Id,
    string BlobName,
    string Url,
    string Title,
    string Description,
    string[] Tags,
    string UploadedAt,
    int Width,
    int Height,
    long SizeBytes,
    bool Featured
);

/// <summary>Response from the list-images endpoint.</summary>
public sealed record ListImagesResponse(ImageDto[] Images);

/// <summary>Validated user information extracted from a JWT.</summary>
public sealed record AuthUserInfo(
    string Oid,
    string Email,
    string Name
);

/// <summary>Response from the auth-validate endpoint.</summary>
public sealed record AuthValidateResponse(
    bool Valid,
    AuthUserInfo User
);

/// <summary>Simple error response.</summary>
public sealed record ErrorResponse(string Error, string? Details = null);

/// <summary>Health-check response.</summary>
public sealed record HealthResponse(
    string Status,
    string Timestamp,
    string Version
);
