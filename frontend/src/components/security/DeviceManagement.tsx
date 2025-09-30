"use client"

import React, { useState } from 'react'
import { Card, CardContent, CardHeader, CardTitle, CardDescription, CardFooter } from '../ui/card'
import { Button } from '../ui/button'
import { Badge } from '../ui/badge'
import { Input } from '../ui/input'
import { Label } from '../ui/label'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '../ui/select'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '../ui/tabs'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger } from '../ui/dialog'
import { Alert, AlertDescription } from '../ui/alert'
import { 
  Smartphone, 
  Monitor, 
  Laptop, 
  Tablet, 
  Globe, 
  Clock, 
  MapPin, 
  Calendar, 
  CheckCircle, 
  XCircle,
  AlertTriangle,
  Search,
  Eye,
  LogOut,
  Lock,
  Unlock,
  ShieldCheck,
  ShieldX,
  RefreshCw
} from 'lucide-react'

interface Device {
  id: string
  type: 'mobile' | 'tablet' | 'desktop' | 'laptop' | 'other'
  name: string
  lastActive: Date
  browser: string
  os: string
  isTrusted: boolean
  location?: {
    ip: string
    city: string
    country: string
    coordinates?: string
  }
  riskLevel: 'low' | 'medium' | 'high'
}

interface Session {
  id: string
  deviceId: string
  startTime: Date
  expiresAt: Date
  isActive: boolean
  ipAddress: string
  userAgent: string
  lastActivity: Date
  location?: {
    city: string
    country: string
  }
}

export function DeviceManagement() {
  // Mock data for devices and sessions
  const [devices, setDevices] = useState<Device[]>([
    {
      id: '1',
      type: 'laptop',
      name: 'MacBook Pro',
      lastActive: new Date(Date.now() - 5 * 60 * 1000), // 5 minutes ago
      browser: 'Chrome 121.0',
      os: 'macOS 13.1',
      isTrusted: true,
      location: {
        ip: '192.168.1.5',
        city: 'New York',
        country: 'United States',
        coordinates: '40.7128° N, 74.0060° W'
      },
      riskLevel: 'low'
    },
    {
      id: '2',
      type: 'mobile',
      name: 'iPhone 15',
      lastActive: new Date(Date.now() - 3 * 60 * 60 * 1000), // 3 hours ago
      browser: 'Safari 17.0',
      os: 'iOS 17.2',
      isTrusted: true,
      location: {
        ip: '10.0.0.15',
        city: 'New York',
        country: 'United States',
        coordinates: '40.7128° N, 74.0060° W'
      },
      riskLevel: 'low'
    },
    {
      id: '3',
      type: 'desktop',
      name: 'Work Computer',
      lastActive: new Date(Date.now() - 2 * 24 * 60 * 60 * 1000), // 2 days ago
      browser: 'Edge 121.0',
      os: 'Windows 11',
      isTrusted: false,
      location: {
        ip: '203.0.113.45',
        city: 'Toronto',
        country: 'Canada',
        coordinates: '43.6532° N, 79.3832° W'
      },
      riskLevel: 'medium'
    },
    {
      id: '4',
      type: 'tablet',
      name: 'Samsung Tablet',
      lastActive: new Date(Date.now() - 15 * 24 * 60 * 60 * 1000), // 15 days ago
      browser: 'Chrome 120.0',
      os: 'Android 14',
      isTrusted: false,
      location: {
        ip: '203.0.113.198',
        city: 'Tokyo',
        country: 'Japan',
        coordinates: '35.6762° N, 139.6503° E'
      },
      riskLevel: 'high'
    }
  ])

  const [sessions, setSessions] = useState<Session[]>([
    {
      id: 's1',
      deviceId: '1',
      startTime: new Date(Date.now() - 45 * 60 * 1000), // 45 minutes ago
      expiresAt: new Date(Date.now() + 45 * 60 * 1000), // 45 minutes from now
      isActive: true,
      ipAddress: '192.168.1.5',
      userAgent: 'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7)',
      lastActivity: new Date(Date.now() - 5 * 60 * 1000), // 5 minutes ago
      location: {
        city: 'New York',
        country: 'United States'
      }
    },
    {
      id: 's2',
      deviceId: '2',
      startTime: new Date(Date.now() - 4 * 60 * 60 * 1000), // 4 hours ago
      expiresAt: new Date(Date.now() - 60 * 60 * 1000), // expired 1 hour ago
      isActive: false,
      ipAddress: '10.0.0.15',
      userAgent: 'Mozilla/5.0 (iPhone; CPU iPhone OS 17_2 like Mac OS X)',
      lastActivity: new Date(Date.now() - 3 * 60 * 60 * 1000), // 3 hours ago
      location: {
        city: 'New York',
        country: 'United States'
      }
    },
    {
      id: 's3',
      deviceId: '3',
      startTime: new Date(Date.now() - 3 * 24 * 60 * 60 * 1000), // 3 days ago
      expiresAt: new Date(Date.now() - 2 * 24 * 60 * 60 * 1000 - 30 * 60 * 1000), // expired 2 days and 30 minutes ago
      isActive: false,
      ipAddress: '203.0.113.45',
      userAgent: 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36',
      lastActivity: new Date(Date.now() - 2 * 24 * 60 * 60 * 1000), // 2 days ago
      location: {
        city: 'Toronto',
        country: 'Canada'
      }
    }
  ])

  const [suspiciousActivity, setSuspiciousActivity] = useState([
    {
      id: 'a1',
      deviceId: '4',
      timestamp: new Date(Date.now() - 15 * 24 * 60 * 60 * 1000 + 30 * 60 * 1000), // 15 days ago + 30 minutes
      type: 'location_change',
      description: 'Login from unusual location (Tokyo, Japan)',
      severity: 'high'
    },
    {
      id: 'a2',
      deviceId: '3',
      timestamp: new Date(Date.now() - 2 * 24 * 60 * 60 * 1000 + 15 * 60 * 1000), // 2 days ago + 15 minutes
      type: 'multiple_attempts',
      description: 'Multiple failed login attempts (5) before success',
      severity: 'medium'
    },
    {
      id: 'a3',
      deviceId: '1',
      timestamp: new Date(Date.now() - 7 * 24 * 60 * 60 * 1000), // 7 days ago
      type: 'unusual_timing',
      description: 'Login at unusual time (3:27 AM)',
      severity: 'low'
    }
  ])

  const [activeTab, setActiveTab] = useState('devices')
  const [searchTerm, setSearchTerm] = useState('')
  const [deviceFilter, setDeviceFilter] = useState('all')
  const [sessionFilter, setSessionFilter] = useState('active')
  const [selectedDevice, setSelectedDevice] = useState<Device | null>(null)
  const [showDeviceDetails, setShowDeviceDetails] = useState(false)

  const getDeviceIcon = (type: Device['type']) => {
    switch (type) {
      case 'mobile': return <Smartphone className="h-5 w-5" />
      case 'tablet': return <Tablet className="h-5 w-5" />
      case 'laptop': return <Laptop className="h-5 w-5" />
      case 'desktop': return <Monitor className="h-5 w-5" />
      default: return <Monitor className="h-5 w-5" />
    }
  }

  const formatTimeAgo = (date: Date) => {
    const now = new Date()
    const diff = now.getTime() - date.getTime()
    const minutes = Math.floor(diff / (1000 * 60))
    const hours = Math.floor(minutes / 60)
    const days = Math.floor(hours / 24)
    
    if (minutes < 60) {
      return `${minutes} minutes ago`
    } else if (hours < 24) {
      return `${hours} hours ago`
    } else {
      return `${days} days ago`
    }
  }

  const formatRiskLevel = (level: 'low' | 'medium' | 'high') => {
    switch (level) {
      case 'low': return (
        <Badge variant="outline" className="text-green-600 border-green-200 flex items-center gap-1">
          <ShieldCheck className="h-3 w-3" /> Low
        </Badge>
      )
      case 'medium': return (
        <Badge variant="outline" className="text-yellow-600 border-yellow-200 flex items-center gap-1">
          <AlertTriangle className="h-3 w-3" /> Medium
        </Badge>
      )
      case 'high': return (
        <Badge variant="outline" className="text-red-600 border-red-200 flex items-center gap-1">
          <ShieldX className="h-3 w-3" /> High
        </Badge>
      )
      default: return null
    }
  }

  const handleTerminateSession = (sessionId: string) => {
    // In a real app, this would call an API to terminate the session
    setSessions(prevSessions => prevSessions.map(session => 
      session.id === sessionId ? { ...session, isActive: false } : session
    ))
  }

  const handleTerminateAllSessions = (exceptCurrentSession = true) => {
    // In a real app, this would call an API to terminate all sessions except current
    setSessions(prevSessions => prevSessions.map(session => {
      if (exceptCurrentSession && session.id === 's1') {
        return session // Keep current session
      }
      return { ...session, isActive: false }
    }))
  }

  const handleRevokeDevice = (deviceId: string) => {
    // In a real app, this would call an API to revoke device access
    setDevices(prevDevices => prevDevices.map(device => 
      device.id === deviceId ? { ...device, isTrusted: false } : device
    ))
    
    // Also terminate any active sessions for this device
    setSessions(prevSessions => prevSessions.map(session => 
      session.deviceId === deviceId ? { ...session, isActive: false } : session
    ))
  }

  const handleTrustDevice = (deviceId: string) => {
    // In a real app, this would call an API to trust a device
    setDevices(prevDevices => prevDevices.map(device => 
      device.id === deviceId ? { ...device, isTrusted: true } : device
    ))
  }

  // Filtering functions
  const filteredDevices = devices.filter(device => {
    const matchesSearch = 
      device.name.toLowerCase().includes(searchTerm.toLowerCase()) ||
      device.browser.toLowerCase().includes(searchTerm.toLowerCase()) ||
      device.os.toLowerCase().includes(searchTerm.toLowerCase()) ||
      device.location?.city.toLowerCase().includes(searchTerm.toLowerCase()) ||
      device.location?.country.toLowerCase().includes(searchTerm.toLowerCase())
    
    if (deviceFilter === 'all') return matchesSearch
    if (deviceFilter === 'trusted') return matchesSearch && device.isTrusted
    if (deviceFilter === 'untrusted') return matchesSearch && !device.isTrusted
    if (deviceFilter === 'active') {
      const hasActiveSession = sessions.some(s => s.deviceId === device.id && s.isActive)
      return matchesSearch && hasActiveSession
    }
    return matchesSearch
  })
  
  const filteredSessions = sessions.filter(session => {
    if (sessionFilter === 'all') return true
    if (sessionFilter === 'active') return session.isActive
    if (sessionFilter === 'expired') return !session.isActive
    return true
  })

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-2">
          <Laptop className="h-6 w-6" />
          <div>
            <h2 className="text-2xl font-bold">Device & Session Management</h2>
            <p className="text-muted-foreground">
              Manage your devices, active sessions, and security
            </p>
          </div>
        </div>
        
        <div className="flex items-center gap-2">
          <Button 
            variant="outline" 
            size="sm"
            onClick={() => handleTerminateAllSessions()}
          >
            <LogOut className="h-4 w-4 mr-2" />
            Terminate All Other Sessions
          </Button>
        </div>
      </div>

      {/* Suspicious Activity Alert */}
      {suspiciousActivity.length > 0 && (
        <Alert className="bg-amber-50 border-amber-200">
          <AlertTriangle className="h-5 w-5 text-amber-600" />
          <AlertDescription className="flex items-center justify-between">
            <span>
              <span className="font-medium">Suspicious activity detected.</span> We noticed unusual login patterns on some of your devices.
            </span>
            <Button variant="outline" size="sm" className="ml-2">
              Review Activity
            </Button>
          </AlertDescription>
        </Alert>
      )}

      {/* Tabs and Content */}
      <Tabs value={activeTab} onValueChange={setActiveTab}>
        <TabsList className="grid grid-cols-2 w-full max-w-md">
          <TabsTrigger value="devices">Devices</TabsTrigger>
          <TabsTrigger value="sessions">Sessions</TabsTrigger>
        </TabsList>
        
        {/* Devices Tab */}
        <TabsContent value="devices">
          <Card>
            <CardHeader>
              <div className="flex items-center justify-between">
                <CardTitle className="flex items-center gap-2">
                  <Monitor className="h-5 w-5" />
                  Your Devices
                </CardTitle>
                <div className="flex items-center gap-2">
                  <div className="relative">
                    <Search className="absolute left-2 top-1/2 transform -translate-y-1/2 h-4 w-4 text-muted-foreground" />
                    <Input
                      placeholder="Search devices..."
                      className="pl-8 w-60"
                      value={searchTerm}
                      onChange={(e) => setSearchTerm(e.target.value)}
                    />
                  </div>
                  <Select value={deviceFilter} onValueChange={setDeviceFilter}>
                    <SelectTrigger className="w-36">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="all">All Devices</SelectItem>
                      <SelectItem value="trusted">Trusted</SelectItem>
                      <SelectItem value="untrusted">Untrusted</SelectItem>
                      <SelectItem value="active">Active Now</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
              </div>
              <CardDescription>
                Devices that have been used to access your account
              </CardDescription>
            </CardHeader>
            <CardContent>
              <div className="space-y-4">
                {filteredDevices.length === 0 ? (
                  <div className="text-center py-10 text-muted-foreground">
                    <Monitor className="h-10 w-10 mx-auto mb-2 text-muted-foreground/50" />
                    <p>No devices match your filters</p>
                  </div>
                ) : (
                  filteredDevices.map((device) => {
                    const hasActiveSession = sessions.some(s => s.deviceId === device.id && s.isActive)
                    return (
                      <div 
                        key={device.id}
                        className="border rounded-lg p-4 hover:border-primary/50 transition-colors cursor-pointer"
                        onClick={() => {
                          setSelectedDevice(device)
                          setShowDeviceDetails(true)
                        }}
                      >
                        <div className="flex items-start justify-between">
                          <div className="flex items-start gap-3">
                            <div className={`p-2 rounded-full ${hasActiveSession ? 'bg-green-50' : 'bg-gray-50'}`}>
                              {getDeviceIcon(device.type)}
                            </div>
                            <div>
                              <div className="flex items-center gap-2">
                                <h3 className="font-medium">{device.name}</h3>
                                {device.isTrusted && (
                                  <Badge variant="secondary" className="text-xs">Trusted</Badge>
                                )}
                                {hasActiveSession && (
                                  <Badge variant="outline" className="text-green-600 border-green-200 text-xs">Active Now</Badge>
                                )}
                              </div>
                              
                              <div className="text-sm text-muted-foreground mt-1">
                                <p>{device.browser} • {device.os}</p>
                                <div className="flex items-center gap-4 mt-1">
                                  <div className="flex items-center gap-1">
                                    <Clock className="h-3.5 w-3.5" />
                                    <span>{formatTimeAgo(device.lastActive)}</span>
                                  </div>
                                  
                                  {device.location && (
                                    <div className="flex items-center gap-1">
                                      <MapPin className="h-3.5 w-3.5" />
                                      <span>{device.location.city}, {device.location.country}</span>
                                    </div>
                                  )}
                                </div>
                              </div>
                            </div>
                          </div>
                          
                          <div className="flex flex-col items-end gap-2">
                            {formatRiskLevel(device.riskLevel)}
                            <Button 
                              variant="ghost" 
                              size="sm"
                              className="h-7"
                              onClick={(e) => {
                                e.stopPropagation() 
                                if (device.isTrusted) {
                                  handleRevokeDevice(device.id)
                                } else {
                                  handleTrustDevice(device.id)
                                }
                              }}
                            >
                              {device.isTrusted ? (
                                <>
                                  <XCircle className="h-3.5 w-3.5 mr-1" />
                                  Revoke
                                </>
                              ) : (
                                <>
                                  <CheckCircle className="h-3.5 w-3.5 mr-1" />
                                  Trust
                                </>
                              )}
                            </Button>
                          </div>
                        </div>
                      </div>
                    )
                  })
                )}
              </div>
            </CardContent>
          </Card>
        </TabsContent>
        
        {/* Sessions Tab */}
        <TabsContent value="sessions">
          <Card>
            <CardHeader>
              <div className="flex items-center justify-between">
                <CardTitle className="flex items-center gap-2">
                  <RefreshCw className="h-5 w-5" />
                  Active Sessions
                </CardTitle>
                <div className="flex items-center gap-2">
                  <Select value={sessionFilter} onValueChange={setSessionFilter}>
                    <SelectTrigger className="w-36">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="all">All Sessions</SelectItem>
                      <SelectItem value="active">Active</SelectItem>
                      <SelectItem value="expired">Expired</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
              </div>
              <CardDescription>
                Current and recent login sessions for your account
              </CardDescription>
            </CardHeader>
            <CardContent>
              <div className="space-y-4">
                {filteredSessions.length === 0 ? (
                  <div className="text-center py-10 text-muted-foreground">
                    <Clock className="h-10 w-10 mx-auto mb-2 text-muted-foreground/50" />
                    <p>No sessions match your filters</p>
                  </div>
                ) : (
                  filteredSessions.map((session) => {
                    const device = devices.find(d => d.id === session.deviceId)
                    const isCurrentSession = session.id === 's1'
                    return (
                      <div key={session.id} className="border rounded-lg p-4">
                        <div className="flex items-start justify-between">
                          <div className="flex items-start gap-3">
                            <div className={`p-2 rounded-full ${session.isActive ? 'bg-green-50' : 'bg-gray-50'}`}>
                              {device ? getDeviceIcon(device.type) : <Globe className="h-5 w-5" />}
                            </div>
                            <div>
                              <div className="flex items-center gap-2">
                                <h3 className="font-medium">
                                  {device ? device.name : 'Unknown Device'}
                                </h3>
                                {session.isActive && (
                                  <Badge variant="outline" className="text-green-600 border-green-200 text-xs">
                                    Active
                                  </Badge>
                                )}
                                {isCurrentSession && (
                                  <Badge variant="secondary" className="text-xs">Current Session</Badge>
                                )}
                              </div>
                              
                              <div className="text-sm text-muted-foreground mt-1">
                                <div className="flex items-center gap-4">
                                  <div className="flex items-center gap-1">
                                    <Clock className="h-3.5 w-3.5" />
                                    <span>
                                      {session.isActive 
                                        ? `Active ${formatTimeAgo(session.startTime)}` 
                                        : `Ended ${formatTimeAgo(session.lastActivity)}`
                                      }
                                    </span>
                                  </div>
                                  
                                  {session.location && (
                                    <div className="flex items-center gap-1">
                                      <MapPin className="h-3.5 w-3.5" />
                                      <span>{session.location.city}, {session.location.country}</span>
                                    </div>
                                  )}
                                </div>
                                
                                <div className="flex items-center gap-1 mt-1">
                                  <Globe className="h-3.5 w-3.5" />
                                  <span>{session.ipAddress}</span>
                                </div>
                              </div>
                            </div>
                          </div>
                          
                          <div>
                            {session.isActive && !isCurrentSession && (
                              <Button 
                                variant="outline" 
                                size="sm"
                                className="text-red-600 hover:text-red-700 h-7"
                                onClick={() => handleTerminateSession(session.id)}
                              >
                                <LogOut className="h-3.5 w-3.5 mr-1" />
                                Terminate
                              </Button>
                            )}
                            {isCurrentSession && (
                              <Button 
                                variant="outline" 
                                size="sm"
                                className="h-7"
                                disabled
                              >
                                <CheckCircle className="h-3.5 w-3.5 mr-1" />
                                Current
                              </Button>
                            )}
                          </div>
                        </div>
                      </div>
                    )
                  })
                )}
              </div>
            </CardContent>
            <CardFooter className="flex justify-between">
              <p className="text-sm text-muted-foreground">
                Sessions automatically expire after 90 minutes of inactivity
              </p>
            </CardFooter>
          </Card>
        </TabsContent>
      </Tabs>

      {/* Device Details Dialog */}
      <Dialog open={showDeviceDetails} onOpenChange={setShowDeviceDetails}>
        <DialogContent className="max-w-lg">
          {selectedDevice && (
            <>
              <DialogHeader>
                <DialogTitle className="flex items-center gap-2">
                  {getDeviceIcon(selectedDevice.type)}
                  {selectedDevice.name}
                </DialogTitle>
                <DialogDescription>
                  Device details and security information
                </DialogDescription>
              </DialogHeader>

              <div className="space-y-6 py-2">
                <div className="grid grid-cols-2 gap-4">
                  <div>
                    <Label className="text-xs text-muted-foreground">DEVICE TYPE</Label>
                    <p className="capitalize">{selectedDevice.type}</p>
                  </div>
                  <div>
                    <Label className="text-xs text-muted-foreground">LAST ACTIVE</Label>
                    <p>{formatTimeAgo(selectedDevice.lastActive)}</p>
                  </div>
                  <div>
                    <Label className="text-xs text-muted-foreground">BROWSER</Label>
                    <p>{selectedDevice.browser}</p>
                  </div>
                  <div>
                    <Label className="text-xs text-muted-foreground">OPERATING SYSTEM</Label>
                    <p>{selectedDevice.os}</p>
                  </div>
                  <div>
                    <Label className="text-xs text-muted-foreground">IP ADDRESS</Label>
                    <p>{selectedDevice.location?.ip || 'Unknown'}</p>
                  </div>
                  <div>
                    <Label className="text-xs text-muted-foreground">TRUST STATUS</Label>
                    <p>
                      {selectedDevice.isTrusted ? (
                        <span className="flex items-center text-green-600 gap-1">
                          <CheckCircle className="h-3.5 w-3.5" /> Trusted
                        </span>
                      ) : (
                        <span className="flex items-center text-amber-600 gap-1">
                          <AlertTriangle className="h-3.5 w-3.5" /> Not Trusted
                        </span>
                      )}
                    </p>
                  </div>
                </div>

                {selectedDevice.location && (
                  <div>
                    <Label className="text-xs text-muted-foreground">LOCATION</Label>
                    <p className="font-medium">{selectedDevice.location.city}, {selectedDevice.location.country}</p>
                    {selectedDevice.location.coordinates && (
                      <p className="text-sm text-muted-foreground">{selectedDevice.location.coordinates}</p>
                    )}
                  </div>
                )}

                <div>
                  <Label className="text-xs text-muted-foreground">RISK ASSESSMENT</Label>
                  <div className="flex items-center gap-2 mt-1">
                    {formatRiskLevel(selectedDevice.riskLevel)}
                    <span className="text-sm">
                      {selectedDevice.riskLevel === 'low' && 'Normal usage patterns detected'}
                      {selectedDevice.riskLevel === 'medium' && 'Some unusual activities detected'}
                      {selectedDevice.riskLevel === 'high' && 'Suspicious login location or behavior'}
                    </span>
                  </div>
                </div>

                {/* Recent Sessions on this device */}
                <div>
                  <Label className="text-xs text-muted-foreground">RECENT SESSIONS</Label>
                  <div className="space-y-2 mt-1">
                    {sessions
                      .filter(s => s.deviceId === selectedDevice.id)
                      .slice(0, 3)
                      .map((session) => (
                        <div key={session.id} className="flex items-center justify-between border rounded-md p-2 text-sm">
                          <div className="flex items-center gap-2">
                            <Clock className="h-4 w-4" />
                            <span>
                              {new Date(session.startTime).toLocaleString()} 
                              {session.isActive ? ' (Active)' : ''}
                            </span>
                          </div>
                          <Badge variant={session.isActive ? "outline" : "secondary"} className="text-xs">
                            {session.isActive ? 'Active' : 'Ended'}
                          </Badge>
                        </div>
                      ))
                    }
                  </div>
                </div>

                {/* Suspicious activity on this device */}
                {suspiciousActivity.filter(a => a.deviceId === selectedDevice.id).length > 0 && (
                  <div>
                    <Label className="text-xs text-muted-foreground">SUSPICIOUS ACTIVITY</Label>
                    <div className="space-y-2 mt-1">
                      {suspiciousActivity
                        .filter(a => a.deviceId === selectedDevice.id)
                        .map((activity) => (
                          <div key={activity.id} className="flex items-center justify-between border border-amber-200 bg-amber-50 rounded-md p-2 text-sm">
                            <div className="flex items-center gap-2">
                              <AlertTriangle className="h-4 w-4 text-amber-600" />
                              <span>{activity.description}</span>
                            </div>
                            <Badge variant="outline" className="text-xs capitalize">{activity.severity}</Badge>
                          </div>
                        ))
                      }
                    </div>
                  </div>
                )}
              </div>

              <DialogFooter>
                {selectedDevice.isTrusted ? (
                  <Button 
                    variant="outline" 
                    className="border-red-200 text-red-600 hover:bg-red-50"
                    onClick={() => {
                      handleRevokeDevice(selectedDevice.id)
                      setShowDeviceDetails(false)
                    }}
                  >
                    <Lock className="h-4 w-4 mr-2" />
                    Revoke Device
                  </Button>
                ) : (
                  <Button 
                    variant="outline"
                    className="border-green-200 text-green-600 hover:bg-green-50"
                    onClick={() => {
                      handleTrustDevice(selectedDevice.id)
                      setShowDeviceDetails(false)
                    }}
                  >
                    <Unlock className="h-4 w-4 mr-2" />
                    Trust Device
                  </Button>
                )}

                <Button 
                  variant="destructive"
                  onClick={() => {
                    handleTerminateAllSessions()
                    setShowDeviceDetails(false)
                  }}
                >
                  <LogOut className="h-4 w-4 mr-2" />
                  Terminate All Sessions
                </Button>
              </DialogFooter>
            </>
          )}
        </DialogContent>
      </Dialog>
    </div>
  )
}