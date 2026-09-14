using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

public partial class INVREQFU003ReturnReversalAllocationGate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(TriggerSql(openAllocationsOnly: true));

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(TriggerSql(openAllocationsOnly: false));

    private static string TriggerSql(bool openAllocationsOnly)
    {
        var allocationQuantityGate = openAllocationsOnly
            ? "(i.ReversalPostingEventId IS NULL AND i.Quantity > lineage.ReturnedQuantity)"
            : "i.Quantity > lineage.ReturnedQuantity";
        var allocationSumGate = openAllocationsOnly
            ? "AND a.IsDeleted = 0 AND a.ReversalPostingEventId IS NULL"
            : "AND a.IsDeleted = 0";

        return $$"""
            CREATE OR ALTER TRIGGER [dbo].[TR_InventoryIssueReturnAllocations_Immutable]
            ON [dbo].[InventoryIssueReturnAllocations]
            AFTER INSERT, UPDATE, DELETE
            AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id = d.Id WHERE i.Id IS NULL)
                    THROW 51846, 'INV_RETURN_ALLOCATION_DELETE_BLOCKED: return-to-issue allocation is immutable.', 1;

                IF EXISTS (
                    SELECT 1 FROM inserted i INNER JOIN deleted d ON d.Id = i.Id
                    WHERE i.TenantId <> d.TenantId OR i.InventoryReturnVoucherLineId <> d.InventoryReturnVoucherLineId
                       OR i.InventoryIssueFinanceLineageId <> d.InventoryIssueFinanceLineageId
                       OR i.Quantity <> d.Quantity OR i.Value <> d.Value OR i.ReturnPostingEventId <> d.ReturnPostingEventId
                       OR i.ReturnJournalEntryId <> d.ReturnJournalEntryId OR i.PostedAtUtc <> d.PostedAtUtc
                       OR i.IntegrityHash <> d.IntegrityHash OR i.CreatedAt <> d.CreatedAt OR i.IsDeleted <> d.IsDeleted
                       OR d.ReversalPostingEventId IS NOT NULL OR d.ReversalJournalEntryId IS NOT NULL OR d.ReversedAtUtc IS NOT NULL
                       OR i.ReversalPostingEventId IS NULL OR i.ReversalJournalEntryId IS NULL OR i.ReversedAtUtc IS NULL)
                    THROW 51847, 'INV_RETURN_ALLOCATION_IMMUTABLE: only one complete reversal binding is permitted.', 1;

                IF EXISTS (
                    SELECT 1 FROM inserted i
                    LEFT JOIN InventoryReturnVoucherLines line ON line.Id = i.InventoryReturnVoucherLineId AND line.TenantId = i.TenantId AND line.IsDeleted = 0
                    LEFT JOIN InventoryReturnVouchers v ON v.Id = line.InventoryReturnVoucherId AND v.TenantId = i.TenantId AND v.IsDeleted = 0
                    LEFT JOIN InventoryIssueFinanceLineages lineage ON lineage.Id = i.InventoryIssueFinanceLineageId AND lineage.TenantId = i.TenantId AND lineage.IsDeleted = 0
                    LEFT JOIN FinancePostingEvents pe ON pe.Id = i.ReturnPostingEventId AND pe.TenantId = i.TenantId AND pe.IsDeleted = 0
                    LEFT JOIN JournalEntries j ON j.Id = i.ReturnJournalEntryId AND j.TenantId = i.TenantId AND j.IsDeleted = 0
                    LEFT JOIN FinancePostingEvents rpe ON rpe.Id = i.ReversalPostingEventId AND rpe.TenantId = i.TenantId AND rpe.IsDeleted = 0
                    LEFT JOIN JournalEntries rj ON rj.Id = i.ReversalJournalEntryId AND rj.TenantId = i.TenantId AND rj.IsDeleted = 0
                    WHERE line.Id IS NULL OR v.Id IS NULL OR lineage.Id IS NULL OR v.Status NOT IN (4,5)
                       OR i.Quantity > lineage.IssuedQuantity OR {{allocationQuantityGate}}
                       OR (SELECT COALESCE(SUM(a.Quantity),0) FROM InventoryIssueReturnAllocations a
                           WHERE a.InventoryIssueFinanceLineageId = lineage.Id AND a.TenantId = i.TenantId {{allocationSumGate}}) > lineage.ReturnedQuantity
                       OR i.Value <> ROUND((lineage.IssuedValue / lineage.IssuedQuantity) * i.Quantity, 2)
                       OR pe.Id IS NULL OR pe.SourceDocumentType <> N'InventoryReturnVoucher' OR pe.SourceDocumentId <> v.Id
                       OR pe.JournalEntryId <> i.ReturnJournalEntryId OR pe.PostingStatus <> N'Posted'
                       OR j.Id IS NULL OR j.SourceDocumentType <> N'InventoryReturnVoucher' OR j.SourceDocumentId <> v.Id
                       OR j.PostingStatus <> N'Posted' OR j.IsBalanced = 0 OR j.TotalDebitAmount <> j.TotalCreditAmount OR j.BalanceDifference <> 0
                       OR (i.ReversalPostingEventId IS NOT NULL AND (rpe.Id IS NULL OR rpe.SourceDocumentType <> N'InventoryReturnVoucher'
                           OR rpe.SourceDocumentId <> v.Id OR rpe.JournalEntryId <> i.ReversalJournalEntryId OR rpe.PostingStatus <> N'Posted'
                           OR rj.Id IS NULL OR rj.OriginalJournalEntryId <> i.ReturnJournalEntryId OR rj.PostingStatus <> N'Posted'
                           OR rj.IsBalanced = 0 OR rj.TotalDebitAmount <> rj.TotalCreditAmount OR rj.BalanceDifference <> 0)))
                    THROW 51848, 'INV_RETURN_ALLOCATION_INVALID: allocation must reference its posted balanced return or reversal lineage.', 1;
            END
            """;
    }
}
