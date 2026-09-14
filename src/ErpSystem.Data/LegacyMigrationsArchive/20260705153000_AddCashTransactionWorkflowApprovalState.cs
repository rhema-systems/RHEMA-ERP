using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    [Microsoft.EntityFrameworkCore.Infrastructure.DbContext(typeof(ApplicationDbContext))]
    [Migration("20260705153000_AddCashTransactionWorkflowApprovalState")]
    public partial class AddCashTransactionWorkflowApprovalState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ApprovalStatus",
                table: "CashTransaction",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "ApprovalComments",
                table: "CashTransaction",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                table: "CashTransaction",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ApprovedById",
                table: "CashTransaction",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAt",
                table: "CashTransaction",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CancelledById",
                table: "CashTransaction",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                table: "CashTransaction",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RejectedAt",
                table: "CashTransaction",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RejectedById",
                table: "CashTransaction",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "CashTransaction",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SubmittedAt",
                table: "CashTransaction",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SubmittedById",
                table: "CashTransaction",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WorkflowInstanceId",
                table: "CashTransaction",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE [CashTransaction] SET [ApprovalStatus] = CASE WHEN [IsPosted] = 1 THEN 7 ELSE 1 END");

            migrationBuilder.CreateIndex(
                name: "IX_CashTransaction_TenantId_ApprovalStatus",
                table: "CashTransaction",
                columns: new[] { "TenantId", "ApprovalStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_CashTransaction_TenantId_WorkflowInstanceId",
                table: "CashTransaction",
                columns: new[] { "TenantId", "WorkflowInstanceId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CashTransaction_TenantId_ApprovalStatus",
                table: "CashTransaction");

            migrationBuilder.DropIndex(
                name: "IX_CashTransaction_TenantId_WorkflowInstanceId",
                table: "CashTransaction");

            migrationBuilder.DropColumn(
                name: "ApprovalStatus",
                table: "CashTransaction");

            migrationBuilder.DropColumn(
                name: "ApprovalComments",
                table: "CashTransaction");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "CashTransaction");

            migrationBuilder.DropColumn(
                name: "ApprovedById",
                table: "CashTransaction");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "CashTransaction");

            migrationBuilder.DropColumn(
                name: "CancelledById",
                table: "CashTransaction");

            migrationBuilder.DropColumn(
                name: "CancellationReason",
                table: "CashTransaction");

            migrationBuilder.DropColumn(
                name: "RejectedAt",
                table: "CashTransaction");

            migrationBuilder.DropColumn(
                name: "RejectedById",
                table: "CashTransaction");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "CashTransaction");

            migrationBuilder.DropColumn(
                name: "SubmittedAt",
                table: "CashTransaction");

            migrationBuilder.DropColumn(
                name: "SubmittedById",
                table: "CashTransaction");

            migrationBuilder.DropColumn(
                name: "WorkflowInstanceId",
                table: "CashTransaction");
        }
    }
}
