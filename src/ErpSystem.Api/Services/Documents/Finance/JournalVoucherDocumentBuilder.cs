using ErpSystem.Core.DTOs.Documents;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
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

        var pdfBytes = BuildPdf(entry, tenant, request.CopyType, _currentUser.UserName);
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

    private static byte[] BuildPdf(JournalEntry entry, Tenant? tenant, string copyType, string? generatedBy)
    {
        var currency = ResolveCurrency(entry, tenant);
        var lines = entry.Transactions
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
                page.Content().Element(content => ComposeContent(content, entry, lines, currency));
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

    private static void ComposeContent(IContainer container, JournalEntry entry, IReadOnlyList<AccountTransaction> lines, string currency)
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
            column.Item().PaddingTop(14).Element(content => ComposeAuditTrail(content, entry));
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
                row.RelativeItem().Element(cell => MetaCell(cell, "Currency", currency));
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
                columns.RelativeColumn(2.8f);
                columns.RelativeColumn(1.2f);
                columns.RelativeColumn(1.2f);
            });

            table.Header(header =>
            {
                header.Cell().Element(HeaderCell).Text("#");
                header.Cell().Element(HeaderCell).Text("Account");
                header.Cell().Element(HeaderCell).Text("Description");
                header.Cell().Element(HeaderCell).AlignRight().Text("Debit");
                header.Cell().Element(HeaderCell).AlignRight().Text("Credit");
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
                table.Cell().Element(BodyCell).AlignRight().Text(line.DebitAmount > 0 ? Money(line.DebitAmount, currency) : "-");
                table.Cell().Element(BodyCell).AlignRight().Text(line.CreditAmount > 0 ? Money(line.CreditAmount, currency) : "-");

                index++;
            }

            if (lines.Count == 0)
            {
                table.Cell().ColumnSpan(5).Element(BodyCell).AlignCenter().Text("No transaction lines found.");
            }
        });
    }

    private static void ComposeTotals(IContainer container, JournalEntry entry, string currency)
    {
        container.AlignRight().Width(280).Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Text("Total Debit").SemiBold();
                row.ConstantItem(120).AlignRight().Text(Money(entry.TotalDebitAmount, currency)).SemiBold();
            });
            column.Item().PaddingTop(4).Row(row =>
            {
                row.RelativeItem().Text("Total Credit").SemiBold();
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

    private static void ComposeAuditTrail(IContainer container, JournalEntry entry)
    {
        container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(column =>
        {
            column.Item().Text("Workflow and Audit").Bold().FontSize(11).FontColor(Colors.Blue.Darken3);
            column.Item().PaddingTop(6).Row(row =>
            {
                row.RelativeItem().Element(cell => MetaCell(cell, "Created", entry.CreatedAt.ToString("dd MMM yyyy HH:mm")));
                row.RelativeItem().Element(cell => MetaCell(cell, "Approval", Normalize(entry.ApprovalStatus ?? "Not Required")));
                row.RelativeItem().Element(cell => MetaCell(cell, "Approved Date", entry.ApprovedDate?.ToString("dd MMM yyyy HH:mm") ?? "-"));
                row.RelativeItem().Element(cell => MetaCell(cell, "Posting Date", entry.PostingDate?.ToString("dd MMM yyyy HH:mm") ?? "-"));
            });

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

    private static string ResolveCurrency(JournalEntry entry, Tenant? tenant)
    {
        if (!string.IsNullOrWhiteSpace(entry.PrimaryCurrency))
        {
            return entry.PrimaryCurrency;
        }

        var transactionCurrency = entry.Transactions
            .Select(t => t.TransactionCurrency)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        return transactionCurrency ?? tenant?.BaseCurrency ?? "GHS";
    }

    private static string Money(decimal amount, string currency)
        => $"{currency} {amount:N2}";

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
}
