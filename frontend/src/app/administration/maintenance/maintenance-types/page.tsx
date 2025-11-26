'use client';

import React, { useState, useEffect, useMemo } from 'react';
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
import { apiService } from '@/services/api.service';

// Maintenance types interface
interface MaintenanceType {
  id: string | number;
  name: string;
  code: string;
  description?: string;
  category?: string;
  location?: string; // Internal, External, Onsite, Offsite
  isActive: boolean;
  priority?: string;
  color?: string;
  icon?: string;
  estimatedDuration?: number;
  requiresDowntime?: boolean;
  skillLevel?: string;
  frequency?: string;
}

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
  const [selectedType, setSelectedType] = useState<MaintenanceType | null>(null);
  const [maintenanceTypesData, setMaintenanceTypesData] = useState<MaintenanceType[]>([]);
  const [loading, setLoading] = useState(true);
  const [mounted, setMounted] = useState(false);
  const [error, setError] = useState<string | null>(null);
  
  // Form state
  const [formData, setFormData] = useState({
    name: '',
    code: '',
    description: '',
    category: 'Preventive',
    location: 'Internal',
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

  // Fetch maintenance types from API using shared apiService
  const fetchMaintenanceTypes = async () => {
    try {
      setLoading(true);
      setError(null);

      const result = await apiService.request<any>('/maintenance/maintenance-types?pageSize=1000');
      const data = (result as any).data || (result as any).items || result || [];
      setMaintenanceTypesData(data);
    } catch (error: any) {
      console.error('Error fetching maintenance types:', error);
      setError(error?.message || 'Error loading maintenance types');
      setMaintenanceTypesData([]);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchMaintenanceTypes();
  }, []);

  useEffect(() => {
    setMounted(true);
  }, []);

  // Memoized filtered data
  const filteredData = useMemo(() => {
    let filtered = maintenanceTypesData;

    if (searchTerm) {
      filtered = filtered.filter(item =>
        item.name?.toLowerCase().includes(searchTerm.toLowerCase()) ||
        item.code?.toLowerCase().includes(searchTerm.toLowerCase()) ||
        item.description?.toLowerCase().includes(searchTerm.toLowerCase())
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

    return filtered;
  }, [maintenanceTypesData, searchTerm, statusFilter, categoryFilter, priorityFilter]);

  const handleCreate = async () => {
    try {
      // Map form data to CreateMaintenanceTypeDto structure
      const createDto = {
        name: formData.name,
        code: formData.code,
        description: formData.description || '',
        category: formData.category,
        location: formData.location || 'Internal',
        priority: formData.priority,
        estimatedDuration: formData.estimatedDuration / 60, // Convert minutes to hours
        isActive: formData.isActive,
        requiresDowntime: formData.requiresDowntime,
        color: formData.color,
        icon: formData.icon,
        frequency: formData.frequency,
        skillLevel: formData.skillLevel,
        safetyRequirements: formData.safetyRequirements || '',
        toolsRequired: formData.toolsRequired || '',
        notes: formData.notes || ''
      };

      console.log('Sending create request:', createDto);

      const newMaintenanceType = await apiService.post<any>('/maintenance/maintenance-types', createDto);
      setMaintenanceTypesData(prev => [...prev, newMaintenanceType]);
      setIsCreateDialogOpen(false);
      resetForm();
      console.log('Maintenance type created successfully');
    } catch (error) {
      console.error('Error creating maintenance type:', error);
      setError(error instanceof Error ? error.message : 'Failed to create maintenance type');
    }
  };

  const handleEdit = (type: any) => {
    setSelectedType(type);
    setFormData({
      name: type.name || '',
      code: type.code || '',
      description: type.description || '',
      category: type.category || 'Preventive',
      location: type.location || 'Internal',
      isActive: type.isActive ?? true,
      priority: type.priority || 'Medium',
      color: type.color || '#10b981',
      icon: type.icon || 'wrench',
      estimatedDuration: (type.estimatedDuration || 2) * 60, // Convert hours to minutes for form
      requiresDowntime: type.requiresDowntime ?? false,
      skillLevel: type.skillLevel || 'Intermediate',
      frequency: type.frequency || 'Monthly',
      safetyRequirements: type.safetyRequirements || '',
      toolsRequired: type.toolsRequired || '',
      notes: type.notes || ''
    });
    setIsEditDialogOpen(true);
  };

  const handleUpdate = async () => {
    if (!selectedType?.id) return;

    try {
      const updateDto = {
        name: formData.name,
        code: formData.code,
        description: formData.description || '',
        category: formData.category,
        location: formData.location || 'Internal',
        priority: formData.priority,
        estimatedDuration: formData.estimatedDuration / 60, // Convert minutes to hours
        isActive: formData.isActive,
        requiresDowntime: formData.requiresDowntime,
        color: formData.color,
        icon: formData.icon,
        frequency: formData.frequency,
        skillLevel: formData.skillLevel,
        safetyRequirements: formData.safetyRequirements || '',
        toolsRequired: formData.toolsRequired || '',
        notes: formData.notes || ''
      };

      console.log('Sending update request:', updateDto);

      const updatedMaintenanceType = await apiService.put<any>(`/maintenance/maintenance-types/${selectedType.id}`, updateDto);
      setMaintenanceTypesData(prev =>
        prev.map(item => (item.id === selectedType.id ? updatedMaintenanceType : item))
      );
      setIsEditDialogOpen(false);
      resetForm();
      console.log('Maintenance type updated successfully');
    } catch (error) {
      console.error('Error updating maintenance type:', error);
      setError(error instanceof Error ? error.message : 'Failed to update maintenance type');
    }
  };

  const handleDelete = async (id: string | number) => {
    if (!confirm('Are you sure you want to delete this maintenance type?')) {
      return;
    }

    try {
      await apiService.delete(`/maintenance/maintenance-types/${id}`);
      setMaintenanceTypesData(prev => prev.filter(item => item.id !== id));
      console.log('Maintenance type deleted successfully');
    } catch (error) {
      console.error('Error deleting maintenance type:', error);
      setError(error instanceof Error ? error.message : 'Failed to delete maintenance type');
    }
  };

  const resetForm = () => {
    setFormData({
      name: '',
      code: '',
      description: '',
      category: 'Preventive',
      location: 'Internal',
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
        {mounted && <Dialog open={isCreateDialogOpen} onOpenChange={setIsCreateDialogOpen}>
          <DialogTrigger asChild>
            <Button>
              <Plus className="mr-2 h-4 w-4" />
              Add Maintenance Type
            </Button>
          </DialogTrigger>
        <DialogContent className="max-w-[900px]">
            <DialogHeader>
              <DialogTitle>Add Maintenance Type</DialogTitle>
              <DialogDescription>
                Create a new maintenance type to categorize maintenance activities.
              </DialogDescription>
            </DialogHeader>
            <div className="grid gap-4 py-4 max-h-[60vh] overflow-y-auto">
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
                  <Label htmlFor="location">Location Type</Label>
                  <Select value={formData.location} onValueChange={(value) => setFormData({...formData, location: value})}>
                    <SelectTrigger>
                      <SelectValue placeholder="Select location type" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="Internal">Internal</SelectItem>
                      <SelectItem value="External">External</SelectItem>
                      <SelectItem value="Onsite">Onsite</SelectItem>
                      <SelectItem value="Offsite">Offsite</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
              </div>

              <div className="grid grid-cols-2 gap-4">
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
              </div>

              <div className="grid grid-cols-2 gap-4">
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
        </Dialog>}
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
                  {filteredData.length > 0 
                    ? Math.round(filteredData.reduce((sum, type) => sum + (type.estimatedDuration || 0), 0) / filteredData.length)
                    : 0
                  }
                </p>
                <p className="text-sm text-muted-foreground">Avg Duration (hrs)</p>
              </div>
              <Clock className="h-8 w-8 text-purple-500" />
            </div>
          </CardContent>
        </Card>
      </div>

      {/* Error Display */}
      {error && (
        <Card className="border-red-200 bg-red-50">
          <CardContent className="pt-6">
            <div className="flex items-center space-x-2 text-red-700">
              <AlertTriangle className="h-5 w-5" />
              <p className="font-medium">{error}</p>
            </div>
          </CardContent>
        </Card>
      )}

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
            
            {mounted && (
              <>
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
              </>
            )}
            {!mounted && (
              <>
                <div className="h-10 bg-gray-100 rounded-md animate-pulse" />
                <div className="h-10 bg-gray-100 rounded-md animate-pulse" />
                <div className="h-10 bg-gray-100 rounded-md animate-pulse" />
              </>
            )}
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
          {loading ? (
            <div className="space-y-4">
              {[...Array(3)].map((_, index) => (
                <div key={index} className="border rounded-lg p-4 animate-pulse">
                  <div className="flex items-start justify-between">
                    <div className="space-y-3 flex-1">
                      <div className="flex items-center space-x-3">
                        <div className="w-4 h-4 rounded-full bg-gray-200" />
                        <div className="h-4 bg-gray-200 rounded w-32" />
                        <div className="h-6 bg-gray-200 rounded w-16" />
                        <div className="h-6 bg-gray-200 rounded w-20" />
                      </div>
                      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
                        {[...Array(4)].map((_, i) => (
                          <div key={i} className="h-4 bg-gray-200 rounded w-24" />
                        ))}
                      </div>
                      <div className="h-4 bg-gray-200 rounded w-full" />
                    </div>
                  </div>
                </div>
              ))}
            </div>
          ) : filteredData.length === 0 ? (
            <div className="text-center py-12">
              <Wrench className="mx-auto h-12 w-12 text-muted-foreground mb-4" />
              <h3 className="text-lg font-semibold mb-2">No maintenance types found</h3>
              <p className="text-muted-foreground mb-4">
                {error ? 'There was an error loading maintenance types.' : 
                 searchTerm || statusFilter !== 'all' || categoryFilter !== 'all' || priorityFilter !== 'all'
                   ? 'No maintenance types match your current filters.'
                   : 'Get started by creating your first maintenance type.'}
              </p>
              {!error && (
                <Button onClick={() => setIsCreateDialogOpen(true)}>
                  <Plus className="mr-2 h-4 w-4" />
                  Add First Maintenance Type
                </Button>
              )}
            </div>
          ) : (
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
                        <span className="font-medium">Duration:</span> {type.estimatedDuration} hrs
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
          )}
        </CardContent>
      </Card>

      {/* Edit Dialog */}
      {mounted && <Dialog open={isEditDialogOpen} onOpenChange={setIsEditDialogOpen}>
        <DialogContent className="max-w-[900px]">
          <DialogHeader>
            <DialogTitle>Edit Maintenance Type {selectedType?.code ? `- ${selectedType.code}` : ''}</DialogTitle>
            <DialogDescription>
              Update the maintenance type information.
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 py-4 max-h-[60vh] overflow-y-auto">
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
                rows={2}
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
                <Label htmlFor="edit-location">Location Type</Label>
                <Select value={formData.location} onValueChange={(value) => setFormData({...formData, location: value})}>
                  <SelectTrigger>
                    <SelectValue placeholder="Select location type" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="Internal">Internal</SelectItem>
                    <SelectItem value="External">External</SelectItem>
                    <SelectItem value="Onsite">Onsite</SelectItem>
                    <SelectItem value="Offsite">Offsite</SelectItem>
                  </SelectContent>
                </Select>
              </div>
            </div>

            <div className="grid grid-cols-2 gap-4">
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
                rows={1}
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
                rows={2}
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
      </Dialog>}
    </div>
  );
}