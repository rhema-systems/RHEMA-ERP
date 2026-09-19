using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class RetireDormantStaffTravelTables : Migration
    {
        /// <inheritdoc />
        /// <remarks>
        /// <para><b>Retires six tables whose surfaces were removed in earlier slices of area 12.</b>
        /// The four <c>StaffTravelApproval*</c> tables were travel's bespoke approval chain, replaced
        /// by the generic workflow engine in slice 2; <c>StaffTravelVendors</c> duplicated
        /// Procurement's <c>Suppliers</c> and was retired in slice 3; <c>StaffTravelCurrencyExchangeRates</c>
        /// duplicated Finance's <c>ExchangeRate</c> and was retired in slice 6. Nothing has read or
        /// written any of them since, and no controller route reached them.</para>
        ///
        /// <para><b>Guarded, per the repo convention:</b> every drop is conditional on the table
        /// existing, so the migration is safe to re-run and safe on a database built from the EF
        /// model (where these tables never existed) as well as one built from the migration chain.</para>
        ///
        /// <para>⚠ <b>Order matters and is not alphabetical.</b> Children go before parents:
        /// Decisions → Instances, Steps → Templates, and Instances also references
        /// <c>StaffTravelRequests</c>. SQL Server refuses to drop a table another table still
        /// references, so the sequence below is load-bearing rather than cosmetic.</para>
        ///
        /// <para>⚠ <b>This is destructive and there is no data-preserving Down.</b> The reverse
        /// method recreates the schema but not the rows. The tables were confirmed unused in code
        /// before this was written, and the repository owner confirmed no records had been saved
        /// into them.</para>
        /// </remarks>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
-- Children first: each of these is referenced by nothing, but references the tables below.
IF OBJECT_ID(N'[dbo].[StaffTravelApprovalDecisions]', N'U') IS NOT NULL
    DROP TABLE [dbo].[StaffTravelApprovalDecisions];

IF OBJECT_ID(N'[dbo].[StaffTravelApprovalWorkflowSteps]', N'U') IS NOT NULL
    DROP TABLE [dbo].[StaffTravelApprovalWorkflowSteps];

IF OBJECT_ID(N'[dbo].[StaffTravelApprovalInstances]', N'U') IS NOT NULL
    DROP TABLE [dbo].[StaffTravelApprovalInstances];

-- Parent of Steps and Instances.
IF OBJECT_ID(N'[dbo].[StaffTravelApprovalWorkflowTemplates]', N'U') IS NOT NULL
    DROP TABLE [dbo].[StaffTravelApprovalWorkflowTemplates];

-- Independent duplicates of masters owned by other modules.
IF OBJECT_ID(N'[dbo].[StaffTravelVendors]', N'U') IS NOT NULL
    DROP TABLE [dbo].[StaffTravelVendors];

IF OBJECT_ID(N'[dbo].[StaffTravelCurrencyExchangeRates]', N'U') IS NOT NULL
    DROP TABLE [dbo].[StaffTravelCurrencyExchangeRates];
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StaffTravelApprovalWorkflowTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppliesToLevelFromId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AppliesToLevelToId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    IsInternational = table.Column<bool>(type: "bit", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MaxBudgetThreshold = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    MinBudgetThreshold = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RiskLevel = table.Column<int>(type: "int", nullable: true),
                    TravelType = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffTravelApprovalWorkflowTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StaffTravelApprovalWorkflowTemplates_StaffLevels_AppliesToLevelFromId",
                        column: x => x.AppliesToLevelFromId,
                        principalTable: "StaffLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StaffTravelApprovalWorkflowTemplates_StaffLevels_AppliesToLevelToId",
                        column: x => x.AppliesToLevelToId,
                        principalTable: "StaffLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StaffTravelApprovalWorkflowTemplates_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StaffTravelCurrencyExchangeRates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FromCurrency = table.Column<string>(type: "char(3)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    IsOfficial = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Rate = table.Column<decimal>(type: "decimal(18,8)", nullable: false),
                    RateDate = table.Column<DateOnly>(type: "date", nullable: false),
                    RateSource = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ToCurrency = table.Column<string>(type: "char(3)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffTravelCurrencyExchangeRates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StaffTravelCurrencyExchangeRates_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StaffTravelVendors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CountryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ContactEmail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ContactPhone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ContractEndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ContractStartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    IsPreferred = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PaymentTerms = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Rating = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VendorCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    VendorName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    VendorType = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffTravelVendors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StaffTravelVendors_Countries_CountryId",
                        column: x => x.CountryId,
                        principalTable: "Countries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StaffTravelVendors_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StaffTravelApprovalInstances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StaffTravelRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkflowTemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CurrentStepOrder = table.Column<int>(type: "int", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InitiatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffTravelApprovalInstances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StaffTravelApprovalInstances_StaffTravelApprovalWorkflowTemplates_WorkflowTemplateId",
                        column: x => x.WorkflowTemplateId,
                        principalTable: "StaffTravelApprovalWorkflowTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StaffTravelApprovalInstances_StaffTravelRequests_StaffTravelRequestId",
                        column: x => x.StaffTravelRequestId,
                        principalTable: "StaffTravelRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StaffTravelApprovalInstances_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StaffTravelApprovalWorkflowSteps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EscalationApproverId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SpecificApproverId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkflowTemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApproverRole = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ApproverType = table.Column<int>(type: "int", nullable: false),
                    CanDelegate = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    IsMandatory = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SlaHours = table.Column<int>(type: "int", nullable: true),
                    StepName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    StepOrder = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffTravelApprovalWorkflowSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StaffTravelApprovalWorkflowSteps_Employees_EscalationApproverId",
                        column: x => x.EscalationApproverId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StaffTravelApprovalWorkflowSteps_Employees_SpecificApproverId",
                        column: x => x.SpecificApproverId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StaffTravelApprovalWorkflowSteps_StaffTravelApprovalWorkflowTemplates_WorkflowTemplateId",
                        column: x => x.WorkflowTemplateId,
                        principalTable: "StaffTravelApprovalWorkflowTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StaffTravelApprovalWorkflowSteps_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StaffTravelApprovalDecisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovalInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApproverId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OriginalApproverId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Comments = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Decision = table.Column<int>(type: "int", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EscalatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    IsEscalated = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SlaDeadline = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StepOrder = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffTravelApprovalDecisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StaffTravelApprovalDecisions_Employees_ApproverId",
                        column: x => x.ApproverId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StaffTravelApprovalDecisions_Employees_OriginalApproverId",
                        column: x => x.OriginalApproverId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StaffTravelApprovalDecisions_StaffTravelApprovalInstances_ApprovalInstanceId",
                        column: x => x.ApprovalInstanceId,
                        principalTable: "StaffTravelApprovalInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StaffTravelApprovalDecisions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StaffTravelApprovalDecisions_ApprovalInstanceId",
                table: "StaffTravelApprovalDecisions",
                column: "ApprovalInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffTravelApprovalDecisions_ApproverId",
                table: "StaffTravelApprovalDecisions",
                column: "ApproverId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffTravelApprovalDecisions_OriginalApproverId",
                table: "StaffTravelApprovalDecisions",
                column: "OriginalApproverId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffTravelApprovalDecisions_TenantId",
                table: "StaffTravelApprovalDecisions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffTravelApprovalInstances_StaffTravelRequestId",
                table: "StaffTravelApprovalInstances",
                column: "StaffTravelRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffTravelApprovalInstances_TenantId",
                table: "StaffTravelApprovalInstances",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffTravelApprovalInstances_WorkflowTemplateId",
                table: "StaffTravelApprovalInstances",
                column: "WorkflowTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffTravelApprovalWorkflowSteps_EscalationApproverId",
                table: "StaffTravelApprovalWorkflowSteps",
                column: "EscalationApproverId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffTravelApprovalWorkflowSteps_SpecificApproverId",
                table: "StaffTravelApprovalWorkflowSteps",
                column: "SpecificApproverId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffTravelApprovalWorkflowSteps_TenantId",
                table: "StaffTravelApprovalWorkflowSteps",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffTravelApprovalWorkflowSteps_WorkflowTemplateId",
                table: "StaffTravelApprovalWorkflowSteps",
                column: "WorkflowTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffTravelApprovalWorkflowTemplates_AppliesToLevelFromId",
                table: "StaffTravelApprovalWorkflowTemplates",
                column: "AppliesToLevelFromId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffTravelApprovalWorkflowTemplates_AppliesToLevelToId",
                table: "StaffTravelApprovalWorkflowTemplates",
                column: "AppliesToLevelToId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffTravelApprovalWorkflowTemplates_TenantId",
                table: "StaffTravelApprovalWorkflowTemplates",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffTravelCurrencyExchangeRates_TenantId_FromCurrency_ToCurrency_RateDate",
                table: "StaffTravelCurrencyExchangeRates",
                columns: new[] { "TenantId", "FromCurrency", "ToCurrency", "RateDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StaffTravelVendors_CountryId",
                table: "StaffTravelVendors",
                column: "CountryId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffTravelVendors_IsActive",
                table: "StaffTravelVendors",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_StaffTravelVendors_TenantId_VendorCode",
                table: "StaffTravelVendors",
                columns: new[] { "TenantId", "VendorCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StaffTravelVendors_VendorType",
                table: "StaffTravelVendors",
                column: "VendorType");
        }
    }
}
