using ErpSystem.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260731170000_AddEstateGroundRentAdministration")]
public partial class AddEstateGroundRentAdministration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "EstateGroundRentAccounts",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EstateManagedAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CustomerBusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PaymentFrequency = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                CalculationMethod = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                AnnualAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                RatePerAcre = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                CurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                NextDueDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                PaymentTermsDays = table.Column<int>(type: "int", nullable: false),
                ReviewFrequencyMonths = table.Column<int>(type: "int", nullable: false),
                NextReviewDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                EscalationMethod = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                EscalationValue = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                GracePeriodDays = table.Column<int>(type: "int", nullable: false),
                PenaltyMethod = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                PenaltyValue = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                PenaltyCapAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                GroundRentIncomeAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AutoPostInvoices = table.Column<bool>(type: "bit", nullable: false),
                Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                table.PrimaryKey("PK_EstateGroundRentAccounts", x => x.Id);
                table.ForeignKey(
                    name: "FK_EstateGroundRentAccounts_Accounts_GroundRentIncomeAccountId",
                    column: x => x.GroundRentIncomeAccountId,
                    principalTable: "Accounts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_EstateGroundRentAccounts_EstateManagedAssets_EstateManagedAssetId",
                    column: x => x.EstateManagedAssetId,
                    principalTable: "EstateManagedAssets",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_EstateGroundRentAccounts_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "EstateGroundRentCharges",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                GroundRentAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false),
                PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false),
                DueDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                BaseAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                FinanceInvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                FinanceInvoiceNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                PenaltyAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                PenaltyInvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                PenaltyInvoiceNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                table.PrimaryKey("PK_EstateGroundRentCharges", x => x.Id);
                table.ForeignKey(
                    name: "FK_EstateGroundRentCharges_EstateGroundRentAccounts_GroundRentAccountId",
                    column: x => x.GroundRentAccountId,
                    principalTable: "EstateGroundRentAccounts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_EstateGroundRentCharges_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "EstateGroundRentReviews",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                GroundRentAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                PreviousAnnualAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                NewAnnualAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                PreviousRatePerAcre = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                NewRatePerAcre = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                EscalationMethod = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                EscalationValue = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                table.PrimaryKey("PK_EstateGroundRentReviews", x => x.Id);
                table.ForeignKey(
                    name: "FK_EstateGroundRentReviews_EstateGroundRentAccounts_GroundRentAccountId",
                    column: x => x.GroundRentAccountId,
                    principalTable: "EstateGroundRentAccounts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_EstateGroundRentReviews_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_EstateGroundRentAccounts_EstateManagedAssetId",
            table: "EstateGroundRentAccounts",
            column: "EstateManagedAssetId");

        migrationBuilder.CreateIndex(
            name: "IX_EstateGroundRentAccounts_GroundRentIncomeAccountId",
            table: "EstateGroundRentAccounts",
            column: "GroundRentIncomeAccountId");

        migrationBuilder.CreateIndex(
            name: "IX_EstateGroundRentAccounts_TenantId_CustomerBusinessPartnerId_Status",
            table: "EstateGroundRentAccounts",
            columns: new[] { "TenantId", "CustomerBusinessPartnerId", "Status" });

        migrationBuilder.CreateIndex(
            name: "IX_EstateGroundRentAccounts_TenantId_EstateManagedAssetId",
            table: "EstateGroundRentAccounts",
            columns: new[] { "TenantId", "EstateManagedAssetId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_EstateGroundRentAccounts_TenantId_NextDueDate_Status",
            table: "EstateGroundRentAccounts",
            columns: new[] { "TenantId", "NextDueDate", "Status" });

        migrationBuilder.CreateIndex(
            name: "IX_EstateGroundRentAccounts_TenantId_NextReviewDate_Status",
            table: "EstateGroundRentAccounts",
            columns: new[] { "TenantId", "NextReviewDate", "Status" });

        migrationBuilder.CreateIndex(
            name: "IX_EstateGroundRentCharges_GroundRentAccountId",
            table: "EstateGroundRentCharges",
            column: "GroundRentAccountId");

        migrationBuilder.CreateIndex(
            name: "IX_EstateGroundRentCharges_TenantId_FinanceInvoiceId",
            table: "EstateGroundRentCharges",
            columns: new[] { "TenantId", "FinanceInvoiceId" });

        migrationBuilder.CreateIndex(
            name: "IX_EstateGroundRentCharges_TenantId_GroundRentAccountId_DueDate",
            table: "EstateGroundRentCharges",
            columns: new[] { "TenantId", "GroundRentAccountId", "DueDate" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_EstateGroundRentCharges_TenantId_PenaltyInvoiceId",
            table: "EstateGroundRentCharges",
            columns: new[] { "TenantId", "PenaltyInvoiceId" });

        migrationBuilder.CreateIndex(
            name: "IX_EstateGroundRentReviews_GroundRentAccountId",
            table: "EstateGroundRentReviews",
            column: "GroundRentAccountId");

        migrationBuilder.CreateIndex(
            name: "IX_EstateGroundRentReviews_TenantId_GroundRentAccountId_EffectiveDate",
            table: "EstateGroundRentReviews",
            columns: new[] { "TenantId", "GroundRentAccountId", "EffectiveDate" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "EstateGroundRentCharges");
        migrationBuilder.DropTable(name: "EstateGroundRentReviews");
        migrationBuilder.DropTable(name: "EstateGroundRentAccounts");
    }
}
