"use client"

import React, { useState } from 'react'
import { Card, CardContent, CardHeader, CardTitle } from '../ui/card'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '../ui/tabs'
import { Button } from '../ui/button'
import { Input } from '../ui/input'
import { Label } from '../ui/label'
import { Badge } from '../ui/badge'
import { Switch } from '../ui/switch'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '../ui/select'
import { Textarea } from '../ui/textarea'
import { Alert, AlertDescription } from '../ui/alert'
import { 
  Settings, 
  Shield, 
  Users, 
  Lock, 
  Globe, 
  Smartphone, 
  Mail, 
  Clock, 
  AlertTriangle,
  CheckCircle,
  Save,
  RotateCcw,
  Plus,
  X,
  Key,
  Eye,
  EyeOff,
  Database,
  FileText,
  Zap
} from 'lucide-react'

interface SecurityConfig {
  general: {
    sessionTimeout: number
    maxConcurrentSessions: number
    passwordExpiryDays: number
    enableAccountLockout: boolean
    maxFailedAttempts: number
    lockoutDuration: number
    enableAuditLogging: boolean
    auditRetentionDays: number
  }
  twoFactor: {
    required: boolean
    allowedMethods: string[]
    gracePeriodDays: number
    backupCodesCount: number
    enableTrustedDevices: boolean
    trustedDevicesDays: number
  }
  passwordPolicy: {
    minLength: number
    requireUppercase: boolean
    requireLowercase: boolean
    requireNumbers: boolean
    requireSpecialChars: boolean
    preventReuse: number
    strengthRequirement: 'weak' | 'medium' | 'strong'
  }
  notifications: {
    enableEmailAlerts: boolean
    enableSmsAlerts: boolean
    alertsFor: string[]
    adminEmails: string[]
    webhookUrl: string
  }
  ipSecurity: {
    enableWhitelist: boolean
    allowedIpRanges: string[]
    enableGeoBlocking: boolean
    blockedCountries: string[]
    detectVPN: boolean
  }
  advanced: {
    enableEncryption: boolean
    encryptionLevel: 'standard' | 'high'
    enableRateLimiting: boolean
    requestsPerMinute: number
    enableCORS: boolean
    allowedOrigins: string[]
  }
}

export function SecuritySettings() {
  const [activeTab, setActiveTab] = useState('general')
  const [showSensitive, setShowSensitive] = useState(false)
  const [hasChanges, setHasChanges] = useState(false)
  
  // Mock initial configuration
  const [config, setConfig] = useState<SecurityConfig>({
    general: {
      sessionTimeout: 30,
      maxConcurrentSessions: 3,
      passwordExpiryDays: 90,
      enableAccountLockout: true,
      maxFailedAttempts: 5,
      lockoutDuration: 15,
      enableAuditLogging: true,
      auditRetentionDays: 365
    },
    twoFactor: {
      required: false,
      allowedMethods: ['totp', 'sms'],
      gracePeriodDays: 7,
      backupCodesCount: 10,
      enableTrustedDevices: true,
      trustedDevicesDays: 30
    },
    passwordPolicy: {
      minLength: 8,
      requireUppercase: true,
      requireLowercase: true,
      requireNumbers: true,
      requireSpecialChars: false,
      preventReuse: 5,
      strengthRequirement: 'medium'
    },
    notifications: {
      enableEmailAlerts: true,
      enableSmsAlerts: false,
      alertsFor: ['failed_login', 'policy_change', 'security_incident'],
      adminEmails: ['admin@company.com', 'security@company.com'],
      webhookUrl: ''
    },
    ipSecurity: {
      enableWhitelist: false,
      allowedIpRanges: ['192.168.1.0/24'],
      enableGeoBlocking: false,
      blockedCountries: ['CN', 'RU'],
      detectVPN: true
    },
    advanced: {
      enableEncryption: true,
      encryptionLevel: 'high',
      enableRateLimiting: true,
      requestsPerMinute: 100,
      enableCORS: true,
      allowedOrigins: ['https://app.company.com']
    }
  })

  const updateConfig = (section: keyof SecurityConfig, field: string, value: any) => {
    setConfig(prev => ({
      ...prev,
      [section]: {
        ...prev[section],
        [field]: value
      }
    }))
    setHasChanges(true)
  }

  const addArrayItem = (section: keyof SecurityConfig, field: string, value: string) => {
    if (!value.trim()) return
    
    setConfig(prev => ({
      ...prev,
      [section]: {
        ...prev[section],
        [field]: [...(prev[section][field] as string[]), value.trim()]
      }
    }))
    setHasChanges(true)
  }

  const removeArrayItem = (section: keyof SecurityConfig, field: string, index: number) => {
    setConfig(prev => ({
      ...prev,
      [section]: {
        ...prev[section],
        [field]: (prev[section][field] as string[]).filter((_, i) => i !== index)
      }
    }))
    setHasChanges(true)
  }

  const handleSave = async () => {
    // Simulate API call
    await new Promise(resolve => setTimeout(resolve, 1000))
    setHasChanges(false)
    // Show success message or notification
  }

  const handleReset = () => {
    // Reset to original values
    setHasChanges(false)
    // Reload original config
  }

  const ArrayInputField = ({ 
    label, 
    items, 
    onAdd, 
    onRemove, 
    placeholder 
  }: {
    label: string
    items: string[]
    onAdd: (value: string) => void
    onRemove: (index: number) => void
    placeholder: string
  }) => {
    const [newItem, setNewItem] = useState('')

    const handleAdd = () => {
      onAdd(newItem)
      setNewItem('')
    }

    return (
      <div className="space-y-2">
        <Label className="text-sm font-medium">{label}</Label>
        <div className="flex gap-2">
          <Input
            value={newItem}
            onChange={(e) => setNewItem(e.target.value)}
            placeholder={placeholder}
            onKeyDown={(e) => e.key === 'Enter' && handleAdd()}
          />
          <Button onClick={handleAdd} size="sm" variant="outline">
            <Plus className="h-4 w-4" />
          </Button>
        </div>
        <div className="flex flex-wrap gap-2">
          {items.map((item, index) => (
            <Badge key={index} variant="secondary" className="flex items-center gap-1">
              {item}
              <Button
                variant="ghost"
                size="sm"
                className="h-auto p-0 hover:bg-transparent"
                onClick={() => onRemove(index)}
              >
                <X className="h-3 w-3" />
              </Button>
            </Badge>
          ))}
        </div>
      </div>
    )
  }

  return (
    <div className="space-y-6">
      
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-2">
          <Settings className="h-6 w-6" />
          <div>
            <h2 className="text-2xl font-bold">Security Settings</h2>
            <p className="text-muted-foreground">
              Configure security policies and system behavior
            </p>
          </div>
        </div>
        
        <div className="flex items-center gap-2">
          <Button 
            variant="outline" 
            size="sm"
            onClick={() => setShowSensitive(!showSensitive)}
          >
            {showSensitive ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
            {showSensitive ? 'Hide' : 'Show'} Sensitive
          </Button>
          
          {hasChanges && (
            <>
              <Button variant="outline" size="sm" onClick={handleReset}>
                <RotateCcw className="h-4 w-4 mr-2" />
                Reset
              </Button>
              <Button size="sm" onClick={handleSave}>
                <Save className="h-4 w-4 mr-2" />
                Save Changes
              </Button>
            </>
          )}
        </div>
      </div>

      {hasChanges && (
        <Alert>
          <AlertTriangle className="h-4 w-4" />
          <AlertDescription>
            You have unsaved changes. Don't forget to save your configuration.
          </AlertDescription>
        </Alert>
      )}

      <Tabs value={activeTab} onValueChange={setActiveTab}>
        <TabsList className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-6 w-full">
          <TabsTrigger value="general">General</TabsTrigger>
          <TabsTrigger value="2fa">Two-Factor</TabsTrigger>
          <TabsTrigger value="password">Password</TabsTrigger>
          <TabsTrigger value="notifications">Notifications</TabsTrigger>
          <TabsTrigger value="ip-security">IP Security</TabsTrigger>
          <TabsTrigger value="advanced">Advanced</TabsTrigger>
        </TabsList>

        {/* General Settings */}
        <TabsContent value="general" className="space-y-6">
          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <Settings className="h-5 w-5" />
                General Security Settings
              </CardTitle>
            </CardHeader>
            <CardContent className="space-y-6">
              
              {/* Session Management */}
              <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                <div className="space-y-2">
                  <Label htmlFor="sessionTimeout">Session Timeout (minutes)</Label>
                  <Input
                    id="sessionTimeout"
                    type="number"
                    value={config.general.sessionTimeout}
                    onChange={(e) => updateConfig('general', 'sessionTimeout', parseInt(e.target.value))}
                  />
                  <p className="text-xs text-muted-foreground">
                    Automatic logout after inactivity
                  </p>
                </div>
                
                <div className="space-y-2">
                  <Label htmlFor="maxSessions">Max Concurrent Sessions</Label>
                  <Input
                    id="maxSessions"
                    type="number"
                    value={config.general.maxConcurrentSessions}
                    onChange={(e) => updateConfig('general', 'maxConcurrentSessions', parseInt(e.target.value))}
                  />
                  <p className="text-xs text-muted-foreground">
                    Maximum active sessions per user
                  </p>
                </div>
              </div>

              {/* Account Lockout */}
              <div className="space-y-4">
                <div className="flex items-center justify-between">
                  <div>
                    <Label className="text-base">Account Lockout</Label>
                    <p className="text-sm text-muted-foreground">
                      Temporarily lock accounts after failed login attempts
                    </p>
                  </div>
                  <Switch
                    checked={config.general.enableAccountLockout}
                    onCheckedChange={(checked) => updateConfig('general', 'enableAccountLockout', checked)}
                  />
                </div>
                
                {config.general.enableAccountLockout && (
                  <div className="grid grid-cols-1 md:grid-cols-2 gap-4 pl-6">
                    <div className="space-y-2">
                      <Label htmlFor="maxAttempts">Max Failed Attempts</Label>
                      <Input
                        id="maxAttempts"
                        type="number"
                        value={config.general.maxFailedAttempts}
                        onChange={(e) => updateConfig('general', 'maxFailedAttempts', parseInt(e.target.value))}
                      />
                    </div>
                    
                    <div className="space-y-2">
                      <Label htmlFor="lockoutDuration">Lockout Duration (minutes)</Label>
                      <Input
                        id="lockoutDuration"
                        type="number"
                        value={config.general.lockoutDuration}
                        onChange={(e) => updateConfig('general', 'lockoutDuration', parseInt(e.target.value))}
                      />
                    </div>
                  </div>
                )}
              </div>

              {/* Audit Logging */}
              <div className="space-y-4">
                <div className="flex items-center justify-between">
                  <div>
                    <Label className="text-base">Audit Logging</Label>
                    <p className="text-sm text-muted-foreground">
                      Track and log all security-related events
                    </p>
                  </div>
                  <Switch
                    checked={config.general.enableAuditLogging}
                    onCheckedChange={(checked) => updateConfig('general', 'enableAuditLogging', checked)}
                  />
                </div>
                
                {config.general.enableAuditLogging && (
                  <div className="pl-6">
                    <div className="space-y-2">
                      <Label htmlFor="auditRetention">Log Retention (days)</Label>
                      <Input
                        id="auditRetention"
                        type="number"
                        value={config.general.auditRetentionDays}
                        onChange={(e) => updateConfig('general', 'auditRetentionDays', parseInt(e.target.value))}
                        className="w-48"
                      />
                    </div>
                  </div>
                )}
              </div>
            </CardContent>
          </Card>
        </TabsContent>

        {/* Two-Factor Authentication */}
        <TabsContent value="2fa" className="space-y-6">
          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <Shield className="h-5 w-5" />
                Two-Factor Authentication Settings
              </CardTitle>
            </CardHeader>
            <CardContent className="space-y-6">
              
              <div className="flex items-center justify-between">
                <div>
                  <Label className="text-base">Require 2FA for All Users</Label>
                  <p className="text-sm text-muted-foreground">
                    Mandate two-factor authentication for system access
                  </p>
                </div>
                <Switch
                  checked={config.twoFactor.required}
                  onCheckedChange={(checked) => updateConfig('twoFactor', 'required', checked)}
                />
              </div>

              <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                <div className="space-y-2">
                  <Label>Allowed Methods</Label>
                  <div className="space-y-2">
                    {['totp', 'sms', 'email'].map(method => (
                      <div key={method} className="flex items-center space-x-2">
                        <input
                          type="checkbox"
                          id={method}
                          checked={config.twoFactor.allowedMethods.includes(method)}
                          onChange={(e) => {
                            const methods = e.target.checked 
                              ? [...config.twoFactor.allowedMethods, method]
                              : config.twoFactor.allowedMethods.filter(m => m !== method)
                            updateConfig('twoFactor', 'allowedMethods', methods)
                          }}
                        />
                        <Label htmlFor={method} className="text-sm capitalize">{method === 'totp' ? 'Authenticator App' : method.toUpperCase()}</Label>
                      </div>
                    ))}
                  </div>
                </div>

                <div className="space-y-4">
                  <div className="space-y-2">
                    <Label htmlFor="gracePeriod">Grace Period (days)</Label>
                    <Input
                      id="gracePeriod"
                      type="number"
                      value={config.twoFactor.gracePeriodDays}
                      onChange={(e) => updateConfig('twoFactor', 'gracePeriodDays', parseInt(e.target.value))}
                    />
                    <p className="text-xs text-muted-foreground">
                      Allow users to set up 2FA within this timeframe
                    </p>
                  </div>

                  <div className="space-y-2">
                    <Label htmlFor="backupCodes">Backup Codes Count</Label>
                    <Input
                      id="backupCodes"
                      type="number"
                      value={config.twoFactor.backupCodesCount}
                      onChange={(e) => updateConfig('twoFactor', 'backupCodesCount', parseInt(e.target.value))}
                    />
                  </div>
                </div>
              </div>

              <div className="space-y-4">
                <div className="flex items-center justify-between">
                  <div>
                    <Label className="text-base">Trusted Devices</Label>
                    <p className="text-sm text-muted-foreground">
                      Allow users to mark devices as trusted
                    </p>
                  </div>
                  <Switch
                    checked={config.twoFactor.enableTrustedDevices}
                    onCheckedChange={(checked) => updateConfig('twoFactor', 'enableTrustedDevices', checked)}
                  />
                </div>
                
                {config.twoFactor.enableTrustedDevices && (
                  <div className="pl-6">
                    <div className="space-y-2">
                      <Label htmlFor="trustedDevicesDays">Trust Duration (days)</Label>
                      <Input
                        id="trustedDevicesDays"
                        type="number"
                        value={config.twoFactor.trustedDevicesDays}
                        onChange={(e) => updateConfig('twoFactor', 'trustedDevicesDays', parseInt(e.target.value))}
                        className="w-48"
                      />
                    </div>
                  </div>
                )}
              </div>
            </CardContent>
          </Card>
        </TabsContent>

        {/* Password Policy */}
        <TabsContent value="password" className="space-y-6">
          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <Key className="h-5 w-5" />
                Password Policy Configuration
              </CardTitle>
            </CardHeader>
            <CardContent className="space-y-6">
              
              <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                <div className="space-y-2">
                  <Label htmlFor="minLength">Minimum Length</Label>
                  <Input
                    id="minLength"
                    type="number"
                    value={config.passwordPolicy.minLength}
                    onChange={(e) => updateConfig('passwordPolicy', 'minLength', parseInt(e.target.value))}
                  />
                </div>

                <div className="space-y-2">
                  <Label htmlFor="strengthReq">Strength Requirement</Label>
                  <Select
                    value={config.passwordPolicy.strengthRequirement}
                    onValueChange={(value) => updateConfig('passwordPolicy', 'strengthRequirement', value)}
                  >
                    <SelectTrigger>
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="weak">Weak</SelectItem>
                      <SelectItem value="medium">Medium</SelectItem>
                      <SelectItem value="strong">Strong</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
              </div>

              <div className="space-y-4">
                <Label className="text-base">Character Requirements</Label>
                <div className="grid grid-cols-2 gap-4">
                  {[
                    { key: 'requireUppercase', label: 'Uppercase Letters' },
                    { key: 'requireLowercase', label: 'Lowercase Letters' },
                    { key: 'requireNumbers', label: 'Numbers' },
                    { key: 'requireSpecialChars', label: 'Special Characters' }
                  ].map(({ key, label }) => (
                    <div key={key} className="flex items-center justify-between">
                      <Label className="text-sm">{label}</Label>
                      <Switch
                        checked={config.passwordPolicy[key as keyof typeof config.passwordPolicy] as boolean}
                        onCheckedChange={(checked) => updateConfig('passwordPolicy', key, checked)}
                      />
                    </div>
                  ))}
                </div>
              </div>

              <div className="space-y-2">
                <Label htmlFor="preventReuse">Prevent Password Reuse</Label>
                <Input
                  id="preventReuse"
                  type="number"
                  value={config.passwordPolicy.preventReuse}
                  onChange={(e) => updateConfig('passwordPolicy', 'preventReuse', parseInt(e.target.value))}
                  className="w-48"
                />
                <p className="text-xs text-muted-foreground">
                  Number of previous passwords to remember
                </p>
              </div>
            </CardContent>
          </Card>
        </TabsContent>

        {/* Notifications */}
        <TabsContent value="notifications" className="space-y-6">
          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <Mail className="h-5 w-5" />
                Security Notifications
              </CardTitle>
            </CardHeader>
            <CardContent className="space-y-6">
              
              <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                <div className="flex items-center justify-between">
                  <div>
                    <Label className="text-base">Email Alerts</Label>
                    <p className="text-sm text-muted-foreground">Send email notifications</p>
                  </div>
                  <Switch
                    checked={config.notifications.enableEmailAlerts}
                    onCheckedChange={(checked) => updateConfig('notifications', 'enableEmailAlerts', checked)}
                  />
                </div>

                <div className="flex items-center justify-between">
                  <div>
                    <Label className="text-base">SMS Alerts</Label>
                    <p className="text-sm text-muted-foreground">Send SMS notifications</p>
                  </div>
                  <Switch
                    checked={config.notifications.enableSmsAlerts}
                    onCheckedChange={(checked) => updateConfig('notifications', 'enableSmsAlerts', checked)}
                  />
                </div>
              </div>

              <div className="space-y-4">
                <Label className="text-base">Alert Events</Label>
                <div className="grid grid-cols-1 md:grid-cols-2 gap-2">
                  {[
                    { key: 'failed_login', label: 'Failed Login Attempts' },
                    { key: 'policy_change', label: 'Policy Changes' },
                    { key: 'security_incident', label: 'Security Incidents' },
                    { key: 'user_lockout', label: 'User Lockouts' },
                    { key: 'password_change', label: 'Password Changes' },
                    { key: 'admin_access', label: 'Admin Access' }
                  ].map(({ key, label }) => (
                    <div key={key} className="flex items-center space-x-2">
                      <input
                        type="checkbox"
                        id={key}
                        checked={config.notifications.alertsFor.includes(key)}
                        onChange={(e) => {
                          const alerts = e.target.checked 
                            ? [...config.notifications.alertsFor, key]
                            : config.notifications.alertsFor.filter(a => a !== key)
                          updateConfig('notifications', 'alertsFor', alerts)
                        }}
                      />
                      <Label htmlFor={key} className="text-sm">{label}</Label>
                    </div>
                  ))}
                </div>
              </div>

              <ArrayInputField
                label="Admin Email Addresses"
                items={config.notifications.adminEmails}
                onAdd={(email) => addArrayItem('notifications', 'adminEmails', email)}
                onRemove={(index) => removeArrayItem('notifications', 'adminEmails', index)}
                placeholder="admin@company.com"
              />

              <div className="space-y-2">
                <Label htmlFor="webhook">Webhook URL</Label>
                <Input
                  id="webhook"
                  type="url"
                  value={showSensitive ? config.notifications.webhookUrl : '••••••••••••'}
                  onChange={(e) => updateConfig('notifications', 'webhookUrl', e.target.value)}
                  placeholder="https://your-webhook-endpoint.com"
                />
                <p className="text-xs text-muted-foreground">
                  Send security events to external webhook
                </p>
              </div>
            </CardContent>
          </Card>
        </TabsContent>

        {/* IP Security */}
        <TabsContent value="ip-security" className="space-y-6">
          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <Globe className="h-5 w-5" />
                IP & Geographic Security
              </CardTitle>
            </CardHeader>
            <CardContent className="space-y-6">
              
              <div className="space-y-4">
                <div className="flex items-center justify-between">
                  <div>
                    <Label className="text-base">IP Whitelist</Label>
                    <p className="text-sm text-muted-foreground">
                      Only allow access from specific IP ranges
                    </p>
                  </div>
                  <Switch
                    checked={config.ipSecurity.enableWhitelist}
                    onCheckedChange={(checked) => updateConfig('ipSecurity', 'enableWhitelist', checked)}
                  />
                </div>
                
                {config.ipSecurity.enableWhitelist && (
                  <div className="pl-6">
                    <ArrayInputField
                      label="Allowed IP Ranges"
                      items={config.ipSecurity.allowedIpRanges}
                      onAdd={(ip) => addArrayItem('ipSecurity', 'allowedIpRanges', ip)}
                      onRemove={(index) => removeArrayItem('ipSecurity', 'allowedIpRanges', index)}
                      placeholder="192.168.1.0/24 or 10.0.0.1"
                    />
                  </div>
                )}
              </div>

              <div className="space-y-4">
                <div className="flex items-center justify-between">
                  <div>
                    <Label className="text-base">Geographic Blocking</Label>
                    <p className="text-sm text-muted-foreground">
                      Block access from specific countries
                    </p>
                  </div>
                  <Switch
                    checked={config.ipSecurity.enableGeoBlocking}
                    onCheckedChange={(checked) => updateConfig('ipSecurity', 'enableGeoBlocking', checked)}
                  />
                </div>
                
                {config.ipSecurity.enableGeoBlocking && (
                  <div className="pl-6">
                    <ArrayInputField
                      label="Blocked Countries (ISO codes)"
                      items={config.ipSecurity.blockedCountries}
                      onAdd={(country) => addArrayItem('ipSecurity', 'blockedCountries', country)}
                      onRemove={(index) => removeArrayItem('ipSecurity', 'blockedCountries', index)}
                      placeholder="CN, RU, etc."
                    />
                  </div>
                )}
              </div>

              <div className="flex items-center justify-between">
                <div>
                  <Label className="text-base">VPN Detection</Label>
                  <p className="text-sm text-muted-foreground">
                    Detect and flag VPN/proxy connections
                  </p>
                </div>
                <Switch
                  checked={config.ipSecurity.detectVPN}
                  onCheckedChange={(checked) => updateConfig('ipSecurity', 'detectVPN', checked)}
                />
              </div>
            </CardContent>
          </Card>
        </TabsContent>

        {/* Advanced Settings */}
        <TabsContent value="advanced" className="space-y-6">
          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <Zap className="h-5 w-5" />
                Advanced Security Configuration
              </CardTitle>
            </CardHeader>
            <CardContent className="space-y-6">
              
              <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                <div className="flex items-center justify-between">
                  <div>
                    <Label className="text-base">Database Encryption</Label>
                    <p className="text-sm text-muted-foreground">Encrypt sensitive data</p>
                  </div>
                  <Switch
                    checked={config.advanced.enableEncryption}
                    onCheckedChange={(checked) => updateConfig('advanced', 'enableEncryption', checked)}
                  />
                </div>

                <div className="space-y-2">
                  <Label htmlFor="encryptionLevel">Encryption Level</Label>
                  <Select
                    value={config.advanced.encryptionLevel}
                    onValueChange={(value) => updateConfig('advanced', 'encryptionLevel', value)}
                  >
                    <SelectTrigger>
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="standard">Standard (AES-256)</SelectItem>
                      <SelectItem value="high">High (AES-256-GCM)</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
              </div>

              <div className="space-y-4">
                <div className="flex items-center justify-between">
                  <div>
                    <Label className="text-base">Rate Limiting</Label>
                    <p className="text-sm text-muted-foreground">
                      Limit API requests per user
                    </p>
                  </div>
                  <Switch
                    checked={config.advanced.enableRateLimiting}
                    onCheckedChange={(checked) => updateConfig('advanced', 'enableRateLimiting', checked)}
                  />
                </div>
                
                {config.advanced.enableRateLimiting && (
                  <div className="pl-6">
                    <div className="space-y-2">
                      <Label htmlFor="requestsPerMinute">Requests Per Minute</Label>
                      <Input
                        id="requestsPerMinute"
                        type="number"
                        value={config.advanced.requestsPerMinute}
                        onChange={(e) => updateConfig('advanced', 'requestsPerMinute', parseInt(e.target.value))}
                        className="w-48"
                      />
                    </div>
                  </div>
                )}
              </div>

              <div className="space-y-4">
                <div className="flex items-center justify-between">
                  <div>
                    <Label className="text-base">CORS Configuration</Label>
                    <p className="text-sm text-muted-foreground">
                      Configure cross-origin resource sharing
                    </p>
                  </div>
                  <Switch
                    checked={config.advanced.enableCORS}
                    onCheckedChange={(checked) => updateConfig('advanced', 'enableCORS', checked)}
                  />
                </div>
                
                {config.advanced.enableCORS && (
                  <div className="pl-6">
                    <ArrayInputField
                      label="Allowed Origins"
                      items={config.advanced.allowedOrigins}
                      onAdd={(origin) => addArrayItem('advanced', 'allowedOrigins', origin)}
                      onRemove={(index) => removeArrayItem('advanced', 'allowedOrigins', index)}
                      placeholder="https://app.company.com"
                    />
                  </div>
                )}
              </div>
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>
    </div>
  )
}