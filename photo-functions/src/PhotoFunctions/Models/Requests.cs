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
    string? SpeciesType,
    string? ScientificName,
    string? TaxonomyOrder,
    string? TaxonomyFamily,
    string? TaxonomyGenus,
    Dictionary<string, string>? CommonNames,
    string ContentType,
    long SizeBytes,
    int? Width = null,
    int? Height = null,
    double? Latitude = null,
    double? Longitude = null
);

/// <summary>Request body for updating image metadata.</summary>
public sealed record UpdateMetadataRequest(
    string Title,
    string Description,
    string[] Tags,
    string? SpeciesType = null,
    string? ScientificName = null,
    string? TaxonomyOrder = null,
    string? TaxonomyFamily = null,
    string? TaxonomyGenus = null,
    Dictionary<string, string>? CommonNames = null
);

public sealed record ManageFolderTreeRequest(
    string[] FolderPaths,
    Dictionary<string, string> TagRules,
    Dictionary<string, string>? RootLabels = null
);

public sealed record ManageTranslationsRequest(
    Dictionary<string, Dictionary<string, TranslationEntry>> Locales
);

public sealed record TranslateAllTranslationsRequest(
    string SourceLocale,
    string[]? TargetLocales,
    Dictionary<string, Dictionary<string, TranslationEntry>> Locales
);

public sealed record TranslateSingleTranslationRequest(
    string SourceLocale,
    string Key,
    string[]? TargetLocales,
    Dictionary<string, Dictionary<string, TranslationEntry>> Locales
);

public sealed record ManageSlideshowSettingsRequest(
    int PhotoCount,
    int IntervalSeconds,
    double TransitionSeconds,
    bool ShowProjectMap
);

/// <summary>Request payload metadata for insect identification.</summary>
public sealed record InsectIdentificationRequest(
    string Locale
);
