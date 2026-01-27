'use client';

import { useState, useEffect } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle, DialogTrigger } from '@/components/ui/dialog';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Switch } from '@/components/ui/switch';
import { Plus, Search, Edit, Trash2, ClipboardCheck, Loader2, GripVertical } from 'lucide-react';
import { toast } from 'sonner';
import { 
  awardVerificationService, 
  AwardVerificationChecklistTemplate, 
  CreateChecklistTemplateDto, 
  CreateChecklistItemDto 
} from '@/services/awardVerificationService';

interface ChecklistItemForm {
  itemText: string;
  description: string;
  displayOrder: number;
  isRequired: boolean;
  category: string;
  isActive: boolean;
}

export default function AwardVerificationChecklistsPage() {
  const [searchTerm, setSearchTerm] = useState('');
  const [includeInactive, setIncludeInactive] = useState(false);
  const [isCreateDialogOpen, setIsCreateDialogOpen] = useState(false);
  const [isEditDialogOpen, setIsEditDialogOpen] = useState(false);
  const [templates, setTemplates] = useState<AwardVerificationChecklistTemplate[]>([]);
  const [loading, setLoading] = useState(true);
  const [selectedTemplate, setSelectedTemplate] = useState<AwardVerificationChecklistTemplate | null>(null);
  
  const [formData, setFormData] = useState<{
    name: string;
    description: string;
    category: string;
    minContractValue: string;
    maxContractValue: string;
    isActive: boolean;
    isDefault: boolean;
    displayOrder: number;
    items: ChecklistItemForm[];
  }>({
    name: '',
    description: '',
    category: '',
    minContractValue: '',
    maxContractValue: '',
    isActive: true,
    isDefault: false,
    displayOrder: 0,
    items: [],
  });

  useEffect(() => {
    loadData();
  }, [includeInactive]);

  const loadData = async () => {
    try {
      setLoading(true);
      const data = await awardVerificationService.getTemplates(includeInactive);
      setTemplates(data);
    } catch (error) {
      toast.error('Failed to load checklist templates');
      console.error(error);
    } finally {
      setLoading(false);
    }
  };

  const handleAddItem = () => {
    setFormData({
      ...formData,
      items: [...formData.items, {
        itemText: '',
        description: '',
        displayOrder: formData.items.length,
        isRequired: true,
        category: '',
        isActive: true,
      }],
    });
  };

  const handleRemoveItem = (index: number) => {
    setFormData({
      ...formData,
      items: formData.items.filter((_, i) => i !== index).map((item, i) => ({ ...item, displayOrder: i })),
    });
  };

  const handleItemChange = (index: number, field: keyof ChecklistItemForm, value: any) => {
    const updated = [...formData.items];
    updated[index] = { ...updated[index], [field]: value };
    setFormData({ ...formData, items: updated });
  };

  const handleCreate = async () => {
    if (!formData.name.trim()) {
      toast.error('Template name is required');
      return;
    }
    if (formData.items.length === 0) {
      toast.error('At least one checklist item is required');
      return;
    }
    if (formData.items.some(item => !item.itemText.trim())) {
      toast.error('All checklist items must have text');
      return;
    }
    try {
      const dto: CreateChecklistTemplateDto = {
        name: formData.name,
        description: formData.description || undefined,
        category: formData.category || undefined,
        minContractValue: formData.minContractValue ? parseFloat(formData.minContractValue) : undefined,
        maxContractValue: formData.maxContractValue ? parseFloat(formData.maxContractValue) : undefined,
        isActive: formData.isActive,
        isDefault: formData.isDefault,
        displayOrder: formData.displayOrder,
        items: formData.items.map((item, i) => ({
          itemText: item.itemText,
          description: item.description || undefined,
          displayOrder: i,
          isRequired: item.isRequired,
          category: item.category || undefined,
          isActive: item.isActive,
        })),
      };
      await awardVerificationService.createTemplate(dto);
      toast.success('Checklist template created successfully');
      setIsCreateDialogOpen(false);
      resetForm();
      loadData();
    } catch (error: any) {
      toast.error(error.message || 'Failed to create checklist template');
    }
  };

  const handleUpdate = async () => {
    if (!selectedTemplate) return;
    if (!formData.name.trim()) {
      toast.error('Template name is required');
      return;
    }
    try {
      await awardVerificationService.updateTemplate(selectedTemplate.id, {
        name: formData.name,
        description: formData.description || undefined,
        category: formData.category || undefined,
        minContractValue: formData.minContractValue ? parseFloat(formData.minContractValue) : undefined,
        maxContractValue: formData.maxContractValue ? parseFloat(formData.maxContractValue) : undefined,
        isActive: formData.isActive,
        isDefault: formData.isDefault,
        displayOrder: formData.displayOrder,
        items: formData.items.map((item, i) => ({
          itemText: item.itemText,
          description: item.description || undefined,
          displayOrder: i,
          isRequired: item.isRequired,
          category: item.category || undefined,
          isActive: item.isActive,
        })),
      });
      toast.success('Checklist template updated successfully');
      setIsEditDialogOpen(false);
      setSelectedTemplate(null);
      resetForm();
      loadData();
    } catch (error: any) {
      toast.error(error.message || 'Failed to update checklist template');
    }
  };

  const handleDelete = async (id: string) => {
    if (!confirm('Are you sure you want to delete this checklist template?')) return;
    try {
      await awardVerificationService.deleteTemplate(id);
      toast.success('Checklist template deleted successfully');
      loadData();
    } catch (error: any) {
      toast.error(error.message || 'Failed to delete checklist template');
    }
  };

  const handleEdit = async (template: AwardVerificationChecklistTemplate) => {
    try {
      const fullTemplate = await awardVerificationService.getTemplateById(template.id);
      setSelectedTemplate(fullTemplate);
      setFormData({
        name: fullTemplate.name,
        description: fullTemplate.description || '',
        category: fullTemplate.category || '',
        minContractValue: fullTemplate.minContractValue?.toString() || '',
        maxContractValue: fullTemplate.maxContractValue?.toString() || '',
        isActive: fullTemplate.isActive,
        isDefault: fullTemplate.isDefault,
        displayOrder: fullTemplate.displayOrder,
        items: fullTemplate.items.map(item => ({
          itemText: item.itemText,
          description: item.description || '',
          displayOrder: item.displayOrder,
          isRequired: item.isRequired,
          category: item.category || '',
          isActive: item.isActive,
        })),
      });
      setIsEditDialogOpen(true);
    } catch (error) {
      toast.error('Failed to load template details');
    }
  };

  const resetForm = () => {
    setFormData({
      name: '',
      description: '',
      category: '',
      minContractValue: '',
      maxContractValue: '',
      isActive: true,
      isDefault: false,
      displayOrder: 0,
      items: [],
    });
  };

  const filteredTemplates = templates.filter(template =>
    template.name.toLowerCase().includes(searchTerm.toLowerCase()) ||
    (template.description?.toLowerCase().includes(searchTerm.toLowerCase()))
  );

  const renderItemsForm = () => (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <Label>Checklist Items ({formData.items.length})</Label>
        <Button type="button" variant="outline" size="sm" onClick={handleAddItem}>
          <Plus className="h-4 w-4 mr-1" /> Add Item
        </Button>
      </div>
      <div className="space-y-3 max-h-[300px] overflow-y-auto">
        {formData.items.map((item, index) => (
          <div key={index} className="border rounded-lg p-3 space-y-2 bg-muted/30">
            <div className="flex items-start gap-2">
              <GripVertical className="h-5 w-5 text-muted-foreground mt-2 cursor-move" />
              <div className="flex-1 space-y-2">
                <Input
                  placeholder="Checklist item text *"
                  value={item.itemText}
                  onChange={(e) => handleItemChange(index, 'itemText', e.target.value)}
                />
                <Input
                  placeholder="Description (optional)"
                  value={item.description}
                  onChange={(e) => handleItemChange(index, 'description', e.target.value)}
                />
                <div className="flex items-center gap-4">
                  <div className="flex items-center gap-2">
                    <Switch
                      checked={item.isRequired}
                      onCheckedChange={(checked) => handleItemChange(index, 'isRequired', checked)}
                    />
                    <Label className="text-sm">Required</Label>
                  </div>
                  <div className="flex items-center gap-2">
                    <Switch
                      checked={item.isActive}
                      onCheckedChange={(checked) => handleItemChange(index, 'isActive', checked)}
                    />
                    <Label className="text-sm">Active</Label>
                  </div>
                  <Input
                    placeholder="Category"
                    value={item.category}
                    onChange={(e) => handleItemChange(index, 'category', e.target.value)}
                    className="w-32"
                  />
                </div>
              </div>
              <Button variant="ghost" size="sm" onClick={() => handleRemoveItem(index)}>
                <Trash2 className="h-4 w-4 text-destructive" />
              </Button>
            </div>
          </div>
        ))}
        {formData.items.length === 0 && (
          <div className="text-center py-4 text-muted-foreground">
            No items added. Click "Add Item" to add checklist items.
          </div>
        )}
      </div>
    </div>
  );

  const renderTemplateForm = () => (
    <div className="grid gap-4 py-4">
      <div className="grid grid-cols-2 gap-4">
        <div className="grid gap-2">
          <Label>Template Name *</Label>
          <Input
            value={formData.name}
            onChange={(e) => setFormData({ ...formData, name: e.target.value })}
            placeholder="e.g., Standard Award Verification"
          />
        </div>
        <div className="grid gap-2">
          <Label>Category</Label>
          <Input
            value={formData.category}
            onChange={(e) => setFormData({ ...formData, category: e.target.value })}
            placeholder="e.g., Construction, IT, Services"
          />
        </div>
      </div>
      <div className="grid gap-2">
        <Label>Description</Label>
        <Textarea
          value={formData.description}
          onChange={(e) => setFormData({ ...formData, description: e.target.value })}
          placeholder="Brief description of this checklist template"
          rows={2}
        />
      </div>
      <div className="grid grid-cols-2 gap-4">
        <div className="grid gap-2">
          <Label>Min Contract Value</Label>
          <Input
            type="number"
            value={formData.minContractValue}
            onChange={(e) => setFormData({ ...formData, minContractValue: e.target.value })}
            placeholder="Optional minimum value"
          />
        </div>
        <div className="grid gap-2">
          <Label>Max Contract Value</Label>
          <Input
            type="number"
            value={formData.maxContractValue}
            onChange={(e) => setFormData({ ...formData, maxContractValue: e.target.value })}
            placeholder="Optional maximum value"
          />
        </div>
      </div>
      <div className="flex items-center gap-6">
        <div className="flex items-center gap-2">
          <Switch
            checked={formData.isActive}
            onCheckedChange={(checked) => setFormData({ ...formData, isActive: checked })}
          />
          <Label>Active</Label>
        </div>
        <div className="flex items-center gap-2">
          <Switch
            checked={formData.isDefault}
            onCheckedChange={(checked) => setFormData({ ...formData, isDefault: checked })}
          />
          <Label>Default Template</Label>
        </div>
        <div className="flex items-center gap-2">
          <Label>Display Order</Label>
          <Input
            type="number"
            value={formData.displayOrder}
            onChange={(e) => setFormData({ ...formData, displayOrder: parseInt(e.target.value) || 0 })}
            className="w-20"
          />
        </div>
      </div>
      {renderItemsForm()}
    </div>
  );

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Award Verification Checklists</h1>
          <p className="text-muted-foreground">
            Manage verification checklist templates for tender award background checks
          </p>
        </div>
        <Dialog open={isCreateDialogOpen} onOpenChange={setIsCreateDialogOpen}>
          <DialogTrigger asChild>
            <Button><Plus className="mr-2 h-4 w-4" />Add Template</Button>
          </DialogTrigger>
          <DialogContent className="sm:max-w-[800px] max-h-[90vh] overflow-y-auto">
            <DialogHeader>
              <DialogTitle>Create Checklist Template</DialogTitle>
              <DialogDescription>Create a new verification checklist template with items.</DialogDescription>
            </DialogHeader>
            {renderTemplateForm()}
            <div className="flex justify-end gap-2">
              <Button variant="outline" onClick={() => { setIsCreateDialogOpen(false); resetForm(); }}>Cancel</Button>
              <Button onClick={handleCreate}>Create Template</Button>
            </div>
          </DialogContent>
        </Dialog>
      </div>

      {/* Filters */}
      <Card>
        <CardHeader><CardTitle>Filters</CardTitle></CardHeader>
        <CardContent>
          <div className="flex items-center gap-4">
            <div className="relative flex-1">
              <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
              <Input
                placeholder="Search templates..."
                className="pl-8"
                value={searchTerm}
                onChange={(e) => setSearchTerm(e.target.value)}
              />
            </div>
            <div className="flex items-center gap-2">
              <Switch checked={includeInactive} onCheckedChange={setIncludeInactive} />
              <Label>Include Inactive</Label>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Templates List */}
      <Card>
        <CardHeader>
          <CardTitle>Checklist Templates</CardTitle>
          <CardDescription>
            {loading ? 'Loading...' : `${filteredTemplates.length} template${filteredTemplates.length !== 1 ? 's' : ''} found`}
          </CardDescription>
        </CardHeader>
        <CardContent>
          {loading ? (
            <div className="flex justify-center py-8"><Loader2 className="h-8 w-8 animate-spin text-muted-foreground" /></div>
          ) : filteredTemplates.length === 0 ? (
            <div className="text-center py-8 text-muted-foreground">No templates found.</div>
          ) : (
            <div className="space-y-4">
              {filteredTemplates.map((template) => (
                <div key={template.id} className="border rounded-lg p-4 hover:bg-muted/50 transition-colors">
                  <div className="flex items-start justify-between">
                    <div className="space-y-2 flex-1">
                      <div className="flex items-center space-x-3">
                        <ClipboardCheck className="h-5 w-5 text-muted-foreground" />
                        <h3 className="font-semibold">{template.name}</h3>
                        {template.category && <Badge variant="outline">{template.category}</Badge>}
                        {template.isDefault && <Badge className="bg-blue-100 text-blue-800">Default</Badge>}
                        <Badge className={template.isActive ? 'bg-green-100 text-green-800' : 'bg-gray-100 text-gray-800'}>
                          {template.isActive ? 'Active' : 'Inactive'}
                        </Badge>
                      </div>
                      <div className="text-sm text-muted-foreground">
                        <span className="font-medium">{template.items?.length || 0} items</span>
                        {template.minContractValue && <span className="ml-4">Min: ${template.minContractValue.toLocaleString()}</span>}
                        {template.maxContractValue && <span className="ml-2">Max: ${template.maxContractValue.toLocaleString()}</span>}
                      </div>
                      {template.description && <p className="text-sm text-muted-foreground">{template.description}</p>}
                    </div>
                    <div className="flex items-center space-x-2 ml-4">
                      <Button variant="ghost" size="sm" onClick={() => handleEdit(template)}><Edit className="h-4 w-4" /></Button>
                      <Button variant="ghost" size="sm" onClick={() => handleDelete(template.id)}><Trash2 className="h-4 w-4" /></Button>
                    </div>
                  </div>
                </div>
              ))}
            </div>
          )}
        </CardContent>
      </Card>

      {/* Edit Dialog */}
      <Dialog open={isEditDialogOpen} onOpenChange={setIsEditDialogOpen}>
        <DialogContent className="sm:max-w-[800px] max-h-[90vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Edit Checklist Template</DialogTitle>
            <DialogDescription>Update the checklist template details and items.</DialogDescription>
          </DialogHeader>
          {renderTemplateForm()}
          <div className="flex justify-end gap-2">
            <Button variant="outline" onClick={() => { setIsEditDialogOpen(false); setSelectedTemplate(null); resetForm(); }}>Cancel</Button>
            <Button onClick={handleUpdate}>Update Template</Button>
          </div>
        </DialogContent>
      </Dialog>
    </div>
  );
}

