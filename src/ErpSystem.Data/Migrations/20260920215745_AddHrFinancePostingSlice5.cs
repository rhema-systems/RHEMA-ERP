using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// HR finish plan lane 8, slice 5 (third-party payees): the Accounts Payable side of a posting
    /// register row. An event of kind <c>VendorInvoice</c> raises a Finance vendor invoice instead of
    /// a journal, and the row keeps the invoice id and number and Finance's status as last pulled.
    /// </summary>
    /// <remarks>
    /// <para><b>⚠ The scaffolded body was replaced with guarded SQL</b>, as on every HR migration
    /// since <c>20260907003611_AddSheChecklistBuilder</c>: <c>rebuild-db</c> and the UAT builder
    /// create the schema from the EF model, so a rebuilt database already has these columns and a
    /// bare <c>AddColumn</c> stops the chain.</para>
    ///
    /// <para>All four columns are nullable: every row written before this slice is a journal row and
    /// has no invoice. The index on <c>VendorInvoiceId</c> is what the register's refresh and the
    /// invoice-side lookups read by.</para>
    /// </remarks>
    public partial class AddHrFinancePostingSlice5 : Migration
    {
        private const string Table = "HrFinancePostingRecords";
        private const string Index = "IX_HrFinancePostingRecords_VendorInvoiceId";

        private static string AddColumn(string column, string definition) => $@"
IF COL_LENGTH('dbo.{Table}', '{column}') IS NULL
    ALTER TABLE [dbo].[{Table}] ADD [{column}] {definition};";

        private static string DropColumn(string column) => $@"
IF COL_LENGTH('dbo.{Table}', '{column}') IS NOT NULL
    ALTER TABLE [dbo].[{Table}] DROP COLUMN [{column}];";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(AddColumn("VendorInvoiceId", "uniqueidentifier NULL"));
            migrationBuilder.Sql(AddColumn("VendorInvoiceNumber", "nvarchar(50) NULL"));
            migrationBuilder.Sql(AddColumn("ExternalStatus", "nvarchar(50) NULL"));
            migrationBuilder.Sql(AddColumn("ExternalStatusAt", "datetime2 NULL"));
            migrationBuilder.Sql($@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{Index}' AND object_id = OBJECT_ID('dbo.{Table}'))
    CREATE INDEX [{Index}] ON [dbo].[{Table}] ([VendorInvoiceId]);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{Index}' AND object_id = OBJECT_ID('dbo.{Table}'))
    DROP INDEX [{Index}] ON [dbo].[{Table}];");
            migrationBuilder.Sql(DropColumn("ExternalStatusAt"));
            migrationBuilder.Sql(DropColumn("ExternalStatus"));
            migrationBuilder.Sql(DropColumn("VendorInvoiceNumber"));
            migrationBuilder.Sql(DropColumn("VendorInvoiceId"));
        }
    }
}
