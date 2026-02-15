using System.Security.Claims;

namespace PhotoFunctions.Services;

public interface IJwtValidationService
{
    /// <summary>
    /// Validates the JWT Bearer token from the Authorization header.
    /// Returns the <see cref="ClaimsPrincipal"/> on success, or null if the token is invalid.
    /// </summary>
    Task<ClaimsPrincipal?> ValidateTokenAsync(string token);

    /// <summary>
    /// Checks whether the given Object ID (oid) is in the authorized user list.
    /// </summary>
    bool IsAuthorizedUser(string oid);

    /// <summary>
    /// Extracts the Bearer token from an Authorization header value.
    /// Returns null if the header is missing or malformed.
    /// </summary>
    string? ExtractBearerToken(string? authorizationHeader);
}
