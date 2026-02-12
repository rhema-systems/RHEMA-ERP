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

// Asset categories interface
interface AssetCategory {
  id: string | number;
  name: string;
  code: string;
  description?: string;
  parentCategory?: string | number | null;
  isActive: boolean;
  autoGenerateSchedules?: boolean;
  maintenanceFrequency?: string;
  assetCount?: number;
  color?: string;
  icon?: string;
  assetType?: string; // Asset type classification (Building, Vehicle, Equipment, etc.)
  maintenanceScheduleType?: string;
  maintenanceType?: string;
  maintenanceValue?: string;
  maintenanceUnit?: string;
  secondaryMaintenanceType?: string;
  secondaryMaintenanceFrequency?: string;
  secondaryMaintenanceValue?: string;
  secondaryMaintenanceUnit?: string;
}

export default function AssetCategoriesPage() {
  const [searchTerm, setSearchTerm] = useState('');
  const [statusFilter, setStatusFilter] = useState('all');
  const [parentFilter, setParentFilter] = useState('all');
  const [isCreateDialogOpen, setIsCreateDialogOpen] = useState(false);
  const [isEditDialogOpen, setIsEditDialogOpen] = useState(false);
  const [selectedCategory, setSelectedCategory] = useState<AssetCategory | null>(null);
  const [assetCategoriesData, setAssetCategoriesData] = useState<AssetCategory[]>([]);
  const [filteredData, setFilteredData] = useState<AssetCategory[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Form state
  const [formData, setFormData] = useState({
    name: '',
    code: '',
    description: '',
    parentCategory: '',
    isActive: true,
    autoGenerateSchedules: true,
    assetType: 'Equipment', // Default asset type
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

  // Fetch asset categories from API
  const fetchAssetCategories = async () => {
    try {
      setLoading(true);
      setError(null);
      const token = localStorage.getItem('token') || localStorage.getItem('authToken');

      console.log('🔄 Fetching asset categories...');
      console.log('Token available:', !!token);
      console.log('Token preview:', token ? `${token.substring(0, 20)}...` : 'None');
      console.log('API URL:', 'http://localhost:5000/api/maintenance/asset-categories');

      // For now, we'll proceed without authentication since the API endpoint allows anonymous access
      // TODO: Restore authentication requirement when proper auth is implemented
      // if (!token) {
      //   console.warn('⚠️ No authentication token found');
      //   setError('Authentication required. Please log in to access this page.');
      //   return;
      // }

      const headers: Record<string, string> = {
        'Content-Type': 'application/json'
      };

      // Only add Authorization header if token exists
      if (token) {
        headers['Authorization'] = `Bearer ${token}`;
      }

      const response = await fetch('http://localhost:5000/api/maintenance/asset-categories?pageSize=1000', {
        headers
      });

      console.log('Response status:', response.status);
      console.log('Response ok:', response.ok);

      if (!response.ok) {
        const errorText = await response.text();
        console.error('API Error Response:', {
          status: response.status,
          statusText: response.statusText,
          body: errorText
        });

        if (response.status === 401) {
          setError('Authentication failed. Please log in again or check your credentials.');
          // Optionally redirect to login
          // window.location.href = '/login';
        } else if (response.status === 403) {
          setError('Access denied. You do not have permission to view asset categories.');
        } else {
          throw new Error(`Failed to fetch asset categories: ${response.status} ${response.statusText} - ${errorText}`);
        }
        return;
      }

      const result = await response.json();
      console.log('✅ API Response received:', result);

      const categories = result.data || result.items || result || [];
      console.log('Processed categories count:', categories.length);

      setAssetCategoriesData(categories);
    } catch (error) {
      console.error('❌ Error fetching asset categories:', error);

      // Check if it's a network error
      if (error instanceof TypeError && error.message.includes('fetch')) {
        setError('Unable to connect to the server. Please check if the API is running on http://localhost:5000');
      } else {
        setError(`Failed to load asset categories: ${error.message}`);
      }

      setAssetCategoriesData([]);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchAssetCategories();
  }, []);

  useEffect(() => {
    let filtered = assetCategoriesData;

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

    if (parentFilter !== 'all') {
      if (parentFilter === 'parent') {
        filtered = filtered.filter(item => !item.parentCategory);
      } else if (parentFilter === 'child') {
        filtered = filtered.filter(item => item.parentCategory);
      }
    }

    setFilteredData(filtered);
  }, [searchTerm, statusFilter, parentFilter, assetCategoriesData]);

  const handleCreate = async () => {
    try {
      const token = localStorage.getItem('token') || localStorage.getItem('authToken');
      // Map form data to CreateMaintenanceAssetCategoryDto structure
      const createDto = {
        name: formData.name,
        code: formData.code || '',
        description: formData.description || '',
        parentCategoryId: formData.parentCategory || null,
        isActive: formData.isActive,
        autoGenerateSchedules: formData.autoGenerateSchedules,
        assetType: formData.assetType, // Add asset type classification
        color: formData.color,
        icon: formData.icon,
        maintenanceScheduleType: formData.maintenanceScheduleType,
        maintenanceType: formData.maintenanceType,
        maintenanceFrequency: formData.maintenanceType === 'Time' ? formData.maintenanceFrequency : null,
        maintenanceValue: formData.maintenanceType !== 'Time' ? parseFloat(formData.maintenanceValue) || null : null,
        maintenanceUnit: formData.maintenanceType !== 'Time' ? formData.maintenanceUnit : null,
        secondaryMaintenanceType: formData.maintenanceScheduleType === 'multi' ? formData.secondaryMaintenanceType : null,
        secondaryMaintenanceFrequency: formData.maintenanceScheduleType === 'multi' && formData.secondaryMaintenanceType === 'Time' ? formData.secondaryMaintenanceFrequency : null,
        secondaryMaintenanceValue: formData.maintenanceScheduleType === 'multi' && formData.secondaryMaintenanceType !== 'Time' ? parseFloat(formData.secondaryMaintenanceValue) || null : null,
        secondaryMaintenanceUnit: formData.maintenanceScheduleType === 'multi' && formData.secondaryMaintenanceType !== 'Time' ? formData.secondaryMaintenanceUnit : null
      };

      console.log('Sending create request:', createDto);

      const headers: Record<string, string> = {
        'Content-Type': 'application/json'
      };

      if (token) {
        headers['Authorization'] = `Bearer ${token}`;
      }

      const response = await fetch('http://localhost:5000/api/maintenance/asset-categories', {
        method: 'POST',
        headers,
        body: JSON.stringify(createDto)
      });

      if (response.ok) {
        const newCategory = await response.json();
        setAssetCategoriesData(prev => [...prev, newCategory]);
        setIsCreateDialogOpen(false);
        resetForm();
        console.log('Asset category created successfully');
      } else {
        const error = await response.text();
        console.error('Failed to create asset category:', error);
      }
    } catch (error) {
      console.error('Error creating asset category:', error);
    }
  };

  const handleEdit = (category: AssetCategory) => {
    setSelectedCategory(category);
    setFormData({
      name: category.name || '',
      code: category.code || '',
      description: category.description || '',
      parentCategory: category.parentCategory?.toString() || '',
      isActive: category.isActive ?? true,
      autoGenerateSchedules: category.autoGenerateSchedules ?? true,
      assetType: category.assetType || 'Equipment',
      maintenanceScheduleType: category.maintenanceScheduleType || 'single',
      maintenanceType: category.maintenanceType || 'Time',
      maintenanceFrequency: category.maintenanceFrequency || 'Monthly',
      maintenanceValue: category.maintenanceValue?.toString() || '',
      maintenanceUnit: category.maintenanceUnit || 'months',
      secondaryMaintenanceType: category.secondaryMaintenanceType || 'Distance',
      secondaryMaintenanceFrequency: category.secondaryMaintenanceFrequency || 'Monthly',
      secondaryMaintenanceValue: category.secondaryMaintenanceValue?.toString() || '',
      secondaryMaintenanceUnit: category.secondaryMaintenanceUnit || 'km',
      color: category.color || '#3b82f6',
      icon: category.icon || 'package'
    });
    setIsEditDialogOpen(true);
  };

  const handleUpdate = async () => {
    if (!selectedCategory?.id) return;

    try {
      const token = localStorage.getItem('token') || localStorage.getItem('authToken');
      // Map form data to UpdateMaintenanceAssetCategoryDto structure
      const updateDto = {
        name: formData.name,
        code: formData.code || '',
        description: formData.description || '',
        parentCategoryId: formData.parentCategory || null,
        isActive: formData.isActive,
        autoGenerateSchedules: formData.autoGenerateSchedules,
        assetType: formData.assetType, // Add asset type classification
        color: formData.color,
        icon: formData.icon,
        maintenanceScheduleType: formData.maintenanceScheduleType,
        maintenanceType: formData.maintenanceType,
        maintenanceFrequency: formData.maintenanceType === 'Time' ? formData.maintenanceFrequency : null,
        maintenanceValue: formData.maintenanceType !== 'Time' ? parseFloat(formData.maintenanceValue) || null : null,
        maintenanceUnit: formData.maintenanceType !== 'Time' ? formData.maintenanceUnit : null,
        secondaryMaintenanceType: formData.maintenanceScheduleType === 'multi' ? formData.secondaryMaintenanceType : null,
        secondaryMaintenanceFrequency: formData.maintenanceScheduleType === 'multi' && formData.secondaryMaintenanceType === 'Time' ? formData.secondaryMaintenanceFrequency : null,
        secondaryMaintenanceValue: formData.maintenanceScheduleType === 'multi' && formData.secondaryMaintenanceType !== 'Time' ? parseFloat(formData.secondaryMaintenanceValue) || null : null,
        secondaryMaintenanceUnit: formData.maintenanceScheduleType === 'multi' && formData.secondaryMaintenanceType !== 'Time' ? formData.secondaryMaintenanceUnit : null
      };

      console.log('Sending update request:', updateDto);

      const headers: Record<string, string> = {
        'Content-Type': 'application/json'
      };

      if (token) {
        headers['Authorization'] = `Bearer ${token}`;
      }

      const response = await fetch(`http://localhost:5000/api/maintenance/asset-categories/${selectedCategory.id}`, {
        method: 'PUT',
        headers,
        body: JSON.stringify(updateDto)
      });

      if (response.ok) {
        const updatedCategory = await response.json();
        setAssetCategoriesData(prev =>
          prev.map(item => item.id === selectedCategory.id ? updatedCategory : item)
        );
        setIsEditDialogOpen(false);
        resetForm();
        console.log('Asset category updated successfully');
      } else {
        const error = await response.text();
        console.error('Failed to update asset category:', error);
      }
    } catch (error) {
      console.error('Error updating asset category:', error);
    }
  };

  const handleDelete = async (id: string | number) => {
    if (!confirm('Are you sure you want to delete this asset category?')) {
      return;
    }

    try {
      const token = localStorage.getItem('token') || localStorage.getItem('authToken');
      const headers: Record<string, string> = {
        'Content-Type': 'application/json'
      };

      if (token) {
        headers['Authorization'] = `Bearer ${token}`;
      }

      const response = await fetch(`http://localhost:5000/api/maintenance/asset-categories/${id}`, {
        method: 'DELETE',
        headers
      });

      if (response.ok) {
        setAssetCategoriesData(prev => prev.filter(item => item.id !== id));
        console.log('Asset category deleted successfully');
      } else {
        const error = await response.text();
        console.error('Failed to delete asset category:', error);
      }
    } catch (error) {
      console.error('Error deleting asset category:', error);
    }
  };

  const resetForm = () => {
    setFormData({
      name: '',
      code: '',
      description: '',
      parentCategory: '',
      isActive: true,
      autoGenerateSchedules: true,
      assetType: 'Equipment',
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

  const getMaintenanceScheduleDisplay = (category: Partial<AssetCategory>) => {
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
          <DialogContent className="sm:max-w-[900px]">
            <DialogHeader>
              <DialogTitle>Add Asset Category</DialogTitle>
              <DialogDescription>
                Create a new asset category to organize and classify your assets.
              </DialogDescription>
            </DialogHeader>
            <div className="max-h-[70vh] overflow-y-auto pr-2">
              <div className="grid gap-4 py-4">
                <div className="grid grid-cols-2 gap-4">
                  <div className="space-y-2">
                    <Label htmlFor="name">Category Name</Label>
                    <Input
                      id="name"
                      value={formData.name}
                      onChange={(e) => setFormData({ ...formData, name: e.target.value })}
                      placeholder="Enter category name"
                    />
                  </div>
                  <div className="space-y-2">
                    <Label htmlFor="code">Category Code</Label>
                    <Input
                      id="code"
                      value={formData.code}
                      onChange={(e) => setFormData({ ...formData, code: e.target.value })}
                      placeholder="e.g., HVAC, ELEC"
                    />
                  </div>
                </div>

                <div className="space-y-2">
                  <Label htmlFor="description">Description</Label>
                  <Textarea
                    id="description"
                    value={formData.description}
                    onChange={(e) => setFormData({ ...formData, description: e.target.value })}
                    placeholder="Describe this category..."
                    rows={3}
                  />
                </div>

                <div className="grid grid-cols-3 gap-4">
                  <div className="space-y-2">
                    <Label htmlFor="parent">Parent Category</Label>
                    <Select value={formData.parentCategory || 'none'} onValueChange={(value) => setFormData({ ...formData, parentCategory: value === 'none' ? null : value })}>
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
                    <Label htmlFor="assetType">Asset Type</Label>
                    <Select value={formData.assetType} onValueChange={(value) => setFormData({ ...formData, assetType: value })}>
                      <SelectTrigger>
                        <SelectValue placeholder="Select asset type" />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="Equipment">Equipment</SelectItem>
                        <SelectItem value="Vehicle">Vehicle</SelectItem>
                        <SelectItem value="Building">Building</SelectItem>
                        <SelectItem value="Infrastructure">Infrastructure</SelectItem>
                        <SelectItem value="ITAsset">IT Asset</SelectItem>
                        <SelectItem value="Furniture">Furniture</SelectItem>
                        <SelectItem value="Tool">Tool</SelectItem>
                        <SelectItem value="Safety">Safety</SelectItem>
                        <SelectItem value="Other">Other</SelectItem>
                      </SelectContent>
                    </Select>
                  </div>
                  <div className="space-y-2">
                    <Label htmlFor="icon">Icon</Label>
                    <Input
                      id="icon"
                      value={formData.icon}
                      onChange={(e) => setFormData({ ...formData, icon: e.target.value })}
                      placeholder="Icon name or emoji"
                    />
                  </div>
                </div>

                {/* Maintenance Schedule Type Selection */}
                <div className="space-y-4">
                  <div className="space-y-2">
                    <Label className="text-base font-medium">Maintenance Schedule</Label>
                    <Select value={formData.maintenanceScheduleType} onValueChange={(value) => setFormData({ ...formData, maintenanceScheduleType: value })}>
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
                        const defaults: Record<string, { unit: string; value: string }> = {
                          'Time': { unit: 'months', value: '' },
                          'Distance': { unit: 'km', value: '' },
                          'Usage': { unit: 'hours', value: '' },
                          'Cycles': { unit: 'cycles', value: '' }
                        };
                        setFormData({ ...formData, maintenanceType: value, maintenanceUnit: defaults[value]?.unit || 'months', maintenanceValue: defaults[value]?.value || '' });
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
                            onChange={(e) => setFormData({ ...formData, maintenanceValue: e.target.value })}
                            placeholder="1000"
                          />
                        </div>
                        <div className="space-y-2">
                          <Label htmlFor="maintenanceUnit">Unit</Label>
                          <Select value={formData.maintenanceUnit} onValueChange={(value) => setFormData({ ...formData, maintenanceUnit: value })}>
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
                          <Select value={formData.maintenanceFrequency} onValueChange={(value) => setFormData({ ...formData, maintenanceFrequency: value })}>
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
                          const defaults: Record<string, { unit: string; value: string }> = {
                            'Time': { unit: 'months', value: '' },
                            'Distance': { unit: 'km', value: '' },
                            'Usage': { unit: 'hours', value: '' },
                            'Cycles': { unit: 'cycles', value: '' }
                          };
                          setFormData({ ...formData, secondaryMaintenanceType: value, secondaryMaintenanceUnit: defaults[value]?.unit || 'km', secondaryMaintenanceValue: defaults[value]?.value || '' });
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
                              onChange={(e) => setFormData({ ...formData, secondaryMaintenanceValue: e.target.value })}
                              placeholder="10000"
                            />
                          </div>
                          <div className="space-y-2">
                            <Label htmlFor="secondaryMaintenanceUnit">Unit</Label>
                            <Select value={formData.secondaryMaintenanceUnit} onValueChange={(value) => setFormData({ ...formData, secondaryMaintenanceUnit: value })}>
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
                            <Select value={formData.secondaryMaintenanceFrequency} onValueChange={(value) => setFormData({ ...formData, secondaryMaintenanceFrequency: value })}>
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

                <div className="grid grid-cols-3 gap-6 items-center">
                  <div className="space-y-2">
                    <Label htmlFor="color">Category Color</Label>
                    <div className="flex items-center space-x-2">
                      <Input
                        id="color"
                        type="color"
                        value={formData.color}
                        onChange={(e) => setFormData({ ...formData, color: e.target.value })}
                        className="w-16 h-10"
                      />
                      <Input
                        value={formData.color}
                        onChange={(e) => setFormData({ ...formData, color: e.target.value })}
                        placeholder="#000000"
                        className="w-24"
                      />
                    </div>
                  </div>
                  <div className="flex items-center space-x-2">
                    <Label className="text-sm font-medium">Active</Label>
                    <input
                      type="checkbox"
                      checked={formData.isActive}
                      onChange={(e) => setFormData({ ...formData, isActive: e.target.checked })}
                      className="rounded border-gray-300"
                    />
                  </div>
                  <div className="flex items-center space-x-2">
                    <Label className="text-sm font-medium">Auto-generate schedules</Label>
                    <input
                      type="checkbox"
                      checked={formData.autoGenerateSchedules}
                      onChange={(e) => setFormData({ ...formData, autoGenerateSchedules: e.target.checked })}
                      className="rounded border-gray-300"
                    />
                  </div>
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
                  {filteredData.reduce((sum, cat) => sum + (cat.assetCount || 0), 0)}
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
            {loading ? 'Loading...' : error ? 'Error loading data' : `${filteredData.length} categor${filteredData.length === 1 ? 'y' : 'ies'} found`}
          </CardDescription>
        </CardHeader>
        <CardContent>
          {loading && (
            <div className="flex items-center justify-center py-8">
              <div className="text-muted-foreground">Loading asset categories...</div>
            </div>
          )}

          {error && (
            <div className="flex items-center justify-center py-8">
              <div className="text-red-600">{error}</div>
            </div>
          )}

          {!loading && !error && (
            <div className="space-y-4">
              {filteredData.length === 0 ? (
                <div className="text-center py-8 text-muted-foreground">
                  No asset categories found. {searchTerm || statusFilter !== 'all' || parentFilter !== 'all' ? 'Try adjusting your filters.' : 'Create your first asset category to get started.'}
                </div>
              ) : (
                filteredData.map((category) => (
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
                          {category.assetType && (
                            <Badge variant="outline" className="bg-blue-50 text-blue-700">
                              {category.assetType}
                            </Badge>
                          )}
                          {category.parentCategory && (
                            <Badge variant="secondary">Sub-category</Badge>
                          )}
                        </div>

                        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-5 gap-4 text-sm text-muted-foreground">
                          <div>
                            <span className="font-medium">Parent:</span> {getParentCategoryName(category.parentCategory)}
                          </div>
                          <div>
                            <span className="font-medium">Type:</span> {category.assetType || 'Not specified'}
                          </div>
                          <div>
                            <span className="font-medium">Maintenance:</span> {getMaintenanceScheduleDisplay(category)}
                          </div>
                          <div>
                            <a
                              href={`/maintenance/assets?category=${encodeURIComponent(category.name)}`}
                              target="_blank"
                              rel="noopener noreferrer"
                              className="text-blue-600 hover:underline font-medium"
                            >
                              <span className="font-medium">Assets:</span>{' '}
                              {typeof category.assetCount === 'number' ? category.assetCount : 0}
                            </a>
                          </div>
                          <div>
                            <span className="font-medium">Icon:</span> {category.icon}
                          </div>
                          <div>
                            <span className="font-medium">Auto-Scheduling:</span>{' '}
                            {category.autoGenerateSchedules === false ? 'Disabled' : 'Enabled'}
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
                ))
              )}
            </div>
          )}
        </CardContent>
      </Card>


      {/* Edit Dialog */}
      <Dialog open={isEditDialogOpen} onOpenChange={setIsEditDialogOpen}>
        <DialogContent className="sm:max-w-[900px]">
          <DialogHeader>
            <DialogTitle>Edit Asset Category</DialogTitle>
            <DialogDescription>
              Update the asset category information.
            </DialogDescription>
          </DialogHeader>
          <div className="max-h-[70vh] overflow-y-auto pr-2">
            <div className="grid gap-4 py-4">
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="edit-name">Category Name</Label>
                  <Input
                    id="edit-name"
                    value={formData.name}
                    onChange={(e) => setFormData({ ...formData, name: e.target.value })}
                    placeholder="e.g., HVAC Systems"
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="edit-code">Category Code</Label>
                  <Input
                    id="edit-code"
                    value={formData.code}
                    onChange={(e) => setFormData({ ...formData, code: e.target.value.toUpperCase() })}
                    placeholder="e.g., HVAC"
                  />
                </div>
              </div>

              <div className="space-y-2">
                <Label htmlFor="edit-description">Description</Label>
                <Textarea
                  id="edit-description"
                  value={formData.description}
                  onChange={(e) => setFormData({ ...formData, description: e.target.value })}
                  placeholder="Description of the asset category..."
                  rows={3}
                />
              </div>

              <div className="grid grid-cols-3 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="edit-parent">Parent Category</Label>
                  <Select value={formData.parentCategory || 'none'} onValueChange={(value) => setFormData({ ...formData, parentCategory: value === 'none' ? null : value })}>
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
                  <Label htmlFor="edit-assetType">Asset Type</Label>
                  <Select value={formData.assetType} onValueChange={(value) => setFormData({ ...formData, assetType: value })}>
                    <SelectTrigger>
                      <SelectValue placeholder="Select asset type" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="Equipment">Equipment</SelectItem>
                      <SelectItem value="Vehicle">Vehicle</SelectItem>
                      <SelectItem value="Building">Building</SelectItem>
                      <SelectItem value="Infrastructure">Infrastructure</SelectItem>
                      <SelectItem value="ITAsset">IT Asset</SelectItem>
                      <SelectItem value="Furniture">Furniture</SelectItem>
                      <SelectItem value="Tool">Tool</SelectItem>
                      <SelectItem value="Safety">Safety</SelectItem>
                      <SelectItem value="Other">Other</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-2">
                  <Label htmlFor="edit-icon">Icon Name</Label>
                  <Input
                    id="edit-icon"
                    value={formData.icon}
                    onChange={(e) => setFormData({ ...formData, icon: e.target.value })}
                    placeholder="e.g., package, wrench, settings"
                  />
                </div>
              </div>

              {/* Edit Maintenance Schedule Type Selection */}
              <div className="space-y-4">
                <div className="space-y-2">
                  <Label className="text-base font-medium">Maintenance Schedule</Label>
                  <Select value={formData.maintenanceScheduleType} onValueChange={(value) => setFormData({ ...formData, maintenanceScheduleType: value })}>
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
                      const defaults: Record<string, { unit: string; value: string }> = {
                        'Time': { unit: 'months', value: '' },
                        'Distance': { unit: 'km', value: '' },
                        'Usage': { unit: 'hours', value: '' },
                        'Cycles': { unit: 'cycles', value: '' }
                      };
                      setFormData({ ...formData, maintenanceType: value, maintenanceUnit: defaults[value]?.unit || 'months', maintenanceValue: defaults[value]?.value || '' });
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
                          onChange={(e) => setFormData({ ...formData, maintenanceValue: e.target.value })}
                          placeholder="1000"
                        />
                      </div>
                      <div className="space-y-2">
                        <Label htmlFor="edit-maintenanceUnit">Unit</Label>
                        <Select value={formData.maintenanceUnit} onValueChange={(value) => setFormData({ ...formData, maintenanceUnit: value })}>
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
                        <Select value={formData.maintenanceFrequency} onValueChange={(value) => setFormData({ ...formData, maintenanceFrequency: value })}>
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
                        const defaults: Record<string, { unit: string; value: string }> = {
                          'Time': { unit: 'months', value: '' },
                          'Distance': { unit: 'km', value: '' },
                          'Usage': { unit: 'hours', value: '' },
                          'Cycles': { unit: 'cycles', value: '' }
                        };
                        setFormData({ ...formData, secondaryMaintenanceType: value, secondaryMaintenanceUnit: defaults[value]?.unit || 'km', secondaryMaintenanceValue: defaults[value]?.value || '' });
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
                            onChange={(e) => setFormData({ ...formData, secondaryMaintenanceValue: e.target.value })}
                            placeholder="10000"
                          />
                        </div>
                        <div className="space-y-2">
                          <Label htmlFor="edit-secondaryMaintenanceUnit">Unit</Label>
                          <Select value={formData.secondaryMaintenanceUnit} onValueChange={(value) => setFormData({ ...formData, secondaryMaintenanceUnit: value })}>
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
                          <Select value={formData.secondaryMaintenanceFrequency} onValueChange={(value) => setFormData({ ...formData, secondaryMaintenanceFrequency: value })}>
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

              <div className="grid grid-cols-3 gap-6 items-center">
                <div className="space-y-2">
                  <Label htmlFor="edit-color">Category Color</Label>
                  <Input
                    id="edit-color"
                    type="color"
                    value={formData.color}
                    onChange={(e) => setFormData({ ...formData, color: e.target.value })}
                    className="w-16 h-10"
                  />
                </div>
                <div className="flex items-center space-x-2">
                  <Label className="text-sm font-medium" htmlFor="edit-active">Active</Label>
                  <Switch
                    id="edit-active"
                    checked={formData.isActive}
                    onCheckedChange={(checked) => setFormData({ ...formData, isActive: checked })}
                  />
                </div>
                <div className="flex items-center space-x-2">
                  <Label className="text-sm font-medium" htmlFor="edit-auto-generate">Auto-generate schedules</Label>
                  <Switch
                    id="edit-auto-generate"
                    checked={formData.autoGenerateSchedules}
                    onCheckedChange={(checked) => setFormData({ ...formData, autoGenerateSchedules: checked })}
                  />
                </div>
              </div>
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