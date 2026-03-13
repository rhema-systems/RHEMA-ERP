using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectAdvancedExecutionSlices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProjectAssetLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MaintenanceAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CompanyAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    JobCardId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LinkType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
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
                    table.PrimaryKey("PK_ProjectAssetLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectAssetLinks_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectAssetLinks_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectBaselines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", maxLength: 16000, nullable: false),
                    IsLocked = table.Column<bool>(type: "bit", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
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
                    table.PrimaryKey("PK_ProjectBaselines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectBaselines_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectBaselines_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectDeliverables",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MilestoneId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubmittedDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    TargetDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SubmittedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ExternalSubmissionAllowed = table.Column<bool>(type: "bit", nullable: false),
                    ExternalSignOffRequired = table.Column<bool>(type: "bit", nullable: false),
                    IsExternalVisible = table.Column<bool>(type: "bit", nullable: false),
                    AcceptanceNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_ProjectDeliverables", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectDeliverables_ProjectDocuments_SubmittedDocumentId",
                        column: x => x.SubmittedDocumentId,
                        principalTable: "ProjectDocuments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProjectDeliverables_ProjectMilestones_MilestoneId",
                        column: x => x.MilestoneId,
                        principalTable: "ProjectMilestones",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProjectDeliverables_ProjectWorkItems_WorkItemId",
                        column: x => x.WorkItemId,
                        principalTable: "ProjectWorkItems",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProjectDeliverables_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectDeliverables_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectExpenses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExpenseDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsBillable = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ReceiptDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ApprovedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_ProjectExpenses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectExpenses_ProjectDocuments_ReceiptDocumentId",
                        column: x => x.ReceiptDocumentId,
                        principalTable: "ProjectDocuments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProjectExpenses_ProjectWorkItems_WorkItemId",
                        column: x => x.WorkItemId,
                        principalTable: "ProjectWorkItems",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProjectExpenses_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectExpenses_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectExternalAccessPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ArtifactType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ArtifactId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AccessLevel = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CanComment = table.Column<bool>(type: "bit", nullable: false),
                    CanUpload = table.Column<bool>(type: "bit", nullable: false),
                    CanApprove = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_ProjectExternalAccessPolicies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectExternalAccessPolicies_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectExternalAccessPolicies_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectRevenueRecognitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvoiceRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RecognitionPeriod = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RecognizedRevenue = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    RecognizedCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    GrossMargin = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    CashCollected = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
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
                    table.PrimaryKey("PK_ProjectRevenueRecognitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectRevenueRecognitions_ProjectInvoiceRequests_InvoiceRequestId",
                        column: x => x.InvoiceRequestId,
                        principalTable: "ProjectInvoiceRequests",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProjectRevenueRecognitions_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectRevenueRecognitions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectTaskDependencies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PredecessorWorkItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SuccessorWorkItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DependencyType = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    LagDays = table.Column<int>(type: "int", nullable: false),
                    IsEnforced = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_ProjectTaskDependencies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectTaskDependencies_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectTaskDependencies_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectTimesheetEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntryDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Hours = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    IsBillable = table.Column<bool>(type: "bit", nullable: false),
                    HourlyRate = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    CostAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    WorkType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ApprovedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_ProjectTimesheetEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectTimesheetEntries_ProjectWorkItems_WorkItemId",
                        column: x => x.WorkItemId,
                        principalTable: "ProjectWorkItems",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProjectTimesheetEntries_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectTimesheetEntries_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectAssetLinks_ProjectId_LinkType_Status",
                table: "ProjectAssetLinks",
                columns: new[] { "ProjectId", "LinkType", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectAssetLinks_TenantId",
                table: "ProjectAssetLinks",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectBaselines_ProjectId_Name",
                table: "ProjectBaselines",
                columns: new[] { "ProjectId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectBaselines_TenantId",
                table: "ProjectBaselines",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectDeliverables_MilestoneId",
                table: "ProjectDeliverables",
                column: "MilestoneId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectDeliverables_ProjectId_IsExternalVisible_ExternalSubmissionAllowed",
                table: "ProjectDeliverables",
                columns: new[] { "ProjectId", "IsExternalVisible", "ExternalSubmissionAllowed" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectDeliverables_ProjectId_Status",
                table: "ProjectDeliverables",
                columns: new[] { "ProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectDeliverables_SubmittedDocumentId",
                table: "ProjectDeliverables",
                column: "SubmittedDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectDeliverables_TenantId",
                table: "ProjectDeliverables",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectDeliverables_WorkItemId",
                table: "ProjectDeliverables",
                column: "WorkItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectExpenses_ProjectId_Status",
                table: "ProjectExpenses",
                columns: new[] { "ProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectExpenses_ProjectId_UserId_ExpenseDate",
                table: "ProjectExpenses",
                columns: new[] { "ProjectId", "UserId", "ExpenseDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectExpenses_ReceiptDocumentId",
                table: "ProjectExpenses",
                column: "ReceiptDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectExpenses_TenantId",
                table: "ProjectExpenses",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectExpenses_WorkItemId",
                table: "ProjectExpenses",
                column: "WorkItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectExternalAccessPolicies_ProjectId_BusinessPartnerId_ArtifactType_ArtifactId",
                table: "ProjectExternalAccessPolicies",
                columns: new[] { "ProjectId", "BusinessPartnerId", "ArtifactType", "ArtifactId" },
                unique: true,
                filter: "[ArtifactId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectExternalAccessPolicies_TenantId",
                table: "ProjectExternalAccessPolicies",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectRevenueRecognitions_InvoiceRequestId",
                table: "ProjectRevenueRecognitions",
                column: "InvoiceRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectRevenueRecognitions_ProjectId_RecognitionPeriod",
                table: "ProjectRevenueRecognitions",
                columns: new[] { "ProjectId", "RecognitionPeriod" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectRevenueRecognitions_TenantId",
                table: "ProjectRevenueRecognitions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectTaskDependencies_ProjectId_PredecessorWorkItemId_SuccessorWorkItemId",
                table: "ProjectTaskDependencies",
                columns: new[] { "ProjectId", "PredecessorWorkItemId", "SuccessorWorkItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectTaskDependencies_TenantId",
                table: "ProjectTaskDependencies",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectTimesheetEntries_ProjectId_Status",
                table: "ProjectTimesheetEntries",
                columns: new[] { "ProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectTimesheetEntries_ProjectId_UserId_EntryDate",
                table: "ProjectTimesheetEntries",
                columns: new[] { "ProjectId", "UserId", "EntryDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectTimesheetEntries_TenantId",
                table: "ProjectTimesheetEntries",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectTimesheetEntries_WorkItemId",
                table: "ProjectTimesheetEntries",
                column: "WorkItemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectAssetLinks");

            migrationBuilder.DropTable(
                name: "ProjectBaselines");

            migrationBuilder.DropTable(
                name: "ProjectDeliverables");

            migrationBuilder.DropTable(
                name: "ProjectExpenses");

            migrationBuilder.DropTable(
                name: "ProjectExternalAccessPolicies");

            migrationBuilder.DropTable(
                name: "ProjectRevenueRecognitions");

            migrationBuilder.DropTable(
                name: "ProjectTaskDependencies");

            migrationBuilder.DropTable(
                name: "ProjectTimesheetEntries");
        }
    }
}
