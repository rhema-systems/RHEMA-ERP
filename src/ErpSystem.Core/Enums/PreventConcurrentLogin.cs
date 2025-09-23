namespace ErpSystem.Core.Enums;

/// <summary>
/// Options for preventing concurrent logins
/// </summary>
public enum PreventConcurrentLogin
{
    /// <summary>
    /// Allow users to log in from multiple devices/sessions simultaneously
    /// </summary>
    Disabled = 0,
    
    /// <summary>
    /// New login will logout all other active sessions
    /// If you have logged in anywhere, this new login will log out all previous sessions
    /// </summary>
    LogoutFromAllDevices = 1,
    
    /// <summary>
    /// Prevent subsequent logins if user already has an active session
    /// If you have already logged in somewhere, you can't have subsequent login anywhere until you logout
    /// </summary>
    PreventSubsequentLogins = 2
}
