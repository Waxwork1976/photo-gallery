using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using PhotoFunctions.Models;
using PhotoFunctions.Services;

namespace PhotoFunctions.Functions;

/// <summary>
/// POST /api/auth-validate
/// Validates a JWT Bearer token and checks whether the caller is an authorized uploader.
/// </summary>
public sealed class AuthValidateFunction
{
    private readonly IJwtValidationService _jwtService;
    private readonly ILogger<AuthValidateFunction> _logger;

    public AuthValidateFunction(
        IJwtValidationService jwtService,
        ILogger<AuthValidateFunction> logger)
    {
        _jwtService = jwtService;
        _logger = logger;
    }

    [Function("auth-validate")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth-validate")] HttpRequestData req)
    {
        // 1. Extract bearer token
        var token = _jwtService.ExtractBearerToken(
            req.Headers.TryGetValues("Authorization", out var values)
                ? values.FirstOrDefault()
                : null);

        if (token is null)
        {
            _logger.LogWarning("auth-validate called without a Bearer token");
            return await CreateJsonResponse(req, HttpStatusCode.Unauthorized,
                new ErrorResponse("No token provided"));
        }

        // 2. Validate JWT signature & claims
        var principal = await _jwtService.ValidateTokenAsync(token);
        if (principal is null)
        {
            return await CreateJsonResponse(req, HttpStatusCode.Unauthorized,
                new ErrorResponse("Invalid token"));
        }

        // 3. Extract user claims
        var oid = principal.FindFirst("oid")?.Value
                   ?? principal.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;
        var email = principal.FindFirst("preferred_username")?.Value
                    ?? principal.FindFirst("email")?.Value ?? string.Empty;
        var name = principal.FindFirst("name")?.Value ?? string.Empty;

        if (string.IsNullOrEmpty(oid))
        {
            _logger.LogWarning("Token is valid but missing 'oid' claim");
            return await CreateJsonResponse(req, HttpStatusCode.Unauthorized,
                new ErrorResponse("Token missing required claims"));
        }

        // 4. Check authorization
        if (!_jwtService.IsAuthorizedUser(oid))
        {
            _logger.LogWarning("User {Oid} is not in the allowed list", oid);
            return await CreateJsonResponse(req, HttpStatusCode.Forbidden,
                new ErrorResponse("User not authorized"));
        }

        _logger.LogInformation("User {Name} ({Oid}) validated successfully", name, oid);

        return await CreateJsonResponse(req, HttpStatusCode.OK,
            new AuthValidateResponse(true, new AuthUserInfo(oid, email, name)));
    }

    // ----- Helper -----

    private static async Task<HttpResponseData> CreateJsonResponse<T>(
        HttpRequestData req, HttpStatusCode statusCode, T body)
    {
        var response = req.CreateResponse(statusCode);
        await response.WriteAsJsonAsync(body);
        return response;
    }
}
