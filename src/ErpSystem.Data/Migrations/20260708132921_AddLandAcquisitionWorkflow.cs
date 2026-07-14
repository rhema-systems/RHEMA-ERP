using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLandAcquisitionWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkflowDelegations_WorkflowDefinitionId",
                table: "WorkflowDelegations");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowDelegations_WorkflowStepId",
                table: "WorkflowDelegations");

            migrationBuilder.CreateTable(
                name: "LandAcquisitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectReference = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    IntendedUse = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EstimatedSize = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Location = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    CurrentStage = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    OwnershipType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Coordinates = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    StageOrder = table.Column<int>(type: "int", nullable: false),
                    PlanningUploaded = table.Column<bool>(type: "bit", nullable: false),
                    InternalApproved = table.Column<bool>(type: "bit", nullable: false),
                    SuitableForDueDiligence = table.Column<bool>(type: "bit", nullable: false),
                    WorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubmittedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_LandAcquisitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LandAcquisitions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CadastralSurveys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LandAcquisitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SurveyorName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    SurveyDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PlanNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BeaconCount = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CoordinateReference = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CadastralMatch = table.Column<bool>(type: "bit", nullable: false),
                    OverlapCleared = table.Column<bool>(type: "bit", nullable: false),
                    BoundaryConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    VerificationReference = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
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
                    table.PrimaryKey("PK_CadastralSurveys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CadastralSurveys_LandAcquisitions_LandAcquisitionId",
                        column: x => x.LandAcquisitionId,
                        principalTable: "LandAcquisitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CadastralSurveys_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LandAcquisitionChecklistResponses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LandAcquisitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Procedure = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    StageOrder = table.Column<int>(type: "int", nullable: false),
                    IsChecked = table.Column<bool>(type: "bit", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_LandAcquisitionChecklistResponses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LandAcquisitionChecklistResponses_LandAcquisitions_LandAcquisitionId",
                        column: x => x.LandAcquisitionId,
                        principalTable: "LandAcquisitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LandAcquisitionChecklistResponses_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LandAcquisitionDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LandAcquisitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Procedure = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
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
                    table.PrimaryKey("PK_LandAcquisitionDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LandAcquisitionDocuments_LandAcquisitions_LandAcquisitionId",
                        column: x => x.LandAcquisitionId,
                        principalTable: "LandAcquisitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LandAcquisitionDocuments_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LandAcquisitionNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LandAcquisitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Stage = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    StageOrder = table.Column<int>(type: "int", nullable: false),
                    InputType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
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
                    table.PrimaryKey("PK_LandAcquisitionNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LandAcquisitionNotes_LandAcquisitions_LandAcquisitionId",
                        column: x => x.LandAcquisitionId,
                        principalTable: "LandAcquisitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LandAcquisitionNotes_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LandAgreements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LandAcquisitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LegalReviewComplete = table.Column<bool>(type: "bit", nullable: false),
                    FinanceReviewComplete = table.Column<bool>(type: "bit", nullable: false),
                    BoardApprovalReference = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    ApprovalConditions = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    AgreementDate = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_LandAgreements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LandAgreements_LandAcquisitions_LandAcquisitionId",
                        column: x => x.LandAcquisitionId,
                        principalTable: "LandAcquisitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LandAgreements_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LandAssets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LandAcquisitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssetCode = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    AssetCategory = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    CapitalizationValue = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 2, nullable: false),
                    GlAccount = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Custodian = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
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
                    table.PrimaryKey("PK_LandAssets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LandAssets_LandAcquisitions_LandAcquisitionId",
                        column: x => x.LandAcquisitionId,
                        principalTable: "LandAcquisitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LandAssets_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LandInstruments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LandAcquisitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstrumentType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    InstrumentNumber = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    ExecutionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WitnessDetails = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsExecuted = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_LandInstruments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LandInstruments_LandAcquisitions_LandAcquisitionId",
                        column: x => x.LandAcquisitionId,
                        principalTable: "LandAcquisitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LandInstruments_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LandPhysicalAssessments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LandAcquisitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanningCompatible = table.Column<bool>(type: "bit", nullable: false),
                    AccessConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    EnvironmentalClearance = table.Column<bool>(type: "bit", nullable: false),
                    UtilityAvailability = table.Column<bool>(type: "bit", nullable: false),
                    ZoningClassification = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
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
                    table.PrimaryKey("PK_LandPhysicalAssessments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LandPhysicalAssessments_LandAcquisitions_LandAcquisitionId",
                        column: x => x.LandAcquisitionId,
                        principalTable: "LandAcquisitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LandPhysicalAssessments_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LandRegistrations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LandAcquisitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RegistryOffice = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    RegistrationNumber = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Volume = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Folio = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    RegistrationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsRegistered = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_LandRegistrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LandRegistrations_LandAcquisitions_LandAcquisitionId",
                        column: x => x.LandAcquisitionId,
                        principalTable: "LandAcquisitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LandRegistrations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NegotiationOffers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LandAcquisitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OpeningOffer = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 2, nullable: false),
                    CounterOffer = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 2, nullable: true),
                    NegotiatedValue = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 2, nullable: true),
                    PaymentTerms = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsAccepted = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_NegotiationOffers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NegotiationOffers_LandAcquisitions_LandAcquisitionId",
                        column: x => x.LandAcquisitionId,
                        principalTable: "LandAcquisitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NegotiationOffers_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OwnershipHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LandAcquisitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ContactNumber = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    OwnershipType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    AcquisitionMethod = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    InterestHeld = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    RiskLevel = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    TitleSearchCompleted = table.Column<bool>(type: "bit", nullable: false),
                    OwnerIdentityVerified = table.Column<bool>(type: "bit", nullable: false),
                    AuthorityToSellVerified = table.Column<bool>(type: "bit", nullable: false),
                    SearchReference = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
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
                    table.PrimaryKey("PK_OwnershipHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OwnershipHistories_LandAcquisitions_LandAcquisitionId",
                        column: x => x.LandAcquisitionId,
                        principalTable: "LandAcquisitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OwnershipHistories_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StampDutyAssessments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LandAcquisitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssessedValue = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 2, nullable: false),
                    DutyAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AssessmentReference = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    AssessmentDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FinanceApprovalReference = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    ApproverName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsApproved = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_StampDutyAssessments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StampDutyAssessments_LandAcquisitions_LandAcquisitionId",
                        column: x => x.LandAcquisitionId,
                        principalTable: "LandAcquisitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StampDutyAssessments_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StampDutyPayments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LandAcquisitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceiptNumber = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    PaymentDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AmountPaid = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PaymentMethod = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsPaid = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_StampDutyPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StampDutyPayments_LandAcquisitions_LandAcquisitionId",
                        column: x => x.LandAcquisitionId,
                        principalTable: "LandAcquisitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StampDutyPayments_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StatutoryConsents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LandAcquisitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConsentAuthority = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    ApplicationNumber = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    SubmissionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovalReference = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    ApprovalDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Conditions = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsApproved = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_StatutoryConsents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StatutoryConsents_LandAcquisitions_LandAcquisitionId",
                        column: x => x.LandAcquisitionId,
                        principalTable: "LandAcquisitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StatutoryConsents_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CadastralSurveys_LandAcquisitionId",
                table: "CadastralSurveys",
                column: "LandAcquisitionId");

            migrationBuilder.CreateIndex(
                name: "IX_CadastralSurveys_TenantId",
                table: "CadastralSurveys",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_LandAcquisitionChecklistResponses_LandAcquisitionId",
                table: "LandAcquisitionChecklistResponses",
                column: "LandAcquisitionId");

            migrationBuilder.CreateIndex(
                name: "IX_LandAcquisitionChecklistResponses_TenantId",
                table: "LandAcquisitionChecklistResponses",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_LandAcquisitionDocuments_LandAcquisitionId",
                table: "LandAcquisitionDocuments",
                column: "LandAcquisitionId");

            migrationBuilder.CreateIndex(
                name: "IX_LandAcquisitionDocuments_TenantId",
                table: "LandAcquisitionDocuments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_LandAcquisitionNotes_LandAcquisitionId",
                table: "LandAcquisitionNotes",
                column: "LandAcquisitionId");

            migrationBuilder.CreateIndex(
                name: "IX_LandAcquisitionNotes_TenantId",
                table: "LandAcquisitionNotes",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_LandAcquisitions_TenantId_ProjectReference",
                table: "LandAcquisitions",
                columns: new[] { "TenantId", "ProjectReference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LandAcquisitions_TenantId_StageOrder_Status",
                table: "LandAcquisitions",
                columns: new[] { "TenantId", "StageOrder", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_LandAgreements_LandAcquisitionId",
                table: "LandAgreements",
                column: "LandAcquisitionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LandAgreements_TenantId",
                table: "LandAgreements",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_LandAssets_LandAcquisitionId",
                table: "LandAssets",
                column: "LandAcquisitionId");

            migrationBuilder.CreateIndex(
                name: "IX_LandAssets_TenantId",
                table: "LandAssets",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_LandInstruments_LandAcquisitionId",
                table: "LandInstruments",
                column: "LandAcquisitionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LandInstruments_TenantId",
                table: "LandInstruments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_LandPhysicalAssessments_LandAcquisitionId",
                table: "LandPhysicalAssessments",
                column: "LandAcquisitionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LandPhysicalAssessments_TenantId",
                table: "LandPhysicalAssessments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_LandRegistrations_LandAcquisitionId",
                table: "LandRegistrations",
                column: "LandAcquisitionId");

            migrationBuilder.CreateIndex(
                name: "IX_LandRegistrations_TenantId",
                table: "LandRegistrations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_NegotiationOffers_LandAcquisitionId",
                table: "NegotiationOffers",
                column: "LandAcquisitionId");

            migrationBuilder.CreateIndex(
                name: "IX_NegotiationOffers_TenantId",
                table: "NegotiationOffers",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_OwnershipHistories_LandAcquisitionId",
                table: "OwnershipHistories",
                column: "LandAcquisitionId");

            migrationBuilder.CreateIndex(
                name: "IX_OwnershipHistories_TenantId",
                table: "OwnershipHistories",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_StampDutyAssessments_LandAcquisitionId",
                table: "StampDutyAssessments",
                column: "LandAcquisitionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StampDutyAssessments_TenantId",
                table: "StampDutyAssessments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_StampDutyPayments_LandAcquisitionId",
                table: "StampDutyPayments",
                column: "LandAcquisitionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StampDutyPayments_TenantId",
                table: "StampDutyPayments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_StatutoryConsents_LandAcquisitionId",
                table: "StatutoryConsents",
                column: "LandAcquisitionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StatutoryConsents_TenantId",
                table: "StatutoryConsents",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CadastralSurveys");

            migrationBuilder.DropTable(
                name: "LandAcquisitionChecklistResponses");

            migrationBuilder.DropTable(
                name: "LandAcquisitionDocuments");

            migrationBuilder.DropTable(
                name: "LandAcquisitionNotes");

            migrationBuilder.DropTable(
                name: "LandAgreements");

            migrationBuilder.DropTable(
                name: "LandAssets");

            migrationBuilder.DropTable(
                name: "LandInstruments");

            migrationBuilder.DropTable(
                name: "LandPhysicalAssessments");

            migrationBuilder.DropTable(
                name: "LandRegistrations");

            migrationBuilder.DropTable(
                name: "NegotiationOffers");

            migrationBuilder.DropTable(
                name: "OwnershipHistories");

            migrationBuilder.DropTable(
                name: "StampDutyAssessments");

            migrationBuilder.DropTable(
                name: "StampDutyPayments");

            migrationBuilder.DropTable(
                name: "StatutoryConsents");

            migrationBuilder.DropTable(
                name: "LandAcquisitions");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDelegations_WorkflowDefinitionId",
                table: "WorkflowDelegations",
                column: "WorkflowDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDelegations_WorkflowStepId",
                table: "WorkflowDelegations",
                column: "WorkflowStepId");
        }
    }
}
