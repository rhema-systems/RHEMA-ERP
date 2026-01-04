'use client';

import { useEffect, useState } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { ArrowLeft, FileSignature, Save, Loader2, AlertCircle } from 'lucide-react';
import { toast } from 'sonner';
import { contractService, type CreateContractDto } from '@/services/contractService';
import * as tenderAwardService from '@/services/tenderAwardService';
import { type TenderAwardDto } from '@/services/tenderAwardService';
import { format } from 'date-fns';
import { Suspense } from 'react';

function CreateContractForm() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const awardId = searchParams.get('awardId');

  const [award, setAward] = useState<TenderAwardDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [existingContractId, setExistingContractId] = useState<string | null>(null);

  // Form state
  const [formData, setFormData] = useState<CreateContractDto>({
    tenderAwardId: '',
    contractTitle: '',
    contractType: 'Service',
    contractValue: 0,
    currency: 'USD',
    paymentTerms: 'Net 30',
    retentionPercentage: 5,
    startDate: format(new Date(), 'yyyy-MM-dd'),
    endDate: '',
    durationDays: 365,
    warrantyPeriodDays: 90,
    scopeOfWork: '',
    deliverables: '',
    specialConditions: '',
    penaltyClause: '',
    notes: '',
  });

  useEffect(() => {
    if (awardId) {
      loadAwardData(awardId);
    } else {
      setLoading(false);
    }
  }, [awardId]);

  const loadAwardData = async (id: string) => {
    try {
      setLoading(true);
      const awardData = await tenderAwardService.getAwardById(id);
      setAward(awardData);

      // Check if contract already exists for this award
      const existingContract = await contractService.getContractByAwardId(id);
      if (existingContract) {
        setExistingContractId(existingContract.id);
      } else {
        // Pre-fill form with award data
        setFormData(prev => ({
          ...prev,
          tenderAwardId: id,
          contractTitle: `Contract for ${awardData.tenderTitle}`,
          contractValue: awardData.awardedAmount,
          currency: awardData.currency || 'USD',
        }));
      }
    } catch (error) {
      console.error('Error loading award:', error);
      toast.error('Failed to load award details');
    } finally {
      setLoading(false);
    }
  };

  const handleInputChange = (field: keyof CreateContractDto, value: string | number) => {
    setFormData(prev => ({ ...prev, [field]: value }));
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    if (!formData.contractTitle.trim()) {
      toast.error('Contract title is required');
      return;
    }
    if (formData.contractValue <= 0) {
      toast.error('Contract value must be greater than zero');
      return;
    }
    if (!formData.startDate) {
      toast.error('Start date is required');
      return;
    }
    if (!formData.endDate) {
      toast.error('End date is required');
      return;
    }
    if (formData.startDate && formData.endDate && new Date(formData.endDate) <= new Date(formData.startDate)) {
      toast.error('End date must be after start date');
      return;
    }

    try {
      setSaving(true);
      const contract = await contractService.createContract(formData);
      toast.success('Contract created successfully');
      router.push(`/procurement/contracts/${contract.id}`);
    } catch (error: any) {
      console.error('Error creating contract:', error);
      toast.error(error.message || 'Failed to create contract');
    } finally {
      setSaving(false);
    }
  };

  if (loading) {
    return (
      <div className="container mx-auto py-6">
        <div className="text-center py-12">
          <Loader2 className="h-12 w-12 animate-spin mx-auto mb-4 text-blue-500" />
          <p className="text-gray-500">Loading award details...</p>
        </div>
      </div>
    );
  }

  if (!awardId) {
    return (
      <div className="container mx-auto py-6">
        <div className="text-center py-12">
          <AlertCircle className="h-12 w-12 mx-auto mb-4 text-yellow-500" />
          <p className="text-gray-500 mb-4">No award ID provided. Contracts must be created from an award.</p>
          <Button variant="outline" onClick={() => router.push('/procurement/awards')}>
            <ArrowLeft className="h-4 w-4 mr-2" />Go to Awards
          </Button>
        </div>
      </div>
    );
  }

  if (existingContractId) {
    return (
      <div className="container mx-auto py-6">
        <div className="text-center py-12">
          <FileSignature className="h-12 w-12 mx-auto mb-4 text-blue-500" />
          <p className="text-gray-500 mb-4">A contract already exists for this award.</p>
          <Button onClick={() => router.push(`/procurement/contracts/${existingContractId}`)}>
            <FileSignature className="h-4 w-4 mr-2" />View Existing Contract
          </Button>
        </div>
      </div>
    );
  }

  return (
    <div className="container mx-auto py-6 space-y-6">
      {/* Header */}
      <div className="flex items-center gap-4">
        <Button variant="outline" onClick={() => router.back()}>
          <ArrowLeft className="h-4 w-4 mr-2" />Back
        </Button>
        <div>
          <h1 className="text-2xl font-bold flex items-center gap-2">
            <FileSignature className="h-6 w-6 text-blue-600" />
            Create Contract
          </h1>
          <p className="text-gray-500">Create a new contract from award</p>
        </div>
      </div>

      {/* Award Info Card */}
      {award && (
        <Card className="bg-blue-50 border-blue-200">
          <CardHeader className="pb-2">
            <CardTitle className="text-sm text-blue-700">Award Information</CardTitle>
          </CardHeader>
          <CardContent>
            <div className="grid grid-cols-2 md:grid-cols-4 gap-4 text-sm">
              <div><span className="text-gray-500">Tender:</span> <span className="font-medium">{award.tenderNumber}</span></div>
              <div><span className="text-gray-500">Business Partner:</span> <span className="font-medium">{award.businessPartnerName}</span></div>
              <div><span className="text-gray-500">Award Amount:</span> <span className="font-medium text-green-600">{award.currency} {award.awardedAmount.toLocaleString()}</span></div>
              <div><span className="text-gray-500">Award Date:</span> <span className="font-medium">{format(new Date(award.awardDate), 'dd MMM yyyy')}</span></div>
            </div>
          </CardContent>
        </Card>
      )}

      {/* Form */}
      <form onSubmit={handleSubmit}>
        <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
          {/* Basic Info */}
          <Card>
            <CardHeader>
              <CardTitle>Contract Details</CardTitle>
              <CardDescription>Basic contract information</CardDescription>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="space-y-2">
                <Label htmlFor="contractTitle">Contract Title *</Label>
                <Input id="contractTitle" value={formData.contractTitle} onChange={(e) => handleInputChange('contractTitle', e.target.value)} placeholder="Enter contract title" required />
              </div>
              <div className="space-y-2">
                <Label htmlFor="contractType">Contract Type</Label>
                <Select value={formData.contractType} onValueChange={(v) => handleInputChange('contractType', v)}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="Service">Service</SelectItem>
                    <SelectItem value="Supply">Supply</SelectItem>
                    <SelectItem value="Works">Works</SelectItem>
                    <SelectItem value="Consultancy">Consultancy</SelectItem>
                    <SelectItem value="Framework">Framework Agreement</SelectItem>
                  </SelectContent>
                </Select>
              </div>
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="contractValue">Contract Value *</Label>
                  <Input id="contractValue" type="number" step="0.01" value={formData.contractValue} onChange={(e) => handleInputChange('contractValue', parseFloat(e.target.value) || 0)} required />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="currency">Currency</Label>
                  <Select value={formData.currency} onValueChange={(v) => handleInputChange('currency', v)}>
                    <SelectTrigger><SelectValue /></SelectTrigger>
                    <SelectContent>
                      <SelectItem value="USD">USD</SelectItem>
                      <SelectItem value="EUR">EUR</SelectItem>
                      <SelectItem value="GBP">GBP</SelectItem>
                      <SelectItem value="AED">AED</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
              </div>
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="paymentTerms">Payment Terms</Label>
                  <Select value={formData.paymentTerms || ''} onValueChange={(v) => handleInputChange('paymentTerms', v)}>
                    <SelectTrigger><SelectValue /></SelectTrigger>
                    <SelectContent>
                      <SelectItem value="Net 30">Net 30</SelectItem>
                      <SelectItem value="Net 60">Net 60</SelectItem>
                      <SelectItem value="Net 90">Net 90</SelectItem>
                      <SelectItem value="Milestone-based">Milestone-based</SelectItem>
                      <SelectItem value="Progress Payment">Progress Payment</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-2">
                  <Label htmlFor="retentionPercentage">Retention %</Label>
                  <Input id="retentionPercentage" type="number" step="0.1" min="0" max="100" value={formData.retentionPercentage} onChange={(e) => handleInputChange('retentionPercentage', parseFloat(e.target.value) || 0)} />
                </div>
              </div>
            </CardContent>
          </Card>

          {/* Timeline */}
          <Card>
            <CardHeader>
              <CardTitle>Timeline</CardTitle>
              <CardDescription>Contract duration and warranty</CardDescription>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="startDate">Start Date *</Label>
                  <Input id="startDate" type="date" value={formData.startDate || ''} onChange={(e) => handleInputChange('startDate', e.target.value)} required />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="endDate">End Date *</Label>
                  <Input id="endDate" type="date" value={formData.endDate || ''} onChange={(e) => handleInputChange('endDate', e.target.value)} required />
                </div>
              </div>
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="durationDays">Duration (Days)</Label>
                  <Input id="durationDays" type="number" value={formData.durationDays || ''} onChange={(e) => handleInputChange('durationDays', parseInt(e.target.value) || 0)} />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="warrantyPeriodDays">Warranty Period (Days)</Label>
                  <Input id="warrantyPeriodDays" type="number" value={formData.warrantyPeriodDays || ''} onChange={(e) => handleInputChange('warrantyPeriodDays', parseInt(e.target.value) || 0)} />
                </div>
              </div>
            </CardContent>
          </Card>

          {/* Scope & Deliverables */}
          <Card className="md:col-span-2">
            <CardHeader>
              <CardTitle>Scope & Conditions</CardTitle>
              <CardDescription>Contract scope, deliverables, and special conditions</CardDescription>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="scopeOfWork">Scope of Work</Label>
                  <Textarea id="scopeOfWork" rows={4} value={formData.scopeOfWork || ''} onChange={(e) => handleInputChange('scopeOfWork', e.target.value)} placeholder="Describe the scope of work..." />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="deliverables">Deliverables</Label>
                  <Textarea id="deliverables" rows={4} value={formData.deliverables || ''} onChange={(e) => handleInputChange('deliverables', e.target.value)} placeholder="List the expected deliverables..." />
                </div>
              </div>
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="specialConditions">Special Conditions</Label>
                  <Textarea id="specialConditions" rows={3} value={formData.specialConditions || ''} onChange={(e) => handleInputChange('specialConditions', e.target.value)} placeholder="Any special conditions..." />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="penaltyClause">Penalty Clause</Label>
                  <Textarea id="penaltyClause" rows={3} value={formData.penaltyClause || ''} onChange={(e) => handleInputChange('penaltyClause', e.target.value)} placeholder="Penalties for non-compliance..." />
                </div>
              </div>
              <div className="space-y-2">
                <Label htmlFor="notes">Notes</Label>
                <Textarea id="notes" rows={2} value={formData.notes || ''} onChange={(e) => handleInputChange('notes', e.target.value)} placeholder="Additional notes..." />
              </div>
            </CardContent>
          </Card>
        </div>

        {/* Actions */}
        <div className="flex justify-end gap-4 mt-6">
          <Button type="button" variant="outline" onClick={() => router.back()}>Cancel</Button>
          <Button type="submit" disabled={saving} className="bg-green-600 hover:bg-green-700">
            {saving ? <><Loader2 className="h-4 w-4 mr-2 animate-spin" />Creating...</> : <><Save className="h-4 w-4 mr-2" />Create Contract</>}
          </Button>
        </div>
      </form>
    </div>
  );
}

export default function CreateContractPage() {
  return (
    <Suspense fallback={
      <div className="container mx-auto py-6">
        <div className="text-center py-12">
          <Loader2 className="h-12 w-12 animate-spin mx-auto mb-4 text-blue-500" />
          <p className="text-gray-500">Loading...</p>
        </div>
      </div>
    }>
      <CreateContractForm />
    </Suspense>
  );
}

