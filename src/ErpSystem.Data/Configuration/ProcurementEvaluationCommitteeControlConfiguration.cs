using ErpSystem.Core.Entities.Procurement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration;

public sealed class ProcurementEvaluationCommitteeControlConfiguration :
    IEntityTypeConfiguration<ProcurementEvaluationCommitteeControl>
{
    public void Configure(EntityTypeBuilder<ProcurementEvaluationCommitteeControl> builder)
    {
        builder.ToTable("ProcurementEvaluationCommitteeControls", table =>
        {
            table.HasTrigger("TR_ProcurementEvaluationCommitteeControls_Lifecycle");
            table.HasCheckConstraint("CK_ProcurementEvaluationCommitteeControls_State",
                "[SourceType] BETWEEN 0 AND 1 AND [Version] >= 1 AND [Status] BETWEEN 0 AND 3 " +
                "AND [RequiredQuorum] BETWEEN 1 AND 50 AND [PolicyVersion] >= 1 " +
                "AND ([ConfigurationProfileVersion] IS NULL OR [ConfigurationProfileVersion] >= 1) " +
                "AND ([EffectiveToUtc] IS NULL OR [EffectiveToUtc] >= [EffectiveFromUtc]) " +
                "AND LEN([CompositionIntegrityHash]) = 64 AND ISJSON([CompositionSnapshotJson]) = 1 " +
                "AND (([Status] = 0 AND [ActivatedAtUtc] IS NULL AND [ActivatedByUserId] IS NULL " +
                "AND [ActivationEvidenceReference] IS NULL AND [RetiredAtUtc] IS NULL " +
                "AND [RetiredByUserId] IS NULL AND [RetirementReason] IS NULL " +
                "AND [RetirementEvidenceReference] IS NULL AND [RetirementIdempotencyKey] IS NULL) OR " +
                "([Status] IN (1, 2) AND [ActivatedAtUtc] IS NOT NULL AND [ActivatedByUserId] IS NOT NULL " +
                "AND LEN(LTRIM(RTRIM(ISNULL([ActivationEvidenceReference], '')))) > 0 " +
                "AND [RetiredAtUtc] IS NULL AND [RetiredByUserId] IS NULL " +
                "AND [RetirementReason] IS NULL AND [RetirementEvidenceReference] IS NULL " +
                "AND [RetirementIdempotencyKey] IS NULL) OR " +
                "([Status] = 3 AND [ActivatedAtUtc] IS NULL AND [ActivatedByUserId] IS NULL " +
                "AND [ActivationEvidenceReference] IS NULL AND [ActivationIdempotencyKey] IS NULL " +
                "AND [RetiredAtUtc] IS NOT NULL AND [RetiredByUserId] IS NOT NULL " +
                "AND LEN(LTRIM(RTRIM(ISNULL([RetirementReason], '')))) >= 10 " +
                "AND LEN(LTRIM(RTRIM(ISNULL([RetirementEvidenceReference], '')))) > 0 " +
                "AND LEN(LTRIM(RTRIM(ISNULL([RetirementIdempotencyKey], '')))) > 0))");
        });
        builder.HasIndex(item => new
            { item.TenantId, item.SourceType, item.SourceId, item.Version }).IsUnique();
        builder.HasIndex(item => new
            { item.TenantId, item.SourceType, item.SourceId }).IsUnique()
            .HasFilter("[Status] IN (0, 1) AND [IsDeleted] = 0");
        builder.HasIndex(item => new
            { item.TenantId, item.SourceType, item.SourceId, item.CreationIdempotencyKey })
            .IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.CommitteeTemplateId });
        builder.HasIndex(item => new { item.TenantId, item.PolicySetId });
        builder.HasIndex(item => new { item.TenantId, item.WorkflowDefinitionId });
        builder.HasOne(item => item.CommitteeTemplate).WithMany()
            .HasForeignKey(item => item.CommitteeTemplateId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.PolicySet).WithMany()
            .HasForeignKey(item => item.PolicySetId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.ConfigurationProfile).WithMany()
            .HasForeignKey(item => item.ConfigurationProfileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.MethodRule).WithMany()
            .HasForeignKey(item => item.MethodRuleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.WorkflowDefinition).WithMany()
            .HasForeignKey(item => item.WorkflowDefinitionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.WorkflowInstance).WithMany()
            .HasForeignKey(item => item.WorkflowInstanceId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProcurementEvaluationCommitteeRoleRequirementConfiguration :
    IEntityTypeConfiguration<ProcurementEvaluationCommitteeRoleRequirement>
{
    public void Configure(
        EntityTypeBuilder<ProcurementEvaluationCommitteeRoleRequirement> builder)
    {
        builder.ToTable("ProcurementEvaluationCommitteeRoleRequirements", table =>
        {
            table.HasTrigger("TR_ProcurementEvaluationCommitteeRoleRequirements_Immutable");
            table.HasCheckConstraint(
                "CK_ProcurementEvaluationCommitteeRoleRequirements_State",
                "[MemberKind] BETWEEN 0 AND 4 AND [MinimumCount] BETWEEN 1 AND 50");
        });
        builder.HasIndex(item => new
            { item.TenantId, item.CommitteeControlId, item.MemberKind, item.RoleName })
            .IsUnique();
        builder.HasOne(item => item.CommitteeControl)
            .WithMany(item => item.RequiredRoles)
            .HasForeignKey(item => item.CommitteeControlId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProcurementEvaluationCommitteeAppointmentConfiguration :
    IEntityTypeConfiguration<ProcurementEvaluationCommitteeAppointment>
{
    public void Configure(
        EntityTypeBuilder<ProcurementEvaluationCommitteeAppointment> builder)
    {
        builder.ToTable("ProcurementEvaluationCommitteeAppointments", table =>
        {
            table.HasTrigger("TR_ProcurementEvaluationCommitteeAppointments_Lifecycle");
            table.HasCheckConstraint(
                "CK_ProcurementEvaluationCommitteeAppointments_State",
                "[MemberKind] BETWEEN 0 AND 4 AND [Status] BETWEEN 0 AND 3 " +
                "AND ([EffectiveToUtc] IS NULL OR [EffectiveToUtc] >= [EffectiveFromUtc]) " +
                "AND (([Status] = 0 AND [AcceptedAtUtc] IS NULL AND [AcceptanceSignatureReference] IS NULL " +
                "AND [AcceptanceEvidenceReference] IS NULL) OR " +
                "([Status] = 1 AND [AcceptedAtUtc] IS NOT NULL " +
                "AND LEN(LTRIM(RTRIM(ISNULL([AcceptanceSignatureReference], '')))) > 0 " +
                "AND LEN(LTRIM(RTRIM(ISNULL([AcceptanceEvidenceReference], '')))) > 0) OR " +
                "([Status] IN (2, 3) AND [AcceptedAtUtc] IS NULL))");
        });
        builder.HasIndex(item => new
            { item.TenantId, item.CommitteeControlId, item.UserId }).IsUnique();
        builder.HasIndex(item => new
            { item.TenantId, item.CommitteeControlId, item.CommitteeMemberId }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.ResponsibilityAssignmentId });
        builder.HasOne(item => item.CommitteeControl)
            .WithMany(item => item.Appointments)
            .HasForeignKey(item => item.CommitteeControlId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.CommitteeMember).WithMany()
            .HasForeignKey(item => item.CommitteeMemberId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.ResponsibilityAssignment).WithMany()
            .HasForeignKey(item => item.ResponsibilityAssignmentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.User).WithMany()
            .HasForeignKey(item => item.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProcurementEvaluationConflictDeclarationConfiguration :
    IEntityTypeConfiguration<ProcurementEvaluationConflictDeclaration>
{
    public void Configure(
        EntityTypeBuilder<ProcurementEvaluationConflictDeclaration> builder)
    {
        builder.ToTable("ProcurementEvaluationConflictDeclarations", table =>
        {
            table.HasTrigger("TR_ProcurementEvaluationConflictDeclarations_Immutable");
            table.HasCheckConstraint(
                "CK_ProcurementEvaluationConflictDeclarations_State",
                "[Version] >= 1 AND [Outcome] BETWEEN 0 AND 2 " +
                "AND ([ValidToUtc] IS NULL OR [ValidToUtc] >= [ValidFromUtc]) " +
                "AND (([Outcome] <> 1) OR LEN(LTRIM(RTRIM(ISNULL([ConflictDetails], '')))) > 0) " +
                "AND LEN([IntegrityHash]) = 64 AND ISJSON([SnapshotJson]) = 1 " +
                "AND ([WorkflowEvidenceDocumentId] IS NULL OR [FileUploadRecordId] IS NULL)");
        });
        builder.HasIndex(item => new { item.TenantId, item.AppointmentId, item.Version })
            .IsUnique();
        builder.HasIndex(item => new
            { item.TenantId, item.AppointmentId, item.IdempotencyKey }).IsUnique();
        builder.HasOne(item => item.Appointment)
            .WithMany(item => item.ConflictDeclarations)
            .HasForeignKey(item => item.AppointmentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.WorkflowEvidenceDocument).WithMany()
            .HasForeignKey(item => item.WorkflowEvidenceDocumentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.FileUploadRecord).WithMany()
            .HasForeignKey(item => item.FileUploadRecordId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProcurementEvaluationMeetingConfiguration :
    IEntityTypeConfiguration<ProcurementEvaluationMeeting>
{
    public void Configure(EntityTypeBuilder<ProcurementEvaluationMeeting> builder)
    {
        builder.ToTable("ProcurementEvaluationMeetings", table =>
        {
            table.HasTrigger("TR_ProcurementEvaluationMeetings_Lifecycle");
            table.HasCheckConstraint("CK_ProcurementEvaluationMeetings_State",
                "[Sequence] >= 1 AND [Phase] BETWEEN 0 AND 2 AND [Status] BETWEEN 0 AND 3 " +
                "AND [MeetingMode] IN ('InPerson', 'Remote', 'Hybrid') " +
                "AND (([MeetingMode] = 'InPerson') OR " +
                "LEN(LTRIM(RTRIM(ISNULL([RemoteMeetingEvidenceReference], '')))) > 0) " +
                "AND [EligibleVotingMemberCount] >= 0 AND [SignedVotingAttendanceCount] >= 0 " +
                "AND LEN([QuorumIntegrityHash]) = 64 AND ISJSON([QuorumSnapshotJson]) = 1 " +
                "AND (([Status] = 0 AND [QuorumMet] = 0) OR " +
                "([Status] = 1 AND [QuorumMet] = 1 AND [StartedAtUtc] IS NOT NULL " +
                "AND [ChairPresent] = 1 AND [SecretaryPresent] = 1) OR " +
                "([Status] = 2 AND [QuorumMet] = 0 AND [StartedAtUtc] IS NOT NULL) OR " +
                "([Status] = 3 AND [ClosedAtUtc] IS NOT NULL))");
        });
        builder.HasIndex(item => new
            { item.TenantId, item.CommitteeControlId, item.Sequence }).IsUnique();
        builder.HasIndex(item => new
            { item.TenantId, item.CommitteeControlId, item.IdempotencyKey }).IsUnique();
        builder.HasIndex(item => new
            { item.TenantId, item.CommitteeControlId, item.Phase, item.Status });
        builder.HasOne(item => item.CommitteeControl)
            .WithMany(item => item.Meetings)
            .HasForeignKey(item => item.CommitteeControlId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProcurementEvaluationAttendanceRecordConfiguration :
    IEntityTypeConfiguration<ProcurementEvaluationAttendanceRecord>
{
    public void Configure(
        EntityTypeBuilder<ProcurementEvaluationAttendanceRecord> builder)
    {
        builder.ToTable("ProcurementEvaluationAttendanceRecords", table =>
        {
            table.HasTrigger("TR_ProcurementEvaluationAttendanceRecords_Immutable");
            table.HasCheckConstraint(
                "CK_ProcurementEvaluationAttendanceRecords_State",
                "[SignedAtUtc] IS NOT NULL " +
                "AND LEN(LTRIM(RTRIM(ISNULL([SignatureReference], '')))) > 0 " +
                "AND LEN(LTRIM(RTRIM(ISNULL([EvidenceReference], '')))) > 0 " +
                "AND LEN([IntegrityHash]) = 64 AND ISJSON([SnapshotJson]) = 1");
        });
        builder.HasIndex(item => new { item.TenantId, item.MeetingId, item.AppointmentId })
            .IsUnique();
        builder.HasIndex(item => new
            { item.TenantId, item.MeetingId, item.IdempotencyKey }).IsUnique();
        builder.HasOne(item => item.Meeting)
            .WithMany(item => item.AttendanceRecords)
            .HasForeignKey(item => item.MeetingId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Appointment)
            .WithMany(item => item.AttendanceRecords)
            .HasForeignKey(item => item.AppointmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProcurementEvaluationScoreSheetConfiguration :
    IEntityTypeConfiguration<ProcurementEvaluationScoreSheet>
{
    public void Configure(EntityTypeBuilder<ProcurementEvaluationScoreSheet> builder)
    {
        builder.ToTable("ProcurementEvaluationScoreSheets", table =>
        {
            table.HasTrigger("TR_ProcurementEvaluationScoreSheets_Immutable");
            table.HasCheckConstraint("CK_ProcurementEvaluationScoreSheets_State",
                "[Phase] BETWEEN 0 AND 2 AND [Attempt] >= 1 AND [Status] = 0 " +
                "AND LEN([IntegrityHash]) = 64 AND ISJSON([ScoreSnapshotJson]) = 1");
        });
        builder.HasIndex(item => new
        {
            item.TenantId,
            item.CommitteeControlId,
            item.Phase,
            item.AppointmentId,
            item.ScoreSubjectType,
            item.ScoreSubjectId,
            item.Attempt
        }).IsUnique();
        builder.HasIndex(item => new
            { item.TenantId, item.CommitteeControlId, item.IdempotencyKey }).IsUnique();
        builder.HasIndex(item => new
            { item.TenantId, item.CommitteeControlId, item.Phase, item.ScoreSubjectId });
        builder.HasOne(item => item.CommitteeControl)
            .WithMany(item => item.ScoreSheets)
            .HasForeignKey(item => item.CommitteeControlId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Meeting)
            .WithMany(item => item.ScoreSheets)
            .HasForeignKey(item => item.MeetingId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Appointment)
            .WithMany(item => item.ScoreSheets)
            .HasForeignKey(item => item.AppointmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProcurementEvaluationScoreRecallConfiguration :
    IEntityTypeConfiguration<ProcurementEvaluationScoreRecall>
{
    public void Configure(EntityTypeBuilder<ProcurementEvaluationScoreRecall> builder)
    {
        builder.ToTable("ProcurementEvaluationScoreRecalls", table =>
        {
            table.HasTrigger("TR_ProcurementEvaluationScoreRecalls_Lifecycle");
            table.HasCheckConstraint("CK_ProcurementEvaluationScoreRecalls_State",
                "[Status] BETWEEN 0 AND 2 AND LEN([IntegrityHash]) = 64 " +
                "AND ISJSON([SnapshotJson]) = 1 " +
                "AND ([WorkflowEvidenceDocumentId] IS NULL OR [FileUploadRecordId] IS NULL) " +
                "AND (([Status] = 0 AND [DecidedByUserId] IS NULL AND [DecidedAtUtc] IS NULL " +
                "AND [DecisionReference] IS NULL AND [DecisionEvidenceReference] IS NULL " +
                "AND [AuthorizedNewAttempt] IS NULL) OR " +
                "([Status] = 1 AND [DecidedByUserId] IS NOT NULL AND [DecidedAtUtc] IS NOT NULL " +
                "AND LEN(LTRIM(RTRIM(ISNULL([DecisionReference], '')))) > 0 " +
                "AND LEN(LTRIM(RTRIM(ISNULL([DecisionEvidenceReference], '')))) > 0 " +
                "AND [AuthorizedNewAttempt] >= 2) OR " +
                "([Status] = 2 AND [DecidedByUserId] IS NOT NULL AND [DecidedAtUtc] IS NOT NULL " +
                "AND LEN(LTRIM(RTRIM(ISNULL([DecisionReference], '')))) > 0 " +
                "AND LEN(LTRIM(RTRIM(ISNULL([DecisionEvidenceReference], '')))) > 0 " +
                "AND [AuthorizedNewAttempt] IS NULL))");
        });
        builder.HasIndex(item => new
            { item.TenantId, item.ScoreSheetId, item.IdempotencyKey }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.ScoreSheetId }).IsUnique()
            .HasFilter("[Status] IN (0, 1) AND [IsDeleted] = 0");
        builder.HasIndex(item => new { item.TenantId, item.WorkflowDefinitionId });
        builder.HasIndex(item => new { item.TenantId, item.WorkflowInstanceId });
        builder.HasOne(item => item.ScoreSheet)
            .WithMany(item => item.Recalls)
            .HasForeignKey(item => item.ScoreSheetId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.WorkflowDefinition).WithMany()
            .HasForeignKey(item => item.WorkflowDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.WorkflowInstance).WithMany()
            .HasForeignKey(item => item.WorkflowInstanceId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.WorkflowEvidenceDocument).WithMany()
            .HasForeignKey(item => item.WorkflowEvidenceDocumentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.FileUploadRecord).WithMany()
            .HasForeignKey(item => item.FileUploadRecordId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
