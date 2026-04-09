using CIS.Phase2.CrowdsourcedIdeation.Features;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure;
using Microsoft.AspNetCore.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddFeatures(); // Add this line

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
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapFeatures();

app.Run();

public partial class Program { }