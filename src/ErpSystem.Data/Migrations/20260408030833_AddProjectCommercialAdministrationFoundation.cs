using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectCommercialAdministrationFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProjectExtensionOfTimeRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectPhaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProjectPackageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    RequestedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DecisionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DaysRequested = table.Column<int>(type: "int", nullable: true),
                    DaysApproved = table.Column<int>(type: "int", nullable: true),
                    RevisedCompletionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RequestedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DecidedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_ProjectExtensionOfTimeRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectExtensionOfTimeRequests_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProjectExtensionOfTimeRequests_ProjectPackages_ProjectPackageId",
                        column: x => x.ProjectPackageId,
                        principalTable: "ProjectPackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectExtensionOfTimeRequests_ProjectPhases_ProjectPhaseId",
                        column: x => x.ProjectPhaseId,
                        principalTable: "ProjectPhases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectExtensionOfTimeRequests_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectExtensionOfTimeRequests_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectFinalAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    SettlementDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OriginalContractValue = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ApprovedVariationAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CertifiedToDate = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    RetentionHeldAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RetentionReleasedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    FinalAccountValue = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_ProjectFinalAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectFinalAccounts_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProjectFinalAccounts_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectFinalAccounts_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectInterimValuations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectPhaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProjectPackageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ValuationNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ValuationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GrossWorkValue = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    MaterialsOnSiteValue = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    VariationValue = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    RetentionPercentage = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    RetentionAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PreviousCertifiedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NetValuationAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_ProjectInterimValuations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectInterimValuations_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProjectInterimValuations_ProjectPackages_ProjectPackageId",
                        column: x => x.ProjectPackageId,
                        principalTable: "ProjectPackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectInterimValuations_ProjectPhases_ProjectPhaseId",
                        column: x => x.ProjectPhaseId,
                        principalTable: "ProjectPhases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectInterimValuations_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectInterimValuations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectVariationOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectPhaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProjectPackageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    VariationType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    RequestedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ApprovedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ImplementedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RequestedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ApprovedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    EstimatedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ApprovedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    ScheduleImpactDays = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_ProjectVariationOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectVariationOrders_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProjectVariationOrders_ProjectPackages_ProjectPackageId",
                        column: x => x.ProjectPackageId,
                        principalTable: "ProjectPackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectVariationOrders_ProjectPhases_ProjectPhaseId",
                        column: x => x.ProjectPhaseId,
                        principalTable: "ProjectPhases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectVariationOrders_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectVariationOrders_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectPaymentCertificates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectPhaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProjectPackageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProjectInterimValuationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CertificateNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    IssueDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PaymentDueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    GrossCertifiedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RetentionHeldAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RetentionReleasedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OtherDeductionsAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NetCertifiedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_ProjectPaymentCertificates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectPaymentCertificates_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProjectPaymentCertificates_ProjectInterimValuations_ProjectInterimValuationId",
                        column: x => x.ProjectInterimValuationId,
                        principalTable: "ProjectInterimValuations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProjectPaymentCertificates_ProjectPackages_ProjectPackageId",
                        column: x => x.ProjectPackageId,
                        principalTable: "ProjectPackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectPaymentCertificates_ProjectPhases_ProjectPhaseId",
                        column: x => x.ProjectPhaseId,
                        principalTable: "ProjectPhases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectPaymentCertificates_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectPaymentCertificates_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectExtensionOfTimeRequests_ContractId",
                table: "ProjectExtensionOfTimeRequests",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectExtensionOfTimeRequests_ProjectId_ContractId",
                table: "ProjectExtensionOfTimeRequests",
                columns: new[] { "ProjectId", "ContractId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectExtensionOfTimeRequests_ProjectId_ProjectPhaseId_ProjectPackageId",
                table: "ProjectExtensionOfTimeRequests",
                columns: new[] { "ProjectId", "ProjectPhaseId", "ProjectPackageId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectExtensionOfTimeRequests_ProjectId_RequestedDate",
                table: "ProjectExtensionOfTimeRequests",
                columns: new[] { "ProjectId", "RequestedDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectExtensionOfTimeRequests_ProjectId_Status",
                table: "ProjectExtensionOfTimeRequests",
                columns: new[] { "ProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectExtensionOfTimeRequests_ProjectPackageId",
                table: "ProjectExtensionOfTimeRequests",
                column: "ProjectPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectExtensionOfTimeRequests_ProjectPhaseId",
                table: "ProjectExtensionOfTimeRequests",
                column: "ProjectPhaseId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectExtensionOfTimeRequests_TenantId",
                table: "ProjectExtensionOfTimeRequests",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectFinalAccounts_ContractId",
                table: "ProjectFinalAccounts",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectFinalAccounts_ProjectId",
                table: "ProjectFinalAccounts",
                column: "ProjectId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectFinalAccounts_ProjectId_ContractId",
                table: "ProjectFinalAccounts",
                columns: new[] { "ProjectId", "ContractId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectFinalAccounts_ProjectId_Status",
                table: "ProjectFinalAccounts",
                columns: new[] { "ProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectFinalAccounts_TenantId",
                table: "ProjectFinalAccounts",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectInterimValuations_ContractId",
                table: "ProjectInterimValuations",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectInterimValuations_ProjectId_ContractId",
                table: "ProjectInterimValuations",
                columns: new[] { "ProjectId", "ContractId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectInterimValuations_ProjectId_ProjectPhaseId_ProjectPackageId",
                table: "ProjectInterimValuations",
                columns: new[] { "ProjectId", "ProjectPhaseId", "ProjectPackageId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectInterimValuations_ProjectId_Status",
                table: "ProjectInterimValuations",
                columns: new[] { "ProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectInterimValuations_ProjectId_ValuationDate",
                table: "ProjectInterimValuations",
                columns: new[] { "ProjectId", "ValuationDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectInterimValuations_ProjectPackageId",
                table: "ProjectInterimValuations",
                column: "ProjectPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectInterimValuations_ProjectPhaseId",
                table: "ProjectInterimValuations",
                column: "ProjectPhaseId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectInterimValuations_TenantId",
                table: "ProjectInterimValuations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPaymentCertificates_ContractId",
                table: "ProjectPaymentCertificates",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPaymentCertificates_ProjectId_ContractId",
                table: "ProjectPaymentCertificates",
                columns: new[] { "ProjectId", "ContractId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPaymentCertificates_ProjectId_IssueDate",
                table: "ProjectPaymentCertificates",
                columns: new[] { "ProjectId", "IssueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPaymentCertificates_ProjectId_ProjectInterimValuationId",
                table: "ProjectPaymentCertificates",
                columns: new[] { "ProjectId", "ProjectInterimValuationId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPaymentCertificates_ProjectId_ProjectPhaseId_ProjectPackageId",
                table: "ProjectPaymentCertificates",
                columns: new[] { "ProjectId", "ProjectPhaseId", "ProjectPackageId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPaymentCertificates_ProjectId_Status",
                table: "ProjectPaymentCertificates",
                columns: new[] { "ProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPaymentCertificates_ProjectInterimValuationId",
                table: "ProjectPaymentCertificates",
                column: "ProjectInterimValuationId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPaymentCertificates_ProjectPackageId",
                table: "ProjectPaymentCertificates",
                column: "ProjectPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPaymentCertificates_ProjectPhaseId",
                table: "ProjectPaymentCertificates",
                column: "ProjectPhaseId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPaymentCertificates_TenantId",
                table: "ProjectPaymentCertificates",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectVariationOrders_ContractId",
                table: "ProjectVariationOrders",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectVariationOrders_ProjectId_ContractId",
                table: "ProjectVariationOrders",
                columns: new[] { "ProjectId", "ContractId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectVariationOrders_ProjectId_ProjectPhaseId_ProjectPackageId",
                table: "ProjectVariationOrders",
                columns: new[] { "ProjectId", "ProjectPhaseId", "ProjectPackageId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectVariationOrders_ProjectId_RequestedDate",
                table: "ProjectVariationOrders",
                columns: new[] { "ProjectId", "RequestedDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectVariationOrders_ProjectId_Status",
                table: "ProjectVariationOrders",
                columns: new[] { "ProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectVariationOrders_ProjectPackageId",
                table: "ProjectVariationOrders",
                column: "ProjectPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectVariationOrders_ProjectPhaseId",
                table: "ProjectVariationOrders",
                column: "ProjectPhaseId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectVariationOrders_TenantId",
                table: "ProjectVariationOrders",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectExtensionOfTimeRequests");

            migrationBuilder.DropTable(
                name: "ProjectFinalAccounts");

            migrationBuilder.DropTable(
                name: "ProjectPaymentCertificates");

            migrationBuilder.DropTable(
                name: "ProjectVariationOrders");

            migrationBuilder.DropTable(
                name: "ProjectInterimValuations");
        }
    }
}
