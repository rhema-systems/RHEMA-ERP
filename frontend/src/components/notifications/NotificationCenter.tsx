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
  RefreshCw
} from 'lucide-react'
import { 
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger
} from '../ui/dropdown-menu'

interface Notification {
  id: string
  title: string
  message: string
  type: 'info' | 'success' | 'warning' | 'error'
  category: 'system' | 'user' | 'tenant' | 'financial' | 'inventory'
  priority: 'low' | 'medium' | 'high' | 'critical'
  isRead: boolean
  isStarred: boolean
  timestamp: Date
  actionUrl?: string
  metadata?: {
    userId?: string
    tenantId?: string
    relatedId?: string
    [key: string]: any
  }
}

const NotificationCenter: React.FC = () => {
  const [notifications, setNotifications] = useState<Notification[]>([])
  const [searchQuery, setSearchQuery] = useState('')
  const [selectedFilter, setSelectedFilter] = useState('all')
  const [selectedCategory, setSelectedCategory] = useState('all')
  const [showUnreadOnly, setShowUnreadOnly] = useState(false)
  const [isRealTimeEnabled, setIsRealTimeEnabled] = useState(true)

  // Mock notifications data
  useEffect(() => {
    const mockNotifications: Notification[] = [
      {
        id: '1',
        title: 'New User Registration',
        message: 'John Smith has registered a new account and is awaiting approval.',
        type: 'info',
        category: 'user',
        priority: 'medium',
        isRead: false,
        isStarred: true,
        timestamp: new Date(Date.now() - 5 * 60 * 1000), // 5 minutes ago
        actionUrl: '/administration/identity-management/users',
        metadata: { userId: 'user_123' }
      },
      {
        id: '2',
        title: 'Payment Processed Successfully',
        message: 'Payment of $2,500 from TechCorp Inc has been processed successfully.',
        type: 'success',
        category: 'financial',
        priority: 'low',
        isRead: false,
        isStarred: false,
        timestamp: new Date(Date.now() - 15 * 60 * 1000), // 15 minutes ago
        metadata: { tenantId: 'tenant_456', amount: 2500 }
      },
      {
        id: '3',
        title: 'Low Inventory Alert',
        message: 'Product "Premium Widget" is running low on stock (5 units remaining).',
        type: 'warning',
        category: 'inventory',
        priority: 'high',
        isRead: true,
        isStarred: false,
        timestamp: new Date(Date.now() - 30 * 60 * 1000), // 30 minutes ago
        actionUrl: '/inventory/products',
        metadata: { productId: 'product_789', stockLevel: 5 }
      },
      {
        id: '4',
        title: 'System Maintenance Completed',
        message: 'Scheduled system maintenance has been completed successfully. All services are now operational.',
        type: 'success',
        category: 'system',
        priority: 'low',
        isRead: true,
        isStarred: false,
        timestamp: new Date(Date.now() - 2 * 60 * 60 * 1000), // 2 hours ago
      },
      {
        id: '5',
        title: 'Failed Login Attempts',
        message: 'Multiple failed login attempts detected from IP 192.168.1.100.',
        type: 'error',
        category: 'system',
        priority: 'critical',
        isRead: false,
        isStarred: true,
        timestamp: new Date(Date.now() - 3 * 60 * 60 * 1000), // 3 hours ago
        metadata: { ipAddress: '192.168.1.100', attemptCount: 5 }
      },
      {
        id: '6',
        title: 'New Tenant Created',
        message: 'Tenant "StartupXYZ" has been successfully created and activated.',
        type: 'info',
        category: 'tenant',
        priority: 'medium',
        isRead: true,
        isStarred: false,
        timestamp: new Date(Date.now() - 4 * 60 * 60 * 1000), // 4 hours ago
        actionUrl: '/administration/tenant-management',
        metadata: { tenantId: 'tenant_999' }
      }
    ]
    
    setNotifications(mockNotifications)
  }, [])

  // Simulated real-time updates
  useEffect(() => {
    if (!isRealTimeEnabled) return

    const interval = setInterval(() => {
      // Simulate receiving new notifications
      if (Math.random() < 0.3) { // 30% chance every 10 seconds
        const newNotification: Notification = {
          id: `notification_${Date.now()}`,
          title: 'Real-time Update',
          message: 'This is a simulated real-time notification.',
          type: ['info', 'success', 'warning'][Math.floor(Math.random() * 3)] as any,
          category: ['system', 'user', 'tenant'][Math.floor(Math.random() * 3)] as any,
          priority: 'medium',
          isRead: false,
          isStarred: false,
          timestamp: new Date()
        }
        
        setNotifications(prev => [newNotification, ...prev])
      }
    }, 10000) // Every 10 seconds

    return () => clearInterval(interval)
  }, [isRealTimeEnabled])

  const getTypeIcon = (type: string) => {
    switch (type) {
      case 'success': return <CheckCircle className="h-4 w-4 text-green-600" />
      case 'warning': return <AlertCircle className="h-4 w-4 text-yellow-600" />
      case 'error': return <XCircle className="h-4 w-4 text-red-600" />
      default: return <Info className="h-4 w-4 text-blue-600" />
    }
  }

  const getCategoryIcon = (category: string) => {
    switch (category) {
      case 'user': return <Users className="h-4 w-4" />
      case 'tenant': return <Building className="h-4 w-4" />
      case 'financial': return <DollarSign className="h-4 w-4" />
      case 'inventory': return <Package className="h-4 w-4" />
      default: return <Settings className="h-4 w-4" />
    }
  }

  const getPriorityColor = (priority: string) => {
    switch (priority) {
      case 'critical': return 'bg-red-100 text-red-800'
      case 'high': return 'bg-orange-100 text-orange-800'
      case 'medium': return 'bg-yellow-100 text-yellow-800'
      default: return 'bg-gray-100 text-gray-800'
    }
  }

  const formatTimeAgo = (date: Date) => {
    const now = new Date()
    const diff = now.getTime() - date.getTime()
    
    const minutes = Math.floor(diff / (1000 * 60))
    const hours = Math.floor(diff / (1000 * 60 * 60))
    const days = Math.floor(diff / (1000 * 60 * 60 * 24))
    
    if (days > 0) return `${days}d ago`
    if (hours > 0) return `${hours}h ago`
    if (minutes > 0) return `${minutes}m ago`
    return 'Just now'
  }

  const filteredNotifications = notifications.filter(notification => {
    const matchesSearch = notification.title.toLowerCase().includes(searchQuery.toLowerCase()) ||
                         notification.message.toLowerCase().includes(searchQuery.toLowerCase())
    
    const matchesFilter = selectedFilter === 'all' || notification.type === selectedFilter
    const matchesCategory = selectedCategory === 'all' || notification.category === selectedCategory
    const matchesUnread = !showUnreadOnly || !notification.isRead
    
    return matchesSearch && matchesFilter && matchesCategory && matchesUnread
  })

  const markAsRead = (id: string) => {
    setNotifications(prev => prev.map(n => 
      n.id === id ? { ...n, isRead: true } : n
    ))
  }

  const markAsUnread = (id: string) => {
    setNotifications(prev => prev.map(n => 
      n.id === id ? { ...n, isRead: false } : n
    ))
  }

  const toggleStar = (id: string) => {
    setNotifications(prev => prev.map(n => 
      n.id === id ? { ...n, isStarred: !n.isStarred } : n
    ))
  }

  const deleteNotification = (id: string) => {
    setNotifications(prev => prev.filter(n => n.id !== id))
  }

  const markAllAsRead = () => {
    setNotifications(prev => prev.map(n => ({ ...n, isRead: true })))
  }

  const clearAllRead = () => {
    setNotifications(prev => prev.filter(n => !n.isRead))
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
              
              <Button variant="outline" size="sm">
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
                <SelectItem value="info">Info</SelectItem>
                <SelectItem value="success">Success</SelectItem>
                <SelectItem value="warning">Warning</SelectItem>
                <SelectItem value="error">Error</SelectItem>
              </SelectContent>
            </Select>
            
            {/* Category Filter */}
            <Select value={selectedCategory} onValueChange={setSelectedCategory}>
              <SelectTrigger className="w-48">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Categories</SelectItem>
                <SelectItem value="system">System</SelectItem>
                <SelectItem value="user">User</SelectItem>
                <SelectItem value="tenant">Tenant</SelectItem>
                <SelectItem value="financial">Financial</SelectItem>
                <SelectItem value="inventory">Inventory</SelectItem>
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
        {filteredNotifications.length === 0 ? (
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
                          
                          {/* Badges */}
                          <Badge variant="outline" className={getPriorityColor(notification.priority)}>
                            {notification.priority}
                          </Badge>
                          
                          <div className="flex items-center gap-1 text-muted-foreground">
                            {getCategoryIcon(notification.category)}
                            <span className="text-xs capitalize">{notification.category}</span>
                          </div>
                        </div>
                        
                        <p className="text-sm text-muted-foreground mb-2">
                          {notification.message}
                        </p>
                        
                        <div className="flex items-center gap-4 text-xs text-muted-foreground">
                          <div className="flex items-center gap-1">
                            <Clock className="h-3 w-3" />
                            {formatTimeAgo(notification.timestamp)}
                          </div>
                          
                          {notification.actionUrl && (
                            <Button
                              variant="link"
                              size="sm"
                              className="h-auto p-0 text-xs"
                              onClick={() => {
                                markAsRead(notification.id)
                                // TODO: Navigate to action URL
                                console.log('Navigate to:', notification.actionUrl)
                              }}
                            >
                              View Details →
                            </Button>
                          )}
                        </div>
                      </div>
                      
                      {/* Actions */}
                      <div className="flex items-center gap-2">
                        {/* Star */}
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={() => toggleStar(notification.id)}
                        >
                          {notification.isStarred ? (
                            <Star className="h-4 w-4 fill-current text-yellow-500" />
                          ) : (
                            <StarOff className="h-4 w-4" />
                          )}
                        </Button>
                        
                        {/* Read/Unread */}
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={() => notification.isRead 
                            ? markAsUnread(notification.id) 
                            : markAsRead(notification.id)
                          }
                        >
                          {notification.isRead ? (
                            <EyeOff className="h-4 w-4" />
                          ) : (
                            <Eye className="h-4 w-4" />
                          )}
                        </Button>
                        
                        {/* More Actions */}
                        <DropdownMenu>
                          <DropdownMenuTrigger asChild>
                            <Button variant="ghost" size="sm">
                              <MoreHorizontal className="h-4 w-4" />
                            </Button>
                          </DropdownMenuTrigger>
                          <DropdownMenuContent align="end">
                            <DropdownMenuItem
                              onClick={() => notification.isRead 
                                ? markAsUnread(notification.id) 
                                : markAsRead(notification.id)
                              }
                            >
                              {notification.isRead ? 'Mark as Unread' : 'Mark as Read'}
                            </DropdownMenuItem>
                            <DropdownMenuItem onClick={() => toggleStar(notification.id)}>
                              {notification.isStarred ? 'Remove Star' : 'Add Star'}
                            </DropdownMenuItem>
                            <DropdownMenuSeparator />
                            <DropdownMenuItem 
                              onClick={() => deleteNotification(notification.id)}
                              className="text-red-600"
                            >
                              Delete
                            </DropdownMenuItem>
                          </DropdownMenuContent>
                        </DropdownMenu>
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