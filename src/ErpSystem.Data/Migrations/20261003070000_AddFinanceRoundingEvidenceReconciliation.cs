using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261003070000_AddFinanceRoundingEvidenceReconciliation")]
public partial class AddFinanceRoundingEvidenceReconciliation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "FinanceRoundingEvidence",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PostingIdempotencyKey = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                SourceModule = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                SourceDocumentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                SourceDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PostingAction = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                Eligibility = table.Column<int>(type: "int", nullable: false),
                CurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                DecimalPlaces = table.Column<int>(type: "int", nullable: false),
                OriginalAmount = table.Column<decimal>(type: "decimal(20,6)", nullable: false),
                RoundedAmount = table.Column<decimal>(type: "decimal(20,6)", nullable: false),
                DeltaAmount = table.Column<decimal>(type: "decimal(20,6)", nullable: false),
                Increment = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                Method = table.Column<int>(type: "int", nullable: false),
                FunctionalCurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                FunctionalDecimalPlaces = table.Column<int>(type: "int", nullable: false),
                OriginalFunctionalAmount = table.Column<decimal>(type: "decimal(20,6)", nullable: false),
                RoundedFunctionalAmount = table.Column<decimal>(type: "decimal(20,6)", nullable: false),
                FunctionalDeltaAmount = table.Column<decimal>(type: "decimal(20,6)", nullable: false),
                ExchangeRateId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ExchangeRate = table.Column<decimal>(type: "decimal(18,10)", nullable: false),
                ExchangeRateSource = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                ExchangeRateDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                GainAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                LossAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FinanceRoundingEvidence", x => x.Id);
                table.UniqueConstraint("AK_FinanceRoundingEvidence_TenantId_Id", x => new { x.TenantId, x.Id });
                table.CheckConstraint("CK_FinanceRoundingEvidence_Amounts", "[OriginalAmount] > 0 AND [RoundedAmount] > 0 AND [DeltaAmount] = [RoundedAmount] - [OriginalAmount]");
                table.CheckConstraint("CK_FinanceRoundingEvidence_Increment", "[Increment] > 0");
                table.CheckConstraint("CK_FinanceRoundingEvidence_Places", "[DecimalPlaces] BETWEEN 0 AND 4 AND [FunctionalDecimalPlaces] BETWEEN 0 AND 4");
                table.ForeignKey("FK_FinanceRoundingEvidence_Accounts_TenantId_GainAccountId", x => new { x.TenantId, x.GainAccountId }, "Accounts", new[] { "TenantId", "Id" }, onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_FinanceRoundingEvidence_Accounts_TenantId_LossAccountId", x => new { x.TenantId, x.LossAccountId }, "Accounts", new[] { "TenantId", "Id" }, onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_FinanceRoundingEvidence_ExchangeRates_ExchangeRateId", x => x.ExchangeRateId, "ExchangeRates", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_FinanceRoundingEvidence_Tenants_TenantId", x => x.TenantId, "Tenants", "Id", onDelete: ReferentialAction.Restrict);
            });

        AddSourceColumns(migrationBuilder, "Invoices");
        AddSourceColumns(migrationBuilder, "VendorInvoice");
        AddSourceColumns(migrationBuilder, "CustomerPayment");
        AddSourceColumns(migrationBuilder, "VendorPayment");
        AddSourceColumns(migrationBuilder, "CashTransactions");
        migrationBuilder.AddColumn<Guid>("FinanceRoundingEvidenceId", "FinancePostingEvents", "uniqueidentifier", nullable: true);

        migrationBuilder.CreateIndex("IX_FinanceRoundingEvidence_ExchangeRateId", "FinanceRoundingEvidence", "ExchangeRateId");
        migrationBuilder.CreateIndex("IX_FinanceRoundingEvidence_TenantId_GainAccountId", "FinanceRoundingEvidence", new[] { "TenantId", "GainAccountId" });
        migrationBuilder.CreateIndex("IX_FinanceRoundingEvidence_TenantId_LossAccountId", "FinanceRoundingEvidence", new[] { "TenantId", "LossAccountId" });
        migrationBuilder.CreateIndex("IX_FinanceRoundingEvidence_TenantId_PostingIdempotencyKey", "FinanceRoundingEvidence", new[] { "TenantId", "PostingIdempotencyKey" }, unique: true, filter: "[IsDeleted] = 0");
        migrationBuilder.CreateIndex("IX_FinanceRoundingEvidence_TenantId_SourceModule_SourceDocumentType_SourceDocumentId_PostingAction", "FinanceRoundingEvidence", new[] { "TenantId", "SourceModule", "SourceDocumentType", "SourceDocumentId", "PostingAction" });
        AddSourceEvidenceLink(migrationBuilder, "Invoices");
        AddSourceEvidenceLink(migrationBuilder, "VendorInvoice");
        AddSourceEvidenceLink(migrationBuilder, "CustomerPayment");
        AddSourceEvidenceLink(migrationBuilder, "VendorPayment");
        AddSourceEvidenceLink(migrationBuilder, "CashTransactions");
        AddSourceEvidenceLink(migrationBuilder, "FinancePostingEvents", adjustment: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        DropSourceEvidenceLink(migrationBuilder, "FinancePostingEvents", adjustment: false);
        DropSourceEvidenceLink(migrationBuilder, "CashTransactions");
        DropSourceEvidenceLink(migrationBuilder, "VendorPayment");
        DropSourceEvidenceLink(migrationBuilder, "CustomerPayment");
        DropSourceEvidenceLink(migrationBuilder, "VendorInvoice");
        DropSourceEvidenceLink(migrationBuilder, "Invoices");
        migrationBuilder.DropTable("FinanceRoundingEvidence");
    }

    private static void AddSourceColumns(MigrationBuilder migrationBuilder, string table)
    {
        migrationBuilder.AddColumn<decimal>("RoundingAdjustmentAmount", table, "decimal(20,6)", nullable: false, defaultValue: 0m);
        migrationBuilder.AddColumn<Guid>("FinanceRoundingEvidenceId", table, "uniqueidentifier", nullable: true);
    }

    private static void AddSourceEvidenceLink(MigrationBuilder migrationBuilder, string table, bool adjustment = true)
    {
        migrationBuilder.CreateIndex($"IX_{table}_FinanceRoundingEvidenceId", table, "FinanceRoundingEvidenceId");
        migrationBuilder.AddForeignKey($"FK_{table}_FinanceRoundingEvidence_TenantId_FinanceRoundingEvidenceId", table,
            new[] { "TenantId", "FinanceRoundingEvidenceId" }, "FinanceRoundingEvidence", new[] { "TenantId", "Id" }, onDelete: ReferentialAction.Restrict);
    }

    private static void DropSourceEvidenceLink(MigrationBuilder migrationBuilder, string table, bool adjustment = true)
    {
        migrationBuilder.DropForeignKey($"FK_{table}_FinanceRoundingEvidence_TenantId_FinanceRoundingEvidenceId", table);
        migrationBuilder.DropIndex($"IX_{table}_FinanceRoundingEvidenceId", table);
        migrationBuilder.DropColumn("FinanceRoundingEvidenceId", table);
        if (adjustment) migrationBuilder.DropColumn("RoundingAdjustmentAmount", table);
    }
}
