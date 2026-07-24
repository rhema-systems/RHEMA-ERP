using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHRPerformanceModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppraisalAttachments_PerformanceAppraisals_AppraisalId",
                table: "AppraisalAttachments");

            migrationBuilder.DropForeignKey(
                name: "FK_AppraisalEmployeeResponses_AppraisalCriterias_CriteriaId",
                table: "AppraisalEmployeeResponses");

            migrationBuilder.DropForeignKey(
                name: "FK_CriterionScores_AppraisalCriterias_CriteriaId",
                table: "CriterionScores");

            migrationBuilder.DropForeignKey(
                name: "FK_CriterionScores_KpiEvaluationRecords_KpiEvaluationRecordId",
                table: "CriterionScores");

            migrationBuilder.DropForeignKey(
                name: "FK_TeamMemberHistories_Employees_EmployeeId",
                table: "TeamMemberHistories");

            migrationBuilder.DropForeignKey(
                name: "FK_TeamMemberHistories_Teams_TeamId",
                table: "TeamMemberHistories");

            migrationBuilder.DropForeignKey(
                name: "FK_TeamMembers_Employees_EmployeeId",
                table: "TeamMembers");

            migrationBuilder.DropForeignKey(
                name: "FK_TeamMembers_Teams_TeamId",
                table: "TeamMembers");

            migrationBuilder.DropForeignKey(
                name: "FK_Teams_Employees_TeamLeadId",
                table: "Teams");

            migrationBuilder.DropForeignKey(
                name: "FK_Teams_Locations_LocationId",
                table: "Teams");

            migrationBuilder.DropForeignKey(
                name: "FK_Teams_ShiftDefinitions_ShiftId",
                table: "Teams");

            migrationBuilder.DropForeignKey(
                name: "FK_Teams_Teams_ParentTeamId",
                table: "Teams");

            // [HR-MODULE-PORT] Fail-safe data-loss guards. The performance module was REDESIGNED
            // (not renamed): the tables/columns dropped below have no faithful auto-migration path
            // into the new snapshot/config/goal schema (no FK bridge to resolve the new parent keys).
            // On a fresh/dev database these legacy tables are empty and this block is a no-op. On a
            // populated pre-port database it HALTS the upgrade (rolls back the transaction) with
            // guidance instead of silently destroying evaluation history. See
            // docs/hr-port-data-migration.md.
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'KpiEvaluationRecords', N'U') IS NOT NULL AND EXISTS (SELECT 1 FROM KpiEvaluationRecords)
    THROW 50000, 'HR-port upgrade halted: KpiEvaluationRecords holds data the performance redesign cannot auto-migrate. Migrate it into AppraisalKpiEvaluationSnapshots per docs/hr-port-data-migration.md, then re-run.', 1;
IF OBJECT_ID(N'EmployeeKpiTargets', N'U') IS NOT NULL AND EXISTS (SELECT 1 FROM EmployeeKpiTargets)
    THROW 50000, 'HR-port upgrade halted: EmployeeKpiTargets holds data the performance redesign cannot auto-migrate. Migrate it into EmployeeGoals per docs/hr-port-data-migration.md, then re-run.', 1;
IF OBJECT_ID(N'AppraisalCriterias', N'U') IS NOT NULL AND EXISTS (SELECT 1 FROM AppraisalCriterias)
    THROW 50000, 'HR-port upgrade halted: AppraisalCriterias holds data the performance redesign cannot auto-migrate. Migrate it into PerformanceAppraisalCriterionConfigs/AppraisalCompetencies per docs/hr-port-data-migration.md, then re-run.', 1;
IF OBJECT_ID(N'PositionCriteriaMappings', N'U') IS NOT NULL AND EXISTS (SELECT 1 FROM PositionCriteriaMappings)
    THROW 50000, 'HR-port upgrade halted: PositionCriteriaMappings holds data the performance redesign cannot auto-migrate. Migrate it into PerformanceAppraisalCriterionConfigs per docs/hr-port-data-migration.md, then re-run.', 1;
IF OBJECT_ID(N'MappingGradeRanges', N'U') IS NOT NULL AND EXISTS (SELECT 1 FROM MappingGradeRanges)
    THROW 50000, 'HR-port upgrade halted: MappingGradeRanges holds data the performance redesign cannot auto-migrate. Migrate it into the new criterion/template GradeRanges tables per docs/hr-port-data-migration.md, then re-run.', 1;
IF OBJECT_ID(N'PerformanceAppraisals', N'U') IS NOT NULL AND EXISTS (SELECT 1 FROM PerformanceAppraisals)
    THROW 50000, 'HR-port upgrade halted: PerformanceAppraisals holds appraisals the redesign cannot auto-migrate (AppealOutcome is dropped, and required AppraisalCycleId/AppraisalTemplateId are added with no legacy source). Migrate per docs/hr-port-data-migration.md, then re-run.', 1;
IF COL_LENGTH('EvaluatorEvaluations', 'EvaluationDate') IS NOT NULL AND EXISTS (SELECT 1 FROM EvaluatorEvaluations WHERE EvaluationDate IS NOT NULL)
    THROW 50000, 'HR-port upgrade halted: EvaluatorEvaluations.EvaluationDate holds data with no target column in the redesign. Preserve it per docs/hr-port-data-migration.md, then re-run.', 1;
");

            migrationBuilder.DropTable(
                name: "KpiEvaluationRecords");

            migrationBuilder.DropTable(
                name: "MappingGradeRanges");

            migrationBuilder.DropTable(
                name: "EmployeeKpiTargets");

            migrationBuilder.DropTable(
                name: "PositionCriteriaMappings");

            migrationBuilder.DropTable(
                name: "AppraisalCriterias");

            migrationBuilder.DropIndex(
                name: "IX_Teams_TenantId",
                table: "Teams");

            migrationBuilder.DropIndex(
                name: "IX_PerformanceAppraisals_AppraisalType",
                table: "PerformanceAppraisals");

            migrationBuilder.DropIndex(
                name: "IX_AppraisalAttachments_FileName",
                table: "AppraisalAttachments");

            migrationBuilder.DropColumn(
                name: "AppealOutcome",
                table: "PerformanceAppraisals");

            migrationBuilder.DropColumn(
                name: "EvaluationDate",
                table: "EvaluatorEvaluations");

            migrationBuilder.RenameIndex(
                name: "IX_Teams_TeamLeadId",
                table: "Teams",
                newName: "IX_Team_TeamLeadId");

            migrationBuilder.RenameIndex(
                name: "IX_Teams_ShiftId",
                table: "Teams",
                newName: "IX_Team_ShiftId");

            migrationBuilder.RenameIndex(
                name: "IX_Teams_ParentTeamId",
                table: "Teams",
                newName: "IX_Team_ParentTeamId");

            migrationBuilder.RenameIndex(
                name: "IX_Teams_OrganizationUnitId",
                table: "Teams",
                newName: "IX_Team_OrganizationUnitId");

            migrationBuilder.RenameIndex(
                name: "IX_Teams_LocationId",
                table: "Teams",
                newName: "IX_Team_LocationId");

            migrationBuilder.RenameIndex(
                name: "IX_TeamMembers_TeamId",
                table: "TeamMembers",
                newName: "IX_TeamMember_TeamId");

            migrationBuilder.RenameIndex(
                name: "IX_TeamMembers_EmployeeId",
                table: "TeamMembers",
                newName: "IX_TeamMember_EmployeeId");

            migrationBuilder.RenameIndex(
                name: "IX_TeamMemberHistories_TeamId",
                table: "TeamMemberHistories",
                newName: "IX_TeamMemberHistory_TeamId");

            migrationBuilder.RenameIndex(
                name: "IX_TeamMemberHistories_EmployeeId",
                table: "TeamMemberHistories",
                newName: "IX_TeamMemberHistory_EmployeeId");

            migrationBuilder.RenameColumn(
                name: "AppraisalType",
                table: "PerformanceAppraisals",
                newName: "PeerEvaluatorsCount");

            migrationBuilder.RenameColumn(
                name: "AppealReason",
                table: "PerformanceAppraisals",
                newName: "EmployeeAcknowledgmentComments");

            migrationBuilder.RenameColumn(
                name: "AppealFiled",
                table: "PerformanceAppraisals",
                newName: "RecommendPIP");

            migrationBuilder.RenameColumn(
                name: "AppealDate",
                table: "PerformanceAppraisals",
                newName: "EmployeeAcknowledgedDate");

            migrationBuilder.RenameColumn(
                name: "KpiEvaluationRecordId",
                table: "CriterionScores",
                newName: "GradeDefinitionId");

            migrationBuilder.RenameColumn(
                name: "CriteriaId",
                table: "CriterionScores",
                newName: "TemplateItemId");

            migrationBuilder.RenameIndex(
                name: "IX_CriterionScores_KpiEvaluationRecordId",
                table: "CriterionScores",
                newName: "IX_CriterionScores_GradeDefinitionId");

            migrationBuilder.RenameIndex(
                name: "IX_CriterionScores_CriteriaId",
                table: "CriterionScores",
                newName: "IX_CriterionScores_TemplateItemId");

            migrationBuilder.RenameColumn(
                name: "CriteriaId",
                table: "AppraisalEmployeeResponses",
                newName: "TemplateItemId");

            migrationBuilder.RenameIndex(
                name: "IX_AppraisalEmployeeResponses_CriteriaId",
                table: "AppraisalEmployeeResponses",
                newName: "IX_AppraisalEmployeeResponses_TemplateItemId");

            migrationBuilder.RenameColumn(
                name: "AppraisalId",
                table: "AppraisalAttachments",
                newName: "UploadedById");

            migrationBuilder.RenameIndex(
                name: "IX_AppraisalAttachments_AppraisalId",
                table: "AppraisalAttachments",
                newName: "IX_AppraisalAttachments_UploadedById");

            migrationBuilder.AddColumn<Guid>(
                name: "ShiftDefinitionId",
                table: "Teams",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "SupportProvided",
                table: "PerformanceImprovementPlans",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000);

            migrationBuilder.AlterColumn<string>(
                name: "MeasurementCriteria",
                table: "PerformanceImprovementPlans",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000);

            migrationBuilder.AddColumn<Guid>(
                name: "HROwnerId",
                table: "PerformanceImprovementPlans",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AdjustedScore",
                table: "PerformanceAppraisals",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AppealRemandDeadline",
                table: "PerformanceAppraisals",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AppealRemandedDate",
                table: "PerformanceAppraisals",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AppraisalCycleId",
                table: "PerformanceAppraisals",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "AppraisalTemplateId",
                table: "PerformanceAppraisals",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CalibrationSessionId",
                table: "PerformanceAppraisals",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CurrentAppealStatus",
                table: "PerformanceAppraisals",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DevelopmentPlanId",
                table: "PerformanceAppraisals",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EmployeeAcknowledged",
                table: "PerformanceAppraisals",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasAppeal",
                table: "PerformanceAppraisals",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsCalibrated",
                table: "PerformanceAppraisals",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "OverallGradeDefinitionId",
                table: "PerformanceAppraisals",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PreCalibrationScore",
                table: "PerformanceAppraisals",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RankInUnit",
                table: "PerformanceAppraisals",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RecommendAward",
                table: "PerformanceAppraisals",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "KpiDefinitions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // [HR-MODULE-PORT] Backfill: KPI definitions were implicitly active before this flag
            // existed. Keep every pre-existing row active (the false default would silently disable
            // live configuration). No-op on an empty/fresh table.
            migrationBuilder.Sql("UPDATE KpiDefinitions SET IsActive = 1;");

            migrationBuilder.AddColumn<DateTime>(
                name: "StartedDate",
                table: "EvaluatorEvaluations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SubmittedDate",
                table: "EvaluatorEvaluations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalScore",
                table: "EvaluatorEvaluations",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ActualValue",
                table: "CriterionScores",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EvidenceLinks",
                table: "CriterionScores",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "AppraisalGradeDefinitions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // [HR-MODULE-PORT] Backfill: grade definitions were implicitly active before this flag
            // existed. Keep every pre-existing row active. No-op on an empty/fresh table.
            migrationBuilder.Sql("UPDATE AppraisalGradeDefinitions SET IsActive = 1;");

            migrationBuilder.AddColumn<int>(
                name: "MappedRating",
                table: "AppraisalGradeDefinitions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OverallMaxScore",
                table: "AppraisalGradeDefinitions",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OverallMinScore",
                table: "AppraisalGradeDefinitions",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ResponseText",
                table: "AppraisalEmployeeResponses",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ResponseStatus",
                table: "AppraisalEmployeeResponses",
                type: "int",
                maxLength: 20,
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "SubmittedDate",
                table: "AppraisalEmployeeResponses",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AppealId",
                table: "AppraisalAttachments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CalibrationSessionId",
                table: "AppraisalAttachments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CheckInId",
                table: "AppraisalAttachments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EmployeeGoalId",
                table: "AppraisalAttachments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EntityType",
                table: "AppraisalAttachments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "FileSizeBytes",
                table: "AppraisalAttachments",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PerformanceAppraisalId",
                table: "AppraisalAttachments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PipId",
                table: "AppraisalAttachments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReviewEventId",
                table: "AppraisalAttachments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UnitGoalId",
                table: "AppraisalAttachments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AppraisalAppeals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PerformanceAppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmittedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AppealReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ReviewedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ResolutionNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ResolvedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_AppraisalAppeals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppraisalAppeals_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppraisalAppeals_Employees_ReviewedById",
                        column: x => x.ReviewedById,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppraisalAppeals_PerformanceAppraisals_PerformanceAppraisalId",
                        column: x => x.PerformanceAppraisalId,
                        principalTable: "PerformanceAppraisals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppraisalAppeals_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppraisalCompetencies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CriteriaName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RequireEvidence = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_AppraisalCompetencies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppraisalCompetencies_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppraisalEvaluationSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EvaluatorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EvaluatorRole = table.Column<int>(type: "int", nullable: false),
                    TotalScore = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    SnapshotDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SnapshotReason = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
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
                    table.PrimaryKey("PK_AppraisalEvaluationSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppraisalEvaluationSnapshots_Employees_EvaluatorId",
                        column: x => x.EvaluatorId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppraisalEvaluationSnapshots_PerformanceAppraisals_AppraisalId",
                        column: x => x.AppraisalId,
                        principalTable: "PerformanceAppraisals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppraisalEvaluationSnapshots_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppraisalHRReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewedByHRId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewStartedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewCompletedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsApproved = table.Column<bool>(type: "bit", nullable: false),
                    HRNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    AdjustedOverallScore = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    AdjustmentReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_AppraisalHRReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppraisalHRReviews_Employees_ReviewedByHRId",
                        column: x => x.ReviewedByHRId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppraisalHRReviews_PerformanceAppraisals_AppraisalId",
                        column: x => x.AppraisalId,
                        principalTable: "PerformanceAppraisals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppraisalHRReviews_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppraisalManualAdvanceLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PerformanceAppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AdvancedByEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AdvancedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FromSubStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ToSubStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FromMajorStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ToMajorStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ActionsPerformedJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
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
                    table.PrimaryKey("PK_AppraisalManualAdvanceLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppraisalManualAdvanceLogs_Employees_AdvancedByEmployeeId",
                        column: x => x.AdvancedByEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AppraisalManualAdvanceLogs_PerformanceAppraisals_PerformanceAppraisalId",
                        column: x => x.PerformanceAppraisalId,
                        principalTable: "PerformanceAppraisals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AppraisalManualAdvanceLogs_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppraisalNotifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecipientEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    SubjectEmployeeName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CycleName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    NavigationUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsRead = table.Column<bool>(type: "bit", nullable: false),
                    ReadDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Urgency = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_AppraisalNotifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppraisalNotifications_Employees_RecipientEmployeeId",
                        column: x => x.RecipientEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AppraisalNotifications_PerformanceAppraisals_AppraisalId",
                        column: x => x.AppraisalId,
                        principalTable: "PerformanceAppraisals",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AppraisalNotifications_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppraisalOutcomeRecommendations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PerformanceAppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecommendationType = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RecommendedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RecommendedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ActionedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ResolutionNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    TargetEntityType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TargetEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_AppraisalOutcomeRecommendations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppraisalOutcomeRecommendations_PerformanceAppraisals_PerformanceAppraisalId",
                        column: x => x.PerformanceAppraisalId,
                        principalTable: "PerformanceAppraisals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AppraisalOutcomeRecommendations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppraisalSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SettingsName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RequireSelfEvaluation = table.Column<bool>(type: "bit", nullable: false),
                    AllowSelfSoftSkillRating = table.Column<bool>(type: "bit", nullable: false),
                    SelfEvaluationWeight = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    RequirePeerReviews = table.Column<bool>(type: "bit", nullable: false),
                    PeerNominationMode = table.Column<int>(type: "int", nullable: false),
                    MinPeerEvaluators = table.Column<int>(type: "int", nullable: false),
                    MaxPeerEvaluators = table.Column<int>(type: "int", nullable: false),
                    PeerReviewsAnonymous = table.Column<bool>(type: "bit", nullable: false),
                    AllowPeerKpiEvaluation = table.Column<bool>(type: "bit", nullable: false),
                    PeerEvaluationWeight = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    PeerEvaluationOpenMode = table.Column<int>(type: "int", nullable: false),
                    RequireManagerEvaluation = table.Column<bool>(type: "bit", nullable: false),
                    ManagerEvaluationWeight = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    IsManagerAuthoritative = table.Column<bool>(type: "bit", nullable: false),
                    ShowSelfScoreToManager = table.Column<bool>(type: "bit", nullable: false),
                    ShowPeerScoresToManager = table.Column<bool>(type: "bit", nullable: false),
                    ShowScoreBreakdownToEmployee = table.Column<bool>(type: "bit", nullable: false),
                    RequireCalibration = table.Column<bool>(type: "bit", nullable: false),
                    RequireHRReview = table.Column<bool>(type: "bit", nullable: false),
                    HRCanModifyScores = table.Column<bool>(type: "bit", nullable: false),
                    DefaultHRReviewerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    HRReviewTiming = table.Column<int>(type: "int", nullable: false),
                    RequireEmployeeAcknowledgment = table.Column<bool>(type: "bit", nullable: false),
                    AllowEmployeeResponse = table.Column<bool>(type: "bit", nullable: false),
                    AllowAcknowledgmentWithoutConversation = table.Column<bool>(type: "bit", nullable: false),
                    EnableAppeals = table.Column<bool>(type: "bit", nullable: false),
                    AppealWindowDays = table.Column<int>(type: "int", nullable: false),
                    AppealReevaluationWindowDays = table.Column<int>(type: "int", nullable: false),
                    RequireGoalSetting = table.Column<bool>(type: "bit", nullable: false),
                    RequireManagerGoalApproval = table.Column<bool>(type: "bit", nullable: false),
                    MaxGoalsPerEmployee = table.Column<int>(type: "int", nullable: true),
                    MinGoalsPerEmployee = table.Column<int>(type: "int", nullable: true),
                    EnableCheckIns = table.Column<bool>(type: "bit", nullable: false),
                    EnablePrivateJournal = table.Column<bool>(type: "bit", nullable: false),
                    RequireKickOffConversation = table.Column<bool>(type: "bit", nullable: false),
                    RequireMidYearConversation = table.Column<bool>(type: "bit", nullable: false),
                    RequireFinalConversation = table.Column<bool>(type: "bit", nullable: false),
                    ReviewFrequency = table.Column<int>(type: "int", nullable: false),
                    InterimReviewDepth = table.Column<int>(type: "int", nullable: false),
                    RequireMidYearSelfAssessment = table.Column<bool>(type: "bit", nullable: false),
                    RequireGoalProgressUpdateAtReview = table.Column<bool>(type: "bit", nullable: false),
                    RequireDevelopmentPlanUpdate = table.Column<bool>(type: "bit", nullable: false),
                    AutoLockOnDeadline = table.Column<bool>(type: "bit", nullable: false),
                    ProbationExtensionMonths = table.Column<int>(type: "int", nullable: false),
                    ManagerWorkloadThreshold = table.Column<int>(type: "int", nullable: false),
                    DeadlineRiskHighDays = table.Column<int>(type: "int", nullable: false),
                    DeadlineRiskMediumDays = table.Column<int>(type: "int", nullable: false),
                    DeadlineRiskLowDays = table.Column<int>(type: "int", nullable: false),
                    SuccessionPoolName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SuccessionDefaultReadiness = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_AppraisalSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppraisalSettings_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppraisalTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    OrganizationLevelId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OrganizationUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PositionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    ApprovalStatus = table.Column<int>(type: "int", nullable: false),
                    SubmittedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubmittedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovalDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_AppraisalTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppraisalTemplates_EmployeePositions_PositionId",
                        column: x => x.PositionId,
                        principalTable: "EmployeePositions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppraisalTemplates_OrganizationLevels_OrganizationLevelId",
                        column: x => x.OrganizationLevelId,
                        principalTable: "OrganizationLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppraisalTemplates_OrganizationUnits_OrganizationUnitId",
                        column: x => x.OrganizationUnitId,
                        principalTable: "OrganizationUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppraisalTemplates_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmploymentActionProposals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceAppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActionType = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_EmploymentActionProposals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmploymentActionProposals_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmploymentActionProposals_PerformanceAppraisals_SourceAppraisalId",
                        column: x => x.SourceAppraisalId,
                        principalTable: "PerformanceAppraisals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmploymentActionProposals_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GoalLibraries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SuccessCriteria = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    OrganizationLevelId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OrganizationUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PositionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_GoalLibraries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GoalLibraries_EmployeePositions_PositionId",
                        column: x => x.PositionId,
                        principalTable: "EmployeePositions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GoalLibraries_OrganizationLevels_OrganizationLevelId",
                        column: x => x.OrganizationLevelId,
                        principalTable: "OrganizationLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GoalLibraries_OrganizationUnits_OrganizationUnitId",
                        column: x => x.OrganizationUnitId,
                        principalTable: "OrganizationUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GoalLibraries_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GoalRiskSetting",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DaysRemainingThreshold = table.Column<int>(type: "int", nullable: false),
                    MinimumProgressPercent = table.Column<int>(type: "int", nullable: false),
                    ExpectedProgressTolerancePercent = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_GoalRiskSetting", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GoalRiskSetting_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PeerNominations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PeerEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NominatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NominationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    InvitationSentDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InstructionsToPeer = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    NominationStatus = table.Column<int>(type: "int", nullable: false),
                    ApprovedByManagerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_PeerNominations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PeerNominations_Employees_ApprovedByManagerId",
                        column: x => x.ApprovedByManagerId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PeerNominations_Employees_NominatedById",
                        column: x => x.NominatedById,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PeerNominations_Employees_PeerEmployeeId",
                        column: x => x.PeerEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PeerNominations_PerformanceAppraisals_AppraisalId",
                        column: x => x.AppraisalId,
                        principalTable: "PerformanceAppraisals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PeerNominations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PipGoals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PipId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SuccessCriteria = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ProgressPercent = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    ProgressNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_PipGoals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PipGoals_PerformanceImprovementPlans_PipId",
                        column: x => x.PipId,
                        principalTable: "PerformanceImprovementPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PipGoals_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SalaryReviewProposals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceAppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProposalType = table.Column<int>(type: "int", nullable: false),
                    ProposedPercent = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    ProposedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_SalaryReviewProposals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalaryReviewProposals_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalaryReviewProposals_PerformanceAppraisals_SourceAppraisalId",
                        column: x => x.SourceAppraisalId,
                        principalTable: "PerformanceAppraisals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalaryReviewProposals_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StrategicGoals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SuccessCriteria = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    StartYear = table.Column<int>(type: "int", nullable: false),
                    EndYear = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_StrategicGoals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StrategicGoals_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppraisalCycles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CycleCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CycleName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    AppraisalType = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    AppraisalSettingsId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    GoalSettingOpenDate = table.Column<DateOnly>(type: "date", nullable: true),
                    GoalSettingDeadline = table.Column<DateOnly>(type: "date", nullable: true),
                    Q1ReviewOpenDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Q1ReviewDeadline = table.Column<DateOnly>(type: "date", nullable: true),
                    MidYearOpenDate = table.Column<DateOnly>(type: "date", nullable: true),
                    MidYearDeadline = table.Column<DateOnly>(type: "date", nullable: true),
                    Q3ReviewOpenDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Q3ReviewDeadline = table.Column<DateOnly>(type: "date", nullable: true),
                    PeerNominationDeadline = table.Column<DateOnly>(type: "date", nullable: true),
                    SelfEvaluationOpenDate = table.Column<DateOnly>(type: "date", nullable: true),
                    SelfEvaluationDeadline = table.Column<DateOnly>(type: "date", nullable: true),
                    PeerEvaluationOpenDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PeerEvaluationDeadline = table.Column<DateOnly>(type: "date", nullable: true),
                    ManagerEvaluationOpenDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ManagerEvaluationDeadline = table.Column<DateOnly>(type: "date", nullable: true),
                    CalibrationOpenDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CalibrationDeadline = table.Column<DateOnly>(type: "date", nullable: true),
                    HRReviewOpenDate = table.Column<DateOnly>(type: "date", nullable: true),
                    HRReviewDeadline = table.Column<DateOnly>(type: "date", nullable: true),
                    EmployeeAcknowledgeDeadline = table.Column<DateOnly>(type: "date", nullable: true),
                    FinalConversationDeadline = table.Column<DateOnly>(type: "date", nullable: true),
                    OpenedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OpenedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClosedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_AppraisalCycles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppraisalCycles_AppraisalSettings_AppraisalSettingsId",
                        column: x => x.AppraisalSettingsId,
                        principalTable: "AppraisalSettings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppraisalCycles_Employees_ClosedById",
                        column: x => x.ClosedById,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppraisalCycles_Employees_OpenedById",
                        column: x => x.OpenedById,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppraisalCycles_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppraisalTemplateSections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppraisalTemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SectionName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    Weight = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_AppraisalTemplateSections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppraisalTemplateSections_AppraisalTemplates_AppraisalTemplateId",
                        column: x => x.AppraisalTemplateId,
                        principalTable: "AppraisalTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppraisalTemplateSections_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppraisalCycleTargets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppraisalCycleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetType = table.Column<int>(type: "int", nullable: false),
                    OrganizationLevelId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OrganizationUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PositionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EstimatedEmployeeCount = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_AppraisalCycleTargets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppraisalCycleTargets_AppraisalCycles_AppraisalCycleId",
                        column: x => x.AppraisalCycleId,
                        principalTable: "AppraisalCycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppraisalCycleTargets_EmployeePositions_PositionId",
                        column: x => x.PositionId,
                        principalTable: "EmployeePositions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppraisalCycleTargets_OrganizationLevels_OrganizationLevelId",
                        column: x => x.OrganizationLevelId,
                        principalTable: "OrganizationLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppraisalCycleTargets_OrganizationUnits_OrganizationUnitId",
                        column: x => x.OrganizationUnitId,
                        principalTable: "OrganizationUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppraisalCycleTargets_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppraisalCycleTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppraisalCycleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppraisalTemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_AppraisalCycleTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppraisalCycleTemplates_AppraisalCycles_AppraisalCycleId",
                        column: x => x.AppraisalCycleId,
                        principalTable: "AppraisalCycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppraisalCycleTemplates_AppraisalTemplates_AppraisalTemplateId",
                        column: x => x.AppraisalTemplateId,
                        principalTable: "AppraisalTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppraisalCycleTemplates_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CalibrationSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppraisalCycleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OrganizationLevelId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OrganizationUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ScheduledDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StartedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FacilitatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CompletedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Agenda = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    MeetingNotes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
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
                    table.PrimaryKey("PK_CalibrationSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CalibrationSessions_AppraisalCycles_AppraisalCycleId",
                        column: x => x.AppraisalCycleId,
                        principalTable: "AppraisalCycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CalibrationSessions_Employees_CompletedById",
                        column: x => x.CompletedById,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CalibrationSessions_Employees_FacilitatedById",
                        column: x => x.FacilitatedById,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CalibrationSessions_OrganizationLevels_OrganizationLevelId",
                        column: x => x.OrganizationLevelId,
                        principalTable: "OrganizationLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CalibrationSessions_OrganizationUnits_OrganizationUnitId",
                        column: x => x.OrganizationUnitId,
                        principalTable: "OrganizationUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CalibrationSessions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CheckIns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppraisalCycleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConductedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CheckInType = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ScheduledDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConductedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Agenda = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SharedNotes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    PrivateNotes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ActionItems = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    FollowUpDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EmployeeComments = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_CheckIns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CheckIns_AppraisalCycles_AppraisalCycleId",
                        column: x => x.AppraisalCycleId,
                        principalTable: "AppraisalCycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CheckIns_Employees_ConductedById",
                        column: x => x.ConductedById,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CheckIns_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CheckIns_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CompanyGoals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppraisalCycleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StrategicGoalId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SuccessCriteria = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    TargetValue = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    Unit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    IsVisible = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_CompanyGoals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompanyGoals_AppraisalCycles_AppraisalCycleId",
                        column: x => x.AppraisalCycleId,
                        principalTable: "AppraisalCycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CompanyGoals_StrategicGoals_StrategicGoalId",
                        column: x => x.StrategicGoalId,
                        principalTable: "StrategicGoals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CompanyGoals_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeDevelopmentPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppraisalCycleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PlanStatus = table.Column<int>(type: "int", nullable: false),
                    OverallNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_EmployeeDevelopmentPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeDevelopmentPlans_AppraisalCycles_AppraisalCycleId",
                        column: x => x.AppraisalCycleId,
                        principalTable: "AppraisalCycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeDevelopmentPlans_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeDevelopmentPlans_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppraisalTemplateItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppraisalTemplateSectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompetencyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    KpiDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    KpiTargetValue = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    KpiMinValue = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    KpiMaxValue = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    CustomQuestion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    Weight = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_AppraisalTemplateItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppraisalTemplateItems_AppraisalCompetencies_CompetencyId",
                        column: x => x.CompetencyId,
                        principalTable: "AppraisalCompetencies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppraisalTemplateItems_AppraisalTemplateSections_AppraisalTemplateSectionId",
                        column: x => x.AppraisalTemplateSectionId,
                        principalTable: "AppraisalTemplateSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppraisalTemplateItems_KpiDefinitions_KpiDefinitionId",
                        column: x => x.KpiDefinitionId,
                        principalTable: "KpiDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppraisalTemplateItems_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppraisalCycleTargetExclusions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppraisalCycleTargetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationLevelId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OrganizationUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PositionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
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
                    table.PrimaryKey("PK_AppraisalCycleTargetExclusions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppraisalCycleTargetExclusions_AppraisalCycleTargets_AppraisalCycleTargetId",
                        column: x => x.AppraisalCycleTargetId,
                        principalTable: "AppraisalCycleTargets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppraisalCycleTargetExclusions_EmployeePositions_PositionId",
                        column: x => x.PositionId,
                        principalTable: "EmployeePositions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppraisalCycleTargetExclusions_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppraisalCycleTargetExclusions_OrganizationLevels_OrganizationLevelId",
                        column: x => x.OrganizationLevelId,
                        principalTable: "OrganizationLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppraisalCycleTargetExclusions_OrganizationUnits_OrganizationUnitId",
                        column: x => x.OrganizationUnitId,
                        principalTable: "OrganizationUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppraisalCycleTargetExclusions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CalibrationParticipants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CalibrationSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Attended = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_CalibrationParticipants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CalibrationParticipants_CalibrationSessions_CalibrationSessionId",
                        column: x => x.CalibrationSessionId,
                        principalTable: "CalibrationSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CalibrationParticipants_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CalibrationParticipants_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CheckInObjectiveLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CheckInId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyGoalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_CheckInObjectiveLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CheckInObjectiveLinks_CheckIns_CheckInId",
                        column: x => x.CheckInId,
                        principalTable: "CheckIns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CheckInObjectiveLinks_CompanyGoals_CompanyGoalId",
                        column: x => x.CompanyGoalId,
                        principalTable: "CompanyGoals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CheckInObjectiveLinks_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UnitGoals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppraisalCycleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParentCompanyGoalId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ParentUnitGoalId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OrganizationLevelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedByManagerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SuccessCriteria = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    TargetValue = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    Unit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
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
                    table.PrimaryKey("PK_UnitGoals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UnitGoals_AppraisalCycles_AppraisalCycleId",
                        column: x => x.AppraisalCycleId,
                        principalTable: "AppraisalCycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UnitGoals_CompanyGoals_ParentCompanyGoalId",
                        column: x => x.ParentCompanyGoalId,
                        principalTable: "CompanyGoals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UnitGoals_Employees_CreatedByManagerId",
                        column: x => x.CreatedByManagerId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UnitGoals_OrganizationLevels_OrganizationLevelId",
                        column: x => x.OrganizationLevelId,
                        principalTable: "OrganizationLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UnitGoals_OrganizationUnits_OrganizationUnitId",
                        column: x => x.OrganizationUnitId,
                        principalTable: "OrganizationUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UnitGoals_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UnitGoals_UnitGoals_ParentUnitGoalId",
                        column: x => x.ParentUnitGoalId,
                        principalTable: "UnitGoals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeDevelopmentPlanFeedbacks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DevelopmentPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ManagerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FeedbackType = table.Column<int>(type: "int", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: false),
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
                    table.PrimaryKey("PK_EmployeeDevelopmentPlanFeedbacks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeDevelopmentPlanFeedbacks_EmployeeDevelopmentPlans_DevelopmentPlanId",
                        column: x => x.DevelopmentPlanId,
                        principalTable: "EmployeeDevelopmentPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmployeeDevelopmentPlanFeedbacks_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppraisalAppealItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppraisalAppealId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ResolutionNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ScoreAdjusted = table.Column<bool>(type: "bit", nullable: true),
                    OriginalScore = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    RevisedScore = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
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
                    table.PrimaryKey("PK_AppraisalAppealItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppraisalAppealItems_AppraisalAppeals_AppraisalAppealId",
                        column: x => x.AppraisalAppealId,
                        principalTable: "AppraisalAppeals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppraisalAppealItems_AppraisalTemplateItems_TemplateItemId",
                        column: x => x.TemplateItemId,
                        principalTable: "AppraisalTemplateItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppraisalAppealItems_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppraisalCriterionScoreSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppraisalEvaluationSnapshotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NumericScore = table.Column<int>(type: "int", nullable: true),
                    WeightedScore = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    KpiTargetValue = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    KpiMinValue = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    KpiMaxValue = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    KpiTargetSource = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_AppraisalCriterionScoreSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppraisalCriterionScoreSnapshots_AppraisalEvaluationSnapshots_AppraisalEvaluationSnapshotId",
                        column: x => x.AppraisalEvaluationSnapshotId,
                        principalTable: "AppraisalEvaluationSnapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppraisalCriterionScoreSnapshots_AppraisalTemplateItems_TemplateItemId",
                        column: x => x.TemplateItemId,
                        principalTable: "AppraisalTemplateItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppraisalCriterionScoreSnapshots_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppraisalCustomQuestionResponses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PerformanceAppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResponseText = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    IsDraft = table.Column<bool>(type: "bit", nullable: false),
                    SubmittedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_AppraisalCustomQuestionResponses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppraisalCustomQuestionResponses_AppraisalTemplateItems_TemplateItemId",
                        column: x => x.TemplateItemId,
                        principalTable: "AppraisalTemplateItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppraisalCustomQuestionResponses_PerformanceAppraisals_PerformanceAppraisalId",
                        column: x => x.PerformanceAppraisalId,
                        principalTable: "PerformanceAppraisals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AppraisalCustomQuestionResponses_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CalibrationRatingAdjustments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CalibrationSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PerformanceAppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OriginalScore = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    AdjustedScore = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    AdjustedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AdjustmentDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Rationale = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_CalibrationRatingAdjustments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CalibrationRatingAdjustments_AppraisalTemplateItems_TemplateItemId",
                        column: x => x.TemplateItemId,
                        principalTable: "AppraisalTemplateItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CalibrationRatingAdjustments_CalibrationSessions_CalibrationSessionId",
                        column: x => x.CalibrationSessionId,
                        principalTable: "CalibrationSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CalibrationRatingAdjustments_Employees_AdjustedById",
                        column: x => x.AdjustedById,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CalibrationRatingAdjustments_PerformanceAppraisals_PerformanceAppraisalId",
                        column: x => x.PerformanceAppraisalId,
                        principalTable: "PerformanceAppraisals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CalibrationRatingAdjustments_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PerformanceAppraisalCriterionConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PerformanceAppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WeightUsed = table.Column<int>(type: "int", nullable: false),
                    KpiTargetValue = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    KpiMinValue = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    KpiMaxValue = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    KpiTargetSource = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_PerformanceAppraisalCriterionConfigs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PerformanceAppraisalCriterionConfigs_AppraisalTemplateItems_TemplateItemId",
                        column: x => x.TemplateItemId,
                        principalTable: "AppraisalTemplateItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PerformanceAppraisalCriterionConfigs_PerformanceAppraisals_PerformanceAppraisalId",
                        column: x => x.PerformanceAppraisalId,
                        principalTable: "PerformanceAppraisals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PerformanceAppraisalCriterionConfigs_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TemplateItemGradeRanges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppraisalTemplateItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GradeDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LowScore = table.Column<int>(type: "int", nullable: false),
                    HighScore = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_TemplateItemGradeRanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TemplateItemGradeRanges_AppraisalGradeDefinitions_GradeDefinitionId",
                        column: x => x.GradeDefinitionId,
                        principalTable: "AppraisalGradeDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TemplateItemGradeRanges_AppraisalTemplateItems_AppraisalTemplateItemId",
                        column: x => x.AppraisalTemplateItemId,
                        principalTable: "AppraisalTemplateItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TemplateItemGradeRanges_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeGoals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppraisalCycleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PerformanceAppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CompanyGoalId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UnitGoalId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ParentGoalId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ParentType = table.Column<int>(type: "int", nullable: true),
                    GoalLibraryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    KpiDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SuccessCriteria = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Weight = table.Column<int>(type: "int", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    MeasurementType = table.Column<int>(type: "int", nullable: false),
                    Period = table.Column<int>(type: "int", nullable: false),
                    TargetValue = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    MinValue = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    MaxValue = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    Unit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ProgressPercent = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    SubmittedToManagerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubmittedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovalDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ManagerFeedback = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsLocked = table.Column<bool>(type: "bit", nullable: false),
                    LockedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_EmployeeGoals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeGoals_AppraisalCycles_AppraisalCycleId",
                        column: x => x.AppraisalCycleId,
                        principalTable: "AppraisalCycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeGoals_CompanyGoals_CompanyGoalId",
                        column: x => x.CompanyGoalId,
                        principalTable: "CompanyGoals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeGoals_EmployeeGoals_ParentGoalId",
                        column: x => x.ParentGoalId,
                        principalTable: "EmployeeGoals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeGoals_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeGoals_Employees_SubmittedToManagerId",
                        column: x => x.SubmittedToManagerId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeGoals_GoalLibraries_GoalLibraryId",
                        column: x => x.GoalLibraryId,
                        principalTable: "GoalLibraries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeGoals_KpiDefinitions_KpiDefinitionId",
                        column: x => x.KpiDefinitionId,
                        principalTable: "KpiDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeGoals_PerformanceAppraisals_PerformanceAppraisalId",
                        column: x => x.PerformanceAppraisalId,
                        principalTable: "PerformanceAppraisals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeGoals_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeGoals_UnitGoals_UnitGoalId",
                        column: x => x.UnitGoalId,
                        principalTable: "UnitGoals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppraisalKpiEvaluationSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppraisalCriterionScoreSnapshotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActualValue = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    AchievementPercent = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    EvidenceLinks = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SnapshotDate = table.Column<DateTime>(type: "datetime2", nullable: false),
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
                    table.PrimaryKey("PK_AppraisalKpiEvaluationSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppraisalKpiEvaluationSnapshots_AppraisalCriterionScoreSnapshots_AppraisalCriterionScoreSnapshotId",
                        column: x => x.AppraisalCriterionScoreSnapshotId,
                        principalTable: "AppraisalCriterionScoreSnapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppraisalKpiEvaluationSnapshots_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PerformanceAppraisalCriterionConfigGradeRanges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PerformanceAppraisalCriterionConfigId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GradeDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LowScore = table.Column<int>(type: "int", nullable: false),
                    HighScore = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_PerformanceAppraisalCriterionConfigGradeRanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PerformanceAppraisalCriterionConfigGradeRanges_AppraisalGradeDefinitions_GradeDefinitionId",
                        column: x => x.GradeDefinitionId,
                        principalTable: "AppraisalGradeDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PerformanceAppraisalCriterionConfigGradeRanges_PerformanceAppraisalCriterionConfigs_PerformanceAppraisalCriterionConfigId",
                        column: x => x.PerformanceAppraisalCriterionConfigId,
                        principalTable: "PerformanceAppraisalCriterionConfigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PerformanceAppraisalCriterionConfigGradeRanges_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CheckInGoalUpdates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CheckInId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeGoalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedProgress = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    UpdatedStatus = table.Column<int>(type: "int", nullable: false),
                    FlaggedAtRisk = table.Column<bool>(type: "bit", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_CheckInGoalUpdates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CheckInGoalUpdates_CheckIns_CheckInId",
                        column: x => x.CheckInId,
                        principalTable: "CheckIns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CheckInGoalUpdates_EmployeeGoals_EmployeeGoalId",
                        column: x => x.EmployeeGoalId,
                        principalTable: "EmployeeGoals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CheckInGoalUpdates_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeGoalAppraisalAssessments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeGoalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PerformanceAppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SelfFinalProgressPercent = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    SelfFinalStatus = table.Column<int>(type: "int", nullable: true),
                    SelfFinalActualValue = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    SelfAssessmentNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SelfEvidenceLinks = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ManagerFinalProgressPercent = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    ManagerFinalStatus = table.Column<int>(type: "int", nullable: true),
                    ManagerFinalActualValue = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    ManagerAssessmentNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ManagerEvidenceLinks = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_EmployeeGoalAppraisalAssessments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeGoalAppraisalAssessments_EmployeeGoals_EmployeeGoalId",
                        column: x => x.EmployeeGoalId,
                        principalTable: "EmployeeGoals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmployeeGoalAppraisalAssessments_PerformanceAppraisals_PerformanceAppraisalId",
                        column: x => x.PerformanceAppraisalId,
                        principalTable: "PerformanceAppraisals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmployeeGoalAppraisalAssessments_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GoalRequiredSkills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeGoalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompetencyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DevelopmentNeeded = table.Column<bool>(type: "bit", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_GoalRequiredSkills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GoalRequiredSkills_AppraisalCompetencies_CompetencyId",
                        column: x => x.CompetencyId,
                        principalTable: "AppraisalCompetencies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GoalRequiredSkills_EmployeeGoals_EmployeeGoalId",
                        column: x => x.EmployeeGoalId,
                        principalTable: "EmployeeGoals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GoalRequiredSkills_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PerformanceJournalEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppraisalCycleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubjectEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RelatedGoalId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    EntryDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsPrivate = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_PerformanceJournalEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PerformanceJournalEntries_AppraisalCycles_AppraisalCycleId",
                        column: x => x.AppraisalCycleId,
                        principalTable: "AppraisalCycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PerformanceJournalEntries_EmployeeGoals_RelatedGoalId",
                        column: x => x.RelatedGoalId,
                        principalTable: "EmployeeGoals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PerformanceJournalEntries_Employees_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PerformanceJournalEntries_Employees_SubjectEmployeeId",
                        column: x => x.SubjectEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PerformanceJournalEntries_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppraisalConversations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScheduledById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ConductedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Type = table.Column<int>(type: "int", nullable: false),
                    ScheduledDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    HeldDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Agenda = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    PostMeetingNotes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    KeyTakeaways = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsCompleted = table.Column<bool>(type: "bit", nullable: false),
                    ReviewEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_AppraisalConversations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppraisalConversations_Employees_ConductedById",
                        column: x => x.ConductedById,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppraisalConversations_Employees_ScheduledById",
                        column: x => x.ScheduledById,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppraisalConversations_PerformanceAppraisals_AppraisalId",
                        column: x => x.AppraisalId,
                        principalTable: "PerformanceAppraisals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppraisalConversations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppraisalReviewEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppraisalCycleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PerformanceAppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    EventDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    IsLightTouch = table.Column<bool>(type: "bit", nullable: false),
                    IsFullAppraisal = table.Column<bool>(type: "bit", nullable: false),
                    OverallPeriodScore = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    AchievementsSummary = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ChallengesSummary = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ManagerNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedDevelopmentPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_AppraisalReviewEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppraisalReviewEvents_AppraisalConversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "AppraisalConversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppraisalReviewEvents_AppraisalCycles_AppraisalCycleId",
                        column: x => x.AppraisalCycleId,
                        principalTable: "AppraisalCycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppraisalReviewEvents_EmployeeDevelopmentPlans_UpdatedDevelopmentPlanId",
                        column: x => x.UpdatedDevelopmentPlanId,
                        principalTable: "EmployeeDevelopmentPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppraisalReviewEvents_PerformanceAppraisals_PerformanceAppraisalId",
                        column: x => x.PerformanceAppraisalId,
                        principalTable: "PerformanceAppraisals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppraisalReviewEvents_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeDevelopmentObjectives",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DevelopmentPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Actions = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    TargetDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ProgressPercent = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ProgressNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ObjectiveStatus = table.Column<int>(type: "int", nullable: false),
                    UpdatedInReviewEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_EmployeeDevelopmentObjectives", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeDevelopmentObjectives_AppraisalReviewEvents_UpdatedInReviewEventId",
                        column: x => x.UpdatedInReviewEventId,
                        principalTable: "AppraisalReviewEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeDevelopmentObjectives_EmployeeDevelopmentPlans_DevelopmentPlanId",
                        column: x => x.DevelopmentPlanId,
                        principalTable: "EmployeeDevelopmentPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeDevelopmentObjectives_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GoalProgressEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeGoalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProgressPercent = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    ActualValue = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Challenges = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RecordedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntryDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_GoalProgressEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GoalProgressEntries_AppraisalReviewEvents_ReviewEventId",
                        column: x => x.ReviewEventId,
                        principalTable: "AppraisalReviewEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GoalProgressEntries_EmployeeGoals_EmployeeGoalId",
                        column: x => x.EmployeeGoalId,
                        principalTable: "EmployeeGoals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GoalProgressEntries_Employees_RecordedById",
                        column: x => x.RecordedById,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GoalProgressEntries_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "GoalRiskSetting",
                columns: new[] { "Id", "CreatedAt", "CreatedBy", "CreatedById", "DaysRemainingThreshold", "DeletedAt", "DeletedBy", "ExpectedProgressTolerancePercent", "IsActive", "IsDeleted", "LastModifiedById", "MinimumProgressPercent", "TenantId", "UpdatedAt", "UpdatedBy" },
                values: new object[] { new Guid("a1b2c3d4-0000-0000-0000-000000000001"), new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System", null, 14, null, null, 20, true, false, null, 60, new Guid("00000000-0000-0000-0000-000000000001"), null, null });

            migrationBuilder.CreateIndex(
                name: "IX_Team_OrgUnit_Sequence",
                table: "Teams",
                columns: new[] { "OrganizationUnitId", "Sequence" });

            migrationBuilder.CreateIndex(
                name: "IX_Team_Tenant_Code",
                table: "Teams",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Team_Tenant_Status",
                table: "Teams",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Teams_ShiftDefinitionId",
                table: "Teams",
                column: "ShiftDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamMember_Employee_Primary",
                table: "TeamMembers",
                columns: new[] { "EmployeeId", "IsPrimary" });

            migrationBuilder.CreateIndex(
                name: "IX_TeamMember_Team_Active",
                table: "TeamMembers",
                columns: new[] { "TeamId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_TeamMember_Team_Employee",
                table: "TeamMembers",
                columns: new[] { "TeamId", "EmployeeId" });

            migrationBuilder.CreateIndex(
                name: "IX_TeamMemberHistory_Team_Employee_EffectiveFrom",
                table: "TeamMemberHistories",
                columns: new[] { "TeamId", "EmployeeId", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceImprovementPlans_HROwnerId",
                table: "PerformanceImprovementPlans",
                column: "HROwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceAppraisals_AppraisalCycleId",
                table: "PerformanceAppraisals",
                column: "AppraisalCycleId");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceAppraisals_AppraisalTemplateId",
                table: "PerformanceAppraisals",
                column: "AppraisalTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceAppraisals_CalibrationSessionId",
                table: "PerformanceAppraisals",
                column: "CalibrationSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceAppraisals_CurrentAppealStatus",
                table: "PerformanceAppraisals",
                column: "CurrentAppealStatus");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceAppraisals_DevelopmentPlanId",
                table: "PerformanceAppraisals",
                column: "DevelopmentPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceAppraisals_HasAppeal",
                table: "PerformanceAppraisals",
                column: "HasAppeal");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceAppraisals_IsCalibrated",
                table: "PerformanceAppraisals",
                column: "IsCalibrated");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceAppraisals_OverallGradeDefinitionId",
                table: "PerformanceAppraisals",
                column: "OverallGradeDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_KpiDefinitions_IsActive",
                table: "KpiDefinitions",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalEmployeeResponses_ResponseStatus",
                table: "AppraisalEmployeeResponses",
                column: "ResponseStatus");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalAttachments_AppealId",
                table: "AppraisalAttachments",
                column: "AppealId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalAttachments_CalibrationSessionId",
                table: "AppraisalAttachments",
                column: "CalibrationSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalAttachments_CheckInId",
                table: "AppraisalAttachments",
                column: "CheckInId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalAttachments_EmployeeGoalId",
                table: "AppraisalAttachments",
                column: "EmployeeGoalId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalAttachments_EntityType",
                table: "AppraisalAttachments",
                column: "EntityType");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalAttachments_PerformanceAppraisalId",
                table: "AppraisalAttachments",
                column: "PerformanceAppraisalId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalAttachments_PipId",
                table: "AppraisalAttachments",
                column: "PipId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalAttachments_ReviewEventId",
                table: "AppraisalAttachments",
                column: "ReviewEventId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalAttachments_UnitGoalId",
                table: "AppraisalAttachments",
                column: "UnitGoalId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalAppealItems_AppraisalAppealId",
                table: "AppraisalAppealItems",
                column: "AppraisalAppealId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalAppealItems_TemplateItemId",
                table: "AppraisalAppealItems",
                column: "TemplateItemId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalAppealItems_TenantId",
                table: "AppraisalAppealItems",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalAppeals_EmployeeId",
                table: "AppraisalAppeals",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalAppeals_PerformanceAppraisalId",
                table: "AppraisalAppeals",
                column: "PerformanceAppraisalId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalAppeals_ReviewedById",
                table: "AppraisalAppeals",
                column: "ReviewedById");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalAppeals_Status",
                table: "AppraisalAppeals",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalAppeals_SubmittedDate",
                table: "AppraisalAppeals",
                column: "SubmittedDate");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalAppeals_TenantId",
                table: "AppraisalAppeals",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalCompetencies_Code",
                table: "AppraisalCompetencies",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalCompetencies_CriteriaName",
                table: "AppraisalCompetencies",
                column: "CriteriaName");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalCompetencies_TenantId",
                table: "AppraisalCompetencies",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalConversations_AppraisalId",
                table: "AppraisalConversations",
                column: "AppraisalId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalConversations_ConductedById",
                table: "AppraisalConversations",
                column: "ConductedById");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalConversations_IsCompleted",
                table: "AppraisalConversations",
                column: "IsCompleted");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalConversations_ReviewEventId",
                table: "AppraisalConversations",
                column: "ReviewEventId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalConversations_ScheduledById",
                table: "AppraisalConversations",
                column: "ScheduledById");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalConversations_ScheduledDate",
                table: "AppraisalConversations",
                column: "ScheduledDate");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalConversations_TenantId",
                table: "AppraisalConversations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalConversations_Type",
                table: "AppraisalConversations",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalCriterionScoreSnapshots_AppraisalEvaluationSnapshotId",
                table: "AppraisalCriterionScoreSnapshots",
                column: "AppraisalEvaluationSnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalCriterionScoreSnapshots_TemplateItemId",
                table: "AppraisalCriterionScoreSnapshots",
                column: "TemplateItemId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalCriterionScoreSnapshots_TenantId",
                table: "AppraisalCriterionScoreSnapshots",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalCustomQuestionResponses_PerformanceAppraisalId",
                table: "AppraisalCustomQuestionResponses",
                column: "PerformanceAppraisalId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalCustomQuestionResponses_PerformanceAppraisalId_TemplateItemId",
                table: "AppraisalCustomQuestionResponses",
                columns: new[] { "PerformanceAppraisalId", "TemplateItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalCustomQuestionResponses_TemplateItemId",
                table: "AppraisalCustomQuestionResponses",
                column: "TemplateItemId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalCustomQuestionResponses_TenantId",
                table: "AppraisalCustomQuestionResponses",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalCycles_AppraisalSettingsId",
                table: "AppraisalCycles",
                column: "AppraisalSettingsId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalCycles_AppraisalType",
                table: "AppraisalCycles",
                column: "AppraisalType");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalCycles_ClosedById",
                table: "AppraisalCycles",
                column: "ClosedById");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalCycles_CycleCode",
                table: "AppraisalCycles",
                column: "CycleCode");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalCycles_CycleName",
                table: "AppraisalCycles",
                column: "CycleName");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalCycles_EndDate",
                table: "AppraisalCycles",
                column: "EndDate");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalCycles_OpenedById",
                table: "AppraisalCycles",
                column: "OpenedById");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalCycles_StartDate",
                table: "AppraisalCycles",
                column: "StartDate");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalCycles_Status",
                table: "AppraisalCycles",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalCycles_TenantId",
                table: "AppraisalCycles",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalCycles_Year",
                table: "AppraisalCycles",
                column: "Year");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalCycleTargetExclusions_AppraisalCycleTargetId",
                table: "AppraisalCycleTargetExclusions",
                column: "AppraisalCycleTargetId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalCycleTargetExclusions_EmployeeId",
                table: "AppraisalCycleTargetExclusions",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalCycleTargetExclusions_IsActive",
                table: "AppraisalCycleTargetExclusions",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalCycleTargetExclusions_OrganizationLevelId",
                table: "AppraisalCycleTargetExclusions",
                column: "OrganizationLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalCycleTargetExclusions_OrganizationUnitId",
                table: "AppraisalCycleTargetExclusions",
                column: "OrganizationUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalCycleTargetExclusions_PositionId",
                table: "AppraisalCycleTargetExclusions",
                column: "PositionId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalCycleTargetExclusions_TenantId",
                table: "AppraisalCycleTargetExclusions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalCycleTargets_AppraisalCycleId",
                table: "AppraisalCycleTargets",
                column: "AppraisalCycleId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalCycleTargets_IsActive",
                table: "AppraisalCycleTargets",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalCycleTargets_OrganizationLevelId",
                table: "AppraisalCycleTargets",
                column: "OrganizationLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalCycleTargets_OrganizationUnitId",
                table: "AppraisalCycleTargets",
                column: "OrganizationUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalCycleTargets_PositionId",
                table: "AppraisalCycleTargets",
                column: "PositionId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalCycleTargets_TargetType",
                table: "AppraisalCycleTargets",
                column: "TargetType");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalCycleTargets_TenantId",
                table: "AppraisalCycleTargets",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalCycleTemplates_AppraisalCycleId",
                table: "AppraisalCycleTemplates",
                column: "AppraisalCycleId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalCycleTemplates_AppraisalTemplateId",
                table: "AppraisalCycleTemplates",
                column: "AppraisalTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalCycleTemplates_IsActive",
                table: "AppraisalCycleTemplates",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalCycleTemplates_Priority",
                table: "AppraisalCycleTemplates",
                column: "Priority");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalCycleTemplates_TenantId",
                table: "AppraisalCycleTemplates",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalEvaluationSnapshots_AppraisalId",
                table: "AppraisalEvaluationSnapshots",
                column: "AppraisalId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalEvaluationSnapshots_EvaluatorId",
                table: "AppraisalEvaluationSnapshots",
                column: "EvaluatorId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalEvaluationSnapshots_SnapshotDate",
                table: "AppraisalEvaluationSnapshots",
                column: "SnapshotDate");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalEvaluationSnapshots_TenantId",
                table: "AppraisalEvaluationSnapshots",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalHRReviews_AppraisalId",
                table: "AppraisalHRReviews",
                column: "AppraisalId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalHRReviews_IsApproved",
                table: "AppraisalHRReviews",
                column: "IsApproved");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalHRReviews_ReviewedByHRId",
                table: "AppraisalHRReviews",
                column: "ReviewedByHRId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalHRReviews_TenantId",
                table: "AppraisalHRReviews",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalKpiEvaluationSnapshots_AppraisalCriterionScoreSnapshotId",
                table: "AppraisalKpiEvaluationSnapshots",
                column: "AppraisalCriterionScoreSnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalKpiEvaluationSnapshots_TenantId",
                table: "AppraisalKpiEvaluationSnapshots",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalManualAdvanceLogs_AdvancedByEmployeeId",
                table: "AppraisalManualAdvanceLogs",
                column: "AdvancedByEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalManualAdvanceLogs_PerformanceAppraisalId",
                table: "AppraisalManualAdvanceLogs",
                column: "PerformanceAppraisalId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalManualAdvanceLogs_TenantId",
                table: "AppraisalManualAdvanceLogs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalNotifications_AppraisalId",
                table: "AppraisalNotifications",
                column: "AppraisalId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalNotifications_RecipientEmployeeId",
                table: "AppraisalNotifications",
                column: "RecipientEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalNotifications_TenantId",
                table: "AppraisalNotifications",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalOutcomeRecommendations_PerformanceAppraisalId",
                table: "AppraisalOutcomeRecommendations",
                column: "PerformanceAppraisalId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalOutcomeRecommendations_RecommendationType",
                table: "AppraisalOutcomeRecommendations",
                column: "RecommendationType");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalOutcomeRecommendations_Status",
                table: "AppraisalOutcomeRecommendations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalOutcomeRecommendations_TenantId",
                table: "AppraisalOutcomeRecommendations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalReviewEvents_AppraisalCycleId",
                table: "AppraisalReviewEvents",
                column: "AppraisalCycleId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalReviewEvents_ConversationId",
                table: "AppraisalReviewEvents",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalReviewEvents_EventDate",
                table: "AppraisalReviewEvents",
                column: "EventDate");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalReviewEvents_PerformanceAppraisalId",
                table: "AppraisalReviewEvents",
                column: "PerformanceAppraisalId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalReviewEvents_Status",
                table: "AppraisalReviewEvents",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalReviewEvents_TenantId",
                table: "AppraisalReviewEvents",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalReviewEvents_Type",
                table: "AppraisalReviewEvents",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalReviewEvents_UpdatedDevelopmentPlanId",
                table: "AppraisalReviewEvents",
                column: "UpdatedDevelopmentPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalSettings_RequireHRReview",
                table: "AppraisalSettings",
                column: "RequireHRReview");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalSettings_RequireManagerEvaluation",
                table: "AppraisalSettings",
                column: "RequireManagerEvaluation");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalSettings_RequirePeerReviews",
                table: "AppraisalSettings",
                column: "RequirePeerReviews");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalSettings_RequireSelfEvaluation",
                table: "AppraisalSettings",
                column: "RequireSelfEvaluation");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalSettings_SettingsName",
                table: "AppraisalSettings",
                column: "SettingsName");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalSettings_TenantId",
                table: "AppraisalSettings",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalTemplateItems_AppraisalTemplateSectionId",
                table: "AppraisalTemplateItems",
                column: "AppraisalTemplateSectionId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalTemplateItems_CompetencyId",
                table: "AppraisalTemplateItems",
                column: "CompetencyId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalTemplateItems_DisplayOrder",
                table: "AppraisalTemplateItems",
                column: "DisplayOrder");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalTemplateItems_KpiDefinitionId",
                table: "AppraisalTemplateItems",
                column: "KpiDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalTemplateItems_TenantId",
                table: "AppraisalTemplateItems",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalTemplates_ApprovalStatus",
                table: "AppraisalTemplates",
                column: "ApprovalStatus");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalTemplates_IsActive",
                table: "AppraisalTemplates",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalTemplates_OrganizationLevelId",
                table: "AppraisalTemplates",
                column: "OrganizationLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalTemplates_OrganizationUnitId",
                table: "AppraisalTemplates",
                column: "OrganizationUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalTemplates_PositionId",
                table: "AppraisalTemplates",
                column: "PositionId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalTemplates_TemplateName",
                table: "AppraisalTemplates",
                column: "TemplateName");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalTemplates_TenantId",
                table: "AppraisalTemplates",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalTemplateSections_AppraisalTemplateId",
                table: "AppraisalTemplateSections",
                column: "AppraisalTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalTemplateSections_DisplayOrder",
                table: "AppraisalTemplateSections",
                column: "DisplayOrder");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalTemplateSections_TenantId",
                table: "AppraisalTemplateSections",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CalibrationParticipants_CalibrationSessionId",
                table: "CalibrationParticipants",
                column: "CalibrationSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_CalibrationParticipants_EmployeeId",
                table: "CalibrationParticipants",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_CalibrationParticipants_TenantId",
                table: "CalibrationParticipants",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CalibrationRatingAdjustments_AdjustedById",
                table: "CalibrationRatingAdjustments",
                column: "AdjustedById");

            migrationBuilder.CreateIndex(
                name: "IX_CalibrationRatingAdjustments_AdjustmentDate",
                table: "CalibrationRatingAdjustments",
                column: "AdjustmentDate");

            migrationBuilder.CreateIndex(
                name: "IX_CalibrationRatingAdjustments_CalibrationSessionId",
                table: "CalibrationRatingAdjustments",
                column: "CalibrationSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_CalibrationRatingAdjustments_PerformanceAppraisalId",
                table: "CalibrationRatingAdjustments",
                column: "PerformanceAppraisalId");

            migrationBuilder.CreateIndex(
                name: "IX_CalibrationRatingAdjustments_TemplateItemId",
                table: "CalibrationRatingAdjustments",
                column: "TemplateItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CalibrationRatingAdjustments_TenantId",
                table: "CalibrationRatingAdjustments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CalibrationSessions_AppraisalCycleId",
                table: "CalibrationSessions",
                column: "AppraisalCycleId");

            migrationBuilder.CreateIndex(
                name: "IX_CalibrationSessions_CompletedById",
                table: "CalibrationSessions",
                column: "CompletedById");

            migrationBuilder.CreateIndex(
                name: "IX_CalibrationSessions_FacilitatedById",
                table: "CalibrationSessions",
                column: "FacilitatedById");

            migrationBuilder.CreateIndex(
                name: "IX_CalibrationSessions_OrganizationLevelId",
                table: "CalibrationSessions",
                column: "OrganizationLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_CalibrationSessions_OrganizationUnitId",
                table: "CalibrationSessions",
                column: "OrganizationUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_CalibrationSessions_ScheduledDate",
                table: "CalibrationSessions",
                column: "ScheduledDate");

            migrationBuilder.CreateIndex(
                name: "IX_CalibrationSessions_Status",
                table: "CalibrationSessions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_CalibrationSessions_TenantId",
                table: "CalibrationSessions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CheckInGoalUpdates_CheckInId",
                table: "CheckInGoalUpdates",
                column: "CheckInId");

            migrationBuilder.CreateIndex(
                name: "IX_CheckInGoalUpdates_EmployeeGoalId",
                table: "CheckInGoalUpdates",
                column: "EmployeeGoalId");

            migrationBuilder.CreateIndex(
                name: "IX_CheckInGoalUpdates_TenantId",
                table: "CheckInGoalUpdates",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CheckInObjectiveLinks_CheckInId",
                table: "CheckInObjectiveLinks",
                column: "CheckInId");

            migrationBuilder.CreateIndex(
                name: "IX_CheckInObjectiveLinks_CheckInId_CompanyGoalId",
                table: "CheckInObjectiveLinks",
                columns: new[] { "CheckInId", "CompanyGoalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CheckInObjectiveLinks_CompanyGoalId",
                table: "CheckInObjectiveLinks",
                column: "CompanyGoalId");

            migrationBuilder.CreateIndex(
                name: "IX_CheckInObjectiveLinks_TenantId",
                table: "CheckInObjectiveLinks",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CheckIns_AppraisalCycleId",
                table: "CheckIns",
                column: "AppraisalCycleId");

            migrationBuilder.CreateIndex(
                name: "IX_CheckIns_CheckInType",
                table: "CheckIns",
                column: "CheckInType");

            migrationBuilder.CreateIndex(
                name: "IX_CheckIns_ConductedById",
                table: "CheckIns",
                column: "ConductedById");

            migrationBuilder.CreateIndex(
                name: "IX_CheckIns_EmployeeId",
                table: "CheckIns",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_CheckIns_ScheduledDate",
                table: "CheckIns",
                column: "ScheduledDate");

            migrationBuilder.CreateIndex(
                name: "IX_CheckIns_TenantId",
                table: "CheckIns",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyGoals_AppraisalCycleId",
                table: "CompanyGoals",
                column: "AppraisalCycleId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyGoals_IsVisible",
                table: "CompanyGoals",
                column: "IsVisible");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyGoals_Priority",
                table: "CompanyGoals",
                column: "Priority");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyGoals_StrategicGoalId",
                table: "CompanyGoals",
                column: "StrategicGoalId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyGoals_TenantId",
                table: "CompanyGoals",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDevelopmentObjectives_DevelopmentPlanId",
                table: "EmployeeDevelopmentObjectives",
                column: "DevelopmentPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDevelopmentObjectives_ObjectiveStatus",
                table: "EmployeeDevelopmentObjectives",
                column: "ObjectiveStatus");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDevelopmentObjectives_TenantId",
                table: "EmployeeDevelopmentObjectives",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDevelopmentObjectives_UpdatedInReviewEventId",
                table: "EmployeeDevelopmentObjectives",
                column: "UpdatedInReviewEventId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDevelopmentPlanFeedbacks_DevelopmentPlanId",
                table: "EmployeeDevelopmentPlanFeedbacks",
                column: "DevelopmentPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDevelopmentPlanFeedbacks_TenantId",
                table: "EmployeeDevelopmentPlanFeedbacks",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDevelopmentPlans_AppraisalCycleId",
                table: "EmployeeDevelopmentPlans",
                column: "AppraisalCycleId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDevelopmentPlans_EmployeeId",
                table: "EmployeeDevelopmentPlans",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDevelopmentPlans_PlanStatus",
                table: "EmployeeDevelopmentPlans",
                column: "PlanStatus");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDevelopmentPlans_TenantId",
                table: "EmployeeDevelopmentPlans",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeGoalAppraisalAssessments_EmployeeGoalId",
                table: "EmployeeGoalAppraisalAssessments",
                column: "EmployeeGoalId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeGoalAppraisalAssessments_PerformanceAppraisalId",
                table: "EmployeeGoalAppraisalAssessments",
                column: "PerformanceAppraisalId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeGoalAppraisalAssessments_TenantId",
                table: "EmployeeGoalAppraisalAssessments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeGoal_DueDate",
                table: "EmployeeGoals",
                column: "DueDate");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeGoal_ProgressPercent",
                table: "EmployeeGoals",
                column: "ProgressPercent");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeGoals_AppraisalCycleId",
                table: "EmployeeGoals",
                column: "AppraisalCycleId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeGoals_CompanyGoalId",
                table: "EmployeeGoals",
                column: "CompanyGoalId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeGoals_EmployeeId",
                table: "EmployeeGoals",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeGoals_GoalLibraryId",
                table: "EmployeeGoals",
                column: "GoalLibraryId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeGoals_IsLocked",
                table: "EmployeeGoals",
                column: "IsLocked");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeGoals_KpiDefinitionId",
                table: "EmployeeGoals",
                column: "KpiDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeGoals_ParentGoalId",
                table: "EmployeeGoals",
                column: "ParentGoalId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeGoals_PerformanceAppraisalId",
                table: "EmployeeGoals",
                column: "PerformanceAppraisalId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeGoals_Priority",
                table: "EmployeeGoals",
                column: "Priority");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeGoals_Status",
                table: "EmployeeGoals",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeGoals_SubmittedToManagerId",
                table: "EmployeeGoals",
                column: "SubmittedToManagerId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeGoals_TenantId",
                table: "EmployeeGoals",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeGoals_UnitGoalId",
                table: "EmployeeGoals",
                column: "UnitGoalId");

            migrationBuilder.CreateIndex(
                name: "IX_EmploymentActionProposals_EmployeeId",
                table: "EmploymentActionProposals",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmploymentActionProposals_SourceAppraisalId",
                table: "EmploymentActionProposals",
                column: "SourceAppraisalId");

            migrationBuilder.CreateIndex(
                name: "IX_EmploymentActionProposals_Status",
                table: "EmploymentActionProposals",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_EmploymentActionProposals_TenantId",
                table: "EmploymentActionProposals",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_GoalLibraries_IsActive",
                table: "GoalLibraries",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_GoalLibraries_OrganizationLevelId",
                table: "GoalLibraries",
                column: "OrganizationLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_GoalLibraries_OrganizationUnitId",
                table: "GoalLibraries",
                column: "OrganizationUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_GoalLibraries_PositionId",
                table: "GoalLibraries",
                column: "PositionId");

            migrationBuilder.CreateIndex(
                name: "IX_GoalLibraries_TenantId",
                table: "GoalLibraries",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_GoalProgressEntries_EmployeeGoalId",
                table: "GoalProgressEntries",
                column: "EmployeeGoalId");

            migrationBuilder.CreateIndex(
                name: "IX_GoalProgressEntries_EntryDate",
                table: "GoalProgressEntries",
                column: "EntryDate");

            migrationBuilder.CreateIndex(
                name: "IX_GoalProgressEntries_RecordedById",
                table: "GoalProgressEntries",
                column: "RecordedById");

            migrationBuilder.CreateIndex(
                name: "IX_GoalProgressEntries_ReviewEventId",
                table: "GoalProgressEntries",
                column: "ReviewEventId");

            migrationBuilder.CreateIndex(
                name: "IX_GoalProgressEntries_Status",
                table: "GoalProgressEntries",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_GoalProgressEntries_TenantId",
                table: "GoalProgressEntries",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_GoalRequiredSkills_CompetencyId",
                table: "GoalRequiredSkills",
                column: "CompetencyId");

            migrationBuilder.CreateIndex(
                name: "IX_GoalRequiredSkills_EmployeeGoalId",
                table: "GoalRequiredSkills",
                column: "EmployeeGoalId");

            migrationBuilder.CreateIndex(
                name: "IX_GoalRequiredSkills_EmployeeGoalId_CompetencyId",
                table: "GoalRequiredSkills",
                columns: new[] { "EmployeeGoalId", "CompetencyId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GoalRequiredSkills_TenantId",
                table: "GoalRequiredSkills",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_GoalRiskSetting_IsActive",
                table: "GoalRiskSetting",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_GoalRiskSetting_TenantId",
                table: "GoalRiskSetting",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PeerNominations_AppraisalId",
                table: "PeerNominations",
                column: "AppraisalId");

            migrationBuilder.CreateIndex(
                name: "IX_PeerNominations_AppraisalId_PeerEmployeeId",
                table: "PeerNominations",
                columns: new[] { "AppraisalId", "PeerEmployeeId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_PeerNominations_ApprovedByManagerId",
                table: "PeerNominations",
                column: "ApprovedByManagerId");

            migrationBuilder.CreateIndex(
                name: "IX_PeerNominations_NominatedById",
                table: "PeerNominations",
                column: "NominatedById");

            migrationBuilder.CreateIndex(
                name: "IX_PeerNominations_NominationDate",
                table: "PeerNominations",
                column: "NominationDate");

            migrationBuilder.CreateIndex(
                name: "IX_PeerNominations_NominationStatus",
                table: "PeerNominations",
                column: "NominationStatus");

            migrationBuilder.CreateIndex(
                name: "IX_PeerNominations_PeerEmployeeId",
                table: "PeerNominations",
                column: "PeerEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_PeerNominations_TenantId",
                table: "PeerNominations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceAppraisalCriterionConfigGradeRanges_GradeDefinitionId",
                table: "PerformanceAppraisalCriterionConfigGradeRanges",
                column: "GradeDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceAppraisalCriterionConfigGradeRanges_PerformanceAppraisalCriterionConfigId",
                table: "PerformanceAppraisalCriterionConfigGradeRanges",
                column: "PerformanceAppraisalCriterionConfigId");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceAppraisalCriterionConfigGradeRanges_TenantId",
                table: "PerformanceAppraisalCriterionConfigGradeRanges",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceAppraisalCriterionConfigs_PerformanceAppraisalId",
                table: "PerformanceAppraisalCriterionConfigs",
                column: "PerformanceAppraisalId");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceAppraisalCriterionConfigs_TemplateItemId",
                table: "PerformanceAppraisalCriterionConfigs",
                column: "TemplateItemId");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceAppraisalCriterionConfigs_TenantId",
                table: "PerformanceAppraisalCriterionConfigs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceJournalEntries_AppraisalCycleId",
                table: "PerformanceJournalEntries",
                column: "AppraisalCycleId");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceJournalEntries_EntryDate",
                table: "PerformanceJournalEntries",
                column: "EntryDate");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceJournalEntries_IsPrivate",
                table: "PerformanceJournalEntries",
                column: "IsPrivate");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceJournalEntries_OwnerId",
                table: "PerformanceJournalEntries",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceJournalEntries_RelatedGoalId",
                table: "PerformanceJournalEntries",
                column: "RelatedGoalId");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceJournalEntries_SubjectEmployeeId",
                table: "PerformanceJournalEntries",
                column: "SubjectEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceJournalEntries_TenantId",
                table: "PerformanceJournalEntries",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PipGoals_DueDate",
                table: "PipGoals",
                column: "DueDate");

            migrationBuilder.CreateIndex(
                name: "IX_PipGoals_PipId",
                table: "PipGoals",
                column: "PipId");

            migrationBuilder.CreateIndex(
                name: "IX_PipGoals_Status",
                table: "PipGoals",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PipGoals_TenantId",
                table: "PipGoals",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_SalaryReviewProposals_EmployeeId",
                table: "SalaryReviewProposals",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_SalaryReviewProposals_SourceAppraisalId",
                table: "SalaryReviewProposals",
                column: "SourceAppraisalId");

            migrationBuilder.CreateIndex(
                name: "IX_SalaryReviewProposals_Status",
                table: "SalaryReviewProposals",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_SalaryReviewProposals_TenantId",
                table: "SalaryReviewProposals",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_StrategicGoals_IsActive",
                table: "StrategicGoals",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_StrategicGoals_Priority",
                table: "StrategicGoals",
                column: "Priority");

            migrationBuilder.CreateIndex(
                name: "IX_StrategicGoals_StartYear",
                table: "StrategicGoals",
                column: "StartYear");

            migrationBuilder.CreateIndex(
                name: "IX_StrategicGoals_TenantId",
                table: "StrategicGoals",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TemplateItemGradeRanges_AppraisalTemplateItemId",
                table: "TemplateItemGradeRanges",
                column: "AppraisalTemplateItemId");

            migrationBuilder.CreateIndex(
                name: "IX_TemplateItemGradeRanges_GradeDefinitionId",
                table: "TemplateItemGradeRanges",
                column: "GradeDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_TemplateItemGradeRanges_TenantId",
                table: "TemplateItemGradeRanges",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_UnitGoals_AppraisalCycleId",
                table: "UnitGoals",
                column: "AppraisalCycleId");

            migrationBuilder.CreateIndex(
                name: "IX_UnitGoals_CreatedByManagerId",
                table: "UnitGoals",
                column: "CreatedByManagerId");

            migrationBuilder.CreateIndex(
                name: "IX_UnitGoals_OrganizationLevelId",
                table: "UnitGoals",
                column: "OrganizationLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_UnitGoals_OrganizationUnitId",
                table: "UnitGoals",
                column: "OrganizationUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_UnitGoals_ParentCompanyGoalId",
                table: "UnitGoals",
                column: "ParentCompanyGoalId");

            migrationBuilder.CreateIndex(
                name: "IX_UnitGoals_ParentUnitGoalId",
                table: "UnitGoals",
                column: "ParentUnitGoalId");

            migrationBuilder.CreateIndex(
                name: "IX_UnitGoals_Priority",
                table: "UnitGoals",
                column: "Priority");

            migrationBuilder.CreateIndex(
                name: "IX_UnitGoals_TenantId",
                table: "UnitGoals",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_AppraisalAttachments_AppraisalAppeals_AppealId",
                table: "AppraisalAttachments",
                column: "AppealId",
                principalTable: "AppraisalAppeals",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AppraisalAttachments_AppraisalReviewEvents_ReviewEventId",
                table: "AppraisalAttachments",
                column: "ReviewEventId",
                principalTable: "AppraisalReviewEvents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AppraisalAttachments_CalibrationSessions_CalibrationSessionId",
                table: "AppraisalAttachments",
                column: "CalibrationSessionId",
                principalTable: "CalibrationSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AppraisalAttachments_CheckIns_CheckInId",
                table: "AppraisalAttachments",
                column: "CheckInId",
                principalTable: "CheckIns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AppraisalAttachments_EmployeeGoals_EmployeeGoalId",
                table: "AppraisalAttachments",
                column: "EmployeeGoalId",
                principalTable: "EmployeeGoals",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AppraisalAttachments_Employees_UploadedById",
                table: "AppraisalAttachments",
                column: "UploadedById",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AppraisalAttachments_PerformanceAppraisals_PerformanceAppraisalId",
                table: "AppraisalAttachments",
                column: "PerformanceAppraisalId",
                principalTable: "PerformanceAppraisals",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AppraisalAttachments_PerformanceImprovementPlans_PipId",
                table: "AppraisalAttachments",
                column: "PipId",
                principalTable: "PerformanceImprovementPlans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AppraisalAttachments_UnitGoals_UnitGoalId",
                table: "AppraisalAttachments",
                column: "UnitGoalId",
                principalTable: "UnitGoals",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AppraisalEmployeeResponses_AppraisalTemplateItems_TemplateItemId",
                table: "AppraisalEmployeeResponses",
                column: "TemplateItemId",
                principalTable: "AppraisalTemplateItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CriterionScores_AppraisalGradeDefinitions_GradeDefinitionId",
                table: "CriterionScores",
                column: "GradeDefinitionId",
                principalTable: "AppraisalGradeDefinitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CriterionScores_AppraisalTemplateItems_TemplateItemId",
                table: "CriterionScores",
                column: "TemplateItemId",
                principalTable: "AppraisalTemplateItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PerformanceAppraisals_AppraisalCycles_AppraisalCycleId",
                table: "PerformanceAppraisals",
                column: "AppraisalCycleId",
                principalTable: "AppraisalCycles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PerformanceAppraisals_AppraisalGradeDefinitions_OverallGradeDefinitionId",
                table: "PerformanceAppraisals",
                column: "OverallGradeDefinitionId",
                principalTable: "AppraisalGradeDefinitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PerformanceAppraisals_AppraisalTemplates_AppraisalTemplateId",
                table: "PerformanceAppraisals",
                column: "AppraisalTemplateId",
                principalTable: "AppraisalTemplates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PerformanceAppraisals_CalibrationSessions_CalibrationSessionId",
                table: "PerformanceAppraisals",
                column: "CalibrationSessionId",
                principalTable: "CalibrationSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PerformanceAppraisals_EmployeeDevelopmentPlans_DevelopmentPlanId",
                table: "PerformanceAppraisals",
                column: "DevelopmentPlanId",
                principalTable: "EmployeeDevelopmentPlans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PerformanceImprovementPlans_Employees_HROwnerId",
                table: "PerformanceImprovementPlans",
                column: "HROwnerId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TeamMemberHistories_Employees_EmployeeId",
                table: "TeamMemberHistories",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TeamMemberHistories_Teams_TeamId",
                table: "TeamMemberHistories",
                column: "TeamId",
                principalTable: "Teams",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TeamMembers_Employees_EmployeeId",
                table: "TeamMembers",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TeamMembers_Teams_TeamId",
                table: "TeamMembers",
                column: "TeamId",
                principalTable: "Teams",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Teams_Employees_TeamLeadId",
                table: "Teams",
                column: "TeamLeadId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Teams_Locations_LocationId",
                table: "Teams",
                column: "LocationId",
                principalTable: "Locations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Teams_ShiftDefinitions_ShiftDefinitionId",
                table: "Teams",
                column: "ShiftDefinitionId",
                principalTable: "ShiftDefinitions",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Teams_ShiftDefinitions_ShiftId",
                table: "Teams",
                column: "ShiftId",
                principalTable: "ShiftDefinitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Teams_Teams_ParentTeamId",
                table: "Teams",
                column: "ParentTeamId",
                principalTable: "Teams",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AppraisalConversations_AppraisalReviewEvents_ReviewEventId",
                table: "AppraisalConversations",
                column: "ReviewEventId",
                principalTable: "AppraisalReviewEvents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppraisalAttachments_AppraisalAppeals_AppealId",
                table: "AppraisalAttachments");

            migrationBuilder.DropForeignKey(
                name: "FK_AppraisalAttachments_AppraisalReviewEvents_ReviewEventId",
                table: "AppraisalAttachments");

            migrationBuilder.DropForeignKey(
                name: "FK_AppraisalAttachments_CalibrationSessions_CalibrationSessionId",
                table: "AppraisalAttachments");

            migrationBuilder.DropForeignKey(
                name: "FK_AppraisalAttachments_CheckIns_CheckInId",
                table: "AppraisalAttachments");

            migrationBuilder.DropForeignKey(
                name: "FK_AppraisalAttachments_EmployeeGoals_EmployeeGoalId",
                table: "AppraisalAttachments");

            migrationBuilder.DropForeignKey(
                name: "FK_AppraisalAttachments_Employees_UploadedById",
                table: "AppraisalAttachments");

            migrationBuilder.DropForeignKey(
                name: "FK_AppraisalAttachments_PerformanceAppraisals_PerformanceAppraisalId",
                table: "AppraisalAttachments");

            migrationBuilder.DropForeignKey(
                name: "FK_AppraisalAttachments_PerformanceImprovementPlans_PipId",
                table: "AppraisalAttachments");

            migrationBuilder.DropForeignKey(
                name: "FK_AppraisalAttachments_UnitGoals_UnitGoalId",
                table: "AppraisalAttachments");

            migrationBuilder.DropForeignKey(
                name: "FK_AppraisalEmployeeResponses_AppraisalTemplateItems_TemplateItemId",
                table: "AppraisalEmployeeResponses");

            migrationBuilder.DropForeignKey(
                name: "FK_CriterionScores_AppraisalGradeDefinitions_GradeDefinitionId",
                table: "CriterionScores");

            migrationBuilder.DropForeignKey(
                name: "FK_CriterionScores_AppraisalTemplateItems_TemplateItemId",
                table: "CriterionScores");

            migrationBuilder.DropForeignKey(
                name: "FK_PerformanceAppraisals_AppraisalCycles_AppraisalCycleId",
                table: "PerformanceAppraisals");

            migrationBuilder.DropForeignKey(
                name: "FK_PerformanceAppraisals_AppraisalGradeDefinitions_OverallGradeDefinitionId",
                table: "PerformanceAppraisals");

            migrationBuilder.DropForeignKey(
                name: "FK_PerformanceAppraisals_AppraisalTemplates_AppraisalTemplateId",
                table: "PerformanceAppraisals");

            migrationBuilder.DropForeignKey(
                name: "FK_PerformanceAppraisals_CalibrationSessions_CalibrationSessionId",
                table: "PerformanceAppraisals");

            migrationBuilder.DropForeignKey(
                name: "FK_PerformanceAppraisals_EmployeeDevelopmentPlans_DevelopmentPlanId",
                table: "PerformanceAppraisals");

            migrationBuilder.DropForeignKey(
                name: "FK_PerformanceImprovementPlans_Employees_HROwnerId",
                table: "PerformanceImprovementPlans");

            migrationBuilder.DropForeignKey(
                name: "FK_TeamMemberHistories_Employees_EmployeeId",
                table: "TeamMemberHistories");

            migrationBuilder.DropForeignKey(
                name: "FK_TeamMemberHistories_Teams_TeamId",
                table: "TeamMemberHistories");

            migrationBuilder.DropForeignKey(
                name: "FK_TeamMembers_Employees_EmployeeId",
                table: "TeamMembers");

            migrationBuilder.DropForeignKey(
                name: "FK_TeamMembers_Teams_TeamId",
                table: "TeamMembers");

            migrationBuilder.DropForeignKey(
                name: "FK_Teams_Employees_TeamLeadId",
                table: "Teams");

            migrationBuilder.DropForeignKey(
                name: "FK_Teams_Locations_LocationId",
                table: "Teams");

            migrationBuilder.DropForeignKey(
                name: "FK_Teams_ShiftDefinitions_ShiftDefinitionId",
                table: "Teams");

            migrationBuilder.DropForeignKey(
                name: "FK_Teams_ShiftDefinitions_ShiftId",
                table: "Teams");

            migrationBuilder.DropForeignKey(
                name: "FK_Teams_Teams_ParentTeamId",
                table: "Teams");

            migrationBuilder.DropForeignKey(
                name: "FK_AppraisalConversations_AppraisalReviewEvents_ReviewEventId",
                table: "AppraisalConversations");

            migrationBuilder.DropTable(
                name: "AppraisalAppealItems");

            migrationBuilder.DropTable(
                name: "AppraisalCustomQuestionResponses");

            migrationBuilder.DropTable(
                name: "AppraisalCycleTargetExclusions");

            migrationBuilder.DropTable(
                name: "AppraisalCycleTemplates");

            migrationBuilder.DropTable(
                name: "AppraisalHRReviews");

            migrationBuilder.DropTable(
                name: "AppraisalKpiEvaluationSnapshots");

            migrationBuilder.DropTable(
                name: "AppraisalManualAdvanceLogs");

            migrationBuilder.DropTable(
                name: "AppraisalNotifications");

            migrationBuilder.DropTable(
                name: "AppraisalOutcomeRecommendations");

            migrationBuilder.DropTable(
                name: "CalibrationParticipants");

            migrationBuilder.DropTable(
                name: "CalibrationRatingAdjustments");

            migrationBuilder.DropTable(
                name: "CheckInGoalUpdates");

            migrationBuilder.DropTable(
                name: "CheckInObjectiveLinks");

            migrationBuilder.DropTable(
                name: "EmployeeDevelopmentObjectives");

            migrationBuilder.DropTable(
                name: "EmployeeDevelopmentPlanFeedbacks");

            migrationBuilder.DropTable(
                name: "EmployeeGoalAppraisalAssessments");

            migrationBuilder.DropTable(
                name: "EmploymentActionProposals");

            migrationBuilder.DropTable(
                name: "GoalProgressEntries");

            migrationBuilder.DropTable(
                name: "GoalRequiredSkills");

            migrationBuilder.DropTable(
                name: "GoalRiskSetting");

            migrationBuilder.DropTable(
                name: "PeerNominations");

            migrationBuilder.DropTable(
                name: "PerformanceAppraisalCriterionConfigGradeRanges");

            migrationBuilder.DropTable(
                name: "PerformanceJournalEntries");

            migrationBuilder.DropTable(
                name: "PipGoals");

            migrationBuilder.DropTable(
                name: "SalaryReviewProposals");

            migrationBuilder.DropTable(
                name: "TemplateItemGradeRanges");

            migrationBuilder.DropTable(
                name: "AppraisalAppeals");

            migrationBuilder.DropTable(
                name: "AppraisalCycleTargets");

            migrationBuilder.DropTable(
                name: "AppraisalCriterionScoreSnapshots");

            migrationBuilder.DropTable(
                name: "CalibrationSessions");

            migrationBuilder.DropTable(
                name: "CheckIns");

            migrationBuilder.DropTable(
                name: "PerformanceAppraisalCriterionConfigs");

            migrationBuilder.DropTable(
                name: "EmployeeGoals");

            migrationBuilder.DropTable(
                name: "AppraisalEvaluationSnapshots");

            migrationBuilder.DropTable(
                name: "AppraisalTemplateItems");

            migrationBuilder.DropTable(
                name: "GoalLibraries");

            migrationBuilder.DropTable(
                name: "UnitGoals");

            migrationBuilder.DropTable(
                name: "AppraisalCompetencies");

            migrationBuilder.DropTable(
                name: "AppraisalTemplateSections");

            migrationBuilder.DropTable(
                name: "CompanyGoals");

            migrationBuilder.DropTable(
                name: "AppraisalTemplates");

            migrationBuilder.DropTable(
                name: "StrategicGoals");

            migrationBuilder.DropTable(
                name: "AppraisalReviewEvents");

            migrationBuilder.DropTable(
                name: "AppraisalConversations");

            migrationBuilder.DropTable(
                name: "EmployeeDevelopmentPlans");

            migrationBuilder.DropTable(
                name: "AppraisalCycles");

            migrationBuilder.DropTable(
                name: "AppraisalSettings");

            migrationBuilder.DropIndex(
                name: "IX_Team_OrgUnit_Sequence",
                table: "Teams");

            migrationBuilder.DropIndex(
                name: "IX_Team_Tenant_Code",
                table: "Teams");

            migrationBuilder.DropIndex(
                name: "IX_Team_Tenant_Status",
                table: "Teams");

            migrationBuilder.DropIndex(
                name: "IX_Teams_ShiftDefinitionId",
                table: "Teams");

            migrationBuilder.DropIndex(
                name: "IX_TeamMember_Employee_Primary",
                table: "TeamMembers");

            migrationBuilder.DropIndex(
                name: "IX_TeamMember_Team_Active",
                table: "TeamMembers");

            migrationBuilder.DropIndex(
                name: "IX_TeamMember_Team_Employee",
                table: "TeamMembers");

            migrationBuilder.DropIndex(
                name: "IX_TeamMemberHistory_Team_Employee_EffectiveFrom",
                table: "TeamMemberHistories");

            migrationBuilder.DropIndex(
                name: "IX_PerformanceImprovementPlans_HROwnerId",
                table: "PerformanceImprovementPlans");

            migrationBuilder.DropIndex(
                name: "IX_PerformanceAppraisals_AppraisalCycleId",
                table: "PerformanceAppraisals");

            migrationBuilder.DropIndex(
                name: "IX_PerformanceAppraisals_AppraisalTemplateId",
                table: "PerformanceAppraisals");

            migrationBuilder.DropIndex(
                name: "IX_PerformanceAppraisals_CalibrationSessionId",
                table: "PerformanceAppraisals");

            migrationBuilder.DropIndex(
                name: "IX_PerformanceAppraisals_CurrentAppealStatus",
                table: "PerformanceAppraisals");

            migrationBuilder.DropIndex(
                name: "IX_PerformanceAppraisals_DevelopmentPlanId",
                table: "PerformanceAppraisals");

            migrationBuilder.DropIndex(
                name: "IX_PerformanceAppraisals_HasAppeal",
                table: "PerformanceAppraisals");

            migrationBuilder.DropIndex(
                name: "IX_PerformanceAppraisals_IsCalibrated",
                table: "PerformanceAppraisals");

            migrationBuilder.DropIndex(
                name: "IX_PerformanceAppraisals_OverallGradeDefinitionId",
                table: "PerformanceAppraisals");

            migrationBuilder.DropIndex(
                name: "IX_KpiDefinitions_IsActive",
                table: "KpiDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_AppraisalEmployeeResponses_ResponseStatus",
                table: "AppraisalEmployeeResponses");

            migrationBuilder.DropIndex(
                name: "IX_AppraisalAttachments_AppealId",
                table: "AppraisalAttachments");

            migrationBuilder.DropIndex(
                name: "IX_AppraisalAttachments_CalibrationSessionId",
                table: "AppraisalAttachments");

            migrationBuilder.DropIndex(
                name: "IX_AppraisalAttachments_CheckInId",
                table: "AppraisalAttachments");

            migrationBuilder.DropIndex(
                name: "IX_AppraisalAttachments_EmployeeGoalId",
                table: "AppraisalAttachments");

            migrationBuilder.DropIndex(
                name: "IX_AppraisalAttachments_EntityType",
                table: "AppraisalAttachments");

            migrationBuilder.DropIndex(
                name: "IX_AppraisalAttachments_PerformanceAppraisalId",
                table: "AppraisalAttachments");

            migrationBuilder.DropIndex(
                name: "IX_AppraisalAttachments_PipId",
                table: "AppraisalAttachments");

            migrationBuilder.DropIndex(
                name: "IX_AppraisalAttachments_ReviewEventId",
                table: "AppraisalAttachments");

            migrationBuilder.DropIndex(
                name: "IX_AppraisalAttachments_UnitGoalId",
                table: "AppraisalAttachments");

            migrationBuilder.DropColumn(
                name: "ShiftDefinitionId",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "HROwnerId",
                table: "PerformanceImprovementPlans");

            migrationBuilder.DropColumn(
                name: "AdjustedScore",
                table: "PerformanceAppraisals");

            migrationBuilder.DropColumn(
                name: "AppealRemandDeadline",
                table: "PerformanceAppraisals");

            migrationBuilder.DropColumn(
                name: "AppealRemandedDate",
                table: "PerformanceAppraisals");

            migrationBuilder.DropColumn(
                name: "AppraisalCycleId",
                table: "PerformanceAppraisals");

            migrationBuilder.DropColumn(
                name: "AppraisalTemplateId",
                table: "PerformanceAppraisals");

            migrationBuilder.DropColumn(
                name: "CalibrationSessionId",
                table: "PerformanceAppraisals");

            migrationBuilder.DropColumn(
                name: "CurrentAppealStatus",
                table: "PerformanceAppraisals");

            migrationBuilder.DropColumn(
                name: "DevelopmentPlanId",
                table: "PerformanceAppraisals");

            migrationBuilder.DropColumn(
                name: "EmployeeAcknowledged",
                table: "PerformanceAppraisals");

            migrationBuilder.DropColumn(
                name: "HasAppeal",
                table: "PerformanceAppraisals");

            migrationBuilder.DropColumn(
                name: "IsCalibrated",
                table: "PerformanceAppraisals");

            migrationBuilder.DropColumn(
                name: "OverallGradeDefinitionId",
                table: "PerformanceAppraisals");

            migrationBuilder.DropColumn(
                name: "PreCalibrationScore",
                table: "PerformanceAppraisals");

            migrationBuilder.DropColumn(
                name: "RankInUnit",
                table: "PerformanceAppraisals");

            migrationBuilder.DropColumn(
                name: "RecommendAward",
                table: "PerformanceAppraisals");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "KpiDefinitions");

            migrationBuilder.DropColumn(
                name: "StartedDate",
                table: "EvaluatorEvaluations");

            migrationBuilder.DropColumn(
                name: "SubmittedDate",
                table: "EvaluatorEvaluations");

            migrationBuilder.DropColumn(
                name: "TotalScore",
                table: "EvaluatorEvaluations");

            migrationBuilder.DropColumn(
                name: "ActualValue",
                table: "CriterionScores");

            migrationBuilder.DropColumn(
                name: "EvidenceLinks",
                table: "CriterionScores");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "AppraisalGradeDefinitions");

            migrationBuilder.DropColumn(
                name: "MappedRating",
                table: "AppraisalGradeDefinitions");

            migrationBuilder.DropColumn(
                name: "OverallMaxScore",
                table: "AppraisalGradeDefinitions");

            migrationBuilder.DropColumn(
                name: "OverallMinScore",
                table: "AppraisalGradeDefinitions");

            migrationBuilder.DropColumn(
                name: "ResponseStatus",
                table: "AppraisalEmployeeResponses");

            migrationBuilder.DropColumn(
                name: "SubmittedDate",
                table: "AppraisalEmployeeResponses");

            migrationBuilder.DropColumn(
                name: "AppealId",
                table: "AppraisalAttachments");

            migrationBuilder.DropColumn(
                name: "CalibrationSessionId",
                table: "AppraisalAttachments");

            migrationBuilder.DropColumn(
                name: "CheckInId",
                table: "AppraisalAttachments");

            migrationBuilder.DropColumn(
                name: "EmployeeGoalId",
                table: "AppraisalAttachments");

            migrationBuilder.DropColumn(
                name: "EntityType",
                table: "AppraisalAttachments");

            migrationBuilder.DropColumn(
                name: "FileSizeBytes",
                table: "AppraisalAttachments");

            migrationBuilder.DropColumn(
                name: "PerformanceAppraisalId",
                table: "AppraisalAttachments");

            migrationBuilder.DropColumn(
                name: "PipId",
                table: "AppraisalAttachments");

            migrationBuilder.DropColumn(
                name: "ReviewEventId",
                table: "AppraisalAttachments");

            migrationBuilder.DropColumn(
                name: "UnitGoalId",
                table: "AppraisalAttachments");

            migrationBuilder.RenameIndex(
                name: "IX_Team_TeamLeadId",
                table: "Teams",
                newName: "IX_Teams_TeamLeadId");

            migrationBuilder.RenameIndex(
                name: "IX_Team_ShiftId",
                table: "Teams",
                newName: "IX_Teams_ShiftId");

            migrationBuilder.RenameIndex(
                name: "IX_Team_ParentTeamId",
                table: "Teams",
                newName: "IX_Teams_ParentTeamId");

            migrationBuilder.RenameIndex(
                name: "IX_Team_OrganizationUnitId",
                table: "Teams",
                newName: "IX_Teams_OrganizationUnitId");

            migrationBuilder.RenameIndex(
                name: "IX_Team_LocationId",
                table: "Teams",
                newName: "IX_Teams_LocationId");

            migrationBuilder.RenameIndex(
                name: "IX_TeamMember_TeamId",
                table: "TeamMembers",
                newName: "IX_TeamMembers_TeamId");

            migrationBuilder.RenameIndex(
                name: "IX_TeamMember_EmployeeId",
                table: "TeamMembers",
                newName: "IX_TeamMembers_EmployeeId");

            migrationBuilder.RenameIndex(
                name: "IX_TeamMemberHistory_TeamId",
                table: "TeamMemberHistories",
                newName: "IX_TeamMemberHistories_TeamId");

            migrationBuilder.RenameIndex(
                name: "IX_TeamMemberHistory_EmployeeId",
                table: "TeamMemberHistories",
                newName: "IX_TeamMemberHistories_EmployeeId");

            migrationBuilder.RenameColumn(
                name: "RecommendPIP",
                table: "PerformanceAppraisals",
                newName: "AppealFiled");

            migrationBuilder.RenameColumn(
                name: "PeerEvaluatorsCount",
                table: "PerformanceAppraisals",
                newName: "AppraisalType");

            migrationBuilder.RenameColumn(
                name: "EmployeeAcknowledgmentComments",
                table: "PerformanceAppraisals",
                newName: "AppealReason");

            migrationBuilder.RenameColumn(
                name: "EmployeeAcknowledgedDate",
                table: "PerformanceAppraisals",
                newName: "AppealDate");

            migrationBuilder.RenameColumn(
                name: "TemplateItemId",
                table: "CriterionScores",
                newName: "CriteriaId");

            migrationBuilder.RenameColumn(
                name: "GradeDefinitionId",
                table: "CriterionScores",
                newName: "KpiEvaluationRecordId");

            migrationBuilder.RenameIndex(
                name: "IX_CriterionScores_TemplateItemId",
                table: "CriterionScores",
                newName: "IX_CriterionScores_CriteriaId");

            migrationBuilder.RenameIndex(
                name: "IX_CriterionScores_GradeDefinitionId",
                table: "CriterionScores",
                newName: "IX_CriterionScores_KpiEvaluationRecordId");

            migrationBuilder.RenameColumn(
                name: "TemplateItemId",
                table: "AppraisalEmployeeResponses",
                newName: "CriteriaId");

            migrationBuilder.RenameIndex(
                name: "IX_AppraisalEmployeeResponses_TemplateItemId",
                table: "AppraisalEmployeeResponses",
                newName: "IX_AppraisalEmployeeResponses_CriteriaId");

            migrationBuilder.RenameColumn(
                name: "UploadedById",
                table: "AppraisalAttachments",
                newName: "AppraisalId");

            migrationBuilder.RenameIndex(
                name: "IX_AppraisalAttachments_UploadedById",
                table: "AppraisalAttachments",
                newName: "IX_AppraisalAttachments_AppraisalId");

            migrationBuilder.AlterColumn<string>(
                name: "SupportProvided",
                table: "PerformanceImprovementPlans",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "MeasurementCriteria",
                table: "PerformanceImprovementPlans",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AppealOutcome",
                table: "PerformanceAppraisals",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EvaluationDate",
                table: "EvaluatorEvaluations",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AlterColumn<string>(
                name: "ResponseText",
                table: "AppraisalEmployeeResponses",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(4000)",
                oldMaxLength: 4000,
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "AppraisalCriterias",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    KpiDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CriteriaName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CriteriaType = table.Column<int>(type: "int", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppraisalCriterias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppraisalCriterias_KpiDefinitions_KpiDefinitionId",
                        column: x => x.KpiDefinitionId,
                        principalTable: "KpiDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppraisalCriterias_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PositionCriteriaMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CriteriaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PositionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    KpiMaxValue = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    KpiMinValue = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    KpiTargetValue = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Weight = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PositionCriteriaMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PositionCriteriaMappings_AppraisalCriterias_CriteriaId",
                        column: x => x.CriteriaId,
                        principalTable: "AppraisalCriterias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PositionCriteriaMappings_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PositionCriteriaMappings_EmployeePositions_PositionId",
                        column: x => x.PositionId,
                        principalTable: "EmployeePositions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PositionCriteriaMappings_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeKpiTargets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    KpiDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PositionCriteriaMappingId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MaxValue = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    MinValue = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    PeriodEnd = table.Column<DateOnly>(type: "date", nullable: false),
                    PeriodStart = table.Column<DateOnly>(type: "date", nullable: false),
                    TargetValue = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WeightOverride = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeKpiTargets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeKpiTargets_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeKpiTargets_KpiDefinitions_KpiDefinitionId",
                        column: x => x.KpiDefinitionId,
                        principalTable: "KpiDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeKpiTargets_PositionCriteriaMappings_PositionCriteriaMappingId",
                        column: x => x.PositionCriteriaMappingId,
                        principalTable: "PositionCriteriaMappings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeKpiTargets_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MappingGradeRanges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GradeDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PositionCriteriaMappingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HighScore = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LowScore = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MappingGradeRanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MappingGradeRanges_AppraisalGradeDefinitions_GradeDefinitionId",
                        column: x => x.GradeDefinitionId,
                        principalTable: "AppraisalGradeDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MappingGradeRanges_PositionCriteriaMappings_PositionCriteriaMappingId",
                        column: x => x.PositionCriteriaMappingId,
                        principalTable: "PositionCriteriaMappings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MappingGradeRanges_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "KpiEvaluationRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeKpiTargetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EvaluatorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AchievementPercent = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ActualValue = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EvaluationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EvidenceLinks = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    IsFinal = table.Column<bool>(type: "bit", nullable: false),
                    IsSelfEvaluation = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KpiEvaluationRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KpiEvaluationRecords_EmployeeKpiTargets_EmployeeKpiTargetId",
                        column: x => x.EmployeeKpiTargetId,
                        principalTable: "EmployeeKpiTargets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KpiEvaluationRecords_Employees_EvaluatorId",
                        column: x => x.EvaluatorId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KpiEvaluationRecords_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Teams_TenantId",
                table: "Teams",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceAppraisals_AppraisalType",
                table: "PerformanceAppraisals",
                column: "AppraisalType");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalAttachments_FileName",
                table: "AppraisalAttachments",
                column: "FileName");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalCriterias_Code",
                table: "AppraisalCriterias",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalCriterias_CriteriaName",
                table: "AppraisalCriterias",
                column: "CriteriaName");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalCriterias_CriteriaType",
                table: "AppraisalCriterias",
                column: "CriteriaType");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalCriterias_KpiDefinitionId",
                table: "AppraisalCriterias",
                column: "KpiDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalCriterias_TenantId",
                table: "AppraisalCriterias",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeKpiTargets_EmployeeId",
                table: "EmployeeKpiTargets",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeKpiTargets_EmployeeId_KpiDefinitionId_PeriodStart_PeriodEnd",
                table: "EmployeeKpiTargets",
                columns: new[] { "EmployeeId", "KpiDefinitionId", "PeriodStart", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeKpiTargets_KpiDefinitionId",
                table: "EmployeeKpiTargets",
                column: "KpiDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeKpiTargets_PositionCriteriaMappingId",
                table: "EmployeeKpiTargets",
                column: "PositionCriteriaMappingId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeKpiTargets_TenantId",
                table: "EmployeeKpiTargets",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_KpiEvaluationRecords_EmployeeKpiTargetId",
                table: "KpiEvaluationRecords",
                column: "EmployeeKpiTargetId");

            migrationBuilder.CreateIndex(
                name: "IX_KpiEvaluationRecords_EvaluationDate",
                table: "KpiEvaluationRecords",
                column: "EvaluationDate");

            migrationBuilder.CreateIndex(
                name: "IX_KpiEvaluationRecords_EvaluatorId",
                table: "KpiEvaluationRecords",
                column: "EvaluatorId");

            migrationBuilder.CreateIndex(
                name: "IX_KpiEvaluationRecords_IsSelfEvaluation",
                table: "KpiEvaluationRecords",
                column: "IsSelfEvaluation");

            migrationBuilder.CreateIndex(
                name: "IX_KpiEvaluationRecords_TenantId",
                table: "KpiEvaluationRecords",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_MappingGradeRanges_GradeDefinitionId",
                table: "MappingGradeRanges",
                column: "GradeDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_MappingGradeRanges_PositionCriteriaMappingId",
                table: "MappingGradeRanges",
                column: "PositionCriteriaMappingId");

            migrationBuilder.CreateIndex(
                name: "IX_MappingGradeRanges_TenantId",
                table: "MappingGradeRanges",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PositionCriteriaMappings_CriteriaId_PositionId",
                table: "PositionCriteriaMappings",
                columns: new[] { "CriteriaId", "PositionId" });

            migrationBuilder.CreateIndex(
                name: "IX_PositionCriteriaMappings_DepartmentId",
                table: "PositionCriteriaMappings",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_PositionCriteriaMappings_PositionId",
                table: "PositionCriteriaMappings",
                column: "PositionId");

            migrationBuilder.CreateIndex(
                name: "IX_PositionCriteriaMappings_TenantId",
                table: "PositionCriteriaMappings",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_AppraisalAttachments_PerformanceAppraisals_AppraisalId",
                table: "AppraisalAttachments",
                column: "AppraisalId",
                principalTable: "PerformanceAppraisals",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AppraisalEmployeeResponses_AppraisalCriterias_CriteriaId",
                table: "AppraisalEmployeeResponses",
                column: "CriteriaId",
                principalTable: "AppraisalCriterias",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CriterionScores_AppraisalCriterias_CriteriaId",
                table: "CriterionScores",
                column: "CriteriaId",
                principalTable: "AppraisalCriterias",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CriterionScores_KpiEvaluationRecords_KpiEvaluationRecordId",
                table: "CriterionScores",
                column: "KpiEvaluationRecordId",
                principalTable: "KpiEvaluationRecords",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TeamMemberHistories_Employees_EmployeeId",
                table: "TeamMemberHistories",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TeamMemberHistories_Teams_TeamId",
                table: "TeamMemberHistories",
                column: "TeamId",
                principalTable: "Teams",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TeamMembers_Employees_EmployeeId",
                table: "TeamMembers",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TeamMembers_Teams_TeamId",
                table: "TeamMembers",
                column: "TeamId",
                principalTable: "Teams",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Teams_Employees_TeamLeadId",
                table: "Teams",
                column: "TeamLeadId",
                principalTable: "Employees",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Teams_Locations_LocationId",
                table: "Teams",
                column: "LocationId",
                principalTable: "Locations",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Teams_ShiftDefinitions_ShiftId",
                table: "Teams",
                column: "ShiftId",
                principalTable: "ShiftDefinitions",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Teams_Teams_ParentTeamId",
                table: "Teams",
                column: "ParentTeamId",
                principalTable: "Teams",
                principalColumn: "Id");
        }
    }
}
