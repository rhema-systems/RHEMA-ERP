using System.Text.Json;
using System.Text.Json.Serialization;
using ErpSystem.Core.DTOs.QuantitySurvey;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.QuantitySurvey;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.QuantitySurvey;
using ErpSystem.Core.Services.DocumentManagement;
using ErpSystem.Core.Services.QuantitySurvey;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Data.Services;

public sealed class QuantitySurveyConfigurationService : IQuantitySurveyConfigurationService
{
    public const string ProfileCode = "TDC-QUANTITY-SURVEY";
    private static readonly HashSet<string> AllowedEvidenceTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Policy", "Committee minute", "Technical standard", "Contract clause", "Approval memorandum"
    };
    private static readonly JsonSerializerOptions AuditJson = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserProvider _user;
    private readonly ILogger<QuantitySurveyConfigurationService> _logger;

    public QuantitySurveyConfigurationService(ApplicationDbContext db, ICurrentUserProvider user, ILogger<QuantitySurveyConfigurationService> logger)
    {
        _db = db;
        _user = user;
        _logger = logger;
    }

    public IReadOnlyList<QuantitySurveyDecisionSchemaDto> GetSchemas() => QuantitySurveyConfigurationDecisionRegistry.ToDtos();

    public async Task<QuantitySurveyLookupsDto> GetLookupsAsync(CancellationToken cancellationToken = default)
    {
        EnsureTenant();
        var roles = await _db.Roles.AsNoTracking().Where(x => x.Name != null).OrderBy(x => x.Name)
            .Select(x => new QuantitySurveyLookupOptionDto { Value = x.Id.ToString(), Label = x.Name!, Group = "Security role" }).ToListAsync(cancellationToken);
        var projectTypes = await _db.ProjectTypes.AsNoTracking().Where(x => x.TenantId == _user.TenantId && !x.IsDeleted && x.IsActive).OrderBy(x => x.Name)
            .Select(x => new QuantitySurveyLookupOptionDto { Value = x.Id.ToString(), Label = x.Code + " - " + x.Name }).ToListAsync(cancellationToken);
        var locations = await _db.Locations.AsNoTracking().Where(x => x.TenantId == _user.TenantId && !x.IsDeleted && x.IsActive).OrderBy(x => x.Name)
            .Select(x => new QuantitySurveyLookupOptionDto { Value = x.Id.ToString(), Label = x.Code + " - " + x.Name }).ToListAsync(cancellationToken);
        var currencies = await _db.Currencies.AsNoTracking().Where(x => x.TenantId == _user.TenantId && !x.IsDeleted && x.IsActive).OrderBy(x => x.DisplayOrder).ThenBy(x => x.CurrencyCode)
            .Select(x => new QuantitySurveyLookupOptionDto { Value = x.CurrencyCode, Label = x.CurrencyCode + " - " + x.CurrencyName }).ToListAsync(cancellationToken);
        var workflows = await _db.WorkflowDefinitions.AsNoTracking().Where(x => x.TenantId == _user.TenantId && !x.IsDeleted && x.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Published && x.IsActive && x.EntityType.IsActive && !x.EntityType.IsDeleted).OrderBy(x => x.Name).ThenByDescending(x => x.Version)
            .Select(x => new QuantitySurveyLookupOptionDto { Value = x.Id.ToString(), Label = x.Name + " v" + x.Version, Group = x.EntityType.Code }).ToListAsync(cancellationToken);
        var reports = await _db.Reports.AsNoTracking().Where(x => x.TenantId == _user.TenantId && !x.IsDeleted && (x.Status == "published" || x.Status == "Published")).OrderBy(x => x.Name)
            .Select(x => new QuantitySurveyLookupOptionDto { Value = x.Id.ToString(), Label = x.Name, Group = x.Type }).ToListAsync(cancellationToken);
        var reportTemplates = await _db.ReportTemplates.AsNoTracking().Where(x => x.TenantId == _user.TenantId && !x.IsDeleted && (x.Status == "published" || x.Status == "Published")).OrderBy(x => x.Name).ThenByDescending(x => x.Version)
            .Select(x => new QuantitySurveyLookupOptionDto { Value = x.Id.ToString(), Label = x.Name + " v" + x.Version, Group = x.Category }).ToListAsync(cancellationToken);
        var dmsTemplates = await _db.CentralDocumentMetadataTemplates.AsNoTracking().Where(x => x.TenantId == _user.TenantId && !x.IsDeleted && x.IsActive && x.PublishedAt.HasValue).OrderBy(x => x.DocumentType)
            .Select(x => new QuantitySurveyLookupOptionDto { Value = x.Id.ToString(), Label = x.TemplateCode + " - " + x.DocumentType, Group = x.Module }).ToListAsync(cancellationToken);
        var postingExpenseAccounts = await _db.Accounts.AsNoTracking().Where(x => x.TenantId == _user.TenantId && !x.IsDeleted && x.Status == AccountStatus.Active && x.AllowDirectPosting && x.AccountType == AccountType.Expense).OrderBy(x => x.AccountNumber)
            .Select(x => new QuantitySurveyLookupOptionDto { Value = x.Id.ToString(), Label = x.AccountNumber + " - " + x.AccountName, Group = "Expense" }).ToListAsync(cancellationToken);
        var accountsPayableAccounts = await _db.Accounts.AsNoTracking().Where(x => x.TenantId == _user.TenantId && !x.IsDeleted && x.Status == AccountStatus.Active && x.AccountType == AccountType.Liability && x.IsControlAccount).OrderBy(x => x.AccountNumber)
            .Select(x => new QuantitySurveyLookupOptionDto { Value = x.Id.ToString(), Label = x.AccountNumber + " - " + x.AccountName, Group = "Accounts Payable" }).ToListAsync(cancellationToken);
        var supplierPaymentTerms = await _db.PaymentTerms.AsNoTracking().Where(x => x.TenantId == _user.TenantId && !x.IsDeleted && x.IsActive && (x.ApplicableTo == "All" || x.ApplicableTo == "Supplier" || x.ApplicableTo == "Contractor")).OrderBy(x => x.DisplayOrder).ThenBy(x => x.Code)
            .Select(x => new QuantitySurveyLookupOptionDto { Value = x.Id.ToString(), Label = x.Code + " - " + x.Name, Group = x.ApplicableTo }).ToListAsync(cancellationToken);
        var supplierWithholdingTaxes = await _db.Taxes.AsNoTracking().Where(x => x.TenantId == _user.TenantId && !x.IsDeleted && x.IsActive && (x.Applicability == TaxApplicability.Purchases || x.Applicability == TaxApplicability.Both) && x.Category == TaxCategory.Withholding).OrderBy(x => x.Code)
            .Select(x => new QuantitySurveyLookupOptionDto { Value = x.Id.ToString(), Label = x.Code + " - " + x.Name, Group = "Withholding" }).ToListAsync(cancellationToken);
        var supplierTaxGroups = await _db.TaxGroups.AsNoTracking().Where(x => x.TenantId == _user.TenantId && !x.IsDeleted && x.IsActive && (x.Applicability == TaxApplicability.Purchases || x.Applicability == TaxApplicability.Both)).OrderBy(x => x.Code)
            .Select(x => new QuantitySurveyLookupOptionDto { Value = x.Id.ToString(), Label = x.Code + " - " + x.Name, Group = "Purchases" }).ToListAsync(cancellationToken);
        return new QuantitySurveyLookupsDto { Sources = new Dictionary<string, IReadOnlyList<QuantitySurveyLookupOptionDto>>(StringComparer.OrdinalIgnoreCase)
        {
            ["roles"] = roles, ["projectTypes"] = projectTypes, ["locations"] = locations, ["currencies"] = currencies,
            ["workflows"] = workflows, ["reports"] = reports, ["reportTemplates"] = reportTemplates, ["dmsTemplates"] = dmsTemplates,
            ["postingExpenseAccounts"] = postingExpenseAccounts, ["accountsPayableAccounts"] = accountsPayableAccounts,
            ["supplierPaymentTerms"] = supplierPaymentTerms, ["supplierTaxGroups"] = supplierTaxGroups,
            ["supplierWithholdingTaxes"] = supplierWithholdingTaxes
        }};
    }

    public async Task<QuantitySurveyPagedResult<QuantitySurveyProfileSummaryDto>> GetProfilesAsync(QuantitySurveyProfileListRequest request, CancellationToken cancellationToken = default)
    {
        EnsureTenant();
        var page = Math.Max(1, request.Page); var size = Math.Clamp(request.PageSize, 1, 100);
        var query = _db.QuantitySurveyConfigurationProfiles.AsNoTracking().Where(x => x.TenantId == _user.TenantId && !x.IsDeleted);
        if (request.Status.HasValue) query = query.Where(x => x.LifecycleStatus == request.Status);
        if (!string.IsNullOrWhiteSpace(request.Search)) { var search = request.Search.Trim(); query = query.Where(x => x.Name.Contains(search) || x.ProfileCode.Contains(search)); }
        var total = await query.CountAsync(cancellationToken);
        var profiles = await query.OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt).ThenByDescending(x => x.Version).Skip((page - 1) * size).Take(size).ToListAsync(cancellationToken);
        var ids = profiles.Select(x => x.Id).ToList();
        var decisions = await _db.QuantitySurveyConfigurationDecisions.AsNoTracking().Where(x => x.TenantId == _user.TenantId && ids.Contains(x.ProfileId) && !x.IsDeleted).ToListAsync(cancellationToken);
        return new QuantitySurveyPagedResult<QuantitySurveyProfileSummaryDto> { Items = profiles.Select(x => MapSummary(x, decisions.Where(d => d.ProfileId == x.Id))).ToList(), Page = page, PageSize = size, TotalCount = total };
    }

    public async Task<QuantitySurveyProfileDto> GetProfileAsync(Guid id, CancellationToken cancellationToken = default)
    {
        EnsureTenant();
        var profile = await FindProfileAsync(id, false, cancellationToken);
        return await MapProfileAsync(profile, cancellationToken);
    }

    public async Task<QuantitySurveyProfileDto?> GetEffectiveProfileAsync(DateTime atUtc, CancellationToken cancellationToken = default)
    {
        EnsureTenant(); var at = Utc(atUtc);
        var profile = await _db.QuantitySurveyConfigurationProfiles.AsNoTracking().Where(x => x.TenantId == _user.TenantId && !x.IsDeleted && x.ProfileCode == ProfileCode && x.LifecycleStatus == QuantitySurveyConfigurationProfileStatus.Published && x.EffectiveFrom <= at && (!x.EffectiveTo.HasValue || x.EffectiveTo >= at)).OrderByDescending(x => x.Version).FirstOrDefaultAsync(cancellationToken);
        return profile is null ? null : await MapProfileAsync(profile, cancellationToken);
    }

    public async Task<QuantitySurveyProfileDto> CreateProfileAsync(CreateQuantitySurveyProfileRequest request, string correlationId, CancellationToken cancellationToken = default)
    {
        EnsureTenant(); ValidateDates(request.EffectiveFrom, request.EffectiveTo);
        var family = await _db.QuantitySurveyConfigurationProfiles
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == _user.TenantId && x.ProfileCode == ProfileCode)
            .OrderByDescending(x => x.Version)
            .ToListAsync(cancellationToken);
        if (family.Any(x => !x.IsDeleted))
            throw new QuantitySurveyConfigurationConflictException("The tenant already has a quantity-survey configuration family. Clone its latest version instead.");
        var predecessor = family.FirstOrDefault();
        var now = DateTime.UtcNow;
        var profile = new QuantitySurveyConfigurationProfile { TenantId = _user.TenantId, ProfileKey = predecessor?.ProfileKey ?? Guid.NewGuid(), ProfileCode = ProfileCode, Name = request.Name.Trim(), Version = QuantitySurveyConfigurationLifecyclePolicy.NextVersion(family), EffectiveFrom = Utc(request.EffectiveFrom), EffectiveTo = Utc(request.EffectiveTo), ChangeSummary = Clean(request.ChangeSummary), IsDefault = request.IsDefault, SupersedesProfileId = predecessor?.Id, CreatedAt = now, CreatedBy = _user.FullName, CreatedById = _user.UserId };
        _db.QuantitySurveyConfigurationProfiles.Add(profile);
        foreach (var definition in QuantitySurveyConfigurationDecisionRegistry.Definitions)
            _db.QuantitySurveyConfigurationDecisions.Add(NewDecision(profile, definition, now));
        AddRevision(profile.Id, null, QuantitySurveyAuditEventMap.CreateProfile, correlationId, request.ChangeSummary, null, Snapshot(profile));
        await SaveAsync(cancellationToken);
        return await GetProfileAsync(profile.Id, cancellationToken);
    }

    public async Task<QuantitySurveyProfileDto> UpdateProfileAsync(Guid id, UpdateQuantitySurveyProfileRequest request, string correlationId, CancellationToken cancellationToken = default)
    {
        EnsureTenant(); ValidateDates(request.EffectiveFrom, request.EffectiveTo);
        var profile = await FindProfileAsync(id, true, cancellationToken); QuantitySurveyConfigurationLifecyclePolicy.EnsureEditable(profile); CheckVersion(profile.RowVersion, request.RowVersion, "profile");
        var before = Snapshot(profile); profile.Name = request.Name.Trim(); profile.EffectiveFrom = Utc(request.EffectiveFrom); profile.EffectiveTo = Utc(request.EffectiveTo); profile.ChangeSummary = Clean(request.ChangeSummary); profile.IsDefault = request.IsDefault; Touch(profile);
        AddRevision(profile.Id, null, QuantitySurveyAuditEventMap.UpdateProfile, correlationId, request.Reason, before, Snapshot(profile)); await SaveAsync(cancellationToken); return await GetProfileAsync(id, cancellationToken);
    }

    public async Task<QuantitySurveyDecisionDto> SaveDecisionAsync(Guid profileId, string key, SaveQuantitySurveyDecisionRequest request, string correlationId, CancellationToken cancellationToken = default)
    {
        EnsureTenant(); var profile = await FindProfileAsync(profileId, true, cancellationToken); var decision = await FindDecisionAsync(profileId, key, true, cancellationToken);
        QuantitySurveyConfigurationLifecyclePolicy.EnsureDecisionEditable(profile, decision); CheckVersion(decision.RowVersion, request.RowVersion, decision.DecisionKey);
        var validation = QuantitySurveyConfigurationDecisionRegistry.Validate(key, request.SchemaVersion, request.Value); if (!validation.IsValid) throw Validation(key, validation.Errors);
        await ValidateLookupSelectionsAsync(key, validation.CanonicalJson!, cancellationToken);
        var before = Snapshot(decision); decision.SchemaVersion = request.SchemaVersion; decision.ValueJson = validation.CanonicalJson!; decision.EffectiveFrom = Utc(validation.EffectiveFrom); decision.EffectiveTo = Utc(validation.EffectiveTo); decision.Status = QuantitySurveyConfigurationDecisionStatus.Draft; decision.ApprovalStatus = QuantitySurveyConfigurationApprovalStatus.Pending; decision.ApprovedAt = null; decision.ApprovedById = null; decision.ApprovalReference = null; decision.DecisionDate = null; decision.SourceLineage = Clean(request.SourceLineage); decision.Notes = Clean(request.Notes); Touch(decision);
        AddRevision(profileId, decision.Id, QuantitySurveyAuditEventMap.SaveDecision, correlationId, request.Reason, before, Snapshot(decision)); await SaveAsync(cancellationToken); return await MapDecisionAsync(decision, cancellationToken);
    }

    public async Task<QuantitySurveyDecisionDto> SubmitDecisionAsync(Guid profileId, string key, SubmitQuantitySurveyDecisionRequest request, string correlationId, CancellationToken cancellationToken = default)
    {
        var profile = await FindProfileAsync(profileId, true, cancellationToken); var decision = await FindDecisionAsync(profileId, key, true, cancellationToken); QuantitySurveyConfigurationLifecyclePolicy.EnsureDecisionEditable(profile, decision); CheckVersion(decision.RowVersion, request.RowVersion, key);
        var value = QuantitySurveyConfigurationDecisionRegistry.Validate(key, decision.SchemaVersion, QuantitySurveyConfigurationDecisionRegistry.ParseValue(decision.ValueJson)); if (!value.IsValid) throw Validation(key, value.Errors);
        if (!await HasCurrentPublishedEvidenceAsync(decision.Id, cancellationToken)) throw Validation(key, ["At least one current published central-DMS evidence record is required before submission."]);
        var before = Snapshot(decision); decision.Status = QuantitySurveyConfigurationDecisionStatus.Proposed; decision.ApprovalStatus = QuantitySurveyConfigurationApprovalStatus.Pending; decision.DecisionDate = DateTime.UtcNow; Touch(decision); AddRevision(profileId, decision.Id, QuantitySurveyAuditEventMap.SubmitDecision, correlationId, request.Reason, before, Snapshot(decision)); await SaveAsync(cancellationToken); return await MapDecisionAsync(decision, cancellationToken);
    }

    public Task<QuantitySurveyDecisionDto> ApproveDecisionAsync(Guid profileId, string key, DecideQuantitySurveyDecisionRequest request, string correlationId, CancellationToken cancellationToken = default) => DecideAsync(profileId, key, request, true, correlationId, cancellationToken);
    public Task<QuantitySurveyDecisionDto> RejectDecisionAsync(Guid profileId, string key, DecideQuantitySurveyDecisionRequest request, string correlationId, CancellationToken cancellationToken = default) => DecideAsync(profileId, key, request, false, correlationId, cancellationToken);

    private async Task<QuantitySurveyDecisionDto> DecideAsync(Guid profileId, string key, DecideQuantitySurveyDecisionRequest request, bool approve, string correlationId, CancellationToken cancellationToken)
    {
        var profile = await FindProfileAsync(profileId, true, cancellationToken); QuantitySurveyConfigurationLifecyclePolicy.EnsureEditable(profile); var decision = await FindDecisionAsync(profileId, key, true, cancellationToken); CheckVersion(decision.RowVersion, request.RowVersion, key);
        if (decision.Status != QuantitySurveyConfigurationDecisionStatus.Proposed) throw new QuantitySurveyConfigurationConflictException("Only a submitted decision can be approved or rejected.");
        var preparerId = decision.LastModifiedById ?? decision.CreatedById;
        if (preparerId == _user.UserId) throw new QuantitySurveyConfigurationConflictException("The user who last prepared or submitted this decision cannot approve or reject it.");
        if (approve && !await HasCurrentPublishedEvidenceAsync(decision.Id, cancellationToken)) throw Validation(key, ["The linked central-DMS evidence is no longer a current published version. Link current evidence and resubmit before approval."]);
        var before = Snapshot(decision); decision.Status = approve ? QuantitySurveyConfigurationDecisionStatus.Approved : QuantitySurveyConfigurationDecisionStatus.Rejected; decision.ApprovalStatus = approve ? QuantitySurveyConfigurationApprovalStatus.Approved : QuantitySurveyConfigurationApprovalStatus.Rejected; decision.ApprovedById = _user.UserId; decision.ApprovedAt = DateTime.UtcNow; decision.ApprovalReference = request.ApprovalReference.Trim();
        if (approve) decision.EvidenceStatus = QuantitySurveyConfigurationEvidenceStatus.Verified; Touch(decision); AddRevision(profileId, decision.Id, approve ? QuantitySurveyAuditEventMap.ApproveDecision : QuantitySurveyAuditEventMap.RejectDecision, correlationId, request.Reason, before, Snapshot(decision)); await SaveAsync(cancellationToken); return await MapDecisionAsync(decision, cancellationToken);
    }

    public async Task<QuantitySurveyEvidenceDto> LinkEvidenceAsync(Guid profileId, string key, LinkQuantitySurveyEvidenceRequest request, string correlationId, CancellationToken cancellationToken = default)
    {
        var profile = await FindProfileAsync(profileId, true, cancellationToken); var decision = await FindDecisionAsync(profileId, key, true, cancellationToken); QuantitySurveyConfigurationLifecyclePolicy.EnsureDecisionEditable(profile, decision); CheckVersion(decision.RowVersion, request.DecisionRowVersion, key);
        if (!request.CentralDocumentRecordId.HasValue || !request.CentralDocumentVersionId.HasValue) throw Validation(key, ["Select a current published central-DMS document version. Direct file paths are not accepted."]);
        if (!AllowedEvidenceTypes.Contains(request.EvidenceType.Trim())) throw Validation(key, ["Select a supported evidence type."]);
        var version = await _db.CentralDocumentVersions.Include(x => x.DocumentRecord).Where(CentralDocumentEvidenceRules.CurrentPublished()).FirstOrDefaultAsync(x => x.TenantId == _user.TenantId && x.Id == request.CentralDocumentVersionId && x.DocumentRecordId == request.CentralDocumentRecordId, cancellationToken);
        if (version is null) throw Validation(key, ["The selected central-DMS version is not the current published version for this tenant."]);
        if (await _db.QuantitySurveyConfigurationEvidenceLinks.AnyAsync(x => x.TenantId == _user.TenantId && x.DecisionId == decision.Id && x.CentralDocumentVersionId == version.Id && !x.IsDeleted, cancellationToken)) throw new QuantitySurveyConfigurationConflictException("This document version is already linked to the decision.");
        var link = new QuantitySurveyConfigurationEvidenceLink { TenantId = _user.TenantId, ProfileId = profileId, DecisionId = decision.Id, CentralDocumentRecordId = version.DocumentRecordId, CentralDocumentVersionId = version.Id, EvidenceType = request.EvidenceType.Trim(), ExternalReference = Clean(request.ExternalReference), Checksum = Clean(request.Checksum), LinkedAt = DateTime.UtcNow, LinkedById = _user.UserId, CreatedAt = DateTime.UtcNow, CreatedBy = _user.FullName, CreatedById = _user.UserId };
        _db.QuantitySurveyConfigurationEvidenceLinks.Add(link); decision.EvidenceStatus = QuantitySurveyConfigurationEvidenceStatus.Attached; Touch(decision); AddRevision(profileId, decision.Id, QuantitySurveyAuditEventMap.LinkEvidence, correlationId, request.Reason, null, new { link.Id, link.EvidenceType, link.CentralDocumentRecordId, link.CentralDocumentVersionId }); await SaveAsync(cancellationToken); return MapEvidence(link, version.DocumentRecord, version);
    }

    public async Task UnlinkEvidenceAsync(Guid profileId, string key, Guid evidenceId, string rowVersion, string? reason, string correlationId, CancellationToken cancellationToken = default)
    {
        var profile = await FindProfileAsync(profileId, true, cancellationToken); var decision = await FindDecisionAsync(profileId, key, true, cancellationToken); QuantitySurveyConfigurationLifecyclePolicy.EnsureDecisionEditable(profile, decision); CheckVersion(decision.RowVersion, rowVersion, key);
        var link = await _db.QuantitySurveyConfigurationEvidenceLinks.FirstOrDefaultAsync(x => x.TenantId == _user.TenantId && x.ProfileId == profileId && x.DecisionId == decision.Id && x.Id == evidenceId && !x.IsDeleted, cancellationToken) ?? throw new QuantitySurveyConfigurationNotFoundException("Evidence link was not found.");
        link.IsDeleted = true; link.DeletedAt = DateTime.UtcNow; link.DeletedBy = _user.FullName; if (!await _db.QuantitySurveyConfigurationEvidenceLinks.AnyAsync(x => x.TenantId == _user.TenantId && x.DecisionId == decision.Id && x.Id != link.Id && !x.IsDeleted, cancellationToken)) decision.EvidenceStatus = QuantitySurveyConfigurationEvidenceStatus.Missing; Touch(decision); AddRevision(profileId, decision.Id, QuantitySurveyAuditEventMap.UnlinkEvidence, correlationId, reason, new { link.Id, link.CentralDocumentVersionId }, null); await SaveAsync(cancellationToken);
    }

    public async Task<QuantitySurveyValidationResultDto> ValidateProfileAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var profile = await FindProfileAsync(id, false, cancellationToken); var decisions = await _db.QuantitySurveyConfigurationDecisions.AsNoTracking().Where(x => x.TenantId == _user.TenantId && x.ProfileId == id && !x.IsDeleted).ToListAsync(cancellationToken); var errors = new List<QuantitySurveyValidationIssueDto>();
        if (profile.EffectiveTo.HasValue && profile.EffectiveTo < profile.EffectiveFrom) Error(errors, "PROFILE_PERIOD", "Effective to cannot be before effective from.");
        var configured = decisions.Where(x => QuantitySurveyArchitectureScope.HasConfiguration(x.ValueJson)).ToList();
        if (configured.Count == 0) Error(errors, "EMPTY_PROFILE", "Configure at least one QS process before publishing.");
        foreach (var decision in configured)
        {
            try
            {
                var typed = QuantitySurveyConfigurationDecisionRegistry.Validate(decision.DecisionKey, decision.SchemaVersion, QuantitySurveyConfigurationDecisionRegistry.ParseValue(decision.ValueJson));
                foreach (var message in typed.Errors) Error(errors, "INVALID_VALUE", message, decision.DecisionKey);
            }
            catch (JsonException) { Error(errors, "INVALID_VALUE", "The saved configuration is not valid JSON.", decision.DecisionKey); }
            if (decision.Status != QuantitySurveyConfigurationDecisionStatus.Approved || decision.ApprovalStatus != QuantitySurveyConfigurationApprovalStatus.Approved) Error(errors, "NOT_APPROVED", "The decision must be approved.", decision.DecisionKey);
            if (decision.EvidenceStatus != QuantitySurveyConfigurationEvidenceStatus.Verified) Error(errors, "EVIDENCE_NOT_VERIFIED", "Central-DMS evidence must be verified by the approver.", decision.DecisionKey);
            else if (!await HasCurrentPublishedEvidenceAsync(decision.Id, cancellationToken)) Error(errors, "EVIDENCE_NOT_CURRENT", "At least one verified evidence link must still reference the current published central-DMS version.", decision.DecisionKey);
            if (!decision.EffectiveFrom.HasValue || decision.EffectiveFrom > profile.EffectiveFrom || (profile.EffectiveTo.HasValue && (!decision.EffectiveTo.HasValue || decision.EffectiveTo < profile.EffectiveTo))) Error(errors, "PERIOD_NOT_COVERED", "The decision effective period must cover the profile period.", decision.DecisionKey);
        }
        return new QuantitySurveyValidationResultDto { Errors = errors };
    }

    public async Task<QuantitySurveyProfileDto> PublishProfileAsync(Guid id, QuantitySurveyLifecycleRequest request, string correlationId, CancellationToken cancellationToken = default)
    {
        var reason = RequiredReason(request.Reason, "publishing a QS configuration profile");
        // Publication, retirement and the audit revision are persisted by one SaveChanges.
        // EF owns that atomic transaction so SQL Server's retry strategy can execute it.
        var profile = await FindProfileAsync(id, true, cancellationToken); QuantitySurveyConfigurationLifecyclePolicy.EnsureEditable(profile); CheckVersion(profile.RowVersion, request.RowVersion, "profile");
        var validation = await ValidateProfileAsync(id, cancellationToken); if (!validation.IsValid) throw new QuantitySurveyConfigurationValidationException("The profile is not ready to publish.", validation);
        var existing = await _db.QuantitySurveyConfigurationProfiles.Where(x => x.TenantId == _user.TenantId && x.ProfileKey == profile.ProfileKey && x.Id != id && !x.IsDeleted && x.LifecycleStatus == QuantitySurveyConfigurationProfileStatus.Published).OrderByDescending(x => x.Version).ToListAsync(cancellationToken);
        foreach (var current in existing)
        {
            if (current.EffectiveFrom >= profile.EffectiveFrom) throw new QuantitySurveyConfigurationConflictException("A published version already starts on or after this profile's effective date.");
            if (!current.EffectiveTo.HasValue || current.EffectiveTo >= profile.EffectiveFrom) { current.EffectiveTo = profile.EffectiveFrom.AddTicks(-1); Touch(current); }
            if (profile.EffectiveFrom <= DateTime.UtcNow) { current.LifecycleStatus = QuantitySurveyConfigurationProfileStatus.Retired; current.RetiredAt = DateTime.UtcNow; current.RetiredById = _user.UserId; }
        }
        if (profile.IsDefault) foreach (var other in await _db.QuantitySurveyConfigurationProfiles.Where(x => x.TenantId == _user.TenantId && x.Id != id && x.IsDefault && !x.IsDeleted).ToListAsync(cancellationToken)) other.IsDefault = false;
        var before = Snapshot(profile); profile.LifecycleStatus = QuantitySurveyConfigurationProfileStatus.Published; profile.PublishedAt = DateTime.UtcNow; profile.PublishedById = _user.UserId; Touch(profile); AddRevision(id, null, QuantitySurveyAuditEventMap.PublishProfile, correlationId, reason, before, Snapshot(profile)); await SaveAsync(cancellationToken); return await GetProfileAsync(id, cancellationToken);
    }

    public async Task<QuantitySurveyProfileDto> RetireProfileAsync(Guid id, QuantitySurveyLifecycleRequest request, string correlationId, CancellationToken cancellationToken = default)
    {
        var reason = RequiredReason(request.Reason, "retiring a QS configuration profile");
        var profile = await FindProfileAsync(id, true, cancellationToken); CheckVersion(profile.RowVersion, request.RowVersion, "profile"); if (profile.LifecycleStatus != QuantitySurveyConfigurationProfileStatus.Published) throw new QuantitySurveyConfigurationConflictException("Only a published profile can be retired."); var before = Snapshot(profile); profile.LifecycleStatus = QuantitySurveyConfigurationProfileStatus.Retired; profile.RetiredAt = DateTime.UtcNow; profile.RetiredById = _user.UserId; Touch(profile); AddRevision(id, null, QuantitySurveyAuditEventMap.RetireProfile, correlationId, reason, before, Snapshot(profile)); await SaveAsync(cancellationToken); return await GetProfileAsync(id, cancellationToken);
    }

    public async Task<QuantitySurveyProfileDto> CloneDraftAsync(Guid id, CloneQuantitySurveyProfileRequest request, string correlationId, CancellationToken cancellationToken = default)
    {
        var source = await FindProfileAsync(id, false, cancellationToken); var all = await _db.QuantitySurveyConfigurationProfiles.IgnoreQueryFilters().Where(x => x.TenantId == _user.TenantId && x.ProfileKey == source.ProfileKey).ToListAsync(cancellationToken); if (all.Any(x => !x.IsDeleted && x.LifecycleStatus == QuantitySurveyConfigurationProfileStatus.Draft)) throw new QuantitySurveyConfigurationConflictException("This profile family already has an active draft.");
        var sourceDecisions = await _db.QuantitySurveyConfigurationDecisions.AsNoTracking().Where(x => x.TenantId == _user.TenantId && x.ProfileId == id && !x.IsDeleted).ToListAsync(cancellationToken); var now = DateTime.UtcNow; var effective = Utc(request.EffectiveFrom ?? source.EffectiveFrom); ValidateDates(effective, source.EffectiveTo);
        var clone = new QuantitySurveyConfigurationProfile { TenantId = _user.TenantId, ProfileKey = source.ProfileKey, ProfileCode = source.ProfileCode, Name = source.Name, Version = QuantitySurveyConfigurationLifecyclePolicy.NextVersion(all), LifecycleStatus = QuantitySurveyConfigurationProfileStatus.Draft, EffectiveFrom = effective, EffectiveTo = source.EffectiveTo, ChangeSummary = Clean(request.ChangeSummary), IsDefault = source.IsDefault, SupersedesProfileId = source.Id, CreatedAt = now, CreatedBy = _user.FullName, CreatedById = _user.UserId }; _db.QuantitySurveyConfigurationProfiles.Add(clone);
        foreach (var old in sourceDecisions) _db.QuantitySurveyConfigurationDecisions.Add(new QuantitySurveyConfigurationDecision { TenantId = _user.TenantId, ProfileId = clone.Id, DecisionKey = old.DecisionKey, SchemaVersion = old.SchemaVersion, OwnerGroup = old.OwnerGroup, Status = QuantitySurveyConfigurationDecisionStatus.Draft, ApprovalStatus = QuantitySurveyConfigurationApprovalStatus.Pending, EvidenceStatus = QuantitySurveyConfigurationEvidenceStatus.Missing, ValueJson = old.ValueJson, EffectiveFrom = old.EffectiveFrom, EffectiveTo = old.EffectiveTo, SourceDecisionId = old.Id, SourceLineage = $"Cloned from {source.ProfileCode}/v{source.Version}/{old.DecisionKey}", CreatedAt = now, CreatedBy = _user.FullName, CreatedById = _user.UserId });
        AddRevision(clone.Id, null, QuantitySurveyAuditEventMap.CloneDraft, correlationId, request.ChangeSummary, Snapshot(source), Snapshot(clone)); await SaveAsync(cancellationToken); return await GetProfileAsync(clone.Id, cancellationToken);
    }

    public async Task DeleteDraftAsync(Guid id, QuantitySurveyLifecycleRequest request, string correlationId, CancellationToken cancellationToken = default)
    {
        var reason = RequiredReason(request.Reason, "deleting a QS configuration draft");
        var profile = await FindProfileAsync(id, true, cancellationToken); QuantitySurveyConfigurationLifecyclePolicy.EnsureEditable(profile); CheckVersion(profile.RowVersion, request.RowVersion, "profile"); var decisions = await _db.QuantitySurveyConfigurationDecisions.Where(x => x.TenantId == _user.TenantId && x.ProfileId == id && !x.IsDeleted).ToListAsync(cancellationToken); var evidence = await _db.QuantitySurveyConfigurationEvidenceLinks.Where(x => x.TenantId == _user.TenantId && x.ProfileId == id && !x.IsDeleted).ToListAsync(cancellationToken); var now = DateTime.UtcNow; foreach (var item in decisions.Cast<BaseEntity>().Concat(evidence)) { item.IsDeleted = true; item.DeletedAt = now; item.DeletedBy = _user.FullName; } profile.IsDeleted = true; profile.DeletedAt = now; profile.DeletedBy = _user.FullName; AddRevision(id, null, QuantitySurveyAuditEventMap.DeleteDraft, correlationId, reason, Snapshot(profile), null); await SaveAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<QuantitySurveyRevisionDto>> GetHistoryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await FindProfileAsync(id, false, cancellationToken); var items = await _db.QuantitySurveyConfigurationRevisions.AsNoTracking().Where(x => x.TenantId == _user.TenantId && x.ProfileId == id && !x.IsDeleted).OrderByDescending(x => x.CreatedAt).ToListAsync(cancellationToken); return items.Select(MapRevision).ToList();
    }

    private async Task ValidateLookupSelectionsAsync(string key, string canonicalJson, CancellationToken cancellationToken)
    {
        var schema = QuantitySurveyConfigurationDecisionRegistry.GetRequired(key); var lookups = (await GetLookupsAsync(cancellationToken)).Sources; using var document = JsonDocument.Parse(canonicalJson); var errors = new List<string>();
        foreach (var field in schema.Fields.Where(x => x.LookupSource != null))
        {
            if (!document.RootElement.TryGetProperty(field.Name, out var value)) continue; var allowed = lookups[field.LookupSource!].Where(x => field.LookupGroup == null || string.Equals(x.Group, field.LookupGroup, StringComparison.OrdinalIgnoreCase)).Select(x => x.Value).ToHashSet(StringComparer.OrdinalIgnoreCase); IEnumerable<string> selected = value.ValueKind == JsonValueKind.Array ? value.EnumerateArray().Select(x => x.GetString() ?? string.Empty) : [value.GetString() ?? string.Empty]; if (selected.Any(x => !allowed.Contains(x))) errors.Add($"{field.Label} contains a missing, inactive, unpublished, cross-tenant, or wrong-entity selection.");
        }
        if (errors.Count > 0) throw Validation(key, errors);
    }

    private async Task<QuantitySurveyConfigurationProfile> FindProfileAsync(Guid id, bool tracked, CancellationToken token)
    {
        EnsureTenant(); var query = _db.QuantitySurveyConfigurationProfiles.Where(x => x.TenantId == _user.TenantId && x.Id == id && !x.IsDeleted); if (!tracked) query = query.AsNoTracking(); return await query.SingleOrDefaultAsync(token) ?? throw new QuantitySurveyConfigurationNotFoundException("Quantity-survey configuration profile was not found.");
    }
    private async Task<QuantitySurveyConfigurationDecision> FindDecisionAsync(Guid profileId, string key, bool tracked, CancellationToken token)
    {
        var normalized = key.Trim().ToUpperInvariant(); QuantitySurveyConfigurationDecisionRegistry.GetRequired(normalized); var query = _db.QuantitySurveyConfigurationDecisions.Where(x => x.TenantId == _user.TenantId && x.ProfileId == profileId && x.DecisionKey == normalized && !x.IsDeleted); if (!tracked) query = query.AsNoTracking(); return await query.SingleOrDefaultAsync(token) ?? throw new QuantitySurveyConfigurationNotFoundException("Quantity-survey configuration decision was not found.");
    }

    private async Task<QuantitySurveyProfileDto> MapProfileAsync(QuantitySurveyConfigurationProfile profile, CancellationToken token)
    {
        var decisions = await _db.QuantitySurveyConfigurationDecisions.AsNoTracking().Where(x => x.TenantId == _user.TenantId && x.ProfileId == profile.Id && !x.IsDeleted).OrderBy(x => x.DecisionKey).ToListAsync(token); var mapped = new List<QuantitySurveyDecisionDto>(); foreach (var decision in decisions) mapped.Add(await MapDecisionAsync(decision, token)); var summary = MapSummary(profile, decisions);
        return new QuantitySurveyProfileDto { Id = summary.Id, ProfileKey = summary.ProfileKey, ProfileCode = summary.ProfileCode, Name = summary.Name, Version = summary.Version, LifecycleStatus = summary.LifecycleStatus, EffectiveFrom = summary.EffectiveFrom, EffectiveTo = summary.EffectiveTo, IsDefault = summary.IsDefault, CompleteDecisionCount = summary.CompleteDecisionCount, TotalDecisionCount = summary.TotalDecisionCount, UpdatedAt = summary.UpdatedAt, RowVersion = summary.RowVersion, ChangeSummary = profile.ChangeSummary, SupersedesProfileId = profile.SupersedesProfileId, PublishedAt = profile.PublishedAt, RetiredAt = profile.RetiredAt, Decisions = mapped, Validation = await ValidateProfileAsync(profile.Id, token) };
    }

    private async Task<QuantitySurveyDecisionDto> MapDecisionAsync(QuantitySurveyConfigurationDecision decision, CancellationToken token)
    {
        var definition = QuantitySurveyConfigurationDecisionRegistry.GetRequired(decision.DecisionKey); var links = await _db.QuantitySurveyConfigurationEvidenceLinks.AsNoTracking().Include(x => x.CentralDocumentRecord).Include(x => x.CentralDocumentVersion).Where(x => x.TenantId == _user.TenantId && x.DecisionId == decision.Id && !x.IsDeleted).ToListAsync(token);
        return new QuantitySurveyDecisionDto { Id = decision.Id, DecisionKey = decision.DecisionKey, DisplayName = definition.DisplayName, Description = definition.Description, OwnerGroup = decision.OwnerGroup, SchemaVersion = decision.SchemaVersion, Status = decision.Status, ApprovalStatus = decision.ApprovalStatus, EvidenceStatus = decision.EvidenceStatus, Value = QuantitySurveyConfigurationDecisionRegistry.ParseValue(decision.ValueJson), DecisionDate = decision.DecisionDate, EffectiveFrom = decision.EffectiveFrom, EffectiveTo = decision.EffectiveTo, ApprovedById = decision.ApprovedById, ApprovedAt = decision.ApprovedAt, ApprovalReference = decision.ApprovalReference, SourceLineage = decision.SourceLineage, Notes = decision.Notes, IsComplete = Complete(decision), RowVersion = Encode(decision.RowVersion), Evidence = links.Select(x => MapEvidence(x, x.CentralDocumentRecord, x.CentralDocumentVersion)).ToList() };
    }

    private Task<bool> HasCurrentPublishedEvidenceAsync(Guid decisionId, CancellationToken token) =>
        _db.QuantitySurveyConfigurationEvidenceLinks
            .AsNoTracking()
            .Where(link => link.TenantId == _user.TenantId && link.DecisionId == decisionId && !link.IsDeleted && link.CentralDocumentVersionId.HasValue)
            .Join(
                _db.CentralDocumentVersions.AsNoTracking().Where(CentralDocumentEvidenceRules.CurrentPublished()),
                link => link.CentralDocumentVersionId!.Value,
                version => version.Id,
                (_, _) => true)
            .AnyAsync(token);

    private static QuantitySurveyProfileSummaryDto MapSummary(QuantitySurveyConfigurationProfile p, IEnumerable<QuantitySurveyConfigurationDecision> decisions) { var list = decisions.Where(d => QuantitySurveyArchitectureScope.HasConfiguration(d.ValueJson)).ToList(); return new() { Id = p.Id, ProfileKey = p.ProfileKey, ProfileCode = p.ProfileCode, Name = p.Name, Version = p.Version, LifecycleStatus = p.LifecycleStatus, EffectiveFrom = p.EffectiveFrom, EffectiveTo = p.EffectiveTo, IsDefault = p.IsDefault, CompleteDecisionCount = list.Count(Complete), TotalDecisionCount = list.Count, UpdatedAt = p.UpdatedAt ?? p.CreatedAt, RowVersion = Encode(p.RowVersion) }; }
    private static QuantitySurveyEvidenceDto MapEvidence(QuantitySurveyConfigurationEvidenceLink x, CentralDocumentRecord? record, CentralDocumentVersion? version) => new() { Id = x.Id, DecisionId = x.DecisionId, EvidenceType = x.EvidenceType, CentralDocumentRecordId = x.CentralDocumentRecordId, CentralDocumentVersionId = x.CentralDocumentVersionId, DocumentReference = record?.DocumentReference, DocumentTitle = record?.Title, VersionNumber = version?.VersionNumber, ExternalReference = x.ExternalReference, Checksum = x.Checksum, LinkedAt = x.LinkedAt, LinkedById = x.LinkedById };
    private static QuantitySurveyRevisionDto MapRevision(QuantitySurveyConfigurationRevision x)
    {
        var definition = QuantitySurveyAuditEventMap.GetRequired(x.Action);
        return new QuantitySurveyRevisionDto
        {
            Id = x.Id,
            ProfileId = x.ProfileId,
            DecisionId = x.DecisionId,
            SourceType = x.DecisionId.HasValue ? "QuantitySurveyConfigurationDecision" : "QuantitySurveyConfigurationProfile",
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
    private static bool Complete(QuantitySurveyConfigurationDecision d) => d.Status == QuantitySurveyConfigurationDecisionStatus.Approved && d.ApprovalStatus == QuantitySurveyConfigurationApprovalStatus.Approved && d.EvidenceStatus == QuantitySurveyConfigurationEvidenceStatus.Verified;
    private static QuantitySurveyConfigurationDecision NewDecision(QuantitySurveyConfigurationProfile profile, QuantitySurveyDecisionDefinition definition, DateTime now) => new() { TenantId = profile.TenantId, ProfileId = profile.Id, DecisionKey = definition.DecisionKey, SchemaVersion = 1, OwnerGroup = definition.OwnerGroup, Status = QuantitySurveyConfigurationDecisionStatus.Draft, ApprovalStatus = QuantitySurveyConfigurationApprovalStatus.Pending, EvidenceStatus = QuantitySurveyConfigurationEvidenceStatus.Missing, CreatedAt = now, CreatedBy = profile.CreatedBy, CreatedById = profile.CreatedById };
    private void AddRevision(Guid profileId, Guid? decisionId, string action, string correlationId, string? reason, object? before, object? after)
    {
        QuantitySurveyAuditEventMap.GetRequired(action);
        _db.QuantitySurveyConfigurationRevisions.Add(new QuantitySurveyConfigurationRevision { TenantId = _user.TenantId, ProfileId = profileId, DecisionId = decisionId, Action = action, Result = "Succeeded", CorrelationId = string.IsNullOrWhiteSpace(correlationId) ? Guid.NewGuid().ToString("N") : correlationId[..Math.Min(100, correlationId.Length)], ActorUserId = _user.UserId, ActorName = _user.FullName, ActorRoles = string.Join(',', _user.Roles.OrderBy(x => x)), Reason = Clean(reason), BeforeJson = before == null ? null : JsonSerializer.Serialize(before, AuditJson), AfterJson = after == null ? null : JsonSerializer.Serialize(after, AuditJson), CreatedAt = DateTime.UtcNow, CreatedBy = _user.FullName, CreatedById = _user.UserId });
    }
    private static object Snapshot(QuantitySurveyConfigurationProfile x) => new { x.Id, x.ProfileKey, x.ProfileCode, x.Name, x.Version, x.LifecycleStatus, x.EffectiveFrom, x.EffectiveTo, x.IsDefault, x.SupersedesProfileId, x.PublishedAt, x.RetiredAt };
    private static object Snapshot(QuantitySurveyConfigurationDecision x) => new { x.Id, x.DecisionKey, x.SchemaVersion, x.OwnerGroup, x.Status, x.ApprovalStatus, x.EvidenceStatus, Value = QuantitySurveyConfigurationDecisionRegistry.ParseValue(x.ValueJson), x.DecisionDate, x.EffectiveFrom, x.EffectiveTo, x.ApprovedById, x.ApprovedAt, x.ApprovalReference, x.SourceLineage, x.Notes };
    private void Touch(BaseEntity entity) { entity.UpdatedAt = DateTime.UtcNow; entity.UpdatedBy = _user.FullName; entity.LastModifiedById = _user.UserId; }
    private void EnsureTenant() { if (!_user.IsAuthenticated || _user.TenantId == Guid.Empty || _user.UserId == Guid.Empty) throw new UnauthorizedAccessException("An authenticated tenant context is required."); }
    private static void ValidateDates(DateTime from, DateTime? to) { if (from == default) throw Validation(null, ["Effective from is required."]); if (to.HasValue && to < from) throw Validation(null, ["Effective to cannot be before effective from."]); }
    private static void CheckVersion(byte[] actual, string supplied, string target) { byte[] expected; try { expected = Convert.FromBase64String(supplied); } catch { throw Validation(null, [$"A valid row version is required for {target}."]); } if (!actual.SequenceEqual(expected)) throw new QuantitySurveyConfigurationConflictException($"The {target} changed. Reload before saving."); }
    private async Task SaveAsync(CancellationToken token) { try { await _db.SaveChangesAsync(token); } catch (DbUpdateConcurrencyException) { throw new QuantitySurveyConfigurationConflictException("The record changed while it was being saved. Reload and retry."); } catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) == true || ex.InnerException?.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) == true) { _logger.LogWarning(ex, "QS configuration unique constraint conflict"); throw new QuantitySurveyConfigurationConflictException("The requested QS configuration version conflicts with an existing record."); } }
    private static QuantitySurveyConfigurationValidationException Validation(string? key, IEnumerable<string> messages) => new("Quantity-survey configuration validation failed.", new QuantitySurveyValidationResultDto { Errors = messages.Select(x => new QuantitySurveyValidationIssueDto { Code = "VALIDATION_ERROR", DecisionKey = key, Message = x }).ToList() });
    private static void Error(ICollection<QuantitySurveyValidationIssueDto> errors, string code, string message, string? key = null) => errors.Add(new() { Code = code, Message = message, DecisionKey = key });
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
