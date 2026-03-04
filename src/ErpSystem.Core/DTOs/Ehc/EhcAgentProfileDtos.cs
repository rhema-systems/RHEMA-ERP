using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.Ehc;

public sealed class EhcAgentReplyProfileDto
{
    public string? Signature { get; set; }
    public bool IsSignatureEnabled { get; set; }
    public bool AppendSignatureToReplies { get; set; }
}

public sealed class UpdateEhcAgentReplyProfileRequestDto
{
    [StringLength(2000)]
    public string? Signature { get; set; }

    public bool IsSignatureEnabled { get; set; } = true;
    public bool AppendSignatureToReplies { get; set; } = true;
}

