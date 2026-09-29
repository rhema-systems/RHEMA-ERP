using System.Text.Json;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Medical;
using ErpSystem.Core.Entities.HR.StaffTravel;
using ErpSystem.Core.Entities.HR.Assets;
using ErpSystem.Core.Entities.HR.StaffDiscipline;
using ErpSystem.Core.Entities.HR.Training;
using ErpSystem.Core.Entities.HR.Requisition;
using ErpSystem.Core.Entities.HR.Safety;
using ErpSystem.Core.Entities.HR.StaffAttendance;
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
    private readonly IVendorInvoiceService _vendorInvoices;
    private readonly IVendorPaymentService _vendorPayments;
    private readonly IInvoiceService _customerInvoices;
    private readonly ICurrentUserProvider _currentUser;
    private readonly ILogger<HrFinancePostingAdminService> _logger;

    public HrFinancePostingAdminService(
        IUnitOfWork unitOfWork,
        IHrFinancePostingStore store,
        IHrFinancePostingAdapter adapter,
        IFinancePostingEngine engine,
        IVendorInvoiceService vendorInvoices,
        IVendorPaymentService vendorPayments,
        IInvoiceService customerInvoices,
        ICurrentUserProvider currentUser,
        ILogger<HrFinancePostingAdminService> logger)
    {
        _unitOfWork = unitOfWork;
        _store = store;
        _adapter = adapter;
        _engine = engine;
        _vendorInvoices = vendorInvoices;
        _vendorPayments = vendorPayments;
        _customerInvoices = customerInvoices;
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
                SupportsSettlementRoute = definition.SupportsSettlementRoute,
                Kind = definition.Kind,
                SettlementRoute = definition.SupportsSettlementRoute
                    ? HrFinancePostingAdapter.ResolveSettlementRoute(definition, rule)
                    : null,
                DefaultSettlementRoute = definition.SupportsSettlementRoute ? definition.DefaultSettlementRoute : null,
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
                SettlementRoute = definition.SupportsSettlementRoute ? dto.SettlementRoute : null,
                Notes = dto.Notes?.Trim(),
                CreatedById = userId,
                CreatedBy = userId.ToString()
            });
        }
        else
        {
            existing.IsEnabled = dto.IsEnabled;
            existing.PostOnActionDate = dto.PostOnActionDate;
            existing.SettlementRoute = definition.SupportsSettlementRoute ? dto.SettlementRoute : null;
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
        // Reversed is retryable too: "post again" after a reversal (or a withdrawn AP draft) is the
        // adapter's next generation — the source is unchanged, so nothing else would re-trigger it.
        if (record.Status is not (HrFinancePostingStatus.Unposted or HrFinancePostingStatus.Failed or HrFinancePostingStatus.Reversed))
            throw new InvalidOperationException($"Only Unposted, Failed or Reversed rows can be posted from the register; this row is {record.Status}.");

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
        var definition = HrFinancePostingEventCatalog.GetRequired(record.EventCode);
        if (definition.Kind == HrFinancePostingKind.VendorInvoice)
            return await ReverseVendorInvoiceAsync(record, definition, reason, cancellationToken);
        if (definition.Kind == HrFinancePostingKind.CustomerInvoice)
            return await ReverseCustomerInvoiceAsync(record, reason, cancellationToken);

        if (record.Status != HrFinancePostingStatus.Posted || !record.PostingEventId.HasValue)
            throw new InvalidOperationException($"Only Posted rows can be reversed; this row is {record.Status}.");

        var userId = _currentUser.UserId;

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

    /// <summary>
    /// Withdrawing an AP hand-off (slice 5). HR may only take back what Finance has not yet acted
    /// on: a draft invoice is deleted. Once it is in AP approval, approved or paid, it is Finance's
    /// document — AP rejects or voids it there, and the register's refresh sees that and marks the
    /// row Reversed. HR never voids a Finance payable itself.
    /// </summary>
    private async Task<HrFinancePostingRecordDto> ReverseVendorInvoiceAsync(
        HrFinancePostingRecord record, HrFinancePostingEventDefinition definition, string reason, CancellationToken cancellationToken)
    {
        if (record.Status != HrFinancePostingStatus.Posted || !record.VendorInvoiceId.HasValue)
            throw new InvalidOperationException($"Only Posted rows can be reversed; this row is {record.Status}.");

        var userId = _currentUser.UserId;
        var invoice = await _vendorInvoices.GetByIdAsync(record.VendorInvoiceId.Value, cancellationToken);
        if (invoice is not null && invoice.Status is not (VendorInvoiceStatus.Draft or VendorInvoiceStatus.Rejected or VendorInvoiceStatus.Voided))
            throw new InvalidOperationException(
                $"Finance holds invoice {invoice.InvoiceNumber} as {invoice.Status}. HR cannot withdraw it from there — ask Accounts Payable to reject or void it, then use Refresh on this row.");

        if (invoice is { Status: VendorInvoiceStatus.Draft })
            await _vendorInvoices.DeleteAsync(invoice.Id, cancellationToken);

        record.Status = HrFinancePostingStatus.Reversed;
        record.ReversedAt = DateTime.UtcNow;
        record.ReversalReason = reason.Length > 500 ? reason[..500] : reason;
        record.ExternalStatus = invoice?.Status.ToString() ?? "Missing";
        record.ExternalStatusAt = DateTime.UtcNow;
        record.LastActedByUserId = userId;
        record.UpdatedAt = DateTime.UtcNow;
        record.UpdatedBy = userId.ToString();
        await _unitOfWork.Repository<HrFinancePostingRecord>().UpdateAsync(record);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("HR Finance AP hand-off {Record} ({Event} {Ref}) withdrawn by {User}: {Reason}",
            record.Id, record.EventCode, record.SourceReference, userId, reason);
        return ToDto(await LoadRecordAsync(record.Id, track: false, cancellationToken));
    }

    /// <summary>The receivables twin of <see cref="ReverseVendorInvoiceAsync"/>: HR deletes only a draft; a submitted AR invoice is Finance's to cancel.</summary>
    private async Task<HrFinancePostingRecordDto> ReverseCustomerInvoiceAsync(HrFinancePostingRecord record, string reason, CancellationToken cancellationToken)
    {
        if (record.Status != HrFinancePostingStatus.Posted || !record.CustomerInvoiceId.HasValue)
            throw new InvalidOperationException($"Only Posted rows can be reversed; this row is {record.Status}.");

        var userId = _currentUser.UserId;
        var invoice = await _customerInvoices.GetByIdAsync(record.CustomerInvoiceId.Value, cancellationToken);
        var status = invoice?.Status ?? "Missing";
        if (invoice is not null && status is not ("Draft" or "Rejected" or "Cancelled"))
            throw new InvalidOperationException(
                $"Finance holds invoice {invoice.InvoiceNumber} as {status}. HR cannot withdraw it from there — ask Accounts Receivable to reject or cancel it, then use Refresh on this row.");

        if (invoice is { Status: "Draft" })
            await _customerInvoices.DeleteAsync(invoice.Id, cancellationToken);

        record.Status = HrFinancePostingStatus.Reversed;
        record.ReversedAt = DateTime.UtcNow;
        record.ReversalReason = reason.Length > 500 ? reason[..500] : reason;
        record.ExternalStatus = status.Length > 50 ? status[..50] : status;
        record.ExternalStatusAt = DateTime.UtcNow;
        record.LastActedByUserId = userId;
        record.UpdatedAt = DateTime.UtcNow;
        record.UpdatedBy = userId.ToString();
        await _unitOfWork.Repository<HrFinancePostingRecord>().UpdateAsync(record);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(await LoadRecordAsync(record.Id, track: false, cancellationToken));
    }

    /// <inheritdoc />
    public async Task<HrFinancePostingRecordDto> RefreshAsync(Guid recordId, CancellationToken cancellationToken = default)
    {
        var record = await LoadRecordAsync(recordId, track: true, cancellationToken);
        var definition = HrFinancePostingEventCatalog.GetRequired(record.EventCode);
        if (definition.Kind == HrFinancePostingKind.CustomerInvoice && record.CustomerInvoiceId.HasValue)
            return await RefreshCustomerInvoiceAsync(record, cancellationToken);
        if (definition.Kind != HrFinancePostingKind.VendorInvoice || !record.VendorInvoiceId.HasValue)
            return ToDto(record); // a journal row has nothing to pull: Finance's reversal comes through this register

        var userId = _currentUser.UserId;
        var invoice = await _vendorInvoices.GetByIdAsync(record.VendorInvoiceId.Value, cancellationToken);
        var now = DateTime.UtcNow;
        record.ExternalStatus = invoice?.Status.ToString() ?? "Missing";
        record.ExternalStatusAt = now;

        // Finance rejected or voided what HR raised: the hand-off is undone on their side, so the
        // register says Reversed and the HR guards lift; a corrected cost can be re-posted.
        if (record.Status == HrFinancePostingStatus.Posted
            && (invoice is null || invoice.Status is VendorInvoiceStatus.Rejected or VendorInvoiceStatus.Voided))
        {
            record.Status = HrFinancePostingStatus.Reversed;
            record.ReversedAt = now;
            record.ReversalReason = invoice is null ? "The vendor invoice no longer exists in Finance." : $"Finance {invoice.Status} the vendor invoice.";
        }

        // The voucher back onto the source: a paid invoice's payment numbers are what the desk
        // used to type into PaymentVoucherNumber by hand.
        if (invoice is { Status: VendorInvoiceStatus.Paid or VendorInvoiceStatus.PartiallyPaid }
            && record.EventCode == HrFinancePostingEventCatalog.RequisitionCostApproved)
        {
            var numbers = new List<string>();
            foreach (var paymentId in invoice.PaymentAllocations.Select(a => a.VendorPaymentId).Distinct())
            {
                var payment = await _vendorPayments.GetByIdAsync(paymentId, cancellationToken);
                if (!string.IsNullOrWhiteSpace(payment?.PaymentNumber)) numbers.Add(payment.PaymentNumber);
            }
            var voucher = numbers.Count > 0 ? string.Join(", ", numbers) : invoice.InvoiceNumber;
            var cost = await _unitOfWork.Repository<StaffRequisitionCost>()
                .GetQueryable(c => c.Id == record.SourceDocumentId && c.TenantId == record.TenantId && !c.IsDeleted)
                .FirstOrDefaultAsync(cancellationToken);
            if (cost is not null && !string.Equals(cost.PaymentVoucherNumber, voucher, StringComparison.Ordinal))
            {
                cost.PaymentVoucherNumber = voucher.Length > 100 ? voucher[..100] : voucher;
                cost.UpdatedAt = now;
                cost.UpdatedBy = userId.ToString();
                await _unitOfWork.Repository<StaffRequisitionCost>().UpdateAsync(cost);
            }
        }

        record.LastActedByUserId = userId;
        record.UpdatedAt = now;
        record.UpdatedBy = userId.ToString();
        await _unitOfWork.Repository<HrFinancePostingRecord>().UpdateAsync(record);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(await LoadRecordAsync(recordId, track: false, cancellationToken));
    }

    /// <summary>
    /// Pulls the AR invoice's status and receipt onto the row and the HR invoice: Finance's
    /// PaidAmount becomes the consulting invoice's PaidAmount/PaidDate/status, which HR may no
    /// longer type by hand once the receivable is Finance's.
    /// </summary>
    private async Task<HrFinancePostingRecordDto> RefreshCustomerInvoiceAsync(HrFinancePostingRecord record, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        var invoice = await _customerInvoices.GetByIdAsync(record.CustomerInvoiceId!.Value, cancellationToken);
        var now = DateTime.UtcNow;
        var status = invoice?.Status ?? "Missing";
        record.ExternalStatus = status.Length > 50 ? status[..50] : status;
        record.ExternalStatusAt = now;

        if (record.Status == HrFinancePostingStatus.Posted && (invoice is null || status is "Rejected" or "Cancelled"))
        {
            record.Status = HrFinancePostingStatus.Reversed;
            record.ReversedAt = now;
            record.ReversalReason = invoice is null ? "The customer invoice no longer exists in Finance." : $"Finance {status} the customer invoice.";
        }

        if (invoice is not null && invoice.PaidAmount > 0m)
        {
            var hrInvoice = await _unitOfWork.Repository<TimesheetInvoice>()
                .GetQueryable(i => i.Id == record.SourceDocumentId && i.TenantId == record.TenantId && !i.IsDeleted)
                .FirstOrDefaultAsync(cancellationToken);
            if (hrInvoice is not null && hrInvoice.Status != TimesheetInvoiceStatus.Voided)
            {
                var fullyPaid = string.Equals(status, "Paid", StringComparison.OrdinalIgnoreCase) || invoice.BalanceAmount <= 0m;
                var changed = hrInvoice.PaidAmount != invoice.PaidAmount
                    || hrInvoice.Status != (fullyPaid ? TimesheetInvoiceStatus.Paid : TimesheetInvoiceStatus.PartiallyPaid);
                if (changed)
                {
                    hrInvoice.PaidAmount = invoice.PaidAmount;
                    hrInvoice.PaidDate = DateOnly.FromDateTime(now);
                    hrInvoice.Status = fullyPaid ? TimesheetInvoiceStatus.Paid : TimesheetInvoiceStatus.PartiallyPaid;
                    hrInvoice.UpdatedAt = now;
                    hrInvoice.UpdatedBy = userId.ToString();
                    await _unitOfWork.Repository<TimesheetInvoice>().UpdateAsync(hrInvoice);
                }
            }
        }

        record.LastActedByUserId = userId;
        record.UpdatedAt = now;
        record.UpdatedBy = userId.ToString();
        await _unitOfWork.Repository<HrFinancePostingRecord>().UpdateAsync(record);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(await LoadRecordAsync(record.Id, track: false, cancellationToken));
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
            case HrFinancePostingEventCatalog.LeaveEncashmentProcessed:
            {
                var encashment = await _unitOfWork.Repository<ErpSystem.Core.Entities.HR.StaffLeave.LeaveEncashment>()
                    .GetQueryable(e => e.TenantId == tenantId && e.Id == record.SourceDocumentId && !e.IsDeleted)
                    .AsNoTracking().FirstOrDefaultAsync(cancellationToken)
                    ?? throw new InvalidOperationException($"Leave encashment {record.SourceReference} no longer exists; nothing to post.");
                if (encashment.Status != LeaveEncashmentStatus.Processed)
                    throw new InvalidOperationException($"Leave encashment {record.SourceReference} is {encashment.Status}; only a processed encashment posts.");
                return HrFinancePostingCommandFactory.LeaveEncashmentProcessed(encashment);
            }
            case HrFinancePostingEventCatalog.AwardConferred:
            case HrFinancePostingEventCatalog.AwardPaid:
            {
                var award = await _unitOfWork.Repository<ErpSystem.Core.Entities.HR.Awards.EmployeeAward>()
                    .GetQueryable(a => a.TenantId == tenantId && a.Id == record.SourceDocumentId && !a.IsDeleted)
                    .AsNoTracking().FirstOrDefaultAsync(cancellationToken)
                    ?? throw new InvalidOperationException($"Award {record.SourceReference} no longer exists; nothing to post.");
                if (record.EventCode == HrFinancePostingEventCatalog.AwardConferred)
                    return HrFinancePostingCommandFactory.AwardConferred(award);
                if (!award.PaymentProcessed)
                    throw new InvalidOperationException($"Award {award.AwardNumber} has not been paid; there is no settlement to post.");
                var conferralPosted = await _adapter.IsPostedAsync(HrFinancePostingEventCatalog.AwardConferred, award.Id, cancellationToken);
                return HrFinancePostingCommandFactory.AwardPaid(award, conferralPosted);
            }
            case HrFinancePostingEventCatalog.LongServiceAwardProcessed:
            {
                var award = await _unitOfWork.Repository<ErpSystem.Core.Entities.HR.Awards.LongServiceAward>()
                    .GetQueryable(a => a.TenantId == tenantId && a.Id == record.SourceDocumentId && !a.IsDeleted)
                    .AsNoTracking().FirstOrDefaultAsync(cancellationToken)
                    ?? throw new InvalidOperationException($"Long-service award {record.SourceReference} no longer exists; nothing to post.");
                if (!award.IsProcessed)
                    throw new InvalidOperationException($"Long-service award {record.SourceReference} has not been processed; there is nothing to post.");
                return HrFinancePostingCommandFactory.LongServiceAwardProcessed(award);
            }
            case HrFinancePostingEventCatalog.BenefitUtilizationApproved:
            case HrFinancePostingEventCatalog.BenefitUtilizationPaid:
            {
                var claim = await _unitOfWork.Repository<ErpSystem.Core.Entities.HR.BenefitUtilization>()
                    .GetQueryable(c => c.TenantId == tenantId && c.Id == record.SourceDocumentId && !c.IsDeleted)
                    .AsNoTracking().FirstOrDefaultAsync(cancellationToken)
                    ?? throw new InvalidOperationException($"Benefit claim {record.SourceReference} no longer exists; nothing to post.");
                var enrollment = await _unitOfWork.Repository<ErpSystem.Core.Entities.HR.EmployeeBenefitEnrollment>()
                    .GetQueryable(e => e.TenantId == tenantId && e.Id == claim.EnrollmentId && !e.IsDeleted)
                    .AsNoTracking().FirstOrDefaultAsync(cancellationToken)
                    ?? throw new InvalidOperationException($"The enrolment behind benefit claim {record.SourceReference} no longer exists.");
                if (record.EventCode == HrFinancePostingEventCatalog.BenefitUtilizationApproved)
                {
                    if (claim.Status is not (BenefitClaimStatus.Approved or BenefitClaimStatus.Paid))
                        throw new InvalidOperationException($"Benefit claim {record.SourceReference} is {claim.Status}; only an approved claim's recognition posts.");
                    return HrFinancePostingCommandFactory.BenefitUtilizationApproved(claim, enrollment);
                }
                if (claim.Status != BenefitClaimStatus.Paid)
                    throw new InvalidOperationException($"Benefit claim {record.SourceReference} is not paid; there is no settlement to post.");
                return HrFinancePostingCommandFactory.BenefitUtilizationPaid(claim, enrollment);
            }
            case HrFinancePostingEventCatalog.SeparationSettlementReleased:
            {
                var settlement = await _unitOfWork.Repository<SeparationSettlement>()
                    .GetQueryable(x => x.TenantId == tenantId && x.Id == record.SourceDocumentId && !x.IsDeleted)
                    .AsNoTracking().FirstOrDefaultAsync(cancellationToken)
                    ?? throw new InvalidOperationException($"Settlement {record.SourceReference} no longer exists; nothing to post.");
                if (settlement.ReviewOutcome != SettlementReviewOutcome.Approved)
                    throw new InvalidOperationException($"Settlement {record.SourceReference} has not been approved by Internal Audit; there is nothing to release.");
                var separation = await _unitOfWork.Repository<EmployeeSeparation>()
                    .GetQueryable(x => x.TenantId == tenantId && x.Id == settlement.SeparationId && !x.IsDeleted)
                    .AsNoTracking().FirstOrDefaultAsync(cancellationToken)
                    ?? throw new InvalidOperationException($"The separation behind settlement {record.SourceReference} no longer exists.");
                var lines = await _unitOfWork.Repository<SeparationSettlementLine>()
                    .GetQueryable(l => l.TenantId == tenantId && l.SettlementId == settlement.Id && !l.IsDeleted)
                    .AsNoTracking().OrderBy(l => l.SortOrder).ToListAsync(cancellationToken);
                return HrFinancePostingCommandFactory.SeparationSettlementReleased(settlement, lines, separation.SeparationNumber, separation.EmployeeId);
            }
            case HrFinancePostingEventCatalog.AssetSurchargeApproved:
            case HrFinancePostingEventCatalog.AssetSurchargeWaived:
            {
                var surcharge = await _unitOfWork.Repository<AssetSurcharge>()
                    .GetQueryable(x => x.TenantId == tenantId && x.Id == record.SourceDocumentId && !x.IsDeleted)
                    .AsNoTracking().FirstOrDefaultAsync(cancellationToken)
                    ?? throw new InvalidOperationException($"Surcharge {record.SourceReference} no longer exists; nothing to post.");
                if (record.EventCode == HrFinancePostingEventCatalog.AssetSurchargeApproved)
                {
                    if (surcharge.ApprovalDate is null)
                        throw new InvalidOperationException($"Surcharge {surcharge.SurchargeNumber} has not been approved; there is no receivable to recognise.");
                    return HrFinancePostingCommandFactory.AssetSurchargeApproved(surcharge);
                }
                if (surcharge.Status != AssetSurchargeStatus.Waived)
                    throw new InvalidOperationException($"Surcharge {surcharge.SurchargeNumber} is {surcharge.Status}, not waived.");
                var approvalPosted = await _adapter.IsPostedAsync(HrFinancePostingEventCatalog.AssetSurchargeApproved, surcharge.Id, cancellationToken);
                return HrFinancePostingCommandFactory.AssetSurchargeWaived(surcharge, approvalPosted);
            }
            case HrFinancePostingEventCatalog.AssetSurchargeRecovered:
            {
                var recovery = await _unitOfWork.Repository<AssetSurchargeRecovery>()
                    .GetQueryable(x => x.TenantId == tenantId && x.Id == record.SourceDocumentId && !x.IsDeleted)
                    .AsNoTracking().FirstOrDefaultAsync(cancellationToken)
                    ?? throw new InvalidOperationException($"Recovery {record.SourceReference} no longer exists; nothing to post.");
                var surcharge = await _unitOfWork.Repository<AssetSurcharge>()
                    .GetQueryable(x => x.TenantId == tenantId && x.Id == recovery.SurchargeId && !x.IsDeleted)
                    .AsNoTracking().FirstOrDefaultAsync(cancellationToken)
                    ?? throw new InvalidOperationException($"The surcharge behind recovery {record.SourceReference} no longer exists.");
                return HrFinancePostingCommandFactory.AssetSurchargeRecovered(surcharge, recovery);
            }
            case HrFinancePostingEventCatalog.DisciplineFineImposed:
            case HrFinancePostingEventCatalog.DisciplineFineSettled:
            {
                var fine = await _unitOfWork.Repository<StaffDisciplineFine>()
                    .GetQueryable(x => x.TenantId == tenantId && x.Id == record.SourceDocumentId && !x.IsDeleted)
                    .AsNoTracking().FirstOrDefaultAsync(cancellationToken)
                    ?? throw new InvalidOperationException($"Fine {record.SourceReference} no longer exists; nothing to post.");
                var disciplinaryCase = await _unitOfWork.Repository<StaffDisciplinaryAction>()
                    .GetQueryable(x => x.TenantId == tenantId && x.Id == fine.DisciplinaryActionId && !x.IsDeleted)
                    .AsNoTracking().FirstOrDefaultAsync(cancellationToken)
                    ?? throw new InvalidOperationException($"The case behind fine {record.SourceReference} no longer exists.");
                if (record.EventCode == HrFinancePostingEventCatalog.DisciplineFineImposed)
                    return HrFinancePostingCommandFactory.DisciplineFineImposed(fine, disciplinaryCase.CaseNumber, disciplinaryCase.EmployeeId);
                if (fine.FinePaymentStatus is not (DisciplinaryFinePaymentStatus.FullyPaid or DisciplinaryFinePaymentStatus.Waived))
                    throw new InvalidOperationException($"Fine {record.SourceReference} is {fine.FinePaymentStatus}; it settles when fully paid or waived.");
                var imposedPosted = await _adapter.IsPostedAsync(HrFinancePostingEventCatalog.DisciplineFineImposed, fine.Id, cancellationToken);
                return HrFinancePostingCommandFactory.DisciplineFineSettled(fine, disciplinaryCase.CaseNumber, disciplinaryCase.EmployeeId, imposedPosted);
            }
            case HrFinancePostingEventCatalog.RequisitionCostApproved:
            {
                var cost = await _unitOfWork.Repository<StaffRequisitionCost>()
                    .GetQueryable(x => x.TenantId == tenantId && x.Id == record.SourceDocumentId && !x.IsDeleted)
                    .Include(x => x.Requisition).AsNoTracking().FirstOrDefaultAsync(cancellationToken)
                    ?? throw new InvalidOperationException($"Recruitment cost {record.SourceReference} no longer exists; nothing to invoice.");
                if (cost.Status != StaffRequisitionCostStatus.Approved)
                    throw new InvalidOperationException($"Recruitment cost {record.SourceReference} is {cost.Status}; only an approved cost is invoiced.");
                return HrFinancePostingCommandFactory.RequisitionCostApproved(cost, cost.Requisition?.RequisitionNumber ?? "REQ");
            }
            case HrFinancePostingEventCatalog.MedicalPremiumPaid:
            {
                var premium = await _unitOfWork.Repository<MedicalInsurancePremiumRecord>()
                    .GetQueryable(x => x.TenantId == tenantId && x.Id == record.SourceDocumentId && !x.IsDeleted)
                    .Include(x => x.MedicalInsuranceProvider).AsNoTracking().FirstOrDefaultAsync(cancellationToken)
                    ?? throw new InvalidOperationException($"Premium record {record.SourceReference} no longer exists; nothing to post.");
                if (premium.Status != MedicalInsurancePremiumPaymentStatus.Paid)
                    throw new InvalidOperationException($"Premium record {record.SourceReference} is {premium.Status}, not paid.");
                return HrFinancePostingCommandFactory.MedicalPremiumPaid(premium, premium.MedicalInsuranceProvider?.Name ?? "the insurer");
            }
            case HrFinancePostingEventCatalog.MedicalInsurerRecoveryReceived:
            {
                var insurerClaim = await _unitOfWork.Repository<MedicalInsuranceClaim>()
                    .GetQueryable(x => x.TenantId == tenantId && x.Id == record.SourceDocumentId && !x.IsDeleted)
                    .AsNoTracking().FirstOrDefaultAsync(cancellationToken)
                    ?? throw new InvalidOperationException($"Insurance claim {record.SourceReference} no longer exists; nothing to post.");
                if (insurerClaim.Status != MedicalInsuranceClaimStatus.Paid)
                    throw new InvalidOperationException($"Insurance claim {record.SourceReference} is {insurerClaim.Status}; the insurer has not paid.");
                return HrFinancePostingCommandFactory.MedicalInsurerRecoveryReceived(insurerClaim);
            }
            case HrFinancePostingEventCatalog.NhisClaimReimbursed:
            {
                var nhis = await _unitOfWork.Repository<NHISClaim>()
                    .GetQueryable(x => x.TenantId == tenantId && x.Id == record.SourceDocumentId && !x.IsDeleted)
                    .AsNoTracking().FirstOrDefaultAsync(cancellationToken)
                    ?? throw new InvalidOperationException($"NHIS claim {record.SourceReference} no longer exists; nothing to post.");
                if (nhis.Status != NHISClaimStatus.Paid)
                    throw new InvalidOperationException($"NHIS claim {record.SourceReference} is {nhis.Status}; the NHIS has not paid.");
                return HrFinancePostingCommandFactory.NhisClaimReimbursed(nhis);
            }
            case HrFinancePostingEventCatalog.SheInsuranceClaimReceived:
            {
                var incident = await _unitOfWork.Repository<SafetyIncident>()
                    .GetQueryable(x => x.TenantId == tenantId && x.Id == record.SourceDocumentId && !x.IsDeleted)
                    .AsNoTracking().FirstOrDefaultAsync(cancellationToken)
                    ?? throw new InvalidOperationException($"Incident {record.SourceReference} no longer exists; nothing to post.");
                return HrFinancePostingCommandFactory.SheInsuranceClaimReceived(incident);
            }
            case HrFinancePostingEventCatalog.TimesheetInvoiceSent:
            {
                var invoice = await _unitOfWork.Repository<TimesheetInvoice>()
                    .GetQueryable(x => x.TenantId == tenantId && x.Id == record.SourceDocumentId && !x.IsDeleted)
                    .Include(x => x.Client).AsNoTracking().FirstOrDefaultAsync(cancellationToken)
                    ?? throw new InvalidOperationException($"Consulting invoice {record.SourceReference} no longer exists; nothing to raise.");
                if (invoice.Status == TimesheetInvoiceStatus.Draft)
                    throw new InvalidOperationException($"Consulting invoice {invoice.InvoiceNumber} has not been sent; it is raised in Finance when it is sent.");
                if (invoice.Status == TimesheetInvoiceStatus.Voided)
                    throw new InvalidOperationException($"Consulting invoice {invoice.InvoiceNumber} is voided.");
                return HrFinancePostingCommandFactory.TimesheetInvoiceSent(invoice, invoice.Client?.FinanceCustomerId, invoice.Client?.ClientName ?? "the client");
            }
            case HrFinancePostingEventCatalog.TrainingBondBreached:
            case HrFinancePostingEventCatalog.TrainingBondSettled:
            case HrFinancePostingEventCatalog.TrainingBondWaived:
            {
                var bond = await _unitOfWork.Repository<TrainingServiceBond>()
                    .GetQueryable(x => x.TenantId == tenantId && x.Id == record.SourceDocumentId && !x.IsDeleted)
                    .AsNoTracking().FirstOrDefaultAsync(cancellationToken)
                    ?? throw new InvalidOperationException($"Bond {record.SourceReference} no longer exists; nothing to post.");
                if (record.EventCode == HrFinancePostingEventCatalog.TrainingBondBreached)
                {
                    if (bond.Status is not (TrainingBondStatus.Breached or TrainingBondStatus.Settled or TrainingBondStatus.Waived) || bond.ExitDate is null)
                        throw new InvalidOperationException($"Bond {record.SourceReference} has no recorded breach; there is nothing to post.");
                    return HrFinancePostingCommandFactory.TrainingBondBreached(bond);
                }
                var breachPosted = await _adapter.IsPostedAsync(HrFinancePostingEventCatalog.TrainingBondBreached, bond.Id, cancellationToken);
                if (record.EventCode == HrFinancePostingEventCatalog.TrainingBondSettled)
                {
                    if (bond.Status != TrainingBondStatus.Settled)
                        throw new InvalidOperationException($"Bond {record.SourceReference} is {bond.Status}, not settled.");
                    return HrFinancePostingCommandFactory.TrainingBondSettled(bond, breachPosted);
                }
                if (bond.Status != TrainingBondStatus.Waived)
                    throw new InvalidOperationException($"Bond {record.SourceReference} is {bond.Status}, not waived.");
                return HrFinancePostingCommandFactory.TrainingBondWaived(bond, breachPosted);
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
            Kind = definition?.Kind ?? HrFinancePostingKind.Journal,
            VendorInvoiceId = r.VendorInvoiceId,
            VendorInvoiceNumber = r.VendorInvoiceNumber,
            CustomerInvoiceId = r.CustomerInvoiceId,
            CustomerInvoiceNumber = r.CustomerInvoiceNumber,
            ExternalStatus = r.ExternalStatus,
            ExternalStatusAt = r.ExternalStatusAt,
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
