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
import { useToast } from '../../../../hooks/use-toast'
import { settingsService } from '../../../../services/settings'
import type { SecuritySettings as SecuritySettingsDto } from '../../../../services/settings'
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
  Monitor
} from 'lucide-react'

interface SecurityMetric {
  label: string
  value: string | number
  change?: string
  trend?: 'up' | 'down' | 'stable'
  status?: 'good' | 'warning' | 'critical'
  icon: React.ComponentType<any>
}

interface SecurityAlert {
  id: string
  type: 'critical' | 'warning' | 'info'
  title: string
  message: string
  timestamp: Date
  dismissed: boolean
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
  recaptchaSiteKey: z.string().optional().nullable().or(z.literal('')),
  recaptchaSecretKey: z.string().optional().nullable().or(z.literal('')),
  hCaptchaSiteKey: z.string().optional().nullable().or(z.literal('')),
  hCaptchaSecretKey: z.string().optional().nullable().or(z.literal('')),
});

const legalSchema = z.object({
  termsOfServiceUrl: z.string().url('Must be a valid URL').optional().nullable().or(z.literal('')),
  privacyPolicyUrl: z.string().url('Must be a valid URL').optional().nullable().or(z.literal('')),
});

export default function SecurityDashboardPage() {
  const [activeTab, setActiveTab] = useState('overview')
  const { toast } = useToast()
  const queryClient = useQueryClient()

  // Fetch current security settings
  const { data: settings, isLoading: settingsLoading, error: settingsError } = useQuery({
    queryKey: ['securitySettings'],
    queryFn: () => settingsService.getSecuritySettings(),
  })

  // Form for security settings
  const {
    register,
    handleSubmit,
    formState: { errors },
    watch,
    setValue,
    reset,
  } = useForm<SecuritySettingsDto>({
    resolver: zodResolver(
      z.object({
        ...passwordPolicySchema.shape,
        ...sessionSchema.shape,
        ...lockoutSchema.shape,
        ...recaptchaSchema.shape,
        ...legalSchema.shape,
      })
    ),
    defaultValues: {
      // Password Policy
      passwordMinLength: 8,
      passwordRequireUppercase: true,
      passwordRequireLowercase: true,
      passwordRequireDigits: true,
      passwordRequireSpecialChars: true,
      passwordMaxAge: 90,
      passwordPreventReuse: 5,
      // Session Settings
      sessionTimeoutMinutes: 30,
      jwtTokenLifetimeMinutes: 60,
      preventConcurrentLogin: 'Disabled' as const,
      // Lockout Settings
      maxFailedLoginAttempts: 5,
      accountLockoutMinutes: 30,
      rateLimitLoginMaxAttempts: 5,
      rateLimitLoginWindowMinutes: 15,
      rateLimitLoginBlockDurationMinutes: 30,
      // CAPTCHA Settings
      captchaEnabled: false,
      captchaProvider: 'recaptcha' as const,
      recaptchaSiteKey: null,
      recaptchaSecretKey: null,
      hCaptchaSiteKey: null,
      hCaptchaSecretKey: null,
      // Legal URLs
      termsOfServiceUrl: null,
      privacyPolicyUrl: null,
    },
  })

  // Update form when settings are loaded
  useEffect(() => {
    if (!settings) return
    
    const cleanSettings = {
      // Password Policy
      passwordMinLength: settings.passwordMinLength || 8,
      passwordRequireUppercase: Boolean(settings.passwordRequireUppercase),
      passwordRequireLowercase: Boolean(settings.passwordRequireLowercase),
      passwordRequireDigits: Boolean(settings.passwordRequireDigits),
      passwordRequireSpecialChars: Boolean(settings.passwordRequireSpecialChars),
      passwordMaxAge: settings.passwordMaxAge,
      passwordPreventReuse: settings.passwordPreventReuse,
      // Session Settings
      sessionTimeoutMinutes: settings.sessionTimeoutMinutes || 30,
      jwtTokenLifetimeMinutes: settings.jwtTokenLifetimeMinutes || 60,
      preventConcurrentLogin: settings.preventConcurrentLogin || 'Disabled',
      // Lockout Settings
      maxFailedLoginAttempts: settings.maxFailedLoginAttempts || 5,
      accountLockoutMinutes: settings.accountLockoutMinutes || 30,
      rateLimitLoginMaxAttempts: settings.rateLimitLoginMaxAttempts || 5,
      rateLimitLoginWindowMinutes: settings.rateLimitLoginWindowMinutes || 15,
      rateLimitLoginBlockDurationMinutes: settings.rateLimitLoginBlockDurationMinutes || 30,
      // CAPTCHA Settings
      captchaEnabled: Boolean(settings.captchaEnabled),
      captchaProvider: settings.captchaProvider || 'recaptcha',
      recaptchaSiteKey: settings.recaptchaSiteKey || '',
      recaptchaSecretKey: settings.recaptchaSecretKey || '',
      hCaptchaSiteKey: settings.hCaptchaSiteKey || '',
      hCaptchaSecretKey: settings.hCaptchaSecretKey || '',
      // Legal URLs
      termsOfServiceUrl: settings.termsOfServiceUrl || '',
      privacyPolicyUrl: settings.privacyPolicyUrl || '',
    } as SecuritySettingsDto
    
    reset(cleanSettings)
  }, [settings, reset])

  // Settings update mutation
  const updateMutation = useMutation({
    mutationFn: (data: SecuritySettingsDto) => settingsService.updateSecuritySettings(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['securitySettings'] })
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

  const onSubmit = (data: SecuritySettingsDto) => {
    const cleanData: SecuritySettingsDto = {
      ...data,
      recaptchaSiteKey: data.recaptchaSiteKey?.trim() || null,
      recaptchaSecretKey: data.recaptchaSecretKey?.trim() || null,
      hCaptchaSiteKey: data.hCaptchaSiteKey?.trim() || null,
      hCaptchaSecretKey: data.hCaptchaSecretKey?.trim() || null,
      termsOfServiceUrl: data.termsOfServiceUrl?.trim() || null,
      privacyPolicyUrl: data.privacyPolicyUrl?.trim() || null,
    }
    updateMutation.mutate(cleanData)
  }

  // Mock security metrics
  const securityMetrics: SecurityMetric[] = [
    {
      label: '2FA Adoption Rate',
      value: '85%',
      change: '+12%',
      trend: 'up',
      status: 'good',
      icon: Shield
    },
    {
      label: 'Failed Login Attempts',
      value: 23,
      change: '-15%',
      trend: 'down',
      status: 'good',
      icon: Lock
    },
    {
      label: 'Active Sessions',
      value: 142,
      change: '+8%',
      trend: 'up',
      status: 'warning',
      icon: Users
    },
    {
      label: 'Security Incidents',
      value: 3,
      change: 'Last 24h',
      trend: 'stable',
      status: 'warning',
      icon: AlertTriangle
    },
    {
      label: 'Password Compliance',
      value: '92%',
      change: '+5%',
      trend: 'up',
      status: 'good',
      icon: Key
    },
    {
      label: 'Audit Events Today',
      value: 1247,
      change: '+22%',
      trend: 'up',
      status: 'good',
      icon: FileText
    }
  ]

  // Mock security alerts
  const [securityAlerts, setSecurityAlerts] = useState<SecurityAlert[]>([
    {
      id: '1',
      type: 'critical',
      title: 'Multiple Failed Login Attempts',
      message: 'User john.doe@company.com has 8 failed login attempts from IP 192.168.1.100',
      timestamp: new Date(Date.now() - 15 * 60 * 1000),
      dismissed: false
    },
    {
      id: '2',
      type: 'warning',
      title: 'Unusual Login Location',
      message: 'Login detected from new location: Tokyo, Japan for user alice@company.com',
      timestamp: new Date(Date.now() - 45 * 60 * 1000),
      dismissed: false
    },
    {
      id: '3',
      type: 'info',
      title: 'Password Policy Updated',
      message: 'Security team updated password complexity requirements',
      timestamp: new Date(Date.now() - 2 * 60 * 60 * 1000),
      dismissed: false
    }
  ])

  const dismissAlert = (alertId: string) => {
    setSecurityAlerts(alerts => 
      alerts.map(alert => 
        alert.id === alertId ? { ...alert, dismissed: true } : alert
      )
    )
  }

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

  if (settingsLoading) {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <Loader2 className="h-8 w-8 animate-spin" />
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
            Comprehensive security management and monitoring
          </p>
        </div>
        
        <div className="flex items-center gap-2">
          <Badge variant="outline" className="text-green-600 border-green-200">
            <Shield className="h-3 w-3 mr-1" />
            Security Status: Good
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

      <Tabs value={activeTab} onValueChange={setActiveTab}>
        <TabsList className="grid w-full grid-cols-6 h-12 bg-slate-100 dark:bg-slate-800 p-1 rounded-lg">
          <TabsTrigger value="overview" className="data-[state=active]:bg-white data-[state=active]:shadow-sm data-[state=active]:text-slate-900 dark:data-[state=active]:bg-slate-700 dark:data-[state=active]:text-slate-100 font-medium">Overview</TabsTrigger>
          <TabsTrigger value="settings" className="data-[state=active]:bg-white data-[state=active]:shadow-sm data-[state=active]:text-slate-900 dark:data-[state=active]:bg-slate-700 dark:data-[state=active]:text-slate-100 font-medium">Settings</TabsTrigger>
          <TabsTrigger value="2fa" className="data-[state=active]:bg-white data-[state=active]:shadow-sm data-[state=active]:text-slate-900 dark:data-[state=active]:bg-slate-700 dark:data-[state=active]:text-slate-100 font-medium">Two-Factor Auth</TabsTrigger>
          <TabsTrigger value="audit" className="data-[state=active]:bg-white data-[state=active]:shadow-sm data-[state=active]:text-slate-900 dark:data-[state=active]:bg-slate-700 dark:data-[state=active]:text-slate-100 font-medium">Audit & Monitoring</TabsTrigger>
          <TabsTrigger value="devices" className="data-[state=active]:bg-white data-[state=active]:shadow-sm data-[state=active]:text-slate-900 dark:data-[state=active]:bg-slate-700 dark:data-[state=active]:text-slate-100 font-medium">Device Management</TabsTrigger>
          <TabsTrigger value="policies" className="data-[state=active]:bg-white data-[state=active]:shadow-sm data-[state=active]:text-slate-900 dark:data-[state=active]:bg-slate-700 dark:data-[state=active]:text-slate-100 font-medium">Security Policies</TabsTrigger>
        </TabsList>

        {/* Overview Tab */}
        <TabsContent value="overview" className="space-y-6">

          {/* Security Metrics */}
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
            {securityMetrics.map((metric, index) => (
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
            ))}
          </div>

          {/* Security Alerts */}
          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <AlertTriangle className="h-5 w-5" />
                Security Alerts
                <Badge variant="destructive">
                  {securityAlerts.filter(alert => !alert.dismissed).length}
                </Badge>
              </CardTitle>
            </CardHeader>
            <CardContent>
              <div className="space-y-4">
                {securityAlerts
                  .filter(alert => !alert.dismissed)
                  .slice(0, 5)
                  .map((alert) => (
                  <Alert key={alert.id} variant={getAlertVariant(alert.type)}>
                    <div className="flex items-start justify-between w-full">
                      <div className="flex-1">
                        <h4 className="font-medium">{alert.title}</h4>
                        <p className="text-sm mt-1">{alert.message}</p>
                        <p className="text-xs text-muted-foreground mt-2">
                          {formatTimeAgo(alert.timestamp)}
                        </p>
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
                
                {securityAlerts.filter(alert => !alert.dismissed).length === 0 && (
                  <div className="text-center py-8 text-muted-foreground">
                    <CheckCircle className="h-12 w-12 mx-auto mb-2 text-green-600" />
                    <p>No active security alerts</p>
                  </div>
                )}
              </div>
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
              <div className="grid grid-cols-2 md:grid-cols-5 gap-4">
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
                      strokeDasharray={351.86} // 2 * pi * 56
                      strokeDashoffset={70.37} // 351.86 * (1 - 0.8)
                      className="text-green-500"
                      strokeLinecap="round"
                    />
                  </svg>
                  <div className="absolute inset-0 flex items-center justify-center">
                    <div className="text-center">
                      <div className="text-3xl font-bold text-green-600">87</div>
                      <div className="text-sm text-muted-foreground">Score</div>
                    </div>
                  </div>
                </div>
                
                <div className="flex-1 space-y-3">
                  <div className="flex items-center justify-between">
                    <span className="text-sm">Password Policies</span>
                    <div className="flex items-center gap-2">
                      <div className="w-20 h-2 bg-gray-200 rounded-full">
                        <div className="w-4/5 h-2 bg-green-500 rounded-full"></div>
                      </div>
                      <span className="text-sm text-green-600">92%</span>
                    </div>
                  </div>
                  
                  <div className="flex items-center justify-between">
                    <span className="text-sm">2FA Adoption</span>
                    <div className="flex items-center gap-2">
                      <div className="w-20 h-2 bg-gray-200 rounded-full">
                        <div className="w-4/5 h-2 bg-green-500 rounded-full"></div>
                      </div>
                      <span className="text-sm text-green-600">85%</span>
                    </div>
                  </div>
                  
                  <div className="flex items-center justify-between">
                    <span className="text-sm">Session Security</span>
                    <div className="flex items-center gap-2">
                      <div className="w-20 h-2 bg-gray-200 rounded-full">
                        <div className="w-3/4 h-2 bg-yellow-500 rounded-full"></div>
                      </div>
                      <span className="text-sm text-yellow-600">78%</span>
                    </div>
                  </div>
                  
                  <div className="flex items-center justify-between">
                    <span className="text-sm">Access Controls</span>
                    <div className="flex items-center gap-2">
                      <div className="w-20 h-2 bg-gray-200 rounded-full">
                        <div className="w-full h-2 bg-green-500 rounded-full"></div>
                      </div>
                      <span className="text-sm text-green-600">95%</span>
                    </div>
                  </div>
                </div>
              </div>
              
              <div className="mt-6 p-4 bg-green-50 rounded-lg">
                <p className="text-sm text-green-800">
                  <strong>Excellent security posture!</strong> Your organization maintains strong security practices. 
                  Consider increasing 2FA adoption and reviewing session policies to reach 95+ score.
                </p>
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
            
            <Tabs defaultValue="password">
              <TabsList className="grid grid-cols-5 gap-1 h-10 bg-slate-50 dark:bg-slate-900 p-1 rounded-md border shadow-sm">
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
            </Tabs>
          </form>
        </TabsContent>

        {/* Two-Factor Authentication Tab */}
        <TabsContent value="2fa">
          <TwoFactorAuth 
            userEmail="admin@company.com" 
            isEnabled={false}
            onStatusChange={(enabled) => console.log('2FA status changed:', enabled)}
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
    </div>
  )
}