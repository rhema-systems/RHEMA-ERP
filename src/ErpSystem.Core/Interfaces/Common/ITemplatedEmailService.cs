using ErpSystem.Core.DTOs.Common;

namespace ErpSystem.Core.Interfaces.Common;

/// <summary>
/// Renders a stored (or built-in default) email template for a code event and sends it.
/// This is the single path all transactional module emails should flow through, replacing the
/// per-service hardcoded inline HTML bodies.
/// </summary>
public interface ITemplatedEmailService
{
    /// <summary>
    /// Resolves the active template for <paramref name="module"/>+<paramref name="eventKey"/> (falling
    /// back to the shipped built-in default when none exists), merges <paramref name="tokens"/> into it
    /// and sends it to <paramref name="to"/>. Never throws for a send failure — returns false and logs.
    /// </summary>
    Task<bool> SendAsync(
        string module,
        string eventKey,
        string to,
        IReadOnlyDictionary<string, string?> tokens,
        IReadOnlyList<EmailAttachmentDto>? attachments = null,
        IReadOnlyList<string>? cc = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Renders the subject + HTML body for a module/event without sending — used by the preview and
    /// test-send endpoints. Uses the stored template if present, otherwise the built-in default.
    /// Returns null when neither a stored template nor a built-in default exists.
    /// </summary>
    Task<RenderedEmail?> RenderAsync(
        string module,
        string eventKey,
        IReadOnlyDictionary<string, string?> tokens,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Renders arbitrary subject/body strings (e.g. the unsaved editor contents) against sample token
    /// values so HR can preview edits before saving.
    /// </summary>
    RenderedEmail RenderInline(
        string subjectTemplate,
        string bodyTemplate,
        IReadOnlyDictionary<string, string?> tokens);
}

/// <summary>
/// Supplies the built-in email event descriptors for one module. Each module (Recruitment, Training,
/// Appraisal, …) registers one implementation; <see cref="ITemplatedEmailService"/> aggregates them
/// to resolve fallbacks, seed defaults, and drive the token catalogue.
/// </summary>
public interface IEmailEventCatalog
{
    string Module { get; }
    IReadOnlyList<EmailEventDescriptor> Events { get; }
}

/// <summary>A rendered subject + HTML body pair.</summary>
public sealed class RenderedEmail
{
    public string Subject { get; set; } = string.Empty;
    public string HtmlBody { get; set; } = string.Empty;
}

/// <summary>Describes a single merge token available to a template (for authoring aids / preview).</summary>
public sealed class EmailTokenDescriptor
{
    public string Token { get; set; } = string.Empty;        // e.g. "CandidateName"
    public string Description { get; set; } = string.Empty;  // human-friendly explanation
    public string SampleValue { get; set; } = string.Empty;  // value used in preview / test-send

    public EmailTokenDescriptor() { }
    public EmailTokenDescriptor(string token, string description, string sampleValue)
    {
        Token = token;
        Description = description;
        SampleValue = sampleValue;
    }
}

/// <summary>
/// Describes a transactional email event: its stable key, the shipped default subject/body, and the
/// tokens available. Used by the seeder, the runtime fallback, and the token-catalogue API.
/// </summary>
public sealed class EmailEventDescriptor
{
    public string Module { get; set; } = string.Empty;
    public string EventKey { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;          // display name for the template row
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string DefaultSubject { get; set; } = string.Empty;
    public string DefaultHtmlBody { get; set; } = string.Empty;
    public List<EmailTokenDescriptor> Tokens { get; set; } = new();
}
