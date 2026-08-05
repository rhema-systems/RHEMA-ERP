using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    // Keep discovery metadata on the executable migration because the normal Debug build omits
    // very large generated designers. Without these attributes the application could start with
    // the alert table absent while incorrectly reporting that every migration was applied.
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260803084838_AddFinanceCloseAgingEscalations")]
    /// <inheritdoc />
    public partial class AddFinanceCloseAgingEscalations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FinanceCloseAlertDeliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FinanceCloseCycleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FinanceCloseTaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FinanceCloseExceptionWaiverId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AlertType = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    DedupeKey = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    RecipientUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecipientUserName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    DueAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastAttemptAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeliveredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    NotificationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_FinanceCloseAlertDeliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FinanceCloseAlertDeliveries_FinanceCloseCycles_FinanceCloseCycleId",
                        column: x => x.FinanceCloseCycleId,
                        principalTable: "FinanceCloseCycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FinanceCloseAlertDeliveries_FinanceCloseExceptionWaivers_FinanceCloseExceptionWaiverId",
                        column: x => x.FinanceCloseExceptionWaiverId,
                        principalTable: "FinanceCloseExceptionWaivers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceCloseAlertDeliveries_FinanceCloseTasks_FinanceCloseTaskId",
                        column: x => x.FinanceCloseTaskId,
                        principalTable: "FinanceCloseTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceCloseAlertDeliveries_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceCloseAlertDeliveries_FinanceCloseCycleId",
                table: "FinanceCloseAlertDeliveries",
                column: "FinanceCloseCycleId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceCloseAlertDeliveries_FinanceCloseExceptionWaiverId",
                table: "FinanceCloseAlertDeliveries",
                column: "FinanceCloseExceptionWaiverId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceCloseAlertDeliveries_FinanceCloseTaskId",
                table: "FinanceCloseAlertDeliveries",
                column: "FinanceCloseTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceCloseAlertDeliveries_Status_LastAttemptAtUtc",
                table: "FinanceCloseAlertDeliveries",
                columns: new[] { "Status", "LastAttemptAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceCloseAlertDeliveries_TenantId_DedupeKey",
                table: "FinanceCloseAlertDeliveries",
                columns: new[] { "TenantId", "DedupeKey" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceCloseAlertDeliveries_TenantId_FinanceCloseCycleId_CreatedAt",
                table: "FinanceCloseAlertDeliveries",
                columns: new[] { "TenantId", "FinanceCloseCycleId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FinanceCloseAlertDeliveries");
        }
    }
}
