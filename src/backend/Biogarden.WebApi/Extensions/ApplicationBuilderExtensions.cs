using Biogarden.WebApi.Middleware;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Serilog;

namespace Biogarden.WebApi.Extensions;

public static class ApplicationBuilderExtensions
{
    public static WebApplication UseAppPipeline(this WebApplication app)
    {
        app.UseDevelopmentDocumentation();
        app.UseRequestInfrastructure();
        app.UseStaticContent();
        app.UseApiAccessControls();
        app.MapAppEndpoints();

        return app;
    }

    private static WebApplication UseDevelopmentDocumentation(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        return app;
    }

    private static WebApplication UseRequestInfrastructure(this WebApplication app)
    {
        app.UseForwardedHeaders();
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseMiddleware<ExceptionHandlingMiddleware>();
        app.UseMiddleware<SecurityHeadersMiddleware>();
        app.UseSerilogRequestLogging();
        app.UseHttpsRedirection();

        return app;
    }

    private static WebApplication UseStaticContent(this WebApplication app)
    {
        app.UseStaticFiles();

        return app;
    }

    private static WebApplication UseApiAccessControls(this WebApplication app)
    {
        app.UseCors("FrontendDev");
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();

        return app;
    }

    private static WebApplication MapAppEndpoints(this WebApplication app)
    {
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains("live")
        });

        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains("ready")
        });

        app.MapControllers();

        return app;
    }
}
