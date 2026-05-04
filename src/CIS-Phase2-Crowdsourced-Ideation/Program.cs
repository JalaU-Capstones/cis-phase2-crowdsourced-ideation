using CIS.Phase2.CrowdsourcedIdeation.Features;
using CIS.Phase2.CrowdsourcedIdeation.Features.Migration;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Middleware;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Fallback.Middleware;
using Microsoft.AspNetCore.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddFeatures();
builder.Services.AddMigrationServices(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var ex = context.Features.Get<IExceptionHandlerFeature>()?.Error;
        if (ex is not null)
        {
            var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger("GlobalExceptionHandler");
            logger.LogError(ex, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);
        }

        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(
            "{\"error\": \"An internal server error occurred. Please try again later.\"}");
    });
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "V1 (MySQL)");
        c.SwaggerEndpoint("/swagger/v2/swagger.json", "V2 (MongoDB)");
    });
}

app.UseMiddleware<MigrationSunsettingMiddleware>(); 

app.UseAuthentication();
app.UseMiddleware<DatabaseFallbackMiddleware>();
app.UseAuthorization();

app.MapFeatures();

app.Run();

public partial class Program { }