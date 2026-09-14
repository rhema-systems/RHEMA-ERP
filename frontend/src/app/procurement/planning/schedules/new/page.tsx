'use client';

import { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Checkbox } from '@/components/ui/checkbox';
import { ArrowLeft, Save, Loader2 } from 'lucide-react';
import { toast } from 'sonner';
import { procurementScheduleService, type CreateProcurementScheduleDto } from '@/services/procurementPlanningService';
import { organizationUnitService } from '@/services/hr/organization-unit.service';
import type { OrganizationUnitSummary } from '@/types/hr/organization';

export default function NewProcurementSchedulePage() {
  const router = useRouter();
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [organizationUnits, setOrganizationUnits] = useState<OrganizationUnitSummary[]>([]);
  const [formData, setFormData] = useState<CreateProcurementScheduleDto>({
    title: '', description: '', organizationUnitId: '', scheduleType: 'Tender',
    plannedStartDate: '', plannedEndDate: '', isOptimalTiming: true, timingRationale: '',
    considerSeasonalPricing: false, seasonalNotes: '', considerCashFlow: false, cashFlowNotes: '',
    storageLimitations: '', consolidationOpportunity: false, consolidationNotes: '', notes: '',
  });

  useEffect(() => {
    const fetchOrganizationUnits = async () => {
      try {
        const units = await organizationUnitService.getSummary();
        setOrganizationUnits(units.filter((unit) => unit.isActive));
      } catch (error) {
        console.error('Error loading organization units:', error);
        toast.error('Failed to load organization units');
      } finally { setLoading(false); }
    };
    fetchOrganizationUnits();
  }, []);

  const handleInputChange = (field: keyof CreateProcurementScheduleDto, value: string | boolean) => {
    setFormData(prev => ({ ...prev, [field]: value }));
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!formData.title.trim()) { toast.error('Title is required'); return; }
    if (!formData.plannedStartDate || !formData.plannedEndDate) { toast.error('Timeline dates are required'); return; }
    try {
      setSaving(true);
      const created = await procurementScheduleService.createSchedule(formData);
      toast.success('Schedule created successfully');
      router.push(`/procurement/planning/schedules/${created.id}`);
    } catch (error) {
      console.error('Error creating schedule:', error);
      toast.error('Failed to create schedule');
    } finally { setSaving(false); }
  };

  if (loading) return <div className="flex items-center justify-center h-96"><Loader2 className="h-8 w-8 animate-spin" /></div>;

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-4">
        <Button variant="ghost" size="icon" onClick={() => router.back()}><ArrowLeft className="h-5 w-5" /></Button>
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Create Procurement Schedule</h1>
          <p className="text-muted-foreground">Define a new procurement schedule</p>
        </div>
      </div>

      <form onSubmit={handleSubmit} className="space-y-6">
        <Card>
          <CardHeader><CardTitle>Basic Information</CardTitle></CardHeader>
          <CardContent className="space-y-4">
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label htmlFor="title">Title *</Label>
                <Input id="title" value={formData.title} onChange={(e) => handleInputChange('title', e.target.value)} required />
              </div>
              <div className="space-y-2">
                <Label htmlFor="organizationUnitId">Organization unit</Label>
                <Select value={formData.organizationUnitId} onValueChange={(value) => handleInputChange('organizationUnitId', value)}>
                  <SelectTrigger><SelectValue placeholder="Select organization unit" /></SelectTrigger>
                  <SelectContent>{organizationUnits.map((unit) => (<SelectItem key={unit.id} value={unit.id}>{unit.code ? `${unit.code} - ${unit.name}` : unit.name}</SelectItem>))}</SelectContent>
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
          <CardHeader><CardTitle>Timeline</CardTitle></CardHeader>
          <CardContent className="space-y-4">
            <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
              <div className="space-y-2">
                <Label>Schedule Type</Label>
                <Select value={formData.scheduleType} onValueChange={(value) => handleInputChange('scheduleType', value)}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="Tender">Tender</SelectItem><SelectItem value="RFQ">RFQ</SelectItem>
                    <SelectItem value="DirectPurchase">Direct Purchase</SelectItem><SelectItem value="Contract">Contract</SelectItem>
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-2"><Label>Planned Start Date *</Label><Input type="date" value={formData.plannedStartDate} onChange={(e) => handleInputChange('plannedStartDate', e.target.value)} required /></div>
              <div className="space-y-2"><Label>Planned End Date *</Label><Input type="date" value={formData.plannedEndDate} onChange={(e) => handleInputChange('plannedEndDate', e.target.value)} required /></div>
            </div>
            <div className="flex items-center space-x-2">
              <Checkbox id="isOptimalTiming" checked={formData.isOptimalTiming} onCheckedChange={(checked) => handleInputChange('isOptimalTiming', checked === true)} />
              <Label htmlFor="isOptimalTiming">This is optimal timing for procurement</Label>
            </div>
            <div className="space-y-2"><Label>Timing Rationale</Label><Textarea value={formData.timingRationale || ''} onChange={(e) => handleInputChange('timingRationale', e.target.value)} rows={2} placeholder="Explain the timing decision..." /></div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader><CardTitle>Considerations</CardTitle></CardHeader>
          <CardContent className="space-y-4">
            <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
              <div className="space-y-3">
                <div className="flex items-center space-x-2"><Checkbox id="considerSeasonalPricing" checked={formData.considerSeasonalPricing} onCheckedChange={(checked) => handleInputChange('considerSeasonalPricing', checked === true)} /><Label htmlFor="considerSeasonalPricing">Consider Seasonal Pricing</Label></div>
                {formData.considerSeasonalPricing && <Textarea value={formData.seasonalNotes || ''} onChange={(e) => handleInputChange('seasonalNotes', e.target.value)} rows={2} placeholder="Seasonal pricing notes..." />}
              </div>
              <div className="space-y-3">
                <div className="flex items-center space-x-2"><Checkbox id="considerCashFlow" checked={formData.considerCashFlow} onCheckedChange={(checked) => handleInputChange('considerCashFlow', checked === true)} /><Label htmlFor="considerCashFlow">Consider Cash Flow</Label></div>
                {formData.considerCashFlow && <Textarea value={formData.cashFlowNotes || ''} onChange={(e) => handleInputChange('cashFlowNotes', e.target.value)} rows={2} placeholder="Cash flow notes..." />}
              </div>
            </div>
            <div className="space-y-2"><Label>Storage Limitations</Label><Textarea value={formData.storageLimitations || ''} onChange={(e) => handleInputChange('storageLimitations', e.target.value)} rows={2} /></div>
            <div className="space-y-3">
              <div className="flex items-center space-x-2"><Checkbox id="consolidationOpportunity" checked={formData.consolidationOpportunity} onCheckedChange={(checked) => handleInputChange('consolidationOpportunity', checked === true)} /><Label htmlFor="consolidationOpportunity">Consolidation Opportunity</Label></div>
              {formData.consolidationOpportunity && <Textarea value={formData.consolidationNotes || ''} onChange={(e) => handleInputChange('consolidationNotes', e.target.value)} rows={2} placeholder="Consolidation notes..." />}
            </div>
            <div className="space-y-2"><Label>Notes</Label><Textarea value={formData.notes || ''} onChange={(e) => handleInputChange('notes', e.target.value)} rows={3} /></div>
          </CardContent>
        </Card>

        <div className="flex justify-end gap-4">
          <Button type="button" variant="outline" onClick={() => router.back()}>Cancel</Button>
          <Button type="submit" disabled={saving} className="gap-2"><Save className="h-4 w-4" />{saving ? 'Creating...' : 'Create Schedule'}</Button>
        </div>
      </form>
    </div>
  );
}
