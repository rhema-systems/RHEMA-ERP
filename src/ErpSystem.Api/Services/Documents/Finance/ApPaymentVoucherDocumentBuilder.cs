using System.Globalization;
using System.Security.Cryptography;
using ErpSystem.Core.DTOs.Documents;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Workflow;
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
/// Renders a controlled AP payment voucher from the existing <see cref="VendorPayment"/> source.
/// The builder is deliberately read-only with respect to payment accounting: it does not allocate,
/// authorize or post a payment, and it never creates a second voucher transaction. This preserves
/// one source chain from payment number through allocation, journal, reversal and WHT evidence.
/// </summary>
public sealed class ApPaymentVoucherDocumentBuilder : IDocumentBuilder
{
    private const string FinanceModule = "AP";

    private static readonly HashSet<VendorPaymentStatus> PrintableStatuses =
    [
        VendorPaymentStatus.Authorized,
        VendorPaymentStatus.Processed,
        VendorPaymentStatus.Cleared,
        VendorPaymentStatus.Reconciled,
        VendorPaymentStatus.Reversed
    ];

    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IFinanceAccessScopeService _financeAccessScopeService;
    private readonly IFinanceAuditService? _financeAuditService;

    public ApPaymentVoucherDocumentBuilder(
        ApplicationDbContext context,
        ICurrentUserService currentUser,
        IFinanceAccessScopeService financeAccessScopeService,
        IFinanceAuditService? financeAuditService = null)
    {
        _context = context;
        _currentUser = currentUser;
        _financeAccessScopeService = financeAccessScopeService;
        _financeAuditService = financeAuditService;
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public string DocumentType => DocumentTypes.FinanceApPaymentVoucher;

    public bool RequiresEntityId => true;

    public bool SupportsFormat(string format)
        => string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase);

    public async Task<RenderedDocumentDto> RenderAsync(
        DocumentRenderRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!SupportsFormat(request.Format))
        {
            throw new NotSupportedException("AP payment vouchers currently support PDF output only.");
        }

        var tenantId = _currentUser.TenantId
            ?? throw new UnauthorizedAccessException("Invalid tenant context.");
        var copyType = NormalizeCopyType(request.CopyType);
        var generatedAtUtc = DateTime.UtcNow;
        var model = await LoadModelAsync(request.EntityId, tenantId, cancellationToken);

        if (!PrintableStatuses.Contains(model.Payment.Status))
        {
            // Draft, pending, failed and voided payments are not approved payment instructions.
            // Blocking their output prevents an attractive PDF from being mistaken for authority
            // to release funds before the configured workflow has completed.
            throw new InvalidOperationException(
                $"Payment '{model.Payment.PaymentNumber}' cannot be printed as a voucher while its status is {model.Payment.Status}.");
        }

        var pdfBytes = BuildPdf(model, copyType, generatedAtUtc, _currentUser.UserName);
        var pdfSha256 = Convert.ToHexString(SHA256.HashData(pdfBytes)).ToLowerInvariant();

        // Rendering is itself a controlled disclosure. Record the emitted-byte hash and the
        // retained evidence counts so reprints can be distinguished without mutating the payment.
        if (_financeAuditService != null)
        {
            await _financeAuditService.RecordAsync(new FinanceAuditEventDto
            {
                EventType = FinanceAuditEvents.ApPaymentVoucherGenerated,
                TenantId = tenantId,
                SourceModule = FinanceModule,
                SourceDocumentType = nameof(VendorPayment),
                SourceDocumentId = model.Payment.Id,
                JournalEntryId = model.Payment.JournalEntryId,
                AfterValues = new
                {
                    VoucherNumber = model.Payment.PaymentNumber,
                    model.Payment.Status,
                    CopyType = copyType,
                    PdfSha256 = pdfSha256,
                    AllocationCount = model.Payment.Allocations.Count,
                    AccountingLineCount = model.JournalEntry?.Transactions.Count ?? 0,
                    ApprovalCount = model.Approvals.Count,
                    EvidenceCount = model.Evidence.Count,
                    GeneratedAtUtc = generatedAtUtc
                },
                Resource = "Finance.APPaymentVoucher",
                ResourceId = model.Payment.Id.ToString(),
                Comment = $"{copyType} AP payment voucher generated from the canonical vendor payment."
            }, cancellationToken);
        }

        return new RenderedDocumentDto
        {
            Content = pdfBytes,
            ContentType = "application/pdf",
            FileName = $"{SafeFileName(model.Payment.PaymentNumber, "payment-voucher")}-{copyType.ToLowerInvariant()}.pdf",
            DocumentType = DocumentType,
            EntityId = request.EntityId,
            Format = "pdf"
        };
    }

    private async Task<PaymentVoucherModel> LoadModelAsync(
        Guid paymentId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        // Keep the tenant predicate explicit even though normal query filters also apply. Voucher
        // identifiers can appear on emails and bank paperwork, so knowing an ID must not reveal a
        // different tenant's payment.
        var payment = await _context.Set<VendorPayment>()
            .AsNoTracking()
            .Include(item => item.Supplier)
            .Include(item => item.BankAccount)
            .Include(item => item.ConfiguredPaymentMethod)
            .Include(item => item.PaymentBatch)
            .Include(item => item.Allocations)
                .ThenInclude(item => item.VendorInvoice)
            .FirstOrDefaultAsync(item =>
                item.Id == paymentId &&
                item.TenantId == tenantId &&
                !item.IsDeleted,
                cancellationToken)
            ?? throw new KeyNotFoundException($"Vendor payment '{paymentId}' was not found.");

        // Document exports must respect the same Finance resource boundary as list/detail APIs.
        // Otherwise a user could bypass a bank-account grant simply by guessing the renderer URL.
        await _financeAccessScopeService.EnsureBankAccountAccessAsync(
            payment.BankAccountId,
            FinanceAccessLevel.Read,
            cancellationToken);

        var tenant = await _context.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == tenantId && !item.IsDeleted, cancellationToken);

        JournalEntry? journalEntry = null;
        if (payment.JournalEntryId.HasValue)
        {
            journalEntry = await _context.JournalEntries
                .AsNoTracking()
                .Include(item => item.Transactions)
                    .ThenInclude(item => item.Account)
                .FirstOrDefaultAsync(item =>
                    item.Id == payment.JournalEntryId.Value &&
                    item.TenantId == tenantId &&
                    !item.IsDeleted,
                    cancellationToken);
        }

        var workflowSourceIds = new List<Guid> { payment.Id };
        if (payment.PaymentBatchId.HasValue)
        {
            workflowSourceIds.Add(payment.PaymentBatchId.Value);
        }

        var workflowInstances = await _context.WorkflowInstances
            .AsNoTracking()
            .Include(item => item.EntityType)
            .Where(item =>
                item.TenantId == tenantId &&
                !item.IsDeleted &&
                workflowSourceIds.Contains(item.EntityId))
            .ToListAsync(cancellationToken);

        // A Guid collision with an unrelated workflow is extremely unlikely, but document control
        // should not depend on probability. Retain only the direct-payment or owning-batch routes.
        workflowInstances = workflowInstances
            .Where(item =>
                (item.EntityId == payment.Id && IsEntityType(item.EntityType, "VendorPayment")) ||
                (payment.PaymentBatchId.HasValue &&
                 item.EntityId == payment.PaymentBatchId.Value &&
                 IsEntityType(item.EntityType, "PaymentBatch")))
            .ToList();

        var instanceIds = workflowInstances.Select(item => item.Id).ToList();
        var stepIds = instanceIds.Count == 0
            ? new List<Guid>()
            : await _context.WorkflowStepInstances
                .AsNoTracking()
                .Where(item =>
                    item.TenantId == tenantId &&
                    !item.IsDeleted &&
                    instanceIds.Contains(item.WorkflowInstanceId))
                .Select(item => item.Id)
                .ToListAsync(cancellationToken);

        var approvals = stepIds.Count == 0
            ? new List<PaymentVoucherApproval>()
            : await _context.WorkflowApprovals
                .AsNoTracking()
                .Where(item =>
                    item.TenantId == tenantId &&
                    !item.IsDeleted &&
                    stepIds.Contains(item.StepInstanceId))
                .OrderBy(item => item.ApprovalGroup)
                .ThenBy(item => item.RequestedDate)
                .Select(item => new PaymentVoucherApproval(
                    item.StepInstance.WorkflowStep.Name,
                    item.ApproverRole,
                    item.Status.ToString(),
                    item.ProcessedBy == null ? null : item.ProcessedBy.UserName,
                    item.ProcessedDate,
                    item.Comments))
                .ToListAsync(cancellationToken);

        var evidence = stepIds.Count == 0
            ? new List<WorkflowEvidenceDocument>()
            : await _context.WorkflowEvidenceDocuments
                .AsNoTracking()
                .Where(item =>
                    item.TenantId == tenantId &&
                    !item.IsDeleted &&
                    stepIds.Contains(item.StepInstanceId))
                .OrderBy(item => item.DocumentName)
                .ThenByDescending(item => item.Version)
                .ToListAsync(cancellationToken);

        var userIds = new[]
            {
                payment.AuthorizedById,
                payment.ReversedById,
                payment.PaymentBatch?.CreatedById,
                payment.PaymentBatch?.ApprovedById,
                payment.PaymentBatch?.ProcessedById
            }
            .Where(item => item.HasValue)
            .Select(item => item!.Value)
            .Distinct()
            .ToList();

        var userNames = userIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _context.Users
                .AsNoTracking()
                .Where(item => userIds.Contains(item.Id))
                .ToDictionaryAsync(
                    item => item.Id,
                    item => string.IsNullOrWhiteSpace(item.UserName)
                        ? item.Id.ToString()
                        : item.UserName!,
                    cancellationToken);

        return new PaymentVoucherModel(
            payment,
            tenant,
            journalEntry,
            approvals,
            evidence,
            userNames);
    }

    private static byte[] BuildPdf(
        PaymentVoucherModel model,
        string copyType,
        DateTime generatedAtUtc,
        string? generatedBy)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.PageColor(Colors.White);
                // Disable standard ligatures so copied/searchable voucher text retains the exact
                // character sequence (for example, "posting" rather than an embedded glyph).
                // This improves archive search and screen-reader extraction without changing the
                // visible TDC document layout.
                page.DefaultTextStyle(style => style
                    .FontSize(8.5f)
                    .FontColor(Colors.Grey.Darken4)
                    .DisableFontFeature(FontFeatures.StandardLigatures));

                page.Header().Element(header => ComposeHeader(header, model, copyType));
                page.Content().PaddingTop(12).Element(content => ComposeContent(content, model));
                page.Footer().Element(footer => ComposeFooter(footer, model, generatedAtUtc, generatedBy));
            });
        }).GeneratePdf();
    }

    private static void ComposeHeader(IContainer container, PaymentVoucherModel model, string copyType)
    {
        var payment = model.Payment;
        var tenantName = string.IsNullOrWhiteSpace(model.Tenant?.Name) ? "ERP System" : model.Tenant!.Name;

        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(left =>
                {
                    left.Item().Text(tenantName).Bold().FontSize(13).FontColor(Colors.Blue.Darken3);
                    if (!string.IsNullOrWhiteSpace(model.Tenant?.Address))
                    {
                        left.Item().Text(model.Tenant.Address).FontSize(8).FontColor(Colors.Grey.Darken1);
                    }

                    var contact = string.Join("  |  ", new[] { model.Tenant?.ContactPhone, model.Tenant?.ContactEmail }
                        .Where(value => !string.IsNullOrWhiteSpace(value)));
                    if (!string.IsNullOrWhiteSpace(contact))
                    {
                        left.Item().Text(contact).FontSize(8).FontColor(Colors.Grey.Darken1);
                    }
                });

                row.ConstantItem(245).AlignRight().Column(right =>
                {
                    right.Item().Text("PAYMENT VOUCHER").Bold().FontSize(18).FontColor(Colors.Blue.Darken3);
                    // The canonical AP payment number doubles as the voucher number. A separate
                    // sequence would make operations reconcile two identifiers for one liability.
                    right.Item().Text(payment.PaymentNumber).SemiBold().FontSize(11);
                    right.Item().Text(copyType.ToUpperInvariant()).FontSize(8).FontColor(Colors.Grey.Darken1);
                });
            });

            column.Item().PaddingTop(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
            column.Item().PaddingTop(7).Element(item => StatusBanner(item, payment));
        });
    }

    private static void StatusBanner(IContainer container, VendorPayment payment)
    {
        var reversed = payment.Status == VendorPaymentStatus.Reversed;
        var color = reversed ? Colors.Red.Darken2 : Colors.Green.Darken2;
        var message = reversed
            ? "REVERSED - this retained voucher no longer authorizes payment"
            : $"{payment.Status.ToString().ToUpperInvariant()} - controlled supplier payment document";

        container.Border(1).BorderColor(color).Background(reversed ? Colors.Red.Lighten5 : Colors.Green.Lighten5)
            .PaddingVertical(5).PaddingHorizontal(9).Text(message).SemiBold().FontSize(8.5f).FontColor(color);
    }

    private static void ComposeContent(IContainer container, PaymentVoucherModel model)
    {
        container.Column(column =>
        {
            column.Item().Element(item => ComposeSummary(item, model));
            column.Item().PaddingTop(12).Element(item => ComposePayeeAndAmount(item, model));
            column.Item().PaddingTop(14).Text("Invoice / Advance Allocation").Bold().FontSize(11).FontColor(Colors.Blue.Darken3);
            column.Item().PaddingTop(5).Element(item => ComposeAllocations(item, model));
            column.Item().PaddingTop(14).Text("Accounting Coding").Bold().FontSize(11).FontColor(Colors.Blue.Darken3);
            column.Item().PaddingTop(5).Element(item => ComposeAccountingCoding(item, model));
            column.Item().PaddingTop(14).Element(item => ComposeControls(item, model));

            if (model.Payment.Status == VendorPaymentStatus.Reversed)
            {
                column.Item().PaddingTop(12).Element(item => ComposeReversal(item, model));
            }

            if (!string.IsNullOrWhiteSpace(model.Payment.Notes))
            {
                column.Item().PaddingTop(12).Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(notes =>
                {
                    notes.Item().Text("Narrative").SemiBold().FontColor(Colors.Grey.Darken1);
                    notes.Item().PaddingTop(3).Text(model.Payment.Notes);
                });
            }
        });
    }

    private static void ComposeSummary(IContainer container, PaymentVoucherModel model)
    {
        var payment = model.Payment;
        container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(9).Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Element(cell => MetaCell(cell, "Voucher Date", payment.PaymentDate.ToString("dd MMM yyyy")));
                row.RelativeItem().Element(cell => MetaCell(cell, "Payment Method", payment.ConfiguredPaymentMethod?.Name ?? SplitWords(payment.PaymentMethod.ToString())));
                row.RelativeItem().Element(cell => MetaCell(cell, "Currency / Rate", $"{payment.CurrencyCode} / {payment.ExchangeRate:N6}"));
                row.RelativeItem().Element(cell => MetaCell(cell, "Payment Status", payment.Status.ToString()));
            });

            column.Item().PaddingTop(7).Row(row =>
            {
                row.RelativeItem().Element(cell => MetaCell(cell, "Bank / Cash Account", PaymentAccountLabel(payment)));
                row.RelativeItem().Element(cell => MetaCell(cell, "Cheque / Transaction Ref.", FirstNonEmpty(payment.ChequeNumber, payment.TransactionReference, "-")));
                row.RelativeItem().Element(cell => MetaCell(cell, "Payment Batch", payment.PaymentBatch?.BatchNumber ?? "Direct payment"));
                row.RelativeItem().Element(cell => MetaCell(cell, "Posting Journal", model.JournalEntry?.JournalEntryNumber ?? "Awaiting posting"));
            });
        });
    }

    private static void ComposePayeeAndAmount(IContainer container, PaymentVoucherModel model)
    {
        var payment = model.Payment;
        container.Row(row =>
        {
            row.RelativeItem(1.55f).Border(1).BorderColor(Colors.Grey.Lighten2).Padding(9).Column(column =>
            {
                column.Item().Text("Payee").FontSize(7.5f).FontColor(Colors.Grey.Darken1);
                column.Item().Text(payment.Supplier?.Name ?? "-").Bold().FontSize(11);
                column.Item().PaddingTop(2).Text($"Supplier: {payment.Supplier?.SupplierCode ?? payment.SupplierId.ToString()}")
                    .FontSize(8).FontColor(Colors.Grey.Darken1);
                if (!string.IsNullOrWhiteSpace(payment.Supplier?.TaxId))
                {
                    column.Item().Text($"Tax ID: {payment.Supplier.TaxId}").FontSize(8).FontColor(Colors.Grey.Darken1);
                }
            });

            row.ConstantItem(10);
            row.RelativeItem(1.45f).Border(1).BorderColor(Colors.Grey.Lighten2).Padding(9).Column(column =>
            {
                column.Item().AlignRight().Text("Gross Payment Amount").FontSize(7.5f).FontColor(Colors.Grey.Darken1);
                column.Item().AlignRight().Text(Money(payment.TotalAmount, payment.CurrencyCode)).Bold().FontSize(15).FontColor(Colors.Blue.Darken3);
                column.Item().PaddingTop(5).Text(AmountInWords(payment.TotalAmount, payment.CurrencyCode).ToUpperInvariant())
                    .SemiBold().FontSize(8);
                if (payment.WithholdingTaxAmount > 0m)
                {
                    column.Item().PaddingTop(4).AlignRight().Text($"WHT withheld: {Money(payment.WithholdingTaxAmount, payment.CurrencyCode)}")
                        .FontSize(8).FontColor(Colors.Orange.Darken3);
                }
            });
        });
    }

    private static void ComposeAllocations(IContainer container, PaymentVoucherModel model)
    {
        var allocations = model.Payment.Allocations
            .OrderBy(item => item.AllocationDate)
            .ThenBy(item => item.Id)
            .ToList();

        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(1.45f);
                columns.RelativeColumn(1.15f);
                columns.RelativeColumn(1.05f);
                columns.RelativeColumn(1.05f);
                columns.RelativeColumn(1.15f);
                columns.RelativeColumn(0.75f);
            });

            table.Header(header =>
            {
                header.Cell().Element(HeaderCell).Text("Invoice / Source");
                header.Cell().Element(HeaderCell).Text("Allocation Date");
                header.Cell().Element(HeaderCell).AlignRight().Text("Applied");
                header.Cell().Element(HeaderCell).AlignRight().Text("Discount");
                header.Cell().Element(HeaderCell).AlignRight().Text("WHT");
                header.Cell().Element(HeaderCell).Text("State");
            });

            foreach (var allocation in allocations)
            {
                table.Cell().Element(BodyCell).Text(allocation.VendorInvoice?.InvoiceNumber ?? allocation.VendorInvoiceId.ToString());
                table.Cell().Element(BodyCell).Text(allocation.AllocationDate.ToString("dd MMM yyyy"));
                table.Cell().Element(BodyCell).AlignRight().Text(Money(allocation.AllocatedAmount, model.Payment.CurrencyCode));
                table.Cell().Element(BodyCell).AlignRight().Text(Money(allocation.DiscountAmount, model.Payment.CurrencyCode));
                table.Cell().Element(BodyCell).AlignRight().Text(Money(allocation.WithholdingTaxAmount, model.Payment.CurrencyCode));
                table.Cell().Element(BodyCell).Text(allocation.IsReversal ? "Reversal" : "Applied")
                    .FontColor(allocation.IsReversal ? Colors.Red.Darken2 : Colors.Green.Darken2);
            }

            if (allocations.Count == 0)
            {
                table.Cell().ColumnSpan(6).Element(BodyCell).AlignCenter()
                    .Text(model.Payment.IsSupplierAdvance
                        ? "Supplier advance - no invoice allocation existed at voucher generation."
                        : "No invoice allocations were recorded.");
            }

            table.Cell().ColumnSpan(2).Element(TotalCell).Text("Allocation totals").SemiBold();
            table.Cell().Element(TotalCell).AlignRight().Text(Money(allocations.Sum(item => item.AllocatedAmount), model.Payment.CurrencyCode)).SemiBold();
            table.Cell().Element(TotalCell).AlignRight().Text(Money(allocations.Sum(item => item.DiscountAmount), model.Payment.CurrencyCode)).SemiBold();
            table.Cell().Element(TotalCell).AlignRight().Text(Money(allocations.Sum(item => item.WithholdingTaxAmount), model.Payment.CurrencyCode)).SemiBold();
            table.Cell().Element(TotalCell).Text(string.Empty);
        });
    }

    private static void ComposeAccountingCoding(IContainer container, PaymentVoucherModel model)
    {
        var lines = model.JournalEntry?.Transactions
            .OrderBy(item => item.LineNumber <= 0 ? int.MaxValue : item.LineNumber)
            .ThenBy(item => item.Id)
            .ToList() ?? [];

        if (lines.Count == 0)
        {
            container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(9)
                .Text("Accounting coding will be populated from the central Finance posting journal. No posting journal was linked when this authorized voucher was generated.")
                .FontColor(Colors.Grey.Darken1);
            return;
        }

        var currency = lines.Select(item => item.FunctionalCurrencyCode)
            .FirstOrDefault(item => !string.IsNullOrWhiteSpace(item)) ?? model.Tenant?.BaseCurrency ?? "GHS";

        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(25);
                columns.RelativeColumn(1.65f);
                columns.RelativeColumn(2.25f);
                columns.RelativeColumn(1f);
                columns.RelativeColumn(1f);
            });

            table.Header(header =>
            {
                header.Cell().Element(HeaderCell).Text("#");
                header.Cell().Element(HeaderCell).Text("Account");
                header.Cell().Element(HeaderCell).Text("Coding Narrative");
                header.Cell().Element(HeaderCell).AlignRight().Text("Debit");
                header.Cell().Element(HeaderCell).AlignRight().Text("Credit");
            });

            var lineNumber = 1;
            foreach (var line in lines)
            {
                table.Cell().Element(BodyCell).Text(lineNumber++.ToString(CultureInfo.InvariantCulture));
                table.Cell().Element(BodyCell).Column(column =>
                {
                    column.Item().Text(line.Account?.AccountNumber ?? line.Account?.AccountCode ?? "-").SemiBold();
                    column.Item().Text(line.Account?.AccountName ?? "-").FontSize(7.5f).FontColor(Colors.Grey.Darken1);
                });
                table.Cell().Element(BodyCell).Text(line.Description ?? "-");
                table.Cell().Element(BodyCell).AlignRight().Text(line.DebitAmount > 0m ? Money(line.DebitAmount, currency) : "-");
                table.Cell().Element(BodyCell).AlignRight().Text(line.CreditAmount > 0m ? Money(line.CreditAmount, currency) : "-");
            }

            table.Cell().ColumnSpan(3).Element(TotalCell).Text($"Journal {model.JournalEntry!.JournalEntryNumber} totals").SemiBold();
            table.Cell().Element(TotalCell).AlignRight().Text(Money(lines.Sum(item => item.DebitAmount), currency)).SemiBold();
            table.Cell().Element(TotalCell).AlignRight().Text(Money(lines.Sum(item => item.CreditAmount), currency)).SemiBold();
        });
    }

    private static void ComposeControls(IContainer container, PaymentVoucherModel model)
    {
        container.Row(row =>
        {
            row.RelativeItem().Element(item => ComposeApprovalRegister(item, model));
            row.ConstantItem(10);
            row.RelativeItem().Element(item => ComposeEvidenceRegister(item, model));
        });
    }

    private static void ComposeApprovalRegister(IContainer container, PaymentVoucherModel model)
    {
        container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(9).Column(column =>
        {
            column.Item().Text("Preparation and Approval").Bold().FontSize(10).FontColor(Colors.Blue.Darken3);
            column.Item().PaddingTop(6).Text($"Prepared by: {FirstNonEmpty(model.Payment.CreatedBy, "System")}");
            column.Item().Text($"Created: {model.Payment.CreatedAt:dd MMM yyyy HH:mm} UTC").FontSize(7.5f).FontColor(Colors.Grey.Darken1);

            if (model.Payment.PaymentBatch != null)
            {
                column.Item().PaddingTop(6).Text($"Batch authority: {model.Payment.PaymentBatch.BatchNumber}").SemiBold();
                column.Item().Text($"Approved by: {model.UserName(model.Payment.PaymentBatch.ApprovedById)}");
                column.Item().Text($"Approved: {DateTimeText(model.Payment.PaymentBatch.ApprovedDate)}").FontSize(7.5f).FontColor(Colors.Grey.Darken1);
            }
            else
            {
                column.Item().PaddingTop(6).Text($"Authorized by: {model.UserName(model.Payment.AuthorizedById)}").SemiBold();
                column.Item().Text($"Authorized: {DateTimeText(model.Payment.AuthorizedDate)}").FontSize(7.5f).FontColor(Colors.Grey.Darken1);
            }

            foreach (var approval in model.Approvals)
            {
                column.Item().PaddingTop(6).BorderTop(1).BorderColor(Colors.Grey.Lighten3).PaddingTop(4).Column(detail =>
                {
                    detail.Item().Text($"{approval.StepName}: {approval.Status}").SemiBold();
                    detail.Item().Text(FirstNonEmpty(approval.ProcessedBy, approval.ApproverRole, "Unassigned"));
                    detail.Item().Text(DateTimeText(approval.ProcessedAt)).FontSize(7.5f).FontColor(Colors.Grey.Darken1);
                    if (!string.IsNullOrWhiteSpace(approval.Comments))
                    {
                        detail.Item().Text(approval.Comments).FontSize(7.5f).FontColor(Colors.Grey.Darken1);
                    }
                });
            }
        });
    }

    private static void ComposeEvidenceRegister(IContainer container, PaymentVoucherModel model)
    {
        container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(9).Column(column =>
        {
            column.Item().Text("Supporting Evidence Register").Bold().FontSize(10).FontColor(Colors.Blue.Darken3);
            if (model.Evidence.Count == 0)
            {
                column.Item().PaddingTop(6).Text("No workflow evidence was linked to the payment or owning payment batch at generation time.")
                    .FontColor(Colors.Grey.Darken1);
                return;
            }

            foreach (var evidence in model.Evidence)
            {
                column.Item().PaddingTop(6).BorderTop(1).BorderColor(Colors.Grey.Lighten3).PaddingTop(4).Column(detail =>
                {
                    detail.Item().Text(FirstNonEmpty(evidence.DocumentName, evidence.FileName)).SemiBold();
                    detail.Item().Text($"{evidence.DocumentType ?? "Evidence"} - v{evidence.Version} - {evidence.VerificationStatus}")
                        .FontSize(7.5f);
                    detail.Item().Text($"SHA-256: {ShortHash(evidence.Sha256)} - {(evidence.IsCurrent ? "Current" : "Superseded")}")
                        .FontSize(7).FontColor(Colors.Grey.Darken1);
                });
            }
        });
    }

    private static void ComposeReversal(IContainer container, PaymentVoucherModel model)
    {
        var payment = model.Payment;
        container.Border(1).BorderColor(Colors.Red.Lighten2).Background(Colors.Red.Lighten5).Padding(9).Column(column =>
        {
            column.Item().Text("Reversal / Cancellation Evidence").Bold().FontSize(10).FontColor(Colors.Red.Darken2);
            column.Item().PaddingTop(5).Text($"Reversal date: {DateTimeText(payment.ReversalDate)}");
            column.Item().Text($"Reversal journal: {payment.ReversalJournalEntryId?.ToString() ?? "-"}");
            column.Item().Text($"Reversed by: {model.UserName(payment.ReversedById)}");
            column.Item().Text($"Reason: {FirstNonEmpty(payment.ReversalReason, "No reason retained")}");
        });
    }

    private static void ComposeFooter(
        IContainer container,
        PaymentVoucherModel model,
        DateTime generatedAtUtc,
        string? generatedBy)
    {
        container.AlignCenter().Text(text =>
        {
            text.Span($"Generated {generatedAtUtc:yyyy-MM-dd HH:mm} UTC");
            if (!string.IsNullOrWhiteSpace(generatedBy))
            {
                text.Span($" by {generatedBy}");
            }

            text.Span($"  |  Source {model.Payment.Id:N}  |  Page ");
            text.CurrentPageNumber();
            text.Span(" of ");
            text.TotalPages();
        });
    }

    private static void MetaCell(IContainer container, string label, string value)
    {
        container.Column(column =>
        {
            column.Item().Text(label).FontSize(7.25f).FontColor(Colors.Grey.Darken1);
            column.Item().Text(string.IsNullOrWhiteSpace(value) ? "-" : value).SemiBold().FontSize(8.25f);
        });
    }

    private static IContainer HeaderCell(IContainer container)
        => container.DefaultTextStyle(style => style.SemiBold().FontSize(7.75f))
            .Background(Colors.Grey.Lighten3)
            .Border(1)
            .BorderColor(Colors.Grey.Lighten2)
            .PaddingVertical(5)
            .PaddingHorizontal(4);

    private static IContainer BodyCell(IContainer container)
        => container.BorderBottom(1)
            .BorderColor(Colors.Grey.Lighten3)
            .PaddingVertical(5)
            .PaddingHorizontal(4)
            .DefaultTextStyle(style => style.FontSize(7.75f));

    private static IContainer TotalCell(IContainer container)
        => container.Background(Colors.Grey.Lighten4)
            .BorderTop(1)
            .BorderColor(Colors.Grey.Lighten2)
            .PaddingVertical(5)
            .PaddingHorizontal(4)
            .DefaultTextStyle(style => style.FontSize(7.75f));

    private static bool IsEntityType(WorkflowEntityType entityType, string expected)
        => string.Equals(entityType.Code, expected, StringComparison.OrdinalIgnoreCase) ||
           string.Equals(entityType.Name, expected, StringComparison.OrdinalIgnoreCase);

    private static string NormalizeCopyType(string? copyType)
    {
        var value = copyType?.Trim();
        if (string.IsNullOrWhiteSpace(value)) return "Original";
        if (value.Equals("Original", StringComparison.OrdinalIgnoreCase)) return "Original";
        if (value.Equals("Reprint", StringComparison.OrdinalIgnoreCase)) return "Reprint";
        return "Copy";
    }

    private static string PaymentAccountLabel(VendorPayment payment)
    {
        if (payment.BankAccount == null)
        {
            return payment.PaymentMethod == VendorPaymentMethod.Cash ? "Cash" : "Tenant default";
        }

        return $"{payment.BankAccount.BankName} - {payment.BankAccount.AccountName} - {MaskAccountNumber(payment.BankAccount.AccountNumber)}";
    }

    private static string MaskAccountNumber(string? accountNumber)
    {
        var value = accountNumber?.Trim();
        if (string.IsNullOrWhiteSpace(value)) return "-";
        if (value.Length <= 4) return value;
        return $"****{value[^4..]}";
    }

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

        var prefix = amount < 0m ? "minus " : string.Empty;
        var words = $"{prefix}{NumberInWords(whole)} {major}";
        if (fraction > 0)
        {
            words += $" and {NumberInWords(fraction)} {minor}";
        }

        return $"{words} only";
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

    private static string ScaleWords(long value, long scale, string scaleName)
        => $"{NumberInWords(value / scale)} {scaleName}{(value % scale == 0 ? string.Empty : $" {NumberInWords(value % scale)}")}";

    private static string Money(decimal value, string currency)
        => $"{currency} {value:N2}";

    private static string SplitWords(string value)
        => string.Concat(value.Select((character, index) =>
            index > 0 && char.IsUpper(character) ? $" {character}" : character.ToString()));

    private static string DateTimeText(DateTime? value)
        => value?.ToString("dd MMM yyyy HH:mm 'UTC'") ?? "-";

    private static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? "-";

    private static string ShortHash(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? "not retained"
            : value.Length <= 20 ? value : $"{value[..12]}...{value[^8..]}";

    private static string SafeFileName(string? value, string fallback)
    {
        var candidate = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            candidate = candidate.Replace(invalid, '-');
        }

        return candidate;
    }

    private sealed record PaymentVoucherApproval(
        string StepName,
        string? ApproverRole,
        string Status,
        string? ProcessedBy,
        DateTime? ProcessedAt,
        string? Comments);

    private sealed record PaymentVoucherModel(
        VendorPayment Payment,
        Tenant? Tenant,
        JournalEntry? JournalEntry,
        IReadOnlyList<PaymentVoucherApproval> Approvals,
        IReadOnlyList<WorkflowEvidenceDocument> Evidence,
        IReadOnlyDictionary<Guid, string> UserNames)
    {
        public string UserName(Guid? userId)
            => userId.HasValue && UserNames.TryGetValue(userId.Value, out var userName)
                ? userName
                : userId?.ToString() ?? "-";
    }
}
