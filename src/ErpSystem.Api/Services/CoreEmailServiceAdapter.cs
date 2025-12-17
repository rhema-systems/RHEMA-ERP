using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.Interfaces.Common;

namespace ErpSystem.Api.Services;

/// <summary>
/// Adapter to bridge the Core IEmailService interface with the existing SimpleEmailService
/// </summary>
public class CoreEmailServiceAdapter : ErpSystem.Core.Interfaces.Common.IEmailService
{
    private readonly ErpSystem.Web.Services.IEmailService _simpleEmailService;
    private readonly ILogger<CoreEmailServiceAdapter> _logger;

    public CoreEmailServiceAdapter(
        ErpSystem.Web.Services.IEmailService simpleEmailService,
        ILogger<CoreEmailServiceAdapter> logger)
    {
        _simpleEmailService = simpleEmailService;
        _logger = logger;
    }

    public async Task<bool> SendEmailAsync(EmailDto email)
    {
        try
        {
            // Convert Core EmailDto to simple email call
            return await _simpleEmailService.SendEmailAsync(
                to: email.To,
                subject: email.Subject,
                body: email.Body,
                isHtml: email.IsHtml);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending email through adapter to {To}", email.To);
            return false;
        }
    }

    public async Task<int> SendBulkEmailsAsync(List<EmailDto> emails)
    {
        int successCount = 0;

        foreach (var email in emails)
        {
            try
            {
                var success = await SendEmailAsync(email);
                if (success)
                {
                    successCount++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending bulk email to {To}", email.To);
            }
        }

        return successCount;
    }

    public async Task<bool> SendTemplateEmailAsync(TemplateEmailDto templateEmail)
    {
        try
        {
            // For now, just send a simple message about the template
            // In a full implementation, this would load and process the template
            var body = $"Template: {templateEmail.TemplateName}\nData: {string.Join(", ", templateEmail.TemplateData.Select(kv => $"{kv.Key}={kv.Value}"))}";

            return await _simpleEmailService.SendEmailAsync(
                to: templateEmail.To,
                subject: $"Template Email: {templateEmail.TemplateName}",
                body: body,
                isHtml: false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending template email {Template} to {To}", templateEmail.TemplateName, templateEmail.To);
            return false;
        }
    }
}
