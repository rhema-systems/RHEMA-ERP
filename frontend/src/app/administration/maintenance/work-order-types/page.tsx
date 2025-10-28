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
  FileText,
  Settings,
  AlertTriangle,
  Wrench,
  Clock
} from 'lucide-react';

// Work order type interface
interface WorkOrderType {
  id: string | number;
  name: string;
  code: string;
  description?: string;
  priority?: string;
  color?: string;
  estimatedHours?: number;
  isActive: boolean;
  requiresApproval?: boolean;
  category?: string;
  autoAssign?: boolean;
  slaHours?: number;
}

export default function WorkOrderTypesPage() {
  const [searchTerm, setSearchTerm] = useState('');
  const [priorityFilter, setPriorityFilter] = useState('all');
  const [categoryFilter, setCategoryFilter] = useState('all');
  const [statusFilter, setStatusFilter] = useState('all');
  const [isCreateDialogOpen, setIsCreateDialogOpen] = useState(false);
  const [isEditDialogOpen, setIsEditDialogOpen] = useState(false);
  const [selectedType, setSelectedType] = useState<WorkOrderType | null>(null);
  const [workOrderTypesData, setWorkOrderTypesData] = useState<WorkOrderType[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [mounted, setMounted] = useState(false);
  
  // Form state
  const [formData, setFormData] = useState({
    name: '',
    code: '',
    description: '',
    priority: 'Medium',
    color: '#3b82f6',
    estimatedHours: 2,
    isActive: true,
    requiresApproval: false,
    category: 'Maintenance',
    autoAssign: false,
    slaHours: 24
  });

  // Fetch work order types from API
  const fetchWorkOrderTypes = async () => {
    try {
      setLoading(true);
      setError(null);
      
      const token = localStorage.getItem('token') || localStorage.getItem('authToken');
      
      if (!token) {
        setError('Authentication required. Please log in to access this page.');
        setWorkOrderTypesData([]);
        return;
      }
      
      console.log('Fetching work order types with token present:', !!token);
      
      const response = await fetch('http://localhost:5000/api/maintenance/work-order-types?pageSize=1000', {
        headers: {
          'Authorization': `Bearer ${token}`,
          'Content-Type': 'application/json'
        }
      });
      
      console.log('API Response Status:', response.status, response.statusText);
      
      if (!response.ok) {
        if (response.status === 401) {
          setError('Authentication failed. Please log in again or check your credentials.');
        } else if (response.status === 403) {
          setError('Access denied. You do not have permission to view work order types.');
        } else {
          const errorText = await response.text();
          console.error('Error details:', errorText);
          setError(`Failed to load work order types: ${errorText}`);
        }
        setWorkOrderTypesData([]);
        return;
      }
      
      const data = await response.json();
      console.log('API Response:', data);
      setWorkOrderTypesData(data.data || data.items || data || []);
      
    } catch (error) {
      console.error('Error fetching work order types:', error);
      setError('Network error occurred while fetching work order types.');
      setWorkOrderTypesData([]);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchWorkOrderTypes();
  }, []);

  useEffect(() => {
    setMounted(true);
  }, []);

  // Memoized filtered data
  const filteredData = useMemo(() => {
    let filtered = workOrderTypesData;

    if (searchTerm) {
      filtered = filtered.filter(item =>
        item.name?.toLowerCase().includes(searchTerm.toLowerCase()) ||
        item.code?.toLowerCase().includes(searchTerm.toLowerCase()) ||
        item.description?.toLowerCase().includes(searchTerm.toLowerCase())
      );
    }

    if (priorityFilter !== 'all') {
      filtered = filtered.filter(item => {
        const priorityString = getPriorityString(item.defaultPriority);
        return priorityString.toLowerCase() === priorityFilter;
      });
    }

    // Category filter removed since it doesn't exist in backend DTO
    // if (categoryFilter !== 'all') {
    //   filtered = filtered.filter(item => item.category?.toLowerCase() === categoryFilter);
    // }

    if (statusFilter !== 'all') {
      filtered = filtered.filter(item => 
        statusFilter === 'active' ? item.isActive : !item.isActive
      );
    }

    return filtered;
  }, [workOrderTypesData, searchTerm, priorityFilter, categoryFilter, statusFilter]);

  const getPriorityBadge = (priority: string) => {
    const colors = {
      'Low': 'bg-green-100 text-green-800',
      'Medium': 'bg-blue-100 text-blue-800',
      'High': 'bg-orange-100 text-orange-800',
      'Critical': 'bg-red-100 text-red-800',
    } as any;

    return (
      <Badge className={colors[priority] || 'bg-gray-100 text-gray-800'}>
        {priority}
      </Badge>
    );
  };

  const getPriorityNumber = (priority: string) => {
    const priorityMap = {
      'Low': 1,
      'Medium': 2, 
      'High': 3,
      'Critical': 4
    } as any;
    return priorityMap[priority] || 2;
  };

  const getPriorityString = (priority: number) => {
    const priorityMap = {
      1: 'Low',
      2: 'Medium',
      3: 'High', 
      4: 'Critical'
    } as any;
    return priorityMap[priority] || 'Medium';
  };

  const handleCreate = async () => {
    try {
      const token = localStorage.getItem('token') || localStorage.getItem('authToken');
      
      if (!token) {
        setError('Authentication required. Please log in to create work order types.');
        return;
      }
      
      // Map form data to CreateWorkOrderTypeDto structure
      const createDto = {
        name: formData.name,
        code: formData.code,
        description: formData.description,
        color: formData.color,
        icon: 'wrench', // Default icon since backend expects this
        isActive: formData.isActive,
        requiresApproval: formData.requiresApproval,
        defaultPriority: getPriorityNumber(formData.priority)
      };

      console.log('Sending create request:', createDto);

      const response = await fetch('http://localhost:5000/api/maintenance/work-order-types', {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${token}`,
          'Content-Type': 'application/json'
        },
        body: JSON.stringify(createDto)
      });
      
      if (!response.ok) {
        const errorText = await response.text();
        console.error('Create failed:', response.status, response.statusText, errorText);
        setError(`Failed to create work order type: ${response.status} ${response.statusText} - ${errorText}`);
        return;
      }
      
      // Refresh the list
      await fetchWorkOrderTypes();
      setIsCreateDialogOpen(false);
      resetForm();
    } catch (error) {
      console.error('Error creating work order type:', error);
      setError('Failed to create work order type. Please try again.');
    }
  };

  const handleEdit = (type: any) => {
    setSelectedType(type);
    setFormData({
      name: type.name || '',
      code: type.code || '',
      description: type.description || '',
      priority: getPriorityString(type.defaultPriority) || 'Medium',
      color: type.color || '#3b82f6',
      estimatedHours: 2, // This field doesn't exist in backend DTO
      isActive: type.isActive ?? true,
      requiresApproval: type.requiresApproval ?? false,
      category: 'Maintenance', // This field doesn't exist in backend DTO
      autoAssign: false, // This field doesn't exist in backend DTO
      slaHours: 24 // This field doesn't exist in backend DTO
    });
    setIsEditDialogOpen(true);
  };

  const handleUpdate = async () => {
    if (!selectedType?.id) return;
    
    try {
      const token = localStorage.getItem('token') || localStorage.getItem('authToken');
      
      if (!token) {
        setError('Authentication required. Please log in to update work order types.');
        return;
      }
      
      // Map form data to UpdateWorkOrderTypeDto structure
      const updateDto = {
        name: formData.name,
        code: formData.code,
        description: formData.description,
        color: formData.color,
        icon: 'wrench', // Default icon since backend expects this
        isActive: formData.isActive,
        requiresApproval: formData.requiresApproval,
        defaultPriority: getPriorityNumber(formData.priority)
      };

      console.log('Sending update request for ID:', selectedType.id, 'DTO:', updateDto);

      const response = await fetch(`http://localhost:5000/api/maintenance/work-order-types/${selectedType.id}`, {
        method: 'PUT',
        headers: {
          'Authorization': `Bearer ${token}`,
          'Content-Type': 'application/json'
        },
        body: JSON.stringify(updateDto)
      });
      
      if (!response.ok) {
        const errorText = await response.text();
        console.error('Update failed:', response.status, response.statusText, errorText);
        setError(`Failed to update work order type: ${response.status} ${response.statusText} - ${errorText}`);
        return;
      }
      
      const updatedWorkOrderType = await response.json();
      console.log('Update response received:', updatedWorkOrderType);
      
      // Refresh the list
      await fetchWorkOrderTypes();
      setIsEditDialogOpen(false);
      resetForm();
    } catch (error) {
      console.error('Error updating work order type:', error);
      setError('Failed to update work order type. Please try again.');
    }
  };

  const handleDelete = async (id: string | number) => {
    if (!confirm('Are you sure you want to delete this work order type?')) return;
    
    try {
      const token = localStorage.getItem('token') || localStorage.getItem('authToken');
      
      if (!token) {
        setError('Authentication required. Please log in to delete work order types.');
        return;
      }
      
      const response = await fetch(`http://localhost:5000/api/maintenance/work-order-types/${id}`, {
        method: 'DELETE',
        headers: {
          'Authorization': `Bearer ${token}`,
          'Content-Type': 'application/json'
        }
      });
      
      if (!response.ok) {
        throw new Error('Failed to delete work order type');
      }
      
      // Refresh the list
      await fetchWorkOrderTypes();
    } catch (error) {
      console.error('Error deleting work order type:', error);
      setError('Failed to delete work order type. Please try again.');
    }
  };

  const resetForm = () => {
    setFormData({
      name: '',
      code: '',
      description: '',
      priority: 'Medium',
      color: '#3b82f6',
      isActive: true,
      requiresApproval: false,
      // Legacy fields kept for form compatibility
      estimatedHours: 2,
      category: 'Maintenance',
      autoAssign: false,
      slaHours: 24
    });
    setSelectedType(null);
  };

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Work Order Types</h1>
          <p className="text-muted-foreground">
            Define and manage different types of maintenance work orders
          </p>
        </div>
        {mounted && <Dialog open={isCreateDialogOpen} onOpenChange={setIsCreateDialogOpen}>
          <DialogTrigger asChild>
            <Button>
              <Plus className="mr-2 h-4 w-4" />
              Add Type
            </Button>
          </DialogTrigger>
          <DialogContent className="sm:max-w-[600px]">
            <DialogHeader>
              <DialogTitle>Add Work Order Type</DialogTitle>
              <DialogDescription>
                Create a new work order type for maintenance activities.
              </DialogDescription>
            </DialogHeader>
            <div className="grid gap-4 py-4">
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="name">Type Name</Label>
                  <Input
                    id="name"
                    value={formData.name}
                    onChange={(e) => setFormData({...formData, name: e.target.value})}
                    placeholder="e.g., Preventive Maintenance"
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="code">Type Code</Label>
                  <Input
                    id="code"
                    value={formData.code}
                    onChange={(e) => setFormData({...formData, code: e.target.value.toUpperCase()})}
                    placeholder="e.g., PM"
                  />
                </div>
              </div>
              
              <div className="space-y-2">
                <Label htmlFor="description">Description</Label>
                <Textarea
                  id="description"
                  value={formData.description}
                  onChange={(e) => setFormData({...formData, description: e.target.value})}
                  placeholder="Description of the work order type..."
                  rows={3}
                />
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
                  <Label htmlFor="color">Color</Label>
                  <Input
                    id="color"
                    type="color"
                    value={formData.color}
                    onChange={(e) => setFormData({...formData, color: e.target.value})}
                  />
                </div>
              </div>
              
              <div className="space-y-4">
                <div className="flex items-center space-x-2">
                  <Switch
                    id="active"
                    checked={formData.isActive}
                    onCheckedChange={(checked) => setFormData({...formData, isActive: checked})}
                  />
                  <Label htmlFor="active">Active</Label>
                </div>
                
                <div className="flex items-center space-x-2">
                  <Switch
                    id="approval"
                    checked={formData.requiresApproval}
                    onCheckedChange={(checked) => setFormData({...formData, requiresApproval: checked})}
                  />
                  <Label htmlFor="approval">Requires Approval</Label>
                </div>
              </div>
            </div>
            <DialogFooter>
              <Button variant="outline" onClick={() => setIsCreateDialogOpen(false)}>
                Cancel
              </Button>
              <Button onClick={handleCreate}>
                Create Type
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
            <BreadcrumbPage>Work Order Types</BreadcrumbPage>
          </BreadcrumbItem>
        </BreadcrumbList>
      </Breadcrumb>

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
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            <div className="relative">
              <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
              <Input
                placeholder="Search types..."
                className="pl-8"
                value={searchTerm}
                onChange={(e) => setSearchTerm(e.target.value)}
              />
            </div>
            
            {mounted && (
              <>
                <Select value={priorityFilter} onValueChange={setPriorityFilter}>
                  <SelectTrigger>
                    <SelectValue placeholder="Priority" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="all">All Priorities</SelectItem>
                    <SelectItem value="low">Low</SelectItem>
                    <SelectItem value="medium">Medium</SelectItem>
                    <SelectItem value="high">High</SelectItem>
                    <SelectItem value="critical">Critical</SelectItem>
                  </SelectContent>
                </Select>

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
              </>
            )}
            {!mounted && (
              <>
                <div className="h-10 bg-gray-100 rounded-md animate-pulse" />
                <div className="h-10 bg-gray-100 rounded-md animate-pulse" />
              </>
            )}
          </div>
        </CardContent>
      </Card>

      {/* Work Order Types List */}
      <Card>
        <CardHeader>
          <CardTitle>Work Order Types</CardTitle>
          <CardDescription>
            {filteredData.length} type{filteredData.length === 1 ? '' : 's'} found
          </CardDescription>
        </CardHeader>
        <CardContent>
          {loading ? (
            <div className="text-center py-4">Loading work order types...</div>
          ) : error ? (
            <div className="text-center py-4 text-red-600">{error}</div>
          ) : filteredData.length === 0 ? (
            <div className="text-center py-4 text-muted-foreground">No work order types found</div>
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
                      {getPriorityBadge(getPriorityString(type.defaultPriority))}
                      <Badge className={type.isActive ? 'bg-green-100 text-green-800' : 'bg-gray-100 text-gray-800'}>
                        {type.isActive ? 'Active' : 'Inactive'}
                      </Badge>
                      {type.requiresApproval && (
                        <Badge variant="secondary">
                          <AlertTriangle className="w-3 h-3 mr-1" />
                          Requires Approval
                        </Badge>
                      )}
                    </div>
                    
                    <div className="grid grid-cols-1 md:grid-cols-3 gap-4 text-sm text-muted-foreground">
                      <div className="flex items-center space-x-2">
                        <Wrench className="h-4 w-4" />
                        <span>Code: {type.code}</span>
                      </div>
                      <div className="flex items-center space-x-2">
                        <AlertTriangle className="h-4 w-4" />
                        <span>Priority: {getPriorityString(type.defaultPriority)}</span>
                      </div>
                      <div className="flex items-center space-x-2">
                        <Clock className="h-4 w-4" />
                        <span>Status: {type.isActive ? 'Active' : 'Inactive'}</span>
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
                      className="text-red-600 hover:text-red-700"
                      onClick={() => handleDelete(type.id)}
                    >
                      <Trash2 className="h-4 w-4" />
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
        <DialogContent className="sm:max-w-[600px]">
          <DialogHeader>
            <DialogTitle>Edit Work Order Type</DialogTitle>
            <DialogDescription>
              Update the work order type information.
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 py-4">
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label htmlFor="edit-name">Type Name</Label>
                <Input
                  id="edit-name"
                  value={formData.name}
                  onChange={(e) => setFormData({...formData, name: e.target.value})}
                  placeholder="e.g., Preventive Maintenance"
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="edit-code">Type Code</Label>
                <Input
                  id="edit-code"
                  value={formData.code}
                  onChange={(e) => setFormData({...formData, code: e.target.value.toUpperCase()})}
                  placeholder="e.g., PM"
                />
              </div>
            </div>
            
            <div className="space-y-2">
              <Label htmlFor="edit-description">Description</Label>
              <Textarea
                id="edit-description"
                value={formData.description}
                onChange={(e) => setFormData({...formData, description: e.target.value})}
                placeholder="Description of the work order type..."
                rows={3}
              />
            </div>
            
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label htmlFor="edit-priority">Priority</Label>
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
            
            <div className="space-y-2">
              <Label htmlFor="edit-color">Color</Label>
              <Input
                id="edit-color"
                type="color"
                value={formData.color}
                onChange={(e) => setFormData({...formData, color: e.target.value})}
              />
            </div>
            
            <div className="space-y-4">
              <div className="flex items-center space-x-2">
                <Switch
                  id="edit-active"
                  checked={formData.isActive}
                  onCheckedChange={(checked) => setFormData({...formData, isActive: checked})}
                />
                <Label htmlFor="edit-active">Active</Label>
              </div>
              
              <div className="flex items-center space-x-2">
                <Switch
                  id="edit-approval"
                  checked={formData.requiresApproval}
                  onCheckedChange={(checked) => setFormData({...formData, requiresApproval: checked})}
                />
                <Label htmlFor="edit-approval">Requires Approval</Label>
              </div>
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setIsEditDialogOpen(false)}>
              Cancel
            </Button>
            <Button onClick={handleUpdate}>
              Update Type
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>}
    </div>
  );
}