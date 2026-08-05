using ErpSystem.Core.Entities;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.DocumentManagement;
using ErpSystem.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Shared streaming helper for every authorized HR document download.
/// </summary>
/// <remarks>
/// <para>
/// IMPORTANT: this helper performs NO user authorization. Neither does
/// <c>ICentralDocumentRepositoryFileService.OpenAsync</c>, which only re-verifies
/// tenant, soft-delete state, and that the version still points at its own stored
/// object. The CALLING endpoint must resolve the owning domain row and prove the
/// caller is entitled to see it before calling in here.
/// </para>
/// <para>
/// Resolution order is deliberate: a central-DMS link first, then a bare controlled
/// upload (used by candidate photos, which are scanned but not worth a DMS record),
/// and only then the pre-migration path stored as a plain string. The legacy branch
/// exists solely until <c>HrLegacyFileMigrationService</c> has processed the row, and
/// it never trusts the stored string beyond a known-folder and traversal check.
/// </para>
/// </remarks>
internal static class HrDocumentDownload
{
    /// <summary>
    /// Legacy public-tree folders that predate the controlled upload boundary. A stored
    /// path whose first segment is not one of these is not something we ever wrote.
    /// </summary>
    private static readonly HashSet<string> LegacyRoots = new(
        [
            "cv-uploads",
            "candidate-documents",
            "candidate-photos",
            "leave-attachments",
            "pip-attachments",
            "staff-discipline",
            "movements",
            "offers"
        ],
        StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Applies the response headers required for personal-data downloads. HR keeps its
    /// own copy on purpose rather than reaching into another module's controller.
    /// </summary>
    public static void ApplySensitiveDownloadHeaders(ControllerBase controller)
    {
        controller.Response.Headers.CacheControl = "no-store, private";
        controller.Response.Headers.Pragma = "no-cache";
        controller.Response.Headers["X-Content-Type-Options"] = "nosniff";
    }

    /// <summary>
    /// Streams an HR document the caller has already been authorized to read.
    /// </summary>
    /// <param name="inline">
    /// True renders in the browser (profile photos); false forces a download. Inline
    /// responses use a short private cache instead of <c>no-store</c>, because an avatar
    /// re-fetched on every render is the one case where no-store is actively harmful.
    /// </param>
    public static async Task<IActionResult> ServeAsync(
        ControllerBase controller,
        ICentralDocumentRepositoryFileService centralDocuments,
        IFileStorageService storage,
        ApplicationDbContext db,
        Guid tenantId,
        Guid? documentRecordId,
        Guid? documentVersionId,
        Guid? fileUploadRecordId,
        string? legacyPath,
        string fallbackFileName,
        string? fallbackContentType,
        bool inline,
        CancellationToken cancellationToken)
    {
        if (inline)
        {
            controller.Response.Headers.CacheControl = "private, max-age=300";
            controller.Response.Headers["X-Content-Type-Options"] = "nosniff";
        }
        else
        {
            ApplySensitiveDownloadHeaders(controller);
        }

        // 1. Central DMS link — the normal path for everything except photos.
        if (documentRecordId is Guid recordId && documentVersionId is Guid versionId)
        {
            var content = await centralDocuments.OpenAsync(
                tenantId, recordId, versionId, cancellationToken);
            if (content is not null)
            {
                controller.Response.RegisterForDisposeAsync(content);
                return Stream(
                    controller, content.Content, content.ContentType,
                    content.FileName, inline);
            }
        }

        // 2. Bare controlled upload — scanned and private, just not DMS-catalogued.
        if (fileUploadRecordId is Guid uploadId)
        {
            var upload = await db.FileUploadRecords
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    item => item.Id == uploadId &&
                            item.TenantId == tenantId &&
                            !item.IsDeleted,
                    cancellationToken);

            // Never serve bytes we did not get a clean scan verdict on.
            if (upload is { VirusScanStatus: FileVirusScanStatus.Clean })
            {
                var stream = await storage.DownloadFileAsync(upload.FilePath, upload.Id);
                controller.Response.RegisterForDisposeAsync(stream);
                return Stream(
                    controller,
                    stream,
                    string.IsNullOrWhiteSpace(upload.ContentType)
                        ? "application/octet-stream"
                        : upload.ContentType,
                    upload.OriginalFileName,
                    inline);
            }
        }

        // 3. Pre-migration path. Transitional only; removed once the legacy migration
        //    utility has run for every row.
        if (IsServeableLegacyPath(legacyPath))
        {
            Stream legacyStream;
            try
            {
                legacyStream = await storage.DownloadFileAsync(legacyPath!, Guid.Empty);
            }
            catch (FileNotFoundException)
            {
                return controller.NotFound();
            }
            catch (UnauthorizedAccessException)
            {
                // ResolveRootedPath throws this for traversal attempts.
                return controller.NotFound();
            }

            controller.Response.RegisterForDisposeAsync(legacyStream);
            return Stream(
                controller,
                legacyStream,
                string.IsNullOrWhiteSpace(fallbackContentType)
                    ? "application/octet-stream"
                    : fallbackContentType,
                fallbackFileName,
                inline);
        }

        return controller.NotFound();
    }

    private static IActionResult Stream(
        ControllerBase controller,
        Stream content,
        string contentType,
        string fileName,
        bool inline)
        => inline
            ? controller.File(content, contentType)
            : controller.File(content, contentType, fileName, enableRangeProcessing: false);

    /// <summary>
    /// A stored legacy path is usable only when it is relative, traversal-free, and
    /// rooted in a folder this application actually wrote to.
    /// </summary>
    private static bool IsServeableLegacyPath(string? legacyPath)
    {
        if (string.IsNullOrWhiteSpace(legacyPath))
            return false;

        var normalized = legacyPath.Replace('\\', '/').TrimStart('/');
        if (Path.IsPathRooted(legacyPath) || normalized.Contains("..", StringComparison.Ordinal))
            return false;

        var firstSegment = normalized.Split('/', 2)[0];
        return LegacyRoots.Contains(firstSegment);
    }
}
