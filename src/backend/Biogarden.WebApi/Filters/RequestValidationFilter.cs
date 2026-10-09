using Biogarden.WebApi.Contracts;
using FluentValidation;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Biogarden.WebApi.Filters;

// Explicit asynchronous validation before the ApiController model-state filter (-2000).
public sealed class RequestValidationFilter : IAsyncActionFilter, IOrderedFilter
{
    public int Order => -2500;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // Binding errors (including malformed JSON) must not reach validators with partial objects.
        if (context.ModelState.IsValid)
        {
            foreach (var argument in context.ActionArguments.Values)
            {
                if (argument is null)
                    continue;
                var serviceType = typeof(IValidator<>).MakeGenericType(argument.GetType());
                if (context.HttpContext.RequestServices.GetService(serviceType) is not IValidator validator)
                    continue;
                var validationContext = new ValidationContext<object>(argument);
                validationContext.RootContextData["Authenticated"] = context.HttpContext.User.Identity?.IsAuthenticated == true;
                var result = await validator.ValidateAsync(validationContext, context.HttpContext.RequestAborted);
                foreach (var error in result.Errors)
                    context.ModelState.AddModelError(ApiProblems.FieldName(error.PropertyName), error.ErrorMessage);
            }
        }

        if (!context.ModelState.IsValid)
        {
            context.Result = ApiProblems.InvalidModel(context);
            return;
        }
        await next();
    }
}
