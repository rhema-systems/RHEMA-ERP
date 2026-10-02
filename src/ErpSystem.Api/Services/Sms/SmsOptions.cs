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
    /// mNotify Quick SMS endpoint. The API key is appended as the key query parameter.
    /// </summary>
    public string? UrlTemplate { get; set; } = MNotifySmsGateway.DefaultEndpoint;

    public string? ApiKey { get; set; }
    public string? SenderId { get; set; }

    [Range(1, 60)]
    public int TimeoutSeconds { get; set; } = 10;
}

