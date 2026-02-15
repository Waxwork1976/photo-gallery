using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using PhotoFunctions.Models;

namespace PhotoFunctions.Functions;

/// <summary>
/// GET /api/health
/// Simple health-check endpoint (no authentication required).
/// Useful for monitoring and deployment verification.
/// </summary>
public sealed class HealthFunction
{
    private static readonly string Version =
        Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? "1.0.0";

    [Function("health")]
    public IActionResult Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] HttpRequest req)
    {
        return new OkObjectResult(new HealthResponse(
            Status: "healthy",
            Timestamp: DateTimeOffset.UtcNow.ToString("o"),
            Version: Version));
    }
}
