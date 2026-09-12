using System.Text.RegularExpressions;
using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Inventory;

// These emitted-operation checks supplement, rather than replace, the rollback-only
// SQL Server harness Test-LocalStockAdjustmentSequentialFifo.ps1.
public sealed class StockAdjustmentSequentialFifoMigrationTests
{
    private static IReadOnlyList<SqlOperation> Operations => new StockAdjustmentSequentialFifoValuation()
        .UpOperations.Cast<SqlOperation>().ToList();

    [Fact]
    public void Migration_adds_no_columns_or_business_data_rewrites()
    {
        new StockAdjustmentSequentialFifoValuation().UpOperations.Should().OnlyContain(value => value.GetType() == typeof(SqlOperation));
        Operations.Should().HaveCount(5);
        Operations.Select(value => value.Sql).Should().NotContain(value =>
            Regex.IsMatch(value, @"(?im)^\s*(UPDATE|DELETE\s+FROM|INSERT\s+(?:INTO\s+)?)(?:dbo\.)?(?:StockAdjustment|Inventory)"));
    }

    [Fact]
    public void Existing_line_predicates_are_replaced_once_and_windows_literals_are_normalized()
    {
        var patches = Operations.Where(value => value.Sql.Contains("DECLARE @definition", StringComparison.Ordinal)).ToList();
        patches.Should().HaveCount(3);
        foreach (var patch in patches)
        {
            patch.Sql.Should().Contain("/NULLIF(LEN(@before),0)<>1")
                .And.Contain("SET @definition=REPLACE(@definition,CHAR(13),N'')")
                .And.Contain("SET @before=REPLACE(@before,CHAR(13),N'')")
                .And.Contain("SET @after=REPLACE(@after,CHAR(13),N'')")
                .And.Contain("THROW 51977");
        }
        patches[1].Sql.Should().Contain("InventoryAdjustmentExpectedUnitCost")
            .And.Contain("InventoryAdjustmentExpectedLineValue")
            .And.Contain("item.ValuationMethod=2 AND i.AdjustmentQuantity<0");
        patches[2].Sql.Should().Contain("a.ReasonCode<>N''INITIAL_STOCK''")
            .And.Contain("ELSE ROUND(i.AdjustmentQuantity * i.UnitCost, 2)");
    }

    [Fact]
    public void Soft_retirement_preserves_all_source_cost_tracking_and_creation_evidence()
    {
        var sql = StockAdjustmentSequentialFifoValuation.DraftRetirementGuard;
        sql.Should().Contain("d.IsDeleted=1 OR (i.IsDeleted=1 AND (d.Id IS NULL OR EXISTS (")
            .And.Contain("EXCEPT").And.Contain("THROW 51980");
        foreach (var column in new[] { "TenantId", "AdjustmentId", "InventoryItemId", "LocationId", "CreatedAt",
                     "CreatedBy", "CreatedById", "SystemQuantity", "PhysicalQuantity", "AdjustmentQuantity",
                     "UnitCost", "AdjustmentValue", "SerialNumber", "LotNumber", "BatchNumber", "ManufactureDate",
                     "ExpiryDate", "Notes", "Reason" })
            sql.Should().Contain($"i.{column}").And.Contain($"d.{column}");
        Operations.Single(value => value.Sql.Contains("DraftRetirement", StringComparison.OrdinalIgnoreCase) ||
            value.Sql.Contains("INV_ADJUSTMENT_DRAFT_RETIREMENT_INVALID", StringComparison.Ordinal))
            .Sql.Should().NotContain("DISABLE TRIGGER");
    }

    [Fact]
    public void Replay_is_scoped_to_the_same_parent_item_and_exact_bin_not_tracking_identity()
    {
        var sql = StockAdjustmentSequentialFifoValuation.ExpectedValueFunction;
        sql.Should().Contain("TenantId=@tenant AND AdjustmentId=@adjustment")
            .And.Contain("InventoryItemId=@item AND LocationId=@location AND IsDeleted=0")
            .And.Contain("ROW_NUMBER() OVER(ORDER BY CreatedAt,Id)")
            .And.Contain("CreatedAt<@created OR (CreatedAt=@created AND Id<=@line)")
            .And.Contain("WarehouseId=@warehouse")
            .And.Contain("IsActive=1 AND IsFullyConsumed=0 AND RemainingQuantity>0")
            .And.NotContain("LotNumber").And.NotContain("SerialNumber").And.NotContain("BatchNumber");
    }

    [Fact]
    public void Consumption_retains_final_value_and_new_layers_use_execution_time()
    {
        var sql = StockAdjustmentSequentialFifoValuation.ExpectedValueFunction;
        sql.Should().Contain("@execution datetime2(7)=SYSUTCDATETIME()")
            .And.Contain("WHEN @taken=@layerQuantity THEN @layerValue")
            .And.Contain("ROUND(@taken*@layerCost,2)")
            .And.Contain("ROUND(@quantity*@cost,4)")
            .And.Contain("IF @remaining>0 AND @fallback<=0 RETURN NULL")
            .And.Contain("IF @current=@line RETURN -ROUND(@amount,2)");
        sql.Should().NotContain("INSERT @layers VALUES(@created");
    }

    [Fact]
    public void Submission_rechecks_only_editable_drafts_and_does_not_reprice_history()
    {
        var sql = StockAdjustmentSequentialFifoValuation.SubmissionGuard;
        sql.Should().Contain("d.Status=N'Draft' AND i.Status IN(N'PendingApproval',N'ReadyToPost')")
            .And.Contain("i.ReasonCode<>N'INITIAL_STOCK' AND p.ValuationMethod=2 AND l.AdjustmentQuantity<0")
            .And.Contain("THROW 51978");
        sql.Should().NotContain("i.Status=N'Posted'").And.NotContain("DISABLE");
        new StockAdjustmentSequentialFifoValuation().DownOperations.Cast<SqlOperation>().Single().Sql
            .Should().Contain("THROW 51979").And.Contain("historical costs must not be reinterpreted");
    }
}
