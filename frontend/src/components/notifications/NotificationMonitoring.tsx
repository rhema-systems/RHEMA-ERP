"use client"

import React, { useState, useEffect } from 'react'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '../ui/card'
import { Button } from '../ui/button'
import { Badge } from '../ui/badge'
import { Input } from '../ui/input'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '../ui/select'
import { ConfirmationDialog } from '../ui/confirmation-dialog'
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '../ui/dialog'
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

interface MessageQueueItem {
  id: string
  notificationType: string
  title: string
  messagePreview: string
  status: string
  priority: string
  deliveryMethods?: string
  recipientId?: string | null
  recipientName?: string | null
  recipientEmail?: string | null
  emailAddress?: string | null
  phoneNumber?: string | null
  attemptCount: number
  lastError?: string | null
  entityType?: string | null
  entityId?: string | null
  actionUrl?: string | null
  createdAt: string
  sentAt?: string | null
}

interface MessageQueueDetail {
  id: string
  notificationType: string
  title: string
  message: string
  status: string
  priority: string
  deliveryMethods?: string
  recipientId?: string | null
  recipientName?: string | null
  recipientEmail?: string | null
  emailAddress?: string | null
  phoneNumber?: string | null
  attemptCount: number
  lastError?: string | null
  entityType?: string | null
  entityId?: string | null
  actionUrl?: string | null
  additionalData?: string | null
  scheduledFor: string
  createdAt: string
  sentAt?: string | null
}

const NotificationMonitoring: React.FC = () => {
  const [metrics, setMetrics] = useState<NotificationMetrics | null>(null)
  const [pendingNotifications, setPendingNotifications] = useState<Notification[]>([])
  const [deadLetters, setDeadLetters] = useState<Notification[]>([])
  const [messageQueue, setMessageQueue] = useState<MessageQueueItem[]>([])
  const [messageQueueStatus, setMessageQueueStatus] = useState<string>('all')
  const [messageQueueChannel, setMessageQueueChannel] = useState<string>('all')
  const [messageQueueSearch, setMessageQueueSearch] = useState<string>('')
  const [messageDetailsOpen, setMessageDetailsOpen] = useState(false)
  const [selectedMessage, setSelectedMessage] = useState<MessageQueueDetail | null>(null)
  const [deleteConfirmOpen, setDeleteConfirmOpen] = useState(false)
  const [deleteTargetId, setDeleteTargetId] = useState<string | null>(null)
  const [deadLetterDeleteConfirmOpen, setDeadLetterDeleteConfirmOpen] = useState(false)
  const [deadLetterDeleteTargetId, setDeadLetterDeleteTargetId] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const [selectedTab, setSelectedTab] = useState<'health' | 'pending' | 'dead-letters' | 'message-queue'>('health')
  const [selectedFilter, setSelectedFilter] = useState('all')
  const { toast } = useToast()

  useEffect(() => {
    loadMetrics()
    loadPendingNotifications()
    loadDeadLetters()
    if (selectedTab === 'message-queue') loadMessageQueue()
    
    // Refresh every 30 seconds
    const interval = setInterval(() => {
      loadMetrics()
      if (selectedTab === 'pending') loadPendingNotifications()
      if (selectedTab === 'dead-letters') loadDeadLetters()
      if (selectedTab === 'message-queue') loadMessageQueue()
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

  const loadMessageQueue = async () => {
    try {
      const params = new URLSearchParams()
      params.set('pageSize', '100')
      if (messageQueueStatus !== 'all') params.set('status', messageQueueStatus)
      if (messageQueueChannel !== 'all') params.set('channel', messageQueueChannel)
      if (messageQueueSearch.trim()) params.set('search', messageQueueSearch.trim())

      const response = await fetch(`/api/admin/notifications/message-queue?${params.toString()}`, {
        headers: { 'Authorization': `Bearer ${localStorage.getItem('token')}` }
      })
      const data = await response.json()
      setMessageQueue(data.items || [])
    } catch (error) {
      console.error('Failed to load message queue:', error)
    } finally {
      setLoading(false)
    }
  }

  const openMessageDetails = async (id: string) => {
    try {
      const response = await fetch(`/api/admin/notifications/message-queue/${id}`, {
        headers: { 'Authorization': `Bearer ${localStorage.getItem('token')}` }
      })
      if (!response.ok) {
        toast({ description: 'Failed to load message details', variant: 'destructive' })
        return
      }
      const detail = await response.json()
      setSelectedMessage(detail)
      setMessageDetailsOpen(true)
    } catch {
      toast({ description: 'Failed to load message details', variant: 'destructive' })
    }
  }

  const retryMessage = async (id: string) => {
    try {
      const response = await fetch(`/api/admin/notifications/message-queue/${id}/retry`, {
        method: 'POST',
        headers: { 'Authorization': `Bearer ${localStorage.getItem('token')}` }
      })
      if (!response.ok) {
        const data = await response.json().catch(() => null)
        toast({ description: data?.error || 'Failed to retry message', variant: 'destructive' })
        return
      }
      toast({ description: 'Message retried successfully' })
      loadMessageQueue()
    } catch {
      toast({ description: 'Failed to retry message', variant: 'destructive' })
    }
  }

  const requestDeleteMessage = (id: string) => {
    setDeleteTargetId(id)
    setDeleteConfirmOpen(true)
  }

  const deleteMessage = async () => {
    if (!deleteTargetId) return

    try {
      const response = await fetch(`/api/admin/notifications/message-queue/${deleteTargetId}`, {
        method: 'DELETE',
        headers: { 'Authorization': `Bearer ${localStorage.getItem('token')}` }
      })
      if (!response.ok) {
        toast({ description: 'Failed to delete message', variant: 'destructive' })
        return
      }
      toast({ description: 'Message deleted' })
      setDeleteTargetId(null)
      loadMessageQueue()
    } catch {
      toast({ description: 'Failed to delete message', variant: 'destructive' })
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

  const requestDeleteDeadLetter = (notificationId: string) => {
    setDeadLetterDeleteTargetId(notificationId)
    setDeadLetterDeleteConfirmOpen(true)
  }

  const deleteDeadLetter = async () => {
    if (!deadLetterDeleteTargetId) return

    try {
      const response = await fetch(`/api/admin/notifications/dead-letters/${deadLetterDeleteTargetId}`, {
        method: 'DELETE',
        headers: { 'Authorization': `Bearer ${localStorage.getItem('token')}` }
      })
      if (response.ok) {
        toast({ description: 'Notification deleted' })
        setDeadLetterDeleteTargetId(null)
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
        {(['health', 'pending', 'dead-letters', 'message-queue'] as const).map(tab => (
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
            {tab === 'message-queue' && 'Message Queue'}
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
                              onClick={() => requestDeleteDeadLetter(notif.id)}
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

      {/* Message Queue Tab */}
      {selectedTab === 'message-queue' && (
        <Card>
          <CardHeader className="flex flex-row items-center justify-between">
            <div>
              <CardTitle>Message Queue</CardTitle>
              <CardDescription>Unified log of in-app + email notifications (with retry and audit)</CardDescription>
            </div>
            <Button onClick={loadMessageQueue} size="sm" variant="outline">
              <RefreshCw className="h-4 w-4 mr-2" />
              Refresh
            </Button>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
              <Input
                placeholder="Search title / type / email / entity..."
                value={messageQueueSearch}
                onChange={(e) => setMessageQueueSearch(e.target.value)}
                onKeyDown={(e) => {
                  if (e.key === 'Enter') loadMessageQueue()
                }}
              />
              <Select
                value={messageQueueChannel}
                onValueChange={(v) => {
                  setMessageQueueChannel(v)
                  setTimeout(loadMessageQueue, 0)
                }}
              >
                <SelectTrigger>
                  <SelectValue placeholder="Channel" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">All Channels</SelectItem>
                  <SelectItem value="Email">Email</SelectItem>
                  <SelectItem value="InApp">In-App</SelectItem>
                </SelectContent>
              </Select>
              <Select
                value={messageQueueStatus}
                onValueChange={(v) => {
                  setMessageQueueStatus(v)
                  setTimeout(loadMessageQueue, 0)
                }}
              >
                <SelectTrigger>
                  <SelectValue placeholder="Status" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">All Statuses</SelectItem>
                  <SelectItem value="Pending">Pending</SelectItem>
                  <SelectItem value="Sent">Sent</SelectItem>
                  <SelectItem value="Failed">Failed</SelectItem>
                  <SelectItem value="Dismissed">Dismissed</SelectItem>
                  <SelectItem value="Archived">Archived</SelectItem>
                </SelectContent>
              </Select>
            </div>

            <div className="overflow-x-auto">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Status</TableHead>
                    <TableHead>Channel</TableHead>
                    <TableHead>Title</TableHead>
                    <TableHead>Recipient</TableHead>
                    <TableHead>Attempts</TableHead>
                    <TableHead>Created</TableHead>
                    <TableHead className="text-right">Actions</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {loading ? (
                    <TableRow>
                      <TableCell colSpan={7} className="text-center py-8 text-muted-foreground">
                        Loading...
                      </TableCell>
                    </TableRow>
                  ) : messageQueue.length === 0 ? (
                    <TableRow>
                      <TableCell colSpan={7} className="text-center py-8 text-muted-foreground">
                        No messages found.
                      </TableCell>
                    </TableRow>
                  ) : (
                    messageQueue.map((m) => (
                      <TableRow key={m.id}>
                        <TableCell>
                          <Badge variant={m.status === 'Failed' ? 'destructive' : m.status === 'Pending' ? 'secondary' : 'outline'}>
                            {m.status}
                          </Badge>
                        </TableCell>
                        <TableCell className="text-sm text-muted-foreground">{m.deliveryMethods || '-'}</TableCell>
                        <TableCell className="max-w-[420px]">
                          <div className="font-medium truncate">{m.title}</div>
                          <div className="text-xs text-muted-foreground truncate">{m.messagePreview}</div>
                        </TableCell>
                        <TableCell className="text-sm">
                          {m.emailAddress || m.recipientEmail || m.recipientName || (m.recipientId ? m.recipientId : '-')}
                        </TableCell>
                        <TableCell className="text-sm">{m.attemptCount}</TableCell>
                        <TableCell className="text-sm text-muted-foreground">{new Date(m.createdAt).toLocaleString()}</TableCell>
                        <TableCell className="text-right">
                          <div className="flex justify-end gap-2">
                            <Button variant="outline" size="sm" onClick={() => openMessageDetails(m.id)}>
                              View
                            </Button>
                            <Button
                              variant="outline"
                              size="sm"
                              onClick={() => retryMessage(m.id)}
                              disabled={m.status !== 'Failed' && m.status !== 'Pending'}
                            >
                              <RefreshCw className="h-4 w-4 mr-1" />
                              Retry
                            </Button>
                            <Button variant="destructive" size="sm" onClick={() => requestDeleteMessage(m.id)}>
                              <Trash2 className="h-4 w-4" />
                            </Button>
                          </div>
                        </TableCell>
                      </TableRow>
                    ))
                  )}
                </TableBody>
              </Table>
            </div>
          </CardContent>
        </Card>
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

      <ConfirmationDialog
        open={deleteConfirmOpen}
        onOpenChange={setDeleteConfirmOpen}
        title="Delete Message?"
        description="This will remove the message from the queue view (soft delete)."
        confirmText="Delete"
        variant="destructive"
        onConfirm={async () => {
          await deleteMessage()
        }}
      />

      <ConfirmationDialog
        open={deadLetterDeleteConfirmOpen}
        onOpenChange={setDeadLetterDeleteConfirmOpen}
        title="Delete Dead-Letter?"
        description="This permanently deletes the dead-letter record."
        confirmText="Delete"
        variant="destructive"
        onConfirm={async () => {
          await deleteDeadLetter()
        }}
      />

      <Dialog open={messageDetailsOpen} onOpenChange={setMessageDetailsOpen}>
        <DialogContent className="max-w-3xl">
          <DialogHeader>
            <DialogTitle>Message Details</DialogTitle>
          </DialogHeader>
          {selectedMessage ? (
            <div className="space-y-3">
              <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
                <div>
                  <div className="text-xs text-muted-foreground">Status</div>
                  <div className="font-medium">{selectedMessage.status}</div>
                </div>
                <div>
                  <div className="text-xs text-muted-foreground">Channel</div>
                  <div className="font-medium">{selectedMessage.deliveryMethods || '-'}</div>
                </div>
                <div>
                  <div className="text-xs text-muted-foreground">Attempts</div>
                  <div className="font-medium">{selectedMessage.attemptCount}</div>
                </div>
              </div>
              <div>
                <div className="text-xs text-muted-foreground">Title</div>
                <div className="font-medium">{selectedMessage.title}</div>
              </div>
              <div>
                <div className="text-xs text-muted-foreground">Recipient</div>
                <div className="font-medium">
                  {selectedMessage.emailAddress || selectedMessage.recipientEmail || selectedMessage.recipientName || selectedMessage.recipientId || '-'}
                </div>
              </div>
              {selectedMessage.lastError && (
                <div className="rounded-md border border-red-200 bg-red-50 p-3 text-sm text-red-700">
                  {selectedMessage.lastError}
                </div>
              )}
              <div>
                <div className="text-xs text-muted-foreground">Message</div>
                <pre className="whitespace-pre-wrap rounded-md bg-muted p-3 text-sm max-h-[360px] overflow-auto">
                  {selectedMessage.message}
                </pre>
              </div>
              {selectedMessage.additionalData && (
                <div>
                  <div className="text-xs text-muted-foreground">Additional Data</div>
                  <pre className="whitespace-pre-wrap rounded-md bg-muted p-3 text-xs max-h-[240px] overflow-auto">
                    {selectedMessage.additionalData}
                  </pre>
                </div>
              )}
            </div>
          ) : (
            <div className="text-sm text-muted-foreground">No message selected.</div>
          )}
        </DialogContent>
      </Dialog>
    </div>
  )
}

export default NotificationMonitoring
