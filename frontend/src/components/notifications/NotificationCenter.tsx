"use client"

import React, { useState, useMemo } from 'react'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '../ui/card'
import { Badge } from '../ui/badge'
import { Button } from '../ui/button'
import { Input } from '../ui/input'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '../ui/select'
import { 
  Bell, 
  Search,
  Check,
  CheckCheck,
  X,
  Clock,
  AlertCircle,
  Info,
  CheckCircle,
  XCircle,
  Wrench,
  FileText,
  Shield,
  Package,
  Settings,
  RefreshCw,
  Trash2
} from 'lucide-react'
import { 
  Notification as NotificationModel,
  notificationService
} from '../../services/notificationService'
import { useNotifications } from '../../hooks/useNotifications'

const NotificationCenter: React.FC = () => {
  const {
    notifications,
    unreadCount,
    loading,
    error,
    markAsRead,
    markAllAsRead,
    deleteNotification,
    fetchNotifications,
    filterNotifications
  } = useNotifications({ autoFetch: true, enableRealTime: true })

  const [searchQuery, setSearchQuery] = useState('')
  const [filterPriority, setFilterPriority] = useState<'all' | 'Low' | 'Normal' | 'High' | 'Critical'>('all')
  const [filterStatus, setFilterStatus] = useState<'all' | 'Pending' | 'Sent' | 'Failed'>('all')
  const [showUnreadOnly, setShowUnreadOnly] = useState(false)

  // Filter notifications with new unified API fields
  const filteredNotifications = useMemo(() => {
    let result = notifications

    if (showUnreadOnly) {
      result = result.filter(n => !n.isRead)
    }

    if (filterPriority !== 'all') {
      result = result.filter(n => n.priority === filterPriority)
    }

    if (filterStatus !== 'all') {
      result = result.filter(n => n.status === filterStatus)
    }

    if (searchQuery.trim()) {
      const query = searchQuery.toLowerCase()
      result = result.filter(n =>
        n.title.toLowerCase().includes(query) ||
        n.message.toLowerCase().includes(query) ||
        n.entityType.toLowerCase().includes(query)
      )
    }

    return result
  }, [notifications, showUnreadOnly, filterPriority, filterStatus, searchQuery, filterNotifications])

  const getTypeIcon = (priority: string) => {
    switch (priority) {
      case 'Critical': return <XCircle className="h-4 w-4 text-red-600" />
      case 'High': return <AlertCircle className="h-4 w-4 text-yellow-600" />
      case 'Normal': return <Info className="h-4 w-4 text-blue-600" />
      case 'Low': return <CheckCircle className="h-4 w-4 text-green-600" />
      default: return <Info className="h-4 w-4 text-blue-600" />
    }
  }

  const getEntityIcon = (entityType: string) => {
    switch (entityType) {
      case 'JobCard': return <FileText className="h-4 w-4" />
      case 'WorkOrder': return <Wrench className="h-4 w-4" />
      case 'Quality': return <Shield className="h-4 w-4" />
      case 'Asset': return <Package className="h-4 w-4" />
      case 'System': return <Settings className="h-4 w-4" />
      default: return <Settings className="h-4 w-4" />
    }
  }

  const formatTimeAgo = (dateString: string) => {
    return notificationService.formatNotificationTime(dateString)
  }

  const handleNotificationClick = async (notification: NotificationModel) => {
    if (!notification.isRead) {
      await markAsRead(notification.id)
    }
    if (notification.actionUrl) {
      window.location.href = notification.actionUrl
    }
  }

  return (
    <div className="space-y-6">
      {/* Controls Header */}
      <Card>
        <CardHeader>
          <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4">
            <div>
              <CardTitle className="flex items-center gap-2">
                <Bell className="h-5 w-5" />
                Notification Center
                {unreadCount > 0 && (
                  <Badge variant="destructive">{unreadCount} unread</Badge>
                )}
              </CardTitle>
              <CardDescription>
                Manage your in-app notifications and real-time alerts
              </CardDescription>
            </div>
            
            <div className="flex items-center gap-2">
              {unreadCount > 0 && (
                <Button variant="outline" size="sm" onClick={() => markAllAsRead()}>
                  <CheckCheck className="h-4 w-4 mr-2" />
                  Mark All Read
                </Button>
              )}
              
              <Button variant="outline" size="sm" onClick={() => fetchNotifications()}>
                <RefreshCw className="h-4 w-4" />
              </Button>
            </div>
          </div>
        </CardHeader>
        
        <CardContent>
          <div className="flex flex-col sm:flex-row gap-4">
            {/* Search */}
            <div className="relative flex-1">
              <Search className="h-4 w-4 absolute left-3 top-3 text-muted-foreground" />
              <Input
                placeholder="Search notifications..."
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
                className="pl-10"
              />
            </div>
            
            {/* Priority Filter */}
            <Select value={filterPriority} onValueChange={(value: any) => setFilterPriority(value)}>
              <SelectTrigger className="w-48">
                <SelectValue placeholder="Priority" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Priorities</SelectItem>
                <SelectItem value="Low">Low</SelectItem>
                <SelectItem value="Normal">Normal</SelectItem>
                <SelectItem value="High">High</SelectItem>
                <SelectItem value="Critical">Critical</SelectItem>
              </SelectContent>
            </Select>
            
            {/* Status Filter */}
            <Select value={filterStatus} onValueChange={(value: any) => setFilterStatus(value)}>
              <SelectTrigger className="w-48">
                <SelectValue placeholder="Status" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Status</SelectItem>
                <SelectItem value="Pending">Pending</SelectItem>
                <SelectItem value="Sent">Sent</SelectItem>
                <SelectItem value="Failed">Failed</SelectItem>
              </SelectContent>
            </Select>
            
            {/* Unread Only Toggle - convert to a simpler select */}
            <Select value={showUnreadOnly ? 'unread' : 'all'} onValueChange={(value) => setShowUnreadOnly(value === 'unread')}>
              <SelectTrigger className="w-32">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All</SelectItem>
                <SelectItem value="unread">Unread</SelectItem>
              </SelectContent>
            </Select>
          </div>
        </CardContent>
      </Card>

      {/* Notifications List */}
      <div className="space-y-3">
        {loading ? (
          <Card>
            <CardContent className="p-8 text-center">
              <RefreshCw className="h-8 w-8 text-muted-foreground mx-auto mb-4 animate-spin" />
              <p className="text-muted-foreground">Loading notifications...</p>
            </CardContent>
          </Card>
        ) : filteredNotifications.length === 0 ? (
          <Card>
            <CardContent className="p-8 text-center">
              <Bell className="h-12 w-12 text-muted-foreground mx-auto mb-4" />
              <h3 className="text-lg font-medium mb-2">No notifications found</h3>
              <p className="text-muted-foreground">
                {searchQuery || filterPriority !== 'all' || filterStatus !== 'all' || showUnreadOnly
                  ? 'Try adjusting your filters'
                  : 'You\'re all caught up!'}
              </p>
            </CardContent>
          </Card>
        ) : (
          filteredNotifications.map((notification) => (
            <Card
              key={notification.id}
              className={`hover:shadow-md transition-shadow cursor-pointer ${
                !notification.isRead ? 'bg-blue-50/50 border-l-4 border-l-blue-500 dark:bg-blue-950/20' : ''
              }`}
              onClick={() => handleNotificationClick(notification)}
            >
              <CardContent className="p-4">
                <div className="flex items-start gap-4">
                  {/* Priority Icon */}
                  <div className="flex-shrink-0 pt-1">
                    {getTypeIcon(notification.priority)}
                  </div>
                  
                  {/* Content */}
                  <div className="flex-1 min-w-0">
                    <div className="flex items-start justify-between gap-4">
                      <div className="flex-1">
                        <div className="flex items-center gap-2 mb-1 flex-wrap">
                          <h4 className={`font-medium ${!notification.isRead ? 'font-semibold' : ''}`}>
                            {notification.title}
                          </h4>
                          
                          {/* Priority Badge */}
                          <Badge variant="outline" className={`${notificationService.getNotificationColor(notification.priority)}`}>
                            {notification.priority}
                          </Badge>

                          {/* Status Badge */}
                          <Badge variant="secondary" className="text-xs">
                            {notification.status}
                          </Badge>
                          
                          {/* Entity Type Icon and Label */}
                          <div className="flex items-center gap-1 text-muted-foreground">
                            {getEntityIcon(notification.entityType)}
                            <span className="text-xs">{notification.entityType}</span>
                          </div>
                        </div>
                        
                        <p className="text-sm text-muted-foreground mb-2">
                          {notification.message}
                        </p>
                        
                        <div className="flex items-center gap-4 text-xs text-muted-foreground flex-wrap">
                          <div className="flex items-center gap-1">
                            <Clock className="h-3 w-3" />
                            {formatTimeAgo(notification.timestamp || notification.createdAt || '')}
                          </div>

                          {notification.attemptCount > 0 && (
                            <div className="text-xs">
                              Attempts: {notification.attemptCount}
                            </div>
                          )}
                          
                          {notification.actionUrl && (
                            <Button
                              variant="link"
                              size="sm"
                              className="h-auto p-0 text-xs"
                              onClick={(e) => {
                                e.stopPropagation()
                                handleNotificationClick(notification)
                              }}
                            >
                              View Details →
                            </Button>
                          )}
                        </div>
                      </div>
                      
                      {/* Actions */}
                      <div className="flex items-center gap-2 flex-shrink-0" onClick={(e) => e.stopPropagation()}>
                        {/* Mark as Read */}
                        {!notification.isRead && (
                          <Button
                            variant="ghost"
                            size="sm"
                            onClick={() => markAsRead(notification.id)}
                            title="Mark as read"
                          >
                            <Check className="h-4 w-4" />
                          </Button>
                        )}
                        
                        {/* Delete */}
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={() => deleteNotification(notification.id)}
                          className="text-red-600 hover:text-red-800 hover:bg-red-50"
                          title="Delete notification"
                        >
                          <Trash2 className="h-4 w-4" />
                        </Button>
                      </div>
                    </div>
                  </div>
                </div>
              </CardContent>
            </Card>
          ))
        )}
      </div>
    </div>
  )
}

export default NotificationCenter