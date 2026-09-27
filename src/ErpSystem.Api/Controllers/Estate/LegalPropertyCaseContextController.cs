using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procedures;
using ErpSystem.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Estate;

[ApiController]
[Route("api/legal/property-cases/{legalCaseId:guid}/context")]
[Authorize]
public sealed class LegalPropertyCaseContextController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IProcedureCaseService _caseService;
    private readonly IFileStorageService _fileStorage;

    public LegalPropertyCaseContextController(
        ApplicationDbContext db,
        ICurrentUserService currentUser,
        IProcedureCaseService caseService,
        IFileStorageService fileStorage)
    {
        _db = db;
        _currentUser = currentUser;
        _caseService = caseService;
        _fileStorage = fileStorage;
    }

    [HttpGet]
    public async Task<IActionResult> GetContext(Guid legalCaseId)
    {
        var link = await GetAuthorizedSourceCaseAsync(legalCaseId);
        if (link == null) return NotFound(new { message = "Linked property case was not found." });
        var (tenantId, sourceCaseId) = link.Value;
        var source = await _db.ProcedureCases.AsNoTracking()
            .Include(item => item.Fields.Where(field => !field.IsDeleted))
            .Include(item => item.Documents.Where(document => !document.IsDeleted))
            .Include(item => item.Activities.Where(activity => !activity.IsDeleted))
            .AsSplitQuery()
            .FirstOrDefaultAsync(item => item.Id == sourceCaseId && item.TenantId == tenantId && !item.IsDeleted);
        if (source == null) return NotFound(new { message = "Linked property case was not found." });

        string? Field(string key) => source.Fields.FirstOrDefault(item => item.Key == key)?.Value;
        var opportunityId = Guid.TryParse(Field("salesOpportunityId"), out var salesId) ? salesId : Guid.Empty;
        var ticketId = Guid.TryParse(Field("ehcTicketId"), out var enquiryId) ? enquiryId : Guid.Empty;
        var listingId = Guid.TryParse(Field("listingId"), out var assetListingId) ? assetListingId : Guid.Empty;
        var isDemarcation = string.Equals(Field("listingRecordType"), "EstateLandDemarcation", StringComparison.OrdinalIgnoreCase);
        var assetId = isDemarcation
            ? await _db.EstateLandDemarcations.AsNoTracking()
                .Where(item => item.Id == listingId && item.TenantId == tenantId && !item.IsDeleted)
                .Select(item => item.EstateManagedAssetId).FirstOrDefaultAsync()
            : listingId;

        var asset = assetId == Guid.Empty ? null : await _db.EstateManagedAssets.AsNoTracking()
            .Where(item => item.Id == assetId && item.TenantId == tenantId && !item.IsDeleted)
            .Select(item => new
            {
                item.Id, item.AssetCode, item.Name, item.AssetType, item.Status, item.Location,
                item.Region, item.District, item.Town, item.AreaValue, item.AreaUnit,
                item.SurveyPlanNumber, item.PropertyFileReference, item.ValuationAmount, item.Currency
            }).FirstOrDefaultAsync();
        var assetDocuments = assetId == Guid.Empty ? [] : await _db.EstateManagedAssetDocuments.AsNoTracking()
            .Where(item => item.EstateManagedAssetId == assetId && item.TenantId == tenantId && !item.IsDeleted)
            .OrderBy(item => item.DocumentType).ThenBy(item => item.DocumentName)
            .Select(item => new { item.Id, item.DocumentName, item.DocumentType, item.FileName,
                item.CentralDocumentRecordId, item.CentralDocumentReference })
            .ToListAsync();

        var opportunity = opportunityId == Guid.Empty ? null : await _db.Opportunities.AsNoTracking()
            .Where(item => item.Id == opportunityId && item.TenantId == tenantId && !item.IsDeleted)
            .Select(item => new
            {
                item.Id, item.Name, item.Description, item.Stage, item.Amount, item.Currency,
                item.ExpectedCloseDate, item.ActualCloseDate, item.LeadSource, item.OpportunityType, item.Notes
            }).FirstOrDefaultAsync();
        var salesActivities = opportunityId == Guid.Empty ? [] : await _db.CrmActivities.AsNoTracking()
            .Where(item => item.OpportunityId == opportunityId && item.TenantId == tenantId && !item.IsDeleted)
            .OrderByDescending(item => item.ActivityDate)
            .Select(item => new { item.Id, item.Subject, item.ActivityType, item.Description, item.ActivityDate, item.ActivityStatus, item.Outcome, item.Notes })
            .ToListAsync();

        var ticket = ticketId == Guid.Empty ? null : await _db.EhcTickets.AsNoTracking()
            .Where(item => item.Id == ticketId && item.TenantId == tenantId && !item.IsDeleted)
            .Select(item => new
            {
                item.Id, item.TicketNumber, item.Subject, item.Description, item.Status, item.Source,
                item.Priority, item.CreatedAt, item.ResolutionSummary
            }).FirstOrDefaultAsync();
        var messages = ticketId == Guid.Empty ? [] : await _db.EhcTicketMessages.AsNoTracking()
            .Where(item => item.TicketId == ticketId && item.TenantId == tenantId && !item.IsDeleted)
            .OrderBy(item => item.CreatedAt)
            .Select(item => new { item.Id, item.Body, item.IsInternal, item.CreatedAt })
            .ToListAsync();
        var ticketHistory = ticketId == Guid.Empty ? [] : await _db.EhcTicketStatusHistories.AsNoTracking()
            .Where(item => item.TicketId == ticketId && item.TenantId == tenantId && !item.IsDeleted)
            .OrderBy(item => item.CreatedAt)
            .Select(item => new { item.FromStatus, item.ToStatus, item.Notes, item.CreatedAt })
            .ToListAsync();
        var ticketAttachments = ticketId == Guid.Empty ? [] : await _db.EhcTicketAttachments.AsNoTracking()
            .Where(item => item.TicketId == ticketId && item.TenantId == tenantId && !item.IsDeleted)
            .Select(item => new { item.Id, item.FileName, item.IsInternal })
            .ToListAsync();

        return Ok(new
        {
            success = true,
            data = new
            {
                sourceCase = new
                {
                    source.Id, source.Title, source.ReferenceNumber, source.Description, source.ApplicantName,
                    source.Status, source.CurrentStageName,
                    fields = source.Fields.Where(item => !string.IsNullOrWhiteSpace(item.Value))
                        .OrderBy(item => item.Label).Select(item => new { item.Label, item.Value }),
                    documents = source.Documents.Where(item => !string.IsNullOrWhiteSpace(item.FileName))
                        .OrderBy(item => item.Name).Select(item => new
                        {
                            item.Id, item.Name, item.FileName, item.Notes, item.UploadedAt,
                            DmsUrl = item.FileUrl?.StartsWith("/document-management", StringComparison.OrdinalIgnoreCase) == true
                                ? item.FileUrl : null
                        }),
                    activities = source.Activities.OrderByDescending(item => item.PerformedAt)
                        .Select(item => new { item.Action, item.StageName, item.Details, item.PerformedAt })
                },
                asset, assetDocuments, opportunity, salesActivities,
                inquiry = new { ticket, messages, ticketHistory, ticketAttachments }
            }
        });
    }

    [HttpGet("source-documents/{documentId:guid}")]
    public async Task<IActionResult> DownloadSourceDocument(Guid legalCaseId, Guid documentId)
    {
        var link = await GetAuthorizedSourceCaseAsync(legalCaseId);
        if (link == null) return NotFound();
        var (tenantId, sourceCaseId) = link.Value;
        var document = await _db.ProcedureCaseDocuments.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == documentId && item.ProcedureCaseId == sourceCaseId
                && item.TenantId == tenantId && !item.IsDeleted);
        if (document == null || string.IsNullOrWhiteSpace(document.FileUrl)) return NotFound();
        if (Uri.TryCreate(document.FileUrl, UriKind.Absolute, out _)) return BadRequest();
        var stream = await _fileStorage.DownloadFileAsync(document.FileUrl, document.Id);
        var fileName = string.IsNullOrWhiteSpace(document.FileName) ? document.Name : document.FileName;
        var contentType = new FileExtensionContentTypeProvider().TryGetContentType(fileName, out var resolved)
            ? resolved : "application/octet-stream";
        return File(stream, contentType, fileName);
    }

    [HttpGet("inquiry-attachments/{attachmentId:guid}")]
    public async Task<IActionResult> DownloadInquiryAttachment(Guid legalCaseId, Guid attachmentId)
    {
        var link = await GetAuthorizedSourceCaseAsync(legalCaseId);
        if (link == null) return NotFound();
        var (tenantId, sourceCaseId) = link.Value;
        var ticketValue = await _db.ProcedureCaseFields.AsNoTracking()
            .Where(item => item.ProcedureCaseId == sourceCaseId && item.TenantId == tenantId
                && item.Key == "ehcTicketId" && !item.IsDeleted)
            .Select(item => item.Value).FirstOrDefaultAsync();
        if (!Guid.TryParse(ticketValue, out var ticketId)) return NotFound();
        var attachment = await _db.EhcTicketAttachments.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == attachmentId && item.TicketId == ticketId
                && item.TenantId == tenantId && !item.IsDeleted);
        if (attachment == null) return NotFound();
        var stream = await _fileStorage.DownloadFileAsync(attachment.FilePath, attachment.Id);
        return File(stream, attachment.ContentType ?? "application/octet-stream", attachment.FileName);
    }

    private async Task<(Guid TenantId, Guid SourceCaseId)?> GetAuthorizedSourceCaseAsync(Guid legalCaseId)
    {
        var tenantId = _currentUser.TenantId;
        if (!tenantId.HasValue || tenantId.Value == Guid.Empty) return null;
        try
        {
            var legalCase = await _caseService.GetCaseAsync(legalCaseId);
            if (legalCase == null || !string.Equals(legalCase.Module, "Legal", StringComparison.OrdinalIgnoreCase)) return null;
            var sourceId = legalCase.Fields.FirstOrDefault(field => field.Key == "sourceProcedureCaseId")?.Value;
            return Guid.TryParse(sourceId, out var id) ? (tenantId.Value, id) : null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}
