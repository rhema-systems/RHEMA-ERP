namespace ErpSystem.Api.Services.Sms;

public interface ITenantSmsSender
{
    Task SendAsync(Guid tenantId, string toPhoneNumber, string message, CancellationToken cancellationToken = default);
}

