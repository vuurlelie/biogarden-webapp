using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System.Text.Json;

namespace Biogarden.DataAccess;

public sealed class DesignTimeAppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseSqlServer(GetConnectionString());

        return new AppDbContext(optionsBuilder.Options);
    }

    private static string GetConnectionString()
    {
        var currentDirectory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (currentDirectory is not null
            && !Directory.Exists(Path.Combine(currentDirectory.FullName, "Biogarden.WebApi")))
        {
            currentDirectory = currentDirectory.Parent;
        }

        if (currentDirectory is null)
        {
            throw new InvalidOperationException("Could not locate the Biogarden.WebApi project directory.");
        }

        var webApiDirectory = Path.Combine(currentDirectory.FullName, "Biogarden.WebApi");
        var appsettingsPath = File.Exists(Path.Combine(webApiDirectory, "appsettings.Development.json"))
            ? Path.Combine(webApiDirectory, "appsettings.Development.json")
            : Path.Combine(webApiDirectory, "appsettings.json");

        using var stream = File.OpenRead(appsettingsPath);
        using var document = JsonDocument.Parse(stream);

        return document.RootElement
            .GetProperty("ConnectionStrings")
            .GetProperty("DefaultConnection")
            .GetString()
            ?? throw new InvalidOperationException("DefaultConnection is not configured.");
    }
}
