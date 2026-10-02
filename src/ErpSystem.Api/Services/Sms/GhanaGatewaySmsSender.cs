using Microsoft.Extensions.Options;

namespace ErpSystem.Api.Services.Sms;

public sealed class GhanaGatewaySmsSender : ISmsSender
{
    private readonly HttpClient _httpClient;
    private readonly SmsOptions _options;
    private readonly ILogger<GhanaGatewaySmsSender> _logger;

    public GhanaGatewaySmsSender(HttpClient httpClient, IOptions<SmsOptions> options, ILogger<GhanaGatewaySmsSender> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(string toPhoneNumber, string message, CancellationToken cancellationToken = default)
    {
        var gw = _options.GhanaGateway;
        if (gw.Enabled != true)
        {
            throw new InvalidOperationException("mNotify SMS is not enabled.");
        }

        _logger.LogInformation("[SMS:mNotify] Sending to {To}", Mask(toPhoneNumber));
        await MNotifySmsGateway.SendAsync(
            _httpClient,
            gw,
            toPhoneNumber,
            message,
            isOtp: false,
            cancellationToken: cancellationToken);
    }

    private static string Mask(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return "Unknown";
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.Length <= 4) return "***";
        return $"***{digits[^4..]}";
    }
}

