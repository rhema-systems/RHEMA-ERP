using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Finance.Integration;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.GL;

/// <summary>
/// Tenant-scoped implementation used only from trusted Finance producer endpoints/adapters.
/// Browser DTOs never choose producer identity or route capabilities.
/// </summary>
public sealed class FinanceSourceDimensionAssignmentStore : IFinanceSourceDimensionAssignmentStore
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public FinanceSourceDimensionAssignmentStore(
        ApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    private Guid TenantId => _currentUser.GetRequiredFinanceTenantId();
    private Guid? UserId => Guid.TryParse(_currentUser.UserId, out var id) && id != Guid.Empty ? id : null;

    public Task<FinanceSourceDimensionAssignmentDto> RegisterDocumentAsync(
        FinancePostingProducerContext producer,
        Guid sourceDocumentId,
        CancellationToken cancellationToken = default) =>
        UpsertAsync(producer, sourceDocumentId, null, null, null, cancellationToken);

    public async Task RegisterDocumentContextAsync(
        FinancePostingProducerContext producer,
        Guid sourceDocumentId,
        DateTime documentDate,
        IReadOnlyList<FinanceSourceDocumentLineContext> authoritativeLines,
        CancellationToken cancellationToken = default)
    {
        if (documentDate == default)
            throw new InvalidOperationException("The source document date is required for Finance dimension evidence.");
        if (authoritativeLines.Count == 0)
            throw new InvalidOperationException("At least one authoritative Finance source line is required.");
        if (authoritativeLines.Any(item => item.SourceLineId == Guid.Empty || item.AccountId == Guid.Empty)
            || authoritativeLines.Select(item => item.SourceLineId).Distinct().Count() != authoritativeLines.Count)
            throw new InvalidOperationException("Finance source-line context must contain unique, non-empty line and account IDs.");

        var (tenantId, route) = ValidateKey(producer, sourceDocumentId, null, validateLineId: false);
        await RegisterDocumentAsync(producer, sourceDocumentId, cancellationToken);
        var header = await _context.FinanceSourceDimensionAssignments.SingleAsync(item =>
            item.TenantId == tenantId
            && item.SourceDocumentType == route.DocumentType
            && item.SourceDocumentId == sourceDocumentId
            && !item.SourceLineId.HasValue
            && !item.IsDeleted, cancellationToken);
        EnsureSameProducer(header, route);
        header.SourceDocumentDate = documentDate.Date;
        header.ExpectedSourceLineCount = authoritativeLines.Count;
        header.SourceLineManifestHash = FinanceSourceLineManifest.Compute(
            authoritativeLines.Select(item => (item.SourceLineId, item.AccountId)));
        header.UpdatedAt = DateTime.UtcNow;
        header.UpdatedBy = _currentUser.UserName;
        header.LastModifiedById = UserId;

        foreach (var line in authoritativeLines)
        {
            var assignment = await _context.FinanceSourceDimensionAssignments.SingleOrDefaultAsync(item =>
                item.TenantId == tenantId
                && item.SourceDocumentType == route.DocumentType
                && item.SourceDocumentId == sourceDocumentId
                && item.SourceLineId == line.SourceLineId
                && !item.IsDeleted, cancellationToken);
            if (assignment is null)
            {
                assignment = new FinanceSourceDimensionAssignment
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    RouteId = route.Id,
                    ProducerModule = route.ProducerModule,
                    SourceRoute = route.SourceRoute,
                    SourceDocumentType = route.DocumentType,
                    ContractVersion = route.ContractVersion,
                    SourceDocumentId = sourceDocumentId,
                    SourceLineId = line.SourceLineId,
                    ResolvedAccountId = line.AccountId,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = _currentUser.UserName,
                    CreatedById = UserId
                };
                _context.FinanceSourceDimensionAssignments.Add(assignment);
                continue;
            }

            EnsureSameProducer(assignment, route);
            if (assignment.EvidenceFrozenAt.HasValue && assignment.ResolvedAccountId != line.AccountId)
                throw new InvalidOperationException("A frozen Finance source line cannot be rebound to another account.");
            assignment.ResolvedAccountId = line.AccountId;
            assignment.ContractVersion = route.ContractVersion;
            assignment.UpdatedAt = DateTime.UtcNow;
            assignment.UpdatedBy = _currentUser.UserName;
            assignment.LastModifiedById = UserId;
        }
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FinanceSourceDimensionAssignmentDto>> GetDocumentAssignmentsAsync(
        FinancePostingProducerContext producer,
        Guid sourceDocumentId,
        CancellationToken cancellationToken = default)
    {
        var (tenantId, route) = ValidateKey(producer, sourceDocumentId, null, validateLineId: false);
        var assignments = await _context.FinanceSourceDimensionAssignments.AsNoTracking()
            .Where(item => item.TenantId == tenantId
                && item.SourceDocumentType == route.DocumentType
                && item.SourceDocumentId == sourceDocumentId
                && !item.IsDeleted)
            .OrderBy(item => item.SourceLineId.HasValue)
            .ThenBy(item => item.SourceLineId)
            .ToListAsync(cancellationToken);
        foreach (var assignment in assignments) EnsureSameProducer(assignment, route);
        return assignments.Select(Map).ToList();
    }

    public async Task<FinanceSourceDimensionAssignmentDto> UpsertAsync(
        FinancePostingProducerContext producer,
        Guid sourceDocumentId,
        Guid? sourceLineId,
        Guid? financeDimensionSetId,
        Guid? financeDimensionSnapshotId = null,
        CancellationToken cancellationToken = default)
    {
        var (tenantId, route) = ValidateKey(producer, sourceDocumentId, sourceLineId, validateLineId: true);
        if (financeDimensionSetId == Guid.Empty)
            throw new ArgumentException("A canonical Finance dimension set cannot be empty.", nameof(financeDimensionSetId));
        if (financeDimensionSnapshotId.HasValue && !financeDimensionSetId.HasValue)
            throw new InvalidOperationException("Frozen dimension evidence requires a canonical Finance dimension set.");
        if (!sourceLineId.HasValue && financeDimensionSnapshotId.HasValue)
            throw new InvalidOperationException("A draft document default cannot be frozen as line-level evidence.");

        if (financeDimensionSetId.HasValue)
        {
            var dimensionSetExists = await _context.FinanceDimensionSets.AsNoTracking().AnyAsync(item =>
                item.Id == financeDimensionSetId.Value && item.TenantId == tenantId && !item.IsDeleted, cancellationToken);
            if (!dimensionSetExists)
                throw new KeyNotFoundException("The canonical Finance dimension set was not found for this tenant.");
        }

        if (financeDimensionSnapshotId.HasValue)
        {
            var snapshot = await _context.FinanceDimensionSnapshots.AsNoTracking().SingleOrDefaultAsync(item =>
                item.Id == financeDimensionSnapshotId.Value && item.TenantId == tenantId && !item.IsDeleted,
                cancellationToken) ?? throw new KeyNotFoundException(
                    "The Finance dimension evidence snapshot was not found for this tenant.");
            if (snapshot.FinanceDimensionSetId != financeDimensionSetId!.Value
                || !string.Equals(snapshot.ProducerModule, route.ProducerModule, StringComparison.Ordinal)
                || !string.Equals(snapshot.SourceRoute, route.SourceRoute, StringComparison.Ordinal)
                || !string.Equals(snapshot.SourceDocumentType, route.DocumentType, StringComparison.Ordinal)
                || !string.Equals(snapshot.ContractVersion, route.ContractVersion, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "The Finance dimension snapshot does not match the canonical set and trusted producer route.");
        }

        var assignment = await _context.FinanceSourceDimensionAssignments.SingleOrDefaultAsync(item =>
            item.TenantId == tenantId
            && item.SourceDocumentType == route.DocumentType
            && item.SourceDocumentId == sourceDocumentId
            && item.SourceLineId == sourceLineId
            && !item.IsDeleted, cancellationToken);

        if (assignment is not null)
        {
            EnsureSameProducer(assignment, route);
            if (assignment.EvidenceFrozenAt.HasValue)
            {
                if (assignment.FinanceDimensionSetId == financeDimensionSetId
                    && assignment.FinanceDimensionSnapshotId == financeDimensionSnapshotId)
                    return Map(assignment);
                throw new InvalidOperationException("Frozen source-dimension evidence is immutable.");
            }

            assignment.FinanceDimensionSetId = financeDimensionSetId;
            assignment.FinanceDimensionSnapshotId = financeDimensionSnapshotId;
            assignment.EvidenceFrozenAt = financeDimensionSnapshotId.HasValue ? DateTime.UtcNow : null;
            assignment.ContractVersion = route.ContractVersion;
            assignment.UpdatedAt = DateTime.UtcNow;
            assignment.UpdatedBy = _currentUser.UserName;
            assignment.LastModifiedById = UserId;
        }
        else
        {
            assignment = new FinanceSourceDimensionAssignment
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                RouteId = route.Id,
                ProducerModule = route.ProducerModule,
                SourceRoute = route.SourceRoute,
                SourceDocumentType = route.DocumentType,
                ContractVersion = route.ContractVersion,
                SourceDocumentId = sourceDocumentId,
                SourceLineId = sourceLineId,
                FinanceDimensionSetId = financeDimensionSetId,
                FinanceDimensionSnapshotId = financeDimensionSnapshotId,
                EvidenceFrozenAt = financeDimensionSnapshotId.HasValue ? DateTime.UtcNow : null,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = _currentUser.UserName,
                CreatedById = UserId
            };
            _context.FinanceSourceDimensionAssignments.Add(assignment);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return Map(assignment);
    }

    public async Task<FinanceSourceDimensionAssignmentDto> FreezeLineAsync(
        FinancePostingProducerContext producer,
        Guid sourceDocumentId,
        Guid sourceLineId,
        Guid? financeDimensionSetId,
        Guid? financeDimensionSnapshotId,
        CancellationToken cancellationToken = default)
    {
        if (sourceLineId == Guid.Empty)
            throw new ArgumentException("A source line ID is required.", nameof(sourceLineId));
        var result = await UpsertAsync(
            producer, sourceDocumentId, sourceLineId,
            financeDimensionSetId, financeDimensionSnapshotId, cancellationToken);
        var assignment = await _context.FinanceSourceDimensionAssignments.SingleAsync(item =>
            item.Id == result.Id && item.TenantId == TenantId && !item.IsDeleted,
            cancellationToken);
        if (!assignment.EvidenceFrozenAt.HasValue)
        {
            assignment.EvidenceFrozenAt = DateTime.UtcNow;
            assignment.UpdatedAt = assignment.EvidenceFrozenAt;
            assignment.UpdatedBy = _currentUser.UserName;
            assignment.LastModifiedById = UserId;
            await _context.SaveChangesAsync(cancellationToken);
        }
        return Map(assignment);
    }

    public async Task ClearAsync(
        FinancePostingProducerContext producer,
        Guid sourceDocumentId,
        Guid? sourceLineId,
        CancellationToken cancellationToken = default)
    {
        var (tenantId, route) = ValidateKey(producer, sourceDocumentId, sourceLineId, validateLineId: true);
        var assignment = await _context.FinanceSourceDimensionAssignments.SingleOrDefaultAsync(item =>
            item.TenantId == tenantId
            && item.SourceDocumentType == route.DocumentType
            && item.SourceDocumentId == sourceDocumentId
            && item.SourceLineId == sourceLineId
            && !item.IsDeleted, cancellationToken);
        if (assignment is null) return;

        EnsureSameProducer(assignment, route);
        if (assignment.EvidenceFrozenAt.HasValue)
            throw new InvalidOperationException("Frozen source-dimension evidence cannot be cleared.");

        assignment.FinanceDimensionSetId = null;
        assignment.FinanceDimensionSnapshotId = null;
        assignment.EvidenceFrozenAt = null;
        assignment.UpdatedAt = DateTime.UtcNow;
        assignment.UpdatedBy = _currentUser.UserName;
        assignment.LastModifiedById = UserId;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveLineAsync(
        FinancePostingProducerContext producer,
        Guid sourceDocumentId,
        Guid sourceLineId,
        CancellationToken cancellationToken = default)
    {
        var (tenantId, route) = ValidateKey(producer, sourceDocumentId, sourceLineId, validateLineId: true);
        var assignment = await _context.FinanceSourceDimensionAssignments.SingleOrDefaultAsync(item =>
            item.TenantId == tenantId
            && item.SourceDocumentType == route.DocumentType
            && item.SourceDocumentId == sourceDocumentId
            && item.SourceLineId == sourceLineId
            && !item.IsDeleted, cancellationToken);
        if (assignment is null) return;
        EnsureSameProducer(assignment, route);
        if (assignment.EvidenceFrozenAt.HasValue)
            throw new InvalidOperationException("Frozen source-dimension evidence cannot be removed until the document returns to Draft.");
        assignment.IsDeleted = true;
        assignment.DeletedAt = DateTime.UtcNow;
        assignment.DeletedBy = _currentUser.UserName;
        assignment.UpdatedAt = assignment.DeletedAt;
        assignment.UpdatedBy = _currentUser.UserName;
        assignment.LastModifiedById = UserId;
        await _context.SaveChangesAsync(cancellationToken);
    }

    private (Guid TenantId, FinanceDimensionRouteDefinition Route) ValidateKey(
        FinancePostingProducerContext producer,
        Guid sourceDocumentId,
        Guid? sourceLineId,
        bool validateLineId)
    {
        ArgumentNullException.ThrowIfNull(producer);
        if (sourceDocumentId == Guid.Empty)
            throw new ArgumentException("A source document ID is required.", nameof(sourceDocumentId));
        if (validateLineId && sourceLineId == Guid.Empty)
            throw new ArgumentException("A source line ID cannot be empty.", nameof(sourceLineId));
        return (TenantId, producer.Definition);
    }

    private static void EnsureSameProducer(
        FinanceSourceDimensionAssignment assignment,
        FinanceDimensionRouteDefinition route)
    {
        if (assignment.RouteId != route.Id
            || !string.Equals(assignment.ProducerModule, route.ProducerModule, StringComparison.Ordinal)
            || !string.Equals(assignment.SourceRoute, route.SourceRoute, StringComparison.Ordinal)
            || !string.Equals(assignment.SourceDocumentType, route.DocumentType, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "The stored source-dimension assignment belongs to a different trusted producer route.");
    }

    private static FinanceSourceDimensionAssignmentDto Map(FinanceSourceDimensionAssignment item) => new()
    {
        Id = item.Id,
        RouteId = item.RouteId,
        ProducerModule = item.ProducerModule,
        SourceRoute = item.SourceRoute,
        SourceDocumentType = item.SourceDocumentType,
        ContractVersion = item.ContractVersion,
        SourceDocumentId = item.SourceDocumentId,
        SourceLineId = item.SourceLineId,
        ResolvedAccountId = item.ResolvedAccountId,
        SourceDocumentDate = item.SourceDocumentDate,
        ExpectedSourceLineCount = item.ExpectedSourceLineCount,
        SourceLineManifestHash = item.SourceLineManifestHash,
        FinanceDimensionSetId = item.FinanceDimensionSetId,
        FinanceDimensionSnapshotId = item.FinanceDimensionSnapshotId,
        EvidenceFrozenAt = item.EvidenceFrozenAt,
        BudgetEvidenceStatus = item.BudgetEvidenceStatus,
        BudgetEvaluationHash = item.BudgetEvaluationHash,
        BudgetEvidenceUpdatedAt = item.BudgetEvidenceUpdatedAt,
        RowVersion = item.RowVersion.Length == 0 ? null : Convert.ToBase64String(item.RowVersion)
    };
}
