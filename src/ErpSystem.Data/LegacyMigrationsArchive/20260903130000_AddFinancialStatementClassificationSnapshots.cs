using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFinancialStatementClassificationSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AccountClassificationId",
                table: "FinancialStatementRowMappings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IncludeClassificationDescendants",
                table: "FinancialStatementRowMappings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "HierarchyFingerprint",
                table: "FinancialStatementLayoutVersions",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PublicationSnapshotSchemaVersion",
                table: "FinancialStatementLayoutVersions",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PublishedAccountingBookCode",
                table: "FinancialStatementLayoutVersions",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PublishedAccountingBookId",
                table: "FinancialStatementLayoutVersions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PublishedAccountingBookName",
                table: "FinancialStatementLayoutVersions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResolutionFingerprint",
                table: "FinancialStatementLayoutVersions",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsProtectedStandard",
                table: "FinancialStatementLayouts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "StandardSourceLayoutId",
                table: "FinancialStatementLayouts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FinancialStatementPublicationAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FinancialStatementLayoutVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FinancialStatementRowId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FinancialStatementRowMappingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MappingType = table.Column<int>(type: "int", nullable: false),
                    AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AccountNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AccountName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AccountType = table.Column<int>(type: "int", nullable: false),
                    AccountingBookId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountingBookCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AccountClassificationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClassificationCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ClassificationName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ClassificationPath = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    MappingSelector = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_FinancialStatementPublicationAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FinancialStatementPublicationAccounts_FinancialStatementLayoutVersions_FinancialStatementLayoutVersionId",
                        column: x => x.FinancialStatementLayoutVersionId,
                        principalTable: "FinancialStatementLayoutVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FinancialStatementPublicationAccounts_FinancialStatementRowMappings_FinancialStatementRowMappingId",
                        column: x => x.FinancialStatementRowMappingId,
                        principalTable: "FinancialStatementRowMappings",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FinancialStatementPublicationAccounts_FinancialStatementRows_FinancialStatementRowId",
                        column: x => x.FinancialStatementRowId,
                        principalTable: "FinancialStatementRows",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FinancialStatementPublicationAccounts_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FinancialStatementRowMappings_AccountClassificationId",
                table: "FinancialStatementRowMappings",
                column: "AccountClassificationId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialStatementRowMappings_TenantId_AccountClassificationId",
                table: "FinancialStatementRowMappings",
                columns: new[] { "TenantId", "AccountClassificationId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinancialStatementLayouts_StandardSourceLayoutId",
                table: "FinancialStatementLayouts",
                column: "StandardSourceLayoutId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialStatementPublicationAccounts_FinancialStatementLayoutVersionId",
                table: "FinancialStatementPublicationAccounts",
                column: "FinancialStatementLayoutVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialStatementPublicationAccounts_FinancialStatementRowId",
                table: "FinancialStatementPublicationAccounts",
                column: "FinancialStatementRowId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialStatementPublicationAccounts_FinancialStatementRowMappingId",
                table: "FinancialStatementPublicationAccounts",
                column: "FinancialStatementRowMappingId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialStatementPublicationAccounts_TenantId_AccountClassificationId",
                table: "FinancialStatementPublicationAccounts",
                columns: new[] { "TenantId", "AccountClassificationId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinancialStatementPublicationAccounts_TenantId_FinancialStatementLayoutVersionId_AccountId",
                table: "FinancialStatementPublicationAccounts",
                columns: new[] { "TenantId", "FinancialStatementLayoutVersionId", "AccountId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_FinancialStatementLayouts_FinancialStatementLayouts_StandardSourceLayoutId",
                table: "FinancialStatementLayouts",
                column: "StandardSourceLayoutId",
                principalTable: "FinancialStatementLayouts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FinancialStatementRowMappings_AccountClassifications_AccountClassificationId",
                table: "FinancialStatementRowMappings",
                column: "AccountClassificationId",
                principalTable: "AccountClassifications",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FinancialStatementLayouts_FinancialStatementLayouts_StandardSourceLayoutId",
                table: "FinancialStatementLayouts");

            migrationBuilder.DropForeignKey(
                name: "FK_FinancialStatementRowMappings_AccountClassifications_AccountClassificationId",
                table: "FinancialStatementRowMappings");

            migrationBuilder.DropTable(
                name: "FinancialStatementPublicationAccounts");

            migrationBuilder.DropIndex(
                name: "IX_FinancialStatementRowMappings_AccountClassificationId",
                table: "FinancialStatementRowMappings");

            migrationBuilder.DropIndex(
                name: "IX_FinancialStatementRowMappings_TenantId_AccountClassificationId",
                table: "FinancialStatementRowMappings");

            migrationBuilder.DropIndex(
                name: "IX_FinancialStatementLayouts_StandardSourceLayoutId",
                table: "FinancialStatementLayouts");

            migrationBuilder.DropColumn(
                name: "AccountClassificationId",
                table: "FinancialStatementRowMappings");

            migrationBuilder.DropColumn(
                name: "IncludeClassificationDescendants",
                table: "FinancialStatementRowMappings");

            migrationBuilder.DropColumn(
                name: "HierarchyFingerprint",
                table: "FinancialStatementLayoutVersions");

            migrationBuilder.DropColumn(
                name: "PublicationSnapshotSchemaVersion",
                table: "FinancialStatementLayoutVersions");

            migrationBuilder.DropColumn(
                name: "PublishedAccountingBookCode",
                table: "FinancialStatementLayoutVersions");

            migrationBuilder.DropColumn(
                name: "PublishedAccountingBookId",
                table: "FinancialStatementLayoutVersions");

            migrationBuilder.DropColumn(
                name: "PublishedAccountingBookName",
                table: "FinancialStatementLayoutVersions");

            migrationBuilder.DropColumn(
                name: "ResolutionFingerprint",
                table: "FinancialStatementLayoutVersions");

            migrationBuilder.DropColumn(
                name: "IsProtectedStandard",
                table: "FinancialStatementLayouts");

            migrationBuilder.DropColumn(
                name: "StandardSourceLayoutId",
                table: "FinancialStatementLayouts");
        }
    }
}
