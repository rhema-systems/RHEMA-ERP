using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.MobilePos;
using ErpSystem.Core.Interfaces.Finance;

namespace ErpSystem.Api.Services.MobilePos;

public interface IMobilePosTillSessionService
{
    Task<CashierTillSessionDto?> GetCurrentAsync(string installationId, CancellationToken cancellationToken);
    Task<CashierTillSessionDto> OpenAsync(MobilePosOpenTillSessionRequestDto dto, CancellationToken cancellationToken);
}

/// <summary>
/// Binds the existing Finance custody service to the authenticated mobile device, store and till.
/// It does not maintain another till balance or bypass Finance ownership and concurrency controls.
/// </summary>
public sealed class MobilePosTillSessionService : IMobilePosTillSessionService
{
    private readonly IMobilePosFoundationService _foundation;
    private readonly ICashierTillService _cashierTills;

    public MobilePosTillSessionService(
        IMobilePosFoundationService foundation,
        ICashierTillService cashierTills)
    {
        _foundation = foundation;
        _cashierTills = cashierTills;
    }

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
