using System.Net;
using System.Reflection;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
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
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] HttpRequestData req)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new HealthResponse(
            Status: "healthy",
            Timestamp: DateTimeOffset.UtcNow.ToString("o"),
            Version: Version));
        return response;
    }
}
