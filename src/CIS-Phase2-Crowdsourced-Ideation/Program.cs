using CIS.Phase2.CrowdsourcedIdeation.Features;
using CIS.Phase2.CrowdsourcedIdeation.Features.Migration;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Middleware;
using CIS.Phase2.CrowdsourcedIdeation.Services;
using Microsoft.AspNetCore.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddFeatures();
builder.Services.AddMigrationServices(builder.Configuration);

var app = builder.Build();

UserResolverAccessor.Current = app.Services.GetService<IUserResolver>();

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.StatusCode  = 500;
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
app.UseAuthorization();

app.MapFeatures();

app.Run();

public partial class Program { }