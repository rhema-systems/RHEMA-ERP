using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.DTOs.QuantitySurvey;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Entities.QuantitySurvey;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.DocumentManagement;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Interfaces.QuantitySurvey;
using ErpSystem.Core.Services.QuantitySurvey;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Core.Services.Workflow;
using ErpSystem.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.QuantitySurvey;

public sealed class QuantitySurveyJointMeasurementService(
    ApplicationDbContext db,
    ICurrentUserService currentUser,
    IProjectService projectService,
    IWorkflowIntegrationService workflow,
    IWorkflowStatusAdapterRegistry workflowAdapters,
    IControlledFileUploadService controlledFiles,
    ICentralDocumentRepositoryFileService centralDocuments) : IQuantitySurveyJointMeasurementService
{
    private const int MaximumEvidenceFiles = 30;
    private const string EndorsementAttestationPrefix = "I confirm that I attended this joint measurement, reviewed the governed Recorded measurement, and endorse its measured quantity.";
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private Guid TenantId => currentUser.TenantId is { } value && value != Guid.Empty
        ? value : throw new UnauthorizedAccessException("A valid tenant context is required.");
    private Guid UserId => Guid.TryParse(currentUser.UserId, out var value) && value != Guid.Empty
        ? value : throw new UnauthorizedAccessException("An authenticated user is required.");
    private string UserName => string.IsNullOrWhiteSpace(currentUser.UserName) ? UserId.ToString() : currentUser.UserName.Trim();
    private string ActorRoles => string.Join(",", currentUser.Roles.Where(value => !string.IsNullOrWhiteSpace(value))
        .Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value));

    public async Task<QuantitySurveyJointMeasurementLookupsDto> GetLookupsAsync(
        Guid projectId, bool external, CancellationToken token = default)
    {
        if (projectId == Guid.Empty) throw Validation("Select a project.");
        ExternalActor? externalPartner = external
            ? await RequireExternalProjectAsync(projectId, false, null, token)
            : null;
        if (!external) await RequireInternalProjectAsync(projectId);
        var policy = await ResolvePolicyAsync(DateTime.UtcNow, token);

        var lines = await db.ProjectBoqVersionLines.AsNoTracking().Include(value => value.Version)
            .Where(value => value.TenantId == TenantId && value.ProjectId == projectId && !value.IsDeleted &&
                            value.ItemType == ProjectBoqItemTypes.Item && !value.Version.IsDeleted &&
                            value.Version.Status == ProjectBoqVersionStatuses.Approved && value.Version.PublishedAt != null)
            .OrderByDescending(value => value.Version.VersionNumber).ThenBy(value => value.SortOrder)
            .Select(value => new QuantitySurveyJointMeasurementLookupDto
            {
                Id = value.Id,
                Label = (value.LineNumber ?? value.ItemCode ?? "Item") + " · " + value.Description,
                Group = $"Approved BoQ v{value.Version.VersionNumber}",
                Description = $"Current quantity {value.Quantity:N4} · {value.UnitOfMeasure ?? "No unit"}"
            }).ToListAsync(token);

        var accessiblePartnerIds = await AccessibleExternalPartnerIdsAsync(projectId, token);
        if (externalPartner is not null) accessiblePartnerIds = [externalPartner.Value.BusinessPartnerId];
        var partners = await db.BusinessPartners.AsNoTracking()
            .Where(value => value.TenantId == TenantId && accessiblePartnerIds.Contains(value.Id) && !value.IsDeleted &&
                            value.IsActive && !value.IsBlacklisted &&
                            (value.RegistrationStatus == BusinessPartnerLifecyclePolicy.ActiveRegistrationStatus ||
                             value.RegistrationStatus == BusinessPartnerLifecyclePolicy.LegacyApprovedRegistrationStatus))
            .OrderBy(value => value.PartnerName)
            .Select(value => new { value.Id, value.PartnerName, value.PartnerCode, value.PartnerType })
            .ToListAsync(token);
        var portalPartnerIds = await db.BusinessPartnerUsers.IgnoreQueryFilters().AsNoTracking()
            .Where(value => value.TenantId == TenantId && accessiblePartnerIds.Contains(value.BusinessPartnerId) &&
                            value.IsActive && !value.IsDeleted && value.User.TenantId == TenantId && value.User.IsActive)
            .Select(value => value.BusinessPartnerId).Distinct().ToListAsync(token);
        var contractorPartners = partners
            .Where(value => value.PartnerType is "Contractor" or "Both")
            .Where(value => (!policy.External.RequirePortalIdentity && !policy.Measurement.RequireContractorSignature) ||
                            portalPartnerIds.Contains(value.Id))
            .Select(value => new QuantitySurveyJointMeasurementLookupDto
            {
                Id = value.Id, Label = value.PartnerName, Group = value.PartnerType,
                Description = value.PartnerCode
            }).ToList();
        var consultantPartners = partners
            .Where(value => externalPartner is null && !contractorPartners.Any(contractor => contractor.Id == value.Id))
            .Where(value => (!policy.External.RequirePortalIdentity && !policy.Measurement.RequireConsultantSignature) ||
                            portalPartnerIds.Contains(value.Id))
            .Select(value => new QuantitySurveyJointMeasurementLookupDto
            {
                Id = value.Id, Label = value.PartnerName, Group = "Consultant / external reviewer",
                Description = $"{value.PartnerCode} · {value.PartnerType}"
            }).ToList();
        var measurements = await db.QuantitySurveyMeasurementSheets.AsNoTracking()
            .Where(value => value.TenantId == TenantId && value.ProjectId == projectId && !value.IsDeleted && value.Status == "Recorded")
            .OrderByDescending(value => value.RecordedAt)
            .Select(value => new QuantitySurveyJointMeasurementLookupDto
            {
                Id = value.Id,
                Label = value.SheetReference + " · " + value.BoqDescriptionSnapshot,
                Group = value.BoqLineKey.ToString(),
                Description = $"Recorded quantity {value.TotalMeasuredQuantity:N4} · {value.UnitOfMeasureSnapshot ?? "No unit"}"
            }).ToListAsync(token);
        return new()
        {
            ApprovedBoqLines = lines,
            ContractorPartners = contractorPartners,
            ConsultantPartners = consultantPartners,
            RecordedMeasurements = measurements
        };
    }

    public async Task<QuantitySurveyJointMeasurementPageDto> ListAsync(
        QuantitySurveyJointMeasurementListRequest request, bool external, CancellationToken token = default)
    {
        IQueryable<QuantitySurveyJointMeasurementRequest> query = Query();
        if (external)
        {
            var actor = await RequireExternalPartnerAsync(token);
            var projectIds = await AccessibleExternalProjectIdsAsync(actor.BusinessPartnerId, token);
            query = query.Where(value => projectIds.Contains(value.ProjectId) &&
                (value.ContractorBusinessPartnerId == actor.BusinessPartnerId || value.ConsultantBusinessPartnerId == actor.BusinessPartnerId));
        }
        else
        {
            var projectIds = (await projectService.LookupProjectsAsync(take: 5000)).Select(value => value.Id).ToList();
            query = query.Where(value => projectIds.Contains(value.ProjectId));
        }
        if (request.ProjectId.HasValue) query = query.Where(value => value.ProjectId == request.ProjectId.Value);
        if (!string.IsNullOrWhiteSpace(request.Status)) query = query.Where(value => value.Status == request.Status.Trim());
        var total = await query.CountAsync(token);
        var values = await query.OrderByDescending(value => value.RequestedAt).ThenByDescending(value => value.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToListAsync(token);
        return new() { Items = values.Select(Map).ToList(), Page = request.Page, PageSize = request.PageSize, TotalCount = total };
    }

    public async Task<QuantitySurveyJointMeasurementDto> GetAsync(Guid id, bool external, CancellationToken token = default)
    {
        var entity = await RequiredAsync(id, false, token);
        if (external) await RequireExternalRequestAsync(entity, false, false, token);
        else await RequireInternalProjectAsync(entity.ProjectId);
        return Map(entity);
    }

    public async Task<QuantitySurveyJointMeasurementDto> CreateAsync(
        CreateQuantitySurveyJointMeasurementRequest request, bool external, string correlationId,
        CancellationToken token = default)
    {
        if (request.ClientRequestId == Guid.Empty || request.ProjectId == Guid.Empty || request.ProjectBoqVersionLineId == Guid.Empty)
            throw Validation("Select a project and approved BoQ item, and provide a client request identifier.");
        var title = RequiredText(request.Title, 3, 200, "Request title");
        var reason = RequiredText(request.Reason, 10, 2000, "Remeasurement reason");
        var location = Clean(request.RequestedSiteLocation, 300);
        ValidateDateWindow(request.PreferredStartAt, request.PreferredEndAt, "preferred measurement window");
        ExternalActor? actorPartner = external
            ? await RequireExternalProjectAsync(request.ProjectId, true, null, token)
            : null;
        if (!external) await RequireInternalProjectAsync(request.ProjectId);
        var contractorId = external
            ? actorPartner!.Value.BusinessPartnerId
            : request.ContractorBusinessPartnerId is { } selected && selected != Guid.Empty
                ? selected : throw Validation("Select the contractor business partner.");
        var line = await RequireApprovedLineAsync(request.ProjectId, request.ProjectBoqVersionLineId, token);
        var policy = await ResolvePolicyAsync(DateTime.UtcNow, token);
        if (external && (!policy.External.Channels.Contains(QuantitySurveyExternalSubmissionChannel.ExternalPortal) ||
                         (policy.External.RequirePortalIdentity && actorPartner is null)))
            throw new UnauthorizedAccessException("The effective QS external-submission policy does not permit portal requests.");
        await RequireProjectPartnerAsync(request.ProjectId, contractorId, token, requireContractor: true);
        var requestHash = Hash(new
        {
            request.ProjectId, request.ProjectBoqVersionLineId, ContractorBusinessPartnerId = contractorId,
            Title = title, Reason = reason, request.ContractorProposedQuantity, RequestedSiteLocation = location,
            PreferredStartAt = Utc(request.PreferredStartAt), PreferredEndAt = Utc(request.PreferredEndAt),
            Policy = policy.PolicyHash
        });
        var retry = await db.QuantitySurveyJointMeasurementRequests.AsNoTracking()
            .FirstOrDefaultAsync(value => value.TenantId == TenantId && value.ClientRequestId == request.ClientRequestId && !value.IsDeleted, token);
        if (retry is not null)
        {
            if (!FixedEquals(retry.RequestHash, requestHash)) throw RetryConflict();
            return await GetAsync(retry.Id, external, token);
        }

        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var entity = new QuantitySurveyJointMeasurementRequest
        {
            Id = id, TenantId = TenantId, ProjectId = request.ProjectId,
            ProjectBoqVersionId = line.ProjectBoqVersionId, ProjectBoqVersionLineId = line.Id,
            BoqLineKey = line.LineKey, ContractorBusinessPartnerId = contractorId,
            ClientRequestId = request.ClientRequestId, RequestHash = requestHash,
            RequestNumber = $"JRM-{now:yyyyMMdd}-{id.ToString("N")[..8].ToUpperInvariant()}",
            Title = title, Reason = reason, PreviousQuantity = line.Quantity,
            ContractorProposedQuantity = request.ContractorProposedQuantity,
            RequestedSiteLocation = location, PreferredStartAt = Utc(request.PreferredStartAt),
            PreferredEndAt = Utc(request.PreferredEndAt),
            ConfigurationProfileId = policy.Profile.Id, MeasurementDecisionId = policy.MeasurementDecision.Id,
            ExternalSubmissionDecisionId = policy.ExternalDecision.Id,
            ApprovalWorkflowDefinitionId = policy.Measurement.WorkflowDefinitionId,
            EvidenceMetadataTemplateId = policy.Template.Id,
            EvidenceMetadataTemplateCodeSnapshot = policy.Template.TemplateCode,
            PolicyHash = policy.PolicyHash,
            JointAttendanceRoleIdsJson = JsonSerializer.Serialize(policy.Measurement.JointAttendanceRoleIds),
            ConsultantRoleIdsJson = JsonSerializer.Serialize(policy.Measurement.ConsultantRoleIds),
            ContractorSignatureRequired = policy.Measurement.RequireContractorSignature,
            ConsultantSignatureRequired = policy.Measurement.RequireConsultantSignature,
            PortalIdentityRequired = policy.External.RequirePortalIdentity,
            EvidenceRequired = policy.External.RequireEvidence,
            ExternalSignatureRequired = policy.External.RequireSignature,
            RequestedByUserId = UserId, RequestedByName = UserName, RequestedAt = now,
            RemeasurementClientRequestId = id,
            AuditAction = QuantitySurveyAuditEventMap.CreateJointMeasurementRequest,
            CorrelationId = NormalizeCorrelation(correlationId),
            CreatedAt = now, CreatedBy = UserName, CreatedById = UserId
        };
        entity.Participants.Add(new QuantitySurveyJointMeasurementParticipant
        {
            Id = Guid.NewGuid(), TenantId = TenantId, RequestId = entity.Id,
            ParticipantType = QuantitySurveyJointMeasurementParticipantTypes.Contractor,
            BusinessPartnerId = contractorId, IsRequired = true,
            CreatedAt = now, CreatedBy = UserName, CreatedById = UserId
        });
        db.QuantitySurveyJointMeasurementRequests.Add(entity);
        AddRevision(entity, QuantitySurveyAuditEventMap.CreateJointMeasurementRequest, reason, null, Snapshot(entity), correlationId, actorPartner?.BusinessPartnerId);
        AddAudit(entity, QuantitySurveyAuditEventMap.CreateJointMeasurementRequest, null, Snapshot(entity), correlationId);
        try { await SaveAsync(token); }
        catch (DbUpdateException exception) when (IsUnique(exception))
        {
            db.ChangeTracker.Clear();
            retry = await db.QuantitySurveyJointMeasurementRequests.AsNoTracking()
                .FirstOrDefaultAsync(value => value.TenantId == TenantId && value.ClientRequestId == request.ClientRequestId && !value.IsDeleted, token);
            if (retry is null || !FixedEquals(retry.RequestHash, requestHash)) throw RetryConflict();
            return await GetAsync(retry.Id, external, token);
        }
        return await GetAsync(entity.Id, external, token);
    }

    public async Task<QuantitySurveyJointMeasurementDto> SubmitAsync(
        Guid id, QuantitySurveyJointMeasurementLifecycleRequest request, bool external,
        string correlationId, CancellationToken token = default)
    {
        var entity = await RequiredAsync(id, true, token);
        Guid? actorPartnerId = null;
        if (external) actorPartnerId = (await RequireExternalRequestAsync(entity, false, false, token)).BusinessPartnerId;
        else await RequireInternalProjectAsync(entity.ProjectId);
        var reason = RequiredText(request.Reason, 5, 2000, "Submission reason");
        var mutationHash = Hash(new { Action = "Submit", Reason = reason });
        if (IsMutationRetry(entity, request.ClientRequestId, mutationHash)) return Map(entity);
        CheckVersion(entity.RowVersion, request.RowVersion);
        if (entity.Status != QuantitySurveyJointMeasurementStatuses.Draft)
            throw Conflict("Only a Draft joint remeasurement request can be submitted.");
        await ValidateFrozenContextAsync(entity, token);
        if (entity.EvidenceRequired && entity.Evidence.Count == 0)
            throw Conflict("Add the evidence required by the effective external-submission policy before submitting.");
        var before = Snapshot(entity);
        entity.Status = QuantitySurveyJointMeasurementStatuses.Submitted;
        entity.SubmittedByUserId = UserId;
        entity.SubmittedAt = DateTime.UtcNow;
        Touch(entity, QuantitySurveyAuditEventMap.SubmitJointMeasurementRequest, correlationId, request.ClientRequestId, mutationHash);
        AddRevision(entity, QuantitySurveyAuditEventMap.SubmitJointMeasurementRequest, reason, before, Snapshot(entity), correlationId, actorPartnerId);
        AddAudit(entity, QuantitySurveyAuditEventMap.SubmitJointMeasurementRequest, before, Snapshot(entity), correlationId);
        await SaveAsync(token);
        return await GetAsync(id, external, token);
    }

    public async Task<QuantitySurveyJointMeasurementDto> ScheduleAsync(
        Guid id, ScheduleQuantitySurveyJointMeasurementRequest request, string correlationId,
        CancellationToken token = default)
    {
        var existing = await RequiredAsync(id, false, token);
        await RequireInternalProjectAsync(existing.ProjectId);
        if (request.ClientRequestId == Guid.Empty || request.ConsultantBusinessPartnerId == Guid.Empty)
            throw Validation("Select a consultant and provide a client request identifier.");
        var location = RequiredText(request.SiteLocation, 3, 300, "Joint measurement site location");
        var start = Utc(request.ScheduledStartAt) ?? throw Validation("Select the scheduled start time.");
        var end = Utc(request.ScheduledEndAt) ?? throw Validation("Select the scheduled end time.");
        if (end <= start) throw Validation("The scheduled end time must be after the start time.");
        await RequireProjectPartnerAsync(existing.ProjectId, request.ConsultantBusinessPartnerId, token);
        if (request.ConsultantBusinessPartnerId == existing.ContractorBusinessPartnerId)
            throw Validation("Contractor and consultant attendance must be assigned to different business partners.");
        if (existing.PortalIdentityRequired && !await db.BusinessPartnerUsers.IgnoreQueryFilters().AsNoTracking()
                .AnyAsync(value => value.TenantId == TenantId && value.BusinessPartnerId == request.ConsultantBusinessPartnerId &&
                    value.IsActive && !value.IsDeleted && value.User.TenantId == TenantId && value.User.IsActive, token))
            throw Validation("Select a consultant business partner with an active portal user for governed attendance and endorsement.");
        var mutationHash = Hash(new { Action = "Schedule", request.ConsultantBusinessPartnerId, start, end, location });
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
            var entity = await RequiredAsync(id, true, token);
            if (IsMutationRetry(entity, request.ClientRequestId, mutationHash))
            {
                await transaction.CommitAsync(token);
                return;
            }
            CheckVersion(entity.RowVersion, request.RowVersion);
            if (entity.Status != QuantitySurveyJointMeasurementStatuses.Submitted)
                throw Conflict("Only a Submitted request can be scheduled.");
            await ValidateFrozenContextAsync(entity, token);
            var roleIds = JsonSerializer.Deserialize<List<Guid>>(entity.JointAttendanceRoleIdsJson, JsonOptions) ?? [];
            var roles = await db.Roles.AsNoTracking().Where(value => roleIds.Contains(value.Id)).ToListAsync(token);
            if (roles.Count != roleIds.Distinct().Count())
                throw Conflict("One or more configured joint-attendance roles are no longer available.");
            var before = Snapshot(entity);
            entity.ConsultantBusinessPartnerId = request.ConsultantBusinessPartnerId;
            entity.ScheduledStartAt = start; entity.ScheduledEndAt = end;
            entity.ScheduledSiteLocation = location; entity.ScheduledByUserId = UserId;
            entity.ScheduledAt = DateTime.UtcNow; entity.Status = QuantitySurveyJointMeasurementStatuses.Scheduled;
            entity.Participants.Add(new QuantitySurveyJointMeasurementParticipant
            {
                Id = Guid.NewGuid(), TenantId = TenantId, RequestId = entity.Id,
                ParticipantType = QuantitySurveyJointMeasurementParticipantTypes.Consultant,
                BusinessPartnerId = request.ConsultantBusinessPartnerId, IsRequired = true,
                CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId
            });
            foreach (var role in roles.OrderBy(value => value.Name))
                entity.Participants.Add(new QuantitySurveyJointMeasurementParticipant
                {
                    Id = Guid.NewGuid(), TenantId = TenantId, RequestId = entity.Id,
                    ParticipantType = QuantitySurveyJointMeasurementParticipantTypes.InternalRole,
                    RequiredRoleId = role.Id, RequiredRoleNameSnapshot = role.Name,
                    IsRequired = true, CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId
                });
            Touch(entity, QuantitySurveyAuditEventMap.ScheduleJointMeasurement, correlationId, request.ClientRequestId, mutationHash);
            AddRevision(entity, QuantitySurveyAuditEventMap.ScheduleJointMeasurement, "Joint measurement scheduled.", before, Snapshot(entity), correlationId);
            AddAudit(entity, QuantitySurveyAuditEventMap.ScheduleJointMeasurement, before, Snapshot(entity), correlationId);
            await SaveAsync(token);
            await projectService.UpsertExternalAccessPolicyAsync(entity.ProjectId, new CreateProjectExternalAccessPolicyDto
            {
                BusinessPartnerId = request.ConsultantBusinessPartnerId,
                ArtifactType = "JointMeasurement", ArtifactId = entity.Id,
                AccessLevel = "Full", CanComment = true, CanUpload = true, CanApprove = true,
                Notes = "Controlled consultant access to the scheduled joint measurement."
            });
            await transaction.CommitAsync(token);
        });
        return await GetAsync(id, false, token);
    }

    public async Task<QuantitySurveyJointMeasurementDto> LinkMeasurementAsync(
        Guid id, LinkQuantitySurveyJointMeasurementSheetRequest request, string correlationId,
        CancellationToken token = default)
    {
        var entity = await RequiredAsync(id, true, token);
        await RequireInternalProjectAsync(entity.ProjectId);
        if (request.ClientRequestId == Guid.Empty || request.MeasurementSheetId == Guid.Empty)
            throw Validation("Select a Recorded measurement and provide a client request identifier.");
        var mutationHash = Hash(new { Action = "LinkMeasurement", request.MeasurementSheetId });
        if (IsMutationRetry(entity, request.ClientRequestId, mutationHash)) return Map(entity);
        CheckVersion(entity.RowVersion, request.RowVersion);
        if (entity.Status is not (QuantitySurveyJointMeasurementStatuses.Scheduled or QuantitySurveyJointMeasurementStatuses.AwaitingAttendance))
            throw Conflict("A Recorded measurement can be linked only after scheduling and before endorsements.");
        var measurement = await db.QuantitySurveyMeasurementSheets.AsNoTracking()
            .Include(value => value.Attachments).ThenInclude(value => value.FileUploadRecord)
            .Include(value => value.Attachments).ThenInclude(value => value.CentralDocumentRecord)
            .Include(value => value.Attachments).ThenInclude(value => value.CentralDocumentVersion)
            .FirstOrDefaultAsync(value => value.TenantId == TenantId && value.Id == request.MeasurementSheetId && !value.IsDeleted, token)
            ?? throw Validation("Select a Recorded measurement in this project.");
        if (measurement.ProjectId != entity.ProjectId || measurement.ProjectBoqVersionId != entity.ProjectBoqVersionId ||
            measurement.ProjectBoqVersionLineId != entity.ProjectBoqVersionLineId || measurement.BoqLineKey != entity.BoqLineKey ||
            measurement.Status != "Recorded" || measurement.RecordedAt is null)
            throw Conflict("The measurement must be Recorded against the request's approved BoQ line and version.");
        if (entity.ScheduledStartAt.HasValue && entity.ScheduledEndAt.HasValue &&
            (measurement.MeasurementDate < entity.ScheduledStartAt.Value.AddDays(-1) ||
             measurement.MeasurementDate > entity.ScheduledEndAt.Value.AddDays(1)))
            throw Conflict("The Recorded measurement date is outside the scheduled joint-measurement window.");
        ValidateMeasurementEvidence(measurement);
        var alreadyLinked = await db.QuantitySurveyJointMeasurementRequests.AsNoTracking().AnyAsync(value =>
            value.TenantId == TenantId && value.Id != entity.Id && value.MeasurementSheetId == measurement.Id && !value.IsDeleted, token);
        if (alreadyLinked) throw Conflict("This Recorded measurement is already linked to another joint request.");
        var before = Snapshot(entity);
        entity.MeasurementSheetId = measurement.Id; entity.MeasurementLinkedAt = DateTime.UtcNow;
        AdvanceReadiness(entity);
        Touch(entity, QuantitySurveyAuditEventMap.LinkJointMeasurementSheet, correlationId, request.ClientRequestId, mutationHash);
        AddRevision(entity, QuantitySurveyAuditEventMap.LinkJointMeasurementSheet, "Recorded measurement linked.", before, Snapshot(entity), correlationId);
        AddAudit(entity, QuantitySurveyAuditEventMap.LinkJointMeasurementSheet, before, Snapshot(entity), correlationId);
        await SaveAsync(token); return await GetAsync(id, false, token);
    }

    public async Task<QuantitySurveyJointMeasurementDto> AttendAsync(
        Guid id, Guid participantId, AttendQuantitySurveyJointMeasurementRequest request, bool external,
        string correlationId, CancellationToken token = default)
    {
        var entity = await RequiredAsync(id, true, token);
        ExternalActor? actor = null;
        if (external) actor = await RequireExternalRequestAsync(entity, false, false, token);
        else await RequireInternalProjectAsync(entity.ProjectId);
        var participant = entity.Participants.SingleOrDefault(value => value.Id == participantId && !value.IsDeleted)
            ?? throw NotFound("The joint-measurement participant was not found.");
        ValidateParticipantActor(participant, actor);
        var notes = Clean(request.Notes, 1000);
        var mutationHash = Hash(new { Action = "Attend", participantId, Notes = notes, Actor = UserId });
        if (IsMutationRetry(entity, request.ClientRequestId, mutationHash)) return Map(entity);
        CheckVersion(entity.RowVersion, request.RowVersion);
        if (entity.Status is not (QuantitySurveyJointMeasurementStatuses.Scheduled or QuantitySurveyJointMeasurementStatuses.AwaitingAttendance))
            throw Conflict("Attendance can be recorded only for a scheduled joint measurement.");
        if (participant.AttendanceStatus == "Attended")
            throw Conflict("Attendance has already been recorded for this participant.");
        var now = DateTime.UtcNow;
        if (!entity.ScheduledStartAt.HasValue || !entity.ScheduledEndAt.HasValue ||
            now < entity.ScheduledStartAt.Value.AddHours(-24) || now > entity.ScheduledEndAt.Value.AddHours(24))
            throw Conflict("Attendance can be recorded only within the scheduled joint-measurement window.");
        var before = Snapshot(entity);
        participant.AttendanceStatus = "Attended"; participant.AttendedByUserId = UserId;
        participant.AttendedByName = UserName; participant.AttendedAt = now;
        participant.AttendanceNotes = notes;
        participant.AttendanceHash = Hash(new { RequestId = entity.Id, ParticipantId = participant.Id, participant.ParticipantType,
            participant.BusinessPartnerId, participant.RequiredRoleId, UserId, UserName, now, notes });
        AdvanceReadiness(entity);
        Touch(entity, QuantitySurveyAuditEventMap.RecordJointMeasurementAttendance, correlationId, request.ClientRequestId, mutationHash);
        AddRevision(entity, QuantitySurveyAuditEventMap.RecordJointMeasurementAttendance, notes ?? "Attendance recorded.", before, Snapshot(entity), correlationId, actor?.BusinessPartnerId);
        AddAudit(entity, QuantitySurveyAuditEventMap.RecordJointMeasurementAttendance, before, Snapshot(entity), correlationId);
        await SaveAsync(token); return await GetAsync(id, external, token);
    }

    public async Task<QuantitySurveyJointMeasurementDto> EndorseAsync(
        Guid id, Guid participantId, EndorseQuantitySurveyJointMeasurementRequest request, bool external,
        string correlationId, CancellationToken token = default)
    {
        var entity = await RequiredAsync(id, true, token);
        ExternalActor? actor = null;
        if (external) actor = await RequireExternalRequestAsync(entity, true, false, token);
        else await RequireInternalProjectAsync(entity.ProjectId);
        var participant = entity.Participants.SingleOrDefault(value => value.Id == participantId && !value.IsDeleted)
            ?? throw NotFound("The joint-measurement participant was not found.");
        if (participant.ParticipantType is not (QuantitySurveyJointMeasurementParticipantTypes.Contractor or QuantitySurveyJointMeasurementParticipantTypes.Consultant))
            throw Validation("Only the assigned contractor or consultant can endorse the joint measurement.");
        ValidateParticipantActor(participant, actor);
        var notes = Clean(request.Notes, 2000);
        var mutationHash = Hash(new { Action = "Endorse", participantId, request.Signature.Method,
            request.Signature.Attestation, request.Signature.ExternalReference, request.Signature.SignedAt, Notes = notes, Actor = UserId });
        if (IsMutationRetry(entity, request.ClientRequestId, mutationHash)) return Map(entity);
        CheckVersion(entity.RowVersion, request.RowVersion);
        if (entity.Status != QuantitySurveyJointMeasurementStatuses.AwaitingEndorsements)
            throw Conflict("All required attendance and the Recorded measurement must be present before endorsement.");
        if (participant.AttendanceStatus != "Attended" || participant.AttendedByUserId != UserId)
            throw Conflict("The same assigned portal user must record attendance before endorsing.");
        if (entity.Endorsements.Any(value => value.ParticipantId == participant.Id && !value.IsDeleted))
            throw Conflict("This participant has already endorsed the joint measurement.");
        var attestation = BuildAttestation(entity);
        var signaturePolicy = new WorkflowSignaturePolicyDto
        {
            IsRequired = true, Method = request.Signature.Method,
            RequireValidCertificateChain = request.Signature.Method == WorkflowSignatureMethod.DigitalCertificate,
            AttestationText = attestation
        };
        var errors = WorkflowSignatureValidator.Validate(signaturePolicy, currentUser.Roles,
            new { signature = request.Signature }, DateTime.UtcNow);
        if (errors.Count > 0) throw Validation(string.Join(" ", errors));
        var certificate = request.Signature.Method == WorkflowSignatureMethod.DigitalCertificate
            ? WorkflowSignatureValidator.InspectCertificate(request.Signature.CertificateBase64, DateTime.UtcNow) : null;
        var before = Snapshot(entity);
        entity.Endorsements.Add(new QuantitySurveyJointMeasurementEndorsement
        {
            Id = Guid.NewGuid(), TenantId = TenantId, RequestId = entity.Id, ParticipantId = participant.Id,
            SignerType = participant.ParticipantType, BusinessPartnerId = participant.BusinessPartnerId,
            SignedByUserId = UserId, SignedByName = UserName,
            SignatureMethod = request.Signature.Method.ToString(), AttestationSnapshot = attestation,
            CertificateThumbprint = certificate?.Thumbprint,
            ExternalSignatureReference = Clean(request.Signature.ExternalReference, 300),
            SignatureHash = Hash(new { RequestId = entity.Id, ParticipantId = participant.Id, entity.MeasurementSheetId,
                participant.BusinessPartnerId, UserId, request.Signature.Method, attestation,
                certificate?.Thumbprint, request.Signature.ExternalReference, request.Signature.SignedAt }),
            Notes = notes, SignedAt = Utc(request.Signature.SignedAt) ?? DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId
        });
        AdvanceReadiness(entity);
        Touch(entity, QuantitySurveyAuditEventMap.EndorseJointMeasurement, correlationId, request.ClientRequestId, mutationHash);
        AddRevision(entity, QuantitySurveyAuditEventMap.EndorseJointMeasurement, notes ?? "Joint measurement endorsed.", before, Snapshot(entity), correlationId, actor?.BusinessPartnerId);
        AddAudit(entity, QuantitySurveyAuditEventMap.EndorseJointMeasurement, before, Snapshot(entity), correlationId);
        await SaveAsync(token); return await GetAsync(id, external, token);
    }

    public async Task<QuantitySurveyJointMeasurementDto> SubmitForApprovalAsync(
        Guid id, QuantitySurveyJointMeasurementLifecycleRequest request, string correlationId,
        CancellationToken token = default)
    {
        var entity = await RequiredAsync(id, true, token);
        await RequireInternalProjectAsync(entity.ProjectId);
        var reason = RequiredText(request.Reason, 5, 2000, "Review submission reason");
        var mutationHash = Hash(new { Action = "SubmitForApproval", Reason = reason });
        if (IsMutationRetry(entity, request.ClientRequestId, mutationHash)) return Map(entity);
        CheckVersion(entity.RowVersion, request.RowVersion);
        if (entity.Status != QuantitySurveyJointMeasurementStatuses.ReadyForReview)
            throw Conflict("The joint measurement is not ready for QS review and approval.");
        await ValidateReviewReadinessAsync(entity, token);
        var before = Snapshot(entity);
        var result = await workflow.SubmitAsync(QuantitySurveyWorkflowBindingRegistry.Measurement, entity.Id,
            entity.ApprovalWorkflowDefinitionId);
        if (!result.ExecutionResult.Success)
            throw Conflict(result.ExecutionResult.Message ?? "The configured QS measurement workflow could not be started.");
        workflowAdapters.GetAdapter(QuantitySurveyWorkflowBindingRegistry.Measurement).ApplySubmitOutcome(entity, result.Outcome, UserId);
        if (result.Outcome == WorkflowOutcome.Approved)
        {
            entity.Status = QuantitySurveyJointMeasurementStatuses.PendingApproval;
            entity.ApprovalStatus = "Pending"; entity.ApprovedById = null; entity.ApprovedAt = null;
        }
        entity.WorkflowInstanceId = result.ExecutionResult.WorkflowInstanceId;
        entity.ReviewedById = UserId; entity.ReviewedAt = DateTime.UtcNow;
        Touch(entity, QuantitySurveyAuditEventMap.SubmitJointMeasurementApproval, correlationId, request.ClientRequestId, mutationHash);
        AddRevision(entity, QuantitySurveyAuditEventMap.SubmitJointMeasurementApproval, reason, before, Snapshot(entity), correlationId);
        AddAudit(entity, QuantitySurveyAuditEventMap.SubmitJointMeasurementApproval, before, Snapshot(entity), correlationId);
        await SaveAsync(token); return await GetAsync(id, false, token);
    }

    public async Task<QuantitySurveyJointMeasurementDto> ApproveAsync(
        Guid id, QuantitySurveyJointMeasurementLifecycleRequest request, string correlationId,
        CancellationToken token = default)
    {
        var entity = await RequiredAsync(id, true, token);
        await RequireInternalProjectAsync(entity.ProjectId);
        var reason = RequiredText(request.Reason, 5, 2000, "Approval reason");
        if (entity.Status is QuantitySurveyJointMeasurementStatuses.ApprovedPendingBoqRevision or
            QuantitySurveyJointMeasurementStatuses.BoqWorkflowPending or QuantitySurveyJointMeasurementStatuses.Applied)
        {
            await EnsureBoqRevisionAsync(entity.Id, reason, correlationId, token);
            return await GetAsync(id, false, token);
        }
        var mutationHash = Hash(new { Action = "Approve", Reason = reason });
        if (IsMutationRetry(entity, request.ClientRequestId, mutationHash)) return Map(entity);
        CheckVersion(entity.RowVersion, request.RowVersion);
        if (entity.Status != QuantitySurveyJointMeasurementStatuses.PendingApproval)
            throw Conflict("The joint measurement must be PendingApproval before approval.");
        if (entity.ReviewedById == UserId || entity.RequestedByUserId == UserId)
            throw Conflict("Maker-checker control prevents the requester or QS submitter from approving this record.");
        await ValidateReviewReadinessAsync(entity, token);
        var workflowStatus = await WorkflowStatusAsync(entity, token);
        WorkflowOutcome outcome;
        if (workflowStatus == WorkflowInstanceStatus.Completed) outcome = WorkflowOutcome.Approved;
        else
        {
            if (workflowStatus is WorkflowInstanceStatus.Cancelled or WorkflowInstanceStatus.Failed)
                throw Conflict("The QS measurement workflow ended without approval.");
            if (!await workflow.CanUserApproveAsync(QuantitySurveyWorkflowBindingRegistry.Measurement, entity.Id, UserId))
                throw new UnauthorizedAccessException("You are not assigned to the current QS measurement approval step.");
            var result = await workflow.ProcessApprovalAsync(QuantitySurveyWorkflowBindingRegistry.Measurement,
                entity.Id, UserId, "Approve", reason);
            if (!result.ExecutionResult.Success)
                throw Conflict(result.ExecutionResult.Message ?? "The joint-measurement approval could not be processed.");
            outcome = result.Outcome;
        }
        if (outcome != WorkflowOutcome.Approved) return await GetAsync(id, false, token);
        var before = Snapshot(entity);
        entity.Status = QuantitySurveyJointMeasurementStatuses.ApprovedPendingBoqRevision;
        entity.ApprovalStatus = "Approved"; entity.ApprovedById = UserId; entity.ApprovedAt = DateTime.UtcNow;
        entity.RejectionReason = null;
        Touch(entity, QuantitySurveyAuditEventMap.ApproveJointMeasurement, correlationId, request.ClientRequestId, mutationHash);
        AddRevision(entity, QuantitySurveyAuditEventMap.ApproveJointMeasurement, reason, before, Snapshot(entity), correlationId);
        AddAudit(entity, QuantitySurveyAuditEventMap.ApproveJointMeasurement, before, Snapshot(entity), correlationId);
        await SaveAsync(token);
        await EnsureBoqRevisionAsync(entity.Id, reason, correlationId, token);
        return await GetAsync(id, false, token);
    }

    public async Task<QuantitySurveyJointMeasurementDto> RejectAsync(
        Guid id, QuantitySurveyJointMeasurementLifecycleRequest request, string correlationId,
        CancellationToken token = default)
    {
        var entity = await RequiredAsync(id, true, token);
        await RequireInternalProjectAsync(entity.ProjectId);
        var reason = RequiredText(request.Reason, 5, 2000, "Rejection reason");
        var mutationHash = Hash(new { Action = "Reject", Reason = reason });
        if (IsMutationRetry(entity, request.ClientRequestId, mutationHash)) return Map(entity);
        CheckVersion(entity.RowVersion, request.RowVersion);
        if (entity.Status != QuantitySurveyJointMeasurementStatuses.PendingApproval)
            throw Conflict("The joint measurement must be PendingApproval before rejection.");
        if (entity.ReviewedById == UserId || entity.RequestedByUserId == UserId)
            throw Conflict("Maker-checker control prevents the requester or QS submitter from rejecting this record.");
        var status = await WorkflowStatusAsync(entity, token);
        if (status == WorkflowInstanceStatus.Completed)
            throw Conflict("A completed QS measurement workflow is approved and cannot be rejected.");
        WorkflowOutcome outcome;
        if (status is WorkflowInstanceStatus.Cancelled or WorkflowInstanceStatus.Failed) outcome = WorkflowOutcome.Rejected;
        else
        {
            if (!await workflow.CanUserApproveAsync(QuantitySurveyWorkflowBindingRegistry.Measurement, entity.Id, UserId))
                throw new UnauthorizedAccessException("You are not assigned to the current QS measurement approval step.");
            var result = await workflow.ProcessApprovalAsync(QuantitySurveyWorkflowBindingRegistry.Measurement,
                entity.Id, UserId, "Reject", reason);
            if (!result.ExecutionResult.Success)
                throw Conflict(result.ExecutionResult.Message ?? "The joint-measurement rejection could not be processed.");
            outcome = result.Outcome;
        }
        if (outcome != WorkflowOutcome.Rejected)
            throw Conflict("The shared workflow did not return a rejected outcome.");
        var before = Snapshot(entity);
        entity.Status = QuantitySurveyJointMeasurementStatuses.Rejected;
        entity.ApprovalStatus = "Rejected"; entity.RejectionReason = reason;
        Touch(entity, QuantitySurveyAuditEventMap.RejectJointMeasurement, correlationId, request.ClientRequestId, mutationHash);
        AddRevision(entity, QuantitySurveyAuditEventMap.RejectJointMeasurement, reason, before, Snapshot(entity), correlationId);
        AddAudit(entity, QuantitySurveyAuditEventMap.RejectJointMeasurement, before, Snapshot(entity), correlationId);
        await SaveAsync(token); return await GetAsync(id, false, token);
    }

    public async Task<QuantitySurveyJointMeasurementEvidenceDto> AddEvidenceAsync(
        Guid id, Stream stream, string fileName, string contentType,
        AddQuantitySurveyJointMeasurementEvidenceRequest request, bool external,
        string correlationId, CancellationToken token = default)
    {
        if (request.ClientRequestId == Guid.Empty || !Enum.IsDefined(request.EvidenceType))
            throw Validation("Select a valid evidence type and provide a client request identifier.");
        var entity = await RequiredAsync(id, true, token);
        ExternalActor? actor = null;
        if (external) actor = await RequireExternalRequestAsync(entity, false, true, token);
        else await RequireInternalProjectAsync(entity.ProjectId);
        if (entity.Status is QuantitySurveyJointMeasurementStatuses.PendingApproval or
            QuantitySurveyJointMeasurementStatuses.ApprovedPendingBoqRevision or
            QuantitySurveyJointMeasurementStatuses.BoqWorkflowPending or
            QuantitySurveyJointMeasurementStatuses.Applied or
            QuantitySurveyJointMeasurementStatuses.Rejected or QuantitySurveyJointMeasurementStatuses.Cancelled)
            throw Conflict("Evidence cannot be changed after the joint measurement enters approval or reaches a terminal state.");
        if (entity.Evidence.Count >= MaximumEvidenceFiles)
            throw Validation($"A joint measurement can contain at most {MaximumEvidenceFiles} evidence files.");
        var policy = await ResolvePolicyAsync(DateTime.UtcNow, token);
        ValidateFrozenPolicy(entity, policy);
        var safeName = Path.GetFileName(fileName?.Trim());
        if (string.IsNullOrWhiteSpace(safeName) || safeName.Length > 260) throw Validation("Select a file with a valid name.");
        var extension = Path.GetExtension(safeName).ToLowerInvariant();
        if (!policy.External.AllowedFileExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            throw Validation("The selected file type is not allowed by the effective external-submission policy.");
        await using var memory = new MemoryStream(); await stream.CopyToAsync(memory, token);
        var maximumBytes = policy.External.MaximumFileSizeMb * 1024L * 1024L;
        if (memory.Length is < 1 || memory.Length > maximumBytes)
            throw Validation($"Evidence files must be between 1 byte and {policy.External.MaximumFileSizeMb} MB.");
        var bytes = memory.ToArray();
        var checksum = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var title = RequiredText(request.Title, 3, 200, "Evidence title");
        var canonicalType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType.Trim().ToLowerInvariant();
        var requestHash = Hash(new { RequestId = id, request.EvidenceType, Title = title, safeName, canonicalType, checksum });
        var retry = await db.QuantitySurveyJointMeasurementEvidence.AsNoTracking()
            .FirstOrDefaultAsync(value => value.TenantId == TenantId && value.ClientRequestId == request.ClientRequestId && !value.IsDeleted, token);
        if (retry is not null)
        {
            if (retry.RequestId != id || !FixedEquals(retry.RequestHash, requestHash)) throw RetryConflict();
            return Map(retry);
        }
        var upload = await controlledFiles.UploadAsync(new ControlledFileUploadRequest
        {
            TenantId = TenantId, ActorUserId = UserId, ActorName = UserName,
            Category = ControlledFileUploadCategories.QuantitySurveyMeasurementEvidence,
            FileName = safeName, ContentType = canonicalType, FileSize = bytes.LongLength,
            OpenReadStream = () => new MemoryStream(bytes, false)
        }, token);
        if (upload.Record.VirusScanStatus != FileVirusScanStatus.Clean || !FixedEquals(upload.ChecksumSha256, checksum))
        {
            await controlledFiles.DeleteAsync(TenantId, upload.Record.Id, UserId, token);
            throw Conflict("The centrally scanned evidence did not pass its integrity check.");
        }
        var evidenceId = Guid.NewGuid();
        CentralDocumentRepositoryLink document;
        try
        {
            document = await centralDocuments.RegisterAsync(new CentralDocumentRepositoryRegistration
            {
                TenantId = TenantId, ActorUserId = UserId, ActorName = UserName,
                FileUploadRecordId = upload.Record.Id, SourceModule = "QuantitySurvey",
                SourceLabel = "Quantity Survey joint remeasurement evidence",
                SourceEntityType = nameof(QuantitySurveyJointMeasurementEvidence), SourceRecordId = evidenceId,
                SourceRecordReference = entity.RequestNumber, Title = $"{entity.RequestNumber} · {title}",
                DocumentType = request.EvidenceType.ToString(), MetadataTemplateCode = entity.EvidenceMetadataTemplateCodeSnapshot,
                AccessProfile = entity.EvidenceMetadataTemplate.AccessProfile, VersionStatus = "Validated",
                ChangeSummary = "Clean scanned joint-remeasurement evidence retained in the central DMS.",
                RequirePublishedGovernance = true,
                MetadataValues =
                [
                    new("jointMeasurementRequestId", "Joint measurement request ID", entity.Id.ToString(), "guid"),
                    new("projectId", "Project ID", entity.ProjectId.ToString(), "guid"),
                    new("boqLineKey", "BoQ line key", entity.BoqLineKey.ToString(), "guid"),
                    new("evidenceType", "Evidence type", request.EvidenceType.ToString()),
                    new("checksumSha256", "Checksum SHA-256", checksum)
                ]
            }, token);
        }
        catch { await controlledFiles.DeleteAsync(TenantId, upload.Record.Id, UserId, token); throw; }
        try
        {
            var before = Snapshot(entity);
            var evidence = new QuantitySurveyJointMeasurementEvidence
            {
                Id = evidenceId, TenantId = TenantId, RequestId = entity.Id,
                ClientRequestId = request.ClientRequestId, RequestHash = requestHash,
                EvidenceType = request.EvidenceType, Title = title,
                OriginalFileName = upload.Record.OriginalFileName, ContentType = canonicalType,
                FileSize = bytes.LongLength, ChecksumSha256 = checksum,
                FileUploadRecordId = document.FileUploadRecordId,
                CentralDocumentRecordId = document.DocumentRecordId,
                CentralDocumentVersionId = document.DocumentVersionId,
                UploadedByUserId = UserId, UploadedByName = UserName, UploadedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId
            };
            entity.Evidence.Add(evidence);
            Touch(entity, QuantitySurveyAuditEventMap.AttachJointMeasurementEvidence, correlationId);
            AddRevision(entity, QuantitySurveyAuditEventMap.AttachJointMeasurementEvidence, title, before, Snapshot(entity), correlationId, actor?.BusinessPartnerId);
            AddAudit(entity, QuantitySurveyAuditEventMap.AttachJointMeasurementEvidence, before, Snapshot(entity), correlationId);
            await SaveAsync(token); return Map(evidence);
        }
        catch { await centralDocuments.DeleteAsync(TenantId, document.DocumentRecordId, UserId, token); throw; }
    }

    public async Task<CentralDocumentRepositoryContent> OpenEvidenceAsync(
        Guid id, Guid evidenceId, bool external, CancellationToken token = default)
    {
        var entity = await RequiredAsync(id, false, token);
        if (external) await RequireExternalRequestAsync(entity, false, false, token);
        else await RequireInternalProjectAsync(entity.ProjectId);
        var evidence = entity.Evidence.SingleOrDefault(value => value.Id == evidenceId && !value.IsDeleted)
            ?? throw NotFound("The joint-measurement evidence was not found.");
        return await centralDocuments.OpenAsync(TenantId, evidence.CentralDocumentRecordId, evidence.CentralDocumentVersionId, token)
            ?? throw Conflict("The central-DMS evidence content is unavailable.");
    }

    public async Task<IReadOnlyList<QuantitySurveyJointMeasurementRevisionDto>> HistoryAsync(
        Guid id, CancellationToken token = default)
    {
        var entity = await RequiredAsync(id, false, token); await RequireInternalProjectAsync(entity.ProjectId);
        return await db.QuantitySurveyJointMeasurementRevisions.AsNoTracking()
            .Where(value => value.TenantId == TenantId && value.RequestId == id && !value.IsDeleted)
            .OrderBy(value => value.CreatedAt).ThenBy(value => value.Id)
            .Select(value => new QuantitySurveyJointMeasurementRevisionDto
            {
                Id = value.Id, Action = value.Action, ActorName = value.ActorName,
                ActorRoles = value.ActorRoles, CorrelationId = value.CorrelationId,
                Reason = value.Reason, BeforeJson = value.BeforeJson, AfterJson = value.AfterJson,
                CreatedAt = value.CreatedAt
            }).ToListAsync(token);
    }

    private async Task EnsureBoqRevisionAsync(Guid id, string reason, string correlationId, CancellationToken token)
    {
        var entity = await RequiredAsync(id, true, token);
        if (entity.Status == QuantitySurveyJointMeasurementStatuses.Applied) return;
        if (entity.Status is not (QuantitySurveyJointMeasurementStatuses.ApprovedPendingBoqRevision or QuantitySurveyJointMeasurementStatuses.BoqWorkflowPending))
            throw Conflict("The approved joint measurement is not ready for BoQ revision.");
        if (!entity.MeasurementSheetId.HasValue) throw Conflict("The approved joint measurement has no Recorded measurement lineage.");
        if (!entity.RemeasurementVersionId.HasValue)
        {
            var version = await projectService.CreateProjectBoqRemeasurementAsync(entity.ProjectId,
                new CreateProjectBoqRemeasurementDto
                {
                    ClientRequestId = entity.RemeasurementClientRequestId,
                    MeasurementSheetIds = [entity.MeasurementSheetId.Value],
                    ChangeSummary = $"{entity.RequestNumber}: {entity.Title}"
                }, correlationId);
            db.ChangeTracker.Clear();
            entity = await RequiredAsync(id, true, token);
            if (!entity.RemeasurementVersionId.HasValue)
            {
                var before = Snapshot(entity);
                entity.RemeasurementVersionId = version.Id;
                entity.Status = QuantitySurveyJointMeasurementStatuses.BoqWorkflowPending;
                Touch(entity, QuantitySurveyAuditEventMap.CreateJointMeasurementBoqRevision, correlationId);
                AddRevision(entity, QuantitySurveyAuditEventMap.CreateJointMeasurementBoqRevision, reason, before, Snapshot(entity), correlationId);
                AddAudit(entity, QuantitySurveyAuditEventMap.CreateJointMeasurementBoqRevision, before, Snapshot(entity), correlationId);
                await SaveAsync(token);
            }
        }
        entity = await RequiredAsync(id, false, token);
        var candidate = await db.ProjectBoqVersions.AsNoTracking().FirstAsync(value =>
            value.TenantId == TenantId && value.Id == entity.RemeasurementVersionId && !value.IsDeleted, token);
        if (candidate.Status is ProjectBoqVersionStatuses.Draft or ProjectBoqVersionStatuses.Rejected)
            await projectService.SubmitProjectBoqVersionAsync(entity.ProjectId, candidate.Id, UserId, correlationId);
    }

    private IQueryable<QuantitySurveyJointMeasurementRequest> Query(bool tracking = false)
    {
        var query = tracking ? db.QuantitySurveyJointMeasurementRequests.AsTracking() : db.QuantitySurveyJointMeasurementRequests.AsNoTracking();
        return query.Include(value => value.Project).Include(value => value.ProjectBoqVersion)
            .Include(value => value.ProjectBoqVersionLine)
            .Include(value => value.ContractorBusinessPartner).Include(value => value.ConsultantBusinessPartner)
            .Include(value => value.EvidenceMetadataTemplate)
            .Include(value => value.MeasurementSheet).ThenInclude(value => value!.Attachments)
            .Include(value => value.RemeasurementVersion)
            .Include(value => value.Participants).ThenInclude(value => value.BusinessPartner)
            .Include(value => value.Evidence).ThenInclude(value => value.CentralDocumentRecord)
            .Include(value => value.Evidence).ThenInclude(value => value.CentralDocumentVersion)
            .Include(value => value.Endorsements).ThenInclude(value => value.BusinessPartner)
            .Where(value => value.TenantId == TenantId && !value.IsDeleted);
    }

    private async Task<QuantitySurveyJointMeasurementRequest> RequiredAsync(Guid id, bool tracking, CancellationToken token)
        => await Query(tracking).FirstOrDefaultAsync(value => value.Id == id, token)
           ?? throw NotFound("The joint remeasurement request was not found.");

    private async Task RequireInternalProjectAsync(Guid projectId)
    {
        if (await projectService.GetProjectByIdAsync(projectId) is null)
            throw new UnauthorizedAccessException("You are not permitted to access the selected project.");
    }

    private async Task<ExternalActor> RequireExternalPartnerAsync(CancellationToken token)
    {
        var link = await db.BusinessPartnerUsers.IgnoreQueryFilters().AsNoTracking()
            .Include(value => value.BusinessPartner).Include(value => value.User)
            .FirstOrDefaultAsync(value => value.UserId == UserId && value.IsActive && !value.IsDeleted &&
                                          !value.BusinessPartner.IsDeleted && value.BusinessPartner.TenantId == TenantId &&
                                          value.User.TenantId == TenantId, token)
            ?? throw new UnauthorizedAccessException("No active business-partner identity is linked to this portal user.");
        if (!link.BusinessPartner.IsActive ||
            !BusinessPartnerLifecyclePolicy.IsOperationalRegistration(link.BusinessPartner.RegistrationStatus))
            throw new UnauthorizedAccessException("The linked business partner is not active.");
        return new(link.BusinessPartnerId, link.BusinessPartner.PartnerName);
    }

    private async Task<ExternalActor> RequireExternalProjectAsync(Guid projectId, bool requireUpload,
        Guid? requestId, CancellationToken token)
    {
        var actor = await RequireExternalPartnerAsync(token);
        _ = await projectService.GetExternalProjectByIdAsync(projectId, UserId)
            ?? throw new UnauthorizedAccessException("You do not have access to the selected project.");
        var project = await db.Projects.AsNoTracking().FirstAsync(value => value.TenantId == TenantId && value.Id == projectId, token);
        if (!project.ExternalPortalAccessEnabled || !project.ExternalCollaborationEnabled)
            throw new UnauthorizedAccessException("External collaboration is not enabled for this project.");
        if (project.BusinessPartnerId == actor.BusinessPartnerId) return actor;
        var policies = await db.ProjectExternalAccessPolicies.AsNoTracking().Where(value =>
            value.TenantId == TenantId && value.ProjectId == projectId && value.BusinessPartnerId == actor.BusinessPartnerId &&
            !value.IsDeleted && (value.ArtifactType == "Project" ||
                (requestId.HasValue && value.ArtifactType == "JointMeasurement" && value.ArtifactId == requestId)))
            .ToListAsync(token);
        if (policies.Count == 0 || (requireUpload && !policies.Any(value => value.CanUpload)))
            throw new UnauthorizedAccessException("The project external-access policy does not permit this action.");
        return actor;
    }

    private async Task<ExternalActor> RequireExternalRequestAsync(QuantitySurveyJointMeasurementRequest entity,
        bool requireApprove, bool requireUpload, CancellationToken token)
    {
        var actor = await RequireExternalProjectAsync(entity.ProjectId, requireUpload, entity.Id, token);
        if (actor.BusinessPartnerId != entity.ContractorBusinessPartnerId && actor.BusinessPartnerId != entity.ConsultantBusinessPartnerId)
            throw new UnauthorizedAccessException("The linked business partner is not assigned to this joint measurement.");
        if (requireApprove)
        {
            var project = await db.Projects.AsNoTracking().FirstAsync(value => value.TenantId == TenantId && value.Id == entity.ProjectId, token);
            if (project.BusinessPartnerId != actor.BusinessPartnerId)
            {
                var allowed = await db.ProjectExternalAccessPolicies.AsNoTracking().AnyAsync(value =>
                    value.TenantId == TenantId && value.ProjectId == entity.ProjectId &&
                    value.BusinessPartnerId == actor.BusinessPartnerId && !value.IsDeleted && value.CanApprove &&
                    (value.ArtifactType == "Project" || (value.ArtifactType == "JointMeasurement" && value.ArtifactId == entity.Id)), token);
                if (!allowed) throw new UnauthorizedAccessException("The external-access policy does not permit endorsement.");
            }
        }
        return actor;
    }

    private async Task<List<Guid>> AccessibleExternalPartnerIdsAsync(Guid projectId, CancellationToken token)
    {
        var projectPartner = await db.Projects.AsNoTracking().Where(value => value.TenantId == TenantId && value.Id == projectId)
            .Select(value => value.BusinessPartnerId).FirstOrDefaultAsync(token);
        var values = await db.ProjectExternalAccessPolicies.AsNoTracking()
            .Where(value => value.TenantId == TenantId && value.ProjectId == projectId && !value.IsDeleted &&
                            value.ArtifactType == "Project")
            .Select(value => value.BusinessPartnerId).Distinct().ToListAsync(token);
        if (projectPartner.HasValue) values.Add(projectPartner.Value);
        return values.Distinct().ToList();
    }

    private async Task<List<Guid>> AccessibleExternalProjectIdsAsync(Guid businessPartnerId, CancellationToken token)
    {
        var policyIds = db.ProjectExternalAccessPolicies.AsNoTracking().Where(value =>
            value.TenantId == TenantId && value.BusinessPartnerId == businessPartnerId && !value.IsDeleted)
            .Select(value => value.ProjectId);
        return await db.Projects.AsNoTracking().Where(value => value.TenantId == TenantId && !value.IsDeleted &&
                value.ExternalPortalAccessEnabled && value.ExternalCollaborationEnabled &&
                (value.BusinessPartnerId == businessPartnerId || policyIds.Contains(value.Id)))
            .Select(value => value.Id).Distinct().ToListAsync(token);
    }

    private async Task RequireProjectPartnerAsync(
        Guid projectId, Guid partnerId, CancellationToken token, bool requireContractor = false)
    {
        var allowed = (await AccessibleExternalPartnerIdsAsync(projectId, token)).Contains(partnerId);
        var partner = await db.BusinessPartners.AsNoTracking().FirstOrDefaultAsync(value =>
            value.TenantId == TenantId && value.Id == partnerId && !value.IsDeleted && value.IsActive && !value.IsBlacklisted &&
            (value.RegistrationStatus == BusinessPartnerLifecyclePolicy.ActiveRegistrationStatus ||
             value.RegistrationStatus == BusinessPartnerLifecyclePolicy.LegacyApprovedRegistrationStatus), token);
        if (!allowed || partner is null)
            throw Validation("Select an active business partner with controlled access to this project.");
        if (requireContractor && partner.PartnerType is not ("Contractor" or "Both"))
            throw Validation("Select an approved contractor business partner for the joint remeasurement request.");
    }

    private async Task<ProjectBoqVersionLine> RequireApprovedLineAsync(Guid projectId, Guid lineId, CancellationToken token)
    {
        var line = await db.ProjectBoqVersionLines.AsNoTracking().Include(value => value.Version)
            .FirstOrDefaultAsync(value => value.TenantId == TenantId && value.ProjectId == projectId && value.Id == lineId && !value.IsDeleted, token)
            ?? throw Validation("Select an approved BoQ item in this project.");
        if (line.ItemType != ProjectBoqItemTypes.Item || line.Version.TenantId != TenantId || line.Version.ProjectId != projectId ||
            line.Version.IsDeleted || line.Version.Status != ProjectBoqVersionStatuses.Approved || line.Version.PublishedAt is null)
            throw Validation("Joint remeasurement requests require an approved published BoQ item.");
        return line;
    }

    private async Task<PolicyContext> ResolvePolicyAsync(DateTime at, CancellationToken token)
    {
        var profiles = await db.QuantitySurveyConfigurationProfiles.AsNoTracking().Where(value =>
                value.TenantId == TenantId && !value.IsDeleted &&
                value.LifecycleStatus == QuantitySurveyConfigurationProfileStatus.Published && value.PublishedAt != null &&
                value.EffectiveFrom <= at && (!value.EffectiveTo.HasValue || value.EffectiveTo >= at))
            .OrderByDescending(value => value.IsDefault).ThenByDescending(value => value.Version).ToListAsync(token);
        if (profiles.Count == 0) throw Validation("No Published quantity-survey configuration is effective for this date.");
        if (profiles.Count > 1 && profiles[0].IsDefault == profiles[1].IsDefault)
            throw Conflict("More than one quantity-survey configuration is effective for this date.");
        var profile = profiles[0];
        var decisions = await db.QuantitySurveyConfigurationDecisions.AsNoTracking().Where(value =>
            value.TenantId == TenantId && value.ProfileId == profile.Id && !value.IsDeleted &&
            (value.DecisionKey == "QS-DEC-007" || value.DecisionKey == "QS-DEC-013")).ToListAsync(token);
        var measurementDecision = decisions.SingleOrDefault(value => value.DecisionKey == "QS-DEC-007")
            ?? throw Validation("The effective configuration has no QS-DEC-007 measurement decision.");
        var externalDecision = decisions.SingleOrDefault(value => value.DecisionKey == "QS-DEC-013")
            ?? throw Validation("The effective configuration has no QS-DEC-013 external-submission decision.");
        ValidateDecision(measurementDecision, at, "QS-DEC-007"); ValidateDecision(externalDecision, at, "QS-DEC-013");
        QsMeasurementValue measurement; QsExternalSubmissionValue external;
        try
        {
            measurement = JsonSerializer.Deserialize<QsMeasurementValue>(measurementDecision.ValueJson, JsonOptions) ?? new();
            external = JsonSerializer.Deserialize<QsExternalSubmissionValue>(externalDecision.ValueJson, JsonOptions) ?? new();
        }
        catch (JsonException) { throw Conflict("The effective QS measurement or external-submission policy contains invalid data."); }
        if (measurement.WorkflowDefinitionId == Guid.Empty || measurement.MetadataTemplateId == Guid.Empty ||
            measurement.JointAttendanceRoleIds.Count == 0 || measurement.ConsultantRoleIds.Count == 0)
            throw Conflict("QS-DEC-007 is incomplete for joint measurement.");
        if (external.Channels.Count == 0 || external.AllowedFileExtensions.Count == 0 || external.MaximumFileSizeMb < 1)
            throw Conflict("QS-DEC-013 is incomplete for external joint-measurement submissions.");
        var template = await db.CentralDocumentMetadataTemplates.AsNoTracking().FirstOrDefaultAsync(value =>
            value.TenantId == TenantId && value.Id == measurement.MetadataTemplateId && !value.IsDeleted, token)
            ?? throw Validation("The QS joint-measurement DMS template is unavailable.");
        if (!template.IsActive || template.PublishedAt is null)
            throw Validation("The QS joint-measurement DMS template must be active and published.");
        var workflowDefinition = await db.WorkflowDefinitions.AsNoTracking().Include(value => value.EntityType)
            .FirstOrDefaultAsync(value => value.TenantId == TenantId && value.Id == measurement.WorkflowDefinitionId &&
                !value.IsDeleted && value.IsActive && value.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Published, token);
        if (workflowDefinition is null || workflowDefinition.EntityType is null ||
            !string.Equals(workflowDefinition.EntityType.Code, QuantitySurveyWorkflowBindingRegistry.Measurement, StringComparison.OrdinalIgnoreCase))
            throw Conflict("QS-DEC-007 must reference an active Published QS_MEASUREMENT workflow.");
        var configuredRoleIds = measurement.JointAttendanceRoleIds.Concat(measurement.ConsultantRoleIds).Distinct().ToList();
        if (await db.Roles.AsNoTracking().CountAsync(value => configuredRoleIds.Contains(value.Id), token) != configuredRoleIds.Count)
            throw Conflict("One or more QS-DEC-007 attendance or consultant roles are unavailable.");
        var policyHash = Hash(new { Profile = profile.Id, profile.Version, Measurement = measurementDecision.Id,
            MeasurementValueJson = measurementDecision.ValueJson, External = externalDecision.Id,
            ExternalValueJson = externalDecision.ValueJson,
            Template = template.Id, template.TemplateCode, Workflow = workflowDefinition.Id });
        return new(profile, measurementDecision, externalDecision, measurement, external, template, policyHash);
    }

    private static void ValidateDecision(QuantitySurveyConfigurationDecision value, DateTime at, string key)
    {
        if (value.Status != QuantitySurveyConfigurationDecisionStatus.Approved ||
            value.ApprovalStatus != QuantitySurveyConfigurationApprovalStatus.Approved ||
            value.EvidenceStatus != QuantitySurveyConfigurationEvidenceStatus.Verified ||
            (value.EffectiveFrom.HasValue && value.EffectiveFrom > at) ||
            (value.EffectiveTo.HasValue && value.EffectiveTo < at))
            throw Validation($"{key} is not approved, verified, and effective for this date.");
    }

    private async Task ValidateFrozenContextAsync(QuantitySurveyJointMeasurementRequest entity, CancellationToken token)
    {
        var line = await RequireApprovedLineAsync(entity.ProjectId, entity.ProjectBoqVersionLineId, token);
        if (line.ProjectBoqVersionId != entity.ProjectBoqVersionId || line.LineKey != entity.BoqLineKey ||
            line.Quantity != entity.PreviousQuantity)
            throw Conflict("The approved BoQ lineage changed. Create a new joint remeasurement request.");
        ValidateFrozenPolicy(entity, await ResolvePolicyAsync(DateTime.UtcNow, token));
    }

    private static void ValidateFrozenPolicy(QuantitySurveyJointMeasurementRequest entity, PolicyContext policy)
    {
        if (entity.ConfigurationProfileId != policy.Profile.Id || entity.MeasurementDecisionId != policy.MeasurementDecision.Id ||
            entity.ExternalSubmissionDecisionId != policy.ExternalDecision.Id ||
            entity.ApprovalWorkflowDefinitionId != policy.Measurement.WorkflowDefinitionId ||
            entity.EvidenceMetadataTemplateId != policy.Template.Id || !FixedEquals(entity.PolicyHash, policy.PolicyHash))
            throw Conflict("The governing QS configuration changed. Create a new joint remeasurement request.");
    }

    private async Task ValidateReviewReadinessAsync(QuantitySurveyJointMeasurementRequest entity, CancellationToken token)
    {
        await ValidateFrozenContextAsync(entity, token);
        if (!entity.MeasurementSheetId.HasValue || entity.MeasurementSheet is null || entity.MeasurementSheet.Status != "Recorded")
            throw Conflict("Link a governed Recorded measurement before QS review.");
        if (entity.Participants.Any(value => value.IsRequired && value.AttendanceStatus != "Attended"))
            throw Conflict("Every required joint-measurement participant must record attendance.");
        if (entity.ContractorSignatureRequired && !entity.Endorsements.Any(value => value.SignerType == QuantitySurveyJointMeasurementParticipantTypes.Contractor))
            throw Conflict("The assigned contractor must endorse the joint measurement.");
        if (entity.ConsultantSignatureRequired && !entity.Endorsements.Any(value => value.SignerType == QuantitySurveyJointMeasurementParticipantTypes.Consultant))
            throw Conflict("The assigned consultant must endorse the joint measurement.");
        if (entity.EvidenceRequired && entity.Evidence.Count == 0 && entity.MeasurementSheet.Attachments.Count == 0)
            throw Conflict("The governed joint measurement requires central-DMS evidence.");
        if (entity.Endorsements.Select(value => value.SignedByUserId).Distinct().Count() != entity.Endorsements.Count)
            throw Conflict("A single user cannot provide multiple required endorsements.");
    }

    private void ValidateParticipantActor(QuantitySurveyJointMeasurementParticipant participant, ExternalActor? externalActor)
    {
        if (externalActor is not null)
        {
            if (participant.BusinessPartnerId != externalActor.Value.BusinessPartnerId ||
                participant.ParticipantType == QuantitySurveyJointMeasurementParticipantTypes.InternalRole)
                throw new UnauthorizedAccessException("This portal user is not assigned to the selected participant role.");
            return;
        }
        if (participant.ParticipantType != QuantitySurveyJointMeasurementParticipantTypes.InternalRole ||
            string.IsNullOrWhiteSpace(participant.RequiredRoleNameSnapshot) ||
            !currentUser.Roles.Contains(participant.RequiredRoleNameSnapshot, StringComparer.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Your assigned roles do not include this joint-attendance role.");
    }

    private static void AdvanceReadiness(QuantitySurveyJointMeasurementRequest entity)
    {
        var allAttended = entity.Participants.Count > 0 && entity.Participants.All(value => !value.IsRequired || value.AttendanceStatus == "Attended");
        if (!allAttended || !entity.MeasurementSheetId.HasValue)
        {
            entity.Status = QuantitySurveyJointMeasurementStatuses.AwaitingAttendance; return;
        }
        var contractorDone = !entity.ContractorSignatureRequired || entity.Endorsements.Any(value => value.SignerType == QuantitySurveyJointMeasurementParticipantTypes.Contractor);
        var consultantDone = !entity.ConsultantSignatureRequired || entity.Endorsements.Any(value => value.SignerType == QuantitySurveyJointMeasurementParticipantTypes.Consultant);
        entity.Status = contractorDone && consultantDone
            ? QuantitySurveyJointMeasurementStatuses.ReadyForReview
            : QuantitySurveyJointMeasurementStatuses.AwaitingEndorsements;
    }

    private static void ValidateMeasurementEvidence(QuantitySurveyMeasurementSheet value)
    {
        if (value.Attachments.Any(item => item.IsDeleted || item.TenantId != value.TenantId ||
            item.FileUploadRecord.TenantId != value.TenantId || item.FileUploadRecord.IsDeleted ||
            item.FileUploadRecord.Category != ControlledFileUploadCategories.QuantitySurveyMeasurementEvidence ||
            item.FileUploadRecord.VirusScanStatus != FileVirusScanStatus.Clean ||
            item.CentralDocumentRecord.TenantId != value.TenantId || item.CentralDocumentRecord.IsDeleted ||
            item.CentralDocumentVersion.TenantId != value.TenantId || item.CentralDocumentVersion.IsDeleted ||
            item.CentralDocumentVersion.DocumentRecordId != item.CentralDocumentRecordId ||
            item.CentralDocumentVersion.FileUploadRecordId != item.FileUploadRecordId || item.CentralDocumentVersion.Status != "Validated"))
            throw Conflict("Recorded measurement evidence no longer satisfies central clean-scan and DMS lineage controls.");
    }

    private async Task<WorkflowInstanceStatus?> WorkflowStatusAsync(QuantitySurveyJointMeasurementRequest entity, CancellationToken token)
        => !entity.WorkflowInstanceId.HasValue ? null :
            (await db.WorkflowInstances.AsNoTracking().FirstOrDefaultAsync(value =>
                value.TenantId == TenantId && value.Id == entity.WorkflowInstanceId && value.EntityId == entity.Id, token))?.Status;

    private static string BuildAttestation(QuantitySurveyJointMeasurementRequest entity)
        => EndorsementAttestationPrefix;

    private static QuantitySurveyJointMeasurementDto Map(QuantitySurveyJointMeasurementRequest value) => new()
    {
        Id = value.Id, ProjectId = value.ProjectId, ProjectCode = value.Project.ProjectCode,
        ProjectTitle = value.Project.Title, ProjectBoqVersionId = value.ProjectBoqVersionId,
        ProjectBoqVersionNumber = value.ProjectBoqVersion.VersionNumber,
        ProjectBoqVersionLineId = value.ProjectBoqVersionLineId, BoqLineKey = value.BoqLineKey,
        BoqLineLabel = (value.ProjectBoqVersionLine.LineNumber ?? value.ProjectBoqVersionLine.ItemCode ?? "Item") + " · " + value.ProjectBoqVersionLine.Description,
        UnitOfMeasure = value.ProjectBoqVersionLine.UnitOfMeasure,
        ContractorBusinessPartnerId = value.ContractorBusinessPartnerId,
        ContractorName = value.ContractorBusinessPartner.PartnerName,
        ConsultantBusinessPartnerId = value.ConsultantBusinessPartnerId,
        ConsultantName = value.ConsultantBusinessPartner?.PartnerName,
        RequestNumber = value.RequestNumber, Title = value.Title, Reason = value.Reason,
        PreviousQuantity = value.PreviousQuantity, ContractorProposedQuantity = value.ContractorProposedQuantity,
        RecordedQuantity = value.MeasurementSheet?.TotalMeasuredQuantity,
        RequestedSiteLocation = value.RequestedSiteLocation, PreferredStartAt = value.PreferredStartAt,
        PreferredEndAt = value.PreferredEndAt, Status = value.Status, ApprovalStatus = value.ApprovalStatus,
        ScheduledStartAt = value.ScheduledStartAt, ScheduledEndAt = value.ScheduledEndAt,
        ScheduledSiteLocation = value.ScheduledSiteLocation, MeasurementSheetId = value.MeasurementSheetId,
        MeasurementSheetReference = value.MeasurementSheet?.SheetReference, WorkflowInstanceId = value.WorkflowInstanceId,
        RemeasurementVersionId = value.RemeasurementVersionId,
        RemeasurementVersionNumber = value.RemeasurementVersion?.VersionNumber,
        ContractorSignatureRequired = value.ContractorSignatureRequired,
        ConsultantSignatureRequired = value.ConsultantSignatureRequired,
        EvidenceRequired = value.EvidenceRequired, EndorsementAttestation = BuildAttestation(value),
        RequestedByName = value.RequestedByName,
        RequestedAt = value.RequestedAt, SubmittedAt = value.SubmittedAt,
        ApprovedAt = value.ApprovedAt, AppliedAt = value.AppliedAt,
        RejectionReason = value.RejectionReason, RowVersion = Convert.ToBase64String(value.RowVersion),
        Participants = value.Participants.Where(item => !item.IsDeleted).OrderBy(item => item.ParticipantType)
            .ThenBy(item => item.RequiredRoleNameSnapshot).Select(item => new QuantitySurveyJointMeasurementParticipantDto
            {
                Id = item.Id, ParticipantType = item.ParticipantType, BusinessPartnerId = item.BusinessPartnerId,
                BusinessPartnerName = item.BusinessPartner?.PartnerName, RequiredRoleId = item.RequiredRoleId,
                RequiredRoleName = item.RequiredRoleNameSnapshot, IsRequired = item.IsRequired,
                AttendanceStatus = item.AttendanceStatus, AttendedByName = item.AttendedByName,
                AttendedAt = item.AttendedAt, AttendanceNotes = item.AttendanceNotes
            }).ToList(),
        Evidence = value.Evidence.Where(item => !item.IsDeleted).OrderBy(item => item.UploadedAt).Select(Map).ToList(),
        Endorsements = value.Endorsements.Where(item => !item.IsDeleted).OrderBy(item => item.SignedAt).Select(item => new QuantitySurveyJointMeasurementEndorsementDto
        {
            Id = item.Id, SignerType = item.SignerType, BusinessPartnerId = item.BusinessPartnerId,
            BusinessPartnerName = item.BusinessPartner?.PartnerName, SignedByName = item.SignedByName,
            SignatureMethod = Enum.Parse<WorkflowSignatureMethod>(item.SignatureMethod),
            Attestation = item.AttestationSnapshot, CertificateThumbprint = item.CertificateThumbprint,
            ExternalSignatureReference = item.ExternalSignatureReference, Notes = item.Notes, SignedAt = item.SignedAt
        }).ToList()
    };

    private static QuantitySurveyJointMeasurementEvidenceDto Map(QuantitySurveyJointMeasurementEvidence value) => new()
    {
        Id = value.Id, EvidenceType = value.EvidenceType, Title = value.Title,
        OriginalFileName = value.OriginalFileName, ContentType = value.ContentType,
        FileSize = value.FileSize, ChecksumSha256 = value.ChecksumSha256,
        CentralDocumentRecordId = value.CentralDocumentRecordId,
        CentralDocumentVersionId = value.CentralDocumentVersionId,
        UploadedByName = value.UploadedByName, UploadedAt = value.UploadedAt
    };

    private void AddRevision(QuantitySurveyJointMeasurementRequest entity, string action, string? reason,
        object? before, object after, string correlationId, Guid? actorPartnerId = null)
        => db.QuantitySurveyJointMeasurementRevisions.Add(new QuantitySurveyJointMeasurementRevision
        {
            Id = Guid.NewGuid(), TenantId = TenantId, RequestId = entity.Id,
            Action = action, ActorUserId = UserId, ActorBusinessPartnerId = actorPartnerId,
            ActorName = UserName, ActorRoles = ActorRoles,
            CorrelationId = NormalizeCorrelation(correlationId), Reason = Clean(reason, 2000),
            BeforeJson = before is null ? null : JsonSerializer.Serialize(before, JsonOptions),
            AfterJson = JsonSerializer.Serialize(after, JsonOptions), CreatedAt = DateTime.UtcNow,
            CreatedBy = UserName, CreatedById = UserId
        });

    private void AddAudit(QuantitySurveyJointMeasurementRequest entity, string action, object? before,
        object after, string correlationId) => db.AuditLogs.Add(new AuditLog
    {
        TenantId = TenantId, UserId = UserId, Username = UserName, Action = action,
        Resource = nameof(QuantitySurveyJointMeasurementRequest), ResourceId = entity.Id.ToString(),
        OldValues = before is null ? null : JsonSerializer.Serialize(before, JsonOptions),
        NewValues = JsonSerializer.Serialize(new { correlationId = NormalizeCorrelation(correlationId), value = after }, JsonOptions),
        IpAddress = "api", UserAgent = "QS-0403", Timestamp = DateTime.UtcNow,
        CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId
    });

    private static object Snapshot(QuantitySurveyJointMeasurementRequest value) => new
    {
        value.Id, value.ProjectId, value.ProjectBoqVersionId, value.ProjectBoqVersionLineId,
        value.BoqLineKey, value.ContractorBusinessPartnerId, value.ConsultantBusinessPartnerId,
        value.RequestNumber, value.Title, value.PreviousQuantity, value.ContractorProposedQuantity,
        value.Status, value.ApprovalStatus, value.ScheduledStartAt, value.ScheduledEndAt,
        value.ScheduledSiteLocation, value.MeasurementSheetId, value.WorkflowInstanceId,
        value.RemeasurementVersionId, value.ApprovedById, value.ApprovedAt, value.AppliedAt,
        Participants = value.Participants.Where(item => !item.IsDeleted).Select(item => new
        {
            item.Id, item.ParticipantType, item.BusinessPartnerId, item.RequiredRoleId,
            item.AttendanceStatus, item.AttendedByUserId, item.AttendedAt, item.AttendanceHash
        }),
        Evidence = value.Evidence.Where(item => !item.IsDeleted).Select(item => new
        {
            item.Id, item.EvidenceType, item.ChecksumSha256, item.CentralDocumentRecordId, item.CentralDocumentVersionId
        }),
        Endorsements = value.Endorsements.Where(item => !item.IsDeleted).Select(item => new
        {
            item.Id, item.SignerType, item.BusinessPartnerId, item.SignedByUserId,
            item.SignatureMethod, item.SignatureHash, item.SignedAt
        })
    };

    private static bool IsMutationRetry(QuantitySurveyJointMeasurementRequest entity, Guid clientRequestId, string requestHash)
    {
        if (clientRequestId == Guid.Empty) throw Validation("A client request identifier is required.");
        if (entity.LastMutationClientRequestId != clientRequestId) return false;
        if (!FixedEquals(entity.LastMutationRequestHash, requestHash)) throw RetryConflict();
        return true;
    }

    private void Touch(QuantitySurveyJointMeasurementRequest entity, string action, string correlationId,
        Guid? clientRequestId = null, string? requestHash = null)
    {
        entity.AuditAction = action; entity.CorrelationId = NormalizeCorrelation(correlationId);
        if (clientRequestId.HasValue) { entity.LastMutationClientRequestId = clientRequestId; entity.LastMutationRequestHash = requestHash; }
        entity.UpdatedAt = DateTime.UtcNow; entity.UpdatedBy = UserName; entity.LastModifiedById = UserId;
    }

    private static void CheckVersion(byte[] current, string supplied)
    {
        byte[] expected;
        try { expected = Convert.FromBase64String(supplied); }
        catch (FormatException) { throw Conflict("The row version is invalid. Refresh and retry."); }
        if (!CryptographicOperations.FixedTimeEquals(current, expected)) throw Conflict("The record changed. Refresh and retry.");
    }

    private async Task SaveAsync(CancellationToken token)
    {
        try { await db.SaveChangesAsync(token); }
        catch (DbUpdateConcurrencyException) { throw Conflict("The record changed concurrently. Refresh and retry."); }
    }

    private static void ValidateDateWindow(DateTime? start, DateTime? end, string label)
    {
        if (start.HasValue != end.HasValue) throw Validation($"Enter both start and end for the {label}.");
        if (start.HasValue && Utc(end) <= Utc(start)) throw Validation($"The {label} end must be after its start.");
    }

    private static string RequiredText(string? value, int min, int max, string label)
    {
        var clean = value?.Trim();
        if (string.IsNullOrWhiteSpace(clean) || clean.Length < min || clean.Length > max)
            throw Validation($"{label} must contain between {min} and {max} characters.");
        return clean;
    }

    private static string? Clean(string? value, int max)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim().Length <= max
            ? value.Trim() : throw Validation($"Text cannot exceed {max} characters.");
    private static DateTime? Utc(DateTime? value) => !value.HasValue ? null : value.Value.Kind == DateTimeKind.Utc
        ? value : value.Value.ToUniversalTime();
    private static string NormalizeCorrelation(string? value) => string.IsNullOrWhiteSpace(value)
        ? Guid.NewGuid().ToString("N") : value.Trim().Length <= 100 ? value.Trim() : value.Trim()[..100];
    private static string Hash(object value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        JsonSerializer.Serialize(value, JsonOptions)))).ToLowerInvariant();
    private static bool FixedEquals(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right)) return false;
        var a = Encoding.UTF8.GetBytes(left); var b = Encoding.UTF8.GetBytes(right);
        return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
    }
    private static bool IsUnique(DbUpdateException exception)
        => exception.InnerException is SqlException { Number: 2601 or 2627 };
    private static QuantitySurveyJointMeasurementValidationException Validation(string message) => new(message);
    private static QuantitySurveyJointMeasurementConflictException Conflict(string message) => new(message);
    private static QuantitySurveyJointMeasurementNotFoundException NotFound(string message) => new(message);
    private static QuantitySurveyJointMeasurementConflictException RetryConflict()
        => Conflict("The client request identifier was already used with a different payload. Refresh and retry.");
    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }

    private readonly record struct ExternalActor(Guid BusinessPartnerId, string PartnerName);
    private sealed record PolicyContext(
        QuantitySurveyConfigurationProfile Profile,
        QuantitySurveyConfigurationDecision MeasurementDecision,
        QuantitySurveyConfigurationDecision ExternalDecision,
        QsMeasurementValue Measurement,
        QsExternalSubmissionValue External,
        ErpSystem.Core.Entities.DocumentManagement.CentralDocumentMetadataTemplate Template,
        string PolicyHash);
}
