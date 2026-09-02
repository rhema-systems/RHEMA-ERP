using ErpSystem.Core.DTOs.Documents;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Documents;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Colors = QuestPDF.Helpers.Colors;

namespace ErpSystem.Api.Services.Documents.Finance;

public sealed class JournalVoucherDocumentBuilder : IDocumentBuilder
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public JournalVoucherDocumentBuilder(ApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public string DocumentType => DocumentTypes.FinanceJournalVoucher;

    public bool RequiresEntityId => true;

    public bool SupportsFormat(string format)
        => string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase);

    public async Task<RenderedDocumentDto> RenderAsync(DocumentRenderRequestDto request, CancellationToken cancellationToken = default)
    {
        if (!SupportsFormat(request.Format))
        {
            throw new NotSupportedException("Journal vouchers currently support PDF output only.");
        }

        var tenantId = _currentUser.TenantId ?? throw new UnauthorizedAccessException("Invalid tenant context.");

        var entry = await _context.JournalEntries
            .AsNoTracking()
            .Include(j => j.FiscalPeriod)
            .Include(j => j.Transactions)
                .ThenInclude(t => t.Account)
            .FirstOrDefaultAsync(j => j.Id == request.EntityId && j.TenantId == tenantId && !j.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException($"Journal entry '{request.EntityId}' was not found.");

        var tenant = await _context.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);

        var auditTrail = await ResolveAuditTrailAsync(entry, tenantId, cancellationToken);
        var pdfBytes = BuildPdf(entry, tenant, auditTrail, request.CopyType, _currentUser.UserName);
        var fileName = $"{SafeFileName(entry.JournalEntryNumber, "journal-voucher")}.pdf";

        return new RenderedDocumentDto
        {
            Content = pdfBytes,
            ContentType = "application/pdf",
            FileName = fileName,
            DocumentType = DocumentType,
            EntityId = request.EntityId,
            Format = "pdf"
        };
    }

    private async Task<JournalVoucherAuditTrail> ResolveAuditTrailAsync(
        JournalEntry entry,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(entry.SourceDocumentType, nameof(OpeningBalanceBatch), StringComparison.OrdinalIgnoreCase))
        {
            return JournalVoucherAuditTrail.ForJournal(entry);
        }

        if (!entry.SourceDocumentId.HasValue)
        {
            throw new InvalidOperationException(
                "Journal voucher cannot be rendered because opening-balance approval evidence is unavailable.");
        }

        var source = await _context.OpeningBalanceBatches
            .AsNoTracking()
            .Where(batch =>
                batch.Id == entry.SourceDocumentId.Value &&
                batch.TenantId == tenantId &&
                !batch.IsDeleted)
            .Select(batch => new
            {
                batch.BatchNumber,
                batch.Status,
                batch.JournalEntryId,
                batch.PostingEventId,
                batch.WorkflowInstanceId,
                batch.ApprovedAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (source == null ||
            source.JournalEntryId != entry.Id ||
            !source.PostingEventId.HasValue ||
            !source.WorkflowInstanceId.HasValue ||
            !source.ApprovedAt.HasValue ||
            !string.Equals(source.Status, "Posted", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Journal voucher cannot be rendered because opening-balance approval evidence is unavailable or does not match the posted journal.");
        }

        var workflowCompleted = await _context.WorkflowInstances
            .AsNoTracking()
            .AnyAsync(workflow =>
                workflow.Id == source.WorkflowInstanceId.Value &&
                workflow.TenantId == tenantId &&
                !workflow.IsDeleted &&
                workflow.EntityId == entry.SourceDocumentId.Value &&
                workflow.Status == WorkflowInstanceStatus.Completed &&
                workflow.CompletedDate.HasValue,
                cancellationToken);

        if (!workflowCompleted)
        {
            throw new InvalidOperationException(
                "Journal voucher cannot be rendered because the opening-balance approval workflow is incomplete or unavailable.");
        }

        var postingEvent = await _context.FinancePostingEvents
            .AsNoTracking()
            .Where(posting =>
                posting.Id == source.PostingEventId.Value &&
                posting.TenantId == tenantId &&
                !posting.IsDeleted &&
                posting.SourceDocumentType == nameof(OpeningBalanceBatch) &&
                posting.SourceDocumentId == entry.SourceDocumentId.Value &&
                posting.PostingAction == "PostOpeningBalance" &&
                posting.JournalEntryId == entry.Id &&
                posting.PostingStatus == "Posted")
            .Select(posting => new
            {
                posting.Id,
                posting.PostedAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (postingEvent == null || !postingEvent.PostedAt.HasValue)
        {
            throw new InvalidOperationException(
                "Journal voucher cannot be rendered because the opening-balance posting event is unavailable or does not match the posted journal.");
        }

        return new JournalVoucherAuditTrail(
            JournalApprovalStatus: Normalize(entry.ApprovalStatus ?? "Not Required"),
            JournalApprovedAt: entry.ApprovedDate,
            PostedAt: postingEvent.PostedAt,
            SourceDocumentLabel: "Opening Balance Batch",
            SourceDocumentReference: source.BatchNumber,
            SourceApprovalStatus: "Approved",
            SourceApprovedAt: source.ApprovedAt,
            SourceWorkflowInstanceId: source.WorkflowInstanceId,
            SourcePostingEventId: postingEvent.Id);
    }

    private static byte[] BuildPdf(
        JournalEntry entry,
        Tenant? tenant,
        JournalVoucherAuditTrail auditTrail,
        string copyType,
        string? generatedBy)
    {
        var currency = ResolveFunctionalCurrency(entry, tenant);
        var lines = entry.Transactions
            .Where(t => !t.IsDeleted)
            .OrderBy(t => t.LineNumber <= 0 ? int.MaxValue : t.LineNumber)
            .ThenBy(t => t.Id)
            .ToList();

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(9).FontColor(Colors.Grey.Darken4));

                page.Header().Element(header => ComposeHeader(header, entry, tenant, copyType));
                page.Content().Element(content => ComposeContent(content, entry, lines, currency, auditTrail));
                page.Footer().Element(footer => ComposeFooter(footer, generatedBy));
            });
        }).GeneratePdf();
    }

    private static void ComposeHeader(IContainer container, JournalEntry entry, Tenant? tenant, string copyType)
    {
        var tenantName = string.IsNullOrWhiteSpace(tenant?.Name) ? "ERP System" : tenant!.Name;

        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text(tenantName).Bold().FontSize(13).FontColor(Colors.Blue.Darken3);
                    if (!string.IsNullOrWhiteSpace(tenant?.Address))
                    {
                        col.Item().Text(tenant.Address).FontSize(8).FontColor(Colors.Grey.Darken1);
                    }

                    var contact = string.Join("  |  ", new[] { tenant?.ContactPhone, tenant?.ContactEmail }
                        .Where(value => !string.IsNullOrWhiteSpace(value)));
                    if (!string.IsNullOrWhiteSpace(contact))
                    {
                        col.Item().Text(contact).FontSize(8).FontColor(Colors.Grey.Darken1);
                    }
                });

                row.ConstantItem(230).AlignRight().Column(col =>
                {
                    col.Item().Text("JOURNAL VOUCHER").Bold().FontSize(18).FontColor(Colors.Blue.Darken3);
                    col.Item().Text(entry.JournalEntryNumber).SemiBold().FontSize(11);
                    col.Item().Text(Normalize(copyType).ToUpperInvariant()).FontSize(8).FontColor(Colors.Grey.Darken1);
                });
            });

            column.Item().PaddingTop(8).LineHorizontal(1);
        });
    }

    private static void ComposeContent(
        IContainer container,
        JournalEntry entry,
        IReadOnlyList<AccountTransaction> lines,
        string currency,
        JournalVoucherAuditTrail auditTrail)
    {
        container.PaddingTop(12).Column(column =>
        {
            column.Item().Element(content => ComposeSummary(content, entry, currency));

            if (!string.IsNullOrWhiteSpace(entry.Description) || !string.IsNullOrWhiteSpace(entry.Notes))
            {
                column.Item().PaddingTop(10).Border(1).BorderColor(Colors.Grey.Lighten2).Padding(9).Column(col =>
                {
                    col.Item().Text("Narrative").SemiBold().FontSize(9).FontColor(Colors.Grey.Darken1);
                    col.Item().Text(Normalize(entry.Description)).FontSize(10);
                    if (!string.IsNullOrWhiteSpace(entry.Notes))
                    {
                        col.Item().PaddingTop(4).Text(entry.Notes).FontSize(8).FontColor(Colors.Grey.Darken1);
                    }
                });
            }

            column.Item().PaddingTop(14).Text("Transaction Lines").Bold().FontSize(12).FontColor(Colors.Blue.Darken3);
            column.Item().PaddingTop(5).Element(content => ComposeLinesTable(content, lines, currency));
            column.Item().PaddingTop(10).Element(content => ComposeTotals(content, entry, currency));
            column.Item().PaddingTop(14).Element(content => ComposeAuditTrail(content, entry, auditTrail));
        });
    }

    private static void ComposeSummary(IContainer container, JournalEntry entry, string currency)
    {
        container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Element(cell => MetaCell(cell, "Entry Date", entry.EntryDate.ToString("dd MMM yyyy")));
                row.RelativeItem().Element(cell => MetaCell(cell, "Type", Normalize(entry.JournalType)));
                row.RelativeItem().Element(cell => MetaCell(cell, "Book", Normalize(entry.BookClassification)));
                row.RelativeItem().Element(cell => MetaCell(cell, "Period", entry.FiscalPeriod?.PeriodCode ?? "-"));
            });

            column.Item().PaddingTop(8).Row(row =>
            {
                row.RelativeItem().Element(cell => MetaCell(cell, "Reference", Normalize(entry.ReferenceNumber)));
                row.RelativeItem().Element(cell => MetaCell(cell, "Source", Normalize(entry.SourceModule ?? "GL")));
                row.RelativeItem().Element(cell => MetaCell(cell, "Functional Currency", currency));
                row.RelativeItem().Element(cell => StatusCell(cell, Normalize(entry.PostingStatus)));
            });
        });
    }

    private static void ComposeLinesTable(IContainer container, IReadOnlyList<AccountTransaction> lines, string currency)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(24);
                columns.RelativeColumn(2.2f);
                columns.RelativeColumn(2.3f);
                columns.RelativeColumn(1.15f);
                columns.RelativeColumn(1.15f);
                columns.RelativeColumn(1.5f);
            });

            table.Header(header =>
            {
                header.Cell().Element(HeaderCell).Text("#");
                header.Cell().Element(HeaderCell).Text("Account");
                header.Cell().Element(HeaderCell).Text("Description");
                header.Cell().Element(HeaderCell).AlignRight().Text($"Debit ({currency})");
                header.Cell().Element(HeaderCell).AlignRight().Text($"Credit ({currency})");
                header.Cell().Element(HeaderCell).Text("Transaction Evidence");
            });

            var index = 1;
            foreach (var line in lines)
            {
                table.Cell().Element(BodyCell).Text(index.ToString());
                table.Cell().Element(BodyCell).Column(col =>
                {
                    var accountNumber = !string.IsNullOrWhiteSpace(line.Account?.AccountNumber)
                        ? line.Account.AccountNumber
                        : line.Account?.AccountCode ?? "-";

                    col.Item().Text(accountNumber).SemiBold().FontSize(8.5f);
                    col.Item().Text(line.Account?.AccountName ?? "-").FontSize(8).FontColor(Colors.Grey.Darken1);
                });
                table.Cell().Element(BodyCell).Text(Normalize(line.Description)).FontSize(8.5f);
                table.Cell().Element(BodyCell).AlignRight().Text(line.DebitAmount > 0 ? Amount(line.DebitAmount) : "-");
                table.Cell().Element(BodyCell).AlignRight().Text(line.CreditAmount > 0 ? Amount(line.CreditAmount) : "-");
                table.Cell().Element(BodyCell).Text(TransactionCurrencyEvidence(line, currency)).FontSize(8);

                index++;
            }

            if (lines.Count == 0)
            {
                table.Cell().ColumnSpan(6).Element(BodyCell).AlignCenter().Text("No transaction lines found.");
            }
        });
    }

    private static void ComposeTotals(IContainer container, JournalEntry entry, string currency)
    {
        container.AlignRight().Width(280).Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Text("Functional Total Debit").SemiBold();
                row.ConstantItem(120).AlignRight().Text(Money(entry.TotalDebitAmount, currency)).SemiBold();
            });
            column.Item().PaddingTop(4).Row(row =>
            {
                row.RelativeItem().Text("Functional Total Credit").SemiBold();
                row.ConstantItem(120).AlignRight().Text(Money(entry.TotalCreditAmount, currency)).SemiBold();
            });
            column.Item().PaddingTop(4).Row(row =>
            {
                row.RelativeItem().Text("Difference").FontColor(Colors.Grey.Darken1);
                row.ConstantItem(120).AlignRight().Text(Money(entry.BalanceDifference, currency)).FontColor(Colors.Grey.Darken1);
            });
            column.Item().PaddingTop(6).AlignRight().Text(entry.IsBalanced ? "Balanced" : "Out of balance")
                .SemiBold()
                .FontColor(entry.IsBalanced ? Colors.Green.Darken2 : Colors.Red.Darken2);
        });
    }

    private static void ComposeAuditTrail(
        IContainer container,
        JournalEntry entry,
        JournalVoucherAuditTrail auditTrail)
    {
        container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(column =>
        {
            column.Item().Text("Workflow and Audit").Bold().FontSize(11).FontColor(Colors.Blue.Darken3);
            column.Item().PaddingTop(6).Row(row =>
            {
                row.RelativeItem().Element(cell => MetaCell(cell, "Created", UtcTimestamp(entry.CreatedAt)));
                row.RelativeItem().Element(cell => MetaCell(cell, "Journal Approval", auditTrail.JournalApprovalStatus));
                row.RelativeItem().Element(cell => MetaCell(cell, "Journal Approved Date", UtcTimestamp(auditTrail.JournalApprovedAt)));
                row.RelativeItem().Element(cell => MetaCell(cell, "Posted At", UtcTimestamp(auditTrail.PostedAt)));
            });

            if (auditTrail.HasSourceApproval)
            {
                column.Item().PaddingTop(8).Background(Colors.Blue.Lighten5).Padding(8).Column(sourceColumn =>
                {
                    sourceColumn.Item().Text("Source Approval Evidence").SemiBold().FontSize(9).FontColor(Colors.Blue.Darken3);
                    sourceColumn.Item().PaddingTop(5).Row(row =>
                    {
                        row.RelativeItem().Element(cell => MetaCell(
                            cell,
                            "Source Document",
                            $"{auditTrail.SourceDocumentLabel} {auditTrail.SourceDocumentReference}"));
                        row.RelativeItem().Element(cell => MetaCell(cell, "Source Approval", auditTrail.SourceApprovalStatus!));
                        row.RelativeItem().Element(cell => MetaCell(cell, "Source Approved Date", UtcTimestamp(auditTrail.SourceApprovedAt)));
                    });

                    sourceColumn.Item()
                        .PaddingTop(5)
                        .DefaultTextStyle(style => style.FontSize(8).FontColor(Colors.Grey.Darken2))
                        .Text(text =>
                        {
                            text.Span("Approval basis: ").SemiBold();
                            text.Span("the source opening-balance batch completed its controlled workflow before this journal was posted. ");
                            text.Span("The system-generated journal did not require a separate approval.");
                        if (auditTrail.SourceWorkflowInstanceId.HasValue)
                        {
                            text.Span($" Workflow reference: {auditTrail.SourceWorkflowInstanceId.Value:D}.");
                        }
                        if (auditTrail.SourcePostingEventId.HasValue)
                        {
                            text.Span($" Posting event: {auditTrail.SourcePostingEventId.Value:D}.");
                        }
                        });
                });
            }

            if (entry.IsReversed || entry.ReversalDate.HasValue || !string.IsNullOrWhiteSpace(entry.ReversalReason))
            {
                column.Item().PaddingTop(8).Border(1).BorderColor(Colors.Red.Lighten3).Padding(8).Text(text =>
                {
                    text.Span("Reversal: ").SemiBold().FontColor(Colors.Red.Darken2);
                    text.Span($"{entry.ReversalDate?.ToString("dd MMM yyyy") ?? "Marked reversed"}");
                    if (!string.IsNullOrWhiteSpace(entry.ReversalReason))
                    {
                        text.Span($" - {entry.ReversalReason}");
                    }
                });
            }
        });
    }

    private static void ComposeFooter(IContainer container, string? generatedBy)
    {
        container.AlignCenter().Text(text =>
        {
            text.Span($"Generated {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC");
            if (!string.IsNullOrWhiteSpace(generatedBy))
            {
                text.Span($" by {generatedBy}");
            }

            text.Span("  |  Page ");
            text.CurrentPageNumber();
            text.Span(" of ");
            text.TotalPages();
        });
    }

    private static void MetaCell(IContainer container, string label, string value)
    {
        container.Column(column =>
        {
            column.Item().Text(label).FontSize(7.5f).FontColor(Colors.Grey.Darken1);
            column.Item().Text(string.IsNullOrWhiteSpace(value) ? "-" : value).SemiBold().FontSize(9);
        });
    }

    private static void StatusCell(IContainer container, string status)
    {
        var color = StatusColor(status);
        container.Column(column =>
        {
            column.Item().Text("Status").FontSize(7.5f).FontColor(Colors.Grey.Darken1);
            column.Item().PaddingTop(2).AlignLeft().Background(Colors.Grey.Lighten4).Border(1).BorderColor(color).PaddingHorizontal(7).PaddingVertical(2)
                .Text(status).SemiBold().FontSize(8).FontColor(color);
        });
    }

    private static IContainer HeaderCell(IContainer container)
        => container.DefaultTextStyle(x => x.SemiBold().FontSize(8.5f))
            .Background(Colors.Grey.Lighten3)
            .Border(1)
            .BorderColor(Colors.Grey.Lighten2)
            .Padding(5);

    private static IContainer BodyCell(IContainer container)
        => container.BorderBottom(1)
            .BorderColor(Colors.Grey.Lighten3)
            .Padding(5);

    private static string ResolveFunctionalCurrency(JournalEntry entry, Tenant? tenant)
    {
        var functionalCurrencies = entry.Transactions
            .Where(transaction => !transaction.IsDeleted)
            .Select(transaction => transaction.FunctionalCurrencyCode?.Trim().ToUpperInvariant())
            .Where(currency => !string.IsNullOrWhiteSpace(currency))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (functionalCurrencies.Length > 1)
        {
            throw new InvalidOperationException("A journal voucher cannot mix functional currencies.");
        }

        return functionalCurrencies.FirstOrDefault()
            ?? tenant?.BaseCurrency?.Trim().ToUpperInvariant()
            ?? "GHS";
    }

    private static string TransactionCurrencyEvidence(AccountTransaction line, string functionalCurrency)
    {
        var transactionCurrency = line.TransactionCurrency?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(transactionCurrency) ||
            string.Equals(transactionCurrency, functionalCurrency, StringComparison.OrdinalIgnoreCase))
        {
            return $"{functionalCurrency} functional only";
        }

        var transactionAmount = line.DebitAmount > 0
            ? line.TransactionDebitAmount
            : line.TransactionCreditAmount;
        transactionAmount ??= line.ForeignCurrencyAmount;

        if (!transactionAmount.HasValue)
        {
            return transactionCurrency;
        }

        var rate = line.ExchangeRate.HasValue && line.ExchangeRate.Value > 0
            ? $" @ {line.ExchangeRate.Value:0.######}"
            : string.Empty;
        return $"{Money(transactionAmount.Value, transactionCurrency)}{rate}";
    }

    private static string Money(decimal amount, string currency)
        => $"{currency} {amount:N2}";

    private static string Amount(decimal amount)
        => $"{amount:N2}";

    private static string UtcTimestamp(DateTime? value)
    {
        if (!value.HasValue)
        {
            return "-";
        }

        var utc = value.Value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
            : value.Value.ToUniversalTime();
        return $"{utc:dd MMM yyyy HH:mm} UTC";
    }

    private static string Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();

    private static string SafeFileName(string? value, string fallback)
    {
        var fileName = Normalize(value);
        if (fileName == "-")
        {
            fileName = fallback;
        }

        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            fileName = fileName.Replace(invalid, '-');
        }

        return fileName;
    }

    private static string StatusColor(string status)
        => status switch
        {
            "Posted" => Colors.Green.Darken2,
            "Approved" => Colors.Blue.Darken2,
            "Pending Approval" => Colors.Orange.Darken2,
            "Rejected" => Colors.Red.Darken2,
            "Reversed" => Colors.Red.Darken2,
            _ => Colors.Grey.Darken2
        };

    private sealed record JournalVoucherAuditTrail(
        string JournalApprovalStatus,
        DateTime? JournalApprovedAt,
        DateTime? PostedAt,
        string? SourceDocumentLabel = null,
        string? SourceDocumentReference = null,
        string? SourceApprovalStatus = null,
        DateTime? SourceApprovedAt = null,
        Guid? SourceWorkflowInstanceId = null,
        Guid? SourcePostingEventId = null)
    {
        public bool HasSourceApproval =>
            !string.IsNullOrWhiteSpace(SourceDocumentLabel) &&
            !string.IsNullOrWhiteSpace(SourceApprovalStatus) &&
            SourceApprovedAt.HasValue;

        public static JournalVoucherAuditTrail ForJournal(JournalEntry entry)
            => new(
                Normalize(entry.ApprovalStatus ?? "Not Required"),
                entry.ApprovedDate,
                entry.PostingDate);
    }
}
