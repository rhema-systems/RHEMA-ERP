using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class PublicPropertyEnquiryProspectLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EhcTickets_Users_RequesterUserId",
                table: "EhcTickets");

            migrationBuilder.DropIndex(
                name: "IX_EhcTickets_TenantId_RequesterUserId_ExternalSubmissionId",
                table: "EhcTickets");

            migrationBuilder.AlterColumn<Guid>(
                name: "RequesterUserId",
                table: "EhcTickets",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.CreateTable(
                name: "EhcPropertyEnquiryEmailAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Recipient = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Sender = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Subject = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    BodySha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AttemptedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_EhcPropertyEnquiryEmailAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EhcPropertyEnquiryEmailAttempts_EhcTickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "EhcTickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EhcPropertyEnquiryEmailAttempts_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EhcPropertyEnquiryProspects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LeadId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OpportunityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SalesAllocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    QualifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    QualifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BusinessPartnerLinkedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BusinessPartnerLinkedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DepositRequirementType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    FixedDepositAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    DepositPercentage = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    AgreedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
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
                    table.PrimaryKey("PK_EhcPropertyEnquiryProspects", x => x.Id);
                    table.CheckConstraint("CK_EhcPropertyEnquiryProspects_DepositRequirement", "[DepositRequirementType] IN ('Fixed','Percentage','Full')");
                    table.ForeignKey(
                        name: "FK_EhcPropertyEnquiryProspects_BusinessPartners_BusinessPartnerId",
                        column: x => x.BusinessPartnerId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EhcPropertyEnquiryProspects_EhcTickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "EhcTickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EhcPropertyEnquiryProspects_Leads_LeadId",
                        column: x => x.LeadId,
                        principalTable: "Leads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EhcPropertyEnquiryProspects_Opportunities_OpportunityId",
                        column: x => x.OpportunityId,
                        principalTable: "Opportunities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EhcPropertyEnquiryProspects_SalesAllocations_SalesAllocationId",
                        column: x => x.SalesAllocationId,
                        principalTable: "SalesAllocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EhcPropertyEnquiryProspects_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EhcPropertyProspectDepositPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SalesSaleableSourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequirementType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    FixedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Percentage = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    DepositLiabilityAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DefaultBankAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DefaultLiquidityAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_EhcPropertyProspectDepositPolicies", x => x.Id);
                    table.CheckConstraint("CK_EhcPropertyProspectDepositPolicies_Requirement", "[RequirementType] IN ('Fixed','Percentage','Full')");
                    table.CheckConstraint("CK_EhcPropertyProspectDepositPolicies_Value", "([RequirementType] = 'Fixed' AND [FixedAmount] > 0) OR ([RequirementType] = 'Percentage' AND [Percentage] > 0 AND [Percentage] <= 100) OR [RequirementType] = 'Full'");
                    table.ForeignKey(
                        name: "FK_EhcPropertyProspectDepositPolicies_SalesSaleableSources_SalesSaleableSourceId",
                        column: x => x.SalesSaleableSourceId,
                        principalTable: "SalesSaleableSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EhcPropertyProspectDepositPolicies_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EhcProspectDepositReceipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProspectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LeadId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OpportunityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SalesAllocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReceiptNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    PaymentMethod = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    TransactionReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ClearedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClearedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReversedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReversedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReversalReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DepositLiabilityAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BankAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LiquidityAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PostingEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    JournalEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReversalPostingEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReversalJournalEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CustomerAdvanceTransferPostingEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CustomerAdvanceTransferJournalEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CustomerPaymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TransferredToCustomerAdvanceAt = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_EhcProspectDepositReceipts", x => x.Id);
                    table.CheckConstraint("CK_EhcProspectDepositReceipts_Amount", "[Amount] > 0");
                    table.CheckConstraint("CK_EhcProspectDepositReceipts_CashSource", "([BankAccountId] IS NOT NULL AND [LiquidityAccountId] IS NULL) OR ([BankAccountId] IS NULL AND [LiquidityAccountId] IS NOT NULL)");
                    table.CheckConstraint("CK_EhcProspectDepositReceipts_Status", "[Status] IN ('Pending','Cleared','Reversed')");
                    table.ForeignKey(
                        name: "FK_EhcProspectDepositReceipts_BusinessPartners_BusinessPartnerId",
                        column: x => x.BusinessPartnerId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EhcProspectDepositReceipts_EhcPropertyEnquiryProspects_ProspectId",
                        column: x => x.ProspectId,
                        principalTable: "EhcPropertyEnquiryProspects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EhcProspectDepositReceipts_EhcTickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "EhcTickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EhcProspectDepositReceipts_Leads_LeadId",
                        column: x => x.LeadId,
                        principalTable: "Leads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EhcProspectDepositReceipts_Opportunities_OpportunityId",
                        column: x => x.OpportunityId,
                        principalTable: "Opportunities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EhcProspectDepositReceipts_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EhcTickets_TenantId_ExternalSubmissionId",
                table: "EhcTickets",
                columns: new[] { "TenantId", "ExternalSubmissionId" },
                unique: true,
                filter: "[ExternalSubmissionId] IS NOT NULL AND [RequesterUserId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EhcTickets_TenantId_RequesterUserId_ExternalSubmissionId",
                table: "EhcTickets",
                columns: new[] { "TenantId", "RequesterUserId", "ExternalSubmissionId" },
                unique: true,
                filter: "[ExternalSubmissionId] IS NOT NULL AND [RequesterUserId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EhcPropertyEnquiryEmailAttempts_TenantId_TicketId_AttemptedAt",
                table: "EhcPropertyEnquiryEmailAttempts",
                columns: new[] { "TenantId", "TicketId", "AttemptedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EhcPropertyEnquiryEmailAttempts_TicketId",
                table: "EhcPropertyEnquiryEmailAttempts",
                column: "TicketId");

            migrationBuilder.CreateIndex(
                name: "IX_EhcPropertyEnquiryProspects_BusinessPartnerId",
                table: "EhcPropertyEnquiryProspects",
                column: "BusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_EhcPropertyEnquiryProspects_LeadId",
                table: "EhcPropertyEnquiryProspects",
                column: "LeadId");

            migrationBuilder.CreateIndex(
                name: "IX_EhcPropertyEnquiryProspects_OpportunityId",
                table: "EhcPropertyEnquiryProspects",
                column: "OpportunityId");

            migrationBuilder.CreateIndex(
                name: "IX_EhcPropertyEnquiryProspects_SalesAllocationId",
                table: "EhcPropertyEnquiryProspects",
                column: "SalesAllocationId");

            migrationBuilder.CreateIndex(
                name: "IX_EhcPropertyEnquiryProspects_TenantId_LeadId",
                table: "EhcPropertyEnquiryProspects",
                columns: new[] { "TenantId", "LeadId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EhcPropertyEnquiryProspects_TenantId_OpportunityId",
                table: "EhcPropertyEnquiryProspects",
                columns: new[] { "TenantId", "OpportunityId" },
                unique: true,
                filter: "[OpportunityId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EhcPropertyEnquiryProspects_TenantId_TicketId",
                table: "EhcPropertyEnquiryProspects",
                columns: new[] { "TenantId", "TicketId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EhcPropertyEnquiryProspects_TicketId",
                table: "EhcPropertyEnquiryProspects",
                column: "TicketId");

            migrationBuilder.CreateIndex(
                name: "IX_EhcPropertyProspectDepositPolicies_SalesSaleableSourceId",
                table: "EhcPropertyProspectDepositPolicies",
                column: "SalesSaleableSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_EhcPropertyProspectDepositPolicies_TenantId_SalesSaleableSourceId",
                table: "EhcPropertyProspectDepositPolicies",
                columns: new[] { "TenantId", "SalesSaleableSourceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EhcProspectDepositReceipts_BusinessPartnerId",
                table: "EhcProspectDepositReceipts",
                column: "BusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_EhcProspectDepositReceipts_LeadId",
                table: "EhcProspectDepositReceipts",
                column: "LeadId");

            migrationBuilder.CreateIndex(
                name: "IX_EhcProspectDepositReceipts_OpportunityId",
                table: "EhcProspectDepositReceipts",
                column: "OpportunityId");

            migrationBuilder.CreateIndex(
                name: "IX_EhcProspectDepositReceipts_ProspectId",
                table: "EhcProspectDepositReceipts",
                column: "ProspectId");

            migrationBuilder.CreateIndex(
                name: "IX_EhcProspectDepositReceipts_TenantId_ProspectId_Status",
                table: "EhcProspectDepositReceipts",
                columns: new[] { "TenantId", "ProspectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_EhcProspectDepositReceipts_TenantId_ReceiptNumber",
                table: "EhcProspectDepositReceipts",
                columns: new[] { "TenantId", "ReceiptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EhcProspectDepositReceipts_TenantId_TransactionReference",
                table: "EhcProspectDepositReceipts",
                columns: new[] { "TenantId", "TransactionReference" },
                unique: true,
                filter: "[TransactionReference] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EhcProspectDepositReceipts_TicketId",
                table: "EhcProspectDepositReceipts",
                column: "TicketId");

            migrationBuilder.AddForeignKey(
                name: "FK_EhcTickets_Users_RequesterUserId",
                table: "EhcTickets",
                column: "RequesterUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EhcTickets_Users_RequesterUserId",
                table: "EhcTickets");

            migrationBuilder.DropTable(
                name: "EhcPropertyEnquiryEmailAttempts");

            migrationBuilder.DropTable(
                name: "EhcPropertyProspectDepositPolicies");

            migrationBuilder.DropTable(
                name: "EhcProspectDepositReceipts");

            migrationBuilder.DropTable(
                name: "EhcPropertyEnquiryProspects");

            migrationBuilder.DropIndex(
                name: "IX_EhcTickets_TenantId_ExternalSubmissionId",
                table: "EhcTickets");

            migrationBuilder.DropIndex(
                name: "IX_EhcTickets_TenantId_RequesterUserId_ExternalSubmissionId",
                table: "EhcTickets");

            migrationBuilder.AlterColumn<Guid>(
                name: "RequesterUserId",
                table: "EhcTickets",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_EhcTickets_TenantId_RequesterUserId_ExternalSubmissionId",
                table: "EhcTickets",
                columns: new[] { "TenantId", "RequesterUserId", "ExternalSubmissionId" },
                unique: true,
                filter: "[ExternalSubmissionId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_EhcTickets_Users_RequesterUserId",
                table: "EhcTickets",
                column: "RequesterUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
