using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Services.Finance;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Finance;

/// <summary>
/// Finance-owned governance for AP and AR defaults attached to the canonical Procurement Business
/// Partner. This controller must never create a supplier/customer identity or accept AP/AR control
/// accounts: Finance settings remain the sole authority for those control accounts.
/// </summary>
[ApiController]
[Route("api/finance/business-partner-profiles")]
[Authorize]
public sealed class BusinessPartnerFinanceProfilesController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserProvider _currentUser;

    public BusinessPartnerFinanceProfilesController(ApplicationDbContext db, ICurrentUserProvider currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    [HttpGet("{businessPartnerId:guid}")]
    [Authorize(Policy = FinancePermissions.ViewFinance)]
    public async Task<ActionResult<BusinessPartnerFinanceProfileSetDto>> Get(Guid businessPartnerId, CancellationToken cancellationToken)
    {
        var result = await LoadAsync(businessPartnerId, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("{businessPartnerId:guid}/ap")]
    [Authorize(Policy = FinancePermissions.ManageBusinessPartnerFinanceProfiles)]
    public async Task<ActionResult<BusinessPartnerApProfileDto>> CreateApDraft(
        Guid businessPartnerId,
        [FromBody] SaveBusinessPartnerApProfileRequest request,
        CancellationToken cancellationToken)
        => await ExecuteApWriteAsync(() => CreateApDraftCore(businessPartnerId, request, cancellationToken), cancellationToken);

    private async Task<ActionResult<BusinessPartnerApProfileDto>> CreateApDraftCore(
        Guid businessPartnerId, SaveBusinessPartnerApProfileRequest request, CancellationToken cancellationToken)
    {
        var validation = await ValidateApRequestAsync(businessPartnerId, request, cancellationToken);
        if (validation is not null) return validation;

        var version = (await _db.BusinessPartnerApProfileVersions
            .Where(item => item.TenantId == TenantId && item.BusinessPartnerRoleId == request.BusinessPartnerRoleId)
            .MaxAsync(item => (int?)item.VersionNumber, cancellationToken) ?? 0) + 1;

        var profile = new BusinessPartnerApProfileVersion
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            BusinessPartnerRoleId = request.BusinessPartnerRoleId,
            VersionNumber = version,
            Status = BusinessPartnerFinanceProfileStatus.Draft,
            CreatedById = UserId
        };
        ApplyApFields(profile, request);
        _db.BusinessPartnerApProfileVersions.Add(profile);
        AddWithholdingLines(profile, request.WithholdingDefaults);
        await _db.SaveChangesAsync(cancellationToken);

        // The default line and profile reference one another. Persist the new graph first, then set
        // the optional default pointer so SQL Server never sees a dangling foreign key.
        profile.DefaultWithholdingLineId = profile.WithholdingDefaults.SingleOrDefault(item => item.IsDefaultForAp)?.Id;
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(await MapApAsync(profile.Id, cancellationToken));
    }

    [HttpPut("{businessPartnerId:guid}/ap/{profileId:guid}")]
    [Authorize(Policy = FinancePermissions.ManageBusinessPartnerFinanceProfiles)]
    public async Task<ActionResult<BusinessPartnerApProfileDto>> UpdateApDraft(
        Guid businessPartnerId,
        Guid profileId,
        [FromBody] SaveBusinessPartnerApProfileRequest request,
        CancellationToken cancellationToken)
        => await ExecuteApWriteAsync(() => UpdateApDraftCore(businessPartnerId, profileId, request, cancellationToken), cancellationToken);

    private async Task<ActionResult<BusinessPartnerApProfileDto>> UpdateApDraftCore(
        Guid businessPartnerId, Guid profileId, SaveBusinessPartnerApProfileRequest request, CancellationToken cancellationToken)
    {
        var profile = await _db.BusinessPartnerApProfileVersions
            .Include(item => item.BusinessPartnerRole)
            .Include(item => item.WithholdingDefaults)
            .SingleOrDefaultAsync(item => item.Id == profileId && item.TenantId == TenantId &&
                item.BusinessPartnerRole.BusinessPartnerId == businessPartnerId, cancellationToken);
        if (profile is null) return NotFound();
        if (profile.Status != BusinessPartnerFinanceProfileStatus.Draft)
            return ConflictProblem("PROFILE_NOT_DRAFT", "Only a draft AP profile can be edited.");
        if (profile.BusinessPartnerRoleId != request.BusinessPartnerRoleId)
            return BadRequestProblem("PROFILE_ROLE_IMMUTABLE", "The AP profile role cannot be changed after the draft is created.");
        var validation = await ValidateApRequestAsync(businessPartnerId, request, cancellationToken);
        if (validation is not null) return validation;

        profile.DefaultWithholdingLineId = null;
        profile.UpdatedAt = DateTime.UtcNow;
        profile.LastModifiedById = UserId;
        await _db.SaveChangesAsync(cancellationToken);
        _db.BusinessPartnerApWhtDefaults.RemoveRange(profile.WithholdingDefaults);
        profile.WithholdingDefaults.Clear();
        ApplyApFields(profile, request);
        AddWithholdingLines(profile, request.WithholdingDefaults);
        await _db.SaveChangesAsync(cancellationToken);
        profile.DefaultWithholdingLineId = profile.WithholdingDefaults.SingleOrDefault(item => item.IsDefaultForAp)?.Id;
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(await MapApAsync(profile.Id, cancellationToken));
    }

    [HttpPost("{businessPartnerId:guid}/ap/{profileId:guid}/submit")]
    [Authorize(Policy = FinancePermissions.ManageBusinessPartnerFinanceProfiles)]
    public async Task<ActionResult<BusinessPartnerApProfileDto>> SubmitAp(
        Guid businessPartnerId, Guid profileId, CancellationToken cancellationToken)
    {
        var profile = await FindApAsync(businessPartnerId, profileId, cancellationToken);
        if (profile is null) return NotFound();
        if (profile.Status != BusinessPartnerFinanceProfileStatus.Draft)
            return ConflictProblem("PROFILE_NOT_DRAFT", "Only a draft AP profile can be submitted.");
        var readiness = ValidateApApproval(profile);
        if (readiness is not null) return readiness;
        var configuration = await ValidateStoredApAsync(businessPartnerId, profile, cancellationToken);
        if (configuration is not null) return configuration;
        profile.Status = BusinessPartnerFinanceProfileStatus.Submitted;
        profile.SubmittedById = UserId;
        profile.SubmittedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(await MapApAsync(profile.Id, cancellationToken));
    }

    [HttpPost("{businessPartnerId:guid}/ap/{profileId:guid}/approve")]
    [Authorize(Policy = FinancePermissions.ApproveBusinessPartnerFinanceProfiles)]
    public async Task<ActionResult<BusinessPartnerApProfileDto>> ApproveAp(
        Guid businessPartnerId,
        Guid profileId,
        [FromBody] BusinessPartnerFinanceProfileDecisionRequest request,
        CancellationToken cancellationToken)
    {
        var profile = await FindApAsync(businessPartnerId, profileId, cancellationToken);
        if (profile is null) return NotFound();
        if (profile.Status != BusinessPartnerFinanceProfileStatus.Submitted)
            return ConflictProblem("PROFILE_NOT_SUBMITTED", "Only a submitted AP profile can be approved.");
        if (profile.SubmittedById == UserId)
            return ConflictProblem("MAKER_CHECKER_REQUIRED", "The user who submitted this AP profile cannot approve it.");
        var readiness = ValidateApApproval(profile);
        if (readiness is not null) return readiness;
        var configuration = await ValidateStoredApAsync(businessPartnerId, profile, cancellationToken);
        if (configuration is not null) return configuration;
        var candidates = await _db.BusinessPartnerApProfileVersions.AsNoTracking()
            .Where(item => item.TenantId == TenantId && item.BusinessPartnerRoleId == profile.BusinessPartnerRoleId)
            .ToListAsync(cancellationToken);
        if (BusinessPartnerFinanceProfilePolicy.HasApprovedOverlap(
            candidates, profile.BusinessPartnerRoleId, profile.EffectiveFrom, profile.EffectiveTo, profile.Id))
            return ConflictProblem("PROFILE_EFFECTIVE_PERIOD_OVERLAP", "An approved AP profile already covers some or all of this effective period.");

        profile.Status = BusinessPartnerFinanceProfileStatus.Approved;
        profile.ApprovedById = UserId;
        profile.ApprovedAtUtc = DateTime.UtcNow;
        profile.DecisionReason = NormalizeReason(request.Reason);
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(await MapApAsync(profile.Id, cancellationToken));
    }

    [HttpPost("{businessPartnerId:guid}/ap/{profileId:guid}/reject")]
    [Authorize(Policy = FinancePermissions.ApproveBusinessPartnerFinanceProfiles)]
    public async Task<ActionResult<BusinessPartnerApProfileDto>> RejectAp(
        Guid businessPartnerId,
        Guid profileId,
        [FromBody] BusinessPartnerFinanceProfileDecisionRequest request,
        CancellationToken cancellationToken)
    {
        var profile = await FindApAsync(businessPartnerId, profileId, cancellationToken);
        if (profile is null) return NotFound();
        if (profile.Status != BusinessPartnerFinanceProfileStatus.Submitted)
            return ConflictProblem("PROFILE_NOT_SUBMITTED", "Only a submitted AP profile can be rejected.");
        if (string.IsNullOrWhiteSpace(request.Reason))
            return BadRequestProblem("DECISION_REASON_REQUIRED", "Enter a rejection reason.");
        if (profile.SubmittedById == UserId)
            return ConflictProblem("MAKER_CHECKER_REQUIRED", "The user who submitted this AP profile cannot decide it.");
        profile.Status = BusinessPartnerFinanceProfileStatus.Rejected;
        profile.ApprovedById = UserId;
        profile.ApprovedAtUtc = DateTime.UtcNow;
        profile.DecisionReason = request.Reason.Trim();
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(await MapApAsync(profile.Id, cancellationToken));
    }

    [HttpPost("{businessPartnerId:guid}/ar")]
    [Authorize(Policy = FinancePermissions.ManageBusinessPartnerFinanceProfiles)]
    public async Task<ActionResult<BusinessPartnerArProfileDto>> CreateArDraft(
        Guid businessPartnerId,
        [FromBody] SaveBusinessPartnerArProfileRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await ValidateArRequestAsync(businessPartnerId, request, cancellationToken);
        if (validation is not null) return validation;
        var version = (await _db.BusinessPartnerArProfileVersions
            .Where(item => item.TenantId == TenantId && item.BusinessPartnerRoleId == request.BusinessPartnerRoleId)
            .MaxAsync(item => (int?)item.VersionNumber, cancellationToken) ?? 0) + 1;
        var profile = new BusinessPartnerArProfileVersion
        {
            Id = Guid.NewGuid(), TenantId = TenantId, BusinessPartnerRoleId = request.BusinessPartnerRoleId,
            VersionNumber = version, Status = BusinessPartnerFinanceProfileStatus.Draft, CreatedById = UserId
        };
        ApplyArFields(profile, request);
        _db.BusinessPartnerArProfileVersions.Add(profile);
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(MapAr(profile));
    }

    [HttpPut("{businessPartnerId:guid}/ar/{profileId:guid}")]
    [Authorize(Policy = FinancePermissions.ManageBusinessPartnerFinanceProfiles)]
    public async Task<ActionResult<BusinessPartnerArProfileDto>> UpdateArDraft(
        Guid businessPartnerId,
        Guid profileId,
        [FromBody] SaveBusinessPartnerArProfileRequest request,
        CancellationToken cancellationToken)
    {
        var profile = await FindArAsync(businessPartnerId, profileId, cancellationToken);
        if (profile is null) return NotFound();
        if (profile.Status != BusinessPartnerFinanceProfileStatus.Draft)
            return ConflictProblem("PROFILE_NOT_DRAFT", "Only a draft AR profile can be edited.");
        if (profile.BusinessPartnerRoleId != request.BusinessPartnerRoleId)
            return BadRequestProblem("PROFILE_ROLE_IMMUTABLE", "The AR profile role cannot be changed after the draft is created.");
        var validation = await ValidateArRequestAsync(businessPartnerId, request, cancellationToken);
        if (validation is not null) return validation;
        ApplyArFields(profile, request);
        profile.UpdatedAt = DateTime.UtcNow;
        profile.LastModifiedById = UserId;
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(MapAr(profile));
    }

    [HttpPost("{businessPartnerId:guid}/ar/{profileId:guid}/submit")]
    [Authorize(Policy = FinancePermissions.ManageBusinessPartnerFinanceProfiles)]
    public async Task<ActionResult<BusinessPartnerArProfileDto>> SubmitAr(
        Guid businessPartnerId, Guid profileId, CancellationToken cancellationToken)
    {
        var profile = await FindArAsync(businessPartnerId, profileId, cancellationToken);
        if (profile is null) return NotFound();
        if (profile.Status != BusinessPartnerFinanceProfileStatus.Draft)
            return ConflictProblem("PROFILE_NOT_DRAFT", "Only a draft AR profile can be submitted.");
        var termsValidation = await ValidatePaymentTermAsync(profile.PaymentTermId, BusinessPartnerRoleType.Customer, cancellationToken);
        if (termsValidation is not null) return termsValidation;
        profile.Status = BusinessPartnerFinanceProfileStatus.Submitted;
        profile.SubmittedById = UserId;
        profile.SubmittedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(MapAr(profile));
    }

    [HttpPost("{businessPartnerId:guid}/ar/{profileId:guid}/approve")]
    [Authorize(Policy = FinancePermissions.ApproveBusinessPartnerFinanceProfiles)]
    public async Task<ActionResult<BusinessPartnerArProfileDto>> ApproveAr(
        Guid businessPartnerId,
        Guid profileId,
        [FromBody] BusinessPartnerFinanceProfileDecisionRequest request,
        CancellationToken cancellationToken)
    {
        var profile = await FindArAsync(businessPartnerId, profileId, cancellationToken);
        if (profile is null) return NotFound();
        if (profile.Status != BusinessPartnerFinanceProfileStatus.Submitted)
            return ConflictProblem("PROFILE_NOT_SUBMITTED", "Only a submitted AR profile can be approved.");
        if (profile.SubmittedById == UserId)
            return ConflictProblem("MAKER_CHECKER_REQUIRED", "The user who submitted this AR profile cannot approve it.");
        var termsValidation = await ValidatePaymentTermAsync(profile.PaymentTermId, BusinessPartnerRoleType.Customer, cancellationToken);
        if (termsValidation is not null) return termsValidation;
        var candidates = await _db.BusinessPartnerArProfileVersions.AsNoTracking()
            .Where(item => item.TenantId == TenantId && item.BusinessPartnerRoleId == profile.BusinessPartnerRoleId)
            .ToListAsync(cancellationToken);
        if (BusinessPartnerFinanceProfilePolicy.HasApprovedOverlap(
            candidates, profile.BusinessPartnerRoleId, profile.EffectiveFrom, profile.EffectiveTo, profile.Id))
            return ConflictProblem("PROFILE_EFFECTIVE_PERIOD_OVERLAP", "An approved AR profile already covers some or all of this effective period.");
        profile.Status = BusinessPartnerFinanceProfileStatus.Approved;
        profile.ApprovedById = UserId;
        profile.ApprovedAtUtc = DateTime.UtcNow;
        profile.DecisionReason = NormalizeReason(request.Reason);
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(MapAr(profile));
    }

    [HttpPost("{businessPartnerId:guid}/ar/{profileId:guid}/reject")]
    [Authorize(Policy = FinancePermissions.ApproveBusinessPartnerFinanceProfiles)]
    public async Task<ActionResult<BusinessPartnerArProfileDto>> RejectAr(
        Guid businessPartnerId,
        Guid profileId,
        [FromBody] BusinessPartnerFinanceProfileDecisionRequest request,
        CancellationToken cancellationToken)
    {
        var profile = await FindArAsync(businessPartnerId, profileId, cancellationToken);
        if (profile is null) return NotFound();
        if (profile.Status != BusinessPartnerFinanceProfileStatus.Submitted)
            return ConflictProblem("PROFILE_NOT_SUBMITTED", "Only a submitted AR profile can be rejected.");
        if (string.IsNullOrWhiteSpace(request.Reason))
            return BadRequestProblem("DECISION_REASON_REQUIRED", "Enter a rejection reason.");
        if (profile.SubmittedById == UserId)
            return ConflictProblem("MAKER_CHECKER_REQUIRED", "The user who submitted this AR profile cannot decide it.");
        profile.Status = BusinessPartnerFinanceProfileStatus.Rejected;
        profile.ApprovedById = UserId;
        profile.ApprovedAtUtc = DateTime.UtcNow;
        profile.DecisionReason = request.Reason.Trim();
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(MapAr(profile));
    }

    private Guid TenantId => _currentUser.TenantId;
    private Guid UserId => _currentUser.UserId;

    private async Task<ActionResult?> ValidateApRequestAsync(
        Guid partnerId, SaveBusinessPartnerApProfileRequest request, CancellationToken cancellationToken)
    {
        var role = await _db.BusinessPartnerRoles.AsNoTracking().SingleOrDefaultAsync(item =>
            item.Id == request.BusinessPartnerRoleId && item.TenantId == TenantId &&
            item.BusinessPartnerId == partnerId && !item.IsDeleted, cancellationToken);
        if (role is null) return NotFoundProblem("BUSINESS_PARTNER_ROLE_NOT_FOUND", "The selected Business Partner role was not found.");
        if (role.RoleType is not (BusinessPartnerRoleType.Supplier or BusinessPartnerRoleType.Contractor))
            return BadRequestProblem("AP_ROLE_REQUIRED", "Select an active Supplier or Contractor role for an AP profile.");
        if (role.Status != BusinessPartnerRoleStatus.Active)
            return BadRequestProblem("AP_ROLE_INACTIVE", "The selected AP role is inactive.");
        if (request.EffectiveTo.HasValue && request.EffectiveTo.Value.Date < request.EffectiveFrom.Date)
            return BadRequestProblem("EFFECTIVE_PERIOD_INVALID", "Effective through cannot be earlier than effective from.");
        var defaultsValidation = await ValidateApDefaultsAsync(request.PaymentTermId, request.DefaultExpenseAccountId,
            request.DefaultTaxGroupId, role.RoleType, request.EffectiveFrom.Date, cancellationToken);
        if (defaultsValidation is not null) return defaultsValidation;
        if (request.WithholdingDefaults is null || request.WithholdingDefaults.Any(item => item is null || string.IsNullOrWhiteSpace(item.CategoryCode)))
            return BadRequestProblem("WHT_CATEGORY_REQUIRED", "Every WHT default requires a category code.");
        if (request.WithholdingDefaults.GroupBy(item => item.CategoryCode.Trim(), StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
            return BadRequestProblem("WHT_CATEGORY_DUPLICATE", "Each WHT category may appear only once in an AP profile.");
        if (request.WithholdingDefaults.Count(item => item.IsActive && item.IsDefaultForAp) > 1)
            return BadRequestProblem("WHT_DEFAULT_AMBIGUOUS", "Select no more than one default WHT configuration for AP.");
        if (request.SubjectToWithholding && request.WithholdingDefaults.Count(item => item.IsActive && item.IsDefaultForAp) != 1)
            return BadRequestProblem("AP_WHT_DEFAULT_REQUIRED", "A WHT-applicable AP profile requires exactly one active default WHT configuration.");
        if (request.WithholdingDefaults.Any(item => string.IsNullOrWhiteSpace(item.CategoryCode)))
            return BadRequestProblem("WHT_CATEGORY_REQUIRED", "Every WHT default requires a category code.");

        var taxIds = request.WithholdingDefaults.Select(item => item.WithholdingTaxId).Distinct().ToList();
        var validTaxCount = await _db.Taxes.AsNoTracking().CountAsync(item => taxIds.Contains(item.Id) &&
            item.TenantId == TenantId && item.IsActive && !item.IsDeleted && item.Category == TaxCategory.Withholding &&
            (item.Applicability == TaxApplicability.Purchases || item.Applicability == TaxApplicability.Both), cancellationToken);
        if (validTaxCount != taxIds.Count)
            return BadRequestProblem("WHT_CONFIGURATION_INVALID", "One or more WHT configurations are inactive, outside this tenant, or not purchase WHT.");
        return null;
    }

    private async Task<ActionResult?> ValidateArRequestAsync(
        Guid partnerId, SaveBusinessPartnerArProfileRequest request, CancellationToken cancellationToken)
    {
        var role = await _db.BusinessPartnerRoles.AsNoTracking().SingleOrDefaultAsync(item =>
            item.Id == request.BusinessPartnerRoleId && item.TenantId == TenantId &&
            item.BusinessPartnerId == partnerId && !item.IsDeleted, cancellationToken);
        if (role is null) return NotFoundProblem("BUSINESS_PARTNER_ROLE_NOT_FOUND", "The selected Business Partner role was not found.");
        if (role.RoleType != BusinessPartnerRoleType.Customer)
            return BadRequestProblem("AR_ROLE_REQUIRED", "Select an active Customer role for an AR profile.");
        if (role.Status != BusinessPartnerRoleStatus.Active)
            return BadRequestProblem("AR_ROLE_INACTIVE", "The selected Customer role is inactive.");
        if (request.EffectiveTo.HasValue && request.EffectiveTo.Value.Date < request.EffectiveFrom.Date)
            return BadRequestProblem("EFFECTIVE_PERIOD_INVALID", "Effective through cannot be earlier than effective from.");
        if (request.CreditLimit < 0)
            return BadRequestProblem("CREDIT_LIMIT_INVALID", "Credit limit must be zero or greater.");
        return await ValidatePaymentTermAsync(request.PaymentTermId, BusinessPartnerRoleType.Customer, cancellationToken);
    }

    private async Task<ActionResult?> ValidateApDefaultsAsync(Guid? paymentTermId, Guid? expenseAccountId,
        Guid? taxGroupId, BusinessPartnerRoleType roleType, DateTime effectiveFrom, CancellationToken cancellationToken)
    {
        var termsValidation = await ValidatePaymentTermAsync(paymentTermId, roleType, cancellationToken);
        if (termsValidation is not null) return termsValidation;
        if (expenseAccountId.HasValue && !await _db.Accounts.AsNoTracking().AnyAsync(account =>
                account.Id == expenseAccountId.Value && account.TenantId == TenantId && !account.IsDeleted &&
                account.Status == AccountStatus.Active && account.AllowDirectPosting && !account.IsControlAccount &&
                (account.AccountType == AccountType.Expense || account.AccountType == AccountType.Asset) &&
                (!account.EffectiveDate.HasValue || account.EffectiveDate <= effectiveFrom) &&
                (!account.ExpirationDate.HasValue || account.ExpirationDate > effectiveFrom), cancellationToken))
            return BadRequestProblem("AP_EXPENSE_ACCOUNT_INVALID", "Select an active, directly postable expense or asset account from the current tenant.");
        if (taxGroupId.HasValue && !await _db.TaxGroups.AsNoTracking().AnyAsync(group =>
                group.Id == taxGroupId.Value && group.TenantId == TenantId && !group.IsDeleted && group.IsActive &&
                (group.Applicability == TaxApplicability.Purchases || group.Applicability == TaxApplicability.Both),
                cancellationToken))
            return BadRequestProblem("AP_TAX_GROUP_INVALID", "Select an active purchase tax group from the current tenant.");
        return null;
    }

    private async Task<ActionResult?> ValidatePaymentTermAsync(Guid? paymentTermId,
        BusinessPartnerRoleType roleType, CancellationToken cancellationToken)
    {
        if (!paymentTermId.HasValue) return null;
        var term = await _db.PaymentTerms.AsNoTracking().SingleOrDefaultAsync(item => item.Id == paymentTermId.Value &&
            item.TenantId == TenantId && !item.IsDeleted && item.IsActive, cancellationToken);
        if (term is null || !(string.Equals(term.ApplicableTo, "All", StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(term.ApplicableTo, roleType.ToString(), StringComparison.OrdinalIgnoreCase) ||
                              (roleType == BusinessPartnerRoleType.Supplier && string.Equals(term.ApplicableTo, "Vendor", StringComparison.OrdinalIgnoreCase)) ||
                              (roleType == BusinessPartnerRoleType.Customer && string.Equals(term.ApplicableTo, "Client", StringComparison.OrdinalIgnoreCase))))
            return BadRequestProblem("PROFILE_PAYMENT_TERM_INVALID", "Select an active payment term applicable to this partner role from the current tenant.");
        return null;
    }

    private Task<ActionResult?> ValidateStoredApAsync(Guid partnerId, BusinessPartnerApProfileVersion profile,
        CancellationToken cancellationToken) => ValidateApRequestAsync(partnerId, new SaveBusinessPartnerApProfileRequest
        {
            BusinessPartnerRoleId = profile.BusinessPartnerRoleId,
            EffectiveFrom = profile.EffectiveFrom, EffectiveTo = profile.EffectiveTo,
            PaymentTermId = profile.PaymentTermId, DefaultExpenseAccountId = profile.DefaultExpenseAccountId,
            DefaultTaxGroupId = profile.DefaultTaxGroupId, SubjectToWithholding = profile.SubjectToWithholding,
            WithholdingDefaults = profile.WithholdingDefaults.Where(line => !line.IsDeleted).Select(line =>
                new SaveBusinessPartnerApWhtDefaultRequest
                {
                    CategoryCode = line.CategoryCode, CategoryName = line.CategoryName,
                    WithholdingTaxId = line.WithholdingTaxId, IsDefaultForAp = line.IsDefaultForAp, IsActive = line.IsActive
                }).ToList()
        }, cancellationToken);

    private async Task<ActionResult<BusinessPartnerApProfileDto>> ExecuteApWriteAsync(
        Func<Task<ActionResult<BusinessPartnerApProfileDto>>> operation, CancellationToken cancellationToken)
    {
        if (!_db.Database.IsRelational() || _db.Database.CurrentTransaction is not null)
            return await operation();
        return await _db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
            try
            {
                var result = await operation();
                await transaction.CommitAsync(cancellationToken);
                return result;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                _db.ChangeTracker.Clear();
                throw;
            }
        });
    }

    private ActionResult? ValidateApApproval(BusinessPartnerApProfileVersion profile)
    {
        if (!profile.SubjectToWithholding) return null;
        if (string.IsNullOrWhiteSpace(profile.BusinessPartnerRole.BusinessPartner.TaxIdentificationNumber))
            return BadRequestProblem("AP_WHT_TIN_REQUIRED", "Enter the Business Partner TIN before submitting a WHT-applicable AP profile.");
        if (profile.WithholdingDefaults.Count(item => !item.IsDeleted && item.IsActive && item.IsDefaultForAp) != 1)
            return BadRequestProblem("AP_WHT_DEFAULT_REQUIRED", "Select exactly one active default WHT configuration before submitting the AP profile.");
        return null;
    }

    private async Task<BusinessPartnerApProfileVersion?> FindApAsync(Guid partnerId, Guid profileId, CancellationToken cancellationToken) =>
        await _db.BusinessPartnerApProfileVersions
            .Include(item => item.BusinessPartnerRole).ThenInclude(role => role.BusinessPartner)
            .Include(item => item.WithholdingDefaults).ThenInclude(line => line.WithholdingTax)
            .SingleOrDefaultAsync(item => item.Id == profileId && item.TenantId == TenantId &&
                item.BusinessPartnerRole.BusinessPartnerId == partnerId, cancellationToken);

    private async Task<BusinessPartnerArProfileVersion?> FindArAsync(Guid partnerId, Guid profileId, CancellationToken cancellationToken) =>
        await _db.BusinessPartnerArProfileVersions
            .Include(item => item.BusinessPartnerRole)
            .SingleOrDefaultAsync(item => item.Id == profileId && item.TenantId == TenantId &&
                item.BusinessPartnerRole.BusinessPartnerId == partnerId, cancellationToken);

    private static void ApplyApFields(BusinessPartnerApProfileVersion profile, SaveBusinessPartnerApProfileRequest request)
    {
        profile.EffectiveFrom = request.EffectiveFrom.Date;
        profile.EffectiveTo = request.EffectiveTo?.Date;
        profile.ApReferenceNumber = TrimOrNull(request.ApReferenceNumber);
        profile.PaymentTermId = request.PaymentTermId;
        profile.DefaultTaxGroupId = request.DefaultTaxGroupId;
        profile.DefaultExpenseAccountId = request.DefaultExpenseAccountId;
        profile.SubjectToWithholding = request.SubjectToWithholding;
    }

    private void AddWithholdingLines(BusinessPartnerApProfileVersion profile, IEnumerable<SaveBusinessPartnerApWhtDefaultRequest> requests)
    {
        foreach (var request in requests)
        {
            profile.WithholdingDefaults.Add(new BusinessPartnerApWhtDefault
            {
                Id = Guid.NewGuid(), TenantId = TenantId, ApProfileVersionId = profile.Id,
                CategoryCode = request.CategoryCode.Trim().ToUpperInvariant(),
                CategoryName = TrimOrNull(request.CategoryName), WithholdingTaxId = request.WithholdingTaxId,
                IsDefaultForAp = request.IsDefaultForAp, IsActive = request.IsActive, CreatedById = UserId
            });
        }
    }

    private static void ApplyArFields(BusinessPartnerArProfileVersion profile, SaveBusinessPartnerArProfileRequest request)
    {
        profile.EffectiveFrom = request.EffectiveFrom.Date;
        profile.EffectiveTo = request.EffectiveTo?.Date;
        profile.ArReferenceNumber = TrimOrNull(request.ArReferenceNumber);
        profile.PaymentTermId = request.PaymentTermId;
        profile.CreditLimit = request.CreditLimit;
        profile.IsWithholdingAgent = request.IsWithholdingAgent;
    }

    private async Task<BusinessPartnerFinanceProfileSetDto?> LoadAsync(Guid businessPartnerId, CancellationToken cancellationToken)
    {
        var partner = await _db.BusinessPartners.AsNoTracking().SingleOrDefaultAsync(item =>
            item.Id == businessPartnerId && item.TenantId == TenantId && !item.IsDeleted, cancellationToken);
        if (partner is null) return null;
        var roles = await _db.BusinessPartnerRoles.AsNoTracking()
            .Where(item => item.BusinessPartnerId == businessPartnerId && item.TenantId == TenantId && !item.IsDeleted)
            .OrderBy(item => item.RoleType).ToListAsync(cancellationToken);
        var roleIds = roles.Select(item => item.Id).ToList();
        var ap = await _db.BusinessPartnerApProfileVersions.AsNoTracking()
            .Include(item => item.WithholdingDefaults).ThenInclude(line => line.WithholdingTax)
            .Where(item => item.TenantId == TenantId && roleIds.Contains(item.BusinessPartnerRoleId) && !item.IsDeleted)
            .OrderByDescending(item => item.VersionNumber).ToListAsync(cancellationToken);
        var ar = await _db.BusinessPartnerArProfileVersions.AsNoTracking()
            .Where(item => item.TenantId == TenantId && roleIds.Contains(item.BusinessPartnerRoleId) && !item.IsDeleted)
            .OrderByDescending(item => item.VersionNumber).ToListAsync(cancellationToken);
        return new BusinessPartnerFinanceProfileSetDto
        {
            BusinessPartnerId = partner.Id, PartnerCode = partner.PartnerCode, PartnerName = partner.PartnerName,
            TaxIdentificationNumber = partner.TaxIdentificationNumber,
            Roles = roles.Select(role => new BusinessPartnerFinanceRoleDto
            {
                Id = role.Id, RoleType = role.RoleType.ToString(), Status = role.Status.ToString(), ActiveFromUtc = role.ActiveFromUtc,
                ApProfiles = ap.Where(item => item.BusinessPartnerRoleId == role.Id).Select(MapAp).ToList(),
                ArProfiles = ar.Where(item => item.BusinessPartnerRoleId == role.Id).Select(MapAr).ToList()
            }).ToList()
        };
    }

    private async Task<BusinessPartnerApProfileDto> MapApAsync(Guid profileId, CancellationToken cancellationToken)
    {
        var profile = await _db.BusinessPartnerApProfileVersions.AsNoTracking()
            .Include(item => item.WithholdingDefaults).ThenInclude(line => line.WithholdingTax)
            .SingleAsync(item => item.Id == profileId && item.TenantId == TenantId && !item.IsDeleted, cancellationToken);
        return MapAp(profile);
    }

    private static BusinessPartnerApProfileDto MapAp(BusinessPartnerApProfileVersion profile) => new()
    {
        Id = profile.Id, BusinessPartnerRoleId = profile.BusinessPartnerRoleId, VersionNumber = profile.VersionNumber,
        Status = profile.Status.ToString(), EffectiveFrom = profile.EffectiveFrom, EffectiveTo = profile.EffectiveTo,
        ApReferenceNumber = profile.ApReferenceNumber, PaymentTermId = profile.PaymentTermId,
        DefaultTaxGroupId = profile.DefaultTaxGroupId, DefaultExpenseAccountId = profile.DefaultExpenseAccountId,
        SubjectToWithholding = profile.SubjectToWithholding, SubmittedById = profile.SubmittedById,
        SubmittedAtUtc = profile.SubmittedAtUtc, ApprovedById = profile.ApprovedById,
        ApprovedAtUtc = profile.ApprovedAtUtc, DecisionReason = profile.DecisionReason,
        WithholdingDefaults = profile.WithholdingDefaults.Where(item => !item.IsDeleted).Select(item => new BusinessPartnerApWhtDefaultDto
        {
            Id = item.Id, CategoryCode = item.CategoryCode, CategoryName = item.CategoryName,
            WithholdingTaxId = item.WithholdingTaxId, WithholdingTaxCode = item.WithholdingTax?.Code ?? string.Empty,
            WithholdingTaxName = item.WithholdingTax?.Name ?? string.Empty, Rate = item.WithholdingTax?.Rate ?? 0,
            IsDefaultForAp = item.IsDefaultForAp, IsActive = item.IsActive
        }).ToList()
    };

    private static BusinessPartnerArProfileDto MapAr(BusinessPartnerArProfileVersion profile) => new()
    {
        Id = profile.Id, BusinessPartnerRoleId = profile.BusinessPartnerRoleId, VersionNumber = profile.VersionNumber,
        Status = profile.Status.ToString(), EffectiveFrom = profile.EffectiveFrom, EffectiveTo = profile.EffectiveTo,
        ArReferenceNumber = profile.ArReferenceNumber, PaymentTermId = profile.PaymentTermId,
        CreditLimit = profile.CreditLimit, IsWithholdingAgent = profile.IsWithholdingAgent,
        SubmittedById = profile.SubmittedById, SubmittedAtUtc = profile.SubmittedAtUtc,
        ApprovedById = profile.ApprovedById, ApprovedAtUtc = profile.ApprovedAtUtc,
        DecisionReason = profile.DecisionReason
    };

    private ActionResult BadRequestProblem(string code, string detail) =>
        Problem(statusCode: StatusCodes.Status400BadRequest, title: code, detail: detail);
    private ActionResult ConflictProblem(string code, string detail) =>
        Problem(statusCode: StatusCodes.Status409Conflict, title: code, detail: detail);
    private ActionResult NotFoundProblem(string code, string detail) =>
        Problem(statusCode: StatusCodes.Status404NotFound, title: code, detail: detail);
    private static string? TrimOrNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string? NormalizeReason(string? value) => TrimOrNull(value);
}
