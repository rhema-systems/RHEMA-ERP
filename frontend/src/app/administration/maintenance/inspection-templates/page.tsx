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
  Eye,
  Copy
} from 'lucide-react';
import { cn } from '@/lib/utils';

// Mock data for inspection templates
const inspectionTemplatesData = [
  {
    id: 1,
    name: 'Monthly Equipment Safety Inspection',
    code: 'MESI-001',
    description: 'Comprehensive monthly safety check for all production equipment',
    category: 'Safety',
    frequency: 'Monthly',
    estimatedDuration: 120,
    isActive: true,
    requiresSignature: true,
    allowPhotos: true,
    version: '2.1',
    createdBy: 'John Smith',
    lastUpdated: '2024-03-15',
    checklistItems: [
      { id: 1, item: 'Check emergency stop buttons', type: 'checklist', required: true },
      { id: 2, item: 'Inspect safety guards', type: 'checklist', required: true },
      { id: 3, item: 'Test warning lights', type: 'checklist', required: true },
      { id: 4, item: 'Verify lockout/tagout procedures', type: 'checklist', required: true },
      { id: 5, item: 'Document any issues found', type: 'text', required: false }
    ],
    assetTypes: ['Production Equipment', 'Conveyors'],
    inspectorRoles: ['Safety Inspector', 'Maintenance Supervisor'],
    priority: 'High'
  },
  {
    id: 2,
    name: 'HVAC System Quarterly Review',
    code: 'HVAC-Q001',
    description: 'Quarterly inspection template for heating, ventilation, and air conditioning systems',
    category: 'HVAC',
    frequency: 'Quarterly',
    estimatedDuration: 90,
    isActive: true,
    requiresSignature: true,
    allowPhotos: true,
    version: '1.5',
    createdBy: 'Sarah Johnson',
    lastUpdated: '2024-03-10',
    checklistItems: [
      { id: 1, item: 'Check air filter condition', type: 'checklist', required: true },
      { id: 2, item: 'Inspect ductwork for leaks', type: 'checklist', required: true },
      { id: 3, item: 'Test thermostat calibration', type: 'measurement', required: true },
      { id: 4, item: 'Record temperature readings', type: 'number', required: true },
      { id: 5, item: 'Clean condenser coils', type: 'checklist', required: false }
    ],
    assetTypes: ['HVAC Units', 'Air Handlers'],
    inspectorRoles: ['HVAC Technician', 'Facility Manager'],
    priority: 'Medium'
  },
  {
    id: 3,
    name: 'Electrical Panel Annual Inspection',
    code: 'ELEC-A001',
    description: 'Annual comprehensive inspection of electrical panels and distribution systems',
    category: 'Electrical',
    frequency: 'Annual',
    estimatedDuration: 180,
    isActive: true,
    requiresSignature: true,
    allowPhotos: true,
    version: '3.0',
    createdBy: 'Mike Davis',
    lastUpdated: '2024-03-08',
    checklistItems: [
      { id: 1, item: 'Inspect panel condition', type: 'checklist', required: true },
      { id: 2, item: 'Check wire connections', type: 'checklist', required: true },
      { id: 3, item: 'Test circuit breakers', type: 'checklist', required: true },
      { id: 4, item: 'Measure voltage levels', type: 'measurement', required: true },
      { id: 5, item: 'Document any anomalies', type: 'text', required: false }
    ],
    assetTypes: ['Electrical Panels', 'Distribution Boards'],
    inspectorRoles: ['Electrician', 'Electrical Engineer'],
    priority: 'High'
  },
  {
    id: 4,
    name: 'Fire Safety Equipment Check',
    code: 'FIRE-M001',
    description: 'Monthly inspection of fire extinguishers, alarms, and emergency equipment',
    category: 'Fire Safety',
    frequency: 'Monthly',
    estimatedDuration: 60,
    isActive: true,
    requiresSignature: true,
    allowPhotos: false,
    version: '1.2',
    createdBy: 'Lisa Brown',
    lastUpdated: '2024-03-12',
    checklistItems: [
      { id: 1, item: 'Check fire extinguisher pressure', type: 'checklist', required: true },
      { id: 2, item: 'Test smoke detector functionality', type: 'checklist', required: true },
      { id: 3, item: 'Verify emergency exit signs', type: 'checklist', required: true },
      { id: 4, item: 'Inspect sprinkler heads', type: 'checklist', required: true }
    ],
    assetTypes: ['Fire Extinguishers', 'Smoke Detectors', 'Emergency Lighting'],
    inspectorRoles: ['Fire Safety Officer', 'Facility Manager'],
    priority: 'Critical'
  },
  {
    id: 5,
    name: 'Conveyor Belt Daily Inspection',
    code: 'CONV-D001',
    description: 'Daily operational inspection for conveyor belt systems',
    category: 'Operations',
    frequency: 'Daily',
    estimatedDuration: 30,
    isActive: true,
    requiresSignature: false,
    allowPhotos: true,
    version: '1.0',
    createdBy: 'Tom Wilson',
    lastUpdated: '2024-03-14',
    checklistItems: [
      { id: 1, item: 'Check belt alignment', type: 'checklist', required: true },
      { id: 2, item: 'Inspect rollers for wear', type: 'checklist', required: true },
      { id: 3, item: 'Lubricate bearings', type: 'checklist', required: false },
      { id: 4, item: 'Record belt speed', type: 'number', required: false }
    ],
    assetTypes: ['Conveyor Systems'],
    inspectorRoles: ['Production Operator', 'Maintenance Technician'],
    priority: 'Medium'
  }
];

export default function InspectionTemplatesPage() {
  const [searchTerm, setSearchTerm] = useState('');
  const [statusFilter, setStatusFilter] = useState('all');
  const [categoryFilter, setCategoryFilter] = useState('all');
  const [frequencyFilter, setFrequencyFilter] = useState('all');
  const [isCreateDialogOpen, setIsCreateDialogOpen] = useState(false);
  const [isEditDialogOpen, setIsEditDialogOpen] = useState(false);
  const [isViewDialogOpen, setIsViewDialogOpen] = useState(false);
  const [selectedTemplate, setSelectedTemplate] = useState(null);
  const [filteredData, setFilteredData] = useState(inspectionTemplatesData);
  
  // Form state
  const [formData, setFormData] = useState({
    name: '',
    code: '',
    description: '',
    category: 'Safety',
    frequency: 'Monthly',
    estimatedDuration: 60,
    isActive: true,
    requiresSignature: false,
    allowPhotos: false,
    version: '1.0',
    assetTypes: [],
    inspectorRoles: [],
    priority: 'Medium',
    checklistItems: []
  });

  useEffect(() => {
    let filtered = inspectionTemplatesData;

    if (searchTerm) {
      filtered = filtered.filter(item =>
        item.name.toLowerCase().includes(searchTerm.toLowerCase()) ||
        item.code.toLowerCase().includes(searchTerm.toLowerCase()) ||
        item.description.toLowerCase().includes(searchTerm.toLowerCase()) ||
        item.category.toLowerCase().includes(searchTerm.toLowerCase())
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
  }, [searchTerm, statusFilter, categoryFilter, frequencyFilter]);

  const handleCreate = () => {
    console.log('Creating inspection template:', formData);
    setIsCreateDialogOpen(false);
    resetForm();
  };

  const handleEdit = (template: any) => {
    setSelectedTemplate(template);
    setFormData({
      name: template.name,
      code: template.code,
      description: template.description,
      category: template.category,
      frequency: template.frequency,
      estimatedDuration: template.estimatedDuration,
      isActive: template.isActive,
      requiresSignature: template.requiresSignature,
      allowPhotos: template.allowPhotos,
      version: template.version,
      assetTypes: template.assetTypes,
      inspectorRoles: template.inspectorRoles,
      priority: template.priority,
      checklistItems: template.checklistItems
    });
    setIsEditDialogOpen(true);
  };

  const handleView = (template: any) => {
    setSelectedTemplate(template);
    setIsViewDialogOpen(true);
  };

  const handleUpdate = () => {
    console.log('Updating inspection template:', selectedTemplate?.id, formData);
    setIsEditDialogOpen(false);
    resetForm();
  };

  const handleDelete = (id: number) => {
    console.log('Deleting inspection template:', id);
  };

  const handleDuplicate = (template: any) => {
    console.log('Duplicating inspection template:', template.id);
  };

  const resetForm = () => {
    setFormData({
      name: '',
      code: '',
      description: '',
      category: 'Safety',
      frequency: 'Monthly',
      estimatedDuration: 60,
      isActive: true,
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
              <Button variant="outline" onClick={() => setIsCreateDialogOpen(false)}>
                Cancel
              </Button>
              <Button onClick={handleCreate}>
                Add Template
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
                      onClick={() => handleDelete(template.id)}
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
            <Button variant="outline" onClick={() => setIsEditDialogOpen(false)}>
              Cancel
            </Button>
            <Button onClick={handleUpdate}>Update Template</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}