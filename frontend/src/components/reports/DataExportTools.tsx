"use client"

import React, { useState } from 'react'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '../ui/card'
import { Badge } from '../ui/badge'
import { Button } from '../ui/button'
import { Input } from '../ui/input'
import { Label } from '../ui/label'
import { Textarea } from '../ui/textarea'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '../ui/select'
import { Checkbox } from '../ui/checkbox'
import { 
  Download, 
  FileText, 
  Calendar, 
  Clock, 
  Database,
  Mail,
  CheckCircle,
  XCircle,
  AlertCircle,
  MoreHorizontal,
  Play,
  Pause,
  Trash2,
  Eye,
  RefreshCw,
  FileSpreadsheet,
  FileJson,
  FileImage
} from 'lucide-react'
import { useIsClient } from '../../lib/ssr-utils'

interface ExportJob {
  id: string
  name: string
  dataSource: string
  format: string
  status: 'pending' | 'running' | 'completed' | 'failed' | 'scheduled'
  progress?: number
  createdAt: string
  completedAt?: string
  fileSize?: string
  downloadUrl?: string
  schedule?: {
    frequency: string
    nextRun: string
  }
  emailRecipients?: string[]
  errorMessage?: string
}

const DataExportTools: React.FC = () => {
  const [selectedDataSource, setSelectedDataSource] = useState('')
  const [selectedFormat, setSelectedFormat] = useState('csv')
  const [exportName, setExportName] = useState('')
  const [exportDescription, setExportDescription] = useState('')
  const [includeHeaders, setIncludeHeaders] = useState(true)
  const [scheduleExport, setScheduleExport] = useState(false)
  const [scheduleFrequency, setScheduleFrequency] = useState('weekly')
  const [emailRecipients, setEmailRecipients] = useState('')
  const isClient = useIsClient()

  // Mock data sources
  const dataSources = [
    { id: 'users', name: 'Users', description: 'User accounts and profiles' },
    { id: 'transactions', name: 'Transactions', description: 'Financial transactions' },
    { id: 'products', name: 'Products', description: 'Product catalog' },
    { id: 'orders', name: 'Orders', description: 'Customer orders' },
    { id: 'analytics', name: 'Analytics Data', description: 'Performance metrics' }
  ]

  const exportFormats = [
    { 
      value: 'csv', 
      label: 'CSV', 
      icon: FileText, 
      description: 'Comma-separated values',
      extension: '.csv'
    },
    { 
      value: 'xlsx', 
      label: 'Excel', 
      icon: FileSpreadsheet, 
      description: 'Microsoft Excel format',
      extension: '.xlsx'
    },
    { 
      value: 'json', 
      label: 'JSON', 
      icon: FileJson, 
      description: 'JavaScript Object Notation',
      extension: '.json'
    },
    { 
      value: 'pdf', 
      label: 'PDF', 
      icon: FileImage, 
      description: 'Portable Document Format',
      extension: '.pdf'
    }
  ]

  const frequencies = [
    { value: 'daily', label: 'Daily' },
    { value: 'weekly', label: 'Weekly' },
    { value: 'monthly', label: 'Monthly' },
    { value: 'quarterly', label: 'Quarterly' }
  ]

  // Mock export jobs
  const exportJobs: ExportJob[] = [
    {
      id: '1',
      name: 'Monthly User Report',
      dataSource: 'users',
      format: 'csv',
      status: 'completed',
      createdAt: '2024-01-25T10:30:00',
      completedAt: '2024-01-25T10:32:00',
      fileSize: '2.3 MB',
      downloadUrl: '/downloads/monthly-users.csv',
      schedule: {
        frequency: 'monthly',
        nextRun: '2024-02-25T10:30:00'
      },
      emailRecipients: ['admin@example.com']
    },
    {
      id: '2',
      name: 'Transaction Analysis',
      dataSource: 'transactions',
      format: 'xlsx',
      status: 'running',
      progress: 75,
      createdAt: '2024-01-26T14:15:00'
    },
    {
      id: '3',
      name: 'Product Inventory',
      dataSource: 'products',
      format: 'json',
      status: 'failed',
      createdAt: '2024-01-26T09:20:00',
      errorMessage: 'Database connection timeout'
    },
    {
      id: '4',
      name: 'Weekly Analytics',
      dataSource: 'analytics',
      format: 'pdf',
      status: 'scheduled',
      createdAt: '2024-01-20T16:45:00',
      schedule: {
        frequency: 'weekly',
        nextRun: '2024-01-28T16:45:00'
      }
    },
    {
      id: '5',
      name: 'Order History Export',
      dataSource: 'orders',
      format: 'csv',
      status: 'pending',
      createdAt: '2024-01-26T15:30:00'
    }
  ]

  const getStatusIcon = (status: string) => {
    switch (status) {
      case 'completed':
        return <CheckCircle className="h-4 w-4 text-green-600" />
      case 'running':
        return <RefreshCw className="h-4 w-4 text-blue-600 animate-spin" />
      case 'failed':
        return <XCircle className="h-4 w-4 text-red-600" />
      case 'scheduled':
        return <Clock className="h-4 w-4 text-yellow-600" />
      case 'pending':
        return <AlertCircle className="h-4 w-4 text-gray-600" />
      default:
        return <AlertCircle className="h-4 w-4 text-gray-600" />
    }
  }

  const getStatusColor = (status: string) => {
    switch (status) {
      case 'completed': return 'bg-green-100 text-green-800'
      case 'running': return 'bg-blue-100 text-blue-800'
      case 'failed': return 'bg-red-100 text-red-800'
      case 'scheduled': return 'bg-yellow-100 text-yellow-800'
      case 'pending': return 'bg-gray-100 text-gray-800'
      default: return 'bg-gray-100 text-gray-800'
    }
  }

  const createExport = () => {
    const exportConfig = {
      name: exportName,
      description: exportDescription,
      dataSource: selectedDataSource,
      format: selectedFormat,
      includeHeaders,
      schedule: scheduleExport ? {
        frequency: scheduleFrequency,
        emailRecipients: emailRecipients.split(',').map(email => email.trim()).filter(Boolean)
      } : undefined
    }
    
    console.log('Creating export:', exportConfig)
    // TODO: Send to backend API to create export
    
    // Reset form
    setExportName('')
    setExportDescription('')
    setSelectedDataSource('')
    setEmailRecipients('')
  }

  const downloadExport = (jobId: string) => {
    console.log('Download export:', jobId)
    // TODO: Implement download functionality
  }

  const retryExport = (jobId: string) => {
    console.log('Retry export:', jobId)
    // TODO: Implement retry functionality
  }

  const cancelExport = (jobId: string) => {
    console.log('Cancel export:', jobId)
    // TODO: Implement cancel functionality
  }

  const deleteExport = (jobId: string) => {
    console.log('Delete export:', jobId)
    // TODO: Implement delete functionality
  }

  return (
    <div className="space-y-6">
      {/* Header */}
      <div>
        <h2 className="text-2xl font-bold">Data Export Tools</h2>
        <p className="text-muted-foreground">Export your data in various formats and schedule automated exports</p>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Export Form */}
        <div className="lg:col-span-1">
          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <Download className="h-5 w-5" />
                Create Export
              </CardTitle>
              <CardDescription>Configure a new data export</CardDescription>
            </CardHeader>
            <CardContent className="space-y-4">
              {/* Export Name */}
              <div className="space-y-2">
                <Label htmlFor="export-name">Export Name</Label>
                <Input
                  id="export-name"
                  placeholder="Enter export name"
                  value={exportName}
                  onChange={(e) => setExportName(e.target.value)}
                />
              </div>

              {/* Data Source */}
              <div className="space-y-2">
                <Label>Data Source</Label>
                <Select value={selectedDataSource} onValueChange={setSelectedDataSource}>
                  <SelectTrigger>
                    <SelectValue placeholder="Select data source" />
                  </SelectTrigger>
                  <SelectContent>
                    {dataSources.map(source => (
                      <SelectItem key={source.id} value={source.id}>
                        <div>
                          <div className="font-medium">{source.name}</div>
                          <div className="text-sm text-muted-foreground">{source.description}</div>
                        </div>
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>

              {/* Export Format */}
              <div className="space-y-2">
                <Label>Export Format</Label>
                <Select value={selectedFormat} onValueChange={setSelectedFormat}>
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {exportFormats.map(format => {
                      const Icon = format.icon
                      return (
                        <SelectItem key={format.value} value={format.value}>
                          <div className="flex items-center gap-2">
                            <Icon className="h-4 w-4" />
                            <div>
                              <div className="font-medium">{format.label}</div>
                              <div className="text-sm text-muted-foreground">{format.description}</div>
                            </div>
                          </div>
                        </SelectItem>
                      )
                    })}
                  </SelectContent>
                </Select>
              </div>

              {/* Options */}
              <div className="space-y-3">
                <div className="flex items-center space-x-2">
                  <Checkbox 
                    id="include-headers" 
                    checked={includeHeaders}
                    onCheckedChange={(checked) => setIncludeHeaders(checked as boolean)}
                  />
                  <Label htmlFor="include-headers" className="text-sm">
                    Include column headers
                  </Label>
                </div>

                <div className="flex items-center space-x-2">
                  <Checkbox 
                    id="schedule-export" 
                    checked={scheduleExport}
                    onCheckedChange={(checked) => setScheduleExport(checked as boolean)}
                  />
                  <Label htmlFor="schedule-export" className="text-sm">
                    Schedule recurring export
                  </Label>
                </div>
              </div>

              {/* Schedule Options */}
              {scheduleExport && (
                <div className="space-y-3 p-3 bg-muted rounded-lg">
                  <div className="space-y-2">
                    <Label>Frequency</Label>
                    <Select value={scheduleFrequency} onValueChange={setScheduleFrequency}>
                      <SelectTrigger>
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        {frequencies.map(freq => (
                          <SelectItem key={freq.value} value={freq.value}>
                            {freq.label}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </div>

                  <div className="space-y-2">
                    <Label htmlFor="email-recipients">Email Recipients</Label>
                    <Input
                      id="email-recipients"
                      placeholder="email1@example.com, email2@example.com"
                      value={emailRecipients}
                      onChange={(e) => setEmailRecipients(e.target.value)}
                    />
                    <p className="text-xs text-muted-foreground">
                      Separate multiple emails with commas
                    </p>
                  </div>
                </div>
              )}

              {/* Description */}
              <div className="space-y-2">
                <Label htmlFor="export-description">Description (Optional)</Label>
                <Textarea
                  id="export-description"
                  placeholder="Describe this export..."
                  value={exportDescription}
                  onChange={(e) => setExportDescription(e.target.value)}
                />
              </div>

              {/* Create Button */}
              <Button 
                onClick={createExport} 
                className="w-full"
                disabled={!exportName || !selectedDataSource}
              >
                <Download className="h-4 w-4 mr-2" />
                Create Export
              </Button>
            </CardContent>
          </Card>
        </div>

        {/* Export Jobs List */}
        <div className="lg:col-span-2">
          <Card>
            <CardHeader>
              <div className="flex items-center justify-between">
                <div>
                  <CardTitle>Export Jobs</CardTitle>
                  <CardDescription>View and manage your data exports</CardDescription>
                </div>
                <Button variant="outline" size="sm">
                  <RefreshCw className="h-4 w-4 mr-2" />
                  Refresh
                </Button>
              </div>
            </CardHeader>
            <CardContent>
              <div className="space-y-4">
                {exportJobs.map(job => {
                  const format = exportFormats.find(f => f.value === job.format)
                  const FormatIcon = format?.icon || FileText
                  
                  return (
                    <div key={job.id} className="flex items-center justify-between p-4 border rounded-lg">
                      <div className="flex items-start gap-3 flex-1">
                        <div className="flex items-center gap-2">
                          <FormatIcon className="h-5 w-5 text-muted-foreground" />
                          {getStatusIcon(job.status)}
                        </div>
                        
                        <div className="flex-1">
                          <div className="flex items-center gap-2">
                            <h4 className="font-medium">{job.name}</h4>
                            <Badge 
                              variant="secondary" 
                              className={getStatusColor(job.status)}
                            >
                              {job.status}
                            </Badge>
                            {job.schedule && (
                              <Badge variant="outline">
                                <Calendar className="h-3 w-3 mr-1" />
                                {job.schedule.frequency}
                              </Badge>
                            )}
                          </div>
                          
                          <div className="text-sm text-muted-foreground mt-1">
                            {dataSources.find(ds => ds.id === job.dataSource)?.name} • {format?.label}
                            {job.fileSize && ` • ${job.fileSize}`}
                          </div>
                          
                          <div className="text-xs text-muted-foreground mt-1">
                            Created: {isClient ? new Date(job.createdAt).toLocaleString() : new Date(job.createdAt).toISOString().split('T')[0]}
                            {job.completedAt && (
                              <span> • Completed: {isClient ? new Date(job.completedAt).toLocaleString() : new Date(job.completedAt).toISOString().split('T')[0]}</span>
                            )}
                          </div>
                          
                          {job.status === 'running' && job.progress && (
                            <div className="mt-2">
                              <div className="flex items-center gap-2 text-sm">
                                <div className="flex-1 bg-gray-200 rounded-full h-2">
                                  <div 
                                    className="bg-blue-600 h-2 rounded-full" 
                                    style={{ width: `${job.progress}%` }}
                                  />
                                </div>
                                <span className="text-muted-foreground">{job.progress}%</span>
                              </div>
                            </div>
                          )}
                          
                          {job.errorMessage && (
                            <div className="text-sm text-red-600 mt-2">
                              Error: {job.errorMessage}
                            </div>
                          )}
                          
                          {job.schedule?.nextRun && (
                            <div className="text-sm text-muted-foreground mt-1">
                              Next run: {isClient ? new Date(job.schedule.nextRun).toLocaleString() : new Date(job.schedule.nextRun).toISOString().split('T')[0]}
                            </div>
                          )}
                        </div>
                      </div>
                      
                      <div className="flex items-center gap-2">
                        {job.status === 'completed' && job.downloadUrl && (
                          <Button
                            variant="outline"
                            size="sm"
                            onClick={() => downloadExport(job.id)}
                          >
                            <Download className="h-3 w-3 mr-1" />
                            Download
                          </Button>
                        )}
                        
                        {job.status === 'failed' && (
                          <Button
                            variant="outline"
                            size="sm"
                            onClick={() => retryExport(job.id)}
                          >
                            <RefreshCw className="h-3 w-3 mr-1" />
                            Retry
                          </Button>
                        )}
                        
                        {job.status === 'running' && (
                          <Button
                            variant="outline"
                            size="sm"
                            onClick={() => cancelExport(job.id)}
                          >
                            <Pause className="h-3 w-3 mr-1" />
                            Cancel
                          </Button>
                        )}
                        
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={() => deleteExport(job.id)}
                          className="text-red-600 hover:text-red-700"
                        >
                          <Trash2 className="h-3 w-3" />
                        </Button>
                      </div>
                    </div>
                  )
                })}
                
                {exportJobs.length === 0 && (
                  <div className="text-center py-8 text-muted-foreground">
                    <Database className="h-12 w-12 mx-auto mb-4" />
                    <h3 className="text-lg font-medium mb-2">No exports yet</h3>
                    <p>Create your first data export to get started.</p>
                  </div>
                )}
              </div>
            </CardContent>
          </Card>
        </div>
      </div>

      {/* Quick Stats */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <Card>
          <CardContent className="p-4 text-center">
            <div className="text-2xl font-bold">{exportJobs.length}</div>
            <div className="text-sm text-muted-foreground">Total Exports</div>
          </CardContent>
        </Card>
        
        <Card>
          <CardContent className="p-4 text-center">
            <div className="text-2xl font-bold">
              {exportJobs.filter(job => job.status === 'completed').length}
            </div>
            <div className="text-sm text-muted-foreground">Completed</div>
          </CardContent>
        </Card>
        
        <Card>
          <CardContent className="p-4 text-center">
            <div className="text-2xl font-bold">
              {exportJobs.filter(job => job.schedule).length}
            </div>
            <div className="text-sm text-muted-foreground">Scheduled</div>
          </CardContent>
        </Card>
        
        <Card>
          <CardContent className="p-4 text-center">
            <div className="text-2xl font-bold">
              {exportJobs.filter(job => job.status === 'failed').length}
            </div>
            <div className="text-sm text-muted-foreground">Failed</div>
          </CardContent>
        </Card>
      </div>
    </div>
  )
}

export default DataExportTools