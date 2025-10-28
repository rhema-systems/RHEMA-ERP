using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddQualityControlTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("2a1f0e53-06fe-4f38-9286-c7dd91e03270"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("2de78176-bfb7-44a8-9f32-ab77caf5aa2d"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("3879bc0c-dd3c-4587-95b7-a2c6dcf67b1d"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("5b4fdd71-a460-4191-815f-6168898fb903"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("6f6897aa-738f-4029-8ed1-3ae2b505c198"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("ee9d425c-b6ba-4f22-8db7-4d0e17078e38"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("fc0244e5-d779-4da3-8f0d-5b73d3bdfa1e"));

            migrationBuilder.CreateTable(
                name: "InspectionApprovals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InspectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovalLevel = table.Column<int>(type: "int", nullable: false),
                    ApproverId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApproverRole = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RequestedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Comments = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Conditions = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    CanDelegate = table.Column<bool>(type: "bit", nullable: false),
                    DelegatedToId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DelegatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DelegationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InspectionApprovals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InspectionApprovals_AssetInspections_InspectionId",
                        column: x => x.InspectionId,
                        principalTable: "AssetInspections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InspectionApprovals_Employees_ApproverId",
                        column: x => x.ApproverId,
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_InspectionApprovals_Employees_DelegatedToId",
                        column: x => x.DelegatedToId,
                        principalTable: "Employees",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "InspectionChecklistTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ApplicableAssetTypes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ApplicableMaintenanceTypes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ApplicableWorkOrderTypes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    VersionNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_InspectionChecklistTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InspectionChecklistTemplates_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QualityControlChecklists",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    WorkOrderType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    AssetCategory = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    MaintenanceType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsMandatory = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    ChecklistItems = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MinimumPassingScore = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QualityControlChecklists", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "QualityMetrics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MetricsDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TechnicianId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssetCategory = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    TotalWorkOrders = table.Column<int>(type: "int", nullable: false),
                    FirstTimePassCount = table.Column<int>(type: "int", nullable: false),
                    ReworkCount = table.Column<int>(type: "int", nullable: false),
                    RejectedCount = table.Column<int>(type: "int", nullable: false),
                    FirstTimeFixRate = table.Column<double>(type: "float", nullable: false),
                    AverageQualityScore = table.Column<double>(type: "float", nullable: false),
                    CustomerSatisfactionScore = table.Column<double>(type: "float", nullable: true),
                    AverageInspectionTime = table.Column<double>(type: "float", nullable: false),
                    SafetyViolations = table.Column<int>(type: "int", nullable: false),
                    CompliancePercentage = table.Column<double>(type: "float", nullable: false),
                    CalculatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QualityMetrics", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkOrderQualitySignOffs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SignOffLevel = table.Column<int>(type: "int", nullable: false),
                    SignOffRole = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SignOffById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SignOffDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Comments = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Conditions = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    QualityRating = table.Column<int>(type: "int", nullable: true),
                    SafetyCompliant = table.Column<bool>(type: "bit", nullable: false),
                    WorkmanshipSatisfactory = table.Column<bool>(type: "bit", nullable: false),
                    MaterialsAcceptable = table.Column<bool>(type: "bit", nullable: false),
                    TestingComplete = table.Column<bool>(type: "bit", nullable: false),
                    DocumentationComplete = table.Column<bool>(type: "bit", nullable: false),
                    DelegatedToId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DelegatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DelegationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PhotoPaths = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DocumentPaths = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RequiresFollowUp = table.Column<bool>(type: "bit", nullable: false),
                    FollowUpDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FollowUpInstructions = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_WorkOrderQualitySignOffs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkOrderQualitySignOffs_Employees_DelegatedToId",
                        column: x => x.DelegatedToId,
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WorkOrderQualitySignOffs_Employees_SignOffById",
                        column: x => x.SignOffById,
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WorkOrderQualitySignOffs_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkOrderQualitySignOffs_WorkOrders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "WorkOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkOrderRejections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RejectedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RejectedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RejectionType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RejectionReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ReworkAssignedToId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReworkAssignedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReworkDueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReworkCompletedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EstimatedReworkCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ActualReworkCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    EstimatedReworkHours = table.Column<double>(type: "float", nullable: false),
                    ActualReworkHours = table.Column<double>(type: "float", nullable: false),
                    CustomerNotified = table.Column<bool>(type: "bit", nullable: false),
                    CustomerNotifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AffectsDelivery = table.Column<bool>(type: "bit", nullable: false),
                    RevisedDeliveryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolutionNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ResolvedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ResolvedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RequiresReinspection = table.Column<bool>(type: "bit", nullable: false),
                    ReinspectedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReinspectedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReinspectionResult = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    PhotoPaths = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DocumentPaths = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsEscalated = table.Column<bool>(type: "bit", nullable: false),
                    EscalatedToId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EscalatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EscalationReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_WorkOrderRejections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkOrderRejections_Employees_EscalatedToId",
                        column: x => x.EscalatedToId,
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WorkOrderRejections_Employees_ReinspectedById",
                        column: x => x.ReinspectedById,
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WorkOrderRejections_Employees_RejectedById",
                        column: x => x.RejectedById,
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WorkOrderRejections_Employees_ResolvedById",
                        column: x => x.ResolvedById,
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WorkOrderRejections_Employees_ReworkAssignedToId",
                        column: x => x.ReworkAssignedToId,
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WorkOrderRejections_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkOrderRejections_WorkOrders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "WorkOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InspectionChecklistItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemText = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ItemType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    IsCritical = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    ValidationRules = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ChoiceOptions = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DefaultValue = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    HelpText = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RequiresPhoto = table.Column<bool>(type: "bit", nullable: false),
                    MinPhotos = table.Column<int>(type: "int", nullable: false),
                    MaxPhotos = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_InspectionChecklistItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InspectionChecklistItems_InspectionChecklistTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "InspectionChecklistTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InspectionChecklistItems_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkOrderQualityChecks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChecklistId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InspectorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InspectionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OverallResult = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Score = table.Column<int>(type: "int", nullable: false),
                    CheckResults = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CorrectiveActions = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RequiresFollowUp = table.Column<bool>(type: "bit", nullable: false),
                    FollowUpDueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AttachmentPaths = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkOrderQualityChecks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkOrderQualityChecks_QualityControlChecklists_ChecklistId",
                        column: x => x.ChecklistId,
                        principalTable: "QualityControlChecklists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QualitySignOffChecklists",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SignOffId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CheckItem = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CheckType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    BooleanResult = table.Column<bool>(type: "bit", nullable: true),
                    RatingResult = table.Column<int>(type: "int", nullable: true),
                    MeasurementResult = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    MeasurementUnit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    TextResult = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    PhotoPaths = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CheckedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CheckedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_QualitySignOffChecklists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QualitySignOffChecklists_Employees_CheckedById",
                        column: x => x.CheckedById,
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_QualitySignOffChecklists_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QualitySignOffChecklists_WorkOrderQualitySignOffs_SignOffId",
                        column: x => x.SignOffId,
                        principalTable: "WorkOrderQualitySignOffs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RejectionFollowUps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RejectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FollowUpAction = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AssignedToId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CompletedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CompletionNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_RejectionFollowUps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RejectionFollowUps_Employees_AssignedToId",
                        column: x => x.AssignedToId,
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RejectionFollowUps_Employees_CompletedById",
                        column: x => x.CompletedById,
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RejectionFollowUps_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RejectionFollowUps_WorkOrderRejections_RejectionId",
                        column: x => x.RejectionId,
                        principalTable: "WorkOrderRejections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkOrderReworks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InspectorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReworkReason = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AssignedTechnicianId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IdentifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TargetCompletionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StartedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EstimatedHours = table.Column<double>(type: "float", nullable: true),
                    ActualHours = table.Column<double>(type: "float", nullable: true),
                    AdditionalCost = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    AttachmentPaths = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RequiresCustomerNotification = table.Column<bool>(type: "bit", nullable: false),
                    AffectsWarranty = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkOrderQualityCheckId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkOrderReworks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkOrderReworks_WorkOrderQualityChecks_WorkOrderQualityCheckId",
                        column: x => x.WorkOrderQualityCheckId,
                        principalTable: "WorkOrderQualityChecks",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WorkOrderReworks_WorkOrders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "WorkOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkOrderReworkTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkOrderReworkId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaskDescription = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    EstimatedHours = table.Column<double>(type: "float", nullable: true),
                    ActualHours = table.Column<double>(type: "float", nullable: true),
                    CompletedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkOrderReworkTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkOrderReworkTasks_WorkOrderReworks_WorkOrderReworkId",
                        column: x => x.WorkOrderReworkId,
                        principalTable: "WorkOrderReworks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 17, 6, 11, 58, 220, DateTimeKind.Utc).AddTicks(5569));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 17, 6, 11, 58, 220, DateTimeKind.Utc).AddTicks(5610));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 17, 6, 11, 58, 220, DateTimeKind.Utc).AddTicks(5613));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 17, 6, 11, 58, 220, DateTimeKind.Utc).AddTicks(5615));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 17, 6, 11, 58, 220, DateTimeKind.Utc).AddTicks(5850));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 17, 6, 11, 58, 220, DateTimeKind.Utc).AddTicks(5858));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 17, 6, 11, 58, 220, DateTimeKind.Utc).AddTicks(5862));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 17, 6, 11, 58, 220, DateTimeKind.Utc).AddTicks(5867));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000005"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 17, 6, 11, 58, 220, DateTimeKind.Utc).AddTicks(5882));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000006"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 17, 6, 11, 58, 220, DateTimeKind.Utc).AddTicks(5888));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000007"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 17, 6, 11, 58, 220, DateTimeKind.Utc).AddTicks(5893));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000008"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 17, 6, 11, 58, 220, DateTimeKind.Utc).AddTicks(5898));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000009"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 17, 6, 11, 58, 220, DateTimeKind.Utc).AddTicks(5905));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000010"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 17, 6, 11, 58, 220, DateTimeKind.Utc).AddTicks(5914));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000011"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 17, 6, 11, 58, 220, DateTimeKind.Utc).AddTicks(5920));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000012"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 17, 6, 11, 58, 220, DateTimeKind.Utc).AddTicks(5924));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000013"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 17, 6, 11, 58, 220, DateTimeKind.Utc).AddTicks(5932));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000014"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 17, 6, 11, 58, 220, DateTimeKind.Utc).AddTicks(5937));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000015"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 17, 6, 11, 58, 220, DateTimeKind.Utc).AddTicks(5946));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000016"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 17, 6, 11, 58, 220, DateTimeKind.Utc).AddTicks(5952));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 6, 11, 58, 220, DateTimeKind.Utc).AddTicks(5984));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 6, 11, 58, 220, DateTimeKind.Utc).AddTicks(5987));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 6, 11, 58, 220, DateTimeKind.Utc).AddTicks(5988));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 6, 11, 58, 220, DateTimeKind.Utc).AddTicks(5988));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 6, 11, 58, 220, DateTimeKind.Utc).AddTicks(5989));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 6, 11, 58, 220, DateTimeKind.Utc).AddTicks(5990));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 6, 11, 58, 220, DateTimeKind.Utc).AddTicks(5991));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 6, 11, 58, 220, DateTimeKind.Utc).AddTicks(5992));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 6, 11, 58, 220, DateTimeKind.Utc).AddTicks(5992));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 6, 11, 58, 220, DateTimeKind.Utc).AddTicks(5994));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 6, 11, 58, 220, DateTimeKind.Utc).AddTicks(5994));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 6, 11, 58, 220, DateTimeKind.Utc).AddTicks(5995));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 6, 11, 58, 220, DateTimeKind.Utc).AddTicks(5995));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 6, 11, 58, 220, DateTimeKind.Utc).AddTicks(5996));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 6, 11, 58, 220, DateTimeKind.Utc).AddTicks(5997));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 6, 11, 58, 220, DateTimeKind.Utc).AddTicks(5997));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 6, 11, 58, 220, DateTimeKind.Utc).AddTicks(6083));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 6, 11, 58, 220, DateTimeKind.Utc).AddTicks(6085));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 6, 11, 58, 220, DateTimeKind.Utc).AddTicks(6086));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 6, 11, 58, 220, DateTimeKind.Utc).AddTicks(6086));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 6, 11, 58, 220, DateTimeKind.Utc).AddTicks(6087));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 6, 11, 58, 220, DateTimeKind.Utc).AddTicks(6087));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 6, 11, 58, 220, DateTimeKind.Utc).AddTicks(6088));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 6, 11, 58, 220, DateTimeKind.Utc).AddTicks(6088));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 6, 11, 58, 220, DateTimeKind.Utc).AddTicks(6089));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 6, 11, 58, 220, DateTimeKind.Utc).AddTicks(6089));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 6, 11, 58, 220, DateTimeKind.Utc).AddTicks(6090));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 6, 11, 58, 220, DateTimeKind.Utc).AddTicks(6091));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 6, 11, 58, 220, DateTimeKind.Utc).AddTicks(6091));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 6, 11, 58, 220, DateTimeKind.Utc).AddTicks(6092));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 6, 11, 58, 220, DateTimeKind.Utc).AddTicks(6092));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 6, 11, 58, 220, DateTimeKind.Utc).AddTicks(6144));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 6, 11, 58, 220, DateTimeKind.Utc).AddTicks(6145));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 6, 11, 58, 220, DateTimeKind.Utc).AddTicks(6146));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 6, 11, 58, 220, DateTimeKind.Utc).AddTicks(6147));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 6, 11, 58, 220, DateTimeKind.Utc).AddTicks(6147));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 6, 11, 58, 220, DateTimeKind.Utc).AddTicks(6148));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 6, 11, 58, 220, DateTimeKind.Utc).AddTicks(6148));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 6, 11, 58, 220, DateTimeKind.Utc).AddTicks(6149));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 6, 11, 58, 220, DateTimeKind.Utc).AddTicks(6150));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 6, 11, 58, 220, DateTimeKind.Utc).AddTicks(6150));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 6, 11, 58, 220, DateTimeKind.Utc).AddTicks(6160));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 6, 11, 58, 220, DateTimeKind.Utc).AddTicks(6161));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 6, 11, 58, 220, DateTimeKind.Utc).AddTicks(6161));

            migrationBuilder.InsertData(
                table: "TenantModules",
                columns: new[] { "Id", "Configuration", "CreatedAt", "CreatedBy", "CreatedById", "DeletedAt", "DeletedBy", "Description", "DisabledDate", "EnabledDate", "IsDeleted", "LastModifiedById", "ModuleName", "Status", "TenantId", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid("0f23e62f-7ddd-4a4c-82d3-24e34c7ea1fb"), null, new DateTime(2025, 10, 17, 6, 11, 58, 220, DateTimeKind.Utc).AddTicks(5692), null, null, null, null, null, null, new DateTime(2025, 10, 17, 6, 11, 58, 220, DateTimeKind.Utc).AddTicks(5691), false, null, "HR", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("1f5997e9-22e8-4045-b1e6-a0daf9007ee7"), null, new DateTime(2025, 10, 17, 6, 11, 58, 220, DateTimeKind.Utc).AddTicks(5760), null, null, null, null, null, null, new DateTime(2025, 10, 17, 6, 11, 58, 220, DateTimeKind.Utc).AddTicks(5759), false, null, "Marketing", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("2d999c82-127d-4d5f-9373-baebd4205cfc"), null, new DateTime(2025, 10, 17, 6, 11, 58, 220, DateTimeKind.Utc).AddTicks(5729), null, null, null, null, null, null, new DateTime(2025, 10, 17, 6, 11, 58, 220, DateTimeKind.Utc).AddTicks(5729), false, null, "Procurement", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("45d9777e-569e-4482-8181-a808ec6157e1"), null, new DateTime(2025, 10, 17, 6, 11, 58, 220, DateTimeKind.Utc).AddTicks(5673), null, null, null, null, null, null, new DateTime(2025, 10, 17, 6, 11, 58, 220, DateTimeKind.Utc).AddTicks(5670), false, null, "Finance", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("d56f42c2-ecfe-4b59-b077-27def1898e71"), null, new DateTime(2025, 10, 17, 6, 11, 58, 220, DateTimeKind.Utc).AddTicks(5744), null, null, null, null, null, null, new DateTime(2025, 10, 17, 6, 11, 58, 220, DateTimeKind.Utc).AddTicks(5744), false, null, "Inventory", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("e7317ef8-a2f0-4d87-a46b-3d8371c5367f"), null, new DateTime(2025, 10, 17, 6, 11, 58, 220, DateTimeKind.Utc).AddTicks(5713), null, null, null, null, null, null, new DateTime(2025, 10, 17, 6, 11, 58, 220, DateTimeKind.Utc).AddTicks(5713), false, null, "Sales", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("f947323f-ef70-4e4e-998a-25bbb496aeda"), null, new DateTime(2025, 10, 17, 6, 11, 58, 220, DateTimeKind.Utc).AddTicks(5777), null, null, null, null, null, null, new DateTime(2025, 10, 17, 6, 11, 58, 220, DateTimeKind.Utc).AddTicks(5777), false, null, "WorkflowEngine", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null }
                });

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 17, 6, 11, 58, 220, DateTimeKind.Utc).AddTicks(5394));

            migrationBuilder.CreateIndex(
                name: "IX_InspectionApprovals_ApprovalLevel",
                table: "InspectionApprovals",
                column: "ApprovalLevel");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionApprovals_ApprovedDate",
                table: "InspectionApprovals",
                column: "ApprovedDate");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionApprovals_ApproverId",
                table: "InspectionApprovals",
                column: "ApproverId");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionApprovals_DelegatedToId",
                table: "InspectionApprovals",
                column: "DelegatedToId");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionApprovals_DueDate",
                table: "InspectionApprovals",
                column: "DueDate");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionApprovals_InspectionId",
                table: "InspectionApprovals",
                column: "InspectionId");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionApprovals_Priority",
                table: "InspectionApprovals",
                column: "Priority");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionApprovals_RequestedDate",
                table: "InspectionApprovals",
                column: "RequestedDate");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionApprovals_Status",
                table: "InspectionApprovals",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionChecklistItems_Category",
                table: "InspectionChecklistItems",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionChecklistItems_IsCritical",
                table: "InspectionChecklistItems",
                column: "IsCritical");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionChecklistItems_IsRequired",
                table: "InspectionChecklistItems",
                column: "IsRequired");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionChecklistItems_ItemType",
                table: "InspectionChecklistItems",
                column: "ItemType");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionChecklistItems_RequiresPhoto",
                table: "InspectionChecklistItems",
                column: "RequiresPhoto");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionChecklistItems_SortOrder",
                table: "InspectionChecklistItems",
                column: "SortOrder");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionChecklistItems_TemplateId",
                table: "InspectionChecklistItems",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionChecklistItems_TenantId",
                table: "InspectionChecklistItems",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionChecklistTemplates_Category",
                table: "InspectionChecklistTemplates",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionChecklistTemplates_IsActive",
                table: "InspectionChecklistTemplates",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionChecklistTemplates_IsDefault",
                table: "InspectionChecklistTemplates",
                column: "IsDefault");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionChecklistTemplates_Name",
                table: "InspectionChecklistTemplates",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionChecklistTemplates_SortOrder",
                table: "InspectionChecklistTemplates",
                column: "SortOrder");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionChecklistTemplates_TenantId",
                table: "InspectionChecklistTemplates",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionChecklistTemplates_Version",
                table: "InspectionChecklistTemplates",
                column: "Version");

            migrationBuilder.CreateIndex(
                name: "IX_QualityControlChecklists_AssetCategory",
                table: "QualityControlChecklists",
                column: "AssetCategory");

            migrationBuilder.CreateIndex(
                name: "IX_QualityControlChecklists_IsActive",
                table: "QualityControlChecklists",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_QualityControlChecklists_IsMandatory",
                table: "QualityControlChecklists",
                column: "IsMandatory");

            migrationBuilder.CreateIndex(
                name: "IX_QualityControlChecklists_MaintenanceType",
                table: "QualityControlChecklists",
                column: "MaintenanceType");

            migrationBuilder.CreateIndex(
                name: "IX_QualityControlChecklists_Name",
                table: "QualityControlChecklists",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_QualityControlChecklists_Version",
                table: "QualityControlChecklists",
                column: "Version");

            migrationBuilder.CreateIndex(
                name: "IX_QualityControlChecklists_WorkOrderType",
                table: "QualityControlChecklists",
                column: "WorkOrderType");

            migrationBuilder.CreateIndex(
                name: "IX_QualityMetrics_AssetCategory",
                table: "QualityMetrics",
                column: "AssetCategory");

            migrationBuilder.CreateIndex(
                name: "IX_QualityMetrics_CalculatedDate",
                table: "QualityMetrics",
                column: "CalculatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_QualityMetrics_MetricsDate",
                table: "QualityMetrics",
                column: "MetricsDate");

            migrationBuilder.CreateIndex(
                name: "IX_QualityMetrics_TeamId",
                table: "QualityMetrics",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_QualityMetrics_TechnicianId",
                table: "QualityMetrics",
                column: "TechnicianId");

            migrationBuilder.CreateIndex(
                name: "IX_QualitySignOffChecklists_Category",
                table: "QualitySignOffChecklists",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_QualitySignOffChecklists_CheckedById",
                table: "QualitySignOffChecklists",
                column: "CheckedById");

            migrationBuilder.CreateIndex(
                name: "IX_QualitySignOffChecklists_CheckedDate",
                table: "QualitySignOffChecklists",
                column: "CheckedDate");

            migrationBuilder.CreateIndex(
                name: "IX_QualitySignOffChecklists_CheckType",
                table: "QualitySignOffChecklists",
                column: "CheckType");

            migrationBuilder.CreateIndex(
                name: "IX_QualitySignOffChecklists_IsRequired",
                table: "QualitySignOffChecklists",
                column: "IsRequired");

            migrationBuilder.CreateIndex(
                name: "IX_QualitySignOffChecklists_SignOffId",
                table: "QualitySignOffChecklists",
                column: "SignOffId");

            migrationBuilder.CreateIndex(
                name: "IX_QualitySignOffChecklists_SortOrder",
                table: "QualitySignOffChecklists",
                column: "SortOrder");

            migrationBuilder.CreateIndex(
                name: "IX_QualitySignOffChecklists_TenantId",
                table: "QualitySignOffChecklists",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_RejectionFollowUps_AssignedToId",
                table: "RejectionFollowUps",
                column: "AssignedToId");

            migrationBuilder.CreateIndex(
                name: "IX_RejectionFollowUps_CompletedById",
                table: "RejectionFollowUps",
                column: "CompletedById");

            migrationBuilder.CreateIndex(
                name: "IX_RejectionFollowUps_CompletedDate",
                table: "RejectionFollowUps",
                column: "CompletedDate");

            migrationBuilder.CreateIndex(
                name: "IX_RejectionFollowUps_DueDate",
                table: "RejectionFollowUps",
                column: "DueDate");

            migrationBuilder.CreateIndex(
                name: "IX_RejectionFollowUps_Priority",
                table: "RejectionFollowUps",
                column: "Priority");

            migrationBuilder.CreateIndex(
                name: "IX_RejectionFollowUps_RejectionId",
                table: "RejectionFollowUps",
                column: "RejectionId");

            migrationBuilder.CreateIndex(
                name: "IX_RejectionFollowUps_Status",
                table: "RejectionFollowUps",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_RejectionFollowUps_TenantId",
                table: "RejectionFollowUps",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderQualityChecks_ChecklistId",
                table: "WorkOrderQualityChecks",
                column: "ChecklistId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderQualityChecks_InspectionDate",
                table: "WorkOrderQualityChecks",
                column: "InspectionDate");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderQualityChecks_InspectorId",
                table: "WorkOrderQualityChecks",
                column: "InspectorId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderQualityChecks_OverallResult",
                table: "WorkOrderQualityChecks",
                column: "OverallResult");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderQualityChecks_RequiresFollowUp",
                table: "WorkOrderQualityChecks",
                column: "RequiresFollowUp");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderQualityChecks_Score",
                table: "WorkOrderQualityChecks",
                column: "Score");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderQualityChecks_WorkOrderId",
                table: "WorkOrderQualityChecks",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderQualitySignOffs_DelegatedToId",
                table: "WorkOrderQualitySignOffs",
                column: "DelegatedToId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderQualitySignOffs_IsRequired",
                table: "WorkOrderQualitySignOffs",
                column: "IsRequired");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderQualitySignOffs_SignOffById",
                table: "WorkOrderQualitySignOffs",
                column: "SignOffById");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderQualitySignOffs_SignOffDate",
                table: "WorkOrderQualitySignOffs",
                column: "SignOffDate");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderQualitySignOffs_SignOffLevel",
                table: "WorkOrderQualitySignOffs",
                column: "SignOffLevel");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderQualitySignOffs_SignOffRole",
                table: "WorkOrderQualitySignOffs",
                column: "SignOffRole");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderQualitySignOffs_SortOrder",
                table: "WorkOrderQualitySignOffs",
                column: "SortOrder");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderQualitySignOffs_Status",
                table: "WorkOrderQualitySignOffs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderQualitySignOffs_TenantId",
                table: "WorkOrderQualitySignOffs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderQualitySignOffs_WorkOrderId",
                table: "WorkOrderQualitySignOffs",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderRejections_EscalatedToId",
                table: "WorkOrderRejections",
                column: "EscalatedToId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderRejections_IsEscalated",
                table: "WorkOrderRejections",
                column: "IsEscalated");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderRejections_ReinspectedById",
                table: "WorkOrderRejections",
                column: "ReinspectedById");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderRejections_RejectedById",
                table: "WorkOrderRejections",
                column: "RejectedById");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderRejections_RejectedDate",
                table: "WorkOrderRejections",
                column: "RejectedDate");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderRejections_RejectionType",
                table: "WorkOrderRejections",
                column: "RejectionType");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderRejections_RequiresReinspection",
                table: "WorkOrderRejections",
                column: "RequiresReinspection");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderRejections_ResolvedById",
                table: "WorkOrderRejections",
                column: "ResolvedById");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderRejections_ReworkAssignedToId",
                table: "WorkOrderRejections",
                column: "ReworkAssignedToId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderRejections_Severity",
                table: "WorkOrderRejections",
                column: "Severity");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderRejections_Status",
                table: "WorkOrderRejections",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderRejections_TenantId",
                table: "WorkOrderRejections",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderRejections_WorkOrderId",
                table: "WorkOrderRejections",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderReworks_AssignedTechnicianId",
                table: "WorkOrderReworks",
                column: "AssignedTechnicianId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderReworks_IdentifiedDate",
                table: "WorkOrderReworks",
                column: "IdentifiedDate");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderReworks_InspectorId",
                table: "WorkOrderReworks",
                column: "InspectorId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderReworks_ReworkReason",
                table: "WorkOrderReworks",
                column: "ReworkReason");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderReworks_Severity",
                table: "WorkOrderReworks",
                column: "Severity");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderReworks_Status",
                table: "WorkOrderReworks",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderReworks_TargetCompletionDate",
                table: "WorkOrderReworks",
                column: "TargetCompletionDate");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderReworks_WorkOrderId",
                table: "WorkOrderReworks",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderReworks_WorkOrderQualityCheckId",
                table: "WorkOrderReworks",
                column: "WorkOrderQualityCheckId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderReworkTasks_CompletedById",
                table: "WorkOrderReworkTasks",
                column: "CompletedById");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderReworkTasks_CompletedDate",
                table: "WorkOrderReworkTasks",
                column: "CompletedDate");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderReworkTasks_Sequence",
                table: "WorkOrderReworkTasks",
                column: "Sequence");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderReworkTasks_Status",
                table: "WorkOrderReworkTasks",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderReworkTasks_WorkOrderReworkId",
                table: "WorkOrderReworkTasks",
                column: "WorkOrderReworkId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InspectionApprovals");

            migrationBuilder.DropTable(
                name: "InspectionChecklistItems");

            migrationBuilder.DropTable(
                name: "QualityMetrics");

            migrationBuilder.DropTable(
                name: "QualitySignOffChecklists");

            migrationBuilder.DropTable(
                name: "RejectionFollowUps");

            migrationBuilder.DropTable(
                name: "WorkOrderReworkTasks");

            migrationBuilder.DropTable(
                name: "InspectionChecklistTemplates");

            migrationBuilder.DropTable(
                name: "WorkOrderQualitySignOffs");

            migrationBuilder.DropTable(
                name: "WorkOrderRejections");

            migrationBuilder.DropTable(
                name: "WorkOrderReworks");

            migrationBuilder.DropTable(
                name: "WorkOrderQualityChecks");

            migrationBuilder.DropTable(
                name: "QualityControlChecklists");

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("0f23e62f-7ddd-4a4c-82d3-24e34c7ea1fb"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("1f5997e9-22e8-4045-b1e6-a0daf9007ee7"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("2d999c82-127d-4d5f-9373-baebd4205cfc"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("45d9777e-569e-4482-8181-a808ec6157e1"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("d56f42c2-ecfe-4b59-b077-27def1898e71"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("e7317ef8-a2f0-4d87-a46b-3d8371c5367f"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("f947323f-ef70-4e4e-998a-25bbb496aeda"));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 17, 0, 38, 54, 311, DateTimeKind.Utc).AddTicks(4886));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 17, 0, 38, 54, 311, DateTimeKind.Utc).AddTicks(4944));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 17, 0, 38, 54, 311, DateTimeKind.Utc).AddTicks(4946));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 17, 0, 38, 54, 311, DateTimeKind.Utc).AddTicks(4948));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 17, 0, 38, 54, 311, DateTimeKind.Utc).AddTicks(5178));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 17, 0, 38, 54, 311, DateTimeKind.Utc).AddTicks(5197));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 17, 0, 38, 54, 311, DateTimeKind.Utc).AddTicks(5203));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 17, 0, 38, 54, 311, DateTimeKind.Utc).AddTicks(5209));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000005"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 17, 0, 38, 54, 311, DateTimeKind.Utc).AddTicks(5218));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000006"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 17, 0, 38, 54, 311, DateTimeKind.Utc).AddTicks(5224));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000007"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 17, 0, 38, 54, 311, DateTimeKind.Utc).AddTicks(5229));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000008"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 17, 0, 38, 54, 311, DateTimeKind.Utc).AddTicks(5234));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000009"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 17, 0, 38, 54, 311, DateTimeKind.Utc).AddTicks(5242));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000010"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 17, 0, 38, 54, 311, DateTimeKind.Utc).AddTicks(5251));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000011"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 17, 0, 38, 54, 311, DateTimeKind.Utc).AddTicks(5257));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000012"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 17, 0, 38, 54, 311, DateTimeKind.Utc).AddTicks(5263));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000013"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 17, 0, 38, 54, 311, DateTimeKind.Utc).AddTicks(5271));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000014"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 17, 0, 38, 54, 311, DateTimeKind.Utc).AddTicks(5283));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000015"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 17, 0, 38, 54, 311, DateTimeKind.Utc).AddTicks(5288));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000016"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 17, 0, 38, 54, 311, DateTimeKind.Utc).AddTicks(5292));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 0, 38, 54, 311, DateTimeKind.Utc).AddTicks(5331));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 0, 38, 54, 311, DateTimeKind.Utc).AddTicks(5337));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 0, 38, 54, 311, DateTimeKind.Utc).AddTicks(5338));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 0, 38, 54, 311, DateTimeKind.Utc).AddTicks(5339));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 0, 38, 54, 311, DateTimeKind.Utc).AddTicks(5339));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 0, 38, 54, 311, DateTimeKind.Utc).AddTicks(5341));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 0, 38, 54, 311, DateTimeKind.Utc).AddTicks(5341));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 0, 38, 54, 311, DateTimeKind.Utc).AddTicks(5342));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 0, 38, 54, 311, DateTimeKind.Utc).AddTicks(5343));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 0, 38, 54, 311, DateTimeKind.Utc).AddTicks(5351));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 0, 38, 54, 311, DateTimeKind.Utc).AddTicks(5352));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 0, 38, 54, 311, DateTimeKind.Utc).AddTicks(5352));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 0, 38, 54, 311, DateTimeKind.Utc).AddTicks(5353));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 0, 38, 54, 311, DateTimeKind.Utc).AddTicks(5354));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 0, 38, 54, 311, DateTimeKind.Utc).AddTicks(5354));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 0, 38, 54, 311, DateTimeKind.Utc).AddTicks(5355));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 0, 38, 54, 311, DateTimeKind.Utc).AddTicks(5450));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 0, 38, 54, 311, DateTimeKind.Utc).AddTicks(5452));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 0, 38, 54, 311, DateTimeKind.Utc).AddTicks(5453));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 0, 38, 54, 311, DateTimeKind.Utc).AddTicks(5453));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 0, 38, 54, 311, DateTimeKind.Utc).AddTicks(5454));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 0, 38, 54, 311, DateTimeKind.Utc).AddTicks(5454));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 0, 38, 54, 311, DateTimeKind.Utc).AddTicks(5455));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 0, 38, 54, 311, DateTimeKind.Utc).AddTicks(5456));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 0, 38, 54, 311, DateTimeKind.Utc).AddTicks(5456));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 0, 38, 54, 311, DateTimeKind.Utc).AddTicks(5457));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 0, 38, 54, 311, DateTimeKind.Utc).AddTicks(5457));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 0, 38, 54, 311, DateTimeKind.Utc).AddTicks(5458));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 0, 38, 54, 311, DateTimeKind.Utc).AddTicks(5459));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 0, 38, 54, 311, DateTimeKind.Utc).AddTicks(5459));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 0, 38, 54, 311, DateTimeKind.Utc).AddTicks(5460));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 0, 38, 54, 311, DateTimeKind.Utc).AddTicks(5528));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 0, 38, 54, 311, DateTimeKind.Utc).AddTicks(5529));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 0, 38, 54, 311, DateTimeKind.Utc).AddTicks(5531));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 0, 38, 54, 311, DateTimeKind.Utc).AddTicks(5531));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 0, 38, 54, 311, DateTimeKind.Utc).AddTicks(5532));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 0, 38, 54, 311, DateTimeKind.Utc).AddTicks(5533));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 0, 38, 54, 311, DateTimeKind.Utc).AddTicks(5533));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 0, 38, 54, 311, DateTimeKind.Utc).AddTicks(5534));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 0, 38, 54, 311, DateTimeKind.Utc).AddTicks(5535));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 0, 38, 54, 311, DateTimeKind.Utc).AddTicks(5535));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 0, 38, 54, 311, DateTimeKind.Utc).AddTicks(5546));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 0, 38, 54, 311, DateTimeKind.Utc).AddTicks(5547));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 17, 0, 38, 54, 311, DateTimeKind.Utc).AddTicks(5548));

            migrationBuilder.InsertData(
                table: "TenantModules",
                columns: new[] { "Id", "Configuration", "CreatedAt", "CreatedBy", "CreatedById", "DeletedAt", "DeletedBy", "Description", "DisabledDate", "EnabledDate", "IsDeleted", "LastModifiedById", "ModuleName", "Status", "TenantId", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid("2a1f0e53-06fe-4f38-9286-c7dd91e03270"), null, new DateTime(2025, 10, 17, 0, 38, 54, 311, DateTimeKind.Utc).AddTicks(5072), null, null, null, null, null, null, new DateTime(2025, 10, 17, 0, 38, 54, 311, DateTimeKind.Utc).AddTicks(5071), false, null, "Inventory", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("2de78176-bfb7-44a8-9f32-ab77caf5aa2d"), null, new DateTime(2025, 10, 17, 0, 38, 54, 311, DateTimeKind.Utc).AddTicks(5043), null, null, null, null, null, null, new DateTime(2025, 10, 17, 0, 38, 54, 311, DateTimeKind.Utc).AddTicks(5042), false, null, "Sales", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("3879bc0c-dd3c-4587-95b7-a2c6dcf67b1d"), null, new DateTime(2025, 10, 17, 0, 38, 54, 311, DateTimeKind.Utc).AddTicks(5013), null, null, null, null, null, null, new DateTime(2025, 10, 17, 0, 38, 54, 311, DateTimeKind.Utc).AddTicks(5010), false, null, "Finance", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("5b4fdd71-a460-4191-815f-6168898fb903"), null, new DateTime(2025, 10, 17, 0, 38, 54, 311, DateTimeKind.Utc).AddTicks(5057), null, null, null, null, null, null, new DateTime(2025, 10, 17, 0, 38, 54, 311, DateTimeKind.Utc).AddTicks(5056), false, null, "Procurement", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("6f6897aa-738f-4029-8ed1-3ae2b505c198"), null, new DateTime(2025, 10, 17, 0, 38, 54, 311, DateTimeKind.Utc).AddTicks(5029), null, null, null, null, null, null, new DateTime(2025, 10, 17, 0, 38, 54, 311, DateTimeKind.Utc).AddTicks(5028), false, null, "HR", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("ee9d425c-b6ba-4f22-8db7-4d0e17078e38"), null, new DateTime(2025, 10, 17, 0, 38, 54, 311, DateTimeKind.Utc).AddTicks(5086), null, null, null, null, null, null, new DateTime(2025, 10, 17, 0, 38, 54, 311, DateTimeKind.Utc).AddTicks(5085), false, null, "Marketing", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("fc0244e5-d779-4da3-8f0d-5b73d3bdfa1e"), null, new DateTime(2025, 10, 17, 0, 38, 54, 311, DateTimeKind.Utc).AddTicks(5108), null, null, null, null, null, null, new DateTime(2025, 10, 17, 0, 38, 54, 311, DateTimeKind.Utc).AddTicks(5107), false, null, "WorkflowEngine", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null }
                });

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 17, 0, 38, 54, 311, DateTimeKind.Utc).AddTicks(4664));
        }
    }
}
