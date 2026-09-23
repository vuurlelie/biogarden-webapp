namespace Biogarden.BusinessLogic.Validation;

public static class ValidationRules
{
    public static bool ValidTaxNumber(string? value) => value is not null &&
        System.Text.RegularExpressions.Regex.IsMatch(value.Trim(), @"^\d{8}(-\d-\d{2})?$");
}
