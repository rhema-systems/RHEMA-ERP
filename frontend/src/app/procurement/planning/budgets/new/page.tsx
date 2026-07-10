'use client';

import { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { ArrowLeft, Save, Plus, Trash2 } from 'lucide-react';
import { toast } from 'sonner';
import { procurementBudgetService, commonService, type CreateProcurementBudgetDto, type CreateProcurementBudgetAllocationDto, type DepartmentDto } from '@/services/procurementPlanningService';
import { FiscalYearSelect } from '../../components/FiscalYearSelect';

export default function NewProcurementBudgetPage() {
  const router = useRouter();
  const [loading, setLoading] = useState(false);
  const [departments, setDepartments] = useState<DepartmentDto[]>([]);
  const [loadingDepartments, setLoadingDepartments] = useState(true);
  const [formData, setFormData] = useState<CreateProcurementBudgetDto>({
    title: '',
    description: '',
    departmentId: '',
    fiscalYear: new Date().getFullYear(),
    allocatedAmount: 0,
    currency: 'USD',
    controlLevel: 'Warning',
    warningThresholdPercent: 80,
    notes: '',
    allocations: [],
  });

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

  const handleInputChange = (field: keyof CreateProcurementBudgetDto, value: string | number) => {
    setFormData(prev => ({ ...prev, [field]: value }));
  };

  const handleAddAllocation = () => {
    setFormData(prev => ({
      ...prev,
      allocations: [...(prev.allocations || []), { categoryName: '', categoryDescription: '', allocatedAmount: 0, notes: '' }],
    }));
  };

  const handleRemoveAllocation = (index: number) => {
    setFormData(prev => ({
      ...prev,
      allocations: prev.allocations?.filter((_, i) => i !== index) || [],
    }));
  };

  const handleAllocationChange = (index: number, field: keyof CreateProcurementBudgetAllocationDto, value: string | number) => {
    setFormData(prev => ({
      ...prev,
      allocations: prev.allocations?.map((alloc, i) => i === index ? { ...alloc, [field]: value } : alloc) || [],
    }));
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!formData.title.trim()) { toast.error('Title is required'); return; }
    if (!formData.departmentId) { toast.error('Department is required'); return; }
    if (formData.allocatedAmount <= 0) { toast.error('Allocated amount must be greater than 0'); return; }

    try {
      setLoading(true);
      await procurementBudgetService.createBudget(formData);
      toast.success('Procurement budget created successfully');
      router.push('/procurement/planning/budgets');
    } catch (error) {
      console.error('Error creating procurement budget:', error);
      toast.error('Failed to create procurement budget');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-4">
        <Button variant="ghost" size="icon" onClick={() => router.back()}>
          <ArrowLeft className="h-5 w-5" />
        </Button>
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Create Procurement Budget</h1>
          <p className="text-muted-foreground">Create a new departmental procurement budget</p>
        </div>
      </div>

      <form onSubmit={handleSubmit}>
        <div className="grid gap-6">
          {/* Basic Information */}
          <Card>
            <CardHeader>
              <CardTitle>Basic Information</CardTitle>
              <CardDescription>Enter the basic details of the budget</CardDescription>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="title">Title *</Label>
                  <Input id="title" value={formData.title} onChange={(e) => handleInputChange('title', e.target.value)} placeholder="Enter budget title" required />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="departmentId">Department *</Label>
                  <Select value={formData.departmentId} onValueChange={(value) => handleInputChange('departmentId', value)} disabled={loadingDepartments}>
                    <SelectTrigger><SelectValue placeholder={loadingDepartments ? "Loading..." : "Select department"} /></SelectTrigger>
                    <SelectContent>
                      {departments.map((dept) => (<SelectItem key={dept.id} value={dept.id}>{dept.code ? `${dept.code} - ${dept.name}` : dept.name}</SelectItem>))}
                    </SelectContent>
                  </Select>
                </div>
              </div>
              <div className="space-y-2">
                <Label htmlFor="description">Description</Label>
                <Textarea id="description" value={formData.description || ''} onChange={(e) => handleInputChange('description', e.target.value)} placeholder="Enter budget description" rows={3} />
              </div>
            </CardContent>
          </Card>

          {/* Budget Details - continued in next section */}
          <Card>
            <CardHeader>
              <CardTitle>Budget Details</CardTitle>
              <CardDescription>Define the budget amount and control settings</CardDescription>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="fiscalYear">Fiscal Year *</Label>
                  <FiscalYearSelect
                    value={formData.fiscalYear}
                    onValueChange={(year) => handleInputChange('fiscalYear', year)}
                    autoSelectFirstAvailable
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="allocatedAmount">Allocated Amount *</Label>
                  <Input id="allocatedAmount" type="number" value={formData.allocatedAmount} onChange={(e) => handleInputChange('allocatedAmount', parseFloat(e.target.value))} min={0} step={0.01} required />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="currency">Currency</Label>
                  <Select value={formData.currency} onValueChange={(value) => handleInputChange('currency', value)}>
                    <SelectTrigger><SelectValue placeholder="Select currency" /></SelectTrigger>
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
              <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="controlLevel">Control Level</Label>
                  <Select value={formData.controlLevel} onValueChange={(value) => handleInputChange('controlLevel', value)}>
                    <SelectTrigger><SelectValue placeholder="Select control level" /></SelectTrigger>
                    <SelectContent>
                      <SelectItem value="Strict">Strict - Block over-budget</SelectItem>
                      <SelectItem value="Warning">Warning - Warn but allow</SelectItem>
                      <SelectItem value="Advisory">Advisory - Inform only</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-2">
                  <Label htmlFor="warningThresholdPercent">Warning Threshold (%)</Label>
                  <Input id="warningThresholdPercent" type="number" value={formData.warningThresholdPercent} onChange={(e) => handleInputChange('warningThresholdPercent', parseFloat(e.target.value))} min={0} max={100} step={1} />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="effectiveDate">Effective Date</Label>
                  <Input id="effectiveDate" type="date" value={formData.effectiveDate || ''} onChange={(e) => handleInputChange('effectiveDate', e.target.value)} />
                </div>
              </div>
              <div className="space-y-2">
                <Label htmlFor="notes">Notes</Label>
                <Textarea id="notes" value={formData.notes || ''} onChange={(e) => handleInputChange('notes', e.target.value)} placeholder="Additional notes" rows={3} />
              </div>
            </CardContent>
          </Card>

          {/* Budget Allocations */}
          <Card>
            <CardHeader>
              <div className="flex items-center justify-between">
                <div>
                  <CardTitle>Budget Allocations</CardTitle>
                  <CardDescription>Allocate budget to different categories</CardDescription>
                </div>
                <Button type="button" variant="outline" size="sm" onClick={handleAddAllocation} className="gap-2">
                  <Plus className="h-4 w-4" />Add Allocation
                </Button>
              </div>
            </CardHeader>
            <CardContent>
              {formData.allocations && formData.allocations.length > 0 ? (
                <div className="space-y-4">
                  {formData.allocations.map((alloc, index) => (
                    <div key={index} className="grid grid-cols-1 md:grid-cols-5 gap-4 p-4 border rounded-lg">
                      <div className="space-y-2">
                        <Label>Category Name *</Label>
                        <Input value={alloc.categoryName} onChange={(e) => handleAllocationChange(index, 'categoryName', e.target.value)} placeholder="e.g., IT Equipment" />
                      </div>
                      <div className="space-y-2">
                        <Label>Description</Label>
                        <Input value={alloc.categoryDescription || ''} onChange={(e) => handleAllocationChange(index, 'categoryDescription', e.target.value)} placeholder="Category description" />
                      </div>
                      <div className="space-y-2">
                        <Label>Allocated Amount *</Label>
                        <Input type="number" value={alloc.allocatedAmount} onChange={(e) => handleAllocationChange(index, 'allocatedAmount', parseFloat(e.target.value))} min={0} step={0.01} />
                      </div>
                      <div className="space-y-2">
                        <Label>Notes</Label>
                        <Input value={alloc.notes || ''} onChange={(e) => handleAllocationChange(index, 'notes', e.target.value)} placeholder="Notes" />
                      </div>
                      <div className="flex items-end">
                        <Button type="button" variant="destructive" size="icon" onClick={() => handleRemoveAllocation(index)}><Trash2 className="h-4 w-4" /></Button>
                      </div>
                    </div>
                  ))}
                </div>
              ) : (
                <div className="text-center py-8 text-gray-500">No allocations added. Click "Add Allocation" to add budget category allocations.</div>
              )}
            </CardContent>
          </Card>

          {/* Actions */}
          <div className="flex justify-end gap-4">
            <Button type="button" variant="outline" onClick={() => router.back()}>Cancel</Button>
            <Button type="submit" disabled={loading} className="gap-2">
              <Save className="h-4 w-4" />
              {loading ? 'Creating...' : 'Create Budget'}
            </Button>
          </div>
        </div>
      </form>
    </div>
  );
}
