using System.Reflection;
using ErpSystem.Api.Controllers.Projects;
using ErpSystem.Core.Services.Projects;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Projects;

public sealed class CivilEngineeringRfisControllerSecurityTests
{
    [Fact]
    public void Controller_is_authenticated_and_internal_routes_use_the_central_civil_permissions()
    {
        var type = typeof(CivilEngineeringRfisController);
        type.GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull();
        type.GetMethod(nameof(CivilEngineeringRfisController.Lookups))!.GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(CivilEngineeringAccessControlRegistry.WorkspaceRead);
        type.GetMethod(nameof(CivilEngineeringRfisController.List))!.GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(CivilEngineeringAccessControlRegistry.WorkspaceRead);
        type.GetMethod(nameof(CivilEngineeringRfisController.SubmitProjectEngineerResponse))!.GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(CivilEngineeringAccessControlRegistry.SupervisionManage);
        type.GetMethod(nameof(CivilEngineeringRfisController.ProcessProjectManagerResponse))!.GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(CivilEngineeringAccessControlRegistry.SupervisionManage);
    }

    [Fact]
    public void External_creation_never_allows_anonymous_access_and_derives_partner_scope_in_the_service()
    {
        var create = typeof(CivilEngineeringRfisController).GetMethod(nameof(CivilEngineeringRfisController.CreateExternal))!;
        create.GetCustomAttributes<HttpMethodAttribute>().Should().ContainSingle();
        create.GetCustomAttribute<AllowAnonymousAttribute>().Should().BeNull();

        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Data", "Services", "CivilEngineeringRfiService.cs"));
        source.Should().Contain("businessPartnerService.GetByUserIdAsync(UserId)")
            .And.Contain("value.ExternalPortalAccessEnabled")
            .And.Contain("value.CanComment")
            .And.Contain("partner.Id != routing.ExternalBusinessPartnerId");
    }

    private static string FindRepositoryRoot([System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "")
    {
        for (var directory = new FileInfo(sourceFile).Directory; directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "ErpSystem.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException("Repository root containing ErpSystem.sln was not found.");
    }
}
