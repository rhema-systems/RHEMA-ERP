using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierEvidenceDmsLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CentralDocumentRecordId",
                table: "BusinessPartnerRegistrationDocuments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CentralDocumentVersionId",
                table: "BusinessPartnerRegistrationDocuments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartnerRegistrationDocuments_CentralDocumentRecordId",
                table: "BusinessPartnerRegistrationDocuments",
                column: "CentralDocumentRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartnerRegistrationDocuments_CentralDocumentVersionId",
                table: "BusinessPartnerRegistrationDocuments",
                column: "CentralDocumentVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartnerRegistrationDocuments_TenantId_CentralDocumentRecordId",
                table: "BusinessPartnerRegistrationDocuments",
                columns: new[] { "TenantId", "CentralDocumentRecordId" });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartnerRegistrationDocuments_TenantId_CentralDocumentVersionId",
                table: "BusinessPartnerRegistrationDocuments",
                columns: new[] { "TenantId", "CentralDocumentVersionId" });

            migrationBuilder.AddForeignKey(
                name: "FK_BusinessPartnerRegistrationDocuments_CentralDocumentRecords_CentralDocumentRecordId",
                table: "BusinessPartnerRegistrationDocuments",
                column: "CentralDocumentRecordId",
                principalTable: "CentralDocumentRecords",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BusinessPartnerRegistrationDocuments_CentralDocumentVersions_CentralDocumentVersionId",
                table: "BusinessPartnerRegistrationDocuments",
                column: "CentralDocumentVersionId",
                principalTable: "CentralDocumentVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BusinessPartnerRegistrationDocuments_CentralDocumentRecords_CentralDocumentRecordId",
                table: "BusinessPartnerRegistrationDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_BusinessPartnerRegistrationDocuments_CentralDocumentVersions_CentralDocumentVersionId",
                table: "BusinessPartnerRegistrationDocuments");

            migrationBuilder.DropIndex(
                name: "IX_BusinessPartnerRegistrationDocuments_CentralDocumentRecordId",
                table: "BusinessPartnerRegistrationDocuments");

            migrationBuilder.DropIndex(
                name: "IX_BusinessPartnerRegistrationDocuments_CentralDocumentVersionId",
                table: "BusinessPartnerRegistrationDocuments");

            migrationBuilder.DropIndex(
                name: "IX_BusinessPartnerRegistrationDocuments_TenantId_CentralDocumentRecordId",
                table: "BusinessPartnerRegistrationDocuments");

            migrationBuilder.DropIndex(
                name: "IX_BusinessPartnerRegistrationDocuments_TenantId_CentralDocumentVersionId",
                table: "BusinessPartnerRegistrationDocuments");

            migrationBuilder.DropColumn(
                name: "CentralDocumentRecordId",
                table: "BusinessPartnerRegistrationDocuments");

            migrationBuilder.DropColumn(
                name: "CentralDocumentVersionId",
                table: "BusinessPartnerRegistrationDocuments");
        }
    }
}
