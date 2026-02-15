using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using PhotoFunctions.Configuration;

namespace PhotoFunctions.Services;

/// <summary>
/// Validates JWT tokens issued by Microsoft Entra ID.
/// Uses the OpenID Connect discovery document to get signing keys so that
/// key rotation is handled automatically.
/// </summary>
public sealed class JwtValidationService : IJwtValidationService
{
    private readonly AzureAdOptions _adOptions;
    private readonly ConfigurationManager<OpenIdConnectConfiguration> _configManager;
    private readonly ILogger<JwtValidationService> _logger;

    public JwtValidationService(
        IOptions<AzureAdOptions> adOptions,
        ILogger<JwtValidationService> logger)
    {
        _adOptions = adOptions.Value;
        _logger = logger;

        // The ConfigurationManager caches the OIDC metadata + JWKS keys
        // and refreshes them automatically every 24 h (or on failure).
        _configManager = new ConfigurationManager<OpenIdConnectConfiguration>(
            _adOptions.MetadataAddress,
            new OpenIdConnectConfigurationRetriever(),
            new HttpDocumentRetriever());
    }

    /// <inheritdoc />
    public async Task<ClaimsPrincipal?> ValidateTokenAsync(string token)
    {
        try
        {
            var oidcConfig = await _configManager.GetConfigurationAsync(CancellationToken.None);

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = _adOptions.Authority,

                ValidateAudience = true,
                ValidAudience = _adOptions.ClientId,

                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(2),

                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = oidcConfig.SigningKeys,

                // Map standard claim types so we can use ClaimTypes constants
                NameClaimType = "name",
                RoleClaimType = "roles",
            };

            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(token, validationParameters, out _);
            return principal;
        }
        catch (SecurityTokenException ex)
        {
            _logger.LogWarning(ex, "JWT validation failed: {Message}", ex.Message);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during JWT validation");
            return null;
        }
    }

    /// <inheritdoc />
    public bool IsAuthorizedUser(string oid)
    {
        return _adOptions.AllowedOidsList.Contains(oid, StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public string? ExtractBearerToken(string? authorizationHeader)
    {
        if (string.IsNullOrWhiteSpace(authorizationHeader))
            return null;

        // Expect "Bearer <token>"
        const string bearerPrefix = "Bearer ";
        if (!authorizationHeader.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
            return null;

        var token = authorizationHeader[bearerPrefix.Length..].Trim();
        return string.IsNullOrEmpty(token) ? null : token;
    }
}
