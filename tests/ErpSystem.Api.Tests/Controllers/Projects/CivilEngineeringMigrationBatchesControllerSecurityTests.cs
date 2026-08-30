using System.Reflection;
using ErpSystem.Api.Controllers.Projects;
using ErpSystem.Core.Services.Projects;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Projects;

public sealed class CivilEngineeringMigrationBatchesControllerSecurityTests
{
    [Fact]
    public void Controller_requires_authentication_and_uses_the_project_scoped_migration_permission()
    {
        var type = typeof(CivilEngineeringMigrationBatchesController);
        type.GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull();
        type.GetMethod(nameof(CivilEngineeringMigrationBatchesController.Lookups))!.GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(CivilEngineeringAccessControlRegistry.MigrationManage);
        type.GetMethod(nameof(CivilEngineeringMigrationBatchesController.List))!.GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(CivilEngineeringAccessControlRegistry.WorkspaceRead);
        type.GetMethod(nameof(CivilEngineeringMigrationBatchesController.Stage))!.GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(CivilEngineeringAccessControlRegistry.MigrationManage);
        type.GetMethod(nameof(CivilEngineeringMigrationBatchesController.Reconcile))!.GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(CivilEngineeringAccessControlRegistry.MigrationManage);
        type.GetMethod(nameof(CivilEngineeringMigrationBatchesController.SignOff))!.GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(CivilEngineeringAccessControlRegistry.MigrationManage);
        type.GetMethod(nameof(CivilEngineeringMigrationBatchesController.History))!.GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(CivilEngineeringAccessControlRegistry.AuditRead);
    }
}
