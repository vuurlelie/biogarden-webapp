using System.Diagnostics;

namespace Biogarden.WebApi.Tests;

public sealed class StartupProcessTests
{
    [Fact]
    public async Task InvalidRequiredConfiguration_LogsFatalAndExitsWithFailure()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"BiogardenStartup-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var start = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = directory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            start.ArgumentList.Add(typeof(Program).Assembly.Location);
            // Command-line configuration overrides any developer machine environment.
            start.ArgumentList.Add("--ConnectionStrings:DefaultConnection=");
            start.ArgumentList.Add("--Jwt:SecretKey=");
            start.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
            using var process = Process.Start(start)!;
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            finally
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            var messages = await output + await error;
            Assert.NotEqual(0, process.ExitCode);
            Assert.Contains("Application terminated unexpectedly.", messages);
            Assert.DoesNotContain("Now listening on", messages);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
