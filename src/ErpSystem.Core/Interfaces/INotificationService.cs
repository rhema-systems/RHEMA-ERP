namespace ErpSystem.Core.Interfaces;

/// <summary>
/// Service interface for sending notifications
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Notifies when a job card is submitted for approval
    /// </summary>
    Task NotifyJobCardSubmittedAsync(Guid jobCardId);

    /// <summary>
    /// Notifies when a job card is approved
    /// </summary>
    Task NotifyJobCardApprovedAsync(Guid jobCardId);

    /// <summary>
    /// Notifies when a job card is rejected
    /// </summary>
    Task NotifyJobCardRejectedAsync(Guid jobCardId, string? reason);

    /// <summary>
    /// Notifies when changes are requested for a job card
    /// </summary>
    Task NotifyJobCardChangesRequestedAsync(Guid jobCardId, string? comments);

    /// <summary>
    /// Sends email notification
    /// </summary>
    Task SendEmailAsync(string to, string subject, string body);

    /// <summary>
    /// Sends SMS notification
    /// </summary>
    Task SendSmsAsync(string phoneNumber, string message);

    /// <summary>
    /// Sends push notification
    /// </summary>
    Task SendPushNotificationAsync(Guid userId, string title, string message, Dictionary<string, string>? data = null);

    /// <summary>
    /// Creates in-app notification
    /// </summary>
    Task CreateInAppNotificationAsync(Guid userId, string title, string message, string type, Dictionary<string, object>? data = null);
}