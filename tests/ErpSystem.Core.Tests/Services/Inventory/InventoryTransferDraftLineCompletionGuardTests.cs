using System.Reflection;
using System.Text.RegularExpressions;
using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Inventory;

public sealed class InventoryTransferDraftLineCompletionGuardTests
{
    [Fact]
    public void Removed_draft_lines_are_excluded_from_receipt_and_automatic_completion_without_changing_other_guards()
    {
        var definitions = Build(new TDC0608ControlledInventoryTransfers()).Operations.OfType<SqlOperation>()
            .ToDictionary(x => Regex.Match(x.Sql, @"CREATE OR ALTER TRIGGER \[dbo\]\.\[([^\]]+)\]").Groups[1].Value, x => x.Sql);
        Apply(definitions, new InventoryTransferOptionalApproval());
        Apply(definitions, new InventoryTransferAutomaticCompletion());
        var before = definitions["TR_InventoryTransfers_ControlledLifecycle"];
        var otherGuards = definitions.Where(x => x.Key != "TR_InventoryTransfers_ControlledLifecycle").ToDictionary();

        Apply(definitions, new InventoryTransferDraftLineCompletionGuard());

        var result = definitions["TR_InventoryTransfers_ControlledLifecycle"];
        result.Should().Be(before.Replace(
                "AND (line.IsDeleted=1 OR line.RequestedQuantity<=0",
                "AND line.IsDeleted=0 AND (line.RequestedQuantity<=0", StringComparison.Ordinal)
            .Replace("line.TenantId = i.TenantId AND (line.ShippedQuantity <> line.RequestedQuantity",
                "line.TenantId = i.TenantId AND line.IsDeleted = 0 AND (line.ShippedQuantity <> line.RequestedQuantity", StringComparison.Ordinal));
        result.Should().NotContain("line.IsDeleted=1 OR line.RequestedQuantity<=0");
        result.Should().Contain("line.TenantId=i.TenantId AND line.IsDeleted=0)");
        result.Should().Contain("line.ReceivedQuantity<>line.ShippedQuantity");
        result.Should().Contain("line.DamagedQuantity<>0 OR line.ShortageQuantity<>0");
        result.Should().Contain("receipt.ActorUserId=completed.ActorUserId");
        result.Should().Contain("completed.Sequence=receipt.Sequence+1");
        result.Should().Contain("$.Metadata.AutomaticCompletion");
        result.Should().Contain("i.ApprovalRequired=1 AND i.ReceivedById IN (i.RequestedById, i.ApprovedById, i.ShippedById)");
        result.Should().Contain("INV_TRANSFER_SOURCE_IMMUTABLE").And.Contain("INV_TRANSFER_STATE_INVALID");
        foreach (var guard in otherGuards) definitions[guard.Key].Should().Be(guard.Value);
    }

    [Fact]
    public void Migration_is_additive_guard_only_and_refuses_unexpected_definition_or_destructive_rollback()
    {
        var builder = Build(new InventoryTransferDraftLineCompletionGuard());
        builder.Operations.Should().HaveCount(2).And.AllBeOfType<SqlOperation>();
        var operations = builder.Operations.Cast<SqlOperation>().ToArray();
        operations[0].Sql.Should().Contain("DECLARE @expectedMatches int=2;");
        operations[1].Sql.Should().Contain("DECLARE @expectedMatches int=1;");
        foreach (var operation in operations)
        {
            operation.Sql.Should().Contain("INV_TRANSFER_DRAFT_LINE_GUARD_DRIFT");
            operation.Sql.Should().Contain("dbo.TR_InventoryTransfers_ControlledLifecycle");
            operation.Sql.Should().NotContain("UPDATE InventoryTransfer").And.NotContain("DELETE FROM");
        }
        var down = Build(new InventoryTransferDraftLineCompletionGuard(), "Down");
        down.Operations.Should().ContainSingle().Which.Should().BeOfType<SqlOperation>()
            .Which.Sql.Should().Contain("THROW 51999").And.Contain("Restore a verified backup");
    }

    private static MigrationBuilder Build(Migration migration, string method = "Up")
    {
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        migration.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(migration, [builder]);
        return builder;
    }

    private static void Apply(Dictionary<string, string> definitions, Migration migration)
    {
        foreach (var operation in Build(migration).Operations.OfType<SqlOperation>())
        {
            var trigger = Regex.Match(operation.Sql, @"OBJECT_ID\(N'dbo\.([^']+)',N'TR'\)").Groups[1].Value;
            var before = Regex.Match(operation.Sql, "DECLARE @before nvarchar\\(max\\)=N'((?:''|[^'])*)';", RegexOptions.Singleline).Groups[1].Value.Replace("''", "'");
            var after = Regex.Match(operation.Sql, "SET @definition=REPLACE\\(@definition,@before,N'((?:''|[^'])*)'\\);", RegexOptions.Singleline).Groups[1].Value.Replace("''", "'");
            var expected = Regex.Match(operation.Sql, @"DECLARE @expectedMatches int=(\d+);");
            var expectedMatches = expected.Success ? int.Parse(expected.Groups[1].Value) : 1;
            before.Should().NotBeEmpty();
            definitions[trigger].Split(before, StringSplitOptions.None).Should().HaveCount(expectedMatches + 1);
            definitions[trigger] = definitions[trigger].Replace(before, after, StringComparison.Ordinal);
        }
    }
}
