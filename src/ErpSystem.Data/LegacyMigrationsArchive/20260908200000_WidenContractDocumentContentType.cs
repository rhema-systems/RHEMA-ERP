using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260908200000_WidenContractDocumentContentType")]
public class WidenContractDocumentContentType : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Some UAT databases were manually widened already. Never truncate their data.
        migrationBuilder.Sql("""
            IF EXISTS (SELECT 1 FROM [dbo].[ContractDocuments] WHERE DATALENGTH([ContentType]) > 510)
                THROW 51000, 'Contract document ContentType exceeds 255 characters; review before migrating.', 1;
            """);
        migrationBuilder.AlterColumn<string>(
            name: "ContentType", table: "ContractDocuments", type: "nvarchar(255)",
            maxLength: 255, nullable: true, oldClrType: typeof(string),
            oldType: "nvarchar(50)", oldMaxLength: 50, oldNullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF EXISTS (SELECT 1 FROM [dbo].[ContractDocuments] WHERE DATALENGTH([ContentType]) > 100)
                THROW 51000, 'Cannot narrow ContentType to 50 characters while longer MIME types are stored.', 1;
            """);
        migrationBuilder.AlterColumn<string>(
            name: "ContentType", table: "ContractDocuments", type: "nvarchar(50)",
            maxLength: 50, nullable: true, oldClrType: typeof(string),
            oldType: "nvarchar(255)", oldMaxLength: 255, oldNullable: true);
    }
}
