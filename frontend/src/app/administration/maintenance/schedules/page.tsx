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
  MoreHorizontal,
  Calendar,
  Clock,
  RefreshCw,
  AlertCircle,
  CheckCircle2,
  Pause,
  Play,
  Settings
} from 'lucide-react';

// Mock data for maintenance schedules
const maintenanceSchedulesData = [
  {
    id: 1,
    name: 'HVAC Monthly Filter Change',
    code: 'HVAC-MFC-001',
    description: 'Monthly replacement of HVAC air filters in all zones',
    maintenanceType: 'Preventive',
    priority: 'Medium',
    frequency: 'Monthly',
    frequencyInterval: 1,
    startDate: '2024-01-01',
    nextDue: '2024-04-01',
    lastCompleted: '2024-03-01',
    estimatedDuration: 120,
    assignedTeam: 'HVAC Team',
    assetCategory: 'HVAC Systems',
    isActive: true,
    autoCreate: true,
    leadTime: 5,
    maxDelayDays: 3,
    completedCount: 15,
    overdueCount: 0,
    status: 'Active',
    notes: 'Check all zones, replace filters with HEPA grade',
    createdBy: 'John Manager',
    createdDate: '2024-01-01'
  },
  {
    id: 2,
    name: 'Generator Weekly Inspection',
    code: 'GEN-WI-002',
    description: 'Weekly inspection and testing of emergency generators',
    maintenanceType: 'Inspection',
    priority: 'High',
    frequency: 'Weekly',
    frequencyInterval: 1,
    startDate: '2024-01-01',
    nextDue: '2024-03-18',
    lastCompleted: '2024-03-11',
    estimatedDuration: 60,
    assignedTeam: 'Electrical Team',
    assetCategory: 'Power Systems',
    isActive: true,
    autoCreate: true,
    leadTime: 2,
    maxDelayDays: 1,
    completedCount: 42,
    overdueCount: 1,
    status: 'Active',
    notes: 'Test run for 30 minutes, check fuel levels',
    createdBy: 'Sarah Tech',
    createdDate: '2024-01-01'
  },
  {
    id: 3,
    name: 'Fire Extinguisher Annual Service',
    code: 'FIRE-AS-003',
    description: 'Annual inspection and service of all fire extinguishers',
    maintenanceType: 'Compliance',
    priority: 'Critical',
    frequency: 'Annual',
    frequencyInterval: 1,
    startDate: '2024-01-01',
    nextDue: '2025-01-01',
    lastCompleted: '2024-01-01',
    estimatedDuration: 480,
    assignedTeam: 'Safety Team',
    assetCategory: 'Safety Equipment',
    isActive: true,
    autoCreate: true,
    leadTime: 14,
    maxDelayDays: 7,
    completedCount: 1,
    overdueCount: 0,
    status: 'Active',
    notes: 'External contractor required for certification',
    createdBy: 'Mike Safety',
    createdDate: '2024-01-01'
  },
  {
    id: 4,
    name: 'Elevator Quarterly Maintenance',
    code: 'ELEV-QM-004',
    description: 'Quarterly maintenance of passenger elevators',
    maintenanceType: 'Preventive',
    priority: 'High',
    frequency: 'Quarterly',
    frequencyInterval: 1,
    startDate: '2024-01-01',
    nextDue: '2024-04-01',
    lastCompleted: '2024-01-01',
    estimatedDuration: 240,
    assignedTeam: 'Elevator Technicians',
    assetCategory: 'Vertical Transport',
    isActive: false,
    autoCreate: false,
    leadTime: 7,
    maxDelayDays: 2,
    completedCount: 1,
    overdueCount: 0,
    status: 'Paused',
    notes: 'Paused pending elevator modernization project',
    createdBy: 'Tom Lead',
    createdDate: '2024-01-01'
  },
  {
    id: 5,
    name: 'Roof Drain Cleaning',
    code: 'ROOF-DC-005',
    description: 'Bi-annual cleaning of roof drains and gutters',
    maintenanceType: 'Preventive',
    priority: 'Medium',
    frequency: 'Semi-Annual',
    frequencyInterval: 2,
    startDate: '2024-01-01',
    nextDue: '2024-07-01',
    lastCompleted: null,
    estimatedDuration: 180,
    assignedTeam: 'Facilities Team',
    assetCategory: 'Building Structure',
    isActive: true,
    autoCreate: true,
    leadTime: 10,
    maxDelayDays: 14,
    completedCount: 0,
    overdueCount: 0,
    status: 'Active',
    notes: 'Weather dependent activity, reschedule if rain forecast',
    createdBy: 'Lisa Facilities',
    createdDate: '2024-01-01'
  }
];

export default function MaintenanceSchedulesPage() {
  const [searchTerm, setSearchTerm] = useState('');
  const [statusFilter, setStatusFilter] = useState('all');
  const [priorityFilter, setPriorityFilter] = useState('all');
  const [frequencyFilter, setFrequencyFilter] = useState('all');
  const [isCreateDialogOpen, setIsCreateDialogOpen] = useState(false);
  const [isEditDialogOpen, setIsEditDialogOpen] = useState(false);
  const [selectedSchedule, setSelectedSchedule] = useState(null);
  const [filteredData, setFilteredData] = useState(maintenanceSchedulesData);
  
  // Form state
  const [formData, setFormData] = useState({
    name: '',
    code: '',
    description: '',
    maintenanceType: 'Preventive',
    priority: 'Medium',
    frequency: 'Monthly',
    frequencyInterval: 1,
    startDate: '',
    estimatedDuration: 60,
    assignedTeam: '',
    assetCategory: '',
    isActive: true,
    autoCreate: true,
    leadTime: 5,
    maxDelayDays: 3,
    notes: ''
  });

  useEffect(() => {
    let filtered = maintenanceSchedulesData;

    if (searchTerm) {
      filtered = filtered.filter(item =>
        item.name.toLowerCase().includes(searchTerm.toLowerCase()) ||
        item.code.toLowerCase().includes(searchTerm.toLowerCase()) ||
        item.description.toLowerCase().includes(searchTerm.toLowerCase()) ||
        item.assignedTeam.toLowerCase().includes(searchTerm.toLowerCase())
      );
    }

    if (statusFilter !== 'all') {
      filtered = filtered.filter(item => 
        statusFilter === 'active' ? item.isActive : !item.isActive
      );
    }

    if (priorityFilter !== 'all') {
      filtered = filtered.filter(item => item.priority === priorityFilter);
    }

    if (frequencyFilter !== 'all') {
      filtered = filtered.filter(item => item.frequency === frequencyFilter);
    }

    setFilteredData(filtered);
  }, [searchTerm, statusFilter, priorityFilter, frequencyFilter]);

  const handleCreate = () => {
    console.log('Creating maintenance schedule:', formData);
    setIsCreateDialogOpen(false);
    resetForm();
  };

  const handleEdit = (schedule: any) => {
    setSelectedSchedule(schedule);
    setFormData({
      name: schedule.name,
      code: schedule.code,
      description: schedule.description,
      maintenanceType: schedule.maintenanceType,
      priority: schedule.priority,
      frequency: schedule.frequency,
      frequencyInterval: schedule.frequencyInterval,
      startDate: schedule.startDate,
      estimatedDuration: schedule.estimatedDuration,
      assignedTeam: schedule.assignedTeam,
      assetCategory: schedule.assetCategory,
      isActive: schedule.isActive,
      autoCreate: schedule.autoCreate,
      leadTime: schedule.leadTime,
      maxDelayDays: schedule.maxDelayDays,
      notes: schedule.notes
    });
    setIsEditDialogOpen(true);
  };

  const handleUpdate = () => {
    console.log('Updating maintenance schedule:', selectedSchedule?.id, formData);
    setIsEditDialogOpen(false);
    resetForm();
  };

  const handleDelete = (id: number) => {
    console.log('Deleting maintenance schedule:', id);
  };

  const handlePause = (id: number) => {
    console.log('Pausing maintenance schedule:', id);
  };

  const handleResume = (id: number) => {
    console.log('Resuming maintenance schedule:', id);
  };

  const resetForm = () => {
    setFormData({
      name: '',
      code: '',
      description: '',
      maintenanceType: 'Preventive',
      priority: 'Medium',
      frequency: 'Monthly',
      frequencyInterval: 1,
      startDate: '',
      estimatedDuration: 60,
      assignedTeam: '',
      assetCategory: '',
      isActive: true,
      autoCreate: true,
      leadTime: 5,
      maxDelayDays: 3,
      notes: ''
    });
    setSelectedSchedule(null);
  };

  const formatDuration = (minutes: number) => {
    if (minutes < 60) return `${minutes}m`;
    const hours = Math.floor(minutes / 60);
    const mins = minutes % 60;
    return mins > 0 ? `${hours}h ${mins}m` : `${hours}h`;
  };

  const getPriorityColor = (priority: string) => {
    switch (priority) {
      case 'Critical': return 'bg-red-100 text-red-800';
      case 'High': return 'bg-orange-100 text-orange-800';
      case 'Medium': return 'bg-yellow-100 text-yellow-800';
      case 'Low': return 'bg-green-100 text-green-800';
      default: return 'bg-gray-100 text-gray-800';
    }
  };

  const getStatusColor = (status: string) => {
    switch (status) {
      case 'Active': return 'bg-green-100 text-green-800';
      case 'Paused': return 'bg-yellow-100 text-yellow-800';
      case 'Completed': return 'bg-blue-100 text-blue-800';
      default: return 'bg-gray-100 text-gray-800';
    }
  };

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Maintenance Schedules</h1>
          <p className="text-muted-foreground">
            Manage scheduled maintenance activities and automation rules
          </p>
        </div>
        <Dialog open={isCreateDialogOpen} onOpenChange={setIsCreateDialogOpen}>
          <DialogTrigger asChild>
            <Button>
              <Plus className="mr-2 h-4 w-4" />
              Add Schedule
            </Button>
          </DialogTrigger>
          <DialogContent className="sm:max-w-[700px]">
            <DialogHeader>
              <DialogTitle>Add Maintenance Schedule</DialogTitle>
              <DialogDescription>
                Create a new recurring maintenance schedule.
              </DialogDescription>
            </DialogHeader>
            <div className="grid gap-4 py-4 max-h-[70vh] overflow-y-auto">
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="name">Schedule Name</Label>
                  <Input
                    id="name"
                    value={formData.name}
                    onChange={(e) => setFormData({...formData, name: e.target.value})}
                    placeholder="Enter schedule name"
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="code">Schedule Code</Label>
                  <Input
                    id="code"
                    value={formData.code}
                    onChange={(e) => setFormData({...formData, code: e.target.value.toUpperCase()})}
                    placeholder="e.g., HVAC-001"
                  />
                </div>
              </div>
              
              <div className="space-y-2">
                <Label htmlFor="description">Description</Label>
                <Textarea
                  id="description"
                  value={formData.description}
                  onChange={(e) => setFormData({...formData, description: e.target.value})}
                  placeholder="Describe this maintenance schedule..."
                  rows={3}
                />
              </div>
              
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="maintenanceType">Maintenance Type</Label>
                  <Select value={formData.maintenanceType} onValueChange={(value) => setFormData({...formData, maintenanceType: value})}>
                    <SelectTrigger>
                      <SelectValue placeholder="Select type" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="Preventive">Preventive</SelectItem>
                      <SelectItem value="Inspection">Inspection</SelectItem>
                      <SelectItem value="Compliance">Compliance</SelectItem>
                      <SelectItem value="Calibration">Calibration</SelectItem>
                      <SelectItem value="Cleaning">Cleaning</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-2">
                  <Label htmlFor="priority">Priority</Label>
                  <Select value={formData.priority} onValueChange={(value) => setFormData({...formData, priority: value})}>
                    <SelectTrigger>
                      <SelectValue placeholder="Select priority" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="Critical">Critical</SelectItem>
                      <SelectItem value="High">High</SelectItem>
                      <SelectItem value="Medium">Medium</SelectItem>
                      <SelectItem value="Low">Low</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
              </div>

              <div className="grid grid-cols-3 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="frequency">Frequency</Label>
                  <Select value={formData.frequency} onValueChange={(value) => setFormData({...formData, frequency: value})}>
                    <SelectTrigger>
                      <SelectValue placeholder="Select frequency" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="Daily">Daily</SelectItem>
                      <SelectItem value="Weekly">Weekly</SelectItem>
                      <SelectItem value="Monthly">Monthly</SelectItem>
                      <SelectItem value="Quarterly">Quarterly</SelectItem>
                      <SelectItem value="Semi-Annual">Semi-Annual</SelectItem>
                      <SelectItem value="Annual">Annual</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-2">
                  <Label htmlFor="frequencyInterval">Interval</Label>
                  <Input
                    id="frequencyInterval"
                    type="number"
                    min="1"
                    value={formData.frequencyInterval}
                    onChange={(e) => setFormData({...formData, frequencyInterval: parseInt(e.target.value) || 1})}
                    placeholder="1"
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="startDate">Start Date</Label>
                  <Input
                    id="startDate"
                    type="date"
                    value={formData.startDate}
                    onChange={(e) => setFormData({...formData, startDate: e.target.value})}
                  />
                </div>
              </div>

              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="estimatedDuration">Duration (minutes)</Label>
                  <Input
                    id="estimatedDuration"
                    type="number"
                    value={formData.estimatedDuration}
                    onChange={(e) => setFormData({...formData, estimatedDuration: parseInt(e.target.value) || 60})}
                    placeholder="60"
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="leadTime">Lead Time (days)</Label>
                  <Input
                    id="leadTime"
                    type="number"
                    value={formData.leadTime}
                    onChange={(e) => setFormData({...formData, leadTime: parseInt(e.target.value) || 5})}
                    placeholder="5"
                  />
                </div>
              </div>

              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="assignedTeam">Assigned Team</Label>
                  <Input
                    id="assignedTeam"
                    value={formData.assignedTeam}
                    onChange={(e) => setFormData({...formData, assignedTeam: e.target.value})}
                    placeholder="Enter team name"
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="assetCategory">Asset Category</Label>
                  <Input
                    id="assetCategory"
                    value={formData.assetCategory}
                    onChange={(e) => setFormData({...formData, assetCategory: e.target.value})}
                    placeholder="Enter asset category"
                  />
                </div>
              </div>

              <div className="space-y-2">
                <Label htmlFor="maxDelayDays">Max Delay Days</Label>
                <Input
                  id="maxDelayDays"
                  type="number"
                  value={formData.maxDelayDays}
                  onChange={(e) => setFormData({...formData, maxDelayDays: parseInt(e.target.value) || 3})}
                  placeholder="3"
                />
              </div>

              <div className="space-y-2">
                <Label htmlFor="notes">Notes</Label>
                <Textarea
                  id="notes"
                  value={formData.notes}
                  onChange={(e) => setFormData({...formData, notes: e.target.value})}
                  placeholder="Additional notes or special instructions..."
                  rows={3}
                />
              </div>
              
              <div className="flex items-center space-x-4">
                <div className="flex items-center space-x-2">
                  <Switch
                    id="autoCreate"
                    checked={formData.autoCreate}
                    onCheckedChange={(checked) => setFormData({...formData, autoCreate: checked})}
                  />
                  <Label htmlFor="autoCreate">Auto-create Work Orders</Label>
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
                Add Schedule
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
            <BreadcrumbPage>Maintenance Schedules</BreadcrumbPage>
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
                <p className="text-sm text-muted-foreground">Total Schedules</p>
              </div>
              <Calendar className="h-8 w-8 text-blue-500" />
            </div>
          </CardContent>
        </Card>
        
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-2xl font-bold">
                  {filteredData.filter(s => s.isActive).length}
                </p>
                <p className="text-sm text-muted-foreground">Active</p>
              </div>
              <CheckCircle2 className="h-8 w-8 text-green-500" />
            </div>
          </CardContent>
        </Card>
        
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-2xl font-bold">
                  {filteredData.reduce((sum, s) => sum + s.overdueCount, 0)}
                </p>
                <p className="text-sm text-muted-foreground">Overdue</p>
              </div>
              <AlertCircle className="h-8 w-8 text-red-500" />
            </div>
          </CardContent>
        </Card>
        
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-2xl font-bold">
                  {filteredData.reduce((sum, s) => sum + s.completedCount, 0)}
                </p>
                <p className="text-sm text-muted-foreground">Completed</p>
              </div>
              <RefreshCw className="h-8 w-8 text-purple-500" />
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
          <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
            <div className="relative">
              <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
              <Input
                placeholder="Search schedules..."
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

            <Select value={priorityFilter} onValueChange={setPriorityFilter}>
              <SelectTrigger>
                <SelectValue placeholder="Priority" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Priorities</SelectItem>
                <SelectItem value="Critical">Critical</SelectItem>
                <SelectItem value="High">High</SelectItem>
                <SelectItem value="Medium">Medium</SelectItem>
                <SelectItem value="Low">Low</SelectItem>
              </SelectContent>
            </Select>

            <Select value={frequencyFilter} onValueChange={setFrequencyFilter}>
              <SelectTrigger>
                <SelectValue placeholder="Frequency" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Frequencies</SelectItem>
                <SelectItem value="Daily">Daily</SelectItem>
                <SelectItem value="Weekly">Weekly</SelectItem>
                <SelectItem value="Monthly">Monthly</SelectItem>
                <SelectItem value="Quarterly">Quarterly</SelectItem>
                <SelectItem value="Annual">Annual</SelectItem>
              </SelectContent>
            </Select>
          </div>
        </CardContent>
      </Card>

      {/* Schedules List */}
      <Card>
        <CardHeader>
          <CardTitle>Maintenance Schedules</CardTitle>
          <CardDescription>
            {filteredData.length} schedule{filteredData.length === 1 ? '' : 's'} found
          </CardDescription>
        </CardHeader>
        <CardContent>
          <div className="space-y-4">
            {filteredData.map((schedule) => (
              <div key={schedule.id} className="border rounded-lg p-4 hover:bg-muted/50 transition-colors">
                <div className="flex items-start justify-between">
                  <div className="space-y-3 flex-1">
                    <div className="flex items-center space-x-3">
                      <Calendar className="h-5 w-5 text-blue-500" />
                      <h3 className="font-semibold">{schedule.name}</h3>
                      <Badge variant="outline">{schedule.code}</Badge>
                      <Badge className={getPriorityColor(schedule.priority)}>
                        {schedule.priority}
                      </Badge>
                      <Badge className={getStatusColor(schedule.status)}>
                        {schedule.status}
                      </Badge>
                      {schedule.autoCreate && (
                        <Badge className="bg-blue-100 text-blue-800">Auto-create</Badge>
                      )}
                      {schedule.overdueCount > 0 && (
                        <Badge className="bg-red-100 text-red-800">
                          {schedule.overdueCount} Overdue
                        </Badge>
                      )}
                    </div>
                    
                    <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4 text-sm text-muted-foreground">
                      <div>
                        <span className="font-medium">Type:</span> {schedule.maintenanceType}
                      </div>
                      <div>
                        <span className="font-medium">Frequency:</span> {schedule.frequency}
                      </div>
                      <div>
                        <span className="font-medium">Duration:</span> {formatDuration(schedule.estimatedDuration)}
                      </div>
                      <div>
                        <span className="font-medium">Team:</span> {schedule.assignedTeam}
                      </div>
                      <div>
                        <span className="font-medium">Next Due:</span> {schedule.nextDue}
                      </div>
                      <div>
                        <span className="font-medium">Last Completed:</span> {schedule.lastCompleted || 'Never'}
                      </div>
                      <div>
                        <span className="font-medium">Completed:</span> {schedule.completedCount} times
                      </div>
                      <div>
                        <span className="font-medium">Category:</span> {schedule.assetCategory}
                      </div>
                    </div>
                    
                    <p className="text-sm text-muted-foreground">{schedule.description}</p>
                  </div>
                  
                  <div className="flex items-center space-x-2">
                    {schedule.isActive ? (
                      <Button size="sm" variant="outline" onClick={() => handlePause(schedule.id)}>
                        <Pause className="h-4 w-4" />
                      </Button>
                    ) : (
                      <Button size="sm" variant="outline" onClick={() => handleResume(schedule.id)}>
                        <Play className="h-4 w-4" />
                      </Button>
                    )}
                    <Button size="sm" variant="outline" onClick={() => handleEdit(schedule)}>
                      <Edit className="h-4 w-4" />
                    </Button>
                    <Button 
                      size="sm" 
                      variant="outline" 
                      onClick={() => handleDelete(schedule.id)}
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

      {/* Edit Dialog - Same fields as create */}
      <Dialog open={isEditDialogOpen} onOpenChange={setIsEditDialogOpen}>
        <DialogContent className="sm:max-w-[700px]">
          <DialogHeader>
            <DialogTitle>Edit Schedule</DialogTitle>
            <DialogDescription>
              Update the maintenance schedule information.
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 py-4 max-h-[70vh] overflow-y-auto">
            {/* Same form fields as create dialog */}
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label htmlFor="edit-name">Schedule Name</Label>
                <Input
                  id="edit-name"
                  value={formData.name}
                  onChange={(e) => setFormData({...formData, name: e.target.value})}
                  placeholder="Enter schedule name"
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="edit-code">Schedule Code</Label>
                <Input
                  id="edit-code"
                  value={formData.code}
                  onChange={(e) => setFormData({...formData, code: e.target.value.toUpperCase()})}
                  placeholder="e.g., HVAC-001"
                />
              </div>
            </div>
            
            <div className="space-y-2">
              <Label htmlFor="edit-description">Description</Label>
              <Textarea
                id="edit-description"
                value={formData.description}
                onChange={(e) => setFormData({...formData, description: e.target.value})}
                placeholder="Describe this maintenance schedule..."
                rows={3}
              />
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
          <DialogFooter>
            <Button variant="outline" onClick={() => setIsEditDialogOpen(false)}>
              Cancel
            </Button>
            <Button onClick={handleUpdate}>Update Schedule</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}