using System.Data;
using System.Security.Cryptography;
using System.Text;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ErpSystem.Api.Services.Finance.GL;

public sealed partial class FinanceSourceBookAuthorityService : IFinanceSourceBookAuthorityService
{
    private static readonly HashSet<string> FreezeStages = new(StringComparer.Ordinal)
    {
        FinanceSourceBookAuthorityFreezeStages.Submitted,
        FinanceSourceBookAuthorityFreezeStages.Authorized,
        FinanceSourceBookAuthorityFreezeStages.PrePost
    };
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public FinanceSourceBookAuthorityService(ApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<FinanceSourceBookAuthorityResult> FreezeInitialPrimaryAsync(
        FinanceSourceBookAuthorityFreezeRequest request, CancellationToken cancellationToken = default)
    {
        var source = Normalize(request);
        await RequireMutationScopeAndLockAsync(source, cancellationToken);
        var existing = await SourceAuthorities(source).OrderByDescending(item => item.AuthorityVersion)
            .Take(2).ToListAsync(cancellationToken);
        if (existing.Count != 0)
        {
            var retry = existing[0];
            if (retry.SourceWorkflowInstanceId != source.WorkflowInstanceId)
                throw new InvalidOperationException("SOURCE_BOOK_AUTHORITY_VERSION_REQUIRED: frozen authority requires a governed rejected-workflow resubmission.");
            await RequireWorkflowOwnershipAsync(source, cancellationToken);
            await ValidateFrozenRequestAsync(retry, source, cancellationToken);
            return Map(retry);
        }
        await RequireWorkflowOwnershipAsync(source, cancellationToken);
        var primary = await ResolvePrimaryAsync(source.EffectiveDate, cancellationToken);
        var entity = CreateAuthority(source, primary.BookId, primary.BookCode, primary.FunctionalCurrency,
            primary.SelectionBasis, 1, null, Actor(), DateTime.UtcNow);
        _db.FinanceSourceBookAuthorities.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return Map(entity);
    }

    public async Task<FinanceSourceBookAuthorityResult> FreezeResubmissionAsync(
        FinanceSourceBookAuthorityFreezeRequest request, Guid supersedesAuthorityId,
        CancellationToken cancellationToken = default)
    {
        var source = Normalize(request);
        if (!source.WorkflowInstanceId.HasValue)
            throw new InvalidOperationException("SOURCE_BOOK_AUTHORITY_WORKFLOW_REQUIRED: resubmission requires a new workflow.");
        await RequireMutationScopeAndLockAsync(source, cancellationToken);
        var predecessor = await _db.FinanceSourceBookAuthorities.AsNoTracking().SingleOrDefaultAsync(item =>
            item.Id == supersedesAuthorityId && item.TenantId == TenantId && !item.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("SOURCE_BOOK_AUTHORITY_PREDECESSOR_REQUIRED: predecessor was not found.");
        RequireSameSourceIdentity(predecessor, source, compareWorkflow: false);
        await RequireWorkflowOwnershipAsync(source, cancellationToken);
        await RequireGovernedReplacementAsync(predecessor, source, cancellationToken);
        if (predecessor.SelectionBasis == FinanceSourceBookAuthoritySelectionBases.InheritedOriginal)
            throw new InvalidOperationException("SOURCE_BOOK_AUTHORITY_INHERITED_RESUBMISSION_REQUIRED: inherited authority must be versioned through FreezeInheritedAsync with exact origins.");
        var versions = await SourceAuthorities(source).OrderByDescending(item => item.AuthorityVersion)
            .Take(2).ToListAsync(cancellationToken);
        var retry = versions.SingleOrDefault(item => item.SourceWorkflowInstanceId == source.WorkflowInstanceId);
        if (retry is not null)
        {
            if (retry.SupersedesAuthorityId != predecessor.Id)
                throw new InvalidOperationException("SOURCE_BOOK_AUTHORITY_LINEAGE_MISMATCH: retry has another predecessor.");
            await ValidateFrozenRequestAsync(retry, source, cancellationToken);
            return Map(retry);
        }
        if (versions.Count == 0 || versions[0].Id != predecessor.Id)
            throw new InvalidOperationException("SOURCE_BOOK_AUTHORITY_PREDECESSOR_STALE: only latest authority can be superseded.");
        var primary = await ResolvePrimaryAsync(source.EffectiveDate, cancellationToken);
        var entity = CreateAuthority(source, primary.BookId, primary.BookCode, primary.FunctionalCurrency,
            primary.SelectionBasis, predecessor.AuthorityVersion + 1, predecessor.Id, Actor(), DateTime.UtcNow);
        _db.FinanceSourceBookAuthorities.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return Map(entity);
    }

    public async Task<FinanceSourceBookAuthorityResult> FreezeInheritedAsync(
        FinanceSourceBookAuthorityFreezeRequest request,
        IReadOnlyCollection<FinanceSourceBookAuthorityOriginRequest> origins,
        CancellationToken cancellationToken = default)
    {
        var source = Normalize(request);
        if (origins is null || origins.Count == 0 || origins.Any(item => item.OriginAuthorityId == Guid.Empty))
            throw new InvalidOperationException("SOURCE_BOOK_AUTHORITY_ORIGIN_REQUIRED: inherited authority requires exact origins.");
        var normalized = origins.Select(item => new OriginInput(item.OriginAuthorityId,
                FinancePreparedIdentityNormalizer.NormalizeValue(item.Role, "Authority origin role")))
            .OrderBy(item => item.AuthorityId).ThenBy(item => item.Role, StringComparer.Ordinal).ToList();
        if (normalized.Distinct().Count() != normalized.Count)
            throw new InvalidOperationException("SOURCE_BOOK_AUTHORITY_ORIGIN_DUPLICATE: inherited origins must be unique.");
        await RequireMutationScopeAndLockAsync(source, cancellationToken);
        await RequireWorkflowOwnershipAsync(source, cancellationToken);
        var existing = await SourceAuthorities(source).Include(item => item.Origins)
            .OrderByDescending(item => item.AuthorityVersion).Take(2).ToListAsync(cancellationToken);
        var predecessor = existing.FirstOrDefault();
        var retry = predecessor is not null && predecessor.SourceWorkflowInstanceId == source.WorkflowInstanceId
            ? predecessor : null;
        if (predecessor is not null && retry is null)
        {
            if (predecessor.SelectionBasis != FinanceSourceBookAuthoritySelectionBases.InheritedOriginal)
                throw new InvalidOperationException("SOURCE_BOOK_AUTHORITY_VERSION_REQUIRED: another authority kind already owns this source action.");
            await RequireGovernedReplacementAsync(predecessor, source, cancellationToken);
        }
        var ids = normalized.Select(item => item.AuthorityId).Distinct().ToList();
        var authorities = await _db.FinanceSourceBookAuthorities.AsNoTracking()
            .Where(item => item.TenantId == TenantId && ids.Contains(item.Id) && !item.IsDeleted).ToListAsync(cancellationToken);
        if (authorities.Count != ids.Count || authorities.Any(item => !item.OriginalFinancePostingEventId.HasValue || !item.OriginalJournalEntryId.HasValue))
            throw new InvalidOperationException("SOURCE_BOOK_AUTHORITY_ORIGIN_UNBOUND: every origin needs exact event/journal evidence.");
        if (authorities.Select(item => item.AccountingBookId).Distinct().Count() != 1 ||
            authorities.Select(item => item.AccountingBookCode).Distinct(StringComparer.Ordinal).Count() != 1 ||
            authorities.Select(item => item.FunctionalCurrencyCode).Distinct(StringComparer.Ordinal).Count() != 1)
            throw new InvalidOperationException("SOURCE_BOOK_AUTHORITY_ORIGIN_MIXED: origins must share one exact book and functional currency.");
        foreach (var authority in authorities)
        {
            await ValidateRetainedCoordinateAsync(authority, true, cancellationToken);
            await ValidateOriginalEvidenceAsync(authority, cancellationToken);
        }
        if (retry is not null)
        {
            await ValidateFrozenRequestAsync(retry, source, cancellationToken);
            var retained = retry.Origins.Select(item => new OriginInput(item.OriginAuthorityId, item.Role))
                .OrderBy(item => item.AuthorityId).ThenBy(item => item.Role, StringComparer.Ordinal);
            if (!retained.SequenceEqual(normalized))
                throw new InvalidOperationException("SOURCE_BOOK_AUTHORITY_ORIGIN_MISMATCH: retry origins differ.");
            return Map(retry);
        }
        var exemplar = authorities[0];
        var entity = CreateAuthority(source, exemplar.AccountingBookId, exemplar.AccountingBookCode,
            exemplar.FunctionalCurrencyCode, FinanceSourceBookAuthoritySelectionBases.InheritedOriginal,
            predecessor?.AuthorityVersion + 1 ?? 1, predecessor?.Id, Actor(), DateTime.UtcNow);
        foreach (var origin in normalized)
        {
            var retained = authorities.Single(item => item.Id == origin.AuthorityId);
            entity.Origins.Add(new FinanceSourceBookAuthorityOrigin
            {
                TenantId = TenantId, OriginAuthorityId = retained.Id, Role = origin.Role,
                OriginalFinancePostingEventId = retained.OriginalFinancePostingEventId!.Value,
                OriginalJournalEntryId = retained.OriginalJournalEntryId!.Value
            });
        }
        entity.AuthorityFingerprint = Fingerprint(entity, entity.Origins);
        _db.FinanceSourceBookAuthorities.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return Map(entity);
    }

    private async Task RequireGovernedReplacementAsync(FinanceSourceBookAuthority predecessor,
        NormalizedSource source, CancellationToken cancellationToken)
    {
        if (!source.WorkflowInstanceId.HasValue || !predecessor.SourceWorkflowInstanceId.HasValue ||
            predecessor.SourceWorkflowInstanceId == source.WorkflowInstanceId)
            throw new InvalidOperationException("SOURCE_BOOK_AUTHORITY_NEW_WORKFLOW_REQUIRED: resubmission requires a different workflow.");
        if (predecessor.OriginalFinancePostingEventId.HasValue || predecessor.OriginalJournalEntryId.HasValue)
            throw new InvalidOperationException("SOURCE_BOOK_AUTHORITY_POSTED_IMMUTABLE: posted authority cannot be superseded.");
        var workflows = await _db.WorkflowInstances.AsNoTracking().Where(item => item.TenantId == TenantId && !item.IsDeleted &&
                (item.Id == predecessor.SourceWorkflowInstanceId.Value || item.Id == source.WorkflowInstanceId.Value))
            .Take(3).ToListAsync(cancellationToken);
        if (workflows.Count != 2)
            throw new InvalidOperationException("SOURCE_BOOK_AUTHORITY_WORKFLOW_EVIDENCE_MISSING: both workflows must be retained.");
        var oldWorkflow = workflows.Single(item => item.Id == predecessor.SourceWorkflowInstanceId.Value);
        var newWorkflow = workflows.Single(item => item.Id == source.WorkflowInstanceId.Value);
        if (oldWorkflow.EntityId != source.SourceDocumentId || newWorkflow.EntityId != source.SourceDocumentId ||
            oldWorkflow.EntityTypeId != newWorkflow.EntityTypeId ||
            oldWorkflow.Status is not (WorkflowInstanceStatus.Cancelled or WorkflowInstanceStatus.Failed))
            throw new InvalidOperationException("SOURCE_BOOK_AUTHORITY_RESUBMISSION_NOT_GOVERNED: predecessor must be rejected and replacement must be the same-source workflow.");
        await ValidateRetainedCoordinateAsync(predecessor, true, cancellationToken);
    }
}
