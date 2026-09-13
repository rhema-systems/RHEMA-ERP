using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGovernedAccountSegmentIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (
    SELECT 1 FROM [AccountSegmentStructures] WHERE [IsDeleted] = 0
    GROUP BY [TenantId], [SegmentCode] HAVING COUNT(*) > 1)
    THROW 51000, 'Phase 5 preflight failed: duplicate account-segment stable codes require reconciliation.', 1;
IF EXISTS (
    SELECT 1 FROM [AccountSegmentStructures] WHERE [IsDeleted] = 0 AND [IsActive] = 1
    GROUP BY [TenantId], [SegmentPosition] HAVING COUNT(*) > 1)
    THROW 51000, 'Phase 5 preflight failed: duplicate active account-segment positions require reconciliation.', 1;
IF EXISTS (
    SELECT 1 FROM [AccountSegmentStructures] WHERE [IsDeleted] = 0 AND [IsActive] = 1 AND [IsNaturalAccount] = 1
    GROUP BY [TenantId] HAVING COUNT(*) > 1)
    THROW 51000, 'Phase 5 preflight failed: multiple active Natural Account segments require reconciliation.', 1;
IF EXISTS (
    SELECT 1 FROM [AccountSegmentValues] WHERE [IsDeleted] = 0
    GROUP BY [TenantId], [AccountId], [SegmentStructureId] HAVING COUNT(*) > 1)
    THROW 51000, 'Phase 5 preflight failed: duplicate account segment assignments require reconciliation.', 1;
IF EXISTS (
    SELECT 1 FROM [AccountSegmentValues] WHERE [IsDeleted] = 0
    GROUP BY [TenantId], [AccountId], [SegmentPosition] HAVING COUNT(*) > 1)
    THROW 51000, 'Phase 5 preflight failed: duplicate account segment positions require reconciliation.', 1;
IF EXISTS (
    SELECT 1
    FROM [AccountSegmentValues] value
    LEFT JOIN [Accounts] account ON account.[Id] = value.[AccountId]
    LEFT JOIN [AccountSegmentStructures] segment ON segment.[Id] = value.[SegmentStructureId]
    WHERE value.[IsDeleted] = 0 AND (
        account.[Id] IS NULL OR segment.[Id] IS NULL
        OR account.[IsDeleted] = 1 OR segment.[IsDeleted] = 1
        OR value.[TenantId] <> account.[TenantId]
        OR value.[TenantId] <> segment.[TenantId]))
    THROW 51000, 'Phase 5 preflight failed: account segment assignments contain missing, deleted, or cross-tenant account/segment lineage.', 1;
IF EXISTS (
    SELECT 1
    FROM [AccountSegmentValues] value
    LEFT JOIN [SegmentLookupValues] lookupValue ON lookupValue.[Id] = value.[SegmentLookupValueId]
    WHERE value.[IsDeleted] = 0 AND value.[SegmentLookupValueId] IS NOT NULL AND (
        lookupValue.[Id] IS NULL OR lookupValue.[IsDeleted] = 1
        OR lookupValue.[TenantId] <> value.[TenantId]
        OR lookupValue.[SegmentStructureId] <> value.[SegmentStructureId]))
    THROW 51000, 'Phase 5 preflight failed: account segment assignments contain missing, deleted, cross-tenant, or wrong-segment lookup lineage.', 1;");

            migrationBuilder.DropForeignKey(
                name: "FK_AccountSegmentValues_AccountSegmentStructures_SegmentStructureId",
                table: "AccountSegmentValues");

            migrationBuilder.DropForeignKey(
                name: "FK_AccountSegmentValues_Accounts_AccountId",
                table: "AccountSegmentValues");

            migrationBuilder.DropForeignKey(
                name: "FK_AccountSegmentValues_SegmentLookupValues_SegmentLookupValueId",
                table: "AccountSegmentValues");

            migrationBuilder.DropIndex(
                name: "IX_AccountSegmentValues_AccountId",
                table: "AccountSegmentValues");

            migrationBuilder.DropIndex(
                name: "IX_AccountSegmentValues_SegmentLookupValueId",
                table: "AccountSegmentValues");

            migrationBuilder.DropIndex(
                name: "IX_AccountSegmentValues_SegmentStructureId",
                table: "AccountSegmentValues");

            migrationBuilder.DropIndex(
                name: "IX_AccountSegmentValues_TenantId",
                table: "AccountSegmentValues");

            migrationBuilder.DropIndex(
                name: "IX_AccountSegmentStructures_TenantId",
                table: "AccountSegmentStructures");

            migrationBuilder.DropColumn(
                name: "IsMandatory",
                table: "AccountSegmentStructures");

            migrationBuilder.AddColumn<DateTime>(
                name: "FrozenAtUtc",
                table: "AccountSegmentStructures",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FrozenByUserId",
                table: "AccountSegmentStructures",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSystemDefined",
                table: "AccountSegmentStructures",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "LifecycleStatus",
                table: "AccountSegmentStructures",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.Sql(
                "UPDATE [AccountSegmentStructures] SET [LifecycleStatus] = 2 WHERE [IsActive] = 1 AND [IsDeleted] = 0;");

            migrationBuilder.AddColumn<string>(
                name: "RetirementReason",
                table: "AccountSegmentStructures",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "AccountSegmentStructures",
                type: "rowversion",
                rowVersion: true,
                nullable: false);

            migrationBuilder.AddCheckConstraint(
                name: "CK_AccountSegmentStructures_LifecycleActive",
                table: "AccountSegmentStructures",
                sql: "([LifecycleStatus] IN (2, 3) AND [IsActive] = 1) OR ([LifecycleStatus] IN (1, 4) AND [IsActive] = 0)");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Accounts_TenantId_Id",
                table: "Accounts",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_AccountSegmentStructures_TenantId_Id",
                table: "AccountSegmentStructures",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_SegmentLookupValues_TenantId_Id",
                table: "SegmentLookupValues",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountSegmentValues_TenantId_AccountId_SegmentPosition",
                table: "AccountSegmentValues",
                columns: new[] { "TenantId", "AccountId", "SegmentPosition" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_AccountSegmentValues_TenantId_AccountId_SegmentStructureId",
                table: "AccountSegmentValues",
                columns: new[] { "TenantId", "AccountId", "SegmentStructureId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_AccountSegmentValues_TenantId_SegmentLookupValueId",
                table: "AccountSegmentValues",
                columns: new[] { "TenantId", "SegmentLookupValueId" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountSegmentValues_TenantId_SegmentStructureId",
                table: "AccountSegmentValues",
                columns: new[] { "TenantId", "SegmentStructureId" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountSegmentStructures_TenantId",
                table: "AccountSegmentStructures",
                column: "TenantId",
                unique: true,
                filter: "[IsDeleted] = 0 AND [IsNaturalAccount] = 1 AND [IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_AccountSegmentStructures_TenantId_SegmentCode",
                table: "AccountSegmentStructures",
                columns: new[] { "TenantId", "SegmentCode" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_AccountSegmentStructures_TenantId_SegmentPosition",
                table: "AccountSegmentStructures",
                columns: new[] { "TenantId", "SegmentPosition" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [IsActive] = 1");

            migrationBuilder.AddForeignKey(
                name: "FK_AccountSegmentValues_AccountSegmentStructures_TenantId_SegmentStructureId",
                table: "AccountSegmentValues",
                columns: new[] { "TenantId", "SegmentStructureId" },
                principalTable: "AccountSegmentStructures",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AccountSegmentValues_Accounts_TenantId_AccountId",
                table: "AccountSegmentValues",
                columns: new[] { "TenantId", "AccountId" },
                principalTable: "Accounts",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AccountSegmentValues_SegmentLookupValues_TenantId_SegmentLookupValueId",
                table: "AccountSegmentValues",
                columns: new[] { "TenantId", "SegmentLookupValueId" },
                principalTable: "SegmentLookupValues",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AccountSegmentValues_AccountSegmentStructures_TenantId_SegmentStructureId",
                table: "AccountSegmentValues");

            migrationBuilder.DropForeignKey(
                name: "FK_AccountSegmentValues_Accounts_TenantId_AccountId",
                table: "AccountSegmentValues");

            migrationBuilder.DropForeignKey(
                name: "FK_AccountSegmentValues_SegmentLookupValues_TenantId_SegmentLookupValueId",
                table: "AccountSegmentValues");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AccountSegmentStructures_LifecycleActive",
                table: "AccountSegmentStructures");

            migrationBuilder.DropIndex(
                name: "IX_AccountSegmentValues_TenantId_AccountId_SegmentPosition",
                table: "AccountSegmentValues");

            migrationBuilder.DropIndex(
                name: "IX_AccountSegmentValues_TenantId_AccountId_SegmentStructureId",
                table: "AccountSegmentValues");

            migrationBuilder.DropIndex(
                name: "IX_AccountSegmentValues_TenantId_SegmentLookupValueId",
                table: "AccountSegmentValues");

            migrationBuilder.DropIndex(
                name: "IX_AccountSegmentValues_TenantId_SegmentStructureId",
                table: "AccountSegmentValues");

            migrationBuilder.DropIndex(
                name: "IX_AccountSegmentStructures_TenantId",
                table: "AccountSegmentStructures");

            migrationBuilder.DropIndex(
                name: "IX_AccountSegmentStructures_TenantId_SegmentCode",
                table: "AccountSegmentStructures");

            migrationBuilder.DropIndex(
                name: "IX_AccountSegmentStructures_TenantId_SegmentPosition",
                table: "AccountSegmentStructures");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Accounts_TenantId_Id",
                table: "Accounts");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_AccountSegmentStructures_TenantId_Id",
                table: "AccountSegmentStructures");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_SegmentLookupValues_TenantId_Id",
                table: "SegmentLookupValues");

            migrationBuilder.DropColumn(
                name: "FrozenAtUtc",
                table: "AccountSegmentStructures");

            migrationBuilder.DropColumn(
                name: "FrozenByUserId",
                table: "AccountSegmentStructures");

            migrationBuilder.DropColumn(
                name: "LifecycleStatus",
                table: "AccountSegmentStructures");

            migrationBuilder.DropColumn(
                name: "RetirementReason",
                table: "AccountSegmentStructures");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "AccountSegmentStructures");

            migrationBuilder.DropColumn(
                name: "IsSystemDefined",
                table: "AccountSegmentStructures");

            migrationBuilder.AddColumn<bool>(
                name: "IsMandatory",
                table: "AccountSegmentStructures",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountSegmentValues_TenantId",
                table: "AccountSegmentValues",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountSegmentValues_AccountId",
                table: "AccountSegmentValues",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountSegmentValues_SegmentLookupValueId",
                table: "AccountSegmentValues",
                column: "SegmentLookupValueId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountSegmentValues_SegmentStructureId",
                table: "AccountSegmentValues",
                column: "SegmentStructureId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountSegmentStructures_TenantId",
                table: "AccountSegmentStructures",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_AccountSegmentValues_AccountSegmentStructures_SegmentStructureId",
                table: "AccountSegmentValues",
                column: "SegmentStructureId",
                principalTable: "AccountSegmentStructures",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AccountSegmentValues_Accounts_AccountId",
                table: "AccountSegmentValues",
                column: "AccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AccountSegmentValues_SegmentLookupValues_SegmentLookupValueId",
                table: "AccountSegmentValues",
                column: "SegmentLookupValueId",
                principalTable: "SegmentLookupValues",
                principalColumn: "Id");
        }
    }
}
