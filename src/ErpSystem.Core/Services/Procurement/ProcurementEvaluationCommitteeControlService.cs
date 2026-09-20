using ErpSystem.Shared;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Interfaces.Services;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ErpSystem.Core.Services.Procurement;

public sealed partial class ProcurementEvaluationCommitteeControlService
    : IProcurementEvaluationCommitteeControlService
{
    private const string EventType = "ProcurementEvaluationCommitteeControl";
    private const string ManagePermission = "procurement.tender.administer";
    private const string EvaluatePermission = "procurement.tender.evaluate";
    private const string ApprovePermission = "procurement.tender.approve";
    private const string RecallRuleCode = "TDC-EVALUATION-SCORE-RECALL";
    private const string AppointmentCreatedTopic =
        "ProcurementEvaluationCommittee.AppointmentCreated.Internal";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly List<string> DecisionKeys =
        Enumerable.Range(1, 14).Select(value => $"DEC-{value:000}").ToList();

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUser;
    private readonly IProcurementAccessControlService _accessControl;
    private readonly IProcurementSodGuardService _sodGuard;
    private readonly IProcurementControlEventService _controlEvents;
    private readonly IProcurementSourcingCaseService _sourcingCases;
    private readonly IWorkflowInstanceService _workflowInstances;
    private readonly INotificationTopicPublisher _notificationTopics;
    private readonly string _frontendUrl;

    public ProcurementEvaluationCommitteeControlService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUser,
        IProcurementAccessControlService accessControl,
        IProcurementSodGuardService sodGuard,
        IProcurementControlEventService controlEvents,
        IProcurementSourcingCaseService sourcingCases,
        IWorkflowInstanceService workflowInstances,
        INotificationTopicPublisher notificationTopics,
        IConfiguration configuration)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _accessControl = accessControl;
        _sodGuard = sodGuard;
        _controlEvents = controlEvents;
        _sourcingCases = sourcingCases;
        _workflowInstances = workflowInstances;
        _notificationTopics = notificationTopics;
        _frontendUrl = (configuration["FrontendUrl"] ?? "http://localhost:3000")
            .TrimEnd('/');
    }

    private IGenericRepository<ProcurementEvaluationCommitteeControl> Controls =>
        _unitOfWork.Repository<ProcurementEvaluationCommitteeControl>();
    private IGenericRepository<ProcurementEvaluationCommitteeRoleRequirement> RoleRequirements =>
        _unitOfWork.Repository<ProcurementEvaluationCommitteeRoleRequirement>();
    private IGenericRepository<ProcurementEvaluationCommitteeAppointment> Appointments =>
        _unitOfWork.Repository<ProcurementEvaluationCommitteeAppointment>();
    private IGenericRepository<ProcurementEvaluationConflictDeclaration> Declarations =>
        _unitOfWork.Repository<ProcurementEvaluationConflictDeclaration>();
    private IGenericRepository<ProcurementEvaluationMeeting> Meetings =>
        _unitOfWork.Repository<ProcurementEvaluationMeeting>();
    private IGenericRepository<ProcurementEvaluationAttendanceRecord> Attendance =>
        _unitOfWork.Repository<ProcurementEvaluationAttendanceRecord>();
    private IGenericRepository<ProcurementEvaluationScoreSheet> ScoreSheets =>
        _unitOfWork.Repository<ProcurementEvaluationScoreSheet>();
    private IGenericRepository<ProcurementEvaluationScoreRecall> Recalls =>
        _unitOfWork.Repository<ProcurementEvaluationScoreRecall>();
    private IGenericRepository<ProcurementCommittee> CommitteeTemplates =>
        _unitOfWork.Repository<ProcurementCommittee>();
    private IGenericRepository<BusinessPartnerUser> BusinessPartnerUsers =>
        _unitOfWork.Repository<BusinessPartnerUser>();
    private IGenericRepository<TenderInvitation> TenderInvitations =>
        _unitOfWork.Repository<TenderInvitation>();
    private IGenericRepository<RequestForQuotationInvitation> RfqInvitations =>
        _unitOfWork.Repository<RequestForQuotationInvitation>();
    private IGenericRepository<Tender> Tenders => _unitOfWork.Repository<Tender>();
    private IGenericRepository<RequestForQuotation> Rfqs =>
        _unitOfWork.Repository<RequestForQuotation>();
    private IGenericRepository<ProcurementTenderControl> TenderControls =>
        _unitOfWork.Repository<ProcurementTenderControl>();
    private IGenericRepository<ProcurementRfqEvaluation> RfqEvaluations =>
        _unitOfWork.Repository<ProcurementRfqEvaluation>();
    private IGenericRepository<TenderBid> TenderBids =>
        _unitOfWork.Repository<TenderBid>();
    private IGenericRepository<TenderEvaluator> TenderEvaluators =>
        _unitOfWork.Repository<TenderEvaluator>();
    private IGenericRepository<WorkflowDefinition> Workflows =>
        _unitOfWork.Repository<WorkflowDefinition>();
    private IGenericRepository<WorkflowInstance> WorkflowRows =>
        _unitOfWork.Repository<WorkflowInstance>();
    private IGenericRepository<WorkflowApproval> WorkflowApprovals =>
        _unitOfWork.Repository<WorkflowApproval>();

    public async Task<ProcurementEvaluationCommitteeReadinessDto> GetReadinessAsync(
        ProcurementEvaluationSourceType sourceType,
        Guid sourceId,
        CancellationToken cancellationToken = default)
    {
        EnsureReader();
        var source = await ResolveSourceAsync(sourceType, sourceId, cancellationToken);
        var control = await ControlQuery()
            .AsNoTracking()
            .OrderByDescending(item => item.Version)
            .FirstOrDefaultAsync(item => item.SourceType == sourceType && item.SourceId == sourceId,
                cancellationToken);
        if (control is null)
        {
            var blockedReasons = new List<string>();
            if (!source.CommitteePreparationGate.Allowed)
                blockedReasons.Add(source.CommitteePreparationGate.Message);
            blockedReasons.Add("No source-specific evaluation committee has been constituted.");
            return new ProcurementEvaluationCommitteeReadinessDto
            {
                SourceType = sourceType,
                SourceId = sourceId,
                SourceReference = source.Reference,
                SourceExists = true,
                HasControl = false,
                BlockedReasons = blockedReasons,
                AllowedActions = CanAdminister() && source.CommitteePreparationGate.Allowed
                    ? ["bind"]
                    : []
            };
        }

        return BuildReadiness(control, source);
    }

    public async Task<ProcurementEvaluationCommitteeOptionsDto> GetOptionsAsync(
        ProcurementEvaluationSourceType sourceType,
        Guid sourceId,
        CancellationToken cancellationToken = default)
    {
        EnsureReader();
        _ = await ResolveSourceAsync(sourceType, sourceId, cancellationToken);
        var now = DateTime.UtcNow;
        var committees = await CommitteeTemplates.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.CommitteeType == ProcurementCommitteeType.EvaluationCommittee &&
                item.Status == ProcurementCommitteeStatus.Active &&
                item.EffectiveFrom <= now &&
                (!item.EffectiveTo.HasValue || item.EffectiveTo.Value >= now) &&
                !item.IsDeleted)
            .Include(item => item.Members)
                .ThenInclude(item => item.Assignment)
                    .ThenInclude(item => item.User)
            .AsNoTracking()
            .OrderBy(item => item.Code)
            .ToListAsync(cancellationToken);
        var workflowOptions = await Workflows.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.IsActive &&
                item.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Published &&
                !item.IsDeleted)
            .AsNoTracking()
            .OrderBy(item => item.Name)
            .ThenByDescending(item => item.Version)
            .Select(item => new ProcurementEvaluationWorkflowOptionDto
            {
                Id = item.Id,
                Name = item.Name,
                Version = item.Version
            })
            .ToListAsync(cancellationToken);
        var users = committees.SelectMany(item => item.Members)
            .Where(item => IsCurrent(item, now) && IsCurrent(item.Assignment, now))
            .Select(item => item.Assignment)
            .GroupBy(item => item.UserId)
            .Select(group => group.First())
            .OrderBy(item => item.User.FirstName)
            .ThenBy(item => item.User.LastName)
            .Select(item => new ProcurementEvaluationUserOptionDto
            {
                UserId = item.UserId,
                Username = item.User.Email ?? item.User.UserName ?? string.Empty,
                DisplayName = DisplayName(item.User)
            })
            .ToList();
        return new ProcurementEvaluationCommitteeOptionsDto
        {
            Committees = committees.Select(item =>
            {
                var current = item.Members.Where(member =>
                    IsCurrent(member, now) && IsCurrent(member.Assignment, now)).ToList();
                var issues = CompositionIssues(item.RequiredQuorum, current);
                return new ProcurementEvaluationCommitteeTemplateOptionDto
                {
                    Id = item.Id,
                    Code = item.Code,
                    Name = item.Name,
                    RequiredQuorum = item.RequiredQuorum,
                    ActiveMemberCount = current.Count,
                    CompositionReady = issues.Count == 0,
                    Issues = issues
                };
            }).ToList(),
            Workflows = workflowOptions,
            Users = users
        };
    }

    public async Task<ProcurementEvaluationCommitteeDto> GetAsync(
        ProcurementEvaluationSourceType sourceType,
        Guid sourceId,
        CancellationToken cancellationToken = default)
    {
        EnsureReader();
        var source = await ResolveSourceAsync(sourceType, sourceId, cancellationToken);
        var control = await ControlQuery()
            .AsNoTracking()
            .OrderByDescending(item => item.Version)
            .FirstOrDefaultAsync(item => item.SourceType == sourceType && item.SourceId == sourceId,
                cancellationToken)
            ?? throw NotFound("EVALUATION_COMMITTEE_NOT_FOUND",
                $"No evaluation committee control exists for {source.Reference}.");
        return Map(control, source);
    }

    public async Task<ProcurementEvaluationCommitteeDto> BindAsync(
        BindProcurementEvaluationCommitteeRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        await EnforceCapabilityAsync(ManagePermission, request.SourceType, request.SourceId, null,
            correlationId, cancellationToken);
        Require(request.Purpose, "EVALUATION_COMMITTEE_PURPOSE_REQUIRED",
            "The committee purpose is required.");
        RequireIdempotency(request.IdempotencyKey);
        if (request.EffectiveFromUtc == default)
            throw Validation("EVALUATION_COMMITTEE_EFFECTIVE_FROM_REQUIRED",
                "EffectiveFromUtc is required.");
        if (request.EffectiveToUtc.HasValue &&
            request.EffectiveToUtc.Value < request.EffectiveFromUtc)
            throw Validation("EVALUATION_COMMITTEE_EFFECTIVE_RANGE_INVALID",
                "EffectiveToUtc cannot precede EffectiveFromUtc.");

        var source = await ResolveSourceAsync(request.SourceType, request.SourceId, cancellationToken);
        var replay = await ControlQuery().AsNoTracking()
            .FirstOrDefaultAsync(item =>
                item.SourceType == request.SourceType &&
                item.SourceId == request.SourceId &&
                item.CreationIdempotencyKey == request.IdempotencyKey,
                cancellationToken);
        if (replay is not null) return Map(replay, source);
        EnsureGate(source.CommitteePreparationGate);

        var existing = await Controls.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.SourceType == request.SourceType &&
                item.SourceId == request.SourceId &&
                (item.Status == ProcurementEvaluationCommitteeControlStatus.Draft ||
                 item.Status == ProcurementEvaluationCommitteeControlStatus.Active) &&
                !item.IsDeleted)
            .AnyAsync(cancellationToken);
        if (existing)
            throw Conflict("EVALUATION_COMMITTEE_ALREADY_BOUND",
                "This source already has a current evaluation committee control.");

        var now = DateTime.UtcNow;
        var committee = await CommitteeTemplates.GetQueryable(item =>
                item.Id == request.CommitteeTemplateId &&
                item.TenantId == _currentUser.TenantId &&
                item.CommitteeType == ProcurementCommitteeType.EvaluationCommittee &&
                item.Status == ProcurementCommitteeStatus.Active &&
                item.EffectiveFrom <= now &&
                (!item.EffectiveTo.HasValue || item.EffectiveTo.Value >= now) &&
                !item.IsDeleted)
            .Include(item => item.Members)
                .ThenInclude(item => item.Assignment)
                    .ThenInclude(item => item.User)
                        .ThenInclude(item => item.UserRoles)
                            .ThenInclude(item => item.Role)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw Validation("EVALUATION_COMMITTEE_TEMPLATE_INVALID",
                "The selected committee must be an active, current Evaluation Committee.");
        var members = committee.Members
            .Where(item => IsCurrent(item, now) && IsCurrent(item.Assignment, now))
            .OrderBy(item => item.MemberKind)
            .ThenBy(item => item.Assignment.RoleName)
            .ThenBy(item => item.Assignment.UserId)
            .ToList();
        await EnsureCommitteeMembersAreSupplierIndependentAsync(
            request.SourceType, request.SourceId, members, cancellationToken);
        var templateIssues = CompositionIssues(committee.RequiredQuorum, members);
        if (templateIssues.Count != 0)
            throw Validation("EVALUATION_COMMITTEE_TEMPLATE_INCOMPLETE",
                string.Join(" ", templateIssues));

        var requirements = NormalizeRequirements(request.RequiredRoles, committee, members);
        ValidateRequirements(requirements, members);
        var version = await Controls.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.SourceType == request.SourceType &&
                item.SourceId == request.SourceId)
            .Select(item => (int?)item.Version)
            .MaxAsync(cancellationToken) ?? 0;
        var control = new ProcurementEvaluationCommitteeControl
        {
            Id = Guid.NewGuid(),
            TenantId = _currentUser.TenantId,
            SourceType = request.SourceType,
            SourceId = request.SourceId,
            Version = version + 1,
            SourceReference = source.Reference,
            Purpose = request.Purpose.Trim(),
            Status = ProcurementEvaluationCommitteeControlStatus.Draft,
            CommitteeTemplateId = committee.Id,
            CommitteeCode = committee.Code,
            CommitteeName = committee.Name,
            RequiredQuorum = committee.RequiredQuorum,
            PolicySetId = source.PolicySetId,
            PolicyCode = source.PolicyCode,
            PolicyVersion = source.PolicyVersion,
            ConfigurationProfileId = source.ConfigurationProfileId,
            ConfigurationProfileCode = source.ConfigurationProfileCode,
            ConfigurationProfileVersion = source.ConfigurationProfileVersion,
            MethodRuleId = source.MethodRuleId,
            MethodRuleCode = source.MethodRuleCode,
            WorkflowDefinitionId = source.WorkflowDefinitionId,
            EffectiveFromUtc = request.EffectiveFromUtc,
            EffectiveToUtc = request.EffectiveToUtc,
            CreationIdempotencyKey = request.IdempotencyKey.Trim(),
            RowVersion = Guid.NewGuid().ToByteArray()
        };
        foreach (var requirement in requirements)
        {
            control.RequiredRoles.Add(new ProcurementEvaluationCommitteeRoleRequirement
            {
                Id = Guid.NewGuid(),
                TenantId = control.TenantId,
                MemberKind = requirement.MemberKind,
                RoleName = requirement.RoleName,
                MinimumCount = requirement.MinimumCount,
                IsVoting = requirement.IsVoting,
                IsRequiredForQuorum = requirement.IsRequiredForQuorum
            });
        }
        foreach (var member in members)
        {
            control.Appointments.Add(new ProcurementEvaluationCommitteeAppointment
            {
                Id = Guid.NewGuid(),
                TenantId = control.TenantId,
                CommitteeMemberId = member.Id,
                ResponsibilityAssignmentId = member.AssignmentId,
                UserId = member.Assignment.UserId,
                UserDisplayName = DisplayName(member.Assignment.User),
                RoleName = member.Assignment.RoleName,
                MemberKind = member.MemberKind,
                IsVoting = member.IsVoting,
                EffectiveFromUtc = MaxUtc(request.EffectiveFromUtc, member.EffectiveFrom,
                    member.Assignment.EffectiveFrom),
                EffectiveToUtc = MinUtc(request.EffectiveToUtc, member.EffectiveTo,
                    member.Assignment.EffectiveTo),
                Status = ProcurementEvaluationAppointmentStatus.Pending,
                RowVersion = Guid.NewGuid().ToByteArray()
            });
        }
        CaptureComposition(control);
        await Controls.AddAsync(control);
        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (
            IsCommitteeLineagePersistenceFailure(exception))
        {
            throw Conflict(
                "EVALUATION_COMMITTEE_SOURCE_LINEAGE_REJECTED",
                "The source's recorded procurement policy and configuration lineage could not be retained by the evaluation committee control. Refresh and retry; if the problem continues, an administrator must reconcile the source's immutable procurement lineage.");
        }
        await RecordAsync(control, "Bound", ProcurementControlEventResult.Succeeded,
            new { request.CommitteeTemplateId, request.Purpose, request.RequiredRoles },
            new { control.Id, control.Version, control.CompositionIntegrityHash },
            correlationId, cancellationToken);
        return Map(await LoadControlAsync(control.Id, false, cancellationToken), source);
    }

    public async Task<ProcurementEvaluationCommitteeDto> ActivateAsync(
        Guid committeeControlId,
        ActivateProcurementEvaluationCommitteeRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        RequireIdempotency(request.IdempotencyKey);
        var control = await LoadControlAsync(committeeControlId, true, cancellationToken);
        await EnforceCapabilityAsync(ManagePermission, control.SourceType, control.SourceId,
            null, correlationId, cancellationToken);
        if (control.ActivationIdempotencyKey == request.IdempotencyKey &&
            control.Status == ProcurementEvaluationCommitteeControlStatus.Active)
            return Map(control, await ResolveSourceAsync(control.SourceType, control.SourceId,
                cancellationToken));
        var source = await ResolveSourceAsync(control.SourceType, control.SourceId,
            cancellationToken);
        EnsureGate(source.CommitteePreparationGate);
        EnsureRowVersion(control.RowVersion, request.RowVersion, "EVALUATION_COMMITTEE");
        if (control.Status != ProcurementEvaluationCommitteeControlStatus.Draft)
            throw Conflict("EVALUATION_COMMITTEE_NOT_DRAFT",
                "Only a draft committee control can be activated.");
        var issues = CompositionIssues(control);
        if (issues.Count != 0)
            throw Validation("EVALUATION_COMMITTEE_COMPOSITION_INCOMPLETE",
                string.Join(" ", issues));
        var now = DateTime.UtcNow;
        if (control.EffectiveToUtc.HasValue && control.EffectiveToUtc.Value < now)
            throw Validation("EVALUATION_COMMITTEE_EXPIRED",
                "An expired committee control cannot be activated.");
        control.Status = ProcurementEvaluationCommitteeControlStatus.Active;
        control.ActivatedAtUtc = now;
        control.ActivatedByUserId = _currentUser.UserId;
        var evidenceReference = ResolveReference(request.EvidenceReference,
            "committee-activation", "evidence", control.Id, request.IdempotencyKey);
        control.ActivationEvidenceReference = evidenceReference;
        control.ActivationIdempotencyKey = request.IdempotencyKey.Trim();
        Touch(control, now);
        await Controls.UpdateAsync(control);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecordAsync(control, "Activated", ProcurementControlEventResult.Succeeded,
            new { EvidenceReference = evidenceReference },
            new { control.Status, control.ActivatedAtUtc },
            correlationId, cancellationToken,
            External(evidenceReference, "Committee constitution", "SRC-008"));
        foreach (var appointment in control.Appointments.Where(item =>
                     item.Status == ProcurementEvaluationAppointmentStatus.Pending))
            await NotifyAppointmentAsync(control, appointment, cancellationToken);
        return Map(control, source);
    }

    public async Task<ProcurementEvaluationAppointmentDto> RespondToAppointmentAsync(
        Guid appointmentId,
        RespondProcurementEvaluationAppointmentRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        RequireIdempotency(request.IdempotencyKey);
        var appointment = await AppointmentQuery(true)
            .FirstOrDefaultAsync(item =>
                item.Id == appointmentId && item.TenantId == _currentUser.TenantId,
                cancellationToken)
            ?? throw NotFound("EVALUATION_APPOINTMENT_NOT_FOUND",
                "The committee appointment was not found.");
        if (appointment.UserId != _currentUser.UserId)
            throw new ProcurementEvaluationCommitteeAuthorizationException(
                "Committee appointment responses are self-service only.");
        await EnforceCapabilityAsync(EvaluatePermission,
            appointment.CommitteeControl.SourceType, appointment.CommitteeControl.SourceId,
            appointment.CommitteeControl.CommitteeCode, correlationId, cancellationToken);
        if (appointment.AcceptanceIdempotencyKey == request.IdempotencyKey)
            return MapAppointment(appointment, DateTime.UtcNow);
        var source = await ResolveSourceAsync(appointment.CommitteeControl.SourceType,
            appointment.CommitteeControl.SourceId, cancellationToken);
        EnsureGate(source.CommitteePreparationGate);
        EnsureRowVersion(appointment.RowVersion, request.RowVersion, "EVALUATION_APPOINTMENT");
        if (appointment.Status != ProcurementEvaluationAppointmentStatus.Pending)
            throw Conflict("EVALUATION_APPOINTMENT_ALREADY_RESPONDED",
                "This appointment has already been accepted, declined, or withdrawn.");
        if (appointment.CommitteeControl.Status !=
            ProcurementEvaluationCommitteeControlStatus.Active)
            throw Conflict("EVALUATION_COMMITTEE_NOT_ACTIVE",
                "Appointment responses require an active committee control.");
        if (!request.Accept)
        {
            Require(request.Reason, "EVALUATION_APPOINTMENT_DECLINE_REASON_REQUIRED",
                "A decline reason is required.");
        }
        var now = DateTime.UtcNow;
        var signatureReference = request.Accept
            ? ResolveReference(request.SignatureReference, "appointment-acceptance",
                "signature", appointment.Id, request.IdempotencyKey)
            : null;
        var evidenceReference = request.Accept
            ? ResolveReference(request.EvidenceReference, "appointment-acceptance",
                "evidence", appointment.Id, request.IdempotencyKey)
            : null;
        appointment.Status = request.Accept
            ? ProcurementEvaluationAppointmentStatus.Accepted
            : ProcurementEvaluationAppointmentStatus.Declined;
        appointment.AcceptedAtUtc = request.Accept ? now : null;
        appointment.AcceptanceSignatureReference = signatureReference;
        appointment.AcceptanceEvidenceReference = evidenceReference;
        appointment.StatusReason = NullIfWhiteSpace(request.Reason);
        appointment.AcceptanceIdempotencyKey = request.IdempotencyKey.Trim();
        Touch(appointment, now);
        await Appointments.UpdateAsync(appointment);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecordAsync(appointment.CommitteeControl,
            request.Accept ? "AppointmentAccepted" : "AppointmentDeclined",
            request.Accept ? ProcurementControlEventResult.Succeeded :
                ProcurementControlEventResult.Rejected,
            new { appointment.Id, request.Accept, request.Reason },
            new { appointment.Status, appointment.AcceptedAtUtc },
            correlationId, cancellationToken,
            request.Accept
                ? External(evidenceReference!, "Appointment acceptance", "SRC-008")
                : null);
        return MapAppointment(appointment, now);
    }

    public async Task<ProcurementEvaluationAppointmentDto> SubmitConflictDeclarationAsync(
        Guid appointmentId,
        SubmitProcurementEvaluationConflictDeclarationRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        Require(request.Declaration, "EVALUATION_COI_DECLARATION_REQUIRED",
            "A conflict-of-interest declaration is required.");
        RequireIdempotency(request.IdempotencyKey);
        if (request.Outcome == ProcurementEvaluationConflictOutcome.ConflictDeclared)
        {
            Require(request.ConflictDetails, "EVALUATION_COI_DETAILS_REQUIRED",
                "Conflict details are required when a conflict is declared.");
            Require(request.SignatureReference, "EVALUATION_COI_SIGNATURE_REQUIRED",
                "A declared conflict must be signed.");
            Require(request.EvidenceReference, "EVALUATION_COI_EVIDENCE_REQUIRED",
                "Declared-conflict evidence is required.");
        }
        if (request.ValidFromUtc == default)
            throw Validation("EVALUATION_COI_VALID_FROM_REQUIRED", "ValidFromUtc is required.");
        if (request.ValidToUtc.HasValue && request.ValidToUtc.Value < request.ValidFromUtc)
            throw Validation("EVALUATION_COI_VALID_RANGE_INVALID",
                "ValidToUtc cannot precede ValidFromUtc.");
        await ValidateEvidenceAsync(request.WorkflowEvidenceDocumentId,
            request.FileUploadRecordId, cancellationToken);
        var appointment = await AppointmentQuery(true)
            .FirstOrDefaultAsync(item =>
                item.Id == appointmentId && item.TenantId == _currentUser.TenantId,
                cancellationToken)
            ?? throw NotFound("EVALUATION_APPOINTMENT_NOT_FOUND",
                "The committee appointment was not found.");
        if (appointment.UserId != _currentUser.UserId)
            throw new ProcurementEvaluationCommitteeAuthorizationException(
                "Conflict-of-interest declarations are self-service only.");
        await EnforceCapabilityAsync(EvaluatePermission,
            appointment.CommitteeControl.SourceType, appointment.CommitteeControl.SourceId,
            appointment.CommitteeControl.CommitteeCode, correlationId, cancellationToken);
        var replay = appointment.ConflictDeclarations
            .FirstOrDefault(item => item.IdempotencyKey == request.IdempotencyKey);
        if (replay is not null) return MapAppointment(appointment, DateTime.UtcNow);
        var source = await ResolveSourceAsync(appointment.CommitteeControl.SourceType,
            appointment.CommitteeControl.SourceId, cancellationToken);
        EnsureGate(source.CommitteePreparationGate);
        EnsureRowVersion(appointment.RowVersion, request.AppointmentRowVersion,
            "EVALUATION_APPOINTMENT");
        if (appointment.Status != ProcurementEvaluationAppointmentStatus.Accepted)
            throw Conflict("EVALUATION_APPOINTMENT_NOT_ACCEPTED",
                "The appointment must be accepted before declaring conflicts.");
        var version = appointment.ConflictDeclarations.Select(item => item.Version)
            .DefaultIfEmpty(0).Max() + 1;
        var now = DateTime.UtcNow;
        var declarationId = Guid.NewGuid();
        var signatureReference = ResolveReference(request.SignatureReference,
            "conflict-declaration", "signature", declarationId, request.IdempotencyKey);
        var evidenceReference = ResolveReference(request.EvidenceReference,
            "conflict-declaration", "evidence", declarationId, request.IdempotencyKey);
        var declaration = new ProcurementEvaluationConflictDeclaration
        {
            Id = declarationId,
            TenantId = appointment.TenantId,
            AppointmentId = appointment.Id,
            Version = version,
            Outcome = request.Outcome,
            Declaration = request.Declaration.Trim(),
            ConflictDetails = NullIfWhiteSpace(request.ConflictDetails),
            SignatureReference = signatureReference,
            EvidenceReference = evidenceReference,
            WorkflowEvidenceDocumentId = request.WorkflowEvidenceDocumentId,
            FileUploadRecordId = request.FileUploadRecordId,
            ValidFromUtc = request.ValidFromUtc,
            ValidToUtc = request.ValidToUtc,
            DeclaredAtUtc = now,
            DeclaredByUserId = _currentUser.UserId,
            IdempotencyKey = request.IdempotencyKey.Trim()
        };
        CaptureDeclaration(declaration);
        await Declarations.AddAsync(declaration);
        Touch(appointment, now);
        await Appointments.UpdateAsync(appointment);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecordAsync(appointment.CommitteeControl, "ConflictDeclarationSubmitted",
            request.Outcome == ProcurementEvaluationConflictOutcome.NoConflict
                ? ProcurementControlEventResult.Succeeded
                : ProcurementControlEventResult.Rejected,
            new { appointment.Id, declaration.Version, request.Outcome },
            new { declaration.Id, declaration.IntegrityHash },
            correlationId, cancellationToken,
            Evidence(evidenceReference, request.WorkflowEvidenceDocumentId,
                request.FileUploadRecordId, "Conflict declaration", "SRC-008"));
        return MapAppointment(appointment, now);
    }

    public async Task<ProcurementEvaluationMeetingDto> CreateMeetingAsync(
        Guid committeeControlId,
        CreateProcurementEvaluationMeetingRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        Require(request.MeetingMode, "EVALUATION_MEETING_MODE_REQUIRED",
            "Meeting mode is required.");
        Require(request.MeetingChannel, "EVALUATION_MEETING_CHANNEL_REQUIRED",
            "Meeting channel or venue is required.");
        RequireIdempotency(request.IdempotencyKey);
        if (request.ScheduledAtUtc == default)
            throw Validation("EVALUATION_MEETING_SCHEDULE_REQUIRED",
                "ScheduledAtUtc is required.");
        var meetingMode = NormalizeMeetingMode(request.MeetingMode);
        if (IsRemote(meetingMode))
            Require(request.RemoteMeetingEvidenceReference,
                "EVALUATION_REMOTE_MEETING_EVIDENCE_REQUIRED",
                "Remote or hybrid meetings require remote-meeting evidence.");
        var control = await LoadControlAsync(committeeControlId, true, cancellationToken);
        await EnforceCapabilityAsync(ManagePermission, control.SourceType, control.SourceId,
            null, correlationId, cancellationToken);
        var replay = control.Meetings.FirstOrDefault(item =>
            item.IdempotencyKey == request.IdempotencyKey);
        if (replay is not null) return MapMeeting(replay, DateTime.UtcNow);
        var source = await ResolveSourceAsync(control.SourceType, control.SourceId,
            cancellationToken);
        EnsureGate(source.MeetingGate);
        EnsureRowVersion(control.RowVersion, request.CommitteeRowVersion,
            "EVALUATION_COMMITTEE");
        EnsureActive(control, DateTime.UtcNow);
        var issues = CompositionIssues(control);
        if (issues.Count != 0)
            throw Validation("EVALUATION_COMMITTEE_COMPOSITION_INCOMPLETE",
                string.Join(" ", issues));
        var meetingId = Guid.NewGuid();
        var evidenceReference = ResolveReference(request.EvidenceReference,
            "meeting-scheduled", "evidence", meetingId, request.IdempotencyKey);
        var meeting = new ProcurementEvaluationMeeting
        {
            Id = meetingId,
            TenantId = control.TenantId,
            CommitteeControlId = control.Id,
            Sequence = control.Meetings.Select(item => item.Sequence).DefaultIfEmpty(0).Max() + 1,
            Phase = request.Phase,
            Status = ProcurementEvaluationMeetingStatus.Draft,
            MeetingMode = meetingMode,
            MeetingChannel = request.MeetingChannel.Trim(),
            ScheduledAtUtc = request.ScheduledAtUtc,
            EvidenceReference = evidenceReference,
            RemoteMeetingEvidenceReference =
                NullIfWhiteSpace(request.RemoteMeetingEvidenceReference),
            IdempotencyKey = request.IdempotencyKey.Trim(),
            QuorumIntegrityHash = ComputeHash("{}"),
            QuorumSnapshotJson = "{}",
            RowVersion = Guid.NewGuid().ToByteArray()
        };
        await Meetings.AddAsync(meeting);
        Touch(control, DateTime.UtcNow);
        await Controls.UpdateAsync(control);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecordAsync(control, "MeetingCreated", ProcurementControlEventResult.Succeeded,
            new { meeting.Phase, meeting.MeetingMode, meeting.ScheduledAtUtc },
            new { meeting.Id, meeting.Sequence }, correlationId, cancellationToken,
            External(meeting.EvidenceReference, "Evaluation meeting", "SRC-008"));
        foreach (var appointment in control.Appointments.Where(item =>
                     item.Status == ProcurementEvaluationAppointmentStatus.Accepted))
            await NotifyAsync("procurement.evaluation.meeting.created", control,
                appointment.UserId, meeting.Id, cancellationToken);
        return MapMeeting(meeting, DateTime.UtcNow);
    }

    public async Task<ProcurementEvaluationAttendanceDto> SignAttendanceAsync(
        Guid meetingId,
        SignProcurementEvaluationAttendanceRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        RequireIdempotency(request.IdempotencyKey);
        var meeting = await MeetingQuery(true)
            .FirstOrDefaultAsync(item =>
                item.Id == meetingId && item.TenantId == _currentUser.TenantId,
                cancellationToken)
            ?? throw NotFound("EVALUATION_MEETING_NOT_FOUND",
                "The evaluation meeting was not found.");
        await EnforceCapabilityAsync(EvaluatePermission,
            meeting.CommitteeControl.SourceType, meeting.CommitteeControl.SourceId,
            meeting.CommitteeControl.CommitteeCode, correlationId, cancellationToken);
        if (meeting.Status != ProcurementEvaluationMeetingStatus.Draft &&
            meeting.Status != ProcurementEvaluationMeetingStatus.QuorumFailed)
            throw Conflict("EVALUATION_ATTENDANCE_CLOSED",
                "Attendance cannot change after quorum is confirmed or the meeting is closed.");
        EnsureRowVersion(meeting.RowVersion, request.MeetingRowVersion,
            "EVALUATION_MEETING");
        var appointment = meeting.CommitteeControl.Appointments
            .FirstOrDefault(item => item.UserId == _currentUser.UserId)
            ?? throw new ProcurementEvaluationCommitteeAuthorizationException(
                "The current user is not appointed to this committee.");
        EnsureRowVersion(appointment.RowVersion, request.AppointmentRowVersion,
            "EVALUATION_APPOINTMENT");
        var replay = meeting.AttendanceRecords.FirstOrDefault(item =>
            item.IdempotencyKey == request.IdempotencyKey &&
            item.AppointmentId == appointment.Id);
        if (replay is not null) return MapAttendance(replay);
        var source = await ResolveSourceAsync(meeting.CommitteeControl.SourceType,
            meeting.CommitteeControl.SourceId, cancellationToken);
        EnsureGate(source.MeetingGate);
        if (meeting.AttendanceRecords.Any(item => item.AppointmentId == appointment.Id))
            throw Conflict("EVALUATION_ATTENDANCE_ALREADY_SIGNED",
                "Attendance has already been signed for this appointment.");
        var now = DateTime.UtcNow;
        var eligibility = AppointmentEligibilityIssues(appointment, now);
        if (eligibility.Count != 0)
            throw Validation("EVALUATION_ATTENDANCE_MEMBER_INELIGIBLE",
                string.Join(" ", eligibility));
        var attendanceId = Guid.NewGuid();
        var signatureReference = ResolveReference(request.SignatureReference,
            "attendance", "signature", attendanceId, request.IdempotencyKey);
        var evidenceReference = ResolveReference(request.EvidenceReference,
            "attendance", "evidence", attendanceId, request.IdempotencyKey);
        var attendance = new ProcurementEvaluationAttendanceRecord
        {
            Id = attendanceId,
            TenantId = meeting.TenantId,
            MeetingId = meeting.Id,
            AppointmentId = appointment.Id,
            IsPresent = request.IsPresent,
            SignedAtUtc = now,
            SignatureReference = signatureReference,
            EvidenceReference = evidenceReference,
            WasEligibleAtSignature = true,
            IdempotencyKey = request.IdempotencyKey.Trim()
        };
        CaptureAttendance(attendance, appointment);
        await Attendance.AddAsync(attendance);
        // A failed quorum is a historical attempt, not a live attendance tally.
        // Reopen the meeting before saving new attendance so the SQL lifecycle
        // guard does not compare it with that stale attempt. Retain its snapshot
        // and audit evidence; only ConfirmQuorumAsync can confirm the new quorum.
        if (meeting.Status == ProcurementEvaluationMeetingStatus.QuorumFailed)
            meeting.Status = ProcurementEvaluationMeetingStatus.Draft;
        Touch(meeting, now);
        await Meetings.UpdateAsync(meeting);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        attendance.Appointment = appointment;
        await RecordAsync(meeting.CommitteeControl, "AttendanceSigned",
            ProcurementControlEventResult.Succeeded,
            new
            {
                MeetingId = meeting.Id,
                AppointmentId = appointment.Id,
                request.IsPresent
            },
            new { attendance.Id, attendance.IntegrityHash },
            correlationId, cancellationToken,
            External(evidenceReference, "Signed attendance", "SRC-008"));
        return MapAttendance(attendance);
    }

    public async Task<ProcurementEvaluationMeetingDto> ConfirmQuorumAsync(
        Guid meetingId,
        ConfirmProcurementEvaluationQuorumRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        RequireIdempotency(request.IdempotencyKey);
        var meeting = await MeetingQuery(true)
            .FirstOrDefaultAsync(item =>
                item.Id == meetingId && item.TenantId == _currentUser.TenantId,
                cancellationToken)
            ?? throw NotFound("EVALUATION_MEETING_NOT_FOUND",
                "The evaluation meeting was not found.");
        await EnforceCapabilityAsync(ManagePermission,
            meeting.CommitteeControl.SourceType, meeting.CommitteeControl.SourceId,
            null, correlationId, cancellationToken);
        if (meeting.QuorumIdempotencyKey == request.IdempotencyKey &&
            meeting.Status is ProcurementEvaluationMeetingStatus.QuorumConfirmed or
                ProcurementEvaluationMeetingStatus.QuorumFailed)
            return MapMeeting(meeting, DateTime.UtcNow);
        var source = await ResolveSourceAsync(meeting.CommitteeControl.SourceType,
            meeting.CommitteeControl.SourceId, cancellationToken);
        EnsureGate(source.MeetingGate);
        EnsureRowVersion(meeting.RowVersion, request.RowVersion, "EVALUATION_MEETING");
        if (meeting.Status is ProcurementEvaluationMeetingStatus.QuorumConfirmed or
            ProcurementEvaluationMeetingStatus.Closed)
            throw Conflict("EVALUATION_QUORUM_ALREADY_FINAL",
                "Quorum has already been confirmed or the meeting is closed.");
        if (IsRemote(meeting.MeetingMode))
        {
            meeting.RemoteMeetingEvidenceReference =
                NullIfWhiteSpace(request.RemoteMeetingEvidenceReference) ??
                meeting.RemoteMeetingEvidenceReference;
            Require(meeting.RemoteMeetingEvidenceReference,
                "EVALUATION_REMOTE_MEETING_EVIDENCE_REQUIRED",
                "Remote or hybrid meetings require remote-meeting evidence.");
        }
        var now = DateTime.UtcNow;
        var eligible = meeting.CommitteeControl.Appointments
            .Where(item => AppointmentEligibilityIssues(item, now).Count == 0)
            .ToList();
        var signed = meeting.AttendanceRecords
            .Where(item => item.IsPresent && item.SignedAtUtc.HasValue &&
                           item.WasEligibleAtSignature &&
                           !string.IsNullOrWhiteSpace(item.SignatureReference))
            .Where(item => eligible.Any(member => member.Id == item.AppointmentId))
            .ToList();
        var signedAppointments = signed.Select(item => item.Appointment).ToList();
        var signedVoting = signedAppointments.Count(item => item.IsVoting);
        var chairPresent = signedAppointments.Any(item =>
            item.MemberKind == ProcurementCommitteeMemberKind.Chair);
        var secretaryPresent = signedAppointments.Any(item =>
            item.MemberKind == ProcurementCommitteeMemberKind.Secretary);
        var rolesPresent = meeting.CommitteeControl.RequiredRoles
            .Where(item => item.IsRequiredForQuorum)
            .All(requirement => signedAppointments.Count(item =>
                item.MemberKind == requirement.MemberKind &&
                string.Equals(item.RoleName, requirement.RoleName,
                    StringComparison.OrdinalIgnoreCase) &&
                (!requirement.IsVoting || item.IsVoting)) >= requirement.MinimumCount);
        var met = signedVoting >= meeting.CommitteeControl.RequiredQuorum &&
                  chairPresent && secretaryPresent && rolesPresent;
        meeting.EligibleVotingMemberCount = eligible.Count(item => item.IsVoting);
        meeting.SignedVotingAttendanceCount = signedVoting;
        meeting.ChairPresent = chairPresent;
        meeting.SecretaryPresent = secretaryPresent;
        meeting.QuorumMet = met;
        meeting.Status = met
            ? ProcurementEvaluationMeetingStatus.QuorumConfirmed
            : ProcurementEvaluationMeetingStatus.QuorumFailed;
        meeting.StartedAtUtc ??= now;
        var evidenceReference = ResolveReference(request.EvidenceReference,
            "quorum-confirmation", "evidence", meeting.Id, request.IdempotencyKey);
        meeting.EvidenceReference = evidenceReference;
        meeting.QuorumIdempotencyKey = request.IdempotencyKey.Trim();
        CaptureQuorum(meeting, signedAppointments);
        Touch(meeting, now);
        await Meetings.UpdateAsync(meeting);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecordAsync(meeting.CommitteeControl, "QuorumEvaluated",
            met ? ProcurementControlEventResult.Allowed :
                ProcurementControlEventResult.Denied,
            new { meeting.Id, EvidenceReference = evidenceReference },
            new
            {
                meeting.QuorumMet,
                meeting.SignedVotingAttendanceCount,
                meeting.ChairPresent,
                meeting.SecretaryPresent,
                meeting.QuorumIntegrityHash
            },
            correlationId, cancellationToken,
            External(evidenceReference, "Quorum confirmation", "SRC-008"));
        return MapMeeting(meeting, now);
    }

    public async Task<ProcurementEvaluationScorerEligibilityDto> EnsureScorerEligibleAsync(
        ProcurementEvaluationSourceType sourceType,
        Guid sourceId,
        ProcurementEvaluationPhase phase,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        await EnforceCapabilityAsync(EvaluatePermission, sourceType, sourceId, null,
            correlationId, cancellationToken);
        var source = await ResolveSourceAsync(sourceType, sourceId, cancellationToken);
        var control = await ControlQuery().AsNoTracking()
            .OrderByDescending(item => item.Version)
            .FirstOrDefaultAsync(item =>
                item.SourceType == sourceType &&
                item.SourceId == sourceId &&
                item.Status == ProcurementEvaluationCommitteeControlStatus.Active,
                cancellationToken);
        var result = new ProcurementEvaluationScorerEligibilityDto
        {
            SourceType = sourceType,
            SourceId = sourceId,
            Phase = phase,
            ActorUserId = _currentUser.UserId
        };
        if (control is null)
        {
            result.BlockedReasons.Add("No active evaluation committee control exists.");
            return result;
        }
        result.CommitteeControlId = control.Id;
        var now = DateTime.UtcNow;
        if (!IsEffective(control, now))
            result.BlockedReasons.Add("The evaluation committee is not currently effective.");
        var appointment = control.Appointments.FirstOrDefault(item =>
            item.UserId == _currentUser.UserId);
        if (appointment is null)
        {
            result.BlockedReasons.Add("The current actor is not appointed to this committee.");
        }
        else
        {
            result.AppointmentId = appointment.Id;
            result.BlockedReasons.AddRange(AppointmentEligibilityIssues(appointment, now));
        }
        var meeting = control.Meetings
            .Where(item => item.Phase == phase &&
                           item.Status == ProcurementEvaluationMeetingStatus.QuorumConfirmed &&
                           item.QuorumMet)
            .OrderByDescending(item => item.Sequence)
            .FirstOrDefault();
        if (meeting is null)
        {
            result.BlockedReasons.Add(
                "No meeting with server-confirmed quorum exists for this evaluation phase.");
        }
        else
        {
            result.MeetingId = meeting.Id;
            if (appointment is not null && !meeting.AttendanceRecords.Any(item =>
                    item.AppointmentId == appointment.Id &&
                    item.IsPresent &&
                    item.SignedAtUtc.HasValue &&
                    item.WasEligibleAtSignature &&
                    !string.IsNullOrWhiteSpace(item.SignatureReference)))
                result.BlockedReasons.Add(
                    "The current actor has no eligible signed attendance for the quorum-confirmed meeting.");
        }
        result.Allowed = result.BlockedReasons.Count == 0;
        if (!result.Allowed)
        {
            await RecordAsync(control, "ScorerEligibilityDenied",
                ProcurementControlEventResult.Denied,
                new { sourceType, sourceId, phase, ActorUserId = _currentUser.UserId },
                new { result.BlockedReasons }, correlationId, cancellationToken);
        }
        return result;
    }

    public async Task<ProcurementEvaluationScoreSheetDto> LockScoreSheetAsync(
        LockProcurementEvaluationScoreSheetRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        Require(request.ScoreSubjectType, "EVALUATION_SCORE_SUBJECT_TYPE_REQUIRED",
            "ScoreSubjectType is required.");
        Require(request.ScoreSnapshotJson, "EVALUATION_SCORE_SNAPSHOT_REQUIRED",
            "A score snapshot is required.");
        Require(request.SignatureReference, "EVALUATION_SCORE_SIGNATURE_REQUIRED",
            "The score sheet must be signed.");
        Require(request.EvidenceReference, "EVALUATION_SCORE_EVIDENCE_REQUIRED",
            "Score-sheet evidence is required.");
        RequireIdempotency(request.IdempotencyKey);
        ValidateJson(request.ScoreSnapshotJson, "EVALUATION_SCORE_SNAPSHOT_INVALID");
        var eligibility = await EnsureScorerEligibleAsync(request.SourceType,
            request.SourceId, request.Phase, correlationId, cancellationToken);
        if (!eligibility.Allowed)
            throw Validation("EVALUATION_SCORER_INELIGIBLE",
                string.Join(" ", eligibility.BlockedReasons));
        if (eligibility.MeetingId != request.MeetingId)
            throw Validation("EVALUATION_MEETING_MISMATCH",
                "The selected meeting is not the current quorum-confirmed phase meeting.");
        if (eligibility.AppointmentId != request.AppointmentId)
            throw new ProcurementEvaluationCommitteeAuthorizationException(
                "The score-sheet appointment does not belong to the current actor.");
        await ValidateScoreSubjectAsync(request, cancellationToken);
        var control = await LoadControlAsync(eligibility.CommitteeControlId!.Value, true,
            cancellationToken);
        var appointment = control.Appointments.Single(item =>
            item.Id == request.AppointmentId);
        var meeting = control.Meetings.Single(item => item.Id == request.MeetingId);
        EnsureRowVersion(control.RowVersion, request.CommitteeRowVersion,
            "EVALUATION_COMMITTEE");
        EnsureRowVersion(meeting.RowVersion, request.MeetingRowVersion,
            "EVALUATION_MEETING");
        EnsureRowVersion(appointment.RowVersion, request.AppointmentRowVersion,
            "EVALUATION_APPOINTMENT");
        var replay = control.ScoreSheets.FirstOrDefault(item =>
            item.IdempotencyKey == request.IdempotencyKey);
        if (replay is not null) return MapScoreSheet(replay);
        var attempts = control.ScoreSheets.Where(item =>
                item.Phase == request.Phase &&
                item.AppointmentId == appointment.Id &&
                item.ScoreSubjectType == request.ScoreSubjectType.Trim() &&
                item.ScoreSubjectId == request.ScoreSubjectId)
            .OrderBy(item => item.Attempt)
            .ToList();
        var current = attempts.LastOrDefault();
        if (current is not null)
        {
            var approvedRecall = current.Recalls.SingleOrDefault(item =>
                item.Status == ProcurementEvaluationScoreRecallStatus.Approved);
            if (approvedRecall?.AuthorizedNewAttempt != current.Attempt + 1)
                throw Conflict("EVALUATION_SCORE_ALREADY_LOCKED",
                    "The current score-sheet attempt is locked and has no approved recall.");
        }
        var attempt = current?.Attempt + 1 ?? 1;
        var now = DateTime.UtcNow;
        var scoreSheet = new ProcurementEvaluationScoreSheet
        {
            Id = Guid.NewGuid(),
            TenantId = control.TenantId,
            CommitteeControlId = control.Id,
            MeetingId = meeting.Id,
            AppointmentId = appointment.Id,
            Phase = request.Phase,
            ScoreSubjectType = request.ScoreSubjectType.Trim(),
            ScoreSubjectId = request.ScoreSubjectId,
            Attempt = attempt,
            Status = ProcurementEvaluationScoreSheetStatus.Locked,
            SubmittedAtUtc = now,
            SubmittedByUserId = _currentUser.UserId,
            SubmittedByName = ActorName(),
            ScoreSnapshotJson = NormalizeJson(request.ScoreSnapshotJson),
            SignatureReference = request.SignatureReference.Trim(),
            EvidenceReference = request.EvidenceReference.Trim(),
            IdempotencyKey = request.IdempotencyKey.Trim(),
            RowVersion = Guid.NewGuid().ToByteArray()
        };
        CaptureScoreSheet(scoreSheet);
        try
        {
            await ScoreSheets.AddAsync(scoreSheet);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            throw Conflict("EVALUATION_SCORE_ATTEMPT_CONFLICT",
                $"A concurrent or duplicate score-sheet attempt was rejected: {exception.GetBaseException().Message}");
        }
        await RecordAsync(control, "ScoreSheetLocked",
            ProcurementControlEventResult.Succeeded,
            new
            {
                request.Phase,
                request.ScoreSubjectType,
                request.ScoreSubjectId,
                scoreSheet.Attempt
            },
            new { scoreSheet.Id, scoreSheet.IntegrityHash },
            correlationId, cancellationToken,
            External(request.EvidenceReference, "Signed score sheet", "SRC-008"));
        return MapScoreSheet(scoreSheet);
    }

    public async Task<ProcurementEvaluationScorerEligibilityDto>
        EnsureScoreSubjectEligibleAsync(
            ProcurementEvaluationSourceType sourceType,
            Guid sourceId,
            ProcurementEvaluationPhase phase,
            string scoreSubjectType,
            Guid scoreSubjectId,
            string correlationId,
            CancellationToken cancellationToken = default)
    {
        Require(scoreSubjectType, "EVALUATION_SCORE_SUBJECT_TYPE_REQUIRED",
            "ScoreSubjectType is required.");
        var result = await EnsureScorerEligibleAsync(sourceType, sourceId, phase,
            correlationId, cancellationToken);
        if (!result.Allowed || !result.CommitteeControlId.HasValue ||
            !result.AppointmentId.HasValue)
            return result;
        await ValidateScoreSubjectAsync(new LockProcurementEvaluationScoreSheetRequest
        {
            SourceType = sourceType,
            SourceId = sourceId,
            Phase = phase,
            ScoreSubjectType = scoreSubjectType,
            ScoreSubjectId = scoreSubjectId
        }, cancellationToken);
        var sheets = await ScoreSheets.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.CommitteeControlId == result.CommitteeControlId.Value &&
                item.AppointmentId == result.AppointmentId.Value &&
                item.Phase == phase &&
                item.ScoreSubjectType == scoreSubjectType.Trim() &&
                item.ScoreSubjectId == scoreSubjectId &&
                !item.IsDeleted)
            .Include(item => item.Recalls)
            .AsNoTracking()
            .OrderBy(item => item.Attempt)
            .ToListAsync(cancellationToken);
        var current = sheets.LastOrDefault();
        if (current is null)
        {
            result.AuthorizedAttempt = 1;
            return result;
        }
        var approvedRecall = current.Recalls.SingleOrDefault(item =>
            item.Status == ProcurementEvaluationScoreRecallStatus.Approved);
        if (approvedRecall?.AuthorizedNewAttempt == current.Attempt + 1)
        {
            result.AuthorizedAttempt = approvedRecall.AuthorizedNewAttempt.Value;
            return result;
        }
        result.Allowed = false;
        result.AuthorizedAttempt = current.Attempt;
        result.BlockedReasons.Add(
            "The current subject score-sheet attempt is locked and has no approved recall.");
        return result;
    }

    public async Task<ProcurementEvaluationScoreRecallDto> RequestScoreRecallAsync(
        Guid scoreSheetId,
        RequestProcurementEvaluationScoreRecallRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        Require(request.Reason, "EVALUATION_SCORE_RECALL_REASON_REQUIRED",
            "A recall reason is required.");
        Require(request.EvidenceReference, "EVALUATION_SCORE_RECALL_EVIDENCE_REQUIRED",
            "Recall evidence is required.");
        RequireIdempotency(request.IdempotencyKey);
        await ValidateEvidenceAsync(request.WorkflowEvidenceDocumentId,
            request.FileUploadRecordId, cancellationToken);
        var scoreSheet = await ScoreSheetQuery(true)
            .FirstOrDefaultAsync(item =>
                item.Id == scoreSheetId && item.TenantId == _currentUser.TenantId,
                cancellationToken)
            ?? throw NotFound("EVALUATION_SCORE_SHEET_NOT_FOUND",
                "The locked score sheet was not found.");
        await EnforceCapabilityAsync(EvaluatePermission,
            scoreSheet.CommitteeControl.SourceType, scoreSheet.CommitteeControl.SourceId,
            scoreSheet.CommitteeControl.CommitteeCode, correlationId, cancellationToken);
        if (scoreSheet.SubmittedByUserId != _currentUser.UserId)
            throw new ProcurementEvaluationCommitteeAuthorizationException(
                "Only the scorer who submitted this score sheet may request its recall.");
        EnsureRowVersion(scoreSheet.RowVersion, request.ScoreSheetRowVersion,
            "EVALUATION_SCORE_SHEET");
        await EnsureRecallWindowOpenAsync(scoreSheet, cancellationToken);
        var replay = scoreSheet.Recalls.FirstOrDefault(item =>
            item.IdempotencyKey == request.IdempotencyKey);
        if (replay is not null) return MapRecall(replay);
        if (scoreSheet.Recalls.Any(item =>
                item.Status is ProcurementEvaluationScoreRecallStatus.PendingApproval or
                    ProcurementEvaluationScoreRecallStatus.Approved))
            throw Conflict("EVALUATION_SCORE_RECALL_ALREADY_EXISTS",
                "This score-sheet attempt already has a pending or approved recall.");
        var workflow = await LoadWorkflowAsync(request.WorkflowDefinitionId,
            cancellationToken);
        var now = DateTime.UtcNow;
        var recall = new ProcurementEvaluationScoreRecall
        {
            Id = Guid.NewGuid(),
            TenantId = scoreSheet.TenantId,
            ScoreSheetId = scoreSheet.Id,
            Status = ProcurementEvaluationScoreRecallStatus.PendingApproval,
            Reason = request.Reason.Trim(),
            EvidenceReference = request.EvidenceReference.Trim(),
            WorkflowEvidenceDocumentId = request.WorkflowEvidenceDocumentId,
            FileUploadRecordId = request.FileUploadRecordId,
            WorkflowDefinitionId = workflow.Id,
            RequestedByUserId = _currentUser.UserId,
            RequestedByName = ActorName(),
            RequestedAtUtc = now,
            IdempotencyKey = request.IdempotencyKey.Trim(),
            RowVersion = Guid.NewGuid().ToByteArray()
        };
        CaptureRecall(recall);
        try
        {
            await _unitOfWork.ExecuteInStrategyAsync(async () =>
            {
                await _unitOfWork.BeginTransactionAsync(cancellationToken);
                try
                {
                    await Recalls.AddAsync(recall);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                    var instance = await _workflowInstances.StartWorkflowAsync(
                        workflow.Id,
                        workflow.EntityTypeId,
                        recall.Id.ToString(),
                        _currentUser.UserId,
                        new
                        {
                            scoreSheet.Id,
                            scoreSheet.CommitteeControl.SourceType,
                            scoreSheet.CommitteeControl.SourceId,
                            scoreSheet.Phase,
                            scoreSheet.ScoreSubjectType,
                            scoreSheet.ScoreSubjectId,
                            scoreSheet.Attempt,
                            recall.Reason
                        },
                        cancellationToken);
                    recall.WorkflowInstanceId = instance.Id;
                    Touch(recall, DateTime.UtcNow);
                    CaptureRecall(recall);
                    await Recalls.UpdateAsync(recall);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                    await _unitOfWork.CommitAsync(cancellationToken);
                }
                catch
                {
                    await _unitOfWork.RollbackAsync(cancellationToken);
                    throw;
                }
            }, cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            throw Conflict("EVALUATION_SCORE_RECALL_CONFLICT",
                $"A concurrent or duplicate recall request was rejected: {exception.GetBaseException().Message}");
        }
        await RecordAsync(scoreSheet.CommitteeControl, "ScoreRecallRequested",
            ProcurementControlEventResult.ReviewRequired,
            new { scoreSheet.Id, recall.Reason, recall.WorkflowDefinitionId },
            new { recall.Id, recall.WorkflowInstanceId, recall.IntegrityHash },
            correlationId, cancellationToken,
            Evidence(request.EvidenceReference, request.WorkflowEvidenceDocumentId,
                request.FileUploadRecordId, "Score recall request", "SRC-008"));
        await NotifyAsync("procurement.evaluation.score-recall.requested",
            scoreSheet.CommitteeControl, null, recall.Id, cancellationToken);
        return MapRecall(recall);
    }

    public async Task<ProcurementEvaluationScoreRecallDto> DecideScoreRecallAsync(
        Guid recallId,
        DecideProcurementEvaluationScoreRecallRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        Require(request.DecisionReference,
            "EVALUATION_SCORE_RECALL_DECISION_REFERENCE_REQUIRED",
            "An independent decision reference is required.");
        Require(request.EvidenceReference,
            "EVALUATION_SCORE_RECALL_DECISION_EVIDENCE_REQUIRED",
            "Independent decision evidence is required.");
        RequireIdempotency(request.IdempotencyKey);
        var recall = await RecallQuery(true)
            .FirstOrDefaultAsync(item =>
                item.Id == recallId && item.TenantId == _currentUser.TenantId,
                cancellationToken)
            ?? throw NotFound("EVALUATION_SCORE_RECALL_NOT_FOUND",
                "The score-sheet recall was not found.");
        await EnforceCapabilityAsync(ApprovePermission,
            recall.ScoreSheet.CommitteeControl.SourceType,
            recall.ScoreSheet.CommitteeControl.SourceId,
            null,
            correlationId, cancellationToken);
        if (recall.DecisionIdempotencyKey == request.IdempotencyKey &&
            recall.Status != ProcurementEvaluationScoreRecallStatus.PendingApproval)
            return MapRecall(recall);
        EnsureRowVersion(recall.RowVersion, request.RowVersion,
            "EVALUATION_SCORE_RECALL");
        if (recall.Status != ProcurementEvaluationScoreRecallStatus.PendingApproval)
            throw Conflict("EVALUATION_SCORE_RECALL_NOT_PENDING",
                "Only a pending recall may be decided.");
        if (!recall.WorkflowInstanceId.HasValue)
            throw Conflict("EVALUATION_SCORE_RECALL_WORKFLOW_MISSING",
                "The recall has no shared-workflow instance.");
        var workflow = await WorkflowRows.GetQueryable(item =>
                item.Id == recall.WorkflowInstanceId.Value &&
                item.TenantId == _currentUser.TenantId &&
                !item.IsDeleted)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw Conflict("EVALUATION_SCORE_RECALL_WORKFLOW_MISSING",
                "The recall shared-workflow instance is unavailable.");
        if (workflow.WorkflowDefinitionId != recall.WorkflowDefinitionId)
            throw Conflict("EVALUATION_SCORE_RECALL_WORKFLOW_MISMATCH",
                "The recall workflow no longer matches its exact definition.");
        var workflowDefinition = await Workflows.GetQueryable(item =>
                item.Id == recall.WorkflowDefinitionId &&
                item.TenantId == _currentUser.TenantId &&
                !item.IsDeleted)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw Conflict("EVALUATION_SCORE_RECALL_WORKFLOW_MISSING",
                "The recall workflow definition is unavailable.");
        if (workflow.EntityId != recall.Id ||
            workflow.EntityTypeId != workflowDefinition.EntityTypeId)
            throw Conflict("EVALUATION_SCORE_RECALL_WORKFLOW_SUBJECT_MISMATCH",
                "The workflow instance is not bound to this exact recall and entity type.");
        if (request.Approve && workflow.Status != WorkflowInstanceStatus.Completed)
            throw Conflict("EVALUATION_SCORE_RECALL_APPROVAL_INCOMPLETE",
                "Recall approval requires a Completed shared workflow.");
        if (!request.Approve &&
            workflow.Status is not (WorkflowInstanceStatus.Cancelled or
                WorkflowInstanceStatus.Failed))
            throw Conflict("EVALUATION_SCORE_RECALL_REJECTION_INCOMPLETE",
                "Recall rejection requires a Cancelled or Failed shared workflow.");
        var processedActors = await WorkflowApprovals.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.StepInstance.WorkflowInstanceId == workflow.Id &&
                item.ProcessedDate.HasValue &&
                item.Status != WorkflowApprovalStatus.Pending &&
                !item.IsDeleted)
            .AsNoTracking()
            .Select(item => item.ProcessedById ?? item.ApproverId)
            .Where(item => item.HasValue)
            .Select(item => item!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);
        if (processedActors.Count == 0)
            throw Conflict("EVALUATION_SCORE_RECALL_APPROVAL_HISTORY_MISSING",
                "The recall workflow has no processed approval history.");
        var prohibitedWorkflowActors = new[]
        {
            recall.RequestedByUserId,
            recall.ScoreSheet.SubmittedByUserId
        };
        if (processedActors.Any(prohibitedWorkflowActors.Contains))
            throw Conflict("EVALUATION_SCORE_RECALL_WORKFLOW_SOD_CONFLICT",
                "The scorer or recall requester cannot approve the shared recall workflow.");
        var sod = await _sodGuard.EnforceAsync(new ProcurementSodGuardRequest
        {
            // A recall is a procurement transaction: its requester and original
            // scorer remain makers, and its decision must use the shared checker.
            ControlCode = "SOD-INITIATOR-APPROVER",
            SourceType = EventType,
            SourceReference = recall.ScoreSheet.CommitteeControl.SourceReference,
            ProhibitedActorUserIds = new List<Guid>
                {
                    recall.RequestedByUserId,
                    recall.ScoreSheet.SubmittedByUserId
                }
                .Distinct().ToList()
        }, correlationId, cancellationToken);
        if (!sod.Allowed)
            throw new ProcurementEvaluationCommitteeAuthorizationException(sod.Message);
        var now = DateTime.UtcNow;
        recall.Status = request.Approve
            ? ProcurementEvaluationScoreRecallStatus.Approved
            : ProcurementEvaluationScoreRecallStatus.Rejected;
        recall.DecidedByUserId = _currentUser.UserId;
        recall.DecidedByName = ActorName();
        recall.DecidedAtUtc = now;
        recall.DecisionReference = request.DecisionReference.Trim();
        recall.DecisionEvidenceReference = request.EvidenceReference.Trim();
        recall.DecisionIdempotencyKey = request.IdempotencyKey.Trim();
        recall.AuthorizedNewAttempt =
            request.Approve ? recall.ScoreSheet.Attempt + 1 : null;
        Touch(recall, now);
        CaptureRecall(recall);
        await Recalls.UpdateAsync(recall);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecordAsync(recall.ScoreSheet.CommitteeControl,
            request.Approve ? "ScoreRecallApproved" : "ScoreRecallRejected",
            request.Approve ? ProcurementControlEventResult.Succeeded :
                ProcurementControlEventResult.Rejected,
            new { recall.Id, request.Approve, request.DecisionReference },
            new
            {
                recall.Status,
                recall.AuthorizedNewAttempt,
                recall.DecidedByUserId,
                recall.IntegrityHash
            },
            correlationId, cancellationToken,
            External(request.EvidenceReference, "Score recall decision", "SRC-008"));
        await NotifyAsync("procurement.evaluation.score-recall.decided",
            recall.ScoreSheet.CommitteeControl, recall.RequestedByUserId, recall.Id,
            cancellationToken);
        return MapRecall(recall);
    }

    private IQueryable<ProcurementEvaluationCommitteeControl> ControlQuery() =>
        Controls.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .Include(item => item.RequiredRoles)
            .Include(item => item.Appointments)
                .ThenInclude(item => item.ConflictDeclarations)
            .Include(item => item.Meetings)
                .ThenInclude(item => item.AttendanceRecords)
                    .ThenInclude(item => item.Appointment)
            .Include(item => item.ScoreSheets)
                .ThenInclude(item => item.Recalls);

    private IQueryable<ProcurementEvaluationCommitteeAppointment> AppointmentQuery(bool tracked) =>
        Query(Appointments.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .Include(item => item.CommitteeControl)
                .ThenInclude(item => item.RequiredRoles)
            .Include(item => item.ConflictDeclarations), tracked);

    private IQueryable<ProcurementEvaluationMeeting> MeetingQuery(bool tracked) =>
        Query(Meetings.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .Include(item => item.CommitteeControl)
                .ThenInclude(item => item.RequiredRoles)
            .Include(item => item.CommitteeControl)
                .ThenInclude(item => item.Appointments)
                    .ThenInclude(item => item.ConflictDeclarations)
            .Include(item => item.AttendanceRecords)
                .ThenInclude(item => item.Appointment), tracked);

    private IQueryable<ProcurementEvaluationScoreSheet> ScoreSheetQuery(bool tracked) =>
        Query(ScoreSheets.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .Include(item => item.CommitteeControl)
            .Include(item => item.Appointment)
            .Include(item => item.Meeting)
            .Include(item => item.Recalls), tracked);

    private IQueryable<ProcurementEvaluationScoreRecall> RecallQuery(bool tracked) =>
        Query(Recalls.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .Include(item => item.ScoreSheet)
                .ThenInclude(item => item.CommitteeControl), tracked);

    private static IQueryable<T> Query<T>(IQueryable<T> query, bool tracked)
        where T : class => tracked ? query : query.AsNoTracking();

    private async Task<ProcurementEvaluationCommitteeControl> LoadControlAsync(
        Guid id,
        bool tracked,
        CancellationToken cancellationToken)
    {
        var query = ControlQuery();
        if (!tracked) query = query.AsNoTracking();
        return await query.FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
               ?? throw NotFound("EVALUATION_COMMITTEE_NOT_FOUND",
                   "The evaluation committee control was not found.");
    }

    private async Task EnsureCommitteeMembersAreSupplierIndependentAsync(
        ProcurementEvaluationSourceType sourceType,
        Guid sourceId,
        IReadOnlyCollection<ProcurementCommitteeMember> members,
        CancellationToken cancellationToken)
    {
        var memberUserIds = members
            .Select(item => item.Assignment.UserId)
            .Distinct()
            .ToList();
        if (memberUserIds.Count == 0) return;

        var hasExternalUser = members.Any(member =>
            member.Assignment.User.UserRoles.Any(userRole =>
                string.Equals(userRole.Role.Name, Constants.Roles.ExternalUser,
                    StringComparison.OrdinalIgnoreCase)));
        if (hasExternalUser)
            throw Validation(
                "EVALUATION_COMMITTEE_EXTERNAL_MEMBER_PROHIBITED",
                "External supplier users cannot be appointed to an evaluation committee.");

        var invitedSupplierIds = sourceType == ProcurementEvaluationSourceType.Tender
            ? await TenderInvitations.GetQueryable(item =>
                    item.TenantId == _currentUser.TenantId &&
                    item.TenderId == sourceId &&
                    !item.IsDeleted)
                .Select(item => item.BusinessPartnerId)
                .Distinct()
                .ToListAsync(cancellationToken)
            : await RfqInvitations.GetQueryable(item =>
                    item.TenantId == _currentUser.TenantId &&
                    item.RfqId == sourceId &&
                    !item.IsDeleted)
                .Select(item => item.BusinessPartnerId)
                .Distinct()
                .ToListAsync(cancellationToken);
        if (invitedSupplierIds.Count == 0) return;

        var hasInvitedSupplierLink = await BusinessPartnerUsers.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                memberUserIds.Contains(item.UserId) &&
                invitedSupplierIds.Contains(item.BusinessPartnerId) &&
                item.IsActive &&
                !item.IsDeleted)
            .AnyAsync(cancellationToken);
        if (hasInvitedSupplierLink)
            throw Validation(
                "EVALUATION_COMMITTEE_INVITED_SUPPLIER_MEMBER_PROHIBITED",
                "Users linked to a supplier invited to this procurement source cannot be appointed to its evaluation committee.");
    }

    private async Task<SourceLineage> ResolveSourceAsync(
        ProcurementEvaluationSourceType sourceType,
        Guid sourceId,
        CancellationToken cancellationToken)
    {
        if (sourceId == Guid.Empty)
            throw Validation("EVALUATION_SOURCE_REQUIRED", "SourceId is required.");
        if (sourceType == ProcurementEvaluationSourceType.Tender)
        {
            var tender = await LoadTenderSourceAsync(sourceId, cancellationToken);
            if (tender.SourcingCase is null)
                tender = await RecoverTenderSourceLineageAsync(tender, cancellationToken);
            return await ApplyTenderEvaluationLifecycleAsync(
                SourceLineage.From(tender), tender, cancellationToken);
        }
        var rfq = await Rfqs.GetQueryable(item =>
                item.Id == sourceId &&
                item.TenantId == _currentUser.TenantId &&
                !item.IsDeleted)
            .Include(item => item.SourcingCase!)
                .ThenInclude(item => item.PolicySet)
                    .ThenInclude(item => item.SourceConfigurationProfile)
            .Include(item => item.SourcingCase!)
                .ThenInclude(item => item.MethodRule)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw NotFound("EVALUATION_SOURCE_NOT_FOUND", "The RFQ was not found.");
        return SourceLineage.From(rfq);
    }

    private async Task<SourceLineage> ApplyTenderEvaluationLifecycleAsync(
        SourceLineage source,
        Tender tender,
        CancellationToken cancellationToken)
    {
        var state = await TenderControls.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.TenderId == tender.Id &&
                !item.IsDeleted)
            .AsNoTracking()
            .Select(item => new
            {
                item.Status,
                item.SubmissionDeadlineUtc,
                HasOnTimeSubmission = item.SubmissionReceipts.Any(receipt =>
                    !receipt.IsDeleted &&
                    receipt.Disposition ==
                    ProcurementTenderSubmissionDisposition.OnTimeAccepted)
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (state is null)
        {
            var sourcing = tender.SourcingCase;
            var requiresAdvanced = sourcing is null || ProcurementTenderRouting.RequiresControlledLifecycle(
                sourcing.SelectedMethod,
                ProcurementTenderRouting.HasAdvancedAuthority(sourcing.AuthorityRouteId, sourcing.AuthorityRouteReference));
            if (!requiresAdvanced && (tender.Status is "Published" or "Closed" or "Awarded") &&
                tender.SubmissionDeadline.HasValue)
            {
                var hasSubmission = await _unitOfWork.Repository<TenderBid>().GetQueryable(bid =>
                        bid.TenantId == _currentUser.TenantId && bid.TenderId == tender.Id && !bid.IsDeleted &&
                        (bid.Status == "Submitted" || bid.Status == "Opened" || bid.Status == "UnderEvaluation" ||
                         bid.Status == "Evaluated" || bid.Status == "Awarded") &&
                        bid.SubmittedDate != default && bid.SubmittedDate <= tender.SubmissionDeadline.Value)
                    .AnyAsync(cancellationToken);
                var preparation = hasSubmission ? LifecycleGate.Ready : LifecycleGate.Blocked(
                    "EVALUATION_COMMITTEE_BID_SUBMISSION_REQUIRED",
                    "At least one on-time sealed bid must be registered before the evaluation committee is constituted or begins member actions.");
                return source with
                {
                    CommitteePreparationGate = preparation,
                    MeetingGate = !hasSubmission ? preparation : DateTime.UtcNow < tender.SubmissionDeadline.Value
                        ? LifecycleGate.Blocked("EVALUATION_MEETING_BEFORE_SUBMISSION_DEADLINE",
                            "The bidding window must close before an evaluation meeting, attendance or quorum can be recorded.")
                        : LifecycleGate.Ready
                };
            }
            var notPublished = LifecycleGate.Blocked(
                "EVALUATION_COMMITTEE_TENDER_NOT_PUBLISHED",
                "Publish the approved tender and open its governed bidding window before constituting the evaluation committee.");
            return source with
            {
                CommitteePreparationGate = notPublished,
                MeetingGate = notPublished
            };
        }

        if (!state.HasOnTimeSubmission)
        {
            var noSubmission = LifecycleGate.Blocked(
                "EVALUATION_COMMITTEE_BID_SUBMISSION_REQUIRED",
                "At least one on-time sealed bid must be registered before the evaluation committee is constituted or begins member actions.");
            return source with
            {
                CommitteePreparationGate = noSubmission,
                MeetingGate = noSubmission
            };
        }

        var committeeGate = LifecycleGate.Ready;
        var meetingGate = DateTime.UtcNow < state.SubmissionDeadlineUtc
            ? LifecycleGate.Blocked(
                "EVALUATION_MEETING_BEFORE_SUBMISSION_DEADLINE",
                "The bidding window must close before an evaluation meeting, attendance or quorum can be recorded.")
            : LifecycleGate.Ready;
        return source with
        {
            CommitteePreparationGate = committeeGate,
            MeetingGate = meetingGate
        };
    }

    private static void EnsureGate(LifecycleGate gate)
    {
        if (!gate.Allowed)
            throw Conflict(gate.Code, gate.Message);
    }

    private async Task<Tender> LoadTenderSourceAsync(
        Guid sourceId,
        CancellationToken cancellationToken) =>
        await Tenders.GetQueryable(item =>
                    item.Id == sourceId &&
                    item.TenantId == _currentUser.TenantId &&
                    !item.IsDeleted)
                .Include(item => item.SourcingCase!)
                    .ThenInclude(item => item.PolicySet)
                        .ThenInclude(item => item.SourceConfigurationProfile)
                .Include(item => item.SourcingCase!)
                    .ThenInclude(item => item.MethodRule)
                .AsNoTracking()
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw NotFound("EVALUATION_SOURCE_NOT_FOUND", "The tender was not found.");

    private async Task<Tender> RecoverTenderSourceLineageAsync(
        Tender tender,
        CancellationToken cancellationToken)
    {
        if (!tender.SourcePurchaseRequisitionId.HasValue ||
            tender.SourcePurchaseRequisitionId.Value == Guid.Empty)
            throw Validation("EVALUATION_SOURCE_LINEAGE_MISSING",
                "The tender has no approved purchase-requisition lineage from which its sourcing case can be recovered.");

        var isRfq = string.Equals(tender.TenderType?.Trim(), "RFQ",
            StringComparison.OrdinalIgnoreCase);
        if (isRfq)
            throw Validation("EVALUATION_SOURCE_LINEAGE_MISSING",
                "RFQ evaluation must use the governed request-for-quotation record rather than a tender shell.");
        var gate = await _sourcingCases.RecoverTenderSourceEntryAsync(
            tender.SourcePurchaseRequisitionId.Value,
            tender.SourcingReleaseId,
            tender.Id,
            tender.TenderNumber,
            $"evaluation-lineage:{tender.Id:N}",
            cancellationToken);

        if (!gate.SourcingCaseId.HasValue || gate.SourcingCaseId.Value == Guid.Empty)
            throw Validation("EVALUATION_SOURCE_LINEAGE_MISSING",
                "The tender's current immutable release has no locked sourcing case.");
        if (tender.SourcingReleaseId.HasValue &&
            tender.SourcingReleaseId.Value != gate.SourcingReleaseId)
            throw Validation("EVALUATION_SOURCE_LINEAGE_MISMATCH",
                "The tender does not match the current immutable sourcing release.");
        if (tender.SourcingCaseId.HasValue &&
            tender.SourcingCaseId.Value != gate.SourcingCaseId.Value)
            throw Validation("EVALUATION_SOURCE_LINEAGE_MISMATCH",
                "The tender does not match the sourcing case locked to its immutable release.");

        tender.SourcingReleaseId = gate.SourcingReleaseId;
        tender.SourcingCaseId = gate.SourcingCaseId;
        tender.UpdatedAt = DateTime.UtcNow;
        try
        {
            await Tenders.UpdateAsync(tender);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (
            exception.GetBaseException().Message.Contains(
                "PR-linked Tender requires an approved requisition",
                StringComparison.OrdinalIgnoreCase))
        {
            throw Validation(
                "EVALUATION_SOURCE_LINEAGE_PERSISTENCE_REJECTED",
                "The tender's validated sourcing-case lineage could not be retained. Refresh the tender and retry the evaluation.");
        }

        return await LoadTenderSourceAsync(tender.Id, cancellationToken);
    }

    private async Task<WorkflowDefinition> LoadWorkflowAsync(
        Guid workflowDefinitionId,
        CancellationToken cancellationToken) =>
        await Workflows.GetQueryable(item =>
                item.Id == workflowDefinitionId &&
                item.TenantId == _currentUser.TenantId &&
                item.IsActive &&
                item.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Published &&
                !item.IsDeleted)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken)
        ?? throw Validation("EVALUATION_SCORE_RECALL_WORKFLOW_INVALID",
            "Recall requires an active Published shared workflow definition.");

    private async Task EnsureRecallWindowOpenAsync(
        ProcurementEvaluationScoreSheet scoreSheet,
        CancellationToken cancellationToken)
    {
        if (scoreSheet.CommitteeControl.SourceType ==
            ProcurementEvaluationSourceType.RequestForQuotation)
        {
            var rfq = await Rfqs.GetQueryable(item =>
                    item.Id == scoreSheet.CommitteeControl.SourceId &&
                    item.TenantId == scoreSheet.TenantId &&
                    !item.IsDeleted)
                .AsNoTracking()
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw NotFound("EVALUATION_SOURCE_NOT_FOUND",
                    "The RFQ was not found.");
            if (string.Equals(rfq.Status, "Awarded", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(rfq.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
                throw Conflict("EVALUATION_SCORE_RECALL_DOWNSTREAM_FINAL",
                    "RFQ scores cannot be recalled after award or cancellation.");
            var evaluation = await RfqEvaluations.GetQueryable(item =>
                    item.RfqId == rfq.Id &&
                    item.TenantId == rfq.TenantId &&
                    !item.IsDeleted)
                .AsNoTracking()
                .FirstOrDefaultAsync(cancellationToken);
            if (evaluation?.Status is ProcurementRfqEvaluationStatus.Approved or
                ProcurementRfqEvaluationStatus.Rejected)
                throw Conflict("EVALUATION_SCORE_RECALL_DOWNSTREAM_FINAL",
                    "RFQ scores cannot be recalled after the evaluation decision is final.");
            return;
        }

        var tender = await Tenders.GetQueryable(item =>
                item.Id == scoreSheet.CommitteeControl.SourceId &&
                item.TenantId == scoreSheet.TenantId &&
                !item.IsDeleted)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw NotFound("EVALUATION_SOURCE_NOT_FOUND",
                "The tender was not found.");
        if (string.Equals(tender.Status, "Awarded", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(tender.Status, "Cancelled", StringComparison.OrdinalIgnoreCase) ||
            tender.AwardDate.HasValue)
            throw Conflict("EVALUATION_SCORE_RECALL_DOWNSTREAM_FINAL",
                "Tender scores cannot be recalled after award or cancellation.");
        var statutory = await TenderControls.GetQueryable(item =>
                item.TenderId == tender.Id &&
                item.TenantId == tender.TenantId &&
                !item.IsDeleted)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);
        if (statutory is null) return;
        if (scoreSheet.Phase == ProcurementEvaluationPhase.Technical &&
            statutory.Status != ProcurementTenderControlStatus.TechnicalEvaluated)
            throw Conflict("EVALUATION_SCORE_RECALL_DOWNSTREAM_FINAL",
                "Technical scores cannot be recalled after financial evaluation or approval has progressed.");
        if (scoreSheet.Phase is ProcurementEvaluationPhase.Financial or
                ProcurementEvaluationPhase.Combined &&
            statutory.Status != ProcurementTenderControlStatus.FinancialEvaluated)
            throw Conflict("EVALUATION_SCORE_RECALL_DOWNSTREAM_FINAL",
                "Financial or combined scores cannot be recalled after approval or award has progressed.");
    }

    private async Task ValidateScoreSubjectAsync(
        LockProcurementEvaluationScoreSheetRequest request,
        CancellationToken cancellationToken)
    {
        var subjectType = request.ScoreSubjectType.Trim();
        if (string.Equals(subjectType, "ProcurementRfqEvaluation",
                StringComparison.OrdinalIgnoreCase))
        {
            if (request.SourceType !=
                    ProcurementEvaluationSourceType.RequestForQuotation ||
                request.Phase != ProcurementEvaluationPhase.Combined)
                throw Validation("EVALUATION_SCORE_SUBJECT_MISMATCH",
                    "RFQ score projections require a RequestForQuotation source and Combined phase.");
            var evaluationExists = await RfqEvaluations.GetQueryable(item =>
                    item.Id == request.ScoreSubjectId &&
                    item.RfqId == request.SourceId &&
                    item.TenantId == _currentUser.TenantId &&
                    !item.IsDeleted)
                .AnyAsync(cancellationToken);
            if (!evaluationExists)
                throw Validation("EVALUATION_SCORE_SUBJECT_NOT_FOUND",
                    "The RFQ evaluation projection does not belong to this tenant and RFQ.");
            return;
        }
        if (string.Equals(subjectType, "ProcurementTenderControl",
                StringComparison.OrdinalIgnoreCase))
        {
            if (request.SourceType != ProcurementEvaluationSourceType.Tender ||
                request.ScoreSubjectId != request.SourceId ||
                request.Phase == ProcurementEvaluationPhase.Combined)
                throw Validation("EVALUATION_SCORE_SUBJECT_MISMATCH",
                    "NCT/ICT projections require the exact tender source and Technical or Financial phase.");
            var exists = await TenderControls.GetQueryable(item =>
                    item.TenderId == request.SourceId &&
                    item.TenantId == _currentUser.TenantId &&
                    !item.IsDeleted)
                .AnyAsync(cancellationToken);
            if (!exists)
                throw Validation("EVALUATION_SCORE_SUBJECT_NOT_FOUND",
                    "The tender statutory control was not found.");
            return;
        }
        if (string.Equals(subjectType, "TenderEvaluation",
                StringComparison.OrdinalIgnoreCase))
        {
            if (request.SourceType != ProcurementEvaluationSourceType.Tender)
                throw Validation("EVALUATION_SCORE_SUBJECT_MISMATCH",
                    "Legacy tender evaluation subjects require a Tender source.");
            var bidExists = await TenderBids.GetQueryable(item =>
                    item.Id == request.ScoreSubjectId &&
                    item.TenderId == request.SourceId &&
                    item.TenantId == _currentUser.TenantId &&
                    !item.IsDeleted)
                .AnyAsync(cancellationToken);
            if (!bidExists)
                throw Validation("EVALUATION_SCORE_SUBJECT_NOT_FOUND",
                    "The tender bid score subject does not belong to this tenant and tender.");
            var evaluatorOwnsProjection = await TenderEvaluators.GetQueryable(item =>
                    item.TenderId == request.SourceId &&
                    item.UserId == _currentUser.UserId &&
                    item.TenantId == _currentUser.TenantId &&
                    item.Status != "Declined" &&
                    !item.IsDeleted)
                .AnyAsync(cancellationToken);
            if (!evaluatorOwnsProjection)
                throw new ProcurementEvaluationCommitteeAuthorizationException(
                    "The current scorer has no active legacy tender-evaluator assignment.");
            return;
        }
        throw Validation("EVALUATION_SCORE_SUBJECT_TYPE_UNSUPPORTED",
            "ScoreSubjectType must be ProcurementRfqEvaluation, ProcurementTenderControl, or TenderEvaluation.");
    }

    private async Task ValidateEvidenceAsync(
        Guid? workflowEvidenceDocumentId,
        Guid? fileUploadRecordId,
        CancellationToken cancellationToken)
    {
        if (workflowEvidenceDocumentId.HasValue)
        {
            var document = await _unitOfWork.Repository<WorkflowEvidenceDocument>()
                .GetQueryable(item =>
                    item.Id == workflowEvidenceDocumentId.Value &&
                    item.TenantId == _currentUser.TenantId &&
                    item.IsCurrent &&
                    !item.IsDeleted)
                .AsNoTracking()
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw Validation("EVALUATION_EVIDENCE_NOT_FOUND",
                    "The workflow evidence document is unavailable.");
            if (document.MalwareScanStatus is WorkflowMalwareScanStatus.Infected or
                WorkflowMalwareScanStatus.Failed ||
                document.VerificationStatus == WorkflowEvidenceVerificationStatus.Rejected)
                throw Validation("EVALUATION_EVIDENCE_UNSAFE",
                    "Rejected, infected, or failed evidence cannot be used.");
        }
        if (fileUploadRecordId.HasValue)
        {
            var upload = await _unitOfWork.Repository<FileUploadRecord>()
                .GetQueryable(item =>
                    item.Id == fileUploadRecordId.Value &&
                    item.TenantId == _currentUser.TenantId &&
                    !item.IsDeleted)
                .AsNoTracking()
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw Validation("EVALUATION_UPLOAD_NOT_FOUND",
                    "The evidence upload is unavailable.");
            if (upload.VirusScanStatus == FileVirusScanStatus.Infected)
                throw Validation("EVALUATION_UPLOAD_UNSAFE",
                    "Infected evidence cannot be used.");
        }
    }

    private async Task EnforceCapabilityAsync(
        string permissionCode,
        ProcurementEvaluationSourceType sourceType,
        Guid sourceId,
        string? committeeCode,
        string correlationId,
        CancellationToken cancellationToken)
    {
        EnsureAuthenticatedTenant();
        var decision = await _accessControl.EnforceCapabilityAsync(
            new ProcurementAccessCapabilityRequest
            {
                PermissionCode = permissionCode,
                CommitteeCode = committeeCode,
                SourceType = EventType,
                SourceReference = $"{sourceType}:{sourceId:N}"
            }, correlationId, cancellationToken);
        if (!decision.Allowed)
            throw new ProcurementEvaluationCommitteeAuthorizationException(decision.Message);
    }

    private ProcurementEvaluationCommitteeReadinessDto BuildReadiness(
        ProcurementEvaluationCommitteeControl control,
        SourceLineage source)
    {
        var now = DateTime.UtcNow;
        var compositionIssues = CompositionIssues(control);
        var eligible = control.Appointments.Where(item =>
            AppointmentEligibilityIssues(item, now).Count == 0).ToList();
        var latestMeeting = control.Meetings
            .OrderByDescending(item => item.Sequence)
            .FirstOrDefault();
        var blocked = new List<string>();
        if (control.Status != ProcurementEvaluationCommitteeControlStatus.Active)
            blocked.Add("The committee control is not active.");
        if (!source.CommitteePreparationGate.Allowed)
            blocked.Add(source.CommitteePreparationGate.Message);
        if (!source.MeetingGate.Allowed)
            blocked.Add(source.MeetingGate.Message);
        if (!IsEffective(control, now))
            blocked.Add("The committee control is not currently effective.");
        blocked.AddRange(compositionIssues);
        if (control.Appointments.Any(item =>
                item.Status != ProcurementEvaluationAppointmentStatus.Accepted))
            blocked.Add("Every current appointment has not been accepted.");
        if (eligible.Count != control.Appointments.Count)
            blocked.Add("Every accepted member does not have a current no-conflict declaration.");
        if (latestMeeting?.QuorumMet != true)
            blocked.Add("No evaluation meeting has server-confirmed quorum.");
        return new ProcurementEvaluationCommitteeReadinessDto
        {
            SourceType = control.SourceType,
            SourceId = control.SourceId,
            SourceReference = source.Reference,
            SourceExists = true,
            HasControl = true,
            CommitteeControlId = control.Id,
            Status = control.Status,
            CompositionReady = compositionIssues.Count == 0,
            AppointmentsReady = control.Appointments.Count != 0 &&
                                control.Appointments.All(item =>
                                    item.Status ==
                                    ProcurementEvaluationAppointmentStatus.Accepted),
            DeclarationsReady = eligible.Count == control.Appointments.Count,
            QuorumMet = latestMeeting?.QuorumMet == true,
            RequiredQuorum = control.RequiredQuorum,
            EligibleVotingMemberCount = eligible.Count(item => item.IsVoting),
            SignedVotingAttendanceCount =
                latestMeeting?.SignedVotingAttendanceCount ?? 0,
            BlockedReasons = blocked.Distinct().ToList(),
            AllowedActions = BuildAllowedActions(control, source, now)
        };
    }

    private ProcurementEvaluationCommitteeDto Map(
        ProcurementEvaluationCommitteeControl control,
        SourceLineage source)
    {
        var now = DateTime.UtcNow;
        var issues = CompositionIssues(control);
        var latestMeeting = control.Meetings.OrderByDescending(item => item.Sequence)
            .FirstOrDefault();
        return new ProcurementEvaluationCommitteeDto
        {
            Id = control.Id,
            SourceType = control.SourceType,
            SourceId = control.SourceId,
            Version = control.Version,
            SourceReference = source.Reference,
            Purpose = control.Purpose,
            Status = control.Status,
            CommitteeTemplateId = control.CommitteeTemplateId,
            CommitteeCode = control.CommitteeCode,
            CommitteeName = control.CommitteeName,
            RequiredQuorum = control.RequiredQuorum,
            CompositionReady = issues.Count == 0,
            QuorumMet = latestMeeting?.QuorumMet == true,
            PolicySetId = control.PolicySetId,
            PolicyCode = control.PolicyCode,
            PolicyVersion = control.PolicyVersion,
            ConfigurationProfileId = control.ConfigurationProfileId,
            ConfigurationProfileCode = control.ConfigurationProfileCode,
            ConfigurationProfileVersion = control.ConfigurationProfileVersion,
            MethodRuleId = control.MethodRuleId,
            MethodRuleCode = control.MethodRuleCode,
            WorkflowDefinitionId = control.WorkflowDefinitionId,
            WorkflowInstanceId = control.WorkflowInstanceId,
            EffectiveFromUtc = control.EffectiveFromUtc,
            EffectiveToUtc = control.EffectiveToUtc,
            ActivatedAtUtc = control.ActivatedAtUtc,
            ActivatedByUserId = control.ActivatedByUserId,
            ActivationEvidenceReference = control.ActivationEvidenceReference,
            RetiredAtUtc = control.RetiredAtUtc,
            RetiredByUserId = control.RetiredByUserId,
            RetirementReason = control.RetirementReason,
            RetirementEvidenceReference = control.RetirementEvidenceReference,
            CompositionIntegrityHash = control.CompositionIntegrityHash,
            RequiredRoles = control.RequiredRoles.OrderBy(item => item.MemberKind)
                .ThenBy(item => item.RoleName)
                .Select(item =>
                {
                    var matched = control.Appointments.Count(member =>
                        member.MemberKind == item.MemberKind &&
                        string.Equals(member.RoleName, item.RoleName,
                            StringComparison.OrdinalIgnoreCase) &&
                        (!item.IsVoting || member.IsVoting));
                    return new ProcurementEvaluationRoleRequirementDto
                    {
                        Id = item.Id,
                        MemberKind = item.MemberKind,
                        RoleName = item.RoleName,
                        MinimumCount = item.MinimumCount,
                        IsVoting = item.IsVoting,
                        IsRequiredForQuorum = item.IsRequiredForQuorum,
                        MatchedCount = matched,
                        IsMet = matched >= item.MinimumCount
                    };
                }).ToList(),
            Members = control.Appointments.OrderBy(item => item.MemberKind)
                .ThenBy(item => item.UserDisplayName)
                .Select(item => MapAppointment(item, now)).ToList(),
            Meetings = control.Meetings.OrderByDescending(item => item.Sequence)
                .Select(item => MapMeeting(item, now)).ToList(),
            ScoreSheets = control.ScoreSheets.OrderByDescending(item => item.SubmittedAtUtc)
                .Select(MapScoreSheet).ToList(),
            Recalls = control.ScoreSheets.SelectMany(item => item.Recalls)
                .OrderByDescending(item => item.RequestedAtUtc)
                .Select(MapRecall).ToList(),
            Timeline = BuildTimeline(control),
            AllowedActions = BuildAllowedActions(control, source, now),
            BlockedReasons = BuildReadiness(control, source).BlockedReasons,
            RowVersion = Convert.ToBase64String(control.RowVersion)
        };
    }

    private static ProcurementEvaluationAppointmentDto MapAppointment(
        ProcurementEvaluationCommitteeAppointment appointment,
        DateTime now)
    {
        var declaration = CurrentDeclaration(appointment, now);
        var blocked = AppointmentEligibilityIssues(appointment, now);
        return new ProcurementEvaluationAppointmentDto
        {
            Id = appointment.Id,
            CommitteeMemberId = appointment.CommitteeMemberId,
            ResponsibilityAssignmentId = appointment.ResponsibilityAssignmentId,
            UserId = appointment.UserId,
            UserDisplayName = appointment.UserDisplayName,
            RoleName = appointment.RoleName,
            MemberKind = appointment.MemberKind,
            IsVoting = appointment.IsVoting,
            EffectiveFromUtc = appointment.EffectiveFromUtc,
            EffectiveToUtc = appointment.EffectiveToUtc,
            Status = appointment.Status,
            AcceptedAtUtc = appointment.AcceptedAtUtc,
            AcceptanceSignatureReference = appointment.AcceptanceSignatureReference,
            AcceptanceEvidenceReference = appointment.AcceptanceEvidenceReference,
            CurrentDeclaration = declaration is null ? null : MapDeclaration(declaration),
            EligibleToScore = blocked.Count == 0,
            BlockedReasons = blocked,
            RowVersion = Convert.ToBase64String(appointment.RowVersion)
        };
    }

    private static ProcurementEvaluationConflictDeclarationDto MapDeclaration(
        ProcurementEvaluationConflictDeclaration declaration) => new()
    {
        Id = declaration.Id,
        Version = declaration.Version,
        Outcome = declaration.Outcome,
        Declaration = declaration.Declaration,
        ConflictDetails = declaration.ConflictDetails,
        SignatureReference = declaration.SignatureReference,
        EvidenceReference = declaration.EvidenceReference,
        ValidFromUtc = declaration.ValidFromUtc,
        ValidToUtc = declaration.ValidToUtc,
        DeclaredAtUtc = declaration.DeclaredAtUtc,
        DeclaredByUserId = declaration.DeclaredByUserId,
        IntegrityHash = declaration.IntegrityHash
    };

    private static ProcurementEvaluationMeetingDto MapMeeting(
        ProcurementEvaluationMeeting meeting,
        DateTime now) => new()
    {
        Id = meeting.Id,
        Sequence = meeting.Sequence,
        Phase = meeting.Phase,
        Status = meeting.Status,
        MeetingMode = meeting.MeetingMode,
        MeetingChannel = meeting.MeetingChannel,
        ScheduledAtUtc = meeting.ScheduledAtUtc,
        StartedAtUtc = meeting.StartedAtUtc,
        ClosedAtUtc = meeting.ClosedAtUtc,
        EligibleVotingMemberCount = meeting.EligibleVotingMemberCount,
        SignedVotingAttendanceCount = meeting.SignedVotingAttendanceCount,
        ChairPresent = meeting.ChairPresent,
        SecretaryPresent = meeting.SecretaryPresent,
        QuorumMet = meeting.QuorumMet,
        EvidenceReference = meeting.EvidenceReference,
        RemoteMeetingEvidenceReference = meeting.RemoteMeetingEvidenceReference,
        QuorumIntegrityHash = meeting.QuorumIntegrityHash,
        Attendance = meeting.AttendanceRecords.OrderBy(item =>
                item.Appointment.UserDisplayName)
            .Select(MapAttendance).ToList(),
        RowVersion = Convert.ToBase64String(meeting.RowVersion)
    };

    private static ProcurementEvaluationAttendanceDto MapAttendance(
        ProcurementEvaluationAttendanceRecord attendance) => new()
    {
        Id = attendance.Id,
        AppointmentId = attendance.AppointmentId,
        UserId = attendance.Appointment.UserId,
        UserDisplayName = attendance.Appointment.UserDisplayName,
        MemberKind = attendance.Appointment.MemberKind,
        IsVoting = attendance.Appointment.IsVoting,
        IsPresent = attendance.IsPresent,
        SignedAtUtc = attendance.SignedAtUtc,
        SignatureReference = attendance.SignatureReference,
        EvidenceReference = attendance.EvidenceReference,
        WasEligibleAtSignature = attendance.WasEligibleAtSignature,
        IntegrityHash = attendance.IntegrityHash
    };

    private static ProcurementEvaluationScoreSheetDto MapScoreSheet(
        ProcurementEvaluationScoreSheet scoreSheet)
    {
        var recalled = scoreSheet.Recalls.Any(item =>
            item.Status == ProcurementEvaluationScoreRecallStatus.Approved);
        return new ProcurementEvaluationScoreSheetDto
        {
            Id = scoreSheet.Id,
            MeetingId = scoreSheet.MeetingId,
            AppointmentId = scoreSheet.AppointmentId,
            SubmittedByUserId = scoreSheet.SubmittedByUserId,
            SubmittedByName = scoreSheet.SubmittedByName,
            Phase = scoreSheet.Phase,
            ScoreSubjectType = scoreSheet.ScoreSubjectType,
            ScoreSubjectId = scoreSheet.ScoreSubjectId,
            Attempt = scoreSheet.Attempt,
            Status = recalled
                ? ProcurementEvaluationScoreSheetStatus.Recalled
                : ProcurementEvaluationScoreSheetStatus.Locked,
            SubmittedAtUtc = scoreSheet.SubmittedAtUtc,
            ScoreSnapshotJson = scoreSheet.ScoreSnapshotJson,
            SignatureReference = scoreSheet.SignatureReference,
            EvidenceReference = scoreSheet.EvidenceReference,
            IntegrityHash = scoreSheet.IntegrityHash,
            RowVersion = Convert.ToBase64String(scoreSheet.RowVersion)
        };
    }

    private static ProcurementEvaluationScoreRecallDto MapRecall(
        ProcurementEvaluationScoreRecall recall) => new()
    {
        Id = recall.Id,
        ScoreSheetId = recall.ScoreSheetId,
        Status = recall.Status,
        Reason = recall.Reason,
        EvidenceReference = recall.EvidenceReference,
        WorkflowDefinitionId = recall.WorkflowDefinitionId,
        WorkflowInstanceId = recall.WorkflowInstanceId,
        RequestedByUserId = recall.RequestedByUserId,
        RequestedByName = recall.RequestedByName,
        RequestedAtUtc = recall.RequestedAtUtc,
        DecidedByUserId = recall.DecidedByUserId,
        DecidedByName = recall.DecidedByName,
        DecidedAtUtc = recall.DecidedAtUtc,
        DecisionReference = recall.DecisionReference,
        DecisionEvidenceReference = recall.DecisionEvidenceReference,
        AuthorizedNewAttempt = recall.AuthorizedNewAttempt,
        IntegrityHash = recall.IntegrityHash,
        RowVersion = Convert.ToBase64String(recall.RowVersion)
    };

    private static List<ProcurementEvaluationTimelineEntryDto> BuildTimeline(
        ProcurementEvaluationCommitteeControl control)
    {
        var result = new List<ProcurementEvaluationTimelineEntryDto>();
        if (control.ActivatedAtUtc.HasValue)
            result.Add(new ProcurementEvaluationTimelineEntryDto
            {
                OccurredAtUtc = control.ActivatedAtUtc.Value,
                Action = "Committee activated",
                Outcome = "Active",
                ActorUserId = control.ActivatedByUserId ?? Guid.Empty,
                Reference = control.ActivationEvidenceReference
            });
        if (control.RetiredAtUtc.HasValue)
            result.Add(new ProcurementEvaluationTimelineEntryDto
            {
                OccurredAtUtc = control.RetiredAtUtc.Value,
                Action = "Draft committee retired",
                Outcome = control.RetirementReason ?? "Retired",
                ActorUserId = control.RetiredByUserId ?? Guid.Empty,
                Reference = control.RetirementEvidenceReference
            });
        result.AddRange(control.Appointments
            .Where(item => item.AcceptedAtUtc.HasValue)
            .Select(item => new ProcurementEvaluationTimelineEntryDto
            {
                OccurredAtUtc = item.AcceptedAtUtc!.Value,
                Action = "Appointment accepted",
                Outcome = item.Status.ToString(),
                ActorUserId = item.UserId,
                ActorName = item.UserDisplayName,
                Reference = item.AcceptanceEvidenceReference
            }));
        result.AddRange(control.Appointments.SelectMany(item =>
            item.ConflictDeclarations.Select(declaration =>
                new ProcurementEvaluationTimelineEntryDto
                {
                    OccurredAtUtc = declaration.DeclaredAtUtc,
                    Action = "Conflict declaration",
                    Outcome = declaration.Outcome.ToString(),
                    ActorUserId = declaration.DeclaredByUserId,
                    ActorName = item.UserDisplayName,
                    Reference = declaration.EvidenceReference
                })));
        result.AddRange(control.Meetings.Where(item => item.StartedAtUtc.HasValue)
            .Select(item => new ProcurementEvaluationTimelineEntryDto
            {
                OccurredAtUtc = item.StartedAtUtc!.Value,
                Action = "Quorum evaluated",
                Outcome = item.QuorumMet ? "Confirmed" : "Failed",
                Reference = item.EvidenceReference
            }));
        result.AddRange(control.ScoreSheets.Select(item =>
            new ProcurementEvaluationTimelineEntryDto
            {
                OccurredAtUtc = item.SubmittedAtUtc,
                Action = "Score sheet locked",
                Outcome = $"Attempt {item.Attempt}",
                ActorUserId = item.SubmittedByUserId,
                ActorName = item.SubmittedByName,
                Reference = item.EvidenceReference
            }));
        result.AddRange(control.ScoreSheets.SelectMany(item => item.Recalls)
            .Select(item => new ProcurementEvaluationTimelineEntryDto
            {
                OccurredAtUtc = item.DecidedAtUtc ?? item.RequestedAtUtc,
                Action = item.DecidedAtUtc.HasValue
                    ? "Score recall decided"
                    : "Score recall requested",
                Outcome = item.Status.ToString(),
                ActorUserId = item.DecidedByUserId ?? item.RequestedByUserId,
                ActorName = item.DecidedByName ?? item.RequestedByName,
                Reference = item.DecisionReference ?? item.EvidenceReference
            }));
        return result.OrderByDescending(item => item.OccurredAtUtc).ToList();
    }

    private List<string> BuildAllowedActions(
        ProcurementEvaluationCommitteeControl control,
        SourceLineage source,
        DateTime now)
    {
        var actions = new List<string>();
        if (CanAdminister() && control.Status ==
            ProcurementEvaluationCommitteeControlStatus.Draft)
        {
            if (source.CommitteePreparationGate.Allowed)
                actions.Add("activate");
            actions.Add("retireDraft");
        }
        if (source.CommitteePreparationGate.Allowed && CanAdminister() && control.Status ==
            ProcurementEvaluationCommitteeControlStatus.Retired)
            actions.Add("bind");
        if (source.MeetingGate.Allowed && CanAdminister() && control.Status ==
            ProcurementEvaluationCommitteeControlStatus.Active)
            actions.Add("createMeeting");
        if (source.MeetingGate.Allowed && CanAdminister() && control.Meetings.Any(item =>
                item.Status is ProcurementEvaluationMeetingStatus.Draft or
                    ProcurementEvaluationMeetingStatus.QuorumFailed))
            actions.Add("confirmQuorum");
        var acceptsMemberActions = source.CommitteePreparationGate.Allowed && control.Status ==
                                   ProcurementEvaluationCommitteeControlStatus.Active &&
                                   IsEffective(control, now);
        var appointment = control.Appointments.FirstOrDefault(item =>
            item.UserId == _currentUser.UserId);
        if (acceptsMemberActions &&
            appointment?.Status == ProcurementEvaluationAppointmentStatus.Pending)
            actions.Add("respondToAppointment");
        if (acceptsMemberActions &&
            appointment?.Status == ProcurementEvaluationAppointmentStatus.Accepted)
            actions.Add("submitConflictDeclaration");
        if (acceptsMemberActions && appointment is not null &&
            AppointmentEligibilityIssues(appointment, now).Count == 0)
        {
            if (source.MeetingGate.Allowed)
                actions.Add("signAttendance");
            if (control.Meetings.Any(item =>
                    item.Status == ProcurementEvaluationMeetingStatus.QuorumConfirmed &&
                    item.QuorumMet &&
                    item.AttendanceRecords.Any(attendance =>
                        attendance.AppointmentId == appointment.Id &&
                        attendance.IsPresent &&
                        attendance.WasEligibleAtSignature)))
                actions.Add("lockScoreSheet");
        }
        if (control.ScoreSheets.Any(item =>
                item.SubmittedByUserId == _currentUser.UserId &&
                !item.Recalls.Any(recall =>
                    recall.Status is
                        ProcurementEvaluationScoreRecallStatus.PendingApproval or
                        ProcurementEvaluationScoreRecallStatus.Approved)))
            actions.Add("requestRecall");
        if (CanApprove() && control.ScoreSheets.SelectMany(item => item.Recalls)
                .Any(item =>
                    item.Status ==
                    ProcurementEvaluationScoreRecallStatus.PendingApproval))
            actions.Add("decideRecall");
        return actions;
    }

    private static List<string> CompositionIssues(
        ProcurementEvaluationCommitteeControl control)
    {
        var result = CompositionIssues(control.RequiredQuorum,
            control.Appointments.ToList());
        foreach (var requirement in control.RequiredRoles)
        {
            var count = control.Appointments.Count(item =>
                item.MemberKind == requirement.MemberKind &&
                string.Equals(item.RoleName, requirement.RoleName,
                    StringComparison.OrdinalIgnoreCase) &&
                (!requirement.IsVoting || item.IsVoting));
            if (count < requirement.MinimumCount)
                result.Add(
                    $"{requirement.MemberKind}/{requirement.RoleName} requires {requirement.MinimumCount} member(s), but {count} are appointed.");
        }
        return result.Distinct().ToList();
    }

    private static List<string> CompositionIssues<T>(
        int requiredQuorum,
        IReadOnlyCollection<T> members)
    {
        var projected = members.Select(item => item switch
        {
            ProcurementCommitteeMember member =>
                (member.MemberKind, member.IsVoting,
                    member.Assignment.UserId),
            ProcurementEvaluationCommitteeAppointment appointment =>
                (appointment.MemberKind, appointment.IsVoting,
                    appointment.UserId),
            _ => throw new InvalidOperationException("Unsupported committee member type.")
        }).ToList();
        var result = new List<string>();
        if (projected.Select(item => item.UserId).Distinct().Count() != projected.Count)
            result.Add("A user cannot hold duplicate source-specific committee appointments.");
        if (projected.Count(item =>
                item.MemberKind == ProcurementCommitteeMemberKind.Chair) != 1)
            result.Add("Exactly one Chair is required.");
        if (projected.Count(item =>
                item.MemberKind == ProcurementCommitteeMemberKind.Secretary) != 1)
            result.Add("Exactly one Secretary is required.");
        if (projected.Count(item => item.IsVoting) < requiredQuorum)
            result.Add("Active voting membership is below the required quorum.");
        return result;
    }

    private static List<SaveProcurementEvaluationRoleRequirementRequest>
        NormalizeRequirements(
            IReadOnlyCollection<SaveProcurementEvaluationRoleRequirementRequest> requested,
            ProcurementCommittee committee,
            IReadOnlyCollection<ProcurementCommitteeMember> members)
    {
        if (requested.Count != 0)
            return requested.Select(item => new
                SaveProcurementEvaluationRoleRequirementRequest
                {
                    MemberKind = item.MemberKind,
                    RoleName = item.RoleName.Trim(),
                    MinimumCount = item.MinimumCount,
                    IsVoting = item.IsVoting,
                    IsRequiredForQuorum = item.IsRequiredForQuorum
                }).ToList();
        var chair = members.Single(item =>
            item.MemberKind == ProcurementCommitteeMemberKind.Chair);
        var secretary = members.Single(item =>
            item.MemberKind == ProcurementCommitteeMemberKind.Secretary);
        return
        [
            new SaveProcurementEvaluationRoleRequirementRequest
            {
                MemberKind = ProcurementCommitteeMemberKind.Chair,
                RoleName = chair.Assignment.RoleName,
                MinimumCount = 1,
                IsVoting = chair.IsVoting,
                IsRequiredForQuorum = true
            },
            new SaveProcurementEvaluationRoleRequirementRequest
            {
                MemberKind = ProcurementCommitteeMemberKind.Secretary,
                RoleName = secretary.Assignment.RoleName,
                MinimumCount = 1,
                IsVoting = secretary.IsVoting,
                IsRequiredForQuorum = true
            },
            new SaveProcurementEvaluationRoleRequirementRequest
            {
                MemberKind = ProcurementCommitteeMemberKind.VotingMember,
                RoleName = committee.RequiredRoleName,
                MinimumCount = Math.Max(1, committee.RequiredQuorum -
                    (chair.IsVoting ? 1 : 0) - (secretary.IsVoting ? 1 : 0)),
                IsVoting = true,
                IsRequiredForQuorum = true
            }
        ];
    }

    private static void ValidateRequirements(
        IReadOnlyCollection<SaveProcurementEvaluationRoleRequirementRequest> requirements,
        IReadOnlyCollection<ProcurementCommitteeMember> members)
    {
        if (requirements.Count == 0)
            throw Validation("EVALUATION_COMMITTEE_ROLES_REQUIRED",
                "At least one role requirement is required.");
        if (requirements.GroupBy(item =>
                $"{item.MemberKind}:{item.RoleName}",
                StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
            throw Validation("EVALUATION_COMMITTEE_ROLE_DUPLICATE",
                "Committee role requirements cannot be duplicated.");
        if (!requirements.Any(item =>
                item.MemberKind == ProcurementCommitteeMemberKind.Chair &&
                item.MinimumCount == 1 &&
                item.IsRequiredForQuorum))
            throw Validation("EVALUATION_COMMITTEE_CHAIR_REQUIRED",
                "Exactly one quorum-required Chair role must be configured.");
        if (!requirements.Any(item =>
                item.MemberKind == ProcurementCommitteeMemberKind.Secretary &&
                item.MinimumCount == 1 &&
                item.IsRequiredForQuorum))
            throw Validation("EVALUATION_COMMITTEE_SECRETARY_REQUIRED",
                "Exactly one quorum-required Secretary role must be configured.");
        foreach (var requirement in requirements)
        {
            Require(requirement.RoleName, "EVALUATION_COMMITTEE_ROLE_NAME_REQUIRED",
                "Every committee role requires a role name.");
            if (requirement.MinimumCount < 1 || requirement.MinimumCount > 50)
                throw Validation("EVALUATION_COMMITTEE_ROLE_COUNT_INVALID",
                    "Role minimum counts must be between 1 and 50.");
            var count = members.Count(item =>
                item.MemberKind == requirement.MemberKind &&
                string.Equals(item.Assignment.RoleName, requirement.RoleName,
                    StringComparison.OrdinalIgnoreCase) &&
                (!requirement.IsVoting || item.IsVoting));
            if (count < requirement.MinimumCount)
                throw Validation("EVALUATION_COMMITTEE_ROLE_UNMET",
                    $"{requirement.MemberKind}/{requirement.RoleName} requires {requirement.MinimumCount} current member(s), but only {count} match.");
        }
    }

    private static List<string> AppointmentEligibilityIssues(
        ProcurementEvaluationCommitteeAppointment appointment,
        DateTime now)
    {
        var result = new List<string>();
        if (appointment.Status != ProcurementEvaluationAppointmentStatus.Accepted)
            result.Add("The appointment has not been accepted.");
        if (appointment.EffectiveFromUtc > now ||
            appointment.EffectiveToUtc.HasValue &&
            appointment.EffectiveToUtc.Value < now)
            result.Add("The appointment is not currently effective.");
        if (string.IsNullOrWhiteSpace(appointment.AcceptanceSignatureReference) ||
            string.IsNullOrWhiteSpace(appointment.AcceptanceEvidenceReference))
            result.Add("Signed appointment acceptance evidence is missing.");
        var declaration = CurrentDeclaration(appointment, now);
        if (declaration is null)
            result.Add("A current conflict-of-interest declaration is missing.");
        else if (declaration.Outcome != ProcurementEvaluationConflictOutcome.NoConflict)
            result.Add("The current conflict declaration does not permit scoring.");
        else if (string.IsNullOrWhiteSpace(declaration.SignatureReference) ||
                 string.IsNullOrWhiteSpace(declaration.EvidenceReference))
            result.Add("Signed conflict declaration evidence is missing.");
        return result;
    }

    private static ProcurementEvaluationConflictDeclaration? CurrentDeclaration(
        ProcurementEvaluationCommitteeAppointment appointment,
        DateTime now) =>
        appointment.ConflictDeclarations
            .Where(item => item.ValidFromUtc <= now &&
                           (!item.ValidToUtc.HasValue || item.ValidToUtc.Value >= now))
            .OrderByDescending(item => item.Version)
            .ThenByDescending(item => item.DeclaredAtUtc)
            .FirstOrDefault();

    private static void CaptureComposition(
        ProcurementEvaluationCommitteeControl control)
    {
        control.CompositionSnapshotJson = JsonSerializer.Serialize(new
        {
            schemaVersion = "tdc.evaluation-committee.composition.v1",
            control.Id,
            control.SourceType,
            control.SourceId,
            control.Version,
            control.CommitteeTemplateId,
            control.CommitteeCode,
            control.CommitteeName,
            control.RequiredQuorum,
            control.PolicySetId,
            control.PolicyCode,
            control.PolicyVersion,
            control.ConfigurationProfileId,
            control.ConfigurationProfileCode,
            control.ConfigurationProfileVersion,
            control.MethodRuleId,
            control.MethodRuleCode,
            requiredRoles = control.RequiredRoles.OrderBy(item => item.MemberKind)
                .ThenBy(item => item.RoleName)
                .Select(item => new
                {
                    item.MemberKind,
                    item.RoleName,
                    item.MinimumCount,
                    item.IsVoting,
                    item.IsRequiredForQuorum
                }),
            appointments = control.Appointments.OrderBy(item => item.MemberKind)
                .ThenBy(item => item.UserId)
                .Select(item => new
                {
                    item.CommitteeMemberId,
                    item.ResponsibilityAssignmentId,
                    item.UserId,
                    item.UserDisplayName,
                    item.RoleName,
                    item.MemberKind,
                    item.IsVoting,
                    item.EffectiveFromUtc,
                    item.EffectiveToUtc
                })
        }, JsonOptions);
        control.CompositionIntegrityHash =
            ComputeHash(control.CompositionSnapshotJson);
    }

    private static void CaptureDeclaration(
        ProcurementEvaluationConflictDeclaration declaration)
    {
        declaration.SnapshotJson = JsonSerializer.Serialize(new
        {
            schemaVersion = "tdc.evaluation-committee.coi.v1",
            declaration.Id,
            declaration.AppointmentId,
            declaration.Version,
            declaration.Outcome,
            declaration.Declaration,
            declaration.ConflictDetails,
            declaration.SignatureReference,
            declaration.EvidenceReference,
            declaration.WorkflowEvidenceDocumentId,
            declaration.FileUploadRecordId,
            declaration.ValidFromUtc,
            declaration.ValidToUtc,
            declaration.DeclaredAtUtc,
            declaration.DeclaredByUserId
        }, JsonOptions);
        declaration.IntegrityHash = ComputeHash(declaration.SnapshotJson);
    }

    private static void CaptureAttendance(
        ProcurementEvaluationAttendanceRecord attendance,
        ProcurementEvaluationCommitteeAppointment appointment)
    {
        attendance.SnapshotJson = JsonSerializer.Serialize(new
        {
            schemaVersion = "tdc.evaluation-committee.attendance.v1",
            attendance.Id,
            attendance.MeetingId,
            attendance.AppointmentId,
            appointment.UserId,
            appointment.UserDisplayName,
            appointment.RoleName,
            appointment.MemberKind,
            appointment.IsVoting,
            attendance.IsPresent,
            attendance.SignedAtUtc,
            attendance.SignatureReference,
            attendance.EvidenceReference,
            attendance.WasEligibleAtSignature
        }, JsonOptions);
        attendance.IntegrityHash = ComputeHash(attendance.SnapshotJson);
    }

    private static void CaptureQuorum(
        ProcurementEvaluationMeeting meeting,
        IReadOnlyCollection<ProcurementEvaluationCommitteeAppointment> signed)
    {
        meeting.QuorumSnapshotJson = JsonSerializer.Serialize(new
        {
            schemaVersion = "tdc.evaluation-committee.quorum.v1",
            meeting.Id,
            meeting.CommitteeControlId,
            meeting.Sequence,
            meeting.Phase,
            meeting.MeetingMode,
            meeting.MeetingChannel,
            meeting.ScheduledAtUtc,
            meeting.StartedAtUtc,
            meeting.EligibleVotingMemberCount,
            meeting.SignedVotingAttendanceCount,
            meeting.ChairPresent,
            meeting.SecretaryPresent,
            meeting.QuorumMet,
            meeting.EvidenceReference,
            meeting.RemoteMeetingEvidenceReference,
            signed = signed.OrderBy(item => item.MemberKind)
                .ThenBy(item => item.UserId)
                .Select(item => new
                {
                    item.Id,
                    item.UserId,
                    item.RoleName,
                    item.MemberKind,
                    item.IsVoting
                })
        }, JsonOptions);
        meeting.QuorumIntegrityHash = ComputeHash(meeting.QuorumSnapshotJson);
    }

    private static void CaptureScoreSheet(
        ProcurementEvaluationScoreSheet scoreSheet)
    {
        var envelope = JsonSerializer.Serialize(new
        {
            schemaVersion = "tdc.evaluation-committee.score-sheet.v1",
            scoreSheet.Id,
            scoreSheet.CommitteeControlId,
            scoreSheet.MeetingId,
            scoreSheet.AppointmentId,
            scoreSheet.Phase,
            scoreSheet.ScoreSubjectType,
            scoreSheet.ScoreSubjectId,
            scoreSheet.Attempt,
            scoreSheet.SubmittedAtUtc,
            scoreSheet.SubmittedByUserId,
            scoreSheet.SubmittedByName,
            scoreSheet.ScoreSnapshotJson,
            scoreSheet.SignatureReference,
            scoreSheet.EvidenceReference
        }, JsonOptions);
        scoreSheet.IntegrityHash = ComputeHash(envelope);
    }

    private static void CaptureRecall(ProcurementEvaluationScoreRecall recall)
    {
        recall.SnapshotJson = JsonSerializer.Serialize(new
        {
            schemaVersion = "tdc.evaluation-committee.score-recall.v1",
            recall.Id,
            recall.ScoreSheetId,
            recall.Status,
            recall.Reason,
            recall.EvidenceReference,
            recall.WorkflowEvidenceDocumentId,
            recall.FileUploadRecordId,
            recall.WorkflowDefinitionId,
            recall.WorkflowInstanceId,
            recall.RequestedByUserId,
            recall.RequestedByName,
            recall.RequestedAtUtc,
            recall.DecidedByUserId,
            recall.DecidedByName,
            recall.DecidedAtUtc,
            recall.DecisionReference,
            recall.DecisionEvidenceReference,
            recall.AuthorizedNewAttempt
        }, JsonOptions);
        recall.IntegrityHash = ComputeHash(recall.SnapshotJson);
    }

    private async Task RecordAsync(
        ProcurementEvaluationCommitteeControl control,
        string action,
        ProcurementControlEventResult result,
        object input,
        object output,
        string correlationId,
        CancellationToken cancellationToken,
        params ProcurementControlEventEvidenceReference?[] evidence)
    {
        await _controlEvents.RecordAsync(new ProcurementControlEventWriteRequest
        {
            EventKey = ProcurementControlEventKey.Create(
                "evaluation-committee", control.TenantId, control.Id, action,
                correlationId),
            EventType = EventType,
            Action = action,
            Result = result,
            RuleCode = RecallRuleCode,
            RuleId = control.MethodRuleId,
            RuleVersion = control.PolicyVersion.ToString(),
            DecisionKeys = DecisionKeys.ToList(),
            SourceType = control.SourceType.ToString(),
            SourceId = control.SourceId,
            SourceReference = control.SourceReference,
            Reason = action,
            InputValues = input,
            ResultValues = output,
            CorrelationId = correlationId,
            OccurredAtUtc = DateTime.UtcNow,
            Evidence = evidence.Where(item => item is not null)
                .Cast<ProcurementControlEventEvidenceReference>().ToList()
        }, cancellationToken);
    }

    private async Task NotifyAsync(
        string topicKey,
        ProcurementEvaluationCommitteeControl control,
        Guid? targetUserId,
        Guid entityId,
        CancellationToken cancellationToken)
    {
        await _notificationTopics.PublishAsync(new NotificationTopicEvent
        {
            TenantId = control.TenantId,
            TopicKey = topicKey,
            NotificationType = "ProcurementEvaluationCommittee",
            EntityType = EventType,
            EntityId = entityId,
            TriggeredByUserId = _currentUser.UserId,
            Data = new Dictionary<string, object>
            {
                ["sourceType"] = control.SourceType.ToString(),
                ["sourceId"] = control.SourceId,
                ["sourceReference"] = control.SourceReference,
                ["committeeControlId"] = control.Id,
                ["committeeCode"] = control.CommitteeCode,
                ["targetUserId"] = targetUserId ?? Guid.Empty
            }
        }, cancellationToken);
    }

    private async Task NotifyAppointmentAsync(
        ProcurementEvaluationCommitteeControl control,
        ProcurementEvaluationCommitteeAppointment appointment,
        CancellationToken cancellationToken)
    {
        var sourcePath = control.SourceType == ProcurementEvaluationSourceType.Tender
            ? $"/procurement/tenders/{control.SourceId:D}/committee-controls"
            : $"/procurement/rfqs/{control.SourceId:D}/committee-controls";
        var actionUrl = $"{_frontendUrl}{sourcePath}";

        await _notificationTopics.PublishAsync(new NotificationTopicEvent
        {
            TenantId = control.TenantId,
            TopicKey = AppointmentCreatedTopic,
            NotificationType = "ProcurementEvaluationCommittee",
            EntityType = EventType,
            EntityId = appointment.Id,
            TriggeredByUserId = _currentUser.UserId,
            Data = new Dictionary<string, object>
            {
                ["SourceType"] = control.SourceType.ToString(),
                ["SourceId"] = control.SourceId,
                ["SourceReference"] = control.SourceReference,
                ["CommitteeControlId"] = control.Id,
                ["CommitteeCode"] = control.CommitteeCode,
                ["CommitteeName"] = control.CommitteeName,
                ["AppointmentId"] = appointment.Id,
                ["MemberKind"] = appointment.MemberKind.ToString(),
                ["RoleName"] = appointment.RoleName,
                ["TargetUserId"] = appointment.UserId,
                ["ActionUrl"] = actionUrl
            },
            Email = new NotificationTopicEmailOptions
            {
                SubjectTemplateOverride =
                    "Evaluation committee appointment: {{SourceReference}}",
                HtmlBodyTemplateOverride =
                    "<p>You have been appointed as <strong>{{MemberKind}}</strong> to {{CommitteeName}} for {{SourceReference}}.</p>" +
                    "<p><a href=\"{{ActionUrl}}\">Review and respond to the appointment</a></p>",
                TextBodyTemplateOverride =
                    "You have been appointed as {{MemberKind}} to {{CommitteeName}} for {{SourceReference}}. Review and respond: {{ActionUrl}}"
            }
        }, cancellationToken);
    }

    private static ProcurementControlEventEvidenceReference External(
        string reference,
        string label,
        string requirement) => new()
    {
        ReferenceKind = ProcurementControlEvidenceReferenceKind.ExternalReference,
        Reference = reference.Trim(),
        Label = label,
        RequirementKey = requirement
    };

    private static ProcurementControlEventEvidenceReference[] Evidence(
        string reference,
        Guid? workflowEvidenceDocumentId,
        Guid? fileUploadRecordId,
        string label,
        string requirement)
    {
        var result = new List<ProcurementControlEventEvidenceReference>
        {
            External(reference, label, requirement)
        };
        if (workflowEvidenceDocumentId.HasValue)
            result.Add(new ProcurementControlEventEvidenceReference
            {
                ReferenceKind =
                    ProcurementControlEvidenceReferenceKind.WorkflowEvidenceDocument,
                ReferenceId = workflowEvidenceDocumentId,
                Label = label,
                RequirementKey = requirement
            });
        if (fileUploadRecordId.HasValue)
            result.Add(new ProcurementControlEventEvidenceReference
            {
                ReferenceKind =
                    ProcurementControlEvidenceReferenceKind.FileUploadRecord,
                ReferenceId = fileUploadRecordId,
                Label = label,
                RequirementKey = requirement
            });
        return result.ToArray();
    }

    private void EnsureReader()
    {
        EnsureAuthenticatedTenant();
        if (_currentUser.HasRegisteredProcurementPermission("procurement.records.read"))
            return;
        throw new ProcurementEvaluationCommitteeAuthorizationException(
            "The procurement records read permission is required.");
    }

    private void EnsureAuthenticatedTenant()
    {
        if (!_currentUser.IsAuthenticated ||
            _currentUser.UserId == Guid.Empty ||
            _currentUser.TenantId == Guid.Empty ||
            _currentUser.IsExternalUser)
            throw new ProcurementEvaluationCommitteeAuthorizationException(
                "An authenticated internal tenant context is required.");
    }

    private bool CanAdminister() =>
        _currentUser.HasRegisteredProcurementPermission(ManagePermission);

    private bool CanApprove() =>
        _currentUser.HasRegisteredProcurementPermission(ApprovePermission);

    private string ActorName() =>
        string.IsNullOrWhiteSpace(_currentUser.FullName)
            ? _currentUser.Username
            : _currentUser.FullName;

    private static string DisplayName(ApplicationUser user)
    {
        var name = $"{user.FirstName} {user.LastName}".Trim();
        return string.IsNullOrWhiteSpace(name)
            ? user.Email ?? user.UserName ?? user.Id.ToString()
            : name;
    }

    private static bool IsCurrent(ProcurementCommitteeMember member, DateTime now) =>
        member.IsActive &&
        member.EffectiveFrom <= now &&
        (!member.EffectiveTo.HasValue || member.EffectiveTo.Value >= now) &&
        !member.IsDeleted;

    private static bool IsCurrent(
        ProcurementResponsibilityAssignment assignment,
        DateTime now) =>
        assignment.IsActive &&
        assignment.EffectiveFrom <= now &&
        (!assignment.EffectiveTo.HasValue || assignment.EffectiveTo.Value >= now) &&
        !assignment.IsDeleted;

    private static bool IsEffective(
        ProcurementEvaluationCommitteeControl control,
        DateTime now) =>
        control.EffectiveFromUtc <= now &&
        (!control.EffectiveToUtc.HasValue || control.EffectiveToUtc.Value >= now);

    private static void EnsureActive(
        ProcurementEvaluationCommitteeControl control,
        DateTime now)
    {
        if (control.Status != ProcurementEvaluationCommitteeControlStatus.Active)
            throw Conflict("EVALUATION_COMMITTEE_NOT_ACTIVE",
                "The committee control must be active.");
        if (!IsEffective(control, now))
            throw Conflict("EVALUATION_COMMITTEE_NOT_EFFECTIVE",
                "The committee control is not currently effective.");
    }

    private static string NormalizeMeetingMode(string mode)
    {
        if (string.Equals(mode, "InPerson", StringComparison.OrdinalIgnoreCase))
            return "InPerson";
        if (string.Equals(mode, "Remote", StringComparison.OrdinalIgnoreCase))
            return "Remote";
        if (string.Equals(mode, "Hybrid", StringComparison.OrdinalIgnoreCase))
            return "Hybrid";
        throw Validation("EVALUATION_MEETING_MODE_INVALID",
            "MeetingMode must be InPerson, Remote, or Hybrid.");
    }

    private static bool IsRemote(string mode) =>
        mode is "Remote" or "Hybrid";

    private static DateTime MaxUtc(params DateTime[] values) => values.Max();

    private static DateTime? MinUtc(params DateTime?[] values)
    {
        var present = values.Where(item => item.HasValue)
            .Select(item => item!.Value).ToList();
        return present.Count == 0 ? null : present.Min();
    }

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private string ResolveReference(
        string? supplied,
        string action,
        string referenceKind,
        Guid subjectId,
        string idempotencyKey)
    {
        if (!string.IsNullOrWhiteSpace(supplied)) return supplied.Trim();

        var fingerprint = ComputeHash(string.Join('|',
            _currentUser.TenantId.ToString("N"),
            _currentUser.UserId.ToString("N"),
            action,
            referenceKind,
            subjectId.ToString("N"),
            idempotencyKey.Trim()));
        return $"urn:tdc:procurement:evaluation-committee:{action}:{referenceKind}:{fingerprint}";
    }

    private static void Touch(BaseEntity entity, DateTime? now = null)
    {
        entity.UpdatedAt = now ?? DateTime.UtcNow;
        switch (entity)
        {
            case ProcurementEvaluationCommitteeControl control:
                control.RowVersion = Guid.NewGuid().ToByteArray();
                break;
            case ProcurementEvaluationCommitteeAppointment appointment:
                appointment.RowVersion = Guid.NewGuid().ToByteArray();
                break;
            case ProcurementEvaluationMeeting meeting:
                meeting.RowVersion = Guid.NewGuid().ToByteArray();
                break;
            case ProcurementEvaluationScoreRecall recall:
                recall.RowVersion = Guid.NewGuid().ToByteArray();
                break;
        }
    }

    private static void EnsureRowVersion(
        byte[] current,
        string? supplied,
        string prefix)
    {
        if (string.IsNullOrWhiteSpace(supplied))
            throw Validation($"{prefix}_ROW_VERSION_REQUIRED",
                "RowVersion is required.");
        byte[] parsed;
        try
        {
            parsed = Convert.FromBase64String(supplied);
        }
        catch (FormatException)
        {
            throw Validation($"{prefix}_ROW_VERSION_INVALID",
                "RowVersion must be valid Base64.");
        }
        if (!current.SequenceEqual(parsed))
            throw Conflict($"{prefix}_STALE",
                "This record changed after it was loaded. Refresh and retry.");
    }

    private static void RequireIdempotency(string? value) =>
        Require(value, "EVALUATION_IDEMPOTENCY_KEY_REQUIRED",
            "IdempotencyKey is required.");

    private static void Require(string? value, string code, string message)
    {
        if (string.IsNullOrWhiteSpace(value)) throw Validation(code, message);
    }

    private static void ValidateJson(string value, string code)
    {
        try
        {
            using var _ = JsonDocument.Parse(value);
        }
        catch (JsonException)
        {
            throw Validation(code, "The supplied JSON snapshot is invalid.");
        }
    }

    private static string NormalizeJson(string value)
    {
        using var document = JsonDocument.Parse(value);
        return JsonSerializer.Serialize(document.RootElement, JsonOptions);
    }

    private static string ComputeHash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))
            .ToLowerInvariant();

    internal static bool IsCommitteeLineagePersistenceFailure(Exception exception)
    {
        const string guardMessage =
            "Evaluation committee source, template, policy, method, configuration, workflow, or tenant lineage is invalid.";
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current.Message.Contains(guardMessage, StringComparison.OrdinalIgnoreCase) &&
                (current is not SqlException sqlException || sqlException.Number == 51301))
                return true;
        }

        return false;
    }

    private static ProcurementEvaluationCommitteeNotFoundException NotFound(
        string code,
        string message) => new(code, message);

    private static ProcurementEvaluationCommitteeConflictException Conflict(
        string code,
        string message) => new(code, message);

    private static ProcurementEvaluationCommitteeValidationException Validation(
        string code,
        string message) => new(code, message);

    private sealed record SourceLineage(
        string Reference,
        Guid PolicySetId,
        string PolicyCode,
        int PolicyVersion,
        Guid ConfigurationProfileId,
        string ConfigurationProfileCode,
        int ConfigurationProfileVersion,
        Guid MethodRuleId,
        string MethodRuleCode,
        Guid? WorkflowDefinitionId,
        LifecycleGate CommitteePreparationGate,
        LifecycleGate MeetingGate)
    {
        public static SourceLineage From(Tender tender)
        {
            var sourcingCase = tender.SourcingCase ??
                throw Validation("EVALUATION_SOURCE_LINEAGE_MISSING",
                    "The tender has no sourcing-case lineage.");
            return From(sourcingCase, tender.TenderNumber);
        }

        public static SourceLineage From(RequestForQuotation rfq)
        {
            var sourcingCase = rfq.SourcingCase ??
                throw Validation("EVALUATION_SOURCE_LINEAGE_MISSING",
                    "The RFQ has no sourcing-case lineage.");
            return From(sourcingCase, rfq.RfqNumber);
        }

        private static SourceLineage From(
            ProcurementSourcingCase sourcingCase,
            string reference)
        {
            var policy = sourcingCase.PolicySet ??
                throw Validation("EVALUATION_POLICY_LINEAGE_MISSING",
                    "The source policy set is unavailable.");
            var profile = policy.SourceConfigurationProfile ??
                throw Validation("EVALUATION_CONFIGURATION_LINEAGE_MISSING",
                    "The source configuration profile is unavailable.");
            var methodRule = sourcingCase.MethodRule ??
                throw Validation("EVALUATION_METHOD_LINEAGE_MISSING",
                    "The source method rule is unavailable.");
            return new SourceLineage(
                reference,
                sourcingCase.PolicySetId,
                sourcingCase.PolicyCode,
                sourcingCase.PolicyVersion,
                policy.SourceConfigurationProfileId,
                profile.ProfileCode,
                profile.Version,
                sourcingCase.MethodRuleId,
                sourcingCase.MethodRuleCode,
                methodRule.WorkflowDefinitionId,
                LifecycleGate.Ready,
                LifecycleGate.Ready);
        }
    }

    private sealed record LifecycleGate(bool Allowed, string Code, string Message)
    {
        public static LifecycleGate Ready { get; } = new(true, string.Empty, string.Empty);

        public static LifecycleGate Blocked(string code, string message) =>
            new(false, code, message);
    }
}
