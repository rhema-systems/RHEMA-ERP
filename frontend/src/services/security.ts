import { apiService } from './api.service'
import { signalRService, type Notification } from './signalr.service'

// Security-specific interfaces
export interface SecurityMetrics {
  twoFactorAdoptionRate: number
  failedLoginAttempts: number
  activeSessions: number
  securityIncidents: number
  passwordCompliance: number
  auditEventsToday: number
  lastUpdated: string
  trends: {
    twoFactorAdoptionRate: number
    failedLoginAttempts: number
    activeSessions: number
    securityIncidents: number
    passwordCompliance: number
    auditEventsToday: number
  }
}

export interface SecurityAlert {
  id: string
  type: 'critical' | 'warning' | 'info'
  title: string
  message: string
  timestamp: Date
  dismissed: boolean
  severity: number
  category: 'authentication' | 'authorization' | 'data' | 'system' | 'policy'
  source: string
  affectedUser?: string
  ipAddress?: string
  location?: string
  metadata?: Record<string, any>
}

export interface AuditLogEntry {
  id: string
  timestamp: Date
  userId: string
  userName: string
  userEmail: string
  action: string
  resource: string
  result: 'success' | 'failure' | 'warning'
  ipAddress: string
  userAgent: string
  location?: string
  details: string
  metadata?: Record<string, any>
  risk: 'low' | 'medium' | 'high' | 'critical'
}

export interface AuditLogFilter {
  startDate?: Date
  endDate?: Date
  userId?: string
  userName?: string
  action?: string
  resource?: string
  result?: 'success' | 'failure' | 'warning'
  risk?: 'low' | 'medium' | 'high' | 'critical'
  ipAddress?: string
  searchTerm?: string
  page?: number
  pageSize?: number
  sortBy?: string
  sortOrder?: 'asc' | 'desc'
}

export interface AuditLogResponse {
  entries: AuditLogEntry[]
  total: number
  page: number
  pageSize: number
  totalPages: number
  hasNext: boolean
  hasPrevious: boolean
}

export interface DeviceSession {
  id: string
  userId: string
  userName: string
  userEmail: string
  deviceInfo: {
    type: 'desktop' | 'mobile' | 'tablet' | 'unknown'
    os: string
    browser: string
    model?: string
  }
  location: {
    city?: string
    country?: string
    ip: string
    coordinates?: {
      latitude: number
      longitude: number
    }
  }
  session: {
    sessionId: string
    startTime: Date
    lastActivity: Date
    isActive: boolean
    duration: number
  }
  security: {
    isTrusted: boolean
    riskScore: number
    flags: string[]
    twoFactorEnabled: boolean
  }
}

// User Session interfaces for the Session API
export interface UserSession {
  sessionId: string
  userId: string
  username: string
  role: string
  email: string
  ipAddress: string
  userAgent: string
  deviceType: string
  browser: string
  operatingSystem: string
  location: string
  loginTime: Date
  lastActivityTime: Date
  sessionDuration: string
}

export interface UserSessionHistory extends UserSession {
  logoutTime?: Date
  isActive: boolean
  terminationReason?: string
  wasTerminatedByConcurrentLogin: boolean
}

export interface SessionFilter {
  username?: string
  ipAddress?: string
  deviceType?: string
  loginTimeAfter?: Date
  lastActivityAfter?: Date
}

export interface TerminateSessionRequest {
  reason?: string
}

export interface BulkTerminateSessionsRequest {
  sessionIds: string[]
  reason?: string
}

export interface TerminateSessionsByCriteriaRequest {
  ipAddressPattern?: string
  deviceType?: string
  browser?: string
  loginTimeBefore?: Date
  lastActivityBefore?: Date
  includeCurrentUser?: boolean
  reason?: string
}

export interface SessionOperationResult {
  sessionId: string
  success: boolean
  message: string
}

export interface BulkSessionOperationResponse {
  totalRequested: number
  successful: number
  failed: number
  message?: string
  results: SessionOperationResult[]
}

export interface SecurityHealthScore {
  overall: number
  categories: {
    passwordPolicies: number
    twoFactorAdoption: number
    sessionSecurity: number
    accessControls: number
    auditCompliance: number
  }
  recommendations: {
    category: string
    message: string
    priority: 'low' | 'medium' | 'high' | 'critical'
    actionRequired: boolean
  }[]
  lastCalculated: Date
}

export interface ThreatDetection {
  id: string
  type: 'brute_force' | 'anomalous_login' | 'privilege_escalation' | 'data_exfiltration' | 'suspicious_behavior'
  severity: 'low' | 'medium' | 'high' | 'critical'
  title: string
  description: string
  timestamp: Date
  source: string
  target?: string
  indicators: {
    key: string
    value: string
    risk: number
  }[]
  status: 'active' | 'investigating' | 'resolved' | 'false_positive'
  assignedTo?: string
  resolution?: string
  metadata: Record<string, any>
}

// Two-Factor Authentication interfaces
export interface TwoFactorSettings {
  isEnabled: boolean
  recoveryCodes: string[]
  authenticatorKey?: string
  enabledAt?: Date
  backupCodesGeneratedAt?: string
  recoveryCodesRemaining: number
}

export interface TwoFactorSetup {
  authenticatorKey: string
  qrCodeUrl: string
  recoveryCodes: string[]
  instructions: string
}

export interface EnableTwoFactorRequest {
  verificationCode: string
  password: string
}

export interface DisableTwoFactorRequest {
  password: string
  reason?: string
}

// Security Settings interfaces
export interface SecuritySettings {
  // Password Policy
  passwordMinLength: number
  passwordRequireUppercase: boolean
  passwordRequireLowercase: boolean
  passwordRequireDigits: boolean
  passwordRequireSpecialChars: boolean
  passwordMaxAge?: number
  passwordPreventReuse?: number

  // Session & Token
  sessionTimeoutMinutes: number
  jwtTokenLifetimeMinutes: number
  preventConcurrentLogin: string

  // Lockout & Rate Limiting
  maxFailedLoginAttempts: number
  accountLockoutMinutes: number
  rateLimitLoginMaxAttempts: number
  rateLimitLoginWindowMinutes: number
  rateLimitLoginBlockDurationMinutes: number

  // CAPTCHA
  captchaEnabled: boolean
  captchaProvider: string
  recaptchaSiteKey?: string
  recaptchaSecretKey?: string
  hCaptchaSiteKey?: string
  hCaptchaSecretKey?: string

  // Legal URLs
  termsOfServiceUrl?: string
  privacyPolicyUrl?: string
}

class SecurityService {
  private alertHandlers: Set<(alert: SecurityAlert) => void> = new Set()
  private metricsHandlers: Set<(metrics: SecurityMetrics) => void> = new Set()
  private threatHandlers: Set<(threat: ThreatDetection) => void> = new Set()
  
  constructor() {
    this.setupSignalRHandlers()
  }

  private setupSignalRHandlers() {
    // Subscribe to security-related SignalR notifications
    signalRService.onNotification((notification: Notification) => {
      if (notification.type.startsWith('security.')) {
        this.handleSecurityNotification(notification)
      }
    })
  }

  private handleSecurityNotification(notification: Notification) {
    switch (notification.type) {
      case 'security.alert':
        if (notification.metadata) {
          const alert: SecurityAlert = {
            id: notification.id,
            type: notification.severity === 'error' ? 'critical' : 
                  notification.severity === 'warning' ? 'warning' : 'info',
            title: notification.title,
            message: notification.message,
            timestamp: new Date(notification.timestamp),
            dismissed: false,
            severity: notification.metadata.severity || 1,
            category: notification.metadata.category || 'system',
            source: notification.metadata.source || 'system',
            affectedUser: notification.metadata.affectedUser,
            ipAddress: notification.metadata.ipAddress,
            location: notification.metadata.location,
            metadata: notification.metadata
          }
          this.notifyAlertHandlers(alert)
        }
        break
      
      case 'security.metrics':
        if (notification.metadata?.metrics) {
          this.notifyMetricsHandlers(notification.metadata.metrics)
        }
        break
      
      case 'security.threat':
        if (notification.metadata) {
          const threat: ThreatDetection = {
            id: notification.id,
            type: notification.metadata.type || 'suspicious_behavior',
            severity: notification.metadata.severity || 'medium',
            title: notification.title,
            description: notification.message,
            timestamp: new Date(notification.timestamp),
            source: notification.metadata.source || 'system',
            target: notification.metadata.target,
            indicators: notification.metadata.indicators || [],
            status: notification.metadata.status || 'active',
            assignedTo: notification.metadata.assignedTo,
            resolution: notification.metadata.resolution,
            metadata: notification.metadata
          }
          this.notifyThreatHandlers(threat)
        }
        break
    }
  }

  // Event subscription methods
  onSecurityAlert(handler: (alert: SecurityAlert) => void): () => void {
    this.alertHandlers.add(handler)
    return () => this.alertHandlers.delete(handler)
  }

  onSecurityMetrics(handler: (metrics: SecurityMetrics) => void): () => void {
    this.metricsHandlers.add(handler)
    return () => this.metricsHandlers.delete(handler)
  }

  onThreatDetection(handler: (threat: ThreatDetection) => void): () => void {
    this.threatHandlers.add(handler)
    return () => this.threatHandlers.delete(handler)
  }

  private notifyAlertHandlers(alert: SecurityAlert) {
    this.alertHandlers.forEach(handler => {
      try {
        handler(alert)
      } catch (error) {
        console.error('Error in security alert handler:', error)
      }
    })
  }

  private notifyMetricsHandlers(metrics: SecurityMetrics) {
    this.metricsHandlers.forEach(handler => {
      try {
        handler(metrics)
      } catch (error) {
        console.error('Error in security metrics handler:', error)
      }
    })
  }

  private notifyThreatHandlers(threat: ThreatDetection) {
    this.threatHandlers.forEach(handler => {
      try {
        handler(threat)
      } catch (error) {
        console.error('Error in threat detection handler:', error)
      }
    })
  }

  // API Methods
  async getSecurityMetrics(): Promise<SecurityMetrics> {
    try {
      const response = await apiService.request<SecurityMetrics>('/security/metrics')
      return response
    } catch (error) {
      console.warn('Failed to fetch security metrics, using fallback data:', error)
      // Return fallback metrics
      return {
        twoFactorAdoptionRate: 85,
        failedLoginAttempts: 23,
        activeSessions: 142,
        securityIncidents: 3,
        passwordCompliance: 92,
        auditEventsToday: 1247,
        lastUpdated: new Date().toISOString(),
        trends: {
          twoFactorAdoptionRate: 12,
          failedLoginAttempts: -15,
          activeSessions: 8,
          securityIncidents: 0,
          passwordCompliance: 5,
          auditEventsToday: 22
        }
      }
    }
  }

  async getSecurityAlerts(dismissed = false): Promise<SecurityAlert[]> {
    try {
      const response = await apiService.request<SecurityAlert[]>(`/security/alerts?dismissed=${dismissed}`)
      return response.map(alert => ({
        ...alert,
        timestamp: new Date(alert.timestamp)
      }))
    } catch (error) {
      console.warn('Failed to fetch security alerts, using fallback data:', error)
      // Return fallback alerts
      return [
        {
          id: '1',
          type: 'critical',
          title: 'Multiple Failed Login Attempts',
          message: 'User john.doe@company.com has 8 failed login attempts from IP 192.168.1.100',
          timestamp: new Date(Date.now() - 15 * 60 * 1000),
          dismissed: false,
          severity: 5,
          category: 'authentication',
          source: 'login_monitor',
          affectedUser: 'john.doe@company.com',
          ipAddress: '192.168.1.100'
        },
        {
          id: '2',
          type: 'warning',
          title: 'Unusual Login Location',
          message: 'Login detected from new location: Tokyo, Japan for user alice@company.com',
          timestamp: new Date(Date.now() - 45 * 60 * 1000),
          dismissed: false,
          severity: 3,
          category: 'authentication',
          source: 'geo_monitor',
          affectedUser: 'alice@company.com',
          location: 'Tokyo, Japan'
        }
      ]
    }
  }

  async dismissAlert(alertId: string): Promise<void> {
    try {
      await apiService.request(`/security/alerts/${alertId}/dismiss`, {
        method: 'POST'
      })
    } catch (error) {
      console.error('Failed to dismiss alert:', error)
      throw error
    }
  }

  async getAuditLogs(filter: AuditLogFilter = {}): Promise<AuditLogResponse> {
    try {
      const queryParams = new URLSearchParams()
      
      if (filter.startDate) queryParams.append('startDate', filter.startDate.toISOString())
      if (filter.endDate) queryParams.append('endDate', filter.endDate.toISOString())
      if (filter.userId) queryParams.append('userId', filter.userId)
      if (filter.userName) queryParams.append('userName', filter.userName)
      if (filter.action) queryParams.append('action', filter.action)
      if (filter.resource) queryParams.append('resource', filter.resource)
      if (filter.result) queryParams.append('result', filter.result)
      if (filter.risk) queryParams.append('risk', filter.risk)
      if (filter.ipAddress) queryParams.append('ipAddress', filter.ipAddress)
      if (filter.searchTerm) queryParams.append('searchTerm', filter.searchTerm)
      if (filter.page) queryParams.append('page', filter.page.toString())
      if (filter.pageSize) queryParams.append('pageSize', filter.pageSize.toString())
      if (filter.sortBy) queryParams.append('sortBy', filter.sortBy)
      if (filter.sortOrder) queryParams.append('sortOrder', filter.sortOrder)

      const response = await apiService.request<AuditLogResponse>(`/security/audit-logs?${queryParams.toString()}`)
      
      return {
        ...response,
        entries: response.entries.map(entry => ({
          ...entry,
          timestamp: new Date(entry.timestamp)
        }))
      }
    } catch (error) {
      console.warn('Failed to fetch audit logs, using fallback data:', error)
      
      // Return fallback audit logs
      const mockEntries: AuditLogEntry[] = [
        {
          id: '1',
          timestamp: new Date(Date.now() - 10 * 60 * 1000),
          userId: 'user-123',
          userName: 'John Doe',
          userEmail: 'john.doe@company.com',
          action: 'LOGIN_SUCCESS',
          resource: 'Authentication',
          result: 'success',
          ipAddress: '192.168.1.100',
          userAgent: 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36',
          location: 'New York, NY',
          details: 'User successfully logged in with 2FA',
          risk: 'low'
        },
        {
          id: '2',
          timestamp: new Date(Date.now() - 25 * 60 * 1000),
          userId: 'user-456',
          userName: 'Jane Smith',
          userEmail: 'jane.smith@company.com',
          action: 'PASSWORD_CHANGE',
          resource: 'User Management',
          result: 'success',
          ipAddress: '192.168.1.105',
          userAgent: 'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7)',
          location: 'San Francisco, CA',
          details: 'User changed password successfully',
          risk: 'medium'
        }
      ]

      return {
        entries: mockEntries,
        total: mockEntries.length,
        page: filter.page || 1,
        pageSize: filter.pageSize || 50,
        totalPages: 1,
        hasNext: false,
        hasPrevious: false
      }
    }
  }

async getDeviceSessions(): Promise<DeviceSession[]> {
    try {
      const response = await apiService.request<DeviceSession[]>('/security/device-sessions')
      return response.map(session => ({
        ...session,
        session: {
          ...session.session,
          startTime: new Date(session.session.startTime),
          lastActivity: new Date(session.session.lastActivity)
        }
      }))
    } catch (error) {
      console.warn('Failed to fetch device sessions, using fallback data:', error)
      
      // Return fallback device sessions
      return [
        {
          id: '1',
          userId: 'user-123',
          userName: 'John Doe',
          userEmail: 'john.doe@company.com',
          deviceInfo: {
            type: 'desktop',
            os: 'Windows 10',
            browser: 'Chrome 120.0'
          },
          location: {
            city: 'New York',
            country: 'USA',
            ip: '192.168.1.100'
          },
          session: {
            sessionId: 'session-123',
            startTime: new Date(Date.now() - 2 * 60 * 60 * 1000),
            lastActivity: new Date(Date.now() - 5 * 60 * 1000),
            isActive: true,
            duration: 2 * 60 * 60 * 1000
          },
          security: {
            isTrusted: true,
            riskScore: 2,
            flags: [],
            twoFactorEnabled: true
          }
        }
      ]
    }
  }

  // User session management methods using the new Session API
  async getAllActiveSessions(filter?: SessionFilter): Promise<UserSession[]> {
    try {
      const queryParams = new URLSearchParams()
      
      if (filter?.username) queryParams.append('username', filter.username)
      if (filter?.ipAddress) queryParams.append('ipAddress', filter.ipAddress)
      if (filter?.deviceType) queryParams.append('deviceType', filter.deviceType)
      if (filter?.loginTimeAfter) queryParams.append('loginTimeAfter', filter.loginTimeAfter.toISOString())
      if (filter?.lastActivityAfter) queryParams.append('lastActivityAfter', filter.lastActivityAfter.toISOString())

      const response = await apiService.request<UserSession[]>(`/session/active?${queryParams.toString()}`)
      return response.map(session => ({
        ...session,
        loginTime: new Date(session.loginTime),
        lastActivityTime: new Date(session.lastActivityTime)
      }))
    } catch (error) {
      console.warn('Failed to fetch active sessions:', error)
      throw error
    }
  }

  async getUserSessions(userId: string): Promise<UserSession[]> {
    try {
      const response = await apiService.request<UserSession[]>(`/session/user/${userId}`)
      return response.map(session => ({
        ...session,
        loginTime: new Date(session.loginTime),
        lastActivityTime: new Date(session.lastActivityTime)
      }))
    } catch (error) {
      console.warn(`Failed to fetch sessions for user ${userId}:`, error)
      throw error
    }
  }

  async getUserSessionHistory(userId: string, days: number = 30): Promise<UserSessionHistory[]> {
    try {
      const response = await apiService.request<UserSessionHistory[]>(`/session/user/${userId}/history?days=${days}`)
      return response.map(session => ({
        ...session,
        loginTime: new Date(session.loginTime),
        lastActivityTime: new Date(session.lastActivityTime),
        logoutTime: session.logoutTime ? new Date(session.logoutTime) : undefined
      }))
    } catch (error) {
      console.warn(`Failed to fetch session history for user ${userId}:`, error)
      throw error
    }
  }

  async terminateSession(sessionId: string, reason?: string): Promise<void> {
    try {
      await apiService.request(`/session/${sessionId}/terminate`, {
        method: 'POST',
        body: JSON.stringify({ reason })
      })
    } catch (error) {
      console.error('Failed to terminate session:', error)
      throw error
    }
  }

  async terminateAllUserSessions(userId: string, reason?: string): Promise<void> {
    try {
      await apiService.request(`/session/user/${userId}/terminate-all`, {
        method: 'POST',
        body: JSON.stringify({ reason })
      })
    } catch (error) {
      console.error(`Failed to terminate all sessions for user ${userId}:`, error)
      throw error
    }
  }

  async bulkTerminateSessions(sessionIds: string[], reason?: string): Promise<BulkSessionOperationResponse> {
    try {
      const request: BulkTerminateSessionsRequest = {
        sessionIds,
        reason
      }
      return await apiService.request<BulkSessionOperationResponse>('/session/bulk-terminate', {
        method: 'POST',
        body: JSON.stringify(request)
      })
    } catch (error) {
      console.error('Failed to bulk terminate sessions:', error)
      throw error
    }
  }

  async terminateSessionsByCriteria(
    criteria: TerminateSessionsByCriteriaRequest
  ): Promise<BulkSessionOperationResponse> {
    try {
      return await apiService.request<BulkSessionOperationResponse>('/session/terminate-by-criteria', {
        method: 'POST',
        body: JSON.stringify(criteria)
      })
    } catch (error) {
      console.error('Failed to terminate sessions by criteria:', error)
      throw error
    }
  }

  async getSecurityHealthScore(): Promise<SecurityHealthScore> {
    try {
      const response = await apiService.request<SecurityHealthScore>('/security/health-score')
      return {
        ...response,
        lastCalculated: new Date(response.lastCalculated)
      }
    } catch (error) {
      console.warn('Failed to fetch security health score, using fallback data:', error)
      
      return {
        overall: 87,
        categories: {
          passwordPolicies: 92,
          twoFactorAdoption: 85,
          sessionSecurity: 78,
          accessControls: 95,
          auditCompliance: 88
        },
        recommendations: [
          {
            category: 'Two-Factor Authentication',
            message: 'Increase 2FA adoption rate to reach 95% target',
            priority: 'medium',
            actionRequired: true
          },
          {
            category: 'Session Security',
            message: 'Review session timeout policies for better security',
            priority: 'low',
            actionRequired: false
          }
        ],
        lastCalculated: new Date()
      }
    }
  }

  async getThreatDetections(): Promise<ThreatDetection[]> {
    try {
      const response = await apiService.request<ThreatDetection[]>('/security/threats')
      return response.map(threat => ({
        ...threat,
        timestamp: new Date(threat.timestamp)
      }))
    } catch (error) {
      console.warn('Failed to fetch threat detections, using fallback data:', error)
      
      return [
        {
          id: '1',
          type: 'brute_force',
          severity: 'critical',
          title: 'Brute Force Attack Detected',
          description: 'Multiple failed login attempts from IP 192.168.1.100',
          timestamp: new Date(Date.now() - 30 * 60 * 1000),
          source: 'login_monitor',
          target: 'john.doe@company.com',
          indicators: [
            { key: 'failed_attempts', value: '15', risk: 5 },
            { key: 'time_window', value: '5 minutes', risk: 4 }
          ],
          status: 'active',
          metadata: {
            ipAddress: '192.168.1.100',
            userAgent: 'automated_tool'
          }
        }
      ]
    }
  }

  async updateThreatStatus(threatId: string, status: ThreatDetection['status'], resolution?: string): Promise<void> {
    try {
      await apiService.request(`/security/threats/${threatId}`, {
        method: 'PATCH',
        body: JSON.stringify({ status, resolution })
      })
    } catch (error) {
      console.error('Failed to update threat status:', error)
      throw error
    }
  }

  // Security Settings methods
  async getSecuritySettings(): Promise<SecuritySettings> {
    try {
      const response = await apiService.request<SecuritySettings>('/settings/security')
      return response
    } catch (error) {
      console.error('Failed to fetch security settings:', error)
      throw error
    }
  }

  async updateSecuritySettings(settings: Partial<SecuritySettings>): Promise<SecuritySettings> {
    try {
      const response = await apiService.request<SecuritySettings>('/settings/security', {
        method: 'PUT',
        body: JSON.stringify(settings)
      })
      return response
    } catch (error) {
      console.error('Failed to update security settings:', error)
      throw error
    }
  }

  // Two-Factor Authentication methods
  async getTwoFactorSettings(): Promise<TwoFactorSettings> {
    try {
      const response = await apiService.request<TwoFactorSettings>('/security/two-factor')
      return {
        ...response,
        enabledAt: response.enabledAt ? new Date(response.enabledAt) : undefined
      }
    } catch (error) {
      console.error('Failed to fetch 2FA settings:', error)
      throw error
    }
  }

  async enableTwoFactor(request: EnableTwoFactorRequest): Promise<TwoFactorSetup> {
    try {
      const response = await apiService.request<TwoFactorSetup>('/security/two-factor/enable', {
        method: 'POST',
        body: JSON.stringify(request)
      })
      return response
    } catch (error) {
      console.error('Failed to enable 2FA:', error)
      throw error
    }
  }

  async disableTwoFactor(request: DisableTwoFactorRequest): Promise<void> {
    try {
      await apiService.request('/security/two-factor/disable', {
        method: 'POST',
        body: JSON.stringify(request)
      })
    } catch (error) {
      console.error('Failed to disable 2FA:', error)
      throw error
    }
  }
}

export const securityService = new SecurityService()
export default securityService