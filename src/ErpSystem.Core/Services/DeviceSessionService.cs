using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Http;
using System.Net.Http;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;
using ErpSystem.Shared;

namespace ErpSystem.Core.Services;

public interface IDeviceSessionService
{
    Task<DeviceSessionInfo> CreateEnhancedSessionAsync(Guid userId, Guid tenantId, CreateSessionRequest request);
    Task<List<DeviceSessionDto>> GetActiveSessionsForUserAsync(Guid userId);
    Task<DeviceSessionDto?> GetSessionDetailsAsync(string sessionId);
    Task<bool> TerminateSessionAsync(string sessionId, string reason = "Manual termination");
    Task<int> TerminateAllSessionsExceptCurrentAsync(Guid userId, string currentSessionId, string reason = "Terminate other sessions");
    Task<DeviceSessionStats> GetSessionStatsAsync(Guid userId, int days = 30);
    Task<List<DeviceSessionDto>> GetRecentSessionsAsync(Guid userId, int count = 10);
    Task<bool> IsLocationSuspiciousAsync(Guid userId, string ipAddress);
    Task<DeviceFingerprint> GenerateDeviceFingerprintAsync(CreateSessionRequest request);
    Task<GeolocationInfo?> GetGeolocationAsync(string ipAddress);
}

public class CreateSessionRequest
{
    public string IpAddress { get; set; } = string.Empty;
    public string UserAgent { get; set; } = string.Empty;
    public string? DeviceFingerprint { get; set; }
    public Dictionary<string, string>? DeviceInfo { get; set; }
    public string? JwtTokenId { get; set; }
}

public class DeviceSessionInfo
{
    public UserSession Session { get; set; } = null!;
    public DeviceFingerprint Fingerprint { get; set; } = null!;
    public GeolocationInfo? Location { get; set; }
    public SecurityAssessment Security { get; set; } = null!;
}

public class DeviceFingerprint
{
    public string Hash { get; set; } = string.Empty;
    public string DeviceType { get; set; } = string.Empty;
    public string Browser { get; set; } = string.Empty;
    public string OperatingSystem { get; set; } = string.Empty;
    public string ScreenResolution { get; set; } = string.Empty;
    public string TimeZone { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public bool CookiesEnabled { get; set; }
    public List<string> Plugins { get; set; } = new();
}

public class GeolocationInfo
{
    public string IpAddress { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? ISP { get; set; }
    public string? Organization { get; set; }
    public bool IsVPN { get; set; }
    public bool IsTor { get; set; }
}

public class SecurityAssessment
{
    public int RiskScore { get; set; } // 0-100
    public List<string> RiskFactors { get; set; } = new();
    public bool IsTrusted { get; set; }
    public bool RequiresVerification { get; set; }
}

public class DeviceSessionStats
{
    public int TotalSessions { get; set; }
    public int ActiveSessions { get; set; }
    public int UniqueDevices { get; set; }
    public int UniqueLocations { get; set; }
    public DateTime? LastLogin { get; set; }
    public List<string> RecentLocations { get; set; } = new();
    public List<string> RecentDevices { get; set; } = new();
}

public class DeviceSessionService : IDeviceSessionService
{
    private readonly IUserSessionService _userSessionService;
    private readonly ILogger<DeviceSessionService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    
    public DeviceSessionService(
        IUserSessionService userSessionService,
        ILogger<DeviceSessionService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _userSessionService = userSessionService;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<DeviceSessionInfo> CreateEnhancedSessionAsync(Guid userId, Guid tenantId, CreateSessionRequest request)
    {
        try
        {
            // Generate device fingerprint
            var fingerprint = await GenerateDeviceFingerprintAsync(request);
            
            // Get geolocation
            var location = await GetGeolocationAsync(request.IpAddress);
            
            // Assess security
            var security = await AssessSecurityRiskAsync(userId, request, location, fingerprint);
            
            // Create session using existing UserSessionService
            var session = await _userSessionService.CreateSessionAsync(
                userId, 
                tenantId, 
                request.IpAddress, 
                request.UserAgent, 
                fingerprint.Hash, 
                "Disabled", // Assume no concurrent login prevention for now
                request.JwtTokenId);

            // Update session with enhanced location info
            if (location != null)
            {
                session.Location = $"{location.City}, {location.Country}";
            }

            _logger.LogInformation("Enhanced session created for user {UserId} from {IpAddress}", userId, request.IpAddress);

            return new DeviceSessionInfo
            {
                Session = session,
                Fingerprint = fingerprint,
                Location = location,
                Security = security
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating enhanced session for user {UserId}", userId);
            throw;
        }
    }

    public async Task<List<DeviceSessionDto>> GetActiveSessionsForUserAsync(Guid userId)
    {
        try
        {
            var sessions = await _userSessionService.GetActiveUserSessionsAsync(userId);
            var deviceSessions = new List<DeviceSessionDto>();

            foreach (var session in sessions)
            {
                var deviceSession = await MapToDeviceSessionDtoAsync(session);
                deviceSessions.Add(deviceSession);
            }

            return deviceSessions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active sessions for user {UserId}", userId);
            throw;
        }
    }

    public async Task<DeviceSessionDto?> GetSessionDetailsAsync(string sessionId)
    {
        try
        {
            var session = await _userSessionService.GetSessionAsync(sessionId);
            if (session == null) return null;

            return await MapToDeviceSessionDtoAsync(session);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting session details for {SessionId}", sessionId);
            throw;
        }
    }

    public async Task<bool> TerminateSessionAsync(string sessionId, string reason = "Manual termination")
    {
        try
        {
            await _userSessionService.TerminateSessionAsync(sessionId, reason);
            _logger.LogInformation("Session {SessionId} terminated: {Reason}", sessionId, reason);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error terminating session {SessionId}", sessionId);
            return false;
        }
    }

    public async Task<int> TerminateAllSessionsExceptCurrentAsync(Guid userId, string currentSessionId, string reason = "Terminate other sessions")
    {
        try
        {
            var sessions = await _userSessionService.GetActiveUserSessionsAsync(userId);
            var terminatedCount = 0;

            foreach (var session in sessions.Where(s => s.SessionId != currentSessionId))
            {
                await _userSessionService.TerminateSessionAsync(session.SessionId, reason);
                terminatedCount++;
            }

            _logger.LogInformation("Terminated {Count} sessions for user {UserId}", terminatedCount, userId);
            return terminatedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error terminating sessions for user {UserId}", userId);
            throw;
        }
    }

    public async Task<DeviceSessionStats> GetSessionStatsAsync(Guid userId, int days = 30)
    {
        try
        {
            var sessions = await _userSessionService.GetUserSessionHistoryAsync(userId, days);
            
            var stats = new DeviceSessionStats
            {
                TotalSessions = sessions.Count,
                ActiveSessions = sessions.Count(s => s.IsActive),
                UniqueDevices = sessions.Select(s => s.DeviceFingerprint).Distinct().Count(),
                UniqueLocations = sessions.Select(s => s.Location).Where(l => !string.IsNullOrEmpty(l)).Distinct().Count(),
                LastLogin = sessions.Where(s => s.IsActive).OrderByDescending(s => s.LoginTime).FirstOrDefault()?.LoginTime,
                RecentLocations = sessions.Select(s => s.Location).Where(l => !string.IsNullOrEmpty(l)).Distinct().Take(5).ToList(),
                RecentDevices = sessions.Select(s => $"{s.DeviceType} - {s.Browser}").Distinct().Take(5).ToList()
            };

            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting session stats for user {UserId}", userId);
            throw;
        }
    }

    public async Task<List<DeviceSessionDto>> GetRecentSessionsAsync(Guid userId, int count = 10)
    {
        try
        {
            var sessions = await _userSessionService.GetUserSessionHistoryAsync(userId, 30);
            var recentSessions = sessions.OrderByDescending(s => s.LoginTime).Take(count).ToList();
            
            var deviceSessions = new List<DeviceSessionDto>();
            foreach (var session in recentSessions)
            {
                var deviceSession = await MapToDeviceSessionDtoAsync(session);
                deviceSessions.Add(deviceSession);
            }

            return deviceSessions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting recent sessions for user {UserId}", userId);
            throw;
        }
    }

    public async Task<bool> IsLocationSuspiciousAsync(Guid userId, string ipAddress)
    {
        try
        {
            var recentSessions = await _userSessionService.GetUserSessionHistoryAsync(userId, 7);
            var location = await GetGeolocationAsync(ipAddress);
            
            if (location == null) return true; // Unknown location is suspicious
            
            // Check if user has logged in from this country before
            var hasLoggedFromCountry = recentSessions.Any(s => 
                s.Location.Contains(location.Country, StringComparison.OrdinalIgnoreCase));
            
            // Consider VPN/Tor usage as suspicious
            if (location.IsVPN || location.IsTor) return true;
            
            // New country login is suspicious
            return !hasLoggedFromCountry;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking location suspicion for user {UserId}", userId);
            return true; // Err on the side of caution
        }
    }

    public async Task<DeviceFingerprint> GenerateDeviceFingerprintAsync(CreateSessionRequest request)
    {
        try
        {
            var fingerprint = new DeviceFingerprint();
            
            // Parse User Agent
            var deviceInfo = ParseUserAgent(request.UserAgent);
            fingerprint.DeviceType = deviceInfo.DeviceType;
            fingerprint.Browser = deviceInfo.Browser;
            fingerprint.OperatingSystem = deviceInfo.OperatingSystem;
            
            // Use provided device info if available
            if (request.DeviceInfo != null)
            {
                if (request.DeviceInfo.TryGetValue("screenResolution", out var screenRes))
                    fingerprint.ScreenResolution = screenRes;
                    
                if (request.DeviceInfo.TryGetValue("timeZone", out var timeZone))
                    fingerprint.TimeZone = timeZone;
                    
                if (request.DeviceInfo.TryGetValue("language", out var language))
                    fingerprint.Language = language;
                
                if (request.DeviceInfo.TryGetValue("cookiesEnabled", out var cookiesEnabled))
                {
                    fingerprint.CookiesEnabled = bool.TryParse(cookiesEnabled, out var cookiesResult) && cookiesResult;
                }
                
                if (request.DeviceInfo.ContainsKey("plugins"))
                {
                    fingerprint.Plugins = request.DeviceInfo["plugins"].Split(',').ToList();
                }
            }
            
            // Generate hash from all fingerprint data
            fingerprint.Hash = GenerateFingerprintHash(fingerprint, request.UserAgent, request.IpAddress);
            
            return fingerprint;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating device fingerprint");
            // Return a basic fingerprint
            return new DeviceFingerprint
            {
                Hash = GenerateBasicHash(request.UserAgent, request.IpAddress),
                DeviceType = "Unknown",
                Browser = "Unknown",
                OperatingSystem = "Unknown"
            };
        }
    }

    public async Task<GeolocationInfo?> GetGeolocationAsync(string ipAddress)
    {
        try
        {
            // Skip localhost and private IPs
            if (IsPrivateOrLocalhost(ipAddress))
            {
                return new GeolocationInfo
                {
                    IpAddress = ipAddress,
                    City = "Local",
                    Country = "Local",
                    CountryCode = "LOCAL"
                };
            }

            // Use a free IP geolocation service (ip-api.com)
            // In production, you might want to use a paid service with API keys
            var httpClient = _httpClientFactory.CreateClient("geolocation");
            
            var response = await httpClient.GetAsync($"http://ip-api.com/json/{ipAddress}?fields=status,message,country,countryCode,region,city,lat,lon,isp,org,proxy");
            
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var data = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                
                if (data != null && data.TryGetValue("status", out var status) && status.ToString() == "success")
                {
                    return new GeolocationInfo
                    {
                        IpAddress = ipAddress,
                        City = GetStringValue(data, "city"),
                        Region = GetStringValue(data, "region"),
                        Country = GetStringValue(data, "country"),
                        CountryCode = GetStringValue(data, "countryCode"),
                        Latitude = GetDoubleValue(data, "lat"),
                        Longitude = GetDoubleValue(data, "lon"),
                        ISP = GetStringValue(data, "isp"),
                        Organization = GetStringValue(data, "org"),
                        IsVPN = GetBoolValue(data, "proxy")
                    };
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get geolocation for IP {IpAddress}", ipAddress);
        }
        
        return null;
    }

    private async Task<DeviceSessionDto> MapToDeviceSessionDtoAsync(UserSession session)
    {
        var location = await GetGeolocationAsync(session.IpAddress);
        
        return new DeviceSessionDto
        {
            Id = session.SessionId,
            UserId = session.UserId.ToString(),
            UserName = "Unknown", // Would need to load user data
            UserEmail = "Unknown", // Would need to load user data
            DeviceInfo = new DeviceInfoDto
            {
                Type = session.DeviceType,
                Os = session.OperatingSystem,
                Browser = session.Browser,
                Model = ""
            },
            Location = new LocationInfoDto
            {
                City = location?.City ?? session.Location.Split(',').FirstOrDefault()?.Trim() ?? "",
                Country = location?.Country ?? session.Location.Split(',').LastOrDefault()?.Trim() ?? "",
                Ip = session.IpAddress,
                Coordinates = location?.Latitude != null && location.Longitude != null ? 
                    new CoordinatesDto { Latitude = location.Latitude.Value, Longitude = location.Longitude.Value } : null
            },
            Session = new SessionInfoDto
            {
                SessionId = session.SessionId,
                StartTime = session.LoginTime,
                LastActivity = session.LastActivityTime,
                IsActive = session.IsActive,
                Duration = (int)(DateTime.UtcNow - session.LoginTime).TotalMinutes
            },
            Security = new SecurityInfoDto
            {
                IsTrusted = true, // Would need security assessment logic
                RiskScore = 1,
                Flags = new List<string>(),
                TwoFactorEnabled = false // Would need user data
            }
        };
    }

    private async Task<SecurityAssessment> AssessSecurityRiskAsync(Guid userId, CreateSessionRequest request, GeolocationInfo? location, DeviceFingerprint fingerprint)
    {
        var assessment = new SecurityAssessment();
        var riskFactors = new List<string>();
        var riskScore = 0;

        // Location-based risk assessment
        if (location != null)
        {
            if (await IsLocationSuspiciousAsync(userId, request.IpAddress))
            {
                riskFactors.Add("New location");
                riskScore += 30;
            }
            
            if (location.IsVPN)
            {
                riskFactors.Add("VPN detected");
                riskScore += 25;
            }
            
            if (location.IsTor)
            {
                riskFactors.Add("Tor network detected");
                riskScore += 40;
            }
        }
        else
        {
            riskFactors.Add("Unknown location");
            riskScore += 20;
        }

        // Device-based risk assessment
        if (string.IsNullOrEmpty(fingerprint.Hash))
        {
            riskFactors.Add("Unidentifiable device");
            riskScore += 15;
        }

        assessment.RiskScore = Math.Min(riskScore, 100);
        assessment.RiskFactors = riskFactors;
        assessment.IsTrusted = riskScore < 30;
        assessment.RequiresVerification = riskScore > 50;

        return assessment;
    }

    private static (string DeviceType, string Browser, string OperatingSystem) ParseUserAgent(string userAgent)
    {
        var deviceType = "Desktop";
        var browser = "Unknown";
        var os = "Unknown";

        if (string.IsNullOrEmpty(userAgent)) 
            return (deviceType, browser, os);

        userAgent = userAgent.ToLowerInvariant();

        // Device type detection
        if (userAgent.Contains("mobile") || userAgent.Contains("android") || userAgent.Contains("iphone"))
        {
            deviceType = userAgent.Contains("ipad") ? "Tablet" : "Mobile";
        }
        else if (userAgent.Contains("tablet") || userAgent.Contains("ipad"))
        {
            deviceType = "Tablet";
        }

        // Browser detection
        if (userAgent.Contains("chrome")) browser = "Chrome";
        else if (userAgent.Contains("firefox")) browser = "Firefox";
        else if (userAgent.Contains("safari")) browser = "Safari";
        else if (userAgent.Contains("edge")) browser = "Edge";
        else if (userAgent.Contains("opera")) browser = "Opera";

        // OS detection
        if (userAgent.Contains("windows")) os = "Windows";
        else if (userAgent.Contains("macintosh") || userAgent.Contains("mac os")) os = "macOS";
        else if (userAgent.Contains("linux")) os = "Linux";
        else if (userAgent.Contains("android")) os = "Android";
        else if (userAgent.Contains("ios") || userAgent.Contains("iphone") || userAgent.Contains("ipad")) os = "iOS";

        return (deviceType, browser, os);
    }

    private static string GenerateFingerprintHash(DeviceFingerprint fingerprint, string userAgent, string ipAddress)
    {
        var data = $"{fingerprint.DeviceType}-{fingerprint.Browser}-{fingerprint.OperatingSystem}-{fingerprint.ScreenResolution}-{fingerprint.TimeZone}-{fingerprint.Language}-{fingerprint.CookiesEnabled}-{string.Join(",", fingerprint.Plugins)}-{userAgent}";
        return GenerateBasicHash(data, ipAddress);
    }

    private static string GenerateBasicHash(string data, string ipAddress)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes($"{data}-{ipAddress}"));
        return Convert.ToBase64String(hashBytes)[..16]; // Take first 16 characters
    }

    private static bool IsPrivateOrLocalhost(string ipAddress)
    {
        if (string.IsNullOrEmpty(ipAddress)) return true;
        return ipAddress == "::1" || ipAddress.StartsWith("127.") || ipAddress.StartsWith("192.168.") || 
               ipAddress.StartsWith("10.") || ipAddress.StartsWith("172.16.") || ipAddress == "localhost";
    }

    private static string GetStringValue(Dictionary<string, object> data, string key)
    {
        return data.TryGetValue(key, out var value) ? value?.ToString() ?? "" : "";
    }

    private static double? GetDoubleValue(Dictionary<string, object> data, string key)
    {
        if (data.TryGetValue(key, out var value) && double.TryParse(value?.ToString(), out var doubleValue))
            return doubleValue;
        return null;
    }

    private static bool GetBoolValue(Dictionary<string, object> data, string key)
    {
        return data.TryGetValue(key, out var value) && bool.TryParse(value?.ToString(), out var boolValue) && boolValue;
    }
}