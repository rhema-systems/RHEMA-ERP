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
  Package,
  MoreHorizontal,
  Settings,
  Tag
} from 'lucide-react';
import { cn } from '@/lib/utils';

// Mock data for asset categories
const assetCategoriesData = [
  {
    id: 1,
    name: 'HVAC Systems',
    code: 'HVAC',
    description: 'Heating, Ventilation, and Air Conditioning systems',
    parentCategory: null,
    isActive: true,
    maintenanceFrequency: 'Monthly',
    assetCount: 25,
    color: '#3b82f6',
    icon: 'hvac'
  },
  {
    id: 2,
    name: 'Electrical Equipment',
    code: 'ELEC',
    description: 'Electrical panels, generators, and power distribution',
    parentCategory: null,
    isActive: true,
    maintenanceFrequency: 'Quarterly',
    assetCount: 18,
    color: '#f59e0b',
    icon: 'electrical'
  },
  {
    id: 3,
    name: 'Plumbing Systems',
    code: 'PLUMB',
    description: 'Water supply, drainage, and plumbing fixtures',
    parentCategory: null,
    isActive: true,
    maintenanceFrequency: 'Bi-Annual',
    assetCount: 32,
    color: '#10b981',
    icon: 'plumbing'
  },
  {
    id: 4,
    name: 'Elevator Systems',
    code: 'ELEV',
    description: 'Elevators, escalators, and vertical transportation',
    parentCategory: null,
    isActive: true,
    maintenanceFrequency: 'Monthly',
    assetCount: 8,
    color: '#8b5cf6',
    icon: 'elevator'
  },
  {
    id: 5,
    name: 'Fire Safety Systems',
    code: 'FIRE',
    description: 'Fire suppression, detection, and safety equipment',
    parentCategory: null,
    isActive: true,
    maintenanceFrequency: 'Quarterly',
    assetCount: 15,
    color: '#ef4444',
    icon: 'fire'
  },
  {
    id: 6,
    name: 'HVAC Filters',
    code: 'HVAC-FILT',
    description: 'Air filters for HVAC systems',
    parentCategory: 1,
    isActive: true,
    maintenanceFrequency: 'Monthly',
    assetCount: 45,
    color: '#3b82f6',
    icon: 'filter'
  },
  {
    id: 7,
    name: 'Backup Generators',
    code: 'GEN',
    description: 'Emergency power generation equipment',
    parentCategory: 2,
    isActive: true,
    maintenanceFrequency: 'Monthly',
    assetCount: 3,
    color: '#f59e0b',
    icon: 'generator'
  }
];

export default function AssetCategoriesPage() {
  const [searchTerm, setSearchTerm] = useState('');
  const [statusFilter, setStatusFilter] = useState('all');
  const [parentFilter, setParentFilter] = useState('all');
  const [isCreateDialogOpen, setIsCreateDialogOpen] = useState(false);
  const [isEditDialogOpen, setIsEditDialogOpen] = useState(false);
  const [selectedCategory, setSelectedCategory] = useState(null);
  const [filteredData, setFilteredData] = useState(assetCategoriesData);
  
  // Form state
  const [formData, setFormData] = useState({
    name: '',
    code: '',
    description: '',
    parentCategory: '',
    isActive: true,
    maintenanceScheduleType: 'single', // 'single' or 'multi'
    maintenanceType: 'Time', // 'Time', 'Usage', 'Distance', 'Cycles'
    maintenanceFrequency: 'Monthly',
    maintenanceValue: '',
    maintenanceUnit: 'months',
    // Multi-criteria fields
    secondaryMaintenanceType: 'Distance',
    secondaryMaintenanceFrequency: 'Monthly',
    secondaryMaintenanceValue: '',
    secondaryMaintenanceUnit: 'km',
    color: '#3b82f6',
    icon: 'package'
  });

  useEffect(() => {
    let filtered = assetCategoriesData;

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

    if (parentFilter !== 'all') {
      if (parentFilter === 'parent') {
        filtered = filtered.filter(item => !item.parentCategory);
      } else if (parentFilter === 'child') {
        filtered = filtered.filter(item => item.parentCategory);
      }
    }

    setFilteredData(filtered);
  }, [searchTerm, statusFilter, parentFilter]);

  const handleCreate = () => {
    console.log('Creating asset category:', formData);
    setIsCreateDialogOpen(false);
    resetForm();
  };

  const handleEdit = (category: any) => {
    setSelectedCategory(category);
    setFormData({
      name: category.name,
      code: category.code,
      description: category.description,
      parentCategory: category.parentCategory?.toString() || '',
      isActive: category.isActive,
      maintenanceScheduleType: category.maintenanceScheduleType || 'single',
      maintenanceType: category.maintenanceType || 'Time',
      maintenanceFrequency: category.maintenanceFrequency || 'Monthly',
      maintenanceValue: category.maintenanceValue || '',
      maintenanceUnit: category.maintenanceUnit || 'months',
      secondaryMaintenanceType: category.secondaryMaintenanceType || 'Distance',
      secondaryMaintenanceFrequency: category.secondaryMaintenanceFrequency || 'Monthly',
      secondaryMaintenanceValue: category.secondaryMaintenanceValue || '',
      secondaryMaintenanceUnit: category.secondaryMaintenanceUnit || 'km',
      color: category.color,
      icon: category.icon
    });
    setIsEditDialogOpen(true);
  };

  const handleUpdate = () => {
    console.log('Updating asset category:', selectedCategory?.id, formData);
    setIsEditDialogOpen(false);
    resetForm();
  };

  const handleDelete = (id: number) => {
    console.log('Deleting asset category:', id);
  };

  const resetForm = () => {
    setFormData({
      name: '',
      code: '',
      description: '',
      parentCategory: '',
      isActive: true,
      maintenanceScheduleType: 'single',
      maintenanceType: 'Time',
      maintenanceFrequency: 'Monthly',
      maintenanceValue: '',
      maintenanceUnit: 'months',
      secondaryMaintenanceType: 'Distance',
      secondaryMaintenanceFrequency: 'Monthly',
      secondaryMaintenanceValue: '',
      secondaryMaintenanceUnit: 'km',
      color: '#3b82f6',
      icon: 'package'
    });
    setSelectedCategory(null);
  };

  const getParentCategoryName = (parentId: number | null) => {
    if (!parentId) return 'Root Category';
    const parent = assetCategoriesData.find(cat => cat.id === parentId);
    return parent ? parent.name : 'Unknown';
  };

  const formatSingleCriteria = (type: string, frequency?: string, value?: string, unit?: string) => {
    switch (type) {
      case 'Distance':
        return value && unit ? `${value} ${unit}` : 'Not configured';
      case 'Usage':
        return value && unit ? `${value} ${unit}` : 'Not configured';
      case 'Cycles':
        return value && unit ? `${value} ${unit}` : 'Not configured';
      case 'Time':
      default:
        return frequency || 'Monthly';
    }
  };

  const getMaintenanceScheduleDisplay = (category: any) => {
    const scheduleType = category.maintenanceScheduleType || 'single';
    
    if (scheduleType === 'multi') {
      const primary = formatSingleCriteria(
        category.maintenanceType || 'Time',
        category.maintenanceFrequency,
        category.maintenanceValue,
        category.maintenanceUnit
      );
      const secondary = formatSingleCriteria(
        category.secondaryMaintenanceType || 'Distance',
        category.secondaryMaintenanceFrequency,
        category.secondaryMaintenanceValue,
        category.secondaryMaintenanceUnit
      );
      return `Every ${primary} OR ${secondary} (whichever comes first)`;
    }
    
    // Single criteria (original logic)
    const formatted = formatSingleCriteria(
      category.maintenanceType || 'Time',
      category.maintenanceFrequency,
      category.maintenanceValue,
      category.maintenanceUnit
    );
    return `Every ${formatted}`;
  };

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Asset Categories</h1>
          <p className="text-muted-foreground">
            Manage asset categories and classification system
          </p>
        </div>
        <Dialog open={isCreateDialogOpen} onOpenChange={setIsCreateDialogOpen}>
          <DialogTrigger asChild>
            <Button>
              <Plus className="mr-2 h-4 w-4" />
              Add Category
            </Button>
          </DialogTrigger>
          <DialogContent className="sm:max-w-[600px]">
            <DialogHeader>
              <DialogTitle>Add Asset Category</DialogTitle>
              <DialogDescription>
                Create a new asset category to organize and classify your assets.
              </DialogDescription>
            </DialogHeader>
            <div className="grid gap-4 py-4">
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="name">Category Name</Label>
                  <Input
                    id="name"
                    value={formData.name}
                    onChange={(e) => setFormData({...formData, name: e.target.value})}
                    placeholder="Enter category name"
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="code">Category Code</Label>
                  <Input
                    id="code"
                    value={formData.code}
                    onChange={(e) => setFormData({...formData, code: e.target.value})}
                    placeholder="e.g., HVAC, ELEC"
                  />
                </div>
              </div>
              
              <div className="space-y-2">
                <Label htmlFor="description">Description</Label>
                <Textarea
                  id="description"
                  value={formData.description}
                  onChange={(e) => setFormData({...formData, description: e.target.value})}
                  placeholder="Describe this category..."
                  rows={3}
                />
              </div>
              
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="parent">Parent Category</Label>
                  <Select value={formData.parentCategory || 'none'} onValueChange={(value) => setFormData({...formData, parentCategory: value === 'none' ? null : value})}>
                    <SelectTrigger>
                      <SelectValue placeholder="None (root level)" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="none">None (root level)</SelectItem>
                      {assetCategoriesData.filter(cat => !cat.parentCategory).map((cat) => (
                        <SelectItem key={cat.id} value={cat.id.toString()}>
                          {cat.name}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-2">
                  <Label htmlFor="icon">Icon</Label>
                  <Input
                    id="icon"
                    value={formData.icon}
                    onChange={(e) => setFormData({...formData, icon: e.target.value})}
                    placeholder="Icon name or emoji"
                  />
                </div>
              </div>
              
              {/* Maintenance Schedule Type Selection */}
              <div className="space-y-4">
                <div className="space-y-2">
                  <Label className="text-base font-medium">Maintenance Schedule</Label>
                  <Select value={formData.maintenanceScheduleType} onValueChange={(value) => setFormData({...formData, maintenanceScheduleType: value})}>
                    <SelectTrigger>
                      <SelectValue placeholder="Select schedule type" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="single">Single Criteria (e.g., Every 6 months)</SelectItem>
                      <SelectItem value="multi">Multiple Criteria (e.g., Every 6 months OR 10,000 km)</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
              </div>
              
              {/* Primary Maintenance Criteria */}
              <div className="space-y-4">
                <div className="flex items-center space-x-2">
                  <Label className="text-base font-medium">
                    {formData.maintenanceScheduleType === 'multi' ? 'Primary Criteria' : 'Maintenance Criteria'}
                  </Label>
                  {formData.maintenanceScheduleType === 'multi' && (
                    <span className="text-sm text-muted-foreground">(First condition)</span>
                  )}
                </div>
                
                <div className="grid grid-cols-3 gap-4">
                  <div className="space-y-2">
                    <Label htmlFor="maintenanceType">Type</Label>
                    <Select value={formData.maintenanceType} onValueChange={(value) => {
                      const defaults = {
                        'Time': { unit: 'months', value: '' },
                        'Distance': { unit: 'km', value: '' },
                        'Usage': { unit: 'hours', value: '' },
                        'Cycles': { unit: 'cycles', value: '' }
                      };
                      setFormData({...formData, maintenanceType: value, maintenanceUnit: defaults[value]?.unit || 'months', maintenanceValue: defaults[value]?.value || ''});
                    }}>
                      <SelectTrigger>
                        <SelectValue placeholder="Select type" />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="Time">Time-based</SelectItem>
                        <SelectItem value="Distance">Distance/Mileage</SelectItem>
                        <SelectItem value="Usage">Usage/Hours</SelectItem>
                        <SelectItem value="Cycles">Cycles/Operations</SelectItem>
                      </SelectContent>
                    </Select>
                  </div>
                  
                  {formData.maintenanceType !== 'Time' && (
                    <>
                      <div className="space-y-2">
                        <Label htmlFor="maintenanceValue">Value</Label>
                        <Input
                          id="maintenanceValue"
                          type="number"
                          value={formData.maintenanceValue}
                          onChange={(e) => setFormData({...formData, maintenanceValue: e.target.value})}
                          placeholder="1000"
                        />
                      </div>
                      <div className="space-y-2">
                        <Label htmlFor="maintenanceUnit">Unit</Label>
                        <Select value={formData.maintenanceUnit} onValueChange={(value) => setFormData({...formData, maintenanceUnit: value})}>
                          <SelectTrigger>
                            <SelectValue placeholder="Select unit" />
                          </SelectTrigger>
                          <SelectContent>
                            {formData.maintenanceType === 'Distance' && (
                              <>
                                <SelectItem value="km">Kilometers</SelectItem>
                                <SelectItem value="miles">Miles</SelectItem>
                              </>
                            )}
                            {formData.maintenanceType === 'Usage' && (
                              <>
                                <SelectItem value="hours">Operating Hours</SelectItem>
                                <SelectItem value="runtime">Runtime Hours</SelectItem>
                              </>
                            )}
                            {formData.maintenanceType === 'Cycles' && (
                              <>
                                <SelectItem value="cycles">Cycles</SelectItem>
                                <SelectItem value="operations">Operations</SelectItem>
                                <SelectItem value="starts">Starts</SelectItem>
                              </>
                            )}
                          </SelectContent>
                        </Select>
                      </div>
                    </>
                  )}
                  
                  {formData.maintenanceType === 'Time' && (
                    <>
                      <div className="space-y-2">
                        <Label htmlFor="frequency">Frequency</Label>
                        <Select value={formData.maintenanceFrequency} onValueChange={(value) => setFormData({...formData, maintenanceFrequency: value})}>
                          <SelectTrigger>
                            <SelectValue placeholder="Select frequency" />
                          </SelectTrigger>
                          <SelectContent>
                            <SelectItem value="Weekly">Weekly</SelectItem>
                            <SelectItem value="Monthly">Monthly</SelectItem>
                            <SelectItem value="Quarterly">Quarterly</SelectItem>
                            <SelectItem value="Semi-Annual">Semi-Annual</SelectItem>
                            <SelectItem value="Annual">Annual</SelectItem>
                          </SelectContent>
                        </Select>
                      </div>
                      <div className="col-span-1"></div>
                    </>
                  )}
                </div>
              </div>
              
              {/* Secondary Criteria for Multi-schedule */}
              {formData.maintenanceScheduleType === 'multi' && (
                <div className="space-y-4">
                  <div className="flex items-center space-x-2">
                    <Label className="text-base font-medium">Secondary Criteria</Label>
                    <span className="text-sm text-muted-foreground">(Alternative condition - OR logic)</span>
                  </div>
                  
                  <div className="grid grid-cols-3 gap-4">
                    <div className="space-y-2">
                      <Label htmlFor="secondaryMaintenanceType">Type</Label>
                      <Select value={formData.secondaryMaintenanceType} onValueChange={(value) => {
                        const defaults = {
                          'Time': { unit: 'months', value: '' },
                          'Distance': { unit: 'km', value: '' },
                          'Usage': { unit: 'hours', value: '' },
                          'Cycles': { unit: 'cycles', value: '' }
                        };
                        setFormData({...formData, secondaryMaintenanceType: value, secondaryMaintenanceUnit: defaults[value]?.unit || 'km', secondaryMaintenanceValue: defaults[value]?.value || ''});
                      }}>
                        <SelectTrigger>
                          <SelectValue placeholder="Select type" />
                        </SelectTrigger>
                        <SelectContent>
                          <SelectItem value="Time">Time-based</SelectItem>
                          <SelectItem value="Distance">Distance/Mileage</SelectItem>
                          <SelectItem value="Usage">Usage/Hours</SelectItem>
                          <SelectItem value="Cycles">Cycles/Operations</SelectItem>
                        </SelectContent>
                      </Select>
                    </div>
                    
                    {formData.secondaryMaintenanceType !== 'Time' && (
                      <>
                        <div className="space-y-2">
                          <Label htmlFor="secondaryMaintenanceValue">Value</Label>
                          <Input
                            id="secondaryMaintenanceValue"
                            type="number"
                            value={formData.secondaryMaintenanceValue}
                            onChange={(e) => setFormData({...formData, secondaryMaintenanceValue: e.target.value})}
                            placeholder="10000"
                          />
                        </div>
                        <div className="space-y-2">
                          <Label htmlFor="secondaryMaintenanceUnit">Unit</Label>
                          <Select value={formData.secondaryMaintenanceUnit} onValueChange={(value) => setFormData({...formData, secondaryMaintenanceUnit: value})}>
                            <SelectTrigger>
                              <SelectValue placeholder="Select unit" />
                            </SelectTrigger>
                            <SelectContent>
                              {formData.secondaryMaintenanceType === 'Distance' && (
                                <>
                                  <SelectItem value="km">Kilometers</SelectItem>
                                  <SelectItem value="miles">Miles</SelectItem>
                                </>
                              )}
                              {formData.secondaryMaintenanceType === 'Usage' && (
                                <>
                                  <SelectItem value="hours">Operating Hours</SelectItem>
                                  <SelectItem value="runtime">Runtime Hours</SelectItem>
                                </>
                              )}
                              {formData.secondaryMaintenanceType === 'Cycles' && (
                                <>
                                  <SelectItem value="cycles">Cycles</SelectItem>
                                  <SelectItem value="operations">Operations</SelectItem>
                                  <SelectItem value="starts">Starts</SelectItem>
                                </>
                              )}
                            </SelectContent>
                          </Select>
                        </div>
                      </>
                    )}
                    
                    {formData.secondaryMaintenanceType === 'Time' && (
                      <>
                        <div className="space-y-2">
                          <Label htmlFor="secondaryFrequency">Frequency</Label>
                          <Select value={formData.secondaryMaintenanceFrequency} onValueChange={(value) => setFormData({...formData, secondaryMaintenanceFrequency: value})}>
                            <SelectTrigger>
                              <SelectValue placeholder="Select frequency" />
                            </SelectTrigger>
                            <SelectContent>
                              <SelectItem value="Weekly">Weekly</SelectItem>
                              <SelectItem value="Monthly">Monthly</SelectItem>
                              <SelectItem value="Quarterly">Quarterly</SelectItem>
                              <SelectItem value="Semi-Annual">Semi-Annual</SelectItem>
                              <SelectItem value="Annual">Annual</SelectItem>
                            </SelectContent>
                          </Select>
                        </div>
                        <div className="col-span-1"></div>
                      </>
                    )}
                  </div>
                  
                  <div className="bg-blue-50 p-3 rounded-lg">
                    <p className="text-sm text-blue-800">
                      <strong>Preview:</strong> {getMaintenanceScheduleDisplay(formData)}
                    </p>
                  </div>
                </div>
              )}
              
              <div className="grid grid-cols-2 gap-4">
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
                  <Label className="flex items-center space-x-2">
                    <input
                      type="checkbox"
                      checked={formData.isActive}
                      onChange={(e) => setFormData({...formData, isActive: e.target.checked})}
                      className="rounded border-gray-300"
                    />
                    <span>Active</span>
                  </Label>
                </div>
              </div>
            </div>
            <DialogFooter>
              <Button variant="outline" onClick={() => setIsCreateDialogOpen(false)}>
                Cancel
              </Button>
              <Button onClick={handleCreate}>
                Add Category
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
            <BreadcrumbPage>Asset Categories</BreadcrumbPage>
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
                <p className="text-sm text-muted-foreground">Total Categories</p>
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
                  {filteredData.filter(cat => cat.isActive).length}
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
                  {filteredData.filter(cat => !cat.parentCategory).length}
                </p>
                <p className="text-sm text-muted-foreground">Parent Categories</p>
              </div>
              <Package className="h-8 w-8 text-purple-500" />
            </div>
          </CardContent>
        </Card>
        
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-2xl font-bold">
                  {filteredData.reduce((sum, cat) => sum + cat.assetCount, 0)}
                </p>
                <p className="text-sm text-muted-foreground">Total Assets</p>
              </div>
              <Package className="h-8 w-8 text-orange-500" />
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
                placeholder="Search categories..."
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

            <Select value={parentFilter} onValueChange={setParentFilter}>
              <SelectTrigger>
                <SelectValue placeholder="Category Type" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Types</SelectItem>
                <SelectItem value="parent">Parent Categories</SelectItem>
                <SelectItem value="child">Sub Categories</SelectItem>
              </SelectContent>
            </Select>
          </div>
        </CardContent>
      </Card>

      {/* Categories List */}
      <Card>
        <CardHeader>
          <CardTitle>Asset Categories</CardTitle>
          <CardDescription>
            {filteredData.length} categor{filteredData.length === 1 ? 'y' : 'ies'} found
          </CardDescription>
        </CardHeader>
        <CardContent>
          <div className="space-y-4">
            {filteredData.map((category) => (
              <div key={category.id} className="border rounded-lg p-4 hover:bg-muted/50 transition-colors">
                <div className="flex items-start justify-between">
                  <div className="space-y-3 flex-1">
                    <div className="flex items-center space-x-3">
                      <div 
                        className="w-4 h-4 rounded-full" 
                        style={{ backgroundColor: category.color }}
                      />
                      <h3 className="font-semibold">{category.name}</h3>
                      <Badge variant="outline">{category.code}</Badge>
                      <Badge className={category.isActive ? 'bg-green-100 text-green-800' : 'bg-gray-100 text-gray-800'}>
                        {category.isActive ? 'Active' : 'Inactive'}
                      </Badge>
                      {category.parentCategory && (
                        <Badge variant="secondary">Sub-category</Badge>
                      )}
                    </div>
                    
                    <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4 text-sm text-muted-foreground">
                      <div>
                        <span className="font-medium">Parent:</span> {getParentCategoryName(category.parentCategory)}
                      </div>
                      <div>
                        <span className="font-medium">Maintenance:</span> {getMaintenanceScheduleDisplay(category)}
                      </div>
                      <div>
                        <span className="font-medium">Assets:</span> {category.assetCount}
                      </div>
                      <div>
                        <span className="font-medium">Icon:</span> {category.icon}
                      </div>
                    </div>
                    
                    <p className="text-sm text-muted-foreground">{category.description}</p>
                  </div>
                  
                  <div className="flex items-center space-x-2">
                    <Button size="sm" variant="outline" onClick={() => handleEdit(category)}>
                      <Edit className="h-4 w-4" />
                    </Button>
                    <Button 
                      size="sm" 
                      variant="outline" 
                      onClick={() => handleDelete(category.id)}
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
            <DialogTitle>Edit Asset Category</DialogTitle>
            <DialogDescription>
              Update the asset category information.
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 py-4">
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label htmlFor="edit-name">Category Name</Label>
                <Input
                  id="edit-name"
                  value={formData.name}
                  onChange={(e) => setFormData({...formData, name: e.target.value})}
                  placeholder="e.g., HVAC Systems"
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="edit-code">Category Code</Label>
                <Input
                  id="edit-code"
                  value={formData.code}
                  onChange={(e) => setFormData({...formData, code: e.target.value.toUpperCase()})}
                  placeholder="e.g., HVAC"
                />
              </div>
            </div>
            
            <div className="space-y-2">
              <Label htmlFor="edit-description">Description</Label>
              <Textarea
                id="edit-description"
                value={formData.description}
                onChange={(e) => setFormData({...formData, description: e.target.value})}
                placeholder="Description of the asset category..."
                rows={3}
              />
            </div>
            
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label htmlFor="edit-parent">Parent Category</Label>
                <Select value={formData.parentCategory || 'none'} onValueChange={(value) => setFormData({...formData, parentCategory: value === 'none' ? null : value})}>
                  <SelectTrigger>
                    <SelectValue placeholder="Select parent (optional)" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">Root Category</SelectItem>
                    {assetCategoriesData.filter(cat => !cat.parentCategory && cat.id !== selectedCategory?.id).map(cat => (
                      <SelectItem key={cat.id} value={cat.id.toString()}>
                        {cat.name}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-2">
                <Label htmlFor="edit-icon">Icon Name</Label>
                <Input
                  id="edit-icon"
                  value={formData.icon}
                  onChange={(e) => setFormData({...formData, icon: e.target.value})}
                  placeholder="e.g., package, wrench, settings"
                />
              </div>
            </div>
            
            {/* Edit Maintenance Schedule Type Selection */}
            <div className="space-y-4">
              <div className="space-y-2">
                <Label className="text-base font-medium">Maintenance Schedule</Label>
                <Select value={formData.maintenanceScheduleType} onValueChange={(value) => setFormData({...formData, maintenanceScheduleType: value})}>
                  <SelectTrigger>
                    <SelectValue placeholder="Select schedule type" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="single">Single Criteria (e.g., Every 6 months)</SelectItem>
                    <SelectItem value="multi">Multiple Criteria (e.g., Every 6 months OR 10,000 km)</SelectItem>
                  </SelectContent>
                </Select>
              </div>
            </div>
            
            {/* Edit Primary Maintenance Criteria */}
            <div className="space-y-4">
              <div className="flex items-center space-x-2">
                <Label className="text-base font-medium">
                  {formData.maintenanceScheduleType === 'multi' ? 'Primary Criteria' : 'Maintenance Criteria'}
                </Label>
                {formData.maintenanceScheduleType === 'multi' && (
                  <span className="text-sm text-muted-foreground">(First condition)</span>
                )}
              </div>
              
              <div className="grid grid-cols-3 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="edit-maintenanceType">Type</Label>
                  <Select value={formData.maintenanceType} onValueChange={(value) => {
                    const defaults = {
                      'Time': { unit: 'months', value: '' },
                      'Distance': { unit: 'km', value: '' },
                      'Usage': { unit: 'hours', value: '' },
                      'Cycles': { unit: 'cycles', value: '' }
                    };
                    setFormData({...formData, maintenanceType: value, maintenanceUnit: defaults[value]?.unit || 'months', maintenanceValue: defaults[value]?.value || ''});
                  }}>
                    <SelectTrigger>
                      <SelectValue placeholder="Select type" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="Time">Time-based</SelectItem>
                      <SelectItem value="Distance">Distance/Mileage</SelectItem>
                      <SelectItem value="Usage">Usage/Hours</SelectItem>
                      <SelectItem value="Cycles">Cycles/Operations</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
                
                {formData.maintenanceType !== 'Time' && (
                  <>
                    <div className="space-y-2">
                      <Label htmlFor="edit-maintenanceValue">Value</Label>
                      <Input
                        id="edit-maintenanceValue"
                        type="number"
                        value={formData.maintenanceValue}
                        onChange={(e) => setFormData({...formData, maintenanceValue: e.target.value})}
                        placeholder="1000"
                      />
                    </div>
                    <div className="space-y-2">
                      <Label htmlFor="edit-maintenanceUnit">Unit</Label>
                      <Select value={formData.maintenanceUnit} onValueChange={(value) => setFormData({...formData, maintenanceUnit: value})}>
                        <SelectTrigger>
                          <SelectValue placeholder="Select unit" />
                        </SelectTrigger>
                        <SelectContent>
                          {formData.maintenanceType === 'Distance' && (
                            <>
                              <SelectItem value="km">Kilometers</SelectItem>
                              <SelectItem value="miles">Miles</SelectItem>
                            </>
                          )}
                          {formData.maintenanceType === 'Usage' && (
                            <>
                              <SelectItem value="hours">Operating Hours</SelectItem>
                              <SelectItem value="runtime">Runtime Hours</SelectItem>
                            </>
                          )}
                          {formData.maintenanceType === 'Cycles' && (
                            <>
                              <SelectItem value="cycles">Cycles</SelectItem>
                              <SelectItem value="operations">Operations</SelectItem>
                              <SelectItem value="starts">Starts</SelectItem>
                            </>
                          )}
                        </SelectContent>
                      </Select>
                    </div>
                  </>
                )}
                
                {formData.maintenanceType === 'Time' && (
                  <>
                    <div className="space-y-2">
                      <Label htmlFor="edit-frequency">Frequency</Label>
                      <Select value={formData.maintenanceFrequency} onValueChange={(value) => setFormData({...formData, maintenanceFrequency: value})}>
                        <SelectTrigger>
                          <SelectValue placeholder="Select frequency" />
                        </SelectTrigger>
                        <SelectContent>
                          <SelectItem value="Weekly">Weekly</SelectItem>
                          <SelectItem value="Monthly">Monthly</SelectItem>
                          <SelectItem value="Quarterly">Quarterly</SelectItem>
                          <SelectItem value="Semi-Annual">Semi-Annual</SelectItem>
                          <SelectItem value="Annual">Annual</SelectItem>
                        </SelectContent>
                      </Select>
                    </div>
                    <div className="col-span-1"></div>
                  </>
                )}
              </div>
            </div>
            
            {/* Edit Secondary Criteria for Multi-schedule */}
            {formData.maintenanceScheduleType === 'multi' && (
              <div className="space-y-4">
                <div className="flex items-center space-x-2">
                  <Label className="text-base font-medium">Secondary Criteria</Label>
                  <span className="text-sm text-muted-foreground">(Alternative condition - OR logic)</span>
                </div>
                
                <div className="grid grid-cols-3 gap-4">
                  <div className="space-y-2">
                    <Label htmlFor="edit-secondaryMaintenanceType">Type</Label>
                    <Select value={formData.secondaryMaintenanceType} onValueChange={(value) => {
                      const defaults = {
                        'Time': { unit: 'months', value: '' },
                        'Distance': { unit: 'km', value: '' },
                        'Usage': { unit: 'hours', value: '' },
                        'Cycles': { unit: 'cycles', value: '' }
                      };
                      setFormData({...formData, secondaryMaintenanceType: value, secondaryMaintenanceUnit: defaults[value]?.unit || 'km', secondaryMaintenanceValue: defaults[value]?.value || ''});
                    }}>
                      <SelectTrigger>
                        <SelectValue placeholder="Select type" />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="Time">Time-based</SelectItem>
                        <SelectItem value="Distance">Distance/Mileage</SelectItem>
                        <SelectItem value="Usage">Usage/Hours</SelectItem>
                        <SelectItem value="Cycles">Cycles/Operations</SelectItem>
                      </SelectContent>
                    </Select>
                  </div>
                  
                  {formData.secondaryMaintenanceType !== 'Time' && (
                    <>
                      <div className="space-y-2">
                        <Label htmlFor="edit-secondaryMaintenanceValue">Value</Label>
                        <Input
                          id="edit-secondaryMaintenanceValue"
                          type="number"
                          value={formData.secondaryMaintenanceValue}
                          onChange={(e) => setFormData({...formData, secondaryMaintenanceValue: e.target.value})}
                          placeholder="10000"
                        />
                      </div>
                      <div className="space-y-2">
                        <Label htmlFor="edit-secondaryMaintenanceUnit">Unit</Label>
                        <Select value={formData.secondaryMaintenanceUnit} onValueChange={(value) => setFormData({...formData, secondaryMaintenanceUnit: value})}>
                          <SelectTrigger>
                            <SelectValue placeholder="Select unit" />
                          </SelectTrigger>
                          <SelectContent>
                            {formData.secondaryMaintenanceType === 'Distance' && (
                              <>
                                <SelectItem value="km">Kilometers</SelectItem>
                                <SelectItem value="miles">Miles</SelectItem>
                              </>
                            )}
                            {formData.secondaryMaintenanceType === 'Usage' && (
                              <>
                                <SelectItem value="hours">Operating Hours</SelectItem>
                                <SelectItem value="runtime">Runtime Hours</SelectItem>
                              </>
                            )}
                            {formData.secondaryMaintenanceType === 'Cycles' && (
                              <>
                                <SelectItem value="cycles">Cycles</SelectItem>
                                <SelectItem value="operations">Operations</SelectItem>
                                <SelectItem value="starts">Starts</SelectItem>
                              </>
                            )}
                          </SelectContent>
                        </Select>
                      </div>
                    </>
                  )}
                  
                  {formData.secondaryMaintenanceType === 'Time' && (
                    <>
                      <div className="space-y-2">
                        <Label htmlFor="edit-secondaryFrequency">Frequency</Label>
                        <Select value={formData.secondaryMaintenanceFrequency} onValueChange={(value) => setFormData({...formData, secondaryMaintenanceFrequency: value})}>
                          <SelectTrigger>
                            <SelectValue placeholder="Select frequency" />
                          </SelectTrigger>
                          <SelectContent>
                            <SelectItem value="Weekly">Weekly</SelectItem>
                            <SelectItem value="Monthly">Monthly</SelectItem>
                            <SelectItem value="Quarterly">Quarterly</SelectItem>
                            <SelectItem value="Semi-Annual">Semi-Annual</SelectItem>
                            <SelectItem value="Annual">Annual</SelectItem>
                          </SelectContent>
                        </Select>
                      </div>
                      <div className="col-span-1"></div>
                    </>
                  )}
                </div>
                
                <div className="bg-blue-50 p-3 rounded-lg">
                  <p className="text-sm text-blue-800">
                    <strong>Preview:</strong> {getMaintenanceScheduleDisplay(formData)}
                  </p>
                </div>
              </div>
            )}
            
            <div className="grid grid-cols-1 gap-4">
              <div className="space-y-2">
                <Label htmlFor="edit-color">Category Color</Label>
                <Input
                  id="edit-color"
                  type="color"
                  value={formData.color}
                  onChange={(e) => setFormData({...formData, color: e.target.value})}
                />
              </div>
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
            <Button onClick={handleUpdate}>Update Category</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}