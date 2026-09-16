using ErpSystem.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260810120000_InsertLandAcquisitionVendorPaymentStage")]
public partial class InsertLandAcquisitionVendorPaymentStage : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE [LandAcquisitionChecklistResponses]
            SET [StageOrder] = [StageOrder] + 1
            WHERE [StageOrder] BETWEEN 8 AND 15;
            """);

        migrationBuilder.Sql("""
            UPDATE [LandAcquisitionNotes]
            SET [StageOrder] = [StageOrder] + 1
            WHERE [StageOrder] BETWEEN 8 AND 15;
            """);

        migrationBuilder.Sql("""
            UPDATE [LandAcquisitions]
            SET [StageOrder] = [StageOrder] + 1
            WHERE [StageOrder] BETWEEN 8 AND 15;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE [LandAcquisitions]
            SET [StageOrder] = [StageOrder] - 1
            WHERE [StageOrder] BETWEEN 9 AND 16;
            """);

        migrationBuilder.Sql("""
            UPDATE [LandAcquisitionNotes]
            SET [StageOrder] = [StageOrder] - 1
            WHERE [StageOrder] BETWEEN 9 AND 16;
            """);

        migrationBuilder.Sql("""
            UPDATE [LandAcquisitionChecklistResponses]
            SET [StageOrder] = [StageOrder] - 1
            WHERE [StageOrder] BETWEEN 9 AND 16;
            """);
    }
}
