using System.Text.Json;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Fallback.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Fallback.Middleware;

/// <summary>
/// Middleware that blocks write operations when fallback is active and returns friendly 503 responses when
/// one or both databases are unavailable.
/// </summary>
public sealed class DatabaseFallbackMiddleware(
    RequestDelegate next,
    IDatabaseFallbackService fallback,
    IOptions<FallbackOptions> options,
    ILogger<DatabaseFallbackMiddleware> logger)
{
    private const string MaintenanceMessage =
        "Our system is currently undergoing planned maintenance. Please try again later. Recommendation: Until further notice, avoid creating, updating, or deleting any resources. Your data is safe, but modifications may not be persisted. If you cannot find recently created items, please wait for the IT department to contact you.";

    private const string OutageMessage =
        "Please try again later. Our maintenance team is working to resolve this issue.";

    /// <summary>
    /// Executes the middleware.
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        if (!options.Value.Enabled)
        {
            await next(context);
            return;
        }

        var path = context.Request.Path.Value ?? string.Empty;
        if (!IsVersionedApiPath(path))
        {
            await next(context);
            return;
        }

        var active = fallback.GetActiveDatabase(path);
        if (active == DatabaseType.BothDown)
        {
            logger.LogWarning("Both databases are down; returning 503 for {Method} {Path}.", context.Request.Method, path);
            await Write503Async(context, OutageMessage);
            return;
        }

        if (IsWriteMethod(context.Request.Method) && fallback.IsFallbackActiveForVersion(path))
        {
            logger.LogWarning("Fallback active; blocking write {Method} {Path} with 503.", context.Request.Method, path);
            await Write503Async(context, MaintenanceMessage);
            return;
        }

        await next(context);
    }

    private static bool IsVersionedApiPath(string path) =>
        path.StartsWith("/api/v1/", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/api/v2/", StringComparison.OrdinalIgnoreCase) ||
        path.Equals("/api/v1", StringComparison.OrdinalIgnoreCase) ||
        path.Equals("/api/v2", StringComparison.OrdinalIgnoreCase);

    private static bool IsWriteMethod(string method) =>
        HttpMethods.IsPost(method) ||
        HttpMethods.IsPut(method) ||
        HttpMethods.IsPatch(method) ||
        HttpMethods.IsDelete(method);

    private static async Task Write503Async(HttpContext context, string message)
    {
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        context.Response.ContentType = "application/json";

        var payload = JsonSerializer.Serialize(new { error = message });
        await context.Response.WriteAsync(payload);
    }
}

