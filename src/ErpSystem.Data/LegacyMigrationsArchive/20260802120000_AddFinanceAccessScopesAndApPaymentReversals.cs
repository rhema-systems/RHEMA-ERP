using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Adds the first Finance-only control slice: tenant reversal policy, effective-dated Finance
/// data scopes, and durable reversal lineage for AP payments and their realized-FX postings.
///
/// The migration intentionally introduces scope enforcement disabled. Administrators must create
/// at least one valid grant before the settings service will allow enforcement to be switched on;
/// this avoids accidentally locking the tenant out during deployment.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260802120000_AddFinanceAccessScopesAndApPaymentReversals")]
public partial class AddFinanceAccessScopesAndApPaymentReversals : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "EnforceFinanceAccessScopes",
            table: "FinanceSettings",
            type: "bit",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<int>(
            name: "MinimumReversalReasonLength",
            table: "FinanceSettings",
            type: "int",
            nullable: false,
            defaultValue: 20);

        migrationBuilder.AddColumn<int>(
            name: "ReversalDatePolicy",
            table: "FinanceSettings",
            type: "int",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.AddColumn<Guid>(
            name: "ReversalJournalEntryId",
            table: "VendorPayment",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "ReversalPostingEventId",
            table: "VendorPayment",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "ReversalDate",
            table: "VendorPayment",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ReversalReason",
            table: "VendorPayment",
            type: "nvarchar(1000)",
            maxLength: 1000,
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "ReversedAt",
            table: "VendorPayment",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "ReversedById",
            table: "VendorPayment",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "ReversalJournalEntryId",
            table: "FxRealizedSettlements",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "ReversalPostingEventId",
            table: "FxRealizedSettlements",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ReversalReason",
            table: "FxRealizedSettlements",
            type: "nvarchar(1000)",
            maxLength: 1000,
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "ReversedAt",
            table: "FxRealizedSettlements",
            type: "datetime2",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "FinanceAccessScopeGrants",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ScopeType = table.Column<int>(type: "int", nullable: false),
                ScopeValue = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                AccessLevel = table.Column<int>(type: "int", nullable: false),
                EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FinanceAccessScopeGrants", x => x.Id);
                table.CheckConstraint(
                    "CK_FinanceAccessScopeGrants_AccessLevel",
                    "[AccessLevel] IN (1, 2, 3, 4)");
                table.CheckConstraint(
                    "CK_FinanceAccessScopeGrants_EffectivePeriod",
                    "[EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom]");
                table.CheckConstraint(
                    "CK_FinanceAccessScopeGrants_ScopeType",
                    "[ScopeType] IN (1, 2, 3, 4, 5)");
                table.CheckConstraint(
                    "CK_FinanceAccessScopeGrants_ScopeValue",
                    "([ScopeType] = 1 AND [ScopeValue] IS NULL) OR ([ScopeType] <> 1 AND [ScopeValue] IS NOT NULL)");
                table.ForeignKey(
                    name: "FK_FinanceAccessScopeGrants_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_FinanceAccessScopeGrants_Users_UserId",
                    column: x => x.UserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_VendorPayment_ReversalJournalEntryId",
            table: "VendorPayment",
            column: "ReversalJournalEntryId");

        migrationBuilder.CreateIndex(
            name: "IX_VendorPayment_ReversalPostingEventId",
            table: "VendorPayment",
            column: "ReversalPostingEventId");

        migrationBuilder.CreateIndex(
            name: "IX_FxRealizedSettlements_ReversalJournalEntryId",
            table: "FxRealizedSettlements",
            column: "ReversalJournalEntryId");

        migrationBuilder.CreateIndex(
            name: "IX_FxRealizedSettlements_ReversalPostingEventId",
            table: "FxRealizedSettlements",
            column: "ReversalPostingEventId");

        migrationBuilder.CreateIndex(
            name: "IX_FinanceAccessScopeGrants_TenantId_UserId_IsActive",
            table: "FinanceAccessScopeGrants",
            columns: new[] { "TenantId", "UserId", "IsActive" });

        migrationBuilder.CreateIndex(
            name: "IX_FinanceAccessScopeGrants_TenantId_UserId_ScopeType_ScopeValue",
            table: "FinanceAccessScopeGrants",
            columns: new[] { "TenantId", "UserId", "ScopeType", "ScopeValue" },
            unique: true,
            filter: "[IsDeleted] = 0 AND [IsActive] = 1");

        migrationBuilder.CreateIndex(
            name: "IX_FinanceAccessScopeGrants_UserId",
            table: "FinanceAccessScopeGrants",
            column: "UserId");

        migrationBuilder.AddForeignKey(
            name: "FK_VendorPayment_JournalEntries_ReversalJournalEntryId",
            table: "VendorPayment",
            column: "ReversalJournalEntryId",
            principalTable: "JournalEntries",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_VendorPayment_FinancePostingEvents_ReversalPostingEventId",
            table: "VendorPayment",
            column: "ReversalPostingEventId",
            principalTable: "FinancePostingEvents",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_FxRealizedSettlements_JournalEntries_ReversalJournalEntryId",
            table: "FxRealizedSettlements",
            column: "ReversalJournalEntryId",
            principalTable: "JournalEntries",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_FxRealizedSettlements_FinancePostingEvents_ReversalPostingEventId",
            table: "FxRealizedSettlements",
            column: "ReversalPostingEventId",
            principalTable: "FinancePostingEvents",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_VendorPayment_JournalEntries_ReversalJournalEntryId",
            table: "VendorPayment");

        migrationBuilder.DropForeignKey(
            name: "FK_VendorPayment_FinancePostingEvents_ReversalPostingEventId",
            table: "VendorPayment");

        migrationBuilder.DropForeignKey(
            name: "FK_FxRealizedSettlements_JournalEntries_ReversalJournalEntryId",
            table: "FxRealizedSettlements");

        migrationBuilder.DropForeignKey(
            name: "FK_FxRealizedSettlements_FinancePostingEvents_ReversalPostingEventId",
            table: "FxRealizedSettlements");

        migrationBuilder.DropTable(name: "FinanceAccessScopeGrants");

        migrationBuilder.DropColumn(name: "EnforceFinanceAccessScopes", table: "FinanceSettings");
        migrationBuilder.DropColumn(name: "MinimumReversalReasonLength", table: "FinanceSettings");
        migrationBuilder.DropColumn(name: "ReversalDatePolicy", table: "FinanceSettings");

        migrationBuilder.DropColumn(name: "ReversalJournalEntryId", table: "VendorPayment");
        migrationBuilder.DropColumn(name: "ReversalPostingEventId", table: "VendorPayment");
        migrationBuilder.DropColumn(name: "ReversalDate", table: "VendorPayment");
        migrationBuilder.DropColumn(name: "ReversalReason", table: "VendorPayment");
        migrationBuilder.DropColumn(name: "ReversedAt", table: "VendorPayment");
        migrationBuilder.DropColumn(name: "ReversedById", table: "VendorPayment");

        migrationBuilder.DropColumn(name: "ReversalJournalEntryId", table: "FxRealizedSettlements");
        migrationBuilder.DropColumn(name: "ReversalPostingEventId", table: "FxRealizedSettlements");
        migrationBuilder.DropColumn(name: "ReversalReason", table: "FxRealizedSettlements");
        migrationBuilder.DropColumn(name: "ReversedAt", table: "FxRealizedSettlements");
    }
}
