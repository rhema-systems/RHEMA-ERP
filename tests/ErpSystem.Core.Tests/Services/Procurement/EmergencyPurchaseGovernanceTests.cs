using System.Reflection;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Data;
using ErpSystem.Data.Migrations;
using ErpSystem.Data.Repositories.Procurement;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class EmergencyPurchaseGovernanceTests
{
    [Theory]
    [InlineData("Draft", "Prepared")]
    [InlineData("Prepared", "PendingAudit")]
    [InlineData("PendingAudit", "AuditVouched")]
    [InlineData("AuditVouched", "PendingApproval")]
    [InlineData("PendingApproval", "Approved")]
    [InlineData("PendingApproval", "Rejected")]
    [InlineData("Approved", "Triggered")]
    [InlineData("Triggered", "Filed")]
    public void Lifecycle_allows_only_the_governed_forward_edges(string from, string to)
    {
        EmergencyPurchaseGovernanceRules.IsAllowedTransition(from, to).Should().BeTrue();
        EmergencyPurchaseGovernanceRules.IsAllowedTransition(to, from).Should().BeFalse();
    }

    [Fact]
    public void Completed_workflow_cannot_be_recorded_as_a_rejection()
    {
        EmergencyPurchaseGovernanceRules.IsRejectedWorkflowOutcome(WorkflowInstanceStatus.Completed).Should().BeFalse();
        EmergencyPurchaseGovernanceRules.IsRejectedWorkflowOutcome(WorkflowInstanceStatus.Cancelled).Should().BeTrue();
        EmergencyPurchaseGovernanceRules.IsRejectedWorkflowOutcome(WorkflowInstanceStatus.Failed).Should().BeTrue();
    }

    [Fact]
    public void Only_configured_MD_or_Board_authorities_are_supported()
    {
        EmergencyPurchaseGovernanceRules.IsApprovalAuthorityRole("TDC_MANAGING_DIRECTOR").Should().BeTrue();
        EmergencyPurchaseGovernanceRules.IsApprovalAuthorityRole("TDC_BOARD_APPROVER").Should().BeTrue();
        EmergencyPurchaseGovernanceRules.IsApprovalAuthorityRole("TenantAdmin").Should().BeFalse();
    }

    [Fact]
    public void Migration_persists_exact_policy_workflow_DMS_audit_and_post_award_lineage()
    {
        var operations = Operations();
        operations.OfType<AddColumnOperation>().Select(item => item.Name).Should().Contain([
            "PurchaseRequisitionId", "ExceptionRuleId", "WorkflowDefinitionId", "WorkflowInstanceId",
            "CentralDocumentVersionId", "FileUploadRecordId", "InternalAuditVouchedById",
            "ApprovalAuthority", "ApprovalReference", "ExceptionalSourcingTenderId",
            "PostAwardCentralDocumentVersionId", "PostAwardFileUploadRecordId", "IntegrityHash", "RowVersion"
        ]);

        operations.OfType<CreateIndexOperation>().Should().ContainSingle(item =>
            item.Name == "IX_EmergencyProcurementPlans_TenantId_PlanCode" && item.IsUnique);
        operations.OfType<CreateIndexOperation>().Should().ContainSingle(item =>
            item.Name == "IX_EmergencyProcurementPlans_TenantId_PurchaseRequisitionId" && item.IsUnique &&
            item.Filter == "[PurchaseRequisitionId] IS NOT NULL AND [IsDeleted] = 0");

        var principals = operations.OfType<AddForeignKeyOperation>().Select(item => item.PrincipalTable);
        principals.Should().Contain(["PurchaseRequisitions", "ProcurementPolicyExceptionRules", "WorkflowDefinitions",
            "WorkflowInstances", "CentralDocumentVersions", "FileUploadRecords", "Tenders", "Users"]);
    }

    [Fact]
    public void Sql_gate_blocks_delete_reverse_terminal_and_lineage_tampering()
    {
        var sql = Sql();
        sql.Should().Contain("TR_EmergencyProcurementPlans_Governance");
        sql.Should().Contain("THROW 51871");
        sql.Should().Contain("THROW 51872");
        sql.Should().Contain("THROW 51873");
        sql.Should().Contain("THROW 51874");
        sql.Should().Contain("THROW 51875");
        sql.Should().Contain("THROW 51876");
        sql.Should().Contain("THROW 51877");
        sql.Should().Contain("d.Status IN ('Rejected','Filed')");
        sql.Should().NotContain("DISABLE TRIGGER");
    }

    [Fact]
    public void Runtime_model_has_tenant_uniqueness_DMS_and_restrictive_foreign_keys()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options;
        using var context = new ApplicationDbContext(options, Guid.NewGuid());
        var entity = context.GetService<IDesignTimeModel>().Model
            .FindEntityType(typeof(EmergencyProcurementPlan))!;

        entity.GetIndexes().Should().Contain(item => item.IsUnique &&
            item.Properties.Select(property => property.Name).SequenceEqual(new[] { "TenantId", "PlanCode" }));
        entity.GetForeignKeys().Should().Contain(item =>
            item.PrincipalEntityType.ClrType == typeof(CentralDocumentVersion) &&
            item.DeleteBehavior == DeleteBehavior.Restrict);
        entity.GetForeignKeys().Should().Contain(item =>
            item.PrincipalEntityType.ClrType == typeof(PurchaseRequisition) &&
            item.DeleteBehavior == DeleteBehavior.Restrict);
        entity.GetCheckConstraints().Select(item => item.Name).Should().Contain([
            "CK_EmergencyProcurementPlans_GovernedStatus",
            "CK_EmergencyProcurementPlans_PreparedLineage",
            "CK_EmergencyProcurementPlans_AuditLineage",
            "CK_EmergencyProcurementPlans_ApprovalLineage",
            "CK_EmergencyProcurementPlans_FilingLineage"
        ]);
    }

    [Fact]
    public async Task Repository_is_tenant_safe_and_does_not_reuse_a_soft_deleted_plan_code()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        var tenantOne = Guid.NewGuid();
        var tenantTwo = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName)
            .EnableServiceProviderCaching(false)
            .Options;

        await using var context = new ApplicationDbContext(options);
        var prefix = $"EP-{DateTime.UtcNow:yyyyMM}-";
        context.Set<EmergencyProcurementPlan>().AddRange(
            NewPlan(tenantTwo, "EP-OTHER-0001"),
            NewPlan(tenantOne, $"{prefix}0001"),
            NewPlan(tenantOne, $"{prefix}0007", isDeleted: true));
        await context.SaveChangesAsync();

        var currentUser = new Mock<ICurrentUserProvider>();
        currentUser.SetupGet(item => item.TenantId).Returns(tenantOne);
        var repository = new EmergencyProcurementPlanRepository(context, currentUser.Object);

        var page = await repository.GetPlansAsync(1, 20);
        page.Items.Should().ContainSingle(item => item.TenantId == tenantOne && !item.IsDeleted);
        page.Items.Should().NotContain(item => item.TenantId == tenantTwo);
        (await repository.GeneratePlanCodeAsync()).Should().Be($"{prefix}0008");
    }

    private static EmergencyProcurementPlan NewPlan(Guid tenantId, string code, bool isDeleted = false) => new()
    {
        Id = Guid.NewGuid(), TenantId = tenantId, PlanCode = code, Title = code,
        EmergencyType = "SupplyShortage", CriticalityLevel = "High", Currency = "GHS",
        Status = "Draft", IsDeleted = isDeleted, CreatedAt = DateTime.UtcNow
    };

    private static IReadOnlyList<MigrationOperation> Operations()
    {
        var migration = new GovernEmergencyPurchaseExceptions();
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        migration.GetType().GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, [builder]);
        return builder.Operations;
    }

    private static string Sql() => string.Join(Environment.NewLine,
        Operations().OfType<SqlOperation>().Select(item => item.Sql));
}
