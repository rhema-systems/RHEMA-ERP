using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Models;
using Syncfusion.DocIO;
using Syncfusion.DocIO.DLS;
using Syncfusion.DocIORenderer;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Parsing;
using Syncfusion.PdfToImageConverter;
using Syncfusion.Presentation;
using Syncfusion.PresentationRenderer;
using Syncfusion.XlsIO;
using Syncfusion.XlsIORenderer;

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

            return new CentralDocumentRenditionResult(true, true, uploadResult.FilePath, null);
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

            using var pdfDocument = new PdfLoadedDocument(request.PdfStream);
            var imageConverter = new PdfToImageConverter();
            request.PdfStream.Position = 0;
            imageConverter.Load(request.PdfStream);

            using var wordDocument = new WordDocument();
            for (var pageIndex = 0; pageIndex < imageConverter.PageCount; pageIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var pageSize = pdfDocument.Pages[pageIndex].Size;
                var section = wordDocument.AddSection();
                section.PageSetup.PageSize = pageSize;
                section.PageSetup.Margins.All = 12;

                using var imageStream = imageConverter.Convert(pageIndex, false, false);
                var picture = section.AddParagraph().AppendPicture(imageStream);
                var availableWidth = pageSize.Width - 24;
                var availableHeight = pageSize.Height - 24;
                var scale = Math.Min(availableWidth / picture.Width, availableHeight / picture.Height);
                picture.Width *= scale;
                picture.Height *= scale;
            }

            var output = new MemoryStream();
            wordDocument.Save(output, Syncfusion.DocIO.FormatType.Docx);
            output.Position = 0;

            var fileName = $"{Path.GetFileNameWithoutExtension(request.FileName)}-word-copy.docx";
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
        renderer.Settings.AutoDetectComplexScript = true;
        renderer.Settings.EmbedFonts = true;
        renderer.Settings.EmbedCompleteFonts = true;
        renderer.Settings.UpdateDocumentFields = true;
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

}
