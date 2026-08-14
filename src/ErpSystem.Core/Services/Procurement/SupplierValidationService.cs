using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Procurement;

/// <summary>
/// Centralized service for validating supplier/contractor eligibility for procurement activities
/// </summary>
public class SupplierValidationService : ISupplierValidationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] DecisionKeys = Enumerable.Range(1, 14)
        .Select(number => $"DEC-{number:000}")
        .ToArray();

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUser;
    private readonly IProcurementSupplierEvidencePackService _evidencePacks;
    private readonly IProcurementControlEventService _controlEvents;
    private readonly ILogger<SupplierValidationService> _logger;

    public SupplierValidationService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUser,
        IProcurementSupplierEvidencePackService evidencePacks,
        IProcurementControlEventService controlEvents,
        ILogger<SupplierValidationService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _evidencePacks = evidencePacks;
        _controlEvents = controlEvents;
        _logger = logger;
    }

    public async Task<SupplierValidationResult> ValidateForPurchaseOrderAsync(Guid businessPartnerId)
    {
        return await EvaluateEligibilityAsync(new SupplierEligibilityEvaluationRequest
        {
            BusinessPartnerId = businessPartnerId,
            Boundary = SupplierEligibilityBoundary.ManualPurchaseOrder
        });
    }

    public async Task<SupplierValidationResult> ValidateForRfqAsync(Guid businessPartnerId, List<Guid>? categoryIds = null, decimal? minimumPerformanceRating = null)
    {
        return await EvaluateEligibilityAsync(new SupplierEligibilityEvaluationRequest
        {
            BusinessPartnerId = businessPartnerId,
            Boundary = SupplierEligibilityBoundary.Invitation,
            CategoryIds = categoryIds ?? new List<Guid>(),
            MinimumPerformanceRating = minimumPerformanceRating
        });
    }

    public async Task<SupplierValidationResult> ValidateForContractAsync(Guid businessPartnerId, bool requiresLicenses = false)
    {
        return await EvaluateEligibilityAsync(new SupplierEligibilityEvaluationRequest
        {
            BusinessPartnerId = businessPartnerId,
            Boundary = SupplierEligibilityBoundary.Contract,
            RequiresLicenses = requiresLicenses
        });
    }

    public async Task<SupplierValidationResult> ValidateFinancialHealthAsync(Guid businessPartnerId, decimal? minimumCreditRatingScore = null)
    {
        return await EvaluateEligibilityAsync(new SupplierEligibilityEvaluationRequest
        {
            BusinessPartnerId = businessPartnerId,
            Boundary = SupplierEligibilityBoundary.StatusReview,
            IncludeFinancialWarnings = true
        });
    }

    public async Task<SupplierValidationResult> ValidateForTenderAsync(Guid businessPartnerId, bool requiresPrequalification, decimal? minimumPerformanceRating = null)
    {
        return await EvaluateEligibilityAsync(new SupplierEligibilityEvaluationRequest
        {
            BusinessPartnerId = businessPartnerId,
            Boundary = SupplierEligibilityBoundary.Invitation,
            RequiresPrequalification = requiresPrequalification,
            MinimumPerformanceRating = minimumPerformanceRating
        });
    }

    public async Task<SupplierValidationResult> ValidateForFrameworkCallOffAsync(Guid businessPartnerId)
    {
        return await EvaluateEligibilityAsync(new SupplierEligibilityEvaluationRequest
        {
            BusinessPartnerId = businessPartnerId,
            Boundary = SupplierEligibilityBoundary.FrameworkCallOff
        });
    }

    public async Task<SupplierValidationResult> EvaluateEligibilityAsync(
        SupplierEligibilityEvaluationRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        if (request.BusinessPartnerId == Guid.Empty)
            return SupplierValidationResult.Fail("A supplier is required.", "SUPPLIER_REQUIRED");

        var now = DateTime.UtcNow;
        var partner = await _unitOfWork.Repository<BusinessPartner>()
            .GetQueryable(item => item.Id == request.BusinessPartnerId &&
                item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .Include(item => item.Categories).ThenInclude(item => item.Category)
            .Include(item => item.Licenses).ThenInclude(item => item.LicenseType)
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);

        if (partner is null)
            return SupplierValidationResult.Fail("Supplier was not found in the current tenant.", "SUPPLIER_NOT_FOUND");

        var result = new SupplierValidationResult
        {
            IsValid = true,
            BusinessPartnerId = partner.Id,
            TenantId = partner.TenantId,
            PartnerCode = partner.PartnerCode,
            PartnerName = partner.PartnerName,
            PartnerType = partner.PartnerType,
            Boundary = request.Boundary,
            EvaluatedAtUtc = now,
            ApprovalStatus = partner.ApprovalStatus,
            RegistrationStatus = partner.RegistrationStatus,
            IsActive = partner.IsActive,
            IsBlacklisted = partner.IsBlacklisted,
            BlacklistReason = partner.BlacklistReason,
            BlacklistDate = partner.BlacklistDate,
            BlacklistExpiryDate = partner.BlacklistExpiryDate,
            PerformanceRating = partner.PerformanceRating,
            RiskLevel = partner.RiskLevel,
            CreditRating = partner.CreditRating,
            CategoryIds = partner.Categories.Select(item => item.CategoryId).Distinct().Order().ToList(),
            Categories = partner.Categories.OrderBy(item => item.Category?.CategoryCode)
                .Select(item => new SupplierEligibilityCategoryLineage
                {
                    CategoryId = item.CategoryId,
                    CategoryCode = item.Category?.CategoryCode,
                    CategoryName = item.Category?.CategoryName,
                    IsPrimary = item.IsPrimary
                }).ToList(),
            RequiredCategoryIds = request.CategoryIds.Distinct().Order().ToList(),
            DecisionKeys = DecisionKeys.ToList()
        };

        AddBaseFindings(partner, result);
        AddCategoryFindings(request, result);
        AddPerformanceAndLicenseFindings(partner, request, result, now);
        AddFinancialWarnings(partner, request, result);
        await AddRegistrationEvidenceAsync(partner.Id, result, cancellationToken);
        await AddPrequalificationAsync(request, result, now, cancellationToken);
        await AddAvlPolicyLineageAsync(result, now, cancellationToken);
        await AddDueDiligenceLineageAsync(result, now, cancellationToken);
        if (!request.SkipFormalAvlMembership)
            await AddFormalAvlLineageAsync(result, now, cancellationToken);
        if (!request.SkipPerformanceScorecard)
            await AddPerformanceScorecardLineageAsync(result, now, cancellationToken);
        if (!request.SkipRiskAssessment)
            await AddRiskLineageAsync(result, now, cancellationToken);

        result.IsValid = result.Findings.All(item => !item.Blocking);
        result.Errors = result.Findings.Where(item => item.Blocking).Select(item => item.Message).ToList();
        result.Warnings.AddRange(result.Findings.Where(item => !item.Blocking).Select(item => item.Message));
        result.ValidationCode = result.Findings.FirstOrDefault(item => item.Blocking)?.Code ?? "ELIGIBLE";
        result.DecisionHash = ComputeDecisionHash(result);

        if (request.RecordAudit)
            await RecordDecisionAsync(request, result, cancellationToken);

        _logger.LogInformation(
            "Supplier eligibility {Result} for {PartnerId} at {Boundary}; decision {DecisionHash}",
            result.IsValid ? "allowed" : "denied", partner.Id, request.Boundary, result.DecisionHash);
        return result;
    }

    public async Task<SupplierValidationResult> EnforceEligibilityAsync(
        SupplierEligibilityEvaluationRequest request,
        CancellationToken cancellationToken = default)
    {
        request.RecordAudit = true;
        var result = await EvaluateEligibilityAsync(request, cancellationToken);
        if (!result.IsValid)
            throw new SupplierEligibilityException(result.ValidationCode,
                $"Supplier is not eligible for {request.Boundary}: {string.Join("; ", result.Errors)}", result);
        return result;
    }

    private void EnsureAuthenticatedTenant()
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty || _currentUser.TenantId == Guid.Empty)
            throw new SupplierEligibilityAuthorizationException("An authenticated tenant context is required.");
    }

    private static void AddBaseFindings(BusinessPartner partner, SupplierValidationResult result)
    {
        if (!(partner.PartnerType.Contains("Supplier", StringComparison.OrdinalIgnoreCase) ||
              partner.PartnerType.Contains("Contractor", StringComparison.OrdinalIgnoreCase) ||
              partner.PartnerType.Contains("Both", StringComparison.OrdinalIgnoreCase)))
            result.Block("SUPPLIER_TYPE_INVALID", $"Business partner type '{partner.PartnerType}' is not eligible for procurement.");

        if (!BusinessPartnerLifecyclePolicy.IsApproved(partner.ApprovalStatus))
            result.Block("SUPPLIER_NOT_APPROVED", $"Supplier approval status is '{partner.ApprovalStatus ?? "Pending"}'.");
        if (!BusinessPartnerLifecyclePolicy.IsOperationalRegistration(partner.RegistrationStatus))
            result.Block("REGISTRATION_NOT_APPROVED", $"Supplier registration status is '{partner.RegistrationStatus}'.");
        if (!partner.IsActive)
            result.Block("SUPPLIER_INACTIVE", "Supplier account is inactive.");
        if (partner.IsBlacklisted)
        {
            var expiry = partner.BlacklistExpiryDate.HasValue
                ? $" Blacklist expiry: {partner.BlacklistExpiryDate.Value:yyyy-MM-dd}."
                : string.Empty;
            result.Block("SUPPLIER_BLACKLISTED",
                $"Supplier is blacklisted. {partner.BlacklistReason ?? "No reason was recorded."}{expiry}");
        }
    }

    private static void AddCategoryFindings(
        SupplierEligibilityEvaluationRequest request,
        SupplierValidationResult result)
    {
        if (request.CategoryIds.Count == 0) return;
        var missing = request.CategoryIds.Distinct().Except(result.CategoryIds).Order().ToList();
        if (missing.Count > 0)
            result.Block("CATEGORY_NOT_REGISTERED",
                $"Supplier is not registered for required categories: {string.Join(", ", missing)}.");
    }

    private static void AddPerformanceAndLicenseFindings(
        BusinessPartner partner,
        SupplierEligibilityEvaluationRequest request,
        SupplierValidationResult result,
        DateTime now)
    {
        if (request.MinimumPerformanceRating.HasValue)
        {
            if (!partner.PerformanceRating.HasValue)
                result.Block("PERFORMANCE_RATING_MISSING",
                    $"A minimum performance rating of {request.MinimumPerformanceRating.Value:F2} is required.");
            else if (partner.PerformanceRating.Value < request.MinimumPerformanceRating.Value)
                result.Block("PERFORMANCE_RATING_LOW",
                    $"Supplier performance rating {partner.PerformanceRating.Value:F2} is below {request.MinimumPerformanceRating.Value:F2}.");
        }

        result.Licenses = partner.Licenses.OrderBy(item => item.LicenseNumber).Select(item =>
            new SupplierEligibilityLicenseLineage
            {
                LicenseId = item.Id,
                LicenseTypeId = item.LicenseTypeId,
                LicenseCode = item.LicenseType?.LicenseCode,
                LicenseNumber = item.LicenseNumber,
                IsMandatory = item.LicenseType?.IsMandatory ?? false,
                Status = item.Status,
                ExpiryDate = item.ExpiryDate,
                IsCurrent = string.Equals(item.Status, "Active", StringComparison.OrdinalIgnoreCase) &&
                    (!item.ExpiryDate.HasValue || item.ExpiryDate.Value >= now)
            }).ToList();

        if (!request.RequiresLicenses) return;
        if (result.Licenses.Count == 0 || result.Licenses.All(item => !item.IsCurrent))
            result.Block("LICENSE_REQUIRED", "No current supplier licence is recorded.");
        else if (result.Licenses.Any(item => item.IsMandatory && !item.IsCurrent))
            result.Block("MANDATORY_LICENSE_INVALID", "One or more mandatory supplier licences are missing, inactive, or expired.");
    }

    private static void AddFinancialWarnings(
        BusinessPartner partner,
        SupplierEligibilityEvaluationRequest request,
        SupplierValidationResult result)
    {
        if (!request.IncludeFinancialWarnings) return;
        if (new[] { "D", "C-", "C", "C+" }.Contains(partner.CreditRating, StringComparer.OrdinalIgnoreCase))
            result.Warn("LOW_CREDIT_RATING", $"Supplier credit rating is {partner.CreditRating}.");
        if (string.Equals(partner.RiskLevel, "High", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(partner.RiskLevel, "Critical", StringComparison.OrdinalIgnoreCase))
            result.Warn("ELEVATED_RISK", $"Supplier risk level is {partner.RiskLevel}.");
        if (partner.PartnerType.Contains("Contractor", StringComparison.OrdinalIgnoreCase) &&
            !partner.InsuranceCoverage.HasValue)
            result.Warn("INSURANCE_NOT_RECORDED", "Contractor insurance coverage is not recorded.");
    }

    private async Task AddRegistrationEvidenceAsync(
        Guid partnerId,
        SupplierValidationResult result,
        CancellationToken cancellationToken)
    {
        var registrations = await _unitOfWork.Repository<BusinessPartnerRegistration>()
            .GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                item.BusinessPartnerId == partnerId && !item.IsDeleted)
            .OrderByDescending(item => item.ApprovedDate)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        if (registrations.Count == 0)
        {
            result.Warn("LEGACY_REGISTRATION_LINEAGE",
                "Supplier predates governed onboarding; no linked application evidence-pack lineage exists.");
            return;
        }

        if (registrations.Count > 1)
        {
            result.Block("REGISTRATION_LINEAGE_AMBIGUOUS",
                "More than one onboarding application is linked to this supplier.");
            return;
        }

        var registration = registrations[0];
        result.RegistrationId = registration.Id;
        result.RegistrationNumber = registration.RegistrationNumber;
        result.RegistrationApplicationStatus = registration.Status;
        if (!string.Equals(registration.Status, "Approved", StringComparison.OrdinalIgnoreCase))
        {
            result.Block("ONBOARDING_APPLICATION_NOT_APPROVED",
                $"Linked onboarding application status is '{registration.Status}'.");
            return;
        }

        try
        {
            var readiness = await _evidencePacks.GetRegistrationReadinessAsync(registration.Id, cancellationToken);
            result.EvidencePackVersionId = readiness.PackVersionId;
            result.EvidencePackCode = readiness.PackCode;
            result.EvidencePackVersion = readiness.PackVersion;
            result.EvidenceReady = readiness.IsReady;
            result.EvidenceBlockingReasons = readiness.BlockingReasons.ToList();
            var binding = await _unitOfWork.Repository<ProcurementSupplierRegistrationEvidencePackBinding>()
                .GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                    item.RegistrationId == registration.Id && !item.IsDeleted)
                .AsNoTracking()
                .SingleOrDefaultAsync(cancellationToken);
            if (binding is not null)
            {
                result.EvidencePackBoundAtUtc = binding.BoundAtUtc;
                result.EvidencePackSnapshotHash = binding.PackSnapshotHash;
                result.EvidencePackBindingIntegrityHash = binding.IntegrityHash;
            }
            if (!readiness.IsReady)
                result.Block("SUPPLIER_EVIDENCE_NOT_READY",
                    readiness.BlockingReasons.Count == 0
                        ? "Supplier onboarding evidence is not current."
                        : string.Join("; ", readiness.BlockingReasons));
        }
        catch (ProcurementSupplierEvidencePackNotFoundException exception)
        {
            result.Block(exception.Code, exception.Message);
        }
        catch (ProcurementSupplierEvidencePackValidationException exception)
        {
            result.Block(exception.Code, exception.Message);
        }
    }

    private async Task AddPrequalificationAsync(
        SupplierEligibilityEvaluationRequest request,
        SupplierValidationResult result,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var entries = await _unitOfWork.Repository<ProcurementQualifiedListEntry>()
            .GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                item.BusinessPartnerId == request.BusinessPartnerId && !item.IsDeleted)
            .Include(item => item.Exercise)
            .Include(item => item.Application)
            .AsNoTracking()
            .OrderBy(item => item.CategoryId)
            .ThenByDescending(item => item.ExpiresAtUtc)
            .ToListAsync(cancellationToken);

        result.QualifiedListEntries = entries.Select(item => new SupplierEligibilityQualifiedListLineage
        {
            EntryId = item.Id,
            ExerciseId = item.ExerciseId,
            ExerciseReference = item.Exercise.Reference,
            ApplicationId = item.ApplicationId,
            ApplicationNumber = item.Application.ApplicationNumber,
            CategoryId = item.CategoryId,
            Status = item.Status.ToString(),
            ValidFromUtc = item.ValidFromUtc,
            ExpiresAtUtc = item.ExpiresAtUtc,
            IsCurrent = item.Status == ProcurementQualifiedListEntryStatus.Active &&
                item.ValidFromUtc <= now && item.ExpiresAtUtc > now,
            ApprovalReference = item.ApprovalReference,
            ApprovalEvidenceReference = item.ApprovalEvidenceReference,
            PolicySetId = item.Exercise.PolicySetId,
            PolicySetCode = item.Exercise.PolicySetCode,
            PolicySetVersion = item.Exercise.PolicySetVersion,
            SourceConfigurationProfileId = item.Exercise.SourceConfigurationProfileId
        }).ToList();

        if (!request.RequiresPrequalification) return;
        var current = result.QualifiedListEntries.Where(item => item.IsCurrent).ToList();
        if (request.CategoryIds.Count == 0 && current.Count == 0)
            result.Block("PREQUALIFICATION_REQUIRED", "No current approved qualified-list entry exists.");
        else
        {
            var missing = request.CategoryIds.Distinct()
                .Except(current.Select(item => item.CategoryId))
                .Order()
                .ToList();
            if (missing.Count > 0)
                result.Block("PREQUALIFICATION_CATEGORY_MISSING",
                    $"No current qualified-list entry exists for categories: {string.Join(", ", missing)}.");
        }
    }

    private async Task AddAvlPolicyLineageAsync(
        SupplierValidationResult result,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var matches = await _unitOfWork.Repository<ProcurementConfigurationDecision>()
            .GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                item.DecisionKey == "DEC-011" && !item.IsDeleted &&
                item.Profile.LifecycleStatus == ProcurementConfigurationProfileStatus.Published &&
                item.Profile.EffectiveFrom <= now &&
                (!item.Profile.EffectiveTo.HasValue || item.Profile.EffectiveTo.Value >= now) &&
                item.Status == ProcurementConfigurationDecisionStatus.Approved &&
                item.ApprovalStatus == ProcurementConfigurationApprovalStatus.Approved &&
                item.EvidenceStatus == ProcurementConfigurationEvidenceStatus.Verified &&
                (!item.EffectiveFrom.HasValue || item.EffectiveFrom.Value <= now) &&
                (!item.EffectiveTo.HasValue || item.EffectiveTo.Value >= now))
            .Include(item => item.Profile)
            .Include(item => item.EvidenceLinks)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        if (matches.Count == 1)
        {
            var policy = matches[0];
            result.AvlPolicyAvailable = true;
            result.AvlDecisionId = policy.Id;
            result.AvlProfileId = policy.ProfileId;
            result.AvlProfileCode = policy.Profile.ProfileCode;
            result.AvlProfileVersion = policy.Profile.Version;
            result.AvlDecisionDate = policy.DecisionDate;
            result.AvlEffectiveFrom = policy.EffectiveFrom;
            result.AvlEffectiveTo = policy.EffectiveTo;
            result.AvlApprovalReference = policy.ApprovalReference;
            result.AvlSourceLineage = policy.SourceLineage;
            result.AvlPolicyValueHash = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(policy.ValueJson)));
            result.AvlEvidenceReferences = policy.EvidenceLinks
                .Select(item => item.ExternalReference ?? item.Checksum ?? item.Id.ToString())
                .Order()
                .ToList();
        }
        else if (matches.Count == 0)
        {
            result.Warn("AVL_POLICY_UNPUBLISHED",
                "No unique Published/effective evidenced DEC-011 AVL policy is available; production AVL values remain a release configuration gate.");
        }
        else
        {
            result.Block("AVL_POLICY_AMBIGUOUS",
                "More than one Published/effective DEC-011 AVL policy applies.");
        }
    }

    private async Task AddDueDiligenceLineageAsync(
        SupplierValidationResult result,
        DateTime now,
        CancellationToken cancellationToken)
    {
        result.DueDiligencePolicyAvailable = result.AvlPolicyAvailable;
        if (!result.AvlPolicyAvailable)
            return;

        var reviews = await _unitOfWork.Repository<ProcurementSupplierDueDiligenceReview>()
            .GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                item.BusinessPartnerId == result.BusinessPartnerId && !item.IsDeleted &&
                item.Status == ProcurementSupplierDueDiligenceStatus.Approved)
            .Include(item => item.Checks.Where(check => !check.IsDeleted))
                .ThenInclude(check => check.EvidenceLinks.Where(link => !link.IsDeleted))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        if (reviews.Count == 0)
        {
            result.Block(
                "SUPPLIER_DUE_DILIGENCE_REQUIRED",
                "No approved supplier due-diligence or annual reassessment is available.");
            return;
        }

        if (reviews.Count > 1)
        {
            result.Block(
                "SUPPLIER_DUE_DILIGENCE_AMBIGUOUS",
                "More than one approved supplier due-diligence review is current in storage.");
            return;
        }

        var review = reviews[0];
        result.DueDiligenceReviewId = review.Id;
        result.DueDiligenceReviewReference = review.ReviewReference;
        result.DueDiligenceCycleNumber = review.CycleNumber;
        result.DueDiligenceReviewType = review.ReviewType;
        result.DueDiligenceStatus = review.Status;
        result.DueDiligenceOutcome = review.Outcome;
        result.DueDiligenceReviewPeriodStartUtc = review.ReviewPeriodStartUtc;
        result.DueDiligenceReviewPeriodEndUtc = review.ReviewPeriodEndUtc;
        result.DueDiligenceNextReviewDueAtUtc = review.NextReviewDueAtUtc;
        result.DueDiligencePolicyDecisionId = review.PolicyDecisionId;
        result.DueDiligencePolicyValueHash = review.PolicyValueHash;
        result.DueDiligenceWorkflowInstanceId = review.WorkflowInstanceId;
        result.DueDiligenceIntegrityHash = review.IntegrityHash;
        result.DueDiligenceChecks = review.Checks
            .OrderBy(item => item.CheckType)
            .Select(item => new SupplierEligibilityDueDiligenceCheckLineage
            {
                CheckId = item.Id,
                CheckType = item.CheckType,
                Status = item.Status,
                SourceName = item.SourceName,
                SourceReference = item.SourceReference,
                CheckedAtUtc = item.CheckedAtUtc,
                ValidUntilUtc = item.ValidUntilUtc,
                IntegrityHash = item.IntegrityHash,
                EvidenceCount = item.EvidenceLinks.Count(link => !link.IsDeleted),
                EvidenceIntegrityHashes = item.EvidenceLinks
                    .Where(link => !link.IsDeleted)
                    .Select(link => link.IntegrityHash)
                    .Order()
                    .ToList()
            })
            .ToList();

        var policyMatches = review.PolicyDecisionId == result.AvlDecisionId &&
            string.Equals(
                review.PolicyValueHash,
                result.AvlPolicyValueHash,
                StringComparison.OrdinalIgnoreCase);
        if (!policyMatches)
            result.Block(
                "SUPPLIER_DUE_DILIGENCE_POLICY_STALE",
                "The approved due-diligence review is not bound to the exact current DEC-011 policy.");

        if (review.Outcome == ProcurementSupplierDueDiligenceOutcome.Adverse)
            result.Block(
                "SUPPLIER_DUE_DILIGENCE_ADVERSE",
                "The approved due-diligence review contains an adverse outcome.");
        else if (review.Outcome != ProcurementSupplierDueDiligenceOutcome.Clear)
            result.Block(
                "SUPPLIER_DUE_DILIGENCE_OUTCOME_INVALID",
                "The approved due-diligence review does not contain a clear outcome.");

        var reviewDatesCurrent = review.ReviewPeriodStartUtc <= now &&
            review.ReviewPeriodEndUtc > now;
        if (!reviewDatesCurrent)
            result.Block(
                "SUPPLIER_DUE_DILIGENCE_EXPIRED",
                $"The approved due-diligence review expired on {review.ReviewPeriodEndUtc:yyyy-MM-dd}.");

        var requiredChecks = Enum.GetValues<ProcurementSupplierDueDiligenceCheckType>();
        var exactCheckSet = review.Checks.Count == requiredChecks.Length &&
            requiredChecks.All(type => review.Checks.Count(item => item.CheckType == type) == 1);
        if (!exactCheckSet)
            result.Block(
                "SUPPLIER_DUE_DILIGENCE_CHECK_SET_INVALID",
                "The approved review does not contain exactly one result for every required due-diligence check.");

        var checksCurrent = exactCheckSet;
        foreach (var check in review.Checks)
        {
            var evidenceCurrent = check.EvidenceLinks.Count(link => !link.IsDeleted) > 0 &&
                check.EvidenceLinks.Where(link => !link.IsDeleted)
                    .All(link => link.IntegrityHash.Length == 64);
            var resolved = check.Status == ProcurementSupplierDueDiligenceCheckStatus.NotApplicable
                ? !string.IsNullOrWhiteSpace(check.Notes)
                : check.Status == ProcurementSupplierDueDiligenceCheckStatus.Clear &&
                  check.CheckedAtUtc.HasValue && check.CheckedAtUtc.Value <= now &&
                  check.ValidUntilUtc.HasValue && check.ValidUntilUtc.Value > now;
            if (resolved && evidenceCurrent && check.IntegrityHash.Length == 64)
                continue;

            checksCurrent = false;
            var code = check.Status == ProcurementSupplierDueDiligenceCheckStatus.Adverse
                ? "SUPPLIER_DUE_DILIGENCE_CHECK_ADVERSE"
                : check.ValidUntilUtc.HasValue && check.ValidUntilUtc.Value <= now
                    ? "SUPPLIER_DUE_DILIGENCE_CHECK_EXPIRED"
                    : "SUPPLIER_DUE_DILIGENCE_CHECK_INCOMPLETE";
            result.Block(
                code,
                $"{check.CheckType} is not clear, current, and supported by retained evidence.");
        }

        var integrityCurrent = review.IntegrityHash.Length == 64 &&
            review.PolicyValueHash.Length == 64;
        if (!integrityCurrent)
            result.Block(
                "SUPPLIER_DUE_DILIGENCE_INTEGRITY_INVALID",
                "The approved due-diligence lineage does not contain valid integrity hashes.");

        result.DueDiligenceCurrent = policyMatches &&
            review.Outcome == ProcurementSupplierDueDiligenceOutcome.Clear &&
            reviewDatesCurrent &&
            checksCurrent &&
            integrityCurrent;
    }

    private async Task AddFormalAvlLineageAsync(
        SupplierValidationResult result,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (!result.AvlPolicyAvailable) return;
        var registers = await _unitOfWork.Repository<ProcurementSupplierAvlRegister>()
            .GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                !item.IsDeleted &&
                item.Status == ProcurementSupplierAvlRegisterStatus.Published &&
                item.EffectiveFromUtc <= now && item.ExpiresAtUtc > now &&
                (!item.ScheduledRetirementAtUtc.HasValue ||
                 item.ScheduledRetirementAtUtc.Value > now) &&
                item.PolicyDecisionId == result.AvlDecisionId &&
                item.PolicyValueHash == result.AvlPolicyValueHash)
            .Include(item => item.Entries.Where(entry =>
                !entry.IsDeleted && entry.BusinessPartnerId == result.BusinessPartnerId))
            .AsNoTracking().ToListAsync(cancellationToken);
        if (registers.Count == 0)
        {
            result.Block("AVL_REGISTER_REQUIRED",
                "No current Published AVL register exists for the effective DEC-011 policy.");
            return;
        }
        if (registers.Count > 1)
        {
            result.Block("AVL_REGISTER_AMBIGUOUS",
                "More than one current Published AVL register applies.");
            return;
        }
        var register = registers[0];
        result.AvlRegisterAvailable = true;
        result.AvlRegisterId = register.Id;
        result.AvlRegisterCode = register.RegisterCode;
        result.AvlRegisterVersion = register.Version;
        result.AvlRegisterEffectiveFromUtc = register.EffectiveFromUtc;
        result.AvlRegisterExpiresAtUtc = register.ExpiresAtUtc;
        result.AvlRegisterIntegrityHash = register.IntegrityHash;
        var entries = register.Entries.Where(item => !item.IsDeleted).ToList();
        if (entries.Count == 0)
        {
            result.Block("SUPPLIER_NOT_ON_AVL",
                "The supplier is not included in the current Published AVL register.");
            return;
        }
        if (entries.Count > 1)
        {
            result.Block("SUPPLIER_AVL_ENTRY_AMBIGUOUS",
                "More than one current AVL entry exists for the supplier.");
            return;
        }
        var entry = entries[0];
        result.AvlEntryId = entry.Id;
        result.AvlEntryStatus = entry.Status;
        result.AvlEntryIntegrityHash = entry.IntegrityHash;
        result.AvlEntryEligibilityDecisionHash = entry.EligibilityDecisionHash;
        result.FormalAvlCurrent = entry.Status == ProcurementSupplierAvlEntryStatus.Active;
        if (entry.Status == ProcurementSupplierAvlEntryStatus.Suspended)
            result.Block("SUPPLIER_AVL_SUSPENDED",
                "The supplier is suspended in the current Published AVL register.");
        else if (entry.Status == ProcurementSupplierAvlEntryStatus.Expired)
            result.Block("SUPPLIER_AVL_EXPIRED",
                "The supplier entry is expired in the current Published AVL register.");
    }

    private async Task AddRiskLineageAsync(
        SupplierValidationResult result,
        DateTime now,
        CancellationToken cancellationToken)
    {
        result.RiskPolicyAvailable = result.AvlPolicyAvailable;
        if (!result.AvlPolicyAvailable)
        {
            if (result.Boundary == SupplierEligibilityBoundary.Award)
                result.Block("SUPPLIER_RISK_POLICY_UNAVAILABLE",
                    "A unique valid effective DEC-011 supplier-risk policy is required before award.");
            else
                result.Warn("SUPPLIER_RISK_POLICY_UNAVAILABLE",
                    "Supplier risk cannot be evaluated until DEC-011 is Published, effective, approved, and evidenced.");
            return;
        }

        var assessment = await _unitOfWork.Repository<ProcurementSupplierRiskAssessment>()
            .GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                item.BusinessPartnerId == result.BusinessPartnerId && !item.IsDeleted)
            .Include(item => item.Alerts.Where(alert => !alert.IsDeleted))
            .AsNoTracking().OrderByDescending(item => item.AssessedAtUtc)
            .ThenByDescending(item => item.AssessmentSequence)
            .FirstOrDefaultAsync(cancellationToken);
        if (assessment is null)
        {
            if (result.Boundary == SupplierEligibilityBoundary.Award)
                result.Block("SUPPLIER_RISK_ASSESSMENT_REQUIRED",
                    "A current supplier-risk and concentration assessment is required before award.");
            else
                result.Warn("SUPPLIER_RISK_ASSESSMENT_REQUIRED",
                    "No supplier-risk and concentration assessment has been recorded.");
            return;
        }

        result.RiskAssessmentId = assessment.Id;
        result.RiskAssessmentReference = assessment.AssessmentReference;
        result.RiskAssessedAtUtc = assessment.AssessedAtUtc;
        result.RiskNextReviewDueAtUtc = assessment.NextReviewDueAtUtc;
        result.RiskScore = assessment.RiskScore;
        result.RiskBand = assessment.RiskBand;
        result.RiskEligibilityAction = assessment.EligibilityAction;
        result.MaximumSpendSharePercent = assessment.MaximumSpendSharePercent;
        result.ConcentrationLimitPercent = assessment.ConcentrationLimitPercent;
        result.SingleSourceCategoryCount = assessment.SingleSourceCategoryCount;
        result.RiskAssessmentIntegrityHash = assessment.IntegrityHash;
        result.RiskAssessmentPolicyValueHash = assessment.PolicyValueHash;
        result.OpenRiskAlertCount = assessment.Alerts.Count(item =>
            item.Status == ProcurementSupplierRiskAlertStatus.Open);
        result.EscalatedRiskAlertCount = assessment.Alerts.Count(item =>
            item.Status == ProcurementSupplierRiskAlertStatus.Escalated);

        var policyMatches = assessment.PolicyDecisionId == result.AvlDecisionId &&
            string.Equals(assessment.PolicyValueHash, result.AvlPolicyValueHash,
                StringComparison.OrdinalIgnoreCase);
        var current = policyMatches && assessment.NextReviewDueAtUtc > now &&
            assessment.DataComplete && assessment.IntegrityHash.Length == 64;
        result.RiskAssessmentCurrent = current;
        var hasBreach = assessment.MinimumScoreBreached ||
            assessment.ConcentrationBreached || assessment.SingleSourceDependency ||
            !assessment.DataComplete;
        var unresolvedAlerts = assessment.Alerts.Any(item =>
            item.Status != ProcurementSupplierRiskAlertStatus.Resolved);
        var policyBlocks = assessment.EligibilityAction ==
                ProcurementSupplierRiskEligibilityAction.AwardHardStop && hasBreach ||
            assessment.EligibilityAction ==
                ProcurementSupplierRiskEligibilityAction.EscalationRequired &&
            unresolvedAlerts;
        result.RiskAwardBlocked = result.Boundary == SupplierEligibilityBoundary.Award &&
            (!current || policyBlocks);

        if (!policyMatches)
            AddRiskFinding(result, "SUPPLIER_RISK_POLICY_STALE",
                "The latest risk assessment is not bound to the exact current DEC-011 policy.");
        else if (assessment.NextReviewDueAtUtc <= now)
            AddRiskFinding(result, "SUPPLIER_RISK_ASSESSMENT_EXPIRED",
                "The latest supplier-risk assessment has reached its policy-derived review date.");
        else if (!assessment.DataComplete)
            AddRiskFinding(result, "SUPPLIER_RISK_ASSESSMENT_INCOMPLETE",
                "The latest supplier-risk assessment is incomplete.");

        if (assessment.EligibilityAction ==
                ProcurementSupplierRiskEligibilityAction.AwardHardStop && hasBreach)
            AddRiskFinding(result, "SUPPLIER_RISK_AWARD_HARD_STOP",
                "DEC-011 applies an award hard stop to the current supplier-risk breach.");
        if (assessment.EligibilityAction ==
                ProcurementSupplierRiskEligibilityAction.EscalationRequired &&
            unresolvedAlerts)
            AddRiskFinding(result, "SUPPLIER_RISK_ESCALATION_REQUIRED",
                "DEC-011 requires all current supplier-risk alerts to complete escalation before award.");
        if (assessment.EligibilityAction ==
                ProcurementSupplierRiskEligibilityAction.AlertOnly && hasBreach)
            result.Warn("SUPPLIER_RISK_ALERT_ONLY",
                "DEC-011 records the current supplier-risk breach as alert-only.");

        void AddRiskFinding(
            SupplierValidationResult target,
            string code,
            string message)
        {
            if (target.Boundary == SupplierEligibilityBoundary.Award)
                target.Block(code, message);
            else
                target.Warn(code, message);
        }
    }

    private async Task AddPerformanceScorecardLineageAsync(
        SupplierValidationResult result,
        DateTime now,
        CancellationToken cancellationToken)
    {
        result.PerformanceScorecardPolicyAvailable = result.AvlPolicyAvailable;
        if (!result.AvlPolicyAvailable)
        {
            if (result.Boundary == SupplierEligibilityBoundary.Award)
                result.Block("SUPPLIER_PERFORMANCE_POLICY_UNAVAILABLE",
                    "A unique valid effective DEC-011 supplier-performance policy is required before award.");
            else
                result.Warn("SUPPLIER_PERFORMANCE_POLICY_UNAVAILABLE",
                    "Supplier performance cannot be evaluated until DEC-011 is Published, effective, approved, evidenced, and scorecard-complete.");
            return;
        }

        var scorecard = await _unitOfWork
            .Repository<ProcurementSupplierPerformanceScorecard>()
            .GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                item.BusinessPartnerId == result.BusinessPartnerId &&
                !item.IsDeleted)
            .AsNoTracking().OrderByDescending(item => item.CalculatedAtUtc)
            .ThenByDescending(item => item.ScorecardSequence)
            .FirstOrDefaultAsync(cancellationToken);
        if (scorecard is null)
        {
            AddPerformanceFinding(result,
                "SUPPLIER_PERFORMANCE_SCORECARD_REQUIRED",
                "A current governed supplier-performance scorecard is required.");
            return;
        }

        result.PerformanceScorecardId = scorecard.Id;
        result.PerformanceScorecardReference = scorecard.ScorecardReference;
        result.PerformanceCalculatedAtUtc = scorecard.CalculatedAtUtc;
        result.PerformanceNextReviewDueAtUtc = scorecard.NextReviewDueAtUtc;
        result.PerformanceOverallScore = scorecard.OverallScore;
        result.PerformanceBand = scorecard.PerformanceBand;
        result.PerformanceDataCoveragePercent = scorecard.DataCoveragePercent;
        result.PerformanceMinimumScore = scorecard.MinimumScore;
        result.PerformanceEligibilityAction = scorecard.EligibilityAction;
        result.PerformanceScorecardPolicyValueHash = scorecard.PolicyValueHash;
        result.PerformanceScorecardIntegrityHash = scorecard.IntegrityHash;
        result.PerformanceMinimumScoreBreached = scorecard.MinimumScoreBreached;
        var policyMatches = scorecard.PolicyDecisionId == result.AvlDecisionId &&
            string.Equals(scorecard.PolicyValueHash, result.AvlPolicyValueHash,
                StringComparison.OrdinalIgnoreCase);
        var current = policyMatches && scorecard.NextReviewDueAtUtc > now &&
            scorecard.DataStatus ==
            ProcurementSupplierPerformanceDataStatus.Complete &&
            scorecard.OverallScore.HasValue &&
            scorecard.IntegrityHash.Length == 64;
        var observedScores = new decimal?[]
        {
            scorecard.DeliveryTimelinessScore, scorecard.GrnQualityScore,
            scorecard.RejectionRateScore, scorecard.PriceCompetitivenessScore,
            scorecard.ResponsivenessScore, scorecard.ComplaintResolutionScore,
            scorecard.ContractCompletionScore
        };
        var firstAwardBaseline = policyMatches && scorecard.NextReviewDueAtUtc > now &&
            scorecard.PurchaseOrderCount == 0 && scorecard.ReceiptCount == 0 &&
            observedScores.Any(score => score.HasValue) &&
            observedScores.Where(score => score.HasValue)
                .All(score => score!.Value >= scorecard.MinimumScore) &&
            scorecard.IntegrityHash.Length == 64;
        result.PerformanceScorecardCurrent = current;
        var actionBlocks = scorecard.MinimumScoreBreached &&
            scorecard.EligibilityAction !=
            ProcurementSupplierRiskEligibilityAction.AlertOnly;
        result.PerformanceAwardBlocked =
            result.Boundary == SupplierEligibilityBoundary.Award &&
            ((!current && !firstAwardBaseline) || actionBlocks);

        if (!policyMatches)
            AddPerformanceFinding(result, "SUPPLIER_PERFORMANCE_POLICY_STALE",
                "The latest scorecard is not bound to the exact current DEC-011 policy.");
        else if (scorecard.NextReviewDueAtUtc <= now)
            AddPerformanceFinding(result, "SUPPLIER_PERFORMANCE_SCORECARD_EXPIRED",
                "The latest scorecard has reached its policy-derived review date.");
        else if (scorecard.DataStatus !=
                 ProcurementSupplierPerformanceDataStatus.Complete)
        {
            if (firstAwardBaseline)
                result.Warn("SUPPLIER_PERFORMANCE_FIRST_AWARD_BASELINE",
                    "The supplier has no prior purchase-order or receipt history; the governed baseline remains visible and performance hard-stop enforcement begins after the first award.");
            else
                AddPerformanceFinding(result,
                    "SUPPLIER_PERFORMANCE_COVERAGE_INSUFFICIENT",
                    "The latest scorecard does not meet the approved minimum data coverage.");
        }

        if (scorecard.MinimumScoreBreached &&
            scorecard.EligibilityAction ==
            ProcurementSupplierRiskEligibilityAction.AwardHardStop)
            AddPerformanceFinding(result,
                "SUPPLIER_PERFORMANCE_AWARD_HARD_STOP",
                "DEC-011 applies an award hard stop to the below-minimum supplier-performance score.");
        else if (scorecard.MinimumScoreBreached &&
                 scorecard.EligibilityAction ==
                 ProcurementSupplierRiskEligibilityAction.EscalationRequired)
            AddPerformanceFinding(result,
                "SUPPLIER_PERFORMANCE_ESCALATION_REQUIRED",
                "DEC-011 requires controlled remediation and a new compliant scorecard before award.");
        else if (scorecard.MinimumScoreBreached)
            result.Warn("SUPPLIER_PERFORMANCE_ALERT_ONLY",
                "DEC-011 records the below-minimum supplier-performance score as alert-only.");

        void AddPerformanceFinding(
            SupplierValidationResult target,
            string code,
            string message)
        {
            if (target.Boundary == SupplierEligibilityBoundary.Award)
                target.Block(code, message);
            else
                target.Warn(code, message);
        }
    }

    private async Task RecordDecisionAsync(
        SupplierEligibilityEvaluationRequest request,
        SupplierValidationResult result,
        CancellationToken cancellationToken)
    {
        var correlationId = string.IsNullOrWhiteSpace(request.CorrelationId)
            ? Guid.NewGuid().ToString("N")
            : request.CorrelationId.Trim();
        var sourceType = string.IsNullOrWhiteSpace(request.SourceType)
            ? "BusinessPartner"
            : request.SourceType.Trim();
        var sourceReference = string.IsNullOrWhiteSpace(request.SourceReference)
            ? result.PartnerCode
            : request.SourceReference.Trim();
        var eventKey =
            $"SUPPLIER-ELIGIBILITY:{correlationId}:{request.Boundary}:{result.BusinessPartnerId:N}:{result.DecisionHash[..16]}";
        var existingDecision = await _unitOfWork.Repository<ProcurementControlEvent>()
            .GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.EventKey == eventKey &&
                item.CorrelationId == correlationId &&
                !item.IsDeleted)
            .AsNoTracking()
            .Select(item => item.ResultValuesJson)
            .SingleOrDefaultAsync(cancellationToken);
        if (existingDecision?.Contains(result.DecisionHash, StringComparison.Ordinal) == true)
            return;

        await _controlEvents.RecordAsync(new ProcurementControlEventWriteRequest
        {
            EventKey = eventKey,
            EventType = "SupplierEligibility",
            Action = request.Boundary.ToString(),
            Result = result.IsValid ? ProcurementControlEventResult.Allowed : ProcurementControlEventResult.Denied,
            RuleCode = result.ValidationCode,
            DecisionKeys = DecisionKeys.ToList(),
            SourceType = sourceType,
            SourceId = request.SourceId ?? result.BusinessPartnerId,
            SourceReference = sourceReference,
            Reason = result.IsValid ? "Current supplier eligibility checks passed." : string.Join("; ", result.Errors),
            InputValues = new
            {
                request.BusinessPartnerId,
                Boundary = request.Boundary.ToString(),
                request.CategoryIds,
                request.RequiresPrequalification,
                request.RequiresLicenses,
                request.MinimumPerformanceRating
            },
            ResultValues = new
            {
                result.IsValid,
                result.ValidationCode,
                result.DecisionHash,
                result.RegistrationId,
                result.EvidencePackVersionId,
                result.AvlDecisionId,
                result.AvlRegisterId,
                result.AvlEntryId,
                result.FormalAvlCurrent,
                result.DueDiligenceReviewId,
                result.DueDiligenceCurrent,
                result.PerformanceScorecardId,
                result.PerformanceScorecardCurrent,
                result.PerformanceAwardBlocked,
                result.RiskAssessmentId,
                result.RiskAssessmentCurrent,
                result.RiskAwardBlocked,
                QualifiedListEntryIds = result.QualifiedListEntries.Select(item => item.EntryId).ToList(),
                Findings = result.Findings
            },
            CorrelationId = correlationId,
            OccurredAtUtc = result.EvaluatedAtUtc
        }, cancellationToken);
    }

    private static string ComputeDecisionHash(SupplierValidationResult result)
    {
        var canonical = JsonSerializer.Serialize(new
        {
            result.TenantId,
            result.BusinessPartnerId,
            Boundary = result.Boundary.ToString(),
            result.ApprovalStatus,
            result.RegistrationStatus,
            result.IsActive,
            result.IsBlacklisted,
            result.BlacklistReason,
            result.BlacklistDate,
            result.BlacklistExpiryDate,
            result.CategoryIds,
            result.RequiredCategoryIds,
            result.RegistrationId,
            result.EvidencePackVersionId,
            result.EvidenceReady,
            result.EvidencePackSnapshotHash,
            result.EvidencePackBindingIntegrityHash,
            QualifiedEntries = result.QualifiedListEntries.Select(item => new
            {
                item.EntryId,
                item.CategoryId,
                item.Status,
                item.ValidFromUtc,
                item.ExpiresAtUtc,
                item.PolicySetId,
                item.SourceConfigurationProfileId
            }),
            result.AvlDecisionId,
            result.AvlPolicyValueHash,
            result.AvlSourceLineage,
            result.AvlRegisterAvailable,
            result.AvlRegisterId,
            result.AvlRegisterCode,
            result.AvlRegisterVersion,
            result.AvlRegisterEffectiveFromUtc,
            result.AvlRegisterExpiresAtUtc,
            result.AvlRegisterIntegrityHash,
            result.AvlEntryId,
            result.AvlEntryStatus,
            result.AvlEntryIntegrityHash,
            result.AvlEntryEligibilityDecisionHash,
            result.FormalAvlCurrent,
            result.DueDiligenceReviewId,
            result.DueDiligenceReviewReference,
            result.DueDiligenceCycleNumber,
            result.DueDiligenceReviewType,
            result.DueDiligenceStatus,
            result.DueDiligenceOutcome,
            result.DueDiligenceReviewPeriodStartUtc,
            result.DueDiligenceReviewPeriodEndUtc,
            result.DueDiligencePolicyDecisionId,
            result.DueDiligencePolicyValueHash,
            result.DueDiligenceWorkflowInstanceId,
            result.DueDiligenceIntegrityHash,
            result.DueDiligenceCurrent,
            result.PerformanceScorecardId,
            result.PerformanceScorecardReference,
            result.PerformanceCalculatedAtUtc,
            result.PerformanceNextReviewDueAtUtc,
            result.PerformanceOverallScore,
            result.PerformanceBand,
            result.PerformanceDataCoveragePercent,
            result.PerformanceMinimumScore,
            result.PerformanceEligibilityAction,
            result.PerformanceMinimumScoreBreached,
            result.PerformanceScorecardPolicyValueHash,
            result.PerformanceScorecardIntegrityHash,
            result.PerformanceScorecardCurrent,
            result.PerformanceAwardBlocked,
            result.RiskAssessmentId,
            result.RiskAssessmentReference,
            result.RiskAssessedAtUtc,
            result.RiskNextReviewDueAtUtc,
            result.RiskScore,
            result.RiskBand,
            result.RiskEligibilityAction,
            result.MaximumSpendSharePercent,
            result.ConcentrationLimitPercent,
            result.SingleSourceCategoryCount,
            result.OpenRiskAlertCount,
            result.EscalatedRiskAlertCount,
            result.RiskAssessmentPolicyValueHash,
            result.RiskAssessmentIntegrityHash,
            result.RiskAssessmentCurrent,
            result.RiskAwardBlocked,
            DueDiligenceChecks = result.DueDiligenceChecks.Select(item => new
            {
                item.CheckId,
                item.CheckType,
                item.Status,
                item.SourceName,
                item.SourceReference,
                item.CheckedAtUtc,
                item.ValidUntilUtc,
                item.IntegrityHash,
                item.EvidenceCount,
                item.EvidenceIntegrityHashes
            }),
            result.Findings
        }, JsonOptions);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}

/// <summary>
/// Result of supplier validation
/// </summary>
public class SupplierValidationResult
{
    public bool IsValid { get; set; }
    public string ValidationCode { get; set; } = "OK";
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public Guid BusinessPartnerId { get; set; }
    public Guid TenantId { get; set; }
    public string PartnerCode { get; set; } = string.Empty;
    public string PartnerName { get; set; } = string.Empty;
    public string PartnerType { get; set; } = string.Empty;
    public SupplierEligibilityBoundary Boundary { get; set; }
    public DateTime EvaluatedAtUtc { get; set; }
    public string? ApprovalStatus { get; set; }
    public string RegistrationStatus { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsBlacklisted { get; set; }
    public string? BlacklistReason { get; set; }
    public DateTime? BlacklistDate { get; set; }
    public DateTime? BlacklistExpiryDate { get; set; }
    public decimal? PerformanceRating { get; set; }
    public string? RiskLevel { get; set; }
    public string? CreditRating { get; set; }
    public List<Guid> CategoryIds { get; set; } = new();
    public List<SupplierEligibilityCategoryLineage> Categories { get; set; } = new();
    public List<Guid> RequiredCategoryIds { get; set; } = new();
    public List<SupplierEligibilityLicenseLineage> Licenses { get; set; } = new();
    public Guid? RegistrationId { get; set; }
    public string? RegistrationNumber { get; set; }
    public string? RegistrationApplicationStatus { get; set; }
    public Guid? EvidencePackVersionId { get; set; }
    public string? EvidencePackCode { get; set; }
    public int? EvidencePackVersion { get; set; }
    public bool? EvidenceReady { get; set; }
    public DateTime? EvidencePackBoundAtUtc { get; set; }
    public string? EvidencePackSnapshotHash { get; set; }
    public string? EvidencePackBindingIntegrityHash { get; set; }
    public List<string> EvidenceBlockingReasons { get; set; } = new();
    public List<SupplierEligibilityQualifiedListLineage> QualifiedListEntries { get; set; } = new();
    public bool AvlPolicyAvailable { get; set; }
    public Guid? AvlDecisionId { get; set; }
    public Guid? AvlProfileId { get; set; }
    public string? AvlProfileCode { get; set; }
    public int? AvlProfileVersion { get; set; }
    public DateTime? AvlDecisionDate { get; set; }
    public DateTime? AvlEffectiveFrom { get; set; }
    public DateTime? AvlEffectiveTo { get; set; }
    public string? AvlApprovalReference { get; set; }
    public string? AvlSourceLineage { get; set; }
    public string? AvlPolicyValueHash { get; set; }
    public List<string> AvlEvidenceReferences { get; set; } = new();
    public bool AvlRegisterAvailable { get; set; }
    public Guid? AvlRegisterId { get; set; }
    public string? AvlRegisterCode { get; set; }
    public int? AvlRegisterVersion { get; set; }
    public DateTime? AvlRegisterEffectiveFromUtc { get; set; }
    public DateTime? AvlRegisterExpiresAtUtc { get; set; }
    public string? AvlRegisterIntegrityHash { get; set; }
    public Guid? AvlEntryId { get; set; }
    public ProcurementSupplierAvlEntryStatus? AvlEntryStatus { get; set; }
    public string? AvlEntryIntegrityHash { get; set; }
    public string? AvlEntryEligibilityDecisionHash { get; set; }
    public bool FormalAvlCurrent { get; set; }
    public bool DueDiligencePolicyAvailable { get; set; }
    public Guid? DueDiligenceReviewId { get; set; }
    public string? DueDiligenceReviewReference { get; set; }
    public int? DueDiligenceCycleNumber { get; set; }
    public ProcurementSupplierDueDiligenceReviewType? DueDiligenceReviewType { get; set; }
    public ProcurementSupplierDueDiligenceStatus? DueDiligenceStatus { get; set; }
    public ProcurementSupplierDueDiligenceOutcome? DueDiligenceOutcome { get; set; }
    public DateTime? DueDiligenceReviewPeriodStartUtc { get; set; }
    public DateTime? DueDiligenceReviewPeriodEndUtc { get; set; }
    public DateTime? DueDiligenceNextReviewDueAtUtc { get; set; }
    public Guid? DueDiligencePolicyDecisionId { get; set; }
    public string? DueDiligencePolicyValueHash { get; set; }
    public Guid? DueDiligenceWorkflowInstanceId { get; set; }
    public string? DueDiligenceIntegrityHash { get; set; }
    public bool DueDiligenceCurrent { get; set; }
    public List<SupplierEligibilityDueDiligenceCheckLineage> DueDiligenceChecks { get; set; } = new();
    public bool PerformanceScorecardPolicyAvailable { get; set; }
    public Guid? PerformanceScorecardId { get; set; }
    public string? PerformanceScorecardReference { get; set; }
    public DateTime? PerformanceCalculatedAtUtc { get; set; }
    public DateTime? PerformanceNextReviewDueAtUtc { get; set; }
    public decimal? PerformanceOverallScore { get; set; }
    public string? PerformanceBand { get; set; }
    public decimal? PerformanceDataCoveragePercent { get; set; }
    public decimal? PerformanceMinimumScore { get; set; }
    public ProcurementSupplierRiskEligibilityAction? PerformanceEligibilityAction { get; set; }
    public bool PerformanceMinimumScoreBreached { get; set; }
    public string? PerformanceScorecardPolicyValueHash { get; set; }
    public string? PerformanceScorecardIntegrityHash { get; set; }
    public bool PerformanceScorecardCurrent { get; set; }
    public bool PerformanceAwardBlocked { get; set; }
    public bool RiskPolicyAvailable { get; set; }
    public Guid? RiskAssessmentId { get; set; }
    public string? RiskAssessmentReference { get; set; }
    public DateTime? RiskAssessedAtUtc { get; set; }
    public DateTime? RiskNextReviewDueAtUtc { get; set; }
    public decimal? RiskScore { get; set; }
    public string? RiskBand { get; set; }
    public ProcurementSupplierRiskEligibilityAction? RiskEligibilityAction { get; set; }
    public decimal? MaximumSpendSharePercent { get; set; }
    public decimal? ConcentrationLimitPercent { get; set; }
    public int SingleSourceCategoryCount { get; set; }
    public int OpenRiskAlertCount { get; set; }
    public int EscalatedRiskAlertCount { get; set; }
    public string? RiskAssessmentPolicyValueHash { get; set; }
    public string? RiskAssessmentIntegrityHash { get; set; }
    public bool RiskAssessmentCurrent { get; set; }
    public bool RiskAwardBlocked { get; set; }
    public List<string> DecisionKeys { get; set; } = new();
    public List<SupplierEligibilityFinding> Findings { get; set; } = new();
    public string DecisionHash { get; set; } = string.Empty;

    public static SupplierValidationResult Success()
    {
        return new SupplierValidationResult { IsValid = true };
    }

    public static SupplierValidationResult Fail(string error, string code = "VALIDATION_FAILED")
    {
        return new SupplierValidationResult
        {
            IsValid = false,
            ValidationCode = code,
            Errors = new List<string> { error }
        };
    }

    public void Block(string code, string message) =>
        Findings.Add(new SupplierEligibilityFinding { Code = code, Message = message, Blocking = true });

    public void Warn(string code, string message) =>
        Findings.Add(new SupplierEligibilityFinding { Code = code, Message = message, Blocking = false });
}

public enum SupplierEligibilityBoundary
{
    StatusReview = 0,
    Invitation = 1,
    Award = 2,
    Contract = 3,
    ManualPurchaseOrder = 4,
    FrameworkCallOff = 5
}

public sealed class SupplierEligibilityEvaluationRequest
{
    public Guid BusinessPartnerId { get; set; }
    public SupplierEligibilityBoundary Boundary { get; set; }
    public List<Guid> CategoryIds { get; set; } = new();
    public bool RequiresPrequalification { get; set; }
    public bool RequiresLicenses { get; set; }
    public bool IncludeFinancialWarnings { get; set; }
    public decimal? MinimumPerformanceRating { get; set; }
    public bool RecordAudit { get; set; }
    public string? SourceType { get; set; }
    public Guid? SourceId { get; set; }
    public string? SourceReference { get; set; }
    public string? CorrelationId { get; set; }
    public bool SkipFormalAvlMembership { get; set; }
    public bool SkipPerformanceScorecard { get; set; }
    public bool SkipRiskAssessment { get; set; }
}

public sealed class SupplierEligibilityFinding
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool Blocking { get; set; }
}

public sealed class SupplierEligibilityCategoryLineage
{
    public Guid CategoryId { get; set; }
    public string? CategoryCode { get; set; }
    public string? CategoryName { get; set; }
    public bool IsPrimary { get; set; }
}

public sealed class SupplierEligibilityLicenseLineage
{
    public Guid LicenseId { get; set; }
    public Guid LicenseTypeId { get; set; }
    public string? LicenseCode { get; set; }
    public string LicenseNumber { get; set; } = string.Empty;
    public bool IsMandatory { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? ExpiryDate { get; set; }
    public bool IsCurrent { get; set; }
}

public sealed class SupplierEligibilityQualifiedListLineage
{
    public Guid EntryId { get; set; }
    public Guid ExerciseId { get; set; }
    public string ExerciseReference { get; set; } = string.Empty;
    public Guid ApplicationId { get; set; }
    public string ApplicationNumber { get; set; } = string.Empty;
    public Guid CategoryId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime ValidFromUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public bool IsCurrent { get; set; }
    public string ApprovalReference { get; set; } = string.Empty;
    public string ApprovalEvidenceReference { get; set; } = string.Empty;
    public Guid PolicySetId { get; set; }
    public string PolicySetCode { get; set; } = string.Empty;
    public int PolicySetVersion { get; set; }
    public Guid SourceConfigurationProfileId { get; set; }
}

public sealed class SupplierEligibilityDueDiligenceCheckLineage
{
    public Guid CheckId { get; set; }
    public ProcurementSupplierDueDiligenceCheckType CheckType { get; set; }
    public ProcurementSupplierDueDiligenceCheckStatus Status { get; set; }
    public string SourceName { get; set; } = string.Empty;
    public string SourceReference { get; set; } = string.Empty;
    public DateTime? CheckedAtUtc { get; set; }
    public DateTime? ValidUntilUtc { get; set; }
    public string IntegrityHash { get; set; } = string.Empty;
    public int EvidenceCount { get; set; }
    public List<string> EvidenceIntegrityHashes { get; set; } = new();
}

public sealed class SupplierEligibilityException(
    string code,
    string message,
    SupplierValidationResult result) : InvalidOperationException(message)
{
    public string Code { get; } = code;
    public SupplierValidationResult Result { get; } = result;
}

public sealed class SupplierEligibilityAuthorizationException(string message) :
    UnauthorizedAccessException(message);

/// <summary>
/// Service interface for supplier validation
/// </summary>
public interface ISupplierValidationService
{
    Task<SupplierValidationResult> ValidateForPurchaseOrderAsync(Guid businessPartnerId);
    Task<SupplierValidationResult> ValidateForRfqAsync(Guid businessPartnerId, List<Guid>? categoryIds = null, decimal? minimumPerformanceRating = null);
    Task<SupplierValidationResult> ValidateForContractAsync(Guid businessPartnerId, bool requiresLicenses = false);
    Task<SupplierValidationResult> ValidateFinancialHealthAsync(Guid businessPartnerId, decimal? minimumCreditRatingScore = null);
    Task<SupplierValidationResult> ValidateForTenderAsync(Guid businessPartnerId, bool requiresPrequalification, decimal? minimumPerformanceRating = null);
    Task<SupplierValidationResult> ValidateForFrameworkCallOffAsync(Guid businessPartnerId);
    Task<SupplierValidationResult> EvaluateEligibilityAsync(SupplierEligibilityEvaluationRequest request, CancellationToken cancellationToken = default);
    Task<SupplierValidationResult> EnforceEligibilityAsync(SupplierEligibilityEvaluationRequest request, CancellationToken cancellationToken = default);
}
