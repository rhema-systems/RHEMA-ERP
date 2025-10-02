"use client"

import React, { useState, useEffect } from 'react'
import { Card, CardContent, CardHeader, CardTitle, CardDescription, CardFooter } from '../ui/card'
import { Button } from '../ui/button'
import { Badge } from '../ui/badge'
import { Input } from '../ui/input'
import { Label } from '../ui/label'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '../ui/select'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '../ui/tabs'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger } from '../ui/dialog'
import { Alert, AlertDescription } from '../ui/alert'
import { useToast } from '../../hooks/use-toast'
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
  RefreshCw,
  Loader2
} from 'lucide-react'
import { deviceService, DeviceSession, SuspiciousActivity, MyDevicesResponse } from '../../lib/api/deviceService'

export function DeviceManagement() {
  const { toast } = useToast()
  
  // State for API data
  const [deviceData, setDeviceData] = useState<MyDevicesResponse | null>(null)
  const [suspiciousActivity, setSuspiciousActivity] = useState<SuspiciousActivity[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  
  // UI state
  const [activeTab, setActiveTab] = useState('devices')
  const [searchTerm, setSearchTerm] = useState('')
  const [deviceFilter, setDeviceFilter] = useState('all')
  const [sessionFilter, setSessionFilter] = useState('active')
  const [selectedDevice, setSelectedDevice] = useState<DeviceSession | null>(null)
  const [showDeviceDetails, setShowDeviceDetails] = useState(false)
  const [actionLoading, setActionLoading] = useState<string | null>(null)
  
  // Load initial data
  useEffect(() => {
    loadDeviceData()
    loadSuspiciousActivity()
  }, [])

  const loadDeviceData = async () => {
    try {
      setLoading(true)
      setError(null)
      const data = await deviceService.getMyDevices()
      setDeviceData(data)
    } catch (error) {
      console.error('Failed to load device data:', error)
      setError('Failed to load device information')
      toast({
        title: "Error",
        description: "Failed to load device information. Please try again.",
        variant: "destructive"
      })
    } finally {
      setLoading(false)
    }
  }

  const loadSuspiciousActivity = async () => {
    try {
      const activity = await deviceService.getSuspiciousActivity()
      setSuspiciousActivity(activity)
    } catch (error) {
      console.error('Failed to load suspicious activity:', error)
      // Don't show error for suspicious activity as it's not critical
    }
  }

  const getDeviceIcon = (type: string) => {
    const deviceType = type.toLowerCase()
    switch (deviceType) {
      case 'mobile': return <Smartphone className="h-5 w-5" />
      case 'tablet': return <Tablet className="h-5 w-5" />
      case 'laptop': return <Laptop className="h-5 w-5" />
      case 'desktop': return <Monitor className="h-5 w-5" />
      default: return <Monitor className="h-5 w-5" />
    }
  }

  const formatTimeAgo = (dateString: string) => {
    const date = new Date(dateString)
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

  const formatRiskLevel = (riskScore: number) => {
    if (riskScore < 30) {
      return (
        <Badge variant="outline" className="text-green-600 border-green-200 flex items-center gap-1">
          <ShieldCheck className="h-3 w-3" /> Low Risk
        </Badge>
      )
    } else if (riskScore < 60) {
      return (
        <Badge variant="outline" className="text-yellow-600 border-yellow-200 flex items-center gap-1">
          <AlertTriangle className="h-3 w-3" /> Medium Risk
        </Badge>
      )
    } else {
      return (
        <Badge variant="outline" className="text-red-600 border-red-200 flex items-center gap-1">
          <ShieldX className="h-3 w-3" /> High Risk
        </Badge>
      )
    }
  }

  const handleTerminateSession = async (sessionId: string) => {
    try {
      setActionLoading(sessionId)
      await deviceService.terminateSession(sessionId, 'Terminated by user')
      await loadDeviceData() // Refresh data
      toast({
        title: "Success",
        description: "Session terminated successfully",
      })
    } catch (error) {
      console.error('Failed to terminate session:', error)
      toast({
        title: "Error",
        description: "Failed to terminate session. Please try again.",
        variant: "destructive"
      })
    } finally {
      setActionLoading(null)
    }
  }

  const handleTerminateAllSessions = async () => {
    try {
      setActionLoading('terminate-all')
      const result = await deviceService.terminateAllOtherSessions('All other sessions terminated by user')
      await loadDeviceData() // Refresh data
      toast({
        title: "Success",
        description: `Successfully terminated ${result.terminatedCount} other sessions`,
      })
    } catch (error) {
      console.error('Failed to terminate all sessions:', error)
      toast({
        title: "Error",
        description: "Failed to terminate other sessions. Please try again.",
        variant: "destructive"
      })
    } finally {
      setActionLoading(null)
    }
  }

  const handleTrustDevice = async (deviceId: string, isTrusted: boolean) => {
    try {
      setActionLoading(deviceId)
      await deviceService.updateDeviceTrust(deviceId, isTrusted)
      // In a real implementation, you'd refresh the data to show updated trust status
      toast({
        title: "Success",
        description: `Device ${isTrusted ? 'trusted' : 'untrusted'} successfully`,
      })
    } catch (error) {
      console.error('Failed to update device trust:', error)
      toast({
        title: "Error",
        description: "Failed to update device trust. Please try again.",
        variant: "destructive"
      })
    } finally {
      setActionLoading(null)
    }
  }

  // Create devices list from sessions (group by device fingerprint or similar identifier)
  const getDevicesFromSessions = () => {
    if (!deviceData) return []
    
    const allSessions = [...deviceData.activeSessions, ...deviceData.recentSessions]
    const deviceMap = new Map<string, DeviceSession>()
    
    // Group sessions by device info to create unique devices
    allSessions.forEach(session => {
      const deviceKey = `${session.deviceInfo.type}-${session.deviceInfo.browser}-${session.deviceInfo.os}-${session.location.ip}`
      if (!deviceMap.has(deviceKey) || session.session.isActive) {
        deviceMap.set(deviceKey, session)
      }
    })
    
    return Array.from(deviceMap.values())
  }

  const filteredDevices = getDevicesFromSessions().filter(device => {
    const matchesSearch = 
      device.deviceInfo.browser.toLowerCase().includes(searchTerm.toLowerCase()) ||
      device.deviceInfo.os.toLowerCase().includes(searchTerm.toLowerCase()) ||
      device.location.city.toLowerCase().includes(searchTerm.toLowerCase()) ||
      device.location.country.toLowerCase().includes(searchTerm.toLowerCase())
    
    if (deviceFilter === 'all') return matchesSearch
    if (deviceFilter === 'trusted') return matchesSearch && device.security.isTrusted
    if (deviceFilter === 'untrusted') return matchesSearch && !device.security.isTrusted
    if (deviceFilter === 'active') return matchesSearch && device.session.isActive
    return matchesSearch
  })

  const filteredSessions = deviceData ? deviceData.activeSessions.concat(deviceData.recentSessions).filter(session => {
    if (sessionFilter === 'all') return true
    if (sessionFilter === 'active') return session.session.isActive
    if (sessionFilter === 'expired') return !session.session.isActive
    return true
  }) : []

  if (loading && !deviceData) {
    return (
      <div className="flex items-center justify-center p-8">
        <Loader2 className="h-8 w-8 animate-spin" />
        <span className="ml-2">Loading device information...</span>
      </div>
    )
  }

  if (error && !deviceData) {
    return (
      <div className="space-y-6">
        <Alert className="bg-red-50 border-red-200">
          <AlertTriangle className="h-5 w-5 text-red-600" />
          <AlertDescription>
            <span className="font-medium">Error loading device information.</span> {error}
            <Button 
              variant="outline" 
              size="sm" 
              className="ml-2"
              onClick={loadDeviceData}
            >
              Retry
            </Button>
          </AlertDescription>
        </Alert>
      </div>
    )
  }

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
            onClick={handleTerminateAllSessions}
            disabled={actionLoading === 'terminate-all'}
          >
            {actionLoading === 'terminate-all' ? (
              <Loader2 className="h-4 w-4 animate-spin mr-2" />
            ) : (
              <LogOut className="h-4 w-4 mr-2" />
            )}
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
                  filteredDevices.map((device) => (
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
                          <div className={`p-2 rounded-full ${device.session.isActive ? 'bg-green-50' : 'bg-gray-50'}`}>
                            {getDeviceIcon(device.deviceInfo.type)}
                          </div>
                          <div>
                            <div className="flex items-center gap-2">
                              <h3 className="font-medium">{device.deviceInfo.browser} on {device.deviceInfo.os}</h3>
                              {device.security.isTrusted && (
                                <Badge variant="secondary" className="text-xs">Trusted</Badge>
                              )}
                              {device.session.isActive && (
                                <Badge variant="outline" className="text-green-600 border-green-200 text-xs">Active Now</Badge>
                              )}
                            </div>
                            
                            <div className="text-sm text-muted-foreground mt-1">
                              <p>{device.deviceInfo.type} • {device.deviceInfo.browser}</p>
                              <div className="flex items-center gap-4 mt-1">
                                <div className="flex items-center gap-1">
                                  <Clock className="h-3.5 w-3.5" />
                                  <span>{formatTimeAgo(device.session.lastActivity)}</span>
                                </div>
                                
                                <div className="flex items-center gap-1">
                                  <MapPin className="h-3.5 w-3.5" />
                                  <span>{device.location.city}, {device.location.country}</span>
                                </div>
                              </div>
                            </div>
                          </div>
                        </div>
                        
                        <div className="flex flex-col items-end gap-2">
                          {formatRiskLevel(device.security.riskScore)}
                          <Button 
                            variant="ghost" 
                            size="sm"
                            className="h-7"
                            disabled={actionLoading === device.id}
                            onClick={(e) => {
                              e.stopPropagation()
                              handleTrustDevice(device.id, !device.security.isTrusted)
                            }}
                          >
                            {actionLoading === device.id ? (
                              <Loader2 className="h-3.5 w-3.5 animate-spin mr-1" />
                            ) : device.security.isTrusted ? (
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
                  ))
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
                    const isCurrentSession = session.session.sessionId === 'current' // Simplified check
                    return (
                      <div key={session.id} className="border rounded-lg p-4">
                        <div className="flex items-start justify-between">
                          <div className="flex items-start gap-3">
                            <div className={`p-2 rounded-full ${session.session.isActive ? 'bg-green-50' : 'bg-gray-50'}`}>
                              {getDeviceIcon(session.deviceInfo.type)}
                            </div>
                            <div>
                              <div className="flex items-center gap-2">
                                <h3 className="font-medium">
                                  {session.deviceInfo.browser} on {session.deviceInfo.os}
                                </h3>
                                {session.session.isActive && (
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
                                      {session.session.isActive 
                                        ? `Active ${formatTimeAgo(session.session.startTime)}` 
                                        : `Ended ${formatTimeAgo(session.session.lastActivity)}`
                                      }
                                    </span>
                                  </div>
                                  
                                  <div className="flex items-center gap-1">
                                    <MapPin className="h-3.5 w-3.5" />
                                    <span>{session.location.city}, {session.location.country}</span>
                                  </div>
                                </div>
                                
                                <div className="flex items-center gap-1 mt-1">
                                  <Globe className="h-3.5 w-3.5" />
                                  <span>{session.location.ip}</span>
                                </div>
                              </div>
                            </div>
                          </div>
                          
                          <div>
                            {session.session.isActive && !isCurrentSession && (
                              <Button 
                                variant="outline" 
                                size="sm"
                                className="text-red-600 hover:text-red-700 h-7"
                                disabled={actionLoading === session.id}
                                onClick={() => handleTerminateSession(session.session.sessionId)}
                              >
                                {actionLoading === session.id ? (
                                  <Loader2 className="h-3.5 w-3.5 animate-spin mr-1" />
                                ) : (
                                  <LogOut className="h-3.5 w-3.5 mr-1" />
                                )}
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
                  {getDeviceIcon(selectedDevice.deviceInfo.type)}
                  {selectedDevice.deviceInfo.browser} on {selectedDevice.deviceInfo.os}
                </DialogTitle>
                <DialogDescription>
                  Device details and security information
                </DialogDescription>
              </DialogHeader>

              <div className="space-y-6 py-2">
                <div className="grid grid-cols-2 gap-4">
                  <div>
                    <Label className="text-xs text-muted-foreground">DEVICE TYPE</Label>
                    <p className="capitalize">{selectedDevice.deviceInfo.type}</p>
                  </div>
                  <div>
                    <Label className="text-xs text-muted-foreground">LAST ACTIVE</Label>
                    <p>{formatTimeAgo(selectedDevice.session.lastActivity)}</p>
                  </div>
                  <div>
                    <Label className="text-xs text-muted-foreground">BROWSER</Label>
                    <p>{selectedDevice.deviceInfo.browser}</p>
                  </div>
                  <div>
                    <Label className="text-xs text-muted-foreground">OPERATING SYSTEM</Label>
                    <p>{selectedDevice.deviceInfo.os}</p>
                  </div>
                  <div>
                    <Label className="text-xs text-muted-foreground">IP ADDRESS</Label>
                    <p>{selectedDevice.location.ip}</p>
                  </div>
                  <div>
                    <Label className="text-xs text-muted-foreground">TRUST STATUS</Label>
                    <p>
                      {selectedDevice.security.isTrusted ? (
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

                <div>
                  <Label className="text-xs text-muted-foreground">LOCATION</Label>
                  <p className="font-medium">{selectedDevice.location.city}, {selectedDevice.location.country}</p>
                  {selectedDevice.location.coordinates && (
                    <p className="text-sm text-muted-foreground">
                      {selectedDevice.location.coordinates.latitude}°, {selectedDevice.location.coordinates.longitude}°
                    </p>
                  )}
                </div>

                <div>
                  <Label className="text-xs text-muted-foreground">RISK ASSESSMENT</Label>
                  <div className="flex items-center gap-2 mt-1">
                    {formatRiskLevel(selectedDevice.security.riskScore)}
                    <span className="text-sm">
                      {selectedDevice.security.riskScore < 30 && 'Normal usage patterns detected'}
                      {selectedDevice.security.riskScore >= 30 && selectedDevice.security.riskScore < 60 && 'Some unusual activities detected'}
                      {selectedDevice.security.riskScore >= 60 && 'Suspicious login location or behavior'}
                    </span>
                  </div>
                </div>
              </div>

              <DialogFooter>
                {selectedDevice.security.isTrusted ? (
                  <Button 
                    variant="outline" 
                    className="border-red-200 text-red-600 hover:bg-red-50"
                    disabled={actionLoading === selectedDevice.id}
                    onClick={() => {
                      handleTrustDevice(selectedDevice.id, false)
                      setShowDeviceDetails(false)
                    }}
                  >
                    {actionLoading === selectedDevice.id ? (
                      <Loader2 className="h-4 w-4 animate-spin mr-2" />
                    ) : (
                      <Lock className="h-4 w-4 mr-2" />
                    )}
                    Revoke Device
                  </Button>
                ) : (
                  <Button 
                    variant="outline"
                    className="border-green-200 text-green-600 hover:bg-green-50"
                    disabled={actionLoading === selectedDevice.id}
                    onClick={() => {
                      handleTrustDevice(selectedDevice.id, true)
                      setShowDeviceDetails(false)
                    }}
                  >
                    {actionLoading === selectedDevice.id ? (
                      <Loader2 className="h-4 w-4 animate-spin mr-2" />
                    ) : (
                      <Unlock className="h-4 w-4 mr-2" />
                    )}
                    Trust Device
                  </Button>
                )}

                <Button 
                  variant="destructive"
                  disabled={actionLoading === 'terminate-all'}
                  onClick={() => {
                    handleTerminateAllSessions()
                    setShowDeviceDetails(false)
                  }}
                >
                  {actionLoading === 'terminate-all' ? (
                    <Loader2 className="h-4 w-4 animate-spin mr-2" />
                  ) : (
                    <LogOut className="h-4 w-4 mr-2" />
                  )}
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