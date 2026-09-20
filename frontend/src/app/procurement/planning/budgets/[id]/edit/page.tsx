'use client';

import { useState, useEffect } from 'react';
import { useRouter, useParams } from 'next/navigation';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { ArrowLeft, Save, Plus, Trash2, Loader2, Edit } from 'lucide-react';
import { toast } from 'sonner';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { procurementBudgetService, type ProcurementBudgetDetailDto, type CreateProcurementBudgetDto, type CreateProcurementBudgetAllocationDto } from '@/services/procurementPlanningService';
import { organizationUnitService } from '@/services/hr/organization-unit.service';
import type { OrganizationUnitSummary } from '@/types/hr/organization';
import { FiscalYearSelect } from '../../../components/FiscalYearSelect';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';

export default function EditProcurementBudgetPage() {
  const router = useRouter();
  const params = useParams();
  const id = Array.isArray(params?.id) ? params.id[0] : params?.id ?? '';
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [organizationUnits, setOrganizationUnits] = useState<OrganizationUnitSummary[]>([]);
  const [loadingOrganizationUnits, setLoadingOrganizationUnits] = useState(true);
  const [budget, setBudget] = useState<ProcurementBudgetDetailDto | null>(null);
  const [formData, setFormData] = useState<CreateProcurementBudgetDto>({
    title: '', description: '', organizationUnitId: '', fiscalYear: new Date().getFullYear(),
    allocatedAmount: 0, currency: 'USD', controlLevel: 'Warning', warningThresholdPercent: 80, notes: '', allocations: [],
  });

  // Allocation dialog state
  const [allocationDialogOpen, setAllocationDialogOpen] = useState(false);
  const [editingAllocation, setEditingAllocation] = useState<{ index: number; data: CreateProcurementBudgetAllocationDto } | null>(null);
  const [allocationForm, setAllocationForm] = useState<CreateProcurementBudgetAllocationDto>({ categoryName: '', categoryDescription: '', allocatedAmount: 0, notes: '' });
  const [allocationToRemove, setAllocationToRemove] = useState<number | null>(null);

  useEffect(() => {
    const fetchData = async () => {
      try {
        setLoadingOrganizationUnits(true);
        const [units, budgetData] = await Promise.all([organizationUnitService.getSummary(), procurementBudgetService.getBudgetById(id)]);
        setOrganizationUnits(units.filter((unit) => unit.isActive));
        setBudget(budgetData);
        setFormData({
          title: budgetData.title, description: budgetData.description || '', organizationUnitId: budgetData.organizationUnitId || '',
          fiscalYear: budgetData.fiscalYear, allocatedAmount: budgetData.allocatedAmount, currency: budgetData.currency,
          controlLevel: budgetData.controlLevel, warningThresholdPercent: budgetData.warningThresholdPercent,
          effectiveDate: budgetData.effectiveDate?.split('T')[0], expiryDate: budgetData.expiryDate?.split('T')[0],
          notes: budgetData.notes || '',
          allocations: budgetData.allocations?.map(a => ({ categoryName: a.categoryName, categoryDescription: a.categoryDescription, allocatedAmount: a.allocatedAmount, notes: a.notes })) || [],
        });
      } catch (error) {
        console.error('Error loading data:', error);
        toast.error('Failed to load budget details');
      } finally {
        setLoading(false);
        setLoadingOrganizationUnits(false);
      }
    };
    if (id) fetchData();
  }, [id]);

  const handleInputChange = (field: keyof CreateProcurementBudgetDto, value: string | number) => {
    setFormData(prev => ({ ...prev, [field]: value }));
  };

  const handleOpenAddAllocation = () => {
    setEditingAllocation(null);
    setAllocationForm({ categoryName: '', categoryDescription: '', allocatedAmount: 0, notes: '' });
    setAllocationDialogOpen(true);
  };

  const handleOpenEditAllocation = (index: number) => {
    const alloc = formData.allocations?.[index];
    if (alloc) {
      setEditingAllocation({ index, data: alloc });
      setAllocationForm({ ...alloc });
      setAllocationDialogOpen(true);
    }
  };

  const handleSaveAllocation = () => {
    if (!allocationForm.categoryName.trim()) { toast.error('Category name is required'); return; }
    if (allocationForm.allocatedAmount <= 0) { toast.error('Amount must be greater than 0'); return; }
    
    if (editingAllocation !== null) {
      setFormData(prev => ({
        ...prev,
        allocations: prev.allocations?.map((a, i) => i === editingAllocation.index ? allocationForm : a) || [],
      }));
    } else {
      setFormData(prev => ({ ...prev, allocations: [...(prev.allocations || []), allocationForm] }));
    }
    setAllocationDialogOpen(false);
    toast.success(editingAllocation ? 'Allocation updated' : 'Allocation added');
  };

  const handleRemoveAllocation = () => {
    if (allocationToRemove === null) return false;
    setFormData(prev => ({
      ...prev,
      allocations: prev.allocations?.filter((_, index) => index !== allocationToRemove) || [],
    }));
    toast.success('Allocation removed');
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!formData.title.trim()) { toast.error('Title is required'); return; }
    if (!formData.organizationUnitId) { toast.error('Organization unit is required'); return; }
    if (formData.allocatedAmount <= 0) { toast.error('Allocated amount must be greater than 0'); return; }

    try {
      setSaving(true);
      await procurementBudgetService.updateBudget(id, formData);
      toast.success('Budget updated successfully');
      router.push(`/procurement/planning/budgets/${id}`);
    } catch (error) {
      console.error('Error updating budget:', error);
      toast.error('Failed to update budget');
    } finally {
      setSaving(false);
    }
  };

  const formatCurrency = (amount: number, currency: string) => {
    return new Intl.NumberFormat('en-US', { style: 'currency', currency: currency || 'USD', minimumFractionDigits: 0, maximumFractionDigits: 0 }).format(amount);
  };

  if (loading) {
    return <div className="flex items-center justify-center h-96"><Loader2 className="h-8 w-8 animate-spin" /></div>;
  }
  if (!budget) {
    return <div className="text-center py-8 text-gray-500">Budget not found</div>;
  }
  if (budget.status !== 'Draft') {
    return <div className="text-center py-8 text-gray-500">Only draft budgets can be edited</div>;
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-4">
        <Button variant="ghost" size="icon" onClick={() => router.back()}><ArrowLeft className="h-5 w-5" /></Button>
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Edit Budget: {budget.budgetCode}</h1>
          <p className="text-muted-foreground">{budget.title}</p>
        </div>
      </div>

      <form onSubmit={handleSubmit}>
        <Tabs defaultValue="details" className="w-full">
          <TabsList><TabsTrigger value="details">Budget Details</TabsTrigger><TabsTrigger value="allocations">Allocations ({formData.allocations?.length || 0})</TabsTrigger></TabsList>
          
          <TabsContent value="details" className="space-y-6">
            <Card>
              <CardHeader><CardTitle>Basic Information</CardTitle></CardHeader>
              <CardContent className="space-y-4">
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <div className="space-y-2">
                    <Label htmlFor="title">Title *</Label>
                    <Input id="title" value={formData.title} onChange={(e) => handleInputChange('title', e.target.value)} required />
                  </div>
                  <div className="space-y-2">
                    <Label htmlFor="organizationUnitId">Organization unit *</Label>
                    <Select value={formData.organizationUnitId} onValueChange={(value) => handleInputChange('organizationUnitId', value)} disabled={loadingOrganizationUnits}>
                      <SelectTrigger><SelectValue placeholder="Select organization unit" /></SelectTrigger>
                      <SelectContent>
                        {organizationUnits.map((unit) => (<SelectItem key={unit.id} value={unit.id}>{unit.code ? `${unit.code} - ${unit.name}` : unit.name}</SelectItem>))}
                      </SelectContent>
                    </Select>
                  </div>
                </div>
                <div className="space-y-2">
                  <Label htmlFor="description">Description</Label>
                  <Textarea id="description" value={formData.description || ''} onChange={(e) => handleInputChange('description', e.target.value)} rows={3} />
                </div>
              </CardContent>
            </Card>
            <Card>
              <CardHeader><CardTitle>Budget Details</CardTitle></CardHeader>
              <CardContent className="space-y-4">
                <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
                  <div className="space-y-2">
                    <Label>Fiscal Year *</Label>
                    <FiscalYearSelect
                      value={formData.fiscalYear}
                      onValueChange={(year) => handleInputChange('fiscalYear', year)}
                    />
                  </div>
                  <div className="space-y-2"><Label>Allocated Amount *</Label><Input type="number" value={formData.allocatedAmount} onChange={(e) => handleInputChange('allocatedAmount', parseFloat(e.target.value))} min={0} step={0.01} /></div>
                  <div className="space-y-2">
                    <Label>Currency</Label>
                    <Select value={formData.currency} onValueChange={(value) => handleInputChange('currency', value)}>
                      <SelectTrigger><SelectValue /></SelectTrigger>
                      <SelectContent>
                        <SelectItem value="USD">USD</SelectItem><SelectItem value="EUR">EUR</SelectItem><SelectItem value="GBP">GBP</SelectItem><SelectItem value="GHS">GHS</SelectItem><SelectItem value="ETB">ETB</SelectItem>
                      </SelectContent>
                    </Select>
                  </div>
                  <div className="space-y-2">
                    <Label>Control Level</Label>
                    <Select value={formData.controlLevel} onValueChange={(value) => handleInputChange('controlLevel', value)}>
                      <SelectTrigger><SelectValue /></SelectTrigger>
                      <SelectContent><SelectItem value="Strict">Strict</SelectItem><SelectItem value="Warning">Warning</SelectItem><SelectItem value="Advisory">Advisory</SelectItem></SelectContent>
                    </Select>
                  </div>
                </div>
                <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                  <div className="space-y-2"><Label>Warning Threshold (%)</Label><Input type="number" value={formData.warningThresholdPercent} onChange={(e) => handleInputChange('warningThresholdPercent', parseFloat(e.target.value))} min={0} max={100} /></div>
                  <div className="space-y-2"><Label>Effective Date</Label><Input type="date" value={formData.effectiveDate || ''} onChange={(e) => handleInputChange('effectiveDate', e.target.value)} /></div>
                  <div className="space-y-2"><Label>Expiry Date</Label><Input type="date" value={formData.expiryDate || ''} onChange={(e) => handleInputChange('expiryDate', e.target.value)} /></div>
                </div>
                <div className="space-y-2"><Label>Notes</Label><Textarea value={formData.notes || ''} onChange={(e) => handleInputChange('notes', e.target.value)} rows={3} /></div>
              </CardContent>
            </Card>
            <div className="flex justify-end gap-4">
              <Button type="button" variant="outline" onClick={() => router.back()}>Cancel</Button>
              <Button type="submit" disabled={saving} className="gap-2"><Save className="h-4 w-4" />{saving ? 'Saving...' : 'Save Changes'}</Button>
            </div>
          </TabsContent>

          <TabsContent value="allocations">
            <Card>
              <CardHeader>
                <div className="flex items-center justify-between">
                  <div><CardTitle>Budget Allocations</CardTitle><CardDescription>Allocate budget to categories</CardDescription></div>
                  <Button type="button" variant="outline" size="sm" onClick={handleOpenAddAllocation} className="gap-2"><Plus className="h-4 w-4" />Add Allocation</Button>
                </div>
              </CardHeader>
              <CardContent>
                {formData.allocations && formData.allocations.length > 0 ? (
                  <Table>
                    <TableHeader><TableRow><TableHead>Category</TableHead><TableHead>Description</TableHead><TableHead>Amount</TableHead><TableHead>Notes</TableHead><TableHead>Actions</TableHead></TableRow></TableHeader>
                    <TableBody>
                      {formData.allocations.map((alloc, index) => (
                        <TableRow key={index}>
                          <TableCell className="font-medium">{alloc.categoryName}</TableCell>
                          <TableCell>{alloc.categoryDescription || '-'}</TableCell>
                          <TableCell>{formatCurrency(alloc.allocatedAmount, formData.currency || 'USD')}</TableCell>
                          <TableCell>{alloc.notes || '-'}</TableCell>
                          <TableCell>
                            <div className="flex gap-2">
                              <Button type="button" variant="ghost" size="sm" onClick={() => handleOpenEditAllocation(index)}><Edit className="h-4 w-4" /></Button>
                              <Button type="button" variant="ghost" size="sm" onClick={() => setAllocationToRemove(index)} className="text-red-600"><Trash2 className="h-4 w-4" /></Button>
                            </div>
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                ) : (
                  <div className="text-center py-8 text-gray-500">No allocations. Click "Add Allocation" to add.</div>
                )}
              </CardContent>
            </Card>
          </TabsContent>
        </Tabs>
      </form>

      {/* Allocation Dialog */}
      <Dialog open={allocationDialogOpen} onOpenChange={setAllocationDialogOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{editingAllocation ? 'Edit Allocation' : 'Add Allocation'}</DialogTitle>
            <DialogDescription>Define budget allocation for a category</DialogDescription>
          </DialogHeader>
          <div className="space-y-4 py-4">
            <div className="space-y-2"><Label>Category Name *</Label><Input value={allocationForm.categoryName} onChange={(e) => setAllocationForm(prev => ({ ...prev, categoryName: e.target.value }))} placeholder="e.g., IT Equipment" /></div>
            <div className="space-y-2"><Label>Description</Label><Input value={allocationForm.categoryDescription || ''} onChange={(e) => setAllocationForm(prev => ({ ...prev, categoryDescription: e.target.value }))} /></div>
            <div className="space-y-2"><Label>Allocated Amount *</Label><Input type="number" value={allocationForm.allocatedAmount} onChange={(e) => setAllocationForm(prev => ({ ...prev, allocatedAmount: parseFloat(e.target.value) }))} min={0} step={0.01} /></div>
            <div className="space-y-2"><Label>Notes</Label><Textarea value={allocationForm.notes || ''} onChange={(e) => setAllocationForm(prev => ({ ...prev, notes: e.target.value }))} rows={2} /></div>
          </div>
          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => setAllocationDialogOpen(false)}>Cancel</Button>
            <Button type="button" onClick={handleSaveAllocation}>{editingAllocation ? 'Update' : 'Add'}</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <ConfirmationDialog
        open={allocationToRemove !== null}
        onOpenChange={(open) => { if (!open) setAllocationToRemove(null); }}
        title="Remove budget allocation?"
        description="The allocation will be removed from this draft. Save the budget to persist the change."
        confirmText="Remove allocation"
        variant="destructive"
        onConfirm={handleRemoveAllocation}
      />
    </div>
  );
}
