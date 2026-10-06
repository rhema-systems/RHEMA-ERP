'use client';

import { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Separator } from '@/components/ui/separator';
import { Switch } from '@/components/ui/switch';
import { ArrowLeft, Plus, Save, Trash2, FileText, DollarSign, MapPin, Milestone } from 'lucide-react';
import { toast } from 'sonner';
import {
  salesAgreementService, type CreateSalesAgreementDto,
  type CreateSalesAgreementLineDto, type CreateSalesAgreementMilestoneDto
} from '@/services/salesAgreementService';
import { salesAllocationService } from '@/services/salesAllocationService';
import { projectService } from '@/services/projectService';
import apiService from '@/services/api.service';
import {
  parseSaleableSourceContextFromParams,
  saleableItemToContext,
  SaleableSourceQuickStart,
  type SaleableAgreementIntent,
  type SalesLinkedSourceContext,
} from '../../components/SaleableSourceQuickStart';
import type { SalesSaleableItemDto, SalesSaleableSourceDto } from '@/services/salesSetupService';

interface CustomerOption {
  id: string;
  companyName: string;
  partnerCode: string;
}

interface CrmHandoffContext {
  contextLabel?: string;
  quoteId?: string;
  quoteName?: string;
  opportunityId?: string;
  opportunityName?: string;
  leadId?: string;
  leadName?: string;
  currency?: string;
  estimatedValue?: number;
}

const buildCrmReferenceText = (context: CrmHandoffContext | null) => {
  if (!context) {
    return '';
  }

  return [
    context.contextLabel ? `CRM Context: ${context.contextLabel}` : undefined,
    context.quoteName ? `Quote: ${context.quoteName}` : undefined,
    context.opportunityName ? `Opportunity: ${context.opportunityName}` : undefined,
    context.leadName ? `Lead: ${context.leadName}` : undefined,
  ].filter(Boolean).join(' | ');
};

export default function CreateSalesAgreementPage() {
  const router = useRouter();
  const [loading, setLoading] = useState(false);
  const [linkedSourceContext, setLinkedSourceContext] = useState<SalesLinkedSourceContext | null>(null);
  const [crmHandoffContext, setCrmHandoffContext] = useState<CrmHandoffContext | null>(null);

  // Customer search
  const [customerSearch, setCustomerSearch] = useState('');
  const [customerResults, setCustomerResults] = useState<CustomerOption[]>([]);
  const [showCustomerDropdown, setShowCustomerDropdown] = useState(false);

  // Form state
  const [form, setForm] = useState({
    businessPartnerId: '',
    customerName: '',
    agreementTitle: '',
    agreementType: 'General',
    startDate: new Date().toISOString().split('T')[0],
    endDate: '',
    expiryWarningDays: 30,
    autoRenew: false,
    renewalPeriodMonths: 12,
    agreedValue: 0,
    minimumCommitment: 0,
    maximumCommitment: 0,
    currency: 'GHS',
    discountPercentage: 0,
    pricingTerms: '',
    paymentSchedule: '',
    propertyReference: '',
    propertyType: '',
    propertyDescription: '',
    propertyLocation: '',
    notes: '',
    internalNotes: '',
    termsAndConditions: '',
  });

  // Lines
  const [lines, setLines] = useState<CreateSalesAgreementLineDto[]>([]);

  // Milestones
  const [milestones, setMilestones] = useState<CreateSalesAgreementMilestoneDto[]>([]);

  // Customer search
  useEffect(() => {
    if (customerSearch.length < 2) { setCustomerResults([]); return; }
    const timer = setTimeout(async () => {
      try {
        const res = await apiService.get<any>(`/api/business-partners?search=${encodeURIComponent(customerSearch)}&pageSize=10`);
        const items = res?.items || res?.data?.items || [];
        setCustomerResults(items.map((p: any) => ({ id: p.id, companyName: p.companyName || p.partnerCode, partnerCode: p.partnerCode })));
        setShowCustomerDropdown(true);
      } catch { setCustomerResults([]); }
    }, 300);
    return () => clearTimeout(timer);
  }, [customerSearch]);

  useEffect(() => {
    if (typeof window === 'undefined') {
      return;
    }

    const params = new URLSearchParams(window.location.search);
    const customerId = params.get('customerId');
    const customerName = params.get('customerName');
    const propertyReference = params.get('propertyReference');
    const agreementType = params.get('agreementType');
    const sourceContext = parseSaleableSourceContextFromParams(params);
    const crmContext: CrmHandoffContext = {
      contextLabel: params.get('crmContext') || undefined,
      quoteId: params.get('quoteId') || undefined,
      quoteName: params.get('quoteName') || undefined,
      opportunityId: params.get('opportunityId') || undefined,
      opportunityName: params.get('opportunityName') || undefined,
      leadId: params.get('leadId') || undefined,
      leadName: params.get('leadName') || undefined,
      currency: params.get('currency') || undefined,
      estimatedValue: params.get('estimatedValue') ? Number(params.get('estimatedValue')) : undefined,
    };
    const hasCrmContext = Object.values(crmContext).some((value) => value !== undefined && value !== null && value !== '');

    if (sourceContext) {
      setLinkedSourceContext(sourceContext);
    }

    if (hasCrmContext) {
      setCrmHandoffContext(crmContext);
    }

    setForm((current) => ({
      ...current,
      businessPartnerId: customerId || current.businessPartnerId,
      customerName: customerName || current.customerName,
      agreementType: agreementType || current.agreementType,
      propertyReference: propertyReference || current.propertyReference,
      agreementTitle: sourceContext?.itemName
        ? `${sourceContext.itemName} ${agreementType === 'LeaseAgreement' ? 'Lease' : 'Agreement'}`
        : crmContext.quoteName
          ? `${crmContext.quoteName} Agreement`
          : crmContext.opportunityName
            ? `${crmContext.opportunityName} Agreement`
        : current.agreementTitle,
      agreedValue: sourceContext?.estimatedValue ?? crmContext.estimatedValue ?? current.agreedValue,
      minimumCommitment: sourceContext?.estimatedValue ?? crmContext.estimatedValue ?? current.minimumCommitment,
      maximumCommitment: sourceContext?.estimatedValue ?? crmContext.estimatedValue ?? current.maximumCommitment,
      currency: sourceContext?.currency || crmContext.currency || current.currency,
      internalNotes: current.internalNotes || (hasCrmContext ? buildCrmReferenceText(crmContext) : current.internalNotes),
    }));

    if (customerName) {
      setCustomerSearch(customerName);
    }
  }, []);

  const applySaleableItem = (
    item: SalesSaleableItemDto,
    source: SalesSaleableSourceDto,
    intent: SaleableAgreementIntent,
  ) => {
    const isLeaseIntent = intent === 'lease';
    const context = saleableItemToContext(item, source);
    setLinkedSourceContext(context);
    setForm((current) => ({
      ...current,
      businessPartnerId: item.customerId || current.businessPartnerId,
      customerName: item.customerName || current.customerName,
      propertyReference: item.propertyReference || current.propertyReference,
      propertyType: item.itemType || current.propertyType,
      propertyDescription: current.propertyDescription || item.itemName,
      agreedValue: item.estimatedValue ?? current.agreedValue,
      minimumCommitment: item.estimatedValue ?? current.minimumCommitment,
      maximumCommitment: item.estimatedValue ?? current.maximumCommitment,
      currency: item.currency || source.defaultCurrency || current.currency,
      agreementType: isLeaseIntent
        ? item.suggestedLeaseAgreementType || current.agreementType
        : item.suggestedAgreementType || current.agreementType,
      agreementTitle: current.agreementTitle || (isLeaseIntent
        ? `${item.itemName} Lease`
        : `${item.itemName} Agreement`),
    }));
    if (item.customerName) {
      setCustomerSearch(item.customerName);
    }
    setLines((current) => {
      const nextLine: CreateSalesAgreementLineDto = {
        description: item.propertyReference ? `${item.itemName} - ${item.propertyReference}` : item.itemName,
        productCode: item.itemCode,
        agreedPrice: item.estimatedValue || 0,
        minimumQuantity: 1,
        maximumQuantity: 1,
        unit: item.areaSquareMeters ? 'Unit' : 'EA',
        discountPercentage: 0,
        notes: source.displayName,
      };

      if (current.length === 0) {
        return [nextLine];
      }

      return current.map((line, index) => index === 0 ? { ...line, ...nextLine } : line);
    });
  };

  const selectCustomer = (c: CustomerOption) => {
    setForm({ ...form, businessPartnerId: c.id, customerName: c.companyName });
    setCustomerSearch(c.companyName);
    setShowCustomerDropdown(false);
  };

  // Line management
  const addLine = () => setLines([...lines, { description: '', agreedPrice: 0, minimumQuantity: 0, maximumQuantity: 0, unit: 'EA', discountPercentage: 0, notes: '' }]);
  const updateLine = (i: number, field: string, value: any) => { const u = [...lines]; (u[i] as any)[field] = value; setLines(u); };
  const removeLine = (i: number) => setLines(lines.filter((_, idx) => idx !== i));

  // Milestone management
  const addMilestone = () => setMilestones([...milestones, { milestoneName: '', paymentPercentage: 0, paymentAmount: 0 }]);
  const updateMilestone = (i: number, field: string, value: any) => { const u = [...milestones]; (u[i] as any)[field] = value; setMilestones(u); };
  const removeMilestone = (i: number) => setMilestones(milestones.filter((_, idx) => idx !== i));

  // Auto-calculate milestone amounts from percentage
  const recalcMilestoneAmounts = () => {
    const updated = milestones.map(m => ({ ...m, paymentAmount: (m.paymentPercentage / 100) * form.agreedValue }));
    setMilestones(updated);
  };

  const handleSubmit = async () => {
    if (!form.businessPartnerId) { toast.error('Please select a customer'); return; }
    if (!form.agreementTitle) { toast.error('Please enter an agreement title'); return; }
    if (!form.startDate) { toast.error('Please enter a start date'); return; }

    try {
      setLoading(true);
      if (linkedSourceContext?.sourceId && linkedSourceContext.sourceItemId) {
        const activeCheck = await salesAllocationService.hasActiveAllocation(
          linkedSourceContext.sourceId,
          linkedSourceContext.sourceItemId,
        );
        if (activeCheck.hasActiveAllocation) {
          toast.error('This saleable item already has an active reservation or allocation.');
          return;
        }
      }

      const data: CreateSalesAgreementDto = {
        businessPartnerId: form.businessPartnerId,
        agreementTitle: form.agreementTitle,
        agreementType: form.agreementType,
        startDate: form.startDate,
        endDate: form.endDate || undefined,
        expiryWarningDays: form.expiryWarningDays,
        autoRenew: form.autoRenew,
        renewalPeriodMonths: form.autoRenew ? form.renewalPeriodMonths : undefined,
        agreedValue: form.agreedValue,
        minimumCommitment: form.minimumCommitment,
        maximumCommitment: form.maximumCommitment,
        currency: form.currency,
        discountPercentage: form.discountPercentage || undefined,
        pricingTerms: form.pricingTerms || undefined,
        paymentSchedule: form.paymentSchedule || undefined,
        propertyReference: form.propertyReference || undefined,
        propertyType: form.propertyType || undefined,
        propertyDescription: form.propertyDescription || undefined,
        propertyLocation: form.propertyLocation || undefined,
        notes: form.notes || undefined,
        internalNotes: form.internalNotes || undefined,
        termsAndConditions: form.termsAndConditions || undefined,
        lines: lines.length > 0 ? lines : undefined,
        milestones: milestones.length > 0 ? milestones : undefined,
      };

      const result = await salesAgreementService.createAgreement(data);
      if (linkedSourceContext?.projectUnitId) {
        try {
          await projectService.linkSalesAgreementToProjectUnit(linkedSourceContext.projectUnitId, result.id);
        } catch (linkError: any) {
          toast.warning(linkError?.message || 'Agreement created, but the project unit could not be linked automatically.');
        }
      }
      if (linkedSourceContext?.sourceId && linkedSourceContext.sourceItemId) {
        try {
          await salesAllocationService.createAllocation({
            saleableSourceId: linkedSourceContext.sourceId,
            sourceItemId: linkedSourceContext.sourceItemId,
            sourceItemCode: linkedSourceContext.itemCode || linkedSourceContext.projectUnitCode,
            sourceItemName: linkedSourceContext.itemName || linkedSourceContext.projectUnitName || form.agreementTitle,
            sourceItemType: linkedSourceContext.itemType || form.propertyType || undefined,
            businessPartnerId: form.businessPartnerId,
            customerName: form.customerName || linkedSourceContext.customerName,
            salesAgreementId: result.id,
            allocationType: form.agreementType === 'LeaseAgreement' ? 'Lease' : 'Reservation',
            status: 'Reserved',
            estimatedValue: linkedSourceContext.estimatedValue ?? undefined,
            agreedValue: form.agreedValue,
            currency: form.currency,
            notes: `Reserved from Sales Agreement ${result.documentNumber || result.id}`,
          });
        } catch (allocationError: any) {
          toast.warning(allocationError?.message || 'Agreement created, but the saleable item reservation could not be recorded.');
        }
      }
      toast.success('Agreement created successfully');
      router.push(`/sales/agreements/${result.id}`);
    } catch (error: any) {
      toast.error(error.message || 'Failed to create agreement');
    } finally {
      setLoading(false);
    }
  };

  const isTDCType = ['LeaseAgreement', 'TenancyAgreement', 'PlotAllocation'].includes(form.agreementType);

  return (
    <div className="container mx-auto py-6 space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-4">
          <Button variant="outline" size="icon" onClick={() => router.push('/sales/agreements')}><ArrowLeft className="h-4 w-4" /></Button>
          <div>
            <h1 className="text-2xl font-bold flex items-center gap-2"><FileText className="h-6 w-6 text-purple-600" />New Sales Agreement</h1>
            <p className="text-gray-500">Create a new customer agreement or contract</p>
          </div>
        </div>
        <Button onClick={handleSubmit} disabled={loading}><Save className="h-4 w-4 mr-2" />{loading ? 'Saving...' : 'Save Draft'}</Button>
      </div>

      <SaleableSourceQuickStart
        mode="agreement"
        linkedContext={linkedSourceContext}
        onUseAgreement={applySaleableItem}
      />

      {crmHandoffContext ? (
        <Card className="border-blue-200 bg-blue-50/60">
          <CardContent className="flex flex-wrap items-center justify-between gap-3 py-3 text-sm">
            <div>
              <div className="font-medium text-blue-900">Started from CRM</div>
              <div className="text-blue-700">
                {buildCrmReferenceText(crmHandoffContext) || 'CRM context will be carried into this agreement.'}
              </div>
            </div>
            {crmHandoffContext.estimatedValue ? (
              <div className="font-medium text-blue-900">
                {(crmHandoffContext.currency || form.currency)} {crmHandoffContext.estimatedValue.toLocaleString(undefined, { minimumFractionDigits: 2 })}
              </div>
            ) : null}
          </CardContent>
        </Card>
      ) : null}

      {/* Customer & Type */}
      <Card>
        <CardHeader><CardTitle>Agreement Details</CardTitle></CardHeader>
        <CardContent className="space-y-4">
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div className="relative">
              <Label>Customer *</Label>
              <Input placeholder="Search customer..." value={customerSearch}
                onChange={(e) => { setCustomerSearch(e.target.value); setForm({ ...form, businessPartnerId: '', customerName: '' }); }}
                onFocus={() => customerResults.length > 0 && setShowCustomerDropdown(true)} />
              {showCustomerDropdown && customerResults.length > 0 && (
                <div className="absolute z-50 mt-1 w-full bg-white rounded-md shadow-lg border max-h-48 overflow-auto">
                  {customerResults.map(c => (
                    <div key={c.id} className="px-3 py-2 hover:bg-gray-100 cursor-pointer" onClick={() => selectCustomer(c)}>
                      <p className="font-medium">{c.companyName}</p><p className="text-xs text-gray-500">{c.partnerCode}</p>
                    </div>
                  ))}
                </div>
              )}
              {form.businessPartnerId && <p className="text-xs text-green-600 mt-1">✓ {form.customerName}</p>}
            </div>
            <div>
              <Label>Agreement Title *</Label>
              <Input placeholder="e.g., Annual lease - Block A, Unit 3" value={form.agreementTitle}
                onChange={(e) => setForm({ ...form, agreementTitle: e.target.value })} />
            </div>
          </div>
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            <div>
              <Label>Agreement Type</Label>
              <Select value={form.agreementType} onValueChange={(v) => setForm({ ...form, agreementType: v })}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  <SelectItem value="General">General</SelectItem>
                  <SelectItem value="VolumeBased">Volume Based</SelectItem>
                  <SelectItem value="PriceLock">Price Lock</SelectItem>
                  <SelectItem value="LeaseAgreement">Lease Agreement</SelectItem>
                  <SelectItem value="TenancyAgreement">Tenancy Agreement</SelectItem>
                  <SelectItem value="PlotAllocation">Plot Allocation</SelectItem>
                  <SelectItem value="ServiceLevel">Service Level</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div><Label>Start Date *</Label><Input type="date" value={form.startDate} onChange={(e) => setForm({ ...form, startDate: e.target.value })} /></div>
            <div><Label>End Date</Label><Input type="date" value={form.endDate} onChange={(e) => setForm({ ...form, endDate: e.target.value })} /></div>
          </div>
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            <div><Label>Expiry Warning (days)</Label><Input type="number" value={form.expiryWarningDays} onChange={(e) => setForm({ ...form, expiryWarningDays: parseInt(e.target.value) || 30 })} /></div>
            <div className="flex items-center gap-3 pt-6">
              <Switch checked={form.autoRenew} onCheckedChange={(v) => setForm({ ...form, autoRenew: v })} /><Label>Auto-Renew</Label>
            </div>
            {form.autoRenew && <div><Label>Renewal Period (months)</Label><Input type="number" value={form.renewalPeriodMonths} onChange={(e) => setForm({ ...form, renewalPeriodMonths: parseInt(e.target.value) || 12 })} /></div>}
          </div>
        </CardContent>
      </Card>

      {/* Financial */}
      <Card>
        <CardHeader><CardTitle className="flex items-center gap-2"><DollarSign className="h-5 w-5" />Financial Terms</CardTitle></CardHeader>
        <CardContent>
          <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
            <div><Label>Agreed Value (GHS) *</Label><Input type="number" value={form.agreedValue} onChange={(e) => setForm({ ...form, agreedValue: parseFloat(e.target.value) || 0 })} /></div>
            <div><Label>Min Commitment</Label><Input type="number" value={form.minimumCommitment} onChange={(e) => setForm({ ...form, minimumCommitment: parseFloat(e.target.value) || 0 })} /></div>
            <div><Label>Max Commitment</Label><Input type="number" value={form.maximumCommitment} onChange={(e) => setForm({ ...form, maximumCommitment: parseFloat(e.target.value) || 0 })} /></div>
            <div><Label>Discount %</Label><Input type="number" step="0.01" value={form.discountPercentage} onChange={(e) => setForm({ ...form, discountPercentage: parseFloat(e.target.value) || 0 })} /></div>
          </div>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4 mt-4">
            <div><Label>Pricing Terms</Label><Textarea placeholder="e.g., Fixed price for 12 months..." value={form.pricingTerms} onChange={(e) => setForm({ ...form, pricingTerms: e.target.value })} /></div>
            <div><Label>Payment Schedule</Label><Textarea placeholder="e.g., Monthly on 1st..." value={form.paymentSchedule} onChange={(e) => setForm({ ...form, paymentSchedule: e.target.value })} /></div>
          </div>
        </CardContent>
      </Card>

      {/* Property Section (TDC) */}
      {isTDCType && (
        <Card>
          <CardHeader><CardTitle className="flex items-center gap-2"><MapPin className="h-5 w-5" />Property Details</CardTitle><CardDescription>TDC property information for this agreement</CardDescription></CardHeader>
          <CardContent>
            <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
              <div><Label>Property Reference</Label><Input placeholder="e.g., BLK-A-UNIT-3" value={form.propertyReference} onChange={(e) => setForm({ ...form, propertyReference: e.target.value })} /></div>
              <div>
                <Label>Property Type</Label>
                <Select value={form.propertyType || '_none'} onValueChange={(v) => setForm({ ...form, propertyType: v === '_none' ? '' : v })}>
                  <SelectTrigger><SelectValue placeholder="Select type" /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="_none">Select type</SelectItem>
                    <SelectItem value="ResidentialHouse">Residential House</SelectItem>
                    <SelectItem value="ResidentialApartment">Residential Apartment</SelectItem>
                    <SelectItem value="ServicedPlot">Serviced Plot</SelectItem>
                    <SelectItem value="CommercialUnit">Commercial Unit</SelectItem>
                    <SelectItem value="IndustrialUnit">Industrial Unit</SelectItem>
                    <SelectItem value="MixedUse">Mixed Use</SelectItem>
                    <SelectItem value="ShortTermRental">Short Term Rental</SelectItem>
                    <SelectItem value="Land">Land</SelectItem>
                  </SelectContent>
                </Select>
              </div>
              <div><Label>Location</Label><Input placeholder="e.g., Tema Community 25" value={form.propertyLocation} onChange={(e) => setForm({ ...form, propertyLocation: e.target.value })} /></div>
            </div>
            <div className="mt-4"><Label>Property Description</Label><Textarea placeholder="Describe the property..." value={form.propertyDescription} onChange={(e) => setForm({ ...form, propertyDescription: e.target.value })} /></div>
          </CardContent>
        </Card>
      )}

      {/* Agreement Lines */}
      <Card>
        <CardHeader>
          <div className="flex items-center justify-between">
            <div><CardTitle>Agreement Lines</CardTitle><CardDescription>Products/services covered by this agreement</CardDescription></div>
            <Button variant="outline" onClick={addLine}><Plus className="h-4 w-4 mr-2" />Add Line</Button>
          </div>
        </CardHeader>
        <CardContent>
          {lines.length === 0 ? (
            <p className="text-center text-gray-500 py-4">No lines added. Click "Add Line" to add items.</p>
          ) : (
            <div className="space-y-4">
              {lines.map((line, i) => (
                <div key={i} className="border rounded-lg p-4 space-y-3">
                  <div className="flex items-center justify-between">
                    <span className="font-semibold text-sm text-gray-500">Line {i + 1}</span>
                    <Button variant="ghost" size="sm" onClick={() => removeLine(i)}><Trash2 className="h-4 w-4 text-red-500" /></Button>
                  </div>
                  <div className="grid grid-cols-1 md:grid-cols-4 gap-3">
                    <div className="md:col-span-2"><Label>Description *</Label>
                      <Input placeholder="Product/Service description" value={line.description} onChange={(e) => updateLine(i, 'description', e.target.value)} /></div>
                    <div><Label>Product Code</Label><Input placeholder="Optional" value={line.productCode || ''} onChange={(e) => updateLine(i, 'productCode', e.target.value)} /></div>
                    <div><Label>Unit</Label><Input placeholder="EA" value={line.unit || ''} onChange={(e) => updateLine(i, 'unit', e.target.value)} /></div>
                  </div>
                  <div className="grid grid-cols-1 md:grid-cols-4 gap-3">
                    <div><Label>Agreed Price</Label><Input type="number" value={line.agreedPrice} onChange={(e) => updateLine(i, 'agreedPrice', parseFloat(e.target.value) || 0)} /></div>
                    <div><Label>Min Quantity</Label><Input type="number" value={line.minimumQuantity || 0} onChange={(e) => updateLine(i, 'minimumQuantity', parseFloat(e.target.value) || 0)} /></div>
                    <div><Label>Max Quantity</Label><Input type="number" value={line.maximumQuantity || 0} onChange={(e) => updateLine(i, 'maximumQuantity', parseFloat(e.target.value) || 0)} /></div>
                    <div><Label>Discount %</Label><Input type="number" step="0.01" value={line.discountPercentage || 0} onChange={(e) => updateLine(i, 'discountPercentage', parseFloat(e.target.value) || 0)} /></div>
                  </div>
                </div>
              ))}
            </div>
          )}
        </CardContent>
      </Card>

      {/* Payment Milestones */}
      <Card>
        <CardHeader>
          <div className="flex items-center justify-between">
            <div><CardTitle className="flex items-center gap-2"><Milestone className="h-5 w-5" />Payment Milestones</CardTitle>
              <CardDescription>Define staged payment schedule (e.g., deposit → foundation → handover)</CardDescription></div>
            <div className="flex gap-2">
              {milestones.length > 0 && form.agreedValue > 0 && <Button variant="outline" size="sm" onClick={recalcMilestoneAmounts}>Recalc Amounts</Button>}
              <Button variant="outline" onClick={addMilestone}><Plus className="h-4 w-4 mr-2" />Add Milestone</Button>
            </div>
          </div>
        </CardHeader>
        <CardContent>
          {milestones.length === 0 ? (
            <p className="text-center text-gray-500 py-4">No milestones added. Click "Add Milestone" for staged payments.</p>
          ) : (
            <div className="space-y-4">
              {milestones.map((m, i) => (
                <div key={i} className="border rounded-lg p-4 space-y-3">
                  <div className="flex items-center justify-between">
                    <span className="font-semibold text-sm text-gray-500">Milestone {i + 1}</span>
                    <Button variant="ghost" size="sm" onClick={() => removeMilestone(i)}><Trash2 className="h-4 w-4 text-red-500" /></Button>
                  </div>
                  <div className="grid grid-cols-1 md:grid-cols-5 gap-3">
                    <div className="md:col-span-2"><Label>Name *</Label>
                      <Input placeholder="e.g., Initial Deposit" value={m.milestoneName} onChange={(e) => updateMilestone(i, 'milestoneName', e.target.value)} /></div>
                    <div><Label>Payment %</Label><Input type="number" value={m.paymentPercentage} onChange={(e) => updateMilestone(i, 'paymentPercentage', parseFloat(e.target.value) || 0)} /></div>
                    <div><Label>Amount (GHS)</Label><Input type="number" value={m.paymentAmount} onChange={(e) => updateMilestone(i, 'paymentAmount', parseFloat(e.target.value) || 0)} /></div>
                    <div><Label>Due Date</Label><Input type="date" value={m.dueDate || ''} onChange={(e) => updateMilestone(i, 'dueDate', e.target.value)} /></div>
                  </div>
                  <div><Label>Description</Label><Input placeholder="Optional details..." value={m.description || ''} onChange={(e) => updateMilestone(i, 'description', e.target.value)} /></div>
                </div>
              ))}
              {milestones.length > 0 && (
                <div className="flex justify-between text-sm p-3 bg-purple-50 rounded-lg">
                  <span className="font-semibold">Total: {milestones.reduce((s, m) => s + m.paymentPercentage, 0).toFixed(1)}%</span>
                  <span className="font-semibold">GHS {milestones.reduce((s, m) => s + m.paymentAmount, 0).toLocaleString(undefined, { minimumFractionDigits: 2 })}</span>
                </div>
              )}
            </div>
          )}
        </CardContent>
      </Card>

      {/* Notes */}
      <Card>
        <CardHeader><CardTitle>Notes & Terms</CardTitle></CardHeader>
        <CardContent>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div><Label>Notes</Label><Textarea placeholder="Customer-facing notes..." value={form.notes} onChange={(e) => setForm({ ...form, notes: e.target.value })} /></div>
            <div><Label>Internal Notes</Label><Textarea placeholder="Internal use only..." value={form.internalNotes} onChange={(e) => setForm({ ...form, internalNotes: e.target.value })} /></div>
          </div>
          <div className="mt-4"><Label>Terms & Conditions</Label><Textarea className="min-h-[100px]" placeholder="Agreement terms and conditions..." value={form.termsAndConditions} onChange={(e) => setForm({ ...form, termsAndConditions: e.target.value })} /></div>
        </CardContent>
      </Card>

      {/* Save */}
      <div className="flex justify-end gap-3">
        <Button variant="outline" onClick={() => router.push('/sales/agreements')}>Cancel</Button>
        <Button onClick={handleSubmit} disabled={loading} className="bg-purple-600 hover:bg-purple-700"><Save className="h-4 w-4 mr-2" />{loading ? 'Saving...' : 'Create Agreement'}</Button>
      </div>
    </div>
  );
}
