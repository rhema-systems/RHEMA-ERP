'use client';

import React, { useEffect, useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { FileText, Loader2, ArrowLeft, Play, CheckCircle, ReceiptText } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { leaseAccountingService, type LeaseContractDetail, type LeaseStatus } from '@/services/finance/leaseAccountingService';
import { SourceDocumentDimensionDefaultsPanel, SourceDocumentDimensionEvidence } from '@/components/finance/dimensions/source-document-dimension-panel';
import { toFinancePostingDimensionValues, toFinanceSourceDimensionFormState } from '@/lib/finance/source-document-dimensions';
import { toast } from 'sonner';
import { useAuth } from '@/hooks/use-auth';

export default function LeaseDetailPage() {
  const params = useParams();
  const router = useRouter();
  const { hasPermission } = useAuth();
  const id = params.id as string;
  const canCreateApInvoice = hasPermission('Finance.AP.Invoices.Create');

  const [lease, setLease] = useState<LeaseContractDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [recognitionDefaults, setRecognitionDefaults] = useState<Record<string, string>>({});

  useEffect(() => {
    const load = async () => {
      try {
        setLoading(true);
        const data = await leaseAccountingService.getById(id);
        setLease(data);
        setRecognitionDefaults(toFinanceSourceDimensionFormState(data.recognitionFinanceDimensions).defaultValues);
      } catch (error) {
        console.error('Failed to load lease:', error);
      } finally {
        setLoading(false);
      }
    };
    load();
  }, [id]);

  const refresh = async () => {
    const data = await leaseAccountingService.getById(id);
    setLease(data);
  };

  const handleActivate = async () => {
    if (!confirm('Activate this lease? This will create the ROU fixed asset and post recognition GL journal.')) return;
    try {
      await leaseAccountingService.activate(id, {
        defaultDimensions: toFinancePostingDimensionValues(recognitionDefaults),
        lines: [],
        applyDefaultToEligibleLines: true,
      });
      await refresh();
    } catch (error: unknown) {
      toast.error(error instanceof Error ? error.message : 'The lease was not activated. Review its commencement data and Finance mappings, then retry.');
    }
  };

  const handlePreparePayable = async (lineId: string, periodNumber: number) => {
    if (!confirm(`Prepare the AP invoice draft for period ${periodNumber}? It will still require normal AP review and approval.`)) return;
    try {
      await leaseAccountingService.preparePeriodPayable(id, lineId);
      await refresh();
    } catch (error: unknown) {
      toast.error(error instanceof Error ? error.message : 'The AP draft was not prepared. Review prior periods and the lease source authority, then retry.');
    }
  };

  const formatMoney = (amount: number) =>
    new Intl.NumberFormat('en-GH', { style: 'currency', currency: 'GHS' }).format(amount || 0);

  const getStatusBadge = (status: LeaseStatus) => {
    const variants: Record<LeaseStatus, 'default' | 'secondary' | 'destructive' | 'outline'> = {
      Draft: 'outline', Active: 'default', Terminated: 'destructive', Completed: 'secondary',
    };
    return <Badge variant={variants[status] || 'default'}>{status}</Badge>;
  };

  if (loading) {
    return <div className="flex items-center justify-center min-h-[400px]"><Loader2 className="h-8 w-8 animate-spin text-muted-foreground" /></div>;
  }

  if (!lease) {
    return <div className="text-center py-8 text-muted-foreground">Lease not found.</div>;
  }

  const postedCount = lease.scheduleLines.filter((l) => l.isPosted).length;

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-4">
          <Button variant="ghost" size="icon" onClick={() => router.push('/finance/fixed-assets/leases')}>
            <ArrowLeft className="h-5 w-5" />
          </Button>
          <div>
            <h1 className="text-3xl font-bold tracking-tight flex items-center gap-2">
              <FileText className="h-8 w-8" />
              Lease {lease.contractNumber}
            </h1>
            <p className="text-muted-foreground">{lease.description}</p>
          </div>
        </div>
        {lease.status === 'Draft' && (
          <Button onClick={handleActivate} size="lg">
            <Play className="mr-2 h-4 w-4" />
            Activate Lease
          </Button>
        )}
      </div>

      <Breadcrumb>
        <BreadcrumbList>
          <BreadcrumbItem><BreadcrumbLink href="/dashboard">Dashboard</BreadcrumbLink></BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem><BreadcrumbLink href="/finance">Finance</BreadcrumbLink></BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem><BreadcrumbLink href="/finance/fixed-assets/leases">Leases</BreadcrumbLink></BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem><BreadcrumbPage>{lease.contractNumber}</BreadcrumbPage></BreadcrumbItem>
        </BreadcrumbList>
      </Breadcrumb>

      {/* Summary Cards */}
      <div className="grid grid-cols-1 md:grid-cols-5 gap-4">
        <Card><CardHeader className="pb-2"><CardTitle className="text-sm text-muted-foreground">Status</CardTitle></CardHeader><CardContent>{getStatusBadge(lease.status)}</CardContent></Card>
        <Card><CardHeader className="pb-2"><CardTitle className="text-sm text-muted-foreground">Lessor</CardTitle></CardHeader><CardContent className="font-medium">{lease.lessorName || '—'}</CardContent></Card>
        <Card><CardHeader className="pb-2"><CardTitle className="text-sm text-muted-foreground">Present Value</CardTitle></CardHeader><CardContent className="text-xl font-semibold">{formatMoney(lease.presentValue)}</CardContent></Card>
        <Card><CardHeader className="pb-2"><CardTitle className="text-sm text-muted-foreground">Payment / Period</CardTitle></CardHeader><CardContent className="text-xl font-semibold">{formatMoney(lease.monthlyPaymentAmount)}</CardContent></Card>
        <Card><CardHeader className="pb-2"><CardTitle className="text-sm text-muted-foreground">Progress</CardTitle></CardHeader><CardContent className="text-xl font-semibold">{postedCount} / {lease.totalPeriods}</CardContent></Card>
      </div>

      {/* Contract Details */}
      <Card>
        <CardHeader><CardTitle>Contract Details</CardTitle></CardHeader>
        <CardContent>
          <div className="grid grid-cols-2 md:grid-cols-4 gap-4 text-sm">
            <div><span className="text-muted-foreground">Start Date</span><p className="font-medium">{new Date(lease.startDate).toLocaleDateString()}</p></div>
            <div><span className="text-muted-foreground">End Date</span><p className="font-medium">{new Date(lease.endDate).toLocaleDateString()}</p></div>
            <div><span className="text-muted-foreground">Frequency</span><p className="font-medium">{lease.paymentFrequency}</p></div>
            <div><span className="text-muted-foreground">Discount Rate</span><p className="font-medium">{(lease.annualDiscountRate * 100).toFixed(2)}%</p></div>
          </div>
          {lease.rouAssetId && (
            <div className="mt-4 p-3 bg-muted rounded-md">
              <span className="text-sm text-muted-foreground">ROU Asset: </span>
              <a href={`/finance/fixed-assets/register/${lease.rouAssetId}/edit`} className="text-primary hover:underline font-mono text-sm">View Asset →</a>
            </div>
          )}
        </CardContent>
      </Card>

      <SourceDocumentDimensionEvidence evidence={lease.recognitionFinanceDimensions} />

      {lease.status === 'Draft' && (
        <SourceDocumentDimensionDefaultsPanel
          effectiveDate={lease.startDate.slice(0, 10)}
          values={recognitionDefaults}
          onChange={setRecognitionDefaults}
        />
      )}

      {/* Amortization Schedule */}
      <Card>
        <CardHeader>
          <CardTitle>Amortization Schedule</CardTitle>
          <CardDescription>{lease.totalPeriods} periods — {postedCount} accounting-posted through AP</CardDescription>
        </CardHeader>
        <CardContent>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>#</TableHead>
                <TableHead>Date</TableHead>
                <TableHead className="text-right">Payment</TableHead>
                <TableHead className="text-right">Interest</TableHead>
                <TableHead className="text-right">Principal</TableHead>
                <TableHead className="text-right">Remaining</TableHead>
                <TableHead>Status</TableHead>
                <TableHead>AP invoice</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {lease.scheduleLines.map((line) => (
                <TableRow key={line.id} className={line.isPosted ? 'bg-muted/50' : ''}>
                  <TableCell className="font-mono">{line.periodNumber}</TableCell>
                  <TableCell>{new Date(line.periodDate).toLocaleDateString('en-US', { year: 'numeric', month: 'short' })}</TableCell>
                  <TableCell className="text-right">{formatMoney(line.paymentAmount)}</TableCell>
                  <TableCell className="text-right">{formatMoney(line.interestExpense)}</TableCell>
                  <TableCell className="text-right">{formatMoney(line.principalReduction)}</TableCell>
                  <TableCell className="text-right">{formatMoney(line.remainingLiability)}</TableCell>
                  <TableCell>
                    {line.vendorInvoiceStatus === 'Paid' ? (
                      <Badge variant="default"><CheckCircle className="h-3 w-3 mr-1" />Paid in AP</Badge>
                    ) : line.vendorInvoiceStatus ? (
                      <Badge variant={line.vendorInvoiceStatus === 'Voided' ? 'destructive' : line.isPosted ? 'default' : 'secondary'}>
                        {line.vendorInvoiceStatus}
                      </Badge>
                    ) : <Badge variant="outline">No AP invoice</Badge>}
                  </TableCell>
                  <TableCell className="space-x-2 whitespace-nowrap">
                    {line.vendorInvoiceId && (
                      <Button size="sm" variant="ghost" asChild>
                        <a href={`/finance/ap/invoices/${line.vendorInvoiceId}`}>
                          <ReceiptText className="mr-1 h-4 w-4" />{line.vendorInvoiceNumber || 'Open AP invoice'}
                        </a>
                      </Button>
                    )}
                    {line.canPreparePayable && canCreateApInvoice && (
                      <Button size="sm" variant="outline" onClick={() => handlePreparePayable(line.id, line.periodNumber)}>
                        Prepare AP draft
                      </Button>
                    )}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </CardContent>
      </Card>

      {lease.scheduleLines.some((line) => line.financeDimensions) && (
        <Card>
          <CardHeader>
            <CardTitle>Historical lease-period dimension evidence</CardTitle>
            <CardDescription>
              Read-only evidence retained from legacy standalone lease-period postings. New instalments are coded and posted by their linked AP invoice.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            {lease.scheduleLines.filter((line) => line.financeDimensions).map((line) => (
              <div key={line.id} className="space-y-2">
                <p className="text-sm font-medium">Period {line.periodNumber}</p>
                <SourceDocumentDimensionEvidence evidence={line.financeDimensions} />
              </div>
            ))}
          </CardContent>
        </Card>
      )}

      <p className="text-sm text-muted-foreground">
        Lease periods create AP drafts only. Invoice approval posts the liability through the normal maker/checker workflow; payment status remains owned by AP.
      </p>
    </div>
  );
}
