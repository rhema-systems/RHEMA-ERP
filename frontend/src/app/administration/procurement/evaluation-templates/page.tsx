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
import { Plus, Search, Edit, Trash2, FileText, Loader2, CheckCircle, AlertCircle } from 'lucide-react';
import { toast } from 'sonner';
import { evaluationTemplateService, EvaluationTemplate, CreateEvaluationTemplateDto, UpdateEvaluationTemplateDto, CreateEvaluationTemplateCriterionDto } from '@/services/evaluationTemplateService';
import { evaluationCriteriaService, EvaluationCriterion } from '@/services/evaluationCriteriaService';

const CATEGORIES = ['General', 'Construction', 'IT', 'Services', 'Goods', 'Consultancy'];
const TENDER_TYPES = ['RFQ', 'RFP', 'ITB', 'EOI'];
const SCORING_METHODS = ['WeightedAverage', 'SimpleAverage', 'PassFail'];

interface TemplateCriterionForm {
  evaluationCriterionId: string;
  criterionName: string;
  weight: number;
  maxScore: number;
  isMandatory: boolean;
  minimumScore?: number;
  displayOrder: number;
}

export default function EvaluationTemplatesPage() {
  const [searchTerm, setSearchTerm] = useState('');
  const [categoryFilter, setCategoryFilter] = useState('all');
  const [tenderTypeFilter, setTenderTypeFilter] = useState('all');
  const [isCreateDialogOpen, setIsCreateDialogOpen] = useState(false);
  const [isEditDialogOpen, setIsEditDialogOpen] = useState(false);
  const [templates, setTemplates] = useState<EvaluationTemplate[]>([]);
  const [availableCriteria, setAvailableCriteria] = useState<EvaluationCriterion[]>([]);
  const [loading, setLoading] = useState(true);
  const [selectedTemplate, setSelectedTemplate] = useState<EvaluationTemplate | null>(null);
  
  const [formData, setFormData] = useState<{
    templateName: string;
    templateCode: string;
    description: string;
    category: string;
    tenderType: string;
    isDefault: boolean;
    isActive: boolean;
    passingScore: number;
    scoringMethod: string;
    displayOrder: number;
    criteria: TemplateCriterionForm[];
  }>({
    templateName: '',
    templateCode: '',
    description: '',
    category: 'General',
    tenderType: 'RFQ',
    isDefault: false,
    isActive: true,
    passingScore: 70,
    scoringMethod: 'WeightedAverage',
    displayOrder: 0,
    criteria: [],
  });

  useEffect(() => {
    loadData();
  }, []);

  const loadData = async () => {
    try {
      setLoading(true);
      const [templatesData, criteriaData] = await Promise.all([
        evaluationTemplateService.getAll(),
        evaluationCriteriaService.getActive(),
      ]);
      setTemplates(templatesData);
      setAvailableCriteria(criteriaData);
    } catch (error) {
      toast.error('Failed to load data');
      console.error(error);
    } finally {
      setLoading(false);
    }
  };

  const getTotalWeight = () => formData.criteria.reduce((sum, c) => sum + c.weight, 0);

  const handleAddCriterion = (criterionId: string) => {
    const criterion = availableCriteria.find(c => c.id === criterionId);
    if (!criterion) return;
    if (formData.criteria.some(c => c.evaluationCriterionId === criterionId)) {
      toast.error('This criterion is already added');
      return;
    }
    setFormData({
      ...formData,
      criteria: [...formData.criteria, {
        evaluationCriterionId: criterionId,
        criterionName: criterion.criterionName,
        weight: criterion.weight,
        maxScore: criterion.maxScore,
        isMandatory: true,
        minimumScore: undefined,
        displayOrder: formData.criteria.length,
      }],
    });
  };

  const handleRemoveCriterion = (index: number) => {
    setFormData({
      ...formData,
      criteria: formData.criteria.filter((_, i) => i !== index),
    });
  };

  const handleCriterionChange = (index: number, field: keyof TemplateCriterionForm, value: any) => {
    const updated = [...formData.criteria];
    updated[index] = { ...updated[index], [field]: value };
    setFormData({ ...formData, criteria: updated });
  };

  const handleCreate = async () => {
    if (getTotalWeight() !== 100) {
      toast.error('Criteria weights must sum to 100%');
      return;
    }
    try {
      const dto: CreateEvaluationTemplateDto = {
        ...formData,
        criteria: formData.criteria.map((c, i) => ({
          evaluationCriterionId: c.evaluationCriterionId,
          weight: c.weight,
          maxScore: c.maxScore,
          isMandatory: c.isMandatory,
          minimumScore: c.minimumScore,
          displayOrder: i,
        })),
      };
      await evaluationTemplateService.create(dto);
      toast.success('Evaluation template created successfully');
      setIsCreateDialogOpen(false);
      resetForm();
      loadData();
    } catch (error: any) {
      toast.error(error.message || 'Failed to create evaluation template');
    }
  };

  const handleUpdate = async () => {
    if (!selectedTemplate) return;
    if (getTotalWeight() !== 100) {
      toast.error('Criteria weights must sum to 100%');
      return;
    }
    try {
      const dto: UpdateEvaluationTemplateDto = {
        templateName: formData.templateName,
        description: formData.description,
        category: formData.category,
        tenderType: formData.tenderType,
        isDefault: formData.isDefault,
        isActive: formData.isActive,
        passingScore: formData.passingScore,
        scoringMethod: formData.scoringMethod,
        displayOrder: formData.displayOrder,
        criteria: formData.criteria.map((c, i) => ({
          evaluationCriterionId: c.evaluationCriterionId,
          weight: c.weight,
          maxScore: c.maxScore,
          isMandatory: c.isMandatory,
          minimumScore: c.minimumScore,
          displayOrder: i,
        })),
      };
      await evaluationTemplateService.update(selectedTemplate.id, dto);
      toast.success('Evaluation template updated successfully');
      setIsEditDialogOpen(false);
      setSelectedTemplate(null);
      resetForm();
      loadData();
    } catch (error: any) {
      toast.error(error.message || 'Failed to update evaluation template');
    }
  };

  const handleDelete = async (id: string) => {
    if (!confirm('Are you sure you want to delete this evaluation template?')) return;
    try {
      await evaluationTemplateService.delete(id);
      toast.success('Evaluation template deleted successfully');
      loadData();
    } catch (error: any) {
      toast.error(error.message || 'Failed to delete evaluation template');
    }
  };

  const handleEdit = async (template: EvaluationTemplate) => {
    try {
      const fullTemplate = await evaluationTemplateService.getById(template.id);
      setSelectedTemplate(fullTemplate);
      setFormData({
        templateName: fullTemplate.templateName,
        templateCode: fullTemplate.templateCode,
        description: fullTemplate.description || '',
        category: fullTemplate.category,
        tenderType: fullTemplate.tenderType,
        isDefault: fullTemplate.isDefault,
        isActive: fullTemplate.isActive,
        passingScore: fullTemplate.passingScore,
        scoringMethod: fullTemplate.scoringMethod,
        displayOrder: fullTemplate.displayOrder,
        criteria: fullTemplate.criteria.map(c => ({
          evaluationCriterionId: c.evaluationCriterionId,
          criterionName: c.criterionName,
          weight: c.weight,
          maxScore: c.maxScore,
          isMandatory: c.isMandatory,
          minimumScore: c.minimumScore,
          displayOrder: c.displayOrder,
        })),
      });
      setIsEditDialogOpen(true);
    } catch (error) {
      toast.error('Failed to load template details');
    }
  };

  const resetForm = () => {
    setFormData({
      templateName: '',
      templateCode: '',
      description: '',
      category: 'General',
      tenderType: 'RFQ',
      isDefault: false,
      isActive: true,
      passingScore: 70,
      scoringMethod: 'WeightedAverage',
      displayOrder: 0,
      criteria: [],
    });
  };

  const filteredTemplates = templates.filter(template => {
    const matchesSearch = template.templateName.toLowerCase().includes(searchTerm.toLowerCase()) ||
                         template.templateCode.toLowerCase().includes(searchTerm.toLowerCase());
    const matchesCategory = categoryFilter === 'all' || template.category === categoryFilter;
    const matchesTenderType = tenderTypeFilter === 'all' || template.tenderType === tenderTypeFilter;
    return matchesSearch && matchesCategory && matchesTenderType;
  });

  const renderCriteriaForm = () => (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <Label>Criteria (Total Weight: {getTotalWeight()}%)</Label>
        {getTotalWeight() === 100 ? (
          <Badge className="bg-green-100 text-green-800"><CheckCircle className="h-3 w-3 mr-1" />Valid</Badge>
        ) : (
          <Badge className="bg-red-100 text-red-800"><AlertCircle className="h-3 w-3 mr-1" />Must equal 100%</Badge>
        )}
      </div>
      <Select onValueChange={handleAddCriterion}>
        <SelectTrigger>
          <SelectValue placeholder="Add criterion..." />
        </SelectTrigger>
        <SelectContent>
          {availableCriteria.filter(c => !formData.criteria.some(fc => fc.evaluationCriterionId === c.id)).map(c => (
            <SelectItem key={c.id} value={c.id}>{c.criterionName} ({c.criterionCode})</SelectItem>
          ))}
        </SelectContent>
      </Select>
      <div className="space-y-2 max-h-[300px] overflow-y-auto">
        {formData.criteria.map((criterion, index) => (
          <div key={index} className="border rounded-lg p-3 space-y-2">
            <div className="flex items-center justify-between">
              <span className="font-medium">{criterion.criterionName}</span>
              <Button variant="ghost" size="sm" onClick={() => handleRemoveCriterion(index)}>
                <Trash2 className="h-4 w-4" />
              </Button>
            </div>
            <div className="grid grid-cols-3 gap-2">
              <div>
                <Label className="text-xs">Weight (%)</Label>
                <Input
                  type="number"
                  value={criterion.weight}
                  onChange={(e) => handleCriterionChange(index, 'weight', parseFloat(e.target.value) || 0)}
                />
              </div>
              <div>
                <Label className="text-xs">Max Score</Label>
                <Input
                  type="number"
                  value={criterion.maxScore}
                  onChange={(e) => handleCriterionChange(index, 'maxScore', parseInt(e.target.value) || 0)}
                />
              </div>
              <div>
                <Label className="text-xs">Min Score</Label>
                <Input
                  type="number"
                  value={criterion.minimumScore || ''}
                  onChange={(e) => handleCriterionChange(index, 'minimumScore', e.target.value ? parseFloat(e.target.value) : undefined)}
                />
              </div>
            </div>
            <div className="flex items-center space-x-2">
              <input
                type="checkbox"
                checked={criterion.isMandatory}
                onChange={(e) => handleCriterionChange(index, 'isMandatory', e.target.checked)}
                className="h-4 w-4"
              />
              <Label className="text-xs">Mandatory</Label>
            </div>
          </div>
        ))}
      </div>
    </div>
  );

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Evaluation Templates</h1>
          <p className="text-muted-foreground">
            Manage evaluation templates for tender bid assessment
          </p>
        </div>
        <Dialog open={isCreateDialogOpen} onOpenChange={setIsCreateDialogOpen}>
          <DialogTrigger asChild>
            <Button><Plus className="mr-2 h-4 w-4" />Add Template</Button>
          </DialogTrigger>
          <DialogContent className="sm:max-w-[900px] max-h-[90vh] overflow-y-auto">
            <DialogHeader>
              <DialogTitle>Create Evaluation Template</DialogTitle>
              <DialogDescription>Create a new evaluation template with criteria and weights.</DialogDescription>
            </DialogHeader>
            <div className="grid gap-4 py-4">
              <div className="grid grid-cols-3 gap-4">
                <div className="grid gap-2">
                  <Label>Template Name</Label>
                  <Input value={formData.templateName} onChange={(e) => setFormData({ ...formData, templateName: e.target.value })} placeholder="e.g., Standard RFQ Evaluation" />
                </div>
                <div className="grid gap-2">
                  <Label>Template Code</Label>
                  <Input value={formData.templateCode} onChange={(e) => setFormData({ ...formData, templateCode: e.target.value })} placeholder="e.g., EVAL-RFQ-001" />
                </div>
                <div className="grid gap-2">
                  <Label>Description</Label>
                  <Input value={formData.description} onChange={(e) => setFormData({ ...formData, description: e.target.value })} placeholder="Brief description" />
                </div>
              </div>
              <div className="grid grid-cols-4 gap-4">
                <div className="grid gap-2">
                  <Label>Category</Label>
                  <Select value={formData.category} onValueChange={(v) => setFormData({ ...formData, category: v })}>
                    <SelectTrigger><SelectValue /></SelectTrigger>
                    <SelectContent>{CATEGORIES.map(c => <SelectItem key={c} value={c}>{c}</SelectItem>)}</SelectContent>
                  </Select>
                </div>
                <div className="grid gap-2">
                  <Label>Tender Type</Label>
                  <Select value={formData.tenderType} onValueChange={(v) => setFormData({ ...formData, tenderType: v })}>
                    <SelectTrigger><SelectValue /></SelectTrigger>
                    <SelectContent>{TENDER_TYPES.map(t => <SelectItem key={t} value={t}>{t}</SelectItem>)}</SelectContent>
                  </Select>
                </div>
                <div className="grid gap-2">
                  <Label>Scoring Method</Label>
                  <Select value={formData.scoringMethod} onValueChange={(v) => setFormData({ ...formData, scoringMethod: v })}>
                    <SelectTrigger><SelectValue /></SelectTrigger>
                    <SelectContent>{SCORING_METHODS.map(m => <SelectItem key={m} value={m}>{m}</SelectItem>)}</SelectContent>
                  </Select>
                </div>
                <div className="grid gap-2">
                  <Label>Passing Score (%)</Label>
                  <Input type="number" value={formData.passingScore} onChange={(e) => setFormData({ ...formData, passingScore: parseFloat(e.target.value) || 0 })} />
                </div>
              </div>
              <div className="flex items-center space-x-6">
                <div className="flex items-center space-x-2">
                  <input type="checkbox" checked={formData.isActive} onChange={(e) => setFormData({ ...formData, isActive: e.target.checked })} className="h-4 w-4" />
                  <Label>Active</Label>
                </div>
                <div className="flex items-center space-x-2">
                  <input type="checkbox" checked={formData.isDefault} onChange={(e) => setFormData({ ...formData, isDefault: e.target.checked })} className="h-4 w-4" />
                  <Label>Default for Category/Type</Label>
                </div>
              </div>
              {renderCriteriaForm()}
            </div>
            <div className="flex justify-end gap-2">
              <Button variant="outline" onClick={() => { setIsCreateDialogOpen(false); resetForm(); }}>Cancel</Button>
              <Button onClick={handleCreate} disabled={getTotalWeight() !== 100}>Create Template</Button>
            </div>
          </DialogContent>
        </Dialog>
      </div>

      {/* Filters */}
      <Card>
        <CardHeader><CardTitle>Filters</CardTitle></CardHeader>
        <CardContent>
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            <div className="relative">
              <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
              <Input placeholder="Search templates..." className="pl-8" value={searchTerm} onChange={(e) => setSearchTerm(e.target.value)} />
            </div>
            <Select value={categoryFilter} onValueChange={setCategoryFilter}>
              <SelectTrigger><SelectValue placeholder="Category" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Categories</SelectItem>
                {CATEGORIES.map(c => <SelectItem key={c} value={c}>{c}</SelectItem>)}
              </SelectContent>
            </Select>
            <Select value={tenderTypeFilter} onValueChange={setTenderTypeFilter}>
              <SelectTrigger><SelectValue placeholder="Tender Type" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Types</SelectItem>
                {TENDER_TYPES.map(t => <SelectItem key={t} value={t}>{t}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
        </CardContent>
      </Card>

      {/* Templates List */}
      <Card>
        <CardHeader>
          <CardTitle>Evaluation Templates</CardTitle>
          <CardDescription>{loading ? 'Loading...' : `${filteredTemplates.length} template${filteredTemplates.length !== 1 ? 's' : ''} found`}</CardDescription>
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
                        <FileText className="h-5 w-5 text-muted-foreground" />
                        <h3 className="font-semibold">{template.templateName}</h3>
                        <Badge variant="outline">{template.templateCode}</Badge>
                        <Badge>{template.category}</Badge>
                        <Badge variant="secondary">{template.tenderType}</Badge>
                        {template.isDefault && <Badge className="bg-blue-100 text-blue-800">Default</Badge>}
                        <Badge className={template.isActive ? 'bg-green-100 text-green-800' : 'bg-gray-100 text-gray-800'}>
                          {template.isActive ? 'Active' : 'Inactive'}
                        </Badge>
                      </div>
                      <div className="grid grid-cols-4 gap-2 text-sm text-muted-foreground">
                        <div><span className="font-medium">Criteria:</span> {template.criteriaCount}</div>
                        <div><span className="font-medium">Total Weight:</span> {template.totalWeight}%</div>
                        <div><span className="font-medium">Passing Score:</span> {template.passingScore}%</div>
                        <div><span className="font-medium">Method:</span> {template.scoringMethod}</div>
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
        <DialogContent className="sm:max-w-[900px] max-h-[90vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Edit Evaluation Template</DialogTitle>
            <DialogDescription>Update the evaluation template details and criteria.</DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 py-4">
            <div className="grid grid-cols-3 gap-4">
              <div className="grid gap-2">
                <Label>Template Name</Label>
                <Input value={formData.templateName} onChange={(e) => setFormData({ ...formData, templateName: e.target.value })} />
              </div>
              <div className="grid gap-2">
                <Label>Template Code (Read-only)</Label>
                <Input value={formData.templateCode} disabled />
              </div>
              <div className="grid gap-2">
                <Label>Description</Label>
                <Input value={formData.description} onChange={(e) => setFormData({ ...formData, description: e.target.value })} placeholder="Brief description" />
              </div>
            </div>
            <div className="grid grid-cols-4 gap-4">
              <div className="grid gap-2">
                <Label>Category</Label>
                <Select value={formData.category} onValueChange={(v) => setFormData({ ...formData, category: v })}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>{CATEGORIES.map(c => <SelectItem key={c} value={c}>{c}</SelectItem>)}</SelectContent>
                </Select>
              </div>
              <div className="grid gap-2">
                <Label>Tender Type</Label>
                <Select value={formData.tenderType} onValueChange={(v) => setFormData({ ...formData, tenderType: v })}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>{TENDER_TYPES.map(t => <SelectItem key={t} value={t}>{t}</SelectItem>)}</SelectContent>
                </Select>
              </div>
              <div className="grid gap-2">
                <Label>Scoring Method</Label>
                <Select value={formData.scoringMethod} onValueChange={(v) => setFormData({ ...formData, scoringMethod: v })}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>{SCORING_METHODS.map(m => <SelectItem key={m} value={m}>{m}</SelectItem>)}</SelectContent>
                </Select>
              </div>
              <div className="grid gap-2">
                <Label>Passing Score (%)</Label>
                <Input type="number" value={formData.passingScore} onChange={(e) => setFormData({ ...formData, passingScore: parseFloat(e.target.value) || 0 })} />
              </div>
            </div>
            <div className="flex items-center space-x-6">
              <div className="flex items-center space-x-2">
                <input type="checkbox" checked={formData.isActive} onChange={(e) => setFormData({ ...formData, isActive: e.target.checked })} className="h-4 w-4" />
                <Label>Active</Label>
              </div>
              <div className="flex items-center space-x-2">
                <input type="checkbox" checked={formData.isDefault} onChange={(e) => setFormData({ ...formData, isDefault: e.target.checked })} className="h-4 w-4" />
                <Label>Default for Category/Type</Label>
              </div>
            </div>
            {renderCriteriaForm()}
          </div>
          <div className="flex justify-end gap-2">
            <Button variant="outline" onClick={() => { setIsEditDialogOpen(false); setSelectedTemplate(null); resetForm(); }}>Cancel</Button>
            <Button onClick={handleUpdate} disabled={getTotalWeight() !== 100}>Update Template</Button>
          </div>
        </DialogContent>
      </Dialog>
    </div>
  );
}
