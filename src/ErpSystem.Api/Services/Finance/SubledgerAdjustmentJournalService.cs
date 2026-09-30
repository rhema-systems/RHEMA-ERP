using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Numbering;
using ErpSystem.Core.Services.Finance;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance;

public partial class SubledgerAdjustmentJournalService : ISubledgerAdjustmentJournalService
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IDocumentNumberingService _documentNumberingService;
    private readonly IFinancePostingEngine _postingEngine;
    private readonly ITenantSettingsService _tenantSettingsService;
    private readonly IFinanceSourceBookAuthorityService _sourceBookAuthority;

    public SubledgerAdjustmentJournalService(
        ApplicationDbContext context,
        ICurrentUserService currentUser,
        IDocumentNumberingService documentNumberingService,
        IFinancePostingEngine postingEngine,
        ITenantSettingsService tenantSettingsService,
        IFinanceSourceBookAuthorityService sourceBookAuthority)
    {
        _context = context;
        _currentUser = currentUser;
        _documentNumberingService = documentNumberingService;
        _postingEngine = postingEngine;
        _tenantSettingsService = tenantSettingsService;
        _sourceBookAuthority = sourceBookAuthority;
    }

    private Guid TenantId => _currentUser.GetRequiredFinanceTenantId();
    private string UserName => string.IsNullOrWhiteSpace(_currentUser.UserName) ? "System" : _currentUser.UserName;

    public async Task<IReadOnlyList<SubledgerAdjustmentJournalDto>> GetAllAsync(
        string? module = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedModule = NormalizeModule(module, allowNull: true);
        var query = _context.SubledgerAdjustmentJournals
            .AsNoTracking()
            .Include(a => a.BusinessPartner)
            .Include(a => a.BusinessPartnerRole)
            .Include(a => a.ContraAccount)
            .Include(a => a.JournalEntry)
            .Where(a => a.TenantId == TenantId && !a.IsDeleted);

        if (!string.IsNullOrWhiteSpace(normalizedModule))
            query = query.Where(a => a.Module == normalizedModule);

        var adjustments = await query
            .OrderByDescending(a => a.AdjustmentDate)
            .ThenByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);

        return adjustments.Select(MapToDto).ToList();
    }

    public async Task<SubledgerAdjustmentJournalDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var adjustment = await LoadAdjustmentAsync(id, asNoTracking: true, cancellationToken);
        return adjustment == null ? null : MapToDto(adjustment);
    }

    public async Task<SubledgerAdjustmentJournalDto> CreateAndPostAsync(
        CreateSubledgerAdjustmentJournalDto dto,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken)
            : await _context.Database.BeginTransactionAsync(cancellationToken);
        var replay = await FindCustomerAdjustmentReplayAsync(dto, cancellationToken);
        if (replay != null)
        {
            await RequireBoundAdjustmentAuthorityAsync(replay, cancellationToken);
            return MapToDto(replay);
        }
        var adjustment = await CreateAndPostCoreAsync(dto, null, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return MapToDto(adjustment);
    }

    public async Task<SubledgerAdjustmentJournalDto> ReverseAsync(
        Guid id,
        ReverseSubledgerAdjustmentJournalDto dto,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Reason))
            throw new InvalidOperationException("A reversal reason is required.");

        await using var transaction = _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken)
            : await _context.Database.BeginTransactionAsync(cancellationToken);

        var original = await LoadAdjustmentAsync(id, asNoTracking: false, cancellationToken)
            ?? throw new KeyNotFoundException($"Subledger adjustment journal '{id}' was not found.");

        if (!string.Equals(original.Status, SubledgerAdjustmentStatuses.Posted, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only posted subledger adjustment journals can be reversed.");
        if (original.ReversalAdjustmentId.HasValue)
            throw new InvalidOperationException("This subledger adjustment journal has already been reversed.");
        if (original.OriginalAdjustmentId.HasValue)
            throw new InvalidOperationException("Reversal adjustment journals cannot be reversed from this action. Reverse the original adjustment instead.");

        var reversalDto = new CreateSubledgerAdjustmentJournalDto
        {
            Module = original.Module,
            Purpose = original.Purpose,
            BusinessPartnerId = original.BusinessPartnerId,
            BusinessPartnerRoleId = original.BusinessPartnerRoleId,
            AdjustmentDate = (dto.ReversalDate ?? DateTime.UtcNow).Date,
            DueDate = original.DueDate,
            AdjustmentType = string.Equals(original.AdjustmentType, SubledgerAdjustmentTypes.Debit, StringComparison.OrdinalIgnoreCase)
                ? SubledgerAdjustmentTypes.Credit
                : SubledgerAdjustmentTypes.Debit,
            Amount = original.Amount,
            CurrencyCode = original.CurrencyCode,
            ExchangeRate = original.ExchangeRate,
            ContraAccountId = original.ContraAccountId,
            Reference = $"REV-{original.AdjustmentNumber}",
            Reason = $"Reversal of {original.AdjustmentNumber}: {dto.Reason.Trim()}",
            Notes = original.Notes
        };

        var reversal = await CreateAndPostCoreAsync(reversalDto, original.Id, cancellationToken);

        original.Status = SubledgerAdjustmentStatuses.Reversed;
        original.ReversalAdjustmentId = reversal.Id;
        original.ReversalReason = dto.Reason.Trim();
        original.ReversedAt = DateTime.UtcNow;
        original.UpdatedAt = DateTime.UtcNow;
        original.UpdatedBy = UserName;
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var reloaded = await LoadAdjustmentAsync(original.Id, asNoTracking: true, cancellationToken)
            ?? original;
        return MapToDto(reloaded);
    }

    private async Task<SubledgerAdjustmentJournal> CreateAndPostCoreAsync(
        CreateSubledgerAdjustmentJournalDto dto,
        Guid? originalAdjustmentId,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("Tenant context is required.");

        var module = NormalizeModule(dto.Module, allowNull: false)!;
        var purpose = NormalizePurpose(dto.Purpose, allowRetiredOpeningBalance: originalAdjustmentId.HasValue);
        var adjustmentType = NormalizeAdjustmentType(dto.AdjustmentType);
        if (dto.Amount <= 0)
            throw new InvalidOperationException("Adjustment amount must be greater than zero.");
        if (string.IsNullOrWhiteSpace(dto.Reason))
            throw new InvalidOperationException("A reason is required.");
        var counterparty = await ResolveCounterpartyAsync(
            module,
            dto.BusinessPartnerId,
            dto.BusinessPartnerRoleId,
            dto.AdjustmentDate,
            cancellationToken);
        var originalAdjustment = originalAdjustmentId.HasValue
            ? await LoadAdjustmentAsync(originalAdjustmentId.Value, false, cancellationToken) : null;
        var accountingBookCode = originalAdjustment is null
            ? await ResolvePrimaryAccountingBookCodeAsync(tenantId, cancellationToken)
            : await ResolveOriginalAccountingBookCodeAsync(originalAdjustment, tenantId, cancellationToken);

        var settings = await _context.FinanceSettings
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && !s.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("Finance settings are not configured for this tenant.");

        var controlAccountId = module == SubledgerModules.AccountsReceivable
            ? settings.ControlAccountArId
            : settings.ControlAccountApId;
        if (IsCustomerAccountPurpose(purpose))
            controlAccountId = originalAdjustment?.ControlAccountId ?? controlAccountId;
        if (!controlAccountId.HasValue)
            throw new InvalidOperationException($"{module} control account is not configured in Finance Settings.");

        var controlAccount = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == controlAccountId.Value && a.TenantId == tenantId && !a.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException($"{module} control account was not found.");

        var requestedContraAccountId = await ResolveCustomerAdjustmentAccountAsync(dto, purpose, adjustmentType,
            module == SubledgerModules.AccountsReceivable ? counterparty.Partner : null, originalAdjustment, cancellationToken);
        var contraAccountId = ResolveContraAccountId(
            requestedContraAccountId,
            purpose,
            settings,
            originalAdjustmentId.HasValue);
        var contraAccount = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == contraAccountId && a.TenantId == tenantId && !a.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("Contra GL account was not found.");

        if (contraAccount.IsControlAccount)
            throw new InvalidOperationException("Contra GL account cannot be a control account. Use the relevant subledger flow for control accounts.");
        if (contraAccount.Id == controlAccount.Id)
            throw new InvalidOperationException("Contra GL account cannot be the same as the subledger control account.");

        var baseCurrencyCode = NormalizeCurrency(await _tenantSettingsService.GetBaseCurrencyAsync()) ?? "GHS";
        var currencyCode = NormalizeCurrency(dto.CurrencyCode) ?? baseCurrencyCode;
        var exchangeRate = dto.ExchangeRate <= 0 ? 1m : dto.ExchangeRate;
        var baseAmount = Math.Round(dto.Amount * exchangeRate, 2, MidpointRounding.AwayFromZero);
        var documentType = module == SubledgerModules.AccountsReceivable
            ? FinanceDocumentTypes.ARAdjustmentJournal
            : FinanceDocumentTypes.APAdjustmentJournal;
        var adjustmentNumber = await _documentNumberingService.GenerateAsync(
            DocumentNumberingModules.Finance,
            documentType,
            tenantId,
            dto.AdjustmentDate,
            nameof(SubledgerAdjustmentJournal),
            cancellationToken: cancellationToken);

        var now = DateTime.UtcNow;
        var adjustment = new SubledgerAdjustmentJournal
        {
            Id = originalAdjustmentId.HasValue ? Guid.NewGuid() : dto.RequestId ?? Guid.NewGuid(),
            TenantId = tenantId,
            Module = module,
            AdjustmentNumber = adjustmentNumber,
            Purpose = purpose,
            BusinessPartnerId = counterparty.Partner.Id,
            BusinessPartnerRoleId = counterparty.Role.Id,
            BusinessPartnerApProfileVersionId = counterparty.ApProfile?.Id,
            BusinessPartnerArProfileVersionId = counterparty.ArProfile?.Id,
            BusinessPartnerCode = counterparty.Partner.PartnerCode,
            BusinessPartnerName = counterparty.Partner.PartnerName,
            BusinessPartnerLegalName = counterparty.Partner.LegalName,
            BusinessPartnerTaxIdentificationNumber = counterparty.Partner.TaxIdentificationNumber,
            AdjustmentDate = dto.AdjustmentDate.Date,
            DueDate = dto.DueDate?.Date,
            AdjustmentType = adjustmentType,
            Amount = dto.Amount,
            CurrencyCode = currencyCode,
            ExchangeRate = exchangeRate,
            BaseCurrencyAmount = baseAmount,
            ContraAccountId = contraAccount.Id,
            ControlAccountId = controlAccount.Id,
            Status = SubledgerAdjustmentStatuses.Posted,
            Reference = string.IsNullOrWhiteSpace(dto.Reference) ? null : dto.Reference.Trim(),
            Reason = dto.Reason.Trim(),
            Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim(),
            OriginalAdjustmentId = originalAdjustmentId,
            CreatedAt = now,
            CreatedBy = UserName
        };

        _context.SubledgerAdjustmentJournals.Add(adjustment);
        await _context.SaveChangesAsync(cancellationToken);

        var authorityRequest = BuildAuthorityRequest(adjustment);
        FinanceSourceBookAuthorityResult authority;
        if (originalAdjustment is null)
        {
            authority = await _sourceBookAuthority.FreezeInitialPrimaryAsync(
                authorityRequest, cancellationToken);
        }
        else
        {
            var originalAuthority = await RequireBoundAdjustmentAuthorityAsync(
                originalAdjustment, cancellationToken);
            authority = await _sourceBookAuthority.FreezeInheritedAsync(
                authorityRequest,
                [new FinanceSourceBookAuthorityOriginRequest
                {
                    OriginAuthorityId = originalAuthority.AuthorityId,
                    Role = "ORIGINAL_ADJUSTMENT"
                }],
                cancellationToken);
        }
        if (!string.Equals(authority.AccountingBookCode, accountingBookCode, StringComparison.Ordinal) ||
            !string.Equals(authority.FunctionalCurrencyCode, baseCurrencyCode, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The frozen source-book authority conflicts with the adjustment's exact book or functional currency.");
        }

        var postingResult = await PostJournalAsync(
            adjustment,
            controlAccount.Id,
            contraAccount.Id,
            counterparty.Name,
            baseCurrencyCode,
            accountingBookCode,
            cancellationToken);
        await _sourceBookAuthority.BindOriginalPostingAsync(
            authority.AuthorityId,
            postingResult.PostingEventId,
            postingResult.JournalEntryId,
            cancellationToken);
        adjustment.JournalEntryId = postingResult.JournalEntryId;
        adjustment.UpdatedAt = DateTime.UtcNow;
        adjustment.UpdatedBy = UserName;

        if (module == SubledgerModules.AccountsReceivable)
        {
            counterparty.Partner.OutstandingBalance = (counterparty.Partner.OutstandingBalance ?? 0m) + GetSignedBaseAmount(adjustment);
            counterparty.Partner.UpdatedAt = DateTime.UtcNow;
            counterparty.Partner.UpdatedBy = UserName;
        }

        await _context.SaveChangesAsync(cancellationToken);

        var reloaded = await LoadAdjustmentAsync(adjustment.Id, asNoTracking: true, cancellationToken)
            ?? adjustment;
        return reloaded;
    }

    private static FinanceSourceBookAuthorityFreezeRequest BuildAuthorityRequest(
        SubledgerAdjustmentJournal adjustment) => new()
    {
        OriginModuleCode = FinanceModuleLockCatalog.Finance,
        SourceDocumentType = "SubledgerAdjustmentJournal",
        SourceDocumentId = adjustment.Id,
        PostingAction = adjustment.OriginalAdjustmentId.HasValue
            ? "PostSubledgerAdjustmentJournalReversal"
            : "PostSubledgerAdjustmentJournal",
        EffectiveDate = adjustment.AdjustmentDate,
        TransactionCurrencyCode = adjustment.CurrencyCode,
        FreezeStage = FinanceSourceBookAuthorityFreezeStages.PrePost
    };

    private async Task<FinanceSourceBookAuthorityResult> RequireBoundAdjustmentAuthorityAsync(
        SubledgerAdjustmentJournal adjustment,
        CancellationToken cancellationToken)
    {
        var authority = await _sourceBookAuthority.RequireForPostingAsync(
            BuildAuthorityRequest(adjustment), cancellationToken);
        return await _sourceBookAuthority.RequireBoundOriginalAsync(
            authority.AuthorityId, cancellationToken);
    }

    private async Task<FinancePostingResultDto> PostJournalAsync(
        SubledgerAdjustmentJournal adjustment,
        Guid controlAccountId,
        Guid contraAccountId,
        string counterpartyName,
        string functionalCurrencyCode,
        string accountingBookCode,
        CancellationToken cancellationToken)
    {
        var isDebit = string.Equals(adjustment.AdjustmentType, SubledgerAdjustmentTypes.Debit, StringComparison.OrdinalIgnoreCase);
        var controlLineType = adjustment.Module == SubledgerModules.AccountsReceivable
            ? (isDebit ? "Debit" : "Credit")
            : (isDebit ? "Debit" : "Credit");
        var contraLineType = string.Equals(controlLineType, "Debit", StringComparison.OrdinalIgnoreCase) ? "Credit" : "Debit";

        var purposeLabel = string.Equals(adjustment.Purpose, SubledgerAdjustmentPurposes.OpeningBalance, StringComparison.OrdinalIgnoreCase)
            ? "opening balance"
            : "adjustment";
        var description = adjustment.Module == SubledgerModules.AccountsReceivable
            ? $"AR {adjustment.AdjustmentType} {purposeLabel} {adjustment.AdjustmentNumber} - {counterpartyName}"
            : $"AP {adjustment.AdjustmentType} {purposeLabel} {adjustment.AdjustmentNumber} - {counterpartyName}";

        return await _postingEngine.PostAsync(new FinancePostingRequestV2Dto
        {
            SourceModule = adjustment.Module,
            SourceDocumentType = "SubledgerAdjustmentJournal",
            SourceDocumentId = adjustment.Id,
            SourceDocumentTenantId = adjustment.TenantId,
            PostingAction = adjustment.OriginalAdjustmentId.HasValue
                ? "PostSubledgerAdjustmentJournalReversal"
                : "PostSubledgerAdjustmentJournal",
            SourceDocumentReference = adjustment.AdjustmentNumber,
            Description = description,
            PostingDate = adjustment.AdjustmentDate,
            JournalType = $"{adjustment.Module} Adjustment",
            AccountingBookCode = accountingBookCode,
            FunctionalCurrencyCode = functionalCurrencyCode,
            IdempotencyKey = $"SubledgerAdjustmentJournal:{adjustment.TenantId:N}:{adjustment.Id:N}:Post",
            ReturnExistingOnDuplicate = true,
            Lines = new List<FinancePostingLineDto>
            {
                BuildPostingLine(controlAccountId, description, controlLineType, adjustment, 1),
                BuildPostingLine(contraAccountId, adjustment.Reason, contraLineType, adjustment, 2)
            }
        }, cancellationToken);
    }

    private async Task<string> ResolvePrimaryAccountingBookCodeAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var books = await _context.AccountingBooks
            .AsNoTracking()
            .Where(book => book.TenantId == tenantId && book.IsDefault && book.IsActive &&
                book.AllowsPosting && !book.IsDeleted &&
                book.BookType == AccountingBookType.PrimaryFull &&
                book.LifecycleStatus == AccountingBookLifecycleStatus.Active)
            .Take(2)
            .Select(book => new { book.Id, book.Code })
            .ToListAsync(cancellationToken);
        if (books.Count != 1 || books[0].Id == Guid.Empty || !IsCanonicalExactBookCode(books[0].Code))
        {
            throw new InvalidOperationException(
                "PRIMARY_BOOK_AUTHORITY_AMBIGUOUS: Exactly one active default posting book with a canonical exact code is required.");
        }

        return books[0].Code;
    }

    private async Task<string> ResolveOriginalAccountingBookCodeAsync(
        SubledgerAdjustmentJournal original,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        if (!original.JournalEntryId.HasValue || original.JournalEntry is null ||
            original.JournalEntry.TenantId != tenantId || original.JournalEntry.AccountingBookId == Guid.Empty ||
            !IsCanonicalExactBookCode(original.JournalEntry.BookClassification))
        {
            throw new InvalidOperationException(
                "The original adjustment is missing valid tenant-owned exact-book journal evidence.");
        }

        var book = await _context.AccountingBooks.AsNoTracking().SingleOrDefaultAsync(candidate =>
            candidate.TenantId == tenantId &&
            candidate.Id == original.JournalEntry.AccountingBookId &&
            candidate.IsActive && candidate.AllowsPosting && !candidate.IsDeleted &&
            candidate.BookType == AccountingBookType.PrimaryFull &&
            candidate.LifecycleStatus == AccountingBookLifecycleStatus.Active,
            cancellationToken);
        if (book is null || !IsCanonicalExactBookCode(book.Code) ||
            !string.Equals(book.Code, original.JournalEntry.BookClassification, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The original adjustment journal's accounting-book evidence is stale or inconsistent.");
        }

        return book.Code;
    }

    private static bool IsCanonicalExactBookCode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 20 ||
            !string.Equals(value, value.Trim().ToUpperInvariant(), StringComparison.Ordinal))
        {
            return false;
        }

        return value.All(character => character is >= 'A' and <= 'Z' or >= '0' and <= '9' or '_') &&
            value is not "ALL" and not "ALL_ACTIVE_BOOKS" and not "ALL_CLASSIFIED_BOOKS" and not "ALLCLASSIFIEDBOOKS";
    }

    private static FinancePostingLineDto BuildPostingLine(
        Guid accountId,
        string description,
        string transactionType,
        SubledgerAdjustmentJournal adjustment,
        int lineNumber)
    {
        var isDebit = string.Equals(transactionType, "Debit", StringComparison.OrdinalIgnoreCase);
        return new FinancePostingLineDto
        {
            AccountId = accountId,
            Description = description,
            DebitAmount = isDebit ? adjustment.BaseCurrencyAmount : 0m,
            CreditAmount = isDebit ? 0m : adjustment.BaseCurrencyAmount,
            TransactionCurrency = adjustment.CurrencyCode,
            TransactionDebitAmount = isDebit ? adjustment.Amount : 0m,
            TransactionCreditAmount = isDebit ? 0m : adjustment.Amount,
            ForeignCurrencyAmount = adjustment.Amount,
            ExchangeRate = adjustment.ExchangeRate,
            ExchangeRateDate = adjustment.AdjustmentDate,
            SourceReferenceNumber = adjustment.AdjustmentNumber,
            LineNumber = lineNumber
        };
    }

    private async Task<CounterpartyResult> ResolveCounterpartyAsync(
        string module,
        Guid businessPartnerId,
        Guid? requestedRoleId,
        DateTime accountingDate,
        CancellationToken cancellationToken)
    {
        if (businessPartnerId == Guid.Empty)
            throw new InvalidOperationException("Business Partner is required for subledger adjustment journals.");

        var partner = await _context.Set<BusinessPartner>()
            .FirstOrDefaultAsync(p =>
                p.TenantId == TenantId &&
                !p.IsDeleted &&
                p.Id == businessPartnerId,
                cancellationToken)
            ?? throw new InvalidOperationException("The Business Partner was not found for this tenant.");

        var expectedRoleTypes = module == SubledgerModules.AccountsReceivable
            ? new[] { BusinessPartnerRoleType.Customer }
            : new[] { BusinessPartnerRoleType.Supplier, BusinessPartnerRoleType.Contractor };
        var roles = await _context.Set<BusinessPartnerRole>()
            .Where(role => role.TenantId == TenantId &&
                role.BusinessPartnerId == partner.Id &&
                !role.IsDeleted &&
                expectedRoleTypes.Contains(role.RoleType))
            .OrderBy(role => role.RoleType)
            .ToListAsync(cancellationToken);

        var role = requestedRoleId.HasValue
            ? roles.SingleOrDefault(candidate => candidate.Id == requestedRoleId.Value)
            : roles.Count == 1
                ? roles[0]
                : null;
        if (role is null)
        {
            throw new InvalidOperationException(roles.Count > 1
                ? "Select the Business Partner role to use for this adjustment."
                : $"The Business Partner does not have the required {module} role.");
        }

        if (module == SubledgerModules.AccountsReceivable)
        {
            var profiles = await _context.Set<BusinessPartnerArProfileVersion>()
                .Where(profile => profile.TenantId == TenantId &&
                    profile.BusinessPartnerRoleId == role.Id && !profile.IsDeleted)
                .ToListAsync(cancellationToken);
            var readiness = BusinessPartnerFinanceProfilePolicy.ResolveAr(partner, role, profiles, accountingDate);
            if (!readiness.IsReady)
                throw new InvalidOperationException($"{readiness.Code}: {readiness.Message}");
            return new CounterpartyResult(partner, role, null, readiness.ArProfile);
        }

        var apProfiles = await _context.Set<BusinessPartnerApProfileVersion>()
            .Include(profile => profile.WithholdingDefaults)
            .Where(profile => profile.TenantId == TenantId &&
                profile.BusinessPartnerRoleId == role.Id && !profile.IsDeleted)
            .ToListAsync(cancellationToken);
        var apReadiness = BusinessPartnerFinanceProfilePolicy.ResolveAp(partner, role, apProfiles, accountingDate);
        if (!apReadiness.IsReady)
            throw new InvalidOperationException($"{apReadiness.Code}: {apReadiness.Message}");
        return new CounterpartyResult(partner, role, apReadiness.ApProfile, null);
    }

    private Task<SubledgerAdjustmentJournal?> LoadAdjustmentAsync(
        Guid id,
        bool asNoTracking,
        CancellationToken cancellationToken)
    {
        var query = _context.SubledgerAdjustmentJournals
            .Include(a => a.BusinessPartner)
            .Include(a => a.BusinessPartnerRole)
            .Include(a => a.ContraAccount)
            .Include(a => a.JournalEntry)
            .Where(a => a.TenantId == TenantId && a.Id == id && !a.IsDeleted);

        if (asNoTracking)
            query = query.AsNoTracking();

        return query.FirstOrDefaultAsync(cancellationToken);
    }

    private static SubledgerAdjustmentJournalDto MapToDto(SubledgerAdjustmentJournal adjustment)
    {
        return new SubledgerAdjustmentJournalDto
        {
            Id = adjustment.Id,
            Module = adjustment.Module,
            AdjustmentNumber = adjustment.AdjustmentNumber,
            Purpose = adjustment.Purpose,
            BusinessPartnerId = adjustment.BusinessPartnerId,
            BusinessPartnerRoleId = adjustment.BusinessPartnerRoleId,
            BusinessPartnerApProfileVersionId = adjustment.BusinessPartnerApProfileVersionId,
            BusinessPartnerArProfileVersionId = adjustment.BusinessPartnerArProfileVersionId,
            BusinessPartnerCode = adjustment.BusinessPartnerCode,
            BusinessPartnerName = adjustment.BusinessPartnerName,
            BusinessPartnerLegalName = adjustment.BusinessPartnerLegalName,
            BusinessPartnerTaxIdentificationNumber = adjustment.BusinessPartnerTaxIdentificationNumber,
            AdjustmentDate = adjustment.AdjustmentDate,
            DueDate = adjustment.DueDate,
            AdjustmentType = adjustment.AdjustmentType,
            Amount = adjustment.Amount,
            SignedSubledgerAmount = adjustment.SignedSubledgerAmount,
            CurrencyCode = adjustment.CurrencyCode,
            ExchangeRate = adjustment.ExchangeRate,
            BaseCurrencyAmount = adjustment.BaseCurrencyAmount,
            ContraAccountId = adjustment.ContraAccountId,
            ContraAccountNumber = adjustment.ContraAccount?.AccountNumber,
            ContraAccountName = adjustment.ContraAccount?.AccountName,
            JournalEntryId = adjustment.JournalEntryId,
            JournalNumber = adjustment.JournalEntry?.JournalEntryNumber,
            Status = adjustment.Status,
            Reference = adjustment.Reference,
            Reason = adjustment.Reason,
            Notes = adjustment.Notes,
            OriginalAdjustmentId = adjustment.OriginalAdjustmentId,
            ReversalAdjustmentId = adjustment.ReversalAdjustmentId,
            ReversalReason = adjustment.ReversalReason,
            ReversedAt = adjustment.ReversedAt,
            CreatedAt = adjustment.CreatedAt,
            CreatedBy = adjustment.CreatedBy
        };
    }

    private static string? NormalizeModule(string? module, bool allowNull)
    {
        if (string.IsNullOrWhiteSpace(module))
        {
            if (allowNull) return null;
            throw new InvalidOperationException("Subledger module is required.");
        }

        var normalized = module.Trim().ToUpperInvariant();
        if (normalized is "AR" or "ACCOUNTSRECEIVABLE" or "ACCOUNTS_RECEIVABLE")
            return SubledgerModules.AccountsReceivable;
        if (normalized is "AP" or "ACCOUNTSPAYABLE" or "ACCOUNTS_PAYABLE")
            return SubledgerModules.AccountsPayable;

        throw new InvalidOperationException("Subledger module must be AR or AP.");
    }

    private static string NormalizeAdjustmentType(string? adjustmentType)
    {
        if (string.IsNullOrWhiteSpace(adjustmentType))
            throw new InvalidOperationException("Adjustment type is required.");

        var normalized = adjustmentType.Trim();
        if (string.Equals(normalized, SubledgerAdjustmentTypes.Debit, StringComparison.OrdinalIgnoreCase))
            return SubledgerAdjustmentTypes.Debit;
        if (string.Equals(normalized, SubledgerAdjustmentTypes.Credit, StringComparison.OrdinalIgnoreCase))
            return SubledgerAdjustmentTypes.Credit;

        throw new InvalidOperationException("Adjustment type must be Debit or Credit.");
    }

    private static string NormalizePurpose(string? purpose, bool allowRetiredOpeningBalance)
    {
        if (string.IsNullOrWhiteSpace(purpose))
            return SubledgerAdjustmentPurposes.StandardAdjustment;

        var normalized = purpose.Trim();
        foreach (var customerPurpose in new[] { SubledgerAdjustmentPurposes.FinanceCharge,
            SubledgerAdjustmentPurposes.Writeoff, SubledgerAdjustmentPurposes.OverpaymentWriteoff })
            if (string.Equals(normalized, customerPurpose, StringComparison.OrdinalIgnoreCase)) return customerPurpose;
        if (string.Equals(normalized, SubledgerAdjustmentPurposes.StandardAdjustment, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "Standard", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "Adjustment", StringComparison.OrdinalIgnoreCase))
        {
            return SubledgerAdjustmentPurposes.StandardAdjustment;
        }

        if (string.Equals(normalized, SubledgerAdjustmentPurposes.OpeningBalance, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "Opening Balance", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "OpenBalance", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "OB", StringComparison.OrdinalIgnoreCase))
        {
            if (allowRetiredOpeningBalance)
                return SubledgerAdjustmentPurposes.OpeningBalance;

            throw new InvalidOperationException(
                "Subledger opening-balance adjustments are retired. Use the controlled Opening Balances workspace and its source-specific processes.");
        }

        throw new InvalidOperationException("Adjustment purpose must be StandardAdjustment.");
    }

    private static Guid ResolveContraAccountId(
        Guid requestedContraAccountId,
        string purpose,
        FinanceSettings settings,
        bool isReversal)
    {
        if (!string.Equals(purpose, SubledgerAdjustmentPurposes.OpeningBalance, StringComparison.OrdinalIgnoreCase))
        {
            if (requestedContraAccountId == Guid.Empty)
                throw new InvalidOperationException("Contra GL account is required.");

            return requestedContraAccountId;
        }

        if (isReversal && requestedContraAccountId != Guid.Empty)
            return requestedContraAccountId;

        if (!settings.MigrationClearingAccountId.HasValue)
            throw new InvalidOperationException("Migration clearing account is not configured in Finance Settings.");

        var clearingAccountId = settings.MigrationClearingAccountId.Value;
        if (requestedContraAccountId != Guid.Empty && requestedContraAccountId != clearingAccountId)
        {
            throw new InvalidOperationException(
                "Opening-balance subledger adjustment journals must use the configured Migration Clearing Account as the contra account.");
        }

        return clearingAccountId;
    }

    private static string? NormalizeCurrency(string? currencyCode)
    {
        return string.IsNullOrWhiteSpace(currencyCode)
            ? null
            : currencyCode.Trim().ToUpperInvariant();
    }

    private static decimal GetSignedBaseAmount(SubledgerAdjustmentJournal adjustment)
    {
        var isDebit = string.Equals(adjustment.AdjustmentType, SubledgerAdjustmentTypes.Debit, StringComparison.OrdinalIgnoreCase);
        return string.Equals(adjustment.Module, SubledgerModules.AccountsReceivable, StringComparison.OrdinalIgnoreCase)
            ? (isDebit ? adjustment.BaseCurrencyAmount : -adjustment.BaseCurrencyAmount)
            : (isDebit ? -adjustment.BaseCurrencyAmount : adjustment.BaseCurrencyAmount);
    }

    private sealed record CounterpartyResult(
        BusinessPartner Partner,
        BusinessPartnerRole Role,
        BusinessPartnerApProfileVersion? ApProfile,
        BusinessPartnerArProfileVersion? ArProfile)
    {
        public string Name => Partner.PartnerName;
    }
}
