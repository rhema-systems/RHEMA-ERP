using System.Globalization;
using System.Security.Cryptography;
using ErpSystem.Api.Services.Finance;
using ErpSystem.Core.DTOs.Documents;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Documents;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Colors = QuestPDF.Helpers.Colors;

namespace ErpSystem.Api.Services.Documents.Finance;

/// <summary>
/// Renders the TDC payment slip from the existing posted cash/bank payment. The builder never
/// creates a payment or a journal: the canonical CashTransaction supplies the final bank account,
/// counter-account, approval state, and central posting evidence required by FR-CB-008.
/// </summary>
public sealed class CashBankPaymentSlipDocumentBuilder : IDocumentBuilder
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IFinanceAccessScopeService _accessScope;
    private readonly IFinanceControlledDocumentIssueService _issueService;

    public CashBankPaymentSlipDocumentBuilder(
        ApplicationDbContext context,
        ICurrentUserService currentUser,
        IFinanceAccessScopeService accessScope,
        IFinanceControlledDocumentIssueService issueService)
    {
        _context = context;
        _currentUser = currentUser;
        _accessScope = accessScope;
        _issueService = issueService;
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
    }

    public string DocumentType => DocumentTypes.FinanceCashBankPaymentSlip;
    public bool RequiresEntityId => true;
    public bool SupportsFormat(string format) => string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase);

    public async Task<RenderedDocumentDto> RenderAsync(
        DocumentRenderRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!SupportsFormat(request.Format))
            throw new NotSupportedException("Controlled payment slips support PDF output only.");

        var tenantId = _currentUser.GetRequiredFinanceTenantId();
        var transaction = await _context.Set<CashTransaction>()
            .AsNoTracking()
            .Include(item => item.BankAccount)
            .Include(item => item.PaymentMethod)
            .Include(item => item.Cheque)
            .Include(item => item.JournalEntry)
                .ThenInclude(item => item!.Transactions)
                    .ThenInclude(item => item.Account)
            .FirstOrDefaultAsync(item =>
                item.Id == request.EntityId &&
                item.TenantId == tenantId &&
                !item.IsDeleted,
                cancellationToken)
            ?? throw new KeyNotFoundException($"Cash/bank transaction '{request.EntityId}' was not found.");

        await _accessScope.EnsureBankAccountAccessAsync(
            transaction.BankAccountId,
            FinanceAccessLevel.Read,
            cancellationToken);

        if (transaction.TransactionType != CashTransactionType.Payment)
            throw new InvalidOperationException("A payment slip can only be issued for a cash/bank transaction of type Payment.");
        if (!transaction.IsPosted || transaction.ApprovalStatus != CashTransactionApprovalStatus.Posted ||
            !transaction.JournalEntryId.HasValue || transaction.JournalEntry == null ||
            !string.Equals(transaction.JournalEntry.PostingStatus, "Posted", StringComparison.OrdinalIgnoreCase))
        {
            // A polished slip must never become informal authority to pay before the configured
            // workflow and central posting path have completed.
            throw new InvalidOperationException("The payment slip is available only after approval and ledger posting are complete.");
        }
        if (transaction.ReversalOfCashTransactionId.HasValue)
            throw new InvalidOperationException("A compensating cash/bank row is reversal evidence and cannot be issued as a new payment slip.");

        var tenant = await LoadTenantAsync(tenantId, cancellationToken);
        var counterAccount = transaction.GLAccountId.HasValue
            ? await _context.Accounts.AsNoTracking().FirstOrDefaultAsync(item =>
                item.Id == transaction.GLAccountId.Value && item.TenantId == tenantId && !item.IsDeleted,
                cancellationToken)
            : null;
        var userNames = await LoadUserNamesAsync(
            [transaction.ApprovedById, transaction.PostedBy],
            cancellationToken);
        var preparation = await _issueService.PrepareAsync(
            DocumentType,
            nameof(CashTransaction),
            transaction.Id,
            transaction.TransactionNumber,
            request.CopyType,
            Option(request, "replacementReason"),
            cancellationToken);

        var model = new ControlledTransactionDocumentModel
        {
            Tenant = tenant,
            Title = "PAYMENT SLIP",
            DocumentNumber = transaction.TransactionNumber,
            Status = transaction.IsReversed ? "REVERSED" : "POSTED",
            StatusDetail = transaction.IsReversed
                ? $"Reversed {DateText(transaction.ReversalDate)}: {transaction.ReversalReason}"
                : "Approved payment instruction posted to the Finance ledger.",
            PartyLabel = "Payee",
            PartyName = TextOrDash(transaction.PayeeOrPayer),
            Amount = transaction.Amount,
            Currency = transaction.Currency,
            Narrative = TextOrDash(transaction.Description),
            Details =
            [
                new("Payment date", transaction.TransactionDate.ToString("dd MMM yyyy")),
                new("Payment method", transaction.PaymentMethod?.Name ?? "Not specified"),
                new("External reference", TextOrDash(transaction.ReferenceNumber)),
                new("Payment account", BankAccountLabel(transaction.BankAccount)),
                new("Bank allocation", $"{transaction.BankAccount.BankName} / {MaskAccountNumber(transaction.BankAccount.AccountNumber)}"),
                new("Final counter-account", counterAccount == null
                    ? transaction.GLAccountId?.ToString() ?? "Defined by posted journal"
                    : $"{AccountNumber(counterAccount)} - {counterAccount.AccountName}"),
                new("Journal", transaction.JournalEntry.JournalEntryNumber),
                new("Ledger posting date", DateText(transaction.PostedDate ?? transaction.JournalEntry.PostingDate))
            ],
            AccountingLines = transaction.JournalEntry.Transactions
                .OrderBy(item => item.LineNumber <= 0 ? int.MaxValue : item.LineNumber)
                .ThenBy(item => item.Id)
                .Select(item => new ControlledTransactionDocumentLine(
                    AccountNumber(item.Account),
                    item.Account?.AccountName ?? "Account",
                    TextOrDash(item.Description),
                    item.DebitAmount,
                    item.CreditAmount))
                .ToList(),
            PreparedBy = TextOrDash(transaction.CreatedBy),
            ApprovedBy = UserName(userNames, transaction.ApprovedById),
            PostedBy = UserName(userNames, transaction.PostedBy),
            JournalNumber = transaction.JournalEntry.JournalEntryNumber,
            SourceId = transaction.Id
        };

        var fileName = ControlledTransactionDocumentPdf.SafeFileName(
            $"{transaction.TransactionNumber}-{CopyFileLabel(preparation)}.pdf");
        var pdf = ControlledTransactionDocumentPdf.Build(model, preparation);
        var hash = Convert.ToHexString(SHA256.HashData(pdf)).ToLowerInvariant();
        await _issueService.RecordRetainedIssuedAsync(
            preparation,
            fileName,
            "application/pdf",
            hash,
            pdf,
            RetainUntil(preparation),
            preparation.CopyType == ControlledDocumentCopyTypes.Original
                ? FinanceAuditEvents.CashBankPaymentSlipIssued
                : FinanceAuditEvents.CashBankPaymentSlipReplacementIssued,
            "CashBank",
            "Finance.CashBank.PaymentSlip",
            transaction.JournalEntryId,
            cancellationToken);

        return new RenderedDocumentDto
        {
            Content = pdf,
            ContentType = "application/pdf",
            FileName = fileName,
            DocumentType = DocumentType,
            EntityId = transaction.Id,
            Format = "pdf"
        };
    }

    private async Task<Tenant> LoadTenantAsync(Guid tenantId, CancellationToken cancellationToken)
        => await _context.Tenants.AsNoTracking().FirstOrDefaultAsync(item => item.Id == tenantId && !item.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException("The Finance tenant was not found.");

    private async Task<IReadOnlyDictionary<Guid, string>> LoadUserNamesAsync(
        IEnumerable<Guid?> userIds,
        CancellationToken cancellationToken)
    {
        var ids = userIds.Where(item => item.HasValue).Select(item => item!.Value).Distinct().ToList();
        return ids.Count == 0
            ? new Dictionary<Guid, string>()
            : await _context.Users.AsNoTracking()
                .Where(item => ids.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, item => item.UserName ?? item.Id.ToString(), cancellationToken);
    }

    internal static string? Option(DocumentRenderRequestDto request, string key)
        => request.Options != null && request.Options.TryGetValue(key, out var value) ? value : null;

    internal static string CopyFileLabel(ControlledDocumentIssuePreparationDto preparation)
        => preparation.CopyType == ControlledDocumentCopyTypes.Original
            ? "original"
            : $"replacement-{preparation.CopyNumber:00}";

    // Cash instructions and receipts are accounting evidence. Retain the exact issued PDF bytes
    // for seven years from issuance; later source-record changes must not recreate or rewrite them.
    internal static DateTime RetainUntil(ControlledDocumentIssuePreparationDto preparation)
        => preparation.IssuedAtUtc.AddYears(7);

    internal static string UserName(IReadOnlyDictionary<Guid, string> users, Guid? id)
        => id.HasValue && users.TryGetValue(id.Value, out var name) ? name : id?.ToString() ?? "-";

    internal static string AccountNumber(Account? account)
        => account?.AccountNumber ?? account?.AccountCode ?? "-";

    internal static string BankAccountLabel(BankAccount account)
        => $"{account.BankName} - {account.AccountName} - {MaskAccountNumber(account.AccountNumber)}";

    internal static string MaskAccountNumber(string? accountNumber)
    {
        var value = accountNumber?.Trim();
        if (string.IsNullOrWhiteSpace(value)) return "-";
        return value.Length <= 4 ? value : $"****{value[^4..]}";
    }

    internal static string TextOrDash(string? value) => string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
    internal static string DateText(DateTime? value) => value?.ToString("dd MMM yyyy") ?? "-";
}

/// <summary>
/// Renders an official receipt from CustomerPayment. Cash, cheque, clearing-account and direct
/// bank methods all remain one AR receipt source; the builder reads their existing settlement
/// account and allocation evidence without manufacturing another receipt transaction.
/// </summary>
public sealed class CustomerReceiptDocumentBuilder : IDocumentBuilder
{
    private static readonly HashSet<string> PrintableStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Posted", "Cleared", "Bounced", "Reversed"
    };

    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IFinanceAccessScopeService _accessScope;
    private readonly IFinanceControlledDocumentIssueService _issueService;

    public CustomerReceiptDocumentBuilder(
        ApplicationDbContext context,
        ICurrentUserService currentUser,
        IFinanceAccessScopeService accessScope,
        IFinanceControlledDocumentIssueService issueService)
    {
        _context = context;
        _currentUser = currentUser;
        _accessScope = accessScope;
        _issueService = issueService;
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
    }

    public string DocumentType => DocumentTypes.FinanceArCustomerReceipt;
    public bool RequiresEntityId => true;
    public bool SupportsFormat(string format) => string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase);

    public async Task<RenderedDocumentDto> RenderAsync(
        DocumentRenderRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!SupportsFormat(request.Format))
            throw new NotSupportedException("Controlled customer receipts support PDF output only.");

        var tenantId = _currentUser.GetRequiredFinanceTenantId();
        var payment = await _context.Set<CustomerPayment>()
            .AsNoTracking()
            .Include(item => item.BankAccount)
            .Include(item => item.LiquidityAccount)
                .ThenInclude(item => item!.GLAccount)
            .Include(item => item.ConfiguredPaymentMethod)
            .Include(item => item.Allocations)
                .ThenInclude(item => item.Invoice)
            .FirstOrDefaultAsync(item =>
                item.Id == request.EntityId &&
                item.TenantId == tenantId &&
                !item.IsDeleted,
                cancellationToken)
            ?? throw new KeyNotFoundException($"Customer receipt '{request.EntityId}' was not found.");

        // Passing null intentionally requires tenant-wide scope for liquidity-held cash/cheque
        // receipts until liquidity accounts become an independently assignable scope dimension.
        await _accessScope.EnsureBankAccountAccessAsync(
            payment.BankAccountId,
            FinanceAccessLevel.Read,
            cancellationToken);

        if (payment.IsCreditNote)
            throw new InvalidOperationException("A credit note is not a cash receipt and must use the canonical credit-note document path.");
        if (!PrintableStatuses.Contains(payment.Status) || !payment.JournalEntryId.HasValue)
            throw new InvalidOperationException("The official receipt is available only after the customer payment is posted.");

        var journal = await _context.JournalEntries
            .AsNoTracking()
            .Include(item => item.Transactions)
                .ThenInclude(item => item.Account)
            .FirstOrDefaultAsync(item =>
                item.Id == payment.JournalEntryId.Value &&
                item.TenantId == tenantId &&
                !item.IsDeleted,
                cancellationToken)
            ?? throw new InvalidOperationException("The posted customer receipt does not have its canonical ledger journal.");
        if (!string.Equals(journal.PostingStatus, "Posted", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The customer receipt journal is not posted.");

        var tenant = await _context.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == tenantId && !item.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException("The Finance tenant was not found.");
        var customer = await _context.Set<BusinessPartner>().AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == payment.BusinessPartnerId && item.TenantId == tenantId && !item.IsDeleted, cancellationToken);
        var postedByName = journal.PostedByUserId.HasValue
            ? await _context.Users.AsNoTracking()
                .Where(item => item.Id == journal.PostedByUserId.Value)
                .Select(item => item.UserName)
                .FirstOrDefaultAsync(cancellationToken)
            : journal.CreatedBy;
        var preparation = await _issueService.PrepareAsync(
            DocumentType,
            nameof(CustomerPayment),
            payment.Id,
            payment.PaymentNumber,
            request.CopyType,
            CashBankPaymentSlipDocumentBuilder.Option(request, "replacementReason"),
            cancellationToken);

        var settlementAccount = payment.BankAccount != null
            ? CashBankPaymentSlipDocumentBuilder.BankAccountLabel(payment.BankAccount)
            : payment.LiquidityAccount == null
                ? "Tenant default settlement account"
                : $"{payment.LiquidityAccount.Code} - {payment.LiquidityAccount.Name} ({CashBankPaymentSlipDocumentBuilder.AccountNumber(payment.LiquidityAccount.GLAccount)})";
        var statusDetail = payment.Status.Equals("Reversed", StringComparison.OrdinalIgnoreCase)
            ? $"Reversed {CashBankPaymentSlipDocumentBuilder.DateText(payment.ReversalDate)}: {payment.ReversalReason}"
            : payment.Status.Equals("Bounced", StringComparison.OrdinalIgnoreCase)
                ? "Receipt instrument was subsequently recorded as bounced."
                : "Customer funds posted to the Finance ledger.";

        var model = new ControlledTransactionDocumentModel
        {
            Tenant = tenant,
            Title = "OFFICIAL RECEIPT",
            DocumentNumber = payment.PaymentNumber,
            Status = payment.Status.ToUpperInvariant(),
            StatusDetail = statusDetail,
            PartyLabel = "Received from",
            PartyName = payment.BusinessPartnerName,
            Amount = payment.TotalAmount,
            Currency = payment.CurrencyCode,
            Narrative = CashBankPaymentSlipDocumentBuilder.TextOrDash(payment.Notes),
            Details =
            [
                new("Receipt date", payment.PaymentDate.ToString("dd MMM yyyy")),
                new("Payment method", payment.ConfiguredPaymentMethod?.Name ?? payment.PaymentMethod),
                new("Transaction reference", CashBankPaymentSlipDocumentBuilder.TextOrDash(payment.TransactionReference)),
                new("Cheque number", CashBankPaymentSlipDocumentBuilder.TextOrDash(payment.CheckNumber)),
                new("Drawer bank", CashBankPaymentSlipDocumentBuilder.TextOrDash(payment.ChequeDrawerBank)),
                new("Settlement account", settlementAccount),
                new("Allocated amount", Money(payment.AllocatedAmount, payment.CurrencyCode)),
                new("Unallocated / advance", Money(payment.UnallocatedAmount, payment.CurrencyCode)),
                new("WHT suffered", Money(payment.WithholdingTaxAmount + payment.VatWithholdingAmount, payment.CurrencyCode)),
                new("Journal", journal.JournalEntryNumber)
            ],
            Applications = payment.Allocations
                .OrderBy(item => item.AllocationDate)
                .ThenBy(item => item.Id)
                .Select(item => new ControlledTransactionDocumentApplication(
                    item.Invoice?.InvoiceNumber ?? item.InvoiceId.ToString(),
                    item.AllocationDate,
                    item.AllocatedAmount,
                    item.DiscountAmount,
                    item.IsReversal ? "Reversal" : "Applied"))
                .ToList(),
            AccountingLines = journal.Transactions
                .OrderBy(item => item.LineNumber <= 0 ? int.MaxValue : item.LineNumber)
                .ThenBy(item => item.Id)
                .Select(item => new ControlledTransactionDocumentLine(
                    CashBankPaymentSlipDocumentBuilder.AccountNumber(item.Account),
                    item.Account?.AccountName ?? "Account",
                    CashBankPaymentSlipDocumentBuilder.TextOrDash(item.Description),
                    item.DebitAmount,
                    item.CreditAmount))
                .ToList(),
            PreparedBy = CashBankPaymentSlipDocumentBuilder.TextOrDash(payment.CreatedBy),
            ApprovedBy = "Posted customer receipt",
            PostedBy = CashBankPaymentSlipDocumentBuilder.TextOrDash(postedByName),
            JournalNumber = journal.JournalEntryNumber,
            SourceId = payment.Id
        };

        var fileName = ControlledTransactionDocumentPdf.SafeFileName(
            $"{payment.PaymentNumber}-{CashBankPaymentSlipDocumentBuilder.CopyFileLabel(preparation)}.pdf");
        var pdf = ControlledTransactionDocumentPdf.Build(model, preparation);
        var hash = Convert.ToHexString(SHA256.HashData(pdf)).ToLowerInvariant();
        await _issueService.RecordRetainedIssuedAsync(
            preparation,
            fileName,
            "application/pdf",
            hash,
            pdf,
            CashBankPaymentSlipDocumentBuilder.RetainUntil(preparation),
            preparation.CopyType == ControlledDocumentCopyTypes.Original
                ? FinanceAuditEvents.CustomerReceiptIssued
                : FinanceAuditEvents.CustomerReceiptReplacementIssued,
            "AR",
            "Finance.AR.CustomerReceipt",
            payment.JournalEntryId,
            cancellationToken);

        return new RenderedDocumentDto
        {
            Content = pdf,
            ContentType = "application/pdf",
            FileName = fileName,
            DocumentType = DocumentType,
            EntityId = payment.Id,
            Format = "pdf"
        };
    }

    private static string Money(decimal value, string currency) => $"{currency} {value:N2}";
}

/// <summary>
/// Shared A4 presentation for the two TDC cash/bank transaction documents. A single composer
/// keeps copy labels, replacement watermark, byte-level issue identity, account coding, and
/// footer evidence consistent while each builder supplies its own canonical accounting model.
/// </summary>
internal static class ControlledTransactionDocumentPdf
{
    public static byte[] Build(
        ControlledTransactionDocumentModel model,
        ControlledDocumentIssuePreparationDto issue)
    {
        return Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(style => style
                    .FontSize(8.5f)
                    .FontColor(Colors.Grey.Darken4)
                    .DisableFontFeature(FontFeatures.StandardLigatures));

                if (issue.CopyType == ControlledDocumentCopyTypes.Replacement)
                {
                    // The watermark sits in the page background, so it repeats on continuation
                    // pages without obscuring searchable accounting text in the primary layer.
                    page.Background().AlignCenter().AlignMiddle()
                        .Text($"REPLACEMENT COPY {issue.CopyNumber}")
                        .Bold().FontSize(44).FontColor(Colors.Grey.Lighten3);
                }

                page.Header().Element(container => Header(container, model, issue));
                page.Content().PaddingVertical(10).Column(column =>
                {
                    column.Spacing(10);
                    column.Item().Element(container => StatusBanner(container, model));
                    if (issue.CopyType == ControlledDocumentCopyTypes.Replacement)
                        column.Item().Element(container => ReplacementNotice(container, issue));
                    column.Item().Element(container => Summary(container, model));
                    column.Item().Element(container => Details(container, model.Details));
                    if (model.Applications.Count > 0)
                        column.Item().Element(container => Applications(container, model));
                    column.Item().Element(container => Accounting(container, model));
                    column.Item().Element(container => ControlSignoff(container, model));
                });
                page.Footer().AlignCenter().Text(text =>
                {
                    text.DefaultTextStyle(style => style.FontSize(6.5f).FontColor(Colors.Grey.Darken1));
                    text.Span($"Issue {issue.IssueId:N} | {issue.CopyType} copy {issue.CopyNumber} | Issued {issue.IssuedAtUtc:yyyy-MM-dd HH:mm} UTC by {issue.IssuedByName} | Source {model.SourceId:N} | Page ");
                    text.CurrentPageNumber();
                    text.Span(" of ");
                    text.TotalPages();
                });
            });
        }).GeneratePdf();
    }

    private static void Header(
        IContainer container,
        ControlledTransactionDocumentModel model,
        ControlledDocumentIssuePreparationDto issue)
    {
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(left =>
                {
                    left.Item().Text(model.Tenant.Name).Bold().FontSize(13).FontColor(Colors.Blue.Darken3);
                    if (!string.IsNullOrWhiteSpace(model.Tenant.Address))
                        left.Item().Text(model.Tenant.Address).FontSize(7.5f).FontColor(Colors.Grey.Darken1);
                    var contact = string.Join(" | ", new[] { model.Tenant.ContactPhone, model.Tenant.ContactEmail }
                        .Where(value => !string.IsNullOrWhiteSpace(value)));
                    if (!string.IsNullOrWhiteSpace(contact))
                        left.Item().Text(contact).FontSize(7.5f).FontColor(Colors.Grey.Darken1);
                });
                row.RelativeItem().AlignRight().Column(right =>
                {
                    right.Item().Text(model.Title).Bold().FontSize(16).FontColor(Colors.Blue.Darken3);
                    right.Item().Text(model.DocumentNumber).SemiBold().FontSize(10);
                    right.Item().Text(issue.CopyType == ControlledDocumentCopyTypes.Original
                            ? "CONTROLLED ORIGINAL"
                            : $"REPLACEMENT COPY {issue.CopyNumber}")
                        .Bold().FontSize(8)
                        .FontColor(issue.CopyType == ControlledDocumentCopyTypes.Original
                            ? Colors.Green.Darken2
                            : Colors.Red.Darken2);
                });
            });
            column.Item().PaddingTop(7).LineHorizontal(1).LineColor(Colors.Blue.Darken3);
        });
    }

    private static void StatusBanner(IContainer container, ControlledTransactionDocumentModel model)
    {
        var adverse = model.Status is "REVERSED" or "BOUNCED" or "CANCELLED";
        container.Border(1)
            .BorderColor(adverse ? Colors.Red.Lighten2 : Colors.Green.Lighten2)
            .Background(adverse ? Colors.Red.Lighten5 : Colors.Green.Lighten5)
            .Padding(8)
            .Row(row =>
            {
                row.ConstantItem(90).Text(model.Status).Bold().FontColor(adverse ? Colors.Red.Darken2 : Colors.Green.Darken2);
                row.RelativeItem().Text(model.StatusDetail).FontSize(7.75f);
            });
    }

    private static void ReplacementNotice(IContainer container, ControlledDocumentIssuePreparationDto issue)
    {
        container.Border(1).BorderColor(Colors.Red.Lighten2).Background(Colors.Red.Lighten5).Padding(8).Column(column =>
        {
            column.Item().Text("REPLACEMENT-COPY CONTROL").Bold().FontColor(Colors.Red.Darken2);
            column.Item().PaddingTop(3).Text($"Reason: {issue.ReplacementReason}");
            column.Item().Text($"Authorized issue user: {issue.IssuedByName} | {issue.IssuedAtUtc:dd MMM yyyy HH:mm} UTC")
                .FontSize(7).FontColor(Colors.Grey.Darken1);
        });
    }

    private static void Summary(IContainer container, ControlledTransactionDocumentModel model)
    {
        container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Row(row =>
        {
            row.RelativeItem().Column(column =>
            {
                column.Item().Text(model.PartyLabel).FontSize(7).FontColor(Colors.Grey.Darken1);
                column.Item().Text(model.PartyName).Bold().FontSize(11);
                column.Item().PaddingTop(6).Text("Narrative").FontSize(7).FontColor(Colors.Grey.Darken1);
                column.Item().Text(model.Narrative);
            });
            row.ConstantItem(210).AlignRight().Column(column =>
            {
                column.Item().Text("Amount").FontSize(7).FontColor(Colors.Grey.Darken1);
                column.Item().Text(Money(model.Amount, model.Currency)).Bold().FontSize(15).FontColor(Colors.Blue.Darken3);
                column.Item().PaddingTop(5).Text(AmountInWords(model.Amount, model.Currency).ToUpperInvariant())
                    .SemiBold().FontSize(7.5f);
            });
        });
    }

    private static void Details(IContainer container, IReadOnlyList<ControlledTransactionDocumentDetail> details)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn();
                columns.RelativeColumn();
            });
            foreach (var detail in details)
            {
                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(6).Column(column =>
                {
                    column.Item().Text(detail.Label).FontSize(6.75f).FontColor(Colors.Grey.Darken1);
                    column.Item().Text(detail.Value).SemiBold().FontSize(8);
                });
            }
        });
    }

    private static void Applications(IContainer container, ControlledTransactionDocumentModel model)
    {
        container.Column(column =>
        {
            column.Item().Text("INVOICE APPLICATIONS").Bold().FontSize(9).FontColor(Colors.Blue.Darken3);
            column.Item().PaddingTop(4).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(1.6f);
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                });
                table.Header(header =>
                {
                    header.Cell().Element(HeaderCell).Text("Invoice");
                    header.Cell().Element(HeaderCell).Text("Date");
                    header.Cell().Element(HeaderCell).AlignRight().Text("Applied");
                    header.Cell().Element(HeaderCell).AlignRight().Text("Discount");
                    header.Cell().Element(HeaderCell).Text("State");
                });
                foreach (var item in model.Applications)
                {
                    table.Cell().Element(BodyCell).Text(item.Reference);
                    table.Cell().Element(BodyCell).Text(item.Date.ToString("dd MMM yyyy"));
                    table.Cell().Element(BodyCell).AlignRight().Text(Money(item.AppliedAmount, model.Currency));
                    table.Cell().Element(BodyCell).AlignRight().Text(Money(item.DiscountAmount, model.Currency));
                    table.Cell().Element(BodyCell).Text(item.State);
                }
            });
        });
    }

    private static void Accounting(IContainer container, ControlledTransactionDocumentModel model)
    {
        container.Column(column =>
        {
            column.Item().Text($"LEDGER POSTING - {model.JournalNumber}").Bold().FontSize(9).FontColor(Colors.Blue.Darken3);
            column.Item().PaddingTop(4).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(1.2f);
                    columns.RelativeColumn(1.6f);
                    columns.RelativeColumn(2f);
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                });
                table.Header(header =>
                {
                    header.Cell().Element(HeaderCell).Text("Account");
                    header.Cell().Element(HeaderCell).Text("Name");
                    header.Cell().Element(HeaderCell).Text("Coding narrative");
                    header.Cell().Element(HeaderCell).AlignRight().Text("Debit");
                    header.Cell().Element(HeaderCell).AlignRight().Text("Credit");
                });
                foreach (var line in model.AccountingLines)
                {
                    table.Cell().Element(BodyCell).Text(line.AccountNumber);
                    table.Cell().Element(BodyCell).Text(line.AccountName);
                    table.Cell().Element(BodyCell).Text(line.Description);
                    table.Cell().Element(BodyCell).AlignRight().Text(line.DebitAmount == 0m ? "-" : Money(line.DebitAmount, model.Currency));
                    table.Cell().Element(BodyCell).AlignRight().Text(line.CreditAmount == 0m ? "-" : Money(line.CreditAmount, model.Currency));
                }
                table.Cell().ColumnSpan(3).Element(TotalCell).Text("Journal totals").SemiBold();
                table.Cell().Element(TotalCell).AlignRight().Text(Money(model.AccountingLines.Sum(item => item.DebitAmount), model.Currency)).SemiBold();
                table.Cell().Element(TotalCell).AlignRight().Text(Money(model.AccountingLines.Sum(item => item.CreditAmount), model.Currency)).SemiBold();
            });
        });
    }

    private static void ControlSignoff(IContainer container, ControlledTransactionDocumentModel model)
    {
        container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Row(row =>
        {
            Signoff(row.RelativeItem(), "Prepared / captured by", model.PreparedBy);
            row.ConstantItem(8);
            Signoff(row.RelativeItem(), "Approved authority", model.ApprovedBy);
            row.ConstantItem(8);
            Signoff(row.RelativeItem(), "Ledger posted by", model.PostedBy);
        });
    }

    private static void Signoff(IContainer container, string label, string value)
    {
        container.Column(column =>
        {
            column.Item().Text(label).FontSize(6.75f).FontColor(Colors.Grey.Darken1);
            column.Item().PaddingTop(3).Text(value).SemiBold();
        });
    }

    private static IContainer HeaderCell(IContainer container)
        => container.Background(Colors.Grey.Lighten3).Border(1).BorderColor(Colors.Grey.Lighten2)
            .Padding(4).DefaultTextStyle(style => style.SemiBold().FontSize(7));

    private static IContainer BodyCell(IContainer container)
        => container.BorderBottom(1).BorderColor(Colors.Grey.Lighten3)
            .Padding(4).DefaultTextStyle(style => style.FontSize(7));

    private static IContainer TotalCell(IContainer container)
        => container.Background(Colors.Grey.Lighten4).BorderTop(1).BorderColor(Colors.Grey.Lighten2)
            .Padding(4).DefaultTextStyle(style => style.FontSize(7));

    private static string Money(decimal value, string currency) => $"{currency} {value:N2}";

    private static string AmountInWords(decimal amount, string currencyCode)
    {
        var rounded = decimal.Round(Math.Abs(amount), 2, MidpointRounding.AwayFromZero);
        var whole = decimal.ToInt64(decimal.Truncate(rounded));
        var fraction = decimal.ToInt32((rounded - whole) * 100m);
        var (major, minor) = currencyCode.ToUpperInvariant() switch
        {
            "GHS" => (whole == 1 ? "Ghana cedi" : "Ghana cedis", fraction == 1 ? "pesewa" : "pesewas"),
            "USD" => (whole == 1 ? "US dollar" : "US dollars", fraction == 1 ? "cent" : "cents"),
            "EUR" => (whole == 1 ? "euro" : "euros", fraction == 1 ? "cent" : "cents"),
            "GBP" => (whole == 1 ? "pound sterling" : "pounds sterling", fraction == 1 ? "penny" : "pence"),
            _ => ($"{currencyCode.ToUpperInvariant()} units", "subunits")
        };
        var words = $"{NumberInWords(whole)} {major}";
        if (fraction > 0) words += $" and {NumberInWords(fraction)} {minor}";
        return $"{(amount < 0m ? "minus " : string.Empty)}{words} only";
    }

    private static string NumberInWords(long value)
    {
        if (value == 0) return "zero";
        string[] units =
        [
            "", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten",
            "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen"
        ];
        string[] tens = ["", "", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety"];
        if (value < 20) return units[value];
        if (value < 100) return $"{tens[value / 10]}{(value % 10 == 0 ? string.Empty : $"-{units[value % 10]}")}";
        if (value < 1_000) return $"{NumberInWords(value / 100)} hundred{(value % 100 == 0 ? string.Empty : $" and {NumberInWords(value % 100)}")}";
        if (value < 1_000_000) return ScaleWords(value, 1_000, "thousand");
        if (value < 1_000_000_000) return ScaleWords(value, 1_000_000, "million");
        if (value < 1_000_000_000_000) return ScaleWords(value, 1_000_000_000, "billion");
        return ScaleWords(value, 1_000_000_000_000, "trillion");
    }

    private static string ScaleWords(long value, long scale, string name)
        => $"{NumberInWords(value / scale)} {name}{(value % scale == 0 ? string.Empty : $" {NumberInWords(value % scale)}")}";

    public static string SafeFileName(string value)
    {
        foreach (var invalid in Path.GetInvalidFileNameChars()) value = value.Replace(invalid, '-');
        return value;
    }
}

internal sealed class ControlledTransactionDocumentModel
{
    public Tenant Tenant { get; init; } = null!;
    public string Title { get; init; } = string.Empty;
    public string DocumentNumber { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string StatusDetail { get; init; } = string.Empty;
    public string PartyLabel { get; init; } = string.Empty;
    public string PartyName { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string Currency { get; init; } = "GHS";
    public string Narrative { get; init; } = string.Empty;
    public IReadOnlyList<ControlledTransactionDocumentDetail> Details { get; init; } = [];
    public IReadOnlyList<ControlledTransactionDocumentApplication> Applications { get; init; } = [];
    public IReadOnlyList<ControlledTransactionDocumentLine> AccountingLines { get; init; } = [];
    public string PreparedBy { get; init; } = string.Empty;
    public string ApprovedBy { get; init; } = string.Empty;
    public string PostedBy { get; init; } = string.Empty;
    public string JournalNumber { get; init; } = string.Empty;
    public Guid SourceId { get; init; }
}

internal sealed record ControlledTransactionDocumentDetail(string Label, string Value);
internal sealed record ControlledTransactionDocumentApplication(string Reference, DateTime Date, decimal AppliedAmount, decimal DiscountAmount, string State);
internal sealed record ControlledTransactionDocumentLine(string AccountNumber, string AccountName, string Description, decimal DebitAmount, decimal CreditAmount);
