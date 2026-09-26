using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Procurement;

public class BusinessPartnerService : IBusinessPartnerService
{
    private readonly IBusinessPartnerRepository _partnerRepository;
    private readonly IBusinessPartnerContactRepository _contactRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IWorkflowIntegrationService _workflowIntegrationService;
    private readonly IWorkflowStatusAdapterRegistry _workflowStatusAdapterRegistry;
    private readonly IPaymentTermRepository _paymentTermRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<BusinessPartnerService> _logger;
    private readonly IProcurementAccessControlService? _accessControl;

    public BusinessPartnerService(
        IBusinessPartnerRepository partnerRepository,
        IBusinessPartnerContactRepository contactRepository,
        ICurrentUserProvider currentUserProvider,
        IWorkflowIntegrationService workflowIntegrationService,
        IWorkflowStatusAdapterRegistry workflowStatusAdapterRegistry,
        IPaymentTermRepository paymentTermRepository,
        ILogger<BusinessPartnerService> logger,
        IUnitOfWork unitOfWork,
        IProcurementAccessControlService? accessControl = null)
    {
        _partnerRepository = partnerRepository;
        _contactRepository = contactRepository;
        _currentUserProvider = currentUserProvider;
        _workflowIntegrationService = workflowIntegrationService;
        _workflowStatusAdapterRegistry = workflowStatusAdapterRegistry;
        _paymentTermRepository = paymentTermRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _accessControl = accessControl;
    }

    public async Task<BusinessPartnerPostingOptionsDto> GetPostingOptionsAsync(string? partnerType = null)
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty || _currentUserProvider.IsExternalUser)
            throw new UnauthorizedAccessException("Business-partner posting options are available to internal users in the current tenant.");
        var now = DateTime.UtcNow;
        var accounts = (await _unitOfWork.Accounts.FindAsync(account =>
            account.TenantId == tenantId && !account.IsDeleted && account.Status == AccountStatus.Active &&
            (account.AllowDirectPosting || account.IsControlAccount) &&
            (!account.EffectiveDate.HasValue || account.EffectiveDate <= now) &&
            (!account.ExpirationDate.HasValue || account.ExpirationDate > now)))
            .OrderBy(account => account.AccountNumber).ToList();
        var accountById = accounts.ToDictionary(account => account.Id);
        var banks = (await _unitOfWork.Repository<BankAccount>().FindAsync(bank =>
            bank.TenantId == tenantId && !bank.IsDeleted && bank.IsActive)).OrderBy(bank => bank.AccountName);
        var applicability = string.Equals(partnerType, "Customer", StringComparison.OrdinalIgnoreCase)
            ? TaxApplicability.Sales : TaxApplicability.Purchases;
        var taxes = (await _unitOfWork.Repository<TaxGroup>().FindAsync(tax =>
            tax.TenantId == tenantId && !tax.IsDeleted && tax.IsActive &&
            (tax.Applicability == TaxApplicability.Both || tax.Applicability == applicability)))
            .OrderBy(tax => tax.Name);
        var withholdingTaxes = (await _unitOfWork.Repository<Tax>().FindAsync(tax =>
            tax.TenantId == tenantId && !tax.IsDeleted && tax.IsActive && tax.Category == TaxCategory.Withholding &&
            (tax.Applicability == TaxApplicability.Purchases || tax.Applicability == TaxApplicability.Both)))
            .OrderBy(tax => tax.Code);
        return new BusinessPartnerPostingOptionsDto
        {
            Accounts = accounts.Select(account => new BusinessPartnerPostingAccountOptionDto(account.Id, account.AccountCode,
                account.AccountNumber, account.AccountName, account.AccountType, account.Status,
                account.AllowDirectPosting, account.IsControlAccount)).ToList(),
            BankAccounts = banks.Select(bank =>
            {
                var gl = bank.GLAccountId.HasValue ? accountById.GetValueOrDefault(bank.GLAccountId.Value) : null;
                return new BusinessPartnerChequeBookOptionDto(bank.Id, bank.AccountNumber, bank.AccountName, bank.Currency,
                    bank.IsActive, bank.GLAccountId, gl?.AccountNumber, gl?.AccountName);
            }).ToList(),
            TaxGroups = taxes.Select(tax => new BusinessPartnerTaxGroupOptionDto(tax.Id, tax.Code, tax.Name,
                tax.Applicability, tax.IsActive)).ToList(),
            WithholdingTaxes = withholdingTaxes.Select(tax => new BusinessPartnerWithholdingTaxOptionDto(tax.Id,
                tax.Code, tax.Name, tax.Rate, tax.EffectiveFrom, tax.TaxPayableAccountId)).ToList()
        };
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
        if (_currentUserProvider.IsExternalUser && userId != _currentUserProvider.UserId)
            throw new UnauthorizedAccessException("Suppliers can only access their own business partner account.");
        var partner = await _partnerRepository.GetByUserIdAsync(userId);
        if (partner == null)
        {
            return null;
        }

        return MapToDetailDto(partner);
    }

    public async Task<BusinessPartnerDetailDto> CreateAsync(CreateBusinessPartnerDto dto)
    {
        BusinessPartnerRoles.Validate(dto.PartnerType);
        if (dto.ReceivablesDefaults != null || dto.PostingDefaults != null)
            await EnsureAccountingConfigurationAccessAsync();
        if (dto.ReceivablesDefaults != null)
            await BusinessPartnerPostingDefaultValidation.ValidateReceivablesAsync(dto.ReceivablesDefaults, dto.PartnerType, _unitOfWork, _currentUserProvider);
        if (_currentUserProvider.IsExternalUser && dto.CreditLimit.HasValue)
            throw new UnauthorizedAccessException("Credit limits are maintained by internal business-partner administrators.");
        ValidateCreditLimit(dto.CreditLimit);
        if (dto.PostingDefaults != null)
            await ValidatePostingDefaultsAsync(dto.PostingDefaults, dto.PartnerType);
        var partnerCode = await _partnerRepository.GeneratePartnerCodeAsync(dto.PartnerType);
        var paymentTerm = await ResolvePaymentTermAsync(dto.PaymentTermId, dto.PartnerType, useDefaultWhenMissing: true);

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
            ParentId = dto.ParentId,
            Currency = dto.Currency
        };

        // PROCUREMENT OWNERSHIP NOTE: Finance uses PaymentTermId as the authoritative value.
        // PaymentTerms is intentionally dual-written for older procurement screens/reports; do not
        // parse or bulk-backfill historical free text without agreement from the procurement owner.
        partner.PaymentTermId = paymentTerm?.Id;
        partner.PaymentTerms = paymentTerm?.Name;
        partner.CreditLimit = dto.CreditLimit;
        if (dto.PostingDefaults != null) BusinessPartnerPostingDefaults.Apply(partner, dto.PostingDefaults);

        if (dto.ReceivablesDefaults != null) BusinessPartnerReceivablesDefaults.Apply(partner, dto.ReceivablesDefaults);

        // Set customer-specific fields if partner type is Customer
        if (BusinessPartnerRoles.HasCustomer(dto.PartnerType))
        {
            partner.CustomerType = dto.CustomerType;
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
        if (partner.TenantId != _currentUserProvider.TenantId)
            throw new UnauthorizedAccessException("Business partner belongs to another tenant.");
        var requestedType = dto.PartnerType ?? partner.PartnerType;
        if (requestedType != partner.PartnerType || dto.ReceivablesDefaults != null || dto.PostingDefaults != null)
            await EnsureAccountingConfigurationAccessAsync();
        if (requestedType != partner.PartnerType && _currentUserProvider.IsExternalUser)
            throw new UnauthorizedAccessException("Partner roles are maintained by internal business-partner administrators.");
        BusinessPartnerRoles.ValidateRoleChange(partner.PartnerType, requestedType);
        if (dto.ReceivablesDefaults != null)
            await BusinessPartnerPostingDefaultValidation.ValidateReceivablesAsync(dto.ReceivablesDefaults, requestedType, _unitOfWork, _currentUserProvider);
        if (_currentUserProvider.IsExternalUser && dto.CreditLimit.HasValue && dto.CreditLimit != partner.CreditLimit)
            throw new UnauthorizedAccessException("Credit limits are maintained by internal business-partner administrators.");
        ValidateCreditLimit(dto.CreditLimit);
        if (dto.PostingDefaults != null)
            await ValidatePostingDefaultsAsync(dto.PostingDefaults, requestedType, partner.DefaultTaxAccountId);
        partner.PartnerType = requestedType;
        if (dto.ReceivablesDefaults != null) BusinessPartnerReceivablesDefaults.Apply(partner, dto.ReceivablesDefaults);
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
        partner.Currency = dto.Currency;
        if (dto.CreditLimit.HasValue || dto.PostingDefaults != null) partner.CreditLimit = dto.CreditLimit;
        if (dto.PostingDefaults != null) BusinessPartnerPostingDefaults.Apply(partner, dto.PostingDefaults);

        if (dto.PaymentTermId.HasValue)
        {
            var paymentTerm = await ResolvePaymentTermAsync(dto.PaymentTermId, partner.PartnerType, useDefaultWhenMissing: false);

            // PROCUREMENT OWNERSHIP NOTE: keep the legacy descriptor synchronized only for
            // forward edits made through the structured selector. Existing legacy-only rows stay untouched.
            partner.PaymentTermId = paymentTerm!.Id;
            partner.PaymentTerms = paymentTerm.Name;
        }
        else if (dto.PostingDefaults != null)
        {
            partner.PaymentTermId = null;
            partner.PaymentTerms = null;
        }
        
        if (!string.IsNullOrEmpty(dto.Status)) 
        {
            partner.RegistrationStatus = dto.Status;
        }

        // Update customer-specific fields if partner type is Customer
        if (BusinessPartnerRoles.HasCustomer(partner.PartnerType))
        {
            partner.CustomerType = dto.CustomerType;
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
        statusAdapter.ApplySubmitOutcome(partner, workflowResult, submittedById);

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
        await GetPartnerEntityAsync(partnerId);

        var contacts = await _contactRepository.GetContactsByPartnerAsync(partnerId);
        return contacts.Select(MapContactDto).ToList();
    }

    public async Task<IEnumerable<BusinessPartnerBankAccountDto>> GetBankAccountsAsync(Guid partnerId)
    {
        var partner = await _partnerRepository.GetWithBankAccountsAsync(partnerId);
        if (partner == null || partner.TenantId != _currentUserProvider.TenantId)
        {
            throw new ArgumentException($"Business partner {partnerId} was not found.");
        }

        return MapBankAccounts(partner);
    }

    public async Task<BusinessPartnerContactDto> AddContactAsync(Guid partnerId, CreateBusinessPartnerContactDto dto)
    {
        var partner = await GetPartnerEntityAsync(partnerId);
        var existingContacts = (await _contactRepository.GetContactsByPartnerAsync(partnerId)).ToList();

        var contact = new BusinessPartnerContact
        {
            Id = Guid.NewGuid(),
            TenantId = partner.TenantId,
            BusinessPartnerId = partnerId,
            ContactName = dto.ContactName.Trim(),
            ContactTitle = CleanNullable(dto.Title),
            Department = CleanNullable(dto.Department),
            Email = CleanNullable(dto.Email),
            Phone = CleanNullable(dto.Phone),
            Mobile = CleanNullable(dto.Mobile),
            IsPrimary = dto.IsPrimary || existingContacts.Count == 0
        };

        await _contactRepository.CreateAsync(contact);
        await _contactRepository.SaveChangesAsync();

        if (contact.IsPrimary)
        {
            await _contactRepository.SetPrimaryContactAsync(partnerId, contact.Id);
        }

        await SyncPrimaryContactSummaryAsync(partner);
        return MapContactDto((await _contactRepository.GetByIdAsync(contact.Id))!);
    }

    public async Task<BusinessPartnerContactDto> UpdateContactAsync(Guid partnerId, Guid contactId, CreateBusinessPartnerContactDto dto)
    {
        var partner = await GetPartnerEntityAsync(partnerId);
        var contact = await _contactRepository.GetByIdAsync(contactId)
            ?? throw new ArgumentException($"Contact {contactId} was not found.");

        if (contact.BusinessPartnerId != partnerId || contact.TenantId != partner.TenantId)
        {
            throw new ArgumentException($"Contact {contactId} was not found for this business partner.");
        }

        var wasPrimary = contact.IsPrimary;

        contact.ContactName = dto.ContactName.Trim();
        contact.ContactTitle = CleanNullable(dto.Title);
        contact.Department = CleanNullable(dto.Department);
        contact.Email = CleanNullable(dto.Email);
        contact.Phone = CleanNullable(dto.Phone);
        contact.Mobile = CleanNullable(dto.Mobile);
        contact.IsPrimary = dto.IsPrimary;

        await _contactRepository.UpdateAsync(contact);
        await _contactRepository.SaveChangesAsync();

        if (dto.IsPrimary)
        {
            await _contactRepository.SetPrimaryContactAsync(partnerId, contactId);
        }
        else if (wasPrimary)
        {
            var replacement = (await _contactRepository.GetContactsByPartnerAsync(partnerId))
                .FirstOrDefault(x => x.Id != contactId);

            if (replacement != null)
            {
                await _contactRepository.SetPrimaryContactAsync(partnerId, replacement.Id);
            }
        }

        await SyncPrimaryContactSummaryAsync(partner);
        return MapContactDto((await _contactRepository.GetByIdAsync(contactId))!);
    }

    public async Task DeleteContactAsync(Guid partnerId, Guid contactId)
    {
        var partner = await GetPartnerEntityAsync(partnerId);
        var contact = await _contactRepository.GetByIdAsync(contactId)
            ?? throw new ArgumentException($"Contact {contactId} was not found.");

        if (contact.BusinessPartnerId != partnerId || contact.TenantId != partner.TenantId)
        {
            throw new ArgumentException($"Contact {contactId} was not found for this business partner.");
        }

        var wasPrimary = contact.IsPrimary;

        await _contactRepository.DeleteAsync(contactId);
        await _contactRepository.SaveChangesAsync();

        if (wasPrimary)
        {
            var replacement = (await _contactRepository.GetContactsByPartnerAsync(partnerId)).FirstOrDefault();
            if (replacement != null)
            {
                await _contactRepository.SetPrimaryContactAsync(partnerId, replacement.Id);
            }
        }

        await SyncPrimaryContactSummaryAsync(partner);
    }

    public async Task SetPrimaryContactAsync(Guid partnerId, Guid contactId)
    {
        var partner = await GetPartnerEntityAsync(partnerId);
        var contact = await _contactRepository.GetByIdAsync(contactId)
            ?? throw new ArgumentException($"Contact {contactId} was not found.");

        if (contact.BusinessPartnerId != partnerId || contact.TenantId != partner.TenantId)
        {
            throw new ArgumentException($"Contact {contactId} was not found for this business partner.");
        }

        await _contactRepository.SetPrimaryContactAsync(partnerId, contactId);
        await SyncPrimaryContactSummaryAsync(partner);
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
        var tenantId = _currentUserProvider.TenantId;
        if (!_currentUserProvider.IsAuthenticated || tenantId == Guid.Empty || _currentUserProvider.IsExternalUser ||
            !(_currentUserProvider.HasRole("SuperAdmin") || _currentUserProvider.HasRole("TenantAdmin")))
            throw new UnauthorizedAccessException("Only a tenant administrator can maintain contractor licences.");
        if (dto.LicenseTypeId == Guid.Empty || string.IsNullOrWhiteSpace(dto.LicenseNumber) ||
            string.IsNullOrWhiteSpace(dto.IssuingAuthority) || dto.IssueDate == default ||
            dto.IssueDate.Date > DateTime.UtcNow.Date || dto.ExpiryDate?.Date < dto.IssueDate.Date)
            throw new InvalidOperationException("A licence type, number, issuing authority and valid issue/expiry dates are required.");
        var partner = await _unitOfWork.Repository<BusinessPartner>().FirstOrDefaultAsync(p =>
            p.Id == partnerId && p.TenantId == tenantId && !p.IsDeleted);
        var type = await _unitOfWork.Repository<LicenseType>().FirstOrDefaultAsync(t =>
            t.Id == dto.LicenseTypeId && t.TenantId == tenantId && !t.IsDeleted && t.IsActive);
        if (partner is null || type is null)
            throw new InvalidOperationException("The partner and an active licence type must belong to the current tenant.");
        return await _unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            await _unitOfWork.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            try
            {
                await _unitOfWork.AcquireTransactionLockAsync($"partner-licences:{tenantId:N}:{partnerId:N}");
                var repository = _unitOfWork.Repository<BusinessPartnerLicense>();
                if (await repository.ExistsAsync(l => l.TenantId == tenantId && l.BusinessPartnerId == partnerId &&
                    !l.IsDeleted && l.LicenseTypeId == dto.LicenseTypeId && l.LicenseNumber == dto.LicenseNumber.Trim()))
                    throw new InvalidOperationException("This licence is already recorded for the partner.");
                var licence = new BusinessPartnerLicense
                {
                    Id = Guid.NewGuid(), TenantId = tenantId, BusinessPartnerId = partnerId,
                    LicenseTypeId = type.Id, LicenseNumber = dto.LicenseNumber.Trim(),
                    IssuingAuthority = dto.IssuingAuthority.Trim(), IssueDate = dto.IssueDate.Date,
                    ExpiryDate = dto.ExpiryDate?.Date,
                    Status = dto.ExpiryDate?.Date < DateTime.UtcNow.Date ? "Expired" : "Active",
                    CreatedById = _currentUserProvider.UserId, CreatedBy = _currentUserProvider.Username
                };
                await repository.AddAsync(licence);
                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitAsync();
                return new BusinessPartnerLicenseDto
                {
                    Id = licence.Id, BusinessPartnerId = partnerId, LicenseTypeId = type.Id,
                    LicenseTypeName = type.LicenseName, LicenseNumber = licence.LicenseNumber,
                    IssuingAuthority = licence.IssuingAuthority, IssueDate = licence.IssueDate,
                    ExpiryDate = licence.ExpiryDate, Status = licence.Status
                };
            }
            catch { await _unitOfWork.RollbackAsync(); _unitOfWork.ClearTrackedChanges(); throw; }
        });
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
            RiskLevel = partner.RiskLevel,
            IsPreferred = partner.IsPreferred,
            IsActive = partner.IsActive,
            IsBlacklisted = partner.IsBlacklisted,
            Currency = partner.Currency,
            CreatedAt = partner.CreatedAt,
            Categories = MapCategoryNames(partner),
            // Customer-specific fields for list view
            CustomerType = partner.CustomerType,
            CreditLimit = partner.CreditLimit,
            OutstandingBalance = partner.OutstandingBalance,
            IsOnCreditHold = partner.IsOnCreditHold,
            PaymentTermId = partner.PaymentTermId,
            // Parent Business Partner
            ParentId = partner.ParentId,
            ParentName = partner.Parent?.PartnerName
        };
    }

    private static BusinessPartnerDetailDto MapToDetailDto(BusinessPartner partner)
    {
        var dto = new BusinessPartnerDetailDto
        {
            ReceivablesDefaults = BusinessPartnerReceivablesDefaults.FromPartner(partner),
            PostingDefaults = BusinessPartnerPostingDefaults.FromPartner(partner),
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
            IsActive = partner.IsActive,
            IsBlacklisted = partner.IsBlacklisted,
            BlacklistReason = partner.BlacklistReason,
            BlacklistDate = partner.BlacklistDate,
            BlacklistExpiryDate = partner.BlacklistExpiryDate,
            RiskLevel = partner.RiskLevel,
            ComplianceStatus = partner.ComplianceStatus,
            ComplianceReviewDateUtc = partner.ComplianceReviewDateUtc,
            ComplianceValidUntilUtc = partner.ComplianceValidUntilUtc,
            ComplianceNotes = partner.ComplianceNotes,
            PerformanceRating = partner.PerformanceRating,
            RegistrationDate = partner.RegistrationDate,
            ApprovedDate = partner.ApprovedDate,
            InsuranceCoverageAmount = partner.InsuranceCoverage,
            Notes = partner.Notes,
            CreatedAt = partner.CreatedAt,
            Categories = MapCategoryNames(partner),
            // Customer-Specific Fields
            CustomerType = partner.CustomerType,
            CustomerAccountNumber = partner.CustomerAccountNumber,
            CreditLimit = partner.CreditLimit,
            OutstandingBalance = partner.OutstandingBalance,
            PaymentTerms = partner.PaymentTerms,
            PaymentTermId = partner.PaymentTermId,
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
            dto.Contacts = partner.Contacts.Select(MapContactDto).ToList();
        }

        dto.BankAccounts = MapBankAccounts(partner);

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

    private async Task EnsureAccountingConfigurationAccessAsync()
    {
        if (_currentUserProvider.IsExternalUser || !_currentUserProvider.IsAuthenticated ||
            _currentUserProvider.TenantId == Guid.Empty || _currentUserProvider.UserId == Guid.Empty)
            throw new UnauthorizedAccessException("An authenticated internal supplier administrator is required to configure partner roles and accounts.");
        if (_currentUserProvider.HasRole(ErpSystem.Shared.Constants.Roles.SuperAdmin)) return;
        if (_accessControl == null)
            throw new UnauthorizedAccessException("Supplier management authorization is unavailable.");
        var decision = await _accessControl.EnforceCapabilityAsync(new()
        {
            PermissionCode = "procurement.supplier.manage", SourceType = "BusinessPartner",
            SourceReference = "RolesAndPostingDefaults"
        }, Guid.NewGuid().ToString("N"));
        if (!decision.Allowed) throw new UnauthorizedAccessException(decision.Message);
    }

    private static List<string> MapCategoryNames(BusinessPartner partner)
    {
        return partner.Categories
            .Where(link => link.Category != null && !string.IsNullOrWhiteSpace(link.Category.CategoryName))
            .OrderByDescending(link => link.IsPrimary)
            .ThenBy(link => link.Category.CategoryName)
            .Select(link => link.Category.CategoryName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<ErpSystem.Core.Entities.Finance.PaymentTerm?> ResolvePaymentTermAsync(
        Guid? paymentTermId,
        string partnerType,
        bool useDefaultWhenMissing)
    {
        var applicableTo = partnerType.Equals("Customer", StringComparison.OrdinalIgnoreCase)
            ? "Customer"
            : partnerType.Equals("Contractor", StringComparison.OrdinalIgnoreCase)
                ? "Contractor"
                : partnerType.Equals("Both", StringComparison.OrdinalIgnoreCase)
                    ? "All"
                    : "Supplier";

        var term = paymentTermId.HasValue
            ? await _paymentTermRepository.GetByIdAsync(paymentTermId.Value)
            : useDefaultWhenMissing
                ? await _paymentTermRepository.GetDefaultAsync(applicableTo)
                : null;

        if (paymentTermId.HasValue && term == null)
        {
            throw new InvalidOperationException("The selected payment term was not found for this tenant.");
        }

        if (term != null && (term.TenantId != _currentUserProvider.TenantId || term.IsDeleted || !term.IsActive ||
            !(term.ApplicableTo.Equals("All", StringComparison.OrdinalIgnoreCase) ||
              term.ApplicableTo.Equals(applicableTo, StringComparison.OrdinalIgnoreCase) ||
              (applicableTo == "Supplier" && term.ApplicableTo.Equals("Vendor", StringComparison.OrdinalIgnoreCase)) ||
              (applicableTo == "Customer" && term.ApplicableTo.Equals("Client", StringComparison.OrdinalIgnoreCase)))))
        {
            throw new InvalidOperationException($"Payment term '{term.Code}' is not active and applicable to {applicableTo.ToLowerInvariant()} partners.");
        }

        return term;
    }

private static void ValidateCreditLimit(decimal? creditLimit)
    {
        if (creditLimit < 0) throw new InvalidOperationException("Credit limit cannot be negative.");
    }

    private Task ValidatePostingDefaultsAsync(BusinessPartnerPostingDefaultsDto defaults, string partnerType,
        Guid? existingTaxAccountId = null) =>
        BusinessPartnerPostingDefaultValidation.ValidateAsync(defaults, partnerType, _unitOfWork, _currentUserProvider, existingTaxAccountId);

    private async Task<BusinessPartner> GetPartnerEntityAsync(Guid partnerId)
    {
        var partner = await _partnerRepository.GetByIdAsync(partnerId);
        if (partner == null)
        {
            throw new ArgumentException($"Business partner {partnerId} was not found.");
        }

        if (partner.TenantId != _currentUserProvider.TenantId)
        {
            throw new UnauthorizedAccessException("You do not have access to this business partner.");
        }

        return partner;
    }

    private async Task SyncPrimaryContactSummaryAsync(BusinessPartner partner)
    {
        var contacts = (await _contactRepository.GetContactsByPartnerAsync(partner.Id)).ToList();
        var primaryContact = contacts.FirstOrDefault(x => x.IsPrimary);

        partner.PrimaryContactName = primaryContact?.ContactName;
        partner.PrimaryContactTitle = primaryContact?.ContactTitle;
        partner.PrimaryEmail = primaryContact?.Email;
        partner.PrimaryPhone = primaryContact?.Phone ?? primaryContact?.Mobile;
        partner.SecondaryPhone = primaryContact?.Phone != null && primaryContact.Mobile != null
            ? primaryContact.Mobile
            : null;

        await _partnerRepository.UpdateAsync(partner);
    }

    private static BusinessPartnerContactDto MapContactDto(BusinessPartnerContact contact)
        => new()
        {
            Id = contact.Id,
            BusinessPartnerId = contact.BusinessPartnerId,
            ContactName = contact.ContactName,
            Title = contact.ContactTitle,
            ContactTitle = contact.ContactTitle,
            Department = contact.Department,
            Email = contact.Email,
            Phone = contact.Phone,
            Mobile = contact.Mobile,
            IsPrimary = contact.IsPrimary,
            IsActive = !contact.IsDeleted
        };

    private static List<BusinessPartnerBankAccountDto> MapBankAccounts(BusinessPartner partner)
    {
        var accounts = partner.BankAccounts
            .Where(account => !account.IsDeleted)
            .OrderByDescending(account => account.IsPrimary)
            .ThenBy(account => account.BankName)
            .ThenBy(account => account.AccountNumber)
            .Select(account => new BusinessPartnerBankAccountDto
            {
                Id = account.Id,
                BusinessPartnerId = account.BusinessPartnerId,
                BankName = account.BankName,
                BranchName = account.BranchName,
                AccountName = account.AccountName,
                AccountNumber = account.AccountNumber,
                SwiftCode = account.SwiftCode,
                Iban = account.Iban,
                Currency = account.Currency,
                IsPrimary = account.IsPrimary,
                IsActive = account.IsActive
            })
            .ToList();

        if (accounts.Count == 0 && HasLegacyBankDetails(partner))
        {
            accounts.Add(new BusinessPartnerBankAccountDto
            {
                Id = Guid.Empty,
                BusinessPartnerId = partner.Id,
                BankName = partner.BankName,
                BranchName = partner.BankBranch,
                AccountName = partner.BankAccountName,
                AccountNumber = partner.BankAccountNumber,
                SwiftCode = partner.BankSwiftCode,
                Iban = partner.BankIBAN,
                Currency = partner.Currency,
                IsPrimary = true,
                IsActive = true
            });
        }

        return accounts;
    }

    private static bool HasLegacyBankDetails(BusinessPartner partner)
        => !string.IsNullOrWhiteSpace(partner.BankName) ||
           !string.IsNullOrWhiteSpace(partner.BankBranch) ||
           !string.IsNullOrWhiteSpace(partner.BankAccountName) ||
           !string.IsNullOrWhiteSpace(partner.BankAccountNumber) ||
           !string.IsNullOrWhiteSpace(partner.BankSwiftCode) ||
           !string.IsNullOrWhiteSpace(partner.BankIBAN);

    private static string? CleanNullable(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
