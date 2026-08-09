using ErpSystem.Api.Services.HR;
using ErpSystem.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// The controlled-upload dance shared by every performance-domain attachment endpoint: resolve the
/// uploading employee from the token, push the file through the scanning + DMS-registration gate,
/// write the attachment row, and roll the stored document back if that write fails.
///
/// <para>Extracted because it is identical at four call sites (check-ins, calibration sessions,
/// unit goals, appraisals) and the parts that are easy to get wrong — the rollback, and taking the
/// uploader from the token rather than the payload — are exactly the parts a copy would drift on.
/// <c>AppraisalAttachment.UploadedById</c> is a required Employee FK, and every one of those four
/// paths used to leave it unset, so each died on a foreign-key violation the first time it ran.</para>
///
/// <para>The counterpart for reading a file back is <see cref="HrDocumentDownload"/>. Neither this
/// nor the DMS performs an entitlement check — that belongs to the calling controller, which is the
/// only thing that knows who may see the parent record.</para>
/// </summary>
internal static class HrAttachmentUpload
{
    /// <summary>
    /// Runs the upload and hands the stored document to <paramref name="persist"/>, which writes the
    /// attachment row and returns the DTO to answer with.
    /// </summary>
    /// <returns>201 with the persisted DTO, or the appropriate problem response.</returns>
    internal static async Task<IActionResult> ExecuteAsync<T>(
        ControllerBase controller,
        IHrControlledDocumentService hrDocuments,
        ICurrentUserService currentUser,
        ILogger logger,
        IFormFile? file,
        string sourceEntityType,
        Guid sourceRecordId,
        string sourceLabel,
        string documentType,
        string? description,
        Func<Guid, HrControlledDocument, Task<T>> persist,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return controller.BadRequest(new { message = "No file provided" });

        if (currentUser.EmployeeId is not Guid uploadedById || uploadedById == Guid.Empty)
            return controller.Unauthorized("User employee context not found");

        if (currentUser.TenantId is not Guid tenantId ||
            !Guid.TryParse(currentUser.UserId, out var actorUserId))
            return controller.Unauthorized("User context could not be resolved");

        HrControlledDocument document;
        try
        {
            document = await hrDocuments.UploadAsync(new HrDocumentUploadRequest
            {
                TenantId    = tenantId,
                ActorUserId = actorUserId,
                ActorName   = currentUser.UserName,
                Category    = ControlledFileUploadCategories.HrAppraisalAttachments,
                File        = file,
                Registration = new HrDocumentDmsRegistration
                {
                    SourceLabel      = sourceLabel,
                    SourceEntityType = sourceEntityType,
                    SourceRecordId   = sourceRecordId,
                    Title            = Path.GetFileName(file.FileName),
                    DocumentType     = documentType,
                    ChangeSummary    = description
                }
            }, cancellationToken);
        }
        catch (ControlledFileUploadException ex)
        {
            // The gate's own {code, message} contract — a refused file is not a server fault.
            return controller.StatusCode(ex.StatusCode, new { code = ex.Code, message = ex.Message });
        }

        try
        {
            var result = await persist(uploadedById, document);
            return controller.StatusCode(StatusCodes.Status201Created, result);
        }
        catch (Exception ex)
        {
            // Leave no scanned-and-registered document behind pointing at a row that was never written.
            await hrDocuments.RollbackAsync(document, tenantId, actorUserId, cancellationToken);

            if (ex is ArgumentException)
                return controller.NotFound(new { message = ex.Message });

            logger.LogError(ex, "Error attaching a file to {SourceEntityType} {SourceRecordId}",
                sourceEntityType, sourceRecordId);
            return controller.StatusCode(500, "An error occurred while adding the attachment");
        }
    }
}
