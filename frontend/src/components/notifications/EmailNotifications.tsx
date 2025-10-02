"use client"

import React, { useState } from 'react'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '../ui/card'
import { Badge } from '../ui/badge'
import { Button } from '../ui/button'
import { Input } from '../ui/input'
import { Label } from '../ui/label'
import { Textarea } from '../ui/textarea'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '../ui/select'
import { Switch } from '../ui/switch'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '../ui/tabs'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '../ui/dialog'
import { 
  Mail, 
  Plus, 
  Search, 
  Filter, 
  Send,
  Clock,
  Users,
  Eye,
  Edit,
  Trash2,
  MoreHorizontal,
  Calendar,
  CheckCircle,
  XCircle,
  AlertCircle,
  RefreshCw,
  FileText,
  Settings
} from 'lucide-react'
import { 
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger
} from '../ui/dropdown-menu'

interface EmailTemplate {
  id: string
  name: string
  subject: string
  content: string
  type: 'welcome' | 'notification' | 'alert' | 'reminder' | 'marketing'
  isActive: boolean
  createdAt: Date
  lastUsed?: Date
  usageCount: number
  variables: string[]
}

interface EmailCampaign {
  id: string
  name: string
  templateId: string
  templateName: string
  status: 'draft' | 'scheduled' | 'sending' | 'sent' | 'failed'
  recipients: number
  sentCount: number
  openRate?: number
  clickRate?: number
  scheduledAt?: Date
  sentAt?: Date
  createdBy: string
}

const EmailNotifications: React.FC = () => {
  const [activeTab, setActiveTab] = useState('campaigns')
  const [searchQuery, setSearchQuery] = useState('')
  const [isCreateDialogOpen, setIsCreateDialogOpen] = useState(false)
  const [selectedTemplate, setSelectedTemplate] = useState<EmailTemplate | null>(null)
  const [isTemplateDialogOpen, setIsTemplateDialogOpen] = useState(false)

  // Mock data
  const emailTemplates: EmailTemplate[] = [
    {
      id: '1',
      name: 'Welcome Email',
      subject: 'Welcome to {{company_name}}!',
      content: 'Dear {{user_name}},\n\nWelcome to our platform! We\'re excited to have you on board.\n\nBest regards,\nThe {{company_name}} Team',
      type: 'welcome',
      isActive: true,
      createdAt: new Date('2024-01-15'),
      lastUsed: new Date('2024-01-25'),
      usageCount: 45,
      variables: ['user_name', 'company_name', 'activation_link']
    },
    {
      id: '2',
      name: 'Password Reset',
      subject: 'Reset Your Password',
      content: 'Hi {{user_name}},\n\nClick the link below to reset your password:\n{{reset_link}}\n\nThis link expires in 24 hours.',
      type: 'notification',
      isActive: true,
      createdAt: new Date('2024-01-10'),
      lastUsed: new Date('2024-01-26'),
      usageCount: 23,
      variables: ['user_name', 'reset_link', 'expiry_time']
    },
    {
      id: '3',
      name: 'Invoice Due Reminder',
      subject: 'Invoice {{invoice_number}} Due Soon',
      content: 'Dear {{client_name}},\n\nThis is a reminder that invoice {{invoice_number}} for ${{amount}} is due on {{due_date}}.\n\nPlease make payment to avoid late fees.',
      type: 'reminder',
      isActive: true,
      createdAt: new Date('2024-01-08'),
      lastUsed: new Date('2024-01-24'),
      usageCount: 156,
      variables: ['client_name', 'invoice_number', 'amount', 'due_date']
    },
    {
      id: '4',
      name: 'System Alert',
      subject: 'System Alert: {{alert_type}}',
      content: 'Alert: {{alert_message}}\n\nTimestamp: {{timestamp}}\nSeverity: {{severity}}\n\nPlease take appropriate action.',
      type: 'alert',
      isActive: true,
      createdAt: new Date('2024-01-12'),
      usageCount: 67,
      variables: ['alert_type', 'alert_message', 'timestamp', 'severity']
    }
  ]

  const emailCampaigns: EmailCampaign[] = [
    {
      id: '1',
      name: 'Monthly Newsletter',
      templateId: '1',
      templateName: 'Newsletter Template',
      status: 'sent',
      recipients: 1250,
      sentCount: 1245,
      openRate: 23.4,
      clickRate: 4.2,
      sentAt: new Date('2024-01-25T10:00:00'),
      createdBy: 'John Admin'
    },
    {
      id: '2',
      name: 'Welcome Campaign',
      templateId: '1',
      templateName: 'Welcome Email',
      status: 'sending',
      recipients: 45,
      sentCount: 32,
      scheduledAt: new Date('2024-01-26T14:30:00'),
      createdBy: 'Sarah Marketing'
    },
    {
      id: '3',
      name: 'Payment Reminders',
      templateId: '3',
      templateName: 'Invoice Due Reminder',
      status: 'scheduled',
      recipients: 89,
      sentCount: 0,
      scheduledAt: new Date('2024-01-27T09:00:00'),
      createdBy: 'Mike Finance'
    },
    {
      id: '4',
      name: 'System Maintenance',
      templateId: '4',
      templateName: 'System Alert',
      status: 'draft',
      recipients: 2000,
      sentCount: 0,
      createdBy: 'Admin User'
    }
  ]

  const getStatusIcon = (status: string) => {
    switch (status) {
      case 'sent': return <CheckCircle className="h-4 w-4 text-green-600" />
      case 'sending': return <RefreshCw className="h-4 w-4 text-blue-600 animate-spin" />
      case 'scheduled': return <Clock className="h-4 w-4 text-yellow-600" />
      case 'failed': return <XCircle className="h-4 w-4 text-red-600" />
      default: return <FileText className="h-4 w-4 text-gray-600" />
    }
  }

  const getStatusColor = (status: string) => {
    switch (status) {
      case 'sent': return 'bg-green-100 text-green-800'
      case 'sending': return 'bg-blue-100 text-blue-800'
      case 'scheduled': return 'bg-yellow-100 text-yellow-800'
      case 'failed': return 'bg-red-100 text-red-800'
      default: return 'bg-gray-100 text-gray-800'
    }
  }

  const getTypeColor = (type: string) => {
    switch (type) {
      case 'welcome': return 'bg-blue-100 text-blue-800'
      case 'notification': return 'bg-green-100 text-green-800'
      case 'alert': return 'bg-red-100 text-red-800'
      case 'reminder': return 'bg-yellow-100 text-yellow-800'
      case 'marketing': return 'bg-purple-100 text-purple-800'
      default: return 'bg-gray-100 text-gray-800'
    }
  }

  const filteredCampaigns = emailCampaigns.filter(campaign =>
    campaign.name.toLowerCase().includes(searchQuery.toLowerCase()) ||
    campaign.templateName.toLowerCase().includes(searchQuery.toLowerCase())
  )

  const filteredTemplates = emailTemplates.filter(template =>
    template.name.toLowerCase().includes(searchQuery.toLowerCase()) ||
    template.subject.toLowerCase().includes(searchQuery.toLowerCase())
  )

  return (
    <div className="space-y-6">
      <Tabs value={activeTab} onValueChange={setActiveTab} className="space-y-6">
        <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4">
          <TabsList>
            <TabsTrigger value="campaigns" className="flex items-center gap-2">
              <Mail className="h-4 w-4" />
              Email Campaigns
            </TabsTrigger>
            <TabsTrigger value="templates" className="flex items-center gap-2">
              <FileText className="h-4 w-4" />
              Templates
            </TabsTrigger>
            <TabsTrigger value="settings" className="flex items-center gap-2">
              <Settings className="h-4 w-4" />
              Email Settings
            </TabsTrigger>
          </TabsList>
          
          <div className="flex items-center gap-3">
            <div className="relative">
              <Search className="h-4 w-4 absolute left-3 top-3 text-muted-foreground" />
              <Input
                placeholder="Search..."
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
                className="pl-10 w-64"
              />
            </div>
            
            <Button onClick={() => setIsCreateDialogOpen(true)}>
              <Plus className="h-4 w-4 mr-2" />
              Create Campaign
            </Button>
          </div>
        </div>

        <TabsContent value="campaigns" className="space-y-6">
          {/* Campaign Statistics */}
          <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
            <Card>
              <CardContent className="p-4">
                <div className="flex items-center justify-between">
                  <div>
                    <p className="text-sm font-medium text-muted-foreground">Total Campaigns</p>
                    <p className="text-2xl font-bold">{emailCampaigns.length}</p>
                  </div>
                  <Mail className="h-8 w-8 text-blue-600" />
                </div>
              </CardContent>
            </Card>
            
            <Card>
              <CardContent className="p-4">
                <div className="flex items-center justify-between">
                  <div>
                    <p className="text-sm font-medium text-muted-foreground">Emails Sent</p>
                    <p className="text-2xl font-bold">
                      {emailCampaigns.reduce((sum, c) => sum + c.sentCount, 0).toLocaleString()}
                    </p>
                  </div>
                  <Send className="h-8 w-8 text-green-600" />
                </div>
              </CardContent>
            </Card>
            
            <Card>
              <CardContent className="p-4">
                <div className="flex items-center justify-between">
                  <div>
                    <p className="text-sm font-medium text-muted-foreground">Avg Open Rate</p>
                    <p className="text-2xl font-bold">23.4%</p>
                  </div>
                  <Eye className="h-8 w-8 text-purple-600" />
                </div>
              </CardContent>
            </Card>
            
            <Card>
              <CardContent className="p-4">
                <div className="flex items-center justify-between">
                  <div>
                    <p className="text-sm font-medium text-muted-foreground">Active</p>
                    <p className="text-2xl font-bold">
                      {emailCampaigns.filter(c => c.status === 'sending' || c.status === 'scheduled').length}
                    </p>
                  </div>
                  <Clock className="h-8 w-8 text-orange-600" />
                </div>
              </CardContent>
            </Card>
          </div>

          {/* Campaigns List */}
          <Card>
            <CardHeader>
              <CardTitle>Email Campaigns</CardTitle>
              <CardDescription>Manage and monitor your email campaigns</CardDescription>
            </CardHeader>
            <CardContent>
              <div className="space-y-4">
                {filteredCampaigns.map((campaign) => (
                  <div key={campaign.id} className="flex items-center justify-between p-4 border rounded-lg hover:bg-muted/50">
                    <div className="flex items-start gap-4 flex-1">
                      <div className="mt-1">
                        {getStatusIcon(campaign.status)}
                      </div>
                      
                      <div className="flex-1 min-w-0">
                        <div className="flex items-center gap-2 mb-1">
                          <h4 className="font-medium">{campaign.name}</h4>
                          <Badge variant="outline" className={getStatusColor(campaign.status)}>
                            {campaign.status}
                          </Badge>
                        </div>
                        
                        <p className="text-sm text-muted-foreground mb-2">
                          Template: {campaign.templateName}
                        </p>
                        
                        <div className="flex items-center gap-4 text-xs text-muted-foreground">
                          <span>{campaign.recipients} recipients</span>
                          {campaign.sentCount > 0 && (
                            <span>{campaign.sentCount} sent</span>
                          )}
                          {campaign.openRate && (
                            <span>{campaign.openRate}% open rate</span>
                          )}
                          {campaign.clickRate && (
                            <span>{campaign.clickRate}% click rate</span>
                          )}
                        </div>
                        
                        <div className="text-xs text-muted-foreground mt-1">
                          {campaign.scheduledAt && (
                            <span>Scheduled: {campaign.scheduledAt.toLocaleString()}</span>
                          )}
                          {campaign.sentAt && (
                            <span>Sent: {campaign.sentAt.toLocaleString()}</span>
                          )}
                          <span className="ml-4">By: {campaign.createdBy}</span>
                        </div>
                      </div>
                    </div>
                    
                    <div className="flex items-center gap-2">
                      <Button variant="outline" size="sm">
                        <Eye className="h-4 w-4 mr-2" />
                        View
                      </Button>
                      
                      <DropdownMenu>
                        <DropdownMenuTrigger asChild>
                          <Button variant="ghost" size="sm">
                            <MoreHorizontal className="h-4 w-4" />
                          </Button>
                        </DropdownMenuTrigger>
                        <DropdownMenuContent align="end">
                          <DropdownMenuItem>
                            <Edit className="h-4 w-4 mr-2" />
                            Edit
                          </DropdownMenuItem>
                          <DropdownMenuItem>
                            <Send className="h-4 w-4 mr-2" />
                            Send Test
                          </DropdownMenuItem>
                          <DropdownMenuItem>
                            <Calendar className="h-4 w-4 mr-2" />
                            Reschedule
                          </DropdownMenuItem>
                          <DropdownMenuSeparator />
                          <DropdownMenuItem className="text-red-600">
                            <Trash2 className="h-4 w-4 mr-2" />
                            Delete
                          </DropdownMenuItem>
                        </DropdownMenuContent>
                      </DropdownMenu>
                    </div>
                  </div>
                ))}
              </div>
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="templates" className="space-y-6">
          {/* Template Statistics */}
          <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
            <Card>
              <CardContent className="p-4">
                <div className="flex items-center justify-between">
                  <div>
                    <p className="text-sm font-medium text-muted-foreground">Total Templates</p>
                    <p className="text-2xl font-bold">{emailTemplates.length}</p>
                  </div>
                  <FileText className="h-8 w-8 text-blue-600" />
                </div>
              </CardContent>
            </Card>
            
            <Card>
              <CardContent className="p-4">
                <div className="flex items-center justify-between">
                  <div>
                    <p className="text-sm font-medium text-muted-foreground">Active</p>
                    <p className="text-2xl font-bold">
                      {emailTemplates.filter(t => t.isActive).length}
                    </p>
                  </div>
                  <CheckCircle className="h-8 w-8 text-green-600" />
                </div>
              </CardContent>
            </Card>
            
            <Card>
              <CardContent className="p-4">
                <div className="flex items-center justify-between">
                  <div>
                    <p className="text-sm font-medium text-muted-foreground">Most Used</p>
                    <p className="text-2xl font-bold">
                      {Math.max(...emailTemplates.map(t => t.usageCount))}
                    </p>
                  </div>
                  <Users className="h-8 w-8 text-purple-600" />
                </div>
              </CardContent>
            </Card>
            
            <Card>
              <CardContent className="p-4">
                <div className="flex items-center justify-between">
                  <div>
                    <p className="text-sm font-medium text-muted-foreground">Variables</p>
                    <p className="text-2xl font-bold">
                      {Array.from(new Set(emailTemplates.flatMap(t => t.variables))).length}
                    </p>
                  </div>
                  <Settings className="h-8 w-8 text-orange-600" />
                </div>
              </CardContent>
            </Card>
          </div>

          {/* Templates Grid */}
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
            {filteredTemplates.map((template) => (
              <Card key={template.id} className="hover:shadow-md transition-shadow">
                <CardHeader className="pb-3">
                  <div className="flex items-start justify-between">
                    <div>
                      <CardTitle className="text-lg">{template.name}</CardTitle>
                      <div className="flex items-center gap-2 mt-1">
                        <Badge variant="outline" className={getTypeColor(template.type)}>
                          {template.type}
                        </Badge>
                        {template.isActive ? (
                          <Badge variant="outline" className="text-green-600">Active</Badge>
                        ) : (
                          <Badge variant="outline" className="text-gray-600">Inactive</Badge>
                        )}
                      </div>
                    </div>
                    
                    <DropdownMenu>
                      <DropdownMenuTrigger asChild>
                        <Button variant="ghost" size="sm">
                          <MoreHorizontal className="h-4 w-4" />
                        </Button>
                      </DropdownMenuTrigger>
                      <DropdownMenuContent align="end">
                        <DropdownMenuItem onClick={() => {
                          setSelectedTemplate(template)
                          setIsTemplateDialogOpen(true)
                        }}>
                          <Edit className="h-4 w-4 mr-2" />
                          Edit
                        </DropdownMenuItem>
                        <DropdownMenuItem>
                          <Send className="h-4 w-4 mr-2" />
                          Send Test
                        </DropdownMenuItem>
                        <DropdownMenuSeparator />
                        <DropdownMenuItem className="text-red-600">
                          <Trash2 className="h-4 w-4 mr-2" />
                          Delete
                        </DropdownMenuItem>
                      </DropdownMenuContent>
                    </DropdownMenu>
                  </div>
                </CardHeader>
                
                <CardContent className="space-y-3">
                  <div>
                    <Label className="text-xs font-medium text-muted-foreground">Subject:</Label>
                    <p className="text-sm">{template.subject}</p>
                  </div>
                  
                  <div>
                    <Label className="text-xs font-medium text-muted-foreground">Content Preview:</Label>
                    <p className="text-sm text-muted-foreground line-clamp-3">
                      {template.content.length > 100 
                        ? template.content.substring(0, 100) + '...' 
                        : template.content
                      }
                    </p>
                  </div>
                  
                  <div className="flex items-center justify-between text-xs text-muted-foreground">
                    <span>Used {template.usageCount} times</span>
                    {template.lastUsed && (
                      <span>Last used: {template.lastUsed.toLocaleDateString()}</span>
                    )}
                  </div>
                  
                  {template.variables.length > 0 && (
                    <div>
                      <Label className="text-xs font-medium text-muted-foreground">Variables:</Label>
                      <div className="flex flex-wrap gap-1 mt-1">
                        {template.variables.slice(0, 3).map((variable) => (
                          <Badge key={variable} variant="secondary" className="text-xs">
                            {variable}
                          </Badge>
                        ))}
                        {template.variables.length > 3 && (
                          <Badge variant="outline" className="text-xs">
                            +{template.variables.length - 3} more
                          </Badge>
                        )}
                      </div>
                    </div>
                  )}
                </CardContent>
              </Card>
            ))}
          </div>
        </TabsContent>

        <TabsContent value="settings" className="space-y-6">
          <Card>
            <CardHeader>
              <CardTitle>Email Settings</CardTitle>
              <CardDescription>Configure your email delivery and SMTP settings</CardDescription>
            </CardHeader>
            <CardContent className="space-y-6">
              {/* SMTP Settings */}
              <div className="space-y-4">
                <h3 className="text-lg font-medium">SMTP Configuration</h3>
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <div className="space-y-2">
                    <Label htmlFor="smtp-host">SMTP Host</Label>
                    <Input id="smtp-host" placeholder="smtp.gmail.com" />
                  </div>
                  <div className="space-y-2">
                    <Label htmlFor="smtp-port">SMTP Port</Label>
                    <Input id="smtp-port" placeholder="587" type="number" />
                  </div>
                  <div className="space-y-2">
                    <Label htmlFor="smtp-username">Username</Label>
                    <Input id="smtp-username" placeholder="your-email@gmail.com" />
                  </div>
                  <div className="space-y-2">
                    <Label htmlFor="smtp-password">Password</Label>
                    <Input id="smtp-password" type="password" placeholder="••••••••" />
                  </div>
                </div>
                
                <div className="flex items-center space-x-2">
                  <Switch id="smtp-ssl" />
                  <Label htmlFor="smtp-ssl">Use SSL/TLS encryption</Label>
                </div>
              </div>

              {/* Default Settings */}
              <div className="space-y-4 border-t pt-6">
                <h3 className="text-lg font-medium">Default Settings</h3>
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <div className="space-y-2">
                    <Label htmlFor="from-name">Default From Name</Label>
                    <Input id="from-name" placeholder="Your Company" />
                  </div>
                  <div className="space-y-2">
                    <Label htmlFor="from-email">Default From Email</Label>
                    <Input id="from-email" placeholder="noreply@yourcompany.com" />
                  </div>
                  <div className="space-y-2">
                    <Label htmlFor="reply-to">Reply To Email</Label>
                    <Input id="reply-to" placeholder="support@yourcompany.com" />
                  </div>
                  <div className="space-y-2">
                    <Label htmlFor="bcc-email">BCC Email (Optional)</Label>
                    <Input id="bcc-email" placeholder="admin@yourcompany.com" />
                  </div>
                </div>
              </div>

              {/* Rate Limiting */}
              <div className="space-y-4 border-t pt-6">
                <h3 className="text-lg font-medium">Rate Limiting</h3>
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <div className="space-y-2">
                    <Label htmlFor="rate-limit">Emails per minute</Label>
                    <Input id="rate-limit" placeholder="60" type="number" />
                  </div>
                  <div className="space-y-2">
                    <Label htmlFor="daily-limit">Daily email limit</Label>
                    <Input id="daily-limit" placeholder="1000" type="number" />
                  </div>
                </div>
              </div>

              <Button>Save Email Settings</Button>
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>

      {/* Create Campaign Dialog */}
      <Dialog open={isCreateDialogOpen} onOpenChange={setIsCreateDialogOpen}>
        <DialogContent className="max-w-2xl">
          <DialogHeader>
            <DialogTitle>Create Email Campaign</DialogTitle>
            <DialogDescription>
              Create a new email campaign using one of your templates
            </DialogDescription>
          </DialogHeader>
          
          <div className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="campaign-name">Campaign Name</Label>
              <Input id="campaign-name" placeholder="Enter campaign name" />
            </div>
            
            <div className="space-y-2">
              <Label htmlFor="template-select">Email Template</Label>
              <Select>
                <SelectTrigger>
                  <SelectValue placeholder="Select a template" />
                </SelectTrigger>
                <SelectContent>
                  {emailTemplates.map((template) => (
                    <SelectItem key={template.id} value={template.id}>
                      {template.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            
            <div className="space-y-2">
              <Label htmlFor="recipients">Recipients</Label>
              <Select>
                <SelectTrigger>
                  <SelectValue placeholder="Select recipient group" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all-users">All Users</SelectItem>
                  <SelectItem value="active-users">Active Users</SelectItem>
                  <SelectItem value="new-users">New Users</SelectItem>
                  <SelectItem value="custom">Custom List</SelectItem>
                </SelectContent>
              </Select>
            </div>
            
            <div className="space-y-2">
              <Label htmlFor="schedule">Schedule</Label>
              <Select>
                <SelectTrigger>
                  <SelectValue placeholder="When to send" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="now">Send Now</SelectItem>
                  <SelectItem value="scheduled">Schedule for Later</SelectItem>
                </SelectContent>
              </Select>
            </div>
          </div>
          
          <DialogFooter>
            <Button variant="outline" onClick={() => setIsCreateDialogOpen(false)}>
              Cancel
            </Button>
            <Button onClick={() => setIsCreateDialogOpen(false)}>
              Create Campaign
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Template Edit Dialog */}
      <Dialog open={isTemplateDialogOpen} onOpenChange={setIsTemplateDialogOpen}>
        <DialogContent className="max-w-4xl max-h-[80vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Edit Email Template</DialogTitle>
            <DialogDescription>
              Modify the email template content and settings
            </DialogDescription>
          </DialogHeader>
          
          {selectedTemplate && (
            <div className="space-y-4">
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="template-name">Template Name</Label>
                  <Input id="template-name" defaultValue={selectedTemplate.name} />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="template-type">Template Type</Label>
                  <Select defaultValue={selectedTemplate.type}>
                    <SelectTrigger>
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="welcome">Welcome</SelectItem>
                      <SelectItem value="notification">Notification</SelectItem>
                      <SelectItem value="alert">Alert</SelectItem>
                      <SelectItem value="reminder">Reminder</SelectItem>
                      <SelectItem value="marketing">Marketing</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
              </div>
              
              <div className="space-y-2">
                <Label htmlFor="template-subject">Subject Line</Label>
                <Input id="template-subject" defaultValue={selectedTemplate.subject} />
              </div>
              
              <div className="space-y-2">
                <Label htmlFor="template-content">Email Content</Label>
                <Textarea 
                  id="template-content" 
                  defaultValue={selectedTemplate.content} 
                  rows={10}
                  className="font-mono"
                />
              </div>
              
              <div className="space-y-2">
                <Label>Available Variables</Label>
                <div className="flex flex-wrap gap-2">
                  {selectedTemplate.variables.map((variable) => (
                    <Badge key={variable} variant="secondary">
                      {`{{${variable}}}`}
                    </Badge>
                  ))}
                </div>
              </div>
              
              <div className="flex items-center space-x-2">
                <Switch id="template-active" defaultChecked={selectedTemplate.isActive} />
                <Label htmlFor="template-active">Template is active</Label>
              </div>
            </div>
          )}
          
          <DialogFooter>
            <Button variant="outline" onClick={() => setIsTemplateDialogOpen(false)}>
              Cancel
            </Button>
            <Button>
              <Send className="h-4 w-4 mr-2" />
              Send Test Email
            </Button>
            <Button onClick={() => setIsTemplateDialogOpen(false)}>
              Save Template
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  )
}

export default EmailNotifications