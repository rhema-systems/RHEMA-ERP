using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCrmEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // InitialBaseline contains the original singular CRM tables. The two migrations
            // immediately after it also create an abandoned plural CRM graph, after which this
            // migration promotes the singular graph to the authoritative names. On a fresh
            // chain both graphs therefore coexist and the first RenameTable used to fail.
            //
            // The redundant graph may be removed only when it is complete and empty. A partially
            // applied graph or any row is an upgrade-data reconciliation case; fail before making
            // changes so no historical CRM evidence is silently discarded.
            migrationBuilder.Sql("""
                DECLARE @RedundantCrmTableCount int =
                    (SELECT COUNT(*) FROM (VALUES
                        (OBJECT_ID(N'[dbo].[Leads]', N'U')),
                        (OBJECT_ID(N'[dbo].[Opportunities]', N'U')),
                        (OBJECT_ID(N'[dbo].[Activities]', N'U')),
                        (OBJECT_ID(N'[dbo].[Quotes]', N'U')),
                        (OBJECT_ID(N'[dbo].[QuoteLineItems]', N'U')),
                        (OBJECT_ID(N'[dbo].[Campaigns]', N'U')),
                        (OBJECT_ID(N'[dbo].[CampaignMembers]', N'U'))
                    ) AS redundant([ObjectId]) WHERE redundant.[ObjectId] IS NOT NULL);

                IF @RedundantCrmTableCount NOT IN (0, 7)
                    THROW 51000, 'CRM migration compatibility failed: the redundant plural CRM graph is incomplete. Reconcile it before continuing.', 1;

                IF @RedundantCrmTableCount = 7
                BEGIN
                    IF EXISTS (SELECT 1 FROM [dbo].[Leads])
                       OR EXISTS (SELECT 1 FROM [dbo].[Opportunities])
                       OR EXISTS (SELECT 1 FROM [dbo].[Activities])
                       OR EXISTS (SELECT 1 FROM [dbo].[Quotes])
                       OR EXISTS (SELECT 1 FROM [dbo].[QuoteLineItems])
                       OR EXISTS (SELECT 1 FROM [dbo].[Campaigns])
                       OR EXISTS (SELECT 1 FROM [dbo].[CampaignMembers])
                        THROW 51000, 'CRM migration compatibility failed: the redundant plural CRM graph contains data. No rows were changed; complete a reviewed data reconciliation before continuing.', 1;

                    DROP TABLE [dbo].[CampaignMembers];
                    DROP TABLE [dbo].[Campaigns];
                    DROP TABLE [dbo].[QuoteLineItems];
                    DROP TABLE [dbo].[Quotes];
                    DROP TABLE [dbo].[Activities];
                    DROP TABLE [dbo].[Opportunities];
                    DROP TABLE [dbo].[Leads];
                END;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_Activity_Customers_CustomerId",
                table: "Activity");

            migrationBuilder.DropForeignKey(
                name: "FK_Activity_Lead_LeadId",
                table: "Activity");

            migrationBuilder.DropForeignKey(
                name: "FK_Activity_Opportunity_OpportunityId",
                table: "Activity");

            migrationBuilder.DropForeignKey(
                name: "FK_Activity_Tenants_TenantId",
                table: "Activity");

            migrationBuilder.DropForeignKey(
                name: "FK_Activity_Users_AssignedToId",
                table: "Activity");

            migrationBuilder.DropForeignKey(
                name: "FK_Lead_Customers_ConvertedCustomerId",
                table: "Lead");

            migrationBuilder.DropForeignKey(
                name: "FK_Lead_Tenants_TenantId",
                table: "Lead");

            migrationBuilder.DropForeignKey(
                name: "FK_Lead_Users_AssignedToId",
                table: "Lead");

            migrationBuilder.DropForeignKey(
                name: "FK_Opportunity_Customers_CustomerId",
                table: "Opportunity");

            migrationBuilder.DropForeignKey(
                name: "FK_Opportunity_Lead_LeadId",
                table: "Opportunity");

            migrationBuilder.DropForeignKey(
                name: "FK_Opportunity_Tenants_TenantId",
                table: "Opportunity");

            migrationBuilder.DropForeignKey(
                name: "FK_Opportunity_Users_AssignedToId",
                table: "Opportunity");

            migrationBuilder.DropForeignKey(
                name: "FK_Product_Tenants_TenantId",
                table: "Product");

            migrationBuilder.DropForeignKey(
                name: "FK_Quote_Customers_CustomerId",
                table: "Quote");

            migrationBuilder.DropForeignKey(
                name: "FK_Quote_Invoices_ConvertedInvoiceId",
                table: "Quote");

            migrationBuilder.DropForeignKey(
                name: "FK_Quote_Opportunity_OpportunityId",
                table: "Quote");

            migrationBuilder.DropForeignKey(
                name: "FK_Quote_Tenants_TenantId",
                table: "Quote");

            migrationBuilder.DropForeignKey(
                name: "FK_QuoteLineItem_Quote_QuoteId",
                table: "QuoteLineItem");

            migrationBuilder.DropForeignKey(
                name: "FK_QuoteLineItem_Tenants_TenantId",
                table: "QuoteLineItem");

            migrationBuilder.DropForeignKey(
                name: "FK_SalesAgreementLines_Product_ProductId",
                table: "SalesAgreementLines");

            migrationBuilder.DropForeignKey(
                name: "FK_SalesOrderLines_Product_ProductId",
                table: "SalesOrderLines");

            migrationBuilder.DropForeignKey(
                name: "FK_SalesOrders_Opportunity_OpportunityId",
                table: "SalesOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_SalesOrders_Quote_QuoteId",
                table: "SalesOrders");

            migrationBuilder.DropPrimaryKey(
                name: "PK_QuoteLineItem",
                table: "QuoteLineItem");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Quote",
                table: "Quote");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Product",
                table: "Product");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Opportunity",
                table: "Opportunity");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Lead",
                table: "Lead");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Activity",
                table: "Activity");

            migrationBuilder.RenameTable(
                name: "QuoteLineItem",
                newName: "QuoteLineItems");

            migrationBuilder.RenameTable(
                name: "Quote",
                newName: "Quotes");

            migrationBuilder.RenameTable(
                name: "Product",
                newName: "SalesProducts");

            migrationBuilder.RenameTable(
                name: "Opportunity",
                newName: "Opportunities");

            migrationBuilder.RenameTable(
                name: "Lead",
                newName: "Leads");

            migrationBuilder.RenameTable(
                name: "Activity",
                newName: "CrmActivities");

            migrationBuilder.RenameIndex(
                name: "IX_QuoteLineItem_TenantId",
                table: "QuoteLineItems",
                newName: "IX_QuoteLineItems_TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_QuoteLineItem_QuoteId",
                table: "QuoteLineItems",
                newName: "IX_QuoteLineItems_QuoteId");

            migrationBuilder.RenameIndex(
                name: "IX_Quote_TenantId",
                table: "Quotes",
                newName: "IX_Quotes_TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_Quote_OpportunityId",
                table: "Quotes",
                newName: "IX_Quotes_OpportunityId");

            migrationBuilder.RenameIndex(
                name: "IX_Quote_CustomerId",
                table: "Quotes",
                newName: "IX_Quotes_CustomerId");

            migrationBuilder.RenameIndex(
                name: "IX_Quote_ConvertedInvoiceId",
                table: "Quotes",
                newName: "IX_Quotes_ConvertedInvoiceId");

            migrationBuilder.RenameIndex(
                name: "IX_Product_TenantId",
                table: "SalesProducts",
                newName: "IX_SalesProducts_TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_Opportunity_TenantId",
                table: "Opportunities",
                newName: "IX_Opportunities_TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_Opportunity_LeadId",
                table: "Opportunities",
                newName: "IX_Opportunities_LeadId");

            migrationBuilder.RenameIndex(
                name: "IX_Opportunity_CustomerId",
                table: "Opportunities",
                newName: "IX_Opportunities_CustomerId");

            migrationBuilder.RenameIndex(
                name: "IX_Opportunity_AssignedToId",
                table: "Opportunities",
                newName: "IX_Opportunities_AssignedToId");

            migrationBuilder.RenameIndex(
                name: "IX_Lead_TenantId",
                table: "Leads",
                newName: "IX_Leads_TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_Lead_ConvertedCustomerId",
                table: "Leads",
                newName: "IX_Leads_ConvertedCustomerId");

            migrationBuilder.RenameIndex(
                name: "IX_Lead_AssignedToId",
                table: "Leads",
                newName: "IX_Leads_AssignedToId");

            migrationBuilder.RenameIndex(
                name: "IX_Activity_TenantId",
                table: "CrmActivities",
                newName: "IX_CrmActivities_TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_Activity_OpportunityId",
                table: "CrmActivities",
                newName: "IX_CrmActivities_OpportunityId");

            migrationBuilder.RenameIndex(
                name: "IX_Activity_LeadId",
                table: "CrmActivities",
                newName: "IX_CrmActivities_LeadId");

            migrationBuilder.RenameIndex(
                name: "IX_Activity_CustomerId",
                table: "CrmActivities",
                newName: "IX_CrmActivities_CustomerId");

            migrationBuilder.RenameIndex(
                name: "IX_Activity_AssignedToId",
                table: "CrmActivities",
                newName: "IX_CrmActivities_AssignedToId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_QuoteLineItems",
                table: "QuoteLineItems",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Quotes",
                table: "Quotes",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_SalesProducts",
                table: "SalesProducts",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Opportunities",
                table: "Opportunities",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Leads",
                table: "Leads",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_CrmActivities",
                table: "CrmActivities",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "Campaigns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CampaignType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CampaignStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Budget = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ActualCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ExpectedRevenue = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ActualRevenue = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ManagerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TargetAudience = table.Column<int>(type: "int", nullable: false),
                    ActualAudience = table.Column<int>(type: "int", nullable: false),
                    ResponseCount = table.Column<int>(type: "int", nullable: false),
                    LeadsGenerated = table.Column<int>(type: "int", nullable: false),
                    OpportunitiesGenerated = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpirationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Metadata = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Tags = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Campaigns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Campaigns_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Campaigns_Users_ManagerId",
                        column: x => x.ManagerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CollectionActivities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Subject = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ActivityType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ActivityDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FollowUpDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CollectionStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    OutstandingAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PromisedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PromisedPayDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AssignedToId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpirationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Metadata = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Tags = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectionActivities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CollectionActivities_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CollectionActivities_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CollectionActivities_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CollectionActivities_Users_AssignedToId",
                        column: x => x.AssignedToId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PaymentPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TotalDebt = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalPaid = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PlanStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    NumberOfInstallments = table.Column<int>(type: "int", nullable: false),
                    InstallmentsPaid = table.Column<int>(type: "int", nullable: false),
                    Frequency = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Terms = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpirationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Metadata = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Tags = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentPlans_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentPlans_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentPlans_Users_ApprovedById",
                        column: x => x.ApprovedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CampaignMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LeadId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MemberStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DateAdded = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResponseDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResponseType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CampaignMembers_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CampaignMembers_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CampaignMembers_Leads_LeadId",
                        column: x => x.LeadId,
                        principalTable: "Leads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CampaignMembers_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PaymentPlanInstallments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstallmentNumber = table.Column<int>(type: "int", nullable: false),
                    AmountDue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AmountPaid = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PaidDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InstallmentStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PaymentReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentPlanInstallments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentPlanInstallments_PaymentPlans_PaymentPlanId",
                        column: x => x.PaymentPlanId,
                        principalTable: "PaymentPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PaymentPlanInstallments_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CreditNoteLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreditNoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TaxCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreditNoteLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreditNoteLines_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CreditNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReturnOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OriginalInvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreditNoteStatus = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    AppliedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AppliedToInvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpirationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Metadata = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Tags = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    ApprovalStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SubmittedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubmittedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovalComments = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    WorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DocumentNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DocumentDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Terms = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    InternalNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ExternalNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreditNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreditNotes_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CreditNotes_Invoices_OriginalInvoiceId",
                        column: x => x.OriginalInvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CreditNotes_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Refunds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreditNoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReturnOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RefundStatus = table.Column<int>(type: "int", nullable: false),
                    RefundMethod = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RefundAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ProcessedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProcessedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PaymentReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpirationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Metadata = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Tags = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    ApprovalStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SubmittedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubmittedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovalComments = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    WorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DocumentNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DocumentDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Terms = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    InternalNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ExternalNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Refunds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Refunds_CreditNotes_CreditNoteId",
                        column: x => x.CreditNoteId,
                        principalTable: "CreditNotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Refunds_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Refunds_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Refunds_Users_ProcessedById",
                        column: x => x.ProcessedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReturnOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SalesOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeliveryNoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReturnStatus = table.Column<int>(type: "int", nullable: false),
                    ReasonCode = table.Column<int>(type: "int", nullable: false),
                    ReasonDescription = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ReceivedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InspectedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InspectedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InspectionNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreditNoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RefundId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpirationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Metadata = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Tags = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    ApprovalStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SubmittedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubmittedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovalComments = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    WorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DocumentNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DocumentDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Terms = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    InternalNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ExternalNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReturnOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReturnOrders_CreditNotes_CreditNoteId",
                        column: x => x.CreditNoteId,
                        principalTable: "CreditNotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReturnOrders_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReturnOrders_DeliveryNotes_DeliveryNoteId",
                        column: x => x.DeliveryNoteId,
                        principalTable: "DeliveryNotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReturnOrders_Refunds_RefundId",
                        column: x => x.RefundId,
                        principalTable: "Refunds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReturnOrders_SalesOrders_SalesOrderId",
                        column: x => x.SalesOrderId,
                        principalTable: "SalesOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReturnOrders_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReturnOrders_Users_InspectedById",
                        column: x => x.InspectedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReturnOrderLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReturnOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SalesOrderLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ProductCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    QuantityReturned = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ReasonCode = table.Column<int>(type: "int", nullable: false),
                    Condition = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsRestockable = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReturnOrderLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReturnOrderLines_ReturnOrders_ReturnOrderId",
                        column: x => x.ReturnOrderId,
                        principalTable: "ReturnOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReturnOrderLines_SalesOrderLines_SalesOrderLineId",
                        column: x => x.SalesOrderLineId,
                        principalTable: "SalesOrderLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReturnOrderLines_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CampaignMembers_CampaignId",
                table: "CampaignMembers",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignMembers_CustomerId",
                table: "CampaignMembers",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignMembers_LeadId",
                table: "CampaignMembers",
                column: "LeadId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignMembers_TenantId",
                table: "CampaignMembers",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Campaigns_ManagerId",
                table: "Campaigns",
                column: "ManagerId");

            migrationBuilder.CreateIndex(
                name: "IX_Campaigns_TenantId",
                table: "Campaigns",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionActivities_AssignedToId",
                table: "CollectionActivities",
                column: "AssignedToId");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionActivities_CustomerId",
                table: "CollectionActivities",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionActivities_InvoiceId",
                table: "CollectionActivities",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionActivities_TenantId",
                table: "CollectionActivities",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditNoteLines_CreditNoteId",
                table: "CreditNoteLines",
                column: "CreditNoteId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditNoteLines_TenantId",
                table: "CreditNoteLines",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditNotes_CustomerId",
                table: "CreditNotes",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditNotes_OriginalInvoiceId",
                table: "CreditNotes",
                column: "OriginalInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditNotes_ReturnOrderId",
                table: "CreditNotes",
                column: "ReturnOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditNotes_TenantId",
                table: "CreditNotes",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentPlanInstallments_PaymentPlanId",
                table: "PaymentPlanInstallments",
                column: "PaymentPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentPlanInstallments_TenantId",
                table: "PaymentPlanInstallments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentPlans_ApprovedById",
                table: "PaymentPlans",
                column: "ApprovedById");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentPlans_CustomerId",
                table: "PaymentPlans",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentPlans_TenantId",
                table: "PaymentPlans",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_CreditNoteId",
                table: "Refunds",
                column: "CreditNoteId");

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_CustomerId",
                table: "Refunds",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_ProcessedById",
                table: "Refunds",
                column: "ProcessedById");

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_ReturnOrderId",
                table: "Refunds",
                column: "ReturnOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_TenantId",
                table: "Refunds",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnOrderLines_ReturnOrderId",
                table: "ReturnOrderLines",
                column: "ReturnOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnOrderLines_SalesOrderLineId",
                table: "ReturnOrderLines",
                column: "SalesOrderLineId");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnOrderLines_TenantId",
                table: "ReturnOrderLines",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnOrders_CreditNoteId",
                table: "ReturnOrders",
                column: "CreditNoteId");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnOrders_CustomerId",
                table: "ReturnOrders",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnOrders_DeliveryNoteId",
                table: "ReturnOrders",
                column: "DeliveryNoteId");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnOrders_InspectedById",
                table: "ReturnOrders",
                column: "InspectedById");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnOrders_RefundId",
                table: "ReturnOrders",
                column: "RefundId");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnOrders_SalesOrderId",
                table: "ReturnOrders",
                column: "SalesOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnOrders_TenantId",
                table: "ReturnOrders",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_CrmActivities_Customers_CustomerId",
                table: "CrmActivities",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CrmActivities_Leads_LeadId",
                table: "CrmActivities",
                column: "LeadId",
                principalTable: "Leads",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CrmActivities_Opportunities_OpportunityId",
                table: "CrmActivities",
                column: "OpportunityId",
                principalTable: "Opportunities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CrmActivities_Tenants_TenantId",
                table: "CrmActivities",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CrmActivities_Users_AssignedToId",
                table: "CrmActivities",
                column: "AssignedToId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Leads_Customers_ConvertedCustomerId",
                table: "Leads",
                column: "ConvertedCustomerId",
                principalTable: "Customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Leads_Tenants_TenantId",
                table: "Leads",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Leads_Users_AssignedToId",
                table: "Leads",
                column: "AssignedToId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Opportunities_Customers_CustomerId",
                table: "Opportunities",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Opportunities_Leads_LeadId",
                table: "Opportunities",
                column: "LeadId",
                principalTable: "Leads",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Opportunities_Tenants_TenantId",
                table: "Opportunities",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Opportunities_Users_AssignedToId",
                table: "Opportunities",
                column: "AssignedToId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_QuoteLineItems_Quotes_QuoteId",
                table: "QuoteLineItems",
                column: "QuoteId",
                principalTable: "Quotes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_QuoteLineItems_Tenants_TenantId",
                table: "QuoteLineItems",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Quotes_Customers_CustomerId",
                table: "Quotes",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Quotes_Invoices_ConvertedInvoiceId",
                table: "Quotes",
                column: "ConvertedInvoiceId",
                principalTable: "Invoices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Quotes_Opportunities_OpportunityId",
                table: "Quotes",
                column: "OpportunityId",
                principalTable: "Opportunities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Quotes_Tenants_TenantId",
                table: "Quotes",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SalesAgreementLines_SalesProducts_ProductId",
                table: "SalesAgreementLines",
                column: "ProductId",
                principalTable: "SalesProducts",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_SalesOrderLines_SalesProducts_ProductId",
                table: "SalesOrderLines",
                column: "ProductId",
                principalTable: "SalesProducts",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_SalesOrders_Opportunities_OpportunityId",
                table: "SalesOrders",
                column: "OpportunityId",
                principalTable: "Opportunities",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_SalesOrders_Quotes_QuoteId",
                table: "SalesOrders",
                column: "QuoteId",
                principalTable: "Quotes",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_SalesProducts_Tenants_TenantId",
                table: "SalesProducts",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CreditNoteLines_CreditNotes_CreditNoteId",
                table: "CreditNoteLines",
                column: "CreditNoteId",
                principalTable: "CreditNotes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CreditNotes_ReturnOrders_ReturnOrderId",
                table: "CreditNotes",
                column: "ReturnOrderId",
                principalTable: "ReturnOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Refunds_ReturnOrders_ReturnOrderId",
                table: "Refunds",
                column: "ReturnOrderId",
                principalTable: "ReturnOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CrmActivities_Customers_CustomerId",
                table: "CrmActivities");

            migrationBuilder.DropForeignKey(
                name: "FK_CrmActivities_Leads_LeadId",
                table: "CrmActivities");

            migrationBuilder.DropForeignKey(
                name: "FK_CrmActivities_Opportunities_OpportunityId",
                table: "CrmActivities");

            migrationBuilder.DropForeignKey(
                name: "FK_CrmActivities_Tenants_TenantId",
                table: "CrmActivities");

            migrationBuilder.DropForeignKey(
                name: "FK_CrmActivities_Users_AssignedToId",
                table: "CrmActivities");

            migrationBuilder.DropForeignKey(
                name: "FK_Leads_Customers_ConvertedCustomerId",
                table: "Leads");

            migrationBuilder.DropForeignKey(
                name: "FK_Leads_Tenants_TenantId",
                table: "Leads");

            migrationBuilder.DropForeignKey(
                name: "FK_Leads_Users_AssignedToId",
                table: "Leads");

            migrationBuilder.DropForeignKey(
                name: "FK_Opportunities_Customers_CustomerId",
                table: "Opportunities");

            migrationBuilder.DropForeignKey(
                name: "FK_Opportunities_Leads_LeadId",
                table: "Opportunities");

            migrationBuilder.DropForeignKey(
                name: "FK_Opportunities_Tenants_TenantId",
                table: "Opportunities");

            migrationBuilder.DropForeignKey(
                name: "FK_Opportunities_Users_AssignedToId",
                table: "Opportunities");

            migrationBuilder.DropForeignKey(
                name: "FK_QuoteLineItems_Quotes_QuoteId",
                table: "QuoteLineItems");

            migrationBuilder.DropForeignKey(
                name: "FK_QuoteLineItems_Tenants_TenantId",
                table: "QuoteLineItems");

            migrationBuilder.DropForeignKey(
                name: "FK_Quotes_Customers_CustomerId",
                table: "Quotes");

            migrationBuilder.DropForeignKey(
                name: "FK_Quotes_Invoices_ConvertedInvoiceId",
                table: "Quotes");

            migrationBuilder.DropForeignKey(
                name: "FK_Quotes_Opportunities_OpportunityId",
                table: "Quotes");

            migrationBuilder.DropForeignKey(
                name: "FK_Quotes_Tenants_TenantId",
                table: "Quotes");

            migrationBuilder.DropForeignKey(
                name: "FK_SalesAgreementLines_SalesProducts_ProductId",
                table: "SalesAgreementLines");

            migrationBuilder.DropForeignKey(
                name: "FK_SalesOrderLines_SalesProducts_ProductId",
                table: "SalesOrderLines");

            migrationBuilder.DropForeignKey(
                name: "FK_SalesOrders_Opportunities_OpportunityId",
                table: "SalesOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_SalesOrders_Quotes_QuoteId",
                table: "SalesOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_SalesProducts_Tenants_TenantId",
                table: "SalesProducts");

            migrationBuilder.DropForeignKey(
                name: "FK_Refunds_CreditNotes_CreditNoteId",
                table: "Refunds");

            migrationBuilder.DropForeignKey(
                name: "FK_ReturnOrders_CreditNotes_CreditNoteId",
                table: "ReturnOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_Refunds_ReturnOrders_ReturnOrderId",
                table: "Refunds");

            migrationBuilder.DropTable(
                name: "CampaignMembers");

            migrationBuilder.DropTable(
                name: "CollectionActivities");

            migrationBuilder.DropTable(
                name: "CreditNoteLines");

            migrationBuilder.DropTable(
                name: "PaymentPlanInstallments");

            migrationBuilder.DropTable(
                name: "ReturnOrderLines");

            migrationBuilder.DropTable(
                name: "Campaigns");

            migrationBuilder.DropTable(
                name: "PaymentPlans");

            migrationBuilder.DropTable(
                name: "CreditNotes");

            migrationBuilder.DropTable(
                name: "ReturnOrders");

            migrationBuilder.DropTable(
                name: "Refunds");

            migrationBuilder.DropPrimaryKey(
                name: "PK_SalesProducts",
                table: "SalesProducts");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Quotes",
                table: "Quotes");

            migrationBuilder.DropPrimaryKey(
                name: "PK_QuoteLineItems",
                table: "QuoteLineItems");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Opportunities",
                table: "Opportunities");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Leads",
                table: "Leads");

            migrationBuilder.DropPrimaryKey(
                name: "PK_CrmActivities",
                table: "CrmActivities");

            migrationBuilder.RenameTable(
                name: "SalesProducts",
                newName: "Product");

            migrationBuilder.RenameTable(
                name: "Quotes",
                newName: "Quote");

            migrationBuilder.RenameTable(
                name: "QuoteLineItems",
                newName: "QuoteLineItem");

            migrationBuilder.RenameTable(
                name: "Opportunities",
                newName: "Opportunity");

            migrationBuilder.RenameTable(
                name: "Leads",
                newName: "Lead");

            migrationBuilder.RenameTable(
                name: "CrmActivities",
                newName: "Activity");

            migrationBuilder.RenameIndex(
                name: "IX_SalesProducts_TenantId",
                table: "Product",
                newName: "IX_Product_TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_Quotes_TenantId",
                table: "Quote",
                newName: "IX_Quote_TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_Quotes_OpportunityId",
                table: "Quote",
                newName: "IX_Quote_OpportunityId");

            migrationBuilder.RenameIndex(
                name: "IX_Quotes_CustomerId",
                table: "Quote",
                newName: "IX_Quote_CustomerId");

            migrationBuilder.RenameIndex(
                name: "IX_Quotes_ConvertedInvoiceId",
                table: "Quote",
                newName: "IX_Quote_ConvertedInvoiceId");

            migrationBuilder.RenameIndex(
                name: "IX_QuoteLineItems_TenantId",
                table: "QuoteLineItem",
                newName: "IX_QuoteLineItem_TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_QuoteLineItems_QuoteId",
                table: "QuoteLineItem",
                newName: "IX_QuoteLineItem_QuoteId");

            migrationBuilder.RenameIndex(
                name: "IX_Opportunities_TenantId",
                table: "Opportunity",
                newName: "IX_Opportunity_TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_Opportunities_LeadId",
                table: "Opportunity",
                newName: "IX_Opportunity_LeadId");

            migrationBuilder.RenameIndex(
                name: "IX_Opportunities_CustomerId",
                table: "Opportunity",
                newName: "IX_Opportunity_CustomerId");

            migrationBuilder.RenameIndex(
                name: "IX_Opportunities_AssignedToId",
                table: "Opportunity",
                newName: "IX_Opportunity_AssignedToId");

            migrationBuilder.RenameIndex(
                name: "IX_Leads_TenantId",
                table: "Lead",
                newName: "IX_Lead_TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_Leads_ConvertedCustomerId",
                table: "Lead",
                newName: "IX_Lead_ConvertedCustomerId");

            migrationBuilder.RenameIndex(
                name: "IX_Leads_AssignedToId",
                table: "Lead",
                newName: "IX_Lead_AssignedToId");

            migrationBuilder.RenameIndex(
                name: "IX_CrmActivities_TenantId",
                table: "Activity",
                newName: "IX_Activity_TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_CrmActivities_OpportunityId",
                table: "Activity",
                newName: "IX_Activity_OpportunityId");

            migrationBuilder.RenameIndex(
                name: "IX_CrmActivities_LeadId",
                table: "Activity",
                newName: "IX_Activity_LeadId");

            migrationBuilder.RenameIndex(
                name: "IX_CrmActivities_CustomerId",
                table: "Activity",
                newName: "IX_Activity_CustomerId");

            migrationBuilder.RenameIndex(
                name: "IX_CrmActivities_AssignedToId",
                table: "Activity",
                newName: "IX_Activity_AssignedToId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Product",
                table: "Product",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Quote",
                table: "Quote",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_QuoteLineItem",
                table: "QuoteLineItem",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Opportunity",
                table: "Opportunity",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Lead",
                table: "Lead",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Activity",
                table: "Activity",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Activity_Customers_CustomerId",
                table: "Activity",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Activity_Lead_LeadId",
                table: "Activity",
                column: "LeadId",
                principalTable: "Lead",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Activity_Opportunity_OpportunityId",
                table: "Activity",
                column: "OpportunityId",
                principalTable: "Opportunity",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Activity_Tenants_TenantId",
                table: "Activity",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Activity_Users_AssignedToId",
                table: "Activity",
                column: "AssignedToId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Lead_Customers_ConvertedCustomerId",
                table: "Lead",
                column: "ConvertedCustomerId",
                principalTable: "Customers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Lead_Tenants_TenantId",
                table: "Lead",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Lead_Users_AssignedToId",
                table: "Lead",
                column: "AssignedToId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Opportunity_Customers_CustomerId",
                table: "Opportunity",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Opportunity_Lead_LeadId",
                table: "Opportunity",
                column: "LeadId",
                principalTable: "Lead",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Opportunity_Tenants_TenantId",
                table: "Opportunity",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Opportunity_Users_AssignedToId",
                table: "Opportunity",
                column: "AssignedToId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Product_Tenants_TenantId",
                table: "Product",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Quote_Customers_CustomerId",
                table: "Quote",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Quote_Invoices_ConvertedInvoiceId",
                table: "Quote",
                column: "ConvertedInvoiceId",
                principalTable: "Invoices",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Quote_Opportunity_OpportunityId",
                table: "Quote",
                column: "OpportunityId",
                principalTable: "Opportunity",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Quote_Tenants_TenantId",
                table: "Quote",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_QuoteLineItem_Quote_QuoteId",
                table: "QuoteLineItem",
                column: "QuoteId",
                principalTable: "Quote",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_QuoteLineItem_Tenants_TenantId",
                table: "QuoteLineItem",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SalesAgreementLines_Product_ProductId",
                table: "SalesAgreementLines",
                column: "ProductId",
                principalTable: "Product",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_SalesOrderLines_Product_ProductId",
                table: "SalesOrderLines",
                column: "ProductId",
                principalTable: "Product",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_SalesOrders_Opportunity_OpportunityId",
                table: "SalesOrders",
                column: "OpportunityId",
                principalTable: "Opportunity",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_SalesOrders_Quote_QuoteId",
                table: "SalesOrders",
                column: "QuoteId",
                principalTable: "Quote",
                principalColumn: "Id");
        }
    }
}
