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
        Guid financeDimensionSetId,
        Guid? financeDimensionSnapshotId = null,
        CancellationToken cancellationToken = default)
    {
        var (tenantId, route) = ValidateKey(producer, sourceDocumentId, sourceLineId, validateLineId: true);
        if (financeDimensionSetId == Guid.Empty)
            throw new ArgumentException("A canonical Finance dimension set is required.", nameof(financeDimensionSetId));
        if (!sourceLineId.HasValue && financeDimensionSnapshotId.HasValue)
            throw new InvalidOperationException("A draft document default cannot be frozen as line-level evidence.");

        var dimensionSetExists = await _context.FinanceDimensionSets.AsNoTracking().AnyAsync(item =>
            item.Id == financeDimensionSetId && item.TenantId == tenantId && !item.IsDeleted, cancellationToken);
        if (!dimensionSetExists)
            throw new KeyNotFoundException("The canonical Finance dimension set was not found for this tenant.");

        if (financeDimensionSnapshotId.HasValue)
        {
            var snapshot = await _context.FinanceDimensionSnapshots.AsNoTracking().SingleOrDefaultAsync(item =>
                item.Id == financeDimensionSnapshotId.Value && item.TenantId == tenantId && !item.IsDeleted,
                cancellationToken) ?? throw new KeyNotFoundException(
                    "The Finance dimension evidence snapshot was not found for this tenant.");
            if (snapshot.FinanceDimensionSetId != financeDimensionSetId
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
            if (assignment.FinanceDimensionSnapshotId.HasValue)
            {
                if (assignment.FinanceDimensionSetId == financeDimensionSetId
                    && assignment.FinanceDimensionSnapshotId == financeDimensionSnapshotId)
                    return Map(assignment);
                throw new InvalidOperationException("Frozen source-dimension evidence is immutable.");
            }

            assignment.FinanceDimensionSetId = financeDimensionSetId;
            assignment.FinanceDimensionSnapshotId = financeDimensionSnapshotId;
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
                CreatedAt = DateTime.UtcNow,
                CreatedBy = _currentUser.UserName,
                CreatedById = UserId
            };
            _context.FinanceSourceDimensionAssignments.Add(assignment);
        }

        await _context.SaveChangesAsync(cancellationToken);
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
        if (assignment.FinanceDimensionSnapshotId.HasValue)
            throw new InvalidOperationException("Frozen source-dimension evidence cannot be cleared.");

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
        FinanceDimensionSetId = item.FinanceDimensionSetId,
        FinanceDimensionSnapshotId = item.FinanceDimensionSnapshotId,
        RowVersion = item.RowVersion.Length == 0 ? null : Convert.ToBase64String(item.RowVersion)
    };
}
