using System.Text.Json;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Medical;
using ErpSystem.Core.Entities.HR.StaffTravel;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR.Finance;

/// <summary>
/// Settings and register behind <c>api/hr/finance-posting</c>. See <see cref="IHrFinancePostingAdminService"/>.
/// </summary>
/// <remarks>
/// <para><b>Enabling a rule is refused until its roles are mapped.</b> An enabled rule is a promise
/// that the next approval will post; making that promise with an unmapped role would turn the next
/// HR action into a refusal the desk cannot fix. The screen shows readiness; the service enforces it.</para>
///
/// <para><b>Retry rebuilds the command from the source document as it stands now</b>, through the
/// same factory the trigger used. It never replays a stored request: a claim corrected after a
/// failed attempt must post its corrected figure.</para>
///
/// <para><b>Reversal is Finance's exact reversal</b> (<c>IFinancePostingEngine.ReverseAsync</c>) —
/// HR supplies the posting event and a reason, never lines. The source document is not changed by
/// a reversal; what to do with the claim afterwards is the area's business, and its own guards lift
/// once the row is no longer Posted.</para>
/// </remarks>
public sealed class HrFinancePostingAdminService : IHrFinancePostingAdminService
{
    private static readonly JsonSerializerOptions SnapshotJson = new(JsonSerializerDefaults.Web);

    private readonly IUnitOfWork _unitOfWork;
    private readonly IHrFinancePostingStore _store;
    private readonly IHrFinancePostingAdapter _adapter;
    private readonly IFinancePostingEngine _engine;
    private readonly ICurrentUserProvider _currentUser;
    private readonly ILogger<HrFinancePostingAdminService> _logger;

    public HrFinancePostingAdminService(
        IUnitOfWork unitOfWork,
        IHrFinancePostingStore store,
        IHrFinancePostingAdapter adapter,
        IFinancePostingEngine engine,
        ICurrentUserProvider currentUser,
        ILogger<HrFinancePostingAdminService> logger)
    {
        _unitOfWork = unitOfWork;
        _store = store;
        _adapter = adapter;
        _engine = engine;
        _currentUser = currentUser;
        _logger = logger;
    }

    // ─────────────────────────────────────────────────────────────────────────────────────────
    //  Settings
    // ─────────────────────────────────────────────────────────────────────────────────────────

    public async Task<HrFinancePostingSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var context = await _store.GetTenantContextAsync(tenantId, cancellationToken);
        var mappings = (await _store.GetMappingsAsync(tenantId, cancellationToken))
            .GroupBy(m => m.Role).ToDictionary(g => g.Key, g => g.OrderByDescending(m => m.UpdatedAt ?? m.CreatedAt).First());
        var accounts = await _store.GetAccountsAsync(tenantId, mappings.Values.Select(m => m.AccountId).ToList(), cancellationToken);
        var rules = await _unitOfWork.Repository<HrFinancePostingRule>()
            .GetQueryable(r => r.TenantId == tenantId && !r.IsDeleted)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var counts = await _unitOfWork.Repository<HrFinancePostingRecord>()
            .GetQueryable(r => r.TenantId == tenantId && !r.IsDeleted)
            .AsNoTracking()
            .GroupBy(r => new { r.EventCode, r.Status })
            .Select(g => new { g.Key.EventCode, g.Key.Status, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var mappingDtos = Enum.GetValues<HrFinanceAccountRole>().Select(role =>
        {
            mappings.TryGetValue(role, out var mapping);
            HrFinanceAccountSnapshot? account = null;
            if (mapping is not null) accounts.TryGetValue(mapping.AccountId, out account);
            return new HrFinanceAccountMappingDto
            {
                Role = role,
                RoleName = HrFinanceAccountRoleNames.Name(role),
                RoleDescription = HrFinanceAccountRoleNames.Description(role),
                RequiredAccountType = HrFinancePostingEventCatalog.RequiredAccountType(role).ToString(),
                AccountId = mapping?.AccountId,
                AccountCode = account?.AccountNumber ?? mapping?.AccountCodeSnapshot,
                AccountName = account?.AccountName ?? mapping?.AccountNameSnapshot,
                AccountIsActive = account?.IsActive,
                Notes = mapping?.Notes
            };
        }).ToList();

        var ruleDtos = HrFinancePostingEventCatalog.Events.Select(definition =>
        {
            var rule = rules.FirstOrDefault(r => r.EventCode == definition.Code);
            var missing = definition.AllRoles.Where(role =>
                !mappings.TryGetValue(role, out var m)
                || !accounts.TryGetValue(m.AccountId, out var a)
                || !a.IsActive
                || a.AccountType != HrFinancePostingEventCatalog.RequiredAccountType(role)).ToList();
            return new HrFinancePostingRuleDto
            {
                EventCode = definition.Code,
                Name = definition.Name,
                Area = definition.Area,
                SourceDocumentType = definition.SourceDocumentType,
                Trigger = definition.Trigger,
                Treatment = definition.Treatment,
                DebitRoles = definition.DebitRoles,
                CreditRoles = definition.CreditRoles,
                IsEnabled = rule?.IsEnabled ?? false,
                PostOnActionDate = rule?.PostOnActionDate ?? true,
                Notes = rule?.Notes,
                IsReady = missing.Count == 0 && context.AccountingBookCode is not null,
                MissingRoles = missing,
                PendingCount = counts.Where(c => c.EventCode == definition.Code
                    && c.Status is HrFinancePostingStatus.Unposted or HrFinancePostingStatus.Failed).Sum(c => c.Count),
                PostedCount = counts.Where(c => c.EventCode == definition.Code && c.Status == HrFinancePostingStatus.Posted).Sum(c => c.Count)
            };
        }).ToList();

        return new HrFinancePostingSettingsDto
        {
            FunctionalCurrencyCode = context.FunctionalCurrencyCode,
            AccountingBookCode = context.AccountingBookCode,
            AccountingBookProblem = context.AccountingBookProblem,
            Mappings = mappingDtos,
            Rules = ruleDtos
        };
    }

    public async Task<HrFinancePostingSettingsDto> UpsertMappingAsync(UpsertHrFinanceAccountMappingDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var tenantId = GetTenantId();
        if (!Enum.IsDefined(dto.Role))
            throw new ArgumentException($"'{dto.Role}' is not an HR Finance account role.");

        var accounts = await _store.GetAccountsAsync(tenantId, [dto.AccountId], cancellationToken);
        if (!accounts.TryGetValue(dto.AccountId, out var account))
            throw new ArgumentException("The chosen account was not found in this organisation's chart of accounts.");
        if (!account.IsActive)
            throw new InvalidOperationException($"{account.AccountNumber} {account.AccountName} is inactive in Finance. Choose an active account.");
        var required = HrFinancePostingEventCatalog.RequiredAccountType(dto.Role);
        if (account.AccountType != required)
            throw new InvalidOperationException(
                $"{HrFinanceAccountRoleNames.Name(dto.Role)} needs a {required} account; {account.AccountNumber} {account.AccountName} is a {account.AccountType} account.");

        var repo = _unitOfWork.Repository<HrFinanceAccountMapping>();
        var existing = await repo.GetQueryable(m => m.TenantId == tenantId && m.Role == dto.Role && !m.IsDeleted)
            .OrderByDescending(m => m.UpdatedAt ?? m.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var userId = _currentUser.UserId;
        if (existing is null)
        {
            await repo.AddAsync(new HrFinanceAccountMapping
            {
                TenantId = tenantId,
                Role = dto.Role,
                AccountId = account.Id,
                AccountCodeSnapshot = account.AccountNumber,
                AccountNameSnapshot = account.AccountName,
                Notes = dto.Notes?.Trim(),
                CreatedById = userId,
                CreatedBy = userId.ToString()
            });
        }
        else
        {
            existing.AccountId = account.Id;
            existing.AccountCodeSnapshot = account.AccountNumber;
            existing.AccountNameSnapshot = account.AccountName;
            existing.Notes = dto.Notes?.Trim();
            existing.UpdatedAt = now;
            existing.UpdatedBy = userId.ToString();
            existing.LastModifiedById = userId;
            await repo.UpdateAsync(existing);
        }
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("HR Finance account role {Role} mapped to {Account} by {User}", dto.Role, account.AccountNumber, userId);
        return await GetSettingsAsync(cancellationToken);
    }

    public async Task<HrFinancePostingSettingsDto> ClearMappingAsync(HrFinanceAccountRole role, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();

        // A role in use by an ENABLED rule cannot be unmapped: the next event would refuse.
        var enabledRules = await _unitOfWork.Repository<HrFinancePostingRule>()
            .GetQueryable(r => r.TenantId == tenantId && r.IsEnabled && !r.IsDeleted)
            .AsNoTracking().Select(r => r.EventCode).ToListAsync(cancellationToken);
        var dependants = enabledRules
            .Where(code => HrFinancePostingEventCatalog.TryGet(code, out var d) && d.AllRoles.Contains(role))
            .Select(code => HrFinancePostingEventCatalog.GetRequired(code).Name).ToList();
        if (dependants.Count > 0)
            throw new InvalidOperationException(
                $"{HrFinanceAccountRoleNames.Name(role)} is used by enabled rule(s): {string.Join(", ", dependants)}. Disable them first.");

        var repo = _unitOfWork.Repository<HrFinanceAccountMapping>();
        var rows = await repo.GetQueryable(m => m.TenantId == tenantId && m.Role == role && !m.IsDeleted).ToListAsync(cancellationToken);
        foreach (var row in rows) await repo.DeleteAsync(row);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetSettingsAsync(cancellationToken);
    }

    public async Task<HrFinancePostingSettingsDto> UpsertRuleAsync(UpsertHrFinancePostingRuleDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var tenantId = GetTenantId();
        var definition = HrFinancePostingEventCatalog.TryGet(dto.EventCode?.Trim() ?? string.Empty, out var d)
            ? d
            : throw new ArgumentException($"'{dto.EventCode}' is not an HR Finance posting event.");

        if (dto.IsEnabled)
        {
            var settings = await GetSettingsAsync(cancellationToken);
            var ruleState = settings.Rules.First(r => r.EventCode == definition.Code);
            if (settings.AccountingBookCode is null)
                throw new InvalidOperationException($"Cannot enable '{definition.Name}': {settings.AccountingBookProblem}");
            if (ruleState.MissingRoles.Count > 0)
                throw new InvalidOperationException(
                    $"Cannot enable '{definition.Name}': map {string.Join(", ", ruleState.MissingRoles.Select(HrFinanceAccountRoleNames.Name))} to an active account of the right type first.");
        }

        var repo = _unitOfWork.Repository<HrFinancePostingRule>();
        var existing = await repo.GetQueryable(r => r.TenantId == tenantId && r.EventCode == definition.Code && !r.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);
        var userId = _currentUser.UserId;
        if (existing is null)
        {
            await repo.AddAsync(new HrFinancePostingRule
            {
                TenantId = tenantId,
                EventCode = definition.Code,
                IsEnabled = dto.IsEnabled,
                PostOnActionDate = dto.PostOnActionDate,
                Notes = dto.Notes?.Trim(),
                CreatedById = userId,
                CreatedBy = userId.ToString()
            });
        }
        else
        {
            existing.IsEnabled = dto.IsEnabled;
            existing.PostOnActionDate = dto.PostOnActionDate;
            existing.Notes = dto.Notes?.Trim();
            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedBy = userId.ToString();
            existing.LastModifiedById = userId;
            await repo.UpdateAsync(existing);
        }
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("HR Finance posting rule {Event} set enabled={Enabled} by {User}", definition.Code, dto.IsEnabled, userId);
        return await GetSettingsAsync(cancellationToken);
    }

    // ─────────────────────────────────────────────────────────────────────────────────────────
    //  Register
    // ─────────────────────────────────────────────────────────────────────────────────────────

    public async Task<HrFinancePostingSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var context = await _store.GetTenantContextAsync(tenantId, cancellationToken);
        var rows = await _unitOfWork.Repository<HrFinancePostingRecord>()
            .GetQueryable(r => r.TenantId == tenantId && !r.IsDeleted)
            .AsNoTracking()
            .GroupBy(r => r.Status)
            .Select(g => new { Status = g.Key, Count = g.Count(), Amount = g.Sum(r => r.Amount) })
            .ToListAsync(cancellationToken);
        int Count(HrFinancePostingStatus s) => rows.Where(r => r.Status == s).Sum(r => r.Count);
        return new HrFinancePostingSummaryDto
        {
            Posted = Count(HrFinancePostingStatus.Posted),
            Failed = Count(HrFinancePostingStatus.Failed),
            Unposted = Count(HrFinancePostingStatus.Unposted),
            Skipped = Count(HrFinancePostingStatus.Skipped),
            Reversed = Count(HrFinancePostingStatus.Reversed),
            PostedAmount = rows.Where(r => r.Status == HrFinancePostingStatus.Posted).Sum(r => r.Amount),
            FunctionalCurrencyCode = context.FunctionalCurrencyCode
        };
    }

    public async Task<PagedResult<HrFinancePostingRecordDto>> GetRecordsAsync(HrFinancePostingRecordQueryDto query, CancellationToken cancellationToken = default)
    {
        query ??= new HrFinancePostingRecordQueryDto();
        var tenantId = GetTenantId();
        var page = Math.Max(1, query.PageNumber);
        var size = Math.Clamp(query.PageSize, 1, 200);

        var q = _unitOfWork.Repository<HrFinancePostingRecord>()
            .GetQueryable(r => r.TenantId == tenantId && !r.IsDeleted)
            .AsNoTracking();
        if (query.Status.HasValue) q = q.Where(r => r.Status == query.Status.Value);
        if (!string.IsNullOrWhiteSpace(query.EventCode)) q = q.Where(r => r.EventCode == query.EventCode);
        if (query.EmployeeId.HasValue) q = q.Where(r => r.EmployeeId == query.EmployeeId.Value);
        if (query.From.HasValue) q = q.Where(r => r.PostingDate >= query.From.Value.Date);
        if (query.To.HasValue) q = q.Where(r => r.PostingDate <= query.To.Value.Date);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(r => r.SourceReference.Contains(s)
                || (r.JournalEntryNumber != null && r.JournalEntryNumber.Contains(s))
                || r.Description.Contains(s));
        }

        var total = await q.CountAsync(cancellationToken);
        var items = await q.OrderByDescending(r => r.LastAttemptAt ?? r.CreatedAt)
            .Skip((page - 1) * size).Take(size)
            .ToListAsync(cancellationToken);

        return new PagedResult<HrFinancePostingRecordDto>
        {
            Items = items.Select(ToDto).ToList(),
            TotalCount = total,
            Page = page,
            PageSize = size
        };
    }

    public async Task<HrFinancePostingRecordDto> GetRecordAsync(Guid id, CancellationToken cancellationToken = default)
        => ToDto(await LoadRecordAsync(id, track: false, cancellationToken));

    public async Task<IReadOnlyList<HrFinancePostingRecordDto>> GetRecordsForSourceAsync(Guid sourceDocumentId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var rows = await _unitOfWork.Repository<HrFinancePostingRecord>()
            .GetQueryable(r => r.TenantId == tenantId && r.SourceDocumentId == sourceDocumentId && !r.IsDeleted)
            .AsNoTracking()
            .OrderBy(r => r.CreatedAt)
            .ToListAsync(cancellationToken);
        return rows.Select(ToDto).ToList();
    }

    public async Task<HrFinancePostingRecordDto> RetryAsync(Guid recordId, CancellationToken cancellationToken = default)
    {
        var record = await LoadRecordAsync(recordId, track: false, cancellationToken);
        if (record.Status is not (HrFinancePostingStatus.Unposted or HrFinancePostingStatus.Failed))
            throw new InvalidOperationException($"Only Unposted or Failed rows can be retried; this row is {record.Status}.");

        var command = await BuildCommandFromSourceAsync(record, cancellationToken);
        await _adapter.PostAsync(command, _currentUser.UserId, cancellationToken);
        return ToDto(await LoadRecordAsync(recordId, track: false, cancellationToken));
    }

    public async Task<HrFinancePostingRecordDto> ReverseAsync(Guid recordId, ReverseHrFinancePostingDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var reason = dto.Reason?.Trim() ?? string.Empty;
        if (reason.Length < 5)
            throw new ArgumentException("A reversal needs a reason of at least five characters.");

        var record = await LoadRecordAsync(recordId, track: true, cancellationToken);
        if (record.Status != HrFinancePostingStatus.Posted || !record.PostingEventId.HasValue)
            throw new InvalidOperationException($"Only Posted rows can be reversed; this row is {record.Status}.");

        var userId = _currentUser.UserId;
        var definition = HrFinancePostingEventCatalog.GetRequired(record.EventCode);

        await _unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                var result = await _engine.ReverseAsync(record.PostingEventId.Value, $"HR {definition.Name} {record.SourceReference}: {reason}", null, cancellationToken);
                record.Status = HrFinancePostingStatus.Reversed;
                record.ReversalPostingEventId = result.PostingEventId;
                record.ReversalJournalEntryId = result.JournalEntryId;
                record.ReversalJournalEntryNumber = result.JournalEntryNumber.Length > 50 ? result.JournalEntryNumber[..50] : result.JournalEntryNumber;
                record.ReversedAt = DateTime.UtcNow;
                record.ReversalReason = reason.Length > 500 ? reason[..500] : reason;
                record.LastActedByUserId = userId;
                record.UpdatedAt = DateTime.UtcNow;
                record.UpdatedBy = userId.ToString();
                await _unitOfWork.Repository<HrFinancePostingRecord>().UpdateAsync(record);
                await _unitOfWork.CommitAsync(cancellationToken);
            }
            catch
            {
                await _unitOfWork.RollbackAsync(cancellationToken);
                throw;
            }
        }, cancellationToken);

        _logger.LogInformation("HR Finance posting {Record} ({Event} {Ref}) reversed by {User}: {Reason}",
            record.Id, record.EventCode, record.SourceReference, userId, reason);
        return ToDto(await LoadRecordAsync(recordId, track: false, cancellationToken));
    }

    // ─────────────────────────────────────────────────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>The retry path: the same factory the trigger used, over the source as it stands now.</summary>
    private async Task<HrFinancePostingCommand> BuildCommandFromSourceAsync(HrFinancePostingRecord record, CancellationToken cancellationToken)
    {
        var tenantId = record.TenantId;
        switch (record.EventCode)
        {
            case HrFinancePostingEventCatalog.MedicalClaimApproved:
            case HrFinancePostingEventCatalog.MedicalClaimPaid:
            {
                var claim = await _unitOfWork.Repository<MedicalExpenseClaim>()
                    .GetQueryable(c => c.TenantId == tenantId && c.Id == record.SourceDocumentId && !c.IsDeleted)
                    .AsNoTracking().FirstOrDefaultAsync(cancellationToken)
                    ?? throw new InvalidOperationException($"Medical claim {record.SourceReference} no longer exists; nothing to post.");
                if (record.EventCode == HrFinancePostingEventCatalog.MedicalClaimApproved)
                {
                    if (claim.Status is not (ClaimStatus.Approved or ClaimStatus.Paid))
                        throw new InvalidOperationException($"Medical claim {claim.ClaimNumber} is {claim.Status}; only an approved claim's recognition can be posted.");
                    return HrFinancePostingCommandFactory.MedicalClaimApproved(claim);
                }
                if (claim.Status != ClaimStatus.Paid || !claim.PaymentProcessed)
                    throw new InvalidOperationException($"Medical claim {claim.ClaimNumber} is not paid; there is no settlement to post.");
                return HrFinancePostingCommandFactory.MedicalClaimPaid(claim);
            }
            case HrFinancePostingEventCatalog.TravelClaimApproved:
            case HrFinancePostingEventCatalog.TravelClaimPaid:
            {
                var claim = await _unitOfWork.Repository<StaffTravelExpenseClaim>()
                    .GetQueryable(c => c.TenantId == tenantId && c.Id == record.SourceDocumentId && !c.IsDeleted)
                    .AsNoTracking().FirstOrDefaultAsync(cancellationToken)
                    ?? throw new InvalidOperationException($"Travel claim {record.SourceReference} no longer exists; nothing to post.");
                if (record.EventCode == HrFinancePostingEventCatalog.TravelClaimApproved)
                {
                    if (claim.Status is not (TravelClaimStatus.Approved or TravelClaimStatus.PartiallyApproved or TravelClaimStatus.Paid))
                        throw new InvalidOperationException($"Travel claim {claim.ClaimNumber} is {claim.Status}; only an approved claim's recognition can be posted.");
                    return HrFinancePostingCommandFactory.TravelClaimApproved(claim);
                }
                if (claim.Status != TravelClaimStatus.Paid)
                    throw new InvalidOperationException($"Travel claim {claim.ClaimNumber} is not paid; there is no settlement to post.");
                return HrFinancePostingCommandFactory.TravelClaimPaid(claim);
            }
            case HrFinancePostingEventCatalog.TravelAdvanceDisbursed:
            {
                var advance = await _unitOfWork.Repository<StaffTravelAdvance>()
                    .GetQueryable(a => a.TenantId == tenantId && a.Id == record.SourceDocumentId && !a.IsDeleted)
                    .AsNoTracking().FirstOrDefaultAsync(cancellationToken)
                    ?? throw new InvalidOperationException($"Travel advance {record.SourceReference} no longer exists; nothing to post.");
                if (advance.Status is TravelAdvanceStatus.Requested or TravelAdvanceStatus.Approved)
                    throw new InvalidOperationException($"Travel advance {advance.AdvanceNumber} has not been disbursed; there is nothing to post.");
                return HrFinancePostingCommandFactory.TravelAdvanceDisbursed(advance);
            }
            default:
                throw new InvalidOperationException($"No retry builder exists for event '{record.EventCode}'.");
        }
    }

    private async Task<HrFinancePostingRecord> LoadRecordAsync(Guid id, bool track, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var q = _unitOfWork.Repository<HrFinancePostingRecord>()
            .GetQueryable(r => r.TenantId == tenantId && r.Id == id && !r.IsDeleted);
        if (!track) q = q.AsNoTracking();
        return await q.FirstOrDefaultAsync(cancellationToken)
            ?? throw new ArgumentException($"HR Finance posting record '{id}' was not found.");
    }

    private Guid GetTenantId()
    {
        var tenantId = _currentUser.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    internal static HrFinancePostingRecordDto ToDto(HrFinancePostingRecord r)
    {
        HrFinancePostingEventCatalog.TryGet(r.EventCode, out var definition);
        IReadOnlyList<HrFinancePostingLineSnapshotDto> lines = Array.Empty<HrFinancePostingLineSnapshotDto>();
        if (!string.IsNullOrWhiteSpace(r.LinesSnapshot))
        {
            try { lines = JsonSerializer.Deserialize<List<HrFinancePostingLineSnapshotDto>>(r.LinesSnapshot, SnapshotJson) ?? lines; }
            catch (JsonException) { /* a corrupt snapshot must not hide the row */ }
        }
        return new HrFinancePostingRecordDto
        {
            Id = r.Id,
            EventCode = r.EventCode,
            EventName = definition?.Name ?? r.EventCode,
            Area = definition?.Area ?? string.Empty,
            SourceDocumentType = r.SourceDocumentType,
            SourceDocumentId = r.SourceDocumentId,
            SourceReference = r.SourceReference,
            EmployeeId = r.EmployeeId,
            Description = r.Description,
            Amount = r.Amount,
            CurrencyCode = r.CurrencyCode,
            TransactionCurrencyCode = r.TransactionCurrencyCode,
            TransactionAmount = r.TransactionAmount,
            PostingDate = r.PostingDate,
            Status = r.Status,
            StatusReason = r.StatusReason,
            AccountingBookCode = r.AccountingBookCode,
            PostingEventId = r.PostingEventId,
            JournalEntryId = r.JournalEntryId,
            JournalEntryNumber = r.JournalEntryNumber,
            PostedAt = r.PostedAt,
            AttemptCount = r.AttemptCount,
            LastAttemptAt = r.LastAttemptAt,
            Lines = lines,
            ReversalPostingEventId = r.ReversalPostingEventId,
            ReversalJournalEntryId = r.ReversalJournalEntryId,
            ReversalJournalEntryNumber = r.ReversalJournalEntryNumber,
            ReversedAt = r.ReversedAt,
            ReversalReason = r.ReversalReason,
            CreatedAt = r.CreatedAt
        };
    }
}
