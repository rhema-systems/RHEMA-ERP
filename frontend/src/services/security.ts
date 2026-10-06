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

export interface SecurityPolicy {
  id: string
  name: string
  description: string
  policyType: string
  isEnabled: boolean
  configuration: Record<string, unknown>
  lastModified: Date
  modifiedBy: string
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

export type SecurityRange = '24h' | '7d' | '30d'

export interface SecurityOperationsOverview {
  range: { key: string; startUtc: string; endUtc: string }
  snapshot: {
    eligibleUsers: number
    activeUsers: number
    mfaEnabledUsers: number
    mfaAdoptionPercent: number
    privilegedUsers: number
    privilegedUsersWithoutMfa: number
    lockedAccounts: number
    activeSessions: number
    staleSessions: number
    disabledUsersWithActiveSessions: number
    sessionIdleThresholdMinutes: number
  }
  activity: {
    successfulLogins: number
    failedLogins: number
    loginFailureRatePercent: number
    passwordChanges: number
    securityEvents: number
    auditEvents: number
  }
  health: {
    score: number
    maximumScore: number
    modelVersion: string
    factors: Array<{
      key: string
      name: string
      earnedPoints: number
      maximumPoints: number
      status: 'healthy' | 'attention' | 'action-required' | 'unavailable'
      evidence: string
    }>
  }
  alerts: {
    openTotal: number
    critical: number
    high: number
    medium: number
    items: Array<{
      id: string
      source: string
      severity: string
      status: string
      title: string
      description: string
      timestampUtc: string
      userName?: string
      ipAddress?: string
    }>
  }
  recentEvents: SecurityOperationsEvent[]
  privilegedUsers: Array<{
    userId: string
    displayName: string
    userName: string
    email: string
    roles: string[]
    privilegeLevel: string
    isActive: boolean
    mfaEnabled: boolean
    isLocked: boolean
    lastLoginUtc?: string
    createdAtUtc: string
    findings: string[]
  }>
  authenticationTrend: Array<{
    periodStartUtc: string
    label: string
    successful: number
    failed: number
  }>
  configuration: {
    persisted: boolean
    passwordMinimumLength?: number
    failedAttemptThreshold?: number
    lockoutMinutes?: number
    sessionTimeoutMinutes?: number
    accessTokenLifetimeMinutes?: number
    concurrentLoginPolicy: string
    loginRateLimitingConfigured: boolean
    captchaEnabled: boolean
    captchaConfigured: boolean
  }
  retention: {
    configured: boolean
    enabled: boolean
    auditLogRetentionDays?: number
    securityLogRetentionDays?: number
    lastRunStartedAtUtc?: string
    lastRunCompletedAtUtc?: string
    lastRunSucceeded?: boolean
  }
  generatedAtUtc: string
}

export interface SecurityOperationsEvent {
  id: string
  timestampUtc: string
  source: string
  eventType: string
  severity: string
  title: string
  userName?: string
  ipAddress?: string
  success?: boolean
  evidence: string
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
  async getOperationsOverview(range: SecurityRange = '24h'): Promise<SecurityOperationsOverview> {
    return apiService.request<SecurityOperationsOverview>(`/security/operations/overview?range=${range}`)
  }

  async getSecurityMetrics(): Promise<SecurityMetrics> {
    return await apiService.request<SecurityMetrics>('/security/metrics')
  }

  async getSecurityAlerts(dismissed = false): Promise<SecurityAlert[]> {
    const response = await apiService.request<SecurityAlert[]>(`/security/alerts?dismissed=${dismissed}`)
    return response.map(alert => ({
      ...alert,
      timestamp: new Date(alert.timestamp)
    }))
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
  }

async getDeviceSessions(): Promise<DeviceSession[]> {
    const response = await apiService.request<DeviceSession[]>('/security/device-sessions')
    return response.map(session => ({
      ...session,
      session: {
        ...session.session,
        startTime: new Date(session.session.startTime),
        lastActivity: new Date(session.session.lastActivity)
      }
    }))
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
    const response = await apiService.request<SecurityHealthScore>('/security/health-score')
    return {
      ...response,
      lastCalculated: new Date(response.lastCalculated)
    }
  }

  async getThreatDetections(): Promise<ThreatDetection[]> {
    const response = await apiService.request<ThreatDetection[]>('/security/threats')
    return response.map(threat => ({
      ...threat,
      timestamp: new Date(threat.timestamp)
    }))
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

  async getSecurityPolicies(): Promise<SecurityPolicy[]> {
    const response = await apiService.request<SecurityPolicy[]>('/security/policies')
    return response.map(policy => ({
      ...policy,
      lastModified: new Date(policy.lastModified)
    }))
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
