using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Common;
using ErpSystem.Core.Interfaces.HR.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Common;

/// <summary>
/// Resolves a stored <see cref="EmailTemplate"/> for a module/event (falling back to the module's
/// built-in default from an <see cref="IEmailEventCatalog"/>), merges tokens via
/// <see cref="IEmailTemplateRenderer"/>, and sends through <see cref="IEmailService"/>.
/// </summary>
/// <remarks>
/// <para>⚠ <b>Whose wording (round 4, lane N).</b> The stored template used to be the first active row
/// for the module and event in ANY tenant: the comment claimed the tenant query filter scoped it, and
/// that filter is inert on this context (no tenant is ever set on it). One tenant's edited letter would
/// have gone out under every tenant's name the day a second tenant existed. It is now the SENDER'S
/// tenant's row — the signed-in user's, or the one a background sender names through
/// <see cref="SendForTenantAsync"/> — and with no tenant known, the shipped default, never somebody
/// else's.</para>
///
/// <para><b>An untouched seeded copy is not a choice.</b> A row the catalogue seeder wrote
/// (<c>IsSystemDefault</c>) and nobody has edited since is ignored in favour of the CURRENT shipped
/// default — otherwise a seeded copy freezes the wording of its day, and every later improvement to
/// that default reaches nobody who was seeded. "Edited since" is read from <c>UpdatedBy</c>, which only
/// an edit writes (the platform designer's, or the HR screen's, which also clears
/// <c>IsSystemDefault</c>). ⚠ Not <c>UpdatedAt</c>: <c>SaveChanges</c> stamps that on every INSERT, so a
/// seeded row carries one from the moment it exists.</para>
/// </remarks>
public sealed class TemplatedEmailService : ITemplatedEmailService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmailTemplateRenderer _renderer;
    private readonly IEmailService _email;
    private readonly IEnumerable<IEmailEventCatalog> _catalogs;
    private readonly IConfiguration _configuration;
    private readonly ICompanyProfileProvider _companyProfile;
    private readonly ICurrentUserProvider _currentUser;
    private readonly ILogger<TemplatedEmailService> _logger;

    public TemplatedEmailService(
        IUnitOfWork unitOfWork,
        IEmailTemplateRenderer renderer,
        IEmailService email,
        IEnumerable<IEmailEventCatalog> catalogs,
        IConfiguration configuration,
        ICompanyProfileProvider companyProfile,
        ICurrentUserProvider currentUser,
        ILogger<TemplatedEmailService> logger)
    {
        _unitOfWork = unitOfWork;
        _renderer = renderer;
        _email = email;
        _catalogs = catalogs;
        _configuration = configuration;
        _companyProfile = companyProfile;
        _currentUser = currentUser;
        _logger = logger;
    }

    /// <summary>The signed-in user's tenant, or null — a background scope has none, and may throw asking.</summary>
    private Guid? CurrentTenantOrNull()
    {
        try
        {
            var tenantId = _currentUser.TenantId;
            return tenantId == Guid.Empty ? null : tenantId;
        }
        catch
        {
            return null;
        }
    }

    public Task<bool> SendAsync(
        string module,
        string eventKey,
        string to,
        IReadOnlyDictionary<string, string?> tokens,
        IReadOnlyList<EmailAttachmentDto>? attachments = null,
        IReadOnlyList<string>? cc = null,
        CancellationToken cancellationToken = default)
        => SendCoreAsync(CurrentTenantOrNull(), module, eventKey, to, tokens, attachments, cc, cancellationToken);

    public Task<bool> SendForTenantAsync(
        Guid tenantId,
        string module,
        string eventKey,
        string to,
        IReadOnlyDictionary<string, string?> tokens,
        CancellationToken cancellationToken = default)
        => SendCoreAsync(tenantId == Guid.Empty ? null : tenantId, module, eventKey, to, tokens, null, null, cancellationToken);

    private async Task<bool> SendCoreAsync(
        Guid? tenantId,
        string module,
        string eventKey,
        string to,
        IReadOnlyDictionary<string, string?> tokens,
        IReadOnlyList<EmailAttachmentDto>? attachments,
        IReadOnlyList<string>? cc,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(to)) return false;

        try
        {
            var rendered = await RenderCoreAsync(tenantId, module, eventKey, tokens, cancellationToken);
            if (rendered is null)
            {
                _logger.LogWarning(
                    "No email template or built-in default found for {Module}/{EventKey}; nothing sent to {To}.",
                    module, eventKey, to);
                return false;
            }

            var dto = new EmailDto
            {
                To = to,
                Subject = rendered.Subject,
                Body = rendered.HtmlBody,
                IsHtml = true,
            };
            if (cc is { Count: > 0 }) dto.Cc = cc.ToList();
            if (attachments is { Count: > 0 }) dto.Attachments = attachments.ToList();

            return await _email.SendEmailAsync(dto);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to send templated email {Module}/{EventKey} to {To}", module, eventKey, to);
            return false;
        }
    }

    public Task<RenderedEmail?> RenderAsync(
        string module,
        string eventKey,
        IReadOnlyDictionary<string, string?> tokens,
        CancellationToken cancellationToken = default)
        => RenderCoreAsync(CurrentTenantOrNull(), module, eventKey, tokens, cancellationToken);

    private async Task<RenderedEmail?> RenderCoreAsync(
        Guid? tenantId,
        string module,
        string eventKey,
        IReadOnlyDictionary<string, string?> tokens,
        CancellationToken cancellationToken)
    {
        var (subject, body) = await ResolveAsync(tenantId, module, eventKey);
        if (subject is null || body is null) return null;

        // Resolve the per-tenant company name for the letterhead (config as last-resort fallback) — the
        // SENDER'S tenant, like the wording: a background or anonymous send has no signed-in user, and
        // asking the current user's profile there threw and fell back to config every time.
        string? companyName = null;
        try
        {
            if (tenantId is { } tenant)
                companyName = (await _companyProfile.GetForTenantAsync(tenant, cancellationToken)).LegalName;
        }
        catch (Exception ex) { _logger.LogDebug(ex, "Company profile lookup failed; using config company name."); }

        var merged = MergeCommonTokens(tokens, companyName);
        return new RenderedEmail
        {
            Subject = _renderer.Render(subject, merged, htmlEncode: false),
            HtmlBody = _renderer.Render(body, merged, htmlEncode: true),
        };
    }

    public RenderedEmail RenderInline(
        string subjectTemplate,
        string bodyTemplate,
        IReadOnlyDictionary<string, string?> tokens)
    {
        var merged = MergeCommonTokens(tokens);
        return new RenderedEmail
        {
            Subject = _renderer.Render(subjectTemplate ?? string.Empty, merged, htmlEncode: false),
            HtmlBody = _renderer.Render(bodyTemplate ?? string.Empty, merged, htmlEncode: true),
        };
    }

    // ── Helpers ─────────────────────────────────────────────────────────────
    private async Task<(string? Subject, string? Body)> ResolveAsync(Guid? tenantId, string module, string eventKey)
    {
        // The sender's tenant's own wording, if it chose any — see the class remarks for why this is
        // no longer "any tenant's", and why an untouched seeded copy does not count as a choice.
        if (tenantId is { } tenant)
        {
            var stored = (await _unitOfWork.Repository<EmailTemplate>()
                    .FindAsync(t => t.TenantId == tenant && t.Module == module && t.EventKey == eventKey && t.IsActive
                                    && (!t.IsSystemDefault || t.UpdatedBy != null)))
                .OrderByDescending(t => t.UpdatedAt ?? t.CreatedAt)
                .FirstOrDefault();

            if (stored is not null)
                return (stored.Subject, stored.HtmlBody);
        }

        // Fall back to the module's shipped built-in default so behaviour is safe pre-seed.
        var descriptor = _catalogs
            .Where(c => string.Equals(c.Module, module, StringComparison.OrdinalIgnoreCase))
            .SelectMany(c => c.Events)
            .FirstOrDefault(e => string.Equals(e.EventKey, eventKey, StringComparison.OrdinalIgnoreCase));

        return descriptor is null ? (null, null) : (descriptor.DefaultSubject, descriptor.DefaultHtmlBody);
    }

    private Dictionary<string, string?> MergeCommonTokens(
        IReadOnlyDictionary<string, string?> tokens, string? companyName = null)
    {
        var merged = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        // Common defaults (overridable by caller-supplied tokens).
        merged["PortalUrl"] = (_configuration["CandidatePortal:PortalUrl"] ?? "http://localhost:5085").TrimEnd('/');
        merged["CompanyName"] = (!string.IsNullOrWhiteSpace(companyName) ? companyName : null)
            ?? _configuration["Company:Name"]
            ?? _configuration["ApplicationName"]
            ?? "Our Company";

        if (tokens is not null)
            foreach (var kvp in tokens)
                merged[kvp.Key] = kvp.Value;

        return merged;
    }
}
