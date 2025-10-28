using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddContractorIdToWorkOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkOrders_Employees_EmployeeId",
                table: "WorkOrders");

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("1fd11209-dc35-4750-8e23-3198f1ed133f"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("588b6773-6a6a-4aa8-ba0c-1e8ead7f4ec4"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("6b13348a-36d4-4d6b-99d3-14d8a377bc4f"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("752d4b26-a221-4c6d-af27-fdaeca22eaad"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("d68e0df1-7fbf-424e-995e-b09d433ad1e3"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("ef24feea-c93b-462e-b089-42362142da3a"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("fd7656a2-1f6a-442f-a6bf-f15c8542c0dd"));

            migrationBuilder.RenameColumn(
                name: "EmployeeId",
                table: "WorkOrders",
                newName: "SupervisorId");

            migrationBuilder.RenameIndex(
                name: "IX_WorkOrders_EmployeeId",
                table: "WorkOrders",
                newName: "IX_WorkOrders_SupervisorId");

            migrationBuilder.AddColumn<Guid>(
                name: "CompletedById",
                table: "WorkOrders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ContractorId",
                table: "WorkOrders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "QualityCheckedById",
                table: "WorkOrders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Category",
                table: "MaintenanceTypes",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AddColumn<string>(
                name: "ApplicableAssetTypes",
                table: "MaintenanceTypes",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ApprovalLevels",
                table: "MaintenanceTypes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "AverageCompletionHours",
                table: "MaintenanceTypes",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<decimal>(
                name: "AverageCost",
                table: "MaintenanceTypes",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ChecklistTemplate",
                table: "MaintenanceTypes",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConditionCriteria",
                table: "MaintenanceTypes",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Criticality",
                table: "MaintenanceTypes",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "CycleTrigger",
                table: "MaintenanceTypes",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DefaultPriority",
                table: "MaintenanceTypes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DowntimeMinutes",
                table: "MaintenanceTypes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "EstimatedCost",
                table: "MaintenanceTypes",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<double>(
                name: "EstimatedHours",
                table: "MaintenanceTypes",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "FrequencyDays",
                table: "MaintenanceTypes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FrequencyMonths",
                table: "MaintenanceTypes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FrequencyWeeks",
                table: "MaintenanceTypes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "HoursTrigger",
                table: "MaintenanceTypes",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Icon",
                table: "MaintenanceTypes",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsConditionBased",
                table: "MaintenanceTypes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsTimeBased",
                table: "MaintenanceTypes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsUsageBased",
                table: "MaintenanceTypes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastPerformanceUpdate",
                table: "MaintenanceTypes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LeadTimeDays",
                table: "MaintenanceTypes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Location",
                table: "MaintenanceTypes",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MaintenanceClass",
                table: "MaintenanceTypes",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "MileageTrigger",
                table: "MaintenanceTypes",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PartsTemplate",
                table: "MaintenanceTypes",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequiredSkills",
                table: "MaintenanceTypes",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequiredTools",
                table: "MaintenanceTypes",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresApproval",
                table: "MaintenanceTypes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresCertification",
                table: "MaintenanceTypes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresDocumentation",
                table: "MaintenanceTypes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresQualityCheck",
                table: "MaintenanceTypes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresSafetyPermit",
                table: "MaintenanceTypes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresShutdown",
                table: "MaintenanceTypes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresSpecialTraining",
                table: "MaintenanceTypes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SafetyRequirements",
                table: "MaintenanceTypes",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SchedulingRules",
                table: "MaintenanceTypes",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "MaintenanceTypes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "TaskTemplate",
                table: "MaintenanceTypes",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConditionCriteria",
                table: "MaintenanceSchedules",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConditionDataSources",
                table: "MaintenanceSchedules",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CycleTrigger",
                table: "MaintenanceSchedules",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastConditionCheckDate",
                table: "MaintenanceSchedules",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "LastConditionCheckResult",
                table: "MaintenanceSchedules",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LastUsageValue",
                table: "MaintenanceSchedules",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "MaintenanceTypeId1",
                table: "MaintenanceSchedules",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MileageTrigger",
                table: "MaintenanceSchedules",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OperatingHoursTrigger",
                table: "MaintenanceSchedules",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrimaryTriggerType",
                table: "MaintenanceSchedules",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SecondaryTriggerType",
                table: "MaintenanceSchedules",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartDate",
                table: "MaintenanceSchedules",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "TriggerLogic",
                table: "MaintenanceSchedules",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "UsageUnit",
                table: "MaintenanceSchedules",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MaintenanceContractor",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ContractorCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ContactInfo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Capabilities = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ServiceAreas = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Rating = table.Column<double>(type: "float", nullable: true),
                    LicenseInfo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InsuranceInfo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceContractor", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ContractorInvoice",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    InvoiceDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    LineItems = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AttachmentPaths = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ApprovedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PaidDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MaintenanceContractorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractorInvoice", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractorInvoice_MaintenanceContractor_MaintenanceContractorId",
                        column: x => x.MaintenanceContractorId,
                        principalTable: "MaintenanceContractor",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ContractorPerformanceReview",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OverallRating = table.Column<int>(type: "int", nullable: false),
                    QualityRating = table.Column<int>(type: "int", nullable: true),
                    TimelinessRating = table.Column<int>(type: "int", nullable: true),
                    CommunicationRating = table.Column<int>(type: "int", nullable: true),
                    CostRating = table.Column<int>(type: "int", nullable: true),
                    Comments = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Recommendations = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    WouldRecommend = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractorPerformanceReview", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractorPerformanceReview_MaintenanceContractor_ContractorId",
                        column: x => x.ContractorId,
                        principalTable: "MaintenanceContractor",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ContractorWorkOrder",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    EstimatedCost = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ActualCost = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    WorkPerformed = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    PartsUsed = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    QualityRating = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractorWorkOrder", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractorWorkOrder_MaintenanceContractor_ContractorId",
                        column: x => x.ContractorId,
                        principalTable: "MaintenanceContractor",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ContractorWorkOrder_WorkOrders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "WorkOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "JobCard",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JobCardNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MaintenanceTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PriorityLevelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ProblemDescription = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    MaintenanceLocation = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RequestedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RequiredCompletionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EstimatedHours = table.Column<double>(type: "float", nullable: false),
                    EstimatedCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PreferredTechnicianId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PreferredTeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ContractorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RequiresSpecialTools = table.Column<bool>(type: "bit", nullable: false),
                    RequiresShutdown = table.Column<bool>(type: "bit", nullable: false),
                    RequiresSafetyPermit = table.Column<bool>(type: "bit", nullable: false),
                    SpecialInstructions = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SafetyRequirements = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    JobCardStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ApprovalStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SubmittedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubmittedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovalComments = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    WorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PlannedStartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PlannedEndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AssignedTechnicianId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssignedTeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    GeneratedWorkOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WorkOrderGeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AttachmentPaths = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CustomFieldValues = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_JobCard", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobCard_Employees_AssignedTechnicianId",
                        column: x => x.AssignedTechnicianId,
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_JobCard_Employees_PreferredTechnicianId",
                        column: x => x.PreferredTechnicianId,
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_JobCard_Employees_RequestedById",
                        column: x => x.RequestedById,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JobCard_MaintenanceAssets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "MaintenanceAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JobCard_MaintenanceContractor_ContractorId",
                        column: x => x.ContractorId,
                        principalTable: "MaintenanceContractor",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_JobCard_MaintenanceTypes_MaintenanceTypeId",
                        column: x => x.MaintenanceTypeId,
                        principalTable: "MaintenanceTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JobCard_PriorityLevels_PriorityLevelId",
                        column: x => x.PriorityLevelId,
                        principalTable: "PriorityLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JobCard_TechnicianTeams_AssignedTeamId",
                        column: x => x.AssignedTeamId,
                        principalTable: "TechnicianTeams",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_JobCard_TechnicianTeams_PreferredTeamId",
                        column: x => x.PreferredTeamId,
                        principalTable: "TechnicianTeams",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_JobCard_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JobCard_WorkOrders_GeneratedWorkOrderId",
                        column: x => x.GeneratedWorkOrderId,
                        principalTable: "WorkOrders",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "JobCardApprovalStep",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JobCardId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StepOrder = table.Column<int>(type: "int", nullable: false),
                    StepName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ApproverId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ActionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Comments = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_JobCardApprovalStep", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobCardApprovalStep_Employees_ApproverId",
                        column: x => x.ApproverId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_JobCardApprovalStep_JobCard_JobCardId",
                        column: x => x.JobCardId,
                        principalTable: "JobCard",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_JobCardApprovalStep_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "JobCardComment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JobCardId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CommentById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CommentType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsInternal = table.Column<bool>(type: "bit", nullable: false),
                    CommentDate = table.Column<DateTime>(type: "datetime2", nullable: false),
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
                    table.PrimaryKey("PK_JobCardComment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobCardComment_Employees_CommentById",
                        column: x => x.CommentById,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_JobCardComment_JobCard_JobCardId",
                        column: x => x.JobCardId,
                        principalTable: "JobCard",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_JobCardComment_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "JobCardDocument",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JobCardId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UploadedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UploadedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
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
                    table.PrimaryKey("PK_JobCardDocument", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobCardDocument_Employees_UploadedById",
                        column: x => x.UploadedById,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_JobCardDocument_JobCard_JobCardId",
                        column: x => x.JobCardId,
                        principalTable: "JobCard",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_JobCardDocument_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

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

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_CompletedById",
                table: "WorkOrders",
                column: "CompletedById");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_ContractorId",
                table: "WorkOrders",
                column: "ContractorId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_QualityCheckedById",
                table: "WorkOrders",
                column: "QualityCheckedById");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceSchedules_MaintenanceTypeId1",
                table: "MaintenanceSchedules",
                column: "MaintenanceTypeId1");

            migrationBuilder.CreateIndex(
                name: "IX_ContractorInvoice_MaintenanceContractorId",
                table: "ContractorInvoice",
                column: "MaintenanceContractorId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractorPerformanceReview_ContractorId",
                table: "ContractorPerformanceReview",
                column: "ContractorId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractorWorkOrder_ContractorId",
                table: "ContractorWorkOrder",
                column: "ContractorId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractorWorkOrder_WorkOrderId",
                table: "ContractorWorkOrder",
                column: "WorkOrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobCard_AssetId",
                table: "JobCard",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_JobCard_AssignedTeamId",
                table: "JobCard",
                column: "AssignedTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_JobCard_AssignedTechnicianId",
                table: "JobCard",
                column: "AssignedTechnicianId");

            migrationBuilder.CreateIndex(
                name: "IX_JobCard_ContractorId",
                table: "JobCard",
                column: "ContractorId");

            migrationBuilder.CreateIndex(
                name: "IX_JobCard_GeneratedWorkOrderId",
                table: "JobCard",
                column: "GeneratedWorkOrderId",
                unique: true,
                filter: "[GeneratedWorkOrderId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_JobCard_MaintenanceTypeId",
                table: "JobCard",
                column: "MaintenanceTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_JobCard_PreferredTeamId",
                table: "JobCard",
                column: "PreferredTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_JobCard_PreferredTechnicianId",
                table: "JobCard",
                column: "PreferredTechnicianId");

            migrationBuilder.CreateIndex(
                name: "IX_JobCard_PriorityLevelId",
                table: "JobCard",
                column: "PriorityLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_JobCard_RequestedById",
                table: "JobCard",
                column: "RequestedById");

            migrationBuilder.CreateIndex(
                name: "IX_JobCard_TenantId",
                table: "JobCard",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_JobCardApprovalStep_ApproverId",
                table: "JobCardApprovalStep",
                column: "ApproverId");

            migrationBuilder.CreateIndex(
                name: "IX_JobCardApprovalStep_JobCardId",
                table: "JobCardApprovalStep",
                column: "JobCardId");

            migrationBuilder.CreateIndex(
                name: "IX_JobCardApprovalStep_TenantId",
                table: "JobCardApprovalStep",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_JobCardComment_CommentById",
                table: "JobCardComment",
                column: "CommentById");

            migrationBuilder.CreateIndex(
                name: "IX_JobCardComment_JobCardId",
                table: "JobCardComment",
                column: "JobCardId");

            migrationBuilder.CreateIndex(
                name: "IX_JobCardComment_TenantId",
                table: "JobCardComment",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_JobCardDocument_JobCardId",
                table: "JobCardDocument",
                column: "JobCardId");

            migrationBuilder.CreateIndex(
                name: "IX_JobCardDocument_TenantId",
                table: "JobCardDocument",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_JobCardDocument_UploadedById",
                table: "JobCardDocument",
                column: "UploadedById");

            migrationBuilder.AddForeignKey(
                name: "FK_MaintenanceSchedules_MaintenanceTypes_MaintenanceTypeId1",
                table: "MaintenanceSchedules",
                column: "MaintenanceTypeId1",
                principalTable: "MaintenanceTypes",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkOrders_Employees_CompletedById",
                table: "WorkOrders",
                column: "CompletedById",
                principalTable: "Employees",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkOrders_Employees_QualityCheckedById",
                table: "WorkOrders",
                column: "QualityCheckedById",
                principalTable: "Employees",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkOrders_Employees_SupervisorId",
                table: "WorkOrders",
                column: "SupervisorId",
                principalTable: "Employees",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkOrders_MaintenanceContractor_ContractorId",
                table: "WorkOrders",
                column: "ContractorId",
                principalTable: "MaintenanceContractor",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MaintenanceSchedules_MaintenanceTypes_MaintenanceTypeId1",
                table: "MaintenanceSchedules");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkOrders_Employees_CompletedById",
                table: "WorkOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkOrders_Employees_QualityCheckedById",
                table: "WorkOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkOrders_Employees_SupervisorId",
                table: "WorkOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkOrders_MaintenanceContractor_ContractorId",
                table: "WorkOrders");

            migrationBuilder.DropTable(
                name: "ContractorInvoice");

            migrationBuilder.DropTable(
                name: "ContractorPerformanceReview");

            migrationBuilder.DropTable(
                name: "ContractorWorkOrder");

            migrationBuilder.DropTable(
                name: "JobCardApprovalStep");

            migrationBuilder.DropTable(
                name: "JobCardComment");

            migrationBuilder.DropTable(
                name: "JobCardDocument");

            migrationBuilder.DropTable(
                name: "JobCard");

            migrationBuilder.DropTable(
                name: "MaintenanceContractor");

            migrationBuilder.DropIndex(
                name: "IX_WorkOrders_CompletedById",
                table: "WorkOrders");

            migrationBuilder.DropIndex(
                name: "IX_WorkOrders_ContractorId",
                table: "WorkOrders");

            migrationBuilder.DropIndex(
                name: "IX_WorkOrders_QualityCheckedById",
                table: "WorkOrders");

            migrationBuilder.DropIndex(
                name: "IX_MaintenanceSchedules_MaintenanceTypeId1",
                table: "MaintenanceSchedules");

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

            migrationBuilder.DropColumn(
                name: "CompletedById",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "ContractorId",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "QualityCheckedById",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "ApplicableAssetTypes",
                table: "MaintenanceTypes");

            migrationBuilder.DropColumn(
                name: "ApprovalLevels",
                table: "MaintenanceTypes");

            migrationBuilder.DropColumn(
                name: "AverageCompletionHours",
                table: "MaintenanceTypes");

            migrationBuilder.DropColumn(
                name: "AverageCost",
                table: "MaintenanceTypes");

            migrationBuilder.DropColumn(
                name: "ChecklistTemplate",
                table: "MaintenanceTypes");

            migrationBuilder.DropColumn(
                name: "ConditionCriteria",
                table: "MaintenanceTypes");

            migrationBuilder.DropColumn(
                name: "Criticality",
                table: "MaintenanceTypes");

            migrationBuilder.DropColumn(
                name: "CycleTrigger",
                table: "MaintenanceTypes");

            migrationBuilder.DropColumn(
                name: "DefaultPriority",
                table: "MaintenanceTypes");

            migrationBuilder.DropColumn(
                name: "DowntimeMinutes",
                table: "MaintenanceTypes");

            migrationBuilder.DropColumn(
                name: "EstimatedCost",
                table: "MaintenanceTypes");

            migrationBuilder.DropColumn(
                name: "EstimatedHours",
                table: "MaintenanceTypes");

            migrationBuilder.DropColumn(
                name: "FrequencyDays",
                table: "MaintenanceTypes");

            migrationBuilder.DropColumn(
                name: "FrequencyMonths",
                table: "MaintenanceTypes");

            migrationBuilder.DropColumn(
                name: "FrequencyWeeks",
                table: "MaintenanceTypes");

            migrationBuilder.DropColumn(
                name: "HoursTrigger",
                table: "MaintenanceTypes");

            migrationBuilder.DropColumn(
                name: "Icon",
                table: "MaintenanceTypes");

            migrationBuilder.DropColumn(
                name: "IsConditionBased",
                table: "MaintenanceTypes");

            migrationBuilder.DropColumn(
                name: "IsTimeBased",
                table: "MaintenanceTypes");

            migrationBuilder.DropColumn(
                name: "IsUsageBased",
                table: "MaintenanceTypes");

            migrationBuilder.DropColumn(
                name: "LastPerformanceUpdate",
                table: "MaintenanceTypes");

            migrationBuilder.DropColumn(
                name: "LeadTimeDays",
                table: "MaintenanceTypes");

            migrationBuilder.DropColumn(
                name: "Location",
                table: "MaintenanceTypes");

            migrationBuilder.DropColumn(
                name: "MaintenanceClass",
                table: "MaintenanceTypes");

            migrationBuilder.DropColumn(
                name: "MileageTrigger",
                table: "MaintenanceTypes");

            migrationBuilder.DropColumn(
                name: "PartsTemplate",
                table: "MaintenanceTypes");

            migrationBuilder.DropColumn(
                name: "RequiredSkills",
                table: "MaintenanceTypes");

            migrationBuilder.DropColumn(
                name: "RequiredTools",
                table: "MaintenanceTypes");

            migrationBuilder.DropColumn(
                name: "RequiresApproval",
                table: "MaintenanceTypes");

            migrationBuilder.DropColumn(
                name: "RequiresCertification",
                table: "MaintenanceTypes");

            migrationBuilder.DropColumn(
                name: "RequiresDocumentation",
                table: "MaintenanceTypes");

            migrationBuilder.DropColumn(
                name: "RequiresQualityCheck",
                table: "MaintenanceTypes");

            migrationBuilder.DropColumn(
                name: "RequiresSafetyPermit",
                table: "MaintenanceTypes");

            migrationBuilder.DropColumn(
                name: "RequiresShutdown",
                table: "MaintenanceTypes");

            migrationBuilder.DropColumn(
                name: "RequiresSpecialTraining",
                table: "MaintenanceTypes");

            migrationBuilder.DropColumn(
                name: "SafetyRequirements",
                table: "MaintenanceTypes");

            migrationBuilder.DropColumn(
                name: "SchedulingRules",
                table: "MaintenanceTypes");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "MaintenanceTypes");

            migrationBuilder.DropColumn(
                name: "TaskTemplate",
                table: "MaintenanceTypes");

            migrationBuilder.DropColumn(
                name: "ConditionCriteria",
                table: "MaintenanceSchedules");

            migrationBuilder.DropColumn(
                name: "ConditionDataSources",
                table: "MaintenanceSchedules");

            migrationBuilder.DropColumn(
                name: "CycleTrigger",
                table: "MaintenanceSchedules");

            migrationBuilder.DropColumn(
                name: "LastConditionCheckDate",
                table: "MaintenanceSchedules");

            migrationBuilder.DropColumn(
                name: "LastConditionCheckResult",
                table: "MaintenanceSchedules");

            migrationBuilder.DropColumn(
                name: "LastUsageValue",
                table: "MaintenanceSchedules");

            migrationBuilder.DropColumn(
                name: "MaintenanceTypeId1",
                table: "MaintenanceSchedules");

            migrationBuilder.DropColumn(
                name: "MileageTrigger",
                table: "MaintenanceSchedules");

            migrationBuilder.DropColumn(
                name: "OperatingHoursTrigger",
                table: "MaintenanceSchedules");

            migrationBuilder.DropColumn(
                name: "PrimaryTriggerType",
                table: "MaintenanceSchedules");

            migrationBuilder.DropColumn(
                name: "SecondaryTriggerType",
                table: "MaintenanceSchedules");

            migrationBuilder.DropColumn(
                name: "StartDate",
                table: "MaintenanceSchedules");

            migrationBuilder.DropColumn(
                name: "TriggerLogic",
                table: "MaintenanceSchedules");

            migrationBuilder.DropColumn(
                name: "UsageUnit",
                table: "MaintenanceSchedules");

            migrationBuilder.RenameColumn(
                name: "SupervisorId",
                table: "WorkOrders",
                newName: "EmployeeId");

            migrationBuilder.RenameIndex(
                name: "IX_WorkOrders_SupervisorId",
                table: "WorkOrders",
                newName: "IX_WorkOrders_EmployeeId");

            migrationBuilder.AlterColumn<string>(
                name: "Category",
                table: "MaintenanceTypes",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30);

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4370));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4423));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4425));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4427));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4649));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4666));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4671));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4676));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000005"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4684));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000006"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4692));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000007"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4697));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000008"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4701));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000009"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4709));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000010"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4715));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000011"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4721));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000012"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4725));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000013"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4732));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000014"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4745));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000015"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4755));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000016"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4759));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4802));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4807));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4808));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4809));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4809));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4811));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4811));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4812));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4813));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4814));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4815));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4815));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4816));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4817));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4817));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4818));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4858));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4859));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4860));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4861));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4861));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4862));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4862));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4863));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4864));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4864));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4865));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4865));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4866));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4866));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4867));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4898));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4899));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4900));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4901));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4901));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4902));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4902));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4903));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4904));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4904));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4918));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4919));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4920));

            migrationBuilder.InsertData(
                table: "TenantModules",
                columns: new[] { "Id", "Configuration", "CreatedAt", "CreatedBy", "CreatedById", "DeletedAt", "DeletedBy", "Description", "DisabledDate", "EnabledDate", "IsDeleted", "LastModifiedById", "ModuleName", "Status", "TenantId", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid("1fd11209-dc35-4750-8e23-3198f1ed133f"), null, new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4537), null, null, null, null, null, null, new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4536), false, null, "Procurement", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("588b6773-6a6a-4aa8-ba0c-1e8ead7f4ec4"), null, new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4506), null, null, null, null, null, null, new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4506), false, null, "HR", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("6b13348a-36d4-4d6b-99d3-14d8a377bc4f"), null, new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4561), null, null, null, null, null, null, new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4561), false, null, "Marketing", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("752d4b26-a221-4c6d-af27-fdaeca22eaad"), null, new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4550), null, null, null, null, null, null, new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4549), false, null, "Inventory", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("d68e0df1-7fbf-424e-995e-b09d433ad1e3"), null, new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4526), null, null, null, null, null, null, new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4525), false, null, "Sales", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("ef24feea-c93b-462e-b089-42362142da3a"), null, new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4572), null, null, null, null, null, null, new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4572), false, null, "WorkflowEngine", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("fd7656a2-1f6a-442f-a6bf-f15c8542c0dd"), null, new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4490), null, null, null, null, null, null, new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4488), false, null, "Finance", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null }
                });

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4205));

            migrationBuilder.AddForeignKey(
                name: "FK_WorkOrders_Employees_EmployeeId",
                table: "WorkOrders",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "Id");
        }
    }
}
