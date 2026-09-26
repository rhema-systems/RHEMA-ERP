using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using ErpSystem.Api.Services.DocumentManagement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SkiaSharp;
using Syncfusion.DocIO;
using Syncfusion.DocIO.DLS;
using Xunit;

namespace ErpSystem.Api.Tests.Services.DocumentManagement;

public sealed class CentralDocumentWordConversionTests
{
    [Theory]
    [InlineData("agreement")]
    [InlineData("invoice")]
    public async Task PdfDownloadAsWordPreservesVisiblePagesForAnyDocument(string documentName)
    {
        using var original = new WordDocument();
        var section = original.AddSection();
        section.AddParagraph().AppendText("DOCUMENT TITLE");
        section.AddParagraph().AppendText("Purchase price GHS 125,000");
        using var wordSource = new MemoryStream();
        original.Save(wordSource, FormatType.Docx);
        wordSource.Position = 0;

        var service = new CentralDocumentRenditionService(
            Mock.Of<IFileStorageService>(),
            NullLogger<CentralDocumentRenditionService>.Instance);
        var pdf = await service.CreatePdfPreviewAsync(
            new CentralDocumentPdfPreviewRequest(wordSource, $"{documentName}.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document"),
            CancellationToken.None);
        Assert.True(pdf.Success, pdf.ErrorMessage);
        Assert.NotNull(pdf.PdfStream);

        using var pdfStream = pdf.PdfStream;
        var converted = await service.CreateEditableWordCopyFromPdfAsync(
            new CentralDocumentWordCopyRequest(pdfStream, $"{documentName}.pdf", "Document", "DMS-1"),
            CancellationToken.None);
        Assert.True(converted.Success, converted.ErrorMessage);
        Assert.NotNull(converted.WordStream);

        using var resultStream = converted.WordStream!;
        using var docx = WordprocessingDocument.Open(resultStream, false);
        Assert.Contains($"{documentName}-word-copy.docx", converted.FileName);
        var mainPart = Assert.IsType<MainDocumentPart>(docx.MainDocumentPart);
        Assert.NotEmpty(mainPart.ImageParts);
        var document = Assert.IsType<Document>(mainPart.Document);
        var body = Assert.IsType<Body>(document.Body);
        Assert.NotEmpty(body.Descendants<Drawing>());
        using var imageStream = mainPart.ImageParts.First().GetStream();
        using var bitmap = SKBitmap.Decode(imageStream);
        Assert.NotNull(bitmap);
        Assert.True(bitmap.Width > 100 && bitmap.Height > 100);
        Assert.Contains(Enumerable.Range(0, bitmap.Height / 2), y =>
            Enumerable.Range(0, bitmap.Width / 2).Any(x =>
            {
                var pixel = bitmap.GetPixel(x * 2, y * 2);
                return pixel.Red < 200 && pixel.Green < 200 && pixel.Blue < 200;
            }));
    }
}
