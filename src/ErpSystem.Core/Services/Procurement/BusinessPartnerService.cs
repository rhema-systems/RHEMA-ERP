using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Procurement;

public class BusinessPartnerService : IBusinessPartnerService
{
    private readonly IBusinessPartnerRepository _partnerRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IWorkflowIntegrationService _workflowIntegrationService;
    private readonly IWorkflowStatusAdapterRegistry _workflowStatusAdapterRegistry;
    private readonly ILogger<BusinessPartnerService> _logger;

    public BusinessPartnerService(
        IBusinessPartnerRepository partnerRepository,
        ICurrentUserProvider currentUserProvider,
        IWorkflowIntegrationService workflowIntegrationService,
        IWorkflowStatusAdapterRegistry workflowStatusAdapterRegistry,
        ILogger<BusinessPartnerService> logger)
    {
        _partnerRepository = partnerRepository;
        _currentUserProvider = currentUserProvider;
        _workflowIntegrationService = workflowIntegrationService;
        _workflowStatusAdapterRegistry = workflowStatusAdapterRegistry;
        _logger = logger;
    }

    public async Task<BusinessPartnerDetailDto?> GetByIdAsync(Guid id)
    {
        var partner = await _partnerRepository.GetWithAllRelatedDataAsync(id);
        if (partner == null)
        {
            return null;
        }

        return MapToDetailDto(partner);
    }

    public async Task<BusinessPartnerDto?> GetByCodeAsync(string partnerCode)
    {
        var partner = await _partnerRepository.GetByCodeAsync(partnerCode);
        if (partner == null)
        {
            return null;
        }

        return MapToDto(partner);
    }

    public async Task<BusinessPartnerDetailDto?> GetByUserIdAsync(Guid userId)
    {
        var partner = await _partnerRepository.GetByUserIdAsync(userId);
        if (partner == null)
        {
            return null;
        }

        return MapToDetailDto(partner);
    }

    public async Task<BusinessPartnerDetailDto> CreateAsync(CreateBusinessPartnerDto dto)
    {
        var partnerCode = await _partnerRepository.GeneratePartnerCodeAsync(dto.PartnerType);

        var partner = new BusinessPartner
        {
            Id = Guid.NewGuid(),
            TenantId = _currentUserProvider.TenantId,
            PartnerCode = partnerCode,
            PartnerName = dto.PartnerName,
            PartnerType = dto.PartnerType,
            LegalName = dto.PartnerName,
            BusinessRegistrationNumber = dto.RegistrationNumber,
            TaxIdentificationNumber = dto.TaxNumber,
            VATNumber = dto.TaxNumber, // Using TaxNumber as VATNumber for now
            PrimaryEmail = dto.Email,
            PrimaryPhone = dto.Phone,
            Website = dto.Website,
            PhysicalAddress = dto.PhysicalAddress,
            PhysicalCity = dto.City,
            PhysicalCountry = dto.Country,
            PhysicalPostalCode = dto.PostalCode,
            // Created internally but not usable in transactions until approved.
            RegistrationStatus = "PendingApproval",
            ApprovalStatus = "Pending",
            IsPreferred = false,
            IsBlacklisted = false,
            IsActive = false,
            CreatedAt = DateTime.UtcNow,
            CreatedById = _currentUserProvider.UserId,
            ParentId = dto.ParentId
        };

        // Set customer-specific fields if partner type is Customer
        if (dto.PartnerType == "Customer")
        {
            partner.CustomerType = dto.CustomerType;
            partner.CreditLimit = dto.CreditLimit;
            partner.PaymentTerms = dto.PaymentTerms;
            partner.Currency = dto.Currency;
            partner.DefaultDiscount = dto.DefaultDiscount;
            partner.PriceList = dto.PriceList;
            partner.SalesRepresentativeId = dto.SalesRepresentativeId;
            partner.SalesTerritory = dto.SalesTerritory;
            partner.IsTaxExempt = dto.IsTaxExempt;
            partner.TaxExemptionNumber = dto.TaxExemptionNumber;
            partner.TaxExemptionExpiry = dto.TaxExemptionExpiry;
            partner.PreferredShippingMethod = dto.PreferredShippingMethod;
            partner.DeliveryInstructions = dto.DeliveryInstructions;
            partner.CustomerSince = dto.CustomerSince ?? DateTime.UtcNow;
            partner.LoyaltyTier = dto.LoyaltyTier;
        }

        var created = await _partnerRepository.CreateAsync(partner);
        return MapToDetailDto(created);
    }

    public async Task<BusinessPartnerDetailDto> UpdateAsync(Guid id, UpdateBusinessPartnerDto dto)
    {
        var partner = await _partnerRepository.GetByIdAsync(id) ?? throw new InvalidOperationException($"Business partner with ID {id} not found");
        partner.PartnerName = dto.PartnerName;
        partner.LegalName = dto.PartnerName;
        partner.BusinessRegistrationNumber = dto.RegistrationNumber;
        partner.TaxIdentificationNumber = dto.TaxNumber;
        partner.VATNumber = dto.TaxNumber; // Using TaxNumber as VATNumber for now
        partner.PrimaryEmail = dto.Email;
        partner.PrimaryPhone = dto.Phone;
        partner.Website = dto.Website;
        partner.PhysicalAddress = dto.PhysicalAddress;
        partner.PhysicalCity = dto.City;
        partner.PhysicalCountry = dto.Country;
        partner.PhysicalPostalCode = dto.PostalCode;
        partner.UpdatedAt = DateTime.UtcNow;
        partner.ParentId = dto.ParentId;

        // Update customer-specific fields if partner type is Customer
        if (partner.PartnerType == "Customer")
        {
            partner.CustomerType = dto.CustomerType;
            partner.CreditLimit = dto.CreditLimit;
            partner.PaymentTerms = dto.PaymentTerms;
            partner.Currency = dto.Currency;
            partner.DefaultDiscount = dto.DefaultDiscount;
            partner.PriceList = dto.PriceList;
            partner.SalesRepresentativeId = dto.SalesRepresentativeId;
            partner.SalesTerritory = dto.SalesTerritory;
            partner.IsTaxExempt = dto.IsTaxExempt;
            partner.TaxExemptionNumber = dto.TaxExemptionNumber;
            partner.TaxExemptionExpiry = dto.TaxExemptionExpiry;
            partner.PreferredShippingMethod = dto.PreferredShippingMethod;
            partner.DeliveryInstructions = dto.DeliveryInstructions;
            partner.LoyaltyTier = dto.LoyaltyTier;
            partner.IsOnCreditHold = dto.IsOnCreditHold;
            partner.CreditHoldReason = dto.CreditHoldReason;
            partner.CreditHoldDate = dto.CreditHoldDate;
        }

        var updated = await _partnerRepository.UpdateAsync(partner);
        return MapToDetailDto(updated);
    }

    public async Task DeleteAsync(Guid id)
    {
        try
        {
            await _partnerRepository.DeleteAsync(id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting business partner {Id}", id);
            throw;
        }
    }

    public async Task<PagedResult<BusinessPartnerDto>> GetPartnersAsync(
        int page, int pageSize, string? search = null, string? partnerType = null,
        string? status = null, string? approvalStatus = null, bool? isPreferred = null,
        bool? isBlacklisted = null, List<Guid>? categoryIds = null, List<Guid>? specializationIds = null)
    {
        var result = await _partnerRepository.GetPartnersAsync(page, pageSize, search, partnerType, status, approvalStatus, isPreferred, isBlacklisted, categoryIds, specializationIds);

        return new PagedResult<BusinessPartnerDto>
        {
            Items = result.Items.Select(MapToDto).ToList(),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize
        };
    }

    public async Task<IEnumerable<BusinessPartnerDto>> GetActivePartnersAsync(string? partnerType = null)
    {
        var partners = await _partnerRepository.GetActivePartnersAsync(partnerType);
        return partners.Select(MapToDto);
    }

    public async Task<IEnumerable<BusinessPartnerDto>> GetPreferredPartnersAsync(string? partnerType = null)
    {
        var partners = await _partnerRepository.GetPreferredPartnersAsync(partnerType);
        return partners.Select(MapToDto);
    }

    public async Task<IEnumerable<BusinessPartnerDto>> GetBlacklistedPartnersAsync()
    {
        var partners = await _partnerRepository.GetBlacklistedPartnersAsync();
        return partners.Select(MapToDto);
    }

    public async Task<IEnumerable<BusinessPartnerDto>> GetPartnersByCategoryAsync(Guid categoryId)
    {
        // This would need a repository method - for now return empty
        await Task.CompletedTask;
        return Enumerable.Empty<BusinessPartnerDto>();
    }

    public async Task<IEnumerable<BusinessPartnerDto>> GetPartnersBySpecializationAsync(Guid specializationId)
    {
        // This would need a repository method - for now return empty
        await Task.CompletedTask;
        return Enumerable.Empty<BusinessPartnerDto>();
    }

    public async Task<IEnumerable<BusinessPartnerDto>> GetPartnersRequiringApprovalAsync()
    {
        var result = await _partnerRepository.GetPartnersAsync(1, 1000, null, null, null, "Pending", null, null, null, null);
        return result.Items.Select(MapToDto);
    }

    public async Task<IEnumerable<BusinessPartnerDto>> GetPartnersWithExpiringLicensesAsync(int daysAhead = 30)
    {
        // This would need a repository method - for now return empty
        await Task.CompletedTask;
        return Enumerable.Empty<BusinessPartnerDto>();
    }

    public async Task<bool> IsPartnerCodeUniqueAsync(string partnerCode, Guid? excludeId = null)
    {
        var partner = await _partnerRepository.GetByCodeAsync(partnerCode);
        if (partner == null)
        {
            return true;
        }

        if (excludeId.HasValue && partner.Id == excludeId.Value)
        {
            return true;
        }

        return false;
    }
    public async Task UpdateStatusAsync(Guid partnerId, string status)
    {
        await _partnerRepository.UpdateStatusAsync(partnerId, status);
    }

    public async Task SubmitPartnerForApprovalAsync(Guid partnerId, Guid submittedById)
    {
        var partner = await _partnerRepository.GetByIdAsync(partnerId)
            ?? throw new InvalidOperationException($"Business partner with ID {partnerId} not found");

        if (string.Equals(partner.ApprovalStatus, "Approved", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Business partner is already approved");
        }

        if (string.Equals(partner.ApprovalStatus, "Rejected", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Business partner is rejected and cannot be submitted for approval");
        }

        var workflowResult = await _workflowIntegrationService.SubmitAsync("BusinessPartner", partnerId);
        if (!workflowResult.ExecutionResult.Success)
        {
            throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Failed to start workflow");
        }

        var statusAdapter = _workflowStatusAdapterRegistry.GetAdapter("BusinessPartner");
        statusAdapter.ApplySubmitOutcome(partner, workflowResult.Outcome, submittedById);

        await _partnerRepository.UpdateAsync(partner);
    }

    public async Task ApprovePartnerAsync(Guid partnerId, Guid approvedById, string? comments = null)
    {
        var partner = await _partnerRepository.GetByIdAsync(partnerId)
            ?? throw new InvalidOperationException($"Business partner with ID {partnerId} not found");

        if (!string.Equals(partner.ApprovalStatus, "Pending", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(partner.ApprovalStatus))
        {
            throw new InvalidOperationException($"Business partner cannot be approved in current approval status: '{partner.ApprovalStatus}'");
        }

        var canApprove = await _workflowIntegrationService.CanUserApproveAsync("BusinessPartner", partnerId, approvedById);
        if (!canApprove)
        {
            throw new UnauthorizedAccessException("You are not assigned as an approver for the current workflow step");
        }

        var workflowResult = await _workflowIntegrationService.ProcessApprovalAsync(
            "BusinessPartner",
            partnerId,
            approvedById,
            "Approve",
            comments);

        if (!workflowResult.ExecutionResult.Success)
        {
            throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Failed to process approval");
        }

        var statusAdapter = _workflowStatusAdapterRegistry.GetAdapter("BusinessPartner");
        statusAdapter.ApplyApprovalOutcome(partner, workflowResult.Outcome, approvedById);

        await _partnerRepository.UpdateAsync(partner);
    }

    public async Task RejectPartnerAsync(Guid partnerId, Guid rejectedById, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new InvalidOperationException("Rejection reason is required");
        }

        var partner = await _partnerRepository.GetByIdAsync(partnerId)
            ?? throw new InvalidOperationException($"Business partner with ID {partnerId} not found");

        var canApprove = await _workflowIntegrationService.CanUserApproveAsync("BusinessPartner", partnerId, rejectedById);
        if (!canApprove)
        {
            throw new UnauthorizedAccessException("You are not assigned as an approver for the current workflow step");
        }

        var workflowResult = await _workflowIntegrationService.ProcessApprovalAsync(
            "BusinessPartner",
            partnerId,
            rejectedById,
            "Reject",
            reason);

        if (!workflowResult.ExecutionResult.Success)
        {
            throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Failed to process rejection");
        }

        var statusAdapter = _workflowStatusAdapterRegistry.GetAdapter("BusinessPartner");
        statusAdapter.ApplyApprovalOutcome(partner, workflowResult.Outcome, rejectedById, reason);

        await _partnerRepository.UpdateAsync(partner);
    }

    public async Task UpdatePerformanceRatingAsync(Guid partnerId, decimal rating)
    {
        await _partnerRepository.UpdatePerformanceRatingAsync(partnerId, rating);
    }

    public async Task AddToBlacklistAsync(Guid partnerId, string reason, DateTime? expiryDate = null)
    {
        await _partnerRepository.AddToBlacklistAsync(partnerId, reason, expiryDate);
    }

    public async Task RemoveFromBlacklistAsync(Guid partnerId)
    {
        await _partnerRepository.RemoveFromBlacklistAsync(partnerId);
    }

    public async Task<string> GeneratePartnerCodeAsync(string partnerType)
    {
        return await _partnerRepository.GeneratePartnerCodeAsync(partnerType);
    }
    public async Task<IEnumerable<BusinessPartnerContactDto>> GetContactsAsync(Guid partnerId)
    {
        var partner = await _partnerRepository.GetWithAllRelatedDataAsync(partnerId);
        if (partner?.Contacts == null)
        {
            return Enumerable.Empty<BusinessPartnerContactDto>();
        }

        return partner.Contacts.Select(c => new BusinessPartnerContactDto
        {
            Id = c.Id,
            BusinessPartnerId = c.BusinessPartnerId,
            ContactName = c.ContactName,
            Title = c.ContactTitle,
            ContactTitle = c.ContactTitle,
            Department = c.Department,
            Email = c.Email,
            Phone = c.Phone,
            Mobile = c.Mobile,
            IsPrimary = c.IsPrimary
        });
    }

    public async Task<BusinessPartnerContactDto> AddContactAsync(Guid partnerId, CreateBusinessPartnerContactDto dto)
    {
        // This would need a contact repository - for now throw
        await Task.CompletedTask;
        throw new NotImplementedException("Contact repository not yet implemented");
    }

    public async Task<BusinessPartnerContactDto> UpdateContactAsync(Guid partnerId, Guid contactId, CreateBusinessPartnerContactDto dto)
    {
        await Task.CompletedTask;
        throw new NotImplementedException("Contact repository not yet implemented");
    }

    public async Task DeleteContactAsync(Guid partnerId, Guid contactId)
    {
        await Task.CompletedTask;
        throw new NotImplementedException("Contact repository not yet implemented");
    }

    public async Task SetPrimaryContactAsync(Guid partnerId, Guid contactId)
    {
        await Task.CompletedTask;
        throw new NotImplementedException("Contact repository not yet implemented");
    }

    public async Task<IEnumerable<BusinessPartnerLicenseDto>> GetLicensesAsync(Guid partnerId)
    {
        var partner = await _partnerRepository.GetWithAllRelatedDataAsync(partnerId);
        if (partner?.Licenses == null)
        {
            return Enumerable.Empty<BusinessPartnerLicenseDto>();
        }

        return partner.Licenses.Select(l => new BusinessPartnerLicenseDto
        {
            Id = l.Id,
            BusinessPartnerId = l.BusinessPartnerId,
            LicenseTypeId = l.LicenseTypeId,
            LicenseTypeName = l.LicenseType?.LicenseName,
            LicenseNumber = l.LicenseNumber,
            IssuingAuthority = l.IssuingAuthority,
            IssueDate = l.IssueDate,
            ExpiryDate = l.ExpiryDate,
            Status = l.Status,
            FilePath = l.DocumentPath,
            VerificationNotes = l.Notes
        });
    }

    public async Task<BusinessPartnerLicenseDto> AddLicenseAsync(Guid partnerId, CreateBusinessPartnerLicenseDto dto)
    {
        await Task.CompletedTask;
        throw new NotImplementedException("License repository not yet implemented");
    }

    public async Task<BusinessPartnerLicenseDto> UpdateLicenseAsync(Guid partnerId, Guid licenseId, CreateBusinessPartnerLicenseDto dto)
    {
        await Task.CompletedTask;
        throw new NotImplementedException("License repository not yet implemented");
    }

    public async Task DeleteLicenseAsync(Guid partnerId, Guid licenseId)
    {
        await Task.CompletedTask;
        throw new NotImplementedException("License repository not yet implemented");
    }

    public async Task<bool> HasValidLicenseAsync(Guid partnerId, Guid licenseTypeId)
    {
        var licenses = await GetLicensesAsync(partnerId);
        return licenses.Any(l => l.LicenseTypeId == licenseTypeId &&
                                l.Status == "Active" &&
                                (!l.ExpiryDate.HasValue || l.ExpiryDate.Value > DateTime.UtcNow));
    }

    public async Task<IEnumerable<BusinessPartnerDocumentDto>> GetDocumentsAsync(Guid partnerId)
    {
        var partner = await _partnerRepository.GetWithAllRelatedDataAsync(partnerId);
        if (partner?.Documents == null)
        {
            return Enumerable.Empty<BusinessPartnerDocumentDto>();
        }

        return partner.Documents.Select(d => new BusinessPartnerDocumentDto
        {
            Id = d.Id,
            BusinessPartnerId = d.BusinessPartnerId,
            DocumentType = d.DocumentType,
            DocumentName = d.DocumentName,
            FilePath = d.DocumentPath,
            FileSize = d.FileSize ?? 0,
            MimeType = d.MimeType,
            IssueDate = d.IssueDate,
            ExpiryDate = d.ExpiryDate,
            IsVerified = d.IsVerified,
            VerifiedBy = d.VerifiedById?.ToString(),
            VerifiedDate = d.VerifiedDate,
            VerificationNotes = d.VerificationNotes,
            UploadedAt = d.CreatedAt
        });
    }

    public async Task<BusinessPartnerDocumentDto> UploadDocumentAsync(Guid partnerId, CreateBusinessPartnerDocumentDto dto)
    {
        await Task.CompletedTask;
        throw new NotImplementedException("Document repository not yet implemented");
    }

    public async Task DeleteDocumentAsync(Guid partnerId, Guid documentId)
    {
        await Task.CompletedTask;
        throw new NotImplementedException("Document repository not yet implemented");
    }

    public async Task VerifyDocumentAsync(Guid partnerId, Guid documentId, Guid verifiedById)
    {
        await Task.CompletedTask;
        throw new NotImplementedException("Document repository not yet implemented");
    }

    public async Task<IEnumerable<BusinessPartnerFinancialDto>> GetFinancialsAsync(Guid partnerId)
    {
        var partner = await _partnerRepository.GetWithAllRelatedDataAsync(partnerId);
        if (partner?.Financials == null)
        {
            return Enumerable.Empty<BusinessPartnerFinancialDto>();
        }

        return partner.Financials.Select(f => new BusinessPartnerFinancialDto
        {
            Id = f.Id,
            BusinessPartnerId = f.BusinessPartnerId,
            FinancialYear = f.FiscalYear,
            FiscalYear = f.FiscalYear,
            Revenue = f.AnnualRevenue,
            AnnualRevenue = f.AnnualRevenue,
            Profit = f.NetProfit,
            NetProfit = f.NetProfit,
            Assets = f.TotalAssets,
            TotalAssets = f.TotalAssets,
            Liabilities = f.TotalLiabilities,
            TotalLiabilities = f.TotalLiabilities,
            CreditRating = f.CreditRating,
            FinancialStatementPath = f.FinancialStatementPath,
            IsAudited = f.AuditorName != null,
            AuditorName = f.AuditorName,
            AuditDate = f.AuditDate
        });
    }

    public async Task<BusinessPartnerFinancialDto> AddFinancialAsync(Guid partnerId, CreateBusinessPartnerFinancialDto dto)
    {
        await Task.CompletedTask;
        throw new NotImplementedException("Financial repository not yet implemented");
    }

    public async Task<BusinessPartnerFinancialDto> UpdateFinancialAsync(Guid partnerId, Guid financialId, CreateBusinessPartnerFinancialDto dto)
    {
        await Task.CompletedTask;
        throw new NotImplementedException("Financial repository not yet implemented");
    }

    public async Task DeleteFinancialAsync(Guid partnerId, Guid financialId)
    {
        await Task.CompletedTask;
        throw new NotImplementedException("Financial repository not yet implemented");
    }

    // Helper mapping methods
    private BusinessPartnerDto MapToDto(BusinessPartner partner)
    {
        return new BusinessPartnerDto
        {
            Id = partner.Id,
            PartnerCode = partner.PartnerCode,
            PartnerName = partner.PartnerName,
            CompanyName = partner.PartnerName, // Alias for frontend compatibility
            PartnerType = partner.PartnerType,
            RegistrationNumber = partner.BusinessRegistrationNumber,
            TaxNumber = partner.TaxIdentificationNumber,
            VatNumber = partner.VATNumber,
            Email = partner.PrimaryEmail,
            Phone = partner.PrimaryPhone,
            AlternatePhone = partner.SecondaryPhone,
            Website = partner.Website,
            PhysicalAddress = partner.PhysicalAddress,
            City = partner.PhysicalCity,
            Country = partner.PhysicalCountry,
            Status = partner.RegistrationStatus,
            ApprovalStatus = partner.ApprovalStatus,
            PerformanceRating = partner.PerformanceRating,
            IsPreferred = partner.IsPreferred,
            IsBlacklisted = partner.IsBlacklisted,
            CreatedAt = partner.CreatedAt,
            // Customer-specific fields for list view
            CustomerType = partner.CustomerType,
            CreditLimit = partner.CreditLimit,
            OutstandingBalance = partner.OutstandingBalance,
            IsOnCreditHold = partner.IsOnCreditHold,
            // Parent Business Partner
            ParentId = partner.ParentId,
            ParentName = partner.Parent?.PartnerName
        };
    }

    private static BusinessPartnerDetailDto MapToDetailDto(BusinessPartner partner)
    {
        var dto = new BusinessPartnerDetailDto
        {
            Id = partner.Id,
            PartnerCode = partner.PartnerCode,
            PartnerName = partner.PartnerName,
            PartnerType = partner.PartnerType,
            RegistrationNumber = partner.BusinessRegistrationNumber,
            TaxNumber = partner.TaxIdentificationNumber,
            VatNumber = partner.VATNumber,
            Email = partner.PrimaryEmail,
            Phone = partner.PrimaryPhone,
            AlternatePhone = partner.SecondaryPhone,
            Website = partner.Website,
            PhysicalAddress = partner.PhysicalAddress,
            City = partner.PhysicalCity,
            Country = partner.PhysicalCountry,
            PhysicalPostalCode = partner.PhysicalPostalCode,
            PhysicalState = partner.PhysicalState,
            MailingAddress = partner.MailingAddress,
            MailingCity = partner.MailingCity,
            MailingState = partner.MailingState,
            MailingCountry = partner.MailingCountry,
            MailingPostalCode = partner.MailingPostalCode,
            // User Account Link
            UserId = partner.UserId,
            UserEmail = partner.User?.Email,
            UserFullName = partner.User != null ? $"{partner.User.FirstName} {partner.User.LastName}" : null,
            // Banking Information
            BankName = partner.BankName,
            BankBranch = partner.BankBranch,
            AccountNumber = partner.BankAccountNumber,
            AccountName = partner.BankAccountName,
            SwiftCode = partner.BankSwiftCode,
            Iban = partner.BankIBAN,
            // Contact Person
            ContactPerson = partner.PrimaryContactName,
            ContactTitle = partner.PrimaryContactTitle,
            // Classification
            IndustryType = partner.IndustryClassification,
            CompanySize = partner.CompanySize,
            AnnualRevenue = partner.AnnualTurnover,
            GeographicCoverage = partner.GeographicCoverage,
            // Status
            Status = partner.RegistrationStatus,
            ApprovalStatus = partner.ApprovalStatus,
            IsPreferred = partner.IsPreferred,
            IsBlacklisted = partner.IsBlacklisted,
            BlacklistReason = partner.BlacklistReason,
            BlacklistDate = partner.BlacklistDate,
            BlacklistExpiryDate = partner.BlacklistExpiryDate,
            PerformanceRating = partner.PerformanceRating,
            RegistrationDate = partner.RegistrationDate,
            ApprovedDate = partner.ApprovedDate,
            InsuranceCoverageAmount = partner.InsuranceCoverage,
            Notes = partner.Notes,
            CreatedAt = partner.CreatedAt,
            // Customer-Specific Fields
            CustomerType = partner.CustomerType,
            CustomerAccountNumber = partner.CustomerAccountNumber,
            CreditLimit = partner.CreditLimit,
            OutstandingBalance = partner.OutstandingBalance,
            PaymentTerms = partner.PaymentTerms,
            Currency = partner.Currency,
            DefaultDiscount = partner.DefaultDiscount,
            PriceList = partner.PriceList,
            SalesRepresentativeId = partner.SalesRepresentativeId,
            SalesRepresentativeName = partner.SalesRepresentative != null
                ? $"{partner.SalesRepresentative.FirstName} {partner.SalesRepresentative.LastName}"
                : null,
            SalesTerritory = partner.SalesTerritory,
            IsTaxExempt = partner.IsTaxExempt,
            TaxExemptionNumber = partner.TaxExemptionNumber,
            TaxExemptionExpiry = partner.TaxExemptionExpiry,
            PreferredShippingMethod = partner.PreferredShippingMethod,
            DeliveryInstructions = partner.DeliveryInstructions,
            CustomerSince = partner.CustomerSince,
            LastPurchaseDate = partner.LastPurchaseDate,
            TotalLifetimePurchases = partner.TotalLifetimePurchases,
            AverageOrderValue = partner.AverageOrderValue,
            LoyaltyTier = partner.LoyaltyTier,
            LoyaltyPoints = partner.LoyaltyPoints,
            IsOnCreditHold = partner.IsOnCreditHold,
            CreditHoldReason = partner.CreditHoldReason,
            CreditHoldDate = partner.CreditHoldDate,
            // Parent Business Partner
            ParentId = partner.ParentId,
            ParentName = partner.Parent?.PartnerName
        };

        // Map contacts
        if (partner.Contacts != null && partner.Contacts.Any())
        {
            dto.Contacts = partner.Contacts.Select(c => new BusinessPartnerContactDto
            {
                Id = c.Id,
                BusinessPartnerId = c.BusinessPartnerId,
                ContactName = c.ContactName,
                Title = c.ContactTitle,
                ContactTitle = c.ContactTitle,
                Department = c.Department,
                Email = c.Email,
                Phone = c.Phone,
                Mobile = c.Mobile,
                IsPrimary = c.IsPrimary
            }).ToList();
        }

        // Map documents
        if (partner.Documents != null && partner.Documents.Any())
        {
            dto.Documents = partner.Documents.Select(d => new BusinessPartnerDocumentDto
            {
                Id = d.Id,
                BusinessPartnerId = d.BusinessPartnerId,
                DocumentType = d.DocumentType,
                DocumentName = d.DocumentName,
                FilePath = d.DocumentPath,
                FileSize = d.FileSize ?? 0,
                MimeType = d.MimeType,
                IssueDate = d.IssueDate,
                ExpiryDate = d.ExpiryDate,
                IsVerified = d.IsVerified,
                VerifiedBy = d.VerifiedById?.ToString(),
                VerifiedDate = d.VerifiedDate,
                VerificationNotes = d.VerificationNotes,
                UploadedAt = d.CreatedAt
            }).ToList();
        }

        // Map licenses
        if (partner.Licenses != null && partner.Licenses.Any())
        {
            dto.Licenses = partner.Licenses.Select(l => new BusinessPartnerLicenseDto
            {
                Id = l.Id,
                BusinessPartnerId = l.BusinessPartnerId,
                LicenseTypeId = l.LicenseTypeId,
                LicenseTypeName = l.LicenseType?.LicenseName,
                LicenseNumber = l.LicenseNumber,
                IssuingAuthority = l.IssuingAuthority,
                IssueDate = l.IssueDate,
                ExpiryDate = l.ExpiryDate,
                Status = l.Status,
                FilePath = l.DocumentPath,
                VerificationNotes = l.Notes
            }).ToList();
        }

        // Map financials
        if (partner.Financials != null && partner.Financials.Any())
        {
            dto.FinancialRecords = partner.Financials.Select(f => new BusinessPartnerFinancialDto
            {
                Id = f.Id,
                BusinessPartnerId = f.BusinessPartnerId,
                FinancialYear = f.FiscalYear,
                FiscalYear = f.FiscalYear,
                Revenue = f.AnnualRevenue,
                AnnualRevenue = f.AnnualRevenue,
                Profit = f.NetProfit,
                NetProfit = f.NetProfit,
                Assets = f.TotalAssets,
                TotalAssets = f.TotalAssets,
                Liabilities = f.TotalLiabilities,
                TotalLiabilities = f.TotalLiabilities,
                CreditRating = f.CreditRating,
                FinancialStatementPath = f.FinancialStatementPath,
                IsAudited = f.AuditorName != null,
                AuditorName = f.AuditorName,
                AuditDate = f.AuditDate
            }).ToList();

            dto.FinancialInfo = dto.FinancialRecords;
        }

        return dto;
    }
}
