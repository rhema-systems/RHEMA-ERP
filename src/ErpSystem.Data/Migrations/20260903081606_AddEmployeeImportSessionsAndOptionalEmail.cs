using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeImportSessionsAndOptionalEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Email is optional from here on (docs/HR/HR-EMPLOYEE-IMPORT-DESIGN.md §4, decision 1,
            // 2026-09-03). Two things EF cannot know about:
            //  1. The old NormalizeEmail wrote '' for blank. The new unique index is filtered on
            //     NOT NULL, and '' is not NULL — two blanks would still collide. Convert them first.
            //  2. The 2026-03-13 baseline created a single-column, tenant-blind unique index
            //     IX_Employees_EmailAddress that the model no longer declares. EF will not touch it,
            //     and if it survives in a live database it rejects the second NULL. Drop it if present.
            migrationBuilder.Sql(@"
UPDATE [dbo].[Employees] SET [EmailAddress] = NULL WHERE [EmailAddress] IS NOT NULL AND LTRIM(RTRIM([EmailAddress])) = N'';

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Employees_EmailAddress' AND object_id = OBJECT_ID(N'[dbo].[Employees]'))
    DROP INDEX [IX_Employees_EmailAddress] ON [dbo].[Employees];
");

            migrationBuilder.DropIndex(
                name: "IX_Employee_Tenant_EmailAddress",
                table: "Employees");

            migrationBuilder.AlterColumn<string>(
                name: "EmailAddress",
                table: "Employees",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.CreateTable(
                name: "EmployeeImportSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FileHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    TemplateVersion = table.Column<int>(type: "int", nullable: false),
                    SourceFileUploadRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceDocumentRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UploadedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UploadedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    UploadedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CommitPolicy = table.Column<int>(type: "int", nullable: true),
                    CommitRequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CommitRequestedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CommitStartedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CommitCompletedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TotalRows = table.Column<int>(type: "int", nullable: false),
                    ReadyCount = table.Column<int>(type: "int", nullable: false),
                    WarningCount = table.Column<int>(type: "int", nullable: false),
                    ErrorCount = table.Column<int>(type: "int", nullable: false),
                    SkippedCount = table.Column<int>(type: "int", nullable: false),
                    CommittedCount = table.Column<int>(type: "int", nullable: false),
                    FailedCount = table.Column<int>(type: "int", nullable: false),
                    FileFindingsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CounterReconciliationJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FailureMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_EmployeeImportSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeImportSessions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeImportRows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowNumber = table.Column<int>(type: "int", nullable: false),
                    StaffNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DisplayName = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    EmploymentType = table.Column<int>(type: "int", nullable: true),
                    RawJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ResolvedJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FindingsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Outcome = table.Column<int>(type: "int", nullable: false),
                    Skip = table.Column<bool>(type: "bit", nullable: false),
                    ErrorCount = table.Column<int>(type: "int", nullable: false),
                    WarningCount = table.Column<int>(type: "int", nullable: false),
                    ManagerRowNumber = table.Column<int>(type: "int", nullable: true),
                    CreatedEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CommitMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CommittedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_EmployeeImportRows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeImportRows_EmployeeImportSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "EmployeeImportSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmployeeImportRows_Employees_CreatedEmployeeId",
                        column: x => x.CreatedEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeImportRows_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Employee_Tenant_EmailAddress",
                table: "Employees",
                columns: new[] { "TenantId", "EmailAddress" },
                unique: true,
                filter: "[EmailAddress] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeImportRow_Session_RowNumber",
                table: "EmployeeImportRows",
                columns: new[] { "SessionId", "RowNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeImportRows_CreatedEmployeeId",
                table: "EmployeeImportRows",
                column: "CreatedEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeImportRows_SessionId_Outcome",
                table: "EmployeeImportRows",
                columns: new[] { "SessionId", "Outcome" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeImportRows_TenantId",
                table: "EmployeeImportRows",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeImportSession_Tenant_Reference",
                table: "EmployeeImportSessions",
                columns: new[] { "TenantId", "Reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeImportSessions_TenantId_FileHash",
                table: "EmployeeImportSessions",
                columns: new[] { "TenantId", "FileHash" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeImportSessions_TenantId_SourceFileUploadRecordId",
                table: "EmployeeImportSessions",
                columns: new[] { "TenantId", "SourceFileUploadRecordId" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeImportSessions_TenantId_Status",
                table: "EmployeeImportSessions",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeImportSessions_UploadedOn",
                table: "EmployeeImportSessions",
                column: "UploadedOn");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmployeeImportRows");

            migrationBuilder.DropTable(
                name: "EmployeeImportSessions");

            migrationBuilder.DropIndex(
                name: "IX_Employee_Tenant_EmailAddress",
                table: "Employees");

            // ALTER COLUMN ... NOT NULL fails while NULLs exist; the defaultValue below does not
            // backfill. Put the old '' back so the unfiltered unique index can be recreated at all
            // (it will still refuse a second blank, exactly as before this migration).
            migrationBuilder.Sql("UPDATE [dbo].[Employees] SET [EmailAddress] = N'' WHERE [EmailAddress] IS NULL;");

            migrationBuilder.AlterColumn<string>(
                name: "EmailAddress",
                table: "Employees",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Employee_Tenant_EmailAddress",
                table: "Employees",
                columns: new[] { "TenantId", "EmailAddress" },
                unique: true);
        }
    }
}
