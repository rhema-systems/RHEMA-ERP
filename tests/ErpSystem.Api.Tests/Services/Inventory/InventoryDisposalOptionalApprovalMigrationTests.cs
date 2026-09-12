using System.Text.RegularExpressions;
using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Inventory;

public sealed class InventoryDisposalOptionalApprovalMigrationTests
{
    private static string PatchSql => new InventoryDisposalOptionalApprovalAndDrafts().UpOperations
        .OfType<SqlOperation>().Select(value => value.Sql.Replace("\r", string.Empty))
        .Single(value => value.Contains("DECLARE @definition", StringComparison.Ordinal));

    private static string Literal(string sql, string variable)
    {
        var match = Regex.Match(sql, $@"DECLARE @{variable} nvarchar\(max\)=N'((?:''|[^'])*)';", RegexOptions.Singleline);
        match.Success.Should().BeTrue();
        return match.Groups[1].Value.Replace("''", "'");
    }

    [Fact]
    public void ExistingDisposalsRemainApprovalRequired_AndNoOperationalRowsAreRewritten()
    {
        var operations = new InventoryDisposalOptionalApprovalAndDrafts().UpOperations;
        var added = operations.OfType<AddColumnOperation>().Should().ContainSingle().Which;
        added.Table.Should().Be("InventoryDisposalCases");
        added.Name.Should().Be("ApprovalRequired");
        added.ColumnType.Should().Be("bit");
        added.IsNullable.Should().BeFalse();
        added.DefaultValue.Should().Be(true);
        operations.OfType<AddCheckConstraintOperation>().Should().ContainSingle()
            .Which.Sql.Should().Be("[Status] BETWEEN 1 AND 11");
        operations.Should().NotContain(value => value.GetType() == typeof(UpdateDataOperation) ||
            value.GetType() == typeof(DeleteDataOperation) || value.GetType() == typeof(InsertDataOperation));
    }

    [Fact]
    public void DisposalDecision_RequiresCentralPolicyOnlyOnInitialNoApprovalTransition()
    {
        var guard = InventoryDisposalOptionalApprovalAndDrafts.CaseGuard;
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
        var prior = new InventoryOptionalApprovalSnapshots().UpOperations.OfType<SqlOperation>()
            .Select(value => value.Sql.Replace("\r", string.Empty))
            .Single(value => value.Contains("-- Approval requirement may be decided only once", StringComparison.Ordinal));
        var priorReplacement = Regex.Match(prior,
            @"SET @definition=REPLACE\(@definition,N'((?:''|[^'])*)',N'((?:''|[^'])*)'\);", RegexOptions.Singleline);
        priorReplacement.Success.Should().BeTrue();
        var priorPredicate = priorReplacement.Groups[2].Value.Replace("''", "'");
        var before = Literal(PatchSql, "before");
        var after = Literal(PatchSql, "after");

        priorPredicate.Split(before, StringSplitOptions.None).Should().HaveCount(2);
        after.Should().Be(InventoryDisposalOptionalApprovalAndDrafts.AdjustmentApprovalDelegation.Replace("\r", string.Empty));
        var changed = priorPredicate.Replace(before, after, StringComparison.Ordinal);
        changed.Should().Contain("d.Status=N'Draft' AND i.Status=N'ReadyToPost'")
            .And.Contain("d.ApprovedAt IS NULL")
            .And.Contain("i.SubmittedById IS NOT NULL AND i.SubmittedAtUtc IS NOT NULL")
            .And.Contain("INV_APPROVAL_SNAPSHOT_INVALID");
        PatchSql.Should().Contain("TR_StockAdjustments_ControlledLifecycle")
            .And.Contain("/NULLIF(LEN(@before),0)<>1")
            .And.Contain("THROW 51998");
    }

    [Fact]
    public void AdjustmentPatch_NormalizesDefinitionAndWindowsSqlLiterals()
    {
        PatchSql.Should().Contain("SET @definition=REPLACE(@definition,CHAR(13),N'')")
            .And.Contain("SET @before=REPLACE(@before,CHAR(13),N'')")
            .And.Contain("SET @after=REPLACE(@after,CHAR(13),N'')");
        Literal(PatchSql.Replace("\n", "\r\n"), "after").Replace("\r", string.Empty)
            .Should().Be(Literal(PatchSql, "after"));
    }

    [Fact]
    public void Delegation_IsAnAlternativeToOrdinaryPolicy_NotAGlobalOverride()
    {
        var sql = InventoryDisposalOptionalApprovalAndDrafts.AdjustmentApprovalDelegation;
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
        var sql = InventoryDisposalOptionalApprovalAndDrafts.AdjustmentApprovalDelegation;
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
        var sql = InventoryDisposalOptionalApprovalAndDrafts.AdjustmentApprovalDelegation;
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
        var sql = InventoryDisposalOptionalApprovalAndDrafts.LineGuard;
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
        InventoryDisposalOptionalApprovalAndDrafts.CaseGuard.Should()
            .Contain("StockAdjustmentId IS NULL OR ExecutionReference IS NULL")
            .And.Contain("a.Status<>'Posted'")
            .And.Contain("i.ProceedsPostingEventId IS NULL OR i.ProceedsJournalEntryId IS NULL")
            .And.Contain("i.Method NOT IN (1,2) AND i.ProceedsAmount<>0");
        var down = new InventoryDisposalOptionalApprovalAndDrafts().DownOperations;
        down.Should().ContainSingle().Which.Should().BeOfType<SqlOperation>()
            .Which.Sql.Should().Contain("THROW 51997").And.Contain("reviewed forward migration");
    }
}
