using CIS.Phase2.CrowdsourcedIdeation.Features;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure;
using Microsoft.AspNetCore.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddFeatures();

var app = builder.Build();

// Global Exception Handler to avoid leaking internal details
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync("{\"error\": \"An internal server error occurred. Please try again later.\"}");
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

app.UseAuthentication();
app.UseAuthorization();

app.MapFeatures();

app.Run();

public partial class Program { }
