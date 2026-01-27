'use client';

import { useState, useEffect } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Badge } from '@/components/ui/badge';
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle, DialogTrigger } from '@/components/ui/dialog';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Plus, Search, Edit, Trash2, Gavel, Copy, Loader2 } from 'lucide-react';
import { toast } from 'sonner';
import { tenderTemplateService, type TenderTemplateDto, type CreateTenderTemplateDto, type UpdateTenderTemplateDto } from '@/services/tenderTemplateService';

export default function TenderTemplatesPage() {
  const [templates, setTemplates] = useState<TenderTemplateDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchTerm, setSearchTerm] = useState('');
  const [typeFilter, setTypeFilter] = useState('all');
  const [isCreateDialogOpen, setIsCreateDialogOpen] = useState(false);
  const [isEditDialogOpen, setIsEditDialogOpen] = useState(false);
  const [selectedTemplate, setSelectedTemplate] = useState<TenderTemplateDto | null>(null);
  const [formData, setFormData] = useState<CreateTenderTemplateDto>({
    templateName: '',
    description: '',
    tenderType: 'RFQ',
    category: 'General',
    priceWeightage: 60,
    qualityWeightage: 20,
    deliveryWeightage: 10,
    experienceWeightage: 10,
    evaluationCriteriaJson: '',
    defaultValidityDays: 30,
    requiredDocuments: '',
    termsAndConditions: '',
    requiresPrequalification: false,
    allowPartialBids: false,
    isActive: true,
  });

  useEffect(() => {
    loadTemplates();
  }, []);

  const loadTemplates = async () => {
    try {
      setLoading(true);
      const data = await tenderTemplateService.getAll();
      setTemplates(data);
    } catch (error) {
      console.error('Error loading templates:', error);
      toast.error('Failed to load tender templates');
    } finally {
      setLoading(false);
    }
  };

  const handleCreate = async () => {
    try {
      await tenderTemplateService.create(formData);
      toast.success('Tender template created successfully');
      setIsCreateDialogOpen(false);
      resetForm();
      loadTemplates();
    } catch (error: any) {
      console.error('Error creating template:', error);
      toast.error(error.message || 'Failed to create tender template');
    }
  };

  const handleUpdate = async () => {
    if (!selectedTemplate) return;
    try {
      await tenderTemplateService.update(selectedTemplate.id, formData as UpdateTenderTemplateDto);
      toast.success('Tender template updated successfully');
      setIsEditDialogOpen(false);
      setSelectedTemplate(null);
      resetForm();
      loadTemplates();
    } catch (error: any) {
      console.error('Error updating template:', error);
      toast.error(error.message || 'Failed to update tender template');
    }
  };

  const handleDelete = async (id: string) => {
    if (!confirm('Are you sure you want to delete this tender template?')) return;
    try {
      await tenderTemplateService.delete(id);
      toast.success('Tender template deleted successfully');
      loadTemplates();
    } catch (error: any) {
      console.error('Error deleting template:', error);
      toast.error(error.message || 'Failed to delete tender template');
    }
  };

  const handleEdit = (template: TenderTemplateDto) => {
    setSelectedTemplate(template);
    setFormData({
      templateName: template.templateName,
      description: template.description || '',
      tenderType: template.tenderType,
      category: template.category,
      priceWeightage: template.priceWeightage,
      qualityWeightage: template.qualityWeightage,
      deliveryWeightage: template.deliveryWeightage,
      experienceWeightage: template.experienceWeightage,
      evaluationCriteriaJson: template.evaluationCriteriaJson || '',
      defaultValidityDays: template.defaultValidityDays || 30,
      requiredDocuments: template.requiredDocuments || '',
      termsAndConditions: template.termsAndConditions || '',
      requiresPrequalification: template.requiresPrequalification,
      allowPartialBids: template.allowPartialBids,
      isActive: template.isActive,
    });
    setIsEditDialogOpen(true);
  };

  const resetForm = () => {
    setFormData({
      templateName: '',
      description: '',
      tenderType: 'RFQ',
      category: 'General',
      priceWeightage: 60,
      qualityWeightage: 20,
      deliveryWeightage: 10,
      experienceWeightage: 10,
      evaluationCriteriaJson: '',
      defaultValidityDays: 30,
      requiredDocuments: '',
      termsAndConditions: '',
      requiresPrequalification: false,
      allowPartialBids: false,
      isActive: true,
    });
  };

  const filteredTemplates = templates.filter(template => {
    const matchesSearch = template.templateName.toLowerCase().includes(searchTerm.toLowerCase()) ||
                         (template.description && template.description.toLowerCase().includes(searchTerm.toLowerCase()));
    const matchesType = typeFilter === 'all' || template.tenderType === typeFilter;
    return matchesSearch && matchesType;
  });

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Tender Templates</h1>
          <p className="text-muted-foreground">
            Manage reusable tender templates for different procurement types
          </p>
        </div>
        <Dialog open={isCreateDialogOpen} onOpenChange={setIsCreateDialogOpen}>
          <DialogTrigger asChild>
            <Button>
              <Plus className="mr-2 h-4 w-4" />
              Add Template
            </Button>
          </DialogTrigger>
          <DialogContent className="sm:max-w-[600px]">
            <DialogHeader>
              <DialogTitle>Add Tender Template</DialogTitle>
              <DialogDescription>
                Create a new reusable template for tender creation.
              </DialogDescription>
            </DialogHeader>
            <div className="grid gap-4 py-4">
              <div className="grid gap-2">
                <Label htmlFor="name">Template Name</Label>
                <Input
                  id="name"
                  placeholder="e.g., Standard RFQ Template"
                  value={formData.templateName}
                  onChange={(e) => setFormData({ ...formData, templateName: e.target.value })}
                />
              </div>
              <div className="grid gap-2">
                <Label htmlFor="type">Tender Type</Label>
                <Select value={formData.tenderType} onValueChange={(value) => setFormData({ ...formData, tenderType: value })}>
                  <SelectTrigger id="type">
                    <SelectValue placeholder="Select type" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="RFQ">Request for Quotation (RFQ)</SelectItem>
                    <SelectItem value="RFP">Request for Proposal (RFP)</SelectItem>
                    <SelectItem value="ITB">Invitation to Bid (ITB)</SelectItem>
                    <SelectItem value="EOI">Expression of Interest (EOI)</SelectItem>
                  </SelectContent>
                </Select>
              </div>
              <div className="grid gap-2">
                <Label htmlFor="category">Category</Label>
                <Input
                  id="category"
                  placeholder="e.g., General"
                  value={formData.category}
                  onChange={(e) => setFormData({ ...formData, category: e.target.value })}
                />
              </div>
              <div className="grid gap-2">
                <Label htmlFor="description">Description</Label>
                <Textarea
                  id="description"
                  placeholder="Template description..."
                  rows={3}
                  value={formData.description}
                  onChange={(e) => setFormData({ ...formData, description: e.target.value })}
                />
              </div>
            </div>
            <div className="flex justify-end gap-2">
              <Button variant="outline" onClick={() => { setIsCreateDialogOpen(false); resetForm(); }}>Cancel</Button>
              <Button onClick={handleCreate}>Create Template</Button>
            </div>
          </DialogContent>
        </Dialog>
      </div>

      {/* Filters */}
      <Card>
        <CardHeader>
          <CardTitle>Filters</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
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
                <SelectValue placeholder="Tender Type" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Types</SelectItem>
                <SelectItem value="RFQ">RFQ</SelectItem>
                <SelectItem value="RFP">RFP</SelectItem>
                <SelectItem value="ITB">ITB</SelectItem>
                <SelectItem value="EOI">EOI</SelectItem>
              </SelectContent>
            </Select>
          </div>
        </CardContent>
      </Card>

      {/* Templates List */}
      <Card>
        <CardHeader>
          <CardTitle>Templates</CardTitle>
          <CardDescription>
            {loading ? 'Loading...' : `${filteredTemplates.length} template${filteredTemplates.length !== 1 ? 's' : ''} found`}
          </CardDescription>
        </CardHeader>
        <CardContent>
          {loading ? (
            <div className="flex justify-center py-8">
              <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
            </div>
          ) : (
            <div className="space-y-4">
              {filteredTemplates.length === 0 ? (
                <div className="text-center py-8 text-muted-foreground">
                  No templates found. {searchTerm || typeFilter !== 'all' ? 'Try adjusting your filters.' : 'Create your first template to get started.'}
                </div>
              ) : (
                filteredTemplates.map((template) => (
                  <div key={template.id} className="border rounded-lg p-4 hover:bg-muted/50 transition-colors">
                    <div className="flex items-start justify-between">
                      <div className="space-y-2 flex-1">
                        <div className="flex items-center space-x-3">
                          <Gavel className="h-5 w-5 text-muted-foreground" />
                          <h3 className="font-semibold">{template.templateName}</h3>
                          <Badge>{template.tenderType}</Badge>
                          <Badge variant="outline">{template.category}</Badge>
                          <Badge className={template.isActive ? 'bg-green-100 text-green-800' : 'bg-gray-100 text-gray-800'}>
                            {template.isActive ? 'Active' : 'Inactive'}
                          </Badge>
                        </div>

                        <div className="grid grid-cols-1 md:grid-cols-2 gap-2 text-sm text-muted-foreground">
                          <div>
                            <span className="font-medium">Used:</span> {template.usageCount} times
                          </div>
                          <div>
                            <span className="font-medium">Created:</span> {new Date(template.createdAt).toLocaleDateString()}
                          </div>
                        </div>

                        {template.description && (
                          <p className="text-sm text-muted-foreground">{template.description}</p>
                        )}
                      </div>

                      <div className="flex items-center space-x-2 ml-4">
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={() => handleEdit(template)}
                        >
                          <Edit className="h-4 w-4" />
                        </Button>
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={() => handleDelete(template.id)}
                        >
                          <Trash2 className="h-4 w-4" />
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
        <DialogContent className="sm:max-w-[600px]">
          <DialogHeader>
            <DialogTitle>Edit Tender Template</DialogTitle>
            <DialogDescription>
              Update the tender template details.
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 py-4">
            <div className="grid gap-2">
              <Label htmlFor="edit-name">Template Name</Label>
              <Input
                id="edit-name"
                value={formData.templateName}
                onChange={(e) => setFormData({ ...formData, templateName: e.target.value })}
              />
            </div>
            <div className="grid gap-2">
              <Label htmlFor="edit-type">Tender Type</Label>
              <Select value={formData.tenderType} onValueChange={(value) => setFormData({ ...formData, tenderType: value })}>
                <SelectTrigger id="edit-type">
                  <SelectValue placeholder="Select type" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="RFQ">Request for Quotation (RFQ)</SelectItem>
                  <SelectItem value="RFP">Request for Proposal (RFP)</SelectItem>
                  <SelectItem value="ITB">Invitation to Bid (ITB)</SelectItem>
                  <SelectItem value="EOI">Expression of Interest (EOI)</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div className="grid gap-2">
              <Label htmlFor="edit-category">Category</Label>
              <Input
                id="edit-category"
                value={formData.category}
                onChange={(e) => setFormData({ ...formData, category: e.target.value })}
              />
            </div>
            <div className="grid gap-2">
              <Label htmlFor="edit-description">Description</Label>
              <Textarea
                id="edit-description"
                rows={3}
                value={formData.description}
                onChange={(e) => setFormData({ ...formData, description: e.target.value })}
              />
            </div>
            <div className="flex items-center space-x-2">
              <input
                type="checkbox"
                id="edit-isActive"
                checked={formData.isActive}
                onChange={(e) => setFormData({ ...formData, isActive: e.target.checked })}
                className="h-4 w-4"
              />
              <Label htmlFor="edit-isActive">Active</Label>
            </div>
          </div>
          <div className="flex justify-end gap-2">
            <Button variant="outline" onClick={() => { setIsEditDialogOpen(false); setSelectedTemplate(null); resetForm(); }}>Cancel</Button>
            <Button onClick={handleUpdate}>Update Template</Button>
          </div>
        </DialogContent>
      </Dialog>
    </div>
  );
}
