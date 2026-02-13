using Microsoft.Extensions.Options;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace ErpSystem.Api.Services.Sms;

public sealed class TwilioSmsSender : ISmsSender
{
    private readonly SmsOptions _options;
    private readonly ILogger<TwilioSmsSender> _logger;

    public TwilioSmsSender(IOptions<SmsOptions> options, ILogger<TwilioSmsSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(string toPhoneNumber, string message, CancellationToken cancellationToken = default)
    {
        var twilio = _options.Twilio;
        if (twilio.Enabled != true)
        {
            throw new InvalidOperationException("Twilio SMS is not enabled.");
        }

        if (string.IsNullOrWhiteSpace(twilio.AccountSid) ||
            string.IsNullOrWhiteSpace(twilio.AuthToken) ||
            string.IsNullOrWhiteSpace(twilio.FromNumber))
        {
            throw new InvalidOperationException("Twilio SMS is enabled but not configured (AccountSid/AuthToken/FromNumber).");
        }

        TwilioClient.Init(twilio.AccountSid, twilio.AuthToken);

        _logger.LogInformation("[SMS:Twilio] Sending to {To}", Mask(toPhoneNumber));

        var to = new PhoneNumber(toPhoneNumber);
        var from = new PhoneNumber(twilio.FromNumber);

        // Twilio SDK does not accept CancellationToken directly here; keep it best-effort.
        await MessageResource.CreateAsync(
            to: to,
            from: from,
            body: message);
    }

    private static string Mask(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return "Unknown";
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.Length <= 4) return "***";
        return $"***{digits[^4..]}";
    }
}

