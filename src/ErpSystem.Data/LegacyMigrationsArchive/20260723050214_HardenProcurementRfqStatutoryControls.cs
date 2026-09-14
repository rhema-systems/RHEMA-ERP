using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class HardenProcurementRfqStatutoryControls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProcurementRfqEvaluationLines_BusinessPartners_BusinessPartnerId",
                table: "ProcurementRfqEvaluationLines");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcurementRfqEvaluationLines_ProcurementRfqEvaluations_EvaluationId",
                table: "ProcurementRfqEvaluationLines");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcurementRfqEvaluationLines_RequestForQuotationItems_RfqItemId",
                table: "ProcurementRfqEvaluationLines");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcurementRfqEvaluationLines_RequestForQuotationQuotes_QuoteId",
                table: "ProcurementRfqEvaluationLines");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcurementRfqEvaluations_ProcurementPolicyMethodRules_MethodRuleId",
                table: "ProcurementRfqEvaluations");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcurementRfqEvaluations_ProcurementRfqOpeningRegisters_OpeningRegisterId",
                table: "ProcurementRfqEvaluations");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcurementRfqEvaluations_RequestForQuotations_RfqId",
                table: "ProcurementRfqEvaluations");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcurementRfqEvaluations_WorkflowInstances_WorkflowInstanceId",
                table: "ProcurementRfqEvaluations");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcurementRfqOpeningEntries_BusinessPartners_BusinessPartnerId",
                table: "ProcurementRfqOpeningEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcurementRfqOpeningEntries_ProcurementRfqOpeningRegisters_OpeningRegisterId",
                table: "ProcurementRfqOpeningEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcurementRfqOpeningEntries_ProcurementRfqReceipts_ReceiptId",
                table: "ProcurementRfqOpeningEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcurementRfqOpeningEntries_RequestForQuotationQuotes_QuoteId",
                table: "ProcurementRfqOpeningEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcurementRfqOpeningParticipants_ProcurementRfqOpeningRegisters_OpeningRegisterId",
                table: "ProcurementRfqOpeningParticipants");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcurementRfqOpeningRegisters_RequestForQuotations_RfqId",
                table: "ProcurementRfqOpeningRegisters");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcurementRfqReceipts_BusinessPartners_BusinessPartnerId",
                table: "ProcurementRfqReceipts");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcurementRfqReceipts_ProcurementRfqOpeningRegisters_OpeningRegisterId",
                table: "ProcurementRfqReceipts");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcurementRfqReceipts_RequestForQuotationQuotes_QuoteId",
                table: "ProcurementRfqReceipts");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcurementRfqReceipts_RequestForQuotations_RfqId",
                table: "ProcurementRfqReceipts");

            migrationBuilder.DropIndex(
                name: "IX_ProcurementRfqReceipts_TenantId",
                table: "ProcurementRfqReceipts");

            migrationBuilder.DropIndex(
                name: "IX_ProcurementRfqOpeningRegisters_TenantId",
                table: "ProcurementRfqOpeningRegisters");

            migrationBuilder.DropIndex(
                name: "IX_ProcurementRfqOpeningParticipants_TenantId",
                table: "ProcurementRfqOpeningParticipants");

            migrationBuilder.DropIndex(
                name: "IX_ProcurementRfqOpeningEntries_TenantId",
                table: "ProcurementRfqOpeningEntries");

            migrationBuilder.DropIndex(
                name: "IX_ProcurementRfqEvaluations_TenantId",
                table: "ProcurementRfqEvaluations");

            migrationBuilder.DropIndex(
                name: "IX_ProcurementRfqEvaluationLines_TenantId",
                table: "ProcurementRfqEvaluationLines");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementRfqReceipts_TenantId_ReceiptNumber",
                table: "ProcurementRfqReceipts",
                columns: new[] { "TenantId", "ReceiptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementRfqReceipts_TenantId_RfqId_QuoteId",
                table: "ProcurementRfqReceipts",
                columns: new[] { "TenantId", "RfqId", "QuoteId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementRfqReceipts_TenantId_RfqId_ReceiptSequence",
                table: "ProcurementRfqReceipts",
                columns: new[] { "TenantId", "RfqId", "ReceiptSequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementRfqOpeningRegisters_TenantId_RfqId",
                table: "ProcurementRfqOpeningRegisters",
                columns: new[] { "TenantId", "RfqId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementRfqOpeningParticipants_TenantId_OpeningRegisterId_ParticipantName_RoleName",
                table: "ProcurementRfqOpeningParticipants",
                columns: new[] { "TenantId", "OpeningRegisterId", "ParticipantName", "RoleName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementRfqOpeningEntries_TenantId_OpeningRegisterId_ReceiptId",
                table: "ProcurementRfqOpeningEntries",
                columns: new[] { "TenantId", "OpeningRegisterId", "ReceiptId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementRfqEvaluations_TenantId_RfqId",
                table: "ProcurementRfqEvaluations",
                columns: new[] { "TenantId", "RfqId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementRfqEvaluationLines_TenantId_EvaluationId_RfqItemId",
                table: "ProcurementRfqEvaluationLines",
                columns: new[] { "TenantId", "EvaluationId", "RfqItemId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ProcurementRfqEvaluationLines_BusinessPartners_BusinessPartnerId",
                table: "ProcurementRfqEvaluationLines",
                column: "BusinessPartnerId",
                principalTable: "BusinessPartners",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProcurementRfqEvaluationLines_ProcurementRfqEvaluations_EvaluationId",
                table: "ProcurementRfqEvaluationLines",
                column: "EvaluationId",
                principalTable: "ProcurementRfqEvaluations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProcurementRfqEvaluationLines_RequestForQuotationItems_RfqItemId",
                table: "ProcurementRfqEvaluationLines",
                column: "RfqItemId",
                principalTable: "RequestForQuotationItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProcurementRfqEvaluationLines_RequestForQuotationQuotes_QuoteId",
                table: "ProcurementRfqEvaluationLines",
                column: "QuoteId",
                principalTable: "RequestForQuotationQuotes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProcurementRfqEvaluations_ProcurementPolicyMethodRules_MethodRuleId",
                table: "ProcurementRfqEvaluations",
                column: "MethodRuleId",
                principalTable: "ProcurementPolicyMethodRules",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProcurementRfqEvaluations_ProcurementRfqOpeningRegisters_OpeningRegisterId",
                table: "ProcurementRfqEvaluations",
                column: "OpeningRegisterId",
                principalTable: "ProcurementRfqOpeningRegisters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProcurementRfqEvaluations_RequestForQuotations_RfqId",
                table: "ProcurementRfqEvaluations",
                column: "RfqId",
                principalTable: "RequestForQuotations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProcurementRfqEvaluations_WorkflowInstances_WorkflowInstanceId",
                table: "ProcurementRfqEvaluations",
                column: "WorkflowInstanceId",
                principalTable: "WorkflowInstances",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProcurementRfqOpeningEntries_BusinessPartners_BusinessPartnerId",
                table: "ProcurementRfqOpeningEntries",
                column: "BusinessPartnerId",
                principalTable: "BusinessPartners",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProcurementRfqOpeningEntries_ProcurementRfqOpeningRegisters_OpeningRegisterId",
                table: "ProcurementRfqOpeningEntries",
                column: "OpeningRegisterId",
                principalTable: "ProcurementRfqOpeningRegisters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProcurementRfqOpeningEntries_ProcurementRfqReceipts_ReceiptId",
                table: "ProcurementRfqOpeningEntries",
                column: "ReceiptId",
                principalTable: "ProcurementRfqReceipts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProcurementRfqOpeningEntries_RequestForQuotationQuotes_QuoteId",
                table: "ProcurementRfqOpeningEntries",
                column: "QuoteId",
                principalTable: "RequestForQuotationQuotes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProcurementRfqOpeningParticipants_ProcurementRfqOpeningRegisters_OpeningRegisterId",
                table: "ProcurementRfqOpeningParticipants",
                column: "OpeningRegisterId",
                principalTable: "ProcurementRfqOpeningRegisters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProcurementRfqOpeningRegisters_RequestForQuotations_RfqId",
                table: "ProcurementRfqOpeningRegisters",
                column: "RfqId",
                principalTable: "RequestForQuotations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProcurementRfqReceipts_BusinessPartners_BusinessPartnerId",
                table: "ProcurementRfqReceipts",
                column: "BusinessPartnerId",
                principalTable: "BusinessPartners",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProcurementRfqReceipts_ProcurementRfqOpeningRegisters_OpeningRegisterId",
                table: "ProcurementRfqReceipts",
                column: "OpeningRegisterId",
                principalTable: "ProcurementRfqOpeningRegisters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProcurementRfqReceipts_RequestForQuotationQuotes_QuoteId",
                table: "ProcurementRfqReceipts",
                column: "QuoteId",
                principalTable: "RequestForQuotationQuotes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProcurementRfqReceipts_RequestForQuotations_RfqId",
                table: "ProcurementRfqReceipts",
                column: "RfqId",
                principalTable: "RequestForQuotations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProcurementRfqEvaluationLines_BusinessPartners_BusinessPartnerId",
                table: "ProcurementRfqEvaluationLines");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcurementRfqEvaluationLines_ProcurementRfqEvaluations_EvaluationId",
                table: "ProcurementRfqEvaluationLines");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcurementRfqEvaluationLines_RequestForQuotationItems_RfqItemId",
                table: "ProcurementRfqEvaluationLines");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcurementRfqEvaluationLines_RequestForQuotationQuotes_QuoteId",
                table: "ProcurementRfqEvaluationLines");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcurementRfqEvaluations_ProcurementPolicyMethodRules_MethodRuleId",
                table: "ProcurementRfqEvaluations");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcurementRfqEvaluations_ProcurementRfqOpeningRegisters_OpeningRegisterId",
                table: "ProcurementRfqEvaluations");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcurementRfqEvaluations_RequestForQuotations_RfqId",
                table: "ProcurementRfqEvaluations");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcurementRfqEvaluations_WorkflowInstances_WorkflowInstanceId",
                table: "ProcurementRfqEvaluations");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcurementRfqOpeningEntries_BusinessPartners_BusinessPartnerId",
                table: "ProcurementRfqOpeningEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcurementRfqOpeningEntries_ProcurementRfqOpeningRegisters_OpeningRegisterId",
                table: "ProcurementRfqOpeningEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcurementRfqOpeningEntries_ProcurementRfqReceipts_ReceiptId",
                table: "ProcurementRfqOpeningEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcurementRfqOpeningEntries_RequestForQuotationQuotes_QuoteId",
                table: "ProcurementRfqOpeningEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcurementRfqOpeningParticipants_ProcurementRfqOpeningRegisters_OpeningRegisterId",
                table: "ProcurementRfqOpeningParticipants");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcurementRfqOpeningRegisters_RequestForQuotations_RfqId",
                table: "ProcurementRfqOpeningRegisters");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcurementRfqReceipts_BusinessPartners_BusinessPartnerId",
                table: "ProcurementRfqReceipts");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcurementRfqReceipts_ProcurementRfqOpeningRegisters_OpeningRegisterId",
                table: "ProcurementRfqReceipts");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcurementRfqReceipts_RequestForQuotationQuotes_QuoteId",
                table: "ProcurementRfqReceipts");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcurementRfqReceipts_RequestForQuotations_RfqId",
                table: "ProcurementRfqReceipts");

            migrationBuilder.DropIndex(
                name: "IX_ProcurementRfqReceipts_TenantId_ReceiptNumber",
                table: "ProcurementRfqReceipts");

            migrationBuilder.DropIndex(
                name: "IX_ProcurementRfqReceipts_TenantId_RfqId_QuoteId",
                table: "ProcurementRfqReceipts");

            migrationBuilder.DropIndex(
                name: "IX_ProcurementRfqReceipts_TenantId_RfqId_ReceiptSequence",
                table: "ProcurementRfqReceipts");

            migrationBuilder.DropIndex(
                name: "IX_ProcurementRfqOpeningRegisters_TenantId_RfqId",
                table: "ProcurementRfqOpeningRegisters");

            migrationBuilder.DropIndex(
                name: "IX_ProcurementRfqOpeningParticipants_TenantId_OpeningRegisterId_ParticipantName_RoleName",
                table: "ProcurementRfqOpeningParticipants");

            migrationBuilder.DropIndex(
                name: "IX_ProcurementRfqOpeningEntries_TenantId_OpeningRegisterId_ReceiptId",
                table: "ProcurementRfqOpeningEntries");

            migrationBuilder.DropIndex(
                name: "IX_ProcurementRfqEvaluations_TenantId_RfqId",
                table: "ProcurementRfqEvaluations");

            migrationBuilder.DropIndex(
                name: "IX_ProcurementRfqEvaluationLines_TenantId_EvaluationId_RfqItemId",
                table: "ProcurementRfqEvaluationLines");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementRfqReceipts_TenantId",
                table: "ProcurementRfqReceipts",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementRfqOpeningRegisters_TenantId",
                table: "ProcurementRfqOpeningRegisters",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementRfqOpeningParticipants_TenantId",
                table: "ProcurementRfqOpeningParticipants",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementRfqOpeningEntries_TenantId",
                table: "ProcurementRfqOpeningEntries",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementRfqEvaluations_TenantId",
                table: "ProcurementRfqEvaluations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementRfqEvaluationLines_TenantId",
                table: "ProcurementRfqEvaluationLines",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProcurementRfqEvaluationLines_BusinessPartners_BusinessPartnerId",
                table: "ProcurementRfqEvaluationLines",
                column: "BusinessPartnerId",
                principalTable: "BusinessPartners",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ProcurementRfqEvaluationLines_ProcurementRfqEvaluations_EvaluationId",
                table: "ProcurementRfqEvaluationLines",
                column: "EvaluationId",
                principalTable: "ProcurementRfqEvaluations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ProcurementRfqEvaluationLines_RequestForQuotationItems_RfqItemId",
                table: "ProcurementRfqEvaluationLines",
                column: "RfqItemId",
                principalTable: "RequestForQuotationItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ProcurementRfqEvaluationLines_RequestForQuotationQuotes_QuoteId",
                table: "ProcurementRfqEvaluationLines",
                column: "QuoteId",
                principalTable: "RequestForQuotationQuotes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ProcurementRfqEvaluations_ProcurementPolicyMethodRules_MethodRuleId",
                table: "ProcurementRfqEvaluations",
                column: "MethodRuleId",
                principalTable: "ProcurementPolicyMethodRules",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ProcurementRfqEvaluations_ProcurementRfqOpeningRegisters_OpeningRegisterId",
                table: "ProcurementRfqEvaluations",
                column: "OpeningRegisterId",
                principalTable: "ProcurementRfqOpeningRegisters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ProcurementRfqEvaluations_RequestForQuotations_RfqId",
                table: "ProcurementRfqEvaluations",
                column: "RfqId",
                principalTable: "RequestForQuotations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ProcurementRfqEvaluations_WorkflowInstances_WorkflowInstanceId",
                table: "ProcurementRfqEvaluations",
                column: "WorkflowInstanceId",
                principalTable: "WorkflowInstances",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ProcurementRfqOpeningEntries_BusinessPartners_BusinessPartnerId",
                table: "ProcurementRfqOpeningEntries",
                column: "BusinessPartnerId",
                principalTable: "BusinessPartners",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ProcurementRfqOpeningEntries_ProcurementRfqOpeningRegisters_OpeningRegisterId",
                table: "ProcurementRfqOpeningEntries",
                column: "OpeningRegisterId",
                principalTable: "ProcurementRfqOpeningRegisters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ProcurementRfqOpeningEntries_ProcurementRfqReceipts_ReceiptId",
                table: "ProcurementRfqOpeningEntries",
                column: "ReceiptId",
                principalTable: "ProcurementRfqReceipts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ProcurementRfqOpeningEntries_RequestForQuotationQuotes_QuoteId",
                table: "ProcurementRfqOpeningEntries",
                column: "QuoteId",
                principalTable: "RequestForQuotationQuotes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ProcurementRfqOpeningParticipants_ProcurementRfqOpeningRegisters_OpeningRegisterId",
                table: "ProcurementRfqOpeningParticipants",
                column: "OpeningRegisterId",
                principalTable: "ProcurementRfqOpeningRegisters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ProcurementRfqOpeningRegisters_RequestForQuotations_RfqId",
                table: "ProcurementRfqOpeningRegisters",
                column: "RfqId",
                principalTable: "RequestForQuotations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ProcurementRfqReceipts_BusinessPartners_BusinessPartnerId",
                table: "ProcurementRfqReceipts",
                column: "BusinessPartnerId",
                principalTable: "BusinessPartners",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ProcurementRfqReceipts_ProcurementRfqOpeningRegisters_OpeningRegisterId",
                table: "ProcurementRfqReceipts",
                column: "OpeningRegisterId",
                principalTable: "ProcurementRfqOpeningRegisters",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ProcurementRfqReceipts_RequestForQuotationQuotes_QuoteId",
                table: "ProcurementRfqReceipts",
                column: "QuoteId",
                principalTable: "RequestForQuotationQuotes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ProcurementRfqReceipts_RequestForQuotations_RfqId",
                table: "ProcurementRfqReceipts",
                column: "RfqId",
                principalTable: "RequestForQuotations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
