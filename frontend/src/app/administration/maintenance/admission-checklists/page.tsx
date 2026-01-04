'use client';

import React, { useState, useEffect } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Badge } from '@/components/ui/badge';
import { 
  Dialog, 
  DialogContent, 
  DialogDescription, 
  DialogFooter, 
  DialogHeader, 
  DialogTitle,
  DialogTrigger 
} from '@/components/ui/dialog';
import { 
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Textarea } from '@/components/ui/textarea';
import { Switch } from '@/components/ui/switch';
import { 
  Plus,
  Search,
  Edit,
  Trash2,
  Eye,
  Copy,
  ClipboardCheck,
  CheckCircle,
  XCircle,
  GripVertical,
  ChevronDown,
  ChevronUp,
  AlertTriangle
} from 'lucide-react';
import { cn } from '@/lib/utils';
import { 
  assetConditionService,
  AssetConditionChecklistTemplateDto,
  AssetConditionChecklistItemDto,
  CreateAssetConditionTemplateDto,
  CreateAssetConditionItemDto
} from '@/services/assetConditionService';
import { compatibleApiService as apiService } from '@/services/compatibleApiService';

interface AssetCategory {
  id: string;
  name: string;
  code?: string;
  description?: string;
  isActive?: boolean;
}

const itemTypeOptions = [
  { value: 'Boolean', label: 'Yes/No (Present/Absent)' },
  { value: 'Text', label: 'Text Description' },
  { value: 'Numeric', label: 'Numeric Value' },
  { value: 'Choice', label: 'Multiple Choice' }
];

const categoryOptions = [
  'Safety Equipment',
  'Exterior',
  'Interior',
  'Engine/Mechanical',
  'Electrical',
  'Fluids',
  'Documents',
  'Accessories',
  'General'
];

export default function AdmissionChecklistsPage() {
  // State for templates
  const [templates, setTemplates] = useState<AssetConditionChecklistTemplateDto[]>([]);
  const [assetCategories, setAssetCategories] = useState<AssetCategory[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [searchTerm, setSearchTerm] = useState('');
  const [selectedAssetCategory, setSelectedAssetCategory] = useState<string>('all');
  const [showInactive, setShowInactive] = useState(false);
  
  // Dialog states
  const [isCreateDialogOpen, setIsCreateDialogOpen] = useState(false);
  const [isEditDialogOpen, setIsEditDialogOpen] = useState(false);
  const [isViewDialogOpen, setIsViewDialogOpen] = useState(false);
  const [isDeleteDialogOpen, setIsDeleteDialogOpen] = useState(false);
  const [selectedTemplate, setSelectedTemplate] = useState<AssetConditionChecklistTemplateDto | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  // Form state
  const [formData, setFormData] = useState<CreateAssetConditionTemplateDto>({
    name: '',
    description: '',
    category: 'General',
    assetCategoryId: '',
    isActive: true,
    isDefault: false,
    sortOrder: 0,
    checklistItems: []
  });

  // New item form state
  const [newItem, setNewItem] = useState<CreateAssetConditionItemDto>({
    itemName: '',
    description: '',
    category: 'General',
    itemType: 'Boolean',
    isRequired: true,
    sortOrder: 0,
    choiceOptions: [],
    unit: '',
    minValue: undefined,
    maxValue: undefined,
    defaultValue: '',
    helpText: '',
    requiresPhoto: false
  });
  const [choiceOptionsInput, setChoiceOptionsInput] = useState('');

  useEffect(() => {
    fetchTemplates();
    fetchAssetCategories();
  }, [showInactive]);

  const fetchTemplates = async () => {
    try {
      setLoading(true);
      const data = await assetConditionService.getAllTemplates(showInactive);
      setTemplates(data);
      setError(null);
    } catch (err) {
      console.error('Error fetching templates:', err);
      setError('Failed to load templates');
    } finally {
      setLoading(false);
    }
  };

  const fetchAssetCategories = async () => {
    try {
      const response = await apiService.get('/maintenance/asset-categories');
      setAssetCategories(response || []);
    } catch (err) {
      console.error('Error fetching asset categories:', err);
      setAssetCategories([]);
    }
  };

  const filteredTemplates = templates.filter(template => {
    const matchesSearch = template.name.toLowerCase().includes(searchTerm.toLowerCase()) ||
                         template.description?.toLowerCase().includes(searchTerm.toLowerCase());
    const matchesAssetCategory = selectedAssetCategory === 'all' || template.assetCategoryId === selectedAssetCategory;
    return matchesSearch && matchesAssetCategory;
  });

  const resetFormData = () => {
    setFormData({
      name: '',
      description: '',
      category: 'General',
      assetCategoryId: '',
      isActive: true,
      isDefault: false,
      sortOrder: 0,
      checklistItems: []
    });
    resetNewItem();
  };

  const resetNewItem = () => {
    setNewItem({
      itemName: '',
      description: '',
      category: 'General',
      itemType: 'Boolean',
      isRequired: true,
      sortOrder: 0,
      choiceOptions: [],
      unit: '',
      minValue: undefined,
      maxValue: undefined,
      defaultValue: '',
      helpText: '',
      requiresPhoto: false
    });
    setChoiceOptionsInput('');
  };

  const handleAddItem = () => {
    if (!newItem.itemName.trim()) return;

    const itemToAdd: CreateAssetConditionItemDto = {
      ...newItem,
      sortOrder: formData.checklistItems.length + 1,
      choiceOptions: newItem.itemType === 'Choice'
        ? choiceOptionsInput.split(',').map(o => o.trim()).filter(o => o)
        : undefined
    };

    setFormData(prev => ({
      ...prev,
      checklistItems: [...prev.checklistItems, itemToAdd]
    }));
    resetNewItem();
  };

  const handleRemoveItem = (index: number) => {
    setFormData(prev => ({
      ...prev,
      checklistItems: prev.checklistItems.filter((_, i) => i !== index)
    }));
  };

  const handleMoveItem = (index: number, direction: 'up' | 'down') => {
    const newItems = [...formData.checklistItems];
    const newIndex = direction === 'up' ? index - 1 : index + 1;
    if (newIndex < 0 || newIndex >= newItems.length) return;
    [newItems[index], newItems[newIndex]] = [newItems[newIndex], newItems[index]];
    newItems.forEach((item, i) => item.sortOrder = i + 1);
    setFormData(prev => ({ ...prev, checklistItems: newItems }));
  };

  const handleCreate = async () => {
    try {
      setIsSubmitting(true);
      await assetConditionService.createTemplate(formData);
      setIsCreateDialogOpen(false);
      resetFormData();
      await fetchTemplates();
    } catch (err) {
      console.error('Error creating template:', err);
      setError('Failed to create template');
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleEdit = (template: AssetConditionChecklistTemplateDto) => {
    setSelectedTemplate(template);
    setFormData({
      name: template.name,
      description: template.description || '',
      category: template.category,
      assetCategoryId: template.assetCategoryId,
      isActive: template.isActive,
      isDefault: template.isDefault,
      sortOrder: template.sortOrder,
      checklistItems: template.checklistItems.map(item => ({
        itemName: item.itemName,
        description: item.description || '',
        category: item.category,
        itemType: item.itemType,
        isRequired: item.isRequired,
        sortOrder: item.sortOrder,
        choiceOptions: item.choiceOptions,
        unit: item.unit || '',
        minValue: item.minValue,
        maxValue: item.maxValue,
        defaultValue: item.defaultValue || '',
        helpText: item.helpText || '',
        requiresPhoto: item.requiresPhoto
      }))
    });
    setIsEditDialogOpen(true);
  };

  const handleUpdate = async () => {
    if (!selectedTemplate) return;
    try {
      setIsSubmitting(true);
      await assetConditionService.updateTemplate(selectedTemplate.id, {
        ...formData,
        versionNotes: 'Updated via admin UI'
      });
      setIsEditDialogOpen(false);
      resetFormData();
      setSelectedTemplate(null);
      await fetchTemplates();
    } catch (err) {
      console.error('Error updating template:', err);
      setError('Failed to update template');
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleDelete = async () => {
    if (!selectedTemplate) return;
    try {
      setIsSubmitting(true);
      await assetConditionService.deleteTemplate(selectedTemplate.id);
      setIsDeleteDialogOpen(false);
      setSelectedTemplate(null);
      await fetchTemplates();
    } catch (err) {
      console.error('Error deleting template:', err);
      setError('Failed to delete template');
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleDuplicate = async (template: AssetConditionChecklistTemplateDto) => {
    try {
      setIsSubmitting(true);
      const duplicateData: CreateAssetConditionTemplateDto = {
        name: `${template.name} (Copy)`,
        description: template.description,
        category: template.category,
        assetCategoryId: template.assetCategoryId,
        isActive: false,
        isDefault: false,
        sortOrder: template.sortOrder,
        checklistItems: template.checklistItems.map(item => ({
          itemName: item.itemName,
          description: item.description,
          category: item.category,
          itemType: item.itemType,
          isRequired: item.isRequired,
          sortOrder: item.sortOrder,
          choiceOptions: item.choiceOptions,
          unit: item.unit,
          minValue: item.minValue,
          maxValue: item.maxValue,
          defaultValue: item.defaultValue,
          helpText: item.helpText,
          requiresPhoto: item.requiresPhoto
        }))
      };
      await assetConditionService.createTemplate(duplicateData);
      await fetchTemplates();
    } catch (err) {
      console.error('Error duplicating template:', err);
      setError('Failed to duplicate template');
    } finally {
      setIsSubmitting(false);
    }
  };

  const getItemTypeLabel = (type: string) => {
    const option = itemTypeOptions.find(o => o.value === type);
    return option?.label || type;
  };

  const renderChecklistItemForm = () => (
    <div className="border rounded-lg p-4 space-y-4 bg-muted/30">
      <h4 className="font-medium">Add Checklist Item</h4>
      <div className="grid grid-cols-2 gap-4">
        <div className="space-y-2">
          <Label htmlFor="itemName">Item Name *</Label>
          <Input
            id="itemName"
            value={newItem.itemName}
            onChange={(e) => setNewItem(prev => ({ ...prev, itemName: e.target.value }))}
            placeholder="e.g., Fire Extinguisher"
          />
        </div>
        <div className="space-y-2">
          <Label htmlFor="itemCategory">Category</Label>
          <Select value={newItem.category} onValueChange={(value) => setNewItem(prev => ({ ...prev, category: value }))}>
            <SelectTrigger>
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {categoryOptions.map(cat => (
                <SelectItem key={cat} value={cat}>{cat}</SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
      </div>
      <div className="grid grid-cols-2 gap-4">
        <div className="space-y-2">
          <Label htmlFor="itemType">Response Type</Label>
          <Select value={newItem.itemType} onValueChange={(value) => setNewItem(prev => ({ ...prev, itemType: value }))}>
            <SelectTrigger>
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {itemTypeOptions.map(opt => (
                <SelectItem key={opt.value} value={opt.value}>{opt.label}</SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
        <div className="space-y-2">
          <Label htmlFor="helpText">Help Text</Label>
          <Input
            id="helpText"
            value={newItem.helpText}
            onChange={(e) => setNewItem(prev => ({ ...prev, helpText: e.target.value }))}
            placeholder="Instructions for inspector"
          />
        </div>
      </div>

      {newItem.itemType === 'Numeric' && (
        <div className="grid grid-cols-3 gap-4">
          <div className="space-y-2">
            <Label htmlFor="unit">Unit</Label>
            <Input
              id="unit"
              value={newItem.unit}
              onChange={(e) => setNewItem(prev => ({ ...prev, unit: e.target.value }))}
              placeholder="e.g., %, km, liters"
            />
          </div>
          <div className="space-y-2">
            <Label htmlFor="minValue">Min Value</Label>
            <Input
              id="minValue"
              type="number"
              value={newItem.minValue || ''}
              onChange={(e) => setNewItem(prev => ({ ...prev, minValue: e.target.value ? parseFloat(e.target.value) : undefined }))}
            />
          </div>
          <div className="space-y-2">
            <Label htmlFor="maxValue">Max Value</Label>
            <Input
              id="maxValue"
              type="number"
              value={newItem.maxValue || ''}
              onChange={(e) => setNewItem(prev => ({ ...prev, maxValue: e.target.value ? parseFloat(e.target.value) : undefined }))}
            />
          </div>
        </div>
      )}

      {newItem.itemType === 'Choice' && (
        <div className="space-y-2">
          <Label htmlFor="choiceOptions">Choice Options (comma-separated)</Label>
          <Input
            id="choiceOptions"
            value={choiceOptionsInput}
            onChange={(e) => setChoiceOptionsInput(e.target.value)}
            placeholder="e.g., Good, Fair, Poor, Damaged"
          />
        </div>
      )}

      <div className="flex items-center gap-6">
        <div className="flex items-center space-x-2">
          <Switch
            id="isRequired"
            checked={newItem.isRequired}
            onCheckedChange={(checked) => setNewItem(prev => ({ ...prev, isRequired: checked }))}
          />
          <Label htmlFor="isRequired">Required</Label>
        </div>
        <div className="flex items-center space-x-2">
          <Switch
            id="requiresPhoto"
            checked={newItem.requiresPhoto}
            onCheckedChange={(checked) => setNewItem(prev => ({ ...prev, requiresPhoto: checked }))}
          />
          <Label htmlFor="requiresPhoto">Requires Photo</Label>
        </div>
      </div>

      <Button type="button" onClick={handleAddItem} disabled={!newItem.itemName.trim()}>
        <Plus className="mr-2 h-4 w-4" />
        Add Item
      </Button>
    </div>
  );

  const renderChecklistItems = (items: CreateAssetConditionItemDto[], editable = true) => (
    <div className="space-y-2">
      {items.length === 0 ? (
        <p className="text-sm text-muted-foreground text-center py-4">No checklist items added yet</p>
      ) : (
        items.map((item, index) => (
          <div key={index} className="flex items-center gap-2 p-3 border rounded-lg bg-background">
            {editable && (
              <div className="flex flex-col gap-1">
                <Button variant="ghost" size="icon" className="h-5 w-5" onClick={() => handleMoveItem(index, 'up')} disabled={index === 0}>
                  <ChevronUp className="h-3 w-3" />
                </Button>
                <Button variant="ghost" size="icon" className="h-5 w-5" onClick={() => handleMoveItem(index, 'down')} disabled={index === items.length - 1}>
                  <ChevronDown className="h-3 w-3" />
                </Button>
              </div>
            )}
            <div className="flex-1">
              <div className="flex items-center gap-2">
                <span className="font-medium">{item.itemName}</span>
                {item.isRequired && <Badge variant="destructive" className="text-xs">Required</Badge>}
                {item.requiresPhoto && <Badge variant="outline" className="text-xs">📷 Photo</Badge>}
              </div>
              <div className="flex items-center gap-2 text-sm text-muted-foreground">
                <span>{item.category}</span>
                <span>•</span>
                <span>{getItemTypeLabel(item.itemType)}</span>
                {item.unit && <span>• {item.unit}</span>}
              </div>
            </div>
            {editable && (
              <Button variant="ghost" size="icon" className="text-red-600" onClick={() => handleRemoveItem(index)}>
                <Trash2 className="h-4 w-4" />
              </Button>
            )}
          </div>
        ))
      )}
    </div>
  );

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Admission Checklist Templates</h1>
          <p className="text-muted-foreground">
            Configure condition checklists for asset admission and discharge inspections
          </p>
        </div>
        <Dialog open={isCreateDialogOpen} onOpenChange={setIsCreateDialogOpen}>
          <DialogTrigger asChild>
            <Button onClick={() => resetFormData()}>
              <Plus className="mr-2 h-4 w-4" />
              Add Template
            </Button>
          </DialogTrigger>
          <DialogContent className="sm:max-w-[800px] max-h-[90vh] overflow-y-auto">
            <DialogHeader>
              <DialogTitle>Create Checklist Template</DialogTitle>
              <DialogDescription>
                Define a new condition checklist for asset inspections
              </DialogDescription>
            </DialogHeader>
            <div className="grid gap-4 py-4">
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="name">Template Name *</Label>
                  <Input
                    id="name"
                    value={formData.name}
                    onChange={(e) => setFormData(prev => ({ ...prev, name: e.target.value }))}
                    placeholder="e.g., Vehicle Admission Checklist"
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="assetCategory">Asset Category *</Label>
                  <Select value={formData.assetCategoryId} onValueChange={(value) => setFormData(prev => ({ ...prev, assetCategoryId: value }))}>
                    <SelectTrigger>
                      <SelectValue placeholder="Select asset category" />
                    </SelectTrigger>
                    <SelectContent>
                      {assetCategories.map(cat => (
                        <SelectItem key={cat.id} value={cat.id}>{cat.name}</SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
              </div>
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="category">Category</Label>
                  <Select value={formData.category} onValueChange={(value) => setFormData(prev => ({ ...prev, category: value }))}>
                    <SelectTrigger>
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {categoryOptions.map(cat => (
                        <SelectItem key={cat} value={cat}>{cat}</SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-2">
                  <Label htmlFor="sortOrder">Sort Order</Label>
                  <Input
                    id="sortOrder"
                    type="number"
                    value={formData.sortOrder}
                    onChange={(e) => setFormData(prev => ({ ...prev, sortOrder: parseInt(e.target.value) || 0 }))}
                  />
                </div>
              </div>
              <div className="space-y-2">
                <Label htmlFor="description">Description</Label>
                <Textarea
                  id="description"
                  value={formData.description}
                  onChange={(e) => setFormData(prev => ({ ...prev, description: e.target.value }))}
                  placeholder="Describe the purpose of this checklist"
                  rows={2}
                />
              </div>
              <div className="flex items-center gap-6">
                <div className="flex items-center space-x-2">
                  <Switch
                    id="isActive"
                    checked={formData.isActive}
                    onCheckedChange={(checked) => setFormData(prev => ({ ...prev, isActive: checked }))}
                  />
                  <Label htmlFor="isActive">Active</Label>
                </div>
                <div className="flex items-center space-x-2">
                  <Switch
                    id="isDefault"
                    checked={formData.isDefault}
                    onCheckedChange={(checked) => setFormData(prev => ({ ...prev, isDefault: checked }))}
                  />
                  <Label htmlFor="isDefault">Default for Asset Type</Label>
                </div>
              </div>

              <div className="space-y-4">
                <h3 className="font-semibold">Checklist Items ({formData.checklistItems.length})</h3>
                {renderChecklistItems(formData.checklistItems)}
                {renderChecklistItemForm()}
              </div>
            </div>
            <DialogFooter>
              <Button variant="outline" onClick={() => setIsCreateDialogOpen(false)}>Cancel</Button>
              <Button onClick={handleCreate} disabled={isSubmitting || !formData.name || !formData.assetCategoryId}>
                {isSubmitting ? 'Creating...' : 'Create Template'}
              </Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>
      </div>

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

      {/* Filters */}
      <Card>
        <CardContent className="pt-6">
          <div className="flex items-center gap-4">
            <div className="relative flex-1">
              <Search className="absolute left-3 top-1/2 transform -translate-y-1/2 text-muted-foreground h-4 w-4" />
              <Input
                placeholder="Search templates..."
                value={searchTerm}
                onChange={(e) => setSearchTerm(e.target.value)}
                className="pl-9"
              />
            </div>
            <Select value={selectedAssetCategory} onValueChange={setSelectedAssetCategory}>
              <SelectTrigger className="w-[200px]">
                <SelectValue placeholder="Filter by asset category" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Asset Categories</SelectItem>
                {assetCategories.map(cat => (
                  <SelectItem key={cat.id} value={cat.id}>{cat.name}</SelectItem>
                ))}
              </SelectContent>
            </Select>
            <div className="flex items-center space-x-2">
              <Switch
                id="showInactive"
                checked={showInactive}
                onCheckedChange={setShowInactive}
              />
              <Label htmlFor="showInactive">Show Inactive</Label>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Templates List */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <ClipboardCheck className="h-5 w-5" />
            Templates ({filteredTemplates.length})
          </CardTitle>
        </CardHeader>
        <CardContent>
          {loading ? (
            <div className="text-center py-8">Loading templates...</div>
          ) : filteredTemplates.length === 0 ? (
            <div className="text-center py-8 text-muted-foreground">
              No templates found. Create your first template to get started.
            </div>
          ) : (
            <div className="space-y-4">
              {filteredTemplates.map((template) => (
                <div key={template.id} className="border rounded-lg p-4 hover:bg-muted/50 transition-colors">
                  <div className="flex items-start justify-between">
                    <div className="space-y-2 flex-1">
                      <div className="flex items-center space-x-3">
                        <ClipboardCheck className="h-5 w-5 text-muted-foreground" />
                        <h3 className="font-semibold">{template.name}</h3>
                        <Badge variant="outline">{template.assetCategoryName}</Badge>
                        <Badge>{template.category}</Badge>
                        {template.isDefault && <Badge variant="secondary">Default</Badge>}
                        <Badge className={template.isActive ? 'bg-green-100 text-green-800' : 'bg-gray-100 text-gray-800'}>
                          {template.isActive ? 'Active' : 'Inactive'}
                        </Badge>
                      </div>
                      <p className="text-sm text-muted-foreground">{template.description || 'No description'}</p>
                      <div className="flex items-center gap-4 text-sm text-muted-foreground">
                        <span>{template.itemCount} items</span>
                        <span>•</span>
                        <span>Version {template.version}</span>
                        <span>•</span>
                        <span>Created {new Date(template.createdAt).toLocaleDateString()}</span>
                      </div>
                    </div>
                    <div className="flex items-center space-x-2">
                      <Button size="sm" variant="outline" onClick={() => { setSelectedTemplate(template); setIsViewDialogOpen(true); }}>
                        <Eye className="h-4 w-4" />
                      </Button>
                      <Button size="sm" variant="outline" onClick={() => handleEdit(template)}>
                        <Edit className="h-4 w-4" />
                      </Button>
                      <Button size="sm" variant="outline" onClick={() => handleDuplicate(template)}>
                        <Copy className="h-4 w-4" />
                      </Button>
                      <Button
                        size="sm"
                        variant="outline"
                        onClick={() => { setSelectedTemplate(template); setIsDeleteDialogOpen(true); }}
                        className="text-red-600 hover:text-red-700"
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

      {/* View Template Dialog */}
      <Dialog open={isViewDialogOpen} onOpenChange={setIsViewDialogOpen}>
        <DialogContent className="sm:max-w-[800px] max-h-[90vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle>View Template: {selectedTemplate?.name}</DialogTitle>
            <DialogDescription>Template details and checklist items</DialogDescription>
          </DialogHeader>
          {selectedTemplate && (
            <div className="space-y-4">
              <div className="grid grid-cols-2 gap-4">
                <div>
                  <Label>Asset Category</Label>
                  <p className="text-sm text-muted-foreground">{selectedTemplate.assetCategoryName}</p>
                </div>
                <div>
                  <Label>Category</Label>
                  <p className="text-sm text-muted-foreground">{selectedTemplate.category}</p>
                </div>
                <div>
                  <Label>Status</Label>
                  <div className="flex gap-2 mt-1">
                    <Badge className={selectedTemplate.isActive ? 'bg-green-100 text-green-800' : 'bg-gray-100 text-gray-800'}>
                      {selectedTemplate.isActive ? 'Active' : 'Inactive'}
                    </Badge>
                    {selectedTemplate.isDefault && <Badge variant="secondary">Default</Badge>}
                  </div>
                </div>
                <div>
                  <Label>Version</Label>
                  <p className="text-sm text-muted-foreground">{selectedTemplate.version}</p>
                </div>
              </div>
              {selectedTemplate.description && (
                <div>
                  <Label>Description</Label>
                  <p className="text-sm text-muted-foreground">{selectedTemplate.description}</p>
                </div>
              )}
              <div>
                <Label>Checklist Items ({selectedTemplate.checklistItems.length})</Label>
                <div className="mt-2 space-y-2">
                  {selectedTemplate.checklistItems.map((item, index) => (
                    <div key={item.id} className="flex items-center gap-3 p-3 border rounded-lg">
                      <span className="text-sm font-medium w-6">{index + 1}.</span>
                      <div className="flex-1">
                        <div className="flex items-center gap-2">
                          <span className="font-medium">{item.itemName}</span>
                          {item.isRequired && <Badge variant="destructive" className="text-xs">Required</Badge>}
                          {item.requiresPhoto && <Badge variant="outline" className="text-xs">📷</Badge>}
                        </div>
                        <div className="text-sm text-muted-foreground">
                          {item.category} • {getItemTypeLabel(item.itemType)}
                          {item.unit && ` • ${item.unit}`}
                          {item.choiceOptions && item.choiceOptions.length > 0 && ` • Options: ${item.choiceOptions.join(', ')}`}
                        </div>
                        {item.helpText && <p className="text-xs text-muted-foreground mt-1">{item.helpText}</p>}
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            </div>
          )}
          <DialogFooter>
            <Button variant="outline" onClick={() => setIsViewDialogOpen(false)}>Close</Button>
            <Button onClick={() => { setIsViewDialogOpen(false); if (selectedTemplate) handleEdit(selectedTemplate); }}>
              <Edit className="mr-2 h-4 w-4" />
              Edit Template
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Edit Template Dialog */}
      <Dialog open={isEditDialogOpen} onOpenChange={setIsEditDialogOpen}>
        <DialogContent className="sm:max-w-[800px] max-h-[90vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Edit Template</DialogTitle>
            <DialogDescription>Update the checklist template</DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 py-4">
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label htmlFor="edit-name">Template Name *</Label>
                <Input
                  id="edit-name"
                  value={formData.name}
                  onChange={(e) => setFormData(prev => ({ ...prev, name: e.target.value }))}
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="edit-assetCategory">Asset Category *</Label>
                <Select value={formData.assetCategoryId} onValueChange={(value) => setFormData(prev => ({ ...prev, assetCategoryId: value }))}>
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {assetCategories.map(cat => (
                      <SelectItem key={cat.id} value={cat.id}>{cat.name}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            </div>
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label htmlFor="edit-category">Category</Label>
                <Select value={formData.category} onValueChange={(value) => setFormData(prev => ({ ...prev, category: value }))}>
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {categoryOptions.map(cat => (
                      <SelectItem key={cat} value={cat}>{cat}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-2">
                <Label htmlFor="edit-sortOrder">Sort Order</Label>
                <Input
                  id="edit-sortOrder"
                  type="number"
                  value={formData.sortOrder}
                  onChange={(e) => setFormData(prev => ({ ...prev, sortOrder: parseInt(e.target.value) || 0 }))}
                />
              </div>
            </div>
            <div className="space-y-2">
              <Label htmlFor="edit-description">Description</Label>
              <Textarea
                id="edit-description"
                value={formData.description}
                onChange={(e) => setFormData(prev => ({ ...prev, description: e.target.value }))}
                rows={2}
              />
            </div>
            <div className="flex items-center gap-6">
              <div className="flex items-center space-x-2">
                <Switch
                  id="edit-isActive"
                  checked={formData.isActive}
                  onCheckedChange={(checked) => setFormData(prev => ({ ...prev, isActive: checked }))}
                />
                <Label htmlFor="edit-isActive">Active</Label>
              </div>
              <div className="flex items-center space-x-2">
                <Switch
                  id="edit-isDefault"
                  checked={formData.isDefault}
                  onCheckedChange={(checked) => setFormData(prev => ({ ...prev, isDefault: checked }))}
                />
                <Label htmlFor="edit-isDefault">Default for Asset Type</Label>
              </div>
            </div>

            <div className="space-y-4">
              <h3 className="font-semibold">Checklist Items ({formData.checklistItems.length})</h3>
              {renderChecklistItems(formData.checklistItems)}
              {renderChecklistItemForm()}
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => { setIsEditDialogOpen(false); resetFormData(); }}>Cancel</Button>
            <Button onClick={handleUpdate} disabled={isSubmitting || !formData.name || !formData.assetCategoryId}>
              {isSubmitting ? 'Saving...' : 'Save Changes'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Delete Confirmation Dialog */}
      <Dialog open={isDeleteDialogOpen} onOpenChange={setIsDeleteDialogOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Delete Template</DialogTitle>
            <DialogDescription>
              Are you sure you want to delete "{selectedTemplate?.name}"? This action cannot be undone.
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button variant="outline" onClick={() => setIsDeleteDialogOpen(false)}>Cancel</Button>
            <Button variant="destructive" onClick={handleDelete} disabled={isSubmitting}>
              {isSubmitting ? 'Deleting...' : 'Delete'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
