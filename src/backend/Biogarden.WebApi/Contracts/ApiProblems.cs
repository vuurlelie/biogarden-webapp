using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Biogarden.WebApi.Contracts;

public static class ApiProblems
{
    public static ApiErrorResponse Create(HttpContext context, int status, string? detail = null,
        IDictionary<string, string[]>? errors = null) => new()
        {
            Status = status,
            Title = status switch
            {
                400 => "Hibás adatok.",
                401 => "Bejelentkezés szükséges.",
                403 => "Nincs jogosultság a művelethez.",
                404 => "A kért adat nem található.",
                409 => "A művelet ütközik az aktuális állapottal.",
                429 => "Túl sok kérés.",
                _ => "A művelet nem sikerült."
            },
            Detail = detail,
            TraceId = context.TraceIdentifier,
            Instance = context.Request.Path,
            Errors = errors
        };

    public static ObjectResult Result(ApiErrorResponse problem)
    {
        var result = new ObjectResult(problem) { StatusCode = problem.Status };
        result.ContentTypes.Add("application/problem+json");
        return result;
    }

    public static ObjectResult InvalidModel(ActionContext context) => Result(Create(context.HttpContext, 400,
        "Ellenőrizd a megadott adatokat.", FieldErrors(context.ModelState)));

    public static Dictionary<string, string[]> FieldErrors(ModelStateDictionary modelState) => modelState
        .Where(pair => pair.Value?.Errors.Count > 0)
        .GroupBy(pair => FieldName(pair.Key))
        .ToDictionary(group => group.Key, group => group.SelectMany(pair => pair.Value!.Errors)
            .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage)
                ? "Érvénytelen érték." : error.ErrorMessage).Distinct().ToArray());

    public static string FieldName(string name) => string.Join('.', name.TrimStart('$', '.')
        .Split('.').Select(JsonNamingPolicy.CamelCase.ConvertName));
}
