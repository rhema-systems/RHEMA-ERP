using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchaseRequisitionPolicySelection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ProcurementPolicySetId",
                table: "PurchaseRequisitions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequisitions_ProcurementPolicySetId",
                table: "PurchaseRequisitions",
                column: "ProcurementPolicySetId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequisitions_TenantId_ProcurementPolicySetId",
                table: "PurchaseRequisitions",
                columns: new[] { "TenantId", "ProcurementPolicySetId" });

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseRequisitions_ProcurementPolicySets_ProcurementPolicySetId",
                table: "PurchaseRequisitions",
                column: "ProcurementPolicySetId",
                principalTable: "ProcurementPolicySets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseRequisitions_ProcurementPolicySets_ProcurementPolicySetId",
                table: "PurchaseRequisitions");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseRequisitions_ProcurementPolicySetId",
                table: "PurchaseRequisitions");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseRequisitions_TenantId_ProcurementPolicySetId",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "ProcurementPolicySetId",
                table: "PurchaseRequisitions");
        }
    }
}
