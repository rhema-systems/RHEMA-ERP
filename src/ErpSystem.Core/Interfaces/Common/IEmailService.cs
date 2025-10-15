using ErpSystem.Core.DTOs.Common;

namespace ErpSystem.Core.Interfaces.Common;

/// <summary>
/// Interface for email service functionality
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Sends an email asynchronously
    /// </summary>
    /// <param name="email">Email details</param>
    /// <returns>True if email sent successfully</returns>
    Task<bool> SendEmailAsync(EmailDto email);
    
    /// <summary>
    /// Sends bulk emails asynchronously
    /// </summary>
    /// <param name="emails">List of emails to send</param>
    /// <returns>Number of emails sent successfully</returns>
    Task<int> SendBulkEmailsAsync(List<EmailDto> emails);
    
    /// <summary>
    /// Sends email using template
    /// </summary>
    /// <param name="templateEmail">Template email details</param>
    /// <returns>True if email sent successfully</returns>
    Task<bool> SendTemplateEmailAsync(TemplateEmailDto templateEmail);
}

/// <summary>
/// Basic email DTO
/// </summary>
public class EmailDto
{
    public string To { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;
    public List<string> Cc { get; set; } = new();
    public List<string> Bcc { get; set; } = new();
    public bool IsHtml { get; set; } = true;
    public List<EmailAttachmentDto> Attachments { get; set; } = new();
}

/// <summary>
/// Template email DTO
/// </summary>
public class TemplateEmailDto
{
    public string To { get; set; } = string.Empty;
    public string TemplateName { get; set; } = string.Empty;
    public Dictionary<string, object> TemplateData { get; set; } = new();
    public string From { get; set; } = string.Empty;
    public List<string> Cc { get; set; } = new();
    public List<string> Bcc { get; set; } = new();
}

/// <summary>
/// Email attachment DTO
/// </summary>
public class EmailAttachmentDto
{
    public string FileName { get; set; } = string.Empty;
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public string ContentType { get; set; } = string.Empty;
}