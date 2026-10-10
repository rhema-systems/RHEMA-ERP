using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMobilePosTillCloseControl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MobilePosTillCloseSubmissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CashierTillSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MobilePosStoreId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MobilePosTillId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MobilePosDeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MobilePosOfflinePolicyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubmittedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmittedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PolicyAllowedPendingSync = table.Column<bool>(type: "bit", nullable: false),
                    PendingMutationCount = table.Column<int>(type: "int", nullable: false),
                    PendingMutationIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PendingMutationDigest = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SyncExceptionResolvedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SyncExceptionResolvedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SyncExceptionResolutionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    FinalizedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FinalizedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_MobilePosTillCloseSubmissions", x => x.Id);
                    table.CheckConstraint("CK_MobilePosTillCloseSubmissions_Digest", "LEN([PendingMutationDigest]) = 64");
                    table.CheckConstraint("CK_MobilePosTillCloseSubmissions_PendingCount", "[PendingMutationCount] >= 0");
                    table.ForeignKey(
                        name: "FK_MobilePosTillCloseSubmissions_CashierTillSessions_CashierTillSessionId",
                        column: x => x.CashierTillSessionId,
                        principalTable: "CashierTillSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MobilePosTillCloseSubmissions_MobilePosDevices_MobilePosDeviceId",
                        column: x => x.MobilePosDeviceId,
                        principalTable: "MobilePosDevices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MobilePosTillCloseSubmissions_MobilePosOfflinePolicies_MobilePosOfflinePolicyId",
                        column: x => x.MobilePosOfflinePolicyId,
                        principalTable: "MobilePosOfflinePolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MobilePosTillCloseSubmissions_MobilePosStores_MobilePosStoreId",
                        column: x => x.MobilePosStoreId,
                        principalTable: "MobilePosStores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MobilePosTillCloseSubmissions_MobilePosTills_MobilePosTillId",
                        column: x => x.MobilePosTillId,
                        principalTable: "MobilePosTills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MobilePosTillCloseSubmissions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosTillCloseSubmissions_CashierTillSessionId",
                table: "MobilePosTillCloseSubmissions",
                column: "CashierTillSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosTillCloseSubmissions_MobilePosDeviceId",
                table: "MobilePosTillCloseSubmissions",
                column: "MobilePosDeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosTillCloseSubmissions_MobilePosOfflinePolicyId",
                table: "MobilePosTillCloseSubmissions",
                column: "MobilePosOfflinePolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosTillCloseSubmissions_MobilePosStoreId",
                table: "MobilePosTillCloseSubmissions",
                column: "MobilePosStoreId");

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosTillCloseSubmissions_MobilePosTillId",
                table: "MobilePosTillCloseSubmissions",
                column: "MobilePosTillId");

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosTillCloseSubmissions_TenantId_CashierTillSessionId",
                table: "MobilePosTillCloseSubmissions",
                columns: new[] { "TenantId", "CashierTillSessionId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosTillCloseSubmissions_TenantId_Status_SubmittedAtUtc",
                table: "MobilePosTillCloseSubmissions",
                columns: new[] { "TenantId", "Status", "SubmittedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MobilePosTillCloseSubmissions");
        }
    }
}
