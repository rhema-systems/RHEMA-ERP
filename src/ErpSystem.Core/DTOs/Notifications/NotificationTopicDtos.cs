namespace ErpSystem.Core.DTOs.Notifications;

public class NotificationTopicDto
{
    public Guid Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string? Activity { get; set; }
    public string? Audience { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? EntityType { get; set; }
    public bool IsSystem { get; set; }
    public bool IsRequired { get; set; }
    public bool IsActive { get; set; }

    public bool EnableInApp { get; set; }
    public bool EnableEmail { get; set; }

    public string? InAppTitleTemplate { get; set; }
    public string? InAppBodyTemplate { get; set; }

    public Guid? EmailTemplateId { get; set; }
    public string? ActionUrlTemplate { get; set; }

    public List<NotificationTopicRecipientDto> Recipients { get; set; } = new();
}

public class NotificationTopicRecipientDto
{
    public Guid Id { get; set; }
    public string RecipientKind { get; set; } = string.Empty;
    public string RecipientValue { get; set; } = string.Empty;
    public bool IsSystem { get; set; }
    public bool SendInApp { get; set; }
    public bool SendEmail { get; set; }
}

public class CreateNotificationTopicDto
{
    // Key is system-controlled. Prefer providing EntityType + Activity + Audience so the API can generate the Key.
    public string Key { get; set; } = string.Empty;
    public string? Activity { get; set; }
    public string? Audience { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? EntityType { get; set; }
    public bool IsActive { get; set; } = true;

    public bool EnableInApp { get; set; } = true;
    public bool EnableEmail { get; set; } = false;

    public string? InAppTitleTemplate { get; set; }
    public string? InAppBodyTemplate { get; set; }

    public Guid? EmailTemplateId { get; set; }
    public string? ActionUrlTemplate { get; set; }

    public List<CreateNotificationTopicRecipientDto> Recipients { get; set; } = new();
}

public class CreateNotificationTopicRecipientDto
{
    public string RecipientKind { get; set; } = "Role";
    public string RecipientValue { get; set; } = string.Empty;
    public bool SendInApp { get; set; } = true;
    public bool SendEmail { get; set; } = false;
}

public class UpdateNotificationTopicDto : CreateNotificationTopicDto
{
}
