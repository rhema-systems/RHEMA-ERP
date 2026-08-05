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
public sealed class TemplatedEmailService : ITemplatedEmailService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmailTemplateRenderer _renderer;
    private readonly IEmailService _email;
    private readonly IEnumerable<IEmailEventCatalog> _catalogs;
    private readonly IConfiguration _configuration;
    private readonly ICompanyProfileProvider _companyProfile;
    private readonly ILogger<TemplatedEmailService> _logger;

    public TemplatedEmailService(
        IUnitOfWork unitOfWork,
        IEmailTemplateRenderer renderer,
        IEmailService email,
        IEnumerable<IEmailEventCatalog> catalogs,
        IConfiguration configuration,
        ICompanyProfileProvider companyProfile,
        ILogger<TemplatedEmailService> logger)
    {
        _unitOfWork = unitOfWork;
        _renderer = renderer;
        _email = email;
        _catalogs = catalogs;
        _configuration = configuration;
        _companyProfile = companyProfile;
        _logger = logger;
    }

    public async Task<bool> SendAsync(
        string module,
        string eventKey,
        string to,
        IReadOnlyDictionary<string, string?> tokens,
        IReadOnlyList<EmailAttachmentDto>? attachments = null,
        IReadOnlyList<string>? cc = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(to)) return false;

        try
        {
            var rendered = await RenderAsync(module, eventKey, tokens, cancellationToken);
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

    public async Task<RenderedEmail?> RenderAsync(
        string module,
        string eventKey,
        IReadOnlyDictionary<string, string?> tokens,
        CancellationToken cancellationToken = default)
    {
        var (subject, body) = await ResolveAsync(module, eventKey);
        if (subject is null || body is null) return null;

        // Resolve the per-tenant company name for the letterhead (config as last-resort fallback).
        string? companyName = null;
        try { companyName = (await _companyProfile.GetAsync(cancellationToken)).LegalName; }
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
    private async Task<(string? Subject, string? Body)> ResolveAsync(string module, string eventKey)
    {
        // Prefer an active stored template (tenant-scoped by the global query filter).
        var stored = (await _unitOfWork.Repository<EmailTemplate>()
                .FindAsync(t => t.Module == module && t.EventKey == eventKey && t.IsActive))
            .OrderByDescending(t => t.UpdatedAt ?? t.CreatedAt)
            .FirstOrDefault();

        if (stored is not null)
            return (stored.Subject, stored.HtmlBody);

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
