using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using ErpSystem.Api.Services.DocumentManagement;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Api.Tests.Services.DocumentManagement;

public sealed class DocumentTemplateWordBodyExtractorTests
{
    [Fact]
    public void ExtractBody_preserves_word_template_text_for_editor_body()
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(
            stream,
            DocumentFormat.OpenXml.WordprocessingDocumentType.Document,
            true))
        {
            var mainPart = document.AddMainDocumentPart();
            mainPart.Document = new Document(new Body(
                new Paragraph(
                    new Run(new Text("Agreement for ")),
                    new Run(new Text("{{")),
                    new Run(new Text("CustomerName")),
                    new Run(new Text("}}"))),
                new Paragraph(
                    new Run(new Text("Move in")),
                    new Run(new TabChar()),
                    new Run(new Text("{{MoveInDate}}")),
                    new Run(new Break()),
                    new Run(new Text("Billing starts {{BillingStartDate}}"))),
                new Table(
                    new TableRow(
                        new TableCell(
                            new Paragraph(new Run(new Text("Unit {{UnitNumber}}"))))))));
            mainPart.Document.Save();
        }

        var body = DocumentTemplateWordBodyExtractor.ExtractBody(stream);

        body.Should().Be(string.Join(
            Environment.NewLine + Environment.NewLine,
            "Agreement for {{CustomerName}}",
            $"Move in\t{{{{MoveInDate}}}}{Environment.NewLine}Billing starts {{{{BillingStartDate}}}}",
            "Unit {{UnitNumber}}"));
    }

    [Fact]
    public void ExtractMergeFields_discovers_distinct_word_placeholders()
    {
        var fields = DocumentTemplateWordBodyExtractor.ExtractMergeFields(
            "Amount {{ PaymentAmount }}; customer {{GranteeName}}; repeat {{paymentamount}}.");

        fields.Should().BeEquivalentTo("PaymentAmount", "GranteeName");
    }
}
