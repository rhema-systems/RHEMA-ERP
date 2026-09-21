using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// HR finish plan lane 8, slice 6 (HR's one revenue): the Accounts Receivable side of a posting
    /// register row, and the Finance customer a consultant client is billed as.
    /// </summary>
    /// <remarks>
    /// <para><b>⚠ The scaffolded body was replaced with guarded SQL</b>, as on every HR migration
    /// since <c>20260907003611_AddSheChecklistBuilder</c>: <c>rebuild-db</c> and the UAT builder
    /// create the schema from the EF model, so a rebuilt database already has these columns and a
    /// bare <c>AddColumn</c> stops the chain.</para>
    ///
    /// <para>All three columns are nullable. A register row written before this slice is a journal
    /// or AP row; a client without a Finance customer keeps its invoices HR-side (the AR hand-off
    /// records them Skipped) until an administrator links one. <c>FinanceCustomerId</c> is
    /// deliberately not a foreign key: the customer is Finance's row, read through HR's door, and
    /// deleting a customer in Finance must not cascade into HR's client list.</para>
    /// </remarks>
    public partial class AddHrFinancePostingSlice6 : Migration
    {
        private const string Records = "HrFinancePostingRecords";
        private const string Clients = "ConsultantClients";
        private const string Index = "IX_HrFinancePostingRecords_CustomerInvoiceId";

        private static string AddColumn(string table, string column, string definition) => $@"
IF COL_LENGTH('dbo.{table}', '{column}') IS NULL
    ALTER TABLE [dbo].[{table}] ADD [{column}] {definition};";

        private static string DropColumn(string table, string column) => $@"
IF COL_LENGTH('dbo.{table}', '{column}') IS NOT NULL
    ALTER TABLE [dbo].[{table}] DROP COLUMN [{column}];";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(AddColumn(Records, "CustomerInvoiceId", "uniqueidentifier NULL"));
            migrationBuilder.Sql(AddColumn(Records, "CustomerInvoiceNumber", "nvarchar(50) NULL"));
            migrationBuilder.Sql(AddColumn(Clients, "FinanceCustomerId", "uniqueidentifier NULL"));
            migrationBuilder.Sql($@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{Index}' AND object_id = OBJECT_ID('dbo.{Records}'))
    CREATE INDEX [{Index}] ON [dbo].[{Records}] ([CustomerInvoiceId]);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{Index}' AND object_id = OBJECT_ID('dbo.{Records}'))
    DROP INDEX [{Index}] ON [dbo].[{Records}];");
            migrationBuilder.Sql(DropColumn(Clients, "FinanceCustomerId"));
            migrationBuilder.Sql(DropColumn(Records, "CustomerInvoiceNumber"));
            migrationBuilder.Sql(DropColumn(Records, "CustomerInvoiceId"));
        }
    }
}
