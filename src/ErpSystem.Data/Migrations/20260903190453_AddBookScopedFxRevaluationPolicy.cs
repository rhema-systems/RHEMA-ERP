using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBookScopedFxRevaluationPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [FxRevaluationBatches] WHERE [IsDeleted] = 0)
                    THROW 51000, 'Phase 4 preflight failed: existing FX revaluation batches require an approved reset/reseed or a separately reviewed evidence backfill before book-scoped policy migration.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM [AccountCurrencyLinks] AS [l]
                    INNER JOIN [Accounts] AS [a] ON [a].[Id] = [l].[AccountId] AND [a].[TenantId] = [l].[TenantId]
                    WHERE [l].[IsDeleted] = 0 AND [l].[IsActive] = 1 AND [a].[IsDeleted] = 0
                      AND (
                        NOT EXISTS (
                            SELECT 1
                            FROM [AccountAccountingBooks] AS [m]
                            INNER JOIN [AccountingBooks] AS [b] ON [b].[Id] = [m].[AccountingBookId] AND [b].[TenantId] = [m].[TenantId]
                            INNER JOIN [AccountClassifications] AS [c] ON [c].[Id] = [m].[AccountClassificationId]
                                AND [c].[TenantId] = [m].[TenantId] AND [c].[AccountingBookId] = [m].[AccountingBookId]
                            WHERE [m].[TenantId] = [l].[TenantId] AND [m].[AccountId] = [l].[AccountId]
                              AND [m].[IsDeleted] = 0 AND [m].[IsEnabled] = 1
                              AND [b].[IsDeleted] = 0 AND [b].[IsActive] = 1 AND [b].[AllowsPosting] = 1
                              AND [c].[IsDeleted] = 0 AND [c].[Status] = 2 AND [c].[IsPostingClassification] = 1)
                        OR EXISTS (
                            SELECT 1
                            FROM [AccountAccountingBooks] AS [m]
                            INNER JOIN [AccountClassifications] AS [c] ON [c].[Id] = [m].[AccountClassificationId]
                                AND [c].[TenantId] = [m].[TenantId] AND [c].[AccountingBookId] = [m].[AccountingBookId]
                            WHERE [m].[TenantId] = [l].[TenantId] AND [m].[AccountId] = [l].[AccountId]
                              AND [m].[IsDeleted] = 0 AND [m].[IsEnabled] = 1
                              AND [c].[IsDeleted] = 0
                              AND [l].[RevaluationRequired] <> CASE WHEN [c].[DefaultRevaluationTreatment] = 2 THEN 1 ELSE 0 END)))
                    THROW 51001, 'Phase 4 preflight failed: legacy currency-link revaluation flags do not have an unambiguous classification-default equivalent. Reset/reseed or perform a separately reviewed policy backfill before retrying.', 1;
                """);

            migrationBuilder.DropColumn(
                name: "RevaluationRequired",
                table: "AccountCurrencyLinks");

            migrationBuilder.DropIndex(
                name: "IX_FxRevaluationBatches_TenantId_Scope_RevaluationDate_FiscalPeriodId",
                table: "FxRevaluationBatches");

            migrationBuilder.AddColumn<Guid>(
                name: "AccountAccountingBookId",
                table: "FxRevaluationLines",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "AccountBookCurrencyPolicyId",
                table: "FxRevaluationLines",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AccountClassificationCode",
                table: "FxRevaluationLines",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "AccountClassificationId",
                table: "FxRevaluationLines",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "AccountClassificationName",
                table: "FxRevaluationLines",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ClassificationDefault",
                table: "FxRevaluationLines",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ClosingQuoteSide",
                table: "FxRevaluationLines",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ClosingRateDate",
                table: "FxRevaluationLines",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "ClosingRateType",
                table: "FxRevaluationLines",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CoreAccountType",
                table: "FxRevaluationLines",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EffectivePolicySource",
                table: "FxRevaluationLines",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "EffectiveRevaluationRequired",
                table: "FxRevaluationLines",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "GovernanceWarning",
                table: "FxRevaluationLines",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasGovernanceWarning",
                table: "FxRevaluationLines",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "PriorUnreversedAdjustment",
                table: "FxRevaluationLines",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "RevaluationOverride",
                table: "FxRevaluationLines",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AccountingBookCode",
                table: "FxRevaluationBatches",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "AccountingBookId",
                table: "FxRevaluationBatches",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "PreviewFingerprint",
                table: "FxRevaluationBatches",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AccountBookCurrencyPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountAccountingBookId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountCurrencyLinkId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RevaluationOverride = table.Column<bool>(type: "bit", nullable: true),
                    OverrideReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PendingRevaluationOverride = table.Column<bool>(type: "bit", nullable: true),
                    PendingReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LifecycleStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    WorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecidedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DecidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecisionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_AccountBookCurrencyPolicies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountBookCurrencyPolicies_AccountAccountingBooks_AccountAccountingBookId",
                        column: x => x.AccountAccountingBookId,
                        principalTable: "AccountAccountingBooks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountBookCurrencyPolicies_AccountCurrencyLinks_AccountCurrencyLinkId",
                        column: x => x.AccountCurrencyLinkId,
                        principalTable: "AccountCurrencyLinks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountBookCurrencyPolicies_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FxRevaluationLines_AccountAccountingBookId",
                table: "FxRevaluationLines",
                column: "AccountAccountingBookId");

            migrationBuilder.CreateIndex(
                name: "IX_FxRevaluationLines_AccountBookCurrencyPolicyId",
                table: "FxRevaluationLines",
                column: "AccountBookCurrencyPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_FxRevaluationLines_AccountClassificationId",
                table: "FxRevaluationLines",
                column: "AccountClassificationId");

            migrationBuilder.CreateIndex(
                name: "IX_FxRevaluationBatches_AccountingBookId",
                table: "FxRevaluationBatches",
                column: "AccountingBookId");

            migrationBuilder.CreateIndex(
                name: "IX_FxRevaluationBatches_TenantId_AccountingBookId_Scope_RevaluationDate_FiscalPeriodId",
                table: "FxRevaluationBatches",
                columns: new[] { "TenantId", "AccountingBookId", "Scope", "RevaluationDate", "FiscalPeriodId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_AccountBookCurrencyPolicies_AccountAccountingBookId",
                table: "AccountBookCurrencyPolicies",
                column: "AccountAccountingBookId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountBookCurrencyPolicies_AccountCurrencyLinkId",
                table: "AccountBookCurrencyPolicies",
                column: "AccountCurrencyLinkId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountBookCurrencyPolicies_TenantId_AccountAccountingBookId_AccountCurrencyLinkId",
                table: "AccountBookCurrencyPolicies",
                columns: new[] { "TenantId", "AccountAccountingBookId", "AccountCurrencyLinkId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.AddForeignKey(
                name: "FK_FxRevaluationBatches_AccountingBooks_AccountingBookId",
                table: "FxRevaluationBatches",
                column: "AccountingBookId",
                principalTable: "AccountingBooks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FxRevaluationLines_AccountAccountingBooks_AccountAccountingBookId",
                table: "FxRevaluationLines",
                column: "AccountAccountingBookId",
                principalTable: "AccountAccountingBooks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FxRevaluationLines_AccountBookCurrencyPolicies_AccountBookCurrencyPolicyId",
                table: "FxRevaluationLines",
                column: "AccountBookCurrencyPolicyId",
                principalTable: "AccountBookCurrencyPolicies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FxRevaluationLines_AccountClassifications_AccountClassificationId",
                table: "FxRevaluationLines",
                column: "AccountClassificationId",
                principalTable: "AccountClassifications",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RevaluationRequired",
                table: "AccountCurrencyLinks",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.DropForeignKey(
                name: "FK_FxRevaluationBatches_AccountingBooks_AccountingBookId",
                table: "FxRevaluationBatches");

            migrationBuilder.DropForeignKey(
                name: "FK_FxRevaluationLines_AccountAccountingBooks_AccountAccountingBookId",
                table: "FxRevaluationLines");

            migrationBuilder.DropForeignKey(
                name: "FK_FxRevaluationLines_AccountBookCurrencyPolicies_AccountBookCurrencyPolicyId",
                table: "FxRevaluationLines");

            migrationBuilder.DropForeignKey(
                name: "FK_FxRevaluationLines_AccountClassifications_AccountClassificationId",
                table: "FxRevaluationLines");

            migrationBuilder.DropTable(
                name: "AccountBookCurrencyPolicies");

            migrationBuilder.DropIndex(
                name: "IX_FxRevaluationLines_AccountAccountingBookId",
                table: "FxRevaluationLines");

            migrationBuilder.DropIndex(
                name: "IX_FxRevaluationLines_AccountBookCurrencyPolicyId",
                table: "FxRevaluationLines");

            migrationBuilder.DropIndex(
                name: "IX_FxRevaluationLines_AccountClassificationId",
                table: "FxRevaluationLines");

            migrationBuilder.DropIndex(
                name: "IX_FxRevaluationBatches_AccountingBookId",
                table: "FxRevaluationBatches");

            migrationBuilder.DropIndex(
                name: "IX_FxRevaluationBatches_TenantId_AccountingBookId_Scope_RevaluationDate_FiscalPeriodId",
                table: "FxRevaluationBatches");

            migrationBuilder.DropColumn(
                name: "AccountAccountingBookId",
                table: "FxRevaluationLines");

            migrationBuilder.DropColumn(
                name: "AccountBookCurrencyPolicyId",
                table: "FxRevaluationLines");

            migrationBuilder.DropColumn(
                name: "AccountClassificationCode",
                table: "FxRevaluationLines");

            migrationBuilder.DropColumn(
                name: "AccountClassificationId",
                table: "FxRevaluationLines");

            migrationBuilder.DropColumn(
                name: "AccountClassificationName",
                table: "FxRevaluationLines");

            migrationBuilder.DropColumn(
                name: "ClassificationDefault",
                table: "FxRevaluationLines");

            migrationBuilder.DropColumn(
                name: "ClosingQuoteSide",
                table: "FxRevaluationLines");

            migrationBuilder.DropColumn(
                name: "ClosingRateDate",
                table: "FxRevaluationLines");

            migrationBuilder.DropColumn(
                name: "ClosingRateType",
                table: "FxRevaluationLines");

            migrationBuilder.DropColumn(
                name: "CoreAccountType",
                table: "FxRevaluationLines");

            migrationBuilder.DropColumn(
                name: "EffectivePolicySource",
                table: "FxRevaluationLines");

            migrationBuilder.DropColumn(
                name: "EffectiveRevaluationRequired",
                table: "FxRevaluationLines");

            migrationBuilder.DropColumn(
                name: "GovernanceWarning",
                table: "FxRevaluationLines");

            migrationBuilder.DropColumn(
                name: "HasGovernanceWarning",
                table: "FxRevaluationLines");

            migrationBuilder.DropColumn(
                name: "PriorUnreversedAdjustment",
                table: "FxRevaluationLines");

            migrationBuilder.DropColumn(
                name: "RevaluationOverride",
                table: "FxRevaluationLines");

            migrationBuilder.DropColumn(
                name: "AccountingBookCode",
                table: "FxRevaluationBatches");

            migrationBuilder.DropColumn(
                name: "AccountingBookId",
                table: "FxRevaluationBatches");

            migrationBuilder.DropColumn(
                name: "PreviewFingerprint",
                table: "FxRevaluationBatches");

            migrationBuilder.CreateIndex(
                name: "IX_FxRevaluationBatches_TenantId_Scope_RevaluationDate_FiscalPeriodId",
                table: "FxRevaluationBatches",
                columns: new[] { "TenantId", "Scope", "RevaluationDate", "FiscalPeriodId" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }
    }
}
