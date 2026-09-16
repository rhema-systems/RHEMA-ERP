using System.Text.RegularExpressions;
using ErpSystem.Api.Tests.Services.Finance;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Inventory;

public sealed class InventoryDisposalOptionalApprovalMigrationTests
{
    private const string MigrationFile = "20260912234000_InventoryDisposalOptionalApprovalAndDrafts.cs";
    private static string Source => ArchivedMigrationSource.Read(MigrationFile);
    private static string CaseGuard => ArchivedMigrationSource.RawStringConstant(MigrationFile, "CaseGuard");
    private static string LineGuard => ArchivedMigrationSource.RawStringConstant(MigrationFile, "LineGuard");
    private static string AdjustmentApprovalDelegation =>
        ArchivedMigrationSource.RawStringConstant(MigrationFile, "AdjustmentApprovalDelegation");

    [Fact]
    public void ExistingDisposalsRemainApprovalRequired_AndNoOperationalRowsAreRewritten()
    {
        Source.Should().Contain("migrationBuilder.AddColumn<bool>(\"ApprovalRequired\", \"InventoryDisposalCases\", type: \"bit\", nullable: false, defaultValue: true)")
            .And.Contain("migrationBuilder.AddCheckConstraint(\"CK_InventoryDisposalCases_Status\", \"InventoryDisposalCases\", \"[Status] BETWEEN 1 AND 11\")")
            .And.NotContain("UpdateData(").And.NotContain("DeleteData(").And.NotContain("InsertData(");
    }

    [Fact]
    public void DisposalDecision_RequiresCentralPolicyOnlyOnInitialNoApprovalTransition()
    {
        var guard = CaseGuard;
        guard.Should().Contain("d.Status IN (1,2,3,4) AND i.Status=11")
            .And.Contain("d.ApprovalRequired=1 AND i.ApprovalRequired=0")
            .And.Contain("dbo.WorkflowApprovalRequiredAtSubmission(i.TenantId,N'InventoryDisposal',i.Id)=0")
            .And.Contain("d.WorkflowInstanceId IS NULL AND d.ApprovedById IS NULL");
        guard.Split("dbo.WorkflowApprovalRequiredAtSubmission", StringSplitOptions.None).Should().HaveCount(2,
            "a later configuration change must not rewrite the persisted source decision");
        guard.Should().Contain("i.Status<>1 OR i.ApprovalRequired<>1")
            .And.Contain("ApprovalRequired=0 AND (Status NOT IN (7,8,10,11) OR WorkflowInstanceId IS NOT NULL OR ApprovedById IS NOT NULL OR ApprovedAtUtc IS NOT NULL)");
    }

    [Fact]
    public void AdjustmentPatch_MatchesExactlyOnePriorEmittedPredicate()
    {
        var priorSource = ArchivedMigrationSource.Read("20260912120000_InventoryOptionalApprovalSnapshots.cs");
        var marker = priorSource.IndexOf("-- Approval requirement may be decided only once", StringComparison.Ordinal);
        marker.Should().BeGreaterThan(0);
        var rawStart = priorSource.LastIndexOf("\"\"\"", marker, StringComparison.Ordinal);
        var rawEnd = priorSource.IndexOf("\"\"\"", marker, StringComparison.Ordinal);
        rawStart.Should().BeGreaterThan(0);
        rawEnd.Should().BeGreaterThan(marker);
        var priorPredicate = priorSource[(rawStart + 3)..rawEnd].Replace("\r", string.Empty);
        const string before = "dbo.WorkflowApprovalRequiredAtSubmission(i.TenantId,N'StockAdjustment',i.Id)=0";
        var after = AdjustmentApprovalDelegation.Replace("\r", string.Empty);

        priorPredicate.Split(before, StringSplitOptions.None).Should().HaveCount(2);
        var changed = priorPredicate.Replace(before, after, StringComparison.Ordinal);
        changed.Should().Contain("d.Status=N'Draft' AND i.Status=N'ReadyToPost'")
            .And.Contain("d.ApprovedAt IS NULL")
            .And.Contain("i.SubmittedById IS NOT NULL AND i.SubmittedAtUtc IS NOT NULL")
            .And.Contain("INV_APPROVAL_SNAPSHOT_INVALID");
        Source.Should().Contain("TR_StockAdjustments_ControlledLifecycle")
            .And.Contain("/NULLIF(LEN(@before),0)<>1")
            .And.Contain("THROW 51998");
    }

    [Fact]
    public void AdjustmentPatch_NormalizesDefinitionAndWindowsSqlLiterals()
    {
        Source.Should().Contain("SET @definition=REPLACE(@definition,CHAR(13),N'')")
            .And.Contain("SET @before=REPLACE(@before,CHAR(13),N'')")
            .And.Contain("SET @after=REPLACE(@after,CHAR(13),N'')");
        Source.Should().Contain("static string Literal(string value) => value.Replace(\"'\", \"''\", StringComparison.Ordinal)");
    }

    [Fact]
    public void Delegation_IsAnAlternativeToOrdinaryPolicy_NotAGlobalOverride()
    {
        var sql = AdjustmentApprovalDelegation;
        sql.Should().StartWith("(dbo.WorkflowApprovalRequiredAtSubmission(i.TenantId,N'StockAdjustment',i.Id)=0")
            .And.Contain("OR EXISTS")
            .And.Contain("c.TenantId=i.TenantId AND c.IsDeleted=0 AND c.StockAdjustmentId=i.Id")
            .And.Contain("c.ApprovalRequired=0 AND c.Status IN (7,11)")
            .And.Contain("c.WorkflowInstanceId IS NULL AND c.ApprovedById IS NULL AND c.ApprovedAtUtc IS NULL")
            .And.Contain("c.WarehouseId=i.WarehouseId AND c.DisposalNumber=i.Reference")
            .And.Contain("i.IdempotencyKey=N'disposal:'")
            .And.Contain("i.ReasonCode=CASE WHEN c.Method=4 THEN N'DONATION' ELSE N'WRITE_OFF' END");
    }

    [Theory]
    [InlineData("w.EntityId=i.Id", "STOCKADJUSTMENT")]
    [InlineData("w.EntityId=c.Id", "INVENTORYDISPOSAL")]
    public void RetainedSourceOrGeneratedAdjustmentWorkflowsCannotBeBypassed(string identity, string entity)
    {
        var sql = AdjustmentApprovalDelegation;
        sql.Should().Contain("w.TenantId=i.TenantId AND w.IsDeleted=0 AND w.Status IN (0,1,5,6)")
            .And.Contain(identity)
            .And.Contain($"dbo.WorkflowApprovalEntityKey(e.Code)=N'{entity}'")
            .And.Contain($"dbo.WorkflowApprovalEntityKey(e.Name)=N'{entity}'");
    }

    [Theory]
    [InlineData("InventoryItemId")]
    [InlineData("LocationId")]
    [InlineData("LotNumber")]
    [InlineData("BatchNumber")]
    [InlineData("SerialNumber")]
    public void Delegation_RequiresBidirectionalExactSourceLineage(string field)
    {
        var sql = AdjustmentApprovalDelegation;
        sql.Should().Contain("AND EXISTS (SELECT 1 FROM dbo.InventoryDisposalLines l")
            .And.Contain("AND 1<>(SELECT COUNT(*) FROM dbo.InventoryDisposalLines l")
            .And.Contain("AND 1<>(SELECT COUNT(*) FROM dbo.StockAdjustmentItems a")
            .And.Contain("l.Quantity=-a.AdjustmentQuantity")
            .And.Contain("a.AdjustmentQuantity=-l.Quantity")
            .And.Contain("l.TenantId=c.TenantId AND l.InventoryDisposalCaseId=c.Id AND l.IsDeleted=0")
            .And.Contain("a.TenantId=i.TenantId AND a.AdjustmentId=i.Id AND a.IsDeleted=0");
        if (field is "InventoryItemId" or "LocationId")
            sql.Should().Contain($"l.{field}=a.{field}").And.Contain($"a.{field}=l.{field}");
        else
            sql.Should().Contain($"ISNULL(l.{field},N'')=ISNULL(a.{field},N'')")
                .And.Contain($"ISNULL(a.{field},N'')=ISNULL(l.{field},N'')");
    }

    [Fact]
    public void DraftLineEditing_AllowsOnlyRetirementAndReplacement_NotHistoryRewrites()
    {
        var sql = LineGuard;
        sql.Should().Contain("INV_DISPOSAL_LINE_DELETE_PROHIBITED")
            .And.Contain("c.Status<>1 OR c.IsDeleted=1")
            .And.Contain("i.TenantId<>d.TenantId OR i.InventoryDisposalCaseId<>d.InventoryDisposalCaseId")
            .And.Contain("i.InventoryItemId<>d.InventoryItemId OR i.LocationId<>d.LocationId")
            .And.Contain("i.Quantity<>d.Quantity OR i.UnitCost<>d.UnitCost OR i.TotalValue<>d.TotalValue")
            .And.Contain("d.IsDeleted=1 OR i.IsDeleted<>1");
    }

    [Fact]
    public void PostingAndProceedsGuardsRemain_AndDowngradeDoesNotDestroyHistory()
    {
        CaseGuard.Should()
            .Contain("StockAdjustmentId IS NULL OR ExecutionReference IS NULL")
            .And.Contain("a.Status<>'Posted'")
            .And.Contain("i.ProceedsPostingEventId IS NULL OR i.ProceedsJournalEntryId IS NULL")
            .And.Contain("i.Method NOT IN (1,2) AND i.ProceedsAmount<>0");
        Source.Should().Contain("THROW 51997").And.Contain("reviewed forward migration");
    }
}
