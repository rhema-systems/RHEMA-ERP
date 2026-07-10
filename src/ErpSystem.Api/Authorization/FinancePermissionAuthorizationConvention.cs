using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Mvc.Routing;

namespace ErpSystem.Api.Authorization;

public sealed class FinancePermissionAuthorizationConvention : IControllerModelConvention
{
    private const string FinanceControllerNamespace = "ErpSystem.Api.Controllers.Finance";

    public void Apply(ControllerModel controller)
    {
        if (!IsFinanceController(controller))
        {
            return;
        }

        foreach (var action in controller.Actions)
        {
            if (HasAllowAnonymous(action))
            {
                continue;
            }

            var httpMethods = action.Attributes
                .OfType<IActionHttpMethodProvider>()
                .SelectMany(attribute => attribute.HttpMethods)
                .ToArray();

            var routeTemplates = action.Attributes
                .OfType<HttpMethodAttribute>()
                .Select(attribute => attribute.Template)
                .ToArray();

            var policies = FinancePermissionPolicyMap.GetRequiredPolicies(
                controller.ControllerName,
                action.ActionName,
                httpMethods,
                routeTemplates);

            foreach (var policy in policies)
            {
                action.Filters.Add(new AuthorizeFilter(policy));
            }
        }
    }

    private static bool IsFinanceController(ControllerModel controller)
        => string.Equals(
                controller.ControllerType.Namespace,
                FinanceControllerNamespace,
                StringComparison.Ordinal) ||
           string.Equals(controller.ControllerName, "Finance", StringComparison.Ordinal);

    private static bool HasAllowAnonymous(ActionModel action)
        => action.Attributes.OfType<IAllowAnonymous>().Any() ||
           action.Controller.Attributes.OfType<IAllowAnonymous>().Any();
}
