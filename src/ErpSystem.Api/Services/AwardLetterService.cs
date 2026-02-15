using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ErpSystem.Api.Services;

/// <summary>
/// Service for generating PDF award letters for tender awards
/// </summary>
public class AwardLetterService : IAwardLetterService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AwardLetterService> _logger;
    private readonly IConfiguration _configuration;

    public AwardLetterService(
        ApplicationDbContext context, 
        ILogger<AwardLetterService> logger,
        IConfiguration configuration)
    {
        _context = context;
        _logger = logger;
        _configuration = configuration;
        QuestPDF.Settings.License = LicenseType.Community;
    }

    /// <summary>
    /// Generates a PDF award letter for the specified award
    /// </summary>
    /// <returns>A tuple containing the PDF bytes and the filename</returns>
    public async Task<(byte[] PdfBytes, string FileName)> GenerateAwardLetterAsync(Guid awardId)
    {
        var award = await _context.TenderAwards
            .Include(a => a.Tender)
            .Include(a => a.TenderBid)
            .Include(a => a.BusinessPartner)
            .Include(a => a.AwardedBy)
            .Include(a => a.Negotiation)
            .FirstOrDefaultAsync(a => a.Id == awardId);

        if (award == null)
            throw new ArgumentException($"Award {awardId} not found");

        var organizationName = _configuration["Organization:Name"] ?? "ERP System";
        var organizationAddress = _configuration["Organization:Address"] ?? "";

        _logger.LogInformation("Generating award letter PDF for award {AwardId}, IsNegotiated: {IsNegotiated}, Amount: {Amount}",
            awardId, award.IsNegotiated, award.AwardedAmount);

        var pdfBytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(11).FontColor(Colors.Black));

                page.Header().Element(h => ComposeHeader(h, organizationName, organizationAddress, award));
                page.Content().Element(c => ComposeContent(c, award, organizationName));
                page.Footer().Element(f => ComposeFooter(f));
            });
        }).GeneratePdf();

        // Generate a descriptive filename
        var tenderNumber = award.Tender?.TenderNumber?.Replace("/", "-").Replace("\\", "-") ?? "Unknown";
        var fileName = $"Award_Letter_{tenderNumber}_{award.AwardDate:yyyyMMdd}.pdf";

        return (pdfBytes, fileName);
    }

    private void ComposeHeader(IContainer container, string organizationName, string organizationAddress, ErpSystem.Core.Entities.Procurement.TenderAward award)
    {
        container.Column(column =>
        {
            // Organization Header
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text(organizationName).Bold().FontSize(18).FontColor(Colors.Blue.Darken2);
                    if (!string.IsNullOrEmpty(organizationAddress))
                    {
                        col.Item().Text(organizationAddress).FontSize(9).FontColor(Colors.Grey.Darken1);
                    }
                });
                row.ConstantItem(150).AlignRight().Column(col =>
                {
                    col.Item().Text("AWARD LETTER").Bold().FontSize(14).FontColor(Colors.Green.Darken2);
                    col.Item().Text($"Date: {award.AwardDate:MMMM dd, yyyy}").FontSize(9);
                    col.Item().Text($"Ref: AWD-{award.Tender?.TenderNumber ?? "N/A"}").FontSize(9);
                });
            });

            column.Item().PaddingTop(15).LineHorizontal(2).LineColor(Colors.Blue.Darken2);
        });
    }

    private void ComposeContent(IContainer container, ErpSystem.Core.Entities.Procurement.TenderAward award, string organizationName)
    {
        var tender = award.Tender;
        var businessPartner = award.BusinessPartner;
        var bid = award.TenderBid;

        container.PaddingVertical(20).Column(column =>
        {
            // Recipient Address
            column.Item().Column(col =>
            {
                col.Item().Text("To:").Bold();
                col.Item().Text(businessPartner?.PartnerName ?? "N/A").Bold().FontSize(12);
                if (!string.IsNullOrEmpty(businessPartner?.PhysicalAddress))
                {
                    col.Item().Text(businessPartner.PhysicalAddress);
                }
                if (!string.IsNullOrEmpty(businessPartner?.PrimaryEmail))
                {
                    col.Item().Text($"Email: {businessPartner.PrimaryEmail}");
                }
            });

            column.Item().PaddingTop(20);

            // Subject Line
            column.Item().Text(text =>
            {
                text.Span("Subject: ").Bold();
                text.Span($"Award of Tender {tender?.TenderNumber ?? "N/A"} - {tender?.Title ?? "N/A"}");
            });

            column.Item().PaddingTop(15);

            // Salutation
            column.Item().Text($"Dear {businessPartner?.PartnerName ?? "Sir/Madam"},");

            column.Item().PaddingTop(10);

            // Opening Paragraph
            column.Item().Text(text =>
            {
                text.Span("We are pleased to inform you that your bid ");
                text.Span($"({bid?.BidNumber ?? "N/A"})").Bold();
                text.Span(" for the above-referenced tender has been ");
                text.Span("SUCCESSFUL").Bold().FontColor(Colors.Green.Darken2);
                text.Span(".");
            });

            column.Item().PaddingTop(10);

            // Award Details Box
            column.Item().Background(Colors.Green.Lighten5).Border(1).BorderColor(Colors.Green.Darken1).Padding(15).Column(detailsCol =>
            {
                detailsCol.Item().Text("AWARD DETAILS").Bold().FontSize(12).FontColor(Colors.Green.Darken2);
                detailsCol.Item().PaddingTop(10);

                detailsCol.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(2);
                    });

                    // Tender Number
                    table.Cell().Padding(3).Text("Tender Number:").Bold();
                    table.Cell().Padding(3).Text(tender?.TenderNumber ?? "N/A");

                    // Tender Title
                    table.Cell().Padding(3).Text("Tender Title:").Bold();
                    table.Cell().Padding(3).Text(tender?.Title ?? "N/A");

                    // Your Bid Number
                    table.Cell().Padding(3).Text("Your Bid Number:").Bold();
                    table.Cell().Padding(3).Text(bid?.BidNumber ?? "N/A");

                    // Award Date
                    table.Cell().Padding(3).Text("Award Date:").Bold();
                    table.Cell().Padding(3).Text(award.AwardDate.ToString("MMMM dd, yyyy"));

                    // Award Amount - Show negotiated amount prominently
                    table.Cell().Padding(3).Text("Award Amount:").Bold();
                    table.Cell().Padding(3).Text(text =>
                    {
                        text.Span($"{award.Currency ?? "USD"} {award.AwardedAmount:N2}").Bold().FontSize(13).FontColor(Colors.Green.Darken2);
                        if (award.IsNegotiated)
                        {
                            text.Span(" (Negotiated)").FontSize(9).FontColor(Colors.Orange.Darken2);
                        }
                    });

                    // Show original amount if negotiated
                    if (award.IsNegotiated && award.OriginalBidAmount != award.AwardedAmount)
                    {
                        table.Cell().Padding(3).Text("Original Bid Amount:").Bold();
                        table.Cell().Padding(3).Text(text =>
                        {
                            text.Span($"{award.Currency ?? "USD"} {award.OriginalBidAmount:N2}").FontColor(Colors.Grey.Darken1);
                        });

                        var savings = award.OriginalBidAmount - award.AwardedAmount;
                        if (savings > 0)
                        {
                            table.Cell().Padding(3).Text("Negotiation Savings:").Bold();
                            table.Cell().Padding(3).Text(text =>
                            {
                                text.Span($"{award.Currency ?? "USD"} {savings:N2}").FontColor(Colors.Green.Darken2);
                            });
                        }
                    }
                });
            });

            column.Item().PaddingTop(15);

            // Next Steps
            column.Item().Text("Next Steps:").Bold();
            column.Item().PaddingTop(5);
            column.Item().PaddingLeft(15).Column(stepsCol =>
            {
                stepsCol.Item().Text("1. Our team will contact you shortly to discuss contract finalization.");
                stepsCol.Item().Text("2. Please prepare all necessary documentation for contract signing.");
                stepsCol.Item().Text("3. A performance bond may be required as per tender terms.");
                stepsCol.Item().Text("4. Contract execution is expected within 14 business days.");
            });

            column.Item().PaddingTop(15);

            // Closing
            column.Item().Text("We look forward to a successful partnership and thank you for your participation in our procurement process.");

            column.Item().PaddingTop(20);

            // Signature Block
            column.Item().Text("Yours sincerely,");
            column.Item().PaddingTop(30);
            column.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
            column.Item().PaddingTop(5);
            column.Item().Text(award.AwardedBy?.FullName ?? "Procurement Team").Bold();
            column.Item().Text(organizationName);
            column.Item().Text($"Date: {DateTime.UtcNow:MMMM dd, yyyy}");
        });
    }

    private void ComposeFooter(IContainer container)
    {
        container.Column(column =>
        {
            column.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
            column.Item().PaddingTop(5).Row(row =>
            {
                row.RelativeItem().Text(text =>
                {
                    text.Span("This is an official award notification document. ").FontSize(8).FontColor(Colors.Grey.Darken1);
                    text.Span("Generated on ").FontSize(8).FontColor(Colors.Grey.Darken1);
                    text.Span(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC")).FontSize(8).FontColor(Colors.Grey.Darken1);
                });
                row.ConstantItem(100).AlignRight().Text(text =>
                {
                    text.DefaultTextStyle(x => x.FontSize(8).FontColor(Colors.Grey.Darken1));
                    text.Span("Page ");
                    text.CurrentPageNumber();
                    text.Span(" of ");
                    text.TotalPages();
                });
            });
        });
    }
}
