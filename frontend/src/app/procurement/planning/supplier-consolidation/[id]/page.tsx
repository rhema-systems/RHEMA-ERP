'use client';

import { useEffect, useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { ArrowLeft, CheckCircle2, Edit, PlayCircle, Save } from 'lucide-react';
import { toast } from 'sonner';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  supplierConsolidationService,
  type SupplierConsolidationDetailDto,
} from '@/services/procurementPlanningService';

const parseIds = (value?: string): string[] => {
  if (!value) return [];
  try {
    const parsed = JSON.parse(value);
    return Array.isArray(parsed) ? parsed.filter(Boolean) : [];
  } catch {
    return value.split(',').map((item) => item.trim()).filter(Boolean);
  }
};

export default function SupplierConsolidationDetailPage() {
  const router = useRouter();
  const params = useParams<{ id: string }>();
  const [consolidation, setConsolidation] = useState<SupplierConsolidationDetailDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [actualSavings, setActualSavings] = useState('');

  const load = async () => {
    try {
      setLoading(true);
      const result = await supplierConsolidationService.getConsolidationById(params.id);
      setConsolidation(result);
      setActualSavings(String(result.actualSavings || ''));
    } catch (error) {
      console.error('Error loading supplier consolidation:', error);
      toast.error('Failed to load supplier consolidation');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    load();
  }, [params.id]);

  const formatCurrency = (amount: number, currency = consolidation?.currency || 'USD') =>
    new Intl.NumberFormat('en-US', { style: 'currency', currency, maximumFractionDigits: 0 }).format(amount || 0);

  const approve = async () => {
    try {
      setSaving(true);
      const result = await supplierConsolidationService.approveConsolidation(params.id);
      setConsolidation(result);
      toast.success('Consolidation approved');
    } catch (error) {
      console.error('Error approving consolidation:', error);
      toast.error('Failed to approve consolidation');
    } finally {
      setSaving(false);
    }
  };

  const implement = async () => {
    try {
      setSaving(true);
      const result = await supplierConsolidationService.implementConsolidation(params.id);
      setConsolidation(result);
      toast.success('Consolidation marked implemented');
    } catch (error) {
      console.error('Error implementing consolidation:', error);
      toast.error('Failed to implement consolidation');
    } finally {
      setSaving(false);
    }
  };

  const recordSavings = async () => {
    try {
      setSaving(true);
      await supplierConsolidationService.recordActualSavings(params.id, Number(actualSavings || 0));
      toast.success('Actual savings recorded');
      await load();
    } catch (error) {
      console.error('Error recording savings:', error);
      toast.error('Failed to record actual savings');
    } finally {
      setSaving(false);
    }
  };

  if (loading && !consolidation) {
    return <div className="py-12 text-center text-muted-foreground">Loading supplier consolidation...</div>;
  }

  if (!consolidation) {
    return <div className="py-12 text-center text-muted-foreground">Supplier consolidation not found.</div>;
  }

  const preferredSupplierIds = parseIds(consolidation.preferredSupplierIds);

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
        <div>
          <div className="flex items-center gap-2">
            <h1 className="text-2xl font-semibold tracking-tight">{consolidation.title}</h1>
            <Badge variant={consolidation.status === 'Approved' || consolidation.status === 'Implemented' ? 'default' : 'outline'}>{consolidation.status}</Badge>
          </div>
          <p className="text-sm text-muted-foreground">{consolidation.consolidationCode}</p>
        </div>
        <div className="flex flex-wrap gap-2">
          <Button variant="outline" onClick={() => router.push('/procurement/planning/supplier-consolidation')}>
            <ArrowLeft className="mr-2 h-4 w-4" />
            Back
          </Button>
          {consolidation.status === 'Draft' && (
            <Button variant="outline" onClick={() => router.push(`/procurement/planning/supplier-consolidation/${consolidation.id}/edit`)}>
              <Edit className="mr-2 h-4 w-4" />
              Edit
            </Button>
          )}
          {consolidation.status === 'Draft' && (
            <Button onClick={approve} disabled={saving}>
              <CheckCircle2 className="mr-2 h-4 w-4" />
              Approve
            </Button>
          )}
          {consolidation.status === 'Approved' && (
            <Button onClick={implement} disabled={saving}>
              <PlayCircle className="mr-2 h-4 w-4" />
              Implement
            </Button>
          )}
        </div>
      </div>

      <div className="grid gap-4 xl:grid-cols-4">
        <Card>
          <CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-muted-foreground">Current Suppliers</CardTitle></CardHeader>
          <CardContent><div className="text-2xl font-semibold">{consolidation.currentSupplierCount}</div></CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-muted-foreground">Recommended</CardTitle></CardHeader>
          <CardContent><div className="text-2xl font-semibold">{consolidation.recommendedSupplierCount}</div></CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-muted-foreground">Spend</CardTitle></CardHeader>
          <CardContent><div className="text-2xl font-semibold">{formatCurrency(consolidation.totalSpend)}</div></CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-muted-foreground">Potential Savings</CardTitle></CardHeader>
          <CardContent><div className="text-2xl font-semibold">{formatCurrency(consolidation.potentialSavings)}</div></CardContent>
        </Card>
      </div>

      <div className="grid gap-4 xl:grid-cols-[1fr_0.8fr]">
        <Card>
          <CardHeader><CardTitle>Strategy Details</CardTitle></CardHeader>
          <CardContent className="grid gap-3 text-sm sm:grid-cols-2">
            <div><span className="text-muted-foreground">Category</span><div className="font-medium">{consolidation.itemCategory || '-'}</div></div>
            <div><span className="text-muted-foreground">Opportunity</span><div className="font-medium">{consolidation.opportunityLevel}</div></div>
            <div><span className="text-muted-foreground">Strategy</span><div className="font-medium">{consolidation.recommendedStrategy}</div></div>
            <div><span className="text-muted-foreground">Period</span><div className="font-medium">{consolidation.analysisPeriodStart?.slice(0, 10)} to {consolidation.analysisPeriodEnd?.slice(0, 10)}</div></div>
            <div className="sm:col-span-2"><span className="text-muted-foreground">Preferred Supplier IDs</span><div className="font-medium break-all">{preferredSupplierIds.length ? preferredSupplierIds.join(', ') : '-'}</div></div>
            <div className="sm:col-span-2"><span className="text-muted-foreground">Strategy Rationale</span><div className="font-medium whitespace-pre-wrap">{consolidation.strategyRationale || '-'}</div></div>
            <div className="sm:col-span-2"><span className="text-muted-foreground">Implementation Plan</span><div className="font-medium whitespace-pre-wrap">{consolidation.implementationPlan || '-'}</div></div>
            <div className="sm:col-span-2"><span className="text-muted-foreground">Suppliers To Phase Out</span><div className="font-medium whitespace-pre-wrap">{consolidation.suppliersToPhaseOut || '-'}</div></div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader><CardTitle>Implementation Tracking</CardTitle></CardHeader>
          <CardContent className="space-y-4">
            <div className="grid gap-3 sm:grid-cols-2">
              <div><span className="text-sm text-muted-foreground">Implementation Date</span><div className="font-medium">{consolidation.implementationDate?.slice(0, 10) || '-'}</div></div>
              <div><span className="text-sm text-muted-foreground">Actual Savings</span><div className="font-medium">{formatCurrency(consolidation.actualSavings)}</div></div>
            </div>
            <div className="space-y-2">
              <Label>Actual Savings</Label>
              <div className="flex gap-2">
                <Input type="number" min={0} step="0.01" value={actualSavings} onChange={(event) => setActualSavings(event.target.value)} />
                <Button onClick={recordSavings} disabled={saving}>
                  <Save className="mr-2 h-4 w-4" />
                  Save
                </Button>
              </div>
            </div>
            <div className="text-sm">
              <span className="text-muted-foreground">Notes</span>
              <div className="mt-1 whitespace-pre-wrap font-medium">{consolidation.notes || '-'}</div>
            </div>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
