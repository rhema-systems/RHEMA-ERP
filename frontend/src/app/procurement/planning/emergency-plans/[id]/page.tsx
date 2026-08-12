'use client';

import { useEffect, useMemo, useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { ArrowLeft, Edit, RefreshCw } from 'lucide-react';
import { toast } from 'sonner';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import {
  emergencyProcurementPlanService,
  type EmergencyProcurementPlanDetailDto,
} from '@/services/procurementPlanningService';
import EmergencyPurchaseGovernancePanel from '../EmergencyPurchaseGovernancePanel';

const formatDate = (value?: string) => value ? new Date(value).toLocaleDateString() : '-';

const formatMoney = (value: number, currency: string) =>
  new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: currency || 'USD',
    maximumFractionDigits: 0,
  }).format(value || 0);

const getStatusBadge = (status: string) => {
  const className = status === 'Active'
    ? 'bg-green-100 text-green-800'
    : status === 'Triggered' || status === 'Activated'
      ? 'bg-red-100 text-red-800'
      : status === 'Draft'
        ? 'bg-gray-100 text-gray-800'
        : '';

  return <Badge variant="outline" className={className}>{status}</Badge>;
};

export default function EmergencyPlanDetailPage() {
  const router = useRouter();
  const params = useParams<{ id: string }>();
  const [plan, setPlan] = useState<EmergencyProcurementPlanDetailDto | null>(null);
  const [loading, setLoading] = useState(true);

  const planNumber = useMemo(() => plan?.planNumber || plan?.planCode || '-', [plan]);

  const loadPlan = async () => {
    try {
      setLoading(true);
      const data = await emergencyProcurementPlanService.getPlanById(params.id);
      setPlan(data);
    } catch (error) {
      console.error('Error loading emergency plan:', error);
      toast.error('Failed to load emergency plan');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadPlan();
  }, [params.id]);

  if (loading) {
    return <div className="p-6 text-muted-foreground">Loading emergency plan...</div>;
  }

  if (!plan) {
    return <div className="p-6 text-muted-foreground">Emergency plan not found</div>;
  }

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">{plan.title}</h1>
          <div className="mt-1 flex flex-wrap items-center gap-2 text-sm text-muted-foreground">
            <span>{planNumber}</span>
            {getStatusBadge(plan.status)}
            <Badge variant="secondary">{plan.emergencyType.replace(/([A-Z])/g, ' $1').trim()}</Badge>
          </div>
        </div>
        <div className="flex flex-wrap gap-2">
          <Button variant="outline" onClick={() => router.push('/procurement/planning/emergency-plans')}>
            <ArrowLeft className="mr-2 h-4 w-4" />
            Back
          </Button>
          <Button variant="outline" onClick={loadPlan}>
            <RefreshCw className="mr-2 h-4 w-4" />
            Refresh
          </Button>
          {plan.status === 'Draft' && (
            <Button variant="outline" onClick={() => router.push(`/procurement/planning/emergency-plans/${plan.id}/edit`)}>
              <Edit className="mr-2 h-4 w-4" />
              Edit
            </Button>
          )}
        </div>
      </div>

      <div className="grid gap-4 lg:grid-cols-4">
        <Card>
          <CardHeader><CardTitle className="text-sm">Budget Reserve</CardTitle></CardHeader>
          <CardContent className="text-2xl font-semibold">{formatMoney(plan.budgetReserve, plan.currency)}</CardContent>
        </Card>
        <Card>
          <CardHeader><CardTitle className="text-sm">Remaining Reserve</CardTitle></CardHeader>
          <CardContent className="text-2xl font-semibold">{formatMoney(plan.remainingReserve, plan.currency)}</CardContent>
        </Card>
        <Card>
          <CardHeader><CardTitle className="text-sm">Critical Items</CardTitle></CardHeader>
          <CardContent className="text-2xl font-semibold">{plan.criticalItems.length}</CardContent>
        </Card>
        <Card>
          <CardHeader><CardTitle className="text-sm">Emergency Suppliers</CardTitle></CardHeader>
          <CardContent className="text-2xl font-semibold">{plan.emergencySuppliers.length}</CardContent>
        </Card>
      </div>

      <EmergencyPurchaseGovernancePanel plan={plan} onUpdated={setPlan} />

      <Card>
        <CardHeader><CardTitle>Plan Details</CardTitle></CardHeader>
        <CardContent className="grid gap-4 text-sm md:grid-cols-2 lg:grid-cols-4">
          <div><span className="text-muted-foreground">Department</span><div className="font-medium">{plan.departmentName || '-'}</div></div>
          <div><span className="text-muted-foreground">Criticality</span><div className="font-medium">{plan.criticalityLevel}</div></div>
          <div><span className="text-muted-foreground">Effective Date</span><div className="font-medium">{formatDate(plan.effectiveDate || plan.validFrom)}</div></div>
          <div><span className="text-muted-foreground">Expiry Date</span><div className="font-medium">{formatDate(plan.expiryDate || plan.validTo)}</div></div>
          <div><span className="text-muted-foreground">Next Review</span><div className="font-medium">{formatDate(plan.nextReviewDate)}</div></div>
          <div><span className="text-muted-foreground">Max Approval</span><div className="font-medium">{formatMoney(plan.maxApprovalLimit, plan.currency)}</div></div>
          <div className="md:col-span-2"><span className="text-muted-foreground">Rapid Process</span><div className="whitespace-pre-wrap font-medium">{plan.rapidProcurementProcess || '-'}</div></div>
          <div className="md:col-span-2"><span className="text-muted-foreground">Escalation Contacts</span><div className="whitespace-pre-wrap font-medium">{plan.escalationContacts || '-'}</div></div>
          <div className="md:col-span-2"><span className="text-muted-foreground">Notes</span><div className="whitespace-pre-wrap font-medium">{plan.notes || '-'}</div></div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle>Critical Items</CardTitle></CardHeader>
        <CardContent>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Item</TableHead>
                <TableHead>Category</TableHead>
                <TableHead>Stock</TableHead>
                <TableHead>Emergency Qty</TableHead>
                <TableHead>Lead Time</TableHead>
                <TableHead>Criticality</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {plan.criticalItems.map((item) => (
                <TableRow key={item.id}>
                  <TableCell className="font-medium">{item.itemDescription}</TableCell>
                  <TableCell>{item.itemCategory || '-'}</TableCell>
                  <TableCell>{item.currentStockLevel} / {item.minimumStockLevel} {item.unitOfMeasure}</TableCell>
                  <TableCell>{item.emergencyOrderQuantity} {item.unitOfMeasure}</TableCell>
                  <TableCell>{item.maxLeadTimeDays} days</TableCell>
                  <TableCell><Badge variant={item.criticalityLevel === 'Critical' ? 'destructive' : 'outline'}>{item.criticalityLevel}</Badge></TableCell>
                </TableRow>
              ))}
              {plan.criticalItems.length === 0 && (
                <TableRow><TableCell colSpan={6} className="text-center text-muted-foreground">No critical items</TableCell></TableRow>
              )}
            </TableBody>
          </Table>
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle>Emergency Suppliers</CardTitle></CardHeader>
        <CardContent>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Supplier</TableHead>
                <TableHead>Contact</TableHead>
                <TableHead>Items</TableHead>
                <TableHead>Response</TableHead>
                <TableHead>Priority</TableHead>
                <TableHead>Contract</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {plan.emergencySuppliers.map((supplier) => (
                <TableRow key={supplier.id}>
                  <TableCell className="font-medium">{supplier.supplierName}</TableCell>
                  <TableCell>{supplier.contactPhone || supplier.contactEmail || '-'}</TableCell>
                  <TableCell>{supplier.itemsProvided || '-'}</TableCell>
                  <TableCell>{supplier.responseTimeHours} hours</TableCell>
                  <TableCell>{supplier.priority}</TableCell>
                  <TableCell>{supplier.hasEmergencyContract ? 'Yes' : 'No'}</TableCell>
                </TableRow>
              ))}
              {plan.emergencySuppliers.length === 0 && (
                <TableRow><TableCell colSpan={6} className="text-center text-muted-foreground">No emergency suppliers</TableCell></TableRow>
              )}
            </TableBody>
          </Table>
        </CardContent>
      </Card>
    </div>
  );
}
