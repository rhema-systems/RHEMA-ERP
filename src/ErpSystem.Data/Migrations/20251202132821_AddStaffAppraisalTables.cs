using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffAppraisalTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("04e977ad-ad4a-4229-b055-e5f933f20666"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("5b39c4d0-5a73-4ba6-9d76-1382dc303f82"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("78303f76-2605-4f36-a709-cee88ab80cdf"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("99b3536f-ac7f-436f-af1e-165a823fadfc"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("e487ac78-eb13-4f8a-8674-54a0a1b89309"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("f063d770-90d2-4094-a925-777f2cd8033a"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("fd613dca-3987-4696-8ec4-db006a3dc630"));

            migrationBuilder.CreateTable(
                name: "AppraisalGradeDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GradeName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_AppraisalGradeDefinitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppraisalGradeDefinitions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "KpiDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    KpiName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    MeasurementType = table.Column<int>(type: "int", nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    TolerancePercent = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
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
                    table.PrimaryKey("PK_KpiDefinitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KpiDefinitions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PerformanceAppraisals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppraisalNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    AppraisalType = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    OverallScore = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    RankInPosition = table.Column<int>(type: "int", nullable: true),
                    OverallComments = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    StrengthsIdentified = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    AreasForImprovement = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    TrainingNeeds = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CareerAspirations = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RecommendPromotion = table.Column<bool>(type: "bit", nullable: false),
                    RecommendIncrement = table.Column<bool>(type: "bit", nullable: false),
                    RecommendTraining = table.Column<bool>(type: "bit", nullable: false),
                    RecommendTermination = table.Column<bool>(type: "bit", nullable: false),
                    RecommendationNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    NextAppraisalDate = table.Column<DateOnly>(type: "date", nullable: true),
                    AppealFiled = table.Column<bool>(type: "bit", nullable: false),
                    AppealDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AppealReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    AppealOutcome = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_PerformanceAppraisals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PerformanceAppraisals_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PerformanceAppraisals_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppraisalCriterias",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CriteriaName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CriteriaType = table.Column<int>(type: "int", nullable: false),
                    KpiDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                name: "AppraisalAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    UploadDate = table.Column<DateTime>(type: "datetime2", nullable: false),
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
                    table.PrimaryKey("PK_AppraisalAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppraisalAttachments_PerformanceAppraisals_AppraisalId",
                        column: x => x.AppraisalId,
                        principalTable: "PerformanceAppraisals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppraisalAttachments_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EvaluatorEvaluations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EvaluatorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EvaluatorRole = table.Column<int>(type: "int", nullable: false),
                    EvaluatorWeight = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    IsAuthoritative = table.Column<bool>(type: "bit", nullable: false),
                    EvaluationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OverallNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Recommendation = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_EvaluatorEvaluations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EvaluatorEvaluations_Employees_EvaluatorId",
                        column: x => x.EvaluatorId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EvaluatorEvaluations_PerformanceAppraisals_AppraisalId",
                        column: x => x.AppraisalId,
                        principalTable: "PerformanceAppraisals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EvaluatorEvaluations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PerformanceImprovementPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PipNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PerformanceIssues = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ExpectedStandards = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ImprovementActions = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    SupportProvided = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    MeasurementCriteria = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    SupervisorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewSchedule = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CompletionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Outcome = table.Column<int>(type: "int", nullable: true),
                    OutcomeNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_PerformanceImprovementPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PerformanceImprovementPlans_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PerformanceImprovementPlans_Employees_SupervisorId",
                        column: x => x.SupervisorId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PerformanceImprovementPlans_PerformanceAppraisals_AppraisalId",
                        column: x => x.AppraisalId,
                        principalTable: "PerformanceAppraisals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PerformanceImprovementPlans_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppraisalEmployeeResponses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CriteriaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ResponseText = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ResponseDate = table.Column<DateTime>(type: "datetime2", nullable: false),
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
                    table.PrimaryKey("PK_AppraisalEmployeeResponses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppraisalEmployeeResponses_AppraisalCriterias_CriteriaId",
                        column: x => x.CriteriaId,
                        principalTable: "AppraisalCriterias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppraisalEmployeeResponses_PerformanceAppraisals_AppraisalId",
                        column: x => x.AppraisalId,
                        principalTable: "PerformanceAppraisals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppraisalEmployeeResponses_Tenants_TenantId",
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
                    DepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PositionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CriteriaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Weight = table.Column<int>(type: "int", nullable: false),
                    KpiTargetValue = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    KpiMinValue = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    KpiMaxValue = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
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
                name: "PipReviewMeetings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PipId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MeetingDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EmployeeAttended = table.Column<bool>(type: "bit", nullable: false),
                    ProgressNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    IssuesDiscussed = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ActionsAgreed = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    EmployeeComments = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ConductedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_PipReviewMeetings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PipReviewMeetings_Employees_ConductedById",
                        column: x => x.ConductedById,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PipReviewMeetings_PerformanceImprovementPlans_PipId",
                        column: x => x.PipId,
                        principalTable: "PerformanceImprovementPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PipReviewMeetings_Tenants_TenantId",
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
                    TargetValue = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    MinValue = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    MaxValue = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    PeriodStart = table.Column<DateOnly>(type: "date", nullable: false),
                    PeriodEnd = table.Column<DateOnly>(type: "date", nullable: false),
                    WeightOverride = table.Column<int>(type: "int", nullable: true),
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
                    PositionCriteriaMappingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    IsSelfEvaluation = table.Column<bool>(type: "bit", nullable: false),
                    IsFinal = table.Column<bool>(type: "bit", nullable: false),
                    ActualValue = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    AchievementPercent = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    EvaluationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    EvidenceLinks = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
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

            migrationBuilder.CreateTable(
                name: "CriterionScores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EvaluatorEvaluationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CriteriaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NumericScore = table.Column<int>(type: "int", nullable: true),
                    KpiEvaluationRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WeightedScore = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
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
                    table.PrimaryKey("PK_CriterionScores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CriterionScores_AppraisalCriterias_CriteriaId",
                        column: x => x.CriteriaId,
                        principalTable: "AppraisalCriterias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CriterionScores_EvaluatorEvaluations_EvaluatorEvaluationId",
                        column: x => x.EvaluatorEvaluationId,
                        principalTable: "EvaluatorEvaluations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CriterionScores_KpiEvaluationRecords_KpiEvaluationRecordId",
                        column: x => x.KpiEvaluationRecordId,
                        principalTable: "KpiEvaluationRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CriterionScores_Tenants_TenantId",
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
                value: new DateTime(2025, 12, 2, 13, 28, 18, 310, DateTimeKind.Utc).AddTicks(2981));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 12, 2, 13, 28, 18, 310, DateTimeKind.Utc).AddTicks(3054));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 12, 2, 13, 28, 18, 310, DateTimeKind.Utc).AddTicks(3059));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 12, 2, 13, 28, 18, 310, DateTimeKind.Utc).AddTicks(3062));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 12, 2, 13, 28, 18, 310, DateTimeKind.Utc).AddTicks(3321));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 12, 2, 13, 28, 18, 310, DateTimeKind.Utc).AddTicks(3349));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 12, 2, 13, 28, 18, 310, DateTimeKind.Utc).AddTicks(3357));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 12, 2, 13, 28, 18, 310, DateTimeKind.Utc).AddTicks(3363));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000005"),
                column: "CreatedAt",
                value: new DateTime(2025, 12, 2, 13, 28, 18, 310, DateTimeKind.Utc).AddTicks(3373));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000006"),
                column: "CreatedAt",
                value: new DateTime(2025, 12, 2, 13, 28, 18, 310, DateTimeKind.Utc).AddTicks(3379));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000007"),
                column: "CreatedAt",
                value: new DateTime(2025, 12, 2, 13, 28, 18, 310, DateTimeKind.Utc).AddTicks(3384));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000008"),
                column: "CreatedAt",
                value: new DateTime(2025, 12, 2, 13, 28, 18, 310, DateTimeKind.Utc).AddTicks(3388));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000009"),
                column: "CreatedAt",
                value: new DateTime(2025, 12, 2, 13, 28, 18, 310, DateTimeKind.Utc).AddTicks(3397));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000010"),
                column: "CreatedAt",
                value: new DateTime(2025, 12, 2, 13, 28, 18, 310, DateTimeKind.Utc).AddTicks(3404));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000011"),
                column: "CreatedAt",
                value: new DateTime(2025, 12, 2, 13, 28, 18, 310, DateTimeKind.Utc).AddTicks(3416));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000012"),
                column: "CreatedAt",
                value: new DateTime(2025, 12, 2, 13, 28, 18, 310, DateTimeKind.Utc).AddTicks(3420));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000013"),
                column: "CreatedAt",
                value: new DateTime(2025, 12, 2, 13, 28, 18, 310, DateTimeKind.Utc).AddTicks(3431));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000014"),
                column: "CreatedAt",
                value: new DateTime(2025, 12, 2, 13, 28, 18, 310, DateTimeKind.Utc).AddTicks(3436));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000015"),
                column: "CreatedAt",
                value: new DateTime(2025, 12, 2, 13, 28, 18, 310, DateTimeKind.Utc).AddTicks(3440));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000016"),
                column: "CreatedAt",
                value: new DateTime(2025, 12, 2, 13, 28, 18, 310, DateTimeKind.Utc).AddTicks(3444));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 2, 13, 28, 18, 310, DateTimeKind.Utc).AddTicks(3494));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 2, 13, 28, 18, 310, DateTimeKind.Utc).AddTicks(3496));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 2, 13, 28, 18, 310, DateTimeKind.Utc).AddTicks(3497));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 2, 13, 28, 18, 310, DateTimeKind.Utc).AddTicks(3498));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 2, 13, 28, 18, 310, DateTimeKind.Utc).AddTicks(3498));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 2, 13, 28, 18, 310, DateTimeKind.Utc).AddTicks(3500));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 2, 13, 28, 18, 310, DateTimeKind.Utc).AddTicks(3501));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 2, 13, 28, 18, 310, DateTimeKind.Utc).AddTicks(3501));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 2, 13, 28, 18, 310, DateTimeKind.Utc).AddTicks(3502));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 2, 13, 28, 18, 310, DateTimeKind.Utc).AddTicks(3503));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 2, 13, 28, 18, 310, DateTimeKind.Utc).AddTicks(3504));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 2, 13, 28, 18, 310, DateTimeKind.Utc).AddTicks(3505));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 2, 13, 28, 18, 310, DateTimeKind.Utc).AddTicks(3506));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 2, 13, 28, 18, 310, DateTimeKind.Utc).AddTicks(3507));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 2, 13, 28, 18, 310, DateTimeKind.Utc).AddTicks(3508));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 2, 13, 28, 18, 310, DateTimeKind.Utc).AddTicks(3508));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 2, 13, 28, 18, 310, DateTimeKind.Utc).AddTicks(3564));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 2, 13, 28, 18, 310, DateTimeKind.Utc).AddTicks(3581));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 2, 13, 28, 18, 310, DateTimeKind.Utc).AddTicks(3582));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 2, 13, 28, 18, 310, DateTimeKind.Utc).AddTicks(3582));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 2, 13, 28, 18, 310, DateTimeKind.Utc).AddTicks(3583));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 2, 13, 28, 18, 310, DateTimeKind.Utc).AddTicks(3584));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 2, 13, 28, 18, 310, DateTimeKind.Utc).AddTicks(3585));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 2, 13, 28, 18, 310, DateTimeKind.Utc).AddTicks(3586));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 2, 13, 28, 18, 310, DateTimeKind.Utc).AddTicks(3587));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 2, 13, 28, 18, 310, DateTimeKind.Utc).AddTicks(3587));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 2, 13, 28, 18, 310, DateTimeKind.Utc).AddTicks(3588));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 2, 13, 28, 18, 310, DateTimeKind.Utc).AddTicks(3589));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 2, 13, 28, 18, 310, DateTimeKind.Utc).AddTicks(3590));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 2, 13, 28, 18, 310, DateTimeKind.Utc).AddTicks(3591));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 2, 13, 28, 18, 310, DateTimeKind.Utc).AddTicks(3591));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 2, 13, 28, 18, 310, DateTimeKind.Utc).AddTicks(3673));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 2, 13, 28, 18, 310, DateTimeKind.Utc).AddTicks(3674));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 2, 13, 28, 18, 310, DateTimeKind.Utc).AddTicks(3675));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 2, 13, 28, 18, 310, DateTimeKind.Utc).AddTicks(3676));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 2, 13, 28, 18, 310, DateTimeKind.Utc).AddTicks(3677));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 2, 13, 28, 18, 310, DateTimeKind.Utc).AddTicks(3678));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 2, 13, 28, 18, 310, DateTimeKind.Utc).AddTicks(3679));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 2, 13, 28, 18, 310, DateTimeKind.Utc).AddTicks(3680));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 2, 13, 28, 18, 310, DateTimeKind.Utc).AddTicks(3680));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 2, 13, 28, 18, 310, DateTimeKind.Utc).AddTicks(3681));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 2, 13, 28, 18, 310, DateTimeKind.Utc).AddTicks(3693));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 2, 13, 28, 18, 310, DateTimeKind.Utc).AddTicks(3694));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 2, 13, 28, 18, 310, DateTimeKind.Utc).AddTicks(3695));

            migrationBuilder.InsertData(
                table: "TenantModules",
                columns: new[] { "Id", "Configuration", "CreatedAt", "CreatedBy", "CreatedById", "DeletedAt", "DeletedBy", "Description", "DisabledDate", "EnabledDate", "IsDeleted", "LastModifiedById", "ModuleName", "Status", "TenantId", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid("1503b4db-19fd-4d6e-a42b-3bdb5970a45c"), null, new DateTime(2025, 12, 2, 13, 28, 18, 310, DateTimeKind.Utc).AddTicks(3180), null, null, null, null, null, null, new DateTime(2025, 12, 2, 13, 28, 18, 310, DateTimeKind.Utc).AddTicks(3179), false, null, "Procurement", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("1b256d2a-136d-4c5a-bde2-c0d343115f82"), null, new DateTime(2025, 12, 2, 13, 28, 18, 310, DateTimeKind.Utc).AddTicks(3192), null, null, null, null, null, null, new DateTime(2025, 12, 2, 13, 28, 18, 310, DateTimeKind.Utc).AddTicks(3191), false, null, "Inventory", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("3dbf57a9-547c-42df-9dff-a964049c9817"), null, new DateTime(2025, 12, 2, 13, 28, 18, 310, DateTimeKind.Utc).AddTicks(3137), null, null, null, null, null, null, new DateTime(2025, 12, 2, 13, 28, 18, 310, DateTimeKind.Utc).AddTicks(3129), false, null, "Finance", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("6d663272-b6c8-481d-8bd2-348ae571e48b"), null, new DateTime(2025, 12, 2, 13, 28, 18, 310, DateTimeKind.Utc).AddTicks(3218), null, null, null, null, null, null, new DateTime(2025, 12, 2, 13, 28, 18, 310, DateTimeKind.Utc).AddTicks(3217), false, null, "WorkflowEngine", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("8674636d-e613-43d0-92ae-8b2706ab33c8"), null, new DateTime(2025, 12, 2, 13, 28, 18, 310, DateTimeKind.Utc).AddTicks(3153), null, null, null, null, null, null, new DateTime(2025, 12, 2, 13, 28, 18, 310, DateTimeKind.Utc).AddTicks(3152), false, null, "HR", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("929e6ff2-82b6-4aef-9747-99ae65670b33"), null, new DateTime(2025, 12, 2, 13, 28, 18, 310, DateTimeKind.Utc).AddTicks(3167), null, null, null, null, null, null, new DateTime(2025, 12, 2, 13, 28, 18, 310, DateTimeKind.Utc).AddTicks(3166), false, null, "Sales", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("fbfc2cb4-cd58-486f-acba-405f2daf1c61"), null, new DateTime(2025, 12, 2, 13, 28, 18, 310, DateTimeKind.Utc).AddTicks(3205), null, null, null, null, null, null, new DateTime(2025, 12, 2, 13, 28, 18, 310, DateTimeKind.Utc).AddTicks(3205), false, null, "Marketing", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null }
                });

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 12, 2, 13, 28, 18, 310, DateTimeKind.Utc).AddTicks(2638));

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalAttachments_AppraisalId",
                table: "AppraisalAttachments",
                column: "AppraisalId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalAttachments_FileName",
                table: "AppraisalAttachments",
                column: "FileName");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalAttachments_TenantId",
                table: "AppraisalAttachments",
                column: "TenantId");

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
                name: "IX_AppraisalEmployeeResponses_AppraisalId",
                table: "AppraisalEmployeeResponses",
                column: "AppraisalId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalEmployeeResponses_CriteriaId",
                table: "AppraisalEmployeeResponses",
                column: "CriteriaId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalEmployeeResponses_TenantId",
                table: "AppraisalEmployeeResponses",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalGradeDefinitions_GradeName",
                table: "AppraisalGradeDefinitions",
                column: "GradeName");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalGradeDefinitions_TenantId",
                table: "AppraisalGradeDefinitions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CriterionScores_CriteriaId",
                table: "CriterionScores",
                column: "CriteriaId");

            migrationBuilder.CreateIndex(
                name: "IX_CriterionScores_EvaluatorEvaluationId",
                table: "CriterionScores",
                column: "EvaluatorEvaluationId");

            migrationBuilder.CreateIndex(
                name: "IX_CriterionScores_KpiEvaluationRecordId",
                table: "CriterionScores",
                column: "KpiEvaluationRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_CriterionScores_TenantId",
                table: "CriterionScores",
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
                name: "IX_EvaluatorEvaluations_AppraisalId",
                table: "EvaluatorEvaluations",
                column: "AppraisalId");

            migrationBuilder.CreateIndex(
                name: "IX_EvaluatorEvaluations_AppraisalId_EvaluatorId",
                table: "EvaluatorEvaluations",
                columns: new[] { "AppraisalId", "EvaluatorId" });

            migrationBuilder.CreateIndex(
                name: "IX_EvaluatorEvaluations_EvaluatorId",
                table: "EvaluatorEvaluations",
                column: "EvaluatorId");

            migrationBuilder.CreateIndex(
                name: "IX_EvaluatorEvaluations_TenantId",
                table: "EvaluatorEvaluations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_KpiDefinitions_KpiName",
                table: "KpiDefinitions",
                column: "KpiName");

            migrationBuilder.CreateIndex(
                name: "IX_KpiDefinitions_MeasurementType",
                table: "KpiDefinitions",
                column: "MeasurementType");

            migrationBuilder.CreateIndex(
                name: "IX_KpiDefinitions_TenantId",
                table: "KpiDefinitions",
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
                name: "IX_PerformanceAppraisals_AppraisalNumber",
                table: "PerformanceAppraisals",
                column: "AppraisalNumber");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceAppraisals_AppraisalType",
                table: "PerformanceAppraisals",
                column: "AppraisalType");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceAppraisals_EmployeeId",
                table: "PerformanceAppraisals",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceAppraisals_Status",
                table: "PerformanceAppraisals",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceAppraisals_TenantId",
                table: "PerformanceAppraisals",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceAppraisals_Year",
                table: "PerformanceAppraisals",
                column: "Year");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceImprovementPlans_AppraisalId",
                table: "PerformanceImprovementPlans",
                column: "AppraisalId");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceImprovementPlans_EmployeeId",
                table: "PerformanceImprovementPlans",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceImprovementPlans_PipNumber",
                table: "PerformanceImprovementPlans",
                column: "PipNumber");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceImprovementPlans_Status",
                table: "PerformanceImprovementPlans",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceImprovementPlans_SupervisorId",
                table: "PerformanceImprovementPlans",
                column: "SupervisorId");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceImprovementPlans_TenantId",
                table: "PerformanceImprovementPlans",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PipReviewMeetings_ConductedById",
                table: "PipReviewMeetings",
                column: "ConductedById");

            migrationBuilder.CreateIndex(
                name: "IX_PipReviewMeetings_MeetingDate",
                table: "PipReviewMeetings",
                column: "MeetingDate");

            migrationBuilder.CreateIndex(
                name: "IX_PipReviewMeetings_PipId",
                table: "PipReviewMeetings",
                column: "PipId");

            migrationBuilder.CreateIndex(
                name: "IX_PipReviewMeetings_TenantId",
                table: "PipReviewMeetings",
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppraisalAttachments");

            migrationBuilder.DropTable(
                name: "AppraisalEmployeeResponses");

            migrationBuilder.DropTable(
                name: "CriterionScores");

            migrationBuilder.DropTable(
                name: "MappingGradeRanges");

            migrationBuilder.DropTable(
                name: "PipReviewMeetings");

            migrationBuilder.DropTable(
                name: "EvaluatorEvaluations");

            migrationBuilder.DropTable(
                name: "KpiEvaluationRecords");

            migrationBuilder.DropTable(
                name: "AppraisalGradeDefinitions");

            migrationBuilder.DropTable(
                name: "PerformanceImprovementPlans");

            migrationBuilder.DropTable(
                name: "EmployeeKpiTargets");

            migrationBuilder.DropTable(
                name: "PerformanceAppraisals");

            migrationBuilder.DropTable(
                name: "PositionCriteriaMappings");

            migrationBuilder.DropTable(
                name: "AppraisalCriterias");

            migrationBuilder.DropTable(
                name: "KpiDefinitions");

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("1503b4db-19fd-4d6e-a42b-3bdb5970a45c"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("1b256d2a-136d-4c5a-bde2-c0d343115f82"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("3dbf57a9-547c-42df-9dff-a964049c9817"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("6d663272-b6c8-481d-8bd2-348ae571e48b"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("8674636d-e613-43d0-92ae-8b2706ab33c8"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("929e6ff2-82b6-4aef-9747-99ae65670b33"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("fbfc2cb4-cd58-486f-acba-405f2daf1c61"));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 17, 15, 52, 8, 190, DateTimeKind.Utc).AddTicks(6087));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 17, 15, 52, 8, 190, DateTimeKind.Utc).AddTicks(6150));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 17, 15, 52, 8, 190, DateTimeKind.Utc).AddTicks(6152));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 17, 15, 52, 8, 190, DateTimeKind.Utc).AddTicks(6153));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 17, 15, 52, 8, 190, DateTimeKind.Utc).AddTicks(6437));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 17, 15, 52, 8, 190, DateTimeKind.Utc).AddTicks(6459));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 17, 15, 52, 8, 190, DateTimeKind.Utc).AddTicks(6465));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 17, 15, 52, 8, 190, DateTimeKind.Utc).AddTicks(6470));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000005"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 17, 15, 52, 8, 190, DateTimeKind.Utc).AddTicks(6477));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000006"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 17, 15, 52, 8, 190, DateTimeKind.Utc).AddTicks(6482));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000007"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 17, 15, 52, 8, 190, DateTimeKind.Utc).AddTicks(6487));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000008"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 17, 15, 52, 8, 190, DateTimeKind.Utc).AddTicks(6492));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000009"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 17, 15, 52, 8, 190, DateTimeKind.Utc).AddTicks(6499));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000010"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 17, 15, 52, 8, 190, DateTimeKind.Utc).AddTicks(6507));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000011"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 17, 15, 52, 8, 190, DateTimeKind.Utc).AddTicks(6519));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000012"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 17, 15, 52, 8, 190, DateTimeKind.Utc).AddTicks(6524));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000013"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 17, 15, 52, 8, 190, DateTimeKind.Utc).AddTicks(6533));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000014"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 17, 15, 52, 8, 190, DateTimeKind.Utc).AddTicks(6537));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000015"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 17, 15, 52, 8, 190, DateTimeKind.Utc).AddTicks(6558));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000016"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 17, 15, 52, 8, 190, DateTimeKind.Utc).AddTicks(6562));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 15, 52, 8, 190, DateTimeKind.Utc).AddTicks(6616));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 15, 52, 8, 190, DateTimeKind.Utc).AddTicks(6620));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 15, 52, 8, 190, DateTimeKind.Utc).AddTicks(6621));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 15, 52, 8, 190, DateTimeKind.Utc).AddTicks(6621));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 15, 52, 8, 190, DateTimeKind.Utc).AddTicks(6622));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 15, 52, 8, 190, DateTimeKind.Utc).AddTicks(6623));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 15, 52, 8, 190, DateTimeKind.Utc).AddTicks(6624));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 15, 52, 8, 190, DateTimeKind.Utc).AddTicks(6624));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 15, 52, 8, 190, DateTimeKind.Utc).AddTicks(6625));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 15, 52, 8, 190, DateTimeKind.Utc).AddTicks(6626));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 15, 52, 8, 190, DateTimeKind.Utc).AddTicks(6627));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 15, 52, 8, 190, DateTimeKind.Utc).AddTicks(6627));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 15, 52, 8, 190, DateTimeKind.Utc).AddTicks(6628));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 15, 52, 8, 190, DateTimeKind.Utc).AddTicks(6628));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 15, 52, 8, 190, DateTimeKind.Utc).AddTicks(6629));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 15, 52, 8, 190, DateTimeKind.Utc).AddTicks(6630));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 15, 52, 8, 190, DateTimeKind.Utc).AddTicks(6797));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 15, 52, 8, 190, DateTimeKind.Utc).AddTicks(6798));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 15, 52, 8, 190, DateTimeKind.Utc).AddTicks(6799));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 15, 52, 8, 190, DateTimeKind.Utc).AddTicks(6800));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 15, 52, 8, 190, DateTimeKind.Utc).AddTicks(6800));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 15, 52, 8, 190, DateTimeKind.Utc).AddTicks(6801));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 15, 52, 8, 190, DateTimeKind.Utc).AddTicks(6801));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 15, 52, 8, 190, DateTimeKind.Utc).AddTicks(6802));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 15, 52, 8, 190, DateTimeKind.Utc).AddTicks(6803));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 15, 52, 8, 190, DateTimeKind.Utc).AddTicks(6803));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 15, 52, 8, 190, DateTimeKind.Utc).AddTicks(6804));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 15, 52, 8, 190, DateTimeKind.Utc).AddTicks(6804));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 15, 52, 8, 190, DateTimeKind.Utc).AddTicks(6805));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 15, 52, 8, 190, DateTimeKind.Utc).AddTicks(6806));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 15, 52, 8, 190, DateTimeKind.Utc).AddTicks(6806));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 15, 52, 8, 190, DateTimeKind.Utc).AddTicks(6965));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 15, 52, 8, 190, DateTimeKind.Utc).AddTicks(6966));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 15, 52, 8, 190, DateTimeKind.Utc).AddTicks(6967));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 15, 52, 8, 190, DateTimeKind.Utc).AddTicks(6968));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 15, 52, 8, 190, DateTimeKind.Utc).AddTicks(6969));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 15, 52, 8, 190, DateTimeKind.Utc).AddTicks(6970));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 15, 52, 8, 190, DateTimeKind.Utc).AddTicks(6970));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 15, 52, 8, 190, DateTimeKind.Utc).AddTicks(6971));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 15, 52, 8, 190, DateTimeKind.Utc).AddTicks(6971));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 15, 52, 8, 190, DateTimeKind.Utc).AddTicks(6973));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 15, 52, 8, 190, DateTimeKind.Utc).AddTicks(6984));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 15, 52, 8, 190, DateTimeKind.Utc).AddTicks(6985));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 15, 52, 8, 190, DateTimeKind.Utc).AddTicks(6985));

            migrationBuilder.InsertData(
                table: "TenantModules",
                columns: new[] { "Id", "Configuration", "CreatedAt", "CreatedBy", "CreatedById", "DeletedAt", "DeletedBy", "Description", "DisabledDate", "EnabledDate", "IsDeleted", "LastModifiedById", "ModuleName", "Status", "TenantId", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid("04e977ad-ad4a-4229-b055-e5f933f20666"), null, new DateTime(2025, 11, 17, 15, 52, 8, 190, DateTimeKind.Utc).AddTicks(6329), null, null, null, null, null, null, new DateTime(2025, 11, 17, 15, 52, 8, 190, DateTimeKind.Utc).AddTicks(6329), false, null, "WorkflowEngine", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("5b39c4d0-5a73-4ba6-9d76-1382dc303f82"), null, new DateTime(2025, 11, 17, 15, 52, 8, 190, DateTimeKind.Utc).AddTicks(6249), null, null, null, null, null, null, new DateTime(2025, 11, 17, 15, 52, 8, 190, DateTimeKind.Utc).AddTicks(6249), false, null, "HR", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("78303f76-2605-4f36-a709-cee88ab80cdf"), null, new DateTime(2025, 11, 17, 15, 52, 8, 190, DateTimeKind.Utc).AddTicks(6287), null, null, null, null, null, null, new DateTime(2025, 11, 17, 15, 52, 8, 190, DateTimeKind.Utc).AddTicks(6287), false, null, "Procurement", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("99b3536f-ac7f-436f-af1e-165a823fadfc"), null, new DateTime(2025, 11, 17, 15, 52, 8, 190, DateTimeKind.Utc).AddTicks(6229), null, null, null, null, null, null, new DateTime(2025, 11, 17, 15, 52, 8, 190, DateTimeKind.Utc).AddTicks(6223), false, null, "Finance", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("e487ac78-eb13-4f8a-8674-54a0a1b89309"), null, new DateTime(2025, 11, 17, 15, 52, 8, 190, DateTimeKind.Utc).AddTicks(6316), null, null, null, null, null, null, new DateTime(2025, 11, 17, 15, 52, 8, 190, DateTimeKind.Utc).AddTicks(6315), false, null, "Marketing", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("f063d770-90d2-4094-a925-777f2cd8033a"), null, new DateTime(2025, 11, 17, 15, 52, 8, 190, DateTimeKind.Utc).AddTicks(6272), null, null, null, null, null, null, new DateTime(2025, 11, 17, 15, 52, 8, 190, DateTimeKind.Utc).AddTicks(6272), false, null, "Sales", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("fd613dca-3987-4696-8ec4-db006a3dc630"), null, new DateTime(2025, 11, 17, 15, 52, 8, 190, DateTimeKind.Utc).AddTicks(6301), null, null, null, null, null, null, new DateTime(2025, 11, 17, 15, 52, 8, 190, DateTimeKind.Utc).AddTicks(6301), false, null, "Inventory", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null }
                });

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 17, 15, 52, 8, 190, DateTimeKind.Utc).AddTicks(5873));
        }
    }
}
