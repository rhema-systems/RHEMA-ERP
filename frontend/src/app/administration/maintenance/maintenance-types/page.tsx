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
  Wrench,
  MoreHorizontal,
  Settings,
  Tag,
  Calendar,
  Clock,
  AlertTriangle
} from 'lucide-react';
import { cn } from '@/lib/utils';

// Mock data for maintenance types
const maintenanceTypesData = [
  {
    id: 1,
    name: 'Preventive Maintenance',
    code: 'PM',
    description: 'Scheduled maintenance performed to prevent failures',
    category: 'Preventive',
    isActive: true,
    priority: 'Medium',
    color: '#10b981',
    icon: 'calendar',
    estimatedDuration: 120,
    requiresDowntime: true,
    skillLevel: 'Intermediate',
    frequency: 'Monthly'
  },
  {
    id: 2,
    name: 'Corrective Maintenance',
    code: 'CM',
    description: 'Maintenance performed to restore equipment to working condition',
    category: 'Corrective',
    isActive: true,
    priority: 'High',
    color: '#f59e0b',
    icon: 'wrench',
    estimatedDuration: 240,
    requiresDowntime: true,
    skillLevel: 'Advanced',
    frequency: 'As Required'
  },
  {
    id: 3,
    name: 'Predictive Maintenance',
    code: 'PDM',
    description: 'Maintenance based on condition monitoring and analysis',
    category: 'Predictive',
    isActive: true,
    priority: 'Medium',
    color: '#8b5cf6',
    icon: 'clock',
    estimatedDuration: 180,
    requiresDowntime: false,
    skillLevel: 'Expert',
    frequency: 'Quarterly'
  },
  {
    id: 4,
    name: 'Emergency Maintenance',
    code: 'EM',
    description: 'Urgent repairs to address critical failures',
    category: 'Emergency',
    isActive: true,
    priority: 'Critical',
    color: '#ef4444',
    icon: 'alert-triangle',
    estimatedDuration: 480,
    requiresDowntime: true,
    skillLevel: 'Expert',
    frequency: 'As Required'
  },
  {
    id: 5,
    name: 'Routine Inspection',
    code: 'RI',
    description: 'Regular visual inspection and basic checks',
    category: 'Preventive',
    isActive: true,
    priority: 'Low',
    color: '#3b82f6',
    icon: 'settings',
    estimatedDuration: 60,
    requiresDowntime: false,
    skillLevel: 'Basic',
    frequency: 'Weekly'
  },
  {
    id: 6,
    name: 'Calibration',
    code: 'CAL',
    description: 'Precision adjustment and calibration of instruments',
    category: 'Preventive',
    isActive: true,
    priority: 'Medium',
    color: '#06b6d4',
    icon: 'settings',
    estimatedDuration: 90,
    requiresDowntime: true,
    skillLevel: 'Expert',
    frequency: 'Annually'
  }
];

const categories = ['All', 'Preventive', 'Corrective', 'Predictive', 'Emergency'];
const priorities = ['Low', 'Medium', 'High', 'Critical'];
const skillLevels = ['Basic', 'Intermediate', 'Advanced', 'Expert'];

export default function MaintenanceTypesPage() {
  const [searchTerm, setSearchTerm] = useState('');
  const [statusFilter, setStatusFilter] = useState('all');
  const [categoryFilter, setCategoryFilter] = useState('all');
  const [priorityFilter, setPriorityFilter] = useState('all');
  const [isCreateDialogOpen, setIsCreateDialogOpen] = useState(false);
  const [isEditDialogOpen, setIsEditDialogOpen] = useState(false);
  const [selectedType, setSelectedType] = useState(null);
  const [filteredData, setFilteredData] = useState(maintenanceTypesData);
  
  // Form state
  const [formData, setFormData] = useState({
    name: '',
    code: '',
    description: '',
    category: 'Preventive',
    isActive: true,
    priority: 'Medium',
    color: '#10b981',
    icon: 'wrench',
    estimatedDuration: 120,
    requiresDowntime: false,
    skillLevel: 'Intermediate',
    frequency: 'Monthly',
    safetyRequirements: '',
    toolsRequired: '',
    notes: ''
  });

  useEffect(() => {
    let filtered = maintenanceTypesData;

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

    if (categoryFilter !== 'all') {
      filtered = filtered.filter(item => item.category === categoryFilter);
    }

    if (priorityFilter !== 'all') {
      filtered = filtered.filter(item => item.priority === priorityFilter);
    }

    setFilteredData(filtered);
  }, [searchTerm, statusFilter, categoryFilter, priorityFilter]);

  const handleCreate = () => {
    console.log('Creating maintenance type:', formData);
    setIsCreateDialogOpen(false);
    resetForm();
  };

  const handleEdit = (type: any) => {
    setSelectedType(type);
    setFormData({
      name: type.name,
      code: type.code,
      description: type.description,
      category: type.category,
      isActive: type.isActive,
      priority: type.priority,
      color: type.color,
      icon: type.icon,
      estimatedDuration: type.estimatedDuration,
      requiresDowntime: type.requiresDowntime,
      skillLevel: type.skillLevel,
      frequency: type.frequency,
      safetyRequirements: type.safetyRequirements || '',
      toolsRequired: type.toolsRequired || '',
      notes: type.notes || ''
    });
    setIsEditDialogOpen(true);
  };

  const handleUpdate = () => {
    console.log('Updating maintenance type:', selectedType?.id, formData);
    setIsEditDialogOpen(false);
    resetForm();
  };

  const handleDelete = (id: number) => {
    console.log('Deleting maintenance type:', id);
  };

  const resetForm = () => {
    setFormData({
      name: '',
      code: '',
      description: '',
      category: 'Preventive',
      isActive: true,
      priority: 'Medium',
      color: '#10b981',
      icon: 'wrench',
      estimatedDuration: 120,
      requiresDowntime: false,
      skillLevel: 'Intermediate',
      frequency: 'Monthly',
      safetyRequirements: '',
      toolsRequired: '',
      notes: ''
    });
    setSelectedType(null);
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

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Maintenance Types</h1>
          <p className="text-muted-foreground">
            Manage different types of maintenance activities and their configurations
          </p>
        </div>
        <Dialog open={isCreateDialogOpen} onOpenChange={setIsCreateDialogOpen}>
          <DialogTrigger asChild>
            <Button>
              <Plus className="mr-2 h-4 w-4" />
              Add Maintenance Type
            </Button>
          </DialogTrigger>
          <DialogContent className="sm:max-w-[600px]">
            <DialogHeader>
              <DialogTitle>Add Maintenance Type</DialogTitle>
              <DialogDescription>
                Create a new maintenance type to categorize maintenance activities.
              </DialogDescription>
            </DialogHeader>
            <div className="grid gap-4 py-4 max-h-[70vh] overflow-y-auto">
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="name">Type Name</Label>
                  <Input
                    id="name"
                    value={formData.name}
                    onChange={(e) => setFormData({...formData, name: e.target.value})}
                    placeholder="Enter maintenance type name"
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="code">Type Code</Label>
                  <Input
                    id="code"
                    value={formData.code}
                    onChange={(e) => setFormData({...formData, code: e.target.value.toUpperCase()})}
                    placeholder="e.g., PM, CM, EM"
                  />
                </div>
              </div>
              
              <div className="space-y-2">
                <Label htmlFor="description">Description</Label>
                <Textarea
                  id="description"
                  value={formData.description}
                  onChange={(e) => setFormData({...formData, description: e.target.value})}
                  placeholder="Describe this maintenance type..."
                  rows={3}
                />
              </div>
              
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="category">Category</Label>
                  <Select value={formData.category} onValueChange={(value) => setFormData({...formData, category: value})}>
                    <SelectTrigger>
                      <SelectValue placeholder="Select category" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="Preventive">Preventive</SelectItem>
                      <SelectItem value="Corrective">Corrective</SelectItem>
                      <SelectItem value="Predictive">Predictive</SelectItem>
                      <SelectItem value="Emergency">Emergency</SelectItem>
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
                      <SelectItem value="Low">Low</SelectItem>
                      <SelectItem value="Medium">Medium</SelectItem>
                      <SelectItem value="High">High</SelectItem>
                      <SelectItem value="Critical">Critical</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
              </div>

              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="skillLevel">Required Skill Level</Label>
                  <Select value={formData.skillLevel} onValueChange={(value) => setFormData({...formData, skillLevel: value})}>
                    <SelectTrigger>
                      <SelectValue placeholder="Select skill level" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="Basic">Basic</SelectItem>
                      <SelectItem value="Intermediate">Intermediate</SelectItem>
                      <SelectItem value="Advanced">Advanced</SelectItem>
                      <SelectItem value="Expert">Expert</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-2">
                  <Label htmlFor="frequency">Typical Frequency</Label>
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
                      <SelectItem value="As Required">As Required</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
              </div>

              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="duration">Estimated Duration (minutes)</Label>
                  <Input
                    id="duration"
                    type="number"
                    value={formData.estimatedDuration}
                    onChange={(e) => setFormData({...formData, estimatedDuration: parseInt(e.target.value) || 0})}
                    placeholder="120"
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="icon">Icon</Label>
                  <Input
                    id="icon"
                    value={formData.icon}
                    onChange={(e) => setFormData({...formData, icon: e.target.value})}
                    placeholder="wrench, calendar, settings"
                  />
                </div>
              </div>

              <div className="space-y-2">
                <Label htmlFor="color">Color</Label>
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
                    placeholder="#000000"
                    className="flex-1"
                  />
                </div>
              </div>

              <div className="space-y-2">
                <Label htmlFor="tools">Tools Required</Label>
                <Textarea
                  id="tools"
                  value={formData.toolsRequired}
                  onChange={(e) => setFormData({...formData, toolsRequired: e.target.value})}
                  placeholder="List required tools and equipment..."
                  rows={2}
                />
              </div>

              <div className="space-y-2">
                <Label htmlFor="safety">Safety Requirements</Label>
                <Textarea
                  id="safety"
                  value={formData.safetyRequirements}
                  onChange={(e) => setFormData({...formData, safetyRequirements: e.target.value})}
                  placeholder="Describe safety requirements and PPE..."
                  rows={2}
                />
              </div>
              
              <div className="flex items-center space-x-4">
                <div className="flex items-center space-x-2">
                  <Switch
                    id="downtime"
                    checked={formData.requiresDowntime}
                    onCheckedChange={(checked) => setFormData({...formData, requiresDowntime: checked})}
                  />
                  <Label htmlFor="downtime">Requires Equipment Downtime</Label>
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
                Add Maintenance Type
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
            <BreadcrumbPage>Maintenance Types</BreadcrumbPage>
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
                <p className="text-sm text-muted-foreground">Total Types</p>
              </div>
              <Wrench className="h-8 w-8 text-blue-500" />
            </div>
          </CardContent>
        </Card>
        
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-2xl font-bold">
                  {filteredData.filter(type => type.isActive).length}
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
                  {filteredData.filter(type => type.priority === 'Critical' || type.priority === 'High').length}
                </p>
                <p className="text-sm text-muted-foreground">High Priority</p>
              </div>
              <AlertTriangle className="h-8 w-8 text-red-500" />
            </div>
          </CardContent>
        </Card>
        
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-2xl font-bold">
                  {Math.round(filteredData.reduce((sum, type) => sum + type.estimatedDuration, 0) / filteredData.length)}
                </p>
                <p className="text-sm text-muted-foreground">Avg Duration (min)</p>
              </div>
              <Clock className="h-8 w-8 text-purple-500" />
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
                placeholder="Search maintenance types..."
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

            <Select value={categoryFilter} onValueChange={setCategoryFilter}>
              <SelectTrigger>
                <SelectValue placeholder="Category" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Categories</SelectItem>
                {categories.slice(1).map(category => (
                  <SelectItem key={category} value={category}>{category}</SelectItem>
                ))}
              </SelectContent>
            </Select>

            <Select value={priorityFilter} onValueChange={setPriorityFilter}>
              <SelectTrigger>
                <SelectValue placeholder="Priority" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Priorities</SelectItem>
                {priorities.map(priority => (
                  <SelectItem key={priority} value={priority}>{priority}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
        </CardContent>
      </Card>

      {/* Maintenance Types List */}
      <Card>
        <CardHeader>
          <CardTitle>Maintenance Types</CardTitle>
          <CardDescription>
            {filteredData.length} type{filteredData.length === 1 ? '' : 's'} found
          </CardDescription>
        </CardHeader>
        <CardContent>
          <div className="space-y-4">
            {filteredData.map((type) => (
              <div key={type.id} className="border rounded-lg p-4 hover:bg-muted/50 transition-colors">
                <div className="flex items-start justify-between">
                  <div className="space-y-3 flex-1">
                    <div className="flex items-center space-x-3">
                      <div 
                        className="w-4 h-4 rounded-full" 
                        style={{ backgroundColor: type.color }}
                      />
                      <h3 className="font-semibold">{type.name}</h3>
                      <Badge variant="outline">{type.code}</Badge>
                      <Badge className={type.isActive ? 'bg-green-100 text-green-800' : 'bg-gray-100 text-gray-800'}>
                        {type.isActive ? 'Active' : 'Inactive'}
                      </Badge>
                      <Badge className={getPriorityColor(type.priority)}>
                        {type.priority}
                      </Badge>
                      <Badge variant="secondary">{type.category}</Badge>
                    </div>
                    
                    <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4 text-sm text-muted-foreground">
                      <div>
                        <span className="font-medium">Duration:</span> {type.estimatedDuration} min
                      </div>
                      <div>
                        <span className="font-medium">Skill Level:</span> {type.skillLevel}
                      </div>
                      <div>
                        <span className="font-medium">Frequency:</span> {type.frequency}
                      </div>
                      <div>
                        <span className="font-medium">Downtime:</span> {type.requiresDowntime ? 'Yes' : 'No'}
                      </div>
                    </div>
                    
                    <p className="text-sm text-muted-foreground">{type.description}</p>
                  </div>
                  
                  <div className="flex items-center space-x-2">
                    <Button size="sm" variant="outline" onClick={() => handleEdit(type)}>
                      <Edit className="h-4 w-4" />
                    </Button>
                    <Button 
                      size="sm" 
                      variant="outline" 
                      onClick={() => handleDelete(type.id)}
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
        <DialogContent className="sm:max-w-[700px]">
          <DialogHeader>
            <DialogTitle>Edit Maintenance Type</DialogTitle>
            <DialogDescription>
              Update the maintenance type information.
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 py-4 max-h-[70vh] overflow-y-auto">
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label htmlFor="edit-name">Type Name</Label>
                <Input
                  id="edit-name"
                  value={formData.name}
                  onChange={(e) => setFormData({...formData, name: e.target.value})}
                  placeholder="Enter maintenance type name"
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="edit-code">Type Code</Label>
                <Input
                  id="edit-code"
                  value={formData.code}
                  onChange={(e) => setFormData({...formData, code: e.target.value.toUpperCase()})}
                  placeholder="e.g., PM, CM, EM"
                />
              </div>
            </div>
            
            <div className="space-y-2">
              <Label htmlFor="edit-description">Description</Label>
              <Textarea
                id="edit-description"
                value={formData.description}
                onChange={(e) => setFormData({...formData, description: e.target.value})}
                placeholder="Describe this maintenance type..."
                rows={3}
              />
            </div>
            
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label htmlFor="edit-category">Category</Label>
                <Select value={formData.category} onValueChange={(value) => setFormData({...formData, category: value})}>
                  <SelectTrigger>
                    <SelectValue placeholder="Select category" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="Preventive">Preventive</SelectItem>
                    <SelectItem value="Corrective">Corrective</SelectItem>
                    <SelectItem value="Emergency">Emergency</SelectItem>
                    <SelectItem value="Predictive">Predictive</SelectItem>
                    <SelectItem value="Condition-Based">Condition-Based</SelectItem>
                    <SelectItem value="Scheduled">Scheduled</SelectItem>
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-2">
                <Label htmlFor="edit-priority">Default Priority</Label>
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

            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label htmlFor="edit-duration">Estimated Duration (hours)</Label>
                <Input
                  id="edit-duration"
                  type="number"
                  step="0.5"
                  value={formData.estimatedDuration}
                  onChange={(e) => setFormData({...formData, estimatedDuration: parseFloat(e.target.value) || 0})}
                  placeholder="2.5"
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="edit-skill">Required Skill Level</Label>
                <Select value={formData.skillLevel} onValueChange={(value) => setFormData({...formData, skillLevel: value})}>
                  <SelectTrigger>
                    <SelectValue placeholder="Select skill level" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="Basic">Basic</SelectItem>
                    <SelectItem value="Intermediate">Intermediate</SelectItem>
                    <SelectItem value="Advanced">Advanced</SelectItem>
                    <SelectItem value="Expert">Expert</SelectItem>
                  </SelectContent>
                </Select>
              </div>
            </div>

            <div className="space-y-2">
              <Label htmlFor="edit-color">Color</Label>
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
                  placeholder="#3b82f6"
                  className="flex-1"
                />
              </div>
            </div>

            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label htmlFor="edit-frequency">Frequency</Label>
                <Select value={formData.frequency} onValueChange={(value) => setFormData({...formData, frequency: value})}>
                  <SelectTrigger>
                    <SelectValue placeholder="Select frequency" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="As Needed">As Needed</SelectItem>
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
                <Label htmlFor="edit-icon">Icon</Label>
                <Select value={formData.icon} onValueChange={(value) => setFormData({...formData, icon: value})}>
                  <SelectTrigger>
                    <SelectValue placeholder="Select icon" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="wrench">🔧 Wrench</SelectItem>
                    <SelectItem value="calendar">📅 Calendar</SelectItem>
                    <SelectItem value="alert-triangle">⚠️ Alert Triangle</SelectItem>
                    <SelectItem value="zap">⚡ Lightning</SelectItem>
                    <SelectItem value="settings">⚙️ Settings</SelectItem>
                    <SelectItem value="tool">🔨 Tool</SelectItem>
                  </SelectContent>
                </Select>
              </div>
            </div>

            <div className="space-y-2">
              <Label htmlFor="edit-safety">Safety Requirements</Label>
              <Textarea
                id="edit-safety"
                value={formData.safetyRequirements}
                onChange={(e) => setFormData({...formData, safetyRequirements: e.target.value})}
                placeholder="List safety requirements and protocols..."
                rows={2}
              />
            </div>

            <div className="space-y-2">
              <Label htmlFor="edit-tools">Tools Required</Label>
              <Input
                id="edit-tools"
                value={formData.toolsRequired}
                onChange={(e) => setFormData({...formData, toolsRequired: e.target.value})}
                placeholder="List required tools (comma-separated)"
              />
            </div>

            <div className="space-y-2">
              <Label htmlFor="edit-notes">Notes</Label>
              <Textarea
                id="edit-notes"
                value={formData.notes}
                onChange={(e) => setFormData({...formData, notes: e.target.value})}
                placeholder="Additional notes or instructions..."
                rows={3}
              />
            </div>
            
            <div className="flex items-center space-x-4">
              <div className="flex items-center space-x-2">
                <Switch
                  id="edit-downtime"
                  checked={formData.requiresDowntime}
                  onCheckedChange={(checked) => setFormData({...formData, requiresDowntime: checked})}
                />
                <Label htmlFor="edit-downtime">Requires Downtime</Label>
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
            <Button onClick={handleUpdate}>Update Maintenance Type</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
      </Dialog>
    </div>
  );
}