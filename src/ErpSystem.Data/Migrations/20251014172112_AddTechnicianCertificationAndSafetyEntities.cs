using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTechnicianCertificationAndSafetyEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkflowActivityLogs_WorkflowStepInstances_WorkflowStepInstanceId",
                table: "WorkflowActivityLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkflowApprovals_WorkflowStepInstances_WorkflowStepInstanceId",
                table: "WorkflowApprovals");

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("0aeb65ae-ec96-4dc9-8a71-73fd19561d06"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("7ad80716-15a1-418b-915c-abd613c8288c"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("880503f7-e1da-4d09-8b8a-fe6830020a52"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("a5675b88-0d25-4565-93c1-0aecb492df72"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("c350d359-7c4d-42f4-bcac-6a312702b626"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("d6eb8bdc-02f3-403d-b08e-8e32c700ddbb"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("dd947ba9-556f-4356-9fa9-9f95fb7b1638"));

            migrationBuilder.DropColumn(
                name: "EntityType",
                table: "WorkflowDefinitions");

            migrationBuilder.DropColumn(
                name: "ConditionParameters",
                table: "MaintenanceSchedules");

            migrationBuilder.DropColumn(
                name: "DayOfMonth",
                table: "MaintenanceSchedules");

            migrationBuilder.DropColumn(
                name: "IntervalValue",
                table: "MaintenanceSchedules");

            migrationBuilder.DropColumn(
                name: "PartsTemplate",
                table: "MaintenanceSchedules");

            migrationBuilder.DropColumn(
                name: "PreferredTime",
                table: "MaintenanceSchedules");

            migrationBuilder.DropColumn(
                name: "StartDate",
                table: "MaintenanceSchedules");

            migrationBuilder.DropColumn(
                name: "TaskTemplate",
                table: "MaintenanceSchedules");

            migrationBuilder.DropColumn(
                name: "UsageInterval",
                table: "MaintenanceSchedules");

            migrationBuilder.DropColumn(
                name: "UsageUnit",
                table: "MaintenanceSchedules");

            migrationBuilder.DropColumn(
                name: "WorkOrderDescription",
                table: "MaintenanceSchedules");

            migrationBuilder.DropColumn(
                name: "WorkOrderTitle",
                table: "MaintenanceSchedules");

            migrationBuilder.RenameColumn(
                name: "CreatedDate",
                table: "WorkflowTransitions",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "CreatedDate",
                table: "WorkflowSteps",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "LastActivityDate",
                table: "WorkflowInstances",
                newName: "CreatedDate");

            migrationBuilder.RenameColumn(
                name: "LastModifiedDate",
                table: "WorkflowDefinitions",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "CreatedDate",
                table: "WorkflowDefinitions",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "WorkflowStepInstanceId",
                table: "WorkflowApprovals",
                newName: "StepInstanceId");

            migrationBuilder.RenameColumn(
                name: "ResponseDate",
                table: "WorkflowApprovals",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "CreatedDate",
                table: "WorkflowApprovals",
                newName: "RequestedDate");

            migrationBuilder.RenameIndex(
                name: "IX_WorkflowApprovals_WorkflowStepInstanceId",
                table: "WorkflowApprovals",
                newName: "IX_WorkflowApprovals_StepInstanceId");

            migrationBuilder.RenameColumn(
                name: "WorkflowStepInstanceId",
                table: "WorkflowActivityLogs",
                newName: "StepInstanceId");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "WorkflowActivityLogs",
                newName: "PerformedById");

            migrationBuilder.RenameColumn(
                name: "Timestamp",
                table: "WorkflowActivityLogs",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "ActivityData",
                table: "WorkflowActivityLogs",
                newName: "UpdatedBy");

            migrationBuilder.RenameIndex(
                name: "IX_WorkflowActivityLogs_WorkflowStepInstanceId",
                table: "WorkflowActivityLogs",
                newName: "IX_WorkflowActivityLogs_StepInstanceId");

            migrationBuilder.RenameColumn(
                name: "LeadTimeDays",
                table: "MaintenanceSchedules",
                newName: "FrequencyValue");

            migrationBuilder.RenameColumn(
                name: "EndDate",
                table: "MaintenanceSchedules",
                newName: "LastProcessedDate");

            migrationBuilder.RenameColumn(
                name: "DayOfWeek",
                table: "MaintenanceSchedules",
                newName: "AdvanceNotificationDays");

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "WorkStations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "WorkStations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "WorkOrderTypes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "WorkOrderTypes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "WorkOrderTasks",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "WorkOrderTasks",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "WorkOrders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "WorkOrders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TechnicianId",
                table: "WorkOrders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "WorkOrderParts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "WorkOrderParts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "WorkOrderLabor",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "WorkOrderLabor",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "WorkOrderDocuments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "WorkOrderDocuments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "WorkOrderComments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "WorkOrderComments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "WorkflowTransitions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "WorkflowTransitions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "WorkflowTransitions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "WorkflowTransitions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "WorkflowTransitions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "WorkflowTransitions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "WorkflowTransitions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "WorkflowTransitions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WorkflowDefinitionId",
                table: "WorkflowTransitions",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AlterColumn<int>(
                name: "StepType",
                table: "WorkflowSteps",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<decimal>(
                name: "EstimatedHours",
                table: "WorkflowSteps",
                type: "decimal(5,2)",
                nullable: true,
                oldClrType: typeof(double),
                oldType: "float",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssignmentConfiguration",
                table: "WorkflowSteps",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssignmentType",
                table: "WorkflowSteps",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "WorkflowSteps",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "WorkflowSteps",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "WorkflowSteps",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "WorkflowSteps",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "WorkflowSteps",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsEndStep",
                table: "WorkflowSteps",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsStartStep",
                table: "WorkflowSteps",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "WorkflowSteps",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "WorkflowSteps",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "WorkflowSteps",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "WorkflowStepInstances",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "WorkflowStepInstances",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "WorkflowStepInstances",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "WorkflowStepInstances",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "WorkflowStepInstances",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "WorkflowStepInstances",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "WorkflowStepInstances",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "WorkflowStepInstances",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RetryCount",
                table: "WorkflowStepInstances",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "WorkflowStepInstances",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "WorkflowStepInstances",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "WorkflowInstances",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<DateTime>(
                name: "StartedDate",
                table: "WorkflowInstances",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledDate",
                table: "WorkflowInstances",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "WorkflowInstances",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "WorkflowInstances",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "WorkflowInstances",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Data",
                table: "WorkflowInstances",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "WorkflowInstances",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "WorkflowInstances",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EntityTypeId",
                table: "WorkflowInstances",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "WorkflowInstances",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "WorkflowInstances",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Priority",
                table: "WorkflowInstances",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "StartedById",
                table: "WorkflowInstances",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "WorkflowInstances",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "WorkflowInstances",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "CreatedById",
                table: "WorkflowDefinitions",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "WorkflowDefinitions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "WorkflowDefinitions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "WorkflowDefinitions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EntityTypeId",
                table: "WorkflowDefinitions",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "WorkflowDefinitions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "WorkflowDefinitions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "WorkflowApprovals",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "Comments",
                table: "WorkflowApprovals",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ApproverId",
                table: "WorkflowApprovals",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "WorkflowApprovals",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "WorkflowApprovals",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "WorkflowApprovals",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "WorkflowApprovals",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "WorkflowApprovals",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "WorkflowApprovals",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "WorkflowApprovals",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProcessedById",
                table: "WorkflowApprovals",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProcessedDate",
                table: "WorkflowApprovals",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "WorkflowApprovals",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "WorkflowActivityLogs",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<int>(
                name: "ActivityType",
                table: "WorkflowActivityLogs",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AddColumn<DateTime>(
                name: "ActivityDate",
                table: "WorkflowActivityLogs",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "WorkflowActivityLogs",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "WorkflowActivityLogs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Data",
                table: "WorkflowActivityLogs",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "WorkflowActivityLogs",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "WorkflowActivityLogs",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IpAddress",
                table: "WorkflowActivityLogs",
                type: "nvarchar(45)",
                maxLength: 45,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "WorkflowActivityLogs",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "WorkflowActivityLogs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "WorkflowActivityLogs",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "WorkflowActivityLogs",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserAgent",
                table: "WorkflowActivityLogs",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "Warehouses",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "Warehouses",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "WarehouseLocations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "WarehouseLocations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "UserTenants",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "UserTenants",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "UserTechnicianSkills",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "UserTechnicianSkills",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "UserReportFavorites",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "UserReportFavorites",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "ThreatIndicators",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "ThreatIndicators",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "ThreatDetections",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "ThreatDetections",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "Tenants",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "Tenants",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "TenantModules",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "TenantModules",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "TechnicianTeams",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "TechnicianTeams",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TechnicianId",
                table: "TechnicianTeams",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "TechnicianTeamMembers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "TechnicianTeamMembers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TechnicianId1",
                table: "TechnicianTeamMembers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "TechnicianSkills",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "TechnicianSkills",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "TechnicianShifts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "TechnicianShifts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "TechnicianSchedules",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "TechnicianSchedules",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TechnicianId1",
                table: "TechnicianSchedules",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "TechnicianAvailabilities",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "TechnicianAvailabilities",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TechnicianId1",
                table: "TechnicianAvailabilities",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "SystemSettings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "SystemSettings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "Suppliers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "Suppliers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "SupplierItemCatalogs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "SupplierItemCatalogs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "SupplierContacts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "SupplierContacts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "StockMovements",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "StockMovements",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "StockAdjustments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "StockAdjustments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "StockAdjustmentItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "StockAdjustmentItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "Skill",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "Skill",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "Shifts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "Shifts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "ShiftAssignments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "ShiftAssignments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "SecurityPolicyViolations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "SecurityPolicyViolations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "SecurityPolicies",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "SecurityPolicies",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "SecurityMetricsSet",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "SecurityMetricsSet",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "SecurityLogs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "SecurityLogs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "SecurityAlerts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "SecurityAlerts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "Securities",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "Securities",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "Sections",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "Sections",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "ReportTemplates",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "ReportTemplates",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "ReportSchedules",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "ReportSchedules",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "Reports",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "Reports",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "ReportRoleAssignments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "ReportRoleAssignments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "ReportExports",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "ReportExports",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "ReportExecutions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "ReportExecutions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "ReportDataSourceUsageLogs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "ReportDataSourceUsageLogs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "ReportDataSources",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "ReportDataSources",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "RefreshTokens",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "RefreshTokens",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "PurchaseRequisitions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "PurchaseRequisitions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "PurchaseRequisitionItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "PurchaseRequisitionItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "PurchaseOrders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "PurchaseOrders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "PurchaseOrderReceipts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "PurchaseOrderReceipts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "PurchaseOrderReceiptItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "PurchaseOrderReceiptItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "PurchaseOrderItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "PurchaseOrderItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "PriorityLevels",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "PriorityLevels",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "PositionSkillRequirement",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "PositionSkillRequirement",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "Permissions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "Permissions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "MaintenanceTypes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "MaintenanceTypes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ScheduleType",
                table: "MaintenanceSchedules",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<DateTime>(
                name: "NextDueDate",
                table: "MaintenanceSchedules",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "MaintenanceSchedules",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<decimal>(
                name: "EstimatedHours",
                table: "MaintenanceSchedules",
                type: "decimal(18,4)",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "float");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "MaintenanceSchedules",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AssignedTeamId",
                table: "MaintenanceSchedules",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AssignedTechnicianId",
                table: "MaintenanceSchedules",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AutoGenerateWorkOrders",
                table: "MaintenanceSchedules",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "MaintenanceSchedules",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "MaintenanceSchedules",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FrequencyUnit",
                table: "MaintenanceSchedules",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Instructions",
                table: "MaintenanceSchedules",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastCompletedDate",
                table: "MaintenanceSchedules",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "MaintenanceSchedules",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NotificationRecipients",
                table: "MaintenanceSchedules",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Priority",
                table: "MaintenanceSchedules",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RequiredParts",
                table: "MaintenanceSchedules",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RequiredSkills",
                table: "MaintenanceSchedules",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RequiredTools",
                table: "MaintenanceSchedules",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SafetyNotes",
                table: "MaintenanceSchedules",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "MaintenanceAttachmentTag",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "MaintenanceAttachmentTag",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "MaintenanceAttachments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "MaintenanceAttachments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "MaintenanceAssets",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "MaintenanceAssets",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "MaintenanceAssetCategories",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "MaintenanceAssetCategories",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "InventoryLocations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "InventoryLocations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "InventoryItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "InventoryItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "InventoryCategories",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "InventoryCategories",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "InventoryAllocations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "InventoryAllocations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "InspectionTemplates",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "InspectionTemplates",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "InspectionDocuments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "InspectionDocuments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "EmployeeWorkHistories",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "EmployeeWorkHistories",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "EmployeeSkills",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "EmployeeSkills",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "EmployeeShiftPreferences",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "EmployeeShiftPreferences",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CertificationLevel",
                table: "Employees",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "Employees",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CurrentWorkload",
                table: "Employees",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ExperienceLevel",
                table: "Employees",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "Employees",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSyncDate",
                table: "Employees",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MaxWorkload",
                table: "Employees",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "Employees",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Specialization",
                table: "Employees",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "EmployeeQualifications",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "EmployeeQualifications",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "EmployeePositions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "EmployeePositions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "EmployeeIdentificationCards",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "EmployeeIdentificationCards",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "EmployeeEmergencyContacts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "EmployeeEmergencyContacts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "EmployeeDependents",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "EmployeeDependents",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "EmployeeContractDetails",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "EmployeeContractDetails",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "EmployeeBiometrics",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "EmployeeBiometrics",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "EmailTemplates",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "EmailTemplates",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "EmailSettings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "EmailSettings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "Departments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "Departments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "Countries",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "Countries",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "BlacklistedTokens",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "BlacklistedTokens",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "AuditLogs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "AuditLogs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "AttendanceRecords",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "AttendanceRecords",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "AssetTypes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "AssetTypes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "AssetTypeFields",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "AssetTypeFields",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "AssetInspections",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "AssetInspections",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "AssetDowntimes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "AssetDowntimes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SafetyAudits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuditName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AuditDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LeadAuditor = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AuditTeam = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AuditScope = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    OverallScore = table.Column<int>(type: "int", nullable: false),
                    Findings = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Recommendations = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    AuditStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FollowUpDate = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_SafetyAudits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SafetyProtocols",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RegulatoryStandard = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Procedures = table.Column<string>(type: "nvarchar(max)", maxLength: 5000, nullable: false),
                    RequiredEquipment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RequiredTraining = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RequiredCertifications = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EmergencyProcedures = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    PreventiveMeasures = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ApplicableMaintenanceTypes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ApplicableAssetTypes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsMandatory = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    ReviewDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NextReviewDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewFrequencyMonths = table.Column<int>(type: "int", nullable: false),
                    MinimumTrainingLevel = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpirationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsRegulatory = table.Column<bool>(type: "bit", nullable: false),
                    ComplianceCheckpoints = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RegulatorySources = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ApplicableEnvironments = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ApprovalStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ApprovedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ApprovalDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Version = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
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
                    table.PrimaryKey("PK_SafetyProtocols", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SafetyProtocols_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ScheduledWorkOrder",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScheduleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GeneratedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ScheduledCompletionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ActualCompletionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_ScheduledWorkOrder", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScheduledWorkOrder_MaintenanceSchedules_ScheduleId",
                        column: x => x.ScheduleId,
                        principalTable: "MaintenanceSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TechnicalSkills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SkillLevel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Complexity = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RiskLevel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Prerequisites = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Certifications = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EstimatedLearningHours = table.Column<int>(type: "int", nullable: false),
                    ToolsRequired = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SafetyRequirements = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CompetencyAreas = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RelatedMaintenanceTypes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsFromHRModule = table.Column<bool>(type: "bit", nullable: false),
                    LastSyncDate = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_TechnicalSkills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TechnicalSkills_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TechnicianCertifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TechnicianId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CertificationName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CertificationNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IssuingOrganization = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IssueDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpirationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CertificationLevel = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Requirements = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    RenewalRequirements = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Cost = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    IsMandatory = table.Column<bool>(type: "bit", nullable: false),
                    IsVerified = table.Column<bool>(type: "bit", nullable: false),
                    VerifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    VerificationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    DocumentPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
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
                    table.PrimaryKey("PK_TechnicianCertifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TechnicianCertifications_Employees_TechnicianId",
                        column: x => x.TechnicianId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TechnicianCertifications_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Technicians",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Department = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Position = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Specialization = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CertificationLevel = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ExperienceLevel = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    HireDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CurrentWorkload = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    MaxWorkload = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    AverageRating = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    CompletedWorkOrders = table.Column<int>(type: "int", nullable: false),
                    LastSyncDate = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_Technicians", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Technicians_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Technicians_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowEntityTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    EntityClassName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PropertySchema = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    Icon = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ColorCode = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: true),
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
                    table.PrimaryKey("PK_WorkflowEntityTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowEntityTypes_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProtocolAdherences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProtocolId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TechnicianId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AdherenceDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    WasFollowed = table.Column<bool>(type: "bit", nullable: false),
                    ComplianceScore = table.Column<int>(type: "int", nullable: false),
                    AdherenceLevel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    VerifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    VerificationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_ProtocolAdherences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProtocolAdherences_SafetyProtocols_ProtocolId",
                        column: x => x.ProtocolId,
                        principalTable: "SafetyProtocols",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProtocolAdherences_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProtocolAuditDetails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuditId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProtocolId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ComplianceScore = table.Column<int>(type: "int", nullable: false),
                    Passed = table.Column<bool>(type: "bit", nullable: false),
                    ProtocolFindings = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ProtocolRecommendations = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Priority = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
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
                    table.PrimaryKey("PK_ProtocolAuditDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProtocolAuditDetails_SafetyAudits_AuditId",
                        column: x => x.AuditId,
                        principalTable: "SafetyAudits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProtocolAuditDetails_SafetyProtocols_ProtocolId",
                        column: x => x.ProtocolId,
                        principalTable: "SafetyProtocols",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProtocolTrainings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProtocolId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TechnicianId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrainingDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TrainingMethod = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TrainerName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TrainingHours = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    TestScore = table.Column<int>(type: "int", nullable: true),
                    Completed = table.Column<bool>(type: "bit", nullable: false),
                    CompletionStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CertificationIssued = table.Column<bool>(type: "bit", nullable: false),
                    ExpirationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
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
                    table.PrimaryKey("PK_ProtocolTrainings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProtocolTrainings_SafetyProtocols_ProtocolId",
                        column: x => x.ProtocolId,
                        principalTable: "SafetyProtocols",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProtocolTrainings_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProtocolViolations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProtocolId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TechnicianId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ViolationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ViolationType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ViolationSeverity = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    RootCause = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ImmediateActions = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    PreventiveActions = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    InjuryOccurred = table.Column<bool>(type: "bit", nullable: false),
                    PropertyDamageOccurred = table.Column<bool>(type: "bit", nullable: false),
                    EstimatedCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    InvestigationStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    InvestigatorAssigned = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TargetCompletionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ActualCompletionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_ProtocolViolations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProtocolViolations_SafetyProtocols_ProtocolId",
                        column: x => x.ProtocolId,
                        principalTable: "SafetyProtocols",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProtocolViolations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SafetyComplianceRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SafetyProtocolId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProtocolId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TechnicianId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ComplianceDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CheckDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ComplianceStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsCompliant = table.Column<bool>(type: "bit", nullable: false),
                    ViolationType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CorrectiveActions = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CorrectiveActionDueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ChecklistItems = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Violations = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InspectorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InspectorNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    VerifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VerifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_SafetyComplianceRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SafetyComplianceRecords_Employees_InspectorId",
                        column: x => x.InspectorId,
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SafetyComplianceRecords_Employees_TechnicianId",
                        column: x => x.TechnicianId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SafetyComplianceRecords_Employees_VerifiedById",
                        column: x => x.VerifiedById,
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SafetyComplianceRecords_SafetyProtocols_SafetyProtocolId",
                        column: x => x.SafetyProtocolId,
                        principalTable: "SafetyProtocols",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SafetyComplianceRecords_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SafetyComplianceRecords_WorkOrders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "WorkOrders",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TechnicianSkillAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TechnicianId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SkillId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProficiencyLevel = table.Column<int>(type: "int", nullable: false),
                    ProficiencyDescription = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AcquiredDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpirationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsVerified = table.Column<bool>(type: "bit", nullable: false),
                    VerifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastAssessmentDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    TechnicianId1 = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_TechnicianSkillAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TechnicianSkillAssignments_Employees_TechnicianId",
                        column: x => x.TechnicianId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TechnicianSkillAssignments_TechnicalSkills_SkillId",
                        column: x => x.SkillId,
                        principalTable: "TechnicalSkills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TechnicianSkillAssignments_Technicians_TechnicianId1",
                        column: x => x.TechnicianId1,
                        principalTable: "Technicians",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TechnicianSkillAssignments_Tenants_TenantId",
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
                columns: new[] { "CreatedAt", "CreatedById", "LastModifiedById" },
                values: new object[] { new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4649), null, null });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                columns: new[] { "CreatedAt", "CreatedById", "LastModifiedById" },
                values: new object[] { new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4666), null, null });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                columns: new[] { "CreatedAt", "CreatedById", "LastModifiedById" },
                values: new object[] { new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4671), null, null });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                columns: new[] { "CreatedAt", "CreatedById", "LastModifiedById" },
                values: new object[] { new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4676), null, null });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000005"),
                columns: new[] { "CreatedAt", "CreatedById", "LastModifiedById" },
                values: new object[] { new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4684), null, null });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000006"),
                columns: new[] { "CreatedAt", "CreatedById", "LastModifiedById" },
                values: new object[] { new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4692), null, null });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000007"),
                columns: new[] { "CreatedAt", "CreatedById", "LastModifiedById" },
                values: new object[] { new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4697), null, null });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000008"),
                columns: new[] { "CreatedAt", "CreatedById", "LastModifiedById" },
                values: new object[] { new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4701), null, null });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000009"),
                columns: new[] { "CreatedAt", "CreatedById", "LastModifiedById" },
                values: new object[] { new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4709), null, null });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000010"),
                columns: new[] { "CreatedAt", "CreatedById", "LastModifiedById" },
                values: new object[] { new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4715), null, null });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000011"),
                columns: new[] { "CreatedAt", "CreatedById", "LastModifiedById" },
                values: new object[] { new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4721), null, null });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000012"),
                columns: new[] { "CreatedAt", "CreatedById", "LastModifiedById" },
                values: new object[] { new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4725), null, null });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000013"),
                columns: new[] { "CreatedAt", "CreatedById", "LastModifiedById" },
                values: new object[] { new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4732), null, null });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000014"),
                columns: new[] { "CreatedAt", "CreatedById", "LastModifiedById" },
                values: new object[] { new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4745), null, null });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000015"),
                columns: new[] { "CreatedAt", "CreatedById", "LastModifiedById" },
                values: new object[] { new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4755), null, null });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000016"),
                columns: new[] { "CreatedAt", "CreatedById", "LastModifiedById" },
                values: new object[] { new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4759), null, null });

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
                columns: new[] { "CreatedAt", "CreatedById", "LastModifiedById" },
                values: new object[] { new DateTime(2025, 10, 14, 17, 21, 11, 238, DateTimeKind.Utc).AddTicks(4205), null, null });

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_TechnicianId",
                table: "WorkOrders",
                column: "TechnicianId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowTransitions_FromStepId",
                table: "WorkflowTransitions",
                column: "FromStepId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowTransitions_TenantId",
                table: "WorkflowTransitions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowTransitions_WorkflowDefinitionId",
                table: "WorkflowTransitions",
                column: "WorkflowDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowSteps_IsEndStep",
                table: "WorkflowSteps",
                column: "IsEndStep");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowSteps_IsStartStep",
                table: "WorkflowSteps",
                column: "IsStartStep");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowSteps_Name",
                table: "WorkflowSteps",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowSteps_Order",
                table: "WorkflowSteps",
                column: "Order");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowSteps_StepType",
                table: "WorkflowSteps",
                column: "StepType");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowSteps_TenantId",
                table: "WorkflowSteps",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowStepInstances_AssignedToId",
                table: "WorkflowStepInstances",
                column: "AssignedToId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowStepInstances_DueDate",
                table: "WorkflowStepInstances",
                column: "DueDate");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowStepInstances_StartedDate",
                table: "WorkflowStepInstances",
                column: "StartedDate");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowStepInstances_TenantId",
                table: "WorkflowStepInstances",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowInstances_EntityId",
                table: "WorkflowInstances",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowInstances_EntityTypeId",
                table: "WorkflowInstances",
                column: "EntityTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowInstances_EntityTypeId_EntityId",
                table: "WorkflowInstances",
                columns: new[] { "EntityTypeId", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowInstances_InitiatedById",
                table: "WorkflowInstances",
                column: "InitiatedById");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowInstances_Priority",
                table: "WorkflowInstances",
                column: "Priority");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowInstances_StartedById",
                table: "WorkflowInstances",
                column: "StartedById");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowInstances_StartedDate",
                table: "WorkflowInstances",
                column: "StartedDate");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowInstances_Status",
                table: "WorkflowInstances",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowInstances_TenantId",
                table: "WorkflowInstances",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDefinitions_EntityTypeId",
                table: "WorkflowDefinitions",
                column: "EntityTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDefinitions_IsActive",
                table: "WorkflowDefinitions",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDefinitions_Name",
                table: "WorkflowDefinitions",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDefinitions_TenantId_Name",
                table: "WorkflowDefinitions",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowApprovals_ApproverId",
                table: "WorkflowApprovals",
                column: "ApproverId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowApprovals_DueDate",
                table: "WorkflowApprovals",
                column: "DueDate");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowApprovals_ProcessedById",
                table: "WorkflowApprovals",
                column: "ProcessedById");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowApprovals_RequestedDate",
                table: "WorkflowApprovals",
                column: "RequestedDate");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowApprovals_Status",
                table: "WorkflowApprovals",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowApprovals_TenantId",
                table: "WorkflowApprovals",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowActivityLogs_ActivityDate",
                table: "WorkflowActivityLogs",
                column: "ActivityDate");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowActivityLogs_ActivityType",
                table: "WorkflowActivityLogs",
                column: "ActivityType");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowActivityLogs_PerformedById",
                table: "WorkflowActivityLogs",
                column: "PerformedById");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowActivityLogs_TenantId",
                table: "WorkflowActivityLogs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicianTeams_TechnicianId",
                table: "TechnicianTeams",
                column: "TechnicianId");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicianTeamMembers_TechnicianId1",
                table: "TechnicianTeamMembers",
                column: "TechnicianId1");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicianSchedules_TechnicianId1",
                table: "TechnicianSchedules",
                column: "TechnicianId1");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicianAvailabilities_TechnicianId1",
                table: "TechnicianAvailabilities",
                column: "TechnicianId1");

            migrationBuilder.CreateIndex(
                name: "IX_ProtocolAdherences_ProtocolId",
                table: "ProtocolAdherences",
                column: "ProtocolId");

            migrationBuilder.CreateIndex(
                name: "IX_ProtocolAdherences_TenantId",
                table: "ProtocolAdherences",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProtocolAuditDetails_AuditId",
                table: "ProtocolAuditDetails",
                column: "AuditId");

            migrationBuilder.CreateIndex(
                name: "IX_ProtocolAuditDetails_ProtocolId",
                table: "ProtocolAuditDetails",
                column: "ProtocolId");

            migrationBuilder.CreateIndex(
                name: "IX_ProtocolTrainings_ProtocolId",
                table: "ProtocolTrainings",
                column: "ProtocolId");

            migrationBuilder.CreateIndex(
                name: "IX_ProtocolTrainings_TenantId",
                table: "ProtocolTrainings",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProtocolViolations_ProtocolId",
                table: "ProtocolViolations",
                column: "ProtocolId");

            migrationBuilder.CreateIndex(
                name: "IX_ProtocolViolations_TenantId",
                table: "ProtocolViolations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_SafetyComplianceRecords_ComplianceDate",
                table: "SafetyComplianceRecords",
                column: "ComplianceDate");

            migrationBuilder.CreateIndex(
                name: "IX_SafetyComplianceRecords_ComplianceStatus",
                table: "SafetyComplianceRecords",
                column: "ComplianceStatus");

            migrationBuilder.CreateIndex(
                name: "IX_SafetyComplianceRecords_InspectorId",
                table: "SafetyComplianceRecords",
                column: "InspectorId");

            migrationBuilder.CreateIndex(
                name: "IX_SafetyComplianceRecords_SafetyProtocolId",
                table: "SafetyComplianceRecords",
                column: "SafetyProtocolId");

            migrationBuilder.CreateIndex(
                name: "IX_SafetyComplianceRecords_TechnicianId",
                table: "SafetyComplianceRecords",
                column: "TechnicianId");

            migrationBuilder.CreateIndex(
                name: "IX_SafetyComplianceRecords_TenantId",
                table: "SafetyComplianceRecords",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_SafetyComplianceRecords_VerifiedById",
                table: "SafetyComplianceRecords",
                column: "VerifiedById");

            migrationBuilder.CreateIndex(
                name: "IX_SafetyComplianceRecords_WorkOrderId",
                table: "SafetyComplianceRecords",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_SafetyProtocols_Category",
                table: "SafetyProtocols",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_SafetyProtocols_Code",
                table: "SafetyProtocols",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SafetyProtocols_EffectiveDate",
                table: "SafetyProtocols",
                column: "EffectiveDate");

            migrationBuilder.CreateIndex(
                name: "IX_SafetyProtocols_IsActive",
                table: "SafetyProtocols",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_SafetyProtocols_IsMandatory",
                table: "SafetyProtocols",
                column: "IsMandatory");

            migrationBuilder.CreateIndex(
                name: "IX_SafetyProtocols_Name",
                table: "SafetyProtocols",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_SafetyProtocols_NextReviewDate",
                table: "SafetyProtocols",
                column: "NextReviewDate");

            migrationBuilder.CreateIndex(
                name: "IX_SafetyProtocols_Severity",
                table: "SafetyProtocols",
                column: "Severity");

            migrationBuilder.CreateIndex(
                name: "IX_SafetyProtocols_TenantId",
                table: "SafetyProtocols",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledWorkOrder_ScheduleId",
                table: "ScheduledWorkOrder",
                column: "ScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicalSkills_Category",
                table: "TechnicalSkills",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicalSkills_Code",
                table: "TechnicalSkills",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TechnicalSkills_Complexity",
                table: "TechnicalSkills",
                column: "Complexity");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicalSkills_IsActive",
                table: "TechnicalSkills",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicalSkills_IsFromHRModule",
                table: "TechnicalSkills",
                column: "IsFromHRModule");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicalSkills_LastSyncDate",
                table: "TechnicalSkills",
                column: "LastSyncDate");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicalSkills_Name",
                table: "TechnicalSkills",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicalSkills_RiskLevel",
                table: "TechnicalSkills",
                column: "RiskLevel");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicalSkills_SkillLevel",
                table: "TechnicalSkills",
                column: "SkillLevel");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicalSkills_TenantId",
                table: "TechnicalSkills",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicianCertifications_TechnicianId",
                table: "TechnicianCertifications",
                column: "TechnicianId");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicianCertifications_TenantId",
                table: "TechnicianCertifications",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Technicians_EmployeeId",
                table: "Technicians",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_Technicians_TenantId",
                table: "Technicians",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicianSkillAssignments_ExpirationDate",
                table: "TechnicianSkillAssignments",
                column: "ExpirationDate");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicianSkillAssignments_IsVerified",
                table: "TechnicianSkillAssignments",
                column: "IsVerified");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicianSkillAssignments_LastAssessmentDate",
                table: "TechnicianSkillAssignments",
                column: "LastAssessmentDate");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicianSkillAssignments_ProficiencyLevel",
                table: "TechnicianSkillAssignments",
                column: "ProficiencyLevel");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicianSkillAssignments_SkillId",
                table: "TechnicianSkillAssignments",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicianSkillAssignments_TechnicianId",
                table: "TechnicianSkillAssignments",
                column: "TechnicianId");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicianSkillAssignments_TechnicianId_SkillId",
                table: "TechnicianSkillAssignments",
                columns: new[] { "TechnicianId", "SkillId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TechnicianSkillAssignments_TechnicianId1",
                table: "TechnicianSkillAssignments",
                column: "TechnicianId1");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicianSkillAssignments_TenantId",
                table: "TechnicianSkillAssignments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowEntityTypes_IsActive",
                table: "WorkflowEntityTypes",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowEntityTypes_Name",
                table: "WorkflowEntityTypes",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowEntityTypes_TenantId_Name",
                table: "WorkflowEntityTypes",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_TechnicianAvailabilities_Technicians_TechnicianId1",
                table: "TechnicianAvailabilities",
                column: "TechnicianId1",
                principalTable: "Technicians",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TechnicianSchedules_Technicians_TechnicianId1",
                table: "TechnicianSchedules",
                column: "TechnicianId1",
                principalTable: "Technicians",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TechnicianTeamMembers_Technicians_TechnicianId1",
                table: "TechnicianTeamMembers",
                column: "TechnicianId1",
                principalTable: "Technicians",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TechnicianTeams_Technicians_TechnicianId",
                table: "TechnicianTeams",
                column: "TechnicianId",
                principalTable: "Technicians",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkflowActivityLogs_Tenants_TenantId",
                table: "WorkflowActivityLogs",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkflowActivityLogs_Users_PerformedById",
                table: "WorkflowActivityLogs",
                column: "PerformedById",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkflowActivityLogs_WorkflowStepInstances_StepInstanceId",
                table: "WorkflowActivityLogs",
                column: "StepInstanceId",
                principalTable: "WorkflowStepInstances",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkflowApprovals_Tenants_TenantId",
                table: "WorkflowApprovals",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkflowApprovals_Users_ApproverId",
                table: "WorkflowApprovals",
                column: "ApproverId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkflowApprovals_Users_ProcessedById",
                table: "WorkflowApprovals",
                column: "ProcessedById",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkflowApprovals_WorkflowStepInstances_StepInstanceId",
                table: "WorkflowApprovals",
                column: "StepInstanceId",
                principalTable: "WorkflowStepInstances",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkflowDefinitions_Tenants_TenantId",
                table: "WorkflowDefinitions",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkflowDefinitions_WorkflowEntityTypes_EntityTypeId",
                table: "WorkflowDefinitions",
                column: "EntityTypeId",
                principalTable: "WorkflowEntityTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkflowInstances_Tenants_TenantId",
                table: "WorkflowInstances",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkflowInstances_Users_InitiatedById",
                table: "WorkflowInstances",
                column: "InitiatedById",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkflowInstances_WorkflowEntityTypes_EntityTypeId",
                table: "WorkflowInstances",
                column: "EntityTypeId",
                principalTable: "WorkflowEntityTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkflowStepInstances_Tenants_TenantId",
                table: "WorkflowStepInstances",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkflowStepInstances_Users_AssignedToId",
                table: "WorkflowStepInstances",
                column: "AssignedToId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkflowSteps_Tenants_TenantId",
                table: "WorkflowSteps",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkflowTransitions_Tenants_TenantId",
                table: "WorkflowTransitions",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkflowTransitions_WorkflowDefinitions_WorkflowDefinitionId",
                table: "WorkflowTransitions",
                column: "WorkflowDefinitionId",
                principalTable: "WorkflowDefinitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkOrders_Technicians_TechnicianId",
                table: "WorkOrders",
                column: "TechnicianId",
                principalTable: "Technicians",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TechnicianAvailabilities_Technicians_TechnicianId1",
                table: "TechnicianAvailabilities");

            migrationBuilder.DropForeignKey(
                name: "FK_TechnicianSchedules_Technicians_TechnicianId1",
                table: "TechnicianSchedules");

            migrationBuilder.DropForeignKey(
                name: "FK_TechnicianTeamMembers_Technicians_TechnicianId1",
                table: "TechnicianTeamMembers");

            migrationBuilder.DropForeignKey(
                name: "FK_TechnicianTeams_Technicians_TechnicianId",
                table: "TechnicianTeams");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkflowActivityLogs_Tenants_TenantId",
                table: "WorkflowActivityLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkflowActivityLogs_Users_PerformedById",
                table: "WorkflowActivityLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkflowActivityLogs_WorkflowStepInstances_StepInstanceId",
                table: "WorkflowActivityLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkflowApprovals_Tenants_TenantId",
                table: "WorkflowApprovals");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkflowApprovals_Users_ApproverId",
                table: "WorkflowApprovals");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkflowApprovals_Users_ProcessedById",
                table: "WorkflowApprovals");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkflowApprovals_WorkflowStepInstances_StepInstanceId",
                table: "WorkflowApprovals");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkflowDefinitions_Tenants_TenantId",
                table: "WorkflowDefinitions");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkflowDefinitions_WorkflowEntityTypes_EntityTypeId",
                table: "WorkflowDefinitions");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkflowInstances_Tenants_TenantId",
                table: "WorkflowInstances");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkflowInstances_Users_InitiatedById",
                table: "WorkflowInstances");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkflowInstances_WorkflowEntityTypes_EntityTypeId",
                table: "WorkflowInstances");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkflowStepInstances_Tenants_TenantId",
                table: "WorkflowStepInstances");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkflowStepInstances_Users_AssignedToId",
                table: "WorkflowStepInstances");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkflowSteps_Tenants_TenantId",
                table: "WorkflowSteps");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkflowTransitions_Tenants_TenantId",
                table: "WorkflowTransitions");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkflowTransitions_WorkflowDefinitions_WorkflowDefinitionId",
                table: "WorkflowTransitions");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkOrders_Technicians_TechnicianId",
                table: "WorkOrders");

            migrationBuilder.DropTable(
                name: "ProtocolAdherences");

            migrationBuilder.DropTable(
                name: "ProtocolAuditDetails");

            migrationBuilder.DropTable(
                name: "ProtocolTrainings");

            migrationBuilder.DropTable(
                name: "ProtocolViolations");

            migrationBuilder.DropTable(
                name: "SafetyComplianceRecords");

            migrationBuilder.DropTable(
                name: "ScheduledWorkOrder");

            migrationBuilder.DropTable(
                name: "TechnicianCertifications");

            migrationBuilder.DropTable(
                name: "TechnicianSkillAssignments");

            migrationBuilder.DropTable(
                name: "WorkflowEntityTypes");

            migrationBuilder.DropTable(
                name: "SafetyAudits");

            migrationBuilder.DropTable(
                name: "SafetyProtocols");

            migrationBuilder.DropTable(
                name: "TechnicalSkills");

            migrationBuilder.DropTable(
                name: "Technicians");

            migrationBuilder.DropIndex(
                name: "IX_WorkOrders_TechnicianId",
                table: "WorkOrders");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowTransitions_FromStepId",
                table: "WorkflowTransitions");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowTransitions_TenantId",
                table: "WorkflowTransitions");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowTransitions_WorkflowDefinitionId",
                table: "WorkflowTransitions");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowSteps_IsEndStep",
                table: "WorkflowSteps");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowSteps_IsStartStep",
                table: "WorkflowSteps");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowSteps_Name",
                table: "WorkflowSteps");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowSteps_Order",
                table: "WorkflowSteps");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowSteps_StepType",
                table: "WorkflowSteps");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowSteps_TenantId",
                table: "WorkflowSteps");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowStepInstances_AssignedToId",
                table: "WorkflowStepInstances");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowStepInstances_DueDate",
                table: "WorkflowStepInstances");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowStepInstances_StartedDate",
                table: "WorkflowStepInstances");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowStepInstances_TenantId",
                table: "WorkflowStepInstances");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowInstances_EntityId",
                table: "WorkflowInstances");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowInstances_EntityTypeId",
                table: "WorkflowInstances");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowInstances_EntityTypeId_EntityId",
                table: "WorkflowInstances");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowInstances_InitiatedById",
                table: "WorkflowInstances");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowInstances_Priority",
                table: "WorkflowInstances");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowInstances_StartedById",
                table: "WorkflowInstances");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowInstances_StartedDate",
                table: "WorkflowInstances");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowInstances_Status",
                table: "WorkflowInstances");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowInstances_TenantId",
                table: "WorkflowInstances");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowDefinitions_EntityTypeId",
                table: "WorkflowDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowDefinitions_IsActive",
                table: "WorkflowDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowDefinitions_Name",
                table: "WorkflowDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowDefinitions_TenantId_Name",
                table: "WorkflowDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowApprovals_ApproverId",
                table: "WorkflowApprovals");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowApprovals_DueDate",
                table: "WorkflowApprovals");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowApprovals_ProcessedById",
                table: "WorkflowApprovals");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowApprovals_RequestedDate",
                table: "WorkflowApprovals");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowApprovals_Status",
                table: "WorkflowApprovals");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowApprovals_TenantId",
                table: "WorkflowApprovals");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowActivityLogs_ActivityDate",
                table: "WorkflowActivityLogs");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowActivityLogs_ActivityType",
                table: "WorkflowActivityLogs");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowActivityLogs_PerformedById",
                table: "WorkflowActivityLogs");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowActivityLogs_TenantId",
                table: "WorkflowActivityLogs");

            migrationBuilder.DropIndex(
                name: "IX_TechnicianTeams_TechnicianId",
                table: "TechnicianTeams");

            migrationBuilder.DropIndex(
                name: "IX_TechnicianTeamMembers_TechnicianId1",
                table: "TechnicianTeamMembers");

            migrationBuilder.DropIndex(
                name: "IX_TechnicianSchedules_TechnicianId1",
                table: "TechnicianSchedules");

            migrationBuilder.DropIndex(
                name: "IX_TechnicianAvailabilities_TechnicianId1",
                table: "TechnicianAvailabilities");

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

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "WorkStations");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "WorkStations");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "WorkOrderTypes");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "WorkOrderTypes");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "WorkOrderTasks");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "WorkOrderTasks");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "TechnicianId",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "WorkOrderParts");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "WorkOrderParts");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "WorkOrderLabor");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "WorkOrderLabor");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "WorkOrderDocuments");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "WorkOrderDocuments");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "WorkOrderComments");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "WorkOrderComments");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "WorkflowTransitions");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "WorkflowTransitions");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "WorkflowTransitions");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "WorkflowTransitions");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "WorkflowTransitions");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "WorkflowTransitions");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "WorkflowTransitions");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "WorkflowTransitions");

            migrationBuilder.DropColumn(
                name: "WorkflowDefinitionId",
                table: "WorkflowTransitions");

            migrationBuilder.DropColumn(
                name: "AssignmentConfiguration",
                table: "WorkflowSteps");

            migrationBuilder.DropColumn(
                name: "AssignmentType",
                table: "WorkflowSteps");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "WorkflowSteps");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "WorkflowSteps");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "WorkflowSteps");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "WorkflowSteps");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "WorkflowSteps");

            migrationBuilder.DropColumn(
                name: "IsEndStep",
                table: "WorkflowSteps");

            migrationBuilder.DropColumn(
                name: "IsStartStep",
                table: "WorkflowSteps");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "WorkflowSteps");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "WorkflowSteps");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "WorkflowSteps");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "WorkflowStepInstances");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "WorkflowStepInstances");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "WorkflowStepInstances");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "WorkflowStepInstances");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "WorkflowStepInstances");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "WorkflowStepInstances");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "WorkflowStepInstances");

            migrationBuilder.DropColumn(
                name: "RetryCount",
                table: "WorkflowStepInstances");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "WorkflowStepInstances");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "WorkflowStepInstances");

            migrationBuilder.DropColumn(
                name: "CancelledDate",
                table: "WorkflowInstances");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "WorkflowInstances");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "WorkflowInstances");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "WorkflowInstances");

            migrationBuilder.DropColumn(
                name: "Data",
                table: "WorkflowInstances");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "WorkflowInstances");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "WorkflowInstances");

            migrationBuilder.DropColumn(
                name: "EntityTypeId",
                table: "WorkflowInstances");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "WorkflowInstances");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "WorkflowInstances");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "WorkflowInstances");

            migrationBuilder.DropColumn(
                name: "StartedById",
                table: "WorkflowInstances");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "WorkflowInstances");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "WorkflowInstances");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "WorkflowDefinitions");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "WorkflowDefinitions");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "WorkflowDefinitions");

            migrationBuilder.DropColumn(
                name: "EntityTypeId",
                table: "WorkflowDefinitions");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "WorkflowDefinitions");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "WorkflowDefinitions");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "WorkflowApprovals");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "WorkflowApprovals");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "WorkflowApprovals");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "WorkflowApprovals");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "WorkflowApprovals");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "WorkflowApprovals");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "WorkflowApprovals");

            migrationBuilder.DropColumn(
                name: "ProcessedById",
                table: "WorkflowApprovals");

            migrationBuilder.DropColumn(
                name: "ProcessedDate",
                table: "WorkflowApprovals");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "WorkflowApprovals");

            migrationBuilder.DropColumn(
                name: "ActivityDate",
                table: "WorkflowActivityLogs");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "WorkflowActivityLogs");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "WorkflowActivityLogs");

            migrationBuilder.DropColumn(
                name: "Data",
                table: "WorkflowActivityLogs");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "WorkflowActivityLogs");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "WorkflowActivityLogs");

            migrationBuilder.DropColumn(
                name: "IpAddress",
                table: "WorkflowActivityLogs");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "WorkflowActivityLogs");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "WorkflowActivityLogs");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "WorkflowActivityLogs");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "WorkflowActivityLogs");

            migrationBuilder.DropColumn(
                name: "UserAgent",
                table: "WorkflowActivityLogs");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "WarehouseLocations");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "WarehouseLocations");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "UserTenants");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "UserTenants");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "UserTechnicianSkills");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "UserTechnicianSkills");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "UserReportFavorites");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "UserReportFavorites");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "ThreatIndicators");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "ThreatIndicators");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "ThreatDetections");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "ThreatDetections");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "TenantModules");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "TenantModules");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "TechnicianTeams");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "TechnicianTeams");

            migrationBuilder.DropColumn(
                name: "TechnicianId",
                table: "TechnicianTeams");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "TechnicianTeamMembers");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "TechnicianTeamMembers");

            migrationBuilder.DropColumn(
                name: "TechnicianId1",
                table: "TechnicianTeamMembers");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "TechnicianSkills");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "TechnicianSkills");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "TechnicianShifts");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "TechnicianShifts");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "TechnicianSchedules");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "TechnicianSchedules");

            migrationBuilder.DropColumn(
                name: "TechnicianId1",
                table: "TechnicianSchedules");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "TechnicianAvailabilities");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "TechnicianAvailabilities");

            migrationBuilder.DropColumn(
                name: "TechnicianId1",
                table: "TechnicianAvailabilities");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "SupplierItemCatalogs");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "SupplierItemCatalogs");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "SupplierContacts");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "SupplierContacts");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "StockAdjustments");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "StockAdjustments");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "StockAdjustmentItems");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "StockAdjustmentItems");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "Skill");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "Skill");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "Shifts");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "Shifts");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "ShiftAssignments");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "ShiftAssignments");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "SecurityPolicyViolations");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "SecurityPolicyViolations");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "SecurityPolicies");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "SecurityPolicies");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "SecurityMetricsSet");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "SecurityMetricsSet");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "SecurityLogs");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "SecurityLogs");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "SecurityAlerts");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "SecurityAlerts");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "Securities");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "Securities");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "Sections");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "Sections");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "ReportTemplates");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "ReportTemplates");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "ReportSchedules");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "ReportSchedules");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "ReportRoleAssignments");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "ReportRoleAssignments");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "ReportExports");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "ReportExports");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "ReportExecutions");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "ReportExecutions");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "ReportDataSourceUsageLogs");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "ReportDataSourceUsageLogs");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "ReportDataSources");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "ReportDataSources");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "PurchaseRequisitionItems");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "PurchaseRequisitionItems");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "PurchaseOrderReceipts");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "PurchaseOrderReceipts");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "PurchaseOrderReceiptItems");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "PurchaseOrderReceiptItems");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "PriorityLevels");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "PriorityLevels");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "PositionSkillRequirement");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "PositionSkillRequirement");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "Permissions");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "Permissions");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "MaintenanceTypes");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "MaintenanceTypes");

            migrationBuilder.DropColumn(
                name: "AssignedTeamId",
                table: "MaintenanceSchedules");

            migrationBuilder.DropColumn(
                name: "AssignedTechnicianId",
                table: "MaintenanceSchedules");

            migrationBuilder.DropColumn(
                name: "AutoGenerateWorkOrders",
                table: "MaintenanceSchedules");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "MaintenanceSchedules");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "MaintenanceSchedules");

            migrationBuilder.DropColumn(
                name: "FrequencyUnit",
                table: "MaintenanceSchedules");

            migrationBuilder.DropColumn(
                name: "Instructions",
                table: "MaintenanceSchedules");

            migrationBuilder.DropColumn(
                name: "LastCompletedDate",
                table: "MaintenanceSchedules");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "MaintenanceSchedules");

            migrationBuilder.DropColumn(
                name: "NotificationRecipients",
                table: "MaintenanceSchedules");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "MaintenanceSchedules");

            migrationBuilder.DropColumn(
                name: "RequiredParts",
                table: "MaintenanceSchedules");

            migrationBuilder.DropColumn(
                name: "RequiredSkills",
                table: "MaintenanceSchedules");

            migrationBuilder.DropColumn(
                name: "RequiredTools",
                table: "MaintenanceSchedules");

            migrationBuilder.DropColumn(
                name: "SafetyNotes",
                table: "MaintenanceSchedules");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "MaintenanceAttachmentTag");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "MaintenanceAttachmentTag");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "MaintenanceAttachments");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "MaintenanceAttachments");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "MaintenanceAssets");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "MaintenanceAssets");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "MaintenanceAssetCategories");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "MaintenanceAssetCategories");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "InventoryLocations");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "InventoryLocations");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "InventoryCategories");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "InventoryCategories");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "InventoryAllocations");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "InventoryAllocations");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "InspectionTemplates");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "InspectionTemplates");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "InspectionDocuments");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "InspectionDocuments");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "EmployeeWorkHistories");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "EmployeeWorkHistories");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "EmployeeSkills");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "EmployeeSkills");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "EmployeeShiftPreferences");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "EmployeeShiftPreferences");

            migrationBuilder.DropColumn(
                name: "CertificationLevel",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "CurrentWorkload",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "ExperienceLevel",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "LastSyncDate",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "MaxWorkload",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "Specialization",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "EmployeeQualifications");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "EmployeeQualifications");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "EmployeePositions");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "EmployeePositions");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "EmployeeIdentificationCards");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "EmployeeIdentificationCards");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "EmployeeEmergencyContacts");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "EmployeeEmergencyContacts");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "EmployeeDependents");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "EmployeeDependents");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "EmployeeContractDetails");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "EmployeeContractDetails");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "EmployeeBiometrics");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "EmployeeBiometrics");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "EmailTemplates");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "EmailTemplates");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "EmailSettings");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "EmailSettings");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "Departments");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "Departments");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "Countries");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "Countries");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "BlacklistedTokens");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "BlacklistedTokens");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "AssetTypes");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "AssetTypes");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "AssetTypeFields");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "AssetTypeFields");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "AssetInspections");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "AssetInspections");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "AssetDowntimes");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "AssetDowntimes");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "WorkflowTransitions",
                newName: "CreatedDate");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "WorkflowSteps",
                newName: "CreatedDate");

            migrationBuilder.RenameColumn(
                name: "CreatedDate",
                table: "WorkflowInstances",
                newName: "LastActivityDate");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "WorkflowDefinitions",
                newName: "LastModifiedDate");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "WorkflowDefinitions",
                newName: "CreatedDate");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "WorkflowApprovals",
                newName: "ResponseDate");

            migrationBuilder.RenameColumn(
                name: "StepInstanceId",
                table: "WorkflowApprovals",
                newName: "WorkflowStepInstanceId");

            migrationBuilder.RenameColumn(
                name: "RequestedDate",
                table: "WorkflowApprovals",
                newName: "CreatedDate");

            migrationBuilder.RenameIndex(
                name: "IX_WorkflowApprovals_StepInstanceId",
                table: "WorkflowApprovals",
                newName: "IX_WorkflowApprovals_WorkflowStepInstanceId");

            migrationBuilder.RenameColumn(
                name: "UpdatedBy",
                table: "WorkflowActivityLogs",
                newName: "ActivityData");

            migrationBuilder.RenameColumn(
                name: "StepInstanceId",
                table: "WorkflowActivityLogs",
                newName: "WorkflowStepInstanceId");

            migrationBuilder.RenameColumn(
                name: "PerformedById",
                table: "WorkflowActivityLogs",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "WorkflowActivityLogs",
                newName: "Timestamp");

            migrationBuilder.RenameIndex(
                name: "IX_WorkflowActivityLogs_StepInstanceId",
                table: "WorkflowActivityLogs",
                newName: "IX_WorkflowActivityLogs_WorkflowStepInstanceId");

            migrationBuilder.RenameColumn(
                name: "LastProcessedDate",
                table: "MaintenanceSchedules",
                newName: "EndDate");

            migrationBuilder.RenameColumn(
                name: "FrequencyValue",
                table: "MaintenanceSchedules",
                newName: "LeadTimeDays");

            migrationBuilder.RenameColumn(
                name: "AdvanceNotificationDays",
                table: "MaintenanceSchedules",
                newName: "DayOfWeek");

            migrationBuilder.AlterColumn<string>(
                name: "StepType",
                table: "WorkflowSteps",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<double>(
                name: "EstimatedHours",
                table: "WorkflowSteps",
                type: "float",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(5,2)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "WorkflowStepInstances",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "WorkflowInstances",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<DateTime>(
                name: "StartedDate",
                table: "WorkflowInstances",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "CreatedById",
                table: "WorkflowDefinitions",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EntityType",
                table: "WorkflowDefinitions",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "WorkflowApprovals",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "Comments",
                table: "WorkflowApprovals",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ApproverId",
                table: "WorkflowApprovals",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "WorkflowActivityLogs",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ActivityType",
                table: "WorkflowActivityLogs",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "ScheduleType",
                table: "MaintenanceSchedules",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<DateTime>(
                name: "NextDueDate",
                table: "MaintenanceSchedules",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "MaintenanceSchedules",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<double>(
                name: "EstimatedHours",
                table: "MaintenanceSchedules",
                type: "float",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,4)");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "MaintenanceSchedules",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500);

            migrationBuilder.AddColumn<string>(
                name: "ConditionParameters",
                table: "MaintenanceSchedules",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DayOfMonth",
                table: "MaintenanceSchedules",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IntervalValue",
                table: "MaintenanceSchedules",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PartsTemplate",
                table: "MaintenanceSchedules",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "PreferredTime",
                table: "MaintenanceSchedules",
                type: "time",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartDate",
                table: "MaintenanceSchedules",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "TaskTemplate",
                table: "MaintenanceSchedules",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "UsageInterval",
                table: "MaintenanceSchedules",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UsageUnit",
                table: "MaintenanceSchedules",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkOrderDescription",
                table: "MaintenanceSchedules",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkOrderTitle",
                table: "MaintenanceSchedules",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 12, 2, 53, 51, 97, DateTimeKind.Utc).AddTicks(1902));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 12, 2, 53, 51, 97, DateTimeKind.Utc).AddTicks(1960));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 12, 2, 53, 51, 97, DateTimeKind.Utc).AddTicks(1962));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 12, 2, 53, 51, 97, DateTimeKind.Utc).AddTicks(1964));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 12, 2, 53, 51, 97, DateTimeKind.Utc).AddTicks(2199));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 12, 2, 53, 51, 97, DateTimeKind.Utc).AddTicks(2216));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 12, 2, 53, 51, 97, DateTimeKind.Utc).AddTicks(2221));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 12, 2, 53, 51, 97, DateTimeKind.Utc).AddTicks(2226));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000005"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 12, 2, 53, 51, 97, DateTimeKind.Utc).AddTicks(2240));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000006"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 12, 2, 53, 51, 97, DateTimeKind.Utc).AddTicks(2246));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000007"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 12, 2, 53, 51, 97, DateTimeKind.Utc).AddTicks(2251));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000008"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 12, 2, 53, 51, 97, DateTimeKind.Utc).AddTicks(2257));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000009"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 12, 2, 53, 51, 97, DateTimeKind.Utc).AddTicks(2264));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000010"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 12, 2, 53, 51, 97, DateTimeKind.Utc).AddTicks(2274));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000011"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 12, 2, 53, 51, 97, DateTimeKind.Utc).AddTicks(2279));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000012"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 12, 2, 53, 51, 97, DateTimeKind.Utc).AddTicks(2283));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000013"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 12, 2, 53, 51, 97, DateTimeKind.Utc).AddTicks(2290));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000014"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 12, 2, 53, 51, 97, DateTimeKind.Utc).AddTicks(2295));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000015"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 12, 2, 53, 51, 97, DateTimeKind.Utc).AddTicks(2300));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000016"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 12, 2, 53, 51, 97, DateTimeKind.Utc).AddTicks(2305));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 12, 2, 53, 51, 97, DateTimeKind.Utc).AddTicks(2345));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 12, 2, 53, 51, 97, DateTimeKind.Utc).AddTicks(2349));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 12, 2, 53, 51, 97, DateTimeKind.Utc).AddTicks(2350));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 12, 2, 53, 51, 97, DateTimeKind.Utc).AddTicks(2351));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 12, 2, 53, 51, 97, DateTimeKind.Utc).AddTicks(2351));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 12, 2, 53, 51, 97, DateTimeKind.Utc).AddTicks(2353));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 12, 2, 53, 51, 97, DateTimeKind.Utc).AddTicks(2354));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 12, 2, 53, 51, 97, DateTimeKind.Utc).AddTicks(2354));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 12, 2, 53, 51, 97, DateTimeKind.Utc).AddTicks(2361));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 12, 2, 53, 51, 97, DateTimeKind.Utc).AddTicks(2362));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 12, 2, 53, 51, 97, DateTimeKind.Utc).AddTicks(2363));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 12, 2, 53, 51, 97, DateTimeKind.Utc).AddTicks(2364));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 12, 2, 53, 51, 97, DateTimeKind.Utc).AddTicks(2364));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 12, 2, 53, 51, 97, DateTimeKind.Utc).AddTicks(2365));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 12, 2, 53, 51, 97, DateTimeKind.Utc).AddTicks(2365));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 12, 2, 53, 51, 97, DateTimeKind.Utc).AddTicks(2366));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 12, 2, 53, 51, 97, DateTimeKind.Utc).AddTicks(2434));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 12, 2, 53, 51, 97, DateTimeKind.Utc).AddTicks(2436));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 12, 2, 53, 51, 97, DateTimeKind.Utc).AddTicks(2437));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 12, 2, 53, 51, 97, DateTimeKind.Utc).AddTicks(2438));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 12, 2, 53, 51, 97, DateTimeKind.Utc).AddTicks(2438));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 12, 2, 53, 51, 97, DateTimeKind.Utc).AddTicks(2439));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 12, 2, 53, 51, 97, DateTimeKind.Utc).AddTicks(2440));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 12, 2, 53, 51, 97, DateTimeKind.Utc).AddTicks(2440));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 12, 2, 53, 51, 97, DateTimeKind.Utc).AddTicks(2441));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 12, 2, 53, 51, 97, DateTimeKind.Utc).AddTicks(2442));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 12, 2, 53, 51, 97, DateTimeKind.Utc).AddTicks(2442));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 12, 2, 53, 51, 97, DateTimeKind.Utc).AddTicks(2443));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 12, 2, 53, 51, 97, DateTimeKind.Utc).AddTicks(2443));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 12, 2, 53, 51, 97, DateTimeKind.Utc).AddTicks(2444));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 12, 2, 53, 51, 97, DateTimeKind.Utc).AddTicks(2445));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 12, 2, 53, 51, 97, DateTimeKind.Utc).AddTicks(2505));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 12, 2, 53, 51, 97, DateTimeKind.Utc).AddTicks(2506));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 12, 2, 53, 51, 97, DateTimeKind.Utc).AddTicks(2507));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 12, 2, 53, 51, 97, DateTimeKind.Utc).AddTicks(2508));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 12, 2, 53, 51, 97, DateTimeKind.Utc).AddTicks(2509));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 12, 2, 53, 51, 97, DateTimeKind.Utc).AddTicks(2509));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 12, 2, 53, 51, 97, DateTimeKind.Utc).AddTicks(2510));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 12, 2, 53, 51, 97, DateTimeKind.Utc).AddTicks(2510));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 12, 2, 53, 51, 97, DateTimeKind.Utc).AddTicks(2511));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 12, 2, 53, 51, 97, DateTimeKind.Utc).AddTicks(2512));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 12, 2, 53, 51, 97, DateTimeKind.Utc).AddTicks(2523));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 12, 2, 53, 51, 97, DateTimeKind.Utc).AddTicks(2524));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 12, 2, 53, 51, 97, DateTimeKind.Utc).AddTicks(2524));

            migrationBuilder.InsertData(
                table: "TenantModules",
                columns: new[] { "Id", "Configuration", "CreatedAt", "CreatedBy", "DeletedAt", "DeletedBy", "Description", "DisabledDate", "EnabledDate", "IsDeleted", "ModuleName", "Status", "TenantId", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid("0aeb65ae-ec96-4dc9-8a71-73fd19561d06"), null, new DateTime(2025, 10, 12, 2, 53, 51, 97, DateTimeKind.Utc).AddTicks(2056), null, null, null, null, null, new DateTime(2025, 10, 12, 2, 53, 51, 97, DateTimeKind.Utc).AddTicks(2056), false, "Sales", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("7ad80716-15a1-418b-915c-abd613c8288c"), null, new DateTime(2025, 10, 12, 2, 53, 51, 97, DateTimeKind.Utc).AddTicks(2102), null, null, null, null, null, new DateTime(2025, 10, 12, 2, 53, 51, 97, DateTimeKind.Utc).AddTicks(2101), false, "Marketing", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("880503f7-e1da-4d09-8b8a-fe6830020a52"), null, new DateTime(2025, 10, 12, 2, 53, 51, 97, DateTimeKind.Utc).AddTicks(2114), null, null, null, null, null, new DateTime(2025, 10, 12, 2, 53, 51, 97, DateTimeKind.Utc).AddTicks(2113), false, "WorkflowEngine", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("a5675b88-0d25-4565-93c1-0aecb492df72"), null, new DateTime(2025, 10, 12, 2, 53, 51, 97, DateTimeKind.Utc).AddTicks(2069), null, null, null, null, null, new DateTime(2025, 10, 12, 2, 53, 51, 97, DateTimeKind.Utc).AddTicks(2068), false, "Procurement", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("c350d359-7c4d-42f4-bcac-6a312702b626"), null, new DateTime(2025, 10, 12, 2, 53, 51, 97, DateTimeKind.Utc).AddTicks(2042), null, null, null, null, null, new DateTime(2025, 10, 12, 2, 53, 51, 97, DateTimeKind.Utc).AddTicks(2042), false, "HR", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("d6eb8bdc-02f3-403d-b08e-8e32c700ddbb"), null, new DateTime(2025, 10, 12, 2, 53, 51, 97, DateTimeKind.Utc).AddTicks(2028), null, null, null, null, null, new DateTime(2025, 10, 12, 2, 53, 51, 97, DateTimeKind.Utc).AddTicks(2025), false, "Finance", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("dd947ba9-556f-4356-9fa9-9f95fb7b1638"), null, new DateTime(2025, 10, 12, 2, 53, 51, 97, DateTimeKind.Utc).AddTicks(2080), null, null, null, null, null, new DateTime(2025, 10, 12, 2, 53, 51, 97, DateTimeKind.Utc).AddTicks(2080), false, "Inventory", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null }
                });

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 12, 2, 53, 51, 97, DateTimeKind.Utc).AddTicks(1666));

            migrationBuilder.AddForeignKey(
                name: "FK_WorkflowActivityLogs_WorkflowStepInstances_WorkflowStepInstanceId",
                table: "WorkflowActivityLogs",
                column: "WorkflowStepInstanceId",
                principalTable: "WorkflowStepInstances",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkflowApprovals_WorkflowStepInstances_WorkflowStepInstanceId",
                table: "WorkflowApprovals",
                column: "WorkflowStepInstanceId",
                principalTable: "WorkflowStepInstances",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
