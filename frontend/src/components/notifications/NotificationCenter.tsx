"use client"

import React, { useState, useEffect } from 'react'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '../ui/card'
import { Badge } from '../ui/badge'
import { Button } from '../ui/button'
import { Input } from '../ui/input'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '../ui/select'
import { Switch } from '../ui/switch'
import { Label } from '../ui/label'
import { 
  Bell, 
  Search, 
  Filter, 
  MoreHorizontal, 
  Check,
  CheckCheck,
  X,
  Star,
  StarOff,
  Eye,
  EyeOff,
  Clock,
  AlertCircle,
  Info,
  CheckCircle,
  XCircle,
  Users,
  Building,
  DollarSign,
  Package,
  Settings,
  RefreshCw,
  Wrench,
  FileText,
  Shield
} from 'lucide-react'
import { 
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger
} from '../ui/dropdown-menu'
import { 
  Notification as NotificationModel,
  notificationService
} from '../../services/notificationService'

const NotificationCenter: React.FC = () => {
  const [notifications, setNotifications] = useState<NotificationModel[]>([])
  const [loading, setLoading] = useState(true)
  const [searchQuery, setSearchQuery] = useState('')
  const [selectedFilter, setSelectedFilter] = useState('all')
  const [selectedCategory, setSelectedCategory] = useState('all')
  const [showUnreadOnly, setShowUnreadOnly] = useState(false)
  const [isRealTimeEnabled, setIsRealTimeEnabled] = useState(true)

  // Load notifications from API
  useEffect(() => {
    loadNotifications()
  }, [])

  // Setup real-time notifications
  useEffect(() => {
    if (!isRealTimeEnabled) {
      notificationService.disconnectFromRealTimeNotifications()
      return
    }

    notificationService.connectToRealTimeNotifications()
    
    const unsubscribe = notificationService.onNotification((notification) => {
      setNotifications(prev => [notification, ...prev])
      
      // Show browser notification if permission granted
      if ('Notification' in window && Notification.permission === 'granted') {
        new window.Notification(notification.title, {
          body: notification.message,
          icon: '/favicon.ico'
        })
      }
    })

    return () => {
      unsubscribe()
      notificationService.disconnectFromRealTimeNotifications()
    }
  }, [isRealTimeEnabled])

  const loadNotifications = async () => {
    setLoading(true)
    try {
      const result = await notificationService.getNotifications({
        page: 1,
        pageSize: 50
      })
      setNotifications(result.items)
    } catch (error) {
      console.error('Failed to load notifications:', error)
    } finally {
      setLoading(false)
    }
  }

  const getTypeIcon = (type: string) => {
    switch (type) {
      case 'Success': return <CheckCircle className="h-4 w-4 text-green-600" />
      case 'Warning': return <AlertCircle className="h-4 w-4 text-yellow-600" />
      case 'Error': return <XCircle className="h-4 w-4 text-red-600" />
      default: return <Info className="h-4 w-4 text-blue-600" />
    }
  }

  const getCategoryIcon = (category: string) => {
    switch (category) {
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

  const filteredNotifications = notifications.filter(notification => {
    const matchesSearch = notification.title.toLowerCase().includes(searchQuery.toLowerCase()) ||
                         notification.message.toLowerCase().includes(searchQuery.toLowerCase())
    
    const matchesFilter = selectedFilter === 'all' || notification.type === selectedFilter
    const matchesCategory = selectedCategory === 'all' || notification.category === selectedCategory
    const matchesUnread = !showUnreadOnly || !notification.isRead
    
    return matchesSearch && matchesFilter && matchesCategory && matchesUnread
  })

  const markAsRead = async (id: string) => {
    try {
      await notificationService.markAsRead(id)
      setNotifications(prev => prev.map(n => 
        n.id === id ? { ...n, isRead: true } : n
      ))
    } catch (error) {
      console.error('Failed to mark as read:', error)
    }
  }

  const deleteNotification = async (id: string) => {
    try {
      await notificationService.deleteNotification(id)
      setNotifications(prev => prev.filter(n => n.id !== id))
    } catch (error) {
      console.error('Failed to delete notification:', error)
    }
  }

  const markAllAsRead = async () => {
    try {
      await notificationService.markAllAsRead()
      setNotifications(prev => prev.map(n => ({ ...n, isRead: true })))
    } catch (error) {
      console.error('Failed to mark all as read:', error)
    }
  }

  const clearAllRead = () => {
    setNotifications(prev => prev.filter(n => !n.isRead))
  }

  const refreshNotifications = () => {
    loadNotifications()
  }

  const unreadCount = notifications.filter(n => !n.isRead).length

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
              <div className="flex items-center space-x-2">
                <Switch
                  id="real-time"
                  checked={isRealTimeEnabled}
                  onCheckedChange={setIsRealTimeEnabled}
                />
                <Label htmlFor="real-time" className="text-sm">Real-time</Label>
              </div>
              
              <Button variant="outline" size="sm" onClick={markAllAsRead}>
                <CheckCheck className="h-4 w-4 mr-2" />
                Mark All Read
              </Button>
              
              <Button variant="outline" size="sm" onClick={clearAllRead}>
                <X className="h-4 w-4 mr-2" />
                Clear Read
              </Button>
              
              <Button variant="outline" size="sm" onClick={refreshNotifications}>
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
            
            {/* Type Filter */}
            <Select value={selectedFilter} onValueChange={setSelectedFilter}>
              <SelectTrigger className="w-48">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Types</SelectItem>
                <SelectItem value="Info">Info</SelectItem>
                <SelectItem value="Success">Success</SelectItem>
                <SelectItem value="Warning">Warning</SelectItem>
                <SelectItem value="Error">Error</SelectItem>
              </SelectContent>
            </Select>
            
            {/* Category Filter */}
            <Select value={selectedCategory} onValueChange={setSelectedCategory}>
              <SelectTrigger className="w-48">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Categories</SelectItem>
                <SelectItem value="JobCard">Job Cards</SelectItem>
                <SelectItem value="WorkOrder">Work Orders</SelectItem>
                <SelectItem value="Quality">Quality Control</SelectItem>
                <SelectItem value="Asset">Assets</SelectItem>
                <SelectItem value="System">System</SelectItem>
              </SelectContent>
            </Select>
            
            {/* Unread Only Toggle */}
            <div className="flex items-center space-x-2">
              <Switch
                id="unread-only"
                checked={showUnreadOnly}
                onCheckedChange={setShowUnreadOnly}
              />
              <Label htmlFor="unread-only" className="text-sm">Unread only</Label>
            </div>
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
                {searchQuery || selectedFilter !== 'all' || selectedCategory !== 'all' || showUnreadOnly
                  ? 'Try adjusting your filters'
                  : 'You\'re all caught up!'}
              </p>
            </CardContent>
          </Card>
        ) : (
          filteredNotifications.map((notification) => (
            <Card
              key={notification.id}
              className={`hover:shadow-md transition-shadow ${
                !notification.isRead ? 'bg-blue-50/50 border-l-4 border-l-blue-500' : ''
              }`}
            >
              <CardContent className="p-4">
                <div className="flex items-start gap-4">
                  {/* Type Icon */}
                  <div className="flex-shrink-0 pt-1">
                    {getTypeIcon(notification.type)}
                  </div>
                  
                  {/* Content */}
                  <div className="flex-1 min-w-0">
                    <div className="flex items-start justify-between gap-4">
                      <div className="flex-1">
                        <div className="flex items-center gap-2 mb-1">
                          <h4 className={`font-medium ${!notification.isRead ? 'font-semibold' : ''}`}>
                            {notification.title}
                          </h4>
                          
                          {/* Type Badge */}
                          <Badge variant="outline" className={`${notificationService.getNotificationColor(notification.type)}`}>
                            {notification.type}
                          </Badge>
                          
                          <div className="flex items-center gap-1 text-muted-foreground">
                            {getCategoryIcon(notification.category)}
                            <span className="text-xs">{notification.category}</span>
                          </div>
                        </div>
                        
                        <p className="text-sm text-muted-foreground mb-2">
                          {notification.message}
                        </p>
                        
                        <div className="flex items-center gap-4 text-xs text-muted-foreground">
                          <div className="flex items-center gap-1">
                            <Clock className="h-3 w-3" />
                            {formatTimeAgo(notification.createdAt)}
                          </div>
                          
                          {notification.actionUrl && (
                            <Button
                              variant="link"
                              size="sm"
                              className="h-auto p-0 text-xs"
                              onClick={() => {
                                markAsRead(notification.id)
                                window.location.href = notification.actionUrl || ''
                              }}
                            >
                              View Details →
                            </Button>
                          )}
                        </div>
                      </div>
                      
                      {/* Actions */}
                      <div className="flex items-center gap-2">
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
                          className="text-red-600 hover:text-red-800"
                          title="Delete notification"
                        >
                          <X className="h-4 w-4" />
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