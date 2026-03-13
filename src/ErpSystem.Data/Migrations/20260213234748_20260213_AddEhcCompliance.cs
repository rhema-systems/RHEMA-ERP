using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class _20260213_AddEhcCompliance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EhcComplianceAuditExports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ToUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FileUploadRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RowCount = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_EhcComplianceAuditExports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EhcComplianceAuditExports_EhcTicketCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "EhcTicketCategories",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EhcComplianceAuditExports_EhcTickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "EhcTickets",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EhcComplianceAuditExports_FileUploadRecords_FileUploadRecordId",
                        column: x => x.FileUploadRecordId,
                        principalTable: "FileUploadRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EhcComplianceAuditExports_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EhcLegalHolds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ReleasedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReleasedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_EhcLegalHolds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EhcLegalHolds_EhcTickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "EhcTickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EhcLegalHolds_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EhcRetentionCategoryExceptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    AuditEventRetentionDays = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_EhcRetentionCategoryExceptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EhcRetentionCategoryExceptions_EhcTicketCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "EhcTicketCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EhcRetentionCategoryExceptions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EhcComplianceAuditExports_CategoryId",
                table: "EhcComplianceAuditExports",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_EhcComplianceAuditExports_FileUploadRecordId",
                table: "EhcComplianceAuditExports",
                column: "FileUploadRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_EhcComplianceAuditExports_TenantId_CreatedAt",
                table: "EhcComplianceAuditExports",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EhcComplianceAuditExports_TenantId_FromUtc_ToUtc",
                table: "EhcComplianceAuditExports",
                columns: new[] { "TenantId", "FromUtc", "ToUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_EhcComplianceAuditExports_TicketId",
                table: "EhcComplianceAuditExports",
                column: "TicketId");

            migrationBuilder.CreateIndex(
                name: "IX_EhcLegalHolds_TenantId_CreatedAt",
                table: "EhcLegalHolds",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EhcLegalHolds_TenantId_TicketId_IsActive",
                table: "EhcLegalHolds",
                columns: new[] { "TenantId", "TicketId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_EhcLegalHolds_TicketId",
                table: "EhcLegalHolds",
                column: "TicketId");

            migrationBuilder.CreateIndex(
                name: "IX_EhcRetentionCategoryExceptions_CategoryId",
                table: "EhcRetentionCategoryExceptions",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_EhcRetentionCategoryExceptions_TenantId_CategoryId",
                table: "EhcRetentionCategoryExceptions",
                columns: new[] { "TenantId", "CategoryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EhcRetentionCategoryExceptions_TenantId_IsActive",
                table: "EhcRetentionCategoryExceptions",
                columns: new[] { "TenantId", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EhcComplianceAuditExports");

            migrationBuilder.DropTable(
                name: "EhcLegalHolds");

            migrationBuilder.DropTable(
                name: "EhcRetentionCategoryExceptions");
        }
    }
}
