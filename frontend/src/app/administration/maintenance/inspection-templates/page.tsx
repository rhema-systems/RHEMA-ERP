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
  MoreHorizontal,
  FileText,
  CheckSquare,
  ClipboardList,
  Users,
  Clock,
  AlertCircle,
  AlertTriangle,
  Eye,
  Copy
} from 'lucide-react';
import { cn } from '@/lib/utils';
import { 
  InspectionTemplate,
  CreateInspectionTemplateDto,
  UpdateInspectionTemplateDto,
  inspectionTemplateService
} from '@/services/inspectionTemplateService';


export default function InspectionTemplatesPage() {
  const [searchTerm, setSearchTerm] = useState('');
  const [statusFilter, setStatusFilter] = useState('all');
  const [categoryFilter, setCategoryFilter] = useState('all');
  const [frequencyFilter, setFrequencyFilter] = useState('all');
  const [isCreateDialogOpen, setIsCreateDialogOpen] = useState(false);
  const [isEditDialogOpen, setIsEditDialogOpen] = useState(false);
  const [isViewDialogOpen, setIsViewDialogOpen] = useState(false);
  const [selectedTemplate, setSelectedTemplate] = useState<InspectionTemplate | null>(null);
  const [templatesData, setTemplatesData] = useState<InspectionTemplate[]>([]);
  const [filteredData, setFilteredData] = useState<InspectionTemplate[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  
  // Form state
  const [formData, setFormData] = useState<CreateInspectionTemplateDto>({
    name: '',
    code: '',
    description: '',
    category: 'Safety',
    frequency: 'Monthly',
    estimatedDuration: 60,
    requiresSignature: false,
    allowPhotos: false,
    version: '1.0',
    assetTypes: [],
    inspectorRoles: [],
    priority: 'Medium',
    checklistItems: []
  });

  // Fetch templates from API
  const fetchTemplates = async () => {
    try {
      setLoading(true);
      setError(null);
      const data = await inspectionTemplateService.getAllTemplates();
      setTemplatesData(data);
    } catch (error) {
      console.error('Error fetching inspection templates:', error);
      setError('Failed to load inspection templates. Please try again later.');
      setTemplatesData([]);
    } finally {
      setLoading(false);
    }
  };
  
  useEffect(() => {
    fetchTemplates();
  }, []);

  useEffect(() => {
    let filtered = templatesData;

    if (searchTerm) {
      filtered = filtered.filter(item =>
        item.name?.toLowerCase().includes(searchTerm.toLowerCase()) ||
        item.code?.toLowerCase().includes(searchTerm.toLowerCase()) ||
        item.description?.toLowerCase().includes(searchTerm.toLowerCase()) ||
        item.category?.toLowerCase().includes(searchTerm.toLowerCase())
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

    if (frequencyFilter !== 'all') {
      filtered = filtered.filter(item => item.frequency === frequencyFilter);
    }

    setFilteredData(filtered);
  }, [searchTerm, statusFilter, categoryFilter, frequencyFilter, templatesData]);

  const handleCreate = async () => {
    if (isSubmitting) return;
    
    try {
      setIsSubmitting(true);
      setError(null);
      console.log('Creating inspection template with data:', formData);
      const newTemplate = await inspectionTemplateService.createTemplate(formData);
      setTemplatesData([...templatesData, newTemplate]);
      setIsCreateDialogOpen(false);
      resetForm();
    } catch (error) {
      console.error('Error creating inspection template:', error);
      setError('Failed to create inspection template. Please try again.');
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleEdit = (template: InspectionTemplate) => {
    setSelectedTemplate(template);
    setFormData({
      name: template.name,
      code: template.code,
      description: template.description,
      category: template.category,
      frequency: template.frequency,
      estimatedDuration: template.estimatedDuration,
      requiresSignature: template.requiresSignature,
      allowPhotos: template.allowPhotos,
      version: template.version,
      assetTypes: template.assetTypes,
      inspectorRoles: template.inspectorRoles,
      priority: template.priority,
      checklistItems: template.checklistItems.map(item => ({
        item: item.item,
        type: item.type,
        required: item.required
      }))
    });
    setIsEditDialogOpen(true);
  };

  const handleView = (template: InspectionTemplate) => {
    setSelectedTemplate(template);
    setIsViewDialogOpen(true);
  };

  const handleUpdate = async () => {
    if (!selectedTemplate?.id || isSubmitting) return;
    
    try {
      setIsSubmitting(true);
      setError(null);
      const updateData: UpdateInspectionTemplateDto = {
        ...formData,
        isActive: selectedTemplate.isActive
      };
      console.log('Updating inspection template with data:', updateData);
      
      const updatedTemplate = await inspectionTemplateService.updateTemplate(selectedTemplate.id, updateData);
      setTemplatesData(templatesData.map(t => t.id === selectedTemplate.id ? updatedTemplate : t));
      setIsEditDialogOpen(false);
      resetForm();
    } catch (error) {
      console.error('Error updating inspection template:', error);
      setError('Failed to update inspection template. Please try again.');
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleDelete = async (template: InspectionTemplate) => {
    if (!confirm(`Are you sure you want to delete "${template.name}"?`)) return;
    
    try {
      setError(null);
      console.log(`Deleting inspection template: ${template.name}`);
      await inspectionTemplateService.deleteTemplate(template.id);
      setTemplatesData(templatesData.filter(t => t.id !== template.id));
    } catch (error) {
      console.error('Error deleting inspection template:', error);
      setError('Failed to delete inspection template. Please try again.');
    }
  };

  const handleDuplicate = async (template: InspectionTemplate) => {
    try {
      setError(null);
      const newName = `${template.name} (Copy)`;
      const newCode = `${template.code}-COPY`;
      console.log(`Duplicating inspection template: ${template.name}`);
      
      const duplicatedTemplate = await inspectionTemplateService.duplicateTemplate(template.id, newName, newCode);
      setTemplatesData([...templatesData, duplicatedTemplate]);
    } catch (error) {
      console.error('Error duplicating inspection template:', error);
      setError('Failed to duplicate inspection template. Please try again.');
    }
  };

  const resetForm = () => {
    setFormData({
      name: '',
      code: '',
      description: '',
      category: 'Safety',
      frequency: 'Monthly',
      estimatedDuration: 60,
      requiresSignature: false,
      allowPhotos: false,
      version: '1.0',
      assetTypes: [],
      inspectorRoles: [],
      priority: 'Medium',
      checklistItems: []
    });
    setSelectedTemplate(null);
  };

  const formatDuration = (minutes: number) => {
    if (minutes < 60) return `${minutes}m`;
    const hours = Math.floor(minutes / 60);
    const mins = minutes % 60;
    return mins > 0 ? `${hours}h ${mins}m` : `${hours}h`;
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
          <h1 className="text-3xl font-bold tracking-tight">Inspection Templates</h1>
          <p className="text-muted-foreground">
            Manage standardized inspection templates and checklists
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
              <DialogTitle>Add Inspection Template</DialogTitle>
              <DialogDescription>
                Create a new inspection template with checklist items.
              </DialogDescription>
            </DialogHeader>
            <div className="grid gap-4 py-4 max-h-[70vh] overflow-y-auto">
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="name">Template Name</Label>
                  <Input
                    id="name"
                    value={formData.name}
                    onChange={(e) => setFormData({...formData, name: e.target.value})}
                    placeholder="Enter template name"
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="code">Template Code</Label>
                  <Input
                    id="code"
                    value={formData.code}
                    onChange={(e) => setFormData({...formData, code: e.target.value.toUpperCase()})}
                    placeholder="e.g., SAFE-001"
                  />
                </div>
              </div>
              
              <div className="space-y-2">
                <Label htmlFor="description">Description</Label>
                <Textarea
                  id="description"
                  value={formData.description}
                  onChange={(e) => setFormData({...formData, description: e.target.value})}
                  placeholder="Describe this inspection template..."
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
                      <SelectItem value="Safety">Safety</SelectItem>
                      <SelectItem value="HVAC">HVAC</SelectItem>
                      <SelectItem value="Electrical">Electrical</SelectItem>
                      <SelectItem value="Fire Safety">Fire Safety</SelectItem>
                      <SelectItem value="Operations">Operations</SelectItem>
                      <SelectItem value="Quality">Quality</SelectItem>
                      <SelectItem value="Environmental">Environmental</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-2">
                  <Label htmlFor="frequency">Frequency</Label>
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
                    onChange={(e) => setFormData({...formData, estimatedDuration: parseInt(e.target.value) || 60})}
                    placeholder="60"
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="priority">Priority</Label>
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

              <div className="space-y-2">
                <Label htmlFor="version">Version</Label>
                <Input
                  id="version"
                  value={formData.version}
                  onChange={(e) => setFormData({...formData, version: e.target.value})}
                  placeholder="1.0"
                />
              </div>
              
              <div className="flex items-center space-x-4">
                <div className="flex items-center space-x-2">
                  <Switch
                    id="signature"
                    checked={formData.requiresSignature}
                    onCheckedChange={(checked) => setFormData({...formData, requiresSignature: checked})}
                  />
                  <Label htmlFor="signature">Requires Signature</Label>
                </div>
                <div className="flex items-center space-x-2">
                  <Switch
                    id="photos"
                    checked={formData.allowPhotos}
                    onCheckedChange={(checked) => setFormData({...formData, allowPhotos: checked})}
                  />
                  <Label htmlFor="photos">Allow Photos</Label>
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
              <Button variant="outline" onClick={() => setIsCreateDialogOpen(false)} disabled={isSubmitting}>
                Cancel
              </Button>
              <Button onClick={handleCreate} disabled={!formData.name || !formData.code || isSubmitting}>
                {isSubmitting ? 'Creating...' : 'Add Template'}
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
            <BreadcrumbPage>Inspection Templates</BreadcrumbPage>
          </BreadcrumbItem>
        </BreadcrumbList>
      </Breadcrumb>

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

      {/* Stats */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-2xl font-bold">{filteredData.length}</p>
                <p className="text-sm text-muted-foreground">Total Templates</p>
              </div>
              <FileText className="h-8 w-8 text-blue-500" />
            </div>
          </CardContent>
        </Card>
        
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-2xl font-bold">
                  {filteredData.filter(t => t.isActive).length}
                </p>
                <p className="text-sm text-muted-foreground">Active</p>
              </div>
              <CheckSquare className="h-8 w-8 text-green-500" />
            </div>
          </CardContent>
        </Card>
        
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-2xl font-bold">
                  {filteredData.filter(t => t.requiresSignature).length}
                </p>
                <p className="text-sm text-muted-foreground">Require Signature</p>
              </div>
              <Users className="h-8 w-8 text-orange-500" />
            </div>
          </CardContent>
        </Card>
        
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-2xl font-bold">
                  {Math.round(filteredData.reduce((sum, t) => sum + t.estimatedDuration, 0) / filteredData.length) || 0}
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
                placeholder="Search templates..."
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
                <SelectItem value="Safety">Safety</SelectItem>
                <SelectItem value="HVAC">HVAC</SelectItem>
                <SelectItem value="Electrical">Electrical</SelectItem>
                <SelectItem value="Fire Safety">Fire Safety</SelectItem>
                <SelectItem value="Operations">Operations</SelectItem>
              </SelectContent>
            </Select>

            <Select value={frequencyFilter} onValueChange={setFrequencyFilter}>
              <SelectTrigger>
                <SelectValue placeholder="Frequency" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Frequencies</SelectItem>
                <SelectItem value="Daily">Daily</SelectItem>
                <SelectItem value="Weekly">Weekly</SelectItem>
                <SelectItem value="Monthly">Monthly</SelectItem>
                <SelectItem value="Quarterly">Quarterly</SelectItem>
                <SelectItem value="Annual">Annual</SelectItem>
              </SelectContent>
            </Select>
          </div>
        </CardContent>
      </Card>

      {/* Templates List */}
      <Card>
        <CardHeader>
          <CardTitle>Inspection Templates</CardTitle>
          <CardDescription>
            {filteredData.length} template{filteredData.length === 1 ? '' : 's'} found
          </CardDescription>
        </CardHeader>
        <CardContent>
          {loading ? (
            <div className="text-center py-4">Loading inspection templates...</div>
          ) : error ? (
            <div className="text-center py-4 text-red-600">{error}</div>
          ) : filteredData.length === 0 ? (
            <div className="text-center py-4 text-muted-foreground">No inspection templates found</div>
          ) : (
          <div className="space-y-4">
            {filteredData.map((template) => (
              <div key={template.id} className="border rounded-lg p-4 hover:bg-muted/50 transition-colors">
                <div className="flex items-start justify-between">
                  <div className="space-y-3 flex-1">
                    <div className="flex items-center space-x-3">
                      <ClipboardList className="h-5 w-5 text-blue-500" />
                      <h3 className="font-semibold">{template.name}</h3>
                      <Badge variant="outline">{template.code}</Badge>
                      <Badge className={getPriorityColor(template.priority)}>
                        {template.priority}
                      </Badge>
                      <Badge className={template.isActive ? 'bg-green-100 text-green-800' : 'bg-gray-100 text-gray-800'}>
                        {template.isActive ? 'Active' : 'Inactive'}
                      </Badge>
                      {template.requiresSignature && (
                        <Badge className="bg-blue-100 text-blue-800">Signature Required</Badge>
                      )}
                      {template.allowPhotos && (
                        <Badge className="bg-purple-100 text-purple-800">Photos Allowed</Badge>
                      )}
                    </div>
                    
                    <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4 text-sm text-muted-foreground">
                      <div>
                        <span className="font-medium">Category:</span> {template.category}
                      </div>
                      <div>
                        <span className="font-medium">Frequency:</span> {template.frequency}
                      </div>
                      <div>
                        <span className="font-medium">Duration:</span> {formatDuration(template.estimatedDuration)}
                      </div>
                      <div>
                        <span className="font-medium">Items:</span> {template.checklistItems.length}
                      </div>
                      <div>
                        <span className="font-medium">Version:</span> {template.version}
                      </div>
                      <div>
                        <span className="font-medium">Updated:</span> {template.lastUpdated}
                      </div>
                      <div>
                        <span className="font-medium">Asset Types:</span> {template.assetTypes.join(', ')}
                      </div>
                      <div>
                        <span className="font-medium">Inspectors:</span> {template.inspectorRoles.join(', ')}
                      </div>
                    </div>
                    
                    <p className="text-sm text-muted-foreground">{template.description}</p>
                  </div>
                  
                  <div className="flex items-center space-x-2">
                    <Button size="sm" variant="outline" onClick={() => handleView(template)}>
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
                      onClick={() => handleDelete(template)}
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

      {/* View Template Dialog */}
      <Dialog open={isViewDialogOpen} onOpenChange={setIsViewDialogOpen}>
        <DialogContent className="sm:max-w-[800px]">
          <DialogHeader>
            <DialogTitle>View Template: {selectedTemplate?.name}</DialogTitle>
            <DialogDescription>
              Template details and checklist items
            </DialogDescription>
          </DialogHeader>
          {selectedTemplate && (
            <div className="space-y-4 max-h-[70vh] overflow-y-auto">
              <div className="grid grid-cols-2 gap-4">
                <div>
                  <Label>Code</Label>
                  <p className="text-sm text-muted-foreground">{selectedTemplate.code}</p>
                </div>
                <div>
                  <Label>Category</Label>
                  <p className="text-sm text-muted-foreground">{selectedTemplate.category}</p>
                </div>
                <div>
                  <Label>Frequency</Label>
                  <p className="text-sm text-muted-foreground">{selectedTemplate.frequency}</p>
                </div>
                <div>
                  <Label>Duration</Label>
                  <p className="text-sm text-muted-foreground">{formatDuration(selectedTemplate.estimatedDuration)}</p>
                </div>
              </div>
              
              <div>
                <Label>Description</Label>
                <p className="text-sm text-muted-foreground">{selectedTemplate.description}</p>
              </div>

              <div>
                <Label>Checklist Items ({selectedTemplate.checklistItems.length})</Label>
                <div className="space-y-2 mt-2">
                  {selectedTemplate.checklistItems.map((item, index) => (
                    <div key={item.id} className="flex items-center space-x-2 p-2 border rounded">
                      <span className="text-sm font-medium">{index + 1}.</span>
                      <span className="text-sm flex-1">{item.item}</span>
                      <Badge variant="outline" className="text-xs">
                        {item.type}
                      </Badge>
                      {item.required && (
                        <Badge className="bg-red-100 text-red-800 text-xs">Required</Badge>
                      )}
                    </div>
                  ))}
                </div>
              </div>
            </div>
          )}
          <DialogFooter>
            <Button variant="outline" onClick={() => setIsViewDialogOpen(false)}>
              Close
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Edit Dialog */}
      <Dialog open={isEditDialogOpen} onOpenChange={setIsEditDialogOpen}>
        <DialogContent className="sm:max-w-[700px]">
          <DialogHeader>
            <DialogTitle>Edit Template</DialogTitle>
            <DialogDescription>
              Update the inspection template information.
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 py-4 max-h-[70vh] overflow-y-auto">
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label htmlFor="edit-name">Template Name</Label>
                <Input
                  id="edit-name"
                  value={formData.name}
                  onChange={(e) => setFormData({...formData, name: e.target.value})}
                  placeholder="Enter template name"
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="edit-code">Template Code</Label>
                <Input
                  id="edit-code"
                  value={formData.code}
                  onChange={(e) => setFormData({...formData, code: e.target.value.toUpperCase()})}
                  placeholder="e.g., SAFE-001"
                />
              </div>
            </div>
            
            <div className="space-y-2">
              <Label htmlFor="edit-description">Description</Label>
              <Textarea
                id="edit-description"
                value={formData.description}
                onChange={(e) => setFormData({...formData, description: e.target.value})}
                placeholder="Describe this inspection template..."
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
                    <SelectItem value="Safety">Safety</SelectItem>
                    <SelectItem value="HVAC">HVAC</SelectItem>
                    <SelectItem value="Electrical">Electrical</SelectItem>
                    <SelectItem value="Fire Safety">Fire Safety</SelectItem>
                    <SelectItem value="Operations">Operations</SelectItem>
                    <SelectItem value="Quality">Quality</SelectItem>
                    <SelectItem value="Environmental">Environmental</SelectItem>
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-2">
                <Label htmlFor="edit-frequency">Frequency</Label>
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
                  </SelectContent>
                </Select>
              </div>
            </div>

            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label htmlFor="edit-duration">Estimated Duration (minutes)</Label>
                <Input
                  id="edit-duration"
                  type="number"
                  value={formData.estimatedDuration}
                  onChange={(e) => setFormData({...formData, estimatedDuration: parseInt(e.target.value) || 60})}
                  placeholder="60"
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="edit-priority">Priority</Label>
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

            <div className="space-y-2">
              <Label htmlFor="edit-version">Version</Label>
              <Input
                id="edit-version"
                value={formData.version}
                onChange={(e) => setFormData({...formData, version: e.target.value})}
                placeholder="1.0"
              />
            </div>
            
            <div className="flex items-center space-x-4">
              <div className="flex items-center space-x-2">
                <Switch
                  id="edit-signature"
                  checked={formData.requiresSignature}
                  onCheckedChange={(checked) => setFormData({...formData, requiresSignature: checked})}
                />
                <Label htmlFor="edit-signature">Requires Signature</Label>
              </div>
              <div className="flex items-center space-x-2">
                <Switch
                  id="edit-photos"
                  checked={formData.allowPhotos}
                  onCheckedChange={(checked) => setFormData({...formData, allowPhotos: checked})}
                />
                <Label htmlFor="edit-photos">Allow Photos</Label>
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
            <Button variant="outline" onClick={() => setIsEditDialogOpen(false)} disabled={isSubmitting}>
              Cancel
            </Button>
            <Button onClick={handleUpdate} disabled={!formData.name || !formData.code || isSubmitting}>
              {isSubmitting ? 'Updating...' : 'Update Template'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}