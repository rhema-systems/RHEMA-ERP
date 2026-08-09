using ErpSystem.Core.Services.QuantitySurvey;
using ErpSystem.Core.Entities;
using ErpSystem.Data;
using ErpSystem.Data.Seeders;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.QuantitySurvey;

public sealed class QuantitySurveyAccessControlRegistryTests
{
    [Fact]
    public void Roles_and_operations_reference_only_registered_central_permissions()
    {
        var permissions = QuantitySurveyAccessControlRegistry.Permissions
            .Select(value => value.Code)
            .ToHashSet(StringComparer.Ordinal);

        permissions.Should().HaveCount(QuantitySurveyAccessControlRegistry.Permissions.Count);
        QuantitySurveyAccessControlRegistry.Roles.Select(value => value.Code)
            .Should().OnlyHaveUniqueItems();
        QuantitySurveyAccessControlRegistry.Roles.SelectMany(value => value.Permissions)
            .Should().OnlyContain(value => permissions.Contains(value));
        QuantitySurveyAccessControlRegistry.Operations.Select(value => value.Operation)
            .Should().OnlyHaveUniqueItems();
        QuantitySurveyAccessControlRegistry.Operations.Select(value => value.Permission)
            .Should().OnlyContain(value => permissions.Contains(value));
    }

    [Fact]
    public void Transaction_approval_requires_both_project_membership_and_configured_authority()
    {
        var approval = QuantitySurveyAccessControlRegistry.GetOperation("ApproveTransaction");

        approval.Permission.Should().Be(QuantitySurveyAccessControlRegistry.TransactionsApprove);
        approval.RequiresProjectMembership.Should().BeTrue();
        approval.RequiresConfiguredAuthority.Should().BeTrue();

        var assistant = QuantitySurveyAccessControlRegistry.Roles
            .Single(value => value.Code == "TDC_ASSISTANT_QUANTITY_SURVEYOR");
        assistant.Permissions.Should().NotContain(QuantitySurveyAccessControlRegistry.TransactionsApprove);

        var supervisor = QuantitySurveyAccessControlRegistry.Roles
            .Single(value => value.Code == "TDC_SUPERVISING_QUANTITY_SURVEYOR");
        supervisor.Permissions.Should().Contain(QuantitySurveyAccessControlRegistry.TransactionsApprove);
    }

    [Fact]
    public async Task Seeder_is_idempotent_and_uses_the_shared_security_tables()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        await using var context = new ApplicationDbContext(options);
        var tenantId = Guid.NewGuid();
        context.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Code = "TDC",
            Name = "TDC",
            Status = TenantStatus.Active,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "Tests"
        });
        await context.SaveChangesAsync();
        var seeder = new QuantitySurveyAccessControlSeeder(
            context,
            NullLogger<QuantitySurveyAccessControlSeeder>.Instance);

        await seeder.SeedAsync();
        await seeder.SeedAsync();

        (await context.Permissions.CountAsync(value => value.Category == QuantitySurveyAccessControlRegistry.Category))
            .Should().Be(QuantitySurveyAccessControlRegistry.Permissions.Count);
        (await context.Roles.CountAsync(value => QuantitySurveyAccessControlRegistry.Roles
                .Select(definition => definition.Code)
                .Contains(value.Name!)))
            .Should().Be(QuantitySurveyAccessControlRegistry.Roles.Count);
        (await context.WorkflowEntityTypes.CountAsync(value => value.TenantId == tenantId))
            .Should().Be(QuantitySurveyWorkflowBindingRegistry.EntityTypes.Count);

        foreach (var definition in QuantitySurveyAccessControlRegistry.Roles)
        {
            var role = await context.Roles.SingleAsync(value => value.Name == definition.Code);
            var granted = await context.RolePermissions
                .Where(value => value.RoleId == role.Id)
                .Join(context.Permissions, value => value.PermissionId, value => value.Id, (_, permission) => permission.Name)
                .ToListAsync();
            granted.Should().BeEquivalentTo(definition.Permissions);
        }
    }
}
