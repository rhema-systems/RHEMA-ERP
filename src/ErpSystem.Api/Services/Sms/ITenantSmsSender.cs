namespace ErpSystem.Api.Services.Sms;

public interface ITenantSmsSender
{
    Task SendAsync(Guid tenantId, string toPhoneNumber, string message, CancellationToken cancellationToken = default);
    Task SendOtpAsync(Guid tenantId, string toPhoneNumber, string message, CancellationToken cancellationToken = default);
    Task<MNotifySmsBalance> GetMNotifyBalanceAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

public sealed record MNotifySmsBalance(decimal Balance, decimal Bonus);

