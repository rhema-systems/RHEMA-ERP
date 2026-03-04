using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Entities.Ehc;

namespace ErpSystem.Core.DTOs.Ehc;

public sealed class EhcServiceRequestTypeDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public string? WorkflowName { get; set; }
    public string FormDefinitionJson { get; set; } = "{}";
}

public sealed class UpsertEhcServiceRequestTypeRequestDto
{
    [Required]
    [StringLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    [StringLength(200)]
    public string? WorkflowName { get; set; }

    [Required]
    public string FormDefinitionJson { get; set; } = "{}";
}

public sealed class EhcServiceRequestSummaryDto
{
    public Guid Id { get; set; }
    public string RequestNumber { get; set; } = string.Empty;
    public string RequestTypeName { get; set; } = string.Empty;
    public EhcServiceRequestStatus Status { get; set; }
    public string? Title { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }
}

public sealed class EhcServiceRequestDetailDto
{
    public Guid Id { get; set; }
    public string RequestNumber { get; set; } = string.Empty;
    public Guid RequestTypeId { get; set; }
    public string RequestTypeName { get; set; } = string.Empty;
    public EhcServiceRequestStatus Status { get; set; }
    public string? Title { get; set; }
    public string FormDefinitionJson { get; set; } = "{}";
    public string FormDataJson { get; set; } = "{}";
    public Guid? WorkflowInstanceId { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public DateTime? RejectedAtUtc { get; set; }
    public string? RejectionReason { get; set; }

    public List<EhcServiceRequestAttachmentDto> Attachments { get; set; } = new();
}

public sealed class CreateEhcServiceRequestRequestDto
{
    [Required]
    public Guid RequestTypeId { get; set; }

    [StringLength(200)]
    public string? Title { get; set; }

    [Required]
    public string FormDataJson { get; set; } = "{}";

    public string? CaptchaToken { get; set; }
}

public sealed class EhcServiceRequestAttachmentDto
{
    public Guid Id { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string? PublicUrl { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public long FileSize { get; set; }
    public bool IsInternal { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class AddEhcServiceRequestAttachmentRequestDto
{
    [Required]
    public string FilePath { get; set; } = string.Empty;

    [Required]
    public string FileName { get; set; } = string.Empty;

    public string? ContentType { get; set; }
    public long FileSize { get; set; }
    public bool IsInternal { get; set; } = false;
}
