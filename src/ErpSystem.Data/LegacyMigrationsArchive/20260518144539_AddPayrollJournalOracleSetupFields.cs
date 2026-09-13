using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPayrollJournalOracleSetupFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LegacyCompanyCode",
                table: "PayrollJournalMappings",
                type: "nvarchar(5)",
                maxLength: 5,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SequenceNo",
                table: "PayrollJournalMappings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ShortDescription",
                table: "PayrollJournalMappings",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollJournalMappings_TenantId_LegacyCompanyCode_SequenceNo",
                table: "PayrollJournalMappings",
                columns: new[] { "TenantId", "LegacyCompanyCode", "SequenceNo" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PayrollJournalMappings_TenantId_LegacyCompanyCode_SequenceNo",
                table: "PayrollJournalMappings");

            migrationBuilder.DropColumn(
                name: "LegacyCompanyCode",
                table: "PayrollJournalMappings");

            migrationBuilder.DropColumn(
                name: "SequenceNo",
                table: "PayrollJournalMappings");

            migrationBuilder.DropColumn(
                name: "ShortDescription",
                table: "PayrollJournalMappings");
        }
    }
}
