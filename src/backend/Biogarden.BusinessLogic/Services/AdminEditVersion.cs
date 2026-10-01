namespace Biogarden.BusinessLogic.Services;

internal static class AdminEditVersion
{
    public static void RequireCurrent(string? expected, byte[] current)
    {
        if (string.IsNullOrWhiteSpace(expected))
            throw new ArgumentException("A mentéshez szükséges verzió hiányzik. Töltsd újra az adatokat.");
        if (!string.Equals(expected, Convert.ToBase64String(current), StringComparison.Ordinal))
            throw new InvalidOperationException("Az adatok időközben megváltoztak. Töltsd be az aktuális adatokat, majd ellenőrizd és alkalmazd újra a módosításaidat.");
    }
}
