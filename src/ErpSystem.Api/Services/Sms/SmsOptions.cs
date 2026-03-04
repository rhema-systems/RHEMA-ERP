using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Api.Services.Sms;

public sealed class SmsOptions
{
    public string DefaultProvider { get; set; } = "Twilio";
    public List<string> FallbackProviders { get; set; } = new();

    public TwilioSmsOptions Twilio { get; set; } = new();
    public GhanaGatewaySmsOptions GhanaGateway { get; set; } = new();
}

public sealed class TwilioSmsOptions
{
    public bool Enabled { get; set; } = false;

    public string? AccountSid { get; set; }
    public string? AuthToken { get; set; }

    /// <summary>
    /// E.164 number to send from (e.g. +1XXXXXXXXXX)
    /// </summary>
    public string? FromNumber { get; set; }
}

public sealed class GhanaGatewaySmsOptions
{
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// GET URL template with placeholders:
    /// {to}, {message}, {senderId}, {apiKey}
    /// Example:
    /// https://gateway.example/send?to={to}&from={senderId}&msg={message}&key={apiKey}
    /// </summary>
    public string? UrlTemplate { get; set; }

    public string? ApiKey { get; set; }
    public string? SenderId { get; set; }

    [Range(1, 60)]
    public int TimeoutSeconds { get; set; } = 10;
}

