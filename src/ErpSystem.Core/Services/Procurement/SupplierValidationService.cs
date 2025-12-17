using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Procurement;

/// <summary>
/// Centralized service for validating supplier/contractor eligibility for procurement activities
/// </summary>
public class SupplierValidationService : ISupplierValidationService
{
    private readonly IBusinessPartnerRepository _partnerRepository;
    private readonly ILogger<SupplierValidationService> _logger;

    public SupplierValidationService(
        IBusinessPartnerRepository partnerRepository,
        ILogger<SupplierValidationService> logger)
    {
        _partnerRepository = partnerRepository;
        _logger = logger;
    }

    public async Task<SupplierValidationResult> ValidateForPurchaseOrderAsync(Guid businessPartnerId)
    {
        _logger.LogInformation("Validating business partner {PartnerId} for purchase order", businessPartnerId);

        var partner = await _partnerRepository.GetByIdAsync(businessPartnerId);
        if (partner == null)
        {
            return SupplierValidationResult.Fail("Business partner not found");
        }

        var result = new SupplierValidationResult { IsValid = true };

        // Check if blacklisted
        if (partner.IsBlacklisted)
        {
            result.IsValid = false;
            result.Errors.Add($"Supplier is blacklisted. Reason: {partner.BlacklistReason}");
            if (partner.BlacklistExpiryDate.HasValue)
            {
                result.Errors.Add($"Blacklist expires on: {partner.BlacklistExpiryDate.Value:yyyy-MM-dd}");
            }
            result.ValidationCode = "BLACKLISTED";
        }

        // Check if approved
        if (partner.ApprovalStatus != "Approved")
        {
            result.IsValid = false;
            result.Errors.Add($"Supplier is not approved. Current status: {partner.ApprovalStatus}");
            result.ValidationCode = "NOT_APPROVED";
        }

        // Check if active
        if (!partner.IsActive)
        {
            result.IsValid = false;
            result.Errors.Add("Supplier is not active");
            result.ValidationCode = "INACTIVE";
        }

        // Check registration status
        if (partner.RegistrationStatus != "Approved")
        {
            result.IsValid = false;
            result.Errors.Add($"Supplier registration is not approved. Current status: {partner.RegistrationStatus}");
            result.ValidationCode = "REGISTRATION_NOT_APPROVED";
        }

        _logger.LogInformation("Validation result for {PartnerId}: {IsValid}", businessPartnerId, result.IsValid);
        return result;
    }

    public async Task<SupplierValidationResult> ValidateForRfqAsync(Guid businessPartnerId, List<Guid>? categoryIds = null, decimal? minimumPerformanceRating = null)
    {
        _logger.LogInformation("Validating business partner {PartnerId} for RFQ", businessPartnerId);

        var partner = await _partnerRepository.GetByIdAsync(businessPartnerId);
        if (partner == null)
        {
            return SupplierValidationResult.Fail("Business partner not found");
        }

        var result = new SupplierValidationResult { IsValid = true };

        // Check if blacklisted
        if (partner.IsBlacklisted)
        {
            result.IsValid = false;
            result.Errors.Add($"Supplier is blacklisted. Reason: {partner.BlacklistReason}");
            result.ValidationCode = "BLACKLISTED";
        }

        // Check if approved
        if (partner.ApprovalStatus != "Approved")
        {
            result.IsValid = false;
            result.Errors.Add($"Supplier is not approved. Current status: {partner.ApprovalStatus}");
            result.ValidationCode = "NOT_APPROVED";
        }

        // Check if active
        if (!partner.IsActive)
        {
            result.IsValid = false;
            result.Errors.Add("Supplier is not active");
            result.ValidationCode = "INACTIVE";
        }

        // Check performance rating threshold
        if (minimumPerformanceRating.HasValue && partner.PerformanceRating.HasValue)
        {
            if (partner.PerformanceRating.Value < minimumPerformanceRating.Value)
            {
                result.IsValid = false;
                result.Errors.Add($"Supplier performance rating ({partner.PerformanceRating.Value:F2}) is below minimum threshold ({minimumPerformanceRating.Value:F2})");
                result.ValidationCode = "LOW_PERFORMANCE";
            }
        }

        // Check category qualification (if categories provided)
        if (categoryIds != null && categoryIds.Any())
        {
            var partnerCategories = partner.Categories.Select(c => c.CategoryId).ToList();
            var missingCategories = categoryIds.Except(partnerCategories).ToList();
            
            if (missingCategories.Any())
            {
                result.IsValid = false;
                result.Errors.Add($"Supplier is not qualified for {missingCategories.Count} required categories");
                result.ValidationCode = "CATEGORY_NOT_QUALIFIED";
            }
        }

        _logger.LogInformation("RFQ validation result for {PartnerId}: {IsValid}", businessPartnerId, result.IsValid);
        return result;
    }

    public async Task<SupplierValidationResult> ValidateForContractAsync(Guid businessPartnerId, bool requiresLicenses = false)
    {
        _logger.LogInformation("Validating business partner {PartnerId} for contract", businessPartnerId);

        var partner = await _partnerRepository.GetByIdAsync(businessPartnerId);
        if (partner == null)
        {
            return SupplierValidationResult.Fail("Business partner not found");
        }

        var result = new SupplierValidationResult { IsValid = true };

        // Check if blacklisted
        if (partner.IsBlacklisted)
        {
            result.IsValid = false;
            result.Errors.Add($"Supplier is blacklisted. Reason: {partner.BlacklistReason}");
            result.ValidationCode = "BLACKLISTED";
        }

        // Check if approved
        if (partner.ApprovalStatus != "Approved")
        {
            result.IsValid = false;
            result.Errors.Add($"Supplier is not approved. Current status: {partner.ApprovalStatus}");
            result.ValidationCode = "NOT_APPROVED";
        }

        // Check if active
        if (!partner.IsActive)
        {
            result.IsValid = false;
            result.Errors.Add("Supplier is not active");
            result.ValidationCode = "INACTIVE";
        }

        // Check licenses if required (for contractors)
        if (requiresLicenses && partner.PartnerType.Contains("Contractor"))
        {
            var activeLicenses = partner.Licenses.Where(l => l.Status == "Active").ToList();
            var expiredLicenses = activeLicenses.Where(l => l.ExpiryDate.HasValue && l.ExpiryDate.Value < DateTime.UtcNow).ToList();

            if (expiredLicenses.Any())
            {
                result.IsValid = false;
                result.Errors.Add($"{expiredLicenses.Count} required licenses have expired");
                result.ValidationCode = "EXPIRED_LICENSES";
            }

            // Check for mandatory licenses (IsMandatory is on LicenseType, not BusinessPartnerLicense)
            var mandatoryLicenses = partner.Licenses.Where(l => l.LicenseType != null && l.LicenseType.IsMandatory).ToList();
            var missingMandatory = mandatoryLicenses.Where(l => l.Status != "Active" || (l.ExpiryDate.HasValue && l.ExpiryDate.Value < DateTime.UtcNow)).ToList();

            if (missingMandatory.Any())
            {
                result.IsValid = false;
                result.Errors.Add($"{missingMandatory.Count} mandatory licenses are missing or expired");
                result.ValidationCode = "MISSING_MANDATORY_LICENSES";
            }
        }

        _logger.LogInformation("Contract validation result for {PartnerId}: {IsValid}", businessPartnerId, result.IsValid);
        return result;
    }

    public async Task<SupplierValidationResult> ValidateFinancialHealthAsync(Guid businessPartnerId, decimal? minimumCreditRatingScore = null)
    {
        _logger.LogInformation("Validating financial health for business partner {PartnerId}", businessPartnerId);

        var partner = await _partnerRepository.GetByIdAsync(businessPartnerId);
        if (partner == null)
        {
            return SupplierValidationResult.Fail("Business partner not found");
        }

        var result = new SupplierValidationResult { IsValid = true };

        // Check credit rating
        if (!string.IsNullOrEmpty(partner.CreditRating))
        {
            var riskRatings = new[] { "D", "C-", "C", "C+" };
            if (riskRatings.Contains(partner.CreditRating))
            {
                result.Warnings.Add($"Supplier has a low credit rating: {partner.CreditRating}");
            }
        }

        // Check risk level
        if (partner.RiskLevel == "High" || partner.RiskLevel == "Critical")
        {
            result.Warnings.Add($"Supplier has {partner.RiskLevel} risk level");
        }

        // Check insurance coverage for contractors
        if (partner.PartnerType.Contains("Contractor") && !partner.InsuranceCoverage.HasValue)
        {
            result.Warnings.Add("Contractor does not have insurance coverage information on file");
        }

        _logger.LogInformation("Financial health validation for {PartnerId}: {WarningCount} warnings", businessPartnerId, result.Warnings.Count);
        return result;
    }

    public async Task<SupplierValidationResult> ValidateForTenderAsync(Guid businessPartnerId, bool requiresPrequalification, decimal? minimumPerformanceRating = null)
    {
        _logger.LogInformation("Validating business partner {PartnerId} for tender (Prequalification: {RequiresPrequalification}, MinRating: {MinRating})",
            businessPartnerId, requiresPrequalification, minimumPerformanceRating);

        var partner = await _partnerRepository.GetByIdAsync(businessPartnerId);
        if (partner == null)
        {
            return SupplierValidationResult.Fail("Business partner not found");
        }

        var result = new SupplierValidationResult { IsValid = true };

        // Check if blacklisted
        if (partner.IsBlacklisted)
        {
            result.IsValid = false;
            result.Errors.Add($"Your organization is blacklisted. Reason: {partner.BlacklistReason}");
            if (partner.BlacklistExpiryDate.HasValue)
            {
                result.Errors.Add($"Blacklist expires on: {partner.BlacklistExpiryDate.Value:yyyy-MM-dd}");
            }
            result.ValidationCode = "BLACKLISTED";
        }

        // Check if approved
        if (partner.ApprovalStatus != "Approved")
        {
            result.IsValid = false;
            result.Errors.Add($"Your organization is not approved. Current status: {partner.ApprovalStatus ?? "Pending"}");
            result.ValidationCode = "NOT_APPROVED";
        }

        // Check if active
        if (!partner.IsActive)
        {
            result.IsValid = false;
            result.Errors.Add("Your organization account is not active");
            result.ValidationCode = "INACTIVE";
        }

        // Check registration status
        if (partner.RegistrationStatus != "Approved")
        {
            result.IsValid = false;
            result.Errors.Add($"Your organization registration is not approved. Current status: {partner.RegistrationStatus}");
            result.ValidationCode = "REGISTRATION_NOT_APPROVED";
        }

        // Check prequalification requirement
        if (requiresPrequalification)
        {
            // For now, we check if the partner has been approved and has a performance rating
            // In the future, this could check a dedicated prequalification status field
            if (!partner.PerformanceRating.HasValue)
            {
                result.IsValid = false;
                result.Errors.Add("This tender requires prequalification. Your organization does not have a performance rating on record");
                result.ValidationCode = "NOT_PREQUALIFIED";
            }
        }

        // Check performance rating threshold
        if (minimumPerformanceRating.HasValue)
        {
            if (!partner.PerformanceRating.HasValue)
            {
                result.IsValid = false;
                result.Errors.Add($"This tender requires a minimum performance rating of {minimumPerformanceRating.Value:F2}. Your organization does not have a performance rating on record");
                result.ValidationCode = "NO_PERFORMANCE_RATING";
            }
            else if (partner.PerformanceRating.Value < minimumPerformanceRating.Value)
            {
                result.IsValid = false;
                result.Errors.Add($"Your organization's performance rating ({partner.PerformanceRating.Value:F2}) is below the minimum required ({minimumPerformanceRating.Value:F2})");
                result.ValidationCode = "LOW_PERFORMANCE";
            }
        }

        _logger.LogInformation("Tender validation result for {PartnerId}: {IsValid}", businessPartnerId, result.IsValid);
        return result;
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
}

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
}

