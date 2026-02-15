using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
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
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth-validate")] HttpRequest req)
    {
        // 1. Extract bearer token
        var token = _jwtService.ExtractBearerToken(
            req.Headers.Authorization.FirstOrDefault());

        if (token is null)
        {
            _logger.LogWarning("auth-validate called without a Bearer token");
            return new UnauthorizedObjectResult(new ErrorResponse("No token provided"));
        }

        // 2. Validate JWT signature & claims
        var principal = await _jwtService.ValidateTokenAsync(token);
        if (principal is null)
        {
            return new UnauthorizedObjectResult(new ErrorResponse("Invalid token"));
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
            return new UnauthorizedObjectResult(new ErrorResponse("Token missing required claims"));
        }

        // 4. Check authorization
        if (!_jwtService.IsAuthorizedUser(oid))
        {
            _logger.LogWarning("User {Oid} is not in the allowed list", oid);
            return new ObjectResult(new ErrorResponse("User not authorized")) { StatusCode = StatusCodes.Status403Forbidden };
        }

        _logger.LogInformation("User {Name} ({Oid}) validated successfully", name, oid);

        return new OkObjectResult(new AuthValidateResponse(true, new AuthUserInfo(oid, email, name)));
    }
}
