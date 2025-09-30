using ErpSystem.Core.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ErpSystem.Core.Interfaces
{
    public interface IUserSessionService
    {
        Task<UserSession> CreateSessionAsync(Guid userId, Guid tenantId, string ipAddress, string userAgent, 
            string deviceFingerprint, string preventConcurrentLogin, string? jwtTokenId = null);
        
        Task<UserSession> GetSessionAsync(string sessionId);
        
        Task<List<UserSession>> GetActiveUserSessionsAsync(Guid userId);
        
        Task UpdateSessionActivityAsync(string sessionId);
        
        Task TerminateSessionAsync(string sessionId, string? reason = null);
        
        Task TerminateAllUserSessionsAsync(Guid userId, string? excludeSessionId = null, string? reason = null);
        
        Task<bool> CanUserLoginAsync(Guid userId, string preventConcurrentLogin);
        
        Task CleanupExpiredSessionsAsync(int sessionTimeoutMinutes);
        
        Task<List<UserSession>> GetUserSessionHistoryAsync(Guid userId, int days = 30);
        
        Task<SessionInfo> GetSessionInfoAsync(string sessionId);
    }

    public class SessionInfo
    {
        public string SessionId { get; set; }
        public Guid UserId { get; set; }
        public Guid TenantId { get; set; }
        public DateTime LoginTime { get; set; }
        public DateTime LastActivityTime { get; set; }
        public string IpAddress { get; set; }
        public string DeviceType { get; set; }
        public string Browser { get; set; }
        public string OperatingSystem { get; set; }
        public string Location { get; set; }
        public bool IsActive { get; set; }
        public TimeSpan SessionDuration { get; set; }
    }
}