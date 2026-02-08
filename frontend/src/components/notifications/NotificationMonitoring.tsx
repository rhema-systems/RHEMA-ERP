"use client"

import React, { useMemo, useState, useEffect } from 'react'
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
import { apiService } from '../../services/api.service'
import authService from '../../services/auth'

interface NotificationMetrics {
  status: string
  timestamp: string
  metrics: {
    pendingNotifications: number
    sentNotifications: number
    deadLetters: number
    criticalIssues: number
    highPriority: number
  }
  details: Record<string, number>
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

type MonitoringMode = 'full' | 'queueOnly'

const isHtmlLike = (value: string | null | undefined) => {
  if (!value) return false
  return /<\/?[a-z][\s\S]*>/i.test(value)
}

const tryFormatJson = (value: string | null | undefined) => {
  if (!value) return null
  try {
    return JSON.stringify(JSON.parse(value), null, 2)
  } catch {
    return null
  }
}

const wrapHtmlDoc = (html: string) => {
  if (/<\s*html[\s>]/i.test(html)) return html
  return `<!doctype html>
<html>
  <head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <style>
      body { font-family: ui-sans-serif, system-ui, -apple-system, Segoe UI, Roboto, Helvetica, Arial, "Apple Color Emoji", "Segoe UI Emoji"; padding: 12px; }
      pre { white-space: pre-wrap; }
      a { color: #2563eb; }
    </style>
  </head>
  <body>${html}</body>
</html>`
}

const formatDateTime = (value: string | null | undefined) => {
  if (!value) return '-'
  const d = new Date(value)
  if (Number.isNaN(d.getTime())) return value
  return new Intl.DateTimeFormat(undefined, {
    year: 'numeric',
    month: 'short',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  }).format(d)
}

const NotificationMonitoring: React.FC<{ mode?: MonitoringMode }> = ({ mode = 'full' }) => {
  const isAdmin = useMemo(() => authService.hasAnyRole(['SuperAdmin', 'TenantAdmin']), [])
  const [metrics, setMetrics] = useState<NotificationMetrics | null>(null)
  const [pendingNotifications, setPendingNotifications] = useState<Notification[]>([])
  const [deadLetters, setDeadLetters] = useState<Notification[]>([])
  const [messageQueue, setMessageQueue] = useState<MessageQueueItem[]>([])
  const [messageQueueStatus, setMessageQueueStatus] = useState<string>('all')
  const [messageQueueChannel, setMessageQueueChannel] = useState<string>('all')
  const [messageQueueSearch, setMessageQueueSearch] = useState<string>('')
  const [messageDetailsOpen, setMessageDetailsOpen] = useState(false)
  const [selectedMessage, setSelectedMessage] = useState<MessageQueueDetail | null>(null)
  const [messageDetailsLoading, setMessageDetailsLoading] = useState(false)
  const [messageDetailsError, setMessageDetailsError] = useState<string | null>(null)
  const [deleteConfirmOpen, setDeleteConfirmOpen] = useState(false)
  const [deleteTargetId, setDeleteTargetId] = useState<string | null>(null)
  const [deadLetterDeleteConfirmOpen, setDeadLetterDeleteConfirmOpen] = useState(false)
  const [deadLetterDeleteTargetId, setDeadLetterDeleteTargetId] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const [selectedTab, setSelectedTab] = useState<'health' | 'pending' | 'dead-letters' | 'message-queue'>(
    mode === 'queueOnly' ? 'message-queue' : 'health'
  )
  const [selectedFilter, setSelectedFilter] = useState('all')
  const { toast } = useToast()

  useEffect(() => {
    if (!isAdmin) {
      setLoading(false)
      return
    }

    if (mode === 'queueOnly') {
      loadMessageQueue()
      const interval = setInterval(() => {
        loadMessageQueue()
      }, 30000)
      return () => clearInterval(interval)
    }

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
  }, [isAdmin, mode, selectedTab])

  const loadMetrics = async () => {
    try {
      const data = await apiService.request<NotificationMetrics>('/admin/notifications/health')
      setMetrics(data)
    } catch (error) {
      console.error('Failed to load metrics:', error)
    }
  }

  const loadPendingNotifications = async () => {
    try {
      const data = await apiService.request<any>('/admin/notifications/pending?pageSize=50')
      setPendingNotifications(data?.items || [])
    } catch (error) {
      console.error('Failed to load pending:', error)
    } finally {
      setLoading(false)
    }
  }

  const loadDeadLetters = async () => {
    try {
      const data = await apiService.request<any>('/admin/notifications/dead-letters?pageSize=50')
      setDeadLetters(data?.items || [])
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

      const data = await apiService.request<any>(`/admin/notifications/message-queue?${params.toString()}`)
      setMessageQueue(data?.items || [])
    } catch (error) {
      console.error('Failed to load message queue:', error)
    } finally {
      setLoading(false)
    }
  }

  const openMessageDetails = async (id: string) => {
    setMessageDetailsOpen(true)
    setSelectedMessage(null)
    setMessageDetailsLoading(true)
    setMessageDetailsError(null)

    try {
      const detail = await apiService.request<MessageQueueDetail>(`/admin/notifications/message-queue/${id}`)
      setSelectedMessage(detail)
    } catch (e: any) {
      const msg = e?.error || e?.message || 'Failed to load message details'
      setMessageDetailsError(msg)
      toast({ description: msg, variant: 'destructive' })
    } finally {
      setMessageDetailsLoading(false)
    }
  }

  const retryMessage = async (id: string) => {
    try {
      await apiService.request<any>(`/admin/notifications/message-queue/${id}/retry`, { method: 'POST' })
      toast({ description: 'Message retried successfully' })
      loadMessageQueue()
    } catch (e: any) {
      toast({ description: e?.error || e?.message || 'Failed to retry message', variant: 'destructive' })
    }
  }

  const requestDeleteMessage = (id: string) => {
    setDeleteTargetId(id)
    setDeleteConfirmOpen(true)
  }

  const deleteMessage = async () => {
    if (!deleteTargetId) return

    try {
      await apiService.request<any>(`/admin/notifications/message-queue/${deleteTargetId}`, { method: 'DELETE' })
      toast({ description: 'Message cancelled/deleted' })
      setDeleteTargetId(null)
      loadMessageQueue()
    } catch (e: any) {
      toast({ description: e?.message || 'Failed to delete message', variant: 'destructive' })
    }
  }

  const retryDeadLetter = async (notificationId: string) => {
    try {
      await apiService.request<any>(`/admin/notifications/dead-letters/${notificationId}/retry`, { method: 'POST' })
      toast({ description: 'Notification moved to pending queue' })
      loadDeadLetters()
      loadMetrics()
    } catch (error) {
      toast({ description: 'Failed to retry notification', variant: 'destructive' })
    }
  }

  const retryAllDeadLetters = async () => {
    try {
      const data = await apiService.request<any>('/admin/notifications/dead-letters/retry-all', { method: 'POST' })
      toast({ description: `Moved ${data?.retryCount ?? 0} notifications to pending queue` })
      loadDeadLetters()
      loadMetrics()
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
      await apiService.request<any>(`/admin/notifications/dead-letters/${deadLetterDeleteTargetId}`, { method: 'DELETE' })
      toast({ description: 'Notification deleted' })
      setDeadLetterDeleteTargetId(null)
      loadDeadLetters()
      loadMetrics()
    } catch (error) {
      toast({ description: 'Failed to delete notification', variant: 'destructive' })
    }
  }

  const evaluateEscalations = async () => {
    try {
      await apiService.request<any>('/admin/notifications/escalation-rules/evaluate', { method: 'POST' })
      toast({ description: 'Escalation evaluation triggered' })
      loadMetrics()
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

  if (!isAdmin) {
    return (
      <Card>
        <CardHeader>
          <CardTitle>Admin only</CardTitle>
          <CardDescription>Notification monitoring is available to Tenant Admin / Super Admin users.</CardDescription>
        </CardHeader>
      </Card>
    )
  }

  return (
    <div className="space-y-6">
      {/* Health Overview */}
      {mode === 'full' && metrics ? (
        <div className="grid grid-cols-1 md:grid-cols-5 gap-4">
          <Card>
            <CardContent className="pt-6">
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-sm text-muted-foreground">Pending</p>
                  <p className="text-2xl font-bold">{metrics.metrics.pendingNotifications}</p>
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
                  <p className="text-2xl font-bold">{metrics.metrics.sentNotifications}</p>
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
                  <p className="text-2xl font-bold">{metrics.metrics.deadLetters}</p>
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
                  <p className="text-2xl font-bold">{metrics.metrics.criticalIssues}</p>
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
                  <Badge variant="outline" className="mt-2">{metrics.status}</Badge>
                </div>
                <Activity className="h-8 w-8 text-green-500" />
              </div>
            </CardContent>
          </Card>
        </div>
      ) : null}

      {/* Tabs */}
      {mode === 'full' ? (
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
      ) : null}

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
                        <TableCell className="text-sm text-muted-foreground">{formatDateTime(m.createdAt)}</TableCell>
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
          {messageDetailsLoading ? (
            <div className="text-sm text-muted-foreground">Loading…</div>
          ) : messageDetailsError ? (
            <div className="rounded-md border border-red-200 bg-red-50 p-3 text-sm text-red-700">
              {messageDetailsError}
            </div>
          ) : selectedMessage ? (
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
              <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
                <div>
                  <div className="text-xs text-muted-foreground">Created</div>
                  <div className="font-medium">{formatDateTime(selectedMessage.createdAt)}</div>
                </div>
                <div>
                  <div className="text-xs text-muted-foreground">Scheduled For</div>
                  <div className="font-medium">{formatDateTime(selectedMessage.scheduledFor)}</div>
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
                {isHtmlLike(selectedMessage.message) ? (
                  <div className="rounded-md border bg-background overflow-hidden">
                    <iframe
                      title="Rendered message"
                      className="w-full h-[360px] bg-white"
                      sandbox=""
                      srcDoc={wrapHtmlDoc(selectedMessage.message)}
                    />
                  </div>
                ) : (
                  <pre className="whitespace-pre-wrap rounded-md bg-muted p-3 text-sm max-h-[360px] overflow-auto">
                    {selectedMessage.message}
                  </pre>
                )}

                {isHtmlLike(selectedMessage.message) ? (
                  <details className="mt-2">
                    <summary className="text-xs text-muted-foreground cursor-pointer select-none">
                      View raw HTML
                    </summary>
                    <pre className="whitespace-pre-wrap rounded-md bg-muted p-3 text-xs max-h-[240px] overflow-auto mt-2">
                      {selectedMessage.message}
                    </pre>
                  </details>
                ) : null}
              </div>
              {selectedMessage.additionalData && (
                <div>
                  <div className="text-xs text-muted-foreground">Additional Data</div>
                  <pre className="w-full max-w-full whitespace-pre-wrap break-all rounded-md bg-muted p-3 text-xs max-h-[240px] overflow-auto">
                    {tryFormatJson(selectedMessage.additionalData) ?? selectedMessage.additionalData}
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
