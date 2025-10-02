"use client"

import React, { useState, useMemo } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Card, CardContent, CardHeader, CardTitle } from '../ui/card'
import { Button } from '../ui/button'
import { Input } from '../ui/input'
import { Badge } from '../ui/badge'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '../ui/select'
import { securityService, type AuditLogEntry, type AuditLogFilter } from '../../services/security'
import { 
  Search, 
  Filter, 
  Download, 
  Shield,
  User,
  Settings,
  Database,
  Lock,
  AlertTriangle,
  CheckCircle,
  XCircle,
  FileText,
  Loader2,
  RefreshCw
} from 'lucide-react'

interface AuditLogProps {
  className?: string
}

export const AuditLog: React.FC<AuditLogProps> = ({ className }) => {
  const [searchTerm, setSearchTerm] = useState('')
  const [selectedAction, setSelectedAction] = useState('all')
  const [selectedRiskLevel, setSelectedRiskLevel] = useState('all')
  const [selectedUser, setSelectedUser] = useState('all')
  const [selectedResult, setSelectedResult] = useState('all')
  const [currentPage, setCurrentPage] = useState(1)
  const [pageSize] = useState(20)
  const [sortBy, setSortBy] = useState('timestamp')
  const [sortOrder, setSortOrder] = useState<'asc' | 'desc'>('desc')

  // Build filter for API call
  const filter: AuditLogFilter = useMemo(() => ({
    searchTerm: searchTerm || undefined,
    action: selectedAction !== 'all' ? selectedAction : undefined,
    risk: selectedRiskLevel !== 'all' ? selectedRiskLevel as 'low' | 'medium' | 'high' | 'critical' : undefined,
    userName: selectedUser !== 'all' ? selectedUser : undefined,
    result: selectedResult !== 'all' ? selectedResult as 'success' | 'failure' | 'warning' : undefined,
    page: currentPage,
    pageSize,
    sortBy,
    sortOrder
  }), [searchTerm, selectedAction, selectedRiskLevel, selectedUser, selectedResult, currentPage, pageSize, sortBy, sortOrder])

  // Fetch audit logs with real-time updates
  const { data: auditData, isLoading, error, refetch } = useQuery({
    queryKey: ['auditLogs', filter],
    queryFn: () => securityService.getAuditLogs(filter),
    refetchInterval: 30000, // Refresh every 30 seconds
  })

  // Use real audit data
  const entries = auditData?.entries || []
  const totalPages = auditData?.totalPages || 1
  const hasNext = auditData?.hasNext || false
  const hasPrevious = auditData?.hasPrevious || false
  
  // Get unique values for filters from current data
  const uniqueUsers = useMemo(() => {
    const users = new Set(entries.map(e => e.userName))
    return Array.from(users)
  }, [entries])

  const uniqueActions = useMemo(() => {
    const actions = new Set(entries.map(e => e.action))
    return Array.from(actions)
  }, [entries])

  const getRiskLevelColor = (riskLevel: string) => {
    switch (riskLevel.toLowerCase()) {
      case 'low':
        return 'bg-green-100 text-green-800 border-green-200'
      case 'medium':
        return 'bg-yellow-100 text-yellow-800 border-yellow-200'
      case 'high':
        return 'bg-orange-100 text-orange-800 border-orange-200'
      case 'critical':
        return 'bg-red-100 text-red-800 border-red-200'
      default:
        return 'bg-gray-100 text-gray-800 border-gray-200'
    }
  }

  const getResultIcon = (result: string) => {
    switch (result.toLowerCase()) {
      case 'success':
        return <CheckCircle className="h-4 w-4 text-green-600" />
      case 'failure':
        return <XCircle className="h-4 w-4 text-red-600" />
      case 'warning':
        return <AlertTriangle className="h-4 w-4 text-yellow-600" />
      default:
        return <FileText className="h-4 w-4 text-gray-600" />
    }
  }

  const exportAuditLog = () => {
    const csvContent = [
      'Timestamp,User,Email,Action,Resource,Risk Level,Result,IP Address,Location,Details',
      ...entries.map(entry => 
        `"${entry.timestamp.toISOString()}","${entry.userName}","${entry.userEmail}","${entry.action}","${entry.resource}","${entry.risk}","${entry.result}","${entry.ipAddress}","${entry.location || ''}","${entry.details}"`
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

  const formatTimestamp = (timestamp: Date) => {
    return new Intl.DateTimeFormat('en-US', {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit'
    }).format(timestamp)
  }

  if (error) {
    return (
      <div className={`space-y-6 ${className}`}>
        <Card>
          <CardContent className="flex items-center justify-center p-8">
            <div className="text-center space-y-4">
              <AlertTriangle className="h-12 w-12 text-red-500 mx-auto" />
              <div>
                <h3 className="text-lg font-semibold">Error Loading Audit Logs</h3>
                <p className="text-muted-foreground">Failed to fetch audit log data</p>
                <Button onClick={() => refetch()} className="mt-4">
                  <RefreshCw className="h-4 w-4 mr-2" />
                  Retry
                </Button>
              </div>
            </div>
          </CardContent>
        </Card>
      </div>
    )
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
        <div className="flex items-center gap-2">
          <Button onClick={() => refetch()} variant="outline" size="sm">
            <RefreshCw className="h-4 w-4 mr-2" />
            Refresh
          </Button>
          <Button onClick={exportAuditLog} variant="outline">
            <Download className="h-4 w-4 mr-2" />
            Export CSV
          </Button>
        </div>
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
                  placeholder="Search actions, users, or details..."
                  value={searchTerm}
                  onChange={(e) => setSearchTerm(e.target.value)}
                  className="pl-10"
                />
              </div>
            </div>

            {/* Action Filter */}
            <div className="space-y-2">
              <label className="text-sm font-medium">Action</label>
              <Select value={selectedAction} onValueChange={setSelectedAction}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">All Actions</SelectItem>
                  {uniqueActions.map(action => (
                    <SelectItem key={action} value={action}>{action}</SelectItem>
                  ))}
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
                  <SelectItem value="low">Low</SelectItem>
                  <SelectItem value="medium">Medium</SelectItem>
                  <SelectItem value="high">High</SelectItem>
                  <SelectItem value="critical">Critical</SelectItem>
                </SelectContent>
              </Select>
            </div>

            {/* Result Filter */}
            <div className="space-y-2">
              <label className="text-sm font-medium">Result</label>
              <Select value={selectedResult} onValueChange={setSelectedResult}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">All Results</SelectItem>
                  <SelectItem value="success">Success</SelectItem>
                  <SelectItem value="failure">Failure</SelectItem>
                  <SelectItem value="warning">Warning</SelectItem>
                </SelectContent>
              </Select>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Audit Log Table */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center justify-between">
            <span>Audit Events</span>
            {auditData && (
              <span className="text-sm font-normal text-muted-foreground">
                {auditData.total} total events
              </span>
            )}
          </CardTitle>
        </CardHeader>
        <CardContent>
          {isLoading ? (
            <div className="flex items-center justify-center p-8">
              <div className="text-center space-y-4">
                <Loader2 className="h-8 w-8 animate-spin mx-auto" />
                <p className="text-muted-foreground">Loading audit logs...</p>
              </div>
            </div>
          ) : entries.length === 0 ? (
            <div className="text-center py-8 text-muted-foreground">
              <FileText className="h-12 w-12 mx-auto mb-2" />
              <p>No audit events found</p>
            </div>
          ) : (
            <div className="space-y-4">
              {entries.map((entry) => (
                <div key={entry.id} className="border rounded-lg p-4 space-y-3">
                  <div className="flex items-start justify-between">
                    <div className="flex items-start space-x-3">
                      {getResultIcon(entry.result)}
                      <div className="flex-1">
                        <div className="flex items-center gap-2 mb-1">
                          <h4 className="font-medium">{entry.action}</h4>
                          <Badge className={`text-xs ${getRiskLevelColor(entry.risk)}`}>
                            {entry.risk.toUpperCase()}
                          </Badge>
                          <Badge variant={entry.result === 'success' ? 'outline' : 'destructive'} className="text-xs">
                            {entry.result.toUpperCase()}
                          </Badge>
                        </div>
                        <p className="text-sm text-muted-foreground mb-2">{entry.details}</p>
                        <div className="flex items-center gap-4 text-xs text-muted-foreground">
                          <span className="flex items-center gap-1">
                            <User className="h-3 w-3" />
                            {entry.userName} ({entry.userEmail})
                          </span>
                          <span>Resource: {entry.resource}</span>
                          <span>IP: {entry.ipAddress}</span>
                          {entry.location && <span>Location: {entry.location}</span>}
                        </div>
                      </div>
                    </div>
                    <div className="text-xs text-muted-foreground text-right">
                      {formatTimestamp(entry.timestamp)}
                    </div>
                  </div>
                </div>
              ))}
            </div>
          )}
        </CardContent>
      </Card>

      {/* Pagination */}
      {totalPages > 1 && (
        <div className="flex items-center justify-between">
          <div className="text-sm text-muted-foreground">
            Page {currentPage} of {totalPages}
          </div>
          <div className="flex items-center gap-2">
            <Button
              variant="outline"
              size="sm"
              onClick={() => setCurrentPage(currentPage - 1)}
              disabled={!hasPrevious}
            >
              Previous
            </Button>
            <Button
              variant="outline"
              size="sm"
              onClick={() => setCurrentPage(currentPage + 1)}
              disabled={!hasNext}
            >
              Next
            </Button>
          </div>
        </div>
      )}
    </div>
  )
}