using System.Text.Json;
using System.Text.Json.Serialization;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Procurement;

public sealed class ProcurementConfigurationService : IProcurementConfigurationService
{
    private static readonly JsonSerializerOptions AuditJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUser;
    private readonly ILogger<ProcurementConfigurationService> _logger;

    public ProcurementConfigurationService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUser,
        ILogger<ProcurementConfigurationService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _logger = logger;
    }

    private IGenericRepository<ProcurementConfigurationProfile> Profiles => _unitOfWork.Repository<ProcurementConfigurationProfile>();
    private IGenericRepository<ProcurementConfigurationDecision> Decisions => _unitOfWork.Repository<ProcurementConfigurationDecision>();
    private IGenericRepository<ProcurementConfigurationEvidenceLink> EvidenceLinks => _unitOfWork.Repository<ProcurementConfigurationEvidenceLink>();
    private IGenericRepository<ProcurementConfigurationRevision> Revisions => _unitOfWork.Repository<ProcurementConfigurationRevision>();
    private IGenericRepository<FileUploadRecord> FileUploads => _unitOfWork.Repository<FileUploadRecord>();

    public IReadOnlyList<ProcurementDecisionSchemaDto> GetDecisionSchemas() =>
        ProcurementConfigurationDecisionRegistry.ToDtos();

    public async Task<ProcurementConfigurationPagedResult<ProcurementConfigurationProfileSummaryDto>> GetProfilesAsync(
        ProcurementConfigurationProfileListRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var query = Profiles.GetQueryable(item => item.TenantId == _currentUser.TenantId);

        if (request.Status.HasValue)
            query = query.Where(item => item.LifecycleStatus == request.Status.Value);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(item => item.Name.Contains(search) || item.ProfileCode.Contains(search));
        }

        var total = await query.CountAsync(cancellationToken);
        var profiles = await query
            .AsNoTracking()
            .OrderByDescending(item => item.UpdatedAt ?? item.CreatedAt)
            .ThenByDescending(item => item.Version)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var profileIds = profiles.Select(item => item.Id).ToList();
        var decisions = profileIds.Count == 0
            ? new List<ProcurementConfigurationDecision>()
            : await Decisions.GetQueryable(item =>
                    item.TenantId == _currentUser.TenantId && profileIds.Contains(item.ProfileId))
                .AsNoTracking()
                .ToListAsync(cancellationToken);

        return new ProcurementConfigurationPagedResult<ProcurementConfigurationProfileSummaryDto>
        {
            Items = profiles.Select(profile => MapSummary(profile, decisions.Where(item => item.ProfileId == profile.Id))).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        };
    }

    public async Task<ProcurementConfigurationProfileDto> GetProfileAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        var profile = await FindProfileAsync(id, tracked: false, cancellationToken);
        return await MapProfileAsync(profile, cancellationToken);
    }

    public async Task<ProcurementConfigurationProfileDto?> GetEffectiveProfileAsync(
        string profileCode,
        DateTime atUtc,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        var code = NormalizeProfileCode(profileCode);
        var profile = await Profiles.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.ProfileCode == code &&
                item.LifecycleStatus == ProcurementConfigurationProfileStatus.Published &&
                item.EffectiveFrom <= atUtc &&
                (!item.EffectiveTo.HasValue || item.EffectiveTo.Value >= atUtc))
            .AsNoTracking()
            .OrderByDescending(item => item.Version)
            .FirstOrDefaultAsync(cancellationToken);

        return profile is null ? null : await MapProfileAsync(profile, cancellationToken);
    }

    public async Task<ProcurementConfigurationProfileDto> CreateProfileAsync(
        CreateProcurementConfigurationProfileRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureEditor();
        ValidateProfileDates(request.EffectiveFrom, request.EffectiveTo);
        var code = NormalizeProfileCode(request.ProfileCode);

        if (await Profiles.GetQueryable(item => item.TenantId == _currentUser.TenantId && item.ProfileCode == code)
                .AnyAsync(cancellationToken))
            throw new ProcurementConfigurationConflictException(
                $"A procurement configuration family with code '{code}' already exists. Clone its latest version instead.");

        var now = DateTime.UtcNow;
        var profile = new ProcurementConfigurationProfile
        {
            TenantId = _currentUser.TenantId,
            ProfileKey = Guid.NewGuid(),
            ProfileCode = code,
            Name = request.Name.Trim(),
            Version = 1,
            LifecycleStatus = ProcurementConfigurationProfileStatus.Draft,
            EffectiveFrom = EnsureUtc(request.EffectiveFrom),
            EffectiveTo = EnsureUtc(request.EffectiveTo),
            ChangeSummary = NullIfWhiteSpace(request.ChangeSummary),
            IsDefault = request.IsDefault,
            CreatedAt = now,
            CreatedBy = _currentUser.FullName,
            CreatedById = _currentUser.UserId
        };

        await Profiles.AddAsync(profile);
        var decisions = ProcurementConfigurationDecisionRegistry.Definitions.Select(definition =>
            NewDecision(profile, definition, now)).ToList();
        await Decisions.AddRangeAsync(decisions);
        await AddRevisionAsync(profile.Id, null, "Create", "Succeeded", correlationId, request.ChangeSummary, null, ProfileSnapshot(profile));
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Created procurement configuration profile {ProfileId} version {Version} for tenant {TenantId}",
            profile.Id,
            profile.Version,
            _currentUser.TenantId);
        return await GetProfileAsync(profile.Id, cancellationToken);
    }

    public async Task<ProcurementConfigurationProfileDto> UpdateProfileAsync(
        Guid id,
        UpdateProcurementConfigurationProfileRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureEditor();
        var profile = await FindProfileAsync(id, tracked: true, cancellationToken);
        ProcurementConfigurationLifecyclePolicy.EnsureEditable(profile);
        EnsureRowVersion(profile.RowVersion, request.RowVersion, "profile");
        ValidateProfileDates(request.EffectiveFrom, request.EffectiveTo);
        var before = ProfileSnapshot(profile);

        profile.Name = request.Name.Trim();
        profile.EffectiveFrom = EnsureUtc(request.EffectiveFrom);
        profile.EffectiveTo = EnsureUtc(request.EffectiveTo);
        profile.ChangeSummary = NullIfWhiteSpace(request.ChangeSummary);
        profile.IsDefault = request.IsDefault;
        SetModified(profile);

        await Profiles.UpdateAsync(profile);
        await AddRevisionAsync(profile.Id, null, "UpdateProfile", "Succeeded", correlationId, request.Reason, before, ProfileSnapshot(profile));
        await SaveWithConcurrencyAsync("profile", cancellationToken);
        return await GetProfileAsync(profile.Id, cancellationToken);
    }

    public async Task<ProcurementConfigurationDecisionDto> SaveDecisionAsync(
        Guid profileId,
        string decisionKey,
        SaveProcurementConfigurationDecisionRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureEditor();
        var profile = await FindProfileAsync(profileId, tracked: true, cancellationToken);
        var decision = await FindDecisionAsync(profileId, decisionKey, tracked: true, cancellationToken);
        ProcurementConfigurationLifecyclePolicy.EnsureEditable(profile);
        EnsureRowVersion(decision.RowVersion, request.RowVersion, decision.DecisionKey);

        var returnToProposed = ProcurementConfigurationLifecyclePolicy.IsReturnToProposed(
            decision,
            request.Status,
            request.ApprovalStatus);
        if (!ProcurementConfigurationLifecyclePolicy.IsDecisionEditable(profile, decision) && !returnToProposed)
            ProcurementConfigurationLifecyclePolicy.EnsureDecisionEditable(profile, decision);

        var before = DecisionSnapshot(decision);
        if (returnToProposed && !_currentUser.HasRole("SuperAdmin"))
        {
            await AddRevisionAsync(profile.Id, decision.Id, "ReturnDecisionToProposed", "Rejected", correlationId,
                "Only SuperAdmin may return an approved or rejected configuration decision to proposed.", before, null);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            throw new ProcurementConfigurationAuthorizationException(
                "Only SuperAdmin may return an approved or rejected configuration decision to proposed.");
        }

        var definition = ProcurementConfigurationDecisionRegistry.GetRequired(decision.DecisionKey);
        var valueValidation = ProcurementConfigurationDecisionRegistry.Validate(
            decision.DecisionKey,
            request.SchemaVersion,
            request.Value);
        if (!valueValidation.IsValid)
            throw ValidationException(decision.DecisionKey, valueValidation.Errors);

        ValidateDecisionState(request, decision.DecisionKey);
        if (request.Status == ProcurementConfigurationDecisionStatus.Approved && !_currentUser.HasRole("SuperAdmin"))
        {
            await AddRevisionAsync(profile.Id, decision.Id, "ApproveDecision", "Rejected", correlationId,
                "Only SuperAdmin may approve a configuration decision.", before, null);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            throw new ProcurementConfigurationAuthorizationException("Only SuperAdmin may approve a configuration decision.");
        }

        decision.SchemaVersion = request.SchemaVersion;
        decision.OwnerGroup = request.OwnerGroup.Trim();
        decision.Status = request.Status;
        decision.ApprovalStatus = request.ApprovalStatus;
        decision.ValueJson = valueValidation.CanonicalJson!;
        decision.DecisionDate = EnsureUtc(request.DecisionDate);
        decision.EffectiveFrom = valueValidation.EffectiveFrom;
        decision.EffectiveTo = valueValidation.EffectiveTo;
        decision.ApprovalWorkflowInstanceId = request.ApprovalWorkflowInstanceId;
        decision.ApprovalReference = NullIfWhiteSpace(request.ApprovalReference);
        decision.SourceLineage = NullIfWhiteSpace(request.SourceLineage);
        decision.Notes = NullIfWhiteSpace(request.Notes);

        if (request.Status == ProcurementConfigurationDecisionStatus.Approved)
        {
            decision.ApprovedById = _currentUser.UserId;
            decision.ApprovedAt = DateTime.UtcNow;
        }
        else
        {
            decision.ApprovedById = null;
            decision.ApprovedAt = null;
        }

        SetModified(decision);
        await Decisions.UpdateAsync(decision);
        await AddRevisionAsync(
            profile.Id,
            decision.Id,
            returnToProposed ? "ReturnDecisionToProposed" : "UpdateDecision",
            "Succeeded",
            correlationId,
            request.Reason,
            before,
            DecisionSnapshot(decision));
        await SaveWithConcurrencyAsync(decision.DecisionKey, cancellationToken);

        var mappedProfile = await GetProfileAsync(profile.Id, cancellationToken);
        return mappedProfile.Decisions.Single(item => item.DecisionKey == definition.DecisionKey);
    }

    public async Task<ProcurementConfigurationValidationResultDto> ValidateProfileAsync(
        Guid id,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureEditor();
        var profile = await FindProfileAsync(id, tracked: false, cancellationToken);
        var validation = await BuildValidationAsync(profile, cancellationToken);
        await AddRevisionAsync(profile.Id, null, "Validate", validation.IsValid ? "Succeeded" : "Rejected", correlationId,
            validation.IsValid ? "Profile validation passed." : $"Profile validation returned {validation.Errors.Count} error(s).",
            null,
            new { validation.IsValid, ErrorCount = validation.Errors.Count, WarningCount = validation.Warnings.Count });
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return validation;
    }

    public async Task<ProcurementConfigurationProfileDto> PublishProfileAsync(
        Guid id,
        ProcurementConfigurationLifecycleRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureEditor();
        var profile = await FindProfileAsync(id, tracked: true, cancellationToken);
        if (!_currentUser.HasRole("SuperAdmin"))
        {
            await AddRevisionAsync(profile.Id, null, "Publish", "Rejected", correlationId,
                "Direct publish rejected: SuperAdmin privilege is required.", ProfileSnapshot(profile), null);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            throw new ProcurementConfigurationAuthorizationException("Only SuperAdmin may publish procurement configuration profiles.");
        }

        ProcurementConfigurationLifecyclePolicy.EnsureCanPublish(profile);
        EnsureRowVersion(profile.RowVersion, request.RowVersion, "profile");
        var validation = await BuildValidationAsync(profile, cancellationToken);
        if (!validation.IsValid)
        {
            await AddRevisionAsync(profile.Id, null, "Publish", "Rejected", correlationId,
                $"Publication blocked by {validation.Errors.Count} validation error(s).", ProfileSnapshot(profile), null);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            throw new ProcurementConfigurationValidationException("The profile is not ready to publish.", validation);
        }

        await _unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                var now = DateTime.UtcNow;
                var priorPublished = await Profiles.GetQueryable(item =>
                        item.TenantId == _currentUser.TenantId &&
                        item.ProfileKey == profile.ProfileKey &&
                        item.Id != profile.Id &&
                        item.LifecycleStatus == ProcurementConfigurationProfileStatus.Published)
                    .ToListAsync(cancellationToken);

                foreach (var prior in priorPublished)
                {
                    var priorBefore = ProfileSnapshot(prior);
                    prior.LifecycleStatus = ProcurementConfigurationProfileStatus.Retired;
                    prior.RetiredAt = now;
                    prior.RetiredById = _currentUser.UserId;
                    if (!prior.EffectiveTo.HasValue || prior.EffectiveTo.Value >= profile.EffectiveFrom)
                        prior.EffectiveTo = profile.EffectiveFrom.AddTicks(-1);
                    SetModified(prior);
                    await Profiles.UpdateAsync(prior);
                    await AddRevisionAsync(prior.Id, null, "RetireOnSupersede", "Succeeded", correlationId,
                        $"Retired atomically by publication of version {profile.Version}.", priorBefore, ProfileSnapshot(prior));
                }

                var before = ProfileSnapshot(profile);
                profile.LifecycleStatus = ProcurementConfigurationProfileStatus.Published;
                profile.PublishedAt = now;
                profile.PublishedById = _currentUser.UserId;
                profile.RetiredAt = null;
                profile.RetiredById = null;
                SetModified(profile);
                await Profiles.UpdateAsync(profile);
                await AddRevisionAsync(profile.Id, null, "Publish", "Succeeded", correlationId, request.Reason, before, ProfileSnapshot(profile));
                await _unitOfWork.CommitAsync(cancellationToken);
            }
            catch
            {
                try
                {
                    await _unitOfWork.RollbackAsync(cancellationToken);
                }
                catch (InvalidOperationException)
                {
                    // UnitOfWork.CommitAsync already rolls back and clears a failed transaction.
                }
                throw;
            }
        }, cancellationToken);

        return await GetProfileAsync(profile.Id, cancellationToken);
    }

    public async Task<ProcurementConfigurationProfileDto> RetireProfileAsync(
        Guid id,
        ProcurementConfigurationLifecycleRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureEditor();
        var profile = await FindProfileAsync(id, tracked: true, cancellationToken);
        await EnsurePublisherAsync(profile, "Retire", correlationId, cancellationToken);
        ProcurementConfigurationLifecyclePolicy.EnsureCanRetire(profile);
        EnsureRowVersion(profile.RowVersion, request.RowVersion, "profile");

        var before = ProfileSnapshot(profile);
        profile.LifecycleStatus = ProcurementConfigurationProfileStatus.Retired;
        profile.RetiredAt = DateTime.UtcNow;
        profile.RetiredById = _currentUser.UserId;
        profile.EffectiveTo ??= DateTime.UtcNow;
        SetModified(profile);
        await Profiles.UpdateAsync(profile);
        await AddRevisionAsync(profile.Id, null, "Retire", "Succeeded", correlationId, request.Reason, before, ProfileSnapshot(profile));
        await SaveWithConcurrencyAsync("profile", cancellationToken);
        return await GetProfileAsync(profile.Id, cancellationToken);
    }

    public async Task<ProcurementConfigurationProfileDto> CloneDraftAsync(
        Guid id,
        CloneProcurementConfigurationProfileRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureEditor();
        var source = await FindProfileAsync(id, tracked: false, cancellationToken);
        if (source.LifecycleStatus == ProcurementConfigurationProfileStatus.Draft)
            throw new ProcurementConfigurationConflictException("The selected profile is already a draft.");

        var versions = await Profiles.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId && item.ProfileKey == source.ProfileKey)
            .ToListAsync(cancellationToken);
        if (versions.Any(item => item.LifecycleStatus == ProcurementConfigurationProfileStatus.Draft))
            throw new ProcurementConfigurationConflictException("This profile family already has an editable draft.");

        var sourceDecisions = await Decisions.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId && item.ProfileId == source.Id)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var clone = new ProcurementConfigurationProfile
        {
            TenantId = source.TenantId,
            ProfileKey = source.ProfileKey,
            ProfileCode = source.ProfileCode,
            Name = source.Name,
            Version = ProcurementConfigurationLifecyclePolicy.GetNextVersion(versions),
            LifecycleStatus = ProcurementConfigurationProfileStatus.Draft,
            EffectiveFrom = source.EffectiveFrom,
            EffectiveTo = source.EffectiveTo,
            ChangeSummary = NullIfWhiteSpace(request.ChangeSummary) ?? $"Drafted from version {source.Version}",
            IsDefault = source.IsDefault,
            SupersedesProfileId = source.Id,
            CreatedAt = now,
            CreatedBy = _currentUser.FullName,
            CreatedById = _currentUser.UserId
        };
        await Profiles.AddAsync(clone);

        foreach (var definition in ProcurementConfigurationDecisionRegistry.Definitions)
        {
            var sourceDecision = sourceDecisions.SingleOrDefault(item => item.DecisionKey == definition.DecisionKey);
            var clonedDecision = NewDecision(clone, definition, now);
            if (sourceDecision is not null)
            {
                clonedDecision.SchemaVersion = sourceDecision.SchemaVersion;
                clonedDecision.OwnerGroup = sourceDecision.OwnerGroup;
                clonedDecision.Status = ProcurementConfigurationDecisionStatus.Proposed;
                clonedDecision.ApprovalStatus = definition.RequiresRenewedApproval
                    ? ProcurementConfigurationApprovalStatus.Pending
                    : sourceDecision.ApprovalStatus;
                clonedDecision.EvidenceStatus = definition.RequiresRenewedApproval
                    ? ProcurementConfigurationEvidenceStatus.Missing
                    : sourceDecision.EvidenceStatus;
                clonedDecision.ValueJson = sourceDecision.ValueJson;
                clonedDecision.DecisionDate = sourceDecision.DecisionDate;
                clonedDecision.EffectiveFrom = sourceDecision.EffectiveFrom;
                clonedDecision.EffectiveTo = sourceDecision.EffectiveTo;
                clonedDecision.SourceDecisionId = sourceDecision.Id;
                clonedDecision.SourceLineage = $"{source.ProfileCode}/v{source.Version}/{sourceDecision.DecisionKey}";
                clonedDecision.Notes = sourceDecision.Notes;
            }
            await Decisions.AddAsync(clonedDecision);
        }

        await AddRevisionAsync(source.Id, null, "CloneSource", "Succeeded", correlationId,
            $"Created draft version {clone.Version}.", ProfileSnapshot(source), null);
        await AddRevisionAsync(clone.Id, null, "CloneDraft", "Succeeded", correlationId, request.ChangeSummary, null, ProfileSnapshot(clone));
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetProfileAsync(clone.Id, cancellationToken);
    }

    public async Task DeleteDraftAsync(
        Guid id,
        ProcurementConfigurationLifecycleRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureEditor();
        var profile = await FindProfileAsync(id, tracked: true, cancellationToken);
        ProcurementConfigurationLifecyclePolicy.EnsureEditable(profile);
        EnsureRowVersion(profile.RowVersion, request.RowVersion, "profile");

        var decisions = await Decisions.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId && item.ProfileId == profile.Id)
            .ToListAsync(cancellationToken);
        var evidenceExists = await EvidenceLinks.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId && item.ProfileId == profile.Id)
            .AnyAsync(cancellationToken);
        if (evidenceExists || decisions.Any(item => item.ApprovalWorkflowInstanceId.HasValue))
            throw new ProcurementConfigurationConflictException(
                "A draft with evidence or workflow dependencies cannot be deleted. Unlink eligible evidence or preserve it for audit.");

        var before = ProfileSnapshot(profile);
        await Revisions.AddAsync(NewRevision(profile.Id, null, "DeleteDraft", "Succeeded", correlationId, request.Reason, before, null));
        foreach (var decision in decisions)
        {
            decision.IsDeleted = true;
            decision.DeletedAt = DateTime.UtcNow;
            decision.DeletedBy = _currentUser.FullName;
            await Decisions.UpdateAsync(decision);
        }
        profile.IsDeleted = true;
        profile.DeletedAt = DateTime.UtcNow;
        profile.DeletedBy = _currentUser.FullName;
        await Profiles.UpdateAsync(profile);
        await SaveWithConcurrencyAsync("profile", cancellationToken);
    }

    public async Task<ProcurementConfigurationEvidenceLinkDto> LinkEvidenceAsync(
        Guid profileId,
        string decisionKey,
        LinkProcurementConfigurationEvidenceRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureEditor();
        if (string.IsNullOrWhiteSpace(request.FilePath) && string.IsNullOrWhiteSpace(request.ExternalReference))
            throw ValidationException(decisionKey, new[] { "A shared uploaded file or external evidence reference is required." });

        var profile = await FindProfileAsync(profileId, tracked: true, cancellationToken);
        var decision = await FindDecisionAsync(profileId, decisionKey, tracked: true, cancellationToken);
        ProcurementConfigurationLifecyclePolicy.EnsureDecisionEditable(profile, decision);
        EnsureRowVersion(decision.RowVersion, request.DecisionRowVersion, decision.DecisionKey);

        FileUploadRecord? upload = null;
        if (!string.IsNullOrWhiteSpace(request.FilePath))
        {
            var filePath = request.FilePath.Trim();
            upload = await FileUploads.GetQueryable(item =>
                    item.TenantId == _currentUser.TenantId && item.FilePath == filePath)
                .FirstOrDefaultAsync(cancellationToken);
            if (upload is null)
                throw new ProcurementConfigurationNotFoundException("The shared uploaded file was not found in this tenant.");
            if (upload.VirusScanStatus is FileVirusScanStatus.Infected or FileVirusScanStatus.Error or FileVirusScanStatus.Pending)
                throw ValidationException(decision.DecisionKey, new[] { "The uploaded file has not passed the shared malware/file policy." });

            if (await EvidenceLinks.GetQueryable(item =>
                    item.TenantId == _currentUser.TenantId &&
                    item.DecisionId == decision.Id &&
                    item.FileUploadRecordId == upload.Id)
                .AnyAsync(cancellationToken))
                throw new ProcurementConfigurationConflictException("This uploaded file is already linked to the decision.");
        }

        var link = new ProcurementConfigurationEvidenceLink
        {
            TenantId = _currentUser.TenantId,
            ProfileId = profile.Id,
            DecisionId = decision.Id,
            FileUploadRecordId = upload?.Id,
            EvidenceType = request.EvidenceType.Trim(),
            ExternalReference = NullIfWhiteSpace(request.ExternalReference),
            Checksum = NullIfWhiteSpace(request.Checksum),
            ReferenceMetadataJson = NullIfWhiteSpace(request.ReferenceMetadataJson),
            UploadedById = _currentUser.UserId,
            UploadedAt = DateTime.UtcNow,
            CreatedBy = _currentUser.FullName,
            CreatedById = _currentUser.UserId
        };
        await EvidenceLinks.AddAsync(link);
        decision.EvidenceStatus = ProcurementConfigurationEvidenceStatus.Attached;
        SetModified(decision);
        await Decisions.UpdateAsync(decision);
        await AddRevisionAsync(profile.Id, decision.Id, "LinkEvidence", "Succeeded", correlationId, request.Reason, null,
            new { link.Id, link.EvidenceType, link.FileUploadRecordId, link.ExternalReference });
        await SaveWithConcurrencyAsync(decision.DecisionKey, cancellationToken);
        return MapEvidence(link, upload);
    }

    public async Task UnlinkEvidenceAsync(
        Guid profileId,
        string decisionKey,
        Guid evidenceId,
        string decisionRowVersion,
        string? reason,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureEditor();
        var profile = await FindProfileAsync(profileId, tracked: true, cancellationToken);
        var decision = await FindDecisionAsync(profileId, decisionKey, tracked: true, cancellationToken);
        ProcurementConfigurationLifecyclePolicy.EnsureDecisionEditable(profile, decision);
        EnsureRowVersion(decision.RowVersion, decisionRowVersion, decision.DecisionKey);
        var link = await EvidenceLinks.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.ProfileId == profile.Id &&
                item.DecisionId == decision.Id &&
                item.Id == evidenceId)
            .FirstOrDefaultAsync(cancellationToken);
        if (link is null)
            throw new ProcurementConfigurationNotFoundException("Evidence link was not found.");

        var before = new { link.Id, link.EvidenceType, link.FileUploadRecordId, link.ExternalReference };
        link.IsDeleted = true;
        link.DeletedAt = DateTime.UtcNow;
        link.DeletedBy = _currentUser.FullName;
        await EvidenceLinks.UpdateAsync(link);
        var remaining = await EvidenceLinks.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId && item.DecisionId == decision.Id && item.Id != link.Id)
            .AnyAsync(cancellationToken);
        decision.EvidenceStatus = remaining
            ? ProcurementConfigurationEvidenceStatus.Attached
            : ProcurementConfigurationEvidenceStatus.Missing;
        SetModified(decision);
        await Decisions.UpdateAsync(decision);
        await AddRevisionAsync(profile.Id, decision.Id, "UnlinkEvidence", "Succeeded", correlationId, reason, before, null);
        await SaveWithConcurrencyAsync(decision.DecisionKey, cancellationToken);
    }

    public async Task<IReadOnlyList<ProcurementConfigurationRevisionDto>> GetHistoryAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        await FindProfileAsync(id, tracked: false, cancellationToken);
        var revisions = await Revisions.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId && item.ProfileId == id)
            .AsNoTracking()
            .OrderByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);
        return revisions.Select(MapRevision).ToList();
    }

    private async Task<ProcurementConfigurationValidationResultDto> BuildValidationAsync(
        ProcurementConfigurationProfile profile,
        CancellationToken cancellationToken)
    {
        var errors = new List<ProcurementConfigurationValidationIssueDto>();
        var warnings = new List<ProcurementConfigurationValidationIssueDto>();
        if (string.IsNullOrWhiteSpace(profile.Name)) AddError(errors, "PROFILE_NAME", "Profile name is required.");
        if (profile.EffectiveTo.HasValue && profile.EffectiveTo.Value < profile.EffectiveFrom)
            AddError(errors, "PROFILE_DATES", "Profile effective-to date cannot be before effective-from date.");

        var decisions = await Decisions.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId && item.ProfileId == profile.Id)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var evidence = await EvidenceLinks.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId && item.ProfileId == profile.Id)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var uploadIds = evidence.Where(item => item.FileUploadRecordId.HasValue)
            .Select(item => item.FileUploadRecordId!.Value)
            .Distinct()
            .ToList();
        var uploads = uploadIds.Count == 0
            ? new List<FileUploadRecord>()
            : await FileUploads.GetQueryable(item =>
                    item.TenantId == _currentUser.TenantId && uploadIds.Contains(item.Id))
                .AsNoTracking()
                .ToListAsync(cancellationToken);

        foreach (var definition in ProcurementConfigurationDecisionRegistry.Definitions)
        {
            var matching = decisions.Where(item => item.DecisionKey == definition.DecisionKey).ToList();
            if (matching.Count != 1)
            {
                AddError(errors, "DECISION_CARDINALITY", $"{definition.DecisionKey} must occur exactly once; found {matching.Count}.", definition.DecisionKey);
                continue;
            }

            var decision = matching[0];
            if (string.IsNullOrWhiteSpace(decision.OwnerGroup))
                AddError(errors, "OWNER_REQUIRED", "A decision owner group is required.", definition.DecisionKey);
            if (!decision.DecisionDate.HasValue)
                AddError(errors, "DECISION_DATE_REQUIRED", "Decision date is required before publication.", definition.DecisionKey);
            if (decision.Status != ProcurementConfigurationDecisionStatus.Approved)
                AddError(errors, "DECISION_NOT_APPROVED", "Decision status must be Approved before publication.", definition.DecisionKey);
            if (definition.RequiresApproval && decision.ApprovalStatus != ProcurementConfigurationApprovalStatus.Approved)
                AddError(errors, "APPROVAL_REQUIRED", "Approved decision evidence is required before publication.", definition.DecisionKey);
            if (definition.RequiresApproval && !decision.ApprovalWorkflowInstanceId.HasValue && string.IsNullOrWhiteSpace(decision.ApprovalReference))
                AddError(errors, "APPROVAL_REFERENCE_REQUIRED", "A shared workflow instance or approval reference is required.", definition.DecisionKey);

            JsonElement value;
            try { value = ProcurementConfigurationDecisionRegistry.ParseValue(decision.ValueJson); }
            catch (JsonException exception)
            {
                AddError(errors, "VALUE_JSON_INVALID", exception.Message, definition.DecisionKey);
                continue;
            }

            var valueResult = ProcurementConfigurationDecisionRegistry.Validate(definition.DecisionKey, decision.SchemaVersion, value);
            foreach (var valueError in valueResult.Errors)
                AddError(errors, "VALUE_INVALID", valueError, definition.DecisionKey);
            if (valueResult.IsValid)
            {
                if (valueResult.EffectiveFrom < profile.EffectiveFrom ||
                    (profile.EffectiveTo.HasValue &&
                     (!valueResult.EffectiveTo.HasValue || valueResult.EffectiveTo.Value > profile.EffectiveTo.Value)))
                    AddError(errors, "EFFECTIVE_PERIOD_OUTSIDE_PROFILE", "Decision effective period must be contained by the profile effective period.", definition.DecisionKey);
            }

            var decisionEvidence = evidence.Where(item => item.DecisionId == decision.Id).ToList();
            if (definition.RequiresEvidence && decisionEvidence.Count == 0)
                AddError(errors, "EVIDENCE_REQUIRED", "At least one evidence link is required before publication.", definition.DecisionKey);
            foreach (var link in decisionEvidence)
            {
                if (link.FileUploadRecordId.HasValue)
                {
                    var upload = uploads.SingleOrDefault(item => item.Id == link.FileUploadRecordId.Value);
                    if (upload is null)
                        AddError(errors, "EVIDENCE_FILE_MISSING", "Linked shared upload record was not found.", definition.DecisionKey);
                    else if (upload.VirusScanStatus is FileVirusScanStatus.Pending or FileVirusScanStatus.Infected or FileVirusScanStatus.Error)
                        AddError(errors, "EVIDENCE_FILE_UNSAFE", "Linked evidence has not passed the shared malware/file policy.", definition.DecisionKey);
                }
                else if (string.IsNullOrWhiteSpace(link.ExternalReference))
                {
                    AddError(errors, "EVIDENCE_REFERENCE_MISSING", "Evidence link has neither a shared uploaded file nor an external reference.", definition.DecisionKey);
                }
            }
        }

        foreach (var unknown in decisions.Where(item => !ProcurementConfigurationDecisionRegistry.TryGet(item.DecisionKey, out _)))
            AddError(errors, "UNKNOWN_DECISION", $"Unknown decision key {unknown.DecisionKey} is not publishable.", unknown.DecisionKey);

        var familyVersions = await Profiles.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.ProfileKey == profile.ProfileKey &&
                item.Id != profile.Id)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        if (familyVersions.Any(item => item.Version == profile.Version))
            AddError(errors, "VERSION_DUPLICATE", "Profile version must be unique within its tenant and family.");
        if (familyVersions.Any(item => item.LifecycleStatus == ProcurementConfigurationProfileStatus.Published &&
            PeriodsOverlap(profile.EffectiveFrom, profile.EffectiveTo, item.EffectiveFrom, item.EffectiveTo)))
            warnings.Add(new ProcurementConfigurationValidationIssueDto
            {
                Code = "PUBLISHED_PERIOD_SUPERSEDED",
                Message = "Publishing will atomically retire the currently published overlapping version.",
                Severity = "Warning"
            });

        return new ProcurementConfigurationValidationResultDto { Errors = errors, Warnings = warnings };
    }

    private async Task<ProcurementConfigurationProfileDto> MapProfileAsync(
        ProcurementConfigurationProfile profile,
        CancellationToken cancellationToken)
    {
        var decisions = await Decisions.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId && item.ProfileId == profile.Id)
            .AsNoTracking()
            .OrderBy(item => item.DecisionKey)
            .ToListAsync(cancellationToken);
        var evidence = await EvidenceLinks.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId && item.ProfileId == profile.Id)
            .AsNoTracking()
            .OrderByDescending(item => item.UploadedAt)
            .ToListAsync(cancellationToken);
        var uploadIds = evidence.Where(item => item.FileUploadRecordId.HasValue).Select(item => item.FileUploadRecordId!.Value).Distinct().ToList();
        var uploads = uploadIds.Count == 0
            ? new List<FileUploadRecord>()
            : await FileUploads.GetQueryable(item => item.TenantId == _currentUser.TenantId && uploadIds.Contains(item.Id))
                .AsNoTracking().ToListAsync(cancellationToken);
        var validation = await BuildValidationAsync(profile, cancellationToken);
        var history = await Revisions.GetQueryable(item => item.TenantId == _currentUser.TenantId && item.ProfileId == profile.Id)
            .AsNoTracking().OrderByDescending(item => item.CreatedAt).Take(20).ToListAsync(cancellationToken);
        var summary = MapSummary(profile, decisions);

        return new ProcurementConfigurationProfileDto
        {
            Id = summary.Id,
            ProfileKey = summary.ProfileKey,
            ProfileCode = summary.ProfileCode,
            Name = summary.Name,
            Version = summary.Version,
            LifecycleStatus = summary.LifecycleStatus,
            EffectiveFrom = summary.EffectiveFrom,
            EffectiveTo = summary.EffectiveTo,
            IsDefault = summary.IsDefault,
            CompleteDecisionCount = summary.CompleteDecisionCount,
            TotalDecisionCount = summary.TotalDecisionCount,
            UpdatedBy = summary.UpdatedBy,
            UpdatedAt = summary.UpdatedAt,
            RowVersion = summary.RowVersion,
            ChangeSummary = profile.ChangeSummary,
            SupersedesProfileId = profile.SupersedesProfileId,
            PublishedAt = profile.PublishedAt,
            PublishedById = profile.PublishedById,
            RetiredAt = profile.RetiredAt,
            RetiredById = profile.RetiredById,
            Decisions = decisions.Select(decision => MapDecision(
                decision,
                evidence.Where(item => item.DecisionId == decision.Id),
                uploads)).ToList(),
            Validation = validation,
            RecentHistory = history.Select(MapRevision).ToList()
        };
    }

    private ProcurementConfigurationProfileSummaryDto MapSummary(
        ProcurementConfigurationProfile profile,
        IEnumerable<ProcurementConfigurationDecision> decisions)
    {
        var rows = decisions.ToList();
        return new ProcurementConfigurationProfileSummaryDto
        {
            Id = profile.Id,
            ProfileKey = profile.ProfileKey,
            ProfileCode = profile.ProfileCode,
            Name = profile.Name,
            Version = profile.Version,
            LifecycleStatus = profile.LifecycleStatus,
            EffectiveFrom = profile.EffectiveFrom,
            EffectiveTo = profile.EffectiveTo,
            IsDefault = profile.IsDefault,
            CompleteDecisionCount = rows.Count(IsDecisionComplete),
            TotalDecisionCount = ProcurementConfigurationDecisionRegistry.Definitions.Count,
            UpdatedBy = profile.UpdatedBy ?? profile.CreatedBy,
            UpdatedAt = profile.UpdatedAt ?? profile.CreatedAt,
            RowVersion = EncodeRowVersion(profile.RowVersion)
        };
    }

    private ProcurementConfigurationDecisionDto MapDecision(
        ProcurementConfigurationDecision decision,
        IEnumerable<ProcurementConfigurationEvidenceLink> evidence,
        IEnumerable<FileUploadRecord> uploads)
    {
        var definition = ProcurementConfigurationDecisionRegistry.GetRequired(decision.DecisionKey);
        var uploadRows = uploads.ToDictionary(item => item.Id);
        return new ProcurementConfigurationDecisionDto
        {
            Id = decision.Id,
            DecisionKey = decision.DecisionKey,
            DisplayName = definition.DisplayName,
            Description = definition.Description,
            OwnerGroup = decision.OwnerGroup,
            SchemaVersion = decision.SchemaVersion,
            Status = decision.Status,
            ApprovalStatus = decision.ApprovalStatus,
            EvidenceStatus = decision.EvidenceStatus,
            Value = ProcurementConfigurationDecisionRegistry.ParseValue(decision.ValueJson),
            DecisionDate = decision.DecisionDate,
            EffectiveFrom = decision.EffectiveFrom,
            EffectiveTo = decision.EffectiveTo,
            ApprovedById = decision.ApprovedById,
            ApprovedAt = decision.ApprovedAt,
            ApprovalWorkflowInstanceId = decision.ApprovalWorkflowInstanceId,
            ApprovalReference = decision.ApprovalReference,
            SourceLineage = decision.SourceLineage,
            Notes = decision.Notes,
            RequiresApproval = definition.RequiresApproval,
            RequiresEvidence = definition.RequiresEvidence,
            IsComplete = IsDecisionComplete(decision),
            RowVersion = EncodeRowVersion(decision.RowVersion),
            Evidence = evidence.Select(link => MapEvidence(link,
                link.FileUploadRecordId.HasValue && uploadRows.TryGetValue(link.FileUploadRecordId.Value, out var upload) ? upload : null)).ToList()
        };
    }

    private static ProcurementConfigurationEvidenceLinkDto MapEvidence(
        ProcurementConfigurationEvidenceLink link,
        FileUploadRecord? upload) => new()
    {
        Id = link.Id,
        DecisionId = link.DecisionId,
        EvidenceType = link.EvidenceType,
        FileUploadRecordId = link.FileUploadRecordId,
        FilePath = upload?.FilePath,
        OriginalFileName = upload?.OriginalFileName,
        ContentType = upload?.ContentType,
        FileSize = upload?.FileSize,
        VirusScanStatus = upload?.VirusScanStatus,
        ExternalReference = link.ExternalReference,
        Checksum = link.Checksum,
        UploadedAt = link.UploadedAt,
        UploadedById = link.UploadedById
    };

    private static ProcurementConfigurationRevisionDto MapRevision(ProcurementConfigurationRevision revision) => new()
    {
        Id = revision.Id,
        ProfileId = revision.ProfileId,
        DecisionId = revision.DecisionId,
        Action = revision.Action,
        Result = revision.Result,
        CorrelationId = revision.CorrelationId,
        ActorUserId = revision.ActorUserId,
        ActorName = revision.ActorName,
        ActorRoles = revision.ActorRoles,
        Reason = revision.Reason,
        Before = ParseOptionalJson(revision.BeforeJson),
        After = ParseOptionalJson(revision.AfterJson),
        Timestamp = revision.CreatedAt
    };

    private async Task<ProcurementConfigurationProfile> FindProfileAsync(
        Guid id,
        bool tracked,
        CancellationToken cancellationToken)
    {
        var query = Profiles.GetQueryable(item => item.TenantId == _currentUser.TenantId && item.Id == id);
        if (!tracked) query = query.AsNoTracking();
        return await query.FirstOrDefaultAsync(cancellationToken)
               ?? throw new ProcurementConfigurationNotFoundException("Procurement configuration profile was not found.");
    }

    private async Task<ProcurementConfigurationDecision> FindDecisionAsync(
        Guid profileId,
        string decisionKey,
        bool tracked,
        CancellationToken cancellationToken)
    {
        var key = ProcurementConfigurationDecisionRegistry.GetRequired(decisionKey).DecisionKey;
        var query = Decisions.GetQueryable(item =>
            item.TenantId == _currentUser.TenantId && item.ProfileId == profileId && item.DecisionKey == key);
        if (!tracked) query = query.AsNoTracking();
        return await query.FirstOrDefaultAsync(cancellationToken)
               ?? throw new ProcurementConfigurationNotFoundException($"{key} was not found in this profile.");
    }

    private async Task EnsurePublisherAsync(
        ProcurementConfigurationProfile profile,
        string action,
        string correlationId,
        CancellationToken cancellationToken)
    {
        if (_currentUser.HasRole("SuperAdmin")) return;
        await AddRevisionAsync(profile.Id, null, action, "Rejected", correlationId,
            $"Direct {action.ToLowerInvariant()} rejected: SuperAdmin privilege is required.", ProfileSnapshot(profile), null);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        throw new ProcurementConfigurationAuthorizationException($"Only SuperAdmin may {action.ToLowerInvariant()} procurement configuration profiles.");
    }

    private void EnsureAuthenticatedTenant()
    {
        if (!_currentUser.IsAuthenticated || _currentUser.TenantId == Guid.Empty || _currentUser.UserId == Guid.Empty)
            throw new ProcurementConfigurationAuthorizationException("An authenticated tenant and user context is required.");
    }

    private void EnsureEditor()
    {
        EnsureAuthenticatedTenant();
        if (!_currentUser.HasRole("SuperAdmin") && !_currentUser.HasRole("TenantAdmin"))
            throw new ProcurementConfigurationAuthorizationException("Procurement configuration administration requires SuperAdmin or TenantAdmin.");
    }

    private static void ValidateDecisionState(
        SaveProcurementConfigurationDecisionRequest request,
        string decisionKey)
    {
        if (string.IsNullOrWhiteSpace(request.OwnerGroup))
            throw ValidationException(decisionKey, new[] { "Owner group is required." });
        if (request.Status == ProcurementConfigurationDecisionStatus.Approved &&
            request.ApprovalStatus != ProcurementConfigurationApprovalStatus.Approved)
            throw ValidationException(decisionKey, new[] { "Approved decision status requires Approved approval status." });
        if (request.ApprovalStatus == ProcurementConfigurationApprovalStatus.Approved &&
            request.Status != ProcurementConfigurationDecisionStatus.Approved)
            throw ValidationException(decisionKey, new[] { "Approved approval status requires Approved decision status." });
        if (request.Status == ProcurementConfigurationDecisionStatus.Approved && !request.DecisionDate.HasValue)
            throw ValidationException(decisionKey, new[] { "Decision date is required for approval." });
        if (request.Status == ProcurementConfigurationDecisionStatus.Approved &&
            !request.ApprovalWorkflowInstanceId.HasValue && string.IsNullOrWhiteSpace(request.ApprovalReference))
            throw ValidationException(decisionKey, new[] { "A shared workflow instance or approval reference is required for approval." });
    }

    private static void ValidateProfileDates(DateTime from, DateTime? to)
    {
        if (from == default)
            throw ValidationException(null, new[] { "Profile effective-from date is required." });
        if (to.HasValue && to.Value < from)
            throw ValidationException(null, new[] { "Profile effective-to date cannot be before effective-from date." });
    }

    private async Task SaveWithConcurrencyAsync(string target, CancellationToken cancellationToken)
    {
        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            _logger.LogWarning(exception, "Stale row version rejected for procurement configuration {Target}", target);
            throw new ProcurementConfigurationConflictException(
                $"The {target} was changed by another user. Reload the latest version before saving.");
        }
        catch (DbUpdateException exception) when (IsUniqueConstraint(exception))
        {
            _logger.LogWarning(exception, "Unique lifecycle constraint rejected for procurement configuration {Target}", target);
            throw new ProcurementConfigurationConflictException(
                "The requested profile version or published lifecycle state conflicts with another saved version.");
        }
    }

    private static bool IsUniqueConstraint(DbUpdateException exception) =>
        exception.InnerException?.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) == true ||
        exception.InnerException?.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) == true;

    private static void EnsureRowVersion(byte[] current, string supplied, string target)
    {
        byte[] expected;
        try { expected = Convert.FromBase64String(supplied); }
        catch (FormatException)
        {
            throw ValidationException(null, new[] { $"A valid row version is required for {target}." });
        }

        if (!current.SequenceEqual(expected))
            throw new ProcurementConfigurationConflictException(
                $"The {target} was changed by another user. Reload the latest version before saving.");
    }

    private Task AddRevisionAsync(
        Guid profileId,
        Guid? decisionId,
        string action,
        string result,
        string correlationId,
        string? reason,
        object? before,
        object? after) => Revisions.AddAsync(NewRevision(profileId, decisionId, action, result, correlationId, reason, before, after));

    private ProcurementConfigurationRevision NewRevision(
        Guid profileId,
        Guid? decisionId,
        string action,
        string result,
        string correlationId,
        string? reason,
        object? before,
        object? after) => new()
    {
        TenantId = _currentUser.TenantId,
        ProfileId = profileId,
        DecisionId = decisionId,
        Action = action,
        Result = result,
        CorrelationId = string.IsNullOrWhiteSpace(correlationId) ? Guid.NewGuid().ToString("N") : correlationId[..Math.Min(100, correlationId.Length)],
        ActorUserId = _currentUser.UserId,
        ActorName = _currentUser.FullName,
        ActorRoles = string.Join(",", _currentUser.Roles.OrderBy(item => item, StringComparer.OrdinalIgnoreCase)),
        Reason = NullIfWhiteSpace(reason),
        BeforeJson = before is null ? null : JsonSerializer.Serialize(before, AuditJsonOptions),
        AfterJson = after is null ? null : JsonSerializer.Serialize(after, AuditJsonOptions),
        CreatedAt = DateTime.UtcNow,
        CreatedBy = _currentUser.FullName,
        CreatedById = _currentUser.UserId
    };

    private ProcurementConfigurationDecision NewDecision(
        ProcurementConfigurationProfile profile,
        ProcurementDecisionSchemaDefinition definition,
        DateTime now) => new()
    {
        TenantId = profile.TenantId,
        ProfileId = profile.Id,
        DecisionKey = definition.DecisionKey,
        SchemaVersion = definition.SchemaVersion,
        OwnerGroup = definition.OwnerGroup,
        Status = ProcurementConfigurationDecisionStatus.Draft,
        ApprovalStatus = ProcurementConfigurationApprovalStatus.Pending,
        EvidenceStatus = ProcurementConfigurationEvidenceStatus.Missing,
        ValueJson = "{}",
        CreatedAt = now,
        CreatedBy = _currentUser.FullName,
        CreatedById = _currentUser.UserId
    };

    private static object ProfileSnapshot(ProcurementConfigurationProfile profile) => new
    {
        profile.Id,
        profile.ProfileKey,
        profile.ProfileCode,
        profile.Name,
        profile.Version,
        profile.LifecycleStatus,
        profile.EffectiveFrom,
        profile.EffectiveTo,
        profile.ChangeSummary,
        profile.IsDefault,
        profile.SupersedesProfileId,
        profile.PublishedAt,
        profile.PublishedById,
        profile.RetiredAt,
        profile.RetiredById
    };

    private static object DecisionSnapshot(ProcurementConfigurationDecision decision) => new
    {
        decision.Id,
        decision.ProfileId,
        decision.DecisionKey,
        decision.SchemaVersion,
        decision.OwnerGroup,
        decision.Status,
        decision.ApprovalStatus,
        decision.EvidenceStatus,
        Value = ProcurementConfigurationDecisionRegistry.ParseValue(decision.ValueJson),
        decision.DecisionDate,
        decision.EffectiveFrom,
        decision.EffectiveTo,
        decision.ApprovedById,
        decision.ApprovedAt,
        decision.ApprovalWorkflowInstanceId,
        decision.ApprovalReference,
        decision.SourceLineage,
        decision.SourceDecisionId,
        decision.Notes
    };

    private static bool IsDecisionComplete(ProcurementConfigurationDecision decision)
    {
        if (!ProcurementConfigurationDecisionRegistry.TryGet(decision.DecisionKey, out var definition)) return false;
        return decision.Status == ProcurementConfigurationDecisionStatus.Approved &&
               (!definition.RequiresApproval || decision.ApprovalStatus == ProcurementConfigurationApprovalStatus.Approved) &&
               (!definition.RequiresEvidence || decision.EvidenceStatus != ProcurementConfigurationEvidenceStatus.Missing);
    }

    private static ProcurementConfigurationValidationException ValidationException(
        string? decisionKey,
        IEnumerable<string> errors) => new(
        "Procurement configuration validation failed.",
        new ProcurementConfigurationValidationResultDto
        {
            Errors = errors.Select(message => new ProcurementConfigurationValidationIssueDto
            {
                Code = "VALIDATION_ERROR",
                DecisionKey = decisionKey,
                Message = message
            }).ToList()
        });

    private static void AddError(
        ICollection<ProcurementConfigurationValidationIssueDto> errors,
        string code,
        string message,
        string? decisionKey = null) => errors.Add(new ProcurementConfigurationValidationIssueDto
    {
        Code = code,
        Message = message,
        DecisionKey = decisionKey
    });

    private void SetModified(BaseEntity entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = _currentUser.FullName;
        entity.LastModifiedById = _currentUser.UserId;
    }

    private static string NormalizeProfileCode(string value) => value.Trim().ToUpperInvariant();
    private static string? NullIfWhiteSpace(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static DateTime EnsureUtc(DateTime value) => value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);
    private static DateTime? EnsureUtc(DateTime? value) => value.HasValue ? EnsureUtc(value.Value) : null;
    private static string EncodeRowVersion(byte[] value) => Convert.ToBase64String(value ?? Array.Empty<byte>());

    private static bool PeriodsOverlap(DateTime leftFrom, DateTime? leftTo, DateTime rightFrom, DateTime? rightTo) =>
        leftFrom <= (rightTo ?? DateTime.MaxValue) && rightFrom <= (leftTo ?? DateTime.MaxValue);

    private static JsonElement? ParseOptionalJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
