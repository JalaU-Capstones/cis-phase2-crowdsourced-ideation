namespace CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Middleware;

public sealed class MigrationSunsettingMiddleware
{
    private const string MaintenanceJson =
        """{"error":"Service is under maintenance. Write operations are temporarily disabled. Please try again shortly."}""";

    private const string GoneJson =
        """{"error":"API v1 has been permanently decommissioned. Please use /api/v2/ for all write operations."}""";

    private const string DeprecationWarning =
        """299 - "API v1 is deprecated. Please migrate to /api/v2/" """;

    private readonly RequestDelegate _next;
    private readonly MigrationStateManager _state;
    private readonly ILogger<MigrationSunsettingMiddleware> _logger;

    public MigrationSunsettingMiddleware(
        RequestDelegate next,
        MigrationStateManager state,
        ILogger<MigrationSunsettingMiddleware> logger)
    {
        _next   = next;
        _state  = state;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var method    = context.Request.Method;
        var path      = context.Request.Path.Value ?? string.Empty;
        var isWriteOp = HttpMethods.IsPost(method) || HttpMethods.IsPut(method) || HttpMethods.IsDelete(method);
        var isApiPath = path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase);
        var isV1Path  = path.StartsWith("/api/v1/", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(path, "/api/v1", StringComparison.OrdinalIgnoreCase);
        
        if (_state.IsMigrationRunning && isWriteOp && isApiPath)
        {
            _logger.LogWarning(
                "503 Maintenance – blocked {Method} {Path} (migration running)", method, path);
            context.Response.StatusCode  = StatusCodes.Status503ServiceUnavailable;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(MaintenanceJson);
            return;
        }
        
        if (_state.HasMigrated && isV1Path)
        {
            if (isWriteOp)
            {
                _logger.LogWarning(
                    "410 Gone – blocked {Method} {Path} (v1 permanently decommissioned)", method, path);
                context.Response.StatusCode  = StatusCodes.Status410Gone;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(GoneJson);
                return;
            }

            context.Response.Headers["Warning"] = DeprecationWarning;
        }

        await _next(context);
    }
}