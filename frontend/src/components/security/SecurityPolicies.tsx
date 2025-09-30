"use client"

import React, { useState } from 'react'
import { Card, CardContent, CardHeader, CardTitle } from '../ui/card'
import { Button } from '../ui/button'
import { Input } from '../ui/input'
import { Label } from '../ui/label'
import { Badge } from '../ui/badge'
import { Switch } from '../ui/switch'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '../ui/select'
import { Textarea } from '../ui/textarea'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '../ui/tabs'
import { Alert, AlertDescription } from '../ui/alert'
import { 
  Shield, 
  Lock, 
  Clock, 
  Users, 
  Key, 
  AlertTriangle, 
  CheckCircle,
  Settings,
  Save,
  RotateCcw,
  Eye,
  EyeOff,
  Info,
  Globe,
  Smartphone,
  Monitor
} from 'lucide-react'

interface SecurityPolicy {
  id: string
  name: string
  description: string
  enabled: boolean
  lastModified: Date
  modifiedBy: string
}

interface PasswordPolicy {
  minLength: number
  requireUppercase: boolean
  requireLowercase: boolean
  requireNumbers: boolean
  requireSpecialChars: boolean
  preventReuse: number
  maxAge: number
  lockoutAttempts: number
  lockoutDuration: number
  complexityScore: number
}

interface SessionPolicy {
  maxSessionDuration: number
  idleTimeout: number
  maxConcurrentSessions: number
  requireReauthentication: boolean
  secureOnly: boolean
  sameSite: 'strict' | 'lax' | 'none'
}

interface AccessControlPolicy {
  mfaRequired: boolean
  ipWhitelisting: boolean
  allowedIpRanges: string[]
  deviceTrustRequired: boolean
  locationBasedAccess: boolean
  workingHoursOnly: boolean
  workingHours: {
    start: string
    end: string
    timezone: string
  }
}

export const SecurityPolicies: React.FC = () => {
  const [activeTab, setActiveTab] = useState('password')
  const [hasChanges, setHasChanges] = useState(false)
  const [isLoading, setIsLoading] = useState(false)
  
  // Password Policy State
  const [passwordPolicy, setPasswordPolicy] = useState<PasswordPolicy>({
    minLength: 8,
    requireUppercase: true,
    requireLowercase: true,
    requireNumbers: true,
    requireSpecialChars: true,
    preventReuse: 5,
    maxAge: 90,
    lockoutAttempts: 5,
    lockoutDuration: 30,
    complexityScore: 3
  })

  // Session Policy State
  const [sessionPolicy, setSessionPolicy] = useState<SessionPolicy>({
    maxSessionDuration: 8,
    idleTimeout: 30,
    maxConcurrentSessions: 3,
    requireReauthentication: true,
    secureOnly: true,
    sameSite: 'strict'
  })

  // Access Control Policy State
  const [accessPolicy, setAccessPolicy] = useState<AccessControlPolicy>({
    mfaRequired: true,
    ipWhitelisting: false,
    allowedIpRanges: ['192.168.1.0/24', '10.0.0.0/8'],
    deviceTrustRequired: false,
    locationBasedAccess: false,
    workingHoursOnly: false,
    workingHours: {
      start: '09:00',
      end: '17:00',
      timezone: 'UTC'
    }
  })

  // Mock security policies
  const [securityPolicies] = useState<SecurityPolicy[]>([
    {
      id: 'policy-1',
      name: 'Strong Password Enforcement',
      description: 'Enforces complex password requirements and prevents password reuse',
      enabled: true,
      lastModified: new Date('2024-01-15'),
      modifiedBy: 'Admin User'
    },
    {
      id: 'policy-2',
      name: 'Session Security',
      description: 'Controls session duration and concurrent login limits',
      enabled: true,
      lastModified: new Date('2024-01-10'),
      modifiedBy: 'Security Team'
    },
    {
      id: 'policy-3',
      name: 'Multi-Factor Authentication',
      description: 'Requires additional authentication factors for access',
      enabled: true,
      lastModified: new Date('2024-01-05'),
      modifiedBy: 'IT Administrator'
    },
    {
      id: 'policy-4',
      name: 'IP Whitelisting',
      description: 'Restricts access to approved IP addresses only',
      enabled: false,
      lastModified: new Date('2023-12-20'),
      modifiedBy: 'Network Admin'
    }
  ])

  const updatePasswordPolicy = (updates: Partial<PasswordPolicy>) => {
    setPasswordPolicy(prev => ({ ...prev, ...updates }))
    setHasChanges(true)
  }

  const updateSessionPolicy = (updates: Partial<SessionPolicy>) => {
    setSessionPolicy(prev => ({ ...prev, ...updates }))
    setHasChanges(true)
  }

  const updateAccessPolicy = (updates: Partial<AccessControlPolicy>) => {
    setAccessPolicy(prev => ({ ...prev, ...updates }))
    setHasChanges(true)
  }

  const calculatePasswordStrength = () => {
    let score = 0
    if (passwordPolicy.minLength >= 8) score++
    if (passwordPolicy.minLength >= 12) score++
    if (passwordPolicy.requireUppercase) score++
    if (passwordPolicy.requireLowercase) score++
    if (passwordPolicy.requireNumbers) score++
    if (passwordPolicy.requireSpecialChars) score++
    if (passwordPolicy.preventReuse >= 5) score++
    
    if (score <= 2) return { level: 'Weak', color: 'text-red-600', bgColor: 'bg-red-100' }
    if (score <= 4) return { level: 'Medium', color: 'text-yellow-600', bgColor: 'bg-yellow-100' }
    if (score <= 6) return { level: 'Strong', color: 'text-green-600', bgColor: 'bg-green-100' }
    return { level: 'Very Strong', color: 'text-blue-600', bgColor: 'bg-blue-100' }
  }

  const handleSavePolicies = async () => {
    setIsLoading(true)
    try {
      // Simulate API call
      await new Promise(resolve => setTimeout(resolve, 2000))
      setHasChanges(false)
    } catch (error) {
      console.error('Failed to save policies:', error)
    } finally {
      setIsLoading(false)
    }
  }

  const handleResetPolicies = () => {
    // Reset to default values
    setPasswordPolicy({
      minLength: 8,
      requireUppercase: true,
      requireLowercase: true,
      requireNumbers: true,
      requireSpecialChars: true,
      preventReuse: 5,
      maxAge: 90,
      lockoutAttempts: 5,
      lockoutDuration: 30,
      complexityScore: 3
    })
    
    setSessionPolicy({
      maxSessionDuration: 8,
      idleTimeout: 30,
      maxConcurrentSessions: 3,
      requireReauthentication: true,
      secureOnly: true,
      sameSite: 'strict'
    })
    
    setAccessPolicy({
      mfaRequired: true,
      ipWhitelisting: false,
      allowedIpRanges: ['192.168.1.0/24', '10.0.0.0/8'],
      deviceTrustRequired: false,
      locationBasedAccess: false,
      workingHoursOnly: false,
      workingHours: {
        start: '09:00',
        end: '17:00',
        timezone: 'UTC'
      }
    })
    
    setHasChanges(false)
  }

  const passwordStrength = calculatePasswordStrength()

  return (
    <div className="space-y-6">
      
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-2xl font-bold">Security Policies</h2>
          <p className="text-muted-foreground">
            Configure and manage security policies for your organization
          </p>
        </div>
        
        <div className="flex gap-2">
          <Button 
            variant="outline" 
            onClick={handleResetPolicies}
            disabled={!hasChanges}
          >
            <RotateCcw className="h-4 w-4 mr-2" />
            Reset
          </Button>
          <Button 
            onClick={handleSavePolicies} 
            disabled={!hasChanges || isLoading}
          >
            {isLoading ? (
              <div className="h-4 w-4 mr-2 animate-spin rounded-full border-2 border-white border-t-transparent" />
            ) : (
              <Save className="h-4 w-4 mr-2" />
            )}
            Save Changes
          </Button>
        </div>
      </div>

      {/* Active Policies Overview */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <Shield className="h-5 w-5" />
            Active Security Policies
          </CardTitle>
        </CardHeader>
        <CardContent>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            {securityPolicies.map((policy) => (
              <div key={policy.id} className="flex items-center justify-between p-3 border rounded-lg">
                <div className="flex-1">
                  <div className="flex items-center gap-2">
                    <h4 className="font-medium">{policy.name}</h4>
                    <Badge variant={policy.enabled ? "default" : "secondary"}>
                      {policy.enabled ? 'Active' : 'Inactive'}
                    </Badge>
                  </div>
                  <p className="text-sm text-muted-foreground mt-1">
                    {policy.description}
                  </p>
                  <p className="text-xs text-muted-foreground mt-1">
                    Modified by {policy.modifiedBy} on {policy.lastModified.toLocaleDateString()}
                  </p>
                </div>
                <Switch checked={policy.enabled} />
              </div>
            ))}
          </div>
        </CardContent>
      </Card>

      {/* Policy Configuration Tabs */}
      <Tabs value={activeTab} onValueChange={setActiveTab}>
        <TabsList className="grid w-full grid-cols-3">
          <TabsTrigger value="password">Password Policy</TabsTrigger>
          <TabsTrigger value="session">Session Policy</TabsTrigger>
          <TabsTrigger value="access">Access Control</TabsTrigger>
        </TabsList>

        {/* Password Policy Tab */}
        <TabsContent value="password" className="space-y-4">
          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <Lock className="h-5 w-5" />
                Password Requirements
              </CardTitle>
            </CardHeader>
            <CardContent className="space-y-6">
              
              {/* Password Strength Indicator */}
              <div className={`p-4 rounded-lg ${passwordStrength.bgColor}`}>
                <div className="flex items-center gap-2 mb-2">
                  <Shield className="h-4 w-4" />
                  <span className="font-medium">Current Policy Strength</span>
                  <Badge className={passwordStrength.color}>
                    {passwordStrength.level}
                  </Badge>
                </div>
                <p className="text-sm opacity-80">
                  Based on your current password requirements configuration
                </p>
              </div>

              <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                
                {/* Basic Requirements */}
                <div className="space-y-4">
                  <h4 className="font-medium">Basic Requirements</h4>
                  
                  <div className="space-y-2">
                    <Label htmlFor="min-length">Minimum Length</Label>
                    <Input
                      id="min-length"
                      type="number"
                      min="6"
                      max="20"
                      value={passwordPolicy.minLength}
                      onChange={(e) => updatePasswordPolicy({ minLength: parseInt(e.target.value) || 8 })}
                    />
                  </div>

                  <div className="space-y-3">
                    <div className="flex items-center justify-between">
                      <Label>Require uppercase letters (A-Z)</Label>
                      <Switch
                        checked={passwordPolicy.requireUppercase}
                        onCheckedChange={(checked) => updatePasswordPolicy({ requireUppercase: checked })}
                      />
                    </div>
                    
                    <div className="flex items-center justify-between">
                      <Label>Require lowercase letters (a-z)</Label>
                      <Switch
                        checked={passwordPolicy.requireLowercase}
                        onCheckedChange={(checked) => updatePasswordPolicy({ requireLowercase: checked })}
                      />
                    </div>
                    
                    <div className="flex items-center justify-between">
                      <Label>Require numbers (0-9)</Label>
                      <Switch
                        checked={passwordPolicy.requireNumbers}
                        onCheckedChange={(checked) => updatePasswordPolicy({ requireNumbers: checked })}
                      />
                    </div>
                    
                    <div className="flex items-center justify-between">
                      <Label>Require special characters (!@#$%)</Label>
                      <Switch
                        checked={passwordPolicy.requireSpecialChars}
                        onCheckedChange={(checked) => updatePasswordPolicy({ requireSpecialChars: checked })}
                      />
                    </div>
                  </div>
                </div>

                {/* Advanced Settings */}
                <div className="space-y-4">
                  <h4 className="font-medium">Advanced Settings</h4>
                  
                  <div className="space-y-2">
                    <Label htmlFor="prevent-reuse">Prevent password reuse (last N passwords)</Label>
                    <Input
                      id="prevent-reuse"
                      type="number"
                      min="0"
                      max="20"
                      value={passwordPolicy.preventReuse}
                      onChange={(e) => updatePasswordPolicy({ preventReuse: parseInt(e.target.value) || 0 })}
                    />
                  </div>

                  <div className="space-y-2">
                    <Label htmlFor="max-age">Password expiry (days)</Label>
                    <Input
                      id="max-age"
                      type="number"
                      min="0"
                      max="365"
                      value={passwordPolicy.maxAge}
                      onChange={(e) => updatePasswordPolicy({ maxAge: parseInt(e.target.value) || 0 })}
                    />
                    <p className="text-xs text-muted-foreground">Set to 0 to disable expiry</p>
                  </div>

                  <div className="space-y-2">
                    <Label htmlFor="lockout-attempts">Failed attempts before lockout</Label>
                    <Input
                      id="lockout-attempts"
                      type="number"
                      min="3"
                      max="10"
                      value={passwordPolicy.lockoutAttempts}
                      onChange={(e) => updatePasswordPolicy({ lockoutAttempts: parseInt(e.target.value) || 5 })}
                    />
                  </div>

                  <div className="space-y-2">
                    <Label htmlFor="lockout-duration">Lockout duration (minutes)</Label>
                    <Input
                      id="lockout-duration"
                      type="number"
                      min="5"
                      max="1440"
                      value={passwordPolicy.lockoutDuration}
                      onChange={(e) => updatePasswordPolicy({ lockoutDuration: parseInt(e.target.value) || 30 })}
                    />
                  </div>
                </div>
              </div>
            </CardContent>
          </Card>
        </TabsContent>

        {/* Session Policy Tab */}
        <TabsContent value="session" className="space-y-4">
          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <Clock className="h-5 w-5" />
                Session Management
              </CardTitle>
            </CardHeader>
            <CardContent>
              <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                
                <div className="space-y-4">
                  <h4 className="font-medium">Session Timeouts</h4>
                  
                  <div className="space-y-2">
                    <Label htmlFor="max-session">Maximum session duration (hours)</Label>
                    <Input
                      id="max-session"
                      type="number"
                      min="1"
                      max="24"
                      value={sessionPolicy.maxSessionDuration}
                      onChange={(e) => updateSessionPolicy({ maxSessionDuration: parseInt(e.target.value) || 8 })}
                    />
                  </div>

                  <div className="space-y-2">
                    <Label htmlFor="idle-timeout">Idle timeout (minutes)</Label>
                    <Input
                      id="idle-timeout"
                      type="number"
                      min="5"
                      max="120"
                      value={sessionPolicy.idleTimeout}
                      onChange={(e) => updateSessionPolicy({ idleTimeout: parseInt(e.target.value) || 30 })}
                    />
                  </div>

                  <div className="space-y-2">
                    <Label htmlFor="concurrent-sessions">Max concurrent sessions</Label>
                    <Input
                      id="concurrent-sessions"
                      type="number"
                      min="1"
                      max="10"
                      value={sessionPolicy.maxConcurrentSessions}
                      onChange={(e) => updateSessionPolicy({ maxConcurrentSessions: parseInt(e.target.value) || 3 })}
                    />
                  </div>
                </div>

                <div className="space-y-4">
                  <h4 className="font-medium">Security Options</h4>
                  
                  <div className="flex items-center justify-between">
                    <div>
                      <Label>Require re-authentication for sensitive actions</Label>
                      <p className="text-xs text-muted-foreground">Ask for password before critical operations</p>
                    </div>
                    <Switch
                      checked={sessionPolicy.requireReauthentication}
                      onCheckedChange={(checked) => updateSessionPolicy({ requireReauthentication: checked })}
                    />
                  </div>
                  
                  <div className="flex items-center justify-between">
                    <div>
                      <Label>Secure cookies only</Label>
                      <p className="text-xs text-muted-foreground">Only send cookies over HTTPS</p>
                    </div>
                    <Switch
                      checked={sessionPolicy.secureOnly}
                      onCheckedChange={(checked) => updateSessionPolicy({ secureOnly: checked })}
                    />
                  </div>

                  <div className="space-y-2">
                    <Label htmlFor="same-site">SameSite cookie policy</Label>
                    <Select 
                      value={sessionPolicy.sameSite} 
                      onValueChange={(value: 'strict' | 'lax' | 'none') => updateSessionPolicy({ sameSite: value })}
                    >
                      <SelectTrigger>
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="strict">Strict</SelectItem>
                        <SelectItem value="lax">Lax</SelectItem>
                        <SelectItem value="none">None</SelectItem>
                      </SelectContent>
                    </Select>
                  </div>
                </div>
              </div>
            </CardContent>
          </Card>
        </TabsContent>

        {/* Access Control Tab */}
        <TabsContent value="access" className="space-y-4">
          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <Users className="h-5 w-5" />
                Access Control Policies
              </CardTitle>
            </CardHeader>
            <CardContent>
              <div className="space-y-6">
                
                {/* Authentication Requirements */}
                <div className="space-y-4">
                  <h4 className="font-medium">Authentication Requirements</h4>
                  
                  <div className="flex items-center justify-between">
                    <div>
                      <Label>Multi-Factor Authentication Required</Label>
                      <p className="text-xs text-muted-foreground">Require 2FA for all users</p>
                    </div>
                    <Switch
                      checked={accessPolicy.mfaRequired}
                      onCheckedChange={(checked) => updateAccessPolicy({ mfaRequired: checked })}
                    />
                  </div>
                  
                  <div className="flex items-center justify-between">
                    <div>
                      <Label>Device Trust Required</Label>
                      <p className="text-xs text-muted-foreground">Only allow access from trusted devices</p>
                    </div>
                    <Switch
                      checked={accessPolicy.deviceTrustRequired}
                      onCheckedChange={(checked) => updateAccessPolicy({ deviceTrustRequired: checked })}
                    />
                  </div>
                </div>

                {/* Network Access Controls */}
                <div className="space-y-4">
                  <h4 className="font-medium">Network Access Controls</h4>
                  
                  <div className="flex items-center justify-between">
                    <div>
                      <Label>IP Address Whitelisting</Label>
                      <p className="text-xs text-muted-foreground">Restrict access to specific IP ranges</p>
                    </div>
                    <Switch
                      checked={accessPolicy.ipWhitelisting}
                      onCheckedChange={(checked) => updateAccessPolicy({ ipWhitelisting: checked })}
                    />
                  </div>

                  {accessPolicy.ipWhitelisting && (
                    <div className="space-y-2">
                      <Label>Allowed IP Ranges</Label>
                      <Textarea
                        placeholder="192.168.1.0/24&#10;10.0.0.0/8&#10;172.16.0.0/12"
                        value={accessPolicy.allowedIpRanges.join('\n')}
                        onChange={(e) => updateAccessPolicy({ 
                          allowedIpRanges: e.target.value.split('\n').filter(ip => ip.trim()) 
                        })}
                        rows={4}
                      />
                      <p className="text-xs text-muted-foreground">
                        Enter one IP range per line (CIDR notation supported)
                      </p>
                    </div>
                  )}
                </div>

                {/* Location and Time Based Access */}
                <div className="space-y-4">
                  <h4 className="font-medium">Location and Time Based Access</h4>
                  
                  <div className="flex items-center justify-between">
                    <div>
                      <Label>Location-Based Access Control</Label>
                      <p className="text-xs text-muted-foreground">Restrict access based on geographic location</p>
                    </div>
                    <Switch
                      checked={accessPolicy.locationBasedAccess}
                      onCheckedChange={(checked) => updateAccessPolicy({ locationBasedAccess: checked })}
                    />
                  </div>
                  
                  <div className="flex items-center justify-between">
                    <div>
                      <Label>Working Hours Only</Label>
                      <p className="text-xs text-muted-foreground">Restrict access to business hours</p>
                    </div>
                    <Switch
                      checked={accessPolicy.workingHoursOnly}
                      onCheckedChange={(checked) => updateAccessPolicy({ workingHoursOnly: checked })}
                    />
                  </div>

                  {accessPolicy.workingHoursOnly && (
                    <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                      <div className="space-y-2">
                        <Label htmlFor="work-start">Start Time</Label>
                        <Input
                          id="work-start"
                          type="time"
                          value={accessPolicy.workingHours.start}
                          onChange={(e) => updateAccessPolicy({ 
                            workingHours: { ...accessPolicy.workingHours, start: e.target.value } 
                          })}
                        />
                      </div>
                      <div className="space-y-2">
                        <Label htmlFor="work-end">End Time</Label>
                        <Input
                          id="work-end"
                          type="time"
                          value={accessPolicy.workingHours.end}
                          onChange={(e) => updateAccessPolicy({ 
                            workingHours: { ...accessPolicy.workingHours, end: e.target.value } 
                          })}
                        />
                      </div>
                      <div className="space-y-2">
                        <Label htmlFor="timezone">Timezone</Label>
                        <Select 
                          value={accessPolicy.workingHours.timezone}
                          onValueChange={(value) => updateAccessPolicy({ 
                            workingHours: { ...accessPolicy.workingHours, timezone: value } 
                          })}
                        >
                          <SelectTrigger>
                            <SelectValue />
                          </SelectTrigger>
                          <SelectContent>
                            <SelectItem value="UTC">UTC</SelectItem>
                            <SelectItem value="America/New_York">Eastern Time</SelectItem>
                            <SelectItem value="America/Chicago">Central Time</SelectItem>
                            <SelectItem value="America/Denver">Mountain Time</SelectItem>
                            <SelectItem value="America/Los_Angeles">Pacific Time</SelectItem>
                            <SelectItem value="Europe/London">London</SelectItem>
                          </SelectContent>
                        </Select>
                      </div>
                    </div>
                  )}
                </div>
              </div>
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>

      {/* Save Changes Alert */}
      {hasChanges && (
        <Alert>
          <Info className="h-4 w-4" />
          <AlertDescription>
            You have unsaved changes. Click "Save Changes" to apply your security policy updates.
          </AlertDescription>
        </Alert>
      )}
    </div>
  )
}