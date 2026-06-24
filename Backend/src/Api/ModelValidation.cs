using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;

namespace Api;

public class ValidateModelAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (!context.ModelState.IsValid)
        {
            context.Result = new BadRequestObjectResult(context.ModelState);
        }
    }
}

public class ValidateModelEndpointFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var modelState = new ModelStateDictionary();

        foreach (var arg in context.Arguments)
        {
            if (arg == null)
            {
                continue;
            }

            var validationContext = new ValidationContext(arg, context.HttpContext.RequestServices, null);
            var validationResults = new List<ValidationResult>();

            if (!Validator.TryValidateObject(arg, validationContext, validationResults, true))
            {
                foreach (var validationResult in validationResults)
                {
                    var memberNames = validationResult.MemberNames.Any()
                        ? validationResult.MemberNames
                        : new[] { string.Empty };

                    foreach (var memberName in memberNames)
                    {
                        modelState.AddModelError(memberName, validationResult.ErrorMessage ?? "The request is invalid.");
                    }
                }
            }
        }

        if (!modelState.IsValid)
        {
            var errors = modelState.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Errors.Select(e => e.ErrorMessage ?? "Invalid value.").ToArray());
            return Results.ValidationProblem(errors);
        }

        return await next(context);
    }
}
