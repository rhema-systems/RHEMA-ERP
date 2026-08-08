using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    // Routine Debug builds intentionally omit the multi-megabyte EF designer history. Keep the
    // discovery attributes on the executable migration so startup and `database update` cannot
    // silently miss this payment-control schema change.
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260803132236_AddApPaymentEvidenceAndAuthorityControls")]
    /// <inheritdoc />
    public partial class AddApPaymentEvidenceAndAuthorityControls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // These fields retain the exact policy and human authority decisions for a direct AP
            // payment submission. They intentionally live on the canonical payment so workflow,
            // voucher, posting, reversal, and audit queries all reference one control chain.
            migrationBuilder.AddColumn<string>(
                name: "AppliedApprovalPolicyCode",
                table: "VendorPayment",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AppliedApprovalPolicySetId",
                table: "VendorPayment",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovalControlSnapshotHash",
                table: "VendorPayment",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovalControlSnapshotJson",
                table: "VendorPayment",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EvidenceExceptionApprovedAt",
                table: "VendorPayment",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EvidenceExceptionApprovedById",
                table: "VendorPayment",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EvidenceExceptionReason",
                table: "VendorPayment",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EvidenceExceptionRequested",
                table: "VendorPayment",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "EvidenceExceptionRequestedAt",
                table: "VendorPayment",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EvidenceExceptionRequestedById",
                table: "VendorPayment",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExceptionalPaymentReason",
                table: "VendorPayment",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsExceptionalPayment",
                table: "VendorPayment",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ManagingDirectorApprovedAt",
                table: "VendorPayment",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ManagingDirectorApprovedById",
                table: "VendorPayment",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresManagingDirectorApproval",
                table: "VendorPayment",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "SubmittedAt",
                table: "VendorPayment",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SubmittedById",
                table: "VendorPayment",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WorkflowInstanceId",
                table: "VendorPayment",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_VendorPayment_AppliedApprovalPolicySetId",
                table: "VendorPayment",
                column: "AppliedApprovalPolicySetId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorPayment_WorkflowInstanceId",
                table: "VendorPayment",
                column: "WorkflowInstanceId");

            // Restrict deletion of both references: retirement is supported, but deleting an
            // applied policy or workflow would destroy evidence for an already-authorized payment.
            migrationBuilder.AddForeignKey(
                name: "FK_VendorPayment_WorkflowApprovalPolicySets_AppliedApprovalPolicySetId",
                table: "VendorPayment",
                column: "AppliedApprovalPolicySetId",
                principalTable: "WorkflowApprovalPolicySets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_VendorPayment_WorkflowInstances_WorkflowInstanceId",
                table: "VendorPayment",
                column: "WorkflowInstanceId",
                principalTable: "WorkflowInstances",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_VendorPayment_WorkflowApprovalPolicySets_AppliedApprovalPolicySetId",
                table: "VendorPayment");

            migrationBuilder.DropForeignKey(
                name: "FK_VendorPayment_WorkflowInstances_WorkflowInstanceId",
                table: "VendorPayment");

            migrationBuilder.DropIndex(
                name: "IX_VendorPayment_AppliedApprovalPolicySetId",
                table: "VendorPayment");

            migrationBuilder.DropIndex(
                name: "IX_VendorPayment_WorkflowInstanceId",
                table: "VendorPayment");

            migrationBuilder.DropColumn(
                name: "AppliedApprovalPolicyCode",
                table: "VendorPayment");

            migrationBuilder.DropColumn(
                name: "AppliedApprovalPolicySetId",
                table: "VendorPayment");

            migrationBuilder.DropColumn(
                name: "ApprovalControlSnapshotHash",
                table: "VendorPayment");

            migrationBuilder.DropColumn(
                name: "ApprovalControlSnapshotJson",
                table: "VendorPayment");

            migrationBuilder.DropColumn(
                name: "EvidenceExceptionApprovedAt",
                table: "VendorPayment");

            migrationBuilder.DropColumn(
                name: "EvidenceExceptionApprovedById",
                table: "VendorPayment");

            migrationBuilder.DropColumn(
                name: "EvidenceExceptionReason",
                table: "VendorPayment");

            migrationBuilder.DropColumn(
                name: "EvidenceExceptionRequested",
                table: "VendorPayment");

            migrationBuilder.DropColumn(
                name: "EvidenceExceptionRequestedAt",
                table: "VendorPayment");

            migrationBuilder.DropColumn(
                name: "EvidenceExceptionRequestedById",
                table: "VendorPayment");

            migrationBuilder.DropColumn(
                name: "ExceptionalPaymentReason",
                table: "VendorPayment");

            migrationBuilder.DropColumn(
                name: "IsExceptionalPayment",
                table: "VendorPayment");

            migrationBuilder.DropColumn(
                name: "ManagingDirectorApprovedAt",
                table: "VendorPayment");

            migrationBuilder.DropColumn(
                name: "ManagingDirectorApprovedById",
                table: "VendorPayment");

            migrationBuilder.DropColumn(
                name: "RequiresManagingDirectorApproval",
                table: "VendorPayment");

            migrationBuilder.DropColumn(
                name: "SubmittedAt",
                table: "VendorPayment");

            migrationBuilder.DropColumn(
                name: "SubmittedById",
                table: "VendorPayment");

            migrationBuilder.DropColumn(
                name: "WorkflowInstanceId",
                table: "VendorPayment");
        }
    }
}
