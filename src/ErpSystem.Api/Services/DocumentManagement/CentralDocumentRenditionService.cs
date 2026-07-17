using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Models;
using iText.Kernel.Pdf.Canvas.Parser;
using Syncfusion.DocIO;
using Syncfusion.DocIO.DLS;
using Syncfusion.DocIORenderer;
using Syncfusion.Pdf;
using Syncfusion.Presentation;
using Syncfusion.PresentationRenderer;
using Syncfusion.XlsIO;
using Syncfusion.XlsIORenderer;
using ITextPdfDocument = iText.Kernel.Pdf.PdfDocument;
using ITextPdfReader = iText.Kernel.Pdf.PdfReader;

namespace ErpSystem.Api.Services.DocumentManagement;

public interface ICentralDocumentRenditionService
{
    Task<CentralDocumentRenditionResult> CreatePdfRenditionAsync(
        CentralDocumentRenditionRequest request,
        CancellationToken cancellationToken);

    Task<CentralDocumentPdfPreviewResult> CreatePdfPreviewAsync(
        CentralDocumentPdfPreviewRequest request,
        CancellationToken cancellationToken);

    Task<CentralDocumentWordCopyResult> CreateEditableWordCopyFromPdfAsync(
        CentralDocumentWordCopyRequest request,
        CancellationToken cancellationToken);
}

public sealed record CentralDocumentRenditionRequest(
    Stream SourceStream,
    string FileName,
    string ContentType,
    Guid TenantId,
    Guid DocumentRecordId,
    string DocumentReference,
    string SourceModule);

public sealed record CentralDocumentRenditionResult(
    bool Success,
    bool IsSupported,
    string? RenditionPath,
    string? ErrorMessage);

public sealed record CentralDocumentPdfPreviewRequest(
    Stream SourceStream,
    string FileName,
    string ContentType);

public sealed record CentralDocumentPdfPreviewResult(
    bool Success,
    bool IsSupported,
    MemoryStream? PdfStream,
    string? FileName,
    string? ErrorMessage);

public sealed record CentralDocumentWordCopyRequest(
    Stream PdfStream,
    string FileName,
    string DocumentTitle,
    string DocumentReference);

public sealed record CentralDocumentWordCopyResult(
    bool Success,
    string? FileName,
    MemoryStream? WordStream,
    string? ErrorMessage);

public sealed class CentralDocumentRenditionService : ICentralDocumentRenditionService
{
    private readonly IFileStorageService _fileStorageService;
    private readonly ILogger<CentralDocumentRenditionService> _logger;

    public CentralDocumentRenditionService(
        IFileStorageService fileStorageService,
        ILogger<CentralDocumentRenditionService> logger)
    {
        _fileStorageService = fileStorageService;
        _logger = logger;
    }

    public async Task<CentralDocumentRenditionResult> CreatePdfRenditionAsync(
        CentralDocumentRenditionRequest request,
        CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(request.FileName).ToLowerInvariant();
        if (!CanConvert(extension))
        {
            return new CentralDocumentRenditionResult(false, false, null, "File type is not supported for PDF rendition.");
        }

        try
        {
            await using var pdfStream = new MemoryStream();
            ConvertToPdf(request.SourceStream, extension, pdfStream);
            pdfStream.Position = 0;
            cancellationToken.ThrowIfCancellationRequested();

            var pdfFileName = $"{Path.GetFileNameWithoutExtension(request.FileName)}.pdf";
            var uploadResult = await _fileStorageService.UploadFileAsync(new FileUploadRequest
            {
                FileStream = pdfStream,
                FileName = pdfFileName,
                ContentType = "application/pdf",
                FileSize = pdfStream.Length,
                Category = "central-dms-renditions",
                TenantId = request.TenantId.ToString(),
                Metadata =
                {
                    ["DocumentRecordId"] = request.DocumentRecordId.ToString(),
                    ["DocumentReference"] = request.DocumentReference,
                    ["SourceModule"] = request.SourceModule,
                    ["SourceFileName"] = request.FileName,
                    ["RenditionEngine"] = "Syncfusion Document Processing"
                }
            });

            if (!uploadResult.Success)
            {
                return new CentralDocumentRenditionResult(
                    false,
                    true,
                    null,
                    uploadResult.ErrorMessage ?? "Unable to store the generated PDF rendition.");
            }

            return new CentralDocumentRenditionResult(true, true, uploadResult.PublicUrl, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to create Central DMS PDF rendition for {FileName}", request.FileName);
            return new CentralDocumentRenditionResult(false, true, null, ex.Message);
        }
    }

    public Task<CentralDocumentPdfPreviewResult> CreatePdfPreviewAsync(
        CentralDocumentPdfPreviewRequest request,
        CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(request.FileName).ToLowerInvariant();
        if (!CanConvert(extension))
        {
            return Task.FromResult(new CentralDocumentPdfPreviewResult(false, false, null, null, "File type is not supported for PDF preview."));
        }

        try
        {
            var pdfStream = new MemoryStream();
            ConvertToPdf(request.SourceStream, extension, pdfStream);
            pdfStream.Position = 0;
            cancellationToken.ThrowIfCancellationRequested();

            var pdfFileName = $"{Path.GetFileNameWithoutExtension(request.FileName)}.pdf";
            return Task.FromResult(new CentralDocumentPdfPreviewResult(true, true, pdfStream, pdfFileName, null));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to create Central DMS PDF preview for {FileName}", request.FileName);
            return Task.FromResult(new CentralDocumentPdfPreviewResult(false, true, null, null, ex.Message));
        }
    }

    public Task<CentralDocumentWordCopyResult> CreateEditableWordCopyFromPdfAsync(
        CentralDocumentWordCopyRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (request.PdfStream.CanSeek)
            {
                request.PdfStream.Position = 0;
            }

            var extractedPages = new List<string>();
            using (var reader = new ITextPdfReader(request.PdfStream))
            using (var pdfDocument = new ITextPdfDocument(reader))
            {
                for (var pageNumber = 1; pageNumber <= pdfDocument.GetNumberOfPages(); pageNumber++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var text = PdfTextExtractor.GetTextFromPage(pdfDocument.GetPage(pageNumber));
                    extractedPages.Add(NormalizeExtractedPdfText(text));
                }
            }

            using var wordDocument = new WordDocument();
            var section = wordDocument.AddSection();
            section.PageSetup.Margins.All = 48;

            var title = section.AddParagraph();
            title.ApplyStyle(BuiltinStyle.Heading1);
            title.AppendText(string.IsNullOrWhiteSpace(request.DocumentTitle)
                ? Path.GetFileNameWithoutExtension(request.FileName)
                : request.DocumentTitle.Trim());

            var reference = section.AddParagraph();
            reference.ApplyStyle(BuiltinStyle.BodyText);
            reference.AppendText($"DMS Reference: {request.DocumentReference}");

            var hasText = extractedPages.Any(page => !string.IsNullOrWhiteSpace(page));
            if (!hasText)
            {
                var emptyNotice = section.AddParagraph();
                emptyNotice.AppendText("No selectable text could be extracted from the PDF. Use this editable copy to make manual changes, then upload it as a new DMS version.");
            }
            else
            {
                for (var index = 0; index < extractedPages.Count; index++)
                {
                    var pageText = extractedPages[index];
                    if (string.IsNullOrWhiteSpace(pageText))
                    {
                        continue;
                    }

                    if (index > 0)
                    {
                        section.AddParagraph().AppendBreak(BreakType.PageBreak);
                    }

                    foreach (var paragraphText in pageText.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        section.AddParagraph().AppendText(paragraphText);
                    }
                }
            }

            var output = new MemoryStream();
            wordDocument.Save(output, Syncfusion.DocIO.FormatType.Docx);
            output.Position = 0;

            var fileName = $"{Path.GetFileNameWithoutExtension(request.FileName)}-editable.docx";
            return Task.FromResult(new CentralDocumentWordCopyResult(true, fileName, output, null));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to create editable Word copy from Central DMS PDF {FileName}", request.FileName);
            return Task.FromResult(new CentralDocumentWordCopyResult(false, null, null, ex.Message));
        }
    }

    private static bool CanConvert(string extension) =>
        extension is ".doc" or ".docx" or ".rtf"
            or ".xls" or ".xlsx" or ".xlsm"
            or ".pptx";

    private static void ConvertToPdf(Stream sourceStream, string extension, Stream outputStream)
    {
        if (sourceStream.CanSeek)
        {
            sourceStream.Position = 0;
        }

        switch (extension)
        {
            case ".doc":
            case ".docx":
            case ".rtf":
                ConvertWordToPdf(sourceStream, extension, outputStream);
                break;
            case ".xls":
            case ".xlsx":
            case ".xlsm":
                ConvertExcelToPdf(sourceStream, outputStream);
                break;
            case ".pptx":
                ConvertPowerPointToPdf(sourceStream, outputStream);
                break;
        }
    }

    private static void ConvertWordToPdf(Stream sourceStream, string extension, Stream outputStream)
    {
        var formatType = extension switch
        {
            ".doc" => Syncfusion.DocIO.FormatType.Doc,
            ".rtf" => Syncfusion.DocIO.FormatType.Rtf,
            _ => Syncfusion.DocIO.FormatType.Docx
        };

        using var wordDocument = new WordDocument(sourceStream, formatType);
        using var renderer = new DocIORenderer();
        using var pdfDocument = renderer.ConvertToPDF(wordDocument);
        pdfDocument.Save(outputStream);
    }

    private static void ConvertExcelToPdf(Stream sourceStream, Stream outputStream)
    {
        using var excelEngine = new ExcelEngine();
        var application = excelEngine.Excel;
        application.DefaultVersion = ExcelVersion.Xlsx;
        var workbook = application.Workbooks.Open(sourceStream, ExcelOpenType.Automatic);
        var renderer = new XlsIORenderer();
        using var pdfDocument = renderer.ConvertToPDF(workbook);
        pdfDocument.Save(outputStream);
        workbook.Close();
    }

    private static void ConvertPowerPointToPdf(Stream sourceStream, Stream outputStream)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.pptx");
        try
        {
            using (var tempFile = File.Create(tempPath))
            {
                sourceStream.CopyTo(tempFile);
            }

            using var presentation = Presentation.Open(tempPath);
            using var pdfDocument = PresentationToPdfConverter.Convert(presentation);
            pdfDocument.Save(outputStream);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static string NormalizeExtractedPdfText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var lines = value
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n', StringSplitOptions.TrimEntries)
            .Where(line => !string.IsNullOrWhiteSpace(line));

        return string.Join('\n', lines);
    }
}
