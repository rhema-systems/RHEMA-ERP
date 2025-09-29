# Session Timeout Implementation

This implementation provides comprehensive session timeout and JWT token refresh functionality based on the security settings configured in the admin panel.

## Features

### ✅ Implemented Features

1. **Dynamic Session Timeout**
   - Reads session timeout from security settings (`sessionTimeoutMinutes`)
   - Default 30 minutes if no settings configured
   - Updates automatically when security settings change

2. **Activity Monitoring**
   - Tracks user activity: mouse movement, clicks, keyboard input, scrolling, touch
   - Throttled to prevent excessive timer resets (max 1 reset per second)
   - Resets session timer on any user activity

3. **Session Warning Dialog**
   - Shows warning popup 120 seconds (2 minutes) before session expires
   - Live countdown timer with progress bar
   - Visual urgency indicators when < 30 seconds remaining
   - Two action buttons: "Continue Session" or "Logout Now"

4. **JWT Token Refresh**
   - Automatic token refresh 5 minutes before JWT expires
   - Manual refresh when extending session
   - Handles refresh token expiry gracefully
   - Prevents multiple simultaneous refresh requests

5. **Session Status Indicator**
   - Shows remaining session time in header
   - Updates in real-time
   - Hidden on mobile for space optimization

6. **Automatic Logout**
   - Clears all tokens when session expires
   - Redirects to login page
   - Can be triggered manually or automatically

## File Structure

### Frontend Components
```
frontend/src/
├── components/
│   ├── session/
│   │   └── session-timeout-dialog.tsx     # Warning popup component
│   ├── layout/
│   │   ├── dashboard-layout.tsx           # Wraps app with session provider
│   │   └── header.tsx                     # Shows session status
│   └── ui/
│       └── progress.tsx                   # Progress bar component
├── contexts/
│   └── session-timeout-context.tsx       # React context provider
├── hooks/
│   └── use-session-timeout.ts            # Main session logic hook
└── services/
    └── token-refresh.service.ts           # JWT refresh service
```

### Backend Components
```
src/ErpSystem.Api/Controllers/
└── AuthController.cs                      # Token refresh endpoint (/api/auth/refresh)
```

## Configuration

### Security Settings
Configure session timeout in the admin panel:
- Navigate to **Administration → Settings → Security**
- Go to **Session** tab
- Set **Session Timeout (Minutes)** (default: 30 minutes)
- Set **Token Lifetime (Minutes)** (default: 60 minutes)

### Warning Timing
- Warning appears: **120 seconds** (2 minutes) before session expires
- Token refresh: **5 minutes** before JWT expires
- Activity throttle: **1 second** maximum frequency

## Usage

### Automatic Integration
The session timeout is automatically enabled for all authenticated pages through the `DashboardLayout` component:

```tsx
<SessionTimeoutProvider enabled={true}>
  {/* Your app content */}
</SessionTimeoutProvider>
```

### Manual Control
For custom components, you can access session functionality:

```tsx
import { useSessionTimeoutContext } from '../contexts/session-timeout-context';

function MyComponent() {
  const { sessionState, extendSession, logout, resetActivity } = useSessionTimeoutContext();
  
  // Check if session is active
  const isActive = sessionState.isActive;
  
  // Check if warning is showing
  const showingWarning = sessionState.showWarning;
  
  // Get remaining time
  const remainingSeconds = sessionState.remainingSeconds;
  
  // Manual actions
  const handleExtendSession = () => extendSession();
  const handleLogout = () => logout();
  const handleResetActivity = () => resetActivity();
}
```

### Optional Session Context
For components that may not have session context available:

```tsx
import { useOptionalSessionTimeoutContext } from '../contexts/session-timeout-context';

function OptionalComponent() {
  const sessionTimeout = useOptionalSessionTimeoutContext();
  
  if (sessionTimeout) {
    // Session context is available
    return <div>Session time: {sessionTimeout.sessionState.sessionTimeoutMinutes}m</div>;
  }
  
  // No session context
  return <div>No session monitoring</div>;
}
```

## How It Works

### Session Flow
1. **User logs in** → Session timer starts
2. **User is active** → Timer resets on activity
3. **User goes idle** → Timer continues counting down
4. **2 minutes before expiry** → Warning dialog appears
5. **User clicks "Continue"** → Token refreshes, timer resets
6. **User clicks "Logout" or timer expires** → Auto logout

### Token Refresh Flow
1. **JWT token issued** with expiration time stored in `localStorage`
2. **5 minutes before expiry** → Automatic refresh attempt
3. **Refresh successful** → New tokens stored, timer updated
4. **Refresh failed** → User logged out
5. **Session extended manually** → Token refreshed immediately

### Activity Monitoring
- Listens for: `mousedown`, `mousemove`, `keypress`, `scroll`, `touchstart`, `click`
- **Throttled** to prevent excessive resets
- **Passive listeners** for better performance
- **Automatic cleanup** when component unmounts

## Security Considerations

### Token Storage
- **Access tokens**: Stored in `localStorage`
- **Refresh tokens**: Stored in `localStorage` 
- **Expiry times**: Stored in `localStorage`
- All tokens cleared on logout

### Network Security
- **Token refresh endpoint**: `/api/auth/refresh`
- **HTTPS required** in production
- **CORS configured** for frontend domain
- **Rate limiting** should be applied to refresh endpoint

### Session Security
- **Activity-based** session management
- **Server-side token validation** required
- **Refresh token rotation** recommended
- **Audit logging** for session events

## Customization

### Timing Configuration
```typescript
// In use-session-timeout.ts
const DEFAULT_SESSION_TIMEOUT = 30; // Default minutes
const WARNING_TIME = 120; // Warning seconds before expiry

// In token-refresh.service.ts
const refreshTime = timeUntilExpiry - (5 * 60 * 1000); // 5 minutes before expiry
```

### UI Customization
```tsx
// Custom warning dialog
<SessionTimeoutDialog
  isOpen={showWarning}
  remainingSeconds={remainingSeconds}
  onExtendSession={extendSession}
  onLogout={logout}
/>

// Custom session indicator
{sessionTimeout && (
  <div className="session-status">
    Time left: {calculateTimeRemaining()}
  </div>
)}
```

### Activity Events
```typescript
// In use-session-timeout.ts - customize monitored events
const events = [
  'mousedown',
  'mousemove', 
  'keypress',
  'scroll',
  'touchstart',
  'click',
  // Add custom events here
];
```

## Error Handling

### Common Issues
1. **Session context not available**: Use `useOptionalSessionTimeoutContext()`
2. **Token refresh fails**: User automatically logged out
3. **Network issues**: Graceful fallback to logout
4. **Multiple tabs**: Each tab manages its own session

### Debug Logging
Session events are logged to console:
- `🔄 Activity reset - session timeout in X minutes`
- `⚠️ Showing session timeout warning`  
- `⏰ Session timeout - logging out`
- `✅ Token refreshed successfully`
- `❌ Token refresh failed`

## Testing

### Manual Testing
1. **Set short session timeout** (e.g., 3 minutes) in security settings
2. **Login and wait** for warning dialog to appear
3. **Test "Continue Session"** button functionality
4. **Test "Logout Now"** button functionality
5. **Test automatic logout** by waiting for countdown to reach zero

### Activity Testing
1. **Login and remain idle** until warning appears
2. **Move mouse or click** to verify activity resets timer
3. **Test throttling** by rapidly clicking/moving mouse

### Token Refresh Testing
1. **Set short JWT lifetime** (e.g., 5 minutes) 
2. **Monitor network tab** for refresh requests
3. **Verify tokens are updated** in localStorage
4. **Test refresh failure** by invalidating refresh token

## Future Enhancements

### Potential Additions
- [ ] **Cross-tab session sync** using localStorage events
- [ ] **Server-side session validation** API endpoint
- [ ] **Configurable activity events** in admin settings
- [ ] **Session analytics** and reporting
- [ ] **Remember device** option for extended sessions
- [ ] **Progressive warning intervals** (10m, 5m, 2m, 30s)
- [ ] **Custom warning messages** per tenant/role
- [ ] **Idle detection API** for more accurate tracking

This implementation provides a robust, configurable, and user-friendly session management system that enhances security while maintaining a good user experience.