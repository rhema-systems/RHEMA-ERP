using System.Reflection;
using ErpSystem.Api.Controllers.Projects;
using ErpSystem.Core.Services.Projects;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Projects;

public sealed class CivilEngineeringSiteInstructionsControllerSecurityTests
{
    [Fact]
    public void Controller_is_authenticated_and_internal_routes_use_central_civil_permissions()
    {
        var type = typeof(CivilEngineeringSiteInstructionsController);
        type.GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull();
        type.GetMethod(nameof(CivilEngineeringSiteInstructionsController.Lookups))!.GetCustomAttribute<AuthorizeAttribute>()!.Policy
            .Should().Be(CivilEngineeringAccessControlRegistry.WorkspaceRead);
        type.GetMethod(nameof(CivilEngineeringSiteInstructionsController.List))!.GetCustomAttribute<AuthorizeAttribute>()!.Policy
            .Should().Be(CivilEngineeringAccessControlRegistry.WorkspaceRead);
        type.GetMethod(nameof(CivilEngineeringSiteInstructionsController.Issue))!.GetCustomAttribute<AuthorizeAttribute>()!.Policy
            .Should().Be(CivilEngineeringAccessControlRegistry.SupervisionManage);
        type.GetMethod(nameof(CivilEngineeringSiteInstructionsController.ProcessRouting))!.GetCustomAttribute<AuthorizeAttribute>()!.Policy
            .Should().Be(CivilEngineeringAccessControlRegistry.SupervisionManage);
        type.GetMethod(nameof(CivilEngineeringSiteInstructionsController.ReviewContractorResponse))!.GetCustomAttribute<AuthorizeAttribute>()!.Policy
            .Should().Be(CivilEngineeringAccessControlRegistry.SupervisionManage);
        type.GetMethod(nameof(CivilEngineeringSiteInstructionsController.RecordEngineeringFollowUp))!.GetCustomAttribute<AuthorizeAttribute>()!.Policy
            .Should().Be(CivilEngineeringAccessControlRegistry.SupervisionManage);
    }

    [Fact]
    public void Contractor_route_never_allows_anonymous_access_and_delegates_scope_to_the_controlled_service()
    {
        var route = typeof(CivilEngineeringSiteInstructionsController).GetMethod(nameof(CivilEngineeringSiteInstructionsController.ContractorResponse))!;
        route.GetCustomAttributes<HttpMethodAttribute>().Should().ContainSingle();
        route.GetCustomAttribute<AllowAnonymousAttribute>().Should().BeNull();
        typeof(CivilEngineeringSiteInstructionsController).GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull();

        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Data", "Services", "CivilEngineeringSiteInstructionService.cs"));
        source.Should().Contain("RequireExternalContractorAsync(routing, token)")
            .And.Contain("partner.Id != routing.ContractorBusinessPartnerId")
            .And.Contain("project.ExternalPortalAccessEnabled")
            .And.Contain("policy.CanComment");
    }

    private static string FindRepositoryRoot([System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "")
    {
        for (var directory = new FileInfo(sourceFile).Directory; directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "ErpSystem.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException("Repository root containing ErpSystem.sln was not found.");
    }
}
