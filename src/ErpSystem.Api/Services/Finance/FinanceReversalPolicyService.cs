using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance;

/// <summary>
/// Authoritative evaluator for tenant reversal narrative and fiscal-date policy.
/// </summary>
public sealed class FinanceReversalPolicyService : IFinanceReversalPolicyService
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public FinanceReversalPolicyService(
        ApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<FinanceReversalPolicyDecision> ResolveAsync(
        DateTime sourceDocumentDate,
        string? reason,
        DateTime? requestedReversalDate = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.GetRequiredFinanceTenantId();
        var settings = await _context.FinanceSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(item =>
                item.TenantId == tenantId && !item.IsDeleted,
                cancellationToken)
            ?? throw new InvalidOperationException("Finance settings are not configured for this tenant.");

        var normalizedReason = reason?.Trim() ?? string.Empty;
        if (normalizedReason.Length < settings.MinimumReversalReasonLength)
        {
            throw new ArgumentException(
                $"A reversal reason of at least {settings.MinimumReversalReasonLength} characters is required.",
                nameof(reason));
        }

        if (!Enum.IsDefined(settings.ReversalDatePolicy))
            throw new InvalidOperationException("The configured Finance reversal-date policy is invalid.");

        var openPeriods = await _context.FiscalPeriods
            .AsNoTracking()
            .Where(item =>
                item.TenantId == tenantId &&
                !item.IsDeleted &&
                item.IsOpen &&
                !item.IsClosed &&
                !item.IsLocked)
            .OrderByDescending(item => item.StartDate)
            .ToListAsync(cancellationToken);
        if (openPeriods.Count == 0)
            throw new InvalidOperationException("No open fiscal period is available for the Finance reversal.");

        if (settings.ReversalDatePolicy == FinanceReversalDatePolicy.OriginalDocumentPeriodIfOpen)
        {
            var originalDate = sourceDocumentDate.Date;
            if (openPeriods.Any(period =>
                    period.StartDate.Date <= originalDate &&
                    period.EndDate.Date >= originalDate))
            {
                return new FinanceReversalPolicyDecision(normalizedReason, originalDate);
            }
        }

        // "Current" means the latest period Finance has deliberately opened, rather than the
        // server's calendar month. This supports controlled catch-up periods without backdating.
        var currentOpenPeriod = openPeriods[0];
        if (requestedReversalDate.HasValue)
        {
            var preferredDate = requestedReversalDate.Value.Date;
            if (preferredDate < currentOpenPeriod.StartDate.Date ||
                preferredDate > currentOpenPeriod.EndDate.Date)
            {
                throw new InvalidOperationException(
                    $"The requested reversal date must fall in the current open period '{currentOpenPeriod.PeriodName}'.");
            }

            return new FinanceReversalPolicyDecision(normalizedReason, preferredDate);
        }

        var today = DateTime.UtcNow.Date;
        var resolvedDate = today >= currentOpenPeriod.StartDate.Date && today <= currentOpenPeriod.EndDate.Date
            ? today
            : today > currentOpenPeriod.EndDate.Date
                ? currentOpenPeriod.EndDate.Date
                : currentOpenPeriod.StartDate.Date;
        return new FinanceReversalPolicyDecision(normalizedReason, resolvedDate);
    }
}
