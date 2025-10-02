# Enhanced Device Session Management

## Overview

The Enhanced Device Session Management system provides comprehensive tracking, geolocation, device fingerprinting, and security assessment for user sessions. This feature helps administrators monitor active sessions and provides users with security insights about their account access.

## Features

### 1. Device Fingerprinting
- **Browser Detection**: Chrome, Firefox, Safari, Edge, Opera
- **Operating System Detection**: Windows, macOS, Linux, iOS, Android
- **Device Type Classification**: Desktop, Mobile, Tablet
- **Advanced Properties**: Screen resolution, timezone, language, cookies, plugins
- **Unique Hash Generation**: SHA256-based fingerprinting for device identification

### 2. Geolocation Services
- **IP-based Location Detection**: City, region, country identification
- **Coordinates**: Latitude and longitude when available
- **ISP/Organization Information**: Internet service provider details
- **Security Flags**: VPN and Tor network detection
- **Private IP Handling**: Local network detection and labeling

### 3. Security Assessment
- **Risk Scoring**: 0-100 scale based on multiple factors
- **Risk Factors**: New location, VPN usage, Tor networks, unknown devices
- **Trust Evaluation**: Automatic trust scoring for sessions
- **Verification Requirements**: Flags high-risk sessions for additional verification

### 4. Session Statistics
- **Activity Metrics**: Total sessions, active sessions, unique devices/locations
- **Historical Data**: Recent login information and patterns
- **Device History**: Track of recent devices and locations used

## API Endpoints

### Admin Endpoints

#### Get All Device Sessions
```http
GET /api/security/device-sessions
Authorization: Bearer <admin_token>
```
Returns all active device sessions across the tenant.

#### Get Device Session Details
```http
GET /api/security/device-sessions/{sessionId}
Authorization: Bearer <admin_token>
```
Returns detailed information about a specific session.

#### Terminate Device Session
```http
DELETE /api/security/device-sessions/{sessionId}?reason=security_concern
Authorization: Bearer <admin_token>
```
Terminates a specific device session.

### User Endpoints

#### Get Personal Device Session Stats
```http
GET /api/security/device-sessions/stats?days=30
Authorization: Bearer <user_token>
```
Returns session statistics for the current user.

#### Get Recent Device Sessions
```http
GET /api/security/device-sessions/recent?count=10
Authorization: Bearer <user_token>
```
Returns recent sessions for the current user.

#### Terminate All Other Sessions
```http
POST /api/security/device-sessions/terminate-others
Authorization: Bearer <user_token>
Content-Type: application/json

{
  "currentSessionId": "current_session_id",
  "reason": "Security cleanup"
}
```
Terminates all sessions except the current one.

## Data Models

### DeviceSessionDto
```csharp
public class DeviceSessionDto
{
    public string Id { get; set; }
    public string UserId { get; set; }
    public string UserName { get; set; }
    public string UserEmail { get; set; }
    public DeviceInfoDto DeviceInfo { get; set; }
    public LocationInfoDto Location { get; set; }
    public SessionInfoDto Session { get; set; }
    public SecurityInfoDto Security { get; set; }
}
```

### DeviceFingerprint
```csharp
public class DeviceFingerprint
{
    public string Hash { get; set; }
    public string DeviceType { get; set; }
    public string Browser { get; set; }
    public string OperatingSystem { get; set; }
    public string ScreenResolution { get; set; }
    public string TimeZone { get; set; }
    public string Language { get; set; }
    public bool CookiesEnabled { get; set; }
    public List<string> Plugins { get; set; }
}
```

### GeolocationInfo
```csharp
public class GeolocationInfo
{
    public string IpAddress { get; set; }
    public string City { get; set; }
    public string Region { get; set; }
    public string Country { get; set; }
    public string CountryCode { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? ISP { get; set; }
    public string? Organization { get; set; }
    public bool IsVPN { get; set; }
    public bool IsTor { get; set; }
}
```

### SecurityAssessment
```csharp
public class SecurityAssessment
{
    public int RiskScore { get; set; } // 0-100
    public List<string> RiskFactors { get; set; }
    public bool IsTrusted { get; set; }
    public bool RequiresVerification { get; set; }
}
```

## Security Considerations

### Risk Assessment Algorithm
- **New Location**: +30 points if user hasn't logged in from this country before
- **VPN Detection**: +25 points for detected VPN usage
- **Tor Network**: +40 points for Tor network access
- **Unknown Location**: +20 points for unresolvable IP addresses
- **Unidentifiable Device**: +15 points for devices without fingerprint

### Trust Levels
- **Trusted**: Risk score < 30
- **Moderate**: Risk score 30-50
- **High Risk**: Risk score > 50 (requires additional verification)

### Privacy Compliance
- IP geolocation uses free public APIs (ip-api.com)
- No personally identifiable information is stored in device fingerprints
- Location data is general (city/country level) rather than precise coordinates
- Users can view and manage their own session data

## Configuration

### Service Registration
The DeviceSessionService is registered in `ServiceCollectionExtensions.cs`:
```csharp
services.AddScoped<IDeviceSessionService, DeviceSessionService>();
```

### Geolocation Service
Currently uses ip-api.com for free IP geolocation. For production, consider:
- Upgrading to a paid service for higher limits and accuracy
- Implementing API key authentication
- Adding fallback services for redundancy

## Usage Examples

### Creating an Enhanced Session
```csharp
var request = new CreateSessionRequest
{
    IpAddress = "192.168.1.100",
    UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36",
    DeviceInfo = new Dictionary<string, string>
    {
        ["screenResolution"] = "1920x1080",
        ["timeZone"] = "America/New_York",
        ["language"] = "en-US",
        ["cookiesEnabled"] = "true"
    },
    JwtTokenId = "token_123"
};

var sessionInfo = await deviceSessionService.CreateEnhancedSessionAsync(userId, tenantId, request);
```

### Checking Location Suspicion
```csharp
var isSuspicious = await deviceSessionService.IsLocationSuspiciousAsync(userId, ipAddress);
if (isSuspicious)
{
    // Require additional verification
}
```

### Getting Session Statistics
```csharp
var stats = await deviceSessionService.GetSessionStatsAsync(userId, 30);
Console.WriteLine($"Total Sessions: {stats.TotalSessions}");
Console.WriteLine($"Active Sessions: {stats.ActiveSessions}");
Console.WriteLine($"Unique Devices: {stats.UniqueDevices}");
```

## Integration Points

### Authentication Service
- Enhanced sessions are created during login
- Session termination integrates with JWT blacklisting
- Risk assessment can trigger additional security measures

### Security Dashboard
- Session metrics contribute to overall security health score
- Active session monitoring for administrators
- Suspicious activity alerts

### Audit Logging
- All session management actions are logged
- Device fingerprint changes are tracked
- Location changes trigger security events

## Future Enhancements

1. **Machine Learning Risk Assessment**: Train models on historical login patterns
2. **Behavioral Analytics**: Track user behavior patterns for anomaly detection
3. **Real-time Notifications**: Instant alerts for suspicious login attempts
4. **Advanced Device Fingerprinting**: Canvas fingerprinting, hardware acceleration detection
5. **Threat Intelligence Integration**: Cross-reference IPs with threat databases
6. **Session Correlation**: Link sessions across different applications
7. **Compliance Reporting**: Generate reports for security audits

## Troubleshooting

### Common Issues

1. **Geolocation Service Unavailable**
   - The service gracefully handles API failures
   - Sessions will still be created without location data
   - Consider implementing fallback geolocation services

2. **High False Positive Risk Scores**
   - Adjust risk scoring thresholds based on your environment
   - Consider whitelisting corporate VPN IP ranges
   - Implement user feedback mechanisms for false positives

3. **Performance Considerations**
   - Geolocation API calls have a 5-second timeout
   - Consider caching location data for repeated IP addresses
   - Monitor API rate limits for geolocation services

### Logging
All device session activities are logged at appropriate levels:
- Info: Successful session creation and termination
- Warning: Failed geolocation lookups, suspicious activities
- Error: Service failures, unexpected exceptions