'use client';

import React, { useState, useEffect } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '../../../../components/ui/card'
import { Button } from '../../../../components/ui/button'
import { Badge } from '../../../../components/ui/badge'
import { Alert, AlertDescription } from '../../../../components/ui/alert'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '../../../../components/ui/tabs'
import { Input } from '../../../../components/ui/input'
import { Label } from '../../../../components/ui/label'
import { Switch } from '../../../../components/ui/switch'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '../../../../components/ui/select'
import { TwoFactorAuth } from '../../../../components/security/TwoFactorAuth'
import { AuditLog } from '../../../../components/security/AuditLog'
import { DeviceManagement } from '../../../../components/security/DeviceManagement'
import { SecurityPolicies } from '../../../../components/security/SecurityPolicies'
import { LoginAppearanceSettings } from '../../../../components/security/LoginAppearanceSettings'
import { SessionManagementTab } from '@/components/admin/SessionManagementTab';
import { useToast } from '../../../../hooks/use-toast'
import { useAuth } from '../../../../hooks/use-auth'
import { settingsService } from '../../../../services/settings'
import { securityService, type SecurityMetrics, type SecurityAlert } from '../../../../services/security'
import { authService } from '../../../../services/auth'
import type { LoginPageStyle, SecuritySettings as SecuritySettingsDto } from '../../../../services/settings'
import { 
  Shield, 
  Users, 
  AlertTriangle, 
  CheckCircle, 
  Lock, 
  Key,
  FileText,
  Settings,
  Activity,
  TrendingUp,
  TrendingDown,
  Target,
  Zap,
  Clock,
  Bot,
  Save,
  Loader2,
  Monitor,
  MonitorCog
} from 'lucide-react'
import { ClientOnly } from '../../../../components/ui/client-only'

interface SecurityMetricUI {
  label: string
  value: string | number
  change?: string
  trend?: 'up' | 'down' | 'stable'
  status?: 'good' | 'warning' | 'critical'
  icon: React.ComponentType<any>
}

// Schema definitions for form validation
const passwordPolicySchema = z.object({
  passwordMinLength: z.number().min(8, 'Password length must be at least 8 characters').max(128, 'Password length cannot exceed 128 characters'),
  passwordRequireUppercase: z.boolean(),
  passwordRequireLowercase: z.boolean(),
  passwordRequireDigits: z.boolean(),
  passwordRequireSpecialChars: z.boolean(),
  passwordMaxAge: z.number().min(0, 'Password expiry days must be 0 or greater').max(365, 'Password expiry cannot exceed 365 days').nullable(),
  passwordPreventReuse: z.number().min(0, 'Password history must be 0 or greater').max(24, 'Password history cannot exceed 24').nullable(),
});

const sessionSchema = z.object({
  sessionTimeoutMinutes: z.number().min(5, 'Session timeout must be at least 5 minutes').max(1440),
  jwtTokenLifetimeMinutes: z.number().min(5, 'Token lifetime must be at least 5 minutes').max(480),
  preventConcurrentLogin: z.enum(['Disabled', 'LogoutFromAllDevices', 'PreventSubsequentLogins']),
});

const lockoutSchema = z.object({
  maxFailedLoginAttempts: z.number().min(1).max(20),
  accountLockoutMinutes: z.number().min(1).max(1440),
  rateLimitLoginMaxAttempts: z.number().min(1).max(20),
  rateLimitLoginWindowMinutes: z.number().min(1).max(60),
  rateLimitLoginBlockDurationMinutes: z.number().min(1).max(1440),
});

const recaptchaSchema = z.object({
  captchaEnabled: z.boolean(),
  captchaProvider: z.enum(['recaptcha', 'hcaptcha']),
  recaptchaSiteKey: z.string().nullable(),
  recaptchaSecretKey: z.string().nullable(),
  hCaptchaSiteKey: z.string().nullable(),
  hCaptchaSecretKey: z.string().nullable(),
});

const legalSchema = z.object({
  termsOfServiceUrl: z.union([z.string().url('Must be a valid URL'), z.literal(''), z.null()]),
  privacyPolicyUrl: z.union([z.string().url('Must be a valid URL'), z.literal(''), z.null()]),
});

const securitySettingsSchema = z.object({
  ...passwordPolicySchema.shape,
  ...sessionSchema.shape,
  ...lockoutSchema.shape,
  ...recaptchaSchema.shape,
  ...legalSchema.shape,
});

type SecuritySettingsFormValues = z.input<typeof securitySettingsSchema>

const defaultSecuritySettingsFormValues = (): SecuritySettingsFormValues => ({
  passwordMinLength: 8,
  passwordRequireUppercase: true,
  passwordRequireLowercase: true,
  passwordRequireDigits: true,
  passwordRequireSpecialChars: true,
  passwordMaxAge: 90,
  passwordPreventReuse: 5,
  sessionTimeoutMinutes: 30,
  jwtTokenLifetimeMinutes: 60,
  preventConcurrentLogin: 'Disabled',
  maxFailedLoginAttempts: 5,
  accountLockoutMinutes: 30,
  rateLimitLoginMaxAttempts: 5,
  rateLimitLoginWindowMinutes: 15,
  rateLimitLoginBlockDurationMinutes: 30,
  captchaEnabled: false,
  captchaProvider: 'recaptcha',
  recaptchaSiteKey: '',
  recaptchaSecretKey: '',
  hCaptchaSiteKey: '',
  hCaptchaSecretKey: '',
  termsOfServiceUrl: '',
  privacyPolicyUrl: '',
})

const toSecuritySettingsFormValues = (
  settings?: SecuritySettingsDto | null
): SecuritySettingsFormValues => ({
  passwordMinLength: settings?.passwordMinLength ?? 8,
  passwordRequireUppercase: Boolean(settings?.passwordRequireUppercase),
  passwordRequireLowercase: Boolean(settings?.passwordRequireLowercase),
  passwordRequireDigits: Boolean(settings?.passwordRequireDigits),
  passwordRequireSpecialChars: Boolean(settings?.passwordRequireSpecialChars),
  passwordMaxAge: settings?.passwordMaxAge ?? null,
  passwordPreventReuse: settings?.passwordPreventReuse ?? null,
  sessionTimeoutMinutes: settings?.sessionTimeoutMinutes ?? 30,
  jwtTokenLifetimeMinutes: settings?.jwtTokenLifetimeMinutes ?? 60,
  preventConcurrentLogin: settings?.preventConcurrentLogin ?? 'Disabled',
  maxFailedLoginAttempts: settings?.maxFailedLoginAttempts ?? 5,
  accountLockoutMinutes: settings?.accountLockoutMinutes ?? 30,
  rateLimitLoginMaxAttempts: settings?.rateLimitLoginMaxAttempts ?? 5,
  rateLimitLoginWindowMinutes: settings?.rateLimitLoginWindowMinutes ?? 15,
  rateLimitLoginBlockDurationMinutes:
    settings?.rateLimitLoginBlockDurationMinutes ?? 30,
  captchaEnabled: Boolean(settings?.captchaEnabled),
  captchaProvider: settings?.captchaProvider ?? 'recaptcha',
  recaptchaSiteKey: settings?.recaptchaSiteKey ?? '',
  recaptchaSecretKey: settings?.recaptchaSecretKey ?? '',
  hCaptchaSiteKey: settings?.hCaptchaSiteKey ?? '',
  hCaptchaSecretKey: settings?.hCaptchaSecretKey ?? '',
  termsOfServiceUrl: settings?.termsOfServiceUrl ?? '',
  privacyPolicyUrl: settings?.privacyPolicyUrl ?? '',
})

const toSecuritySettingsDto = (
  data: SecuritySettingsFormValues
): SecuritySettingsDto => ({
  ...data,
  recaptchaSiteKey: data.recaptchaSiteKey?.trim() || null,
  recaptchaSecretKey: data.recaptchaSecretKey?.trim() || null,
  hCaptchaSiteKey: data.hCaptchaSiteKey?.trim() || null,
  hCaptchaSecretKey: data.hCaptchaSecretKey?.trim() || null,
  termsOfServiceUrl: data.termsOfServiceUrl?.trim() || null,
  privacyPolicyUrl: data.privacyPolicyUrl?.trim() || null,
})

export default function SecurityDashboardPage() {
  const [activeTab, setActiveTab] = useState('overview')
  const [settingsTab, setSettingsTab] = useState('password')
  const [selectedLoginPageStyle, setSelectedLoginPageStyle] = useState<LoginPageStyle>('LightCorporate')
  const [realTimeAlerts, setRealTimeAlerts] = useState<SecurityAlert[]>([])
  const [realTimeMetrics, setRealTimeMetrics] = useState<SecurityMetrics | null>(null)
  const { toast } = useToast()
  const queryClient = useQueryClient()
  const { hasAnyRole } = useAuth()
  const canManageLoginAppearance = hasAnyRole(['SuperAdmin', 'TenantAdmin'])
  const currentUser = authService.getStoredUser()

  // Fetch current security settings
  const { data: settings, isLoading: settingsLoading, error: settingsError } = useQuery({
    queryKey: ['securitySettings'],
    queryFn: () => settingsService.getSecuritySettings(),
  })

  const {
    data: loginAppearance,
    isLoading: isLoginAppearanceLoading,
    isError: isLoginAppearanceError,
  } = useQuery({
    queryKey: ['loginAppearanceSettings'],
    queryFn: () => settingsService.getLoginAppearance(),
    enabled: canManageLoginAppearance,
  })

  // Fetch security metrics
  const { data: securityMetrics, isLoading: metricsLoading, error: metricsError } = useQuery({
    queryKey: ['securityMetrics'],
    queryFn: () => securityService.getSecurityMetrics(),
    refetchInterval: 30000, // Refresh every 30 seconds
  })

  // Fetch security alerts
  const { data: securityAlerts, isLoading: alertsLoading, error: alertsError, refetch: refetchAlerts } = useQuery({
    queryKey: ['securityAlerts'],
    queryFn: () => securityService.getSecurityAlerts(false),
    refetchInterval: 15000, // Refresh every 15 seconds
  })

  // Fetch security health score
  const { data: securityHealth, isLoading: healthLoading, error: healthError } = useQuery({
    queryKey: ['securityHealthScore'],
    queryFn: () => securityService.getSecurityHealthScore(),
    refetchInterval: 60000, // Refresh every minute
  })

  // Fetch live threat detections
  const { data: threatDetections, isLoading: threatsLoading, error: threatsError } = useQuery({
    queryKey: ['securityThreats'],
    queryFn: () => securityService.getThreatDetections(),
    refetchInterval: 30000,
  })

  // Fetch current user's 2FA state
  const { data: twoFactorSettings, error: twoFactorError } = useQuery({
    queryKey: ['securityTwoFactorSettings'],
    queryFn: () => securityService.getTwoFactorSettings(),
    staleTime: 30000,
  })

  // Form for security settings
  const {
    register,
    handleSubmit,
    formState: { errors },
    watch,
    setValue,
    reset,
  } = useForm<SecuritySettingsFormValues>({
    resolver: zodResolver(securitySettingsSchema),
    defaultValues: defaultSecuritySettingsFormValues(),
  })

  // Update form when settings are loaded
  useEffect(() => {
    if (!settings) return

    reset(toSecuritySettingsFormValues(settings))
  }, [settings, reset])

  useEffect(() => {
    if (loginAppearance) {
      setSelectedLoginPageStyle(loginAppearance.loginPageStyle)
    }
  }, [loginAppearance])

  useEffect(() => {
    const parameters = new URLSearchParams(window.location.search)
    if (parameters.get('tab') === 'settings') {
      setActiveTab('settings')
    }
    if (parameters.get('section') === 'appearance' && canManageLoginAppearance) {
      setSettingsTab('appearance')
    }
  }, [canManageLoginAppearance])

  // Setup real-time security data subscriptions
  useEffect(() => {
    // Subscribe to real-time security alerts
    const unsubscribeAlerts = securityService.onSecurityAlert((alert) => {
      setRealTimeAlerts(prev => [alert, ...prev.slice(0, 9)]) // Keep only latest 10
      toast({
        title: `Security Alert: ${alert.title}`,
        description: alert.message,
        variant: alert.type === 'critical' ? 'destructive' : 'default',
      })
      // Refetch alerts to update the UI
      refetchAlerts()
    })

    // Subscribe to real-time security metrics
    const unsubscribeMetrics = securityService.onSecurityMetrics((metrics) => {
      setRealTimeMetrics(metrics)
      // Invalidate and refetch metrics query
      queryClient.invalidateQueries({ queryKey: ['securityMetrics'] })
    })

    return () => {
      unsubscribeAlerts()
      unsubscribeMetrics()
    }
  }, [toast, refetchAlerts, queryClient])

  // Settings update mutation
  const updateMutation = useMutation({
    mutationFn: async ({
      securitySettings,
      loginPageStyle,
    }: {
      securitySettings?: SecuritySettingsDto
      loginPageStyle?: LoginPageStyle
    }) => {
      const updates: Promise<unknown>[] = []

      if (securitySettings) {
        updates.push(settingsService.updateSecuritySettings(securitySettings))
      }

      if (loginPageStyle) {
        updates.push(settingsService.updateLoginAppearance({ loginPageStyle }))
      }

      await Promise.all(updates)
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['securitySettings'] })
      queryClient.invalidateQueries({ queryKey: ['loginAppearanceSettings'] })
      toast({
        title: 'Settings Updated',
        description: 'Security settings have been updated successfully.',
        variant: 'default',
      })
    },
    onError: (error: any) => {
      toast({
        title: 'Error',
        description: error.message || 'Failed to update security settings.',
        variant: 'destructive',
      })
    },
  })

  const onSubmit = (data: SecuritySettingsFormValues) => {
    if (settingsTab === 'appearance' && canManageLoginAppearance && loginAppearance) {
      updateMutation.mutate({ loginPageStyle: selectedLoginPageStyle })
      return
    }

    updateMutation.mutate({ securitySettings: toSecuritySettingsDto(data) })
  }

  // Alert dismiss functionality
  const dismissAlert = async (alertId: string) => {
    try {
      await securityService.dismissAlert(alertId)
      // Refetch alerts to update UI
      refetchAlerts()
      toast({
        title: 'Alert Dismissed',
        description: 'Security alert has been dismissed successfully.',
        variant: 'default',
      })
    } catch (error) {
      toast({
        title: 'Error',
        description: 'Failed to dismiss alert. Please try again.',
        variant: 'destructive',
      })
    }
  }

  // Convert real security metrics to UI format
  const metricsData = realTimeMetrics || securityMetrics
  const securityMetricsUI: SecurityMetricUI[] = metricsData ? [
    {
      label: '2FA Adoption Rate',
      value: `${metricsData.twoFactorAdoptionRate}%`,
      change: metricsData.trends.twoFactorAdoptionRate > 0 ? `+${metricsData.trends.twoFactorAdoptionRate}%` : 
              metricsData.trends.twoFactorAdoptionRate < 0 ? `${metricsData.trends.twoFactorAdoptionRate}%` : '0%',
      trend: metricsData.trends.twoFactorAdoptionRate > 0 ? 'up' : 
             metricsData.trends.twoFactorAdoptionRate < 0 ? 'down' : 'stable',
      status: metricsData.twoFactorAdoptionRate >= 90 ? 'good' : metricsData.twoFactorAdoptionRate >= 75 ? 'warning' : 'critical',
      icon: Shield
    },
    {
      label: 'Failed Login Attempts',
      value: metricsData.failedLoginAttempts,
      change: metricsData.trends.failedLoginAttempts > 0 ? `+${metricsData.trends.failedLoginAttempts}%` : 
              metricsData.trends.failedLoginAttempts < 0 ? `${metricsData.trends.failedLoginAttempts}%` : '0%',
      trend: metricsData.trends.failedLoginAttempts > 0 ? 'up' : 
             metricsData.trends.failedLoginAttempts < 0 ? 'down' : 'stable',
      status: metricsData.failedLoginAttempts <= 10 ? 'good' : metricsData.failedLoginAttempts <= 50 ? 'warning' : 'critical',
      icon: Lock
    },
    {
      label: 'Active Sessions',
      value: metricsData.activeSessions,
      change: metricsData.trends.activeSessions > 0 ? `+${metricsData.trends.activeSessions}%` : 
              metricsData.trends.activeSessions < 0 ? `${metricsData.trends.activeSessions}%` : '0%',
      trend: metricsData.trends.activeSessions > 0 ? 'up' : 
             metricsData.trends.activeSessions < 0 ? 'down' : 'stable',
      status: metricsData.activeSessions <= 200 ? 'good' : metricsData.activeSessions <= 500 ? 'warning' : 'critical',
      icon: Users
    },
    {
      label: 'Security Incidents',
      value: metricsData.securityIncidents,
      change: 'Last 24h',
      trend: metricsData.trends.securityIncidents > 0 ? 'up' : 
             metricsData.trends.securityIncidents < 0 ? 'down' : 'stable',
      status: metricsData.securityIncidents === 0 ? 'good' : metricsData.securityIncidents <= 3 ? 'warning' : 'critical',
      icon: AlertTriangle
    },
    {
      label: 'Password Compliance',
      value: `${metricsData.passwordCompliance}%`,
      change: metricsData.trends.passwordCompliance > 0 ? `+${metricsData.trends.passwordCompliance}%` : 
              metricsData.trends.passwordCompliance < 0 ? `${metricsData.trends.passwordCompliance}%` : '0%',
      trend: metricsData.trends.passwordCompliance > 0 ? 'up' : 
             metricsData.trends.passwordCompliance < 0 ? 'down' : 'stable',
      status: metricsData.passwordCompliance >= 95 ? 'good' : metricsData.passwordCompliance >= 80 ? 'warning' : 'critical',
      icon: Key
    },
    {
      label: 'Audit Events Today',
      value: metricsData.auditEventsToday,
      change: metricsData.trends.auditEventsToday > 0 ? `+${metricsData.trends.auditEventsToday}%` : 
              metricsData.trends.auditEventsToday < 0 ? `${metricsData.trends.auditEventsToday}%` : '0%',
      trend: metricsData.trends.auditEventsToday > 0 ? 'up' : 
             metricsData.trends.auditEventsToday < 0 ? 'down' : 'stable',
      status: 'good',
      icon: FileText
    }
  ] : []

  // Use real security alerts data
  const alertsData = [...(realTimeAlerts || []), ...(securityAlerts || [])]
    .filter((alert, index, self) => index === self.findIndex(a => a.id === alert.id))
    .sort((a, b) => new Date(b.timestamp).getTime() - new Date(a.timestamp).getTime())

  const getStatusColor = (status: string) => {
    switch (status) {
      case 'good': return 'text-green-600'
      case 'warning': return 'text-yellow-600'
      case 'critical': return 'text-red-600'
      default: return 'text-gray-600'
    }
  }

  const getTrendIcon = (trend: string) => {
    switch (trend) {
      case 'up': return <TrendingUp className="h-4 w-4 text-green-600" />
      case 'down': return <TrendingDown className="h-4 w-4 text-red-600" />
      default: return <Activity className="h-4 w-4 text-gray-400" />
    }
  }

  const getAlertVariant = (type: string): 'default' | 'destructive' => {
    return type === 'critical' ? 'destructive' : 'default'
  }

  const formatTimeAgo = (date: Date) => {
    const now = new Date()
    const diff = now.getTime() - date.getTime()
    const minutes = Math.floor(diff / (1000 * 60))
    const hours = Math.floor(minutes / 60)
    
    if (minutes < 60) {
      return `${minutes} minutes ago`
    } else if (hours < 24) {
      return `${hours} hours ago`
    } else {
      return date.toLocaleDateString()
    }
  }

  const activeAlerts = alertsData.filter((alert) => !alert.dismissed)
  const activeThreats = (threatDetections || []).filter((threat) => threat.status === 'active' || threat.status === 'investigating')
  const criticalThreats = activeThreats.filter((threat) => threat.severity === 'critical' || threat.severity === 'high')
  const weakestCategoryEntry = securityHealth
    ? Object.entries(securityHealth.categories).sort(([, left], [, right]) => Number(left) - Number(right))[0]
    : null
  const weakestCategoryLabel = weakestCategoryEntry
    ? weakestCategoryEntry[0]
        .replace(/([A-Z])/g, ' $1')
        .replace(/^./, (value) => value.toUpperCase())
    : 'Unknown'
  const overallHealthScore = securityHealth?.overall ?? 0
  const healthRingCircumference = 351.86
  const healthRingOffset = healthRingCircumference * (1 - Math.max(0, Math.min(overallHealthScore, 100)) / 100)
  const securityPosture = criticalThreats.length > 0 || activeAlerts.some((alert) => alert.type === 'critical')
    ? {
        label: 'Critical',
        badgeClassName: 'border-red-200 text-red-700 dark:border-red-800 dark:text-red-300',
        panelClassName: 'border-red-200 bg-red-50 dark:border-red-900 dark:bg-red-950/30',
        textClassName: 'text-red-700 dark:text-red-300',
      }
    : overallHealthScore >= 90 && activeAlerts.length === 0 && activeThreats.length === 0
      ? {
          label: 'Healthy',
          badgeClassName: 'border-emerald-200 text-emerald-700 dark:border-emerald-800 dark:text-emerald-300',
          panelClassName: 'border-emerald-200 bg-emerald-50 dark:border-emerald-900 dark:bg-emerald-950/30',
          textClassName: 'text-emerald-700 dark:text-emerald-300',
        }
      : {
          label: 'Needs Attention',
          badgeClassName: 'border-amber-200 text-amber-700 dark:border-amber-800 dark:text-amber-300',
          panelClassName: 'border-amber-200 bg-amber-50 dark:border-amber-900 dark:bg-amber-950/30',
          textClassName: 'text-amber-700 dark:text-amber-300',
        }
  const queryErrors = [settingsError, metricsError, alertsError, healthError, threatsError, twoFactorError].filter(Boolean)
  const currentSecurityState = settings
    ? [
        {
          title: 'Authentication',
          value: `${metricsData?.twoFactorAdoptionRate ?? 0}% 2FA adoption`,
          detail: twoFactorSettings?.isEnabled
            ? 'Your account is protected with 2FA'
            : 'Your account still needs 2FA enabled',
          icon: Shield,
        },
        {
          title: 'Threat Exposure',
          value: `${activeAlerts.length} alerts, ${activeThreats.length} active threats`,
          detail: criticalThreats.length > 0
            ? `${criticalThreats.length} threats need immediate action`
            : 'No high-severity threat detections right now',
          icon: AlertTriangle,
        },
        {
          title: 'Session Guardrails',
          value: `${settings.sessionTimeoutMinutes}m timeout / ${settings.jwtTokenLifetimeMinutes}m token`,
          detail: settings.preventConcurrentLogin === 'Disabled'
            ? 'Concurrent login restriction is disabled'
            : settings.preventConcurrentLogin === 'LogoutFromAllDevices'
              ? 'New logins terminate older sessions'
              : 'Existing sessions block additional logins',
          icon: Clock,
        },
        {
          title: 'Login Hardening',
          value: `${settings.maxFailedLoginAttempts} failed attempts before lockout`,
          detail: settings.captchaEnabled
            ? `${settings.captchaProvider} challenge is enabled`
            : 'CAPTCHA is disabled on public auth flows',
          icon: Lock,
        },
      ]
    : []

  if (settingsLoading || metricsLoading || alertsLoading || healthLoading || threatsLoading) {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <div className="text-center space-y-4">
          <Loader2 className="h-8 w-8 animate-spin mx-auto" />
          <p className="text-muted-foreground">Loading security dashboard...</p>
        </div>
      </div>
    )
  }

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold">Security Management</h1>
          <p className="text-muted-foreground">
            Live security posture, persisted control settings, and action-ready monitoring
          </p>
        </div>
        
        <div className="flex items-center gap-2">
          <Badge variant="outline" className={securityPosture.badgeClassName}>
            <Shield className="h-3 w-3 mr-1" />
            Security Status: {securityPosture.label}
          </Badge>
          {activeTab === 'settings' && (
            <Button 
              type="submit" 
              disabled={updateMutation.isPending}
              onClick={handleSubmit(onSubmit)}
            >
              {updateMutation.isPending ? (
                <>
                  <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                  Saving...
                </>
              ) : (
                <>
                  <Save className="mr-2 h-4 w-4" />
                  Save Changes
                </>
              )}
            </Button>
          )}
        </div>
      </div>

      {queryErrors.length > 0 && (
        <Alert variant="destructive">
          <AlertTriangle className="h-4 w-4" />
          <AlertDescription>
            Some live security data could not be loaded. The page now shows only persisted data that was retrieved
            successfully instead of falling back to mock values.
          </AlertDescription>
        </Alert>
      )}

      <ClientOnly fallback={<div className="p-8 text-center text-muted-foreground">Loading security dashboard...</div>}>
        <Tabs value={activeTab} onValueChange={setActiveTab}>
        <TabsList className="grid w-full grid-cols-7 h-12 bg-slate-100 dark:bg-slate-800 p-1 rounded-lg">
          <TabsTrigger value="overview" className="data-[state=active]:bg-white data-[state=active]:shadow-sm data-[state=active]:text-slate-900 dark:data-[state=active]:bg-slate-700 dark:data-[state=active]:text-slate-100 font-medium">Overview</TabsTrigger>
          <TabsTrigger value="settings" className="data-[state=active]:bg-white data-[state=active]:shadow-sm data-[state=active]:text-slate-900 dark:data-[state=active]:bg-slate-700 dark:data-[state=active]:text-slate-100 font-medium">Settings</TabsTrigger>
          <TabsTrigger value="sessions" className="data-[state=active]:bg-white data-[state=active]:shadow-sm data-[state=active]:text-slate-900 dark:data-[state=active]:bg-slate-700 dark:data-[state=active]:text-slate-100 font-medium">Online Users</TabsTrigger>
          <TabsTrigger value="2fa" className="data-[state=active]:bg-white data-[state=active]:shadow-sm data-[state=active]:text-slate-900 dark:data-[state=active]:bg-slate-700 dark:data-[state=active]:text-slate-100 font-medium">Two-Factor Auth</TabsTrigger>
          <TabsTrigger value="audit" className="data-[state=active]:bg-white data-[state=active]:shadow-sm data-[state=active]:text-slate-900 dark:data-[state=active]:bg-slate-700 dark:data-[state=active]:text-slate-100 font-medium">Audit & Monitoring</TabsTrigger>
          <TabsTrigger value="devices" className="data-[state=active]:bg-white data-[state=active]:shadow-sm data-[state=active]:text-slate-900 dark:data-[state=active]:bg-slate-700 dark:data-[state=active]:text-slate-100 font-medium">Device Management</TabsTrigger>
          <TabsTrigger value="policies" className="data-[state=active]:bg-white data-[state=active]:shadow-sm data-[state=active]:text-slate-900 dark:data-[state=active]:bg-slate-700 dark:data-[state=active]:text-slate-100 font-medium">Security Policies</TabsTrigger>
        </TabsList>

        {/* Overview Tab */}
        <TabsContent value="overview" className="space-y-6">

          {/* Security Metrics */}
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
            {securityMetricsUI.length > 0 ? securityMetricsUI.map((metric, index) => (
              <Card key={index}>
                <CardContent className="p-6">
                  <div className="flex items-center justify-between">
                    <div>
                      <p className="text-sm text-muted-foreground">{metric.label}</p>
                      <div className="flex items-center gap-2 mt-1">
                        <p className={`text-2xl font-bold ${getStatusColor(metric.status || 'good')}`}>
                          {metric.value}
                        </p>
                        {metric.change && getTrendIcon(metric.trend || 'stable')}
                      </div>
                      {metric.change && (
                        <p className="text-xs text-muted-foreground mt-1">
                          {metric.change} from last period
                        </p>
                      )}
                    </div>
                    <metric.icon className={`h-8 w-8 ${getStatusColor(metric.status || 'good')}`} />
                  </div>
                </CardContent>
              </Card>
            )) : (
              <div className="col-span-full text-center py-8 text-muted-foreground">
                <Loader2 className="h-8 w-8 animate-spin mx-auto mb-2" />
                <p>Loading security metrics...</p>
              </div>
            )}
          </div>

          <Card className={securityPosture.panelClassName}>
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <Shield className="h-5 w-5" />
                Exact Security State
              </CardTitle>
              <CardDescription>
                This posture summary is derived from persisted security settings plus live alerts, threats, and 2FA state.
              </CardDescription>
            </CardHeader>
            <CardContent className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
              {currentSecurityState.map((state) => (
                <div key={state.title} className="rounded-xl border border-white/60 bg-white/70 p-4 shadow-sm dark:border-slate-800 dark:bg-slate-900/50">
                  <div className="mb-3 flex items-center justify-between">
                    <state.icon className={`h-5 w-5 ${securityPosture.textClassName}`} />
                    <Badge variant="outline" className={securityPosture.badgeClassName}>
                      {state.title}
                    </Badge>
                  </div>
                  <p className="text-sm font-semibold text-slate-900 dark:text-slate-100">{state.value}</p>
                  <p className="mt-2 text-xs text-muted-foreground">{state.detail}</p>
                </div>
              ))}
            </CardContent>
          </Card>

          {/* Security Alerts */}
          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <AlertTriangle className="h-5 w-5" />
                Security Alerts
                <Badge variant="destructive">
                  {alertsData.filter(alert => !alert.dismissed).length}
                </Badge>
              </CardTitle>
            </CardHeader>
            <CardContent>
              <div className="space-y-4">
                {alertsData
                  .filter(alert => !alert.dismissed)
                  .slice(0, 5)
                  .map((alert) => (
                  <Alert key={alert.id} variant={getAlertVariant(alert.type)}>
                    <div className="flex items-start justify-between w-full">
                      <div className="flex-1">
                        <div className="flex items-center gap-2 mb-1">
                          <h4 className="font-medium">{alert.title}</h4>
                          <Badge variant="outline" className="text-xs">
                            {alert.category}
                          </Badge>
                          {alert.severity && (
                            <Badge variant={alert.severity >= 4 ? 'destructive' : alert.severity >= 2 ? 'secondary' : 'outline'} className="text-xs">
                              {alert.severity >= 4 ? 'Critical' : alert.severity >= 2 ? 'Medium' : 'Low'}
                            </Badge>
                          )}
                        </div>
                        <p className="text-sm mt-1">{alert.message}</p>
                        <div className="flex items-center gap-4 mt-2 text-xs text-muted-foreground">
                          <span>{formatTimeAgo(alert.timestamp)}</span>
                          {alert.affectedUser && <span>User: {alert.affectedUser}</span>}
                          {alert.ipAddress && <span>IP: {alert.ipAddress}</span>}
                          {alert.location && <span>Location: {alert.location}</span>}
                        </div>
                      </div>
                      <Button
                        variant="ghost"
                        size="sm"
                        onClick={() => dismissAlert(alert.id)}
                      >
                        Dismiss
                      </Button>
                    </div>
                  </Alert>
                ))}
                
                {alertsData.filter(alert => !alert.dismissed).length === 0 && (
                  <div className="text-center py-8 text-muted-foreground">
                    <CheckCircle className="h-12 w-12 mx-auto mb-2 text-green-600" />
                    <p>No active security alerts</p>
                  </div>
                )}
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <Activity className="h-5 w-5" />
                Threat Detections
                <Badge variant={criticalThreats.length > 0 ? 'destructive' : 'secondary'}>
                  {activeThreats.length}
                </Badge>
              </CardTitle>
              <CardDescription>
                Live threat records that are still active or under investigation.
              </CardDescription>
            </CardHeader>
            <CardContent className="space-y-4">
              {activeThreats.length > 0 ? (
                activeThreats.slice(0, 4).map((threat) => (
                  <div key={threat.id} className="rounded-xl border border-slate-200 p-4 dark:border-slate-800">
                    <div className="flex flex-col gap-3 md:flex-row md:items-start md:justify-between">
                      <div>
                        <div className="flex items-center gap-2">
                          <h4 className="font-medium">{threat.title}</h4>
                          <Badge variant={threat.severity === 'critical' || threat.severity === 'high' ? 'destructive' : 'secondary'}>
                            {threat.severity}
                          </Badge>
                          <Badge variant="outline">{threat.status}</Badge>
                        </div>
                        <p className="mt-2 text-sm text-muted-foreground">{threat.description}</p>
                        <div className="mt-2 flex flex-wrap gap-3 text-xs text-muted-foreground">
                          <span>Detected {formatTimeAgo(threat.timestamp)}</span>
                          <span>Source: {threat.source}</span>
                          {threat.target && <span>Target: {threat.target}</span>}
                        </div>
                      </div>
                      <div className="max-w-sm text-xs text-muted-foreground">
                        {threat.indicators.length > 0
                          ? threat.indicators.slice(0, 3).map((indicator) => `${indicator.key}: ${indicator.value}`).join(' • ')
                          : 'No additional indicators recorded'}
                      </div>
                    </div>
                  </div>
                ))
              ) : (
                <div className="text-center py-8 text-muted-foreground">
                  <CheckCircle className="h-12 w-12 mx-auto mb-2 text-green-600" />
                  <p>No active threat detections</p>
                </div>
              )}
            </CardContent>
          </Card>

          {/* Quick Actions */}
          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <Zap className="h-5 w-5" />
                Quick Actions
              </CardTitle>
            </CardHeader>
            <CardContent>
              <div className="grid grid-cols-2 md:grid-cols-6 gap-4">
                <Button 
                  variant="outline" 
                  className="h-20 flex-col gap-2"
                  onClick={() => setActiveTab('settings')}
                >
                  <Settings className="h-6 w-6" />
                  <span className="text-sm">Settings</span>
                </Button>
                
                <Button 
                  variant="outline" 
                  className="h-20 flex-col gap-2"
                  onClick={() => setActiveTab('2fa')}
                >
                  <Shield className="h-6 w-6" />
                  <span className="text-sm">2FA Setup</span>
                </Button>
                
                <Button 
                  variant="outline" 
                  className="h-20 flex-col gap-2"
                  onClick={() => setActiveTab('audit')}
                >
                  <FileText className="h-6 w-6" />
                  <span className="text-sm">Audit Logs</span>
                </Button>
                
                <Button 
                  variant="outline" 
                  className="h-20 flex-col gap-2"
                  onClick={() => setActiveTab('sessions')}
                >
                  <Users className="h-6 w-6" />
                  <span className="text-sm">Online Users</span>
                </Button>
                
                <Button 
                  variant="outline" 
                  className="h-20 flex-col gap-2"
                  onClick={() => setActiveTab('devices')}
                >
                  <Monitor className="h-6 w-6" />
                  <span className="text-sm">Devices</span>
                </Button>

                <Button 
                  variant="outline" 
                  className="h-20 flex-col gap-2"
                  onClick={() => setActiveTab('policies')}
                >
                  <Lock className="h-6 w-6" />
                  <span className="text-sm">Policies</span>
                </Button>
              </div>
            </CardContent>
          </Card>

          {/* Security Health Score */}
          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <Target className="h-5 w-5" />
                Security Health Score
              </CardTitle>
            </CardHeader>
            <CardContent>
              <div className="flex items-center gap-4">
                <div className="relative w-32 h-32">
                  <svg className="w-32 h-32 transform -rotate-90">
                    <circle
                      cx="64"
                      cy="64"
                      r="56"
                      stroke="currentColor"
                      strokeWidth="8"
                      fill="none"
                      className="text-gray-200"
                    />
                    <circle
                      cx="64"
                      cy="64"
                      r="56"
                      stroke="currentColor"
                      strokeWidth="8"
                      fill="none"
                      strokeDasharray={healthRingCircumference}
                      strokeDashoffset={healthRingOffset}
                      className={
                        overallHealthScore >= 90
                          ? 'text-green-500'
                          : overallHealthScore >= 75
                            ? 'text-yellow-500'
                            : 'text-red-500'
                      }
                      strokeLinecap="round"
                    />
                  </svg>
                  <div className="absolute inset-0 flex items-center justify-center">
                    <div className="text-center">
                      <div className={`text-3xl font-bold ${
                        overallHealthScore >= 90 ? 'text-green-600' :
                        overallHealthScore >= 75 ? 'text-yellow-600' : 'text-red-600'
                      }`}>
                        {overallHealthScore}
                      </div>
                      <div className="text-sm text-muted-foreground">Score</div>
                    </div>
                  </div>
                </div>
                
                <div className="flex-1 space-y-3">
                  {Object.entries(securityHealth?.categories ?? {}).map(([key, value]) => {
                    const label = key === 'passwordPolicies' ? 'Password Policies' :
                                  key === 'twoFactorAdoption' ? '2FA Adoption' :
                                  key === 'sessionSecurity' ? 'Session Security' :
                                  key === 'accessControls' ? 'Access Controls' :
                                  key === 'auditCompliance' ? 'Audit Compliance' : key
                    const color = value >= 90 ? 'bg-green-500 text-green-600' :
                                  value >= 75 ? 'bg-yellow-500 text-yellow-600' : 'bg-red-500 text-red-600'
                    return (
                      <div key={key} className="flex items-center justify-between">
                        <span className="text-sm">{label}</span>
                        <div className="flex items-center gap-2">
                          <div className="w-20 h-2 bg-gray-200 rounded-full">
                            <div 
                              className={`h-2 ${color.split(' ')[0]} rounded-full`}
                              style={{ width: `${Math.min(value, 100)}%` }}
                            ></div>
                          </div>
                          <span className={`text-sm ${color.split(' ')[1]}`}>{value}%</span>
                        </div>
                      </div>
                    )
                  })}
                </div>
              </div>
              
              <div className={`mt-6 p-4 rounded-lg ${
                overallHealthScore >= 90 ? 'bg-green-50' :
                overallHealthScore >= 75 ? 'bg-yellow-50' : 'bg-red-50'
              }`}>
                {securityHealth?.recommendations && securityHealth.recommendations.length > 0 ? (
                  <div className="space-y-2">
                    <p className={`text-sm font-medium ${
                      overallHealthScore >= 90 ? 'text-green-800' :
                      overallHealthScore >= 75 ? 'text-yellow-800' : 'text-red-800'
                    }`}>
                      Security Recommendations
                    </p>
                    {securityHealth.recommendations.slice(0, 2).map((rec, index) => (
                      <div key={index} className="flex items-start gap-2">
                        <Badge 
                          variant={rec.priority === 'critical' ? 'destructive' : 
                                 rec.priority === 'high' ? 'secondary' : 'outline'}
                          className="text-xs mt-0.5"
                        >
                          {rec.priority}
                        </Badge>
                        <p className={`text-sm flex-1 ${
                          overallHealthScore >= 90 ? 'text-green-700' :
                          overallHealthScore >= 75 ? 'text-yellow-700' : 'text-red-700'
                        }`}>
                          <strong>{rec.category}:</strong> {rec.message}
                        </p>
                      </div>
                    ))}
                  </div>
                ) : (
                  <p className="text-sm text-green-800">
                    <strong>Security posture is stable.</strong> Weakest measured area: {weakestCategoryLabel}. Continue
                    monitoring threat detections and 2FA adoption to keep the overall score high.
                  </p>
                )}
              </div>
            </CardContent>
          </Card>
        </TabsContent>

        {/* Settings Tab - Database-backed Security Settings */}
        <TabsContent value="settings">
          <form className="space-y-6">
            {/* Settings Header with separation */}
            <div className="mb-8">
              <h2 className="text-xl font-semibold text-slate-900 dark:text-slate-100 mb-2">Security Configuration</h2>
              <p className="text-sm text-slate-600 dark:text-slate-400 mb-6">
                Configure detailed security policies and settings for your organization
              </p>
              <div className="border-t border-slate-200 dark:border-slate-700 mb-6"></div>
            </div>
            
            <Tabs value={settingsTab} onValueChange={setSettingsTab}>
              <TabsList className={`grid h-auto grid-cols-2 gap-1 bg-slate-50 p-1 shadow-sm md:grid-cols-3 dark:bg-slate-900 ${canManageLoginAppearance ? 'xl:grid-cols-6' : 'xl:grid-cols-5'}`}>
                <TabsTrigger value="password" className="flex items-center gap-2 text-sm data-[state=active]:bg-blue-50 data-[state=active]:text-blue-700 data-[state=active]:border-blue-200 dark:data-[state=active]:bg-blue-900 dark:data-[state=active]:text-blue-100 data-[state=active]:shadow-none">
                  <Key className="h-4 w-4" />
                  Password
                </TabsTrigger>
                <TabsTrigger value="session" className="flex items-center gap-2 text-sm data-[state=active]:bg-blue-50 data-[state=active]:text-blue-700 data-[state=active]:border-blue-200 dark:data-[state=active]:bg-blue-900 dark:data-[state=active]:text-blue-100 data-[state=active]:shadow-none">
                  <Clock className="h-4 w-4" />
                  Session
                </TabsTrigger>
                <TabsTrigger value="lockout" className="flex items-center gap-2 text-sm data-[state=active]:bg-blue-50 data-[state=active]:text-blue-700 data-[state=active]:border-blue-200 dark:data-[state=active]:bg-blue-900 dark:data-[state=active]:text-blue-100 data-[state=active]:shadow-none">
                  <Shield className="h-4 w-4" />
                  Lockout
                </TabsTrigger>
                <TabsTrigger value="recaptcha" className="flex items-center gap-2 text-sm data-[state=active]:bg-blue-50 data-[state=active]:text-blue-700 data-[state=active]:border-blue-200 dark:data-[state=active]:bg-blue-900 dark:data-[state=active]:text-blue-100 data-[state=active]:shadow-none">
                  <Bot className="h-4 w-4" />
                  reCAPTCHA
                </TabsTrigger>
                {canManageLoginAppearance && (
                  <TabsTrigger value="appearance" className="flex items-center gap-2 text-sm data-[state=active]:bg-blue-50 data-[state=active]:text-blue-700 data-[state=active]:border-blue-200 dark:data-[state=active]:bg-blue-900 dark:data-[state=active]:text-blue-100 data-[state=active]:shadow-none">
                    <MonitorCog className="h-4 w-4" />
                    Login Appearance
                  </TabsTrigger>
                )}
                <TabsTrigger value="legal" className="flex items-center gap-2 text-sm data-[state=active]:bg-blue-50 data-[state=active]:text-blue-700 data-[state=active]:border-blue-200 dark:data-[state=active]:bg-blue-900 dark:data-[state=active]:text-blue-100 data-[state=active]:shadow-none">
                  <FileText className="h-4 w-4" />
                  Legal
                </TabsTrigger>
              </TabsList>

              <TabsContent value="password">
                <Card>
                  <CardHeader>
                    <CardTitle>Password Policy</CardTitle>
                    <CardDescription>
                      Configure password requirements and expiration settings
                    </CardDescription>
                  </CardHeader>
                  <CardContent className="space-y-4">
                    <div className="space-y-2">
                      <Label htmlFor="passwordMinLength">Minimum Password Length</Label>
                      <Input
                        id="passwordMinLength"
                        type="number"
                        {...register('passwordMinLength', { valueAsNumber: true })}
                      />
                      {errors.passwordMinLength && (
                        <p className="text-sm text-red-500">{errors.passwordMinLength.message}</p>
                      )}
                    </div>

                    <div className="space-y-4">
                      <div className="flex items-center gap-2">
                        <Switch 
                          id="passwordRequireUppercase"
                          checked={watch('passwordRequireUppercase')}
                          onCheckedChange={(checked) => setValue('passwordRequireUppercase', checked)}
                        />
                        <Label htmlFor="passwordRequireUppercase">Require Uppercase Letters</Label>
                      </div>

                      <div className="flex items-center gap-2">
                        <Switch 
                          id="passwordRequireLowercase"
                          checked={watch('passwordRequireLowercase')}
                          onCheckedChange={(checked) => setValue('passwordRequireLowercase', checked)}
                        />
                        <Label htmlFor="passwordRequireLowercase">Require Lowercase Letters</Label>
                      </div>

                      <div className="flex items-center gap-2">
                        <Switch 
                          id="passwordRequireDigits"
                          checked={watch('passwordRequireDigits')}
                          onCheckedChange={(checked) => setValue('passwordRequireDigits', checked)}
                        />
                        <Label htmlFor="passwordRequireDigits">Require Numbers</Label>
                      </div>

                      <div className="flex items-center gap-2">
                        <Switch 
                          id="passwordRequireSpecialChars"
                          checked={watch('passwordRequireSpecialChars')}
                          onCheckedChange={(checked) => setValue('passwordRequireSpecialChars', checked)}
                        />
                        <Label htmlFor="passwordRequireSpecialChars">Require Special Characters</Label>
                      </div>
                    </div>

                    <div className="space-y-2">
                      <Label htmlFor="passwordPreventReuse">Password History</Label>
                      <Input
                        id="passwordPreventReuse"
                        type="number"
                        {...register('passwordPreventReuse', { valueAsNumber: true })}
                      />
                      <p className="text-sm text-slate-500">
                        Number of previous passwords that cannot be reused (0 to disable)
                      </p>
                      {errors.passwordPreventReuse && (
                        <p className="text-sm text-red-500">{errors.passwordPreventReuse.message}</p>
                      )}
                    </div>

                    <div className="space-y-2">
                      <Label htmlFor="passwordMaxAge">Password Expiry (Days)</Label>
                      <Input
                        id="passwordMaxAge"
                        type="number"
                        {...register('passwordMaxAge', { valueAsNumber: true })}
                      />
                      <p className="text-sm text-slate-500">
                        Days before password expires (0 to disable)
                      </p>
                      {errors.passwordMaxAge && (
                        <p className="text-sm text-red-500">{errors.passwordMaxAge.message}</p>
                      )}
                    </div>
                  </CardContent>
                </Card>
              </TabsContent>

              <TabsContent value="session">
                <Card>
                  <CardHeader>
                    <CardTitle>Session Settings</CardTitle>
                    <CardDescription>
                      Configure session timeout and concurrent login settings
                    </CardDescription>
                  </CardHeader>
                  <CardContent className="space-y-4">
                    <div className="space-y-2">
                      <Label htmlFor="sessionTimeoutMinutes">Session Timeout (Minutes)</Label>
                      <Input
                        id="sessionTimeoutMinutes"
                        type="number"
                        {...register('sessionTimeoutMinutes', { valueAsNumber: true })}
                      />
                      <p className="text-sm text-slate-500">
                        Duration of user inactivity before automatic logout
                      </p>
                      {errors.sessionTimeoutMinutes && (
                        <p className="text-sm text-red-500">{errors.sessionTimeoutMinutes.message}</p>
                      )}
                    </div>

                    <div className="space-y-2">
                      <Label htmlFor="jwtTokenLifetimeMinutes">Token Lifetime (Minutes)</Label>
                      <Input
                        id="jwtTokenLifetimeMinutes"
                        type="number"
                        {...register('jwtTokenLifetimeMinutes', { valueAsNumber: true })}
                      />
                      <p className="text-sm text-slate-500">
                        How long authentication tokens remain valid
                      </p>
                      {errors.jwtTokenLifetimeMinutes && (
                        <p className="text-sm text-red-500">{errors.jwtTokenLifetimeMinutes.message}</p>
                      )}
                    </div>

                    <div className="space-y-2">
                      <Label htmlFor="preventConcurrentLogin">Concurrent Login Prevention</Label>
                      <Select
                        onValueChange={(value) => setValue('preventConcurrentLogin', value as any)}
                        value={watch('preventConcurrentLogin')}
                      >
                        <SelectTrigger>
                          <SelectValue placeholder="Select a policy" />
                        </SelectTrigger>
                        <SelectContent>
                          <SelectItem value="Disabled">
                            <div className="space-y-1">
                              <div className="font-medium">Disabled</div>
                              <div className="text-xs text-slate-500">
                                Allow users to login from multiple devices
                              </div>
                            </div>
                          </SelectItem>
                          <SelectItem value="LogoutFromAllDevices">
                            <div className="space-y-1">
                              <div className="font-medium">Logout from all devices</div>
                              <div className="text-xs text-slate-500">
                                New login terminates all existing sessions
                              </div>
                            </div>
                          </SelectItem>
                          <SelectItem value="PreventSubsequentLogins">
                            <div className="space-y-1">
                              <div className="font-medium">Prevent subsequent logins</div>
                              <div className="text-xs text-slate-500">
                                Block new logins until user logs out from current session
                              </div>
                            </div>
                          </SelectItem>
                        </SelectContent>
                      </Select>
                    </div>
                  </CardContent>
                </Card>
              </TabsContent>

              <TabsContent value="lockout">
                <Card>
                  <CardHeader>
                    <CardTitle>Account Lockout & Rate Limiting</CardTitle>
                    <CardDescription>
                      Configure account lockout policies and rate limiting settings
                    </CardDescription>
                  </CardHeader>
                  <CardContent className="space-y-4">
                    <div className="space-y-2">
                      <Label htmlFor="maxFailedLoginAttempts">Max Failed Login Attempts</Label>
                      <Input
                        id="maxFailedLoginAttempts"
                        type="number"
                        {...register('maxFailedLoginAttempts', { valueAsNumber: true })}
                      />
                      <p className="text-sm text-slate-500">
                        Number of failed login attempts before account is locked
                      </p>
                      {errors.maxFailedLoginAttempts && (
                        <p className="text-sm text-red-500">{errors.maxFailedLoginAttempts.message}</p>
                      )}
                    </div>

                    <div className="space-y-2">
                      <Label htmlFor="accountLockoutMinutes">Account Lockout Duration (Minutes)</Label>
                      <Input
                        id="accountLockoutMinutes"
                        type="number"
                        {...register('accountLockoutMinutes', { valueAsNumber: true })}
                      />
                      <p className="text-sm text-slate-500">
                        How long accounts remain locked after max failed attempts
                      </p>
                      {errors.accountLockoutMinutes && (
                        <p className="text-sm text-red-500">{errors.accountLockoutMinutes.message}</p>
                      )}
                    </div>

                    <div className="space-y-2">
                      <Label htmlFor="rateLimitLoginMaxAttempts">Rate Limit Max Attempts</Label>
                      <Input
                        id="rateLimitLoginMaxAttempts"
                        type="number"
                        {...register('rateLimitLoginMaxAttempts', { valueAsNumber: true })}
                      />
                      <p className="text-sm text-slate-500">
                        Maximum login attempts per time window before rate limiting
                      </p>
                      {errors.rateLimitLoginMaxAttempts && (
                        <p className="text-sm text-red-500">{errors.rateLimitLoginMaxAttempts.message}</p>
                      )}
                    </div>

                    <div className="space-y-2">
                      <Label htmlFor="rateLimitLoginWindowMinutes">Rate Limit Window (Minutes)</Label>
                      <Input
                        id="rateLimitLoginWindowMinutes"
                        type="number"
                        {...register('rateLimitLoginWindowMinutes', { valueAsNumber: true })}
                      />
                      <p className="text-sm text-slate-500">
                        Time window for tracking login attempts
                      </p>
                      {errors.rateLimitLoginWindowMinutes && (
                        <p className="text-sm text-red-500">{errors.rateLimitLoginWindowMinutes.message}</p>
                      )}
                    </div>

                    <div className="space-y-2">
                      <Label htmlFor="rateLimitLoginBlockDurationMinutes">Rate Limit Block Duration (Minutes)</Label>
                      <Input
                        id="rateLimitLoginBlockDurationMinutes"
                        type="number"
                        {...register('rateLimitLoginBlockDurationMinutes', { valueAsNumber: true })}
                      />
                      <p className="text-sm text-slate-500">
                        How long to block login attempts when rate limit is exceeded
                      </p>
                      {errors.rateLimitLoginBlockDurationMinutes && (
                        <p className="text-sm text-red-500">{errors.rateLimitLoginBlockDurationMinutes.message}</p>
                      )}
                    </div>
                  </CardContent>
                </Card>
              </TabsContent>

              <TabsContent value="recaptcha">
                <Card>
                  <CardHeader>
                    <CardTitle>CAPTCHA Settings</CardTitle>
                    <CardDescription>
                      Configure CAPTCHA protection to prevent automated attacks
                    </CardDescription>
                  </CardHeader>
                  <CardContent className="space-y-4">
                    <div className="flex items-center gap-2">
                      <Switch 
                        id="captchaEnabled"
                        checked={watch('captchaEnabled')}
                        onCheckedChange={(checked) => setValue('captchaEnabled', checked)}
                      />
                      <Label htmlFor="captchaEnabled">Enable CAPTCHA Protection</Label>
                    </div>

                    {watch('captchaEnabled') && (
                      <>
                        <div className="space-y-2">
                          <Label htmlFor="captchaProvider">CAPTCHA Provider</Label>
                          <Select
                            onValueChange={(value) => setValue('captchaProvider', value as any)}
                            value={watch('captchaProvider')}
                          >
                            <SelectTrigger>
                              <SelectValue placeholder="Select a provider" />
                            </SelectTrigger>
                            <SelectContent>
                              <SelectItem value="recaptcha">Google reCAPTCHA</SelectItem>
                              <SelectItem value="hcaptcha">hCaptcha</SelectItem>
                            </SelectContent>
                          </Select>
                        </div>

                        {watch('captchaProvider') === 'recaptcha' && (
                          <>
                            <div className="space-y-2">
                              <Label htmlFor="recaptchaSiteKey">reCAPTCHA Site Key</Label>
                              <Input
                                id="recaptchaSiteKey"
                                type="text"
                                placeholder="6Lc..."
                                {...register('recaptchaSiteKey')}
                              />
                              <p className="text-sm text-slate-500">
                                Public site key from Google reCAPTCHA admin console
                              </p>
                              {errors.recaptchaSiteKey && (
                                <p className="text-sm text-red-500">{errors.recaptchaSiteKey.message}</p>
                              )}
                            </div>

                            <div className="space-y-2">
                              <Label htmlFor="recaptchaSecretKey">reCAPTCHA Secret Key</Label>
                              <Input
                                id="recaptchaSecretKey"
                                type="password"
                                placeholder="6Lc..."
                                {...register('recaptchaSecretKey')}
                              />
                              <p className="text-sm text-slate-500">
                                Private secret key from Google reCAPTCHA admin console
                              </p>
                              {errors.recaptchaSecretKey && (
                                <p className="text-sm text-red-500">{errors.recaptchaSecretKey.message}</p>
                              )}
                            </div>
                          </>
                        )}

                        {watch('captchaProvider') === 'hcaptcha' && (
                          <>
                            <div className="space-y-2">
                              <Label htmlFor="hCaptchaSiteKey">hCaptcha Site Key</Label>
                              <Input
                                id="hCaptchaSiteKey"
                                type="text"
                                placeholder="10000000-ffff-ffff-ffff-000000000001"
                                {...register('hCaptchaSiteKey')}
                              />
                              <p className="text-sm text-slate-500">
                                Public site key from hCaptcha dashboard
                              </p>
                              {errors.hCaptchaSiteKey && (
                                <p className="text-sm text-red-500">{errors.hCaptchaSiteKey.message}</p>
                              )}
                            </div>

                            <div className="space-y-2">
                              <Label htmlFor="hCaptchaSecretKey">hCaptcha Secret Key</Label>
                              <Input
                                id="hCaptchaSecretKey"
                                type="password"
                                placeholder="0x0000000000000000000000000000000000000000"
                                {...register('hCaptchaSecretKey')}
                              />
                              <p className="text-sm text-slate-500">
                                Private secret key from hCaptcha dashboard
                              </p>
                              {errors.hCaptchaSecretKey && (
                                <p className="text-sm text-red-500">{errors.hCaptchaSecretKey.message}</p>
                              )}
                            </div>
                          </>
                        )}
                      </>
                    )}
                  </CardContent>
                </Card>
              </TabsContent>

              <TabsContent value="legal">
                <Card>
                  <CardHeader>
                    <CardTitle>Legal Documents</CardTitle>
                    <CardDescription>
                      Configure links to legal documents displayed during user registration and login
                    </CardDescription>
                  </CardHeader>
                  <CardContent className="space-y-4">
                    <div className="space-y-2">
                      <Label htmlFor="termsOfServiceUrl">Terms of Service URL</Label>
                      <Input
                        id="termsOfServiceUrl"
                        type="url"
                        placeholder="https://company.com/terms"
                        {...register('termsOfServiceUrl')}
                      />
                      <p className="text-sm text-slate-500">
                        URL to your organization's terms of service document
                      </p>
                      {errors.termsOfServiceUrl && (
                        <p className="text-sm text-red-500">{errors.termsOfServiceUrl.message}</p>
                      )}
                    </div>

                    <div className="space-y-2">
                      <Label htmlFor="privacyPolicyUrl">Privacy Policy URL</Label>
                      <Input
                        id="privacyPolicyUrl"
                        type="url"
                        placeholder="https://company.com/privacy"
                        {...register('privacyPolicyUrl')}
                      />
                      <p className="text-sm text-slate-500">
                        URL to your organization's privacy policy document
                      </p>
                      {errors.privacyPolicyUrl && (
                        <p className="text-sm text-red-500">{errors.privacyPolicyUrl.message}</p>
                      )}
                    </div>

                    <Alert>
                      <FileText className="h-4 w-4" />
                      <AlertDescription>
                        These URLs will be displayed on the login and registration pages. Users will be required to accept these terms during account creation.
                      </AlertDescription>
                    </Alert>
                  </CardContent>
                </Card>
              </TabsContent>

              {canManageLoginAppearance && (
                <TabsContent value="appearance">
                  <LoginAppearanceSettings
                    value={selectedLoginPageStyle}
                    onValueChange={setSelectedLoginPageStyle}
                    disabled={updateMutation.isPending}
                    isLoading={isLoginAppearanceLoading}
                    errorMessage={
                      isLoginAppearanceError
                        ? 'Login appearance settings could not be loaded. Refresh the page and try again.'
                        : null
                    }
                  />
                </TabsContent>
              )}
            </Tabs>
          </form>
        </TabsContent>

        {/* Session Management Tab */}
        <TabsContent value="sessions">
          <div className="space-y-4">
            <SessionManagementTab />
          </div>
        </TabsContent>

        {/* Two-Factor Authentication Tab */}
        <TabsContent value="2fa">
          <TwoFactorAuth 
            userEmail={currentUser?.email}
            isEnabled={twoFactorSettings?.isEnabled}
            onStatusChange={() => {
              queryClient.invalidateQueries({ queryKey: ['securityMetrics'] })
              queryClient.invalidateQueries({ queryKey: ['securityHealthScore'] })
              queryClient.invalidateQueries({ queryKey: ['securityTwoFactorSettings'] })
            }}
          />
        </TabsContent>

        {/* Audit & Monitoring Tab */}
        <TabsContent value="audit">
          <AuditLog />
        </TabsContent>

        {/* Device Management Tab */}
        <TabsContent value="devices">
          <DeviceManagement />
        </TabsContent>

        {/* Security Policies Tab */}
        <TabsContent value="policies">
          <SecurityPolicies />
        </TabsContent>
        </Tabs>
      </ClientOnly>
    </div>
  )
}
