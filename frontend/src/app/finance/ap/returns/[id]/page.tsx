'use client';

import { useQuery } from '@tanstack/react-query';
import { useParams, useRouter } from 'next/navigation';
import {
  ArrowLeft,
  FileText,
  AlertTriangle,
  User,
  Calendar,
  TrendingUp,
  LockKeyhole,
} from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { accountsPayableService } from '@/services/accountsPayableService';
import { financeDataService } from '@/services/finance/finance-data.service';
import { useTenant } from '@/contexts/TenantContext';
import { formatCurrency } from '@/lib/utils';
import { Skeleton } from '@/components/ui/skeleton';
import { format } from 'date-fns';

export default function ReturnDetailsPage() {
  const params = useParams();
  const router = useRouter();
  const { currentTenantCode, isLoadingTenants } = useTenant();
  const id = params.id as string;

  const { data: financeSettings } = useQuery({
    queryKey: ['finance', 'supplier-returns', currentTenantCode, 'settings'],
    queryFn: () => financeDataService.getFinanceSettings(),
    enabled: !isLoadingTenants && Boolean(currentTenantCode),
  });
  const functionalCurrencyCode = (
    financeSettings?.baseCurrency || 'GHS'
  ).toUpperCase();

  const {
    data: ret,
    isLoading,
    error,
  } = useQuery({
    queryKey: ['finance', 'supplier-returns', currentTenantCode, 'detail', id],
    queryFn: () => accountsPayableService.getSupplierReturn(id),
    enabled: !isLoadingTenants && Boolean(currentTenantCode) && Boolean(id),
  });

  if (isLoading) {
    return (
      <div className="p-8 space-y-6 max-w-[1400px] mx-auto">
        <Skeleton className="h-10 w-[200px]" />
        <Skeleton className="h-[200px] w-full" />
        <Skeleton className="h-[400px] w-full" />
      </div>
    );
  }

  if (error || !ret) {
    return (
      <div className="p-8 text-center text-muted-foreground space-y-4">
        <AlertTriangle className="h-12 w-12 text-destructive mx-auto" />
        <p className="font-bold text-lg">Failed to Load Return Slip</p>
        <p className="text-sm">
          The requested return slip could not be fetched or does not exist.
        </p>
        <Button
          variant="outline"
          onClick={() => router.push('/finance/ap/returns')}
        >
          Back to Returns
        </Button>
      </div>
    );
  }

  const statusStr =
    typeof ret.status === 'number'
      ? ((
          {
            1: 'Draft',
            2: 'Approved',
            3: 'Cancelled',
            4: 'PendingApproval',
            5: 'Rejected',
          } as Record<number, string>
        )[ret.status] ?? String(ret.status))
      : ret.status;

  return (
    <div className="space-y-6 p-8 max-w-[1400px] mx-auto">
      {/* Top action bar */}
      <div className="flex justify-between items-center">
        <div className="flex items-center space-x-4">
          <Button
            variant="outline"
            size="icon"
            onClick={() => router.push('/finance/ap/returns')}
          >
            <ArrowLeft className="h-4 w-4" />
          </Button>
          <div>
            <div className="flex items-center gap-3">
              <h1 className="text-3xl font-bold tracking-tight text-slate-800 dark:text-slate-200">
                {ret.returnNumber}
              </h1>
              {statusStr === 'Draft' ? (
                <Badge
                  variant="secondary"
                  className="bg-slate-200 text-slate-800 dark:bg-slate-800 dark:text-slate-200 text-xs font-semibold py-1"
                >
                  Draft
                </Badge>
              ) : statusStr === 'Approved' ? (
                <Badge className="bg-slate-600 text-white py-1">
                  Legacy Approved Record
                </Badge>
              ) : (
                <Badge variant="secondary" className="py-1">
                  Legacy {statusStr}
                </Badge>
              )}
            </div>
            <p className="text-muted-foreground mt-1">
              Historical Finance record linked to{' '}
              {ret.originalVendorInvoiceId
                ? 'a Vendor Invoice'
                : 'a Purchase Receipt (GRV)'}
            </p>
          </div>
        </div>
      </div>

      <Alert className="border-amber-300 bg-amber-50/70 dark:border-amber-900 dark:bg-amber-950/20">
        <LockKeyhole className="h-4 w-4 text-amber-700" />
        <AlertTitle>
          Read-only legacy record - mutation is quarantined
        </AlertTitle>
        <AlertDescription>
          This status does not prove an approved Procurement Return-to-Vendor
          dispatch, an authoritative Inventory movement/valuation reversal, or a
          supplier&apos;s commercial acceptance. FIN-INT-012 and FIN-INT-013
          remain Planned. No approve, post, or inventory action is available
          from this screen.
        </AlertDescription>
      </Alert>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Left Columns: Metadata details */}
        <div className="lg:col-span-1 space-y-6">
          <Card className="glassmorphism border-slate-200/50 dark:border-slate-800/50 shadow-sm">
            <CardHeader>
              <CardTitle>Return Summary</CardTitle>
            </CardHeader>
            <CardContent className="space-y-4 text-sm">
              <div className="flex items-center gap-3 py-2 border-b">
                <User className="h-4 w-4 text-muted-foreground" />
                <div>
                  <span className="text-xs text-muted-foreground block">
                    Supplier
                  </span>
                  <span className="font-semibold text-slate-800 dark:text-slate-200">
                    {ret.vendorName}
                  </span>
                </div>
              </div>

              <div className="flex items-center gap-3 py-2 border-b">
                <Calendar className="h-4 w-4 text-muted-foreground" />
                <div>
                  <span className="text-xs text-muted-foreground block">
                    Return Date
                  </span>
                  <span className="font-semibold text-slate-800 dark:text-slate-200">
                    {format(new Date(ret.returnDate), 'MMMM dd, yyyy')}
                  </span>
                </div>
              </div>

              <div className="flex items-center gap-3 py-2 border-b">
                <FileText className="h-4 w-4 text-muted-foreground" />
                <div>
                  <span className="text-xs text-muted-foreground block">
                    Source Document
                  </span>
                  {ret.originalVendorInvoiceId ? (
                    <span
                      className="font-semibold text-primary cursor-pointer hover:underline"
                      onClick={() =>
                        router.push(
                          `/finance/ap/invoices/${ret.originalVendorInvoiceId}`
                        )
                      }
                    >
                      Invoice #
                      {ret.originalVendorInvoice?.invoiceNumber ||
                        'Linked Invoice'}
                    </span>
                  ) : (
                    <span
                      className="font-semibold text-amber-600 cursor-pointer hover:underline"
                      onClick={() => router.push(`/finance/ap/receipts`)} // wait, go to receipts index since receipt details page may not exist
                    >
                      GRV #
                      {ret.originalFinancePurchaseOrderReceipt?.receiptNumber ||
                        'Linked GRV'}
                    </span>
                  )}
                </div>
              </div>

              <div className="flex items-center gap-3 py-2">
                <TrendingUp className="h-4 w-4 text-muted-foreground" />
                <div>
                  <span className="text-xs text-muted-foreground block">
                    Locked Exchange Rate
                  </span>
                  <span className="font-semibold text-slate-800 dark:text-slate-200">
                    {ret.exchangeRate}{' '}
                    <span className="text-xs font-normal text-muted-foreground">
                      ({ret.currencyCode})
                    </span>
                  </span>
                </div>
              </div>

              {ret.reason && (
                <div className="mt-4 p-3 bg-muted/50 rounded-lg">
                  <span className="text-xs text-muted-foreground block font-medium mb-1">
                    Reason for Return
                  </span>
                  <span className="text-slate-700 dark:text-slate-300">
                    {ret.reason}
                  </span>
                </div>
              )}
            </CardContent>
          </Card>

          {/* Financial Totals */}
          <Card className="glassmorphism border-slate-200/50 dark:border-slate-800/50 shadow-sm">
            <CardHeader>
              <CardTitle>Recorded Legacy Amounts</CardTitle>
            </CardHeader>
            <CardContent className="space-y-3 font-semibold text-sm">
              <div className="flex justify-between py-1 border-b border-dashed">
                <span className="text-muted-foreground font-normal">
                  Recorded Subtotal:
                </span>
                <span>
                  {formatCurrency(
                    ret.subTotal,
                    ret.currencyCode || functionalCurrencyCode
                  )}
                </span>
              </div>
              <div className="flex justify-between py-1 border-b border-dashed text-emerald-600">
                <span className="font-normal text-emerald-600">
                  Recorded Tax Amount:
                </span>
                <span>
                  {formatCurrency(
                    ret.taxAmount,
                    ret.currencyCode || functionalCurrencyCode
                  )}
                </span>
              </div>
              <div className="flex justify-between py-2 border-b text-xl font-extrabold text-slate-800 dark:text-slate-200">
                <span>Recorded Total:</span>
                <span>
                  {formatCurrency(
                    ret.totalAmount,
                    ret.currencyCode || functionalCurrencyCode
                  )}
                </span>
              </div>
              {ret.currencyCode !== functionalCurrencyCode && (
                <div className="flex justify-between py-1 text-xs text-muted-foreground">
                  <span className="font-normal">
                    Base Currency ({functionalCurrencyCode}):
                  </span>
                  <span>
                    {formatCurrency(
                      ret.baseCurrencyAmount,
                      functionalCurrencyCode
                    )}
                  </span>
                </div>
              )}
            </CardContent>
          </Card>

          {ret.debitNote && (
            <Card className="border-slate-300 bg-slate-50/50 shadow-sm dark:border-slate-800 dark:bg-slate-950/20">
              <CardHeader className="pb-2">
                <CardTitle className="text-sm">
                  Legacy Finance linkage
                </CardTitle>
              </CardHeader>
              <CardContent className="space-y-2 text-xs text-muted-foreground">
                <p>
                  A legacy supplier debit-note row is linked for audit
                  visibility only. Its presence is not evidence that the agreed
                  cross-module Return-to-Vendor lifecycle completed.
                </p>
                <div className="rounded border p-2.5 font-mono">
                  <div>
                    Debit note:{' '}
                    {ret.debitNote.debitNoteNumber || ret.debitNote.id}
                  </div>
                  <div>
                    Status: {ret.debitNote.statusName || ret.debitNote.status}
                  </div>
                  <div>
                    Journal reference:{' '}
                    {ret.debitNote.journalEntryId || 'None retained'}
                  </div>
                </div>
              </CardContent>
            </Card>
          )}
        </div>

        {/* Right Columns: Return Lines list */}
        <div className="lg:col-span-2 space-y-6">
          <Card className="glassmorphism border-slate-200/50 dark:border-slate-800/50 shadow-sm">
            <CardHeader>
              <CardTitle>Returned Line Items</CardTitle>
              <CardDescription>
                Historical values only; verify dispatch, supplier resolution,
                tax, and ledger evidence independently
              </CardDescription>
            </CardHeader>
            <CardContent>
              <div className="rounded-lg border border-slate-200 dark:border-slate-800 overflow-hidden">
                <table className="w-full text-sm border-collapse text-left">
                  <thead className="bg-slate-50 dark:bg-slate-900/50 border-b">
                    <tr>
                      <th className="p-3">Item Description</th>
                      <th className="p-3 text-right">Recorded Qty</th>
                      <th className="p-3 text-right">Unit Price</th>
                      {ret.originalVendorInvoiceId && (
                        <th className="p-3 text-right">Recorded Tax</th>
                      )}
                      <th className="p-3 text-right">Line Total</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-200 dark:divide-slate-800">
                    {ret.lineItems?.map((line: any) => (
                      <tr
                        key={line.id}
                        className="hover:bg-slate-50/30 dark:hover:bg-slate-900/10 transition-colors"
                      >
                        <td className="p-3 font-medium text-slate-800 dark:text-slate-200">
                          {line.description}
                        </td>
                        <td className="p-3 text-right font-bold">
                          {line.quantityReturned}
                        </td>
                        <td className="p-3 text-right">
                          {formatCurrency(
                            line.unitPrice,
                            ret.currencyCode || functionalCurrencyCode
                          )}
                        </td>
                        {ret.originalVendorInvoiceId && (
                          <td className="p-3 text-right text-emerald-600">
                            {formatCurrency(
                              line.taxAmount,
                              ret.currencyCode || functionalCurrencyCode
                            )}
                          </td>
                        )}
                        <td className="p-3 text-right font-bold text-slate-800 dark:text-slate-200">
                          {formatCurrency(
                            line.lineTotal,
                            ret.currencyCode || functionalCurrencyCode
                          )}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </CardContent>
          </Card>
        </div>
      </div>
    </div>
  );
}
