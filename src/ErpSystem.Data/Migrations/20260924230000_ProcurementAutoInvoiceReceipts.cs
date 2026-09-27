using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260924230000_ProcurementAutoInvoiceReceipts")]
public sealed class ProcurementAutoInvoiceReceipts : Migration
{
    internal const string CoherentBefore = "([AcceptedSupplyKind] IS NULL AND [AcceptedSupplySourceId] IS NULL AND [AcceptedSupplySourceReference] IS NULL AND [AcceptedSupplySnapshotHash] IS NULL AND [AcceptedSupplyValidatedAtUtc] IS NULL) OR ([AcceptedSupplyKind] BETWEEN 1 AND 3 AND [AcceptedSupplySourceId] IS NOT NULL AND LEN([AcceptedSupplySourceReference]) BETWEEN 1 AND 100 AND LEN([AcceptedSupplySnapshotHash]) = 64 AND [AcceptedSupplyValidatedAtUtc] IS NOT NULL)";
    internal const string PurchaseOrderBefore = "[AcceptedSupplyKind] IS NULL OR [AcceptedSupplyKind] = 3 OR [PurchaseOrderId] IS NOT NULL";
    internal const string PurchaseOrderAfter = "[AcceptedSupplyKind] IS NULL OR [AcceptedSupplyKind] = 3 OR ([AcceptedSupplyKind] = 4 AND [AutoInvoiceRequestId] IS NOT NULL) OR [PurchaseOrderId] IS NOT NULL";
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>("AutoInvoiceRequestId", "VendorInvoice", nullable: true);
        migrationBuilder.AddColumn<string>("AutoInvoiceRequestHash", "VendorInvoice", maxLength: 64, nullable: true);
        migrationBuilder.CreateIndex("IX_VendorInvoice_TenantId_AutoInvoiceRequestId", "VendorInvoice", new[] { "TenantId", "AutoInvoiceRequestId" },
            unique: true, filter: "[AutoInvoiceRequestId] IS NOT NULL");
        migrationBuilder.DropCheckConstraint("CK_VendorInvoice_AcceptedSupplyCoherent", "VendorInvoice");
        migrationBuilder.DropCheckConstraint("CK_VendorInvoice_AcceptedSupplyPurchaseOrder", "VendorInvoice");
        migrationBuilder.AddCheckConstraint("CK_VendorInvoice_AcceptedSupplyCoherent", "VendorInvoice", CoherentBefore.Replace("BETWEEN 1 AND 3", "BETWEEN 1 AND 4"));
        migrationBuilder.AddCheckConstraint("CK_VendorInvoice_AcceptedSupplyPurchaseOrder", "VendorInvoice", PurchaseOrderAfter);
        migrationBuilder.CreateTable("VendorInvoiceReceiptAllocations", columns: table => new
        {
            Id = table.Column<Guid>(nullable: false), TenantId = table.Column<Guid>(nullable: false),
            VendorInvoiceId = table.Column<Guid>(nullable: false), VendorInvoiceLineItemId = table.Column<Guid>(nullable: false),
            PurchaseOrderId = table.Column<Guid>(nullable: false), PurchaseOrderItemId = table.Column<Guid>(nullable: false),
            PurchaseOrderReceiptId = table.Column<Guid>(nullable: false), PurchaseOrderReceiptItemId = table.Column<Guid>(nullable: false),
            InspectionCaseId = table.Column<Guid>(nullable: false), GoodsReceiptNoteId = table.Column<Guid>(nullable: false),
            GoodsReceiptNoteItemId = table.Column<Guid>(nullable: false), Quantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
            CreatedAt = table.Column<DateTime>(nullable: false), UpdatedAt = table.Column<DateTime>(nullable: true),
            CreatedBy = table.Column<string>(nullable: true), UpdatedBy = table.Column<string>(nullable: true),
            CreatedById = table.Column<Guid>(nullable: true), LastModifiedById = table.Column<Guid>(nullable: true),
            IsDeleted = table.Column<bool>(nullable: false), DeletedAt = table.Column<DateTime>(nullable: true), DeletedBy = table.Column<string>(nullable: true)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_VendorInvoiceReceiptAllocations", x => x.Id);
            table.ForeignKey("FK_VendorInvoiceReceiptAllocations_Tenants_TenantId", x => x.TenantId, "Tenants", "Id", onDelete: ReferentialAction.Restrict);
            table.ForeignKey("FK_VendorInvoiceReceiptAllocations_VendorInvoice_VendorInvoiceId", x => x.VendorInvoiceId, "VendorInvoice", "Id");
            table.ForeignKey("FK_VendorInvoiceReceiptAllocations_VendorInvoiceLineItem_VendorInvoiceLineItemId", x => x.VendorInvoiceLineItemId, "VendorInvoiceLineItem", "Id");
            table.ForeignKey("FK_VendorInvoiceReceiptAllocations_PurchaseOrders_PurchaseOrderId", x => x.PurchaseOrderId, "PurchaseOrders", "Id");
            table.ForeignKey("FK_VendorInvoiceReceiptAllocations_PurchaseOrderItems_PurchaseOrderItemId", x => x.PurchaseOrderItemId, "PurchaseOrderItems", "Id");
            table.ForeignKey("FK_VendorInvoiceReceiptAllocations_PurchaseOrderReceipts_PurchaseOrderReceiptId", x => x.PurchaseOrderReceiptId, "PurchaseOrderReceipts", "Id");
            table.ForeignKey("FK_VendorInvoiceReceiptAllocations_PurchaseOrderReceiptItems_PurchaseOrderReceiptItemId", x => x.PurchaseOrderReceiptItemId, "PurchaseOrderReceiptItems", "Id");
            table.ForeignKey("FK_VendorInvoiceReceiptAllocations_ProcurementReceiptInspectionCases_InspectionCaseId", x => x.InspectionCaseId, "ProcurementReceiptInspectionCases", "Id");
            table.ForeignKey("FK_VendorInvoiceReceiptAllocations_GoodsReceiptNotes_GoodsReceiptNoteId", x => x.GoodsReceiptNoteId, "GoodsReceiptNotes", "Id");
            table.ForeignKey("FK_VendorInvoiceReceiptAllocations_GoodsReceiptNoteItems_GoodsReceiptNoteItemId", x => x.GoodsReceiptNoteItemId, "GoodsReceiptNoteItems", "Id");
        });
        foreach (var column in new[] { "VendorInvoiceId", "VendorInvoiceLineItemId", "PurchaseOrderId", "PurchaseOrderItemId", "PurchaseOrderReceiptId", "PurchaseOrderReceiptItemId", "InspectionCaseId", "GoodsReceiptNoteId", "GoodsReceiptNoteItemId" })
            migrationBuilder.CreateIndex($"IX_VendorInvoiceReceiptAllocations_{column}", "VendorInvoiceReceiptAllocations", column);
        migrationBuilder.CreateIndex("IX_VendorInvoiceReceiptAllocations_TenantId_VendorInvoiceLineItemId", "VendorInvoiceReceiptAllocations", new[] { "TenantId", "VendorInvoiceLineItemId" }, unique: true);
        migrationBuilder.CreateIndex("IX_VendorInvoiceReceiptAllocations_TenantId_GoodsReceiptNoteItemId", "VendorInvoiceReceiptAllocations", new[] { "TenantId", "GoodsReceiptNoteItemId" });
        migrationBuilder.Sql(AllocationTrigger);
        migrationBuilder.Sql(InvoiceTrigger);
        migrationBuilder.Sql(LineTrigger);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM dbo.VendorInvoice WHERE AutoInvoiceRequestId IS NOT NULL) THROW 51720, 'Retain Auto Invoice receipt lineage; rollback requires reconciliation of generated invoices first.', 1;");
        migrationBuilder.Sql("DROP TRIGGER dbo.TR_VendorInvoice_ReceiptSource; DROP TRIGGER dbo.TR_VendorInvoiceLineItem_ReceiptSource;");
        migrationBuilder.DropTable("VendorInvoiceReceiptAllocations");
        migrationBuilder.DropCheckConstraint("CK_VendorInvoice_AcceptedSupplyCoherent", "VendorInvoice");
        migrationBuilder.DropCheckConstraint("CK_VendorInvoice_AcceptedSupplyPurchaseOrder", "VendorInvoice");
        migrationBuilder.AddCheckConstraint("CK_VendorInvoice_AcceptedSupplyCoherent", "VendorInvoice", CoherentBefore);
        migrationBuilder.AddCheckConstraint("CK_VendorInvoice_AcceptedSupplyPurchaseOrder", "VendorInvoice", PurchaseOrderBefore);
        migrationBuilder.DropIndex("IX_VendorInvoice_TenantId_AutoInvoiceRequestId", "VendorInvoice");
        migrationBuilder.DropColumn("AutoInvoiceRequestId", "VendorInvoice");
        migrationBuilder.DropColumn("AutoInvoiceRequestHash", "VendorInvoice");
    }

    internal const string AllocationTrigger = """
        CREATE OR ALTER TRIGGER dbo.TR_VendorInvoiceReceiptAllocations_Source ON dbo.VendorInvoiceReceiptAllocations
        AFTER INSERT, UPDATE, DELETE AS
        BEGIN
            SET NOCOUNT ON;
            IF EXISTS (SELECT 1 FROM deleted) THROW 51721, 'Invoice receipt allocations are immutable source evidence.', 1;
            IF EXISTS (SELECT 1 FROM inserted a
                LEFT JOIN dbo.VendorInvoice i ON i.Id=a.VendorInvoiceId AND i.TenantId=a.TenantId AND i.IsDeleted=0
                LEFT JOIN dbo.VendorInvoiceLineItem l ON l.Id=a.VendorInvoiceLineItemId AND l.VendorInvoiceId=i.Id AND l.TenantId=a.TenantId AND l.IsDeleted=0
                LEFT JOIN dbo.PurchaseOrders po ON po.Id=a.PurchaseOrderId AND po.TenantId=a.TenantId AND po.IsDeleted=0
                LEFT JOIN dbo.PurchaseOrderItems pi ON pi.Id=a.PurchaseOrderItemId AND pi.PurchaseOrderId=po.Id AND pi.TenantId=a.TenantId AND pi.IsDeleted=0
                LEFT JOIN dbo.PurchaseOrderReceipts r ON r.Id=a.PurchaseOrderReceiptId AND r.PurchaseOrderId=po.Id AND r.TenantId=a.TenantId AND r.IsDeleted=0
                LEFT JOIN dbo.PurchaseOrderReceiptItems ri ON ri.Id=a.PurchaseOrderReceiptItemId AND ri.ReceiptId=r.Id AND ri.PurchaseOrderItemId=pi.Id AND ri.TenantId=a.TenantId AND ri.IsDeleted=0
                LEFT JOIN dbo.ProcurementReceiptInspectionCases c ON c.Id=a.InspectionCaseId AND c.PurchaseOrderReceiptId=r.Id AND c.TenantId=a.TenantId AND c.IsDeleted=0
                LEFT JOIN dbo.GoodsReceiptNotes g ON g.Id=a.GoodsReceiptNoteId AND g.PurchaseOrderReceiptId=r.Id AND g.PurchaseOrderId=po.Id AND g.TenantId=a.TenantId AND g.IsDeleted=0
                LEFT JOIN dbo.GoodsReceiptNoteItems gi ON gi.Id=a.GoodsReceiptNoteItemId AND gi.GoodsReceiptNoteId=g.Id AND gi.PurchaseOrderItemId=pi.Id AND gi.TenantId=a.TenantId AND gi.IsDeleted=0
                WHERE i.Id IS NULL OR l.Id IS NULL OR po.Id IS NULL OR pi.Id IS NULL OR r.Id IS NULL OR ri.Id IS NULL OR c.Id IS NULL OR g.Id IS NULL OR gi.Id IS NULL
                   OR i.AutoInvoiceRequestId IS NULL OR i.Status<>1 OR i.AcceptedSupplyKind<>4 OR i.PurchaseOrderId IS NOT NULL
                   OR l.PurchaseOrderItemId<>pi.Id OR l.Quantity<>a.Quantity OR a.Quantity<=0 OR a.IsDeleted=1 OR i.CurrencyCode<>po.Currency)
                THROW 51722, 'Auto Invoice allocation must match an exact same-tenant invoice, PO, GRN and inspection line.', 1;
        END
        """;

    internal const string InvoiceTrigger = """
        CREATE OR ALTER TRIGGER dbo.TR_VendorInvoice_ReceiptSource ON dbo.VendorInvoice
        AFTER INSERT, UPDATE AS
        BEGIN
            SET NOCOUNT ON;
            IF EXISTS (SELECT 1 FROM inserted i LEFT JOIN deleted d ON d.Id=i.Id
                WHERE (d.Id IS NOT NULL AND (ISNULL(i.AutoInvoiceRequestId,'00000000-0000-0000-0000-000000000000')<>ISNULL(d.AutoInvoiceRequestId,'00000000-0000-0000-0000-000000000000') OR ISNULL(i.AutoInvoiceRequestHash,'')<>ISNULL(d.AutoInvoiceRequestHash,'')))
                   OR (i.AcceptedSupplyKind=4 AND i.AutoInvoiceRequestId IS NULL)
                   OR (i.AutoInvoiceRequestId IS NOT NULL AND (ISNULL(i.AcceptedSupplyKind,0)<>4 OR i.AcceptedSupplySourceId<>i.Id OR i.PurchaseOrderId IS NOT NULL OR i.IsOpeningBalance=1 OR ISNULL(LEN(i.AutoInvoiceRequestHash),0)<>64 OR ISNULL(i.AcceptedSupplySnapshotHash,'')<>i.AutoInvoiceRequestHash))
                   OR (i.AutoInvoiceRequestId IS NOT NULL AND i.Status IN (2,3,4,5,6,9) AND (
                        i.MatchingControlEventId IS NULL OR i.MatchingStatus<>2 OR NOT EXISTS (SELECT 1 FROM dbo.VendorInvoiceReceiptAllocations a WHERE a.TenantId=i.TenantId AND a.VendorInvoiceId=i.Id)
                        OR EXISTS (SELECT 1 FROM dbo.VendorInvoiceLineItem l WHERE l.VendorInvoiceId=i.Id AND l.TenantId=i.TenantId AND l.IsDeleted=0 AND NOT EXISTS (
                            SELECT 1 FROM dbo.VendorInvoiceReceiptAllocations a WHERE a.VendorInvoiceLineItemId=l.Id AND a.TenantId=l.TenantId AND a.VendorInvoiceId=i.Id AND a.Quantity=l.Quantity AND a.PurchaseOrderItemId=l.PurchaseOrderItemId)))))
                THROW 51723, 'Auto Invoice source and receipt matching lineage must remain complete and immutable.', 1;
            IF EXISTS (SELECT 1 FROM inserted i LEFT JOIN dbo.ProcurementControlEvents e
                ON e.Id=i.MatchingControlEventId AND e.TenantId=i.TenantId AND e.SourceId=i.Id
                  AND e.SourceType='VendorInvoice' AND e.EventType='InvoiceThreeWayMatching'
                  AND e.Action='InvoiceThreeWayMatchEvaluated' AND e.RuleCode='AP-002' AND e.RuleVersion='TDC-0504'
                  AND e.Result=2 AND e.IsDeleted=0
                WHERE i.AutoInvoiceRequestId IS NOT NULL AND i.IsDeleted=0 AND i.Status IN (2,3,4,5,6,9)
                  AND (e.Id IS NULL OR ISNULL(LEN(i.MatchingSnapshotHash),0)<>64 OR ISJSON(e.ResultValuesJson)<>1 OR ISNULL(i.MatchingSnapshotHash,'')<>ISNULL(JSON_VALUE(CASE WHEN ISJSON(e.ResultValuesJson)=1 THEN e.ResultValuesJson ELSE '{}' END,'$.snapshotHash'),'')
                    OR EXISTS (SELECT 1 FROM (VALUES ('DEC-001'),('DEC-002'),('DEC-003'),('DEC-004'),('DEC-005'),('DEC-006'),('DEC-007'),('DEC-008'),('DEC-009'),('DEC-010'),('DEC-011'),('DEC-012'),('DEC-013'),('DEC-014')) required(DecisionKey)
                        WHERE NOT EXISTS (SELECT 1 FROM OPENJSON(CASE WHEN ISJSON(e.DecisionKeysJson)=1 THEN e.DecisionKeysJson ELSE '[]' END) k WHERE k.[value]=required.DecisionKey))))
                THROW 51725, 'Auto Invoice approval requires the same immutable matching control event as other procurement invoices.', 1;
        END
        """;

    internal const string LineTrigger = """
        CREATE OR ALTER TRIGGER dbo.TR_VendorInvoiceLineItem_ReceiptSource ON dbo.VendorInvoiceLineItem
        AFTER INSERT, UPDATE, DELETE AS
        BEGIN
            SET NOCOUNT ON;
            IF EXISTS (SELECT 1 FROM deleted d JOIN dbo.VendorInvoiceReceiptAllocations a ON a.VendorInvoiceLineItemId=d.Id
                LEFT JOIN inserted i ON i.Id=d.Id WHERE i.Id IS NULL OR i.TenantId<>d.TenantId OR i.VendorInvoiceId<>d.VendorInvoiceId
                OR i.Quantity<>a.Quantity OR ISNULL(i.PurchaseOrderItemId,'00000000-0000-0000-0000-000000000000')<>a.PurchaseOrderItemId)
                THROW 51724, 'Receipt-backed invoice quantities and source lines cannot be reassigned. Cancel the draft and generate a new selection.', 1;
        END
        """;
}
