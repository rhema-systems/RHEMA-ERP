using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProcurementEvaluationCommitteeControls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProcurementEvaluationCommitteeControls",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceType = table.Column<int>(type: "int", nullable: false),
                    SourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    SourceReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CommitteeTemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CommitteeCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CommitteeName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RequiredQuorum = table.Column<int>(type: "int", nullable: false),
                    PolicySetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PolicyCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PolicyVersion = table.Column<int>(type: "int", nullable: false),
                    ConfigurationProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ConfigurationProfileCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ConfigurationProfileVersion = table.Column<int>(type: "int", nullable: true),
                    MethodRuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MethodRuleCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    WorkflowDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EffectiveFromUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveToUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ActivatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ActivatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActivationEvidenceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ActivationIdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CompositionSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompositionIntegrityHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreationIdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
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
                    table.PrimaryKey("PK_ProcurementEvaluationCommitteeControls", x => x.Id);
                    table.CheckConstraint("CK_ProcurementEvaluationCommitteeControls_State", "[SourceType] BETWEEN 0 AND 1 AND [Version] >= 1 AND [Status] BETWEEN 0 AND 2 AND [RequiredQuorum] BETWEEN 1 AND 50 AND [PolicyVersion] >= 1 AND ([ConfigurationProfileVersion] IS NULL OR [ConfigurationProfileVersion] >= 1) AND ([EffectiveToUtc] IS NULL OR [EffectiveToUtc] >= [EffectiveFromUtc]) AND LEN([CompositionIntegrityHash]) = 64 AND ISJSON([CompositionSnapshotJson]) = 1 AND (([Status] = 0 AND [ActivatedAtUtc] IS NULL AND [ActivatedByUserId] IS NULL AND [ActivationEvidenceReference] IS NULL) OR ([Status] IN (1, 2) AND [ActivatedAtUtc] IS NOT NULL AND [ActivatedByUserId] IS NOT NULL AND LEN(LTRIM(RTRIM(ISNULL([ActivationEvidenceReference], '')))) > 0))");
                    table.ForeignKey(
                        name: "FK_ProcurementEvaluationCommitteeControls_ProcurementCommittees_CommitteeTemplateId",
                        column: x => x.CommitteeTemplateId,
                        principalTable: "ProcurementCommittees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementEvaluationCommitteeControls_ProcurementConfigurationProfiles_ConfigurationProfileId",
                        column: x => x.ConfigurationProfileId,
                        principalTable: "ProcurementConfigurationProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementEvaluationCommitteeControls_ProcurementPolicyMethodRules_MethodRuleId",
                        column: x => x.MethodRuleId,
                        principalTable: "ProcurementPolicyMethodRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementEvaluationCommitteeControls_ProcurementPolicySets_PolicySetId",
                        column: x => x.PolicySetId,
                        principalTable: "ProcurementPolicySets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementEvaluationCommitteeControls_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementEvaluationCommitteeControls_WorkflowDefinitions_WorkflowDefinitionId",
                        column: x => x.WorkflowDefinitionId,
                        principalTable: "WorkflowDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementEvaluationCommitteeControls_WorkflowInstances_WorkflowInstanceId",
                        column: x => x.WorkflowInstanceId,
                        principalTable: "WorkflowInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementEvaluationCommitteeAppointments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CommitteeControlId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CommitteeMemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResponsibilityAssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserDisplayName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    RoleName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MemberKind = table.Column<int>(type: "int", nullable: false),
                    IsVoting = table.Column<bool>(type: "bit", nullable: false),
                    EffectiveFromUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveToUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AcceptedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AcceptanceSignatureReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AcceptanceEvidenceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AcceptanceIdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    StatusReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_ProcurementEvaluationCommitteeAppointments", x => x.Id);
                    table.CheckConstraint("CK_ProcurementEvaluationCommitteeAppointments_State", "[MemberKind] BETWEEN 0 AND 4 AND [Status] BETWEEN 0 AND 3 AND ([EffectiveToUtc] IS NULL OR [EffectiveToUtc] >= [EffectiveFromUtc]) AND (([Status] = 0 AND [AcceptedAtUtc] IS NULL AND [AcceptanceSignatureReference] IS NULL AND [AcceptanceEvidenceReference] IS NULL) OR ([Status] = 1 AND [AcceptedAtUtc] IS NOT NULL AND LEN(LTRIM(RTRIM(ISNULL([AcceptanceSignatureReference], '')))) > 0 AND LEN(LTRIM(RTRIM(ISNULL([AcceptanceEvidenceReference], '')))) > 0) OR ([Status] IN (2, 3) AND [AcceptedAtUtc] IS NULL))");
                    table.ForeignKey(
                        name: "FK_ProcurementEvaluationCommitteeAppointments_ProcurementCommitteeMembers_CommitteeMemberId",
                        column: x => x.CommitteeMemberId,
                        principalTable: "ProcurementCommitteeMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementEvaluationCommitteeAppointments_ProcurementEvaluationCommitteeControls_CommitteeControlId",
                        column: x => x.CommitteeControlId,
                        principalTable: "ProcurementEvaluationCommitteeControls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementEvaluationCommitteeAppointments_ProcurementResponsibilityAssignments_ResponsibilityAssignmentId",
                        column: x => x.ResponsibilityAssignmentId,
                        principalTable: "ProcurementResponsibilityAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementEvaluationCommitteeAppointments_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementEvaluationCommitteeAppointments_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementEvaluationCommitteeRoleRequirements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CommitteeControlId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MemberKind = table.Column<int>(type: "int", nullable: false),
                    RoleName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MinimumCount = table.Column<int>(type: "int", nullable: false),
                    IsVoting = table.Column<bool>(type: "bit", nullable: false),
                    IsRequiredForQuorum = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_ProcurementEvaluationCommitteeRoleRequirements", x => x.Id);
                    table.CheckConstraint("CK_ProcurementEvaluationCommitteeRoleRequirements_State", "[MemberKind] BETWEEN 0 AND 4 AND [MinimumCount] BETWEEN 1 AND 50");
                    table.ForeignKey(
                        name: "FK_ProcurementEvaluationCommitteeRoleRequirements_ProcurementEvaluationCommitteeControls_CommitteeControlId",
                        column: x => x.CommitteeControlId,
                        principalTable: "ProcurementEvaluationCommitteeControls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementEvaluationCommitteeRoleRequirements_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementEvaluationMeetings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CommitteeControlId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    Phase = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    MeetingMode = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    MeetingChannel = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ScheduledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EligibleVotingMemberCount = table.Column<int>(type: "int", nullable: false),
                    SignedVotingAttendanceCount = table.Column<int>(type: "int", nullable: false),
                    ChairPresent = table.Column<bool>(type: "bit", nullable: false),
                    SecretaryPresent = table.Column<bool>(type: "bit", nullable: false),
                    QuorumMet = table.Column<bool>(type: "bit", nullable: false),
                    EvidenceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    RemoteMeetingEvidenceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    QuorumIdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    QuorumSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    QuorumIntegrityHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
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
                    table.PrimaryKey("PK_ProcurementEvaluationMeetings", x => x.Id);
                    table.CheckConstraint("CK_ProcurementEvaluationMeetings_State", "[Sequence] >= 1 AND [Phase] BETWEEN 0 AND 2 AND [Status] BETWEEN 0 AND 3 AND [MeetingMode] IN ('InPerson', 'Remote', 'Hybrid') AND (([MeetingMode] = 'InPerson') OR LEN(LTRIM(RTRIM(ISNULL([RemoteMeetingEvidenceReference], '')))) > 0) AND [EligibleVotingMemberCount] >= 0 AND [SignedVotingAttendanceCount] >= 0 AND LEN([QuorumIntegrityHash]) = 64 AND ISJSON([QuorumSnapshotJson]) = 1 AND (([Status] = 0 AND [QuorumMet] = 0) OR ([Status] = 1 AND [QuorumMet] = 1 AND [StartedAtUtc] IS NOT NULL AND [ChairPresent] = 1 AND [SecretaryPresent] = 1) OR ([Status] = 2 AND [QuorumMet] = 0 AND [StartedAtUtc] IS NOT NULL) OR ([Status] = 3 AND [ClosedAtUtc] IS NOT NULL))");
                    table.ForeignKey(
                        name: "FK_ProcurementEvaluationMeetings_ProcurementEvaluationCommitteeControls_CommitteeControlId",
                        column: x => x.CommitteeControlId,
                        principalTable: "ProcurementEvaluationCommitteeControls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementEvaluationMeetings_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementEvaluationConflictDeclarations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppointmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Outcome = table.Column<int>(type: "int", nullable: false),
                    Declaration = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ConflictDetails = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SignatureReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    EvidenceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    WorkflowEvidenceDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FileUploadRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ValidFromUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ValidToUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeclaredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeclaredByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
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
                    table.PrimaryKey("PK_ProcurementEvaluationConflictDeclarations", x => x.Id);
                    table.CheckConstraint("CK_ProcurementEvaluationConflictDeclarations_State", "[Version] >= 1 AND [Outcome] BETWEEN 0 AND 2 AND ([ValidToUtc] IS NULL OR [ValidToUtc] >= [ValidFromUtc]) AND (([Outcome] <> 1) OR LEN(LTRIM(RTRIM(ISNULL([ConflictDetails], '')))) > 0) AND LEN([IntegrityHash]) = 64 AND ISJSON([SnapshotJson]) = 1 AND ([WorkflowEvidenceDocumentId] IS NULL OR [FileUploadRecordId] IS NULL)");
                    table.ForeignKey(
                        name: "FK_ProcurementEvaluationConflictDeclarations_FileUploadRecords_FileUploadRecordId",
                        column: x => x.FileUploadRecordId,
                        principalTable: "FileUploadRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementEvaluationConflictDeclarations_ProcurementEvaluationCommitteeAppointments_AppointmentId",
                        column: x => x.AppointmentId,
                        principalTable: "ProcurementEvaluationCommitteeAppointments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementEvaluationConflictDeclarations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementEvaluationConflictDeclarations_WorkflowEvidenceDocuments_WorkflowEvidenceDocumentId",
                        column: x => x.WorkflowEvidenceDocumentId,
                        principalTable: "WorkflowEvidenceDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementEvaluationAttendanceRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MeetingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppointmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsPresent = table.Column<bool>(type: "bit", nullable: false),
                    SignedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SignatureReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    EvidenceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    WasEligibleAtSignature = table.Column<bool>(type: "bit", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
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
                    table.PrimaryKey("PK_ProcurementEvaluationAttendanceRecords", x => x.Id);
                    table.CheckConstraint("CK_ProcurementEvaluationAttendanceRecords_State", "[SignedAtUtc] IS NOT NULL AND LEN(LTRIM(RTRIM(ISNULL([SignatureReference], '')))) > 0 AND LEN(LTRIM(RTRIM(ISNULL([EvidenceReference], '')))) > 0 AND LEN([IntegrityHash]) = 64 AND ISJSON([SnapshotJson]) = 1");
                    table.ForeignKey(
                        name: "FK_ProcurementEvaluationAttendanceRecords_ProcurementEvaluationCommitteeAppointments_AppointmentId",
                        column: x => x.AppointmentId,
                        principalTable: "ProcurementEvaluationCommitteeAppointments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementEvaluationAttendanceRecords_ProcurementEvaluationMeetings_MeetingId",
                        column: x => x.MeetingId,
                        principalTable: "ProcurementEvaluationMeetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementEvaluationAttendanceRecords_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementEvaluationScoreSheets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CommitteeControlId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MeetingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppointmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Phase = table.Column<int>(type: "int", nullable: false),
                    ScoreSubjectType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ScoreSubjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Attempt = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SubmittedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SubmittedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmittedByName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ScoreSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SignatureReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    EvidenceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IntegrityHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
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
                    table.PrimaryKey("PK_ProcurementEvaluationScoreSheets", x => x.Id);
                    table.CheckConstraint("CK_ProcurementEvaluationScoreSheets_State", "[Phase] BETWEEN 0 AND 2 AND [Attempt] >= 1 AND [Status] = 0 AND LEN([IntegrityHash]) = 64 AND ISJSON([ScoreSnapshotJson]) = 1");
                    table.ForeignKey(
                        name: "FK_ProcurementEvaluationScoreSheets_ProcurementEvaluationCommitteeAppointments_AppointmentId",
                        column: x => x.AppointmentId,
                        principalTable: "ProcurementEvaluationCommitteeAppointments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementEvaluationScoreSheets_ProcurementEvaluationCommitteeControls_CommitteeControlId",
                        column: x => x.CommitteeControlId,
                        principalTable: "ProcurementEvaluationCommitteeControls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementEvaluationScoreSheets_ProcurementEvaluationMeetings_MeetingId",
                        column: x => x.MeetingId,
                        principalTable: "ProcurementEvaluationMeetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementEvaluationScoreSheets_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementEvaluationScoreRecalls",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScoreSheetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    EvidenceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    WorkflowEvidenceDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FileUploadRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WorkflowDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedByName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DecidedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DecidedByName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    DecidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecisionReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DecisionEvidenceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DecisionIdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AuthorizedNewAttempt = table.Column<int>(type: "int", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
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
                    table.PrimaryKey("PK_ProcurementEvaluationScoreRecalls", x => x.Id);
                    table.CheckConstraint("CK_ProcurementEvaluationScoreRecalls_State", "[Status] BETWEEN 0 AND 2 AND LEN([IntegrityHash]) = 64 AND ISJSON([SnapshotJson]) = 1 AND ([WorkflowEvidenceDocumentId] IS NULL OR [FileUploadRecordId] IS NULL) AND (([Status] = 0 AND [DecidedByUserId] IS NULL AND [DecidedAtUtc] IS NULL AND [DecisionReference] IS NULL AND [DecisionEvidenceReference] IS NULL AND [AuthorizedNewAttempt] IS NULL) OR ([Status] = 1 AND [DecidedByUserId] IS NOT NULL AND [DecidedAtUtc] IS NOT NULL AND LEN(LTRIM(RTRIM(ISNULL([DecisionReference], '')))) > 0 AND LEN(LTRIM(RTRIM(ISNULL([DecisionEvidenceReference], '')))) > 0 AND [AuthorizedNewAttempt] >= 2) OR ([Status] = 2 AND [DecidedByUserId] IS NOT NULL AND [DecidedAtUtc] IS NOT NULL AND LEN(LTRIM(RTRIM(ISNULL([DecisionReference], '')))) > 0 AND LEN(LTRIM(RTRIM(ISNULL([DecisionEvidenceReference], '')))) > 0 AND [AuthorizedNewAttempt] IS NULL))");
                    table.ForeignKey(
                        name: "FK_ProcurementEvaluationScoreRecalls_FileUploadRecords_FileUploadRecordId",
                        column: x => x.FileUploadRecordId,
                        principalTable: "FileUploadRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementEvaluationScoreRecalls_ProcurementEvaluationScoreSheets_ScoreSheetId",
                        column: x => x.ScoreSheetId,
                        principalTable: "ProcurementEvaluationScoreSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementEvaluationScoreRecalls_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementEvaluationScoreRecalls_WorkflowDefinitions_WorkflowDefinitionId",
                        column: x => x.WorkflowDefinitionId,
                        principalTable: "WorkflowDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementEvaluationScoreRecalls_WorkflowEvidenceDocuments_WorkflowEvidenceDocumentId",
                        column: x => x.WorkflowEvidenceDocumentId,
                        principalTable: "WorkflowEvidenceDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementEvaluationScoreRecalls_WorkflowInstances_WorkflowInstanceId",
                        column: x => x.WorkflowInstanceId,
                        principalTable: "WorkflowInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementEvaluationAttendanceRecords_AppointmentId",
                table: "ProcurementEvaluationAttendanceRecords",
                column: "AppointmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementEvaluationAttendanceRecords_MeetingId",
                table: "ProcurementEvaluationAttendanceRecords",
                column: "MeetingId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementEvaluationAttendanceRecords_TenantId_MeetingId_AppointmentId",
                table: "ProcurementEvaluationAttendanceRecords",
                columns: new[] { "TenantId", "MeetingId", "AppointmentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementEvaluationAttendanceRecords_TenantId_MeetingId_IdempotencyKey",
                table: "ProcurementEvaluationAttendanceRecords",
                columns: new[] { "TenantId", "MeetingId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementEvaluationCommitteeAppointments_CommitteeControlId",
                table: "ProcurementEvaluationCommitteeAppointments",
                column: "CommitteeControlId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementEvaluationCommitteeAppointments_CommitteeMemberId",
                table: "ProcurementEvaluationCommitteeAppointments",
                column: "CommitteeMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementEvaluationCommitteeAppointments_ResponsibilityAssignmentId",
                table: "ProcurementEvaluationCommitteeAppointments",
                column: "ResponsibilityAssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementEvaluationCommitteeAppointments_TenantId_CommitteeControlId_CommitteeMemberId",
                table: "ProcurementEvaluationCommitteeAppointments",
                columns: new[] { "TenantId", "CommitteeControlId", "CommitteeMemberId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementEvaluationCommitteeAppointments_TenantId_CommitteeControlId_UserId",
                table: "ProcurementEvaluationCommitteeAppointments",
                columns: new[] { "TenantId", "CommitteeControlId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementEvaluationCommitteeAppointments_TenantId_ResponsibilityAssignmentId",
                table: "ProcurementEvaluationCommitteeAppointments",
                columns: new[] { "TenantId", "ResponsibilityAssignmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementEvaluationCommitteeAppointments_UserId",
                table: "ProcurementEvaluationCommitteeAppointments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementEvaluationCommitteeControls_CommitteeTemplateId",
                table: "ProcurementEvaluationCommitteeControls",
                column: "CommitteeTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementEvaluationCommitteeControls_ConfigurationProfileId",
                table: "ProcurementEvaluationCommitteeControls",
                column: "ConfigurationProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementEvaluationCommitteeControls_MethodRuleId",
                table: "ProcurementEvaluationCommitteeControls",
                column: "MethodRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementEvaluationCommitteeControls_PolicySetId",
                table: "ProcurementEvaluationCommitteeControls",
                column: "PolicySetId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementEvaluationCommitteeControls_TenantId_CommitteeTemplateId",
                table: "ProcurementEvaluationCommitteeControls",
                columns: new[] { "TenantId", "CommitteeTemplateId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementEvaluationCommitteeControls_TenantId_PolicySetId",
                table: "ProcurementEvaluationCommitteeControls",
                columns: new[] { "TenantId", "PolicySetId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementEvaluationCommitteeControls_TenantId_SourceType_SourceId",
                table: "ProcurementEvaluationCommitteeControls",
                columns: new[] { "TenantId", "SourceType", "SourceId" },
                unique: true,
                filter: "[Status] IN (0, 1) AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementEvaluationCommitteeControls_TenantId_SourceType_SourceId_CreationIdempotencyKey",
                table: "ProcurementEvaluationCommitteeControls",
                columns: new[] { "TenantId", "SourceType", "SourceId", "CreationIdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementEvaluationCommitteeControls_TenantId_SourceType_SourceId_Version",
                table: "ProcurementEvaluationCommitteeControls",
                columns: new[] { "TenantId", "SourceType", "SourceId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementEvaluationCommitteeControls_TenantId_WorkflowDefinitionId",
                table: "ProcurementEvaluationCommitteeControls",
                columns: new[] { "TenantId", "WorkflowDefinitionId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementEvaluationCommitteeControls_WorkflowDefinitionId",
                table: "ProcurementEvaluationCommitteeControls",
                column: "WorkflowDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementEvaluationCommitteeControls_WorkflowInstanceId",
                table: "ProcurementEvaluationCommitteeControls",
                column: "WorkflowInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementEvaluationCommitteeRoleRequirements_CommitteeControlId",
                table: "ProcurementEvaluationCommitteeRoleRequirements",
                column: "CommitteeControlId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementEvaluationCommitteeRoleRequirements_TenantId_CommitteeControlId_MemberKind_RoleName",
                table: "ProcurementEvaluationCommitteeRoleRequirements",
                columns: new[] { "TenantId", "CommitteeControlId", "MemberKind", "RoleName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementEvaluationConflictDeclarations_AppointmentId",
                table: "ProcurementEvaluationConflictDeclarations",
                column: "AppointmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementEvaluationConflictDeclarations_FileUploadRecordId",
                table: "ProcurementEvaluationConflictDeclarations",
                column: "FileUploadRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementEvaluationConflictDeclarations_TenantId_AppointmentId_IdempotencyKey",
                table: "ProcurementEvaluationConflictDeclarations",
                columns: new[] { "TenantId", "AppointmentId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementEvaluationConflictDeclarations_TenantId_AppointmentId_Version",
                table: "ProcurementEvaluationConflictDeclarations",
                columns: new[] { "TenantId", "AppointmentId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementEvaluationConflictDeclarations_WorkflowEvidenceDocumentId",
                table: "ProcurementEvaluationConflictDeclarations",
                column: "WorkflowEvidenceDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementEvaluationMeetings_CommitteeControlId",
                table: "ProcurementEvaluationMeetings",
                column: "CommitteeControlId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementEvaluationMeetings_TenantId_CommitteeControlId_IdempotencyKey",
                table: "ProcurementEvaluationMeetings",
                columns: new[] { "TenantId", "CommitteeControlId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementEvaluationMeetings_TenantId_CommitteeControlId_Phase_Status",
                table: "ProcurementEvaluationMeetings",
                columns: new[] { "TenantId", "CommitteeControlId", "Phase", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementEvaluationMeetings_TenantId_CommitteeControlId_Sequence",
                table: "ProcurementEvaluationMeetings",
                columns: new[] { "TenantId", "CommitteeControlId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementEvaluationScoreRecalls_FileUploadRecordId",
                table: "ProcurementEvaluationScoreRecalls",
                column: "FileUploadRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementEvaluationScoreRecalls_ScoreSheetId",
                table: "ProcurementEvaluationScoreRecalls",
                column: "ScoreSheetId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementEvaluationScoreRecalls_TenantId_ScoreSheetId",
                table: "ProcurementEvaluationScoreRecalls",
                columns: new[] { "TenantId", "ScoreSheetId" },
                unique: true,
                filter: "[Status] IN (0, 1) AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementEvaluationScoreRecalls_TenantId_ScoreSheetId_IdempotencyKey",
                table: "ProcurementEvaluationScoreRecalls",
                columns: new[] { "TenantId", "ScoreSheetId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementEvaluationScoreRecalls_TenantId_WorkflowDefinitionId",
                table: "ProcurementEvaluationScoreRecalls",
                columns: new[] { "TenantId", "WorkflowDefinitionId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementEvaluationScoreRecalls_TenantId_WorkflowInstanceId",
                table: "ProcurementEvaluationScoreRecalls",
                columns: new[] { "TenantId", "WorkflowInstanceId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementEvaluationScoreRecalls_WorkflowDefinitionId",
                table: "ProcurementEvaluationScoreRecalls",
                column: "WorkflowDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementEvaluationScoreRecalls_WorkflowEvidenceDocumentId",
                table: "ProcurementEvaluationScoreRecalls",
                column: "WorkflowEvidenceDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementEvaluationScoreRecalls_WorkflowInstanceId",
                table: "ProcurementEvaluationScoreRecalls",
                column: "WorkflowInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementEvaluationScoreSheets_AppointmentId",
                table: "ProcurementEvaluationScoreSheets",
                column: "AppointmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementEvaluationScoreSheets_CommitteeControlId",
                table: "ProcurementEvaluationScoreSheets",
                column: "CommitteeControlId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementEvaluationScoreSheets_MeetingId",
                table: "ProcurementEvaluationScoreSheets",
                column: "MeetingId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementEvaluationScoreSheets_TenantId_CommitteeControlId_IdempotencyKey",
                table: "ProcurementEvaluationScoreSheets",
                columns: new[] { "TenantId", "CommitteeControlId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementEvaluationScoreSheets_TenantId_CommitteeControlId_Phase_AppointmentId_ScoreSubjectType_ScoreSubjectId_Attempt",
                table: "ProcurementEvaluationScoreSheets",
                columns: new[] { "TenantId", "CommitteeControlId", "Phase", "AppointmentId", "ScoreSubjectType", "ScoreSubjectId", "Attempt" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementEvaluationScoreSheets_TenantId_CommitteeControlId_Phase_ScoreSubjectId",
                table: "ProcurementEvaluationScoreSheets",
                columns: new[] { "TenantId", "CommitteeControlId", "Phase", "ScoreSubjectId" });

            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX [UX_TenderEvaluations_Tenant_Bid_Evaluator_Draft]
                ON [dbo].[TenderEvaluations] ([TenantId], [TenderBidId], [TenderEvaluatorId])
                WHERE [Status] = 'Draft' AND [IsDeleted] = 0;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER [dbo].[TR_ProcurementEvaluationCommitteeControls_Lifecycle]
                ON [dbo].[ProcurementEvaluationCommitteeControls]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id = d.Id WHERE i.Id IS NULL)
                        THROW 51300, 'Evaluation committee controls cannot be deleted.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        LEFT JOIN [dbo].[Tenders] t
                          ON i.SourceType = 0 AND t.Id = i.SourceId AND t.TenantId = i.TenantId AND t.IsDeleted = 0
                        LEFT JOIN [dbo].[RequestForQuotations] r
                          ON i.SourceType = 1 AND r.Id = i.SourceId AND r.TenantId = i.TenantId AND r.IsDeleted = 0
                        LEFT JOIN [dbo].[ProcurementCommittees] c
                          ON c.Id = i.CommitteeTemplateId AND c.TenantId = i.TenantId AND c.IsDeleted = 0
                        LEFT JOIN [dbo].[ProcurementPolicySets] p
                          ON p.Id = i.PolicySetId AND p.TenantId = i.TenantId AND p.IsDeleted = 0
                        LEFT JOIN [dbo].[ProcurementConfigurationProfiles] cp
                          ON cp.Id = i.ConfigurationProfileId AND cp.TenantId = i.TenantId AND cp.IsDeleted = 0
                        LEFT JOIN [dbo].[ProcurementPolicyMethodRules] mr
                          ON mr.Id = i.MethodRuleId AND mr.TenantId = i.TenantId AND mr.IsDeleted = 0
                        LEFT JOIN [dbo].[WorkflowDefinitions] wd
                          ON wd.Id = i.WorkflowDefinitionId AND wd.TenantId = i.TenantId AND wd.IsDeleted = 0
                        LEFT JOIN [dbo].[WorkflowInstances] wi
                          ON wi.Id = i.WorkflowInstanceId AND wi.TenantId = i.TenantId AND wi.IsDeleted = 0
                        WHERE i.IsDeleted = 1
                           OR (i.SourceType = 0 AND t.Id IS NULL)
                           OR (i.SourceType = 1 AND r.Id IS NULL)
                           OR (i.SourceType = 0 AND t.TenderNumber <> i.SourceReference)
                           OR (i.SourceType = 1 AND r.RfqNumber <> i.SourceReference)
                           OR c.Id IS NULL OR c.CommitteeType <> 2
                           OR c.Code <> i.CommitteeCode OR c.Name <> i.CommitteeName
                           OR c.RequiredQuorum <> i.RequiredQuorum
                           OR p.Id IS NULL
                           OR p.Code <> i.PolicyCode OR p.Version <> i.PolicyVersion
                           OR mr.Id IS NULL OR mr.PolicySetId <> i.PolicySetId
                           OR mr.RuleCode <> i.MethodRuleCode
                           OR ISNULL(mr.WorkflowDefinitionId, '00000000-0000-0000-0000-000000000000')
                              <> ISNULL(i.WorkflowDefinitionId, '00000000-0000-0000-0000-000000000000')
                           OR (i.ConfigurationProfileId IS NOT NULL AND
                               (cp.Id IS NULL OR p.SourceConfigurationProfileId <> i.ConfigurationProfileId
                                OR cp.ProfileCode <> i.ConfigurationProfileCode
                                OR cp.Version <> i.ConfigurationProfileVersion))
                           OR (i.ConfigurationProfileId IS NULL AND
                               (i.ConfigurationProfileCode IS NOT NULL OR i.ConfigurationProfileVersion IS NOT NULL))
                           OR (i.WorkflowDefinitionId IS NOT NULL AND wd.Id IS NULL)
                           OR (i.WorkflowInstanceId IS NOT NULL AND
                               (wi.Id IS NULL OR i.WorkflowDefinitionId IS NULL
                                OR wi.WorkflowDefinitionId <> i.WorkflowDefinitionId))
                           OR (NOT EXISTS (SELECT 1 FROM deleted d WHERE d.Id = i.Id)
                               AND (c.Status <> 1 OR p.LifecycleStatus <> 1
                                    OR mr.IsAllowed = 0 OR mr.IsEnabled = 0
                                    OR c.EffectiveFrom > SYSUTCDATETIME()
                                    OR (c.EffectiveTo IS NOT NULL AND c.EffectiveTo < SYSUTCDATETIME())
                                    OR (cp.Id IS NOT NULL AND cp.LifecycleStatus <> 1))))
                        THROW 51301, 'Evaluation committee source, template, policy, method, configuration, workflow, or tenant lineage is invalid.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        JOIN deleted d ON d.Id = i.Id
                        WHERE i.TenantId <> d.TenantId
                           OR i.SourceType <> d.SourceType OR i.SourceId <> d.SourceId OR i.Version <> d.Version
                           OR i.SourceReference <> d.SourceReference OR i.Purpose <> d.Purpose
                           OR i.CommitteeTemplateId <> d.CommitteeTemplateId OR i.CommitteeCode <> d.CommitteeCode
                           OR i.CommitteeName <> d.CommitteeName OR i.RequiredQuorum <> d.RequiredQuorum
                           OR i.PolicySetId <> d.PolicySetId OR i.PolicyCode <> d.PolicyCode OR i.PolicyVersion <> d.PolicyVersion
                           OR ISNULL(i.ConfigurationProfileId, '00000000-0000-0000-0000-000000000000')
                              <> ISNULL(d.ConfigurationProfileId, '00000000-0000-0000-0000-000000000000')
                           OR ISNULL(i.ConfigurationProfileCode, '') <> ISNULL(d.ConfigurationProfileCode, '')
                           OR ISNULL(i.ConfigurationProfileVersion, 0) <> ISNULL(d.ConfigurationProfileVersion, 0)
                           OR i.MethodRuleId <> d.MethodRuleId OR i.MethodRuleCode <> d.MethodRuleCode
                           OR ISNULL(i.WorkflowDefinitionId, '00000000-0000-0000-0000-000000000000')
                              <> ISNULL(d.WorkflowDefinitionId, '00000000-0000-0000-0000-000000000000')
                           OR ISNULL(i.WorkflowInstanceId, '00000000-0000-0000-0000-000000000000')
                              <> ISNULL(d.WorkflowInstanceId, '00000000-0000-0000-0000-000000000000')
                           OR i.EffectiveFromUtc <> d.EffectiveFromUtc
                           OR ISNULL(i.EffectiveToUtc, '99991231') <> ISNULL(d.EffectiveToUtc, '99991231')
                           OR i.CompositionSnapshotJson <> d.CompositionSnapshotJson
                           OR i.CompositionIntegrityHash <> d.CompositionIntegrityHash
                           OR i.CreationIdempotencyKey <> d.CreationIdempotencyKey
                           OR i.CreatedAt <> d.CreatedAt OR i.IsDeleted <> d.IsDeleted
                           OR NOT (i.Status = d.Status
                               OR (d.Status = 0 AND i.Status = 1)
                               OR (d.Status = 1 AND i.Status = 2))
                           OR (d.Status <> 0 AND
                               (ISNULL(i.ActivatedAtUtc, '19000101') <> ISNULL(d.ActivatedAtUtc, '19000101')
                                OR ISNULL(i.ActivatedByUserId, '00000000-0000-0000-0000-000000000000')
                                   <> ISNULL(d.ActivatedByUserId, '00000000-0000-0000-0000-000000000000')
                                OR ISNULL(i.ActivationEvidenceReference, '') <> ISNULL(d.ActivationEvidenceReference, '')
                                OR ISNULL(i.ActivationIdempotencyKey, '') <> ISNULL(d.ActivationIdempotencyKey, ''))))
                        THROW 51302, 'Evaluation committee immutable lineage or lifecycle transition is invalid.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        JOIN deleted d ON d.Id = i.Id
                        WHERE d.Status = 0 AND i.Status = 1
                          AND (
                              (SELECT COUNT(*) FROM [dbo].[ProcurementEvaluationCommitteeAppointments] a
                               WHERE a.CommitteeControlId = i.Id AND a.TenantId = i.TenantId AND a.IsDeleted = 0 AND a.MemberKind = 0) <> 1
                              OR
                              (SELECT COUNT(*) FROM [dbo].[ProcurementEvaluationCommitteeAppointments] a
                               WHERE a.CommitteeControlId = i.Id AND a.TenantId = i.TenantId AND a.IsDeleted = 0 AND a.MemberKind = 4) <> 1
                              OR
                              (SELECT COUNT(*) FROM [dbo].[ProcurementEvaluationCommitteeAppointments] a
                               WHERE a.CommitteeControlId = i.Id AND a.TenantId = i.TenantId AND a.IsDeleted = 0 AND a.IsVoting = 1) < i.RequiredQuorum
                              OR EXISTS (
                                  SELECT 1
                                  FROM [dbo].[ProcurementEvaluationCommitteeRoleRequirements] rr
                                  WHERE rr.CommitteeControlId = i.Id AND rr.TenantId = i.TenantId AND rr.IsDeleted = 0
                                    AND (SELECT COUNT(*)
                                         FROM [dbo].[ProcurementEvaluationCommitteeAppointments] a
                                         WHERE a.CommitteeControlId = i.Id AND a.TenantId = i.TenantId AND a.IsDeleted = 0
                                           AND a.MemberKind = rr.MemberKind
                                           AND UPPER(LTRIM(RTRIM(a.RoleName))) = UPPER(LTRIM(RTRIM(rr.RoleName)))
                                           AND (rr.IsVoting = 0 OR a.IsVoting = 1)) < rr.MinimumCount)))
                        THROW 51303, 'Evaluation committee activation requires the exact configured Chair, Secretary, voting quorum, and role composition.', 1;
                END
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER [dbo].[TR_ProcurementEvaluationCommitteeRoleRequirements_Immutable]
                ON [dbo].[ProcurementEvaluationCommitteeRoleRequirements]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (SELECT 1 FROM deleted)
                        THROW 51305, 'Evaluation committee role requirements are immutable.', 1;
                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        LEFT JOIN [dbo].[ProcurementEvaluationCommitteeControls] c
                          ON c.Id = i.CommitteeControlId AND c.TenantId = i.TenantId AND c.IsDeleted = 0
                        WHERE i.IsDeleted = 1 OR c.Id IS NULL OR c.Status <> 0)
                        THROW 51306, 'Evaluation committee role requirements require an exact Draft control in the same tenant.', 1;
                END
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER [dbo].[TR_ProcurementEvaluationCommitteeAppointments_Lifecycle]
                ON [dbo].[ProcurementEvaluationCommitteeAppointments]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id = d.Id WHERE i.Id IS NULL)
                        THROW 51310, 'Evaluation committee appointments cannot be deleted.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        LEFT JOIN [dbo].[ProcurementEvaluationCommitteeControls] c
                          ON c.Id = i.CommitteeControlId AND c.TenantId = i.TenantId AND c.IsDeleted = 0
                        LEFT JOIN [dbo].[ProcurementCommitteeMembers] cm
                          ON cm.Id = i.CommitteeMemberId AND cm.TenantId = i.TenantId AND cm.IsDeleted = 0
                        LEFT JOIN [dbo].[ProcurementResponsibilityAssignments] ra
                          ON ra.Id = i.ResponsibilityAssignmentId AND ra.TenantId = i.TenantId AND ra.IsDeleted = 0
                        WHERE i.IsDeleted = 1 OR c.Id IS NULL OR cm.Id IS NULL OR ra.Id IS NULL
                           OR cm.CommitteeId <> c.CommitteeTemplateId
                           OR cm.AssignmentId <> i.ResponsibilityAssignmentId
                           OR ra.UserId <> i.UserId OR ra.RoleName <> i.RoleName
                           OR cm.MemberKind <> i.MemberKind OR cm.IsVoting <> i.IsVoting
                           OR i.EffectiveFromUtc < cm.EffectiveFrom OR i.EffectiveFromUtc < ra.EffectiveFrom
                           OR (cm.EffectiveTo IS NOT NULL AND
                               (i.EffectiveToUtc IS NULL OR i.EffectiveToUtc > cm.EffectiveTo))
                           OR (ra.EffectiveTo IS NOT NULL AND
                               (i.EffectiveToUtc IS NULL OR i.EffectiveToUtc > ra.EffectiveTo))
                           OR (NOT EXISTS (SELECT 1 FROM deleted d WHERE d.Id = i.Id)
                               AND (cm.IsActive = 0 OR ra.IsActive = 0
                                    OR c.Status <> 0 OR i.Status <> 0)))
                        THROW 51311, 'Evaluation committee appointment member, assignment, user, role, effective-date, or tenant lineage is invalid.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        JOIN deleted d ON d.Id = i.Id
                        LEFT JOIN [dbo].[ProcurementEvaluationCommitteeControls] c
                          ON c.Id = i.CommitteeControlId AND c.TenantId = i.TenantId AND c.IsDeleted = 0
                        WHERE i.TenantId <> d.TenantId OR i.CommitteeControlId <> d.CommitteeControlId
                           OR i.CommitteeMemberId <> d.CommitteeMemberId
                           OR i.ResponsibilityAssignmentId <> d.ResponsibilityAssignmentId OR i.UserId <> d.UserId
                           OR i.UserDisplayName <> d.UserDisplayName OR i.RoleName <> d.RoleName
                           OR i.MemberKind <> d.MemberKind OR i.IsVoting <> d.IsVoting
                           OR i.EffectiveFromUtc <> d.EffectiveFromUtc
                           OR ISNULL(i.EffectiveToUtc, '99991231') <> ISNULL(d.EffectiveToUtc, '99991231')
                           OR i.CreatedAt <> d.CreatedAt OR i.IsDeleted <> d.IsDeleted
                           OR NOT ((d.Status = 0 AND i.Status IN (1, 2, 3))
                                   OR (d.Status = 1 AND i.Status IN (1, 3))
                                   OR (d.Status IN (2, 3) AND i.Status = d.Status))
                           OR (d.Status <> 0 AND
                               (ISNULL(i.AcceptedAtUtc, '19000101') <> ISNULL(d.AcceptedAtUtc, '19000101')
                                OR ISNULL(i.AcceptanceSignatureReference, '') <> ISNULL(d.AcceptanceSignatureReference, '')
                                OR ISNULL(i.AcceptanceEvidenceReference, '') <> ISNULL(d.AcceptanceEvidenceReference, '')
                                OR ISNULL(i.AcceptanceIdempotencyKey, '') <> ISNULL(d.AcceptanceIdempotencyKey, '')
                                OR ISNULL(i.StatusReason, '') <> ISNULL(d.StatusReason, '')))
                           OR (d.Status = 0 AND i.Status <> 0 AND (c.Id IS NULL OR c.Status <> 1)))
                        THROW 51312, 'Evaluation committee appointment immutable lineage or response lifecycle is invalid.', 1;
                END
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER [dbo].[TR_ProcurementEvaluationConflictDeclarations_Immutable]
                ON [dbo].[ProcurementEvaluationConflictDeclarations]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (SELECT 1 FROM deleted)
                        THROW 51320, 'Conflict-of-interest declarations are append-only.', 1;
                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        LEFT JOIN [dbo].[ProcurementEvaluationCommitteeAppointments] a
                          ON a.Id = i.AppointmentId AND a.TenantId = i.TenantId AND a.IsDeleted = 0
                        LEFT JOIN [dbo].[WorkflowEvidenceDocuments] we
                          ON we.Id = i.WorkflowEvidenceDocumentId AND we.TenantId = i.TenantId AND we.IsDeleted = 0
                        LEFT JOIN [dbo].[FileUploadRecords] fu
                          ON fu.Id = i.FileUploadRecordId AND fu.TenantId = i.TenantId AND fu.IsDeleted = 0
                        WHERE i.IsDeleted = 1 OR a.Id IS NULL OR a.Status <> 1 OR a.UserId <> i.DeclaredByUserId
                           OR (i.WorkflowEvidenceDocumentId IS NOT NULL AND we.Id IS NULL)
                           OR (i.FileUploadRecordId IS NOT NULL AND fu.Id IS NULL)
                           OR EXISTS (
                               SELECT 1
                               FROM [dbo].[ProcurementEvaluationConflictDeclarations] prior
                               WHERE prior.AppointmentId = i.AppointmentId AND prior.TenantId = i.TenantId
                                 AND prior.IsDeleted = 0 AND prior.Id <> i.Id AND prior.Version >= i.Version)
                           OR (i.Version = 1 AND EXISTS (
                               SELECT 1
                               FROM [dbo].[ProcurementEvaluationConflictDeclarations] prior
                               WHERE prior.AppointmentId = i.AppointmentId AND prior.TenantId = i.TenantId
                                 AND prior.IsDeleted = 0 AND prior.Id <> i.Id))
                           OR (i.Version > 1 AND NOT EXISTS (
                               SELECT 1
                               FROM [dbo].[ProcurementEvaluationConflictDeclarations] prior
                               WHERE prior.AppointmentId = i.AppointmentId AND prior.TenantId = i.TenantId
                                 AND prior.IsDeleted = 0 AND prior.Version = i.Version - 1)))
                        THROW 51321, 'Conflict declaration appointment, declarant, evidence, version, or tenant lineage is invalid.', 1;
                END
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER [dbo].[TR_ProcurementEvaluationMeetings_Lifecycle]
                ON [dbo].[ProcurementEvaluationMeetings]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id = d.Id WHERE i.Id IS NULL)
                        THROW 51330, 'Evaluation meetings cannot be deleted.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        LEFT JOIN [dbo].[ProcurementEvaluationCommitteeControls] c
                          ON c.Id = i.CommitteeControlId AND c.TenantId = i.TenantId AND c.IsDeleted = 0
                        WHERE i.IsDeleted = 1 OR c.Id IS NULL
                           OR UPPER(LTRIM(RTRIM(i.MeetingMode))) NOT IN ('INPERSON', 'REMOTE', 'HYBRID')
                           OR (UPPER(LTRIM(RTRIM(i.MeetingMode))) IN ('REMOTE', 'HYBRID')
                               AND LEN(LTRIM(RTRIM(ISNULL(i.RemoteMeetingEvidenceReference, '')))) = 0)
                           OR (NOT EXISTS (SELECT 1 FROM deleted d WHERE d.Id = i.Id)
                               AND (c.Status <> 1 OR c.EffectiveFromUtc > SYSUTCDATETIME()
                                    OR (c.EffectiveToUtc IS NOT NULL AND c.EffectiveToUtc < SYSUTCDATETIME())
                                    OR i.Status <> 0)))
                        THROW 51331, 'Evaluation meeting control, mode, evidence, active period, or tenant lineage is invalid.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        JOIN deleted d ON d.Id = i.Id
                        WHERE i.TenantId <> d.TenantId OR i.CommitteeControlId <> d.CommitteeControlId
                           OR i.Sequence <> d.Sequence OR i.Phase <> d.Phase
                           OR i.MeetingMode <> d.MeetingMode OR i.MeetingChannel <> d.MeetingChannel
                           OR i.ScheduledAtUtc <> d.ScheduledAtUtc OR i.IdempotencyKey <> d.IdempotencyKey
                           OR i.CreatedAt <> d.CreatedAt OR i.IsDeleted <> d.IsDeleted
                           OR NOT ((d.Status IN (0, 2) AND i.Status IN (0, 1, 2))
                                   OR (d.Status = 1 AND i.Status IN (1, 3))
                                   OR (d.Status = 3 AND i.Status = 3))
                           OR (d.Status IN (1, 3) AND
                               (ISNULL(i.StartedAtUtc, '19000101') <> ISNULL(d.StartedAtUtc, '19000101')
                                OR i.EligibleVotingMemberCount <> d.EligibleVotingMemberCount
                                OR i.SignedVotingAttendanceCount <> d.SignedVotingAttendanceCount
                                OR i.ChairPresent <> d.ChairPresent OR i.SecretaryPresent <> d.SecretaryPresent
                                OR i.QuorumMet <> d.QuorumMet OR i.EvidenceReference <> d.EvidenceReference
                                OR ISNULL(i.RemoteMeetingEvidenceReference, '') <> ISNULL(d.RemoteMeetingEvidenceReference, '')
                                OR ISNULL(i.QuorumIdempotencyKey, '') <> ISNULL(d.QuorumIdempotencyKey, '')
                                OR i.QuorumSnapshotJson <> d.QuorumSnapshotJson
                                OR i.QuorumIntegrityHash <> d.QuorumIntegrityHash))
                           OR (d.Status = 3 AND
                               ISNULL(i.ClosedAtUtc, '19000101') <> ISNULL(d.ClosedAtUtc, '19000101'))
                           OR (d.Status = 0 AND i.Status = 0 AND
                               (ISNULL(i.StartedAtUtc, '19000101') <> ISNULL(d.StartedAtUtc, '19000101')
                                OR ISNULL(i.ClosedAtUtc, '19000101') <> ISNULL(d.ClosedAtUtc, '19000101')
                                OR i.EligibleVotingMemberCount <> d.EligibleVotingMemberCount
                                OR i.SignedVotingAttendanceCount <> d.SignedVotingAttendanceCount
                                OR i.ChairPresent <> d.ChairPresent OR i.SecretaryPresent <> d.SecretaryPresent
                                OR i.QuorumMet <> d.QuorumMet OR i.EvidenceReference <> d.EvidenceReference
                                OR ISNULL(i.RemoteMeetingEvidenceReference, '') <> ISNULL(d.RemoteMeetingEvidenceReference, '')
                                OR ISNULL(i.QuorumIdempotencyKey, '') <> ISNULL(d.QuorumIdempotencyKey, '')
                                OR i.QuorumSnapshotJson <> d.QuorumSnapshotJson
                                OR i.QuorumIntegrityHash <> d.QuorumIntegrityHash)))
                        THROW 51332, 'Evaluation meeting immutable lineage or lifecycle transition is invalid.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        JOIN deleted d ON d.Id = i.Id
                        JOIN [dbo].[ProcurementEvaluationCommitteeControls] c
                          ON c.Id = i.CommitteeControlId AND c.TenantId = i.TenantId
                        WHERE d.Status IN (0, 2) AND i.Status IN (1, 2)
                          AND (
                              i.EligibleVotingMemberCount <>
                                (SELECT COUNT(*)
                                 FROM [dbo].[ProcurementEvaluationCommitteeAppointments] a
                                 WHERE a.CommitteeControlId = i.CommitteeControlId AND a.TenantId = i.TenantId
                                   AND a.IsDeleted = 0 AND a.Status = 1 AND a.IsVoting = 1
                                   AND a.EffectiveFromUtc <= SYSUTCDATETIME()
                                   AND (a.EffectiveToUtc IS NULL OR a.EffectiveToUtc >= SYSUTCDATETIME())
                                   AND EXISTS (
                                       SELECT 1
                                       FROM [dbo].[ProcurementEvaluationConflictDeclarations] cd
                                       WHERE cd.AppointmentId = a.Id AND cd.TenantId = a.TenantId AND cd.IsDeleted = 0
                                         AND cd.Outcome = 0 AND cd.ValidFromUtc <= SYSUTCDATETIME()
                                         AND (cd.ValidToUtc IS NULL OR cd.ValidToUtc >= SYSUTCDATETIME())
                                         AND cd.Version = (
                                             SELECT MAX(cd2.Version)
                                             FROM [dbo].[ProcurementEvaluationConflictDeclarations] cd2
                                             WHERE cd2.AppointmentId = a.Id AND cd2.TenantId = a.TenantId AND cd2.IsDeleted = 0
                                               AND cd2.ValidFromUtc <= SYSUTCDATETIME()
                                               AND (cd2.ValidToUtc IS NULL OR cd2.ValidToUtc >= SYSUTCDATETIME()))))
                              OR i.SignedVotingAttendanceCount <>
                                (SELECT COUNT(*)
                                 FROM [dbo].[ProcurementEvaluationAttendanceRecords] ar
                                 JOIN [dbo].[ProcurementEvaluationCommitteeAppointments] a
                                   ON a.Id = ar.AppointmentId AND a.TenantId = ar.TenantId
                                 WHERE ar.MeetingId = i.Id AND ar.TenantId = i.TenantId AND ar.IsDeleted = 0
                                   AND ar.IsPresent = 1 AND ar.WasEligibleAtSignature = 1
                                   AND ar.SignedAtUtc IS NOT NULL AND a.IsDeleted = 0 AND a.Status = 1 AND a.IsVoting = 1
                                   AND a.EffectiveFromUtc <= SYSUTCDATETIME()
                                   AND (a.EffectiveToUtc IS NULL OR a.EffectiveToUtc >= SYSUTCDATETIME())
                                   AND EXISTS (
                                       SELECT 1
                                       FROM [dbo].[ProcurementEvaluationConflictDeclarations] cd
                                       WHERE cd.AppointmentId = a.Id AND cd.TenantId = a.TenantId AND cd.IsDeleted = 0
                                         AND cd.Outcome = 0 AND cd.ValidFromUtc <= SYSUTCDATETIME()
                                         AND (cd.ValidToUtc IS NULL OR cd.ValidToUtc >= SYSUTCDATETIME())
                                         AND cd.Version = (
                                             SELECT MAX(cd2.Version)
                                             FROM [dbo].[ProcurementEvaluationConflictDeclarations] cd2
                                             WHERE cd2.AppointmentId = a.Id AND cd2.TenantId = a.TenantId AND cd2.IsDeleted = 0
                                               AND cd2.ValidFromUtc <= SYSUTCDATETIME()
                                               AND (cd2.ValidToUtc IS NULL OR cd2.ValidToUtc >= SYSUTCDATETIME()))))
                              OR i.ChairPresent <>
                                CONVERT(bit, CASE WHEN EXISTS (
                                    SELECT 1
                                    FROM [dbo].[ProcurementEvaluationAttendanceRecords] ar
                                    JOIN [dbo].[ProcurementEvaluationCommitteeAppointments] a
                                      ON a.Id = ar.AppointmentId AND a.TenantId = ar.TenantId
                                    WHERE ar.MeetingId = i.Id AND ar.TenantId = i.TenantId AND ar.IsDeleted = 0
                                      AND ar.IsPresent = 1 AND ar.WasEligibleAtSignature = 1
                                      AND a.IsDeleted = 0 AND a.Status = 1 AND a.MemberKind = 0
                                      AND a.EffectiveFromUtc <= SYSUTCDATETIME()
                                      AND (a.EffectiveToUtc IS NULL OR a.EffectiveToUtc >= SYSUTCDATETIME())
                                      AND EXISTS (
                                          SELECT 1
                                          FROM [dbo].[ProcurementEvaluationConflictDeclarations] cd
                                          WHERE cd.AppointmentId = a.Id AND cd.TenantId = a.TenantId AND cd.IsDeleted = 0
                                            AND cd.Outcome = 0 AND cd.ValidFromUtc <= SYSUTCDATETIME()
                                            AND (cd.ValidToUtc IS NULL OR cd.ValidToUtc >= SYSUTCDATETIME())
                                            AND cd.Version = (
                                                SELECT MAX(cd2.Version)
                                                FROM [dbo].[ProcurementEvaluationConflictDeclarations] cd2
                                                WHERE cd2.AppointmentId = a.Id AND cd2.TenantId = a.TenantId AND cd2.IsDeleted = 0
                                                  AND cd2.ValidFromUtc <= SYSUTCDATETIME()
                                                  AND (cd2.ValidToUtc IS NULL OR cd2.ValidToUtc >= SYSUTCDATETIME())))) THEN 1 ELSE 0 END)
                              OR i.SecretaryPresent <>
                                CONVERT(bit, CASE WHEN EXISTS (
                                    SELECT 1
                                    FROM [dbo].[ProcurementEvaluationAttendanceRecords] ar
                                    JOIN [dbo].[ProcurementEvaluationCommitteeAppointments] a
                                      ON a.Id = ar.AppointmentId AND a.TenantId = ar.TenantId
                                    WHERE ar.MeetingId = i.Id AND ar.TenantId = i.TenantId AND ar.IsDeleted = 0
                                      AND ar.IsPresent = 1 AND ar.WasEligibleAtSignature = 1
                                      AND a.IsDeleted = 0 AND a.Status = 1 AND a.MemberKind = 4
                                      AND a.EffectiveFromUtc <= SYSUTCDATETIME()
                                      AND (a.EffectiveToUtc IS NULL OR a.EffectiveToUtc >= SYSUTCDATETIME())
                                      AND EXISTS (
                                          SELECT 1
                                          FROM [dbo].[ProcurementEvaluationConflictDeclarations] cd
                                          WHERE cd.AppointmentId = a.Id AND cd.TenantId = a.TenantId AND cd.IsDeleted = 0
                                            AND cd.Outcome = 0 AND cd.ValidFromUtc <= SYSUTCDATETIME()
                                            AND (cd.ValidToUtc IS NULL OR cd.ValidToUtc >= SYSUTCDATETIME())
                                            AND cd.Version = (
                                                SELECT MAX(cd2.Version)
                                                FROM [dbo].[ProcurementEvaluationConflictDeclarations] cd2
                                                WHERE cd2.AppointmentId = a.Id AND cd2.TenantId = a.TenantId AND cd2.IsDeleted = 0
                                                  AND cd2.ValidFromUtc <= SYSUTCDATETIME()
                                                  AND (cd2.ValidToUtc IS NULL OR cd2.ValidToUtc >= SYSUTCDATETIME())))) THEN 1 ELSE 0 END)
                              OR (i.Status = 1 AND
                                  (i.SignedVotingAttendanceCount < c.RequiredQuorum
                                   OR i.ChairPresent = 0 OR i.SecretaryPresent = 0
                                   OR EXISTS (
                                       SELECT 1
                                       FROM [dbo].[ProcurementEvaluationCommitteeRoleRequirements] rr
                                       WHERE rr.CommitteeControlId = i.CommitteeControlId
                                         AND rr.TenantId = i.TenantId AND rr.IsDeleted = 0 AND rr.IsRequiredForQuorum = 1
                                         AND (SELECT COUNT(*)
                                              FROM [dbo].[ProcurementEvaluationAttendanceRecords] ar
                                              JOIN [dbo].[ProcurementEvaluationCommitteeAppointments] a
                                                ON a.Id = ar.AppointmentId AND a.TenantId = ar.TenantId
                                              WHERE ar.MeetingId = i.Id AND ar.TenantId = i.TenantId AND ar.IsDeleted = 0
                                                AND ar.IsPresent = 1 AND ar.WasEligibleAtSignature = 1
                                                AND a.IsDeleted = 0 AND a.Status = 1
                                                AND a.EffectiveFromUtc <= SYSUTCDATETIME()
                                                AND (a.EffectiveToUtc IS NULL OR a.EffectiveToUtc >= SYSUTCDATETIME())
                                                AND a.MemberKind = rr.MemberKind
                                                AND UPPER(LTRIM(RTRIM(a.RoleName))) = UPPER(LTRIM(RTRIM(rr.RoleName)))
                                                AND (rr.IsVoting = 0 OR a.IsVoting = 1)
                                                AND EXISTS (
                                                    SELECT 1
                                                    FROM [dbo].[ProcurementEvaluationConflictDeclarations] cd
                                                    WHERE cd.AppointmentId = a.Id AND cd.TenantId = a.TenantId AND cd.IsDeleted = 0
                                                      AND cd.Outcome = 0 AND cd.ValidFromUtc <= SYSUTCDATETIME()
                                                      AND (cd.ValidToUtc IS NULL OR cd.ValidToUtc >= SYSUTCDATETIME())
                                                      AND cd.Version = (
                                                          SELECT MAX(cd2.Version)
                                                          FROM [dbo].[ProcurementEvaluationConflictDeclarations] cd2
                                                          WHERE cd2.AppointmentId = a.Id AND cd2.TenantId = a.TenantId AND cd2.IsDeleted = 0
                                                            AND cd2.ValidFromUtc <= SYSUTCDATETIME()
                                                            AND (cd2.ValidToUtc IS NULL OR cd2.ValidToUtc >= SYSUTCDATETIME())))) < rr.MinimumCount))
                              OR (i.Status = 2 AND i.QuorumMet = 1))))
                        THROW 51333, 'Evaluation meeting quorum values do not match signed, eligible attendance and required committee roles.', 1;
                END
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER [dbo].[TR_ProcurementEvaluationAttendanceRecords_Immutable]
                ON [dbo].[ProcurementEvaluationAttendanceRecords]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (SELECT 1 FROM deleted)
                        THROW 51340, 'Evaluation attendance records are append-only.', 1;
                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        LEFT JOIN [dbo].[ProcurementEvaluationMeetings] m
                          ON m.Id = i.MeetingId AND m.TenantId = i.TenantId AND m.IsDeleted = 0
                        LEFT JOIN [dbo].[ProcurementEvaluationCommitteeAppointments] a
                          ON a.Id = i.AppointmentId AND a.TenantId = i.TenantId AND a.IsDeleted = 0
                        WHERE i.IsDeleted = 1 OR m.Id IS NULL OR a.Id IS NULL
                           OR m.Status NOT IN (0, 2) OR a.CommitteeControlId <> m.CommitteeControlId
                           OR a.Status <> 1 OR a.EffectiveFromUtc > i.SignedAtUtc
                           OR (a.EffectiveToUtc IS NOT NULL AND a.EffectiveToUtc < i.SignedAtUtc)
                           OR i.WasEligibleAtSignature = 0
                           OR NOT EXISTS (
                               SELECT 1
                               FROM [dbo].[ProcurementEvaluationConflictDeclarations] cd
                               WHERE cd.AppointmentId = a.Id AND cd.TenantId = a.TenantId AND cd.IsDeleted = 0
                                 AND cd.Outcome = 0 AND cd.ValidFromUtc <= i.SignedAtUtc
                                 AND (cd.ValidToUtc IS NULL OR cd.ValidToUtc >= i.SignedAtUtc)
                                 AND cd.Version = (
                                     SELECT MAX(cd2.Version)
                                     FROM [dbo].[ProcurementEvaluationConflictDeclarations] cd2
                                     WHERE cd2.AppointmentId = a.Id AND cd2.TenantId = a.TenantId AND cd2.IsDeleted = 0
                                       AND cd2.ValidFromUtc <= i.SignedAtUtc
                                       AND (cd2.ValidToUtc IS NULL OR cd2.ValidToUtc >= i.SignedAtUtc))))
                        THROW 51341, 'Evaluation attendance meeting, appointment, signed eligibility, conflict declaration, or tenant lineage is invalid.', 1;
                END
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER [dbo].[TR_ProcurementEvaluationScoreSheets_Immutable]
                ON [dbo].[ProcurementEvaluationScoreSheets]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (SELECT 1 FROM deleted)
                        THROW 51350, 'Evaluation score sheets are append-only.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        LEFT JOIN [dbo].[ProcurementEvaluationCommitteeControls] c
                          ON c.Id = i.CommitteeControlId AND c.TenantId = i.TenantId AND c.IsDeleted = 0
                        LEFT JOIN [dbo].[ProcurementEvaluationMeetings] m
                          ON m.Id = i.MeetingId AND m.TenantId = i.TenantId AND m.IsDeleted = 0
                        LEFT JOIN [dbo].[ProcurementEvaluationCommitteeAppointments] a
                          ON a.Id = i.AppointmentId AND a.TenantId = i.TenantId AND a.IsDeleted = 0
                        LEFT JOIN [dbo].[ProcurementEvaluationAttendanceRecords] ar
                          ON ar.MeetingId = i.MeetingId AND ar.AppointmentId = i.AppointmentId
                         AND ar.TenantId = i.TenantId AND ar.IsDeleted = 0
                        WHERE i.IsDeleted = 1 OR c.Id IS NULL OR m.Id IS NULL OR a.Id IS NULL OR ar.Id IS NULL
                           OR c.Status <> 1 OR c.EffectiveFromUtc > i.SubmittedAtUtc
                           OR (c.EffectiveToUtc IS NOT NULL AND c.EffectiveToUtc < i.SubmittedAtUtc)
                           OR m.CommitteeControlId <> c.Id OR m.Phase <> i.Phase
                           OR m.Status <> 1 OR m.QuorumMet = 0
                           OR a.CommitteeControlId <> c.Id OR a.Status <> 1
                           OR a.UserId <> i.SubmittedByUserId
                           OR a.EffectiveFromUtc > i.SubmittedAtUtc
                           OR (a.EffectiveToUtc IS NOT NULL AND a.EffectiveToUtc < i.SubmittedAtUtc)
                           OR ar.IsPresent = 0 OR ar.WasEligibleAtSignature = 0 OR ar.SignedAtUtc IS NULL
                           OR NOT EXISTS (
                               SELECT 1
                               FROM [dbo].[ProcurementEvaluationConflictDeclarations] cd
                               WHERE cd.AppointmentId = a.Id AND cd.TenantId = a.TenantId AND cd.IsDeleted = 0
                                 AND cd.Outcome = 0 AND cd.ValidFromUtc <= i.SubmittedAtUtc
                                 AND (cd.ValidToUtc IS NULL OR cd.ValidToUtc >= i.SubmittedAtUtc)
                                 AND cd.Version = (
                                     SELECT MAX(cd2.Version)
                                     FROM [dbo].[ProcurementEvaluationConflictDeclarations] cd2
                                     WHERE cd2.AppointmentId = a.Id AND cd2.TenantId = a.TenantId AND cd2.IsDeleted = 0
                                       AND cd2.ValidFromUtc <= i.SubmittedAtUtc
                                       AND (cd2.ValidToUtc IS NULL OR cd2.ValidToUtc >= i.SubmittedAtUtc))))
                        THROW 51351, 'Evaluation score-sheet committee, quorum, scorer, attendance, conflict declaration, or tenant lineage is invalid.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        JOIN [dbo].[ProcurementEvaluationCommitteeControls] c
                          ON c.Id = i.CommitteeControlId AND c.TenantId = i.TenantId
                        LEFT JOIN [dbo].[TenderBids] tb
                          ON i.ScoreSubjectType = 'TenderEvaluation' AND tb.Id = i.ScoreSubjectId
                         AND tb.TenantId = i.TenantId AND tb.IsDeleted = 0
                        LEFT JOIN [dbo].[TenderEvaluators] te
                          ON i.ScoreSubjectType = 'TenderEvaluation' AND te.TenderId = c.SourceId
                         AND te.UserId = i.SubmittedByUserId AND te.TenantId = i.TenantId AND te.IsDeleted = 0
                        LEFT JOIN [dbo].[ProcurementRfqEvaluations] re
                          ON i.ScoreSubjectType = 'ProcurementRfqEvaluation' AND re.Id = i.ScoreSubjectId
                         AND re.TenantId = i.TenantId AND re.IsDeleted = 0
                        WHERE NOT (
                            (c.SourceType = 0 AND i.ScoreSubjectType = 'ProcurementTenderControl'
                             AND i.ScoreSubjectId = c.SourceId AND i.Phase IN (0, 1))
                            OR
                            (c.SourceType = 0 AND i.ScoreSubjectType = 'TenderEvaluation'
                             AND i.Phase = 2 AND tb.Id IS NOT NULL AND tb.TenderId = c.SourceId
                             AND te.Id IS NOT NULL AND UPPER(te.Status) <> 'DECLINED'
                             AND TRY_CONVERT(uniqueidentifier,
                                 JSON_VALUE(i.ScoreSnapshotJson, '$.TenderEvaluatorId')) = te.Id)
                            OR
                            (c.SourceType = 1 AND i.ScoreSubjectType = 'ProcurementRfqEvaluation'
                             AND i.Phase = 2 AND re.Id IS NOT NULL AND re.RfqId = c.SourceId)))
                        THROW 51352, 'Evaluation score-sheet subject type, source, phase, scorer assignment, or tenant lineage is invalid.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        WHERE (i.Attempt = 1 AND EXISTS (
                                  SELECT 1 FROM [dbo].[ProcurementEvaluationScoreSheets] p
                                  WHERE p.TenantId = i.TenantId AND p.CommitteeControlId = i.CommitteeControlId
                                    AND p.Phase = i.Phase AND p.AppointmentId = i.AppointmentId
                                    AND p.ScoreSubjectType = i.ScoreSubjectType AND p.ScoreSubjectId = i.ScoreSubjectId
                                    AND p.IsDeleted = 0 AND p.Id <> i.Id))
                           OR (i.Attempt > 1 AND NOT EXISTS (
                                  SELECT 1
                                  FROM [dbo].[ProcurementEvaluationScoreSheets] p
                                  JOIN [dbo].[ProcurementEvaluationScoreRecalls] r
                                    ON r.ScoreSheetId = p.Id AND r.TenantId = p.TenantId
                                   AND r.IsDeleted = 0 AND r.Status = 1
                                   AND r.AuthorizedNewAttempt = i.Attempt
                                  WHERE p.TenantId = i.TenantId AND p.CommitteeControlId = i.CommitteeControlId
                                    AND p.Phase = i.Phase AND p.AppointmentId = i.AppointmentId
                                    AND p.ScoreSubjectType = i.ScoreSubjectType AND p.ScoreSubjectId = i.ScoreSubjectId
                                    AND p.IsDeleted = 0 AND p.Attempt = i.Attempt - 1
                                    AND NOT EXISTS (
                                        SELECT 1 FROM [dbo].[ProcurementEvaluationScoreSheets] newer
                                        WHERE newer.TenantId = p.TenantId
                                          AND newer.CommitteeControlId = p.CommitteeControlId
                                          AND newer.Phase = p.Phase AND newer.AppointmentId = p.AppointmentId
                                          AND newer.ScoreSubjectType = p.ScoreSubjectType
                                          AND newer.ScoreSubjectId = p.ScoreSubjectId
                                          AND newer.IsDeleted = 0 AND newer.Attempt > p.Attempt AND newer.Id <> i.Id))))
                        THROW 51353, 'Evaluation score-sheet attempts require an exact approved recall of the immediately preceding locked attempt.', 1;
                END
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER [dbo].[TR_ProcurementEvaluationScoreRecalls_Lifecycle]
                ON [dbo].[ProcurementEvaluationScoreRecalls]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id = d.Id WHERE i.Id IS NULL)
                        THROW 51360, 'Evaluation score recalls cannot be deleted.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        LEFT JOIN [dbo].[ProcurementEvaluationScoreSheets] s
                          ON s.Id = i.ScoreSheetId AND s.TenantId = i.TenantId AND s.IsDeleted = 0
                        LEFT JOIN [dbo].[WorkflowDefinitions] wd
                          ON wd.Id = i.WorkflowDefinitionId AND wd.TenantId = i.TenantId AND wd.IsDeleted = 0
                        LEFT JOIN [dbo].[WorkflowInstances] wi
                          ON wi.Id = i.WorkflowInstanceId AND wi.TenantId = i.TenantId AND wi.IsDeleted = 0
                        LEFT JOIN [dbo].[WorkflowEvidenceDocuments] we
                          ON we.Id = i.WorkflowEvidenceDocumentId AND we.TenantId = i.TenantId AND we.IsDeleted = 0
                        LEFT JOIN [dbo].[FileUploadRecords] fu
                          ON fu.Id = i.FileUploadRecordId AND fu.TenantId = i.TenantId AND fu.IsDeleted = 0
                        WHERE i.IsDeleted = 1 OR s.Id IS NULL OR wd.Id IS NULL
                           OR wd.LifecycleStatus <> 1 OR wd.IsActive = 0
                           OR i.RequestedByUserId <> s.SubmittedByUserId
                           OR (i.WorkflowInstanceId IS NOT NULL AND
                               (wi.Id IS NULL OR wi.WorkflowDefinitionId <> i.WorkflowDefinitionId
                                OR wi.EntityId <> i.Id OR wi.EntityTypeId <> wd.EntityTypeId))
                           OR (i.WorkflowEvidenceDocumentId IS NOT NULL AND we.Id IS NULL)
                           OR (i.FileUploadRecordId IS NOT NULL AND fu.Id IS NULL))
                        THROW 51361, 'Evaluation score recall sheet, requester, workflow, evidence, or tenant lineage is invalid.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        JOIN deleted d ON d.Id = i.Id
                        WHERE i.TenantId <> d.TenantId OR i.ScoreSheetId <> d.ScoreSheetId
                           OR i.Reason <> d.Reason OR i.EvidenceReference <> d.EvidenceReference
                           OR ISNULL(i.WorkflowEvidenceDocumentId, '00000000-0000-0000-0000-000000000000')
                              <> ISNULL(d.WorkflowEvidenceDocumentId, '00000000-0000-0000-0000-000000000000')
                           OR ISNULL(i.FileUploadRecordId, '00000000-0000-0000-0000-000000000000')
                              <> ISNULL(d.FileUploadRecordId, '00000000-0000-0000-0000-000000000000')
                           OR i.WorkflowDefinitionId <> d.WorkflowDefinitionId
                           OR i.RequestedByUserId <> d.RequestedByUserId OR i.RequestedByName <> d.RequestedByName
                           OR i.RequestedAtUtc <> d.RequestedAtUtc OR i.IdempotencyKey <> d.IdempotencyKey
                           OR i.CreatedAt <> d.CreatedAt OR i.IsDeleted <> d.IsDeleted
                           OR NOT (
                               (d.Status = 0 AND i.Status = 0
                                AND d.WorkflowInstanceId IS NULL AND i.WorkflowInstanceId IS NOT NULL)
                               OR (d.Status = 0 AND i.Status IN (1, 2))
                               OR (d.Status IN (1, 2) AND i.Status = d.Status))
                           OR (d.Status = 0 AND i.Status = 0 AND
                               (i.DecidedByUserId IS NOT NULL OR i.DecidedAtUtc IS NOT NULL
                                OR i.DecisionReference IS NOT NULL OR i.DecisionEvidenceReference IS NOT NULL
                                OR i.DecisionIdempotencyKey IS NOT NULL OR i.AuthorizedNewAttempt IS NOT NULL))
                           OR (d.Status IN (1, 2) AND
                               (ISNULL(i.WorkflowInstanceId, '00000000-0000-0000-0000-000000000000')
                                  <> ISNULL(d.WorkflowInstanceId, '00000000-0000-0000-0000-000000000000')
                                OR ISNULL(i.DecidedByUserId, '00000000-0000-0000-0000-000000000000')
                                  <> ISNULL(d.DecidedByUserId, '00000000-0000-0000-0000-000000000000')
                                OR ISNULL(i.DecidedAtUtc, '19000101') <> ISNULL(d.DecidedAtUtc, '19000101')
                                OR ISNULL(i.DecisionReference, '') <> ISNULL(d.DecisionReference, '')
                                OR ISNULL(i.DecisionEvidenceReference, '') <> ISNULL(d.DecisionEvidenceReference, '')
                                OR ISNULL(i.DecisionIdempotencyKey, '') <> ISNULL(d.DecisionIdempotencyKey, '')
                                OR ISNULL(i.AuthorizedNewAttempt, 0) <> ISNULL(d.AuthorizedNewAttempt, 0)
                                OR i.SnapshotJson <> d.SnapshotJson OR i.IntegrityHash <> d.IntegrityHash)))
                        THROW 51362, 'Evaluation score recall immutable lineage or lifecycle transition is invalid.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        JOIN deleted d ON d.Id = i.Id
                        JOIN [dbo].[ProcurementEvaluationScoreSheets] s
                          ON s.Id = i.ScoreSheetId AND s.TenantId = i.TenantId
                        LEFT JOIN [dbo].[WorkflowInstances] wi
                          ON wi.Id = i.WorkflowInstanceId AND wi.TenantId = i.TenantId AND wi.IsDeleted = 0
                        WHERE d.Status = 0 AND i.Status IN (1, 2)
                          AND (
                              wi.Id IS NULL
                              OR (i.Status = 1 AND wi.Status <> 2)
                              OR (i.Status = 2 AND wi.Status NOT IN (3, 4))
                              OR i.DecidedByUserId IN (i.RequestedByUserId, s.SubmittedByUserId)
                              OR (i.Status = 1 AND i.AuthorizedNewAttempt <> s.Attempt + 1)
                              OR (i.Status = 2 AND i.AuthorizedNewAttempt IS NOT NULL)
                              OR NOT EXISTS (
                                  SELECT 1
                                  FROM [dbo].[WorkflowStepInstances] wsi
                                  JOIN [dbo].[WorkflowApprovals] wa
                                    ON wa.StepInstanceId = wsi.Id AND wa.TenantId = i.TenantId AND wa.IsDeleted = 0
                                  WHERE wsi.WorkflowInstanceId = wi.Id AND wsi.TenantId = i.TenantId AND wsi.IsDeleted = 0
                                    AND wa.ProcessedDate IS NOT NULL
                                    AND wa.Status NOT IN (0, 6))
                              OR EXISTS (
                                  SELECT 1
                                  FROM [dbo].[WorkflowStepInstances] wsi
                                  JOIN [dbo].[WorkflowApprovals] wa
                                    ON wa.StepInstanceId = wsi.Id AND wa.TenantId = i.TenantId AND wa.IsDeleted = 0
                                  WHERE wsi.WorkflowInstanceId = wi.Id AND wsi.TenantId = i.TenantId AND wsi.IsDeleted = 0
                                    AND wa.ProcessedDate IS NOT NULL
                                    AND ISNULL(wa.ProcessedById, wa.ApproverId) IN (i.RequestedByUserId, s.SubmittedByUserId))))
                        THROW 51363, 'Evaluation score recall outcome is not supported by the exact independent shared-workflow decision.', 1;
                END
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER [dbo].[TR_TenderEvaluations_CommitteeScoreProjection]
                ON [dbo].[TenderEvaluations]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS (
                        SELECT 1
                        FROM deleted d
                        LEFT JOIN inserted i ON i.Id = d.Id
                        WHERE i.Id IS NULL AND d.Status = 'Submitted')
                        THROW 51370, 'Submitted tender-evaluation projections are immutable and cannot be deleted.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        JOIN deleted d ON d.Id = i.Id
                        WHERE d.Status = 'Submitted')
                        THROW 51371, 'Submitted tender-evaluation projections are immutable; an approved recall requires a new Draft projection.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        LEFT JOIN deleted d ON d.Id = i.Id
                        LEFT JOIN [dbo].[TenderBids] b
                          ON b.Id = i.TenderBidId AND b.TenantId = i.TenantId AND b.IsDeleted = 0
                        LEFT JOIN [dbo].[TenderEvaluators] e
                          ON e.Id = i.TenderEvaluatorId AND e.TenantId = i.TenantId AND e.IsDeleted = 0
                        WHERE i.Status = 'Submitted'
                          AND (
                              b.Id IS NULL OR e.Id IS NULL OR e.TenderId <> b.TenderId
                              OR NOT EXISTS (
                                  SELECT 1
                                  FROM [dbo].[ProcurementEvaluationScoreSheets] s
                                  JOIN [dbo].[ProcurementEvaluationCommitteeControls] c
                                    ON c.Id = s.CommitteeControlId AND c.TenantId = s.TenantId
                                  JOIN [dbo].[ProcurementEvaluationCommitteeAppointments] a
                                    ON a.Id = s.AppointmentId AND a.TenantId = s.TenantId
                                  WHERE s.TenantId = i.TenantId AND s.IsDeleted = 0 AND s.Status = 0
                                    AND c.SourceType = 0 AND c.SourceId = b.TenderId AND c.Status = 1
                                    AND s.Phase = 2 AND s.ScoreSubjectType = 'TenderEvaluation'
                                    AND s.ScoreSubjectId = i.TenderBidId
                                    AND a.UserId = e.UserId AND s.SubmittedByUserId = e.UserId
                                    AND TRY_CONVERT(uniqueidentifier, JSON_VALUE(s.ScoreSnapshotJson, '$.evaluationId')) = i.Id
                                    AND TRY_CONVERT(uniqueidentifier, JSON_VALUE(s.ScoreSnapshotJson, '$.TenderBidId')) = i.TenderBidId
                                    AND TRY_CONVERT(uniqueidentifier, JSON_VALUE(s.ScoreSnapshotJson, '$.TenderEvaluatorId')) = i.TenderEvaluatorId
                                    AND JSON_VALUE(s.ScoreSnapshotJson, '$.status') = 'Submitted'
                                    AND TRY_CONVERT(datetime2, JSON_VALUE(s.ScoreSnapshotJson, '$.submittedAtUtc')) = i.SubmittedDate
                                    AND TRY_CONVERT(uniqueidentifier, JSON_VALUE(s.ScoreSnapshotJson, '$.evaluatorUserId')) = e.UserId
                                    AND ISNULL(TRY_CONVERT(decimal(5,2), JSON_VALUE(s.ScoreSnapshotJson, '$.PriceScore')), -1)
                                        = ISNULL(i.PriceScore, -1)
                                    AND ISNULL(TRY_CONVERT(decimal(5,2), JSON_VALUE(s.ScoreSnapshotJson, '$.QualityScore')), -1)
                                        = ISNULL(i.QualityScore, -1)
                                    AND ISNULL(TRY_CONVERT(decimal(5,2), JSON_VALUE(s.ScoreSnapshotJson, '$.DeliveryScore')), -1)
                                        = ISNULL(i.DeliveryScore, -1)
                                    AND ISNULL(TRY_CONVERT(decimal(5,2), JSON_VALUE(s.ScoreSnapshotJson, '$.ExperienceScore')), -1)
                                        = ISNULL(i.ExperienceScore, -1)
                                    AND ISNULL(TRY_CONVERT(decimal(5,2), JSON_VALUE(s.ScoreSnapshotJson, '$.TechnicalScore')), -1)
                                        = ISNULL(i.TechnicalScore, -1)
                                    AND ISNULL(TRY_CONVERT(decimal(5,2), JSON_VALUE(s.ScoreSnapshotJson, '$.ComplianceScore')), -1)
                                        = ISNULL(i.ComplianceScore, -1)
                                    AND ISNULL(TRY_CONVERT(decimal(5,2), JSON_VALUE(s.ScoreSnapshotJson, '$.TotalScore')), -1)
                                        = ISNULL(i.TotalScore, -1)
                                    AND ISNULL(JSON_VALUE(s.ScoreSnapshotJson, '$.EvaluationCriteriaJson'), '')
                                        = ISNULL(i.EvaluationCriteriaJson, '')
                                    AND ISNULL(JSON_VALUE(s.ScoreSnapshotJson, '$.TechnicalComments'), '')
                                        = ISNULL(i.TechnicalComments, '')
                                    AND ISNULL(JSON_VALUE(s.ScoreSnapshotJson, '$.CommercialComments'), '')
                                        = ISNULL(i.CommercialComments, '')
                                    AND ISNULL(JSON_VALUE(s.ScoreSnapshotJson, '$.OverallComments'), '')
                                        = ISNULL(i.OverallComments, '')
                                    AND CASE JSON_VALUE(s.ScoreSnapshotJson, '$.IsRecommended')
                                          WHEN 'true' THEN CONVERT(bit, 1) ELSE CONVERT(bit, 0) END = i.IsRecommended
                                    AND ISNULL(JSON_VALUE(s.ScoreSnapshotJson, '$.Recommendation'), '')
                                        = ISNULL(i.Recommendation, '')
                                    AND NOT EXISTS (
                                        SELECT 1
                                        FROM [dbo].[ProcurementEvaluationScoreSheets] newer
                                        WHERE newer.TenantId = s.TenantId
                                          AND newer.CommitteeControlId = s.CommitteeControlId
                                          AND newer.Phase = s.Phase AND newer.AppointmentId = s.AppointmentId
                                          AND newer.ScoreSubjectType = s.ScoreSubjectType
                                          AND newer.ScoreSubjectId = s.ScoreSubjectId
                                          AND newer.IsDeleted = 0 AND newer.Attempt > s.Attempt)
                                    AND NOT EXISTS (
                                        SELECT 1
                                        FROM [dbo].[ProcurementEvaluationScoreRecalls] recall
                                        WHERE recall.ScoreSheetId = s.Id AND recall.TenantId = s.TenantId
                                          AND recall.IsDeleted = 0 AND recall.Status IN (0, 1)))))
                        THROW 51372, 'Submitted tender evaluation does not match its exact current locked committee score sheet.', 1;
                END
                """);

            migrationBuilder.Sql("""
                CREATE OR ALTER TRIGGER [dbo].[TR_ProcurementRfqEvaluations_Lifecycle]
                ON [dbo].[ProcurementRfqEvaluations]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id = d.Id WHERE i.Id IS NULL)
                        THROW 51089, 'RFQ evaluations cannot be deleted.', 1;
                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        LEFT JOIN [dbo].[RequestForQuotations] r ON r.Id = i.RfqId AND r.TenantId = i.TenantId
                        LEFT JOIN [dbo].[ProcurementRfqOpeningRegisters] o ON o.Id = i.OpeningRegisterId AND o.RfqId = i.RfqId AND o.TenantId = i.TenantId
                        LEFT JOIN [dbo].[ProcurementPolicyMethodRules] m ON m.Id = i.MethodRuleId AND m.TenantId = i.TenantId
                        LEFT JOIN [dbo].[WorkflowInstances] w ON w.Id = i.WorkflowInstanceId AND w.TenantId = i.TenantId
                        WHERE r.Id IS NULL OR o.Id IS NULL OR m.Id IS NULL OR (i.WorkflowInstanceId IS NOT NULL AND w.Id IS NULL))
                        THROW 51090, 'RFQ evaluation tenant or control lineage is invalid.', 1;
                    IF EXISTS (
                        SELECT 1 FROM inserted i JOIN deleted d ON d.Id = i.Id
                        WHERE NOT ((d.Status = 0 AND i.Status IN (0,1)) OR (d.Status = 1 AND i.Status IN (1,2,3)))
                           OR i.TenantId <> d.TenantId OR i.RfqId <> d.RfqId OR i.OpeningRegisterId <> d.OpeningRegisterId
                           OR i.MethodRuleId <> d.MethodRuleId OR i.MethodRuleCode <> d.MethodRuleCode
                           OR ISNULL(i.WorkflowDefinitionId, '00000000-0000-0000-0000-000000000000')
                              <> ISNULL(d.WorkflowDefinitionId, '00000000-0000-0000-0000-000000000000')
                           OR i.IsDeleted <> d.IsDeleted)
                        THROW 51091, 'The RFQ evaluation lifecycle transition or immutable lineage is invalid.', 1;
                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        LEFT JOIN deleted d ON d.Id = i.Id
                        WHERE i.Status = 1
                          AND (d.Id IS NULL OR d.Status = 0
                               OR ISNULL(i.WorkflowInstanceId, '00000000-0000-0000-0000-000000000000')
                                  <> ISNULL(d.WorkflowInstanceId, '00000000-0000-0000-0000-000000000000')
                               OR i.AwardMode <> d.AwardMode OR i.RecommendationReason <> d.RecommendationReason
                               OR i.EvidenceReference <> d.EvidenceReference OR i.SnapshotJson <> d.SnapshotJson)
                          AND NOT EXISTS (
                              SELECT 1
                              FROM [dbo].[ProcurementEvaluationScoreSheets] s
                              JOIN [dbo].[ProcurementEvaluationCommitteeControls] c
                                ON c.Id = s.CommitteeControlId AND c.TenantId = s.TenantId
                              JOIN [dbo].[ProcurementEvaluationCommitteeAppointments] a
                                ON a.Id = s.AppointmentId AND a.TenantId = s.TenantId
                              WHERE s.TenantId = i.TenantId AND s.IsDeleted = 0 AND s.Status = 0
                                AND c.SourceType = 1 AND c.SourceId = i.RfqId AND c.Status = 1
                                AND s.Phase = 2 AND s.ScoreSubjectType = 'ProcurementRfqEvaluation'
                                AND s.ScoreSubjectId = i.Id AND s.ScoreSnapshotJson = i.SnapshotJson
                                AND a.UserId = i.SubmittedByUserId
                                AND TRY_CONVERT(uniqueidentifier, JSON_VALUE(i.SnapshotJson, '$.id')) = i.Id
                                AND TRY_CONVERT(uniqueidentifier, JSON_VALUE(i.SnapshotJson, '$.rfqId')) = i.RfqId
                                AND TRY_CONVERT(uniqueidentifier, JSON_VALUE(i.SnapshotJson, '$.openingRegisterId')) = i.OpeningRegisterId
                                AND TRY_CONVERT(int, JSON_VALUE(i.SnapshotJson, '$.status')) = i.Status
                                AND JSON_VALUE(i.SnapshotJson, '$.awardMode') = i.AwardMode
                                AND JSON_VALUE(i.SnapshotJson, '$.recommendationReason') = i.RecommendationReason
                                AND JSON_VALUE(i.SnapshotJson, '$.evidenceReference') = i.EvidenceReference
                                AND TRY_CONVERT(uniqueidentifier, JSON_VALUE(i.SnapshotJson, '$.methodRuleId')) = i.MethodRuleId
                                AND JSON_VALUE(i.SnapshotJson, '$.methodRuleCode') = i.MethodRuleCode
                                AND TRY_CONVERT(uniqueidentifier, JSON_VALUE(i.SnapshotJson, '$.workflowDefinitionId')) = i.WorkflowDefinitionId
                                AND TRY_CONVERT(uniqueidentifier, JSON_VALUE(i.SnapshotJson, '$.workflowInstanceId')) = i.WorkflowInstanceId
                                AND TRY_CONVERT(datetime2, JSON_VALUE(i.SnapshotJson, '$.submittedAtUtc')) = i.SubmittedAtUtc
                                AND TRY_CONVERT(uniqueidentifier, JSON_VALUE(i.SnapshotJson, '$.submittedByUserId')) = i.SubmittedByUserId
                                AND NOT EXISTS (
                                    SELECT 1 FROM [dbo].[ProcurementEvaluationScoreSheets] newer
                                    WHERE newer.TenantId = s.TenantId AND newer.CommitteeControlId = s.CommitteeControlId
                                      AND newer.Phase = s.Phase AND newer.AppointmentId = s.AppointmentId
                                      AND newer.ScoreSubjectType = s.ScoreSubjectType
                                      AND newer.ScoreSubjectId = s.ScoreSubjectId
                                      AND newer.IsDeleted = 0 AND newer.Attempt > s.Attempt)
                                AND NOT EXISTS (
                                    SELECT 1 FROM [dbo].[ProcurementEvaluationScoreRecalls] recall
                                    WHERE recall.ScoreSheetId = s.Id AND recall.TenantId = s.TenantId
                                      AND recall.IsDeleted = 0 AND recall.Status IN (0, 1))))
                        THROW 51373, 'Submitted RFQ evaluation does not match its exact current locked committee score sheet.', 1;
                    IF EXISTS (
                        SELECT 1
                        FROM inserted i JOIN deleted d ON d.Id = i.Id
                        WHERE d.Status <> 0
                          AND (i.AwardMode <> d.AwardMode OR i.RecommendationReason <> d.RecommendationReason
                               OR i.EvidenceReference <> d.EvidenceReference
                               OR ISNULL(i.WorkflowInstanceId, '00000000-0000-0000-0000-000000000000')
                                  <> ISNULL(d.WorkflowInstanceId, '00000000-0000-0000-0000-000000000000')
                               OR i.SnapshotJson <> d.SnapshotJson)
                          AND i.Status <> 1)
                        THROW 51374, 'Final RFQ evaluation projection values are immutable outside an exact recalled Submitted replacement.', 1;
                END
                """);

            migrationBuilder.Sql("""
                CREATE OR ALTER TRIGGER [dbo].[TR_ProcurementRfqEvaluationLines_Lifecycle]
                ON [dbo].[ProcurementRfqEvaluationLines]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (
                        SELECT 1
                        FROM (SELECT EvaluationId FROM inserted UNION SELECT EvaluationId FROM deleted) x
                        LEFT JOIN [dbo].[ProcurementRfqEvaluations] e ON e.Id = x.EvaluationId
                        WHERE e.Id IS NULL
                           OR (e.Status <> 0 AND NOT EXISTS (
                               SELECT 1
                               FROM [dbo].[ProcurementEvaluationScoreSheets] s
                               JOIN [dbo].[ProcurementEvaluationCommitteeControls] c
                                 ON c.Id = s.CommitteeControlId AND c.TenantId = s.TenantId
                               WHERE s.TenantId = e.TenantId AND s.IsDeleted = 0 AND s.Status = 0
                                 AND c.SourceType = 1 AND c.SourceId = e.RfqId AND c.Status = 1
                                 AND s.Phase = 2 AND s.ScoreSubjectType = 'ProcurementRfqEvaluation'
                                 AND s.ScoreSubjectId = e.Id
                                 AND NOT EXISTS (
                                     SELECT 1 FROM [dbo].[ProcurementEvaluationScoreSheets] newer
                                     WHERE newer.TenantId = s.TenantId AND newer.CommitteeControlId = s.CommitteeControlId
                                       AND newer.Phase = s.Phase AND newer.AppointmentId = s.AppointmentId
                                       AND newer.ScoreSubjectType = s.ScoreSubjectType
                                       AND newer.ScoreSubjectId = s.ScoreSubjectId
                                       AND newer.IsDeleted = 0 AND newer.Attempt > s.Attempt))))
                        THROW 51092, 'RFQ evaluation lines may change only while Draft or through an exact current locked recalled score replacement.', 1;
                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        JOIN [dbo].[ProcurementRfqEvaluations] e ON e.Id = i.EvaluationId
                        JOIN [dbo].[RequestForQuotations] r ON r.Id = e.RfqId
                        LEFT JOIN [dbo].[RequestForQuotationItems] ri ON ri.Id = i.RfqItemId AND ri.RfqId = e.RfqId AND ri.TenantId = i.TenantId
                        LEFT JOIN [dbo].[RequestForQuotationQuotes] q ON q.Id = i.QuoteId AND q.RfqId = e.RfqId AND q.BusinessPartnerId = i.BusinessPartnerId AND q.TenantId = i.TenantId
                        LEFT JOIN [dbo].[BusinessPartners] bp ON bp.Id = i.BusinessPartnerId AND bp.TenantId = i.TenantId
                        WHERE e.TenantId <> i.TenantId OR r.TenantId <> i.TenantId OR ri.Id IS NULL OR q.Id IS NULL OR bp.Id IS NULL)
                        THROW 51093, 'RFQ evaluation-line tenant or source lineage is invalid.', 1;
                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        JOIN [dbo].[ProcurementRfqEvaluations] e ON e.Id = i.EvaluationId
                        WHERE e.Status <> 0
                          AND NOT EXISTS (
                              SELECT 1
                              FROM [dbo].[ProcurementEvaluationScoreSheets] s
                              CROSS APPLY OPENJSON(s.ScoreSnapshotJson, '$.lines')
                              WITH (
                                  RfqItemId uniqueidentifier '$.rfqItemId',
                                  QuoteId uniqueidentifier '$.quoteId',
                                  BusinessPartnerId uniqueidentifier '$.businessPartnerId',
                                  UnitPrice decimal(18,4) '$.unitPrice',
                                  LineTotal decimal(18,2) '$.lineTotal',
                                  TechnicalScore decimal(5,2) '$.technicalScore',
                                  CommercialScore decimal(5,2) '$.commercialScore',
                                  TotalScore decimal(5,2) '$.totalScore',
                                  RecommendationReason nvarchar(1000) '$.recommendationReason'
                              ) line
                              WHERE s.TenantId = i.TenantId AND s.IsDeleted = 0 AND s.Status = 0
                                AND s.ScoreSubjectType = 'ProcurementRfqEvaluation' AND s.ScoreSubjectId = e.Id
                                AND line.RfqItemId = i.RfqItemId AND line.QuoteId = i.QuoteId
                                AND line.BusinessPartnerId = i.BusinessPartnerId
                                AND line.UnitPrice = i.UnitPrice AND line.LineTotal = i.LineTotal
                                AND line.TechnicalScore = i.TechnicalScore
                                AND line.CommercialScore = i.CommercialScore AND line.TotalScore = i.TotalScore
                                AND ISNULL(line.RecommendationReason, '') = ISNULL(i.RecommendationReason, '')
                                AND NOT EXISTS (
                                    SELECT 1 FROM [dbo].[ProcurementEvaluationScoreSheets] newer
                                    WHERE newer.TenantId = s.TenantId AND newer.CommitteeControlId = s.CommitteeControlId
                                      AND newer.Phase = s.Phase AND newer.AppointmentId = s.AppointmentId
                                      AND newer.ScoreSubjectType = s.ScoreSubjectType
                                      AND newer.ScoreSubjectId = s.ScoreSubjectId
                                      AND newer.IsDeleted = 0 AND newer.Attempt > s.Attempt)))
                        THROW 51375, 'RFQ evaluation line does not match its exact current locked committee score-sheet snapshot.', 1;
                END
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER [dbo].[TR_ProcurementTenderControls_CommitteeScoreProjection]
                ON [dbo].[ProcurementTenderControls]
                AFTER INSERT, UPDATE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        LEFT JOIN deleted d ON d.Id = i.Id
                        WHERE LEN(LTRIM(RTRIM(ISNULL(i.TechnicalEvaluationSnapshotJson, '')))) > 0
                          AND (d.Id IS NULL
                               OR ISNULL(i.TechnicalEvaluationSnapshotJson, '') <> ISNULL(d.TechnicalEvaluationSnapshotJson, '')
                               OR ISNULL(i.TechnicalEvaluatedAtUtc, '19000101') <> ISNULL(d.TechnicalEvaluatedAtUtc, '19000101'))
                          AND NOT EXISTS (
                              SELECT 1
                              FROM [dbo].[ProcurementEvaluationScoreSheets] s
                              JOIN [dbo].[ProcurementEvaluationCommitteeControls] c
                                ON c.Id = s.CommitteeControlId AND c.TenantId = s.TenantId
                              JOIN [dbo].[ProcurementEvaluationCommitteeAppointments] a
                                ON a.Id = s.AppointmentId AND a.TenantId = s.TenantId
                              WHERE s.TenantId = i.TenantId AND s.IsDeleted = 0 AND s.Status = 0
                                AND c.SourceType = 0 AND c.SourceId = i.TenderId AND c.Status = 1
                                AND s.Phase = 0 AND s.ScoreSubjectType = 'ProcurementTenderControl'
                                AND s.ScoreSubjectId = i.TenderId
                                AND s.ScoreSnapshotJson = i.TechnicalEvaluationSnapshotJson
                                AND TRY_CONVERT(datetime2, JSON_VALUE(i.TechnicalEvaluationSnapshotJson, '$.evaluatedAtUtc'))
                                    = i.TechnicalEvaluatedAtUtc
                                AND JSON_VALUE(i.TechnicalEvaluationSnapshotJson, '$.evidenceReference')
                                    = i.TechnicalEvaluationEvidenceReference
                                AND TRY_CONVERT(uniqueidentifier, JSON_VALUE(i.TechnicalEvaluationSnapshotJson, '$.evaluatorUserId'))
                                    = a.UserId
                                AND NOT EXISTS (
                                    SELECT 1 FROM [dbo].[ProcurementEvaluationScoreSheets] newer
                                    WHERE newer.TenantId = s.TenantId AND newer.CommitteeControlId = s.CommitteeControlId
                                      AND newer.Phase = s.Phase AND newer.AppointmentId = s.AppointmentId
                                      AND newer.ScoreSubjectType = s.ScoreSubjectType
                                      AND newer.ScoreSubjectId = s.ScoreSubjectId
                                      AND newer.IsDeleted = 0 AND newer.Attempt > s.Attempt)
                                AND NOT EXISTS (
                                    SELECT 1 FROM [dbo].[ProcurementEvaluationScoreRecalls] recall
                                    WHERE recall.ScoreSheetId = s.Id AND recall.TenantId = s.TenantId
                                      AND recall.IsDeleted = 0 AND recall.Status IN (0, 1))))
                        THROW 51386, 'Technical tender-control projection does not match its exact current locked committee score sheet.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        LEFT JOIN deleted d ON d.Id = i.Id
                        WHERE LEN(LTRIM(RTRIM(ISNULL(i.FinancialEvaluationSnapshotJson, '')))) > 0
                          AND (d.Id IS NULL
                               OR ISNULL(i.FinancialEvaluationSnapshotJson, '') <> ISNULL(d.FinancialEvaluationSnapshotJson, '')
                               OR ISNULL(i.FinancialEvaluatedAtUtc, '19000101') <> ISNULL(d.FinancialEvaluatedAtUtc, '19000101')
                               OR ISNULL(i.RecommendedBidId, '00000000-0000-0000-0000-000000000000')
                                  <> ISNULL(d.RecommendedBidId, '00000000-0000-0000-0000-000000000000'))
                          AND NOT EXISTS (
                              SELECT 1
                              FROM [dbo].[ProcurementEvaluationScoreSheets] s
                              JOIN [dbo].[ProcurementEvaluationCommitteeControls] c
                                ON c.Id = s.CommitteeControlId AND c.TenantId = s.TenantId
                              JOIN [dbo].[ProcurementEvaluationCommitteeAppointments] a
                                ON a.Id = s.AppointmentId AND a.TenantId = s.TenantId
                              WHERE s.TenantId = i.TenantId AND s.IsDeleted = 0 AND s.Status = 0
                                AND c.SourceType = 0 AND c.SourceId = i.TenderId AND c.Status = 1
                                AND s.Phase = 1 AND s.ScoreSubjectType = 'ProcurementTenderControl'
                                AND s.ScoreSubjectId = i.TenderId
                                AND s.ScoreSnapshotJson = i.FinancialEvaluationSnapshotJson
                                AND TRY_CONVERT(datetime2, JSON_VALUE(i.FinancialEvaluationSnapshotJson, '$.evaluatedAtUtc'))
                                    = i.FinancialEvaluatedAtUtc
                                AND JSON_VALUE(i.FinancialEvaluationSnapshotJson, '$.evidenceReference')
                                    = i.FinancialEvaluationEvidenceReference
                                AND TRY_CONVERT(uniqueidentifier, JSON_VALUE(i.FinancialEvaluationSnapshotJson, '$.recommendedBidId'))
                                    = i.RecommendedBidId
                                AND TRY_CONVERT(uniqueidentifier, JSON_VALUE(i.FinancialEvaluationSnapshotJson, '$.evaluatorUserId'))
                                    = a.UserId
                                AND NOT EXISTS (
                                    SELECT 1 FROM [dbo].[ProcurementEvaluationScoreSheets] newer
                                    WHERE newer.TenantId = s.TenantId AND newer.CommitteeControlId = s.CommitteeControlId
                                      AND newer.Phase = s.Phase AND newer.AppointmentId = s.AppointmentId
                                      AND newer.ScoreSubjectType = s.ScoreSubjectType
                                      AND newer.ScoreSubjectId = s.ScoreSubjectId
                                      AND newer.IsDeleted = 0 AND newer.Attempt > s.Attempt)
                                AND NOT EXISTS (
                                    SELECT 1 FROM [dbo].[ProcurementEvaluationScoreRecalls] recall
                                    WHERE recall.ScoreSheetId = s.Id AND recall.TenantId = s.TenantId
                                      AND recall.IsDeleted = 0 AND recall.Status IN (0, 1))))
                        THROW 51387, 'Financial tender-control projection does not match its exact current locked committee score sheet.', 1;
                END
                """);

            migrationBuilder.Sql("""
                DECLARE @definition nvarchar(max) =
                    OBJECT_DEFINITION(OBJECT_ID(N'[dbo].[TR_ProcurementTenderControls_Lifecycle]'));
                DECLARE @original nvarchar(max) = @definition;
                IF @definition IS NULL
                    THROW 51380, 'The statutory tender lifecycle trigger is required before committee projection hardening.', 1;

                SET @definition = REPLACE(
                    @definition,
                    N'OR (d.[Status] >= 2 AND',
                    N'OR (d.[Status] >= 2
                        AND NOT EXISTS (
                            SELECT 1
                            FROM [dbo].[ProcurementEvaluationScoreSheets] s
                            JOIN [dbo].[ProcurementEvaluationCommitteeControls] ec
                              ON ec.[Id] = s.[CommitteeControlId] AND ec.[TenantId] = s.[TenantId]
                            JOIN [dbo].[ProcurementEvaluationCommitteeAppointments] ea
                              ON ea.[Id] = s.[AppointmentId] AND ea.[TenantId] = s.[TenantId]
                            WHERE s.[TenantId] = i.[TenantId] AND s.[IsDeleted] = 0 AND s.[Status] = 0
                              AND ec.[SourceType] = 0 AND ec.[SourceId] = i.[TenderId] AND ec.[Status] = 1
                              AND s.[Phase] = 0 AND s.[ScoreSubjectType] = ''ProcurementTenderControl''
                              AND s.[ScoreSubjectId] = i.[TenderId]
                              AND s.[ScoreSnapshotJson] = i.[TechnicalEvaluationSnapshotJson]
                              AND TRY_CONVERT(datetime2, JSON_VALUE(i.[TechnicalEvaluationSnapshotJson], ''$.evaluatedAtUtc''))
                                  = i.[TechnicalEvaluatedAtUtc]
                              AND JSON_VALUE(i.[TechnicalEvaluationSnapshotJson], ''$.evidenceReference'')
                                  = i.[TechnicalEvaluationEvidenceReference]
                              AND TRY_CONVERT(uniqueidentifier, JSON_VALUE(i.[TechnicalEvaluationSnapshotJson], ''$.evaluatorUserId''))
                                  = ea.[UserId]
                              AND NOT EXISTS (
                                  SELECT 1
                                  FROM [dbo].[ProcurementEvaluationScoreSheets] newer
                                  WHERE newer.[TenantId] = s.[TenantId]
                                    AND newer.[CommitteeControlId] = s.[CommitteeControlId]
                                    AND newer.[Phase] = s.[Phase] AND newer.[AppointmentId] = s.[AppointmentId]
                                    AND newer.[ScoreSubjectType] = s.[ScoreSubjectType]
                                    AND newer.[ScoreSubjectId] = s.[ScoreSubjectId]
                                    AND newer.[IsDeleted] = 0 AND newer.[Attempt] > s.[Attempt])
                              AND NOT EXISTS (
                                  SELECT 1
                                  FROM [dbo].[ProcurementEvaluationScoreRecalls] recall
                                  WHERE recall.[ScoreSheetId] = s.[Id] AND recall.[TenantId] = s.[TenantId]
                                    AND recall.[IsDeleted] = 0 AND recall.[Status] IN (0, 1)))
                        AND');

                IF @definition = @original
                    THROW 51381, 'The technical tender projection guard could not be installed.', 1;
                SET @original = @definition;

                SET @definition = REPLACE(
                    @definition,
                    N'OR (d.[Status] >= 3 AND',
                    N'OR (d.[Status] >= 3
                        AND NOT EXISTS (
                            SELECT 1
                            FROM [dbo].[ProcurementEvaluationScoreSheets] s
                            JOIN [dbo].[ProcurementEvaluationCommitteeControls] ec
                              ON ec.[Id] = s.[CommitteeControlId] AND ec.[TenantId] = s.[TenantId]
                            JOIN [dbo].[ProcurementEvaluationCommitteeAppointments] ea
                              ON ea.[Id] = s.[AppointmentId] AND ea.[TenantId] = s.[TenantId]
                            WHERE s.[TenantId] = i.[TenantId] AND s.[IsDeleted] = 0 AND s.[Status] = 0
                              AND ec.[SourceType] = 0 AND ec.[SourceId] = i.[TenderId] AND ec.[Status] = 1
                              AND s.[Phase] = 1 AND s.[ScoreSubjectType] = ''ProcurementTenderControl''
                              AND s.[ScoreSubjectId] = i.[TenderId]
                              AND s.[ScoreSnapshotJson] = i.[FinancialEvaluationSnapshotJson]
                              AND TRY_CONVERT(datetime2, JSON_VALUE(i.[FinancialEvaluationSnapshotJson], ''$.evaluatedAtUtc''))
                                  = i.[FinancialEvaluatedAtUtc]
                              AND JSON_VALUE(i.[FinancialEvaluationSnapshotJson], ''$.evidenceReference'')
                                  = i.[FinancialEvaluationEvidenceReference]
                              AND TRY_CONVERT(uniqueidentifier, JSON_VALUE(i.[FinancialEvaluationSnapshotJson], ''$.recommendedBidId''))
                                  = i.[RecommendedBidId]
                              AND TRY_CONVERT(uniqueidentifier, JSON_VALUE(i.[FinancialEvaluationSnapshotJson], ''$.evaluatorUserId''))
                                  = ea.[UserId]
                              AND NOT EXISTS (
                                  SELECT 1
                                  FROM [dbo].[ProcurementEvaluationScoreSheets] newer
                                  WHERE newer.[TenantId] = s.[TenantId]
                                    AND newer.[CommitteeControlId] = s.[CommitteeControlId]
                                    AND newer.[Phase] = s.[Phase] AND newer.[AppointmentId] = s.[AppointmentId]
                                    AND newer.[ScoreSubjectType] = s.[ScoreSubjectType]
                                    AND newer.[ScoreSubjectId] = s.[ScoreSubjectId]
                                    AND newer.[IsDeleted] = 0 AND newer.[Attempt] > s.[Attempt])
                              AND NOT EXISTS (
                                  SELECT 1
                                  FROM [dbo].[ProcurementEvaluationScoreRecalls] recall
                                  WHERE recall.[ScoreSheetId] = s.[Id] AND recall.[TenantId] = s.[TenantId]
                                    AND recall.[IsDeleted] = 0 AND recall.[Status] IN (0, 1)))
                        AND');

                IF @definition = @original
                    THROW 51382, 'The financial tender projection guard could not be installed.', 1;
                IF CHARINDEX(N'CREATE OR ALTER', UPPER(@definition)) = 0
                    SET @definition = STUFF(
                        @definition,
                        CHARINDEX(N'CREATE', UPPER(@definition)),
                        LEN(N'CREATE'),
                        N'CREATE OR ALTER');
                EXEC sys.sp_executesql @definition;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DECLARE @definition nvarchar(max) =
                    OBJECT_DEFINITION(OBJECT_ID(N'[dbo].[TR_ProcurementTenderControls_Lifecycle]'));
                IF @definition IS NULL
                    THROW 51383, 'The statutory tender lifecycle trigger is required for committee projection rollback.', 1;

                DECLARE @technicalStart int = CHARINDEX(N'OR (d.[Status] >= 2', @definition);
                DECLARE @technicalTail int =
                    CHARINDEX(N'(ISNULL(i.[TechnicalEvaluatedAtUtc]', @definition, @technicalStart);
                IF @technicalStart = 0 OR @technicalTail = 0
                   OR CHARINDEX(N'ProcurementEvaluationScoreSheets',
                                SUBSTRING(@definition, @technicalStart, @technicalTail - @technicalStart)) = 0
                    THROW 51384, 'The technical tender projection guard could not be restored.', 1;
                SET @definition = STUFF(
                    @definition,
                    @technicalStart,
                    @technicalTail - @technicalStart,
                    N'OR (d.[Status] >= 2 AND
                                    ');

                DECLARE @financialStart int = CHARINDEX(N'OR (d.[Status] >= 3', @definition);
                DECLARE @financialTail int =
                    CHARINDEX(N'(ISNULL(i.[FinancialEvaluatedAtUtc]', @definition, @financialStart);
                IF @financialStart = 0 OR @financialTail = 0
                   OR CHARINDEX(N'ProcurementEvaluationScoreSheets',
                                SUBSTRING(@definition, @financialStart, @financialTail - @financialStart)) = 0
                    THROW 51385, 'The financial tender projection guard could not be restored.', 1;
                SET @definition = STUFF(
                    @definition,
                    @financialStart,
                    @financialTail - @financialStart,
                    N'OR (d.[Status] >= 3 AND
                                    ');

                IF CHARINDEX(N'CREATE OR ALTER', UPPER(@definition)) = 0
                    SET @definition = STUFF(
                        @definition,
                        CHARINDEX(N'CREATE', UPPER(@definition)),
                        LEN(N'CREATE'),
                        N'CREATE OR ALTER');
                EXEC sys.sp_executesql @definition;
                """);

            migrationBuilder.Sql("""
                CREATE OR ALTER TRIGGER [dbo].[TR_ProcurementRfqEvaluations_Lifecycle]
                ON [dbo].[ProcurementRfqEvaluations]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id = d.Id WHERE i.Id IS NULL)
                        THROW 51089, 'RFQ evaluations cannot be deleted.', 1;
                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        LEFT JOIN [dbo].[RequestForQuotations] r ON r.Id = i.RfqId AND r.TenantId = i.TenantId
                        LEFT JOIN [dbo].[ProcurementRfqOpeningRegisters] o ON o.Id = i.OpeningRegisterId AND o.RfqId = i.RfqId AND o.TenantId = i.TenantId
                        LEFT JOIN [dbo].[ProcurementPolicyMethodRules] m ON m.Id = i.MethodRuleId AND m.TenantId = i.TenantId
                        LEFT JOIN [dbo].[WorkflowInstances] w ON w.Id = i.WorkflowInstanceId AND w.TenantId = i.TenantId
                        WHERE r.Id IS NULL OR o.Id IS NULL OR m.Id IS NULL OR (i.WorkflowInstanceId IS NOT NULL AND w.Id IS NULL))
                        THROW 51090, 'RFQ evaluation tenant or control lineage is invalid.', 1;
                    IF EXISTS (
                        SELECT 1 FROM inserted i JOIN deleted d ON d.Id = i.Id
                        WHERE NOT ((d.Status = 0 AND i.Status IN (0,1)) OR (d.Status = 1 AND i.Status IN (1,2,3)))
                           OR i.TenantId <> d.TenantId OR i.RfqId <> d.RfqId OR i.OpeningRegisterId <> d.OpeningRegisterId
                           OR i.MethodRuleId <> d.MethodRuleId OR i.MethodRuleCode <> d.MethodRuleCode
                           OR ISNULL(i.WorkflowDefinitionId, '00000000-0000-0000-0000-000000000000') <> ISNULL(d.WorkflowDefinitionId, '00000000-0000-0000-0000-000000000000')
                           OR (d.WorkflowInstanceId IS NOT NULL AND i.WorkflowInstanceId <> d.WorkflowInstanceId)
                           OR (d.Status <> 0 AND (i.AwardMode <> d.AwardMode OR i.RecommendationReason <> d.RecommendationReason OR i.EvidenceReference <> d.EvidenceReference))
                           OR i.IsDeleted <> d.IsDeleted)
                        THROW 51091, 'The RFQ evaluation lifecycle transition or immutable lineage is invalid.', 1;
                END
                """);

            migrationBuilder.Sql("""
                CREATE OR ALTER TRIGGER [dbo].[TR_ProcurementRfqEvaluationLines_Lifecycle]
                ON [dbo].[ProcurementRfqEvaluationLines]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (
                        SELECT 1 FROM (SELECT EvaluationId FROM inserted UNION SELECT EvaluationId FROM deleted) x
                        LEFT JOIN [dbo].[ProcurementRfqEvaluations] e ON e.Id = x.EvaluationId
                        WHERE e.Id IS NULL OR e.Status <> 0)
                        THROW 51092, 'RFQ evaluation lines may change only while the evaluation is Draft.', 1;
                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        JOIN [dbo].[ProcurementRfqEvaluations] e ON e.Id = i.EvaluationId
                        JOIN [dbo].[RequestForQuotations] r ON r.Id = e.RfqId
                        LEFT JOIN [dbo].[RequestForQuotationItems] ri ON ri.Id = i.RfqItemId AND ri.RfqId = e.RfqId AND ri.TenantId = i.TenantId
                        LEFT JOIN [dbo].[RequestForQuotationQuotes] q ON q.Id = i.QuoteId AND q.RfqId = e.RfqId AND q.BusinessPartnerId = i.BusinessPartnerId AND q.TenantId = i.TenantId
                        LEFT JOIN [dbo].[BusinessPartners] bp ON bp.Id = i.BusinessPartnerId AND bp.TenantId = i.TenantId
                        WHERE e.TenantId <> i.TenantId OR r.TenantId <> i.TenantId OR ri.Id IS NULL OR q.Id IS NULL OR bp.Id IS NULL)
                        THROW 51093, 'RFQ evaluation-line tenant or source lineage is invalid.', 1;
                END
                """);

            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_TenderEvaluations_CommitteeScoreProjection];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_ProcurementTenderControls_CommitteeScoreProjection];");
            migrationBuilder.Sql("DROP INDEX IF EXISTS [UX_TenderEvaluations_Tenant_Bid_Evaluator_Draft] ON [dbo].[TenderEvaluations];");

            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_ProcurementEvaluationScoreRecalls_Lifecycle];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_ProcurementEvaluationScoreSheets_Immutable];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_ProcurementEvaluationAttendanceRecords_Immutable];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_ProcurementEvaluationMeetings_Lifecycle];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_ProcurementEvaluationConflictDeclarations_Immutable];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_ProcurementEvaluationCommitteeAppointments_Lifecycle];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_ProcurementEvaluationCommitteeRoleRequirements_Immutable];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_ProcurementEvaluationCommitteeControls_Lifecycle];");

            migrationBuilder.DropTable(
                name: "ProcurementEvaluationAttendanceRecords");

            migrationBuilder.DropTable(
                name: "ProcurementEvaluationCommitteeRoleRequirements");

            migrationBuilder.DropTable(
                name: "ProcurementEvaluationConflictDeclarations");

            migrationBuilder.DropTable(
                name: "ProcurementEvaluationScoreRecalls");

            migrationBuilder.DropTable(
                name: "ProcurementEvaluationScoreSheets");

            migrationBuilder.DropTable(
                name: "ProcurementEvaluationCommitteeAppointments");

            migrationBuilder.DropTable(
                name: "ProcurementEvaluationMeetings");

            migrationBuilder.DropTable(
                name: "ProcurementEvaluationCommitteeControls");
        }
    }
}
