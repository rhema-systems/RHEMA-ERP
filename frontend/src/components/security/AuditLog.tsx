"use client"

import React, { useState, useMemo } from 'react'
import { Card, CardContent, CardHeader, CardTitle } from '../ui/card'
import { Button } from '../ui/button'
import { Input } from '../ui/input'
import { Badge } from '../ui/badge'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '../ui/select'
import { Calendar } from '../ui/calendar'
import { Popover, PopoverContent, PopoverTrigger } from '../ui/popover'
import { format } from 'date-fns'
import { 
  Search, 
  Filter, 
  Download, 
  Calendar as CalendarIcon,
  Shield,
  User,
  Settings,
  Database,
  Lock,
  Unlock,
  AlertTriangle,
  CheckCircle,
  XCircle,
  Eye,
  ChevronDown,
  MoreVertical,
  FileText
} from 'lucide-react'

interface AuditEvent {
  id: string
  timestamp: Date
  userId: string
  userName: string
  userEmail: string
  action: string
  category: 'Authentication' | 'Authorization' | 'DataAccess' | 'SystemConfig' | 'UserManagement' | 'Security'
  resource: string
  details: string
  ipAddress: string
  userAgent: string
  location: string
  riskLevel: 'Low' | 'Medium' | 'High' | 'Critical'
  success: boolean
  sessionId: string
  additionalData?: Record<string, any>
}

interface AuditLogProps {
  className?: string
}

// Mock audit data
const generateMockAuditData = (): AuditEvent[] => {
  const users = [
    { id: '1', name: 'John Doe', email: 'john@company.com' },
    { id: '2', name: 'Jane Smith', email: 'jane@company.com' },
    { id: '3', name: 'Bob Johnson', email: 'bob@company.com' },
    { id: '4', name: 'Alice Brown', email: 'alice@company.com' }
  ]

  const actions = [
    { action: 'User Login', category: 'Authentication' as const, resource: 'Authentication System', riskLevel: 'Low' as const },
    { action: 'Failed Login Attempt', category: 'Authentication' as const, resource: 'Authentication System', riskLevel: 'Medium' as const },
    { action: 'Password Changed', category: 'Authentication' as const, resource: 'User Account', riskLevel: 'Medium' as const },
    { action: '2FA Enabled', category: 'Security' as const, resource: 'Security Settings', riskLevel: 'Low' as const },
    { action: 'Role Assigned', category: 'Authorization' as const, resource: 'User Permissions', riskLevel: 'Medium' as const },
    { action: 'Data Export', category: 'DataAccess' as const, resource: 'Customer Database', riskLevel: 'High' as const },
    { action: 'System Configuration Changed', category: 'SystemConfig' as const, resource: 'System Settings', riskLevel: 'High' as const },
    { action: 'User Account Created', category: 'UserManagement' as const, resource: 'User Management', riskLevel: 'Medium' as const },
    { action: 'Security Policy Updated', category: 'Security' as const, resource: 'Security Policies', riskLevel: 'High' as const },
    { action: 'Unauthorized Access Attempt', category: 'Security' as const, resource: 'Protected Resource', riskLevel: 'Critical' as const },
  ]

  const locations = ['New York, US', 'London, UK', 'Tokyo, JP', 'Sydney, AU', 'Berlin, DE']
  const ips = ['192.168.1.100', '10.0.0.50', '172.16.0.25', '203.0.113.10', '198.51.100.5']

  const events: AuditEvent[] = []

  for (let i = 0; i < 50; i++) {
    const user = users[Math.floor(Math.random() * users.length)]
    const actionData = actions[Math.floor(Math.random() * actions.length)]
    const isSuccess = actionData.action.includes('Failed') || actionData.action.includes('Unauthorized') ? false : Math.random() > 0.1
    
    events.push({
      id: `audit-${i + 1}`,
      timestamp: new Date(Date.now() - Math.random() * 30 * 24 * 60 * 60 * 1000), // Last 30 days
      userId: user.id,
      userName: user.name,
      userEmail: user.email,
      action: actionData.action,
      category: actionData.category,
      resource: actionData.resource,
      details: generateDetails(actionData.action, user.name),
      ipAddress: ips[Math.floor(Math.random() * ips.length)],
      userAgent: 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36',
      location: locations[Math.floor(Math.random() * locations.length)],
      riskLevel: isSuccess ? actionData.riskLevel : 'Critical',
      success: isSuccess,
      sessionId: `session-${Math.random().toString(36).substr(2, 9)}`,
      additionalData: {
        deviceType: Math.random() > 0.5 ? 'Desktop' : 'Mobile',
        browser: Math.random() > 0.5 ? 'Chrome' : 'Firefox'
      }
    })
  }

  return events.sort((a, b) => b.timestamp.getTime() - a.timestamp.getTime())
}

const generateDetails = (action: string, userName: string): string => {
  switch (action) {
    case 'User Login':
      return `${userName} successfully logged into the system`
    case 'Failed Login Attempt':
      return `Failed login attempt for user ${userName} - invalid credentials`
    case 'Password Changed':
      return `${userName} changed their password`
    case '2FA Enabled':
      return `${userName} enabled two-factor authentication`
    case 'Role Assigned':
      return `Admin role assigned to ${userName}`
    case 'Data Export':
      return `${userName} exported customer data (1,234 records)`
    case 'System Configuration Changed':
      return `${userName} modified system timeout settings`
    case 'User Account Created':
      return `${userName} created a new user account`
    case 'Security Policy Updated':
      return `${userName} updated password policy requirements`
    case 'Unauthorized Access Attempt':
      return `${userName} attempted to access restricted resource without permission`
    default:
      return `${userName} performed ${action}`
  }
}

export const AuditLog: React.FC<AuditLogProps> = ({ className }) => {
  const [events] = useState<AuditEvent[]>(generateMockAuditData())
  const [searchTerm, setSearchTerm] = useState('')
  const [selectedCategory, setSelectedCategory] = useState('all')
  const [selectedRiskLevel, setSelectedRiskLevel] = useState('all')
  const [selectedUser, setSelectedUser] = useState('all')
  const [dateRange, setDateRange] = useState<{ from?: Date; to?: Date }>({})
  const [showSuccessOnly, setShowSuccessOnly] = useState(false)
  const [currentPage, setCurrentPage] = useState(1)
  const [pageSize] = useState(10)

  // Get unique values for filters
  const uniqueUsers = useMemo(() => {
    const users = new Set(events.map(e => `${e.userName}:${e.userEmail}`))
    return Array.from(users).map(u => {
      const [name, email] = u.split(':')
      return { name, email }
    })
  }, [events])

  // Filter events based on criteria
  const filteredEvents = useMemo(() => {
    return events.filter(event => {
      // Search filter
      const searchMatch = searchTerm === '' || 
        event.action.toLowerCase().includes(searchTerm.toLowerCase()) ||
        event.userName.toLowerCase().includes(searchTerm.toLowerCase()) ||
        event.details.toLowerCase().includes(searchTerm.toLowerCase()) ||
        event.resource.toLowerCase().includes(searchTerm.toLowerCase())

      // Category filter
      const categoryMatch = selectedCategory === 'all' || event.category === selectedCategory

      // Risk level filter
      const riskMatch = selectedRiskLevel === 'all' || event.riskLevel === selectedRiskLevel

      // User filter
      const userMatch = selectedUser === 'all' || `${event.userName}:${event.userEmail}` === selectedUser

      // Success filter
      const successMatch = !showSuccessOnly || event.success

      // Date range filter
      const dateMatch = !dateRange.from || !dateRange.to ||
        (event.timestamp >= dateRange.from && event.timestamp <= dateRange.to)

      return searchMatch && categoryMatch && riskMatch && userMatch && successMatch && dateMatch
    })
  }, [events, searchTerm, selectedCategory, selectedRiskLevel, selectedUser, showSuccessOnly, dateRange])

  // Paginate results
  const paginatedEvents = useMemo(() => {
    const startIndex = (currentPage - 1) * pageSize
    return filteredEvents.slice(startIndex, startIndex + pageSize)
  }, [filteredEvents, currentPage, pageSize])

  const totalPages = Math.ceil(filteredEvents.length / pageSize)

  const getRiskLevelColor = (riskLevel: string) => {
    switch (riskLevel) {
      case 'Low':
        return 'bg-green-100 text-green-800 border-green-200'
      case 'Medium':
        return 'bg-yellow-100 text-yellow-800 border-yellow-200'
      case 'High':
        return 'bg-orange-100 text-orange-800 border-orange-200'
      case 'Critical':
        return 'bg-red-100 text-red-800 border-red-200'
      default:
        return 'bg-gray-100 text-gray-800 border-gray-200'
    }
  }

  const getCategoryIcon = (category: string) => {
    switch (category) {
      case 'Authentication':
        return <Lock className="h-4 w-4" />
      case 'Authorization':
        return <Shield className="h-4 w-4" />
      case 'DataAccess':
        return <Database className="h-4 w-4" />
      case 'SystemConfig':
        return <Settings className="h-4 w-4" />
      case 'UserManagement':
        return <User className="h-4 w-4" />
      case 'Security':
        return <AlertTriangle className="h-4 w-4" />
      default:
        return <FileText className="h-4 w-4" />
    }
  }

  const exportAuditLog = () => {
    const csvContent = [
      'Timestamp,User,Action,Category,Resource,Risk Level,Success,IP Address,Location,Details',
      ...filteredEvents.map(event => 
        `"${event.timestamp.toISOString()}","${event.userName}","${event.action}","${event.category}","${event.resource}","${event.riskLevel}","${event.success}","${event.ipAddress}","${event.location}","${event.details}"`
      )
    ].join('\n')

    const blob = new Blob([csvContent], { type: 'text/csv' })
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = `audit-log-${new Date().toISOString().split('T')[0]}.csv`
    a.click()
    URL.revokeObjectURL(url)
  }

  return (
    <div className={`space-y-6 ${className}`}>
      
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-2xl font-bold">Audit Log</h2>
          <p className="text-muted-foreground">
            Comprehensive security and activity tracking
          </p>
        </div>
        <Button onClick={exportAuditLog} variant="outline">
          <Download className="h-4 w-4 mr-2" />
          Export CSV
        </Button>
      </div>

      {/* Filters */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <Filter className="h-5 w-5" />
            Filters
          </CardTitle>
        </CardHeader>
        <CardContent>
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
            
            {/* Search */}
            <div className="space-y-2">
              <label className="text-sm font-medium">Search</label>
              <div className="relative">
                <Search className="absolute left-3 top-1/2 transform -translate-y-1/2 h-4 w-4 text-muted-foreground" />
                <Input
                  placeholder="Search events..."
                  value={searchTerm}
                  onChange={(e) => setSearchTerm(e.target.value)}
                  className="pl-9"
                />
              </div>
            </div>

            {/* Category Filter */}
            <div className="space-y-2">
              <label className="text-sm font-medium">Category</label>
              <Select value={selectedCategory} onValueChange={setSelectedCategory}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">All Categories</SelectItem>
                  <SelectItem value="Authentication">Authentication</SelectItem>
                  <SelectItem value="Authorization">Authorization</SelectItem>
                  <SelectItem value="DataAccess">Data Access</SelectItem>
                  <SelectItem value="SystemConfig">System Config</SelectItem>
                  <SelectItem value="UserManagement">User Management</SelectItem>
                  <SelectItem value="Security">Security</SelectItem>
                </SelectContent>
              </Select>
            </div>

            {/* Risk Level Filter */}
            <div className="space-y-2">
              <label className="text-sm font-medium">Risk Level</label>
              <Select value={selectedRiskLevel} onValueChange={setSelectedRiskLevel}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">All Risk Levels</SelectItem>
                  <SelectItem value="Low">Low</SelectItem>
                  <SelectItem value="Medium">Medium</SelectItem>
                  <SelectItem value="High">High</SelectItem>
                  <SelectItem value="Critical">Critical</SelectItem>
                </SelectContent>
              </Select>
            </div>

            {/* User Filter */}
            <div className="space-y-2">
              <label className="text-sm font-medium">User</label>
              <Select value={selectedUser} onValueChange={setSelectedUser}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">All Users</SelectItem>
                  {uniqueUsers.map((user) => (
                    <SelectItem key={`${user.name}:${user.email}`} value={`${user.name}:${user.email}`}>
                      {user.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            {/* Date Range */}
            <div className="space-y-2 md:col-span-2">
              <label className="text-sm font-medium">Date Range</label>
              <div className="flex gap-2">
                <Popover>
                  <PopoverTrigger asChild>
                    <Button variant="outline" className="flex-1 justify-start">
                      <CalendarIcon className="mr-2 h-4 w-4" />
                      {dateRange.from ? format(dateRange.from, "MMM dd, yyyy") : "From date"}
                    </Button>
                  </PopoverTrigger>
                  <PopoverContent className="w-auto p-0">
                    <Calendar
                      mode="single"
                      selected={dateRange.from}
                      onSelect={(date) => setDateRange(prev => ({ ...prev, from: date }))}
                      disabled={(date) => date > new Date()}
                    />
                  </PopoverContent>
                </Popover>
                
                <Popover>
                  <PopoverTrigger asChild>
                    <Button variant="outline" className="flex-1 justify-start">
                      <CalendarIcon className="mr-2 h-4 w-4" />
                      {dateRange.to ? format(dateRange.to, "MMM dd, yyyy") : "To date"}
                    </Button>
                  </PopoverTrigger>
                  <PopoverContent className="w-auto p-0">
                    <Calendar
                      mode="single"
                      selected={dateRange.to}
                      onSelect={(date) => setDateRange(prev => ({ ...prev, to: date }))}
                      disabled={(date) => date > new Date() || (dateRange.from && date < dateRange.from)}
                    />
                  </PopoverContent>
                </Popover>
              </div>
            </div>

            {/* Success Only Toggle */}
            <div className="space-y-2">
              <label className="text-sm font-medium">Show</label>
              <div className="flex items-center space-x-2">
                <input
                  type="checkbox"
                  id="success-only"
                  checked={showSuccessOnly}
                  onChange={(e) => setShowSuccessOnly(e.target.checked)}
                  className="rounded border-gray-300"
                />
                <label htmlFor="success-only" className="text-sm">
                  Successful events only
                </label>
              </div>
            </div>
          </div>

          <div className="flex items-center justify-between mt-4 pt-4 border-t">
            <div className="text-sm text-muted-foreground">
              Showing {filteredEvents.length} of {events.length} events
            </div>
            <Button
              variant="outline"
              size="sm"
              onClick={() => {
                setSearchTerm('')
                setSelectedCategory('all')
                setSelectedRiskLevel('all')
                setSelectedUser('all')
                setDateRange({})
                setShowSuccessOnly(false)
                setCurrentPage(1)
              }}
            >
              Clear Filters
            </Button>
          </div>
        </CardContent>
      </Card>

      {/* Audit Events Table */}
      <Card>
        <CardHeader>
          <CardTitle>Security Events</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="space-y-4">
            {paginatedEvents.map((event) => (
              <div 
                key={event.id} 
                className="p-4 border rounded-lg hover:bg-muted/50 transition-colors"
              >
                <div className="flex items-start justify-between">
                  <div className="flex-1 space-y-2">
                    
                    {/* Header Row */}
                    <div className="flex items-center gap-3 flex-wrap">
                      <div className="flex items-center gap-2">
                        {getCategoryIcon(event.category)}
                        <span className="font-medium">{event.action}</span>
                      </div>
                      
                      <Badge className={getRiskLevelColor(event.riskLevel)}>
                        {event.riskLevel}
                      </Badge>
                      
                      <div className="flex items-center gap-1">
                        {event.success ? (
                          <CheckCircle className="h-4 w-4 text-green-600" />
                        ) : (
                          <XCircle className="h-4 w-4 text-red-600" />
                        )}
                        <span className={`text-sm ${event.success ? 'text-green-600' : 'text-red-600'}`}>
                          {event.success ? 'Success' : 'Failed'}
                        </span>
                      </div>
                      
                      <Badge variant="outline" className="text-xs">
                        {event.category}
                      </Badge>
                    </div>

                    {/* Details Row */}
                    <div className="text-sm text-muted-foreground">
                      {event.details}
                    </div>

                    {/* Meta Information */}
                    <div className="flex items-center gap-4 text-xs text-muted-foreground">
                      <div className="flex items-center gap-1">
                        <User className="h-3 w-3" />
                        {event.userName} ({event.userEmail})
                      </div>
                      
                      <div>
                        {format(event.timestamp, "MMM dd, yyyy 'at' HH:mm")}
                      </div>
                      
                      <div>
                        IP: {event.ipAddress}
                      </div>
                      
                      <div>
                        {event.location}
                      </div>
                      
                      <div>
                        {event.resource}
                      </div>
                    </div>
                  </div>

                  {/* Actions */}
                  <Button variant="ghost" size="sm">
                    <Eye className="h-4 w-4" />
                  </Button>
                </div>
              </div>
            ))}

            {paginatedEvents.length === 0 && (
              <div className="text-center py-8 text-muted-foreground">
                No audit events match your current filters.
              </div>
            )}
          </div>

          {/* Pagination */}
          {totalPages > 1 && (
            <div className="flex items-center justify-center gap-2 mt-6">
              <Button
                variant="outline"
                size="sm"
                onClick={() => setCurrentPage(prev => Math.max(1, prev - 1))}
                disabled={currentPage === 1}
              >
                Previous
              </Button>
              
              <div className="flex items-center gap-1">
                {Array.from({ length: Math.min(5, totalPages) }, (_, i) => {
                  let pageNum
                  if (totalPages <= 5) {
                    pageNum = i + 1
                  } else if (currentPage <= 3) {
                    pageNum = i + 1
                  } else if (currentPage >= totalPages - 2) {
                    pageNum = totalPages - 4 + i
                  } else {
                    pageNum = currentPage - 2 + i
                  }
                  
                  return (
                    <Button
                      key={pageNum}
                      variant={currentPage === pageNum ? "default" : "outline"}
                      size="sm"
                      onClick={() => setCurrentPage(pageNum)}
                    >
                      {pageNum}
                    </Button>
                  )
                })}
              </div>
              
              <Button
                variant="outline"
                size="sm"
                onClick={() => setCurrentPage(prev => Math.min(totalPages, prev + 1))}
                disabled={currentPage === totalPages}
              >
                Next
              </Button>
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  )
}