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
import { Plus, Search, Edit, Trash2, Star, TrendingUp, Loader2 } from 'lucide-react';
import { toast } from 'sonner';
import { evaluationCriteriaService, EvaluationCriterion, CreateEvaluationCriterionDto, UpdateEvaluationCriterionDto } from '@/services/evaluationCriteriaService';

export default function EvaluationCriteriaPage() {
  const [searchTerm, setSearchTerm] = useState('');
  const [categoryFilter, setCategoryFilter] = useState('all');
  const [isCreateDialogOpen, setIsCreateDialogOpen] = useState(false);
  const [isEditDialogOpen, setIsEditDialogOpen] = useState(false);
  const [criteria, setCriteria] = useState<EvaluationCriterion[]>([]);
  const [loading, setLoading] = useState(true);
  const [selectedCriterion, setSelectedCriterion] = useState<EvaluationCriterion | null>(null);
  const [formData, setFormData] = useState<CreateEvaluationCriterionDto>({
    criterionName: '',
    criterionCode: '',
    category: 'General',
    description: '',
    maxScore: 100,
    weight: 0,
    isActive: true,
    displayOrder: 0,
  });

  useEffect(() => {
    loadCriteria();
  }, []);

  const loadCriteria = async () => {
    try {
      setLoading(true);
      const data = await evaluationCriteriaService.getAll();
      setCriteria(data);
    } catch (error) {
      toast.error('Failed to load evaluation criteria');
      console.error(error);
    } finally {
      setLoading(false);
    }
  };
  const handleCreate = async () => {
    try {
      await evaluationCriteriaService.create(formData);
      toast.success('Evaluation criterion created successfully');
      setIsCreateDialogOpen(false);
      resetForm();
      loadCriteria();
    } catch (error: any) {
      toast.error(error.message || 'Failed to create evaluation criterion');
    }
  };

  const handleUpdate = async () => {
    if (!selectedCriterion) return;
    try {
      const updateData: UpdateEvaluationCriterionDto = {
        criterionName: formData.criterionName,
        description: formData.description,
        maxScore: formData.maxScore,
        weight: formData.weight,
        isActive: formData.isActive,
        displayOrder: formData.displayOrder,
      };
      await evaluationCriteriaService.update(selectedCriterion.id, updateData);
      toast.success('Evaluation criterion updated successfully');
      setIsEditDialogOpen(false);
      setSelectedCriterion(null);
      resetForm();
      loadCriteria();
    } catch (error: any) {
      toast.error(error.message || 'Failed to update evaluation criterion');
    }
  };

  const handleDelete = async (id: string) => {
    if (!confirm('Are you sure you want to delete this evaluation criterion?')) return;
    try {
      await evaluationCriteriaService.delete(id);
      toast.success('Evaluation criterion deleted successfully');
      loadCriteria();
    } catch (error: any) {
      toast.error(error.message || 'Failed to delete evaluation criterion');
    }
  };

  const handleEdit = (criterion: EvaluationCriterion) => {
    setSelectedCriterion(criterion);
    setFormData({
      criterionName: criterion.criterionName,
      criterionCode: criterion.criterionCode,
      category: criterion.category,
      description: criterion.description || '',
      maxScore: criterion.maxScore,
      weight: criterion.weight,
      isActive: criterion.isActive,
      displayOrder: criterion.displayOrder,
    });
    setIsEditDialogOpen(true);
  };

  const resetForm = () => {
    setFormData({
      criterionName: '',
      criterionCode: '',
      category: 'General',
      description: '',
      maxScore: 100,
      weight: 0,
      isActive: true,
      displayOrder: 0,
    });
  };

  const filteredCriteria = criteria.filter(criterion => {
    const matchesSearch = criterion.criterionName.toLowerCase().includes(searchTerm.toLowerCase()) ||
                         criterion.criterionCode.toLowerCase().includes(searchTerm.toLowerCase());
    const matchesCategory = categoryFilter === 'all' || criterion.category === categoryFilter;
    return matchesSearch && matchesCategory;
  });

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Evaluation Criteria</h1>
          <p className="text-muted-foreground">
            Manage standard evaluation criteria for bid assessment
          </p>
        </div>
        <Dialog open={isCreateDialogOpen} onOpenChange={setIsCreateDialogOpen}>
          <DialogTrigger asChild>
            <Button>
              <Plus className="mr-2 h-4 w-4" />
              Add Criterion
            </Button>
          </DialogTrigger>
          <DialogContent className="sm:max-w-[600px]">
            <DialogHeader>
              <DialogTitle>Add Evaluation Criterion</DialogTitle>
              <DialogDescription>
                Create a new evaluation criterion for bid assessment.
              </DialogDescription>
            </DialogHeader>
            <div className="grid gap-4 py-4">
              <div className="grid gap-2">
                <Label htmlFor="name">Criterion Name</Label>
                <Input
                  id="name"
                  placeholder="e.g., Price Competitiveness"
                  value={formData.criterionName}
                  onChange={(e) => setFormData({ ...formData, criterionName: e.target.value })}
                />
              </div>
              <div className="grid gap-2">
                <Label htmlFor="code">Criterion Code</Label>
                <Input
                  id="code"
                  placeholder="e.g., PRICE-001"
                  value={formData.criterionCode}
                  onChange={(e) => setFormData({ ...formData, criterionCode: e.target.value })}
                />
              </div>
              <div className="grid gap-2">
                <Label htmlFor="category">Category</Label>
                <Select value={formData.category} onValueChange={(value) => setFormData({ ...formData, category: value })}>
                  <SelectTrigger id="category">
                    <SelectValue placeholder="Select category" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="General">General</SelectItem>
                    <SelectItem value="Financial">Financial</SelectItem>
                    <SelectItem value="Technical">Technical</SelectItem>
                    <SelectItem value="Experience">Experience</SelectItem>
                    <SelectItem value="Schedule">Schedule</SelectItem>
                    <SelectItem value="Quality">Quality</SelectItem>
                    <SelectItem value="Other">Other</SelectItem>
                  </SelectContent>
                </Select>
              </div>
              <div className="grid grid-cols-2 gap-4">
                <div className="grid gap-2">
                  <Label htmlFor="maxScore">Max Score</Label>
                  <Input
                    id="maxScore"
                    type="number"
                    placeholder="e.g., 30"
                    value={formData.maxScore}
                    onChange={(e) => setFormData({ ...formData, maxScore: parseInt(e.target.value) || 0 })}
                  />
                </div>
                <div className="grid gap-2">
                  <Label htmlFor="weight">Weight (%)</Label>
                  <Input
                    id="weight"
                    type="number"
                    placeholder="e.g., 30"
                    value={formData.weight}
                    onChange={(e) => setFormData({ ...formData, weight: parseFloat(e.target.value) || 0 })}
                  />
                </div>
              </div>
              <div className="grid gap-2">
                <Label htmlFor="description">Description</Label>
                <Textarea
                  id="description"
                  placeholder="Criterion description..."
                  rows={3}
                  value={formData.description}
                  onChange={(e) => setFormData({ ...formData, description: e.target.value })}
                />
              </div>
            </div>
            <div className="flex justify-end gap-2">
              <Button variant="outline" onClick={() => { setIsCreateDialogOpen(false); resetForm(); }}>Cancel</Button>
              <Button onClick={handleCreate}>Create Criterion</Button>
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
                placeholder="Search criteria..."
                className="pl-8"
                value={searchTerm}
                onChange={(e) => setSearchTerm(e.target.value)}
              />
            </div>
            <Select value={categoryFilter} onValueChange={setCategoryFilter}>
              <SelectTrigger>
                <SelectValue placeholder="Category" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Categories</SelectItem>
                <SelectItem value="Financial">Financial</SelectItem>
                <SelectItem value="Technical">Technical</SelectItem>
                <SelectItem value="Experience">Experience</SelectItem>
                <SelectItem value="Schedule">Schedule</SelectItem>
                <SelectItem value="Quality">Quality</SelectItem>
                <SelectItem value="Other">Other</SelectItem>
              </SelectContent>
            </Select>
          </div>
        </CardContent>
      </Card>

      {/* Criteria List */}
      <Card>
        <CardHeader>
          <CardTitle>Evaluation Criteria</CardTitle>
          <CardDescription>
            {loading ? 'Loading...' : `${filteredCriteria.length} criteri${filteredCriteria.length !== 1 ? 'a' : 'on'} found`}
          </CardDescription>
        </CardHeader>
        <CardContent>
          {loading ? (
            <div className="flex justify-center py-8">
              <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
            </div>
          ) : (
            <div className="space-y-4">
              {filteredCriteria.length === 0 ? (
                <div className="text-center py-8 text-muted-foreground">
                  No criteria found. {searchTerm || categoryFilter !== 'all' ? 'Try adjusting your filters.' : 'Create your first criterion to get started.'}
                </div>
              ) : (
                filteredCriteria.map((criterion) => (
                  <div key={criterion.id} className="border rounded-lg p-4 hover:bg-muted/50 transition-colors">
                    <div className="flex items-start justify-between">
                      <div className="space-y-2 flex-1">
                        <div className="flex items-center space-x-3">
                          <Star className="h-5 w-5 text-muted-foreground" />
                          <h3 className="font-semibold">{criterion.criterionName}</h3>
                          <Badge variant="outline">{criterion.criterionCode}</Badge>
                          <Badge>{criterion.category}</Badge>
                          <Badge className={criterion.isActive ? 'bg-green-100 text-green-800' : 'bg-gray-100 text-gray-800'}>
                            {criterion.isActive ? 'Active' : 'Inactive'}
                          </Badge>
                        </div>

                        <div className="grid grid-cols-1 md:grid-cols-2 gap-2 text-sm text-muted-foreground">
                          <div>
                            <span className="font-medium">Max Score:</span> {criterion.maxScore}
                          </div>
                          <div>
                            <span className="font-medium">Weight:</span> {criterion.weight}%
                          </div>
                        </div>

                        {criterion.description && (
                          <p className="text-sm text-muted-foreground">{criterion.description}</p>
                        )}
                      </div>

                      <div className="flex items-center space-x-2 ml-4">
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={() => handleEdit(criterion)}
                        >
                          <Edit className="h-4 w-4" />
                        </Button>
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={() => handleDelete(criterion.id)}
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
            <DialogTitle>Edit Evaluation Criterion</DialogTitle>
            <DialogDescription>
              Update the evaluation criterion details.
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 py-4">
            <div className="grid gap-2">
              <Label htmlFor="edit-name">Criterion Name</Label>
              <Input
                id="edit-name"
                value={formData.criterionName}
                onChange={(e) => setFormData({ ...formData, criterionName: e.target.value })}
              />
            </div>
            <div className="grid gap-2">
              <Label htmlFor="edit-code">Criterion Code (Read-only)</Label>
              <Input
                id="edit-code"
                value={formData.criterionCode}
                disabled
              />
            </div>
            <div className="grid grid-cols-2 gap-4">
              <div className="grid gap-2">
                <Label htmlFor="edit-maxScore">Max Score</Label>
                <Input
                  id="edit-maxScore"
                  type="number"
                  value={formData.maxScore}
                  onChange={(e) => setFormData({ ...formData, maxScore: parseInt(e.target.value) || 0 })}
                />
              </div>
              <div className="grid gap-2">
                <Label htmlFor="edit-weight">Weight (%)</Label>
                <Input
                  id="edit-weight"
                  type="number"
                  value={formData.weight}
                  onChange={(e) => setFormData({ ...formData, weight: parseFloat(e.target.value) || 0 })}
                />
              </div>
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
            <Button variant="outline" onClick={() => { setIsEditDialogOpen(false); setSelectedCriterion(null); resetForm(); }}>Cancel</Button>
            <Button onClick={handleUpdate}>Update Criterion</Button>
          </div>
        </DialogContent>
      </Dialog>
    </div>
  );
}
