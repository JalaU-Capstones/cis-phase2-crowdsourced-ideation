using CIS.Phase2.CrowdsourcedIdeation.Features;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure;
using CIS.Phase2.CrowdsourcedIdeation.Services;
using Microsoft.AspNetCore.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddFeatures();

var app = builder.Build();

// Some services are constructed manually in endpoint code; expose the user resolver for V2 flows.
UserResolverAccessor.Current = app.Services.GetService<IUserResolver>();

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
