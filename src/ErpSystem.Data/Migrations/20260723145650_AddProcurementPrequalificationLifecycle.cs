using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProcurementPrequalificationLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProcurementPrequalificationExercises",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CategoryIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OpensAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClosesAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ValidityMonths = table.Column<int>(type: "int", nullable: false),
                    PassingScore = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    WorkflowDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AdvertisementReference = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    AdvertisementEvidenceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AdvertisedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AdvertisedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClosedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubmittedForApprovalAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SubmittedForApprovalById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DecisionReference = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    DecisionEvidenceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DecisionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DecidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecidedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LifecycleSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IntegrityHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
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
                    table.PrimaryKey("PK_ProcurementPrequalificationExercises", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcurementPrequalificationExercises_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementPrequalificationExercises_WorkflowDefinitions_WorkflowDefinitionId",
                        column: x => x.WorkflowDefinitionId,
                        principalTable: "WorkflowDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementPrequalificationExercises_WorkflowInstances_WorkflowInstanceId",
                        column: x => x.WorkflowInstanceId,
                        principalTable: "WorkflowInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementPrequalificationApplications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExerciseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApplicationNumber = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    CategoryIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EvidenceJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SubmittedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SubmittedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EvaluatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EvaluatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TotalScore = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Passed = table.Column<bool>(type: "bit", nullable: true),
                    EvaluationRemarks = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RecommendationEvidenceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    EvaluationSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IntegrityHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
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
                    table.PrimaryKey("PK_ProcurementPrequalificationApplications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcurementPrequalificationApplications_BusinessPartners_BusinessPartnerId",
                        column: x => x.BusinessPartnerId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementPrequalificationApplications_ProcurementPrequalificationExercises_ExerciseId",
                        column: x => x.ExerciseId,
                        principalTable: "ProcurementPrequalificationExercises",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementPrequalificationApplications_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementPrequalificationCriteria",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExerciseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Weight = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    MinimumScore = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    IsMandatory = table.Column<bool>(type: "bit", nullable: false),
                    RequiresEvidence = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_ProcurementPrequalificationCriteria", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcurementPrequalificationCriteria_ProcurementPrequalificationExercises_ExerciseId",
                        column: x => x.ExerciseId,
                        principalTable: "ProcurementPrequalificationExercises",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementPrequalificationCriteria_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementQualifiedListEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExerciseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ValidFromUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ApprovalReference = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ApprovalEvidenceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ExpiredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevokedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevocationReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    LifecycleSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IntegrityHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("PK_ProcurementQualifiedListEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcurementQualifiedListEntries_BusinessPartners_BusinessPartnerId",
                        column: x => x.BusinessPartnerId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementQualifiedListEntries_PartnerCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "PartnerCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementQualifiedListEntries_ProcurementPrequalificationApplications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "ProcurementPrequalificationApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementQualifiedListEntries_ProcurementPrequalificationExercises_ExerciseId",
                        column: x => x.ExerciseId,
                        principalTable: "ProcurementPrequalificationExercises",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementQualifiedListEntries_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementPrequalificationScores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CriterionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Score = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    MeetsRequirement = table.Column<bool>(type: "bit", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    EvidenceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    EvaluatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EvaluatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_ProcurementPrequalificationScores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcurementPrequalificationScores_ProcurementPrequalificationApplications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "ProcurementPrequalificationApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementPrequalificationScores_ProcurementPrequalificationCriteria_CriterionId",
                        column: x => x.CriterionId,
                        principalTable: "ProcurementPrequalificationCriteria",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementPrequalificationScores_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPrequalificationApplications_BusinessPartnerId",
                table: "ProcurementPrequalificationApplications",
                column: "BusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPrequalificationApplications_ExerciseId",
                table: "ProcurementPrequalificationApplications",
                column: "ExerciseId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPrequalificationApplications_TenantId_ApplicationNumber",
                table: "ProcurementPrequalificationApplications",
                columns: new[] { "TenantId", "ApplicationNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPrequalificationApplications_TenantId_ExerciseId_BusinessPartnerId",
                table: "ProcurementPrequalificationApplications",
                columns: new[] { "TenantId", "ExerciseId", "BusinessPartnerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPrequalificationApplications_TenantId_ExerciseId_Status",
                table: "ProcurementPrequalificationApplications",
                columns: new[] { "TenantId", "ExerciseId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPrequalificationCriteria_ExerciseId",
                table: "ProcurementPrequalificationCriteria",
                column: "ExerciseId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPrequalificationCriteria_TenantId_ExerciseId_Code",
                table: "ProcurementPrequalificationCriteria",
                columns: new[] { "TenantId", "ExerciseId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPrequalificationCriteria_TenantId_ExerciseId_SortOrder",
                table: "ProcurementPrequalificationCriteria",
                columns: new[] { "TenantId", "ExerciseId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPrequalificationExercises_TenantId_Reference",
                table: "ProcurementPrequalificationExercises",
                columns: new[] { "TenantId", "Reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPrequalificationExercises_TenantId_Status_ClosesAtUtc",
                table: "ProcurementPrequalificationExercises",
                columns: new[] { "TenantId", "Status", "ClosesAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPrequalificationExercises_TenantId_WorkflowDefinitionId",
                table: "ProcurementPrequalificationExercises",
                columns: new[] { "TenantId", "WorkflowDefinitionId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPrequalificationExercises_TenantId_WorkflowInstanceId",
                table: "ProcurementPrequalificationExercises",
                columns: new[] { "TenantId", "WorkflowInstanceId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPrequalificationExercises_WorkflowDefinitionId",
                table: "ProcurementPrequalificationExercises",
                column: "WorkflowDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPrequalificationExercises_WorkflowInstanceId",
                table: "ProcurementPrequalificationExercises",
                column: "WorkflowInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPrequalificationScores_ApplicationId",
                table: "ProcurementPrequalificationScores",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPrequalificationScores_CriterionId",
                table: "ProcurementPrequalificationScores",
                column: "CriterionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPrequalificationScores_TenantId_ApplicationId_CriterionId",
                table: "ProcurementPrequalificationScores",
                columns: new[] { "TenantId", "ApplicationId", "CriterionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPrequalificationScores_TenantId_EvaluatedById_EvaluatedAtUtc",
                table: "ProcurementPrequalificationScores",
                columns: new[] { "TenantId", "EvaluatedById", "EvaluatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementQualifiedListEntries_ApplicationId",
                table: "ProcurementQualifiedListEntries",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementQualifiedListEntries_BusinessPartnerId",
                table: "ProcurementQualifiedListEntries",
                column: "BusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementQualifiedListEntries_CategoryId",
                table: "ProcurementQualifiedListEntries",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementQualifiedListEntries_ExerciseId",
                table: "ProcurementQualifiedListEntries",
                column: "ExerciseId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementQualifiedListEntries_TenantId_BusinessPartnerId_CategoryId_Status_ExpiresAtUtc",
                table: "ProcurementQualifiedListEntries",
                columns: new[] { "TenantId", "BusinessPartnerId", "CategoryId", "Status", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementQualifiedListEntries_TenantId_ExerciseId_ApplicationId_CategoryId",
                table: "ProcurementQualifiedListEntries",
                columns: new[] { "TenantId", "ExerciseId", "ApplicationId", "CategoryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementQualifiedListEntries_TenantId_ExerciseId_Status",
                table: "ProcurementQualifiedListEntries",
                columns: new[] { "TenantId", "ExerciseId", "Status" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProcurementPrequalificationExercises_Core",
                table: "ProcurementPrequalificationExercises",
                sql: @"[Status] BETWEEN 0 AND 7
AND [ClosesAtUtc] > [OpensAtUtc]
AND [ValidityMonths] BETWEEN 1 AND 60
AND [PassingScore] > 0 AND [PassingScore] <= 100
AND ISJSON([CategoryIdsJson]) = 1
AND LEN([IntegrityHash]) = 64
AND NULLIF(LTRIM(RTRIM([Reference])), '') IS NOT NULL
AND NULLIF(LTRIM(RTRIM([Title])), '') IS NOT NULL
AND NULLIF(LTRIM(RTRIM([Description])), '') IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProcurementPrequalificationExercises_Evidence",
                table: "ProcurementPrequalificationExercises",
                sql: @"([Status] = 0 OR (
    NULLIF(LTRIM(RTRIM([AdvertisementReference])), '') IS NOT NULL
    AND NULLIF(LTRIM(RTRIM([AdvertisementEvidenceReference])), '') IS NOT NULL
    AND [AdvertisedAtUtc] IS NOT NULL AND [AdvertisedById] IS NOT NULL))
AND ([Status] NOT IN (2,3,4,5,6,7) OR ([ClosedAtUtc] IS NOT NULL AND [ClosedById] IS NOT NULL))
AND ([Status] NOT IN (4,5,6,7) OR (
    [WorkflowInstanceId] IS NOT NULL
    AND [SubmittedForApprovalAtUtc] IS NOT NULL
    AND [SubmittedForApprovalById] IS NOT NULL))
AND ([Status] NOT IN (5,6,7) OR (
    NULLIF(LTRIM(RTRIM([DecisionReference])), '') IS NOT NULL
    AND NULLIF(LTRIM(RTRIM([DecisionEvidenceReference])), '') IS NOT NULL
    AND NULLIF(LTRIM(RTRIM([DecisionReason])), '') IS NOT NULL
    AND [DecidedAtUtc] IS NOT NULL AND [DecidedById] IS NOT NULL))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProcurementPrequalificationCriteria_Core",
                table: "ProcurementPrequalificationCriteria",
                sql: @"[Weight] > 0 AND [Weight] <= 100
AND [MinimumScore] >= 0 AND [MinimumScore] <= 100
AND NULLIF(LTRIM(RTRIM([Code])), '') IS NOT NULL
AND NULLIF(LTRIM(RTRIM([Name])), '') IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProcurementPrequalificationApplications_Core",
                table: "ProcurementPrequalificationApplications",
                sql: @"[Status] BETWEEN 0 AND 4
AND ISJSON([CategoryIdsJson]) = 1
AND ISJSON([EvidenceJson]) = 1
AND LEN([IntegrityHash]) = 64
AND NULLIF(LTRIM(RTRIM([ApplicationNumber])), '') IS NOT NULL
AND (([Status] = 0 AND [EvaluatedAtUtc] IS NULL AND [EvaluatedById] IS NULL
      AND [TotalScore] IS NULL AND [Passed] IS NULL)
 OR ([Status] IN (1,2,3,4) AND [EvaluatedAtUtc] IS NOT NULL AND [EvaluatedById] IS NOT NULL
      AND [TotalScore] BETWEEN 0 AND 100 AND [Passed] IS NOT NULL
      AND NULLIF(LTRIM(RTRIM([EvaluationRemarks])), '') IS NOT NULL
      AND NULLIF(LTRIM(RTRIM([RecommendationEvidenceReference])), '') IS NOT NULL))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProcurementPrequalificationScores_Core",
                table: "ProcurementPrequalificationScores",
                sql: @"[Score] >= 0 AND [Score] <= 100
AND NULLIF(LTRIM(RTRIM([Reason])), '') IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProcurementQualifiedListEntries_Core",
                table: "ProcurementQualifiedListEntries",
                sql: @"[Status] BETWEEN 0 AND 2
AND [ExpiresAtUtc] > [ValidFromUtc]
AND LEN([IntegrityHash]) = 64
AND NULLIF(LTRIM(RTRIM([ApprovalReference])), '') IS NOT NULL
AND NULLIF(LTRIM(RTRIM([ApprovalEvidenceReference])), '') IS NOT NULL
AND ([Status] <> 1 OR [ExpiredAtUtc] IS NOT NULL)
AND ([Status] <> 2 OR ([RevokedAtUtc] IS NOT NULL AND NULLIF(LTRIM(RTRIM([RevocationReason])), '') IS NOT NULL))");

            migrationBuilder.Sql(@"
CREATE OR ALTER TRIGGER [dbo].[TR_ProcurementPrequalificationExercises_Lifecycle]
ON [dbo].[ProcurementPrequalificationExercises]
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM deleted) AND NOT EXISTS (SELECT 1 FROM inserted)
        THROW 51140, 'Prequalification exercises cannot be deleted.', 1;

    IF EXISTS (
        SELECT 1 FROM inserted i LEFT JOIN deleted d ON d.[Id] = i.[Id]
        WHERE d.[Id] IS NULL AND i.[Status] <> 0
    ) THROW 51160, 'Prequalification exercises must be created as Draft.', 1;

    IF EXISTS (
        SELECT 1
        FROM inserted i
        LEFT JOIN [dbo].[WorkflowDefinitions] w
          ON w.[Id] = i.[WorkflowDefinitionId] AND w.[TenantId] = i.[TenantId] AND w.[IsDeleted] = 0
        LEFT JOIN [dbo].[WorkflowInstances] wi
          ON wi.[Id] = i.[WorkflowInstanceId] AND wi.[TenantId] = i.[TenantId]
             AND wi.[WorkflowDefinitionId] = i.[WorkflowDefinitionId]
        WHERE w.[Id] IS NULL OR (i.[WorkflowInstanceId] IS NOT NULL AND wi.[Id] IS NULL)
    ) THROW 51141, 'Prequalification workflow lineage must remain within the exercise tenant and exact definition.', 1;

    IF EXISTS (
        SELECT 1 FROM inserted i JOIN deleted d ON d.[Id] = i.[Id]
        WHERE i.[TenantId] <> d.[TenantId]
           OR i.[Reference] <> d.[Reference] OR i.[Title] <> d.[Title]
           OR i.[Description] <> d.[Description] OR i.[CategoryIdsJson] <> d.[CategoryIdsJson]
           OR i.[OpensAtUtc] <> d.[OpensAtUtc] OR i.[ClosesAtUtc] <> d.[ClosesAtUtc]
           OR i.[ValidityMonths] <> d.[ValidityMonths] OR i.[PassingScore] <> d.[PassingScore]
           OR i.[WorkflowDefinitionId] <> d.[WorkflowDefinitionId]
           OR i.[CreatedAt] <> d.[CreatedAt] OR ISNULL(i.[CreatedById], '00000000-0000-0000-0000-000000000000')
                <> ISNULL(d.[CreatedById], '00000000-0000-0000-0000-000000000000')
           OR i.[IsDeleted] <> d.[IsDeleted]
    ) THROW 51142, 'Prequalification scope, criteria basis, workflow definition, tenant, and lineage are immutable.', 1;

    IF EXISTS (
        SELECT 1 FROM inserted i JOIN deleted d ON d.[Id] = i.[Id]
        WHERE d.[Status] IN (6,7)
           OR NOT (
                i.[Status] = d.[Status]
                OR (d.[Status] = 0 AND i.[Status] = 1)
                OR (d.[Status] = 1 AND i.[Status] = 2)
                OR (d.[Status] = 2 AND i.[Status] = 3)
                OR (d.[Status] = 3 AND i.[Status] = 4)
                OR (d.[Status] = 4 AND i.[Status] IN (5,6))
                OR (d.[Status] = 5 AND i.[Status] = 7)
           )
    ) THROW 51143, 'Prequalification lifecycle transition is invalid or the terminal record is immutable.', 1;

    IF EXISTS (
        SELECT 1
        FROM inserted i JOIN deleted d ON d.[Id] = i.[Id]
        WHERE d.[Status] = 0 AND i.[Status] = 1
          AND (
              NOT EXISTS (SELECT 1 FROM OPENJSON(i.[CategoryIdsJson]))
              OR NOT EXISTS (
                  SELECT 1 FROM [dbo].[ProcurementPrequalificationCriteria] c
                  WHERE c.[ExerciseId] = i.[Id] AND c.[TenantId] = i.[TenantId] AND c.[IsDeleted] = 0)
              OR ABS(ISNULL((
                  SELECT SUM(c.[Weight]) FROM [dbo].[ProcurementPrequalificationCriteria] c
                  WHERE c.[ExerciseId] = i.[Id] AND c.[TenantId] = i.[TenantId] AND c.[IsDeleted] = 0), 0) - 100) > 0.01
              OR NOT EXISTS (
                  SELECT 1 FROM [dbo].[ProcurementPrequalificationCriteria] c
                  WHERE c.[ExerciseId] = i.[Id] AND c.[TenantId] = i.[TenantId]
                    AND c.[IsDeleted] = 0 AND c.[IsMandatory] = 1)
          )
    ) THROW 51161, 'Advertisement requires categories and a locked mandatory scorecard weighted to exactly 100.', 1;

    IF EXISTS (
        SELECT 1
        FROM inserted i JOIN deleted d ON d.[Id] = i.[Id]
        WHERE d.[Status] = 3 AND i.[Status] = 4
          AND (
              NOT EXISTS (
                  SELECT 1 FROM [dbo].[ProcurementPrequalificationApplications] a
                  WHERE a.[ExerciseId] = i.[Id] AND a.[TenantId] = i.[TenantId] AND a.[IsDeleted] = 0)
              OR EXISTS (
                  SELECT 1 FROM [dbo].[ProcurementPrequalificationApplications] a
                  WHERE a.[ExerciseId] = i.[Id] AND a.[TenantId] = i.[TenantId]
                    AND a.[IsDeleted] = 0 AND a.[Status] = 0)
          )
    ) THROW 51162, 'Every submitted application must have an immutable evaluation before approval.', 1;

    IF EXISTS (
        SELECT 1 FROM inserted i JOIN deleted d ON d.[Id] = i.[Id]
        WHERE (d.[AdvertisementReference] IS NOT NULL AND
               (ISNULL(i.[AdvertisementReference], '') <> d.[AdvertisementReference]
                OR ISNULL(i.[AdvertisementEvidenceReference], '') <> ISNULL(d.[AdvertisementEvidenceReference], '')
                OR i.[AdvertisedAtUtc] <> d.[AdvertisedAtUtc] OR i.[AdvertisedById] <> d.[AdvertisedById]))
           OR (d.[ClosedAtUtc] IS NOT NULL AND (i.[ClosedAtUtc] <> d.[ClosedAtUtc] OR i.[ClosedById] <> d.[ClosedById]))
           OR (d.[WorkflowInstanceId] IS NOT NULL AND
               (i.[WorkflowInstanceId] <> d.[WorkflowInstanceId]
                OR i.[SubmittedForApprovalAtUtc] <> d.[SubmittedForApprovalAtUtc]
                OR i.[SubmittedForApprovalById] <> d.[SubmittedForApprovalById]))
           OR (d.[DecisionReference] IS NOT NULL AND
               (ISNULL(i.[DecisionReference], '') <> d.[DecisionReference]
                OR ISNULL(i.[DecisionEvidenceReference], '') <> ISNULL(d.[DecisionEvidenceReference], '')
                OR ISNULL(i.[DecisionReason], '') <> ISNULL(d.[DecisionReason], '')
                OR i.[DecidedAtUtc] <> d.[DecidedAtUtc] OR i.[DecidedById] <> d.[DecidedById]))
    ) THROW 51144, 'Captured advertisement, closure, workflow, and decision evidence is immutable.', 1;
END");

            migrationBuilder.Sql(@"
CREATE OR ALTER TRIGGER [dbo].[TR_ProcurementPrequalificationCriteria_ImmutableAfterAdvertisement]
ON [dbo].[ProcurementPrequalificationCriteria]
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (
        SELECT 1
        FROM inserted i
        LEFT JOIN deleted d ON d.[Id] = i.[Id]
        LEFT JOIN [dbo].[ProcurementPrequalificationExercises] e
          ON e.[Id] = i.[ExerciseId] AND e.[TenantId] = i.[TenantId]
        WHERE d.[Id] IS NULL AND (e.[Id] IS NULL OR e.[Status] <> 0)
    ) THROW 51145, 'Criteria may be inserted only while the tenant exercise is Draft.', 1;

    IF EXISTS (
        SELECT 1
        FROM inserted i JOIN deleted d ON d.[Id] = i.[Id]
        JOIN [dbo].[ProcurementPrequalificationExercises] e ON e.[Id] = d.[ExerciseId]
        WHERE e.[Status] <> 0 OR i.[TenantId] <> d.[TenantId] OR i.[ExerciseId] <> d.[ExerciseId]
    ) THROW 51146, 'Advertised prequalification criteria and tenant lineage are immutable.', 1;

    IF EXISTS (
        SELECT 1
        FROM deleted d
        LEFT JOIN inserted i ON i.[Id] = d.[Id]
        JOIN [dbo].[ProcurementPrequalificationExercises] e ON e.[Id] = d.[ExerciseId]
        WHERE i.[Id] IS NULL AND e.[Status] <> 0
    ) THROW 51147, 'Advertised prequalification criteria cannot be deleted.', 1;
END");

            migrationBuilder.Sql(@"
CREATE OR ALTER TRIGGER [dbo].[TR_ProcurementPrequalificationApplications_Lifecycle]
ON [dbo].[ProcurementPrequalificationApplications]
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM deleted) AND NOT EXISTS (SELECT 1 FROM inserted)
        THROW 51148, 'Prequalification applications cannot be deleted.', 1;

    IF EXISTS (
        SELECT 1
        FROM inserted i
        LEFT JOIN [dbo].[ProcurementPrequalificationExercises] e
          ON e.[Id] = i.[ExerciseId] AND e.[TenantId] = i.[TenantId]
        LEFT JOIN [dbo].[BusinessPartners] b
          ON b.[Id] = i.[BusinessPartnerId] AND b.[TenantId] = i.[TenantId] AND b.[IsDeleted] = 0
        WHERE e.[Id] IS NULL OR b.[Id] IS NULL
    ) THROW 51149, 'Prequalification application tenant, exercise, or supplier lineage is invalid.', 1;

    IF EXISTS (
        SELECT 1 FROM inserted i LEFT JOIN deleted d ON d.[Id] = i.[Id]
        JOIN [dbo].[ProcurementPrequalificationExercises] e ON e.[Id] = i.[ExerciseId]
        WHERE d.[Id] IS NULL AND
              (i.[Status] <> 0 OR e.[Status] <> 1
               OR SYSUTCDATETIME() < e.[OpensAtUtc] OR SYSUTCDATETIME() > e.[ClosesAtUtc])
    ) THROW 51150, 'Applications may be submitted only during the advertised submission window.', 1;

    IF EXISTS (
        SELECT 1 FROM inserted i JOIN deleted d ON d.[Id] = i.[Id]
        WHERE i.[TenantId] <> d.[TenantId] OR i.[ExerciseId] <> d.[ExerciseId]
           OR i.[BusinessPartnerId] <> d.[BusinessPartnerId]
           OR i.[ApplicationNumber] <> d.[ApplicationNumber]
           OR i.[CategoryIdsJson] <> d.[CategoryIdsJson] OR i.[EvidenceJson] <> d.[EvidenceJson]
           OR i.[SubmittedAtUtc] <> d.[SubmittedAtUtc] OR i.[SubmittedById] <> d.[SubmittedById]
           OR i.[CreatedAt] <> d.[CreatedAt] OR i.[IsDeleted] <> d.[IsDeleted]
    ) THROW 51151, 'Submitted application identity, categories, evidence, tenant, and lineage are immutable.', 1;

    IF EXISTS (
        SELECT 1 FROM inserted i JOIN deleted d ON d.[Id] = i.[Id]
        WHERE d.[Status] IN (3,4)
           OR NOT (
                i.[Status] = d.[Status]
                OR (d.[Status] = 0 AND i.[Status] IN (1,2))
                OR (d.[Status] IN (1,2) AND i.[Status] IN (3,4))
           )
    ) THROW 51152, 'Prequalification application transition is invalid or the terminal outcome is immutable.', 1;
END");

            migrationBuilder.Sql(@"
CREATE OR ALTER TRIGGER [dbo].[TR_ProcurementPrequalificationScores_Immutable]
ON [dbo].[ProcurementPrequalificationScores]
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM deleted)
        THROW 51153, 'Prequalification scorecards are append-only and cannot be changed or deleted.', 1;

    IF EXISTS (
        SELECT 1
        FROM inserted i
        LEFT JOIN [dbo].[ProcurementPrequalificationApplications] a
          ON a.[Id] = i.[ApplicationId] AND a.[TenantId] = i.[TenantId]
        LEFT JOIN [dbo].[ProcurementPrequalificationCriteria] c
          ON c.[Id] = i.[CriterionId] AND c.[TenantId] = i.[TenantId]
             AND c.[ExerciseId] = a.[ExerciseId]
        LEFT JOIN [dbo].[ProcurementPrequalificationExercises] e ON e.[Id] = a.[ExerciseId]
        WHERE a.[Id] IS NULL OR c.[Id] IS NULL OR e.[Status] NOT IN (2,3)
              OR a.[Status] NOT IN (0,1,2)
    ) THROW 51154, 'Scorecard tenant lineage is invalid or evaluation is not open for the submitted application.', 1;
END");

            migrationBuilder.Sql(@"
CREATE OR ALTER TRIGGER [dbo].[TR_ProcurementQualifiedListEntries_Lifecycle]
ON [dbo].[ProcurementQualifiedListEntries]
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM deleted) AND NOT EXISTS (SELECT 1 FROM inserted)
        THROW 51155, 'Qualified-list entries cannot be deleted.', 1;

    IF EXISTS (
        SELECT 1
        FROM inserted i
        LEFT JOIN [dbo].[ProcurementPrequalificationExercises] e
          ON e.[Id] = i.[ExerciseId] AND e.[TenantId] = i.[TenantId]
        LEFT JOIN [dbo].[ProcurementPrequalificationApplications] a
          ON a.[Id] = i.[ApplicationId] AND a.[TenantId] = i.[TenantId]
             AND a.[ExerciseId] = i.[ExerciseId] AND a.[BusinessPartnerId] = i.[BusinessPartnerId]
        LEFT JOIN [dbo].[BusinessPartners] b
          ON b.[Id] = i.[BusinessPartnerId] AND b.[TenantId] = i.[TenantId] AND b.[IsDeleted] = 0
        LEFT JOIN [dbo].[PartnerCategories] c
          ON c.[Id] = i.[CategoryId] AND c.[TenantId] = i.[TenantId] AND c.[IsDeleted] = 0
        WHERE e.[Id] IS NULL OR a.[Id] IS NULL OR b.[Id] IS NULL OR c.[Id] IS NULL
    ) THROW 51156, 'Qualified-list exercise, application, supplier, category, or tenant lineage is invalid.', 1;

    IF EXISTS (
        SELECT 1
        FROM inserted i LEFT JOIN deleted d ON d.[Id] = i.[Id]
        JOIN [dbo].[ProcurementPrequalificationExercises] e ON e.[Id] = i.[ExerciseId]
        JOIN [dbo].[ProcurementPrequalificationApplications] a ON a.[Id] = i.[ApplicationId]
        WHERE d.[Id] IS NULL AND
              (i.[Status] <> 0 OR e.[Status] NOT IN (4,5) OR a.[Status] NOT IN (1,3))
    ) THROW 51157, 'Qualified-list entries require a workflow-approved qualified application.', 1;

    IF EXISTS (
        SELECT 1 FROM inserted i JOIN deleted d ON d.[Id] = i.[Id]
        WHERE i.[TenantId] <> d.[TenantId] OR i.[ExerciseId] <> d.[ExerciseId]
           OR i.[ApplicationId] <> d.[ApplicationId] OR i.[BusinessPartnerId] <> d.[BusinessPartnerId]
           OR i.[CategoryId] <> d.[CategoryId] OR i.[ValidFromUtc] <> d.[ValidFromUtc]
           OR i.[ExpiresAtUtc] <> d.[ExpiresAtUtc]
           OR i.[ApprovalReference] <> d.[ApprovalReference]
           OR i.[ApprovalEvidenceReference] <> d.[ApprovalEvidenceReference]
           OR i.[CreatedAt] <> d.[CreatedAt] OR i.[IsDeleted] <> d.[IsDeleted]
    ) THROW 51158, 'Qualified-list approval, scope, validity, tenant, and lineage are immutable.', 1;

    IF EXISTS (
        SELECT 1 FROM inserted i JOIN deleted d ON d.[Id] = i.[Id]
        WHERE d.[Status] IN (1,2)
           OR NOT (i.[Status] = d.[Status] OR (d.[Status] = 0 AND i.[Status] IN (1,2)))
    ) THROW 51159, 'Qualified-list transition is invalid or the terminal entry is immutable.', 1;
END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProcurementPrequalificationScores");

            migrationBuilder.DropTable(
                name: "ProcurementQualifiedListEntries");

            migrationBuilder.DropTable(
                name: "ProcurementPrequalificationCriteria");

            migrationBuilder.DropTable(
                name: "ProcurementPrequalificationApplications");

            migrationBuilder.DropTable(
                name: "ProcurementPrequalificationExercises");
        }
    }
}
