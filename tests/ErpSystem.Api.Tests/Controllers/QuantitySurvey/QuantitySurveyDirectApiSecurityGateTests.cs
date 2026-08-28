using System.Reflection;
using ErpSystem.Api.Controllers.Projects;
using ErpSystem.Api.Controllers.QuantitySurvey;
using ErpSystem.Core.Services.QuantitySurvey;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.QuantitySurvey;

public sealed class QuantitySurveyDirectApiSecurityGateTests
{
    private static readonly HashSet<string> RegisteredPolicies = QuantitySurveyAccessControlRegistry.Permissions
        .Select(value => value.Code)
        .ToHashSet(StringComparer.Ordinal);

    [Fact]
    public void EveryQuantitySurveyControllerAction_ShouldRequireARegisteredQsPolicy()
    {
        var controllerAssembly = typeof(QuantitySurveyConfigurationProfilesController).Assembly;
        var actions = controllerAssembly.GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract &&
                           type.Namespace == "ErpSystem.Api.Controllers.QuantitySurvey")
            .SelectMany(HttpActions)
            .ToList();

        actions.Should().NotBeEmpty();
        AssertProtected(actions);
    }

    [Fact]
    public void ProjectHostedQuantitySurveyActions_ShouldRequireARegisteredQsPolicy()
    {
        var actions = HttpActions(typeof(ProjectsController))
            .Where(value =>
                value.Method.Name.Contains("QuantitySurvey", StringComparison.Ordinal) ||
                value.Method.Name.Contains("ProjectBoq", StringComparison.Ordinal) ||
                value.Routes.Any(route => route.Contains("quantity-survey", StringComparison.OrdinalIgnoreCase) ||
                                          route.Contains("boq", StringComparison.OrdinalIgnoreCase)))
            .ToList();

        actions.Should().NotBeEmpty();
        AssertProtected(actions);
    }

    [Fact]
    public void ProjectHostedQuantitySurveyLookup_ShouldHideForeignTenantProjectsAsNotFound()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root, "src", "ErpSystem.Api", "Controllers", "Projects", "ProjectsController.cs"));

        source.Should().Contain("QS_PROJECT_NOT_FOUND");
        source.Should().Contain("outside your tenant scope");
        source.Should().Contain("return NotFound(new ProblemDetails");
    }

    private static void AssertProtected(IReadOnlyCollection<ControllerAction> actions)
    {
        var anonymous = actions.Where(value => value.AllowsAnonymous)
            .Select(Describe)
            .ToList();
        anonymous.Should().BeEmpty("QS API actions must never bypass authentication");

        var unauthenticated = actions.Where(value => !value.RequiresAuthentication)
            .Select(Describe)
            .ToList();
        unauthenticated.Should().BeEmpty("every internal and external QS API action requires authentication");

        var missing = actions.Where(value => !value.IsExternalPortal && value.Policies.Count == 0)
            .Select(Describe)
            .ToList();
        missing.Should().BeEmpty("every internal QS API action requires an explicit permission policy");

        var invalidExternalRoute = actions.Where(value => value.IsExternalPortal &&
                value.Routes.All(route =>
                    !route.Contains("external/my-projects", StringComparison.OrdinalIgnoreCase) &&
                    !route.Contains("external-portal", StringComparison.OrdinalIgnoreCase)))
            .Select(Describe)
            .ToList();
        invalidExternalRoute.Should().BeEmpty(
            "external QS actions must stay behind the controlled external project route and service-level partner/project guard");

        var unknown = actions.SelectMany(value => value.Policies
                .Where(policy => !RegisteredPolicies.Contains(policy))
                .Select(policy => $"{Describe(value)} => {policy}"))
            .ToList();
        unknown.Should().BeEmpty("QS API actions may use only centrally registered QS permission policies");
    }

    private static IReadOnlyList<ControllerAction> HttpActions(Type controllerType) =>
        controllerType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Select(method => new
            {
                Method = method,
                Routes = method.GetCustomAttributes<HttpMethodAttribute>(true).ToArray()
            })
            .Where(value => value.Routes.Length > 0)
            .Select(value => new ControllerAction(
                controllerType,
                value.Method,
                controllerType.GetCustomAttributes(true).OfType<IRouteTemplateProvider>()
                    .Concat(value.Routes)
                    .Select(route => route.Template ?? string.Empty)
                    .ToArray(),
                AuthorizeAttributes(controllerType, value.Method)
                    .Select(attribute => attribute.Policy)
                    .Where(policy => !string.IsNullOrWhiteSpace(policy))
                    .Cast<string>()
                    .Distinct(StringComparer.Ordinal)
                    .ToArray(),
                AuthorizeAttributes(controllerType, value.Method).Count > 0,
                controllerType.Name.StartsWith("External", StringComparison.Ordinal),
                controllerType.IsDefined(typeof(AllowAnonymousAttribute), true) ||
                value.Method.IsDefined(typeof(AllowAnonymousAttribute), true)))
            .ToList();

    private static IReadOnlyList<AuthorizeAttribute> AuthorizeAttributes(Type controllerType, MethodInfo method) =>
        controllerType.GetCustomAttributes<AuthorizeAttribute>(true)
            .Concat(method.GetCustomAttributes<AuthorizeAttribute>(true))
            .ToList();

    private static string Describe(ControllerAction value) =>
        $"{value.Controller.Name}.{value.Method.Name}";

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "src")) &&
                File.Exists(Path.Combine(current.FullName, "ErpSystem.sln")))
                return current.FullName;
            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }

    private sealed record ControllerAction(
        Type Controller,
        MethodInfo Method,
        IReadOnlyList<string> Routes,
        IReadOnlyList<string> Policies,
        bool RequiresAuthentication,
        bool IsExternalPortal,
        bool AllowsAnonymous);
}
