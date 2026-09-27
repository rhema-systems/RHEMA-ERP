using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.AP;

public partial class VendorInvoiceService
{
    private sealed record InvoiceDistributionDraft(int SchemaVersion, string BasisVersion, Guid ActorId,
        DateTime SavedAtUtc, List<SaveVendorInvoiceDistributionLineDto> Lines);

    private static bool IsProcurementDistribution(VendorInvoice invoice) => !invoice.IsOpeningBalance &&
        (invoice.PurchaseOrderId.HasValue || invoice.AcceptedSupplyKind.HasValue || invoice.AutoInvoiceRequestId.HasValue || invoice.EstateAcquisitionId.HasValue ||
         invoice.LineItems.Any(line => !line.IsDeleted && line.LandedCostItemId.HasValue));

    private static bool DistributionEditable(VendorInvoice invoice) => IsProcurementDistribution(invoice) &&
        !invoice.JournalEntryId.HasValue && invoice.Status is VendorInvoiceStatus.Draft or VendorInvoiceStatus.Rejected;

    private static string DistributionHash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static string DistributionVersion(VendorInvoice invoice) => DistributionHash(JsonSerializer.Serialize(new
    {
        invoice.Id, invoice.Status, invoice.UpdatedAt, invoice.DistributionDraftJson, invoice.JournalEntryId
    }));

    private static string DistributionBasis(VendorInvoice invoice, FinancePostingRequestV2Dto request) =>
        DistributionHash(JsonSerializer.Serialize(new
        {
            invoice.BusinessPartnerId, invoice.InvoiceDate, invoice.CurrencyCode, invoice.ExchangeRate, invoice.TotalAmount,
            invoice.PurchaseOrderId, invoice.AcceptedSupplyKind, invoice.AcceptedSupplySourceId, invoice.EstateAcquisitionId, invoice.EstatePayableKind,
            request.FunctionalCurrencyCode,
            Lines = request.Lines.Select(line => new { line.SourceDocumentLineId, line.AccountId, line.TransactionTag,
                line.DebitAmount, line.CreditAmount, line.TransactionDebitAmount, line.TransactionCreditAmount,
                line.ExchangeRate, line.TransactionCurrency, line.Notes }),
            Sources = invoice.LineItems.Where(line => !line.IsDeleted).OrderBy(line => line.Id)
                .Select(line => new { line.Id, line.BudgetEntryId, line.GLAccountId })
        }));

    private static List<VendorInvoiceDistributionGroupDto> DistributionGroups(VendorInvoice invoice, FinancePostingRequestV2Dto request) =>
        request.Lines.Select((line, index) =>
        {
            var editableAccount = !invoice.EstateAcquisitionId.HasValue && line.TransactionTag == "AP-Expense" &&
                !invoice.LineItems.Any(source => source.Id == line.SourceDocumentLineId && source.BudgetEntryId.HasValue);
            return new VendorInvoiceDistributionGroupDto
            {
                GroupId = (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
                Type = DistributionType(line.TransactionTag), Description = line.Description, AccountId = line.AccountId,
                Debit = line.DebitAmount, Credit = line.CreditAmount, CanChangeAccount = editableAccount,
                AccountRestriction = editableAccount ? null : "Retain the source account for control, tax, receipt, asset and budget reconciliation."
            };
        }).ToList();

    public Task<VendorInvoiceDistributionDto> SaveDistributionAsync(Guid id, SaveVendorInvoiceDistributionDto request,
        CancellationToken cancellationToken = default) => WriteDistributionAsync(id, request, false, cancellationToken);

    public Task<VendorInvoiceDistributionDto> ResetDistributionAsync(Guid id, SaveVendorInvoiceDistributionDto request,
        CancellationToken cancellationToken = default) => WriteDistributionAsync(id, request, true, cancellationToken);

    private async Task<VendorInvoiceDistributionDto> WriteDistributionAsync(Guid id, SaveVendorInvoiceDistributionDto request,
        bool reset, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!_currentUser.IsAuthenticated || CurrentUserId == Guid.Empty) throw new UnauthorizedAccessException("An authenticated invoice editor is required.");
        return await _unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            var ownsTransaction = !_unitOfWork.HasActiveTransaction;
            if (ownsTransaction)
            {
                _unitOfWork.ClearTrackedChanges();
                await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            }
            try
            {
                await _unitOfWork.AcquireTransactionLockAsync($"ap-invoice-post:{TenantId:N}:{id:N}", ct);
                var invoice = await _unitOfWork.Repository<VendorInvoice>().GetQueryable(value =>
                        value.TenantId == TenantId && value.Id == id && !value.IsDeleted)
                    .Include(value => value.BusinessPartner).Include(value => value.LineItems).SingleOrDefaultAsync(ct)
                    ?? throw new KeyNotFoundException("Supplier invoice was not found for this tenant.");
                var posted = await _unitOfWork.Repository<FinancePostingEvent>().GetQueryable(value =>
                    value.TenantId == TenantId && value.SourceDocumentId == id && value.SourceDocumentType == "VendorInvoice" &&
                    value.PostingStatus == "Posted" && !value.IsDeleted).AnyAsync(ct);
                if (!DistributionEditable(invoice) || posted)
                    throw new InvoiceDistributionConflictException("Only draft or rejected Procurement invoices can have their distribution edited. Posted distributions are locked.");
                var defaults = await BuildApInvoiceDistributionRequestAsync(invoice, Array.Empty<Guid>(), null, ct);
                var basis = DistributionBasis(invoice, defaults);
                if (request.Version != DistributionVersion(invoice) || request.BasisVersion != basis)
                    throw new InvoiceDistributionConflictException("The invoice or posting defaults changed. Reload and review the distribution before saving.");
                if (!reset) await ValidateDistributionLinesAsync(invoice, request.Lines, defaults, ct);
                var before = invoice.DistributionDraftJson;
                invoice.DistributionDraftJson = reset ? null : JsonSerializer.Serialize(new InvoiceDistributionDraft(1, basis,
                    CurrentUserId, DateTime.UtcNow, request.Lines));
                invoice.UpdatedAt = DateTime.UtcNow;
                invoice.UpdatedBy = UserName;
                await _unitOfWork.SaveChangesAsync(ct);
                await RecordApInvoiceAuditAsync(reset ? "AP.Invoice.DistributionReset" : "AP.Invoice.DistributionSaved", invoice,
                    beforeValues: new { Distribution = before }, afterValues: new { Distribution = invoice.DistributionDraftJson }, cancellationToken: ct);
                var result = await GetDistributionAsync(id, ct);
                if (ownsTransaction) await _unitOfWork.CommitAsync(ct);
                return result;
            }
            catch
            {
                if (ownsTransaction && _unitOfWork.HasActiveTransaction) await _unitOfWork.RollbackAsync(ct);
                if (ownsTransaction) _unitOfWork.ClearTrackedChanges();
                throw;
            }
        }, ct);
    }

    private async Task ValidateDistributionLinesAsync(VendorInvoice invoice, List<SaveVendorInvoiceDistributionLineDto> lines,
        FinancePostingRequestV2Dto defaults, CancellationToken ct)
    {
        if (lines is null || lines.Count is < 2 or > 1000 || lines.Any(line => line is null || line.LineId == Guid.Empty ||
            line.AccountId == Guid.Empty || string.IsNullOrWhiteSpace(line.GroupId) || line.Debit < 0 || line.Credit < 0 || line.Debit > 999999999999.99m ||
            line.Credit > 999999999999.99m || (line.Debit == 0) == (line.Credit == 0) ||
            decimal.Round(line.Debit, 2) != line.Debit || decimal.Round(line.Credit, 2) != line.Credit) ||
            lines.Select(line => line.LineId).Distinct().Count() != lines.Count)
            throw new InvalidOperationException("Use unique lines with one positive debit or credit and no more than two decimal places.");
        if (lines.Sum(line => line.Debit) != lines.Sum(line => line.Credit))
            throw new InvalidOperationException("Total debits and credits must balance before saving or posting.");
        var groups = DistributionGroups(invoice, defaults).ToDictionary(group => group.GroupId);
        var splits = lines.GroupBy(line => line.GroupId).ToList();
        if (splits.Count != groups.Count || splits.Any(group => !groups.ContainsKey(group.Key)))
            throw new InvalidOperationException("Keep each invoice posting purpose. Add or remove splits within those purposes.");
        var accountIds = lines.Select(line => line.AccountId).Distinct().ToArray();
        var accounts = await _unitOfWork.Repository<Account>().GetQueryable(account => account.TenantId == TenantId &&
            accountIds.Contains(account.Id) && !account.IsDeleted && account.Status == AccountStatus.Active)
            .AsNoTracking().ToDictionaryAsync(account => account.Id, ct);
        foreach (var split in splits)
        {
            var source = groups[split.Key];
            if (split.Sum(line => line.Debit) != source.Debit || split.Sum(line => line.Credit) != source.Credit)
                throw new InvalidOperationException($"The {source.Type} split must retain debit {source.Debit:0.00} and credit {source.Credit:0.00}. Invoice, tax and supplier totals cannot change here.");
            foreach (var line in split)
            {
                if (!accounts.TryGetValue(line.AccountId, out var account))
                    throw new InvalidOperationException("Select active posting accounts belonging to this tenant.");
                if (source.CanChangeAccount)
                {
                    if (account.AccountType != AccountType.Expense || !account.AllowDirectPosting || account.IsControlAccount)
                        throw new InvalidOperationException("Expense splits require an active direct-posting expense account, not a control account.");
                    if (line.AccountId != source.AccountId && account.BudgetTrackingEnabled)
                        throw new InvalidOperationException("Select budget-controlled accounts on the invoice line so its budget evidence can be reviewed before distribution.");
                }
                else if (line.AccountId != source.AccountId)
                    throw new InvalidOperationException(source.AccountRestriction);
            }
        }
        // Ensure FX rounding remains exact before storing, using the same conversion as posting.
        ComposeDistributionLines(lines, defaults);
    }

    private static IReadOnlyList<FinancePostingLineDto> ComposeDistributionLines(List<SaveVendorInvoiceDistributionLineDto> lines,
        FinancePostingRequestV2Dto defaults, bool consolidate = false)
    {
        var result = new List<FinancePostingLineDto>();
        for (var index = 0; index < defaults.Lines.Count; index++)
        {
            var source = defaults.Lines[index];
            var rows = lines.Where(line => line.GroupId == (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)).ToList();
            // Multiple UI splits to the same historical account do not create ambiguous
            // AP-control or tax lineage for later payments and supplier debit notes.
            if (consolidate) rows = rows.GroupBy(row => row.AccountId).Select(group => new SaveVendorInvoiceDistributionLineDto
            {
                LineId = group.First().LineId, GroupId = group.First().GroupId, AccountId = group.Key,
                Debit = group.Sum(row => row.Debit), Credit = group.Sum(row => row.Credit)
            }).ToList();
            var foreign = !string.Equals(source.TransactionCurrency, defaults.FunctionalCurrencyCode, StringComparison.OrdinalIgnoreCase);
            var originalTransactionAmount = source.TransactionDebitAmount.GetValueOrDefault() + source.TransactionCreditAmount.GetValueOrDefault();
            var transactionAmounts = foreign
                ? MonetaryAllocation.Allocate(rows.Select(row => row.Debit + row.Credit).ToArray(), originalTransactionAmount)
                : rows.Select(row => row.Debit + row.Credit).ToArray();
            for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                var row = rows[rowIndex];
                if (foreign && ToFunctionalAmount(transactionAmounts[rowIndex], source.TransactionCurrency!, defaults.FunctionalCurrencyCode,
                        source.ExchangeRate ?? 1m) != row.Debit + row.Credit)
                    throw new InvalidOperationException("This split creates an exchange-rate rounding difference. Adjust the split amounts to reconcile both currencies.");
                var line = JsonSerializer.Deserialize<FinancePostingLineDto>(JsonSerializer.Serialize(source))!;
                line.AccountId = row.AccountId;
                line.DebitAmount = row.Debit; line.CreditAmount = row.Credit;
                line.TransactionDebitAmount = row.Debit > 0 ? transactionAmounts[rowIndex] : 0;
                line.TransactionCreditAmount = row.Credit > 0 ? transactionAmounts[rowIndex] : 0;
                if (source.ForeignCurrencyAmount.HasValue)
                    line.ForeignCurrencyAmount = Math.Sign(source.ForeignCurrencyAmount.Value) * transactionAmounts[rowIndex];
                line.LineNumber = result.Count + 1;
                result.Add(line);
            }
        }
        return result;
    }

    private async Task ApplySavedDistributionAsync(VendorInvoice invoice, FinancePostingRequestV2Dto request, CancellationToken ct, bool consolidate = false)
    {
        if (string.IsNullOrWhiteSpace(invoice.DistributionDraftJson)) return;
        if (!IsProcurementDistribution(invoice)) throw new InvalidOperationException("Saved Procurement distribution no longer has a Procurement source.");
        InvoiceDistributionDraft draft;
        try { draft = JsonSerializer.Deserialize<InvoiceDistributionDraft>(invoice.DistributionDraftJson) ?? throw new JsonException(); }
        catch (JsonException) { throw new InvalidOperationException("Saved distribution is unreadable. Reset and review it before posting."); }
        if (draft.SchemaVersion != 1 || draft.BasisVersion != DistributionBasis(invoice, request))
        {
            if (invoice.JournalEntryId.HasValue)
                throw new InvalidOperationException("AP_INVOICE_DISTRIBUTION_HISTORY_REQUIRED: the original posted distribution basis cannot be reconstructed. Finance must review and repair its historical source evidence before retrying; do not reset a posted distribution.");
            throw new InvalidOperationException("The invoice or posting defaults changed after distribution was saved. Review and save Distribution again before posting.");
        }
        await ValidateDistributionLinesAsync(invoice, draft.Lines, request, ct);
        request.Lines = ComposeDistributionLines(draft.Lines, request, consolidate);
    }
}

public sealed class InvoiceDistributionConflictException(string message) : InvalidOperationException(message);
