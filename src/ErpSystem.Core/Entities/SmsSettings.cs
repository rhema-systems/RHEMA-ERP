using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ErpSystem.Core.Entities;

[Table("SmsSettings")]
public class SmsSettings : BaseEntity
{
    public Guid TenantId { get; set; }
    public virtual Tenant Tenant { get; set; } = null!;

    [StringLength(50)]
    public string DefaultProvider { get; set; } = "GhanaGateway";

    /// <summary>
    /// Optional JSON array of fallback providers (e.g. ["GhanaGateway"]).
    /// </summary>
    public string? FallbackProvidersJson { get; set; }

    // Twilio
    public bool TwilioEnabled { get; set; } = false;

    [StringLength(80)]
    public string? TwilioAccountSid { get; set; }

    [StringLength(500)]
    public string? TwilioAuthToken { get; set; }

    [StringLength(40)]
    public string? TwilioFromNumber { get; set; }

    // mNotify (legacy column names retained for database compatibility)
    public bool GhanaGatewayEnabled { get; set; } = false;

    [StringLength(1000)]
    public string? GhanaGatewayUrlTemplate { get; set; }

    [StringLength(500)]
    public string? GhanaGatewayApiKey { get; set; }

    [StringLength(50)]
    public string? GhanaGatewaySenderId { get; set; }

    public int GhanaGatewayTimeoutSeconds { get; set; } = 10;
}

