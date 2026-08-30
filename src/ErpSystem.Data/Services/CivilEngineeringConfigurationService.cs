using System.Text.Json;
using System.Text.Json.Serialization;
using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Services.DocumentManagement;
using ErpSystem.Core.Services.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Data.Services;

public sealed class CivilEngineeringConfigurationService : ICivilEngineeringConfigurationService
{
    public const string ProfileCode = "TDC-CIVIL-ENGINEERING";
    private static readonly HashSet<string> AllowedEvidenceTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Policy", "Committee minute", "Technical standard", "Contract clause", "Approval memorandum"
    };
    private static readonly JsonSerializerOptions AuditJson = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
    private static readonly JsonSerializerOptions ConfigurationJson = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserProvider _user;
    private readonly ILogger<CivilEngineeringConfigurationService> _logger;

    public CivilEngineeringConfigurationService(ApplicationDbContext db, ICurrentUserProvider user, ILogger<CivilEngineeringConfigurationService> logger)
    {
        _db = db;
        _user = user;
        _logger = logger;
    }

    public IReadOnlyList<CivilEngineeringDecisionSchemaDto> GetSchemas() => CivilEngineeringConfigurationDecisionRegistry.ToDtos();

    public async Task<CivilEngineeringLookupsDto> GetLookupsAsync(CancellationToken cancellationToken = default)
    {
        EnsureTenant();
        var roles = await _db.Roles.AsNoTracking().Where(x => x.Name != null).OrderBy(x => x.Name)
            .Select(x => new CivilEngineeringLookupOptionDto { Value = x.Id.ToString(), Label = x.Name!, Group = "Security role" }).ToListAsync(cancellationToken);
        var projectTypes = await _db.ProjectTypes.AsNoTracking().Where(x => x.TenantId == _user.TenantId && !x.IsDeleted && x.IsActive).OrderBy(x => x.Name)
            .Select(x => new CivilEngineeringLookupOptionDto { Value = x.Id.ToString(), Label = x.Code + " - " + x.Name }).ToListAsync(cancellationToken);
        var currencies = await _db.Currencies.AsNoTracking().Where(x => x.TenantId == _user.TenantId && !x.IsDeleted && x.IsActive).OrderBy(x => x.DisplayOrder).ThenBy(x => x.CurrencyCode)
            .Select(x => new CivilEngineeringLookupOptionDto { Value = x.CurrencyCode, Label = x.CurrencyCode + " - " + x.CurrencyName }).ToListAsync(cancellationToken);
        var workflows = await _db.WorkflowDefinitions.AsNoTracking().Where(x => x.TenantId == _user.TenantId && !x.IsDeleted && x.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Published && x.IsActive && x.EntityType.IsActive && !x.EntityType.IsDeleted).OrderBy(x => x.Name).ThenByDescending(x => x.Version)
            .Select(x => new CivilEngineeringLookupOptionDto { Value = x.Id.ToString(), Label = x.Name + " v" + x.Version, Group = x.EntityType.Code }).ToListAsync(cancellationToken);
        var reports = await _db.Reports.AsNoTracking().Where(x => x.TenantId == _user.TenantId && !x.IsDeleted && (x.Status == "published" || x.Status == "Published")).OrderBy(x => x.Name)
            .Select(x => new CivilEngineeringLookupOptionDto { Value = x.Id.ToString(), Label = x.Name, Group = x.Type }).ToListAsync(cancellationToken);
        var dmsTemplates = await _db.CentralDocumentMetadataTemplates.AsNoTracking().Where(x => x.TenantId == _user.TenantId && !x.IsDeleted && x.IsActive && x.PublishedAt.HasValue).OrderBy(x => x.DocumentType)
            .Select(x => new CivilEngineeringLookupOptionDto { Value = x.Id.ToString(), Label = x.TemplateCode + " - " + x.DocumentType, Group = x.Module }).ToListAsync(cancellationToken);
        var projectCatalogEntries = await _db.ProjectCatalogEntries.AsNoTracking()
            .Where(x => x.TenantId == _user.TenantId && !x.IsDeleted && x.IsActive)
            .OrderBy(x => x.CatalogType).ThenBy(x => x.SortOrder).ThenBy(x => x.Name)
            .Select(x => new CivilEngineeringLookupOptionDto { Value = x.Id.ToString(), Label = x.Code + " - " + x.Name, Group = x.CatalogType })
            .ToListAsync(cancellationToken);
        return new CivilEngineeringLookupsDto { Sources = new Dictionary<string, IReadOnlyList<CivilEngineeringLookupOptionDto>>(StringComparer.OrdinalIgnoreCase)
        {
            ["roles"] = roles, ["projectTypes"] = projectTypes, ["currencies"] = currencies,
            ["workflows"] = workflows, ["reports"] = reports, ["dmsTemplates"] = dmsTemplates,
            ["projectCatalogEntries"] = projectCatalogEntries
        }};
    }

    public async Task<CivilEngineeringPagedResult<CivilEngineeringProfileSummaryDto>> GetProfilesAsync(CivilEngineeringProfileListRequest request, CancellationToken cancellationToken = default)
    {
        EnsureTenant();
        var page = Math.Max(1, request.Page); var size = Math.Clamp(request.PageSize, 1, 100);
        var query = _db.CivilEngineeringConfigurationProfiles.AsNoTracking().Where(x => x.TenantId == _user.TenantId && !x.IsDeleted);
        if (request.Status.HasValue) query = query.Where(x => x.LifecycleStatus == request.Status);
        if (!string.IsNullOrWhiteSpace(request.Search)) { var search = request.Search.Trim(); query = query.Where(x => x.Name.Contains(search) || x.ProfileCode.Contains(search)); }
        var total = await query.CountAsync(cancellationToken);
        var profiles = await query.OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt).ThenByDescending(x => x.Version).Skip((page - 1) * size).Take(size).ToListAsync(cancellationToken);
        var ids = profiles.Select(x => x.Id).ToList();
        var decisions = await _db.CivilEngineeringConfigurationDecisions.AsNoTracking().Where(x => x.TenantId == _user.TenantId && ids.Contains(x.ProfileId) && !x.IsDeleted).ToListAsync(cancellationToken);
        return new CivilEngineeringPagedResult<CivilEngineeringProfileSummaryDto> { Items = profiles.Select(x => MapSummary(x, decisions.Where(d => d.ProfileId == x.Id))).ToList(), Page = page, PageSize = size, TotalCount = total };
    }

    public async Task<CivilEngineeringProfileDto> GetProfileAsync(Guid id, CancellationToken cancellationToken = default)
    {
        EnsureTenant();
        var profile = await FindProfileAsync(id, false, cancellationToken);
        return await MapProfileAsync(profile, cancellationToken);
    }

    public async Task<CivilEngineeringProfileDto?> GetEffectiveProfileAsync(DateTime atUtc, CancellationToken cancellationToken = default)
    {
        EnsureTenant(); var at = Utc(atUtc);
        var profile = await _db.CivilEngineeringConfigurationProfiles.AsNoTracking().Where(x => x.TenantId == _user.TenantId && !x.IsDeleted && x.ProfileCode == ProfileCode && x.LifecycleStatus == CivilEngineeringConfigurationProfileStatus.Published && x.EffectiveFrom <= at && (!x.EffectiveTo.HasValue || x.EffectiveTo >= at)).OrderByDescending(x => x.Version).FirstOrDefaultAsync(cancellationToken);
        return profile is null ? null : await MapProfileAsync(profile, cancellationToken);
    }

    public async Task<CivilEngineeringProfileDto> CreateProfileAsync(CreateCivilEngineeringProfileRequest request, string correlationId, CancellationToken cancellationToken = default)
    {
        EnsureTenant(); ValidateDates(request.EffectiveFrom, request.EffectiveTo);
        var family = await _db.CivilEngineeringConfigurationProfiles
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == _user.TenantId && x.ProfileCode == ProfileCode)
            .OrderByDescending(x => x.Version)
            .ToListAsync(cancellationToken);
        if (family.Any(x => !x.IsDeleted))
            throw new CivilEngineeringConfigurationConflictException("The tenant already has a Civil Engineering configuration family. Clone its latest version instead.");
        var predecessor = family.FirstOrDefault();
        var now = DateTime.UtcNow;
        var profile = new CivilEngineeringConfigurationProfile { TenantId = _user.TenantId, ProfileKey = predecessor?.ProfileKey ?? Guid.NewGuid(), ProfileCode = ProfileCode, Name = request.Name.Trim(), Version = CivilEngineeringConfigurationLifecyclePolicy.NextVersion(family), EffectiveFrom = Utc(request.EffectiveFrom), EffectiveTo = Utc(request.EffectiveTo), ChangeSummary = Clean(request.ChangeSummary), IsDefault = request.IsDefault, SupersedesProfileId = predecessor?.Id, CreatedAt = now, CreatedBy = _user.FullName, CreatedById = _user.UserId };
        _db.CivilEngineeringConfigurationProfiles.Add(profile);
        foreach (var definition in CivilEngineeringConfigurationDecisionRegistry.Definitions)
            _db.CivilEngineeringConfigurationDecisions.Add(NewDecision(profile, definition, now));
        AddRevision(profile.Id, null, CivilEngineeringAuditEventMap.CreateProfile, correlationId, request.ChangeSummary, null, Snapshot(profile));
        await SaveAsync(cancellationToken);
        return await GetProfileAsync(profile.Id, cancellationToken);
    }

    public async Task<CivilEngineeringProfileDto> UpdateProfileAsync(Guid id, UpdateCivilEngineeringProfileRequest request, string correlationId, CancellationToken cancellationToken = default)
    {
        EnsureTenant(); ValidateDates(request.EffectiveFrom, request.EffectiveTo);
        var profile = await FindProfileAsync(id, true, cancellationToken); CivilEngineeringConfigurationLifecyclePolicy.EnsureEditable(profile); CheckVersion(profile.RowVersion, request.RowVersion, "profile");
        var before = Snapshot(profile); profile.Name = request.Name.Trim(); profile.EffectiveFrom = Utc(request.EffectiveFrom); profile.EffectiveTo = Utc(request.EffectiveTo); profile.ChangeSummary = Clean(request.ChangeSummary); profile.IsDefault = request.IsDefault; Touch(profile);
        AddRevision(profile.Id, null, CivilEngineeringAuditEventMap.UpdateProfile, correlationId, request.Reason, before, Snapshot(profile)); await SaveAsync(cancellationToken); return await GetProfileAsync(id, cancellationToken);
    }

    public async Task<CivilEngineeringDecisionDto> SaveDecisionAsync(Guid profileId, string key, SaveCivilEngineeringDecisionRequest request, string correlationId, CancellationToken cancellationToken = default)
    {
        EnsureTenant(); var profile = await FindProfileAsync(profileId, true, cancellationToken); var decision = await FindDecisionAsync(profileId, key, true, cancellationToken);
        CivilEngineeringConfigurationLifecyclePolicy.EnsureDecisionEditable(profile, decision); CheckVersion(decision.RowVersion, request.RowVersion, decision.ConfigurationKey);
        var validation = CivilEngineeringConfigurationDecisionRegistry.Validate(key, request.SchemaVersion, request.Value); if (!validation.IsValid) throw Validation(key, validation.Errors);
        await ValidateLookupSelectionsAsync(key, validation.CanonicalJson!, cancellationToken);
        var before = Snapshot(decision); decision.SchemaVersion = request.SchemaVersion; decision.ValueJson = validation.CanonicalJson!; decision.EffectiveFrom = Utc(validation.EffectiveFrom); decision.EffectiveTo = Utc(validation.EffectiveTo); decision.Status = CivilEngineeringConfigurationDecisionStatus.Draft; decision.ApprovalStatus = CivilEngineeringConfigurationApprovalStatus.Pending; decision.ApprovedAt = null; decision.ApprovedById = null; decision.ApprovalReference = null; decision.DecisionDate = null; decision.SourceLineage = Clean(request.SourceLineage); decision.Notes = Clean(request.Notes); Touch(decision);
        AddRevision(profileId, decision.Id, CivilEngineeringAuditEventMap.SaveDecision, correlationId, request.Reason, before, Snapshot(decision)); await SaveAsync(cancellationToken); return await MapDecisionAsync(decision, cancellationToken);
    }

    public async Task<CivilEngineeringDecisionDto> SubmitDecisionAsync(Guid profileId, string key, SubmitCivilEngineeringDecisionRequest request, string correlationId, CancellationToken cancellationToken = default)
    {
        var profile = await FindProfileAsync(profileId, true, cancellationToken); var decision = await FindDecisionAsync(profileId, key, true, cancellationToken); CivilEngineeringConfigurationLifecyclePolicy.EnsureDecisionEditable(profile, decision); CheckVersion(decision.RowVersion, request.RowVersion, key);
        var value = CivilEngineeringConfigurationDecisionRegistry.Validate(key, decision.SchemaVersion, CivilEngineeringConfigurationDecisionRegistry.ParseValue(decision.ValueJson)); if (!value.IsValid) throw Validation(key, value.Errors);
        if (!await HasCurrentPublishedEvidenceAsync(decision.Id, cancellationToken)) throw Validation(key, ["At least one current published central-DMS evidence record is required before submission."]);
        var before = Snapshot(decision); decision.Status = CivilEngineeringConfigurationDecisionStatus.Proposed; decision.ApprovalStatus = CivilEngineeringConfigurationApprovalStatus.Pending; decision.DecisionDate = DateTime.UtcNow; Touch(decision); AddRevision(profileId, decision.Id, CivilEngineeringAuditEventMap.SubmitDecision, correlationId, request.Reason, before, Snapshot(decision)); await SaveAsync(cancellationToken); return await MapDecisionAsync(decision, cancellationToken);
    }

    public Task<CivilEngineeringDecisionDto> ApproveDecisionAsync(Guid profileId, string key, DecideCivilEngineeringDecisionRequest request, string correlationId, CancellationToken cancellationToken = default) => DecideAsync(profileId, key, request, true, correlationId, cancellationToken);
    public Task<CivilEngineeringDecisionDto> RejectDecisionAsync(Guid profileId, string key, DecideCivilEngineeringDecisionRequest request, string correlationId, CancellationToken cancellationToken = default) => DecideAsync(profileId, key, request, false, correlationId, cancellationToken);

    private async Task<CivilEngineeringDecisionDto> DecideAsync(Guid profileId, string key, DecideCivilEngineeringDecisionRequest request, bool approve, string correlationId, CancellationToken cancellationToken)
    {
        var profile = await FindProfileAsync(profileId, true, cancellationToken); CivilEngineeringConfigurationLifecyclePolicy.EnsureEditable(profile); var decision = await FindDecisionAsync(profileId, key, true, cancellationToken); CheckVersion(decision.RowVersion, request.RowVersion, key);
        if (decision.Status != CivilEngineeringConfigurationDecisionStatus.Proposed) throw new CivilEngineeringConfigurationConflictException("Only a submitted decision can be approved or rejected.");
        var preparerId = decision.LastModifiedById ?? decision.CreatedById;
        if (preparerId == _user.UserId) throw new CivilEngineeringConfigurationConflictException("The user who last prepared or submitted this decision cannot approve or reject it.");
        if (approve && !await HasCurrentPublishedEvidenceAsync(decision.Id, cancellationToken)) throw Validation(key, ["The linked central-DMS evidence is no longer a current published version. Link current evidence and resubmit before approval."]);
        var before = Snapshot(decision); decision.Status = approve ? CivilEngineeringConfigurationDecisionStatus.Approved : CivilEngineeringConfigurationDecisionStatus.Rejected; decision.ApprovalStatus = approve ? CivilEngineeringConfigurationApprovalStatus.Approved : CivilEngineeringConfigurationApprovalStatus.Rejected; decision.ApprovedById = _user.UserId; decision.ApprovedAt = DateTime.UtcNow; decision.ApprovalReference = request.ApprovalReference.Trim();
        if (approve) decision.EvidenceStatus = CivilEngineeringConfigurationEvidenceStatus.Verified; Touch(decision); AddRevision(profileId, decision.Id, approve ? CivilEngineeringAuditEventMap.ApproveDecision : CivilEngineeringAuditEventMap.RejectDecision, correlationId, request.Reason, before, Snapshot(decision)); await SaveAsync(cancellationToken); return await MapDecisionAsync(decision, cancellationToken);
    }

    public async Task<CivilEngineeringEvidenceDto> LinkEvidenceAsync(Guid profileId, string key, LinkCivilEngineeringEvidenceRequest request, string correlationId, CancellationToken cancellationToken = default)
    {
        var profile = await FindProfileAsync(profileId, true, cancellationToken); var decision = await FindDecisionAsync(profileId, key, true, cancellationToken); CivilEngineeringConfigurationLifecyclePolicy.EnsureDecisionEditable(profile, decision); CheckVersion(decision.RowVersion, request.DecisionRowVersion, key);
        if (request.CentralDocumentRecordId == Guid.Empty || request.CentralDocumentVersionId == Guid.Empty) throw Validation(key, ["Select a current published central-DMS document version. Direct file paths are not accepted."]);
        if (!AllowedEvidenceTypes.Contains(request.EvidenceType.Trim())) throw Validation(key, ["Select a supported evidence type."]);
        var version = await _db.CentralDocumentVersions.Include(x => x.DocumentRecord).Where(CentralDocumentEvidenceRules.CurrentPublished()).FirstOrDefaultAsync(x => x.TenantId == _user.TenantId && x.Id == request.CentralDocumentVersionId && x.DocumentRecordId == request.CentralDocumentRecordId, cancellationToken);
        if (version is null) throw Validation(key, ["The selected central-DMS version is not the current published version for this tenant."]);
        if (await _db.CivilEngineeringConfigurationEvidenceLinks.AnyAsync(x => x.TenantId == _user.TenantId && x.DecisionId == decision.Id && x.CentralDocumentVersionId == version.Id && !x.IsDeleted, cancellationToken)) throw new CivilEngineeringConfigurationConflictException("This document version is already linked to the decision.");
        var link = new CivilEngineeringConfigurationEvidenceLink { TenantId = _user.TenantId, ProfileId = profileId, DecisionId = decision.Id, CentralDocumentRecordId = version.DocumentRecordId, CentralDocumentVersionId = version.Id, EvidenceType = request.EvidenceType.Trim(), Checksum = Clean(request.Checksum), LinkedAt = DateTime.UtcNow, LinkedById = _user.UserId, CreatedAt = DateTime.UtcNow, CreatedBy = _user.FullName, CreatedById = _user.UserId };
        _db.CivilEngineeringConfigurationEvidenceLinks.Add(link); decision.EvidenceStatus = CivilEngineeringConfigurationEvidenceStatus.Attached; Touch(decision); AddRevision(profileId, decision.Id, CivilEngineeringAuditEventMap.LinkEvidence, correlationId, request.Reason, null, new { link.Id, link.EvidenceType, link.CentralDocumentRecordId, link.CentralDocumentVersionId }); await SaveAsync(cancellationToken); return MapEvidence(link, version.DocumentRecord, version);
    }

    public async Task UnlinkEvidenceAsync(Guid profileId, string key, Guid evidenceId, string rowVersion, string? reason, string correlationId, CancellationToken cancellationToken = default)
    {
        var profile = await FindProfileAsync(profileId, true, cancellationToken); var decision = await FindDecisionAsync(profileId, key, true, cancellationToken); CivilEngineeringConfigurationLifecyclePolicy.EnsureDecisionEditable(profile, decision); CheckVersion(decision.RowVersion, rowVersion, key);
        var link = await _db.CivilEngineeringConfigurationEvidenceLinks.FirstOrDefaultAsync(x => x.TenantId == _user.TenantId && x.ProfileId == profileId && x.DecisionId == decision.Id && x.Id == evidenceId && !x.IsDeleted, cancellationToken) ?? throw new CivilEngineeringConfigurationNotFoundException("Evidence link was not found.");
        link.IsDeleted = true; link.DeletedAt = DateTime.UtcNow; link.DeletedBy = _user.FullName; if (!await _db.CivilEngineeringConfigurationEvidenceLinks.AnyAsync(x => x.TenantId == _user.TenantId && x.DecisionId == decision.Id && x.Id != link.Id && !x.IsDeleted, cancellationToken)) decision.EvidenceStatus = CivilEngineeringConfigurationEvidenceStatus.Missing; Touch(decision); AddRevision(profileId, decision.Id, CivilEngineeringAuditEventMap.UnlinkEvidence, correlationId, reason, new { link.Id, link.CentralDocumentVersionId }, null); await SaveAsync(cancellationToken);
    }

    public async Task<CivilEngineeringValidationResultDto> ValidateProfileAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var profile = await FindProfileAsync(id, false, cancellationToken); var decisions = await _db.CivilEngineeringConfigurationDecisions.AsNoTracking().Where(x => x.TenantId == _user.TenantId && x.ProfileId == id && !x.IsDeleted).ToListAsync(cancellationToken); var errors = new List<CivilEngineeringValidationIssueDto>();
        if (profile.EffectiveTo.HasValue && profile.EffectiveTo < profile.EffectiveFrom) Error(errors, "PROFILE_PERIOD", "Effective to cannot be before effective from.");
        foreach (var definition in CivilEngineeringConfigurationDecisionRegistry.Definitions)
        {
            var decision = decisions.SingleOrDefault(x => x.ConfigurationKey == definition.ConfigurationKey); if (decision is null) { Error(errors, "MISSING_DECISION", "The required decision is missing.", definition.ConfigurationKey); continue; }
            var typed = CivilEngineeringConfigurationDecisionRegistry.Validate(decision.ConfigurationKey, decision.SchemaVersion, CivilEngineeringConfigurationDecisionRegistry.ParseValue(decision.ValueJson)); foreach (var message in typed.Errors) Error(errors, "INVALID_VALUE", message, decision.ConfigurationKey);
            if (decision.Status != CivilEngineeringConfigurationDecisionStatus.Approved || decision.ApprovalStatus != CivilEngineeringConfigurationApprovalStatus.Approved) Error(errors, "NOT_APPROVED", "The decision must be approved.", decision.ConfigurationKey);
            if (decision.EvidenceStatus != CivilEngineeringConfigurationEvidenceStatus.Verified) Error(errors, "EVIDENCE_NOT_VERIFIED", "Central-DMS evidence must be verified by the approver.", decision.ConfigurationKey);
            else if (!await HasCurrentPublishedEvidenceAsync(decision.Id, cancellationToken)) Error(errors, "EVIDENCE_NOT_CURRENT", "At least one verified evidence link must still reference the current published central-DMS version.", decision.ConfigurationKey);
            if (!decision.EffectiveFrom.HasValue || decision.EffectiveFrom > profile.EffectiveFrom || (profile.EffectiveTo.HasValue && (!decision.EffectiveTo.HasValue || decision.EffectiveTo < profile.EffectiveTo))) Error(errors, "PERIOD_NOT_COVERED", "The decision effective period must cover the profile period.", decision.ConfigurationKey);
        }
        return new CivilEngineeringValidationResultDto { Errors = errors };
    }

    public async Task<CivilEngineeringProfileDto> PublishProfileAsync(Guid id, CivilEngineeringLifecycleRequest request, string correlationId, CancellationToken cancellationToken = default)
    {
        var reason = RequiredReason(request.Reason, "publishing a Civil Engineering configuration profile");
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken); var profile = await FindProfileAsync(id, true, cancellationToken); CivilEngineeringConfigurationLifecyclePolicy.EnsureEditable(profile); CheckVersion(profile.RowVersion, request.RowVersion, "profile");
        var validation = await ValidateProfileAsync(id, cancellationToken); if (!validation.IsValid) throw new CivilEngineeringConfigurationValidationException("The profile is not ready to publish.", validation);
        var existing = await _db.CivilEngineeringConfigurationProfiles.Where(x => x.TenantId == _user.TenantId && x.ProfileKey == profile.ProfileKey && x.Id != id && !x.IsDeleted && x.LifecycleStatus == CivilEngineeringConfigurationProfileStatus.Published).OrderByDescending(x => x.Version).ToListAsync(cancellationToken);
        foreach (var current in existing)
        {
            if (current.EffectiveFrom >= profile.EffectiveFrom) throw new CivilEngineeringConfigurationConflictException("A published version already starts on or after this profile's effective date.");
            if (!current.EffectiveTo.HasValue || current.EffectiveTo >= profile.EffectiveFrom) { current.EffectiveTo = profile.EffectiveFrom.AddTicks(-1); Touch(current); }
            if (profile.EffectiveFrom <= DateTime.UtcNow) { current.LifecycleStatus = CivilEngineeringConfigurationProfileStatus.Retired; current.RetiredAt = DateTime.UtcNow; current.RetiredById = _user.UserId; }
        }
        if (profile.IsDefault) foreach (var other in await _db.CivilEngineeringConfigurationProfiles.Where(x => x.TenantId == _user.TenantId && x.Id != id && x.IsDefault && !x.IsDeleted).ToListAsync(cancellationToken)) other.IsDefault = false;
        var before = Snapshot(profile); profile.LifecycleStatus = CivilEngineeringConfigurationProfileStatus.Published; profile.PublishedAt = DateTime.UtcNow; profile.PublishedById = _user.UserId; Touch(profile); AddRevision(id, null, CivilEngineeringAuditEventMap.PublishProfile, correlationId, reason, before, Snapshot(profile)); await SaveAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return await GetProfileAsync(id, cancellationToken);
    }

    public async Task<CivilEngineeringProfileDto> RetireProfileAsync(Guid id, CivilEngineeringLifecycleRequest request, string correlationId, CancellationToken cancellationToken = default)
    {
        var reason = RequiredReason(request.Reason, "retiring a Civil Engineering configuration profile");
        var profile = await FindProfileAsync(id, true, cancellationToken); CheckVersion(profile.RowVersion, request.RowVersion, "profile"); if (profile.LifecycleStatus != CivilEngineeringConfigurationProfileStatus.Published) throw new CivilEngineeringConfigurationConflictException("Only a published profile can be retired."); var before = Snapshot(profile); profile.LifecycleStatus = CivilEngineeringConfigurationProfileStatus.Retired; profile.RetiredAt = DateTime.UtcNow; profile.RetiredById = _user.UserId; Touch(profile); AddRevision(id, null, CivilEngineeringAuditEventMap.RetireProfile, correlationId, reason, before, Snapshot(profile)); await SaveAsync(cancellationToken); return await GetProfileAsync(id, cancellationToken);
    }

    public async Task<CivilEngineeringProfileDto> CloneDraftAsync(Guid id, CloneCivilEngineeringProfileRequest request, string correlationId, CancellationToken cancellationToken = default)
    {
        var source = await FindProfileAsync(id, false, cancellationToken); var all = await _db.CivilEngineeringConfigurationProfiles.IgnoreQueryFilters().Where(x => x.TenantId == _user.TenantId && x.ProfileKey == source.ProfileKey).ToListAsync(cancellationToken); if (all.Any(x => !x.IsDeleted && x.LifecycleStatus == CivilEngineeringConfigurationProfileStatus.Draft)) throw new CivilEngineeringConfigurationConflictException("This profile family already has an active draft.");
        var sourceDecisions = await _db.CivilEngineeringConfigurationDecisions.AsNoTracking().Where(x => x.TenantId == _user.TenantId && x.ProfileId == id && !x.IsDeleted).ToListAsync(cancellationToken); var now = DateTime.UtcNow; var effective = Utc(request.EffectiveFrom ?? source.EffectiveFrom); ValidateDates(effective, source.EffectiveTo);
        var clone = new CivilEngineeringConfigurationProfile { TenantId = _user.TenantId, ProfileKey = source.ProfileKey, ProfileCode = source.ProfileCode, Name = source.Name, Version = CivilEngineeringConfigurationLifecyclePolicy.NextVersion(all), LifecycleStatus = CivilEngineeringConfigurationProfileStatus.Draft, EffectiveFrom = effective, EffectiveTo = source.EffectiveTo, ChangeSummary = Clean(request.ChangeSummary), IsDefault = source.IsDefault, SupersedesProfileId = source.Id, CreatedAt = now, CreatedBy = _user.FullName, CreatedById = _user.UserId }; _db.CivilEngineeringConfigurationProfiles.Add(clone);
        foreach (var old in sourceDecisions) _db.CivilEngineeringConfigurationDecisions.Add(new CivilEngineeringConfigurationDecision { TenantId = _user.TenantId, ProfileId = clone.Id, ConfigurationKey = old.ConfigurationKey, SchemaVersion = old.SchemaVersion, OwnerGroup = old.OwnerGroup, Status = CivilEngineeringConfigurationDecisionStatus.Draft, ApprovalStatus = CivilEngineeringConfigurationApprovalStatus.Pending, EvidenceStatus = CivilEngineeringConfigurationEvidenceStatus.Missing, ValueJson = old.ValueJson, EffectiveFrom = old.EffectiveFrom, EffectiveTo = old.EffectiveTo, SourceDecisionId = old.Id, SourceLineage = $"Cloned from {source.ProfileCode}/v{source.Version}/{old.ConfigurationKey}", CreatedAt = now, CreatedBy = _user.FullName, CreatedById = _user.UserId });
        AddRevision(clone.Id, null, CivilEngineeringAuditEventMap.CloneDraft, correlationId, request.ChangeSummary, Snapshot(source), Snapshot(clone)); await SaveAsync(cancellationToken); return await GetProfileAsync(clone.Id, cancellationToken);
    }

    public async Task DeleteDraftAsync(Guid id, CivilEngineeringLifecycleRequest request, string correlationId, CancellationToken cancellationToken = default)
    {
        var reason = RequiredReason(request.Reason, "deleting a Civil Engineering configuration draft");
        var profile = await FindProfileAsync(id, true, cancellationToken); CivilEngineeringConfigurationLifecyclePolicy.EnsureEditable(profile); CheckVersion(profile.RowVersion, request.RowVersion, "profile"); var decisions = await _db.CivilEngineeringConfigurationDecisions.Where(x => x.TenantId == _user.TenantId && x.ProfileId == id && !x.IsDeleted).ToListAsync(cancellationToken); var evidence = await _db.CivilEngineeringConfigurationEvidenceLinks.Where(x => x.TenantId == _user.TenantId && x.ProfileId == id && !x.IsDeleted).ToListAsync(cancellationToken); var now = DateTime.UtcNow; foreach (var item in decisions.Cast<BaseEntity>().Concat(evidence)) { item.IsDeleted = true; item.DeletedAt = now; item.DeletedBy = _user.FullName; } profile.IsDeleted = true; profile.DeletedAt = now; profile.DeletedBy = _user.FullName; AddRevision(id, null, CivilEngineeringAuditEventMap.DeleteDraft, correlationId, reason, Snapshot(profile), null); await SaveAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CivilEngineeringRevisionDto>> GetHistoryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await FindProfileAsync(id, false, cancellationToken); var items = await _db.CivilEngineeringConfigurationRevisions.AsNoTracking().Where(x => x.TenantId == _user.TenantId && x.ProfileId == id && !x.IsDeleted).OrderByDescending(x => x.CreatedAt).ToListAsync(cancellationToken); return items.Select(MapRevision).ToList();
    }

    private async Task ValidateLookupSelectionsAsync(string key, string canonicalJson, CancellationToken cancellationToken)
    {
        var schema = CivilEngineeringConfigurationDecisionRegistry.GetRequired(key); var lookups = (await GetLookupsAsync(cancellationToken)).Sources; using var document = JsonDocument.Parse(canonicalJson); var errors = new List<string>();
        foreach (var field in schema.Fields.Where(x => x.LookupSource != null))
        {
            if (!document.RootElement.TryGetProperty(field.Name, out var value)) continue; var allowed = lookups[field.LookupSource!].Where(x => field.LookupGroup == null || string.Equals(x.Group, field.LookupGroup, StringComparison.OrdinalIgnoreCase)).Select(x => x.Value).ToHashSet(StringComparer.OrdinalIgnoreCase); IEnumerable<string> selected = value.ValueKind == JsonValueKind.Array ? value.EnumerateArray().Select(x => x.GetString() ?? string.Empty) : [value.GetString() ?? string.Empty]; if (selected.Any(x => !allowed.Contains(x))) errors.Add($"{field.Label} contains a missing, inactive, unpublished, cross-tenant, or wrong-entity selection.");
        }
        if (string.Equals(key, "CIV-CFG-003", StringComparison.OrdinalIgnoreCase))
        {
            var configured = JsonSerializer.Deserialize<CivilEngineeringDesignReviewValue>(canonicalJson, ConfigurationJson);
            if (configured is not null && configured.WorkflowDefinitionId != Guid.Empty)
            {
                var workflow = await _db.WorkflowDefinitions.AsNoTracking()
                    .Include(value => value.Steps)
                    .SingleOrDefaultAsync(value => value.TenantId == _user.TenantId
                                                   && value.Id == configured.WorkflowDefinitionId
                                                   && !value.IsDeleted
                                                   && value.IsActive
                                                   && value.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Published,
                        cancellationToken);
                if (workflow is not null)
                    errors.AddRange(CivilEngineeringDesignReviewChecklistPolicy.Validate(
                        workflow,
                        configured.RequireHodApproval));
            }
        }
        if (errors.Count > 0) throw Validation(key, errors);
    }

    private async Task<CivilEngineeringConfigurationProfile> FindProfileAsync(Guid id, bool tracked, CancellationToken token)
    {
        EnsureTenant(); var query = _db.CivilEngineeringConfigurationProfiles.Where(x => x.TenantId == _user.TenantId && x.Id == id && !x.IsDeleted); if (!tracked) query = query.AsNoTracking(); return await query.SingleOrDefaultAsync(token) ?? throw new CivilEngineeringConfigurationNotFoundException("Civil Engineering configuration profile was not found.");
    }
    private async Task<CivilEngineeringConfigurationDecision> FindDecisionAsync(Guid profileId, string key, bool tracked, CancellationToken token)
    {
        var normalized = key.Trim().ToUpperInvariant(); CivilEngineeringConfigurationDecisionRegistry.GetRequired(normalized); var query = _db.CivilEngineeringConfigurationDecisions.Where(x => x.TenantId == _user.TenantId && x.ProfileId == profileId && x.ConfigurationKey == normalized && !x.IsDeleted); if (!tracked) query = query.AsNoTracking(); return await query.SingleOrDefaultAsync(token) ?? throw new CivilEngineeringConfigurationNotFoundException("Civil Engineering configuration decision was not found.");
    }

    private async Task<CivilEngineeringProfileDto> MapProfileAsync(CivilEngineeringConfigurationProfile profile, CancellationToken token)
    {
        var decisions = await _db.CivilEngineeringConfigurationDecisions.AsNoTracking().Where(x => x.TenantId == _user.TenantId && x.ProfileId == profile.Id && !x.IsDeleted).OrderBy(x => x.ConfigurationKey).ToListAsync(token); var mapped = new List<CivilEngineeringDecisionDto>(); foreach (var decision in decisions) mapped.Add(await MapDecisionAsync(decision, token)); var summary = MapSummary(profile, decisions);
        return new CivilEngineeringProfileDto { Id = summary.Id, ProfileKey = summary.ProfileKey, ProfileCode = summary.ProfileCode, Name = summary.Name, Version = summary.Version, LifecycleStatus = summary.LifecycleStatus, EffectiveFrom = summary.EffectiveFrom, EffectiveTo = summary.EffectiveTo, IsDefault = summary.IsDefault, CompleteDecisionCount = summary.CompleteDecisionCount, TotalDecisionCount = summary.TotalDecisionCount, UpdatedAt = summary.UpdatedAt, RowVersion = summary.RowVersion, ChangeSummary = profile.ChangeSummary, SupersedesProfileId = profile.SupersedesProfileId, PublishedAt = profile.PublishedAt, RetiredAt = profile.RetiredAt, Decisions = mapped, Validation = await ValidateProfileAsync(profile.Id, token) };
    }

    private async Task<CivilEngineeringDecisionDto> MapDecisionAsync(CivilEngineeringConfigurationDecision decision, CancellationToken token)
    {
        var definition = CivilEngineeringConfigurationDecisionRegistry.GetRequired(decision.ConfigurationKey); var links = await _db.CivilEngineeringConfigurationEvidenceLinks.AsNoTracking().Include(x => x.CentralDocumentRecord).Include(x => x.CentralDocumentVersion).Where(x => x.TenantId == _user.TenantId && x.DecisionId == decision.Id && !x.IsDeleted).ToListAsync(token);
        return new CivilEngineeringDecisionDto { Id = decision.Id, ConfigurationKey = decision.ConfigurationKey, DisplayName = definition.DisplayName, Description = definition.Description, OwnerGroup = decision.OwnerGroup, SchemaVersion = decision.SchemaVersion, Status = decision.Status, ApprovalStatus = decision.ApprovalStatus, EvidenceStatus = decision.EvidenceStatus, Value = CivilEngineeringConfigurationDecisionRegistry.ParseValue(decision.ValueJson), DecisionDate = decision.DecisionDate, EffectiveFrom = decision.EffectiveFrom, EffectiveTo = decision.EffectiveTo, ApprovedById = decision.ApprovedById, ApprovedAt = decision.ApprovedAt, ApprovalReference = decision.ApprovalReference, SourceLineage = decision.SourceLineage, Notes = decision.Notes, IsComplete = Complete(decision), RowVersion = Encode(decision.RowVersion), Evidence = links.Select(x => MapEvidence(x, x.CentralDocumentRecord, x.CentralDocumentVersion)).ToList() };
    }

    private Task<bool> HasCurrentPublishedEvidenceAsync(Guid decisionId, CancellationToken token) =>
        _db.CivilEngineeringConfigurationEvidenceLinks
            .AsNoTracking()
            .Where(link => link.TenantId == _user.TenantId && link.DecisionId == decisionId && !link.IsDeleted)
            .Join(
                _db.CentralDocumentVersions.AsNoTracking().Where(CentralDocumentEvidenceRules.CurrentPublished()),
                link => link.CentralDocumentVersionId,
                version => version.Id,
                (_, _) => true)
            .AnyAsync(token);

    private static CivilEngineeringProfileSummaryDto MapSummary(CivilEngineeringConfigurationProfile p, IEnumerable<CivilEngineeringConfigurationDecision> decisions) { var list = decisions.ToList(); return new() { Id = p.Id, ProfileKey = p.ProfileKey, ProfileCode = p.ProfileCode, Name = p.Name, Version = p.Version, LifecycleStatus = p.LifecycleStatus, EffectiveFrom = p.EffectiveFrom, EffectiveTo = p.EffectiveTo, IsDefault = p.IsDefault, CompleteDecisionCount = list.Count(Complete), TotalDecisionCount = list.Count, UpdatedAt = p.UpdatedAt ?? p.CreatedAt, RowVersion = Encode(p.RowVersion) }; }
    private static CivilEngineeringEvidenceDto MapEvidence(CivilEngineeringConfigurationEvidenceLink x, CentralDocumentRecord? record, CentralDocumentVersion? version) => new() { Id = x.Id, DecisionId = x.DecisionId, EvidenceType = x.EvidenceType, CentralDocumentRecordId = x.CentralDocumentRecordId, CentralDocumentVersionId = x.CentralDocumentVersionId, DocumentReference = record?.DocumentReference, DocumentTitle = record?.Title, VersionNumber = version?.VersionNumber, Checksum = x.Checksum, LinkedAt = x.LinkedAt, LinkedById = x.LinkedById };
    private static CivilEngineeringRevisionDto MapRevision(CivilEngineeringConfigurationRevision x)
    {
        var definition = CivilEngineeringAuditEventMap.GetRequired(x.Action);
        return new CivilEngineeringRevisionDto
        {
            Id = x.Id,
            ProfileId = x.ProfileId,
            DecisionId = x.DecisionId,
            SourceType = x.DecisionId.HasValue ? "CivilEngineeringConfigurationDecision" : "CivilEngineeringConfigurationProfile",
            SourceId = x.DecisionId ?? x.ProfileId,
            Action = x.Action,
            Operation = definition.Operation,
            Result = x.Result,
            CorrelationId = x.CorrelationId,
            ActorUserId = x.ActorUserId,
            ActorName = x.ActorName,
            ActorRoles = x.ActorRoles,
            Reason = x.Reason,
            Before = ParseOptional(x.BeforeJson),
            After = ParseOptional(x.AfterJson),
            Timestamp = x.CreatedAt
        };
    }
    private static bool Complete(CivilEngineeringConfigurationDecision d) => d.Status == CivilEngineeringConfigurationDecisionStatus.Approved && d.ApprovalStatus == CivilEngineeringConfigurationApprovalStatus.Approved && d.EvidenceStatus == CivilEngineeringConfigurationEvidenceStatus.Verified;
    private static CivilEngineeringConfigurationDecision NewDecision(CivilEngineeringConfigurationProfile profile, CivilEngineeringDecisionDefinition definition, DateTime now) => new() { TenantId = profile.TenantId, ProfileId = profile.Id, ConfigurationKey = definition.ConfigurationKey, SchemaVersion = 1, OwnerGroup = definition.OwnerGroup, Status = CivilEngineeringConfigurationDecisionStatus.Draft, ApprovalStatus = CivilEngineeringConfigurationApprovalStatus.Pending, EvidenceStatus = CivilEngineeringConfigurationEvidenceStatus.Missing, CreatedAt = now, CreatedBy = profile.CreatedBy, CreatedById = profile.CreatedById };
    private void AddRevision(Guid profileId, Guid? decisionId, string action, string correlationId, string? reason, object? before, object? after)
    {
        CivilEngineeringAuditEventMap.GetRequired(action);
        _db.CivilEngineeringConfigurationRevisions.Add(new CivilEngineeringConfigurationRevision { TenantId = _user.TenantId, ProfileId = profileId, DecisionId = decisionId, Action = action, Result = "Succeeded", CorrelationId = string.IsNullOrWhiteSpace(correlationId) ? Guid.NewGuid().ToString("N") : correlationId[..Math.Min(100, correlationId.Length)], ActorUserId = _user.UserId, ActorName = _user.FullName, ActorRoles = string.Join(',', _user.Roles.OrderBy(x => x)), Reason = Clean(reason), BeforeJson = before == null ? null : JsonSerializer.Serialize(before, AuditJson), AfterJson = after == null ? null : JsonSerializer.Serialize(after, AuditJson), CreatedAt = DateTime.UtcNow, CreatedBy = _user.FullName, CreatedById = _user.UserId });
    }
    private static object Snapshot(CivilEngineeringConfigurationProfile x) => new { x.Id, x.ProfileKey, x.ProfileCode, x.Name, x.Version, x.LifecycleStatus, x.EffectiveFrom, x.EffectiveTo, x.IsDefault, x.SupersedesProfileId, x.PublishedAt, x.RetiredAt };
    private static object Snapshot(CivilEngineeringConfigurationDecision x) => new { x.Id, x.ConfigurationKey, x.SchemaVersion, x.OwnerGroup, x.Status, x.ApprovalStatus, x.EvidenceStatus, Value = CivilEngineeringConfigurationDecisionRegistry.ParseValue(x.ValueJson), x.DecisionDate, x.EffectiveFrom, x.EffectiveTo, x.ApprovedById, x.ApprovedAt, x.ApprovalReference, x.SourceLineage, x.Notes };
    private void Touch(BaseEntity entity) { entity.UpdatedAt = DateTime.UtcNow; entity.UpdatedBy = _user.FullName; entity.LastModifiedById = _user.UserId; }
    private void EnsureTenant() { if (!_user.IsAuthenticated || _user.TenantId == Guid.Empty || _user.UserId == Guid.Empty) throw new UnauthorizedAccessException("An authenticated tenant context is required."); }
    private static void ValidateDates(DateTime from, DateTime? to) { if (from == default) throw Validation(null, ["Effective from is required."]); if (to.HasValue && to < from) throw Validation(null, ["Effective to cannot be before effective from."]); }
    private static void CheckVersion(byte[] actual, string supplied, string target) { byte[] expected; try { expected = Convert.FromBase64String(supplied); } catch { throw Validation(null, [$"A valid row version is required for {target}."]); } if (!actual.SequenceEqual(expected)) throw new CivilEngineeringConfigurationConflictException($"The {target} changed. Reload before saving."); }
    private async Task SaveAsync(CancellationToken token) { try { await _db.SaveChangesAsync(token); } catch (DbUpdateConcurrencyException) { throw new CivilEngineeringConfigurationConflictException("The record changed while it was being saved. Reload and retry."); } catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) == true || ex.InnerException?.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) == true) { _logger.LogWarning(ex, "Civil Engineering configuration unique constraint conflict"); throw new CivilEngineeringConfigurationConflictException("The requested Civil Engineering configuration version conflicts with an existing record."); } }
    private static CivilEngineeringConfigurationValidationException Validation(string? key, IEnumerable<string> messages) => new("Civil Engineering configuration validation failed.", new CivilEngineeringValidationResultDto { Errors = messages.Select(x => new CivilEngineeringValidationIssueDto { Code = "VALIDATION_ERROR", ConfigurationKey = key, Message = x }).ToList() });
    private static void Error(ICollection<CivilEngineeringValidationIssueDto> errors, string code, string message, string? key = null) => errors.Add(new() { Code = code, Message = message, ConfigurationKey = key });
    private static string Encode(byte[] value) => Convert.ToBase64String(value ?? Array.Empty<byte>());
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string RequiredReason(string? value, string action)
    {
        var reason = Clean(value);
        if (reason is null || reason.Length < 3)
            throw Validation(null, [$"A reason of at least 3 characters is required before {action}."]);
        return reason;
    }
    private static DateTime Utc(DateTime value) => value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);
    private static DateTime? Utc(DateTime? value) => value.HasValue ? Utc(value.Value) : null;
    private static JsonElement? ParseOptional(string? json) { if (string.IsNullOrWhiteSpace(json)) return null; using var doc = JsonDocument.Parse(json); return doc.RootElement.Clone(); }
}
