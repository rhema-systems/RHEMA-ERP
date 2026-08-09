using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Extends the shared collection-activity aggregate with Finance AR task,
    /// reminder, completion and concurrency evidence for FR-AR-009.
    /// </summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260808232712_AddArCollectionFollowUpWorkspace")]
    public partial class AddArCollectionFollowUpWorkspace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CollectionActivities_TenantId",
                table: "CollectionActivities");

            // Existing CollectionActivity rows belong to the legacy Sales surface.
            // Classifying them during the schema change prevents Finance queries
            // from treating historical Sales contacts as controlled AR work items.
            migrationBuilder.AddColumn<string>(
                name: "CollectionContext",
                table: "CollectionActivities",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Sales");

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                table: "CollectionActivities",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CompletedById",
                table: "CollectionActivities",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPrimaryTask",
                table: "CollectionActivities",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ParentActivityId",
                table: "CollectionActivities",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReminderChannel",
                table: "CollectionActivities",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReminderRecipient",
                table: "CollectionActivities",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "CollectionActivities",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.CreateIndex(
                name: "IX_CollectionActivities_CompletedById",
                table: "CollectionActivities",
                column: "CompletedById");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionActivities_ParentActivityId",
                table: "CollectionActivities",
                column: "ParentActivityId");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionActivities_TenantId_CollectionContext_CollectionStatus_FollowUpDate",
                table: "CollectionActivities",
                columns: new[] { "TenantId", "CollectionContext", "CollectionStatus", "FollowUpDate" });

            migrationBuilder.CreateIndex(
                name: "IX_CollectionActivities_TenantId_InvoiceId_CollectionContext",
                table: "CollectionActivities",
                columns: new[] { "TenantId", "InvoiceId", "CollectionContext" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [IsPrimaryTask] = 1 AND [InvoiceId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_CollectionActivities_CollectionActivities_ParentActivityId",
                table: "CollectionActivities",
                column: "ParentActivityId",
                principalTable: "CollectionActivities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CollectionActivities_Users_CompletedById",
                table: "CollectionActivities",
                column: "CompletedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CollectionActivities_CollectionActivities_ParentActivityId",
                table: "CollectionActivities");

            migrationBuilder.DropForeignKey(
                name: "FK_CollectionActivities_Users_CompletedById",
                table: "CollectionActivities");

            migrationBuilder.DropIndex(
                name: "IX_CollectionActivities_CompletedById",
                table: "CollectionActivities");

            migrationBuilder.DropIndex(
                name: "IX_CollectionActivities_ParentActivityId",
                table: "CollectionActivities");

            migrationBuilder.DropIndex(
                name: "IX_CollectionActivities_TenantId_CollectionContext_CollectionStatus_FollowUpDate",
                table: "CollectionActivities");

            migrationBuilder.DropIndex(
                name: "IX_CollectionActivities_TenantId_InvoiceId_CollectionContext",
                table: "CollectionActivities");

            migrationBuilder.DropColumn(
                name: "CollectionContext",
                table: "CollectionActivities");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "CollectionActivities");

            migrationBuilder.DropColumn(
                name: "CompletedById",
                table: "CollectionActivities");

            migrationBuilder.DropColumn(
                name: "IsPrimaryTask",
                table: "CollectionActivities");

            migrationBuilder.DropColumn(
                name: "ParentActivityId",
                table: "CollectionActivities");

            migrationBuilder.DropColumn(
                name: "ReminderChannel",
                table: "CollectionActivities");

            migrationBuilder.DropColumn(
                name: "ReminderRecipient",
                table: "CollectionActivities");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "CollectionActivities");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionActivities_TenantId",
                table: "CollectionActivities",
                column: "TenantId");
        }
    }
}
