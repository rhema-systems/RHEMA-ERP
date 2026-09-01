using ErpSystem.Core.DTOs.Documents;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Documents;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Interfaces.QuantitySurvey;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Colors = QuestPDF.Helpers.Colors;

namespace ErpSystem.Api.Services.Documents.QuantitySurvey;

public sealed class QuantitySurveyPaymentCertificateDocumentBuilder(
    ApplicationDbContext db,
    ICurrentUserService currentUser,
    IProjectService projectService) : IDocumentBuilder
{
    public string DocumentType => DocumentTypes.QuantitySurveyPaymentCertificate;
    public bool RequiresEntityId => true;
    public bool SupportsFormat(string format) => string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase);

    public async Task<RenderedDocumentDto> RenderAsync(
        DocumentRenderRequestDto request, CancellationToken cancellationToken = default)
    {
        var tenantId = currentUser.TenantId is { } tenant && tenant != Guid.Empty
            ? tenant : throw new UnauthorizedAccessException("A valid tenant context is required.");
        var certificate = await db.Set<ProjectPaymentCertificate>().AsNoTracking()
            .Include(value => value.Project)
            .Include(value => value.Contract)
            .Include(value => value.ProjectInterimValuation)
            .Include(value => value.VendorInvoice)
            .FirstOrDefaultAsync(value => value.TenantId == tenantId && value.Id == request.EntityId &&
                !value.IsDeleted && value.QuantitySurveyValuationWorksheetId != null, cancellationToken)
            ?? throw new QuantitySurveyPaymentCertificateNotFoundException("The governed payment certificate was not found.");
        if (!await projectService.HasProjectAccessAsync(certificate.ProjectId))
            throw new UnauthorizedAccessException("You are not permitted to export this project's payment certificate.");
        if (certificate.Status is not (ProjectPaymentCertificateStatuses.Approved or ProjectPaymentCertificateStatuses.Paid) ||
            certificate.ApprovalStatus != "Approved")
            throw new QuantitySurveyPaymentCertificateConflictException("Approve the payment certificate before generating its controlled PDF.");
        var tenantName = await db.Tenants.AsNoTracking().Where(value => value.Id == tenantId && !value.IsDeleted)
            .Select(value => value.Name).FirstOrDefaultAsync(cancellationToken) ?? "Tenant";
        var generatedAt = certificate.GeneratedAt ?? certificate.ApprovedAt ?? certificate.IssueDate;
        var bytes = BuildPdf(certificate, tenantName, generatedAt);
        return new RenderedDocumentDto
        {
            Content = bytes,
            ContentType = "application/pdf",
            FileName = SafeFileName(certificate.CertificateNumber ?? certificate.Id.ToString("N")) + ".pdf",
            DocumentType = DocumentType,
            EntityId = certificate.Id,
            Format = "pdf"
        };
    }

    private static byte[] BuildPdf(ProjectPaymentCertificate value, string tenantName, DateTime generatedAt)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        return Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(28);
            page.DefaultTextStyle(style => style.FontFamily(Fonts.Arial).FontSize(9));
            page.Header().Column(column =>
            {
                column.Item().Text(tenantName).Bold().FontSize(14).FontColor(Colors.Blue.Darken3);
                column.Item().Text("INTERIM PAYMENT CERTIFICATE").Bold().FontSize(12);
                column.Item().Text(value.CertificateNumber ?? value.Id.ToString()).SemiBold();
                column.Item().PaddingTop(5).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
            });
            page.Content().PaddingVertical(12).Column(column =>
            {
                column.Spacing(10);
                column.Item().Element(container => Section(container, "Controlled lineage", section =>
                {
                    Meta(section, "Project", $"{value.Project?.ProjectCode} · {value.Project?.Title}");
                    Meta(section, "Works contract", $"{value.Contract?.ContractNumber} · {value.Contract?.ContractTitle}");
                    Meta(section, "Valuation", $"{value.ProjectInterimValuation?.ValuationNumber} · {value.ProjectInterimValuation?.Title}");
                    Meta(section, "Issue / due date", $"{value.IssueDate:dd MMM yyyy} / {value.PaymentDueDate:dd MMM yyyy}");
                    Meta(section, "QS policy", $"Profile {value.ConfigurationProfileId} · Decision {value.ValuationDecisionId}");
                    Meta(section, "Template", $"{value.CertificateTemplateId} · version {value.CertificateTemplateVersionSnapshot}");
                }));
                column.Item().Element(container => AmountTable(container, value));
                column.Item().Element(container => Section(container, "Approval and Finance AP", section =>
                {
                    Meta(section, "Prepared / submitted", $"{value.PreparedById} / {value.SubmittedById}");
                    Meta(section, "Approved", $"{value.ApprovedById} · {value.ApprovedAt:u}");
                    Meta(section, "Workflow", value.WorkflowInstanceId?.ToString() ?? "-");
                    Meta(section, "AP handoff", $"{value.ApHandoffStatus} · {value.VendorInvoice?.InvoiceNumber ?? "Not created"}");
                    Meta(section, "Payment status", $"{value.PaymentStatusSnapshot} · {value.PaymentStatusUpdatedAt:u}");
                    Meta(section, "Audit control", "Amounts and policy references are frozen from the approved QS valuation and shared Finance configuration.");
                }));
                if (!string.IsNullOrWhiteSpace(value.Notes))
                    column.Item().Element(container => Section(container, "Certificate notes", section => section.Item().Text(value.Notes)));
            });
            page.Footer().AlignCenter().Text(text =>
            {
                text.Span($"Controlled output generated {generatedAt:u} · Page ");
                text.CurrentPageNumber(); text.Span(" of "); text.TotalPages();
            });
        })).GeneratePdf();
    }

    private static void AmountTable(IContainer container, ProjectPaymentCertificate value) =>
        container.Column(column =>
        {
            column.Item().Text("Certificate calculation").Bold().FontSize(10).FontColor(Colors.Blue.Darken3);
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns => { columns.RelativeColumn(3); columns.RelativeColumn(); });
                Row(table, "Certified to date", value.CertifiedToDateAmount, value.Currency);
                Row(table, "Previously certified", value.PreviouslyCertifiedAmount, value.Currency);
                Row(table, "Approved materials on site", value.MaterialOnSiteAmount, value.Currency);
                Row(table, "Approved off-site materials", value.MaterialOffSiteAmount, value.Currency);
                Row(table, "Current gross certificate", value.GrossCertifiedAmount, value.Currency, true);
                Row(table, "Retention held", -value.RetentionHeldAmount, value.Currency);
                Row(table, "Retention released", value.RetentionReleasedAmount, value.Currency);
                Row(table, "Advance recovery", -value.AdvanceRecoveryAmount, value.Currency);
                Row(table, "TDC-supplied material deduction", -value.MaterialDeductionAmount, value.Currency);
                Row(table, "Other deductions", -value.OtherDeductionsAmount, value.Currency);
                Row(table, $"Tax ({value.TaxHandling})", value.TaxAmount, value.Currency);
                Row(table, "Net certified payable", value.NetCertifiedAmount, value.Currency, true);
            });
        });

    private static void Row(TableDescriptor table, string label, decimal amount, string currency, bool bold = false)
    {
        var left = table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(label);
        var right = table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4)
            .AlignRight().Text($"{currency} {amount:N2}");
        if (bold) { left.Bold(); right.Bold(); }
    }

    private static void Section(IContainer container, string title, Action<ColumnDescriptor> content) =>
        container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(column =>
        {
            column.Item().Text(title).Bold().FontSize(10).FontColor(Colors.Blue.Darken3);
            column.Item().PaddingTop(4).Column(content);
        });
    private static void Meta(ColumnDescriptor column, string label, string value) =>
        column.Item().PaddingBottom(2).Text(text => { text.Span(label + ": ").SemiBold(); text.Span(value); });
    private static string SafeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var result = new string(value.Select(character => invalid.Contains(character) ? '_' : character).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(result) ? "payment-certificate" : result;
    }
}
