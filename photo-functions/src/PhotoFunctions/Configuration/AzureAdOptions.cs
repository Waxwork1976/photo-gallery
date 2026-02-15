namespace PhotoFunctions.Configuration;

/// <summary>Configuration for Microsoft Entra ID (Azure AD) JWT validation.</summary>
public sealed class AzureAdOptions
{
    public const string SectionName = "AzureAd";

    public string TenantId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Comma-separated list of Object IDs (oid) allowed to upload images.
    /// </summary>
    public string AllowedUserOids { get; set; } = string.Empty;

    // ----- Derived helpers -----

    public string Authority => $"https://login.microsoftonline.com/{TenantId}/v2.0";
    public string MetadataAddress => $"{Authority}/.well-known/openid-configuration";
    public IReadOnlyList<string> AllowedOidsList =>
        AllowedUserOids.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
