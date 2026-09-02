using ErpSystem.Core.Entities;
using ErpSystem.Core.Services.Projects;
using ErpSystem.Data;
using ErpSystem.Data.Seeders;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Projects;

public sealed class CivilEngineeringAccessControlRegistryTests
{
    private static readonly CivilEngineeringAccessScopeFacts FullScope = new(
        IsTenantMatch: true,
        HasPermission: true,
        HasProjectScope: true,
        HasAssetOrBuildingScope: true,
        HasSectionScope: true,
        IsAssigned: true,
        HasConfiguredAuthority: true,
        IsIndependentChecker: true);

    [Fact]
    public void Roles_and_operations_reference_only_registered_shared_permissions()
    {
        var permissions = CivilEngineeringAccessControlRegistry.Permissions
            .Select(value => value.Code)
            .ToHashSet(StringComparer.Ordinal);

        permissions.Should().HaveCount(CivilEngineeringAccessControlRegistry.Permissions.Count);
        CivilEngineeringAccessControlRegistry.Roles.Select(value => value.Code)
            .Should().OnlyHaveUniqueItems();
        CivilEngineeringAccessControlRegistry.Roles.SelectMany(value => value.Permissions)
            .Should().OnlyContain(value => permissions.Contains(value));
        CivilEngineeringAccessControlRegistry.Operations.Select(value => value.Operation)
            .Should().OnlyHaveUniqueItems();
        CivilEngineeringAccessControlRegistry.Operations.Select(value => value.Permission)
            .Should().OnlyContain(value => permissions.Contains(value));
        CivilEngineeringAccessControlRegistry.Roles
            .Single(value => value.Code == CivilEngineeringAccessControlRegistry.CivilEngineerRole)
            .Permissions.Should().Contain(CivilEngineeringAccessControlRegistry.AssignmentsManage,
                "the controller and domain policy permit an independent Civil Engineer to review direct-task completion");
    }

    [Fact]
    public void Civil_project_roles_extend_shared_project_membership_without_granting_finance_permissions()
    {
        CivilEngineeringAccessControlRegistry.IsManagementProjectRole(
            CivilEngineeringAccessControlRegistry.HeadRole).Should().BeTrue();
        CivilEngineeringAccessControlRegistry.IsManagementProjectRole(
            CivilEngineeringAccessControlRegistry.SupervisingEngineerRole).Should().BeTrue();
        CivilEngineeringAccessControlRegistry.IsExecutionProjectRole(
            CivilEngineeringAccessControlRegistry.CivilEngineerRole).Should().BeTrue();
        CivilEngineeringAccessControlRegistry.IsExecutionProjectRole(
            CivilEngineeringAccessControlRegistry.ArtisanRole).Should().BeTrue();

        foreach (var role in CivilEngineeringAccessControlRegistry.Roles)
        {
            role.Permissions.Should().NotContain("finance.configuration.manage");
            role.Permissions.Should().NotContain("projects.financials.manage");
        }
    }

    [Theory]
    [InlineData("ManageDesign", "CIVIL_PROJECT_SCOPE_DENIED")]
    [InlineData("ManageMaintenance", "CIVIL_ASSET_BUILDING_SCOPE_DENIED")]
    [InlineData("ManageSupervision", "CIVIL_SECTION_SCOPE_DENIED")]
    [InlineData("RespondDesignInput", "CIVIL_SECTION_SCOPE_DENIED")]
    [InlineData("ManageAssignedWork", "CIVIL_ASSIGNMENT_SCOPE_DENIED")]
    [InlineData("ApproveTransaction", "CIVIL_CONFIGURED_AUTHORITY_DENIED")]
    [InlineData("ReadReports", "CIVIL_PROJECT_SCOPE_DENIED")]
    [InlineData("ExportReports", "CIVIL_PROJECT_SCOPE_DENIED")]
    [InlineData("ManageMigration", "CIVIL_PROJECT_SCOPE_DENIED")]
    public void Scoped_operations_reject_their_missing_record_or_authority_scope(
        string operationName,
        string expectedCode)
    {
        var operation = CivilEngineeringAccessControlRegistry.GetOperation(operationName);
        var facts = FullScope with
        {
            HasProjectScope = expectedCode != "CIVIL_PROJECT_SCOPE_DENIED",
            HasAssetOrBuildingScope = expectedCode != "CIVIL_ASSET_BUILDING_SCOPE_DENIED",
            HasSectionScope = expectedCode != "CIVIL_SECTION_SCOPE_DENIED",
            IsAssigned = expectedCode != "CIVIL_ASSIGNMENT_SCOPE_DENIED",
            HasConfiguredAuthority = expectedCode != "CIVIL_CONFIGURED_AUTHORITY_DENIED"
        };

        var decision = CivilEngineeringAccessControlRegistry.Evaluate(operation, facts);

        decision.IsAllowed.Should().BeFalse();
        decision.Code.Should().Be(expectedCode);
    }

    [Fact]
    public void Approval_requires_permission_configured_authority_and_an_independent_checker()
    {
        var operation = CivilEngineeringAccessControlRegistry.GetOperation("ApproveTransaction");

        CivilEngineeringAccessControlRegistry.Evaluate(
                operation,
                FullScope with { HasPermission = false })
            .Code.Should().Be("CIVIL_PERMISSION_DENIED");
        CivilEngineeringAccessControlRegistry.Evaluate(
                operation,
                FullScope with { HasConfiguredAuthority = false })
            .Code.Should().Be("CIVIL_CONFIGURED_AUTHORITY_DENIED");
        CivilEngineeringAccessControlRegistry.Evaluate(
                operation,
                FullScope with { IsIndependentChecker = false })
            .Code.Should().Be("CIVIL_MAKER_CHECKER_CONFLICT");
        CivilEngineeringAccessControlRegistry.Evaluate(operation, FullScope)
            .Should().Be(CivilEngineeringAccessDecision.Allowed);
    }

    [Fact]
    public void Cross_section_response_requires_section_scope_but_not_project_membership()
    {
        var operation = CivilEngineeringAccessControlRegistry.GetOperation("RespondDesignInput");

        operation.Permission.Should().Be(CivilEngineeringAccessControlRegistry.DesignInputRespond);
        operation.RequiresSectionScope.Should().BeTrue();
        operation.RequiresProjectScope.Should().BeFalse();
        CivilEngineeringAccessControlRegistry.Evaluate(
                operation,
                FullScope with { HasProjectScope = false })
            .Should().Be(CivilEngineeringAccessDecision.Allowed);
    }

    [Fact]
    public void Tenant_scope_is_checked_before_permission_or_record_scope()
    {
        var decision = CivilEngineeringAccessControlRegistry.Evaluate(
            CivilEngineeringAccessControlRegistry.GetOperation("ManageMaintenance"),
            FullScope with
            {
                IsTenantMatch = false,
                HasPermission = false,
                HasProjectScope = false,
                HasAssetOrBuildingScope = false
            });

        decision.Code.Should().Be("CIVIL_TENANT_SCOPE_DENIED");
    }

    [Fact]
    public void Project_catalogues_expose_controlled_civil_member_and_assignment_roles()
    {
        var catalogues = ProjectCatalogDefaults.GetRecommendedCatalogs();
        var memberCodes = catalogues.Single(value => value.Key == "member-roles")
            .Items.Select(value => value.Code);
        var resourceCodes = catalogues.Single(value => value.Key == "resource-roles")
            .Items.Select(value => value.Code);

        memberCodes.Should().Contain(CivilEngineeringAccessControlRegistry.Roles.Select(value => value.Code));
        resourceCodes.Should().Contain([
            CivilEngineeringAccessControlRegistry.CivilEngineerRole,
            CivilEngineeringAccessControlRegistry.ProjectEngineerRole,
            CivilEngineeringAccessControlRegistry.DraftsmanRole,
            CivilEngineeringAccessControlRegistry.TechnicianRole,
            CivilEngineeringAccessControlRegistry.ArtisanRole]);
    }

    [Fact]
    public async Task Seeder_is_idempotent_and_uses_the_shared_security_tables()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        await using var context = new ApplicationDbContext(options);
        context.Tenants.Add(new Tenant
        {
            Id = Guid.NewGuid(),
            Code = "TDC",
            Name = "TDC",
            Status = TenantStatus.Active,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "Tests"
        });
        await context.SaveChangesAsync();
        var seeder = new CivilEngineeringAccessControlSeeder(
            context,
            NullLogger<CivilEngineeringAccessControlSeeder>.Instance);

        await seeder.SeedAsync();
        await seeder.SeedAsync();

        (await context.Permissions.CountAsync(value =>
                value.Category == CivilEngineeringAccessControlRegistry.Category))
            .Should().Be(CivilEngineeringAccessControlRegistry.Permissions.Count);
        (await context.Roles.CountAsync(value =>
                CivilEngineeringAccessControlRegistry.Roles
                    .Select(definition => definition.Code)
                    .Contains(value.Name!)))
            .Should().Be(CivilEngineeringAccessControlRegistry.Roles.Count);
        (await context.WorkflowEntityTypes.CountAsync(value =>
                CivilEngineeringWorkflowBindingRegistry.EntityTypes
                    .Select(definition => definition.Code)
                    .Contains(value.Code)))
            .Should().Be(CivilEngineeringWorkflowBindingRegistry.EntityTypes.Count);

        foreach (var definition in CivilEngineeringAccessControlRegistry.Roles)
        {
            var role = await context.Roles.SingleAsync(value => value.Name == definition.Code);
            var granted = await context.RolePermissions
                .Where(value => value.RoleId == role.Id)
                .Join(
                    context.Permissions,
                    value => value.PermissionId,
                    value => value.Id,
                    (_, permission) => permission.Name)
                .ToListAsync();
            granted.Should().BeEquivalentTo(
                definition.Permissions.Append(CivilEngineeringAccessControlRegistry.CentralProjectAccess));
        }
    }

    [Fact]
    public async Task Civil_report_catalogue_is_project_scoped_permission_aligned_and_seeded_idempotently()
    {
        CivilEngineeringStatutoryReportCatalogue.Definitions.Should().HaveCount(14);
        CivilEngineeringStatutoryReportCatalogue.Definitions.Select(value => value.Code).Should().OnlyHaveUniqueItems();
        CivilEngineeringStatutoryReportCatalogue.Definitions.Should().OnlyContain(value =>
            value.Query.StartsWith(CivilEngineeringStatutoryReportCatalogue.QueryPrefix, StringComparison.Ordinal) &&
            value.Columns.Count > 0 && value.Tags.Contains("civil-engineering"));
        CivilEngineeringStatutoryReportCatalogue.BuildParameters().Should().ContainKeys("projectId", "startDate", "endDate");
        CivilEngineeringStatutoryReportCatalogue.Resolve(
                CivilEngineeringStatutoryReportCatalogue.QueryPrefix + CivilEngineeringStatutoryReportCatalogue.CompletionHandoverCode)
            .Should().NotBeNull();
        CivilEngineeringStatutoryReportCatalogue.Definitions
            .Where(value => value.Tags.Contains("architecture-16.5"))
            .Select(value => value.Name)
            .Should().BeEquivalentTo(
                "Engineering Work Register",
                "Inspection Report",
                "Site Instruction Log",
                "Progress Report",
                "Defect Report",
                "Completion Certificate Report",
                "Project Dashboard",
                "Engineering Audit Trail");
        CivilEngineeringStatutoryReportCatalogue.Resolve(
                CivilEngineeringStatutoryReportCatalogue.QueryPrefix + CivilEngineeringStatutoryReportCatalogue.SiteInstructionLogCode)!
            .EffectiveSourceCode.Should().Be(CivilEngineeringStatutoryReportCatalogue.SupervisionControlCode);
        CivilEngineeringStatutoryReportCatalogue.Resolve(
                CivilEngineeringStatutoryReportCatalogue.QueryPrefix + CivilEngineeringStatutoryReportCatalogue.DefectReportCode)!
            .EffectiveSourceCode.Should().Be(CivilEngineeringStatutoryReportCatalogue.CompletionHandoverCode);
        CivilEngineeringAccessControlRegistry.GetOperation("ReadReports").Permission
            .Should().Be(CivilEngineeringStatutoryReportCatalogue.ReadPermission);
        CivilEngineeringAccessControlRegistry.GetOperation("ExportReports").Permission
            .Should().Be(CivilEngineeringStatutoryReportCatalogue.ExportPermission);

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        await using var context = new ApplicationDbContext(options);
        var tenantId = Guid.NewGuid();
        context.Tenants.Add(new Tenant { Id = tenantId, Code = "TDC", Name = "TDC" });
        context.TenantModules.Add(new TenantModule
        {
            TenantId = tenantId,
            ModuleName = "Project Management",
            Status = ModuleStatus.Enabled
        });
        await context.SaveChangesAsync();
        var seeder = new CivilEngineeringStatutoryReportSeeder(
            context, NullLogger<CivilEngineeringStatutoryReportSeeder>.Instance);

        await seeder.SeedTenantAsync(tenantId);
        await seeder.SeedTenantAsync(tenantId);

        var reports = await context.Reports.IgnoreQueryFilters().Where(value => value.TenantId == tenantId).ToListAsync();
        reports.Should().HaveCount(CivilEngineeringStatutoryReportCatalogue.Definitions.Count);
        reports.Should().OnlyContain(value => value.Type == CivilEngineeringStatutoryReportCatalogue.ReportType &&
            value.Status == "published" && value.ModuleId != null && !value.IsDeleted);
    }

    [Fact]
    public async Task Civil_report_catalogue_is_withheld_when_the_project_module_is_not_enabled()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        await using var context = new ApplicationDbContext(options);
        var tenantId = Guid.NewGuid();
        context.Tenants.Add(new Tenant { Id = tenantId, Code = "NO-PROJECTS", Name = "No Projects" });
        context.Reports.Add(new Report
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Orphaned Civil report",
            Type = CivilEngineeringStatutoryReportCatalogue.ReportType,
            Status = "published",
            Query = CivilEngineeringStatutoryReportCatalogue.Definitions[0].Query,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "Tests"
        });
        await context.SaveChangesAsync();
        var seeder = new CivilEngineeringStatutoryReportSeeder(
            context, NullLogger<CivilEngineeringStatutoryReportSeeder>.Instance);

        await seeder.SeedTenantAsync(tenantId);

        (await context.Reports.Where(value => value.TenantId == tenantId).ToListAsync()).Should().BeEmpty();
        (await context.Reports.IgnoreQueryFilters().SingleAsync(value => value.TenantId == tenantId))
            .IsDeleted.Should().BeTrue();
    }
}
