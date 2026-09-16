using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectSalesMaintenanceLinkage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SalesAgreementId",
                table: "ProjectUnits",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SalesOrderId",
                table: "ProjectUnits",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "JobCardId",
                table: "ProjectDefectLiabilityCases",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WorkOrderId",
                table: "ProjectDefectLiabilityCases",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "JobCardId",
                table: "ProjectCustomerVariations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SalesAgreementId",
                table: "ProjectCustomerVariations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SalesOrderId",
                table: "ProjectCustomerVariations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WorkOrderId",
                table: "ProjectCustomerVariations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectUnits_ProjectId_SalesAgreementId",
                table: "ProjectUnits",
                columns: new[] { "ProjectId", "SalesAgreementId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectUnits_ProjectId_SalesOrderId",
                table: "ProjectUnits",
                columns: new[] { "ProjectId", "SalesOrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectUnits_SalesAgreementId",
                table: "ProjectUnits",
                column: "SalesAgreementId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectUnits_SalesOrderId",
                table: "ProjectUnits",
                column: "SalesOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectDefectLiabilityCases_JobCardId",
                table: "ProjectDefectLiabilityCases",
                column: "JobCardId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectDefectLiabilityCases_ProjectId_JobCardId",
                table: "ProjectDefectLiabilityCases",
                columns: new[] { "ProjectId", "JobCardId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectDefectLiabilityCases_ProjectId_WorkOrderId",
                table: "ProjectDefectLiabilityCases",
                columns: new[] { "ProjectId", "WorkOrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectDefectLiabilityCases_WorkOrderId",
                table: "ProjectDefectLiabilityCases",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCustomerVariations_JobCardId",
                table: "ProjectCustomerVariations",
                column: "JobCardId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCustomerVariations_ProjectId_JobCardId",
                table: "ProjectCustomerVariations",
                columns: new[] { "ProjectId", "JobCardId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCustomerVariations_ProjectId_SalesAgreementId",
                table: "ProjectCustomerVariations",
                columns: new[] { "ProjectId", "SalesAgreementId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCustomerVariations_ProjectId_SalesOrderId",
                table: "ProjectCustomerVariations",
                columns: new[] { "ProjectId", "SalesOrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCustomerVariations_ProjectId_WorkOrderId",
                table: "ProjectCustomerVariations",
                columns: new[] { "ProjectId", "WorkOrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCustomerVariations_SalesAgreementId",
                table: "ProjectCustomerVariations",
                column: "SalesAgreementId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCustomerVariations_SalesOrderId",
                table: "ProjectCustomerVariations",
                column: "SalesOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCustomerVariations_WorkOrderId",
                table: "ProjectCustomerVariations",
                column: "WorkOrderId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectCustomerVariations_JobCard_JobCardId",
                table: "ProjectCustomerVariations",
                column: "JobCardId",
                principalTable: "JobCard",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectCustomerVariations_SalesAgreements_SalesAgreementId",
                table: "ProjectCustomerVariations",
                column: "SalesAgreementId",
                principalTable: "SalesAgreements",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectCustomerVariations_SalesOrders_SalesOrderId",
                table: "ProjectCustomerVariations",
                column: "SalesOrderId",
                principalTable: "SalesOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectCustomerVariations_WorkOrders_WorkOrderId",
                table: "ProjectCustomerVariations",
                column: "WorkOrderId",
                principalTable: "WorkOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectDefectLiabilityCases_JobCard_JobCardId",
                table: "ProjectDefectLiabilityCases",
                column: "JobCardId",
                principalTable: "JobCard",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectDefectLiabilityCases_WorkOrders_WorkOrderId",
                table: "ProjectDefectLiabilityCases",
                column: "WorkOrderId",
                principalTable: "WorkOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectUnits_SalesAgreements_SalesAgreementId",
                table: "ProjectUnits",
                column: "SalesAgreementId",
                principalTable: "SalesAgreements",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectUnits_SalesOrders_SalesOrderId",
                table: "ProjectUnits",
                column: "SalesOrderId",
                principalTable: "SalesOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProjectCustomerVariations_JobCard_JobCardId",
                table: "ProjectCustomerVariations");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectCustomerVariations_SalesAgreements_SalesAgreementId",
                table: "ProjectCustomerVariations");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectCustomerVariations_SalesOrders_SalesOrderId",
                table: "ProjectCustomerVariations");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectCustomerVariations_WorkOrders_WorkOrderId",
                table: "ProjectCustomerVariations");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectDefectLiabilityCases_JobCard_JobCardId",
                table: "ProjectDefectLiabilityCases");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectDefectLiabilityCases_WorkOrders_WorkOrderId",
                table: "ProjectDefectLiabilityCases");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectUnits_SalesAgreements_SalesAgreementId",
                table: "ProjectUnits");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectUnits_SalesOrders_SalesOrderId",
                table: "ProjectUnits");

            migrationBuilder.DropIndex(
                name: "IX_ProjectUnits_ProjectId_SalesAgreementId",
                table: "ProjectUnits");

            migrationBuilder.DropIndex(
                name: "IX_ProjectUnits_ProjectId_SalesOrderId",
                table: "ProjectUnits");

            migrationBuilder.DropIndex(
                name: "IX_ProjectUnits_SalesAgreementId",
                table: "ProjectUnits");

            migrationBuilder.DropIndex(
                name: "IX_ProjectUnits_SalesOrderId",
                table: "ProjectUnits");

            migrationBuilder.DropIndex(
                name: "IX_ProjectDefectLiabilityCases_JobCardId",
                table: "ProjectDefectLiabilityCases");

            migrationBuilder.DropIndex(
                name: "IX_ProjectDefectLiabilityCases_ProjectId_JobCardId",
                table: "ProjectDefectLiabilityCases");

            migrationBuilder.DropIndex(
                name: "IX_ProjectDefectLiabilityCases_ProjectId_WorkOrderId",
                table: "ProjectDefectLiabilityCases");

            migrationBuilder.DropIndex(
                name: "IX_ProjectDefectLiabilityCases_WorkOrderId",
                table: "ProjectDefectLiabilityCases");

            migrationBuilder.DropIndex(
                name: "IX_ProjectCustomerVariations_JobCardId",
                table: "ProjectCustomerVariations");

            migrationBuilder.DropIndex(
                name: "IX_ProjectCustomerVariations_ProjectId_JobCardId",
                table: "ProjectCustomerVariations");

            migrationBuilder.DropIndex(
                name: "IX_ProjectCustomerVariations_ProjectId_SalesAgreementId",
                table: "ProjectCustomerVariations");

            migrationBuilder.DropIndex(
                name: "IX_ProjectCustomerVariations_ProjectId_SalesOrderId",
                table: "ProjectCustomerVariations");

            migrationBuilder.DropIndex(
                name: "IX_ProjectCustomerVariations_ProjectId_WorkOrderId",
                table: "ProjectCustomerVariations");

            migrationBuilder.DropIndex(
                name: "IX_ProjectCustomerVariations_SalesAgreementId",
                table: "ProjectCustomerVariations");

            migrationBuilder.DropIndex(
                name: "IX_ProjectCustomerVariations_SalesOrderId",
                table: "ProjectCustomerVariations");

            migrationBuilder.DropIndex(
                name: "IX_ProjectCustomerVariations_WorkOrderId",
                table: "ProjectCustomerVariations");

            migrationBuilder.DropColumn(
                name: "SalesAgreementId",
                table: "ProjectUnits");

            migrationBuilder.DropColumn(
                name: "SalesOrderId",
                table: "ProjectUnits");

            migrationBuilder.DropColumn(
                name: "JobCardId",
                table: "ProjectDefectLiabilityCases");

            migrationBuilder.DropColumn(
                name: "WorkOrderId",
                table: "ProjectDefectLiabilityCases");

            migrationBuilder.DropColumn(
                name: "JobCardId",
                table: "ProjectCustomerVariations");

            migrationBuilder.DropColumn(
                name: "SalesAgreementId",
                table: "ProjectCustomerVariations");

            migrationBuilder.DropColumn(
                name: "SalesOrderId",
                table: "ProjectCustomerVariations");

            migrationBuilder.DropColumn(
                name: "WorkOrderId",
                table: "ProjectCustomerVariations");
        }
    }
}
