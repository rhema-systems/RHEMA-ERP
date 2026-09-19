using ErpSystem.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260824113000_AddProcedureCaseDocumentProvider")]
public partial class AddProcedureCaseDocumentProvider : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ProvidedBy",
            table: "ProcedureCaseDocuments",
            type: "nvarchar(50)",
            maxLength: 50,
            nullable: false,
            defaultValue: "Internal");

        migrationBuilder.Sql("""
            UPDATE ProcedureCaseDocuments
            SET ProvidedBy = N'Customer'
            WHERE Name LIKE N'Customer identity or eligibility evidence%'
               OR Name LIKE N'Offer, financing, or purchase supporting evidence%'
               OR Name LIKE N'Rental application or tenancy supporting evidence%';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ProvidedBy",
            table: "ProcedureCaseDocuments");
    }
}
