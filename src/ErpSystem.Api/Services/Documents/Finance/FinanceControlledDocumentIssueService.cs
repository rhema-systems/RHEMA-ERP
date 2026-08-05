using System.Data;
using ErpSystem.Api.Services.Finance;
using ErpSystem.Core.DTOs.Documents;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Documents;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Documents.Finance;

/// <summary>
/// Persists the append-only issue register shared by controlled Finance payment slips and
/// receipts. PDF builders call <see cref="PrepareAsync"/> before rendering, then this service
/// rechecks the copy sequence in a serializable transaction before accepting the exact byte hash.
/// This two-stage check prevents simultaneous callers from both receiving an "Original" copy.
/// </summary>
public sealed class FinanceControlledDocumentIssueService : IFinanceControlledDocumentIssueService
{
    private const int MinimumReplacementReasonLength = 20;

    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IFinanceAuditService? _audit;

    public FinanceControlledDocumentIssueService(
        ApplicationDbContext context,
        ICurrentUserService currentUser,
        IFinanceAuditService? audit = null)
    {
        _context = context;
        _currentUser = currentUser;
        _audit = audit;
    }

    public async Task<ControlledDocumentIssuePreparationDto> PrepareAsync(
        string documentType,
        string sourceDocumentType,
        Guid sourceDocumentId,
        string documentNumber,
        string? requestedCopyType,
        string? replacementReason,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.GetRequiredFinanceTenantId();
        var userId = CurrentUserId();
        var normalizedDocumentType = RequiredText(documentType, "Document type", 100);
        var normalizedSourceType = RequiredText(sourceDocumentType, "Source document type", 100);
        var normalizedDocumentNumber = RequiredText(documentNumber, "Document number", 100);
        if (sourceDocumentId == Guid.Empty)
        {
            throw new InvalidOperationException("A controlled document source id is required.");
        }

        var copyType = NormalizeCopyType(requestedCopyType);
        var existing = await _context.Set<FinanceControlledDocumentIssue>()
            .AsNoTracking()
            .Where(item =>
                item.TenantId == tenantId &&
                item.DocumentType == normalizedDocumentType &&
                item.SourceDocumentId == sourceDocumentId &&
                !item.IsDeleted)
            .OrderBy(item => item.CopyNumber)
            .ToListAsync(cancellationToken);

        ValidateRequestedCopy(copyType, replacementReason, existing.Count);
        var cleanedReason = copyType == ControlledDocumentCopyTypes.Replacement
            ? RequiredText(replacementReason, "Replacement reason", 1000)
            : null;

        return new ControlledDocumentIssuePreparationDto(
            Guid.NewGuid(),
            tenantId,
            normalizedDocumentType,
            normalizedSourceType,
            sourceDocumentId,
            normalizedDocumentNumber,
            existing.Count + 1,
            copyType,
            cleanedReason,
            DateTime.UtcNow,
            userId,
            string.IsNullOrWhiteSpace(_currentUser.UserName) ? userId.ToString() : _currentUser.UserName!.Trim());
    }

    public async Task RecordIssuedAsync(
        ControlledDocumentIssuePreparationDto preparation,
        string fileName,
        string contentType,
        string contentSha256,
        string auditEventType,
        string sourceModule,
        string auditResource,
        Guid? journalEntryId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preparation);
        var tenantId = _currentUser.GetRequiredFinanceTenantId();
        var userId = CurrentUserId();
        if (preparation.TenantId != tenantId || preparation.IssuedById != userId)
        {
            throw new InvalidOperationException("Controlled document issue context no longer matches the current user and tenant.");
        }

        var normalizedHash = RequiredText(contentSha256, "Content SHA-256", 64).ToLowerInvariant();
        if (normalizedHash.Length != 64 || normalizedHash.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new InvalidOperationException("Controlled document content hash must be a 64-character SHA-256 value.");
        }

        // SQL Server SERIALIZABLE protects the read-then-insert sequence. The unique database
        // index remains a second line of defence if another producer ever bypasses this service.
        await using var transaction = _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;

        var existingCount = await _context.Set<FinanceControlledDocumentIssue>()
            .CountAsync(item =>
                item.TenantId == tenantId &&
                item.DocumentType == preparation.DocumentType &&
                item.SourceDocumentId == preparation.SourceDocumentId &&
                !item.IsDeleted,
                cancellationToken);
        if (existingCount + 1 != preparation.CopyNumber)
        {
            throw new InvalidOperationException(
                "The controlled document issue sequence changed while the PDF was being prepared. Refresh the record and request the correct copy type.");
        }

        // Re-run the semantic rule inside the protected transaction. This is important for the
        // first copy because two concurrent browser tabs may both have prepared an original.
        ValidateRequestedCopy(preparation.CopyType, preparation.ReplacementReason, existingCount);

        var issue = new FinanceControlledDocumentIssue
        {
            Id = preparation.IssueId,
            TenantId = tenantId,
            DocumentType = preparation.DocumentType,
            SourceDocumentType = preparation.SourceDocumentType,
            SourceDocumentId = preparation.SourceDocumentId,
            DocumentNumber = preparation.DocumentNumber,
            CopyNumber = preparation.CopyNumber,
            CopyType = preparation.CopyType,
            ReplacementReason = preparation.ReplacementReason,
            IssuedAtUtc = preparation.IssuedAtUtc,
            IssuedById = preparation.IssuedById,
            IssuedByName = preparation.IssuedByName,
            ContentSha256 = normalizedHash,
            FileName = RequiredText(fileName, "File name", 255),
            ContentType = RequiredText(contentType, "Content type", 100),
            JournalEntryId = journalEntryId,
            CreatedAt = preparation.IssuedAtUtc,
            CreatedById = preparation.IssuedById,
            CreatedBy = preparation.IssuedByName
        };
        _context.Set<FinanceControlledDocumentIssue>().Add(issue);
        await _context.SaveChangesAsync(cancellationToken);

        if (_audit != null)
        {
            await _audit.RecordAsync(new FinanceAuditEventDto
            {
                EventType = RequiredText(auditEventType, "Audit event type", 100),
                TenantId = tenantId,
                SourceModule = RequiredText(sourceModule, "Source module", 100),
                SourceDocumentType = preparation.SourceDocumentType,
                SourceDocumentId = preparation.SourceDocumentId,
                JournalEntryId = journalEntryId,
                AfterValues = new
                {
                    IssueId = issue.Id,
                    issue.DocumentNumber,
                    issue.CopyNumber,
                    issue.CopyType,
                    issue.ReplacementReason,
                    issue.IssuedAtUtc,
                    issue.IssuedById,
                    issue.IssuedByName,
                    issue.ContentSha256,
                    issue.FileName
                },
                Reason = issue.ReplacementReason,
                Resource = RequiredText(auditResource, "Audit resource", 255),
                ResourceId = issue.SourceDocumentId.ToString(),
                Comment = issue.CopyType == ControlledDocumentCopyTypes.Original
                    ? "Controlled original Finance document issued."
                    : $"Controlled replacement Finance document #{issue.CopyNumber} issued with a retained reason."
            }, cancellationToken);
        }

        if (transaction != null)
        {
            await transaction.CommitAsync(cancellationToken);
        }
    }

    public async Task<ControlledDocumentIssueSummaryDto> GetSummaryAsync(
        string documentType,
        Guid sourceDocumentId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.GetRequiredFinanceTenantId();
        var normalizedDocumentType = RequiredText(documentType, "Document type", 100);
        var issues = await _context.Set<FinanceControlledDocumentIssue>()
            .AsNoTracking()
            .Where(item =>
                item.TenantId == tenantId &&
                item.DocumentType == normalizedDocumentType &&
                item.SourceDocumentId == sourceDocumentId &&
                !item.IsDeleted)
            .OrderBy(item => item.CopyNumber)
            .ToListAsync(cancellationToken);
        var original = issues.FirstOrDefault(item => item.CopyType == ControlledDocumentCopyTypes.Original);
        var last = issues.LastOrDefault();
        return new ControlledDocumentIssueSummaryDto
        {
            OriginalIssued = original != null,
            OriginalIssuedAtUtc = original?.IssuedAtUtc,
            OriginalIssuedByName = original?.IssuedByName,
            ReplacementCount = issues.Count(item => item.CopyType == ControlledDocumentCopyTypes.Replacement),
            TotalIssued = issues.Count,
            LastIssuedAtUtc = last?.IssuedAtUtc,
            LastIssuedByName = last?.IssuedByName
        };
    }

    private static void ValidateRequestedCopy(string copyType, string? replacementReason, int existingCount)
    {
        if (existingCount == 0 && copyType != ControlledDocumentCopyTypes.Original)
        {
            throw new InvalidOperationException("Issue the controlled original before requesting a replacement copy.");
        }

        if (existingCount > 0 && copyType == ControlledDocumentCopyTypes.Original)
        {
            throw new InvalidOperationException("The controlled original has already been issued. Request a replacement copy and provide a reason.");
        }

        if (copyType == ControlledDocumentCopyTypes.Replacement &&
            (string.IsNullOrWhiteSpace(replacementReason) || replacementReason.Trim().Length < MinimumReplacementReasonLength))
        {
            throw new InvalidOperationException($"Replacement reason must contain at least {MinimumReplacementReasonLength} characters.");
        }
    }

    private Guid CurrentUserId()
        => Guid.TryParse(_currentUser.UserId, out var userId) && userId != Guid.Empty
            ? userId
            : throw new InvalidOperationException("A controlled document issue requires an authenticated Finance user.");

    private static string NormalizeCopyType(string? value)
    {
        var copyType = value?.Trim();
        if (string.IsNullOrWhiteSpace(copyType) || copyType.Equals(ControlledDocumentCopyTypes.Original, StringComparison.OrdinalIgnoreCase))
            return ControlledDocumentCopyTypes.Original;
        if (copyType.Equals(ControlledDocumentCopyTypes.Replacement, StringComparison.OrdinalIgnoreCase) ||
            copyType.Equals("Reprint", StringComparison.OrdinalIgnoreCase))
            return ControlledDocumentCopyTypes.Replacement;
        throw new InvalidOperationException("Copy type must be Original or Replacement.");
    }

    private static string RequiredText(string? value, string label, int maxLength)
    {
        var cleaned = value?.Trim();
        if (string.IsNullOrWhiteSpace(cleaned))
            throw new InvalidOperationException($"{label} is required.");
        if (cleaned.Length > maxLength)
            throw new InvalidOperationException($"{label} cannot exceed {maxLength} characters.");
        return cleaned;
    }
}
