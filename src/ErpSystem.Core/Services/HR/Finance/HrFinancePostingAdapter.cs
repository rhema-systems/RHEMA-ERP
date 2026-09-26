using System.Text.Json;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Finance.Integration;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR.Finance;

/// <summary>
/// The HR side of FIN-INT-001 — see <see cref="IHrFinancePostingAdapter"/> for the contract and
/// <c>docs/HR/integration/HR-FINANCE-POSTING-DESIGN.md</c> for the treatment.
/// </summary>
/// <remarks>
/// <para><b>What this class does NOT do</b>, and why. It does not choose accounts (an administrator
/// maps roles; Finance owns the chart). It does not write a journal, a balance or a period (Finance's
/// engine does, behind one call). It does not pick a rate (Finance's bridge does). It does not carry
/// dimensions (TDC's cost-attribution answer is outstanding; the engine treats HR as an
/// uncertified route and reads none). Every one of those is a boundary the Finance owner drew in
/// writing on 2026-08-31, and this adapter stays inside it.</para>
///
/// <para><b>Foreign-currency sources</b> (a USD travel advance) are converted to the functional
/// currency through <see cref="HrCurrencyBridge"/> — the same valuation every HR money field already
/// uses — and posted as functional lines with the original currency and amount kept on the register
/// row and in the line narration. Finance-side FX evidence on the journal (the rate record id) is
/// deliberately not attempted here: the engine's rate-policy resolution is account-scoped and would
/// refuse a rate whose type or quote side differs from policy, and HR has no business overriding
/// Finance's rate policy. Raised in the routes hand-off as a question for the Finance owner.</para>
/// </remarks>
public sealed class HrFinancePostingAdapter : IHrFinancePostingAdapter
{
    public const string SourceModule = "HR";
    public const string OriginModuleCode = "HR";
    private const string JournalType = "System Generated";

    /// <summary>
    /// The dimension producer HR raises AP invoices under. ⚠ Finance's AP screen approves an invoice
    /// only when it carries "trusted dimension provenance", which Finance records at create/submit
    /// under a producer route — the route-less overload records none, and the first live run proved
    /// an HR invoice could then never be approved. Finance's route catalogue has no HR AP route yet
    /// (hand-off Q1), so HR hands its invoice over under the same manual-AP route Finance's own
    /// clerks key invoices with: to Accounts Payable it is an invoice on their desk, no more. When
    /// Finance grants HR its own route this is the one value that changes (D-7).
    /// </summary>
    private static readonly FinancePostingProducerContext ApProducer = new(FinanceDimensionRouteId.FinanceApVendorInvoice);

    private static readonly JsonSerializerOptions SnapshotJson = new(JsonSerializerDefaults.Web);

    private readonly IHrFinancePostingStore _store;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFinancePostingEngine _engine;
    private readonly IVendorInvoiceService _vendorInvoices;
    private readonly HrCurrencyBridge _currency;
    private readonly ICurrentUserProvider _currentUser;
    private readonly ILogger<HrFinancePostingAdapter> _logger;

    public HrFinancePostingAdapter(
        IHrFinancePostingStore store,
        IUnitOfWork unitOfWork,
        IFinancePostingEngine engine,
        IVendorInvoiceService vendorInvoices,
        HrCurrencyBridge currency,
        ICurrentUserProvider currentUser,
        ILogger<HrFinancePostingAdapter> logger)
    {
        _store = store;
        _unitOfWork = unitOfWork;
        _engine = engine;
        _vendorInvoices = vendorInvoices;
        _currency = currency;
        _currentUser = currentUser;
        _logger = logger;
    }

    // ─────────────────────────────────────────────────────────────────────────────────────────
    //  Public surface
    // ─────────────────────────────────────────────────────────────────────────────────────────

    public async Task<HrFinancePostingOutcome?> RunAsync(
        Func<CancellationToken, Task<HrFinancePostingCommand?>> mutate,
        Guid actedByUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mutate);

        // Joining a caller-owned transaction: the owner commits or rolls back; we neither open
        // nor close anything, and a Finance refusal simply propagates to the owner.
        if (_unitOfWork.HasActiveTransaction)
        {
            var command = await mutate(cancellationToken);
            if (command is null)
            {
                await _store.SaveChangesAsync(cancellationToken);
                return null;
            }
            return await PostInsideTransactionAsync(command, actedByUserId, cancellationToken);
        }

        HrFinancePostingCommand? failedCommand = null;
        string? failureReason = null;
        try
        {
            return await _unitOfWork.ExecuteInStrategyAsync(async () =>
            {
                await _unitOfWork.BeginTransactionAsync(cancellationToken);
                try
                {
                    var command = await mutate(cancellationToken);
                    HrFinancePostingOutcome? outcome = null;
                    if (command is null)
                    {
                        await _store.SaveChangesAsync(cancellationToken);
                    }
                    else
                    {
                        outcome = await PostInsideTransactionAsync(command, actedByUserId, cancellationToken);
                    }
                    await _unitOfWork.CommitAsync(cancellationToken);
                    return outcome;
                }
                catch (HrFinancePostingException ex)
                {
                    // Finance said no. The HR change AND the in-memory register row are rolled
                    // back together; the refusal is persisted below, outside the dead transaction.
                    failedCommand = ex.Data[CommandKey] as HrFinancePostingCommand;
                    failureReason = ex.Message;
                    await _unitOfWork.RollbackAsync(cancellationToken);
                    throw;
                }
                catch
                {
                    await _unitOfWork.RollbackAsync(cancellationToken);
                    throw;
                }
            }, cancellationToken);
        }
        catch (HrFinancePostingException) when (failedCommand is not null)
        {
            await RecordFailureAsync(failedCommand, failureReason ?? "Finance refused the posting.", actedByUserId, cancellationToken);
            throw;
        }
    }

    public Task<HrFinancePostingOutcome> PostAsync(
        HrFinancePostingCommand command,
        Guid actedByUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return RunAsync(_ => Task.FromResult<HrFinancePostingCommand?>(command), actedByUserId, cancellationToken)!;
    }

    public async Task EnsureNotPostedAsync(
        string sourceDocumentType,
        Guid sourceDocumentId,
        string action,
        CancellationToken cancellationToken = default)
    {
        var posted = await _store.FindPostedRecordAsync(GetTenantId(), sourceDocumentType, sourceDocumentId, cancellationToken);
        if (posted is null) return;

        var definition = HrFinancePostingEventCatalog.GetRequired(posted.EventCode);
        throw new InvalidOperationException(
            $"{action} is not allowed: '{posted.SourceReference}' has been posted to Finance " +
            $"({definition.Name}, journal {posted.JournalEntryNumber ?? posted.JournalEntryId?.ToString()}). " +
            "Reverse the posting from HR Settings → Finance posting first, with a reason, and then make the change.");
    }

    public async Task<bool> IsEnabledAsync(string eventCode, CancellationToken cancellationToken = default)
    {
        var rule = await _store.GetRuleAsync(GetTenantId(), eventCode, cancellationToken);
        return rule?.IsEnabled == true;
    }

    public async Task<bool> IsPostedAsync(string eventCode, Guid sourceDocumentId, CancellationToken cancellationToken = default)
    {
        var record = await _store.FindRecordAsync(GetTenantId(), eventCode, sourceDocumentId, cancellationToken);
        return record?.Status == HrFinancePostingStatus.Posted;
    }

    // ─────────────────────────────────────────────────────────────────────────────────────────
    //  The posting itself — always inside the transaction
    // ─────────────────────────────────────────────────────────────────────────────────────────

    private const string CommandKey = "HrFinancePostingCommand";

    private async Task<HrFinancePostingOutcome> PostInsideTransactionAsync(
        HrFinancePostingCommand command,
        Guid actedByUserId,
        CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var definition = HrFinancePostingEventCatalog.GetRequired(command.EventCode);
        var now = DateTime.UtcNow;

        var record = await _store.FindRecordAsync(tenantId, command.EventCode, command.SourceDocumentId, cancellationToken);
        var isNew = record is null;
        record ??= new HrFinancePostingRecord
        {
            TenantId = tenantId,
            EventCode = command.EventCode,
            SourceDocumentType = definition.SourceDocumentType,
            SourceDocumentId = command.SourceDocumentId,
            CreatedById = actedByUserId,
            CreatedBy = actedByUserId.ToString(),
            Generation = 1
        };

        // Already posted: HR-level idempotency. Finance would answer "duplicate" too, but we do
        // not spend a Finance call to learn what the register already knows.
        if (record.Status == HrFinancePostingStatus.Posted)
            return ToOutcome(record, wasDuplicate: true);

        // A reversed row being posted again is a new generation: Finance de-duplicates on the
        // posting action, so the action must change or it would hand back the reversed original.
        if (record.Status == HrFinancePostingStatus.Reversed)
        {
            record.Generation += 1;
            record.ReversalPostingEventId = null;
            record.ReversalJournalEntryId = null;
            record.ReversalJournalEntryNumber = null;
            record.ReversedAt = null;
            record.ReversalReason = null;
        }

        // Refresh the descriptive facts from the command every attempt: a retry posts the source
        // as it stands now, and the register must say what was actually sent.
        record.SourceReference = Truncate(command.SourceReference, 100);
        record.EmployeeId = command.EmployeeId;
        record.Description = Truncate(command.Description, 500);
        record.PostingEventId = null;
        record.JournalEntryId = null;
        record.JournalEntryNumber = null;
        record.VendorInvoiceId = null;
        record.VendorInvoiceNumber = null;
        record.ExternalStatus = null;
        record.ExternalStatusAt = null;
        record.PostedAt = null;
        record.AttemptCount += 1;
        record.LastAttemptAt = now;
        record.LastActedByUserId = actedByUserId;
        record.UpdatedAt = now;
        record.UpdatedBy = actedByUserId.ToString();
        record.PostingAction = ResolvePostingAction(definition, record.Generation);

        var rule = await _store.GetRuleAsync(tenantId, command.EventCode, cancellationToken);
        var context = await _store.GetTenantContextAsync(tenantId, cancellationToken);

        // Settlement route: for single-step events the rule (or the catalogue default) decides
        // whether HR's action settles directly or leaves the payable for payroll. Resolved BEFORE
        // the disabled/skip branches so an Unposted row shows the amount that would have posted.
        var (effectiveLines, routeSkip) = ResolveRouteLines(definition, rule, command);
        var skipReason = command.SkipReason ?? routeSkip;
        record.CurrencyCode = context.FunctionalCurrencyCode;
        record.AccountingBookCode = context.AccountingBookCode;
        record.PostingDate = ResolvePostingDate(rule, command, now);
        record.TransactionCurrencyCode = null;
        record.TransactionAmount = null;

        // ── Not configured: proceed, but say so ──────────────────────────────────────────────
        if (rule is null || !rule.IsEnabled)
        {
            record.Amount = SumDebits(effectiveLines);
            record.LinesSnapshot = null;
            return await FinishAsync(record, isNew, HrFinancePostingStatus.Unposted,
                rule is null
                    ? $"No posting rule exists for '{definition.Name}'. Enable it under HR Settings → Finance posting to post this event."
                    : $"The posting rule for '{definition.Name}' is disabled.",
                cancellationToken);
        }

        // ── Nothing to post for this instance ────────────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(skipReason))
        {
            record.Amount = 0m;
            record.LinesSnapshot = null;
            return await FinishAsync(record, isNew, HrFinancePostingStatus.Skipped, skipReason, cancellationToken);
        }

        // ── From here on the rule is live: any problem refuses the HR action ─────────────────
        try
        {
            if (context.AccountingBookCode is null)
                throw new InvalidOperationException(
                    $"Finance's accounting book is not configured: {context.AccountingBookProblem} Ask Finance to set the subledger posting book.");

            if (effectiveLines.Count == 0)
                throw new InvalidOperationException($"'{definition.Name}' produced no posting lines.");
            if (effectiveLines.Any(l => l.Amount <= 0m))
                throw new InvalidOperationException($"'{definition.Name}' produced a non-positive posting line.");

            var mappings = await ResolveAccountsAsync(tenantId, definition, effectiveLines, cancellationToken);

            if (definition.Kind == HrFinancePostingKind.VendorInvoice)
                return await PostVendorInvoiceAsync(tenantId, definition, command, record, isNew, effectiveLines, mappings, now, cancellationToken);

            // Functional-currency conversion for a foreign source, through Finance's own rate.
            var rate = 1m;
            var functional = context.FunctionalCurrencyCode;
            var sourceCurrency = command.TransactionCurrencyCode?.Trim().ToUpperInvariant();
            var isForeign = !string.IsNullOrWhiteSpace(sourceCurrency)
                && !string.Equals(sourceCurrency, functional, StringComparison.OrdinalIgnoreCase);
            if (isForeign)
            {
                rate = await _currency.GetRateToBaseAsync(sourceCurrency!, DateOnly.FromDateTime(record.PostingDate), cancellationToken);
                record.TransactionCurrencyCode = sourceCurrency;
                record.TransactionAmount = SumDebits(effectiveLines);
            }

            var lines = new List<FinancePostingLineDto>(effectiveLines.Count);
            var snapshot = new List<HrFinancePostingLineSnapshotDto>(effectiveLines.Count);
            var lineNumber = 0;
            foreach (var line in effectiveLines)
            {
                var account = mappings[line.Role];
                var amount = isForeign ? decimal.Round(line.Amount * rate, 2, MidpointRounding.AwayFromZero) : decimal.Round(line.Amount, 2, MidpointRounding.AwayFromZero);
                if (amount <= 0m) continue; // a rounding-to-zero foreign line has no economic content

                var narration = isForeign
                    ? $"{line.Description} ({sourceCurrency} {line.Amount:N2} @ {rate:0.######})"
                    : line.Description;
                lineNumber++;
                lines.Add(new FinancePostingLineDto
                {
                    AccountId = account.Id,
                    Description = Truncate(narration, 500),
                    DebitAmount = line.IsDebit ? amount : 0m,
                    CreditAmount = line.IsDebit ? 0m : amount,
                    TransactionCurrency = functional,
                    SourceReferenceNumber = Truncate(command.SourceReference, 100),
                    LineNumber = lineNumber,
                    TransactionTag = line.Role.ToString()
                });
                snapshot.Add(new HrFinancePostingLineSnapshotDto
                {
                    Role = line.Role,
                    RoleName = HrFinanceAccountRoleNames.Name(line.Role),
                    AccountId = account.Id,
                    AccountCode = account.AccountNumber,
                    AccountName = account.AccountName,
                    Debit = line.IsDebit ? amount : 0m,
                    Credit = line.IsDebit ? 0m : amount,
                    Description = narration
                });
            }

            var debits = lines.Sum(l => l.DebitAmount);
            var credits = lines.Sum(l => l.CreditAmount);
            if (lines.Count < 2 || debits != credits || debits <= 0m)
                throw new InvalidOperationException(
                    $"'{definition.Name}' is not balanced (debits {debits:N2}, credits {credits:N2}). This is an HR defect, not a Finance refusal — report it.");

            record.Amount = debits;
            record.LinesSnapshot = JsonSerializer.Serialize(snapshot, SnapshotJson);
            record.IdempotencyKey = BuildIdempotencyKey(command.EventCode, command.SourceDocumentId, context.AccountingBookCode, record.Generation);

            var request = new FinancePostingRequestV2Dto
            {
                SourceModule = SourceModule,
                OriginModuleCode = OriginModuleCode,
                SourceDocumentType = definition.SourceDocumentType,
                SourceDocumentId = command.SourceDocumentId,
                SourceDocumentTenantId = tenantId,
                SourceDocumentReference = Truncate(command.SourceReference, 100),
                PostingAction = record.PostingAction,
                Description = Truncate(command.Description, 500),
                PostingDate = record.PostingDate,
                JournalType = JournalType,
                AccountingBookCode = context.AccountingBookCode,
                FunctionalCurrencyCode = functional,
                IdempotencyKey = record.IdempotencyKey,
                ReturnExistingOnDuplicate = true,
                Lines = lines
            };

            // No producer route yet — Finance's route catalogue lists one HR route (payroll).
            // When HR routes exist this becomes PostAsync(request, producerContext, ct).
            var result = await _engine.PostAsync(request, cancellationToken);

            record.PostingEventId = result.PostingEventId;
            record.JournalEntryId = result.JournalEntryId;
            record.JournalEntryNumber = Truncate(result.JournalEntryNumber, 50);
            record.PostedAt = now;
            var outcome = await FinishAsync(record, isNew, HrFinancePostingStatus.Posted, null, cancellationToken);

            _logger.LogInformation(
                "HR Finance posting {EventCode} for {SourceReference} posted as journal {Journal} ({Amount} {Currency}, duplicate={Duplicate})",
                command.EventCode, command.SourceReference, result.JournalEntryNumber, debits, functional, result.WasDuplicate);

            return outcome with { WasDuplicate = result.WasDuplicate };
        }
        catch (HrFinancePostingException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "HR Finance posting {EventCode} for {SourceReference} refused: {Message}",
                command.EventCode, command.SourceReference, ex.Message);
            var wrapped = new HrFinancePostingException(
                command.EventCode, command.SourceDocumentId,
                $"Finance did not accept '{definition.Name}' for {command.SourceReference}: {ex.Message}", ex);
            wrapped.Data[CommandKey] = command;
            throw wrapped;
        }
    }

    /// <summary>
    /// The AP hand-off (slice 5): a vendor invoice to a Procurement supplier with one expense line,
    /// submitted into Finance's AP approval. Finance approves, posts and pays it under its own
    /// controls; HR never journals a third party's payable itself. Mirrors the Estate and Quantity
    /// Survey producers (route-less <c>CreateAsync</c>, idempotent by the HR reference, status pulled
    /// back by the register's refresh). No HR AP route exists in Finance's route catalogue yet — the
    /// hand-off document asks for one; this becomes the producer-context overload when it lands.
    /// </summary>
    private async Task<HrFinancePostingOutcome> PostVendorInvoiceAsync(
        Guid tenantId,
        HrFinancePostingEventDefinition definition,
        HrFinancePostingCommand command,
        HrFinancePostingRecord record,
        bool isNew,
        IReadOnlyList<HrFinancePostingLine> effectiveLines,
        IReadOnlyDictionary<HrFinanceAccountRole, HrFinanceAccountSnapshot> mappings,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (effectiveLines.Count != 1 || !effectiveLines[0].IsDebit)
            throw new InvalidOperationException($"'{definition.Name}' must produce exactly one expense line for an AP invoice. HR defect.");
        if (command.PayeeSupplierId is not { } supplierId || supplierId == Guid.Empty)
            throw new InvalidOperationException($"'{definition.Name}' names no supplier to invoice.");

        var supplier = await _unitOfWork.Repository<Supplier>().GetQueryable()
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == supplierId && s.TenantId == tenantId && !s.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("The payee supplier does not exist in this tenant.");
        if (!supplier.IsActive)
            throw new InvalidOperationException($"Supplier {supplier.Name} is inactive; Finance cannot be invoiced for it.");

        // Finance no longer accepts the legacy Procurement Supplier id as AP identity. HR still
        // stores that source id, so cross the boundary only through an exact, tenant-scoped stable
        // code match to the canonical Business Partner; never infer accounting identity by name.
        var businessPartner = await _unitOfWork.Repository<BusinessPartner>().GetQueryable()
            .AsNoTracking()
            .SingleOrDefaultAsync(partner => partner.TenantId == tenantId
                                             && !partner.IsDeleted
                                             && partner.PartnerCode == supplier.SupplierCode,
                cancellationToken)
            ?? throw new InvalidOperationException(
                $"Supplier {supplier.SupplierCode} has no canonical Business Partner identity for Finance posting.");

        var line = effectiveLines[0];
        var account = mappings[line.Role];
        var functional = (await _store.GetTenantContextAsync(tenantId, cancellationToken)).FunctionalCurrencyCode;
        var currency = string.IsNullOrWhiteSpace(command.TransactionCurrencyCode) ? functional : command.TransactionCurrencyCode.Trim().ToUpperInvariant();
        var isForeign = !string.Equals(currency, functional, StringComparison.OrdinalIgnoreCase);
        var rate = isForeign ? await _currency.GetRateToBaseAsync(currency, DateOnly.FromDateTime(record.PostingDate), cancellationToken) : 1m;
        var amount = decimal.Round(line.Amount, 2, MidpointRounding.AwayFromZero);

        record.TransactionCurrencyCode = isForeign ? currency : null;
        record.TransactionAmount = isForeign ? amount : null;
        record.Amount = isForeign ? decimal.Round(amount * rate, 2, MidpointRounding.AwayFromZero) : amount;
        record.IdempotencyKey = BuildIdempotencyKey(command.EventCode, command.SourceDocumentId, record.AccountingBookCode ?? "?", record.Generation);
        record.LinesSnapshot = JsonSerializer.Serialize(new List<HrFinancePostingLineSnapshotDto>
        {
            new()
            {
                Role = line.Role, RoleName = HrFinanceAccountRoleNames.Name(line.Role),
                AccountId = account.Id, AccountCode = account.AccountNumber, AccountName = account.AccountName,
                Debit = record.Amount, Credit = 0m,
                Description = isForeign ? $"{line.Description} ({currency} {amount:N2} @ {rate:0.######})" : line.Description
            }
        }, SnapshotJson);

        var reference = $"HR-{definition.Code}:{command.SourceDocumentId:N}" + (record.Generation > 1 ? $"#{record.Generation}" : string.Empty);
        var invoiceDate = (command.SourceDate ?? now).Date;
        var created = await _vendorInvoices.CreateAsync(new VendorInvoiceCreateDto
        {
            BusinessPartnerId = businessPartner.Id,
            SupplierInvoiceNumber = Truncate(command.SourceReference, 50),
            InvoiceDate = invoiceDate,
            ReceivedDate = now,
            CurrencyCode = currency,
            ExchangeRate = rate,
            ExpenseAccountId = account.Id,
            Notes = Truncate($"Raised by HR from {definition.Name.ToLowerInvariant()} {command.SourceReference}. {command.Description}", 1000),
            Reference = reference,
            LineItems =
            [
                new VendorInvoiceLineItemCreateDto
                {
                    LineItemType = "Expense",
                    GLAccountId = account.Id,
                    Description = Truncate(line.Description, 500),
                    Quantity = 1m,
                    UnitPrice = amount,
                    Unit = "Each"
                }
            ]
        }, ApProducer, cancellationToken);

        // Into AP approval straight away: HR's approval of the cost is the authorising event (the
        // hand-off's question 3). If Finance wants to receive drafts instead, this is the one line.
        //
        // ⚠ Except on a budget-controlled expense account. Finance's AP refuses to submit an expense
        // line on such an account without an adopted Finance budget cell (FIN-INT-016), and HR does
        // not reserve Finance budget — it enforces its own recruitment envelope (D-8, hand-off Q6).
        // So the invoice is left as a Draft for Accounts Payable to attach the cell and submit, and
        // the row says so. A live run found this: the first UAT attempt failed on exactly that rule.
        string? note = null;
        var final = created;
        if (account.BudgetTrackingEnabled)
            note = $"Left as a Draft in Accounts Payable: account {account.AccountNumber} is budget-controlled, so Finance must attach its budget cell and submit the invoice (HR does not reserve Finance budget).";
        else
            final = await _vendorInvoices.SubmitForApprovalAsync(created.Id, ApProducer, cancellationToken);

        record.VendorInvoiceId = final.Id;
        record.VendorInvoiceNumber = Truncate(final.InvoiceNumber, 50);
        record.ExternalStatus = final.Status.ToString();
        record.ExternalStatusAt = now;
        record.PostedAt = now;
        var outcome = await FinishAsync(record, isNew, HrFinancePostingStatus.Posted, note, cancellationToken);

        _logger.LogInformation(
            "HR Finance posting {EventCode} for {SourceReference} raised AP invoice {Invoice} to {Supplier} ({Amount} {Currency})",
            command.EventCode, command.SourceReference, final.InvoiceNumber, supplier.Name, amount, currency);
        return outcome;
    }

    /// <summary>
    /// Persists a Failed row after the transaction that carried the attempt was rolled back. Runs
    /// on a clean change tracker; the row is looked up again because the tracked one is gone.
    /// </summary>
    private async Task RecordFailureAsync(
        HrFinancePostingCommand command, string reason, Guid actedByUserId, CancellationToken cancellationToken)
    {
        try
        {
            _unitOfWork.ClearTrackedChanges();
            var tenantId = GetTenantId();
            var definition = HrFinancePostingEventCatalog.GetRequired(command.EventCode);
            var now = DateTime.UtcNow;
            var record = await _store.FindRecordAsync(tenantId, command.EventCode, command.SourceDocumentId, cancellationToken);
            var isNew = record is null;
            record ??= new HrFinancePostingRecord
            {
                TenantId = tenantId,
                EventCode = command.EventCode,
                SourceDocumentType = definition.SourceDocumentType,
                SourceDocumentId = command.SourceDocumentId,
                CreatedById = actedByUserId,
                CreatedBy = actedByUserId.ToString(),
                Generation = 1,
                PostingAction = ResolvePostingAction(definition, 1),
                IdempotencyKey = BuildIdempotencyKey(command.EventCode, command.SourceDocumentId, "?", 1)
            };
            if (record.Status == HrFinancePostingStatus.Posted) return; // never demote a posted row

            record.SourceReference = Truncate(command.SourceReference, 100);
            record.EmployeeId = command.EmployeeId;
            record.Description = Truncate(command.Description, 500);
            record.Amount = SumDebits(command.Lines);
            if (string.IsNullOrWhiteSpace(record.CurrencyCode)) record.CurrencyCode = "GHS";
            if (record.PostingDate == default) record.PostingDate = now.Date;
            record.Status = HrFinancePostingStatus.Failed;
            record.StatusReason = Truncate(reason, 2000);
            record.AttemptCount += 1;
            record.LastAttemptAt = now;
            record.LastActedByUserId = actedByUserId;
            record.UpdatedAt = now;
            record.UpdatedBy = actedByUserId.ToString();

            if (isNew) await _store.AddRecordAsync(record, cancellationToken);
            else await _store.UpdateRecordAsync(record, cancellationToken);
            await _store.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // The refusal itself is already on its way to the caller; losing the log row is worse
            // than nothing but must not mask the real reason.
            _logger.LogError(ex, "Could not persist the HR Finance posting failure for {EventCode} {SourceId}",
                command.EventCode, command.SourceDocumentId);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Picks the lines for the effective settlement route. Route-independent commands (approvals,
    /// documents that name their own payment method) return <c>Lines</c> unchanged.
    /// </summary>
    public static (IReadOnlyList<HrFinancePostingLine> Lines, string? SkipReason) ResolveRouteLines(
        HrFinancePostingEventDefinition definition,
        HrFinancePostingRule? rule,
        HrFinancePostingCommand command)
    {
        if (command.RouteDirectLines is null) return (command.Lines, null);
        var route = ResolveSettlementRoute(definition, rule);
        if (route == HrFinanceSettlementRoute.Direct) return (command.RouteDirectLines, null);
        if (command.RoutePayrollLines.Count > 0) return (command.RoutePayrollLines, null);
        return (Array.Empty<HrFinancePostingLine>(),
            command.RoutePayrollSkipReason ?? "Settled through payroll: payroll's own journal clears the staff claims payable; HR posts nothing.");
    }

    public static HrFinanceSettlementRoute ResolveSettlementRoute(HrFinancePostingEventDefinition definition, HrFinancePostingRule? rule)
        => definition.SupportsSettlementRoute
            ? rule?.SettlementRoute ?? definition.DefaultSettlementRoute
            : HrFinanceSettlementRoute.Direct;

    private async Task<IReadOnlyDictionary<HrFinanceAccountRole, HrFinanceAccountSnapshot>> ResolveAccountsAsync(
        Guid tenantId,
        HrFinancePostingEventDefinition definition,
        IReadOnlyList<HrFinancePostingLine> effectiveLines,
        CancellationToken cancellationToken)
    {
        var rolesUsed = effectiveLines.Select(l => l.Role).Distinct().ToList();
        var outsideCatalogue = rolesUsed.Where(r => !definition.AllRoles.Contains(r)).ToList();
        if (outsideCatalogue.Count > 0)
            throw new InvalidOperationException(
                $"'{definition.Name}' tried to use role(s) {string.Join(", ", outsideCatalogue)} that its catalogue entry does not declare. HR defect.");

        var mappings = (await _store.GetMappingsAsync(tenantId, cancellationToken))
            .GroupBy(m => m.Role).ToDictionary(g => g.Key, g => g.OrderByDescending(m => m.UpdatedAt ?? m.CreatedAt).First());
        var missing = rolesUsed.Where(r => !mappings.ContainsKey(r)).ToList();
        if (missing.Count > 0)
            throw new InvalidOperationException(
                $"No Finance account is mapped for {string.Join(", ", missing.Select(HrFinanceAccountRoleNames.Name))}. Map it under HR Settings → Finance posting.");

        var accounts = await _store.GetAccountsAsync(tenantId, rolesUsed.Select(r => mappings[r].AccountId).ToList(), cancellationToken);
        var resolved = new Dictionary<HrFinanceAccountRole, HrFinanceAccountSnapshot>();
        foreach (var role in rolesUsed)
        {
            var mapping = mappings[role];
            if (!accounts.TryGetValue(mapping.AccountId, out var account))
                throw new InvalidOperationException(
                    $"The account mapped for {HrFinanceAccountRoleNames.Name(role)} ({mapping.AccountCodeSnapshot}) no longer exists in this organisation's chart. Re-map it.");
            if (!account.IsActive)
                throw new InvalidOperationException(
                    $"The account mapped for {HrFinanceAccountRoleNames.Name(role)} ({account.AccountNumber} {account.AccountName}) is inactive in Finance. Re-map it or ask Finance to reactivate it.");
            var required = HrFinancePostingEventCatalog.RequiredAccountType(role);
            if (account.AccountType != required)
                throw new InvalidOperationException(
                    $"The account mapped for {HrFinanceAccountRoleNames.Name(role)} ({account.AccountNumber} {account.AccountName}) is a {account.AccountType} account; this role needs a {required} account.");
            resolved[role] = account;
        }
        return resolved;
    }

    private async Task<HrFinancePostingOutcome> FinishAsync(
        HrFinancePostingRecord record, bool isNew, HrFinancePostingStatus status, string? reason, CancellationToken cancellationToken)
    {
        record.Status = status;
        record.StatusReason = reason is null ? null : Truncate(reason, 2000);
        if (string.IsNullOrWhiteSpace(record.IdempotencyKey))
            record.IdempotencyKey = BuildIdempotencyKey(record.EventCode, record.SourceDocumentId, record.AccountingBookCode ?? "?", record.Generation);
        if (isNew) await _store.AddRecordAsync(record, cancellationToken);
        else await _store.UpdateRecordAsync(record, cancellationToken);
        await _store.SaveChangesAsync(cancellationToken);
        return ToOutcome(record, wasDuplicate: false);
    }

    private Guid GetTenantId()
    {
        var tenantId = _currentUser.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    /// <summary>
    /// <c>HR|EVENT|source|book|POST|G1</c> — stable for the life of the source document. Includes the
    /// book, as Procurement's does, so a tenant that later posts to a second book cannot collide.
    /// </summary>
    public static string BuildIdempotencyKey(string eventCode, Guid sourceDocumentId, string accountingBookCode, int generation)
        => $"HR|{eventCode}|{sourceDocumentId:N}|{accountingBookCode.Trim().ToUpperInvariant()}|POST|G{Math.Max(1, generation)}";

    public static string ResolvePostingAction(HrFinancePostingEventDefinition definition, int generation)
    {
        var action = string.IsNullOrWhiteSpace(definition.PostingAction) ? "Post" : definition.PostingAction;
        return generation > 1 ? $"{action}#{generation}" : action;
    }

    private static DateTime ResolvePostingDate(HrFinancePostingRule? rule, HrFinancePostingCommand command, DateTime now)
    {
        if (rule is { PostOnActionDate: false } && command.SourceDate.HasValue)
            return command.SourceDate.Value.Date;
        return now.Date;
    }

    private static decimal SumDebits(IReadOnlyList<HrFinancePostingLine> lines)
        => decimal.Round(lines.Where(l => l.IsDebit).Sum(l => l.Amount), 2, MidpointRounding.AwayFromZero);

    private static HrFinancePostingOutcome ToOutcome(HrFinancePostingRecord record, bool wasDuplicate) => new()
    {
        RecordId = record.Id,
        Status = record.Status,
        PostingEventId = record.PostingEventId,
        JournalEntryId = record.JournalEntryId,
        JournalEntryNumber = record.JournalEntryNumber,
        StatusReason = record.StatusReason,
        WasDuplicate = wasDuplicate
    };

    private static string Truncate(string? value, int max)
    {
        var v = (value ?? string.Empty).Trim();
        return v.Length <= max ? v : v[..max];
    }
}

/// <summary>Display names for the account roles, shared by the adapter's messages and the settings DTOs.</summary>
public static class HrFinanceAccountRoleNames
{
    public static string Name(HrFinanceAccountRole role) => role switch
    {
        HrFinanceAccountRole.StaffClaimsPayable => "Staff claims payable",
        HrFinanceAccountRole.StaffAdvancesReceivable => "Staff advances receivable",
        HrFinanceAccountRole.StaffPaymentsClearing => "Staff payments clearing",
        HrFinanceAccountRole.MedicalExpense => "Medical expense",
        HrFinanceAccountRole.TravelExpense => "Travel expense",
        HrFinanceAccountRole.LeaveEncashmentExpense => "Leave encashment expense",
        HrFinanceAccountRole.AwardsExpense => "Awards expense",
        HrFinanceAccountRole.BenefitsExpense => "Benefits expense",
        HrFinanceAccountRole.SeparationExpense => "Separation expense",
        HrFinanceAccountRole.EmployeeRecoveriesIncome => "Employee recoveries income",
        HrFinanceAccountRole.StatutoryDeductionsPayable => "Statutory deductions payable",
        HrFinanceAccountRole.StaffReceivables => "Staff receivables",
        HrFinanceAccountRole.StaffReceivableWriteOff => "Staff receivable write-off",
        HrFinanceAccountRole.RecruitmentExpense => "Recruitment expense",
        HrFinanceAccountRole.InsuranceRecoveriesIncome => "Insurance recoveries income",
        _ => role.ToString()
    };

    public static string Description(HrFinanceAccountRole role) => role switch
    {
        HrFinanceAccountRole.StaffClaimsPayable => "Liability. What the company owes employees for approved medical and travel claims until they are paid.",
        HrFinanceAccountRole.StaffAdvancesReceivable => "Asset. Travel advances handed to employees and not yet recovered from a claim.",
        HrFinanceAccountRole.StaffPaymentsClearing => "Asset (clearing). Where HR's 'paid' lands until Finance's Cash module clears it against the bank — the same shape as the payroll clearing account.",
        HrFinanceAccountRole.MedicalExpense => "Expense. Medical reimbursements to employees and their dependants.",
        HrFinanceAccountRole.TravelExpense => "Expense. Staff travel — per diems, accommodation, transport and incidentals.",
        HrFinanceAccountRole.LeaveEncashmentExpense => "Expense. Leave days paid out instead of taken — in service where policy allows it, and on exit.",
        HrFinanceAccountRole.AwardsExpense => "Expense. Cash awards and long-service awards conferred on employees.",
        HrFinanceAccountRole.BenefitsExpense => "Expense. Benefit utilisations reimbursed to employees under their enrolments.",
        HrFinanceAccountRole.SeparationExpense => "Expense. Final pay on exit — unpaid salary, notice pay, gratuity or end-of-service, pension-related and other earnings on a settlement.",
        HrFinanceAccountRole.EmployeeRecoveriesIncome => "Revenue. What the company recovers from an employee on exit for unreturned property and other deductions (loans and advances clear the receivable instead).",
        HrFinanceAccountRole.StatutoryDeductionsPayable => "Liability. Tax and other statutory amounts withheld from a settlement and owed to the authority.",
        HrFinanceAccountRole.StaffReceivables => "Asset. What employees owe the company for asset surcharges, disciplinary fines and breached training bonds — separate from advances so the two ageings do not mix.",
        HrFinanceAccountRole.StaffReceivableWriteOff => "Expense. A staff receivable forgiven: a waived surcharge, fine or bond.",
        HrFinanceAccountRole.RecruitmentExpense => "Expense. The cost of filling a post — adverts, agency fees, assessments, medicals. The expense line on the AP invoice HR raises to the supplier.",
        HrFinanceAccountRole.InsuranceRecoveriesIncome => "Revenue. What an insurer or the NHIS pays the company back on a medical, NHIS or incident claim.",
        _ => string.Empty
    };
}
