using Biogarden.WebApi.Middleware;
using Serilog;

namespace Biogarden.WebApi.Extensions;

public static class ApplicationBuilderExtensions
{
    public static WebApplication UseAppPipeline(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseMiddleware<ExceptionHandlingMiddleware>();

        app.UseSerilogRequestLogging();

        app.UseHttpsRedirection();

        app.UseStaticFiles();

        app.UseCors("FrontendDev");

        app.UseAuthentication();

        app.UseAuthorization();

        app.MapControllers();

        return app;
    }
}
