using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    // Debug builds omit generated migration designers to keep the very large solution responsive.
    // Discovery metadata therefore also lives on the executable migration class so startup and
    // developer EF commands cannot silently treat this control migration as nonexistent.
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260803075605_AddFinanceCloseEvidenceAndWaivers")]
    /// <inheritdoc />
    public partial class AddFinanceCloseEvidenceAndWaivers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AppliedWaiverId",
                table: "FinanceCloseCheckSnapshots",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EvidenceFingerprint",
                table: "FinanceCloseCheckSnapshots",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FinanceCloseEvidenceAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FinanceCloseCycleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FinanceCloseTaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileUploadRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EvidenceType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_FinanceCloseEvidenceAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FinanceCloseEvidenceAttachments_FileUploadRecords_FileUploadRecordId",
                        column: x => x.FileUploadRecordId,
                        principalTable: "FileUploadRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceCloseEvidenceAttachments_FinanceCloseCycles_FinanceCloseCycleId",
                        column: x => x.FinanceCloseCycleId,
                        principalTable: "FinanceCloseCycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceCloseEvidenceAttachments_FinanceCloseTasks_FinanceCloseTaskId",
                        column: x => x.FinanceCloseTaskId,
                        principalTable: "FinanceCloseTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FinanceCloseEvidenceAttachments_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FinanceCloseExceptionWaivers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FinanceCloseCycleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FinanceCloseTaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FinanceCloseCheckSnapshotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FinanceCloseEvidenceAttachmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CheckCode = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    EvidenceFingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Justification = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedByUserName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedByUserName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewComment = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_FinanceCloseExceptionWaivers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FinanceCloseExceptionWaivers_FinanceCloseCheckSnapshots_FinanceCloseCheckSnapshotId",
                        column: x => x.FinanceCloseCheckSnapshotId,
                        principalTable: "FinanceCloseCheckSnapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceCloseExceptionWaivers_FinanceCloseCycles_FinanceCloseCycleId",
                        column: x => x.FinanceCloseCycleId,
                        principalTable: "FinanceCloseCycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceCloseExceptionWaivers_FinanceCloseEvidenceAttachments_FinanceCloseEvidenceAttachmentId",
                        column: x => x.FinanceCloseEvidenceAttachmentId,
                        principalTable: "FinanceCloseEvidenceAttachments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceCloseExceptionWaivers_FinanceCloseTasks_FinanceCloseTaskId",
                        column: x => x.FinanceCloseTaskId,
                        principalTable: "FinanceCloseTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceCloseExceptionWaivers_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceCloseEvidenceAttachments_FileUploadRecordId",
                table: "FinanceCloseEvidenceAttachments",
                column: "FileUploadRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceCloseEvidenceAttachments_FinanceCloseCycleId",
                table: "FinanceCloseEvidenceAttachments",
                column: "FinanceCloseCycleId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceCloseEvidenceAttachments_FinanceCloseTaskId",
                table: "FinanceCloseEvidenceAttachments",
                column: "FinanceCloseTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceCloseEvidenceAttachments_TenantId_FinanceCloseCycleId_CreatedAt",
                table: "FinanceCloseEvidenceAttachments",
                columns: new[] { "TenantId", "FinanceCloseCycleId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceCloseEvidenceAttachments_TenantId_FinanceCloseTaskId_FileUploadRecordId",
                table: "FinanceCloseEvidenceAttachments",
                columns: new[] { "TenantId", "FinanceCloseTaskId", "FileUploadRecordId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceCloseExceptionWaivers_FinanceCloseCheckSnapshotId",
                table: "FinanceCloseExceptionWaivers",
                column: "FinanceCloseCheckSnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceCloseExceptionWaivers_FinanceCloseCycleId",
                table: "FinanceCloseExceptionWaivers",
                column: "FinanceCloseCycleId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceCloseExceptionWaivers_FinanceCloseEvidenceAttachmentId",
                table: "FinanceCloseExceptionWaivers",
                column: "FinanceCloseEvidenceAttachmentId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceCloseExceptionWaivers_FinanceCloseTaskId",
                table: "FinanceCloseExceptionWaivers",
                column: "FinanceCloseTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceCloseExceptionWaivers_TenantId_FinanceCloseCheckSnapshotId",
                table: "FinanceCloseExceptionWaivers",
                columns: new[] { "TenantId", "FinanceCloseCheckSnapshotId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceCloseExceptionWaivers_TenantId_FinanceCloseCycleId_CheckCode_Status",
                table: "FinanceCloseExceptionWaivers",
                columns: new[] { "TenantId", "FinanceCloseCycleId", "CheckCode", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FinanceCloseExceptionWaivers");

            migrationBuilder.DropTable(
                name: "FinanceCloseEvidenceAttachments");

            migrationBuilder.DropColumn(
                name: "AppliedWaiverId",
                table: "FinanceCloseCheckSnapshots");

            migrationBuilder.DropColumn(
                name: "EvidenceFingerprint",
                table: "FinanceCloseCheckSnapshots");
        }
    }
}
