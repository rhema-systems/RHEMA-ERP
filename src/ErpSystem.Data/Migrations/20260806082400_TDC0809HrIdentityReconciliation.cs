using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class TDC0809HrIdentityReconciliation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HrIdentityReconciliationRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: false),
                    Trigger = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RequestedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CandidateCount = table.Column<int>(type: "int", nullable: false),
                    ReconciledCount = table.Column<int>(type: "int", nullable: false),
                    ReviewRequiredCount = table.Column<int>(type: "int", nullable: false),
                    FailedCount = table.Column<int>(type: "int", nullable: false),
                    Error = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_HrIdentityReconciliationRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HrIdentityReconciliationRuns_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HrIdentityReconciliationRuns_Users_RequestedById",
                        column: x => x.RequestedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HrIdentityWorkflowIssues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkflowStepInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WorkflowApprovalId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IssueType = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StaleAssigneeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SuggestedReplacementUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    DetectedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResolvedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReplacementUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ResolvedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolutionNote = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_HrIdentityWorkflowIssues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HrIdentityWorkflowIssues_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HrIdentityWorkflowIssues_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HrIdentityWorkflowIssues_Users_ReplacementUserId",
                        column: x => x.ReplacementUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HrIdentityWorkflowIssues_Users_ResolvedById",
                        column: x => x.ResolvedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HrIdentityWorkflowIssues_Users_StaleAssigneeId",
                        column: x => x.StaleAssigneeId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HrIdentityWorkflowIssues_Users_SuggestedReplacementUserId",
                        column: x => x.SuggestedReplacementUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HrIdentityWorkflowIssues_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HrIdentityWorkflowIssues_WorkflowApprovals_WorkflowApprovalId",
                        column: x => x.WorkflowApprovalId,
                        principalTable: "WorkflowApprovals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HrIdentityWorkflowIssues_WorkflowInstances_WorkflowInstanceId",
                        column: x => x.WorkflowInstanceId,
                        principalTable: "WorkflowInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HrIdentityWorkflowIssues_WorkflowStepInstances_WorkflowStepInstanceId",
                        column: x => x.WorkflowStepInstanceId,
                        principalTable: "WorkflowStepInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HrIdentityReconciliationItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttemptNumber = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AccessStateChanged = table.Column<bool>(type: "bit", nullable: false),
                    DepartmentChanged = table.Column<bool>(type: "bit", nullable: false),
                    ManagerChanged = table.Column<bool>(type: "bit", nullable: false),
                    RoleSnapshotChanged = table.Column<bool>(type: "bit", nullable: false),
                    PreviousDepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CurrentDepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PreviousManagerEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CurrentManagerEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SessionsRevoked = table.Column<int>(type: "int", nullable: false),
                    RefreshTokensRevoked = table.Column<int>(type: "int", nullable: false),
                    ResponsibilityAssignmentsSuspended = table.Column<int>(type: "int", nullable: false),
                    WorkflowAssignmentsReassigned = table.Column<int>(type: "int", nullable: false),
                    WorkflowIssuesCreated = table.Column<int>(type: "int", nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Error = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ProcessedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
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
                    table.PrimaryKey("PK_HrIdentityReconciliationItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HrIdentityReconciliationItems_Departments_CurrentDepartmentId",
                        column: x => x.CurrentDepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HrIdentityReconciliationItems_Departments_PreviousDepartmentId",
                        column: x => x.PreviousDepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HrIdentityReconciliationItems_Employees_CurrentManagerEmployeeId",
                        column: x => x.CurrentManagerEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HrIdentityReconciliationItems_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HrIdentityReconciliationItems_Employees_PreviousManagerEmployeeId",
                        column: x => x.PreviousManagerEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HrIdentityReconciliationItems_HrIdentityReconciliationRuns_RunId",
                        column: x => x.RunId,
                        principalTable: "HrIdentityReconciliationRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HrIdentityReconciliationItems_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HrIdentityReconciliationItems_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HrIdentityReconciliationStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HrAccessEligible = table.Column<bool>(type: "bit", nullable: false),
                    AccessSuspendedByReconciliation = table.Column<bool>(type: "bit", nullable: false),
                    ReactivationReviewRequired = table.Column<bool>(type: "bit", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ManagerEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ManagerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RoleNamesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SuspendedUserTenantIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourceFingerprint = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false),
                    LastObservedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastReconciledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_HrIdentityReconciliationStates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HrIdentityReconciliationStates_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HrIdentityReconciliationStates_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HrIdentityReconciliationStates_Employees_ManagerEmployeeId",
                        column: x => x.ManagerEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HrIdentityReconciliationStates_HrIdentityReconciliationRuns_LastRunId",
                        column: x => x.LastRunId,
                        principalTable: "HrIdentityReconciliationRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HrIdentityReconciliationStates_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HrIdentityReconciliationStates_Users_ManagerUserId",
                        column: x => x.ManagerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HrIdentityReconciliationStates_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HrIdentityReconciliationItems_CurrentDepartmentId",
                table: "HrIdentityReconciliationItems",
                column: "CurrentDepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_HrIdentityReconciliationItems_CurrentManagerEmployeeId",
                table: "HrIdentityReconciliationItems",
                column: "CurrentManagerEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_HrIdentityReconciliationItems_EmployeeId",
                table: "HrIdentityReconciliationItems",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_HrIdentityReconciliationItems_PreviousDepartmentId",
                table: "HrIdentityReconciliationItems",
                column: "PreviousDepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_HrIdentityReconciliationItems_PreviousManagerEmployeeId",
                table: "HrIdentityReconciliationItems",
                column: "PreviousManagerEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_HrIdentityReconciliationItems_RunId",
                table: "HrIdentityReconciliationItems",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_HrIdentityReconciliationItems_TenantId_RunId_UserId_AttemptNumber",
                table: "HrIdentityReconciliationItems",
                columns: new[] { "TenantId", "RunId", "UserId", "AttemptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HrIdentityReconciliationItems_TenantId_Status_ProcessedAtUtc",
                table: "HrIdentityReconciliationItems",
                columns: new[] { "TenantId", "Status", "ProcessedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_HrIdentityReconciliationItems_UserId",
                table: "HrIdentityReconciliationItems",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_HrIdentityReconciliationRuns_RequestedById",
                table: "HrIdentityReconciliationRuns",
                column: "RequestedById");

            migrationBuilder.CreateIndex(
                name: "IX_HrIdentityReconciliationRuns_TenantId_IdempotencyKey",
                table: "HrIdentityReconciliationRuns",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HrIdentityReconciliationRuns_TenantId_StartedAtUtc",
                table: "HrIdentityReconciliationRuns",
                columns: new[] { "TenantId", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_HrIdentityReconciliationStates_DepartmentId",
                table: "HrIdentityReconciliationStates",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_HrIdentityReconciliationStates_EmployeeId",
                table: "HrIdentityReconciliationStates",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_HrIdentityReconciliationStates_LastRunId",
                table: "HrIdentityReconciliationStates",
                column: "LastRunId");

            migrationBuilder.CreateIndex(
                name: "IX_HrIdentityReconciliationStates_ManagerEmployeeId",
                table: "HrIdentityReconciliationStates",
                column: "ManagerEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_HrIdentityReconciliationStates_ManagerUserId",
                table: "HrIdentityReconciliationStates",
                column: "ManagerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_HrIdentityReconciliationStates_TenantId_EmployeeId",
                table: "HrIdentityReconciliationStates",
                columns: new[] { "TenantId", "EmployeeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HrIdentityReconciliationStates_TenantId_ReactivationReviewRequired",
                table: "HrIdentityReconciliationStates",
                columns: new[] { "TenantId", "ReactivationReviewRequired" });

            migrationBuilder.CreateIndex(
                name: "IX_HrIdentityReconciliationStates_TenantId_UserId",
                table: "HrIdentityReconciliationStates",
                columns: new[] { "TenantId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HrIdentityReconciliationStates_UserId",
                table: "HrIdentityReconciliationStates",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_HrIdentityWorkflowIssues_EmployeeId",
                table: "HrIdentityWorkflowIssues",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_HrIdentityWorkflowIssues_ReplacementUserId",
                table: "HrIdentityWorkflowIssues",
                column: "ReplacementUserId");

            migrationBuilder.CreateIndex(
                name: "IX_HrIdentityWorkflowIssues_ResolvedById",
                table: "HrIdentityWorkflowIssues",
                column: "ResolvedById");

            migrationBuilder.CreateIndex(
                name: "IX_HrIdentityWorkflowIssues_StaleAssigneeId",
                table: "HrIdentityWorkflowIssues",
                column: "StaleAssigneeId");

            migrationBuilder.CreateIndex(
                name: "IX_HrIdentityWorkflowIssues_SuggestedReplacementUserId",
                table: "HrIdentityWorkflowIssues",
                column: "SuggestedReplacementUserId");

            migrationBuilder.CreateIndex(
                name: "IX_HrIdentityWorkflowIssues_TenantId_Status_DetectedAtUtc",
                table: "HrIdentityWorkflowIssues",
                columns: new[] { "TenantId", "Status", "DetectedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_HrIdentityWorkflowIssues_TenantId_WorkflowApprovalId_IssueType",
                table: "HrIdentityWorkflowIssues",
                columns: new[] { "TenantId", "WorkflowApprovalId", "IssueType" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [Status] = 0 AND [WorkflowApprovalId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_HrIdentityWorkflowIssues_UserId",
                table: "HrIdentityWorkflowIssues",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_HrIdentityWorkflowIssues_WorkflowApprovalId",
                table: "HrIdentityWorkflowIssues",
                column: "WorkflowApprovalId");

            migrationBuilder.CreateIndex(
                name: "IX_HrIdentityWorkflowIssues_WorkflowInstanceId",
                table: "HrIdentityWorkflowIssues",
                column: "WorkflowInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_HrIdentityWorkflowIssues_WorkflowStepInstanceId",
                table: "HrIdentityWorkflowIssues",
                column: "WorkflowStepInstanceId");

            CreateTrigger(migrationBuilder,
                """
                CREATE TRIGGER [TR_HrIdentityReconciliationRuns_TenantGuard]
                ON [HrIdentityReconciliationRuns]
                AFTER INSERT, UPDATE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        LEFT JOIN [Users] u ON u.[Id] = i.[RequestedById]
                        WHERE i.[TenantId] = '00000000-0000-0000-0000-000000000000'
                           OR (i.[RequestedById] IS NOT NULL AND (u.[Id] IS NULL OR u.[TenantId] <> i.[TenantId])))
                        THROW 51080, 'HR/Identity reconciliation run tenant linkage is invalid.', 1;
                END
                """);

            CreateTrigger(migrationBuilder,
                """
                CREATE TRIGGER [TR_HrIdentityReconciliationRuns_CompletedImmutable]
                ON [HrIdentityReconciliationRuns]
                AFTER UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.[Id] = d.[Id] WHERE i.[Id] IS NULL)
                        THROW 51081, 'HR/Identity reconciliation runs cannot be deleted.', 1;
                    IF EXISTS (
                        SELECT 1 FROM deleted d JOIN inserted i ON i.[Id] = d.[Id]
                        WHERE d.[Status] <> 0
                           OR i.[TenantId] <> d.[TenantId]
                           OR i.[IdempotencyKey] <> d.[IdempotencyKey]
                           OR i.[StartedAtUtc] <> d.[StartedAtUtc]
                           OR ISNULL(i.[RequestedById], '00000000-0000-0000-0000-000000000000') <>
                              ISNULL(d.[RequestedById], '00000000-0000-0000-0000-000000000000'))
                        THROW 51082, 'Completed HR/Identity reconciliation runs are immutable.', 1;
                END
                """);

            CreateTrigger(migrationBuilder,
                """
                CREATE TRIGGER [TR_HrIdentityReconciliationItems_TenantGuard]
                ON [HrIdentityReconciliationItems]
                AFTER INSERT, UPDATE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        JOIN [HrIdentityReconciliationRuns] r ON r.[Id] = i.[RunId]
                        JOIN [Users] u ON u.[Id] = i.[UserId]
                        JOIN [Employees] e ON e.[Id] = i.[EmployeeId]
                        LEFT JOIN [Departments] pd ON pd.[Id] = i.[PreviousDepartmentId]
                        LEFT JOIN [Departments] cd ON cd.[Id] = i.[CurrentDepartmentId]
                        LEFT JOIN [Employees] pm ON pm.[Id] = i.[PreviousManagerEmployeeId]
                        LEFT JOIN [Employees] cm ON cm.[Id] = i.[CurrentManagerEmployeeId]
                        WHERE r.[TenantId] <> i.[TenantId]
                           OR u.[TenantId] <> i.[TenantId]
                           OR e.[TenantId] <> i.[TenantId]
                           OR (pd.[Id] IS NOT NULL AND pd.[TenantId] <> i.[TenantId])
                           OR (cd.[Id] IS NOT NULL AND cd.[TenantId] <> i.[TenantId])
                           OR (pm.[Id] IS NOT NULL AND pm.[TenantId] <> i.[TenantId])
                           OR (cm.[Id] IS NOT NULL AND cm.[TenantId] <> i.[TenantId]))
                        THROW 51083, 'HR/Identity reconciliation item tenant linkage is invalid.', 1;
                END
                """);

            CreateTrigger(migrationBuilder,
                """
                CREATE TRIGGER [TR_HrIdentityReconciliationItems_NoMutation]
                ON [HrIdentityReconciliationItems]
                AFTER UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    THROW 51084, 'HR/Identity reconciliation items are append-only.', 1;
                END
                """);

            CreateTrigger(migrationBuilder,
                """
                CREATE TRIGGER [TR_HrIdentityReconciliationStates_TenantGuard]
                ON [HrIdentityReconciliationStates]
                AFTER INSERT, UPDATE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        JOIN [Users] u ON u.[Id] = i.[UserId]
                        JOIN [Employees] e ON e.[Id] = i.[EmployeeId]
                        LEFT JOIN [Departments] d ON d.[Id] = i.[DepartmentId]
                        LEFT JOIN [Employees] m ON m.[Id] = i.[ManagerEmployeeId]
                        LEFT JOIN [Users] mu ON mu.[Id] = i.[ManagerUserId]
                        LEFT JOIN [HrIdentityReconciliationRuns] r ON r.[Id] = i.[LastRunId]
                        WHERE u.[TenantId] <> i.[TenantId]
                           OR e.[TenantId] <> i.[TenantId]
                           OR (d.[Id] IS NOT NULL AND d.[TenantId] <> i.[TenantId])
                           OR (m.[Id] IS NOT NULL AND m.[TenantId] <> i.[TenantId])
                           OR (mu.[Id] IS NOT NULL AND mu.[TenantId] <> i.[TenantId])
                           OR (r.[Id] IS NOT NULL AND r.[TenantId] <> i.[TenantId]))
                        THROW 51085, 'HR/Identity reconciliation state tenant linkage is invalid.', 1;
                END
                """);

            CreateTrigger(migrationBuilder,
                """
                CREATE TRIGGER [TR_HrIdentityWorkflowIssues_TenantGuard]
                ON [HrIdentityWorkflowIssues]
                AFTER INSERT, UPDATE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        JOIN [Users] u ON u.[Id] = i.[UserId]
                        JOIN [Employees] e ON e.[Id] = i.[EmployeeId]
                        JOIN [WorkflowInstances] wi ON wi.[Id] = i.[WorkflowInstanceId]
                        LEFT JOIN [WorkflowStepInstances] ws ON ws.[Id] = i.[WorkflowStepInstanceId]
                        LEFT JOIN [WorkflowApprovals] wa ON wa.[Id] = i.[WorkflowApprovalId]
                        LEFT JOIN [Users] stale ON stale.[Id] = i.[StaleAssigneeId]
                        LEFT JOIN [Users] suggested ON suggested.[Id] = i.[SuggestedReplacementUserId]
                        LEFT JOIN [Users] replacement ON replacement.[Id] = i.[ReplacementUserId]
                        LEFT JOIN [Users] resolver ON resolver.[Id] = i.[ResolvedById]
                        WHERE u.[TenantId] <> i.[TenantId]
                           OR e.[TenantId] <> i.[TenantId]
                           OR wi.[TenantId] <> i.[TenantId]
                           OR (ws.[Id] IS NOT NULL AND (ws.[TenantId] <> i.[TenantId] OR ws.[WorkflowInstanceId] <> i.[WorkflowInstanceId]))
                           OR (wa.[Id] IS NOT NULL AND (wa.[TenantId] <> i.[TenantId] OR wa.[StepInstanceId] <> i.[WorkflowStepInstanceId]))
                           OR (stale.[Id] IS NOT NULL AND stale.[TenantId] <> i.[TenantId])
                           OR (suggested.[Id] IS NOT NULL AND suggested.[TenantId] <> i.[TenantId])
                           OR (replacement.[Id] IS NOT NULL AND replacement.[TenantId] <> i.[TenantId])
                           OR (resolver.[Id] IS NOT NULL AND resolver.[TenantId] <> i.[TenantId]))
                        THROW 51086, 'HR/Identity workflow issue tenant linkage is invalid.', 1;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HrIdentityReconciliationItems");

            migrationBuilder.DropTable(
                name: "HrIdentityReconciliationStates");

            migrationBuilder.DropTable(
                name: "HrIdentityWorkflowIssues");

            migrationBuilder.DropTable(
                name: "HrIdentityReconciliationRuns");
        }

        private static void CreateTrigger(MigrationBuilder migrationBuilder, string triggerSql)
        {
            // CREATE TRIGGER must be the first statement in its SQL batch. EF's
            // idempotent script wraps migration SQL in an IF block, so execute
            // the definition as a dynamic batch to keep both MigrateAsync and
            // idempotent deployment scripts valid.
            migrationBuilder.Sql($"EXEC(N'{triggerSql.Replace("'", "''", StringComparison.Ordinal)}')");
        }
    }
}
