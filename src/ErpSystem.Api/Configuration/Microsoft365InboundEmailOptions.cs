using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Web.Configuration;

public sealed class Microsoft365InboundEmailOptions
{
    public const string SectionName = "Microsoft365InboundEmail";

    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Azure AD tenant id.
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// Azure AD application (client) id.
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// Azure AD application client secret.
    /// </summary>
    public string? ClientSecret { get; set; }

    [Range(10, 3600)]
    public int PollingIntervalSeconds { get; set; } = 60;

    [Range(1, 100)]
    public int PageSize { get; set; } = 25;

    [Range(1, 50)]
    public int MaxPagesPerPoll { get; set; } = 10;

    [Range(500, 200_000)]
    public int MaxMessageBodyChars { get; set; } = 20_000;

    public bool IngestAttachments { get; set; } = true;

    [Range(0, 50)]
    public int MaxAttachmentsPerMessage { get; set; } = 10;

    [Range(0, 50_000_000)]
    public long MaxAttachmentSizeBytes { get; set; } = 10 * 1024 * 1024; // 10MB default

    /// <summary>
    /// Optional public base URL for the API, used to build Graph subscription webhook URLs
    /// (e.g. https://api.example.com). Required only if enabling webhook subscriptions.
    /// </summary>
    public string? WebhookBaseUrl { get; set; }

    public bool IsConfigured =>
        Enabled &&
        !string.IsNullOrWhiteSpace(TenantId) &&
        !string.IsNullOrWhiteSpace(ClientId) &&
        !string.IsNullOrWhiteSpace(ClientSecret);
}
