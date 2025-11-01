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
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { 
  Plus,
  Search,
  Edit,
  Trash2,
  FileText,
  Settings,
  AlertTriangle,
  Package,
  ListChecks,
  Clock
} from 'lucide-react';

// Task template interfaces
interface TaskTemplate {
  id: string;
  taskName: string;
  description?: string;
  sequence: number;
  estimatedHours: number;
  isRequired: boolean;
  instructions?: string;
  safetyRequirements?: string;
  requiredTools?: string;
  requiredParts?: string;
  isActive: boolean;
}

interface AssetTaskTemplate extends TaskTemplate {
  assetId: string;
  assetName: string;
  maintenanceTypeId: string;
  maintenanceTypeName: string;
}

interface AssetTypeTaskTemplate extends TaskTemplate {
  assetTypeId: string;
  assetTypeName: string;
  maintenanceTypeId: string;
  maintenanceTypeName: string;
}

interface MaintenanceTaskTemplate extends TaskTemplate {
  maintenanceTypeId: string;
  maintenanceTypeName: string;
}

interface MaintenanceType {
  id: string;
  name: string;
  code: string;
}

interface Asset {
  id: string;
  name: string;
  assetNumber: string;
}

interface AssetType {
  id: string;
  name: string;
}

export default function TaskTemplatesPage() {
  const [activeTab, setActiveTab] = useState('asset');
  const [searchTerm, setSearchTerm] = useState('');
  const [statusFilter, setStatusFilter] = useState('all');
  const [typeFilter, setTypeFilter] = useState('all');
  const [isCreateDialogOpen, setIsCreateDialogOpen] = useState(false);
  const [isEditDialogOpen, setIsEditDialogOpen] = useState(false);
  const [selectedTemplate, setSelectedTemplate] = useState<any | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [mounted, setMounted] = useState(false);
  
  // Data states
  const [assetTemplates, setAssetTemplates] = useState<AssetTaskTemplate[]>([]);
  const [assetTypeTemplates, setAssetTypeTemplates] = useState<AssetTypeTaskTemplate[]>([]);
  const [maintenanceTemplates, setMaintenanceTemplates] = useState<MaintenanceTaskTemplate[]>([]);
  const [maintenanceTypes, setMaintenanceTypes] = useState<MaintenanceType[]>([]);
  const [assets, setAssets] = useState<Asset[]>([]);
  const [assetTypes, setAssetTypes] = useState<AssetType[]>([]);
  
  // Form state
  const [formData, setFormData] = useState({
    taskName: '',
    description: '',
    sequence: 1,
    estimatedHours: 1,
    isRequired: true,
    instructions: '',
    safetyRequirements: '',
    requiredTools: '',
    requiredParts: '',
    isActive: true,
    assetId: '',
    assetTypeId: '',
    maintenanceTypeId: ''
  });

  useEffect(() => {
    setMounted(true);
    fetchMaintenanceTypes();
    fetchAssets();
    fetchAssetTypes();
    fetchAllTemplates();
  }, []);

  const fetchMaintenanceTypes = async () => {
    try {
      const token = localStorage.getItem('token') || localStorage.getItem('authToken');
      const response = await fetch('http://localhost:5000/api/maintenance/maintenance-types?pageSize=1000', {
        headers: {
          'Authorization': `Bearer ${token}`,
          'Content-Type': 'application/json'
        }
      });
      if (response.ok) {
        const data = await response.json();
        setMaintenanceTypes(data.data || data.items || data || []);
      }
    } catch (error) {
      console.error('Failed to fetch maintenance types:', error);
    }
  };

  const fetchAssets = async () => {
    try {
      const token = localStorage.getItem('token') || localStorage.getItem('authToken');
      const response = await fetch('http://localhost:5000/api/maintenance/assets?pageSize=1000', {
        headers: {
          'Authorization': `Bearer ${token}`,
          'Content-Type': 'application/json'
        }
      });
      if (response.ok) {
        const data = await response.json();
        setAssets(data.data || data.items || data || []);
      }
    } catch (error) {
      console.error('Failed to fetch assets:', error);
    }
  };

  const fetchAssetTypes = async () => {
    // For now, use mock data since asset types endpoint might not exist
    setAssetTypes([
      { id: '1', name: 'HVAC System' },
      { id: '2', name: 'Elevator' },
      { id: '3', name: 'Generator' }
    ]);
  };

  const fetchAllTemplates = async () => {
    setLoading(true);
    setError(null);
    try {
      const token = localStorage.getItem('token') || localStorage.getItem('authToken');
      
      // Fetch all asset templates
      try {
        const assetResponse = await fetch('http://localhost:5000/api/maintenance/task-templates/asset', {
          headers: {
            'Authorization': `Bearer ${token}`,
            'Content-Type': 'application/json'
          }
        });
        
        if (assetResponse.ok) {
          const assetData = await assetResponse.json();
          setAssetTemplates(Array.isArray(assetData) ? assetData : []);
        } else {
          console.error('Failed to fetch asset templates:', assetResponse.status);
          setAssetTemplates([]);
        }
      } catch (error) {
        console.error('Error fetching asset templates:', error);
        setAssetTemplates([]);
      }
      
      // Fetch all asset type templates
      try {
        const assetTypeResponse = await fetch('http://localhost:5000/api/maintenance/task-templates/asset-type', {
          headers: {
            'Authorization': `Bearer ${token}`,
            'Content-Type': 'application/json'
          }
        });
        
        if (assetTypeResponse.ok) {
          const assetTypeData = await assetTypeResponse.json();
          setAssetTypeTemplates(Array.isArray(assetTypeData) ? assetTypeData : []);
        } else {
          console.error('Failed to fetch asset type templates:', assetTypeResponse.status);
          setAssetTypeTemplates([]);
        }
      } catch (error) {
        console.error('Error fetching asset type templates:', error);
        setAssetTypeTemplates([]);
      }
      
      // Fetch all maintenance type templates
      const maintenanceResponse = await fetch('http://localhost:5000/api/maintenance/task-templates/maintenance-type', {
        headers: {
          'Authorization': `Bearer ${token}`,
          'Content-Type': 'application/json'
        }
      });
      
      if (maintenanceResponse.ok) {
        const maintenanceData = await maintenanceResponse.json();
        setMaintenanceTemplates(Array.isArray(maintenanceData) ? maintenanceData : []);
      } else {
        console.error('Failed to fetch maintenance templates:', maintenanceResponse.status);
        setMaintenanceTemplates([]);
      }
    } catch (error) {
      console.error('Failed to fetch templates:', error);
      setError('Failed to load task templates.');
      setMaintenanceTemplates([]);
    } finally {
      setLoading(false);
    }
  };

  const getCurrentTemplates = () => {
    switch (activeTab) {
      case 'asset':
        return assetTemplates;
      case 'assetType':
        return assetTypeTemplates;
      case 'maintenance':
        return maintenanceTemplates;
      default:
        return [];
    }
  };

  const filteredData = useMemo(() => {
    let filtered = getCurrentTemplates();

    if (searchTerm) {
      filtered = filtered.filter((item: any) =>
        item.taskName?.toLowerCase().includes(searchTerm.toLowerCase()) ||
        item.description?.toLowerCase().includes(searchTerm.toLowerCase())
      );
    }

    if (statusFilter !== 'all') {
      filtered = filtered.filter((item: any) => 
        statusFilter === 'active' ? item.isActive : !item.isActive
      );
    }

    if (typeFilter !== 'all') {
      filtered = filtered.filter((item: any) => item.maintenanceTypeId === typeFilter);
    }

    return filtered.sort((a: any, b: any) => a.sequence - b.sequence);
  }, [assetTemplates, assetTypeTemplates, maintenanceTemplates, activeTab, searchTerm, statusFilter, typeFilter]);

  // Group templates for better organization
  const groupedData = useMemo(() => {
    const groups: { [key: string]: any[] } = {};
    
    filteredData.forEach((template: any) => {
      let groupKey = '';
      
      if (activeTab === 'asset') {
        groupKey = (template as AssetTaskTemplate).assetName || 'Unknown Asset';
      } else if (activeTab === 'assetType') {
        groupKey = (template as AssetTypeTaskTemplate).assetTypeName || 'Unknown Asset Type';
      } else {
        groupKey = template.maintenanceTypeName || 'Unknown Maintenance Type';
      }
      
      if (!groups[groupKey]) {
        groups[groupKey] = [];
      }
      groups[groupKey].push(template);
    });
    
    return groups;
  }, [filteredData, activeTab]);

  const handleCreate = async () => {
    try {
      const token = localStorage.getItem('token') || localStorage.getItem('authToken');
      
      if (!token) {
        setError('Authentication required. Please log in.');
        return;
      }

      let endpoint = '';
      let createDto: any = {
        taskName: formData.taskName,
        description: formData.description,
        sequence: formData.sequence,
        estimatedHours: formData.estimatedHours,
        isRequired: formData.isRequired,
        instructions: formData.instructions,
        safetyRequirements: formData.safetyRequirements,
        isActive: formData.isActive,
        maintenanceTypeId: formData.maintenanceTypeId
      };

      if (activeTab === 'asset') {
        endpoint = 'http://localhost:5000/api/maintenance/task-templates/asset';
        createDto.assetId = formData.assetId;
        createDto.requiredTools = formData.requiredTools;
        createDto.requiredParts = formData.requiredParts;
      } else if (activeTab === 'assetType') {
        endpoint = 'http://localhost:5000/api/maintenance/task-templates/asset-type';
        createDto.assetTypeId = formData.assetTypeId;
        createDto.requiredTools = formData.requiredTools;
        createDto.requiredParts = formData.requiredParts;
      } else {
        endpoint = 'http://localhost:5000/api/maintenance/task-templates/maintenance-type';
      }

      const response = await fetch(endpoint, {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${token}`,
          'Content-Type': 'application/json'
        },
        body: JSON.stringify(createDto)
      });

      if (!response.ok) {
        const errorText = await response.text();
        console.error('Create failed:', response.status, errorText);
        setError(`Failed to create task template: ${response.status} ${errorText}`);
        return;
      }

      await fetchAllTemplates();
      setIsCreateDialogOpen(false);
      resetForm();
    } catch (error) {
      console.error('Error creating template:', error);
      setError('Failed to create task template. Please try again.');
    }
  };

  const handleEdit = (template: any) => {
    setSelectedTemplate(template);
    setFormData({
      taskName: template.taskName || '',
      description: template.description || '',
      sequence: template.sequence || 1,
      estimatedHours: template.estimatedHours || 1,
      isRequired: template.isRequired ?? true,
      instructions: template.instructions || '',
      safetyRequirements: template.safetyRequirements || '',
      requiredTools: template.requiredTools || '',
      requiredParts: template.requiredParts || '',
      isActive: template.isActive ?? true,
      assetId: template.assetId || '',
      assetTypeId: template.assetTypeId || '',
      maintenanceTypeId: template.maintenanceTypeId || ''
    });
    setIsEditDialogOpen(true);
  };

  const handleUpdate = async () => {
    if (!selectedTemplate?.id) return;
    
    try {
      const token = localStorage.getItem('token') || localStorage.getItem('authToken');
      
      if (!token) {
        setError('Authentication required. Please log in.');
        return;
      }

      let endpoint = '';
      let updateDto: any = {
        taskName: formData.taskName,
        description: formData.description,
        sequence: formData.sequence,
        estimatedHours: formData.estimatedHours,
        isRequired: formData.isRequired,
        instructions: formData.instructions,
        safetyRequirements: formData.safetyRequirements,
        isActive: formData.isActive
      };

      if (activeTab === 'asset') {
        endpoint = `http://localhost:5000/api/maintenance/task-templates/asset/${selectedTemplate.id}`;
        updateDto.requiredTools = formData.requiredTools;
        updateDto.requiredParts = formData.requiredParts;
      } else if (activeTab === 'assetType') {
        endpoint = `http://localhost:5000/api/maintenance/task-templates/asset-type/${selectedTemplate.id}`;
        updateDto.requiredTools = formData.requiredTools;
        updateDto.requiredParts = formData.requiredParts;
      } else {
        endpoint = `http://localhost:5000/api/maintenance/task-templates/maintenance-type/${selectedTemplate.id}`;
      }

      const response = await fetch(endpoint, {
        method: 'PUT',
        headers: {
          'Authorization': `Bearer ${token}`,
          'Content-Type': 'application/json'
        },
        body: JSON.stringify(updateDto)
      });

      if (!response.ok) {
        const errorText = await response.text();
        console.error('Update failed:', response.status, errorText);
        setError(`Failed to update task template: ${response.status} ${errorText}`);
        return;
      }

      await fetchAllTemplates();
      setIsEditDialogOpen(false);
      resetForm();
    } catch (error) {
      console.error('Error updating template:', error);
      setError('Failed to update task template. Please try again.');
    }
  };

  const handleDelete = async (id: string) => {
    if (!confirm('Are you sure you want to delete this task template?')) return;
    
    try {
      const token = localStorage.getItem('token') || localStorage.getItem('authToken');
      
      if (!token) {
        setError('Authentication required. Please log in.');
        return;
      }

      let endpoint = '';
      if (activeTab === 'asset') {
        endpoint = `http://localhost:5000/api/maintenance/task-templates/asset/${id}`;
      } else if (activeTab === 'assetType') {
        endpoint = `http://localhost:5000/api/maintenance/task-templates/asset-type/${id}`;
      } else {
        endpoint = `http://localhost:5000/api/maintenance/task-templates/maintenance-type/${id}`;
      }

      const response = await fetch(endpoint, {
        method: 'DELETE',
        headers: {
          'Authorization': `Bearer ${token}`,
          'Content-Type': 'application/json'
        }
      });

      if (!response.ok) {
        const errorText = await response.text();
        console.error('Delete failed:', response.status, errorText);
        setError(`Failed to delete task template: ${response.status} ${errorText}`);
        return;
      }

      await fetchAllTemplates();
    } catch (error) {
      console.error('Error deleting template:', error);
      setError('Failed to delete task template. Please try again.');
    }
  };

  const resetForm = () => {
    setFormData({
      taskName: '',
      description: '',
      sequence: 1,
      estimatedHours: 1,
      isRequired: true,
      instructions: '',
      safetyRequirements: '',
      requiredTools: '',
      requiredParts: '',
      isActive: true,
      assetId: '',
      assetTypeId: '',
      maintenanceTypeId: ''
    });
    setSelectedTemplate(null);
  };

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Task Templates</h1>
          <p className="text-muted-foreground">
            Manage task templates for work orders across assets, asset types, and maintenance types
          </p>
        </div>
        <Dialog open={isCreateDialogOpen} onOpenChange={setIsCreateDialogOpen}>
          <DialogTrigger asChild>
            <Button>
              <Plus className="mr-2 h-4 w-4" />
              Add Template
            </Button>
          </DialogTrigger>
          <DialogContent className="sm:max-w-[700px]">
            <DialogHeader>
              <DialogTitle>Add Task Template</DialogTitle>
              <DialogDescription>
                Create a new task template for {activeTab === 'asset' ? 'a specific asset' : activeTab === 'assetType' ? 'an asset type' : 'a maintenance type'}.
              </DialogDescription>
            </DialogHeader>
            <div className="grid gap-4 py-4 max-h-[70vh] overflow-y-auto">
              {activeTab === 'asset' && (
                <div className="space-y-2">
                  <Label htmlFor="asset">Asset</Label>
                  <Select value={formData.assetId} onValueChange={(value) => setFormData({...formData, assetId: value})}>
                    <SelectTrigger>
                      <SelectValue placeholder="Select asset" />
                    </SelectTrigger>
                    <SelectContent>
                      {assets.map((asset) => (
                        <SelectItem key={asset.id} value={asset.id}>
                          {asset.name} ({asset.assetNumber})
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
              )}
              {activeTab === 'assetType' && (
                <div className="space-y-2">
                  <Label htmlFor="assetType">Asset Type</Label>
                  <Select value={formData.assetTypeId} onValueChange={(value) => setFormData({...formData, assetTypeId: value})}>
                    <SelectTrigger>
                      <SelectValue placeholder="Select asset type" />
                    </SelectTrigger>
                    <SelectContent>
                      {assetTypes.map((type) => (
                        <SelectItem key={type.id} value={type.id}>
                          {type.name}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
              )}
              <div className="space-y-2">
                <Label htmlFor="maintenanceType">Maintenance Type</Label>
                <Select value={formData.maintenanceTypeId} onValueChange={(value) => setFormData({...formData, maintenanceTypeId: value})}>
                  <SelectTrigger>
                    <SelectValue placeholder="Select maintenance type" />
                  </SelectTrigger>
                  <SelectContent>
                    {maintenanceTypes.map((type) => (
                      <SelectItem key={type.id} value={type.id}>
                        {type.name}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="taskName">Task Name</Label>
                  <Input
                    id="taskName"
                    value={formData.taskName}
                    onChange={(e) => setFormData({...formData, taskName: e.target.value})}
                    placeholder="e.g., Check fluid levels"
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="sequence">Sequence</Label>
                  <Input
                    id="sequence"
                    type="number"
                    value={formData.sequence}
                    onChange={(e) => setFormData({...formData, sequence: parseInt(e.target.value) || 1})}
                    placeholder="1"
                  />
                </div>
              </div>
              <div className="space-y-2">
                <Label htmlFor="description">Description</Label>
                <Textarea
                  id="description"
                  value={formData.description}
                  onChange={(e) => setFormData({...formData, description: e.target.value})}
                  placeholder="Describe the task..."
                  rows={2}
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="instructions">Instructions</Label>
                <Textarea
                  id="instructions"
                  value={formData.instructions}
                  onChange={(e) => setFormData({...formData, instructions: e.target.value})}
                  placeholder="Step-by-step instructions..."
                  rows={3}
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="safetyRequirements">Safety Requirements</Label>
                <Textarea
                  id="safetyRequirements"
                  value={formData.safetyRequirements}
                  onChange={(e) => setFormData({...formData, safetyRequirements: e.target.value})}
                  placeholder="Safety protocols and PPE requirements..."
                  rows={2}
                />
              </div>
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="requiredTools">Required Tools</Label>
                  <Input
                    id="requiredTools"
                    value={formData.requiredTools}
                    onChange={(e) => setFormData({...formData, requiredTools: e.target.value})}
                    placeholder="e.g., Wrench, Multimeter"
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="requiredParts">Required Parts</Label>
                  <Input
                    id="requiredParts"
                    value={formData.requiredParts}
                    onChange={(e) => setFormData({...formData, requiredParts: e.target.value})}
                    placeholder="e.g., Oil filter, Gasket"
                  />
                </div>
              </div>
              <div className="space-y-2">
                <Label htmlFor="estimatedHours">Estimated Hours</Label>
                <Input
                  id="estimatedHours"
                  type="number"
                  step="0.5"
                  value={formData.estimatedHours}
                  onChange={(e) => setFormData({...formData, estimatedHours: parseFloat(e.target.value) || 1})}
                  placeholder="1.0"
                />
              </div>
              <div className="space-y-4">
                <div className="flex items-center space-x-2">
                  <Switch
                    id="isRequired"
                    checked={formData.isRequired}
                    onCheckedChange={(checked) => setFormData({...formData, isRequired: checked})}
                  />
                  <Label htmlFor="isRequired">Required Task</Label>
                </div>
                <div className="flex items-center space-x-2">
                  <Switch
                    id="isActive"
                    checked={formData.isActive}
                    onCheckedChange={(checked) => setFormData({...formData, isActive: checked})}
                  />
                  <Label htmlFor="isActive">Active</Label>
                </div>
              </div>
            </div>
            <DialogFooter>
              <Button variant="outline" onClick={() => setIsCreateDialogOpen(false)}>
                Cancel
              </Button>
              <Button onClick={handleCreate}>
                Create Template
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
            <BreadcrumbPage>Task Templates</BreadcrumbPage>
          </BreadcrumbItem>
        </BreadcrumbList>
      </Breadcrumb>

      {/* Error Display */}
      {error && (
        <Card className="border-yellow-200 bg-yellow-50">
          <CardContent className="pt-6">
            <div className="flex items-center space-x-2 text-yellow-700">
              <AlertTriangle className="h-5 w-5" />
              <p className="font-medium">{error}</p>
            </div>
          </CardContent>
        </Card>
      )}

      {/* Template Type Tabs */}
      <Tabs value={activeTab} onValueChange={setActiveTab} className="w-full">
        <TabsList className="grid w-full grid-cols-3">
          <TabsTrigger value="asset">
            <Package className="mr-2 h-4 w-4" />
            Asset Templates
          </TabsTrigger>
          <TabsTrigger value="assetType">
            <Settings className="mr-2 h-4 w-4" />
            Asset Type Templates
          </TabsTrigger>
          <TabsTrigger value="maintenance">
            <ListChecks className="mr-2 h-4 w-4" />
            Maintenance Type Templates
          </TabsTrigger>
        </TabsList>

        <TabsContent value={activeTab} className="space-y-4">
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
                    placeholder="Search templates..."
                    className="pl-8"
                    value={searchTerm}
                    onChange={(e) => setSearchTerm(e.target.value)}
                  />
                </div>
                
                <Select value={typeFilter} onValueChange={setTypeFilter}>
                      <SelectTrigger>
                        <SelectValue placeholder="Maintenance Type" />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="all">All Types</SelectItem>
                        {maintenanceTypes.map((type) => (
                          <SelectItem key={type.id} value={type.id}>
                            {type.name}
                          </SelectItem>
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

          {/* Templates List */}
          <Card>
            <CardHeader>
              <CardTitle>Task Templates</CardTitle>
              <CardDescription>
                {filteredData.length} template{filteredData.length === 1 ? '' : 's'} found
              </CardDescription>
            </CardHeader>
            <CardContent>
              {loading ? (
                <div className="text-center py-4">Loading task templates...</div>
              ) : filteredData.length === 0 ? (
                <div className="text-center py-8 text-muted-foreground">
                  <FileText className="mx-auto h-12 w-12 mb-4 text-gray-400" />
                  <p>No task templates found.</p>
                  <p className="text-sm">Click "Add Template" to create your first template.</p>
                </div>
              ) : (
              <div className="space-y-6">
                {Object.entries(groupedData).map(([groupName, templates]) => (
                  <div key={groupName} className="space-y-3">
                    {/* Group Header */}
                    <div className="flex items-center gap-3 px-3 py-2 bg-muted/30 rounded-md border-l-4 border-primary">
                      {activeTab === 'asset' && <Package className="h-5 w-5 text-primary" />}
                      {activeTab === 'assetType' && <Settings className="h-5 w-5 text-primary" />}
                      {activeTab === 'maintenance' && <ListChecks className="h-5 w-5 text-primary" />}
                      <h3 className="font-semibold text-lg">{groupName}</h3>
                      <Badge variant="outline" className="ml-auto">{templates.length} template{templates.length === 1 ? '' : 's'}</Badge>
                    </div>
                    
                    {/* Templates in Group */}
                    <div className="space-y-3 pl-3">
                      {templates.map((template: any) => (
                        <div key={template.id} className="border rounded-lg p-4 hover:bg-muted/50 transition-colors">
                          <div className="flex items-start justify-between">
                            <div className="space-y-3 flex-1">
                              <div className="flex items-center space-x-3">
                                <Badge variant="outline" className="font-mono">
                                  #{template.sequence}
                                </Badge>
                                <h3 className="font-semibold">{template.taskName}</h3>
                                {template.isRequired && (
                                  <Badge className="bg-red-100 text-red-800">
                                    Required
                                  </Badge>
                                )}
                                <Badge className={template.isActive ? 'bg-green-100 text-green-800' : 'bg-gray-100 text-gray-800'}>
                                  {template.isActive ? 'Active' : 'Inactive'}
                                </Badge>
                              </div>
                              
                              <div className="grid grid-cols-1 md:grid-cols-2 gap-4 text-sm text-muted-foreground">
                                <div className="flex items-center space-x-2">
                                  <Clock className="h-4 w-4" />
                                  <span>{template.estimatedHours}h estimated</span>
                                </div>
                                <div className="flex items-center space-x-2">
                                  <ListChecks className="h-4 w-4" />
                                  <span>{template.maintenanceTypeName}</span>
                                </div>
                              </div>
                              
                              {template.description && (
                                <p className="text-sm text-muted-foreground">{template.description}</p>
                              )}
                              
                              {template.safetyRequirements && (
                                <div className="flex items-start space-x-2 text-sm">
                                  <AlertTriangle className="h-4 w-4 text-yellow-600 mt-0.5" />
                                  <span className="text-yellow-700">{template.safetyRequirements}</span>
                                </div>
                              )}
                            </div>
                            
                            <div className="flex items-center space-x-2">
                              <Button size="sm" variant="outline" onClick={() => handleEdit(template)}>
                                <Edit className="h-4 w-4" />
                              </Button>
                              <Button 
                                size="sm" 
                                variant="outline" 
                                className="text-red-600 hover:text-red-700"
                                onClick={() => handleDelete(template.id)}
                              >
                                <Trash2 className="h-4 w-4" />
                              </Button>
                            </div>
                          </div>
                        </div>
                      ))}
                    </div>
                  </div>
                ))}
              </div>
              )}
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>

      {/* Edit Dialog (similar structure to create dialog) */}
      {selectedTemplate && <Dialog open={isEditDialogOpen} onOpenChange={setIsEditDialogOpen}>
        <DialogContent className="max-w-5xl">
          <DialogHeader>
            <DialogTitle>Edit Task Template</DialogTitle>
            <DialogDescription>
              Update the task template information.
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-6 py-4 max-h-[75vh] overflow-y-auto">
            {/* Asset/Asset Type/Maintenance Type Dropdowns */}
            {activeTab === 'asset' && (
              <div className="space-y-2">
                <Label htmlFor="edit-asset">Asset</Label>
                <Select value={formData.assetId} onValueChange={(value) => setFormData({...formData, assetId: value})}>
                  <SelectTrigger>
                    <SelectValue placeholder="Select asset" />
                  </SelectTrigger>
                  <SelectContent>
                    {assets.map((asset) => (
                      <SelectItem key={asset.id} value={asset.id}>
                        {asset.name} ({asset.assetNumber})
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            )}
            {activeTab === 'assetType' && (
              <div className="space-y-2">
                <Label htmlFor="edit-assetType">Asset Type</Label>
                <Select value={formData.assetTypeId} onValueChange={(value) => setFormData({...formData, assetTypeId: value})}>
                  <SelectTrigger>
                    <SelectValue placeholder="Select asset type" />
                  </SelectTrigger>
                  <SelectContent>
                    {assetTypes.map((type) => (
                      <SelectItem key={type.id} value={type.id}>
                        {type.name}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            )}
            <div className="space-y-2">
              <Label htmlFor="edit-maintenanceType">Maintenance Type</Label>
              <Select value={formData.maintenanceTypeId} onValueChange={(value) => setFormData({...formData, maintenanceTypeId: value})}>
                <SelectTrigger>
                  <SelectValue placeholder="Select maintenance type" />
                </SelectTrigger>
                <SelectContent>
                  {maintenanceTypes.map((type) => (
                    <SelectItem key={type.id} value={type.id}>
                      {type.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="grid grid-cols-3 gap-4">
              <div className="col-span-2 space-y-2">
                <Label htmlFor="edit-taskName">Task Name</Label>
                <Input
                  id="edit-taskName"
                  value={formData.taskName}
                  onChange={(e) => setFormData({...formData, taskName: e.target.value})}
                  placeholder="e.g., Check fluid levels"
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="edit-sequence">Sequence</Label>
                <Input
                  id="edit-sequence"
                  type="number"
                  value={formData.sequence}
                  onChange={(e) => setFormData({...formData, sequence: parseInt(e.target.value) || 1})}
                  placeholder="1"
                />
              </div>
            </div>
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label htmlFor="edit-description">Description</Label>
                <Textarea
                  id="edit-description"
                  value={formData.description}
                  onChange={(e) => setFormData({...formData, description: e.target.value})}
                  placeholder="Describe the task..."
                  rows={2}
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="edit-instructions">Instructions</Label>
                <Textarea
                  id="edit-instructions"
                  value={formData.instructions}
                  onChange={(e) => setFormData({...formData, instructions: e.target.value})}
                  placeholder="Step-by-step instructions..."
                  rows={2}
                />
              </div>
            </div>
            <div className="space-y-2">
              <Label htmlFor="edit-safetyRequirements">Safety Requirements</Label>
              <Textarea
                id="edit-safetyRequirements"
                value={formData.safetyRequirements}
                onChange={(e) => setFormData({...formData, safetyRequirements: e.target.value})}
                placeholder="Safety protocols and PPE requirements..."
                rows={2}
              />
            </div>
            <div className="grid grid-cols-3 gap-4">
              <div className="space-y-2">
                <Label htmlFor="edit-requiredTools">Required Tools</Label>
                <Input
                  id="edit-requiredTools"
                  value={formData.requiredTools}
                  onChange={(e) => setFormData({...formData, requiredTools: e.target.value})}
                  placeholder="e.g., Wrench, Multimeter"
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="edit-requiredParts">Required Parts</Label>
                <Input
                  id="edit-requiredParts"
                  value={formData.requiredParts}
                  onChange={(e) => setFormData({...formData, requiredParts: e.target.value})}
                  placeholder="e.g., Oil filter, Gasket"
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="edit-estimatedHours">Estimated Hours</Label>
                <Input
                  id="edit-estimatedHours"
                  type="number"
                  step="0.5"
                  value={formData.estimatedHours}
                  onChange={(e) => setFormData({...formData, estimatedHours: parseFloat(e.target.value) || 1})}
                  placeholder="1.0"
                />
              </div>
            </div>
            <div className="space-y-4">
              <div className="flex items-center space-x-2">
                <Switch
                  id="edit-isRequired"
                  checked={formData.isRequired}
                  onCheckedChange={(checked) => setFormData({...formData, isRequired: checked})}
                />
                <Label htmlFor="edit-isRequired">Required Task</Label>
              </div>
              <div className="flex items-center space-x-2">
                <Switch
                  id="edit-isActive"
                  checked={formData.isActive}
                  onCheckedChange={(checked) => setFormData({...formData, isActive: checked})}
                />
                <Label htmlFor="edit-isActive">Active</Label>
              </div>
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setIsEditDialogOpen(false)}>
              Cancel
            </Button>
            <Button onClick={handleUpdate}>
              Update Template
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>}
    </div>
  );
}
