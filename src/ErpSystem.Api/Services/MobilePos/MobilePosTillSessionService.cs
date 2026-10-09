using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.MobilePos;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.MobilePos;

public interface IMobilePosTillSessionService
{
    Task<CashierTillSessionDto?> GetCurrentAsync(string installationId, CancellationToken cancellationToken);
    Task<CashierTillSessionDto> OpenAsync(MobilePosOpenTillSessionRequestDto dto, CancellationToken cancellationToken);
    Task<MobilePosTillReconciliationDto> GetReconciliationAsync(
        Guid sessionId,
        string installationId,
        CancellationToken cancellationToken);
}

/// <summary>
/// Binds the existing Finance custody service to the authenticated mobile device, store and till.
/// It does not maintain another till balance or bypass Finance ownership and concurrency controls.
/// </summary>
public sealed class MobilePosTillSessionService : IMobilePosTillSessionService
{
    private readonly IMobilePosFoundationService _foundation;
    private readonly ICashierTillService _cashierTills;
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public MobilePosTillSessionService(
        IMobilePosFoundationService foundation,
        ICashierTillService cashierTills,
        ApplicationDbContext db,
        ICurrentUserService currentUser)
    {
        _foundation = foundation;
        _cashierTills = cashierTills;
        _db = db;
        _currentUser = currentUser;
    }

    private Guid TenantId => _currentUser.TenantId is { } id && id != Guid.Empty
        ? id
        : throw new UnauthorizedAccessException("A current tenant is required for Mobile POS.");

    private Guid UserId => Guid.TryParse(_currentUser.UserId, out var id) && id != Guid.Empty
        ? id
        : throw new UnauthorizedAccessException("An authenticated user is required for Mobile POS.");

    public async Task<CashierTillSessionDto?> GetCurrentAsync(
        string installationId,
        CancellationToken cancellationToken)
    {
        var bootstrap = await _foundation.GetBootstrapAsync(installationId, cancellationToken);
        if (!bootstrap.CurrentTillSessionId.HasValue)
            return null;

        var session = await _cashierTills.GetSessionAsync(
            bootstrap.CurrentTillSessionId.Value,
            cancellationToken);
        if (session == null || session.LiquidityAccountId != bootstrap.Till.LiquidityAccountId)
            throw new InvalidOperationException("The active cashier session no longer belongs to the assigned Mobile POS till.");

        return session;
    }

    public async Task<MobilePosTillReconciliationDto> GetReconciliationAsync(
        Guid sessionId,
        string installationId,
        CancellationToken cancellationToken)
    {
        if (sessionId == Guid.Empty)
            throw new KeyNotFoundException("The till session was not found.");

        var bootstrap = await _foundation.GetBootstrapAsync(installationId, cancellationToken);
        var session = await _cashierTills.GetSessionAsync(sessionId, cancellationToken)
            ?? throw new KeyNotFoundException("The till session was not found.");
        if (session.LiquidityAccountId != bootstrap.Till.LiquidityAccountId
            || session.CashierUserId != UserId)
        {
            throw new UnauthorizedAccessException("This till session does not belong to the current operator and assigned Mobile POS till.");
        }

        var tenantId = TenantId;
        var sales = await _db.MobilePosSales.AsNoTracking()
            .Where(item => item.TenantId == tenantId
                && item.CashierTillSessionId == sessionId
                && item.MobilePosStoreId == bootstrap.Store.Id
                && item.MobilePosTillId == bootstrap.Till.Id
                && !item.IsDeleted)
            .Select(item => new
            {
                item.Status,
                item.SubTotal,
                item.TaxAmount,
                item.DiscountAmount,
                item.TotalAmount,
                WasRecordedOffline = item.MobilePosOfflineGrantId.HasValue
            })
            .ToListAsync(cancellationToken);

        var completed = sales.Where(item => item.Status == MobilePosSaleStatus.Completed).ToArray();
        var tenderRows = await _db.MobilePosTenders.AsNoTracking()
            .Where(item => item.TenantId == tenantId
                && item.MobilePosSale.CashierTillSessionId == sessionId
                && item.MobilePosSale.MobilePosStoreId == bootstrap.Store.Id
                && item.MobilePosSale.MobilePosTillId == bootstrap.Till.Id
                && item.MobilePosSale.Status == MobilePosSaleStatus.Completed
                && !item.IsDeleted
                && !item.MobilePosSale.IsDeleted)
            .Select(item => new
            {
                item.PaymentMethodId,
                PaymentMethodCode = item.PaymentMethod.Code ?? string.Empty,
                PaymentMethodName = item.PaymentMethod.Name,
                PaymentMethodType = item.PaymentMethod.Type.ToString(),
                item.Amount,
                item.WasRecordedOffline,
                item.CustomerPaymentId,
                item.Status
            })
            .ToListAsync(cancellationToken);

        var completedTenders = tenderRows
            .Where(item => item.Status == MobilePosTenderStatus.Completed)
            .ToArray();
        var salesTotal = completed.Sum(item => item.TotalAmount);
        var tenderTotal = completedTenders.Sum(item => item.Amount);
        var difference = decimal.Round(tenderTotal - salesTotal, 4, MidpointRounding.AwayFromZero);

        return new MobilePosTillReconciliationDto
        {
            GeneratedAtUtc = DateTime.UtcNow,
            Session = session,
            CompletedSaleCount = completed.Length,
            OfflineSaleCount = completed.Count(item => item.WasRecordedOffline),
            PendingSaleCount = sales.Count(item => item.Status == MobilePosSaleStatus.Pending),
            RejectedSaleCount = sales.Count(item => item.Status == MobilePosSaleStatus.Rejected),
            SubTotal = completed.Sum(item => item.SubTotal),
            TaxAmount = completed.Sum(item => item.TaxAmount),
            DiscountAmount = completed.Sum(item => item.DiscountAmount),
            SalesTotal = salesTotal,
            TenderTotal = tenderTotal,
            SalesTenderDifference = difference,
            SalesAndTendersBalance = Math.Abs(difference) < 0.01m,
            IncompleteTenderCount = tenderRows.Count(item => item.Status != MobilePosTenderStatus.Completed),
            Tenders = completedTenders
                .GroupBy(item => new
                {
                    item.PaymentMethodId,
                    item.PaymentMethodCode,
                    item.PaymentMethodName,
                    item.PaymentMethodType
                })
                .OrderBy(group => group.Key.PaymentMethodName)
                .Select(group => new MobilePosTillTenderReconciliationDto
                {
                    PaymentMethodId = group.Key.PaymentMethodId,
                    PaymentMethodCode = group.Key.PaymentMethodCode,
                    PaymentMethodName = group.Key.PaymentMethodName,
                    PaymentMethodType = group.Key.PaymentMethodType,
                    TenderCount = group.Count(),
                    Amount = group.Sum(item => item.Amount),
                    OfflineTenderCount = group.Count(item => item.WasRecordedOffline),
                    OfflineAmount = group.Where(item => item.WasRecordedOffline).Sum(item => item.Amount),
                    CanonicalPaymentCount = group.Count(item => item.CustomerPaymentId.HasValue)
                })
                .ToArray()
        };
    }

    public async Task<CashierTillSessionDto> OpenAsync(
        MobilePosOpenTillSessionRequestDto dto,
        CancellationToken cancellationToken)
    {
        var bootstrap = await _foundation.GetBootstrapAsync(dto.InstallationId, cancellationToken);
        if (bootstrap.CurrentTillSessionId.HasValue)
            throw new InvalidOperationException("This operator already has an open session on the assigned till.");

        var businessDate = ResolveBusinessDate(bootstrap.Store.TimeZoneId, bootstrap.ServerTimeUtc);
        var session = await _cashierTills.OpenSessionAsync(new OpenCashierTillSessionDto
        {
            LiquidityAccountId = bootstrap.Till.LiquidityAccountId,
            BusinessDate = businessDate,
            OpeningFloatAmount = dto.OpeningFloatAmount,
            OpeningNotes = dto.OpeningNotes,
            OpeningEvidenceFileId = dto.OpeningEvidenceFileId
        }, cancellationToken);

        if (session.LiquidityAccountId != bootstrap.Till.LiquidityAccountId)
            throw new InvalidOperationException("Finance opened a cashier session for an unexpected till.");

        return session;
    }

    private static DateTime ResolveBusinessDate(string timeZoneId, DateTime serverTimeUtc)
    {
        try
        {
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(serverTimeUtc, DateTimeKind.Utc),
                timeZone).Date;
        }
        catch (TimeZoneNotFoundException)
        {
            throw new InvalidOperationException($"The Mobile POS store time zone '{timeZoneId}' is not available on this server.");
        }
        catch (InvalidTimeZoneException)
        {
            throw new InvalidOperationException($"The Mobile POS store time zone '{timeZoneId}' is invalid.");
        }
    }
}
