'use client';

import React, { useState, useEffect } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger } from '@/components/ui/dialog';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Switch } from '@/components/ui/switch';
import { 
  Plus,
  Search,
  Edit,
  Trash2,
  AlertTriangle,
  MoreHorizontal,
  Settings,
  Tag,
  ArrowUp,
  ArrowDown,
  Minus,
  Zap
} from 'lucide-react';
import { cn } from '@/lib/utils';

// Mock data for priority levels
const priorityLevelsData = [
  {
    id: 1,
    name: 'Critical',
    code: 'CRIT',
    description: 'System down or safety risk - immediate attention required',
    level: 1,
    isActive: true,
    color: '#dc2626',
    icon: 'zap',
    responseTime: 15,
    escalationTime: 30,
    requiresApproval: true,
    notificationRules: 'Immediate SMS + Email + Phone call',
    slaHours: 1,
    autoAssign: true
  },
  {
    id: 2,
    name: 'High',
    code: 'HIGH',
    description: 'Significant impact on operations - urgent attention needed',
    level: 2,
    isActive: true,
    color: '#ea580c',
    icon: 'arrow-up',
    responseTime: 60,
    escalationTime: 120,
    requiresApproval: false,
    notificationRules: 'Email + Dashboard notification',
    slaHours: 4,
    autoAssign: true
  },
  {
    id: 3,
    name: 'Medium',
    code: 'MED',
    description: 'Normal maintenance priority - scheduled within timeframe',
    level: 3,
    isActive: true,
    color: '#ca8a04',
    icon: 'minus',
    responseTime: 240,
    escalationTime: 480,
    requiresApproval: false,
    notificationRules: 'Email notification',
    slaHours: 24,
    autoAssign: false
  },
  {
    id: 4,
    name: 'Low',
    code: 'LOW',
    description: 'Non-urgent maintenance - can be deferred if needed',
    level: 4,
    isActive: true,
    color: '#16a34a',
    icon: 'arrow-down',
    responseTime: 1440,
    escalationTime: 2880,
    requiresApproval: false,
    notificationRules: 'Dashboard notification only',
    slaHours: 72,
    autoAssign: false
  },
  {
    id: 5,
    name: 'Routine',
    code: 'ROUT',
    description: 'Scheduled maintenance activities - no urgency',
    level: 5,
    isActive: true,
    color: '#64748b',
    icon: 'settings',
    responseTime: 10080,
    escalationTime: 20160,
    requiresApproval: false,
    notificationRules: 'Email notification',
    slaHours: 168,
    autoAssign: false
  }
];

export default function PriorityLevelsPage() {
  const [searchTerm, setSearchTerm] = useState('');
  const [statusFilter, setStatusFilter] = useState('all');
  const [levelFilter, setLevelFilter] = useState('all');
  const [isCreateDialogOpen, setIsCreateDialogOpen] = useState(false);
  const [isEditDialogOpen, setIsEditDialogOpen] = useState(false);
  const [selectedPriority, setSelectedPriority] = useState(null);
  const [filteredData, setFilteredData] = useState(priorityLevelsData);
  
  // Form state
  const [formData, setFormData] = useState({
    name: '',
    code: '',
    description: '',
    level: 1,
    isActive: true,
    color: '#dc2626',
    icon: 'alert-triangle',
    responseTime: 60,
    escalationTime: 120,
    requiresApproval: false,
    notificationRules: '',
    slaHours: 24,
    autoAssign: false,
    emailTemplate: '',
    smsTemplate: ''
  });

  useEffect(() => {
    let filtered = priorityLevelsData;

    if (searchTerm) {
      filtered = filtered.filter(item =>
        item.name.toLowerCase().includes(searchTerm.toLowerCase()) ||
        item.code.toLowerCase().includes(searchTerm.toLowerCase()) ||
        item.description.toLowerCase().includes(searchTerm.toLowerCase())
      );
    }

    if (statusFilter !== 'all') {
      filtered = filtered.filter(item => 
        statusFilter === 'active' ? item.isActive : !item.isActive
      );
    }

    if (levelFilter !== 'all') {
      filtered = filtered.filter(item => item.level.toString() === levelFilter);
    }

    // Sort by priority level
    filtered.sort((a, b) => a.level - b.level);

    setFilteredData(filtered);
  }, [searchTerm, statusFilter, levelFilter]);

  const handleCreate = () => {
    console.log('Creating priority level:', formData);
    setIsCreateDialogOpen(false);
    resetForm();
  };

  const handleEdit = (priority: any) => {
    setSelectedPriority(priority);
    setFormData({
      name: priority.name,
      code: priority.code,
      description: priority.description,
      level: priority.level,
      isActive: priority.isActive,
      color: priority.color,
      icon: priority.icon,
      responseTime: priority.responseTime,
      escalationTime: priority.escalationTime,
      requiresApproval: priority.requiresApproval,
      notificationRules: priority.notificationRules,
      slaHours: priority.slaHours,
      autoAssign: priority.autoAssign,
      emailTemplate: priority.emailTemplate || '',
      smsTemplate: priority.smsTemplate || ''
    });
    setIsEditDialogOpen(true);
  };

  const handleUpdate = () => {
    console.log('Updating priority level:', selectedPriority?.id, formData);
    setIsEditDialogOpen(false);
    resetForm();
  };

  const handleDelete = (id: number) => {
    console.log('Deleting priority level:', id);
  };

  const resetForm = () => {
    setFormData({
      name: '',
      code: '',
      description: '',
      level: 1,
      isActive: true,
      color: '#dc2626',
      icon: 'alert-triangle',
      responseTime: 60,
      escalationTime: 120,
      requiresApproval: false,
      notificationRules: '',
      slaHours: 24,
      autoAssign: false,
      emailTemplate: '',
      smsTemplate: ''
    });
    setSelectedPriority(null);
  };

  const formatTime = (minutes: number) => {
    if (minutes < 60) return `${minutes}m`;
    if (minutes < 1440) return `${Math.round(minutes / 60)}h`;
    return `${Math.round(minutes / 1440)}d`;
  };

  const getPriorityIcon = (iconName: string, className: string = "h-5 w-5") => {
    switch (iconName) {
      case 'zap': return <Zap className={className} />;
      case 'arrow-up': return <ArrowUp className={className} />;
      case 'minus': return <Minus className={className} />;
      case 'arrow-down': return <ArrowDown className={className} />;
      case 'settings': return <Settings className={className} />;
      default: return <AlertTriangle className={className} />;
    }
  };

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Priority Levels</h1>
          <p className="text-muted-foreground">
            Manage maintenance priority levels and their escalation rules
          </p>
        </div>
        <Dialog open={isCreateDialogOpen} onOpenChange={setIsCreateDialogOpen}>
          <DialogTrigger asChild>
            <Button>
              <Plus className="mr-2 h-4 w-4" />
              Add Priority Level
            </Button>
          </DialogTrigger>
          <DialogContent className="sm:max-w-[600px]">
            <DialogHeader>
              <DialogTitle>Add Priority Level</DialogTitle>
              <DialogDescription>
                Create a new priority level for maintenance work orders.
              </DialogDescription>
            </DialogHeader>
            <div className="grid gap-4 py-4 max-h-[70vh] overflow-y-auto">
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="name">Priority Name</Label>
                  <Input
                    id="name"
                    value={formData.name}
                    onChange={(e) => setFormData({...formData, name: e.target.value})}
                    placeholder="Enter priority name"
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="code">Priority Code</Label>
                  <Input
                    id="code"
                    value={formData.code}
                    onChange={(e) => setFormData({...formData, code: e.target.value.toUpperCase()})}
                    placeholder="e.g., CRIT, HIGH"
                  />
                </div>
              </div>
              
              <div className="space-y-2">
                <Label htmlFor="description">Description</Label>
                <Textarea
                  id="description"
                  value={formData.description}
                  onChange={(e) => setFormData({...formData, description: e.target.value})}
                  placeholder="Describe this priority level..."
                  rows={3}
                />
              </div>
              
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="level">Priority Level (1=Highest)</Label>
                  <Input
                    id="level"
                    type="number"
                    min="1"
                    max="10"
                    value={formData.level}
                    onChange={(e) => setFormData({...formData, level: parseInt(e.target.value) || 1})}
                    placeholder="1"
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="slaHours">SLA Hours</Label>
                  <Input
                    id="slaHours"
                    type="number"
                    value={formData.slaHours}
                    onChange={(e) => setFormData({...formData, slaHours: parseInt(e.target.value) || 24})}
                    placeholder="24"
                  />
                </div>
              </div>

              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="responseTime">Response Time (minutes)</Label>
                  <Input
                    id="responseTime"
                    type="number"
                    value={formData.responseTime}
                    onChange={(e) => setFormData({...formData, responseTime: parseInt(e.target.value) || 60})}
                    placeholder="60"
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="escalationTime">Escalation Time (minutes)</Label>
                  <Input
                    id="escalationTime"
                    type="number"
                    value={formData.escalationTime}
                    onChange={(e) => setFormData({...formData, escalationTime: parseInt(e.target.value) || 120})}
                    placeholder="120"
                  />
                </div>
              </div>

              <div className="space-y-2">
                <Label htmlFor="color">Priority Color</Label>
                <div className="flex items-center space-x-2">
                  <Input
                    id="color"
                    type="color"
                    value={formData.color}
                    onChange={(e) => setFormData({...formData, color: e.target.value})}
                    className="w-16 h-10"
                  />
                  <Input
                    value={formData.color}
                    onChange={(e) => setFormData({...formData, color: e.target.value})}
                    placeholder="#dc2626"
                    className="flex-1"
                  />
                </div>
              </div>

              <div className="space-y-2">
                <Label htmlFor="icon">Icon</Label>
                <Select value={formData.icon} onValueChange={(value) => setFormData({...formData, icon: value})}>
                  <SelectTrigger>
                    <SelectValue placeholder="Select icon" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="zap">⚡ Lightning (Critical)</SelectItem>
                    <SelectItem value="arrow-up">↑ Arrow Up (High)</SelectItem>
                    <SelectItem value="minus">− Minus (Medium)</SelectItem>
                    <SelectItem value="arrow-down">↓ Arrow Down (Low)</SelectItem>
                    <SelectItem value="settings">⚙ Settings (Routine)</SelectItem>
                    <SelectItem value="alert-triangle">⚠ Alert Triangle</SelectItem>
                  </SelectContent>
                </Select>
              </div>

              <div className="space-y-2">
                <Label htmlFor="notificationRules">Notification Rules</Label>
                <Textarea
                  id="notificationRules"
                  value={formData.notificationRules}
                  onChange={(e) => setFormData({...formData, notificationRules: e.target.value})}
                  placeholder="Describe notification rules..."
                  rows={2}
                />
              </div>
              
              <div className="flex items-center space-x-4">
                <div className="flex items-center space-x-2">
                  <Switch
                    id="approval"
                    checked={formData.requiresApproval}
                    onCheckedChange={(checked) => setFormData({...formData, requiresApproval: checked})}
                  />
                  <Label htmlFor="approval">Requires Approval</Label>
                </div>
                <div className="flex items-center space-x-2">
                  <Switch
                    id="autoAssign"
                    checked={formData.autoAssign}
                    onCheckedChange={(checked) => setFormData({...formData, autoAssign: checked})}
                  />
                  <Label htmlFor="autoAssign">Auto-assign</Label>
                </div>
                <div className="flex items-center space-x-2">
                  <Switch
                    id="active"
                    checked={formData.isActive}
                    onCheckedChange={(checked) => setFormData({...formData, isActive: checked})}
                  />
                  <Label htmlFor="active">Active</Label>
                </div>
              </div>
            </div>
            <DialogFooter>
              <Button variant="outline" onClick={() => setIsCreateDialogOpen(false)}>
                Cancel
              </Button>
              <Button onClick={handleCreate}>
                Add Priority Level
              </Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>
      </div>

      {/* Breadcrumbs */}
      <Breadcrumb>
        <BreadcrumbList>
          <BreadcrumbItem>
            <BreadcrumbLink href="/dashboard">Dashboard</BreadcrumbLink>
          </BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem>
            <BreadcrumbLink href="/administration">Administration</BreadcrumbLink>
          </BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem>
            <BreadcrumbLink href="/administration/maintenance">Maintenance Setup</BreadcrumbLink>
          </BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem>
            <BreadcrumbPage>Priority Levels</BreadcrumbPage>
          </BreadcrumbItem>
        </BreadcrumbList>
      </Breadcrumb>

      {/* Stats */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-2xl font-bold">{filteredData.length}</p>
                <p className="text-sm text-muted-foreground">Total Levels</p>
              </div>
              <Tag className="h-8 w-8 text-blue-500" />
            </div>
          </CardContent>
        </Card>
        
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-2xl font-bold">
                  {filteredData.filter(p => p.isActive).length}
                </p>
                <p className="text-sm text-muted-foreground">Active</p>
              </div>
              <Settings className="h-8 w-8 text-green-500" />
            </div>
          </CardContent>
        </Card>
        
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-2xl font-bold">
                  {filteredData.filter(p => p.requiresApproval).length}
                </p>
                <p className="text-sm text-muted-foreground">Require Approval</p>
              </div>
              <AlertTriangle className="h-8 w-8 text-orange-500" />
            </div>
          </CardContent>
        </Card>
        
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-2xl font-bold">
                  {filteredData.filter(p => p.autoAssign).length}
                </p>
                <p className="text-sm text-muted-foreground">Auto-assign</p>
              </div>
              <Zap className="h-8 w-8 text-purple-500" />
            </div>
          </CardContent>
        </Card>
      </div>

      {/* Filters */}
      <Card>
        <CardHeader>
          <CardTitle>Filters</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            <div className="relative">
              <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
              <Input
                placeholder="Search priority levels..."
                className="pl-8"
                value={searchTerm}
                onChange={(e) => setSearchTerm(e.target.value)}
              />
            </div>
            
            <Select value={statusFilter} onValueChange={setStatusFilter}>
              <SelectTrigger>
                <SelectValue placeholder="Status" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Status</SelectItem>
                <SelectItem value="active">Active</SelectItem>
                <SelectItem value="inactive">Inactive</SelectItem>
              </SelectContent>
            </Select>

            <Select value={levelFilter} onValueChange={setLevelFilter}>
              <SelectTrigger>
                <SelectValue placeholder="Priority Level" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Levels</SelectItem>
                <SelectItem value="1">Level 1 (Highest)</SelectItem>
                <SelectItem value="2">Level 2</SelectItem>
                <SelectItem value="3">Level 3</SelectItem>
                <SelectItem value="4">Level 4</SelectItem>
                <SelectItem value="5">Level 5 (Lowest)</SelectItem>
              </SelectContent>
            </Select>
          </div>
        </CardContent>
      </Card>

      {/* Priority Levels List */}
      <Card>
        <CardHeader>
          <CardTitle>Priority Levels</CardTitle>
          <CardDescription>
            {filteredData.length} level{filteredData.length === 1 ? '' : 's'} found (sorted by priority)
          </CardDescription>
        </CardHeader>
        <CardContent>
          <div className="space-y-4">
            {filteredData.map((priority) => (
              <div key={priority.id} className="border rounded-lg p-4 hover:bg-muted/50 transition-colors">
                <div className="flex items-start justify-between">
                  <div className="space-y-3 flex-1">
                    <div className="flex items-center space-x-3">
                      <div className="flex items-center space-x-2">
                        <div 
                          className="w-4 h-4 rounded-full" 
                          style={{ backgroundColor: priority.color }}
                        />
                        {getPriorityIcon(priority.icon)}
                      </div>
                      <h3 className="font-semibold">Level {priority.level}: {priority.name}</h3>
                      <Badge variant="outline">{priority.code}</Badge>
                      <Badge className={priority.isActive ? 'bg-green-100 text-green-800' : 'bg-gray-100 text-gray-800'}>
                        {priority.isActive ? 'Active' : 'Inactive'}
                      </Badge>
                      {priority.requiresApproval && (
                        <Badge className="bg-orange-100 text-orange-800">Requires Approval</Badge>
                      )}
                      {priority.autoAssign && (
                        <Badge className="bg-purple-100 text-purple-800">Auto-assign</Badge>
                      )}
                    </div>
                    
                    <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4 text-sm text-muted-foreground">
                      <div>
                        <span className="font-medium">Response:</span> {formatTime(priority.responseTime)}
                      </div>
                      <div>
                        <span className="font-medium">Escalation:</span> {formatTime(priority.escalationTime)}
                      </div>
                      <div>
                        <span className="font-medium">SLA:</span> {priority.slaHours}h
                      </div>
                      <div>
                        <span className="font-medium">Notifications:</span> {priority.notificationRules || 'Not configured'}
                      </div>
                    </div>
                    
                    <p className="text-sm text-muted-foreground">{priority.description}</p>
                  </div>
                  
                  <div className="flex items-center space-x-2">
                    <Button size="sm" variant="outline" onClick={() => handleEdit(priority)}>
                      <Edit className="h-4 w-4" />
                    </Button>
                    <Button 
                      size="sm" 
                      variant="outline" 
                      onClick={() => handleDelete(priority.id)}
                      className="text-red-600 hover:text-red-700"
                    >
                      <Trash2 className="h-4 w-4" />
                    </Button>
                    <Button variant="outline" size="sm">
                      <MoreHorizontal className="h-4 w-4" />
                    </Button>
                  </div>
                </div>
              </div>
            ))}
          </div>
        </CardContent>
      </Card>

      {/* Edit Dialog */}
      <Dialog open={isEditDialogOpen} onOpenChange={setIsEditDialogOpen}>
        <DialogContent className="sm:max-w-[600px]">
          <DialogHeader>
            <DialogTitle>Edit Priority Level</DialogTitle>
            <DialogDescription>
              Update the priority level information.
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 py-4 max-h-[70vh] overflow-y-auto">
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label htmlFor="edit-name">Priority Name</Label>
                <Input
                  id="edit-name"
                  value={formData.name}
                  onChange={(e) => setFormData({...formData, name: e.target.value})}
                  placeholder="Enter priority name"
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="edit-code">Priority Code</Label>
                <Input
                  id="edit-code"
                  value={formData.code}
                  onChange={(e) => setFormData({...formData, code: e.target.value.toUpperCase()})}
                  placeholder="e.g., CRIT, HIGH"
                />
              </div>
            </div>
            
            <div className="space-y-2">
              <Label htmlFor="edit-description">Description</Label>
              <Textarea
                id="edit-description"
                value={formData.description}
                onChange={(e) => setFormData({...formData, description: e.target.value})}
                placeholder="Describe this priority level..."
                rows={3}
              />
            </div>
            
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label htmlFor="edit-level">Priority Level (1=Highest)</Label>
                <Input
                  id="edit-level"
                  type="number"
                  min="1"
                  max="10"
                  value={formData.level}
                  onChange={(e) => setFormData({...formData, level: parseInt(e.target.value) || 1})}
                  placeholder="1"
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="edit-slaHours">SLA Hours</Label>
                <Input
                  id="edit-slaHours"
                  type="number"
                  value={formData.slaHours}
                  onChange={(e) => setFormData({...formData, slaHours: parseInt(e.target.value) || 24})}
                  placeholder="24"
                />
              </div>
            </div>

            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label htmlFor="edit-responseTime">Response Time (minutes)</Label>
                <Input
                  id="edit-responseTime"
                  type="number"
                  value={formData.responseTime}
                  onChange={(e) => setFormData({...formData, responseTime: parseInt(e.target.value) || 60})}
                  placeholder="60"
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="edit-escalationTime">Escalation Time (minutes)</Label>
                <Input
                  id="edit-escalationTime"
                  type="number"
                  value={formData.escalationTime}
                  onChange={(e) => setFormData({...formData, escalationTime: parseInt(e.target.value) || 120})}
                  placeholder="120"
                />
              </div>
            </div>

            <div className="space-y-2">
              <Label htmlFor="edit-color">Priority Color</Label>
              <div className="flex items-center space-x-2">
                <Input
                  id="edit-color"
                  type="color"
                  value={formData.color}
                  onChange={(e) => setFormData({...formData, color: e.target.value})}
                  className="w-16 h-10"
                />
                <Input
                  value={formData.color}
                  onChange={(e) => setFormData({...formData, color: e.target.value})}
                  placeholder="#dc2626"
                  className="flex-1"
                />
              </div>
            </div>

            <div className="space-y-2">
              <Label htmlFor="edit-icon">Icon</Label>
              <Select value={formData.icon} onValueChange={(value) => setFormData({...formData, icon: value})}>
                <SelectTrigger>
                  <SelectValue placeholder="Select icon" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="zap">⚡ Lightning (Critical)</SelectItem>
                  <SelectItem value="arrow-up">↑ Arrow Up (High)</SelectItem>
                  <SelectItem value="minus">− Minus (Medium)</SelectItem>
                  <SelectItem value="arrow-down">↓ Arrow Down (Low)</SelectItem>
                  <SelectItem value="settings">⚙ Settings (Routine)</SelectItem>
                  <SelectItem value="alert-triangle">⚠ Alert Triangle</SelectItem>
                </SelectContent>
              </Select>
            </div>

            <div className="space-y-2">
              <Label htmlFor="edit-notificationRules">Notification Rules</Label>
              <Textarea
                id="edit-notificationRules"
                value={formData.notificationRules}
                onChange={(e) => setFormData({...formData, notificationRules: e.target.value})}
                placeholder="Describe notification rules..."
                rows={2}
              />
            </div>
            
            <div className="flex items-center space-x-4">
              <div className="flex items-center space-x-2">
                <Switch
                  id="edit-approval"
                  checked={formData.requiresApproval}
                  onCheckedChange={(checked) => setFormData({...formData, requiresApproval: checked})}
                />
                <Label htmlFor="edit-approval">Requires Approval</Label>
              </div>
              <div className="flex items-center space-x-2">
                <Switch
                  id="edit-autoAssign"
                  checked={formData.autoAssign}
                  onCheckedChange={(checked) => setFormData({...formData, autoAssign: checked})}
                />
                <Label htmlFor="edit-autoAssign">Auto-assign</Label>
              </div>
              <div className="flex items-center space-x-2">
                <Switch
                  id="edit-active"
                  checked={formData.isActive}
                  onCheckedChange={(checked) => setFormData({...formData, isActive: checked})}
                />
                <Label htmlFor="edit-active">Active</Label>
              </div>
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setIsEditDialogOpen(false)}>
              Cancel
            </Button>
            <Button onClick={handleUpdate}>Update Priority Level</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}