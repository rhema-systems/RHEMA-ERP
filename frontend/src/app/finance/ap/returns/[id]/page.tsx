'use client';

import { useState, useEffect } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useParams, useRouter } from 'next/navigation';
import {
  ArrowLeft,
  CheckCircle,
  FileText,
  RotateCcw,
  AlertTriangle,
  User,
  Calendar,
  DollarSign,
  ArrowRight,
  TrendingUp,
  Tag,
  ShieldCheck,
  Award
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { useToast } from '@/components/ui/use-toast';
import { accountsPayableService } from '@/services/accountsPayableService';
import { formatCurrency } from '@/lib/utils';
import { Skeleton } from '@/components/ui/skeleton';
import { format } from 'date-fns';

export default function ReturnDetailsPage() {
  const params = useParams();
  const router = useRouter();
  const { toast } = useToast();
  const queryClient = useQueryClient();
  const id = params.id as string;

  const { data: ret, isLoading, error } = useQuery({
    queryKey: ['supplier-return', id],
    queryFn: () => accountsPayableService.getSupplierReturn(id),
  });

  const approveMutation = useMutation({
    mutationFn: () => accountsPayableService.approveSupplierReturn(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['supplier-return', id] });
      queryClient.invalidateQueries({ queryKey: ['supplier-returns'] });
      toast({
        title: 'Success',
        description: 'Supplier Return approved and financial debit note posted to AP subledger successfully!',
      });
    },
    onError: (err: any) => {
      toast({
        title: 'Approval Failed',
        description: err.message || 'Failed to approve return.',
        variant: 'destructive',
      });
    },
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
        <p className="text-sm">The requested return slip could not be fetched or does not exist.</p>
        <Button variant="outline" onClick={() => router.push('/finance/ap/returns')}>
          Back to Returns
        </Button>
      </div>
    );
  }

  const statusStr = typeof ret.status === 'number'
    ? (ret.status === 1 ? 'Draft' : ret.status === 2 ? 'Approved' : 'Cancelled')
    : ret.status;

  return (
    <div className="space-y-6 p-8 max-w-[1400px] mx-auto">
      
      {/* Top action bar */}
      <div className="flex justify-between items-center">
        <div className="flex items-center space-x-4">
          <Button variant="outline" size="icon" onClick={() => router.push('/finance/ap/returns')}>
            <ArrowLeft className="h-4 w-4" />
          </Button>
          <div>
            <div className="flex items-center gap-3">
              <h1 className="text-3xl font-bold tracking-tight text-slate-800 dark:text-slate-200">
                {ret.returnNumber}
              </h1>
              {statusStr === 'Draft' ? (
                <Badge variant="secondary" className="bg-slate-200 text-slate-800 dark:bg-slate-800 dark:text-slate-200 text-xs font-semibold py-1">
                  Draft
                </Badge>
              ) : statusStr === 'Approved' ? (
                <Badge className="bg-emerald-600 text-white py-1">
                  Approved & Posted
                </Badge>
              ) : (
                <Badge variant="destructive" className="py-1">Cancelled</Badge>
              )}
            </div>
            <p className="text-muted-foreground mt-1">
              Supplier return created against {ret.originalVendorInvoiceId ? 'Vendor Invoice' : 'Purchase Receipt (GRV)'}
            </p>
          </div>
        </div>

        {statusStr === 'Draft' && (
          <Button 
            onClick={() => approveMutation.mutate()} 
            disabled={approveMutation.isPending}
            className="bg-emerald-600 hover:bg-emerald-700 text-white font-semibold shadow-md px-6"
          >
            <CheckCircle className="h-4 w-4 mr-2" />
            {approveMutation.isPending ? 'Approving...' : 'Confirm & Approve Return'}
          </Button>
        )}
      </div>

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
                  <span className="text-xs text-muted-foreground block">Supplier</span>
                  <span className="font-semibold text-slate-800 dark:text-slate-200">{ret.vendorName}</span>
                </div>
              </div>

              <div className="flex items-center gap-3 py-2 border-b">
                <Calendar className="h-4 w-4 text-muted-foreground" />
                <div>
                  <span className="text-xs text-muted-foreground block">Return Date</span>
                  <span className="font-semibold text-slate-800 dark:text-slate-200">
                    {format(new Date(ret.returnDate), 'MMMM dd, yyyy')}
                  </span>
                </div>
              </div>

              <div className="flex items-center gap-3 py-2 border-b">
                <FileText className="h-4 w-4 text-muted-foreground" />
                <div>
                  <span className="text-xs text-muted-foreground block">Source Document</span>
                  {ret.originalVendorInvoiceId ? (
                    <span 
                      className="font-semibold text-primary cursor-pointer hover:underline"
                      onClick={() => router.push(`/finance/ap/invoices/${ret.originalVendorInvoiceId}`)}
                    >
                      Invoice #{ret.originalVendorInvoice?.invoiceNumber || 'Linked Invoice'}
                    </span>
                  ) : (
                    <span 
                      className="font-semibold text-amber-600 cursor-pointer hover:underline"
                      onClick={() => router.push(`/finance/ap/receipts`)} // wait, go to receipts index since receipt details page may not exist
                    >
                      GRV #{ret.originalFinancePurchaseOrderReceipt?.receiptNumber || 'Linked GRV'}
                    </span>
                  )}
                </div>
              </div>

              <div className="flex items-center gap-3 py-2">
                <TrendingUp className="h-4 w-4 text-muted-foreground" />
                <div>
                  <span className="text-xs text-muted-foreground block">Locked Exchange Rate</span>
                  <span className="font-semibold text-slate-800 dark:text-slate-200">
                    {ret.exchangeRate} <span className="text-xs font-normal text-muted-foreground">({ret.currencyCode})</span>
                  </span>
                </div>
              </div>

              {ret.reason && (
                <div className="mt-4 p-3 bg-muted/50 rounded-lg">
                  <span className="text-xs text-muted-foreground block font-medium mb-1">Reason for Return</span>
                  <span className="text-slate-700 dark:text-slate-300">{ret.reason}</span>
                </div>
              )}
            </CardContent>
          </Card>

          {/* Financial Totals */}
          <Card className="glassmorphism border-slate-200/50 dark:border-slate-800/50 shadow-sm">
            <CardHeader>
              <CardTitle>Financial Reclaimed Balances</CardTitle>
            </CardHeader>
            <CardContent className="space-y-3 font-semibold text-sm">
              <div className="flex justify-between py-1 border-b border-dashed">
                <span className="text-muted-foreground font-normal">Reclaimed Subtotal:</span>
                <span>{formatCurrency(ret.subTotal)} {ret.currencyCode}</span>
              </div>
              <div className="flex justify-between py-1 border-b border-dashed text-emerald-600">
                <span className="font-normal text-emerald-600">Reclaimed Tax Total:</span>
                <span>{formatCurrency(ret.taxAmount)} {ret.currencyCode}</span>
              </div>
              <div className="flex justify-between py-2 border-b text-xl font-extrabold text-slate-800 dark:text-slate-200">
                <span>Reclaimed Grand Total:</span>
                <span>{formatCurrency(ret.totalAmount)} {ret.currencyCode}</span>
              </div>
              {ret.currencyCode !== 'GHS' && (
                <div className="flex justify-between py-1 text-xs text-muted-foreground">
                  <span className="font-normal">Base Currency (GHS):</span>
                  <span>{formatCurrency(ret.baseCurrencyAmount)} GHS</span>
                </div>
              )}
            </CardContent>
          </Card>

          {/* Debit Note flow if approved */}
          {statusStr === 'Approved' && ret.originalVendorInvoiceId && (
            <Card className="border-emerald-500/20 bg-emerald-50/5 dark:bg-emerald-950/5 shadow-sm">
              <CardHeader className="pb-2">
                <div className="flex items-center gap-2 text-emerald-600 dark:text-emerald-400 font-bold text-sm">
                  <ShieldCheck className="h-4 w-4" /> Financial Reversal Confirmed
                </div>
              </CardHeader>
              <CardContent className="text-xs text-muted-foreground space-y-2">
                <p>
                  A corresponding <strong>Supplier Debit Note</strong> has been approved and posted to the General Ledger.
                </p>
                <div className="p-2.5 rounded border border-emerald-500/10 bg-emerald-500/5 text-emerald-700 dark:text-emerald-300 font-semibold space-y-1">
                  <div>Status: Approved & Frozen</div>
                  <div>Liability Ledger reversal: Dr AP Control</div>
                  <div>Asset Ledger reversal: Cr Input VAT / Cost</div>
                </div>
              </CardContent>
            </Card>
          )}

          {/* GRV status flow if approved */}
          {statusStr === 'Approved' && ret.originalFinancePurchaseOrderReceiptId && (
            <Card className="border-amber-500/20 bg-amber-50/5 dark:bg-amber-950/5 shadow-sm">
              <CardHeader className="pb-2">
                <div className="flex items-center gap-2 text-amber-600 dark:text-amber-400 font-bold text-sm">
                  <Award className="h-4 w-4" /> Operational Reversal Confirmed
                </div>
              </CardHeader>
              <CardContent className="text-xs text-muted-foreground space-y-2">
                <p>
                  Purchase Receipt (GRV) remaining quantities and original Purchase Order received quantities have been successfully corrected.
                </p>
                <div className="p-2.5 rounded border border-amber-500/10 bg-amber-500/5 text-amber-700 dark:text-amber-300 font-semibold space-y-1">
                  <div>Status: Approved & Frozen</div>
                  <div>Inventory Stock reduction: Processed</div>
                  <div>Financial GL Posting: None (uninvoiced)</div>
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
              <CardDescription>Review item quantities and proportional reclaimed tax details</CardDescription>
            </CardHeader>
            <CardContent>
              <div className="rounded-lg border border-slate-200 dark:border-slate-800 overflow-hidden">
                <table className="w-full text-sm border-collapse text-left">
                  <thead className="bg-slate-50 dark:bg-slate-900/50 border-b">
                    <tr>
                      <th className="p-3">Item Description</th>
                      <th className="p-3 text-right">Qty Returned</th>
                      <th className="p-3 text-right">Unit Price</th>
                      {ret.originalVendorInvoiceId && <th className="p-3 text-right">Reclaimed Tax</th>}
                      <th className="p-3 text-right">Line Total</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-200 dark:divide-slate-800">
                    {ret.lineItems?.map((line: any) => (
                      <tr key={line.id} className="hover:bg-slate-50/30 dark:hover:bg-slate-900/10 transition-colors">
                        <td className="p-3 font-medium text-slate-800 dark:text-slate-200">
                          {line.description}
                        </td>
                        <td className="p-3 text-right font-bold">{line.quantityReturned}</td>
                        <td className="p-3 text-right">{formatCurrency(line.unitPrice)}</td>
                        {ret.originalVendorInvoiceId && (
                          <td className="p-3 text-right text-emerald-600">
                            {formatCurrency(line.taxAmount)}
                          </td>
                        )}
                        <td className="p-3 text-right font-bold text-slate-800 dark:text-slate-200">
                          {formatCurrency(line.lineTotal)}
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
