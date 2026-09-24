'use client';

import React, { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import { FileText, ArrowLeft, Save } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { leaseAccountingService, type CreateLeaseContractDto, type PaymentFrequency } from '@/services/finance/leaseAccountingService';
import { apiService } from '@/services/api.service';
import { toast } from 'sonner';

interface BusinessPartner {
  id: string;
  partnerName: string;
  companyName?: string;
}

export default function NewLeasePage() {
  const router = useRouter();
  const [saving, setSaving] = useState(false);
  const [partners, setPartners] = useState<BusinessPartner[]>([]);
  const [form, setForm] = useState<CreateLeaseContractDto>({
    contractNumber: '',
    description: '',
    lessorId: '',
    startDate: new Date().toISOString().split('T')[0],
    endDate: '',
    monthlyPaymentAmount: 0,
    paymentFrequency: 'Monthly',
    annualDiscountRate: 0.08,
  });

  useEffect(() => {
    const loadPartners = async () => {
      try {
        const data = await apiService.get<BusinessPartner[]>('/procurement/business-partners/active');
        setPartners(data);
      } catch (error) {
        console.error('Failed to load business partners:', error);
      }
    };
    loadPartners();
  }, []);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      setSaving(true);
      const result = await leaseAccountingService.create(form);
      router.push(`/finance/fixed-assets/leases/${result.id}`);
    } catch (error: unknown) {
      toast.error(error instanceof Error ? error.message : 'The lease was not created. Review its dates, lessor and payment terms, then retry.');
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-4">
        <Button variant="ghost" size="icon" onClick={() => router.push('/finance/fixed-assets/leases')}>
          <ArrowLeft className="h-5 w-5" />
        </Button>
        <div>
          <h1 className="text-3xl font-bold tracking-tight flex items-center gap-2">
            <FileText className="h-8 w-8" />
            New Lease Contract
          </h1>
          <p className="text-muted-foreground">Create a lease contract for IFRS 16 accounting.</p>
        </div>
      </div>

      <Breadcrumb>
        <BreadcrumbList>
          <BreadcrumbItem><BreadcrumbLink href="/dashboard">Dashboard</BreadcrumbLink></BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem><BreadcrumbLink href="/finance">Finance</BreadcrumbLink></BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem><BreadcrumbLink href="/finance/fixed-assets/leases">Leases</BreadcrumbLink></BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem><BreadcrumbPage>New</BreadcrumbPage></BreadcrumbItem>
        </BreadcrumbList>
      </Breadcrumb>

      <form onSubmit={handleSubmit}>
        <Card>
          <CardHeader><CardTitle>Lease Details</CardTitle></CardHeader>
          <CardContent className="space-y-4">
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              <div>
                <Label htmlFor="contractNumber">Contract Number *</Label>
                <Input id="contractNumber" required value={form.contractNumber} onChange={(e) => setForm({ ...form, contractNumber: e.target.value })} placeholder="e.g. LEASE-2026-001" />
              </div>
              <div>
                <Label htmlFor="lessorId">Lessor (Business Partner) *</Label>
                <Select value={form.lessorId} onValueChange={(v) => setForm({ ...form, lessorId: v })}>
                  <SelectTrigger><SelectValue placeholder="Select lessor" /></SelectTrigger>
                  <SelectContent>
                    {partners.map((p) => <SelectItem key={p.id} value={p.id}>{p.companyName || p.partnerName}</SelectItem>)}
                  </SelectContent>
                </Select>
              </div>
            </div>
            <div>
              <Label htmlFor="description">Description *</Label>
              <Input id="description" required value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} placeholder="e.g. Office space lease at 123 Main St" />
            </div>
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              <div>
                <Label htmlFor="startDate">Start Date *</Label>
                <Input id="startDate" type="date" required value={form.startDate} onChange={(e) => setForm({ ...form, startDate: e.target.value })} />
              </div>
              <div>
                <Label htmlFor="endDate">End Date *</Label>
                <Input id="endDate" type="date" required value={form.endDate} onChange={(e) => setForm({ ...form, endDate: e.target.value })} />
              </div>
            </div>
            <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
              <div>
                <Label htmlFor="payment">Payment Amount *</Label>
                <Input id="payment" type="number" required min={0} step={0.01} value={form.monthlyPaymentAmount} onChange={(e) => setForm({ ...form, monthlyPaymentAmount: parseFloat(e.target.value) || 0 })} />
              </div>
              <div>
                <Label htmlFor="frequency">Payment Frequency *</Label>
                <Select value={form.paymentFrequency} onValueChange={(v) => setForm({ ...form, paymentFrequency: v as PaymentFrequency })}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="Monthly">Monthly</SelectItem>
                    <SelectItem value="Quarterly">Quarterly</SelectItem>
                    <SelectItem value="SemiAnnually">Semi-Annually</SelectItem>
                    <SelectItem value="Annually">Annually</SelectItem>
                  </SelectContent>
                </Select>
              </div>
              <div>
                <Label htmlFor="rate">Annual Discount Rate *</Label>
                <Input id="rate" type="number" required min={0} max={1} step={0.001} value={form.annualDiscountRate} onChange={(e) => setForm({ ...form, annualDiscountRate: parseFloat(e.target.value) || 0 })} placeholder="e.g. 0.08 for 8%" />
              </div>
            </div>
          </CardContent>
        </Card>

        <div className="flex justify-end mt-4">
          <Button type="submit" disabled={saving} size="lg">
            <Save className="mr-2 h-4 w-4" />
            {saving ? 'Creating...' : 'Create Lease'}
          </Button>
        </div>
      </form>
    </div>
  );
}
