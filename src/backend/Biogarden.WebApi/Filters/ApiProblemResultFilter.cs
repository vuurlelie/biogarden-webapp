using System.Text.Json;
using Biogarden.WebApi.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Biogarden.WebApi.Filters;

// Normalize legacy controller errors at the HTTP boundary without changing their status semantics.
public sealed class ApiProblemResultFilter : IAlwaysRunResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context)
    {
        var status = context.Result switch
        {
            ObjectResult result => result.StatusCode,
            StatusCodeResult result => result.StatusCode,
            _ => null
        };
        if (status is null or < 400 or >= 500) return;

        var value = (context.Result as ObjectResult)?.Value;
        var problem = value as ApiErrorResponse;
        if (problem is null)
        {
            string? detail = (value as ProblemDetails)?.Detail;
            if (detail is null && value is not null)
            {
                var json = JsonSerializer.SerializeToElement(value);
                if (json.ValueKind == JsonValueKind.Object)
                {
                    foreach (var property in json.EnumerateObject())
                        if (property.Name is "message" or "detail" && property.Value.ValueKind == JsonValueKind.String)
                            detail = property.Value.GetString();
                }
            }
            problem = ApiProblems.Create(context.HttpContext, status.Value, detail);
            if (value is ProblemDetails existing)
            {
                problem.Title = existing.Title ?? problem.Title;
                if (existing is ValidationProblemDetails validation) problem.Errors = validation.Errors;
            }
        }
        problem.TraceId = context.HttpContext.TraceIdentifier;
        problem.Instance = context.HttpContext.Request.Path;
        context.Result = ApiProblems.Result(problem);
    }

    public void OnResultExecuted(ResultExecutedContext context) { }
}
