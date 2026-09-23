using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Common;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Interfaces.HR.Services;
using ErpSystem.Core.Services.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR.Templates;

/// <summary>
/// HR's letter and email templates (round 4, lane N) — see <see cref="IHrLetterTemplateService"/>.
/// </summary>
/// <remarks>
/// <para><b>Listed from the catalogues, stored only when edited.</b> The plan (N2) proposed seeding a
/// row per template on every tenant at startup, so a fresh tenant would have something to edit. It
/// would have — and it would also have frozen each template's wording on the day it was seeded:
/// <c>TemplatedEmailService</c> prefers a stored row, so an improvement to a shipped default would never
/// reach a seeded tenant (lane K-b rewrote twelve of them in one day). So the list comes from the
/// modules' own catalogues — every template, on any tenant, at once — and a row exists only for the
/// wording a tenant chose. Reset removes it and the shipped default goes out again. The startup seeder
/// stays deferred, as <c>HrSeedOrchestrator</c> already had it; the sender now ignores an untouched
/// seeded copy anyway.</para>
///
/// <para><b>A save is checked the way a send cannot be.</b> The renderer forgives everything at send
/// time — an unknown token prints nothing, an unclosed <c>{{#if}}</c> swallows the rest of the letter
/// whenever its condition is empty — which is right when a candidate is waiting and wrong when an
/// author is typing. So a save is refused, with every reason, for a broken structure, a token the
/// event does not supply, or <c>{{{Token}}}</c> (unescaped) on anything but the ready-made HTML the
/// system builds itself: raw, a name somebody typed could become markup in the email.</para>
/// </remarks>
public sealed class HrLetterTemplateService : IHrLetterTemplateService
{
    /// <summary>Supplied to every template by <c>TemplatedEmailService</c> itself.</summary>
    private static readonly (string Token, string Description)[] CommonTokens =
    {
        ("CompanyName", "The employer's legal name, from the company profile"),
        ("PortalUrl", "The candidate careers portal's address"),
    };

    private static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(10);

    private readonly IEnumerable<IEmailEventCatalog> _catalogs;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmailTemplateRenderer _renderer;
    private readonly ITemplatedEmailService _templatedEmail;
    private readonly IEmailService _email;
    private readonly ICompanyProfileProvider _companyProfile;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<HrLetterTemplateService> _logger;

    public HrLetterTemplateService(
        IEnumerable<IEmailEventCatalog> catalogs,
        IUnitOfWork unitOfWork,
        IEmailTemplateRenderer renderer,
        ITemplatedEmailService templatedEmail,
        IEmailService email,
        ICompanyProfileProvider companyProfile,
        ICurrentUserService currentUser,
        ILogger<HrLetterTemplateService> logger)
    {
        _catalogs = catalogs;
        _unitOfWork = unitOfWork;
        _renderer = renderer;
        _templatedEmail = templatedEmail;
        _email = email;
        _companyProfile = companyProfile;
        _currentUser = currentUser;
        _logger = logger;
    }

    private Guid TenantId() => _currentUser.TenantId is { } tenantId && tenantId != Guid.Empty
        ? tenantId
        : throw new InvalidOperationException("No tenant is associated with the current user.");

    private string Who() => string.IsNullOrWhiteSpace(_currentUser.FullName) ? _currentUser.UserName ?? "HR" : _currentUser.FullName;

    private IEnumerable<EmailEventDescriptor> Descriptors => _catalogs.SelectMany(c => c.Events);

    private EmailEventDescriptor Find(string module, string eventKey) =>
        Descriptors.FirstOrDefault(d => string.Equals(d.Module, module, StringComparison.OrdinalIgnoreCase)
                                        && string.Equals(d.EventKey, eventKey, StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentException($"There is no HR email '{module}/{eventKey}'.");

    /// <summary>
    /// The same rule the sender applies: an untouched seeded copy is not the tenant's choice. ⚠ Edited is
    /// read from <c>UpdatedBy</c> — <c>SaveChanges</c> stamps <c>UpdatedAt</c> on insert, so every seeded
    /// row has one (see <c>TemplatedEmailService</c>).
    /// </summary>
    private static bool IsChosen(EmailTemplate t) => t.IsActive && (!t.IsSystemDefault || t.UpdatedBy != null);

    private async Task<Dictionary<(string Module, string EventKey), EmailTemplate>> ChosenAsync(Guid tenantId, CancellationToken ct)
    {
        var rows = await _unitOfWork.Repository<EmailTemplate>().GetQueryable().AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.EventKey != null)
            .ToListAsync(ct);
        return rows.Where(IsChosen)
            .GroupBy(t => (t.Module.ToLowerInvariant(), t.EventKey!.ToLowerInvariant()))
            .ToDictionary(g => g.Key, g => g.OrderByDescending(t => t.UpdatedAt ?? t.CreatedAt).First());
    }

    private static EmailTemplate? ChosenFor(Dictionary<(string Module, string EventKey), EmailTemplate> chosen, EmailEventDescriptor d)
        => chosen.TryGetValue((d.Module.ToLowerInvariant(), d.EventKey.ToLowerInvariant()), out var row) ? row : null;

    // ── Reads ─────────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<HrLetterTemplateSummaryDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var chosen = await ChosenAsync(TenantId(), cancellationToken);
        return Descriptors
            .Select(d => Fill(new HrLetterTemplateSummaryDto(), d, ChosenFor(chosen, d)))
            .OrderBy(s => s.Category).ThenBy(s => s.Name)
            .ToList();
    }

    public async Task<HrLetterTemplateDto> GetAsync(string module, string eventKey, CancellationToken cancellationToken = default)
    {
        var d = Find(module, eventKey);
        var row = ChosenFor(await ChosenAsync(TenantId(), cancellationToken), d);
        var dto = Fill(new HrLetterTemplateDto(), d, row);
        dto.Subject = row?.Subject ?? d.DefaultSubject;
        dto.HtmlBody = row?.HtmlBody ?? d.DefaultHtmlBody;
        dto.DefaultSubject = d.DefaultSubject;
        dto.DefaultHtmlBody = d.DefaultHtmlBody;
        var raw = RawAllowed(d);
        dto.Tokens = d.Tokens.Select(t => new HrLetterTemplateTokenDto
        {
            Token = t.Token, Description = t.Description, SampleValue = t.SampleValue, MayBeRaw = raw.Contains(t.Token),
        }).ToList();
        dto.CommonTokens = CommonTokens
            .Where(c => !d.Tokens.Any(t => string.Equals(t.Token, c.Token, StringComparison.OrdinalIgnoreCase)))
            .Select(c => new HrLetterTemplateTokenDto { Token = c.Token, Description = c.Description, SampleValue = string.Empty })
            .ToList();
        return dto;
    }

    private static T Fill<T>(T dto, EmailEventDescriptor d, EmailTemplate? row) where T : HrLetterTemplateSummaryDto
    {
        dto.Module = d.Module;
        dto.EventKey = d.EventKey;
        dto.Name = d.Name;
        dto.Category = d.Category;
        dto.Description = d.Description;
        dto.TokenCount = d.Tokens.Count;
        dto.State = row is null ? "Default" : "Edited";
        dto.MatchesDefault = row is not null && Same(row.Subject, d.DefaultSubject) && Same(row.HtmlBody, d.DefaultHtmlBody);
        dto.EditedAt = row is null ? null : AsUtc(row.UpdatedAt ?? row.CreatedAt);
        dto.EditedBy = row is null ? null : row.UpdatedBy ?? row.CreatedBy;
        return dto;
    }

    /// <summary>
    /// ⚠ A datetime2 comes back unspecified, and JSON then drops its Z — a browser would read an
    /// edit made at 14:00 UTC as 14:00 local (the round-4 lane E lesson).
    /// </summary>
    private static DateTime AsUtc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static bool Same(string? a, string? b) =>
        string.Equals((a ?? string.Empty).ReplaceLineEndings("\n").Trim(), (b ?? string.Empty).ReplaceLineEndings("\n").Trim(), StringComparison.Ordinal);

    // ── Checks ────────────────────────────────────────────────────────────────

    /// <summary>
    /// The tokens the catalogue marks as ready-made HTML the system builds (<c>IsHtml</c>). ⚠ Not "the
    /// ones the shipped default places raw": the offer letter's <c>ConditionsList</c> and the score
    /// sheet's <c>PanelTable</c> are built as HTML and supplied on every render, but no default places
    /// them — so that rule refused HR the raw form, and the escaped form printed the markup as text.
    /// </summary>
    private static HashSet<string> RawAllowed(EmailEventDescriptor d) =>
        d.Tokens.Where(t => t.IsHtml).Select(t => t.Token).ToHashSet(StringComparer.OrdinalIgnoreCase);

    private List<string> Problems(EmailEventDescriptor d, string? subject, string? body)
    {
        var problems = new List<string>();
        if (string.IsNullOrWhiteSpace(subject)) problems.Add("The subject is empty.");
        else if (subject.Length > 200) problems.Add($"The subject is {subject.Length} characters; the most is 200.");
        if (string.IsNullOrWhiteSpace(body)) problems.Add("The body is empty.");

        problems.AddRange(_renderer.FindProblems(subject ?? string.Empty).Select(p => $"Subject: {p}"));
        problems.AddRange(_renderer.FindProblems(body ?? string.Empty).Select(p => $"Body: {p}"));

        var known = d.Tokens.Select(t => t.Token).Concat(CommonTokens.Select(c => c.Token))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var used = _renderer.ExtractTokenNames(subject ?? string.Empty)
            .Concat(_renderer.ExtractTokenNames(body ?? string.Empty))
            .Concat(_renderer.ExtractRawTokenNames(subject ?? string.Empty))
            .Concat(_renderer.ExtractRawTokenNames(body ?? string.Empty))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var unknown = used.Where(u => !known.Contains(u)).ToList();
        if (unknown.Count > 0)
            problems.Add($"{string.Join(", ", unknown.Select(u => "{{" + u + "}}"))} "
                         + (unknown.Count == 1 ? "is not something this email supplies, so it would print nothing."
                                               : "are not things this email supplies, so they would print nothing.")
                         + $" It supplies: {string.Join(", ", known.OrderBy(k => k))}.");

        var rawAllowed = RawAllowed(d);
        var unsafeRaw = _renderer.ExtractRawTokenNames(subject ?? string.Empty)
            .Concat(_renderer.ExtractRawTokenNames(body ?? string.Empty))
            .Where(r => known.Contains(r) && !rawAllowed.Contains(r))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        foreach (var r in unsafeRaw)
            problems.Add($"{{{{{{{r}}}}}}} places {r} without escaping it, so anything typed into it could become markup in the email. Use {{{{{r}}}}}.");

        return problems;
    }

    // ── Writes ────────────────────────────────────────────────────────────────

    public async Task<HrLetterTemplateDto> SaveAsync(
        string module, string eventKey, SaveHrLetterTemplateDto dto, CancellationToken cancellationToken = default)
    {
        var d = Find(module, eventKey);
        var tenantId = TenantId();
        var problems = Problems(d, dto.Subject, dto.HtmlBody);
        if (problems.Count > 0)
            throw new InvalidOperationException("This template cannot be saved. " + string.Join(" ", problems));

        var repository = _unitOfWork.Repository<EmailTemplate>();
        // Deleted rows included: a reset soft-deletes the row, and (TenantId, Name) is unique whether
        // or not a row is deleted — so an edit after a reset revives that row rather than colliding with it.
        var row = await repository.GetQueryableIncludingDeleted(t => t.TenantId == tenantId && t.Module == d.Module && t.EventKey == d.EventKey)
            .OrderBy(t => t.IsDeleted).ThenByDescending(t => t.UpdatedAt ?? t.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var now = DateTime.UtcNow;
        if (row is null)
        {
            row = new EmailTemplate
            {
                TenantId = tenantId,
                Module = d.Module,
                EventKey = d.EventKey,
                Name = await FreeNameAsync(tenantId, d, cancellationToken),
                Category = Clip(d.Category, 50),
                Description = Clip(d.Description, 500),
                IsActive = true,
                IsSystemDefault = false,
                Subject = dto.Subject.Trim(),
                HtmlBody = dto.HtmlBody,
                CreatedBy = Who(),
            };
            await repository.AddAsync(row);
        }
        else
        {
            row.IsDeleted = false;
            row.DeletedAt = null;
            row.DeletedBy = null;
            row.IsActive = true;
            row.IsSystemDefault = false;
            row.Subject = dto.Subject.Trim();
            row.HtmlBody = dto.HtmlBody;
            row.UpdatedAt = now;
            row.UpdatedBy = Who();
            await repository.UpdateAsync(row);
        }
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("HR email {Module}/{EventKey} reworded by {Who}", d.Module, d.EventKey, Who());
        return await GetAsync(module, eventKey, cancellationToken);
    }

    public async Task<HrLetterTemplateDto> ResetAsync(string module, string eventKey, CancellationToken cancellationToken = default)
    {
        var d = Find(module, eventKey);
        var tenantId = TenantId();
        var repository = _unitOfWork.Repository<EmailTemplate>();
        var rows = await repository.GetQueryable()
            .Where(t => t.TenantId == tenantId && t.Module == d.Module && t.EventKey == d.EventKey)
            .ToListAsync(cancellationToken);
        foreach (var row in rows)
        {
            row.DeletedBy = Who();
            await repository.DeleteAsync(row);
        }
        if (rows.Count > 0) await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("HR email {Module}/{EventKey} reset to its shipped default by {Who}", d.Module, d.EventKey, Who());
        return await GetAsync(module, eventKey, cancellationToken);
    }

    /// <summary>The event's own name, unless some other template of the tenant already holds it.</summary>
    private async Task<string> FreeNameAsync(Guid tenantId, EmailEventDescriptor d, CancellationToken ct)
    {
        var repository = _unitOfWork.Repository<EmailTemplate>();
        foreach (var candidate in new[] { d.Name, $"{d.Name} ({d.Category})", $"{d.Name} ({d.Module}/{d.EventKey})" })
        {
            var name = Clip(candidate, 100);
            if (!await repository.GetQueryableIncludingDeleted(t => t.TenantId == tenantId && t.Name == name).AnyAsync(ct))
                return name;
        }
        return Clip($"{d.Module}/{d.EventKey}/{Guid.NewGuid():N}", 100);
    }

    private static string Clip(string value, int max) => value.Length <= max ? value : value[..max];

    // ── Preview and test ──────────────────────────────────────────────────────

    public async Task<HrLetterTemplatePreviewDto> PreviewAsync(
        string module, string eventKey, PreviewHrLetterTemplateDto dto, CancellationToken cancellationToken = default)
    {
        var d = Find(module, eventKey);
        var current = await GetAsync(module, eventKey, cancellationToken);
        var subject = dto.Subject ?? current.Subject;
        var body = dto.HtmlBody ?? current.HtmlBody;

        var tokens = d.Tokens.ToDictionary(t => t.Token, t => (string?)t.SampleValue, StringComparer.OrdinalIgnoreCase);
        try
        {
            var legalName = (await _companyProfile.GetAsync(cancellationToken)).LegalName;
            if (!string.IsNullOrWhiteSpace(legalName)) tokens["CompanyName"] = legalName;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "Company profile unavailable for the preview; the catalogue's sample name is used.");
        }

        var rendered = _templatedEmail.RenderInline(subject, body, tokens);
        return new HrLetterTemplatePreviewDto
        {
            Subject = rendered.Subject,
            HtmlBody = rendered.HtmlBody,
            Problems = Problems(d, subject, body),
        };
    }

    public async Task<HrLetterTemplateTestSendResultDto> TestSendAsync(
        string module, string eventKey, PreviewHrLetterTemplateDto dto, CancellationToken cancellationToken = default)
    {
        var preview = await PreviewAsync(module, eventKey, dto, cancellationToken);
        if (preview.Problems.Count > 0)
            throw new InvalidOperationException("Fix the template before sending a test. " + string.Join(" ", preview.Problems));

        var subject = $"[Test] {preview.Subject}";
        var to = await CallerAddressAsync(cancellationToken);
        if (to is null)
            return new HrLetterTemplateTestSendResultDto { Outcome = "NoAddress", Subject = subject };
        if (!await MailServerConfiguredAsync())
            return new HrLetterTemplateTestSendResultDto { Outcome = "NoMailServer", SentTo = to, Subject = subject };

        string outcome;
        try
        {
            var send = _email.SendEmailAsync(new EmailDto { To = to, Subject = subject, Body = preview.HtmlBody, IsHtml = true });
            outcome = await Task.WhenAny(send, Task.Delay(SendTimeout)) != send ? "TimedOut" : await send ? "Sent" : "Failed";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Test email {Module}/{EventKey} to {To} failed.", module, eventKey, to);
            outcome = "Failed";
        }
        return new HrLetterTemplateTestSendResultDto { Outcome = outcome, SentTo = to, Subject = subject };
    }

    /// <summary>The signed-in officer's own address — their employee record's, else their account's. Nobody else's.</summary>
    private async Task<string?> CallerAddressAsync(CancellationToken ct)
    {
        if (_currentUser.EmployeeId is { } employeeId)
        {
            var address = await _unitOfWork.Repository<Employee>().GetQueryable().AsNoTracking()
                .Where(e => e.Id == employeeId)
                .Select(e => e.EmailAddress)
                .FirstOrDefaultAsync(ct);
            if (!string.IsNullOrWhiteSpace(address)) return address;
        }
        return string.IsNullOrWhiteSpace(_currentUser.Email) ? null : _currentUser.Email;
    }

    /// <summary>Asked the way <c>SettingsService</c> answers it: the first mail settings row, with a host and a from address.</summary>
    private async Task<bool> MailServerConfiguredAsync()
    {
        var settings = await _unitOfWork.Repository<EmailSettings>().FirstOrDefaultAsync(e => true);
        return settings is not null && !string.IsNullOrWhiteSpace(settings.SmtpHost) && !string.IsNullOrWhiteSpace(settings.FromAddress);
    }
}
