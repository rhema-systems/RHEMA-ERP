using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260728220000_AddFinancialStatementLayouts")]
    public partial class AddFinancialStatementLayouts : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FinancialStatementLayouts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    StatementType = table.Column<int>(type: "int", nullable: false),
                    AccountingBookId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_FinancialStatementLayouts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FinancialStatementLayouts_AccountingBooks_AccountingBookId",
                        column: x => x.AccountingBookId,
                        principalTable: "AccountingBooks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinancialStatementLayouts_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FinancialStatementLayoutVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FinancialStatementLayoutId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PublishedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PublishedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Revision = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_FinancialStatementLayoutVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FinancialStatementLayoutVersions_FinancialStatementLayouts_FinancialStatementLayoutId",
                        column: x => x.FinancialStatementLayoutId,
                        principalTable: "FinancialStatementLayouts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinancialStatementLayoutVersions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FinancialStatementRows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FinancialStatementLayoutVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParentRowId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RowType = table.Column<int>(type: "int", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    Formula = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SignMultiplier = table.Column<int>(type: "int", nullable: false),
                    IsVisible = table.Column<bool>(type: "bit", nullable: false),
                    SuppressIfZero = table.Column<bool>(type: "bit", nullable: false),
                    ShowAccountDetails = table.Column<bool>(type: "bit", nullable: false),
                    IsBold = table.Column<bool>(type: "bit", nullable: false),
                    IsItalic = table.Column<bool>(type: "bit", nullable: false),
                    IsUnderlined = table.Column<bool>(type: "bit", nullable: false),
                    IndentLevel = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_FinancialStatementRows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FinancialStatementRows_FinancialStatementLayoutVersions_FinancialStatementLayoutVersionId",
                        column: x => x.FinancialStatementLayoutVersionId,
                        principalTable: "FinancialStatementLayoutVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FinancialStatementRows_FinancialStatementRows_ParentRowId",
                        column: x => x.ParentRowId,
                        principalTable: "FinancialStatementRows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinancialStatementRows_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FinancialStatementRowMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FinancialStatementRowId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MappingType = table.Column<int>(type: "int", nullable: false),
                    AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FromAccountNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ToAccountNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
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
                    table.PrimaryKey("PK_FinancialStatementRowMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FinancialStatementRowMappings_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinancialStatementRowMappings_FinancialStatementRows_FinancialStatementRowId",
                        column: x => x.FinancialStatementRowId,
                        principalTable: "FinancialStatementRows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FinancialStatementRowMappings_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FinancialStatementLayouts_AccountingBookId",
                table: "FinancialStatementLayouts",
                column: "AccountingBookId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialStatementLayouts_TenantId_AccountingBookId_StatementType_IsActive",
                table: "FinancialStatementLayouts",
                columns: new[] { "TenantId", "AccountingBookId", "StatementType", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_FinancialStatementLayouts_TenantId_Code",
                table: "FinancialStatementLayouts",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinancialStatementLayoutVersions_FinancialStatementLayoutId",
                table: "FinancialStatementLayoutVersions",
                column: "FinancialStatementLayoutId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialStatementLayoutVersions_TenantId_FinancialStatementLayoutId_Status_EffectiveFrom_EffectiveTo",
                table: "FinancialStatementLayoutVersions",
                columns: new[] { "TenantId", "FinancialStatementLayoutId", "Status", "EffectiveFrom", "EffectiveTo" });

            migrationBuilder.CreateIndex(
                name: "IX_FinancialStatementLayoutVersions_TenantId_FinancialStatementLayoutId_VersionNumber",
                table: "FinancialStatementLayoutVersions",
                columns: new[] { "TenantId", "FinancialStatementLayoutId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinancialStatementRowMappings_FinancialStatementRowId",
                table: "FinancialStatementRowMappings",
                column: "FinancialStatementRowId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialStatementRowMappings_TenantId_AccountId",
                table: "FinancialStatementRowMappings",
                columns: new[] { "TenantId", "AccountId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinancialStatementRowMappings_TenantId_FinancialStatementRowId",
                table: "FinancialStatementRowMappings",
                columns: new[] { "TenantId", "FinancialStatementRowId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinancialStatementRows_FinancialStatementLayoutVersionId",
                table: "FinancialStatementRows",
                column: "FinancialStatementLayoutVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialStatementRows_ParentRowId",
                table: "FinancialStatementRows",
                column: "ParentRowId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialStatementRows_TenantId_FinancialStatementLayoutVersionId_DisplayOrder",
                table: "FinancialStatementRows",
                columns: new[] { "TenantId", "FinancialStatementLayoutVersionId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_FinancialStatementRows_TenantId_FinancialStatementLayoutVersionId_RowCode",
                table: "FinancialStatementRows",
                columns: new[] { "TenantId", "FinancialStatementLayoutVersionId", "RowCode" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "FinancialStatementRowMappings");
            migrationBuilder.DropTable(name: "FinancialStatementRows");
            migrationBuilder.DropTable(name: "FinancialStatementLayoutVersions");
            migrationBuilder.DropTable(name: "FinancialStatementLayouts");
        }
    }
}
