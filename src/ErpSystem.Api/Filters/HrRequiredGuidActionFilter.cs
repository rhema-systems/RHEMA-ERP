using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;
using ErpSystem.Core.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ErpSystem.Api.Filters;

/// <summary>
/// Makes <c>[Required]</c> mean what it says on a non-nullable <see cref="Guid"/>.
/// </summary>
/// <remarks>
/// <para><b>The defect (ledger D-17).</b> <c>[Required]</c> rejects a missing value, and
/// <see cref="Guid.Empty"/> is not missing — it is the default a JSON body produces when the property
/// is absent. So <c>[Required] public Guid OwnerId</c> passed model validation with all zeros, reached
/// the database, and died on the foreign key as error 547 — surfacing as a 500 that named neither the
/// field nor the constraint. <c>POST api/talent-pools</c> was the instance that made it visible.</para>
///
/// <para><b>⚠ Why this is opt-in rather than a blanket rule over the HR DTO namespace.</b> An empty
/// required Guid at validation time is not necessarily the caller's mistake: a controller may be about
/// to fill it in. There are two distinct classes of that, both found by suites refusing correct
/// requests rather than by reading the code:</para>
/// <list type="number">
///   <item><description><b>Route-filled.</b> <c>POST descriptions/{jobDescriptionId}/duty-items</c>
///   binds the body, then assigns <c>dto.JobDescriptionId</c> from the route. Matching property names
///   against route keys does not rescue it either: <c>POST responsibilities/{responsibilityId}/kpis</c>
///   fills <c>JobResponsibilityId</c>, and the only thing connecting those two names is an assignment
///   statement no reflection can see.</description></item>
///   <item><description><b>Token-filled.</b> On routes carrying no id at all, controllers assign
///   <c>dto.ReportedById</c>, <c>dto.EmployeeId</c>, <c>dto.InitiatedById</c>, <c>ScheduledById</c>,
///   <c>ConductedById</c> and others from the authenticated employee — deliberately, so a caller
///   cannot assert who acted.</description></item>
/// </list>
///
/// <para>Both classes are invisible to any rule based on names or route shape, and getting either
/// wrong refuses a request that has always been correct. So a DTO earns this check by carrying
/// <see cref="CallerSuppliesIdentifiersAttribute"/> — a claim someone made deliberately about that
/// payload. Adding it is one line; the blanket version cost a working screen instead.</para>
///
/// <para>Ordering: <c>[ApiController]</c>'s own invalid-model-state filter runs at −2000 and
/// short-circuits before this one, so a body that already failed validation never reaches here. When
/// it does reach here the model was otherwise valid, which is why this filter produces the response
/// itself rather than only adding to <c>ModelState</c>.</para>
/// </remarks>
public sealed class HrRequiredGuidActionFilter : IActionFilter
{
    /// <summary>Reflection is done once per DTO type, not once per request.</summary>
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> Offenders = new();

    public void OnActionExecuting(ActionExecutingContext context)
    {
        List<string>? emptied = null;

        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null) continue;

            foreach (var property in Offenders.GetOrAdd(argument.GetType(), RequiredGuidProperties))
            {
                // Belt and braces for an opted-in DTO used under a nested route: the route already
                // carries this value and the controller is about to copy it across.
                if (context.RouteData.Values.ContainsKey(property.Name)) continue;

                if (property.GetValue(argument) is not Guid value || value != Guid.Empty) continue;

                var name = JsonNamingPolicy.CamelCase.ConvertName(property.Name);
                context.ModelState.AddModelError(
                    name, $"The {name} field requires an identifier; it was empty.");
                (emptied ??= new List<string>()).Add(name);
            }
        }

        if (emptied is null) return;

        // The detail line names the fields. A validation response whose only readable text is
        // "One or more validation errors occurred" costs the caller the same hour the 500 did.
        context.Result = new BadRequestObjectResult(new ValidationProblemDetails(context.ModelState)
        {
            Detail = emptied.Count == 1
                ? $"The {emptied[0]} field requires an identifier; it was empty."
                : $"These fields require an identifier and were empty: {string.Join(", ", emptied)}.",
        });
    }

    public void OnActionExecuted(ActionExecutedContext context) { }

    private static PropertyInfo[] RequiredGuidProperties(Type type)
    {
        if (!type.IsDefined(typeof(CallerSuppliesIdentifiersAttribute), inherit: true))
            return Array.Empty<PropertyInfo>();

        return type
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType == typeof(Guid)
                        && p.CanRead
                        && p.IsDefined(typeof(RequiredAttribute), inherit: true))
            .ToArray();
    }
}
