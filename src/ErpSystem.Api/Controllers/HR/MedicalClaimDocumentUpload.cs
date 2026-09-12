using ErpSystem.Api.Services.HR;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Medical;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// The controlled-upload dance for a medical expense claim's supporting documents: push the file
/// through the scanning + DMS-registration gate, write the document row, and roll the stored
/// document back if that write fails.
/// </summary>
/// <remarks>
/// <para>Shared by the HR caseload endpoint and the employee self-service endpoint, which differ
/// only in how they establish that the caller may touch the claim — that check belongs to the
/// calling controller, which is the only thing that knows who may see the parent record.</para>
///
/// <para>Deliberately <b>not</b> <see cref="HrAttachmentUpload"/>: that helper resolves an
/// uploading employee from the token and refuses when there is none, because the rows it writes
/// carry a required <c>UploadedById</c> Employee FK. <see cref="MedicalExpenseDocument"/> has no
/// such column — the uploader is an audit field taking the user id — so requiring an employee link
/// here would lock out the administrative accounts that are routinely unlinked, which is the same
/// mistake the claim-filing path had.</para>
/// </remarks>
internal static class MedicalClaimDocumentUpload
{
    internal static async Task<IActionResult> ExecuteAsync(
        ControllerBase controller,
        IHrControlledDocumentService hrDocuments,
        IMedicalExpenseClaimService claims,
        Guid claimId,
        IFormFile? file,
        MedicalDocumentType type,
        string? description,
        Guid tenantId,
        Guid userId,
        string? actorName,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return controller.BadRequest("No file was provided.");

        HrControlledDocument document;
        try
        {
            document = await hrDocuments.UploadAsync(new HrDocumentUploadRequest
            {
                TenantId = tenantId,
                ActorUserId = userId,
                ActorName = actorName,
                Category = ControlledFileUploadCategories.HrMedicalClaimDocuments,
                File = file,
                Registration = new HrDocumentDmsRegistration
                {
                    SourceLabel = "Medical expense claim document",
                    SourceEntityType = nameof(MedicalExpenseClaim),
                    SourceRecordId = claimId,
                    Title = Path.GetFileName(file.FileName),
                    DocumentType = "MedicalClaimDocument",
                    AccessProfile = "Medical restricted",
                    ChangeSummary = description
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
            var created = await claims.AddDocumentAsync(
                new CreateMedicalExpenseDocumentDto
                {
                    ClaimId = claimId,
                    FileName = document.OriginalFileName,
                    FilePath = string.Empty,
                    FileUploadRecordId = document.FileUploadRecordId,
                    DocumentRecordId = document.DocumentRecordId,
                    DocumentVersionId = document.DocumentVersionId,
                    Type = type,
                    Description = description
                },
                tenantId, userId, cancellationToken);

            return controller.Ok(created);
        }
        catch
        {
            // Leave no scanned-and-registered document behind pointing at a row that was never written.
            await hrDocuments.RollbackAsync(document, tenantId, userId, cancellationToken);
            throw;
        }
    }
}
