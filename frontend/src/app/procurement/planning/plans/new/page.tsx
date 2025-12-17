'use client';

import { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { ArrowLeft, Save } from 'lucide-react';
import { toast } from 'sonner';
import { procurementPlanService, commonService, type CreateProcurementPlanDto, type DepartmentDto } from '@/services/procurementPlanningService';

export default function NewProcurementPlanPage() {
  const router = useRouter();
  const [loading, setLoading] = useState(false);
  const [departments, setDepartments] = useState<DepartmentDto[]>([]);
  const [loadingDepartments, setLoadingDepartments] = useState(true);
  const [formData, setFormData] = useState<CreateProcurementPlanDto>({
    title: '',
    description: '',
    departmentId: '',
    fiscalYear: new Date().getFullYear(),
    planStartDate: new Date().toISOString().split('T')[0],
    planEndDate: new Date(new Date().getFullYear(), 11, 31).toISOString().split('T')[0],
    planDurationYears: 1,
    totalEstimatedBudget: 0,
    currency: 'USD',
    notes: '',
    items: [],
  });

  // Fetch departments on mount
  useEffect(() => {
    const fetchDepartments = async () => {
      try {
        setLoadingDepartments(true);
        const data = await commonService.getDepartments();
        setDepartments(data);
      } catch (error) {
        console.error('Error fetching departments:', error);
        toast.error('Failed to load departments');
      } finally {
        setLoadingDepartments(false);
      }
    };
    fetchDepartments();
  }, []);

  const handleInputChange = (field: keyof CreateProcurementPlanDto, value: string | number) => {
    setFormData(prev => ({ ...prev, [field]: value }));
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    
    if (!formData.title.trim()) {
      toast.error('Title is required');
      return;
    }
    if (!formData.departmentId) {
      toast.error('Department is required');
      return;
    }

    try {
      setLoading(true);
      await procurementPlanService.createPlan(formData);
      toast.success('Procurement plan created successfully');
      router.push('/procurement/planning/plans');
    } catch (error) {
      console.error('Error creating procurement plan:', error);
      toast.error('Failed to create procurement plan');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center gap-4">
        <Button variant="ghost" size="icon" onClick={() => router.back()}>
          <ArrowLeft className="h-5 w-5" />
        </Button>
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Create Procurement Plan</h1>
          <p className="text-muted-foreground">
            Create a new departmental procurement plan
          </p>
        </div>
      </div>

      <form onSubmit={handleSubmit}>
        <div className="grid gap-6">
          {/* Basic Information */}
          <Card>
            <CardHeader>
              <CardTitle>Basic Information</CardTitle>
              <CardDescription>Enter the basic details of the procurement plan</CardDescription>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="title">Title *</Label>
                  <Input
                    id="title"
                    value={formData.title}
                    onChange={(e) => handleInputChange('title', e.target.value)}
                    placeholder="Enter plan title"
                    required
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="departmentId">Department *</Label>
                  <Select
                    value={formData.departmentId}
                    onValueChange={(value) => handleInputChange('departmentId', value)}
                    disabled={loadingDepartments}
                  >
                    <SelectTrigger>
                      <SelectValue placeholder={loadingDepartments ? "Loading departments..." : "Select department"} />
                    </SelectTrigger>
                    <SelectContent>
                      {departments.map((dept) => (
                        <SelectItem key={dept.id} value={dept.id}>
                          {dept.code ? `${dept.code} - ${dept.name}` : dept.name}
                        </SelectItem>
                      ))}
                      {departments.length === 0 && !loadingDepartments && (
                        <div className="px-2 py-4 text-center text-sm text-muted-foreground">
                          No departments available
                        </div>
                      )}
                    </SelectContent>
                  </Select>
                </div>
              </div>

              <div className="space-y-2">
                <Label htmlFor="description">Description</Label>
                <Textarea
                  id="description"
                  value={formData.description || ''}
                  onChange={(e) => handleInputChange('description', e.target.value)}
                  placeholder="Enter plan description"
                  rows={3}
                />
              </div>
            </CardContent>
          </Card>

          {/* Planning Period */}
          <Card>
            <CardHeader>
              <CardTitle>Planning Period</CardTitle>
              <CardDescription>Define the fiscal year and planning period</CardDescription>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="fiscalYear">Fiscal Year *</Label>
                  <Input
                    id="fiscalYear"
                    type="number"
                    value={formData.fiscalYear}
                    onChange={(e) => handleInputChange('fiscalYear', parseInt(e.target.value))}
                    min={2020}
                    max={2050}
                    required
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="planStartDate">Start Date *</Label>
                  <Input
                    id="planStartDate"
                    type="date"
                    value={formData.planStartDate}
                    onChange={(e) => handleInputChange('planStartDate', e.target.value)}
                    required
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="planEndDate">End Date *</Label>
                  <Input
                    id="planEndDate"
                    type="date"
                    value={formData.planEndDate}
                    onChange={(e) => handleInputChange('planEndDate', e.target.value)}
                    required
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="planDurationYears">Duration (Years)</Label>
                  <Input
                    id="planDurationYears"
                    type="number"
                    value={formData.planDurationYears}
                    onChange={(e) => handleInputChange('planDurationYears', parseInt(e.target.value))}
                    min={1}
                    max={10}
                  />
                </div>
              </div>
            </CardContent>
          </Card>

          {/* Budget Information */}
          <Card>
            <CardHeader>
              <CardTitle>Budget Information</CardTitle>
              <CardDescription>Define the estimated budget for this plan</CardDescription>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="totalEstimatedBudget">Estimated Budget</Label>
                  <Input
                    id="totalEstimatedBudget"
                    type="number"
                    value={formData.totalEstimatedBudget}
                    onChange={(e) => handleInputChange('totalEstimatedBudget', parseFloat(e.target.value))}
                    min={0}
                    step={0.01}
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="currency">Currency</Label>
                  <Select
                    value={formData.currency}
                    onValueChange={(value) => handleInputChange('currency', value)}
                  >
                    <SelectTrigger>
                      <SelectValue placeholder="Select currency" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="USD">USD - US Dollar</SelectItem>
                      <SelectItem value="EUR">EUR - Euro</SelectItem>
                      <SelectItem value="GBP">GBP - British Pound</SelectItem>
                      <SelectItem value="GHS">GHS - Ghanaian Cedi</SelectItem>
                      <SelectItem value="ETB">ETB - Ethiopian Birr</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
              </div>

              <div className="space-y-2">
                <Label htmlFor="notes">Notes</Label>
                <Textarea
                  id="notes"
                  value={formData.notes || ''}
                  onChange={(e) => handleInputChange('notes', e.target.value)}
                  placeholder="Additional notes or comments"
                  rows={3}
                />
              </div>
            </CardContent>
          </Card>

          {/* Actions */}
          <div className="flex justify-end gap-4">
            <Button type="button" variant="outline" onClick={() => router.back()}>
              Cancel
            </Button>
            <Button type="submit" disabled={loading} className="gap-2">
              <Save className="h-4 w-4" />
              {loading ? 'Creating...' : 'Create Plan'}
            </Button>
          </div>
        </div>
      </form>
    </div>
  );
}

