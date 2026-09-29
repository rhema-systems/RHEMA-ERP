using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.HR;

// ============================================================================
// HR LETTER & EMAIL TEMPLATES (round 4, lane N)
// ============================================================================
//
// Every email the HR modules send, listed from the modules' own catalogues — not from stored rows —
// so a new tenant sees all of them at once. A row is written only when HR edits one; resetting it
// brings the shipped default back.

/// <summary>One merge field an event supplies.</summary>
public class HrLetterTemplateTokenDto
{
    public string Token { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string SampleValue { get; set; } = string.Empty;

    /// <summary>
    /// Supplied as ready-made HTML the system builds itself (a salary table, say), so it may be placed
    /// with <c>{{{Token}}}</c>. Every other token is text and is always escaped.
    /// </summary>
    public bool MayBeRaw { get; set; }
}

public class HrLetterTemplateSummaryDto
{
    public string Module { get; set; } = string.Empty;
    public string EventKey { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    /// <summary><c>Default</c> — the shipped wording is what goes out; <c>Edited</c> — the tenant's own.</summary>
    public string State { get; set; } = "Default";

    /// <summary>Edited, but still word for word the shipped default (saved without a change).</summary>
    public bool MatchesDefault { get; set; }

    public DateTime? EditedAt { get; set; }
    public string? EditedBy { get; set; }
    public int TokenCount { get; set; }
}

public class HrLetterTemplateDto : HrLetterTemplateSummaryDto
{
    /// <summary>What goes out now — the tenant's edit, or the shipped default.</summary>
    public string Subject { get; set; } = string.Empty;
    public string HtmlBody { get; set; } = string.Empty;

    public string DefaultSubject { get; set; } = string.Empty;
    public string DefaultHtmlBody { get; set; } = string.Empty;

    public List<HrLetterTemplateTokenDto> Tokens { get; set; } = new();

    /// <summary>Tokens every template can use, whatever its event: the company name, the portal link.</summary>
    public List<HrLetterTemplateTokenDto> CommonTokens { get; set; } = new();
}

public class SaveHrLetterTemplateDto
{
    [Required]
    [MaxLength(200)]
    public string Subject { get; set; } = string.Empty;

    [Required]
    public string HtmlBody { get; set; } = string.Empty;
}

/// <summary>Unsaved editor contents to preview; null for either means the current wording.</summary>
public class PreviewHrLetterTemplateDto
{
    public string? Subject { get; set; }
    public string? HtmlBody { get; set; }
}

public class HrLetterTemplatePreviewDto
{
    public string Subject { get; set; } = string.Empty;
    public string HtmlBody { get; set; } = string.Empty;

    /// <summary>What saving it would be refused for — shown as the author types, not only on save.</summary>
    public List<string> Problems { get; set; } = new();
}

public class HrLetterTemplateTestSendResultDto
{
    /// <summary><c>Sent</c>, <c>NoMailServer</c>, <c>NoAddress</c> or <c>Failed</c> — what happened, not what was hoped.</summary>
    public string Outcome { get; set; } = string.Empty;
    public string? SentTo { get; set; }
    public string Subject { get; set; } = string.Empty;
}
