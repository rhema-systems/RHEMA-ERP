"use client"

import React, { useState, useEffect } from 'react'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '../ui/card'
import { Button } from '../ui/button'
import { Badge } from '../ui/badge'
import { Input } from '../ui/input'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '../ui/select'
import { 
  Activity, 
  AlertTriangle, 
  Trash2, 
  RefreshCw, 
  Download,
  TrendingUp,
  Clock,
  CheckCircle,
  AlertCircle,
  Zap,
  Play,
  Filter,
  BarChart3
} from 'lucide-react'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '../ui/table'
import { useToast } from '../ui/use-toast'

interface NotificationMetrics {
  Status: string
  Timestamp: string
  Metrics: {
    PendingNotifications: number
    SentNotifications: number
    DeadLetters: number
    CriticalIssues: number
    HighPriority: number
  }
  Details: Record<string, number>
}

interface Notification {
  id: string
  title: string
  message: string
  status: string
  priority: string
  entityType: string
  attemptCount: number
  lastError?: string
  createdDate: string
  scheduledFor: string
}

const NotificationMonitoring: React.FC = () => {
  const [metrics, setMetrics] = useState<NotificationMetrics | null>(null)
  const [pendingNotifications, setPendingNotifications] = useState<Notification[]>([])
  const [deadLetters, setDeadLetters] = useState<Notification[]>([])
  const [loading, setLoading] = useState(true)
  const [selectedTab, setSelectedTab] = useState<'health' | 'pending' | 'dead-letters'>('health')
  const [selectedFilter, setSelectedFilter] = useState('all')
  const { toast } = useToast()

  useEffect(() => {
    loadMetrics()
    loadPendingNotifications()
    loadDeadLetters()
    
    // Refresh every 30 seconds
    const interval = setInterval(() => {
      loadMetrics()
      if (selectedTab === 'pending') loadPendingNotifications()
      if (selectedTab === 'dead-letters') loadDeadLetters()
    }, 30000)

    return () => clearInterval(interval)
  }, [selectedTab])

  const loadMetrics = async () => {
    try {
      const response = await fetch('/api/admin/notifications/health', {
        headers: { 'Authorization': `Bearer ${localStorage.getItem('token')}` }
      })
      const data = await response.json()
      setMetrics(data)
    } catch (error) {
      console.error('Failed to load metrics:', error)
    }
  }

  const loadPendingNotifications = async () => {
    try {
      const response = await fetch('/api/admin/notifications/pending?pageSize=50', {
        headers: { 'Authorization': `Bearer ${localStorage.getItem('token')}` }
      })
      const data = await response.json()
      setPendingNotifications(data.items)
    } catch (error) {
      console.error('Failed to load pending:', error)
    } finally {
      setLoading(false)
    }
  }

  const loadDeadLetters = async () => {
    try {
      const response = await fetch('/api/admin/notifications/dead-letters?pageSize=50', {
        headers: { 'Authorization': `Bearer ${localStorage.getItem('token')}` }
      })
      const data = await response.json()
      setDeadLetters(data.items)
    } catch (error) {
      console.error('Failed to load dead letters:', error)
    } finally {
      setLoading(false)
    }
  }

  const retryDeadLetter = async (notificationId: string) => {
    try {
      const response = await fetch(`/api/admin/notifications/dead-letters/${notificationId}/retry`, {
        method: 'POST',
        headers: { 'Authorization': `Bearer ${localStorage.getItem('token')}` }
      })
      if (response.ok) {
        toast({ description: 'Notification moved to pending queue' })
        loadDeadLetters()
        loadMetrics()
      }
    } catch (error) {
      toast({ description: 'Failed to retry notification', variant: 'destructive' })
    }
  }

  const retryAllDeadLetters = async () => {
    try {
      const response = await fetch('/api/admin/notifications/dead-letters/retry-all', {
        method: 'POST',
        headers: { 'Authorization': `Bearer ${localStorage.getItem('token')}` }
      })
      if (response.ok) {
        const data = await response.json()
        toast({ description: `Moved ${data.retryCount} notifications to pending queue` })
        loadDeadLetters()
        loadMetrics()
      }
    } catch (error) {
      toast({ description: 'Failed to retry notifications', variant: 'destructive' })
    }
  }

  const deleteDeadLetter = async (notificationId: string) => {
    if (!confirm('Are you sure you want to permanently delete this notification?')) return
    
    try {
      const response = await fetch(`/api/admin/notifications/dead-letters/${notificationId}`, {
        method: 'DELETE',
        headers: { 'Authorization': `Bearer ${localStorage.getItem('token')}` }
      })
      if (response.ok) {
        toast({ description: 'Notification deleted' })
        loadDeadLetters()
        loadMetrics()
      }
    } catch (error) {
      toast({ description: 'Failed to delete notification', variant: 'destructive' })
    }
  }

  const evaluateEscalations = async () => {
    try {
      const response = await fetch('/api/admin/notifications/escalation-rules/evaluate', {
        method: 'POST',
        headers: { 'Authorization': `Bearer ${localStorage.getItem('token')}` }
      })
      if (response.ok) {
        toast({ description: 'Escalation evaluation triggered' })
        loadMetrics()
      }
    } catch (error) {
      toast({ description: 'Failed to evaluate escalations', variant: 'destructive' })
    }
  }

  const getPriorityColor = (priority: string) => {
    switch (priority.toLowerCase()) {
      case 'critical': return 'destructive'
      case 'high': return 'secondary'
      case 'normal': return 'default'
      default: return 'outline'
    }
  }

  const getStatusColor = (status: string) => {
    switch (status.toLowerCase()) {
      case 'sent': return 'text-green-600'
      case 'pending': return 'text-yellow-600'
      case 'deadletter': return 'text-red-600'
      default: return 'text-gray-600'
    }
  }

  return (
    <div className="space-y-6">
      {/* Health Overview */}
      {metrics && (
        <div className="grid grid-cols-1 md:grid-cols-5 gap-4">
          <Card>
            <CardContent className="pt-6">
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-sm text-muted-foreground">Pending</p>
                  <p className="text-2xl font-bold">{metrics.Metrics.PendingNotifications}</p>
                </div>
                <Clock className="h-8 w-8 text-blue-500" />
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardContent className="pt-6">
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-sm text-muted-foreground">Sent</p>
                  <p className="text-2xl font-bold">{metrics.Metrics.SentNotifications}</p>
                </div>
                <CheckCircle className="h-8 w-8 text-green-500" />
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardContent className="pt-6">
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-sm text-muted-foreground">Dead Letters</p>
                  <p className="text-2xl font-bold">{metrics.Metrics.DeadLetters}</p>
                </div>
                <AlertTriangle className="h-8 w-8 text-red-500" />
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardContent className="pt-6">
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-sm text-muted-foreground">Critical</p>
                  <p className="text-2xl font-bold">{metrics.Metrics.CriticalIssues}</p>
                </div>
                <AlertCircle className="h-8 w-8 text-orange-500" />
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardContent className="pt-6">
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-sm text-muted-foreground">Status</p>
                  <Badge variant="outline" className="mt-2">{metrics.Status}</Badge>
                </div>
                <Activity className="h-8 w-8 text-green-500" />
              </div>
            </CardContent>
          </Card>
        </div>
      )}

      {/* Tabs */}
      <div className="flex gap-2 border-b">
        {(['health', 'pending', 'dead-letters'] as const).map(tab => (
          <button
            key={tab}
            onClick={() => setSelectedTab(tab)}
            className={`px-4 py-2 font-medium border-b-2 transition-colors ${
              selectedTab === tab
                ? 'border-primary text-primary'
                : 'border-transparent text-muted-foreground hover:text-foreground'
            }`}
          >
            {tab === 'health' && 'Health'}
            {tab === 'pending' && 'Pending Queue'}
            {tab === 'dead-letters' && 'Dead Letters'}
          </button>
        ))}
      </div>

      {/* Pending Queue Tab */}
      {selectedTab === 'pending' && (
        <Card>
          <CardHeader className="flex flex-row items-center justify-between">
            <div>
              <CardTitle>Pending Notifications Queue</CardTitle>
              <CardDescription>Notifications awaiting dispatch</CardDescription>
            </div>
            <Button onClick={loadPendingNotifications} size="sm" variant="outline">
              <RefreshCw className="h-4 w-4 mr-2" />
              Refresh
            </Button>
          </CardHeader>
          <CardContent>
            {loading ? (
              <div className="text-center py-8">Loading...</div>
            ) : (
              <div className="overflow-x-auto">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Title</TableHead>
                      <TableHead>Type</TableHead>
                      <TableHead>Priority</TableHead>
                      <TableHead>Attempts</TableHead>
                      <TableHead>Scheduled For</TableHead>
                      <TableHead>Created</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {pendingNotifications.map(notif => (
                      <TableRow key={notif.id}>
                        <TableCell className="font-medium">{notif.title}</TableCell>
                        <TableCell>{notif.entityType}</TableCell>
                        <TableCell>
                          <Badge variant={getPriorityColor(notif.priority)}>
                            {notif.priority}
                          </Badge>
                        </TableCell>
                        <TableCell>{notif.attemptCount}</TableCell>
                        <TableCell>{new Date(notif.scheduledFor).toLocaleString()}</TableCell>
                        <TableCell>{new Date(notif.createdDate).toLocaleString()}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </div>
            )}
          </CardContent>
        </Card>
      )}

      {/* Dead Letters Tab */}
      {selectedTab === 'dead-letters' && (
        <div className="space-y-4">
          <Card>
            <CardHeader className="flex flex-row items-center justify-between">
              <div>
                <CardTitle>Dead Letter Queue</CardTitle>
                <CardDescription>Failed notifications (exceeded max retries)</CardDescription>
              </div>
              <div className="flex gap-2">
                <Button onClick={retryAllDeadLetters} size="sm" variant="default">
                  <Play className="h-4 w-4 mr-2" />
                  Retry All
                </Button>
                <Button onClick={loadDeadLetters} size="sm" variant="outline">
                  <RefreshCw className="h-4 w-4 mr-2" />
                  Refresh
                </Button>
              </div>
            </CardHeader>
            <CardContent>
              {loading ? (
                <div className="text-center py-8">Loading...</div>
              ) : (
                <div className="overflow-x-auto">
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead>Title</TableHead>
                        <TableHead>Type</TableHead>
                        <TableHead>Priority</TableHead>
                        <TableHead>Last Error</TableHead>
                        <TableHead>Attempts</TableHead>
                        <TableHead>Actions</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {deadLetters.map(notif => (
                        <TableRow key={notif.id}>
                          <TableCell className="font-medium">{notif.title}</TableCell>
                          <TableCell>{notif.entityType}</TableCell>
                          <TableCell>
                            <Badge variant={getPriorityColor(notif.priority)}>
                              {notif.priority}
                            </Badge>
                          </TableCell>
                          <TableCell className="text-xs text-muted-foreground max-w-xs truncate">
                            {notif.lastError || 'N/A'}
                          </TableCell>
                          <TableCell>{notif.attemptCount}</TableCell>
                          <TableCell className="flex gap-2">
                            <Button
                              onClick={() => retryDeadLetter(notif.id)}
                              size="sm"
                              variant="outline"
                            >
                              <Play className="h-3 w-3" />
                            </Button>
                            <Button
                              onClick={() => deleteDeadLetter(notif.id)}
                              size="sm"
                              variant="destructive"
                            >
                              <Trash2 className="h-3 w-3" />
                            </Button>
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </div>
              )}
            </CardContent>
          </Card>
        </div>
      )}

      {/* Health Tab */}
      {selectedTab === 'health' && metrics && (
        <div className="space-y-4">
          <Card>
            <CardHeader>
              <CardTitle>System Configuration</CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div>
                  <p className="text-sm text-muted-foreground">Max Retry Attempts</p>
                  <p className="text-lg font-medium">5</p>
                </div>
                <div>
                  <p className="text-sm text-muted-foreground">Dispatch Interval</p>
                  <p className="text-lg font-medium">5 minutes</p>
                </div>
                <div>
                  <p className="text-sm text-muted-foreground">Backoff Multiplier</p>
                  <p className="text-lg font-medium">1.5x</p>
                </div>
                <div>
                  <p className="text-sm text-muted-foreground">Status</p>
                  <Badge variant="outline" className="mt-2">Active</Badge>
                </div>
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardHeader className="flex flex-row items-center justify-between">
              <CardTitle>Escalation Rules</CardTitle>
              <Button onClick={evaluateEscalations} size="sm" variant="default">
                <Zap className="h-4 w-4 mr-2" />
                Evaluate Now
              </Button>
            </CardHeader>
            <CardContent>
              <p className="text-sm text-muted-foreground">
                Automatic escalation evaluation is triggered periodically to bump priority of overdue notifications.
              </p>
            </CardContent>
          </Card>
        </div>
      )}
    </div>
  )
}

export default NotificationMonitoring
