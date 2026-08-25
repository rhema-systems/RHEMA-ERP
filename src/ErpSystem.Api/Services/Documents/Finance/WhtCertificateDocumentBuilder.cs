using System.Globalization;
using System.Security.Cryptography;
using ErpSystem.Api.Services.Finance;
using ErpSystem.Core.DTOs.Documents;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Documents;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ErpSystem.Api.Services.Documents.Finance;

/// <summary>
/// Issues a WHT certificate PDF from the immutable statutory snapshot. It deliberately does not
/// read live supplier or tax configuration values: amendments after issue must never rewrite the
/// certificate evidence. Exact rendered bytes are retained through the controlled issue register.
/// </summary>
public sealed class WhtCertificateDocumentBuilder : IDocumentBuilder
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IFinanceControlledDocumentIssueService _issueService;

    public WhtCertificateDocumentBuilder(
        ApplicationDbContext context,
        ICurrentUserService currentUser,
        IFinanceControlledDocumentIssueService issueService)
    {
        _context = context;
        _currentUser = currentUser;
        _issueService = issueService;
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public string DocumentType => DocumentTypes.FinanceTaxWhtCertificate;
    public bool RequiresEntityId => true;
    public bool SupportsFormat(string format) => string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase);

    public async Task<RenderedDocumentDto> RenderAsync(
        DocumentRenderRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!SupportsFormat(request.Format))
            throw new NotSupportedException("Controlled WHT certificates support PDF output only.");

        var tenantId = _currentUser.GetRequiredFinanceTenantId();
        var certificate = await _context.WithholdingTaxCertificates.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == request.EntityId && item.TenantId == tenantId && !item.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException($"WHT certificate '{request.EntityId}' was not found.");

        if (certificate.Status != WhtCertificateStatus.Issued)
            throw new InvalidOperationException("Only the currently issued WHT certificate version can create a new controlled PDF issue.");
        if (!certificate.JournalEntryId.HasValue)
            throw new InvalidOperationException("The WHT certificate has no posted journal evidence.");

        var journal = await _context.JournalEntries.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == certificate.JournalEntryId.Value && item.TenantId == tenantId && !item.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("The WHT certificate journal evidence was not found in this tenant.");
        if (!string.Equals(journal.PostingStatus, "Posted", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The WHT certificate PDF is available only after the source journal is posted.");

        var tenant = await _context.Tenants.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == tenantId && !item.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException("The Finance tenant was not found.");
        var preparation = await _issueService.PrepareAsync(
            DocumentType,
            nameof(WithholdingTaxCertificate),
            certificate.Id,
            certificate.CertificateNumber,
            request.CopyType,
            CashBankPaymentSlipDocumentBuilder.Option(request, "replacementReason"),
            cancellationToken);

        var content = BuildPdf(certificate, tenant.Name, tenant.Code, journal.JournalEntryNumber, preparation);
        var fileName = ControlledTransactionDocumentPdf.SafeFileName(
            $"{certificate.CertificateNumber}-v{certificate.VersionNumber}-{CashBankPaymentSlipDocumentBuilder.CopyFileLabel(preparation)}.pdf");
        var hash = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        await _issueService.RecordRetainedIssuedAsync(
            preparation,
            fileName,
            "application/pdf",
            hash,
            content,
            preparation.IssuedAtUtc.AddYears(7),
            preparation.CopyType == ControlledDocumentCopyTypes.Original
                ? FinanceAuditEvents.WithholdingCertificatePdfIssued
                : FinanceAuditEvents.WithholdingCertificatePdfReplacementIssued,
            "Tax",
            "Finance.WHTCertificate",
            certificate.JournalEntryId,
            cancellationToken);

        return new RenderedDocumentDto
        {
            Content = content,
            ContentType = "application/pdf",
            FileName = fileName,
            DocumentType = DocumentType,
            EntityId = certificate.Id,
            Format = "pdf"
        };
    }

    private static byte[] BuildPdf(
        WithholdingTaxCertificate certificate,
        string tenantName,
        string tenantCode,
        string journalNumber,
        ControlledDocumentIssuePreparationDto issue)
    {
        var replacement = issue.CopyType == ControlledDocumentCopyTypes.Replacement;
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(34);
                page.DefaultTextStyle(style => style.FontSize(9).FontFamily(Fonts.Arial).FontColor(Colors.Grey.Darken3));
                page.Header().Column(column =>
                {
                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Column(left =>
                        {
                            left.Item().Text(tenantName).Bold().FontSize(14).FontColor(Colors.Blue.Darken2);
                            left.Item().Text($"{tenantCode} · Finance / Taxation").FontSize(8).FontColor(Colors.Grey.Darken1);
                        });
                        row.ConstantItem(260).AlignRight().Column(right =>
                        {
                            right.Item().Text("WITHHOLDING TAX CERTIFICATE").Bold().FontSize(16).FontColor(Colors.Blue.Darken2);
                            right.Item().Text(certificate.CertificateNumber).Bold();
                            right.Item().Text($"Version {certificate.VersionNumber} · {issue.CopyType} copy #{issue.CopyNumber}").FontSize(8);
                        });
                    });
                    column.Item().PaddingTop(8).LineHorizontal(1).LineColor(Colors.Blue.Darken2);
                    if (replacement)
                    {
                        column.Item().PaddingTop(6).Background(Colors.Orange.Lighten4).Padding(6)
                            .Text($"REPLACEMENT COPY — {issue.ReplacementReason}").Bold().FontColor(Colors.Orange.Darken3);
                    }
                });

                page.Content().PaddingVertical(18).Column(column =>
                {
                    column.Spacing(12);
                    column.Item().Text("This certificate confirms withholding tax deducted from the posted supplier payment shown below.");
                    column.Item().Element(box => DetailGrid(box, certificate, journalNumber));
                    column.Item().Text("Certificate values").Bold().FontSize(11).FontColor(Colors.Blue.Darken2);
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns => { columns.RelativeColumn(2); columns.RelativeColumn(); });
                        table.Header(header =>
                        {
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(6).Text("Measure").Bold();
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(6).AlignRight().Text("Amount / rate").Bold();
                        });
                        ValueRow(table, "Taxable payment base", Money(certificate.TaxableBase, certificate.CurrencyCode));
                        ValueRow(table, "Withholding rate", $"{certificate.TaxRate:0.####}%");
                        ValueRow(table, "Tax withheld", Money(certificate.WithholdingAmount, certificate.CurrencyCode), true);
                        ValueRow(table, "Net amount paid", Money(certificate.NetPaidAmount, certificate.CurrencyCode));
                    });
                    column.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(audit =>
                    {
                        audit.Item().Text("Controlled issue evidence").Bold().FontColor(Colors.Blue.Darken2);
                        audit.Item().Text($"Issued by {issue.IssuedByName} at {issue.IssuedAtUtc:dd MMM yyyy HH:mm} UTC");
                        audit.Item().Text($"Issue ID: {issue.IssueId}").FontSize(8);
                        if (replacement)
                            audit.Item().Text("This replacement does not create another tax deduction or ledger posting.").Italic().FontSize(8);
                    });
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Controlled Finance PDF · retained for 7 years · ");
                    text.CurrentPageNumber();
                    text.Span(" / ");
                    text.TotalPages();
                });
            });
        });
        return document.GeneratePdf();
    }

    private static void DetailGrid(IContainer container, WithholdingTaxCertificate certificate, string journalNumber)
    {
        container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Table(table =>
        {
            table.ColumnsDefinition(columns => { columns.RelativeColumn(); columns.RelativeColumn(); });
            Detail(table, "Certificate date", certificate.IssueDate.ToString("dd MMM yyyy"));
            Detail(table, "Payment date", certificate.PaymentDate.ToString("dd MMM yyyy"));
            Detail(table, "Supplier", certificate.SupplierName);
            Detail(table, "Supplier TIN", Text(certificate.SupplierTin));
            Detail(table, "Payment number", certificate.PaymentNumber);
            Detail(table, "Currency", certificate.CurrencyCode);
            Detail(table, "Tax", $"{Text(certificate.TaxCode)} — {Text(certificate.TaxName)}");
            Detail(table, "Tax account", $"{Text(certificate.TaxAccountNumber)} — {Text(certificate.TaxAccountName)}");
            Detail(table, "Posted journal", journalNumber);
            Detail(table, "Certificate status", certificate.Status.ToString());
        });
    }

    private static void Detail(TableDescriptor table, string label, string value)
    {
        table.Cell().PaddingVertical(4).Column(cell =>
        {
            cell.Item().Text(label.ToUpperInvariant()).FontSize(7).FontColor(Colors.Grey.Darken1);
            cell.Item().Text(value).Bold();
        });
    }

    private static void ValueRow(TableDescriptor table, string label, string value, bool emphasized = false)
    {
        var left = table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(6).Text(label);
        var right = table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(6).AlignRight().Text(value);
        if (emphasized) { left.Bold(); right.Bold().FontColor(Colors.Blue.Darken2); }
    }

    private static string Money(decimal value, string currency)
        => $"{currency} {value.ToString("N2", CultureInfo.InvariantCulture)}";

    private static string Text(string? value) => string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
}
