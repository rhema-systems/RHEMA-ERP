using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260907180000_RecordTenderBidValidityTerms")]
public sealed class RecordTenderBidValidityTerms : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(name: "BidValidityPeriodDays", table: "Tenders", type: "int", nullable: true);
        migrationBuilder.Sql("ALTER TABLE dbo.Tenders ADD CONSTRAINT CK_Tenders_BidValidityPeriodDays CHECK (BidValidityPeriodDays IS NULL OR BidValidityPeriodDays > 0);");
        migrationBuilder.Sql("""
CREATE OR ALTER TRIGGER dbo.TR_Tenders_BidValidityTerms_Immutable
ON dbo.Tenders AFTER UPDATE AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (
        SELECT 1 FROM inserted i INNER JOIN deleted d ON d.Id=i.Id
        WHERE ISNULL(i.BidValidityPeriodDays,0) <> ISNULL(d.BidValidityPeriodDays,0)
        AND (i.Status <> 'Draft' OR d.Status <> 'Draft'
             OR EXISTS (SELECT 1 FROM dbo.ProcurementTenderDocumentRegisters r WHERE r.TenderId=i.Id))
    ) THROW 51207, 'Bid-validity terms may only change on an unbound draft tender before approval.', 1;
END;
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(
        "THROW 51208, 'Recorded tender validity terms must be retained; use a reviewed recovery migration.', 1;");
}
