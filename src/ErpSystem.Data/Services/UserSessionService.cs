using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ErpSystem.Data.Services
{
    public class UserSessionService : IUserSessionService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<UserSessionService> _logger;
        private readonly IJwtBlacklistService _jwtBlacklistService;

        public UserSessionService(ApplicationDbContext context, ILogger<UserSessionService> logger, IJwtBlacklistService jwtBlacklistService)
        {
            _context = context;
            _logger = logger;
            _jwtBlacklistService = jwtBlacklistService;
        }

        public async Task<UserSession> CreateSessionAsync(Guid userId, Guid tenantId, string ipAddress, 
            string userAgent, string deviceFingerprint, string preventConcurrentLogin, string? jwtTokenId = null)
        {
            _logger.LogInformation($"Creating new session for user {userId} with prevention mode: {preventConcurrentLogin}");

            // Parse user agent for device info
            var deviceInfo = ParseUserAgent(userAgent);

            // Handle concurrent login prevention
            await HandleConcurrentLoginPreventionAsync(userId, preventConcurrentLogin);

            // Create new session
            var session = new UserSession
            {
                UserId = userId,
                TenantId = tenantId,
                IpAddress = ipAddress ?? "Unknown",
                UserAgent = userAgent ?? "Unknown",
                DeviceFingerprint = deviceFingerprint ?? "Unknown",
                DeviceType = deviceInfo.DeviceType ?? "Unknown",
                Browser = deviceInfo.Browser ?? "Unknown",
                OperatingSystem = deviceInfo.OperatingSystem ?? "Unknown",
                Location = "Unknown", // Set default value for required Location field
                TerminationReason = string.Empty, // Set default value to avoid null
                JwtTokenId = jwtTokenId,
                LoginTime = DateTime.UtcNow,
                LastActivityTime = DateTime.UtcNow,
                IsActive = true
            };

            _context.UserSessions.Add(session);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Created session {session.SessionId} for user {userId}");
            return session;
        }

        public async Task<bool> CanUserLoginAsync(Guid userId, string preventConcurrentLogin)
        {
            if (preventConcurrentLogin == "Disabled")
            {
                return true; // Always allow login
            }

            if (preventConcurrentLogin == "PreventSubsequentLogins")
            {
                var activeSessions = await GetActiveUserSessionsAsync(userId);
                if (activeSessions.Any())
                {
                    _logger.LogWarning($"Preventing login for user {userId} - active session exists");
                    return false;
                }
            }

            // For "LogoutFromAllDevices", we allow the login but will terminate existing sessions
            return true;
        }

        private async Task HandleConcurrentLoginPreventionAsync(Guid userId, string preventConcurrentLogin)
        {
            switch (preventConcurrentLogin)
            {
                case "Disabled":
                    // Allow multiple sessions - do nothing
                    _logger.LogDebug($"Concurrent login prevention disabled for user {userId}");
                    break;

                case "LogoutFromAllDevices":
                    // Terminate all existing active sessions
                    await TerminateAllUserSessionsAsync(userId, null, "New login from different device");
                    _logger.LogInformation($"Terminated all existing sessions for user {userId} due to new login");
                    break;

                case "PreventSubsequentLogins":
                    // This is handled in CanUserLoginAsync - should not reach here if validation is proper
                    _logger.LogDebug($"User {userId} login allowed - no existing active sessions");
                    break;

                default:
                    _logger.LogWarning($"Unknown concurrent login prevention mode: {preventConcurrentLogin}");
                    break;
            }
        }

        public async Task<UserSession> GetSessionAsync(string sessionId)
        {
            return await _context.UserSessions
                .FirstOrDefaultAsync(s => s.SessionId == sessionId);
        }

        public async Task<List<UserSession>> GetActiveUserSessionsAsync(Guid userId)
        {
            return await _context.UserSessions
                .Where(s => s.UserId == userId && s.IsActive)
                .OrderByDescending(s => s.LastActivityTime)
                .ToListAsync();
        }

        public async Task UpdateSessionActivityAsync(string sessionId)
        {
            var session = await GetSessionAsync(sessionId);
            if (session != null && session.IsActive)
            {
                session.UpdateActivity();
                await _context.SaveChangesAsync();
            }
        }

        public async Task TerminateSessionAsync(string sessionId, string? reason = null)
        {
            _logger.LogInformation($"Looking for session {sessionId} to terminate");
            var session = await GetSessionAsync(sessionId);
            
            if (session == null)
            {
                _logger.LogWarning($"Session {sessionId} not found - cannot terminate");
                return;
            }
            
            if (!session.IsActive)
            {
                _logger.LogWarning($"Session {sessionId} is already inactive - skipping termination");
                return;
            }
            
            _logger.LogInformation($"Terminating active session {sessionId} for user {session.UserId}. Reason: {reason}");
            
            // Blacklist JWT token if available
            if (!string.IsNullOrEmpty(session.JwtTokenId))
            {
                try
                {
                    // For terminated sessions, we'll set expiry to now to blacklist immediately
                    await _jwtBlacklistService.BlacklistTokenAsync(
                        session.JwtTokenId, 
                        session.UserId, 
                        DateTime.UtcNow.AddHours(24), // Use typical JWT expiry
                        reason ?? "Session terminated");
                    _logger.LogInformation($"Blacklisted JWT token {session.JwtTokenId} for terminated session {sessionId}");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to blacklist JWT token {JwtTokenId} for session {SessionId}", session.JwtTokenId, sessionId);
                    // Continue with session termination even if blacklisting fails
                }
            }
            else
            {
                _logger.LogWarning($"No JWT token ID found for session {sessionId} - cannot blacklist token");
            }
            
            // Mark session as logged out
            _logger.LogDebug($"Setting session {sessionId} as logged out with reason: {reason}");
            session.MarkAsLoggedOut(reason);
            
            if (reason != null && reason.Contains("concurrent"))
            {
                session.WasTerminatedByConcurrentLogin = true;
            }
            
            await _context.SaveChangesAsync();
            _logger.LogInformation($"Successfully terminated session {sessionId}. IsActive: {session.IsActive}, LogoutTime: {session.LogoutTime}, Reason: {session.TerminationReason}");
        }

        public async Task TerminateAllUserSessionsAsync(Guid userId, string? excludeSessionId = null, string? reason = null)
        {
            var sessions = await _context.UserSessions
                .Where(s => s.UserId == userId && s.IsActive)
                .ToListAsync();

            if (excludeSessionId != null)
            {
                sessions = sessions.Where(s => s.SessionId != excludeSessionId).ToList();
            }

            int blacklistedTokens = 0;
            foreach (var session in sessions)
            {
                // Blacklist JWT token if available
                if (!string.IsNullOrEmpty(session.JwtTokenId))
                {
                    try
                    {
                        await _jwtBlacklistService.BlacklistTokenAsync(
                            session.JwtTokenId, 
                            session.UserId, 
                            DateTime.UtcNow.AddHours(24), // Use typical JWT expiry
                            reason ?? "Session terminated");
                        blacklistedTokens++;
                        _logger.LogDebug($"Blacklisted JWT token {session.JwtTokenId} for terminated session {session.SessionId}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to blacklist JWT token {JwtTokenId} for session {SessionId}", session.JwtTokenId, session.SessionId);
                        // Continue with session termination even if blacklisting fails
                    }
                }
                
                session.MarkAsLoggedOut(reason);
                if (reason != null && reason.Contains("concurrent"))
                {
                    session.WasTerminatedByConcurrentLogin = true;
                }
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation($"Terminated {sessions.Count} sessions and blacklisted {blacklistedTokens} JWT tokens for user {userId}. Reason: {reason}");
        }

        public async Task CleanupExpiredSessionsAsync(int sessionTimeoutMinutes)
        {
            var cutoffTime = DateTime.UtcNow.AddMinutes(-sessionTimeoutMinutes);
            
            var expiredSessions = await _context.UserSessions
                .Where(s => s.IsActive && s.LastActivityTime < cutoffTime)
                .ToListAsync();

            foreach (var session in expiredSessions)
            {
                session.MarkAsLoggedOut("Session expired due to inactivity");
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation($"Cleaned up {expiredSessions.Count} expired sessions");
        }

        public async Task<List<UserSession>> GetUserSessionHistoryAsync(Guid userId, int days = 30)
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-days);
            
            return await _context.UserSessions
                .Where(s => s.UserId == userId && s.LoginTime >= cutoffDate)
                .OrderByDescending(s => s.LoginTime)
                .ToListAsync();
        }

        public async Task<SessionInfo> GetSessionInfoAsync(string sessionId)
        {
            var session = await GetSessionAsync(sessionId);
            if (session == null) return null;

            return new SessionInfo
            {
                SessionId = session.SessionId,
                UserId = session.UserId,
                TenantId = session.TenantId,
                LoginTime = session.LoginTime,
                LastActivityTime = session.LastActivityTime,
                IpAddress = session.IpAddress,
                DeviceType = session.DeviceType,
                Browser = session.Browser,
                OperatingSystem = session.OperatingSystem,
                Location = session.Location,
                IsActive = session.IsActive,
                SessionDuration = session.GetSessionDuration()
            };
        }

        private DeviceInfo ParseUserAgent(string userAgent)
        {
            if (string.IsNullOrEmpty(userAgent))
            {
                return new DeviceInfo { DeviceType = "Unknown", Browser = "Unknown", OperatingSystem = "Unknown" };
            }

            var deviceInfo = new DeviceInfo();

            // Detect device type
            if (Regex.IsMatch(userAgent, @"Mobile|Android|iPhone|iPad", RegexOptions.IgnoreCase))
            {
                if (userAgent.Contains("iPad", StringComparison.OrdinalIgnoreCase))
                    deviceInfo.DeviceType = "Tablet";
                else
                    deviceInfo.DeviceType = "Mobile";
            }
            else
            {
                deviceInfo.DeviceType = "Desktop";
            }

            // Detect browser
            if (userAgent.Contains("Edg/", StringComparison.OrdinalIgnoreCase))
                deviceInfo.Browser = "Microsoft Edge";
            else if (userAgent.Contains("Chrome/", StringComparison.OrdinalIgnoreCase))
                deviceInfo.Browser = "Google Chrome";
            else if (userAgent.Contains("Firefox/", StringComparison.OrdinalIgnoreCase))
                deviceInfo.Browser = "Mozilla Firefox";
            else if (userAgent.Contains("Safari/", StringComparison.OrdinalIgnoreCase) && !userAgent.Contains("Chrome"))
                deviceInfo.Browser = "Safari";
            else if (userAgent.Contains("Opera", StringComparison.OrdinalIgnoreCase))
                deviceInfo.Browser = "Opera";
            else
                deviceInfo.Browser = "Unknown";

            // Detect OS
            if (userAgent.Contains("Windows NT", StringComparison.OrdinalIgnoreCase))
                deviceInfo.OperatingSystem = "Windows";
            else if (userAgent.Contains("Mac OS X", StringComparison.OrdinalIgnoreCase))
                deviceInfo.OperatingSystem = "macOS";
            else if (userAgent.Contains("X11", StringComparison.OrdinalIgnoreCase) || userAgent.Contains("Linux", StringComparison.OrdinalIgnoreCase))
                deviceInfo.OperatingSystem = "Linux";
            else if (userAgent.Contains("Android", StringComparison.OrdinalIgnoreCase))
                deviceInfo.OperatingSystem = "Android";
            else if (userAgent.Contains("iPhone OS", StringComparison.OrdinalIgnoreCase) || userAgent.Contains("iOS", StringComparison.OrdinalIgnoreCase))
                deviceInfo.OperatingSystem = "iOS";
            else
                deviceInfo.OperatingSystem = "Unknown";

            return deviceInfo;
        }

        private class DeviceInfo
        {
            public string DeviceType { get; set; } = "Unknown";
            public string Browser { get; set; } = "Unknown";
            public string OperatingSystem { get; set; } = "Unknown";
        }
    }
}