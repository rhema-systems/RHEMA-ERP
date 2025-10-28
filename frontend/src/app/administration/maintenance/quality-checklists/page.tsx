'use client';

import React, { useState, useEffect } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Switch } from '@/components/ui/switch';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger } from '@/components/ui/dialog';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { 
  Plus,
  Search,
  Edit,
  Trash2,
  Copy,
  Eye,
  ClipboardCheck,
  Settings,
  AlertTriangle,
  CheckCircle,
  XCircle,
  GripVertical,
  X
} from 'lucide-react';
import { cn } from '@/lib/utils';
import { 
  QualityChecklist, 
  QualityChecklistItem, 
  CreateQualityChecklistDto,
  UpdateQualityChecklistDto,
  qualityChecklistService 
} from '@/services/qualityChecklistService';

export default function QualityChecklistsPage() {
  const [mounted, setMounted] = useState(false);
  const [checklists, setChecklists] = useState<QualityChecklist[]>([]);
  const [filteredChecklists, setFilteredChecklists] = useState<QualityChecklist[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchTerm, setSearchTerm] = useState('');
  const [typeFilter, setTypeFilter] = useState('all');
  const [categoryFilter, setCategoryFilter] = useState('all');
  const [statusFilter, setStatusFilter] = useState('all');

  // Dialog states
  const [isCreateDialogOpen, setIsCreateDialogOpen] = useState(false);
  const [isEditDialogOpen, setIsEditDialogOpen] = useState(false);
  const [isPreviewDialogOpen, setIsPreviewDialogOpen] = useState(false);
  const [isDuplicateDialogOpen, setIsDuplicateDialogOpen] = useState(false);
  const [selectedChecklist, setSelectedChecklist] = useState<QualityChecklist | null>(null);

  // Error and loading states
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  // Form data
  const [formData, setFormData] = useState<CreateQualityChecklistDto>({
    name: '',
    description: '',
    workOrderType: '',
    assetCategory: '',
    maintenanceType: 'none',
    isMandatory: true,
    minimumPassingScore: 85,
    items: []
  });

  // Current checklist item being edited
  const [editingItem, setEditingItem] = useState<Partial<QualityChecklistItem> | null>(null);
  const [duplicateName, setDuplicateName] = useState('');

  // Dropdown options
  const [workOrderTypes, setWorkOrderTypes] = useState<string[]>([]);
  const [assetCategories, setAssetCategories] = useState<string[]>([]);
  const [maintenanceTypes, setMaintenanceTypes] = useState<string[]>([]);
  const [checklistCategories, setChecklistCategories] = useState<string[]>([]);

  // Define filterChecklists function before useEffect that uses it
  const filterChecklists = () => {
    let filtered = checklists.filter(checklist => checklist != null);

    if (searchTerm) {
      filtered = filtered.filter(checklist =>
        checklist?.name?.toLowerCase().includes(searchTerm.toLowerCase()) ||
        checklist?.description?.toLowerCase().includes(searchTerm.toLowerCase()) ||
        checklist?.workOrderType?.toLowerCase().includes(searchTerm.toLowerCase()) ||
        checklist?.assetCategory?.toLowerCase().includes(searchTerm.toLowerCase())
      );
    }

    if (typeFilter !== 'all') {
      filtered = filtered.filter(checklist => checklist?.workOrderType?.toLowerCase() === typeFilter);
    }

    if (categoryFilter !== 'all') {
      filtered = filtered.filter(checklist => checklist?.assetCategory?.toLowerCase() === categoryFilter);
    }

    if (statusFilter !== 'all') {
      filtered = filtered.filter(checklist => 
        statusFilter === 'active' ? checklist?.isActive : !checklist?.isActive
      );
    }

    setFilteredChecklists(filtered);
  };

  useEffect(() => {
    setMounted(true);
  }, []);

  useEffect(() => {
    if (mounted) {
      loadData();
      loadOptions();
    }
  }, [mounted]);

  useEffect(() => {
    filterChecklists();
  }, [checklists, searchTerm, typeFilter, categoryFilter, statusFilter]);

  // Prevent hydration mismatch by not rendering until mounted
  if (!mounted) {
    return (
      <div className="space-y-6">
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-3xl font-bold tracking-tight">Quality Checklists</h1>
            <p className="text-muted-foreground">Manage quality control checklists for maintenance work orders</p>
          </div>
        </div>
        <div className="text-center py-8">Loading...</div>
      </div>
    );
  }

  const loadData = async () => {
    try {
      setLoading(true);
      setError(null);
      const data = await qualityChecklistService.getAllChecklists();
      setChecklists(data);
    } catch (error) {
      console.error('Error loading checklists:', error);
      setError('Failed to load checklists. Please try again.');
    } finally {
      setLoading(false);
    }
  };

  const loadOptions = async () => {
    try {
      const [types, categories, mainTypes, checkCategories] = await Promise.all([
        qualityChecklistService.getWorkOrderTypes(),
        qualityChecklistService.getAssetCategories(),
        qualityChecklistService.getMaintenanceTypes(),
        qualityChecklistService.getChecklistCategories()
      ]);
      
      setWorkOrderTypes(types);
      setAssetCategories(categories);
      setMaintenanceTypes(mainTypes);
      setChecklistCategories(checkCategories);
    } catch (error) {
      console.error('Error loading options:', error);
    }
  };

  const handleCreate = async () => {
    if (isSubmitting) return;
    
    try {
      setIsSubmitting(true);
      setError(null);
      const createData = {
        ...formData,
        maintenanceType: formData.maintenanceType === 'none' ? '' : formData.maintenanceType
      };
      console.log('Creating quality checklist with data:', createData);
      const newChecklist = await qualityChecklistService.createChecklist(createData);
      setChecklists([...checklists, newChecklist]);
      setIsCreateDialogOpen(false);
      resetForm();
    } catch (error) {
      console.error('Error creating checklist:', error);
      setError('Failed to create checklist. Please try again.');
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleUpdate = async () => {
    if (!selectedChecklist || isSubmitting) return;

    try {
      setIsSubmitting(true);
      setError(null);
      const updateData: UpdateQualityChecklistDto = {
        ...formData,
        maintenanceType: formData.maintenanceType === 'none' ? '' : formData.maintenanceType,
        isActive: selectedChecklist.isActive
      };
      console.log('Updating quality checklist with data:', updateData);
      
      const updatedChecklist = await qualityChecklistService.updateChecklist(selectedChecklist.id, updateData);
      setChecklists(checklists.map(c => c.id === selectedChecklist.id ? updatedChecklist : c));
      setIsEditDialogOpen(false);
      resetForm();
    } catch (error) {
      console.error('Error updating checklist:', error);
      setError('Failed to update checklist. Please try again.');
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleDelete = async (checklist: QualityChecklist) => {
    if (!confirm(`Are you sure you want to delete "${checklist.name}"?`)) {
      return;
    }

    try {
      setError(null);
      console.log(`Deleting quality checklist: ${checklist.name}`);
      await qualityChecklistService.deleteChecklist(checklist.id);
      setChecklists(checklists.filter(c => c.id !== checklist.id));
    } catch (error) {
      console.error('Error deleting checklist:', error);
      setError('Failed to delete checklist. Please try again.');
    }
  };

  const handleDuplicate = async () => {
    if (!selectedChecklist || !duplicateName) return;

    try {
      const duplicatedChecklist = await qualityChecklistService.duplicateChecklist(selectedChecklist.id, duplicateName);
      setChecklists([...checklists, duplicatedChecklist]);
      setIsDuplicateDialogOpen(false);
      setDuplicateName('');
    } catch (error) {
      console.error('Error duplicating checklist:', error);
    }
  };

  const toggleActive = async (checklist: QualityChecklist) => {
    try {
      const updateData: UpdateQualityChecklistDto = {
        name: checklist.name,
        description: checklist.description,
        workOrderType: checklist.workOrderType,
        assetCategory: checklist.assetCategory,
        maintenanceType: checklist.maintenanceType || '',
        isMandatory: checklist.isMandatory,
        minimumPassingScore: checklist.minimumPassingScore,
        items: checklist.items.map(item => ({
          text: item.text,
          description: item.description,
          required: item.required,
          critical: item.critical,
          category: item.category,
          responseType: item.responseType,
          minScore: item.minScore,
          maxScore: item.maxScore,
          order: item.order
        })),
        isActive: !checklist.isActive
      };

      const updatedChecklist = await qualityChecklistService.updateChecklist(checklist.id, updateData);
      setChecklists(checklists.map(c => c.id === checklist.id ? updatedChecklist : c));
    } catch (error) {
      console.error('Error toggling checklist active status:', error);
    }
  };

  const openEditDialog = (checklist: QualityChecklist) => {
    setSelectedChecklist(checklist);
    setFormData({
      name: checklist.name,
      description: checklist.description,
      workOrderType: checklist.workOrderType,
      assetCategory: checklist.assetCategory,
      maintenanceType: checklist.maintenanceType || 'none',
      isMandatory: checklist.isMandatory,
      minimumPassingScore: checklist.minimumPassingScore,
      items: checklist.items.map(item => ({
        text: item.text,
        description: item.description,
        required: item.required,
        critical: item.critical,
        category: item.category,
        responseType: item.responseType,
        minScore: item.minScore,
        maxScore: item.maxScore,
        order: item.order
      }))
    });
    setIsEditDialogOpen(true);
  };

  const openPreviewDialog = (checklist: QualityChecklist) => {
    setSelectedChecklist(checklist);
    setIsPreviewDialogOpen(true);
  };

  const openDuplicateDialog = (checklist: QualityChecklist) => {
    setSelectedChecklist(checklist);
    setDuplicateName(`${checklist.name} (Copy)`);
    setIsDuplicateDialogOpen(true);
  };

  const resetForm = () => {
    setFormData({
      name: '',
      description: '',
      workOrderType: '',
      assetCategory: '',
      maintenanceType: 'none',
      isMandatory: true,
      minimumPassingScore: 85,
      items: []
    });
    setSelectedChecklist(null);
    setEditingItem(null);
  };

  const addChecklistItem = () => {
    const newItem: Omit<QualityChecklistItem, 'id'> = {
      text: '',
      description: '',
      required: true,
      critical: false,
      category: checklistCategories[0] || 'General',
      responseType: 'Pass/Fail',
      order: formData.items.length + 1
    };
    setEditingItem(newItem);
  };

  const saveChecklistItem = () => {
    if (!editingItem?.text) return;

    const itemToSave = {
      ...editingItem,
      id: editingItem.id || `temp-${Date.now()}`
    } as QualityChecklistItem;

    if (editingItem.id && editingItem.id.startsWith('temp-')) {
      // Adding new item
      setFormData({
        ...formData,
        items: [...formData.items, itemToSave]
      });
    } else {
      // Updating existing item
      setFormData({
        ...formData,
        items: formData.items.map(item => item.id === itemToSave.id ? itemToSave : item)
      });
    }

    setEditingItem(null);
  };

  const removeChecklistItem = (itemId: string) => {
    setFormData({
      ...formData,
      items: formData.items.filter(item => item.id !== itemId)
    });
  };

  const getStatusBadge = (checklist: QualityChecklist) => {
    return (
      <Badge className={checklist.isActive ? 'bg-green-100 text-green-800' : 'bg-gray-100 text-gray-800'}>
        {checklist.isActive ? 'Active' : 'Inactive'}
      </Badge>
    );
  };

  const getTypeBadge = (type: string) => {
    const colors: Record<string, string> = {
      'Preventive': 'bg-blue-100 text-blue-800',
      'Safety': 'bg-red-100 text-red-800',
      'Regulatory': 'bg-purple-100 text-purple-800',
      'Corrective': 'bg-orange-100 text-orange-800',
      'Emergency': 'bg-red-100 text-red-800',
      'Installation': 'bg-green-100 text-green-800'
    };

    return (
      <Badge className={colors[type] || 'bg-gray-100 text-gray-800'}>
        {type}
      </Badge>
    );
  };

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Quality Control Checklists</h1>
          <p className="text-muted-foreground">
            Create and manage quality control checklists for different maintenance types
          </p>
        </div>
        
        <Dialog open={isCreateDialogOpen} onOpenChange={setIsCreateDialogOpen}>
          <DialogTrigger asChild>
            <Button>
              <Plus className="mr-2 h-4 w-4" />
              Create Checklist
            </Button>
          </DialogTrigger>
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
            <BreadcrumbLink href="/administration/maintenance">Maintenance</BreadcrumbLink>
          </BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem>
            <BreadcrumbPage>Quality Checklists</BreadcrumbPage>
          </BreadcrumbItem>
        </BreadcrumbList>
      </Breadcrumb>

      {/* Summary Cards */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-2xl font-bold">{loading ? '-' : checklists.length}</p>
                <p className="text-sm text-muted-foreground">Total Checklists</p>
              </div>
              <ClipboardCheck className="h-8 w-8 text-blue-500" />
            </div>
          </CardContent>
        </Card>
        
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-2xl font-bold text-green-600">
                  {loading ? '-' : checklists.filter(c => c?.isActive).length}
                </p>
                <p className="text-sm text-muted-foreground">Active</p>
              </div>
              <CheckCircle className="h-8 w-8 text-green-500" />
            </div>
          </CardContent>
        </Card>
        
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-2xl font-bold text-orange-600">
                  {loading ? '-' : checklists.filter(c => c?.isMandatory).length}
                </p>
                <p className="text-sm text-muted-foreground">Mandatory</p>
              </div>
              <AlertTriangle className="h-8 w-8 text-orange-500" />
            </div>
          </CardContent>
        </Card>
        
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-2xl font-bold text-purple-600">
                  {loading ? '-' : checklists.reduce((total, c) => total + (c?.items?.length || 0), 0)}
                </p>
                <p className="text-sm text-muted-foreground">Total Items</p>
              </div>
              <Settings className="h-8 w-8 text-purple-500" />
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
                placeholder="Search checklists..."
                className="pl-8"
                value={searchTerm}
                onChange={(e) => setSearchTerm(e.target.value)}
              />
            </div>
            
            <Select value={typeFilter} onValueChange={setTypeFilter}>
              <SelectTrigger>
                <SelectValue placeholder="Work Order Type" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Types</SelectItem>
                {workOrderTypes.map(type => (
                  <SelectItem key={type} value={type.toLowerCase()}>{type}</SelectItem>
                ))}
              </SelectContent>
            </Select>

            <Select value={categoryFilter} onValueChange={setCategoryFilter}>
              <SelectTrigger>
                <SelectValue placeholder="Asset Category" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Categories</SelectItem>
                {assetCategories.map(category => (
                  <SelectItem key={category} value={category.toLowerCase()}>{category}</SelectItem>
                ))}
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
          </div>
        </CardContent>
      </Card>

      {/* Error Message */}
      {error && (
        <Card className="border-red-200 bg-red-50">
          <CardContent className="pt-6">
            <div className="flex items-center text-red-800">
              <AlertTriangle className="h-4 w-4 mr-2" />
              {error}
            </div>
          </CardContent>
        </Card>
      )}

      {/* Checklists List */}
      <Card>
        <CardHeader>
          <CardTitle>Quality Control Checklists</CardTitle>
          <CardDescription>
            {filteredChecklists.length} checklist{filteredChecklists.length !== 1 ? 's' : ''} found
          </CardDescription>
        </CardHeader>
        <CardContent>
          <div className="space-y-4">
            {loading ? (
              <div className="text-center py-8">Loading checklists...</div>
            ) : filteredChecklists.length === 0 ? (
              <div className="text-center py-8 text-muted-foreground">
                No checklists found. Create your first checklist to get started.
              </div>
            ) : (
              filteredChecklists.map((checklist) => (
                <div key={checklist.id} className="border rounded-lg p-4 hover:bg-muted/50 transition-colors">
                  <div className="flex items-start justify-between">
                    <div className="space-y-3 flex-1">
                      <div className="flex items-center space-x-3">
                        <h3 className="font-semibold">{checklist.name}</h3>
                        {getTypeBadge(checklist.workOrderType)}
                        <Badge variant="outline">{checklist.assetCategory}</Badge>
                        {getStatusBadge(checklist)}
                        {checklist.isMandatory && (
                          <Badge variant="secondary">
                            <AlertTriangle className="w-3 h-3 mr-1" />
                            Mandatory
                          </Badge>
                        )}
                      </div>
                      
                      <div className="grid grid-cols-1 md:grid-cols-3 gap-4 text-sm text-muted-foreground">
                        <div>
                          <span className="font-medium">Items:</span> {checklist.items.length}
                        </div>
                        <div>
                          <span className="font-medium">Pass Score:</span> {checklist.minimumPassingScore}%
                        </div>
                        <div>
                          <span className="font-medium">Version:</span> {checklist.version}
                        </div>
                      </div>
                      
                      {checklist.description && (
                        <p className="text-sm text-muted-foreground">{checklist.description}</p>
                      )}
                    </div>
                    
                    <div className="flex items-center space-x-2 ml-4">
                      <Button size="sm" variant="outline" onClick={() => openPreviewDialog(checklist)}>
                        <Eye className="h-4 w-4" />
                      </Button>
                      <Button size="sm" variant="outline" onClick={() => openEditDialog(checklist)}>
                        <Edit className="h-4 w-4" />
                      </Button>
                      <Button size="sm" variant="outline" onClick={() => openDuplicateDialog(checklist)}>
                        <Copy className="h-4 w-4" />
                      </Button>
                      <Button 
                        size="sm" 
                        variant="outline" 
                        onClick={() => toggleActive(checklist)}
                        className={cn(
                          checklist.isActive 
                            ? "text-orange-600 hover:text-orange-700" 
                            : "text-green-600 hover:text-green-700"
                        )}
                      >
                        {checklist.isActive ? <XCircle className="h-4 w-4" /> : <CheckCircle className="h-4 w-4" />}
                      </Button>
                      <Button 
                        size="sm" 
                        variant="outline" 
                        className="text-red-600 hover:text-red-700"
                        onClick={() => handleDelete(checklist)}
                      >
                        <Trash2 className="h-4 w-4" />
                      </Button>
                    </div>
                  </div>
                </div>
              ))
            )}
          </div>
        </CardContent>
      </Card>

      {/* Create/Edit Dialog */}
      <Dialog open={isCreateDialogOpen || isEditDialogOpen} onOpenChange={(open) => {
        if (!open) {
          setIsCreateDialogOpen(false);
          setIsEditDialogOpen(false);
          resetForm();
        }
      }}>
        <DialogContent className="max-w-4xl max-h-[80vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle>
              {isCreateDialogOpen ? 'Create New Checklist' : 'Edit Checklist'}
            </DialogTitle>
            <DialogDescription>
              {isCreateDialogOpen 
                ? 'Create a new quality control checklist for maintenance activities.'
                : 'Update the checklist information and items.'
              }
            </DialogDescription>
          </DialogHeader>
          
          <Tabs defaultValue="basic" className="space-y-4">
            <TabsList>
              <TabsTrigger value="basic">Basic Information</TabsTrigger>
              <TabsTrigger value="items">Checklist Items ({formData.items.length})</TabsTrigger>
            </TabsList>
            
            <TabsContent value="basic" className="space-y-4">
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="name">Checklist Name *</Label>
                  <Input
                    id="name"
                    value={formData.name}
                    onChange={(e) => setFormData({...formData, name: e.target.value})}
                    placeholder="Enter checklist name"
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="minimumScore">Minimum Passing Score (%) *</Label>
                  <Input
                    id="minimumScore"
                    type="number"
                    min="0"
                    max="100"
                    value={formData.minimumPassingScore}
                    onChange={(e) => setFormData({...formData, minimumPassingScore: parseInt(e.target.value) || 85})}
                  />
                </div>
              </div>

              <div className="space-y-2">
                <Label htmlFor="description">Description</Label>
                <Textarea
                  id="description"
                  value={formData.description}
                  onChange={(e) => setFormData({...formData, description: e.target.value})}
                  placeholder="Enter checklist description"
                  rows={3}
                />
              </div>

              <div className="grid grid-cols-3 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="workOrderType">Work Order Type *</Label>
                  <Select 
                    value={formData.workOrderType} 
                    onValueChange={(value) => setFormData({...formData, workOrderType: value})}
                  >
                    <SelectTrigger>
                      <SelectValue placeholder="Select type" />
                    </SelectTrigger>
                    <SelectContent>
                      {workOrderTypes.map(type => (
                        <SelectItem key={type} value={type}>{type}</SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>

                <div className="space-y-2">
                  <Label htmlFor="assetCategory">Asset Category *</Label>
                  <Select 
                    value={formData.assetCategory} 
                    onValueChange={(value) => setFormData({...formData, assetCategory: value})}
                  >
                    <SelectTrigger>
                      <SelectValue placeholder="Select category" />
                    </SelectTrigger>
                    <SelectContent>
                      {assetCategories.map(category => (
                        <SelectItem key={category} value={category}>{category}</SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>

                <div className="space-y-2">
                  <Label htmlFor="maintenanceType">Maintenance Type</Label>
                  <Select 
                    value={formData.maintenanceType} 
                    onValueChange={(value) => setFormData({...formData, maintenanceType: value})}
                  >
                    <SelectTrigger>
                      <SelectValue placeholder="Select type (optional)" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="none">None</SelectItem>
                      {maintenanceTypes.map(type => (
                        <SelectItem key={type} value={type}>{type}</SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
              </div>

              <div className="flex items-center space-x-2">
                <Switch
                  id="mandatory"
                  checked={formData.isMandatory}
                  onCheckedChange={(checked) => setFormData({...formData, isMandatory: checked})}
                />
                <Label htmlFor="mandatory">Mandatory Checklist</Label>
              </div>
            </TabsContent>

            <TabsContent value="items" className="space-y-4">
              <div className="flex items-center justify-between">
                <h4 className="text-sm font-medium">Checklist Items</h4>
                <Button size="sm" onClick={addChecklistItem}>
                  <Plus className="mr-2 h-4 w-4" />
                  Add Item
                </Button>
              </div>

              <div className="space-y-3 max-h-96 overflow-y-auto">
                {formData.items.length === 0 ? (
                  <div className="text-center py-8 text-muted-foreground">
                    No checklist items added yet. Click "Add Item" to get started.
                  </div>
                ) : (
                  formData.items.map((item, index) => (
                    <div key={item.id || index} className="border rounded-lg p-3">
                      <div className="flex items-start justify-between">
                        <div className="flex-1">
                          <div className="flex items-center space-x-2 mb-2">
                            <GripVertical className="h-4 w-4 text-muted-foreground" />
                            <span className="font-medium text-sm">{item.text}</span>
                            {item.required && <Badge variant="outline" className="text-xs">Required</Badge>}
                            {item.critical && <Badge variant="destructive" className="text-xs">Critical</Badge>}
                            <Badge variant="secondary" className="text-xs">{item.responseType}</Badge>
                          </div>
                          {item.description && (
                            <p className="text-xs text-muted-foreground ml-6">{item.description}</p>
                          )}
                        </div>
                        <div className="flex items-center space-x-1">
                          <Button size="sm" variant="ghost" onClick={() => setEditingItem(item)}>
                            <Edit className="h-3 w-3" />
                          </Button>
                          <Button 
                            size="sm" 
                            variant="ghost" 
                            onClick={() => removeChecklistItem(item.id)}
                            className="text-red-600 hover:text-red-700"
                          >
                            <X className="h-3 w-3" />
                          </Button>
                        </div>
                      </div>
                    </div>
                  ))
                )}
              </div>

              {/* Item Editor */}
              {editingItem && (
                <div className="border rounded-lg p-4 bg-muted/50">
                  <h5 className="font-medium mb-3">
                    {editingItem.id && !editingItem.id.startsWith('temp-') ? 'Edit Item' : 'Add New Item'}
                  </h5>
                  
                  <div className="space-y-3">
                    <div className="grid grid-cols-2 gap-3">
                      <div className="space-y-2">
                        <Label htmlFor="itemText">Item Text *</Label>
                        <Input
                          id="itemText"
                          value={editingItem.text}
                          onChange={(e) => setEditingItem({...editingItem, text: e.target.value})}
                          placeholder="Enter checklist item"
                        />
                      </div>
                      <div className="space-y-2">
                        <Label htmlFor="itemCategory">Category</Label>
                        <Select 
                          value={editingItem.category} 
                          onValueChange={(value) => setEditingItem({...editingItem, category: value})}
                        >
                          <SelectTrigger>
                            <SelectValue placeholder="Select category" />
                          </SelectTrigger>
                          <SelectContent>
                            {checklistCategories.map(category => (
                              <SelectItem key={category} value={category}>{category}</SelectItem>
                            ))}
                          </SelectContent>
                        </Select>
                      </div>
                    </div>

                    <div className="space-y-2">
                      <Label htmlFor="itemDescription">Description</Label>
                      <Textarea
                        id="itemDescription"
                        value={editingItem.description}
                        onChange={(e) => setEditingItem({...editingItem, description: e.target.value})}
                        placeholder="Enter item description (optional)"
                        rows={2}
                      />
                    </div>

                    <div className="grid grid-cols-4 gap-3">
                      <div className="space-y-2">
                        <Label htmlFor="responseType">Response Type</Label>
                        <Select 
                          value={editingItem.responseType} 
                          onValueChange={(value) => setEditingItem({...editingItem, responseType: value as any})}
                        >
                          <SelectTrigger>
                            <SelectValue />
                          </SelectTrigger>
                          <SelectContent>
                            <SelectItem value="Pass/Fail">Pass/Fail</SelectItem>
                            <SelectItem value="Score">Score</SelectItem>
                            <SelectItem value="Text">Text</SelectItem>
                            <SelectItem value="Checkbox">Checkbox</SelectItem>
                          </SelectContent>
                        </Select>
                      </div>
                      
                      {editingItem.responseType === 'Score' && (
                        <>
                          <div className="space-y-2">
                            <Label htmlFor="minScore">Min Score</Label>
                            <Input
                              id="minScore"
                              type="number"
                              min="1"
                              value={editingItem.minScore || 1}
                              onChange={(e) => setEditingItem({...editingItem, minScore: parseInt(e.target.value) || 1})}
                            />
                          </div>
                          <div className="space-y-2">
                            <Label htmlFor="maxScore">Max Score</Label>
                            <Input
                              id="maxScore"
                              type="number"
                              min="1"
                              value={editingItem.maxScore || 10}
                              onChange={(e) => setEditingItem({...editingItem, maxScore: parseInt(e.target.value) || 10})}
                            />
                          </div>
                        </>
                      )}
                    </div>

                    <div className="flex items-center space-x-6">
                      <div className="flex items-center space-x-2">
                        <Switch
                          id="required"
                          checked={editingItem.required}
                          onCheckedChange={(checked) => setEditingItem({...editingItem, required: checked})}
                        />
                        <Label htmlFor="required">Required</Label>
                      </div>
                      
                      <div className="flex items-center space-x-2">
                        <Switch
                          id="critical"
                          checked={editingItem.critical}
                          onCheckedChange={(checked) => setEditingItem({...editingItem, critical: checked})}
                        />
                        <Label htmlFor="critical">Critical</Label>
                      </div>
                    </div>

                    <div className="flex items-center space-x-2 pt-3">
                      <Button size="sm" onClick={saveChecklistItem}>Save Item</Button>
                      <Button size="sm" variant="outline" onClick={() => setEditingItem(null)}>Cancel</Button>
                    </div>
                  </div>
                </div>
              )}
            </TabsContent>
          </Tabs>

          <DialogFooter>
            <Button variant="outline" onClick={() => {
              setIsCreateDialogOpen(false);
              setIsEditDialogOpen(false);
              resetForm();
            }}>
              Cancel
            </Button>
            <Button 
              onClick={isCreateDialogOpen ? handleCreate : handleUpdate}
              disabled={!formData.name || !formData.workOrderType || !formData.assetCategory || isSubmitting}
            >
              {isSubmitting 
                ? (isCreateDialogOpen ? 'Creating...' : 'Updating...')
                : (isCreateDialogOpen ? 'Create Checklist' : 'Update Checklist')
              }
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Preview Dialog */}
      <Dialog open={isPreviewDialogOpen} onOpenChange={setIsPreviewDialogOpen}>
        <DialogContent className="max-w-3xl max-h-[80vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Checklist Preview</DialogTitle>
            <DialogDescription>
              Preview how this checklist will appear during inspections
            </DialogDescription>
          </DialogHeader>
          
          {selectedChecklist && (
            <div className="space-y-4">
              <div className="border rounded-lg p-4">
                <h3 className="font-semibold text-lg">{selectedChecklist.name}</h3>
                <p className="text-sm text-muted-foreground mt-1">{selectedChecklist.description}</p>
                
                <div className="flex items-center space-x-4 mt-3 text-sm">
                  <span><strong>Type:</strong> {selectedChecklist.workOrderType}</span>
                  <span><strong>Category:</strong> {selectedChecklist.assetCategory}</span>
                  <span><strong>Pass Score:</strong> {selectedChecklist.minimumPassingScore}%</span>
                </div>
              </div>

              <div className="space-y-3">
                <h4 className="font-medium">Checklist Items ({selectedChecklist.items.length})</h4>
                {selectedChecklist.items.map((item, index) => (
                  <div key={item.id} className="border rounded-lg p-3">
                    <div className="flex items-start justify-between">
                      <div className="flex-1">
                        <div className="flex items-center space-x-2 mb-1">
                          <span className="font-medium text-sm">{index + 1}. {item.text}</span>
                          {item.required && <Badge variant="outline" className="text-xs">Required</Badge>}
                          {item.critical && <Badge variant="destructive" className="text-xs">Critical</Badge>}
                        </div>
                        {item.description && (
                          <p className="text-xs text-muted-foreground mb-2">{item.description}</p>
                        )}
                        <div className="flex items-center space-x-2 text-xs text-muted-foreground">
                          <Badge variant="secondary" className="text-xs">{item.responseType}</Badge>
                          <span>Category: {item.category}</span>
                        </div>
                      </div>
                    </div>
                  </div>
                ))}
              </div>
            </div>
          )}
          
          <DialogFooter>
            <Button variant="outline" onClick={() => setIsPreviewDialogOpen(false)}>
              Close Preview
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Duplicate Dialog */}
      <Dialog open={isDuplicateDialogOpen} onOpenChange={setIsDuplicateDialogOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Duplicate Checklist</DialogTitle>
            <DialogDescription>
              Create a copy of "{selectedChecklist?.name}" with a new name.
            </DialogDescription>
          </DialogHeader>
          
          <div className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="duplicateName">New Checklist Name</Label>
              <Input
                id="duplicateName"
                value={duplicateName}
                onChange={(e) => setDuplicateName(e.target.value)}
                placeholder="Enter name for the duplicate checklist"
              />
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setIsDuplicateDialogOpen(false)}>
              Cancel
            </Button>
            <Button onClick={handleDuplicate} disabled={!duplicateName}>
              Create Duplicate
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}