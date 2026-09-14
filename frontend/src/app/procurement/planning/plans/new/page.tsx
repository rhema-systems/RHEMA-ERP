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
import {
  procurementBudgetService,
  procurementPlanService,
  type CreateProcurementPlanDto,
  type ProcurementBudgetDto,
} from '@/services/procurementPlanningService';
import { organizationUnitService } from '@/services/hr/organization-unit.service';
import type { OrganizationUnitSummary } from '@/types/hr/organization';
import { FiscalYearSelect } from '../../components/FiscalYearSelect';
import { applyProcurementPlanBudgetSelection } from '../../components/procurementPlanBudgetSelection';

export default function NewProcurementPlanPage() {
  const router = useRouter();
  const [loading, setLoading] = useState(false);
  const [organizationUnits, setOrganizationUnits] = useState<OrganizationUnitSummary[]>([]);
  const [loadingOrganizationUnits, setLoadingOrganizationUnits] = useState(true);
  const [availableBudgets, setAvailableBudgets] = useState<ProcurementBudgetDto[]>([]);
  const [loadingBudgets, setLoadingBudgets] = useState(false);
  const [formData, setFormData] = useState<CreateProcurementPlanDto>({
    title: '',
    description: '',
    organizationUnitId: '',
    fiscalYear: new Date().getFullYear(),
    planningCycle: 'Annual',
    planningQuarter: '',
    planStartDate: new Date().toISOString().split('T')[0],
    planEndDate: new Date(new Date().getFullYear(), 11, 31).toISOString().split('T')[0],
    planDurationYears: 1,
    totalEstimatedBudget: 0,
    currency: 'USD',
    notes: '',
    items: [],
  });

  // Fetch the active HR organization units on mount.
  useEffect(() => {
    const fetchOrganizationUnits = async () => {
      try {
        setLoadingOrganizationUnits(true);
        const data = await organizationUnitService.getSummary();
        setOrganizationUnits(data.filter((unit) => unit.isActive));
      } catch (error) {
        console.error('Error fetching organization units:', error);
        toast.error('Failed to load organization units');
      } finally {
        setLoadingOrganizationUnits(false);
      }
    };
    fetchOrganizationUnits();
  }, []);

  useEffect(() => {
    if (!formData.organizationUnitId || !formData.fiscalYear) {
      setAvailableBudgets([]);
      return;
    }

    let active = true;
    const loadBudgets = async () => {
      try {
        setLoadingBudgets(true);
        const values = await procurementBudgetService.getAvailableBudgetsForLinking(
          formData.organizationUnitId,
          formData.fiscalYear,
          true,
        );
        if (active) setAvailableBudgets(values);
      } catch (error) {
        console.error('Error fetching selectable procurement budgets:', error);
        if (active) {
          setAvailableBudgets([]);
          toast.error('Failed to load approved budgets for the selected organization unit and fiscal year');
        }
      } finally {
        if (active) setLoadingBudgets(false);
      }
    };

    void loadBudgets();
    return () => { active = false; };
  }, [formData.organizationUnitId, formData.fiscalYear]);

  const handleInputChange = (field: keyof CreateProcurementPlanDto, value: string | number) => {
    setFormData(prev => ({
      ...prev,
      [field]: value,
      ...(field === 'organizationUnitId' || field === 'fiscalYear' ? { budgetId: undefined } : {}),
    }));
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    
    if (!formData.title.trim()) {
      toast.error('Title is required');
      return;
    }
    if (!formData.organizationUnitId) {
      toast.error('Organization unit is required');
      return;
    }

    try {
      setLoading(true);
      await procurementPlanService.createPlan(formData);
      toast.success('Procurement plan created successfully');
      router.push('/procurement/planning/plans');
    } catch (error) {
      console.error('Error creating procurement plan:', error);
      toast.error(error instanceof Error ? error.message : 'Failed to create procurement plan');
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
            Create a new organization-unit procurement plan
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
                  <Label htmlFor="organizationUnitId">Organization unit *</Label>
                  <Select
                    value={formData.organizationUnitId}
                    onValueChange={(value) => handleInputChange('organizationUnitId', value)}
                    disabled={loadingOrganizationUnits}
                  >
                    <SelectTrigger>
                      <SelectValue placeholder={loadingOrganizationUnits ? "Loading organization units..." : "Select organization unit"} />
                    </SelectTrigger>
                    <SelectContent>
                      {organizationUnits.map((unit) => (
                        <SelectItem key={unit.id} value={unit.id}>
                          {unit.code ? `${unit.code} - ${unit.name}` : unit.name}
                        </SelectItem>
                      ))}
                      {organizationUnits.length === 0 && !loadingOrganizationUnits && (
                        <div className="px-2 py-4 text-center text-sm text-muted-foreground">
                          No organization units available
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
              <div className="grid grid-cols-1 md:grid-cols-3 lg:grid-cols-6 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="fiscalYear">Fiscal Year *</Label>
                  <FiscalYearSelect
                    value={formData.fiscalYear}
                    onValueChange={(year) => handleInputChange('fiscalYear', year)}
                    autoSelectFirstAvailable
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="planningCycle">Cycle</Label>
                  <Select
                    value={formData.planningCycle || 'Annual'}
                    onValueChange={(value) => {
                      setFormData((prev) => ({
                        ...prev,
                        planningCycle: value,
                        planningQuarter: value === 'Quarterly' ? prev.planningQuarter : '',
                      }));
                    }}
                  >
                    <SelectTrigger>
                      <SelectValue placeholder="Select cycle" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="Annual">Annual</SelectItem>
                      <SelectItem value="Quarterly">Quarterly</SelectItem>
                      <SelectItem value="MultiYear">Multi-Year</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-2">
                  <Label htmlFor="planningQuarter">Quarter</Label>
                  <Select
                    value={formData.planningQuarter || 'none'}
                    onValueChange={(value) => handleInputChange('planningQuarter', value === 'none' ? '' : value)}
                    disabled={(formData.planningCycle || 'Annual') !== 'Quarterly'}
                  >
                    <SelectTrigger>
                      <SelectValue placeholder="Select quarter" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="none">Not applicable</SelectItem>
                      <SelectItem value="Q1">Q1</SelectItem>
                      <SelectItem value="Q2">Q2</SelectItem>
                      <SelectItem value="Q3">Q3</SelectItem>
                      <SelectItem value="Q4">Q4</SelectItem>
                    </SelectContent>
                  </Select>
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
                  <Label htmlFor="budgetId">Approved Budget</Label>
                  <Select
                    value={formData.budgetId || '__none__'}
                    onValueChange={(value) => {
                      setFormData((previous) =>
                        applyProcurementPlanBudgetSelection(previous, availableBudgets, value));
                    }}
                    disabled={!formData.organizationUnitId || loadingBudgets}
                  >
                    <SelectTrigger id="budgetId">
                      <SelectValue placeholder={
                        !formData.organizationUnitId
                          ? 'Select an organization unit first'
                          : loadingBudgets
                            ? 'Loading approved budgets...'
                            : 'Select approved budget'
                      } />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="__none__">Select later (draft only)</SelectItem>
                      {availableBudgets.map((budget) => (
                        <SelectItem key={budget.id} value={budget.id}>
                          {budget.budgetCode} — {budget.title} ({budget.currency} {budget.allocatedAmount.toLocaleString()})
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                  <p className="text-xs text-muted-foreground">
                    Final approval requires an approved budget for this organization unit and fiscal year. A budget may fund
                    multiple plans while its controlled planning capacity remains sufficient.
                  </p>
                </div>
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
                  <Input
                    id="currency"
                    value={formData.budgetId ? formData.currency : 'Select an approved budget'}
                    readOnly
                    aria-readonly="true"
                  />
                  <p className="text-xs text-muted-foreground">
                    Currency is inherited from the selected approved budget.
                  </p>
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
