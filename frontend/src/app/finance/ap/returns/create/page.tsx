'use client';

import { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { ArrowLeft, Save, AlertCircle, RefreshCw, FileText, CheckSquare, Square, RotateCcw } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import { useToast } from '@/components/ui/use-toast';
import { accountsPayableService } from '@/services/accountsPayableService';
import { apiService } from '@/services/api.service';
import { formatCurrency } from '@/lib/utils';
import { Badge } from '@/components/ui/badge';
import { useDocumentSequence } from '@/hooks/use-document-sequence';
import { FinanceDocumentTypes } from '@/types/document-numbering';

interface SelectedReturnLine {
  id: string; // original line ID
  originalPoItemId?: string; // used for GRVs
  description: string;
  originalQuantity: number;
  alreadyReturnedQuantity: number;
  remainingQuantity: number;
  quantityReturned: number;
  unitPrice: number;
  taxGroupId?: string | null;
  taxRate: number;
  originalTaxAmount: number;
  taxAmount: number; // proportional tax amount
  lineTotal: number;
  selected: boolean;
}

export default function CreateReturnPage() {
  const router = useRouter();
  const { toast } = useToast();

  // Wizard State
  const [sourceType, setSourceType] = useState<'invoice' | 'grv'>('invoice');
  const [invoices, setInvoices] = useState<any[]>([]);
  const [grvs, setGrvs] = useState<any[]>([]);
  
  const [selectedDocId, setSelectedDocId] = useState<string>('');
  const [selectedDoc, setSelectedDoc] = useState<any | null>(null);

  // Return Header fields
  const [returnNumber, setReturnNumber] = useState<string>('');
  const returnSequence = useDocumentSequence('Finance', FinanceDocumentTypes.APSupplierReturn);
  const [returnDate, setReturnDate] = useState<string>(new Date().toISOString().split('T')[0]);
  const [reason, setReason] = useState<string>('');

  // Currency & Rate (Read-Only)
  const [currencyCode, setCurrencyCode] = useState<string>('GHS');
  const [exchangeRate, setExchangeRate] = useState<number>(1.0);

  // Return Lines state
  const [lines, setLines] = useState<SelectedReturnLine[]>([]);

  // Page status
  const [loading, setLoading] = useState(false);
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    loadSourceDocuments();
  }, [sourceType]);

  const loadSourceDocuments = async () => {
    setLoading(true);
    setSelectedDocId('');
    setSelectedDoc(null);
    setLines([]);
    try {
      if (sourceType === 'invoice') {
        const response = await accountsPayableService.getInvoices({ pageSize: 100 });
        // Only allow posted invoices: Approved, PartiallyPaid, Paid
        const eligible = (response?.items || []).filter((inv: any) => 
          inv.status === 'Approved' || inv.status === 'PartiallyPaid' || inv.status === 'Paid'
        );
        setInvoices(eligible);
      } else {
        const data = await apiService.get<any[]>('/finance/ap/purchase-receipts');
        setGrvs(data || []);
      }
    } catch (error) {
      console.error('Failed to load source documents', error);
      toast({ title: 'Error', description: 'Failed to load source documents', variant: 'destructive' });
    } finally {
      setLoading(false);
    }
  };

  const handleDocumentChange = async (id: string) => {
    setSelectedDocId(id);
    if (!id) {
      setSelectedDoc(null);
      setLines([]);
      return;
    }

    setLoading(true);
    try {
      if (sourceType === 'invoice') {
        const doc = await accountsPayableService.getInvoice(id);
        setSelectedDoc(doc);
        setCurrencyCode(doc.currencyCode);
        setExchangeRate(doc.exchangeRate);
        setReturnNumber('');

        // Load existing returns to calculate remaining quantities
        const allReturns = await accountsPayableService.getSupplierReturns();
        const invoiceReturns = allReturns.filter((r: any) => r.originalVendorInvoiceId === id && r.status !== 3); // 3 = Cancelled

        const initialLines: SelectedReturnLine[] = doc.lineItems.map((item: any) => {
          const alreadyReturned = invoiceReturns.reduce((sum: number, r: any) => {
            const matchedLine = r.lineItems.find((li: any) => li.originalVendorInvoiceLineItemId === item.id);
            return sum + (matchedLine ? matchedLine.quantityReturned : 0);
          }, 0);

          const remaining = item.quantity - alreadyReturned;

          return {
            id: item.id,
            description: item.description,
            originalQuantity: item.quantity,
            alreadyReturnedQuantity: alreadyReturned,
            remainingQuantity: remaining > 0 ? remaining : 0,
            quantityReturned: remaining > 0 ? remaining : 0,
            unitPrice: item.unitPrice,
            taxGroupId: item.taxGroupId,
            taxRate: item.taxRate,
            originalTaxAmount: item.taxAmount,
            taxAmount: remaining > 0 ? (remaining / item.quantity) * item.taxAmount : 0,
            lineTotal: remaining > 0 ? (remaining * item.unitPrice) + ((remaining / item.quantity) * item.taxAmount) : 0,
            selected: remaining > 0
          };
        });

        setLines(initialLines);
      } else {
        const doc = await apiService.get<any>(`/finance/ap/purchase-receipts/${id}`);
        setSelectedDoc(doc);
        setCurrencyCode(doc.financePurchaseOrder?.currencyCode || 'GHS');
        setExchangeRate(doc.financePurchaseOrder?.exchangeRate || 1.0);
        setReturnNumber('');

        // Load existing returns to calculate remaining quantities
        const allReturns = await accountsPayableService.getSupplierReturns();
        const grvReturns = allReturns.filter((r: any) => r.originalFinancePurchaseOrderReceiptId === id && r.status !== 3); // 3 = Cancelled

        const initialLines: SelectedReturnLine[] = doc.items.map((item: any) => {
          const alreadyReturned = grvReturns.reduce((sum: number, r: any) => {
            const matchedLine = r.lineItems.find((li: any) => li.originalFinancePurchaseOrderItemId === item.financePurchaseOrderItemId);
            return sum + (matchedLine ? matchedLine.quantityReturned : 0);
          }, 0);

          const remaining = item.quantityReceived - alreadyReturned;

          return {
            id: item.id,
            originalPoItemId: item.financePurchaseOrderItemId,
            description: item.financePurchaseOrderItem?.description || 'PO Line Item',
            originalQuantity: item.quantityReceived,
            alreadyReturnedQuantity: alreadyReturned,
            remainingQuantity: remaining > 0 ? remaining : 0,
            quantityReturned: remaining > 0 ? remaining : 0,
            unitPrice: item.financePurchaseOrderItem?.unitPrice || 0,
            taxGroupId: null,
            taxRate: 0,
            originalTaxAmount: 0,
            taxAmount: 0,
            lineTotal: remaining > 0 ? remaining * (item.financePurchaseOrderItem?.unitPrice || 0) : 0,
            selected: remaining > 0
          };
        });

        setLines(initialLines);
      }
    } catch (error) {
      console.error('Failed to load document details', error);
      toast({ title: 'Error', description: 'Failed to load document details', variant: 'destructive' });
    } finally {
      setLoading(false);
    }
  };

  const handleQtyChange = (id: string, qty: number) => {
    setLines(lines.map(l => {
      if (l.id === id) {
        let cleanQty = isNaN(qty) ? 0 : qty;
        if (cleanQty < 0) cleanQty = 0;
        if (cleanQty > l.remainingQuantity) cleanQty = l.remainingQuantity;

        // Proportional Tax calculation
        const cleanTax = l.originalQuantity > 0 
          ? (cleanQty / l.originalQuantity) * l.originalTaxAmount 
          : 0;

        const sub = cleanQty * l.unitPrice;
        
        return {
          ...l,
          quantityReturned: cleanQty,
          taxAmount: cleanTax,
          lineTotal: sub + cleanTax
        };
      }
      return l;
    }));
  };

  const toggleLine = (id: string) => {
    setLines(lines.map(l => {
      if (l.id === id) {
        if (!l.selected && l.remainingQuantity <= 0) return l;
        return { ...l, selected: !l.selected };
      }
      return l;
    }));
  };

  // Preview Calculations
  const selectedLines = lines.filter(l => l.selected && l.quantityReturned > 0);
  const returnSubTotal = selectedLines.reduce((sum, l) => sum + (l.quantityReturned * l.unitPrice), 0);
  const returnTaxAmount = selectedLines.reduce((sum, l) => sum + l.taxAmount, 0);
  const returnTotal = returnSubTotal + returnTaxAmount;
  const returnBaseTotal = returnTotal * exchangeRate;

  const handleSubmit = async () => {
    if (!selectedDocId) {
      toast({ title: 'Validation Error', description: 'Please select a source document', variant: 'destructive' });
      return;
    }

    if (selectedLines.length === 0) {
      toast({ title: 'Validation Error', description: 'Please select at least one line item with quantity to return', variant: 'destructive' });
      return;
    }

    setSubmitting(true);
    try {
      const payload = {
        returnNumber: returnSequence.allowManualEntry && returnNumber.trim() ? returnNumber.trim() : undefined,
        vendorId: selectedDoc.businessPartnerId || selectedDoc.vendorId || selectedDoc.financePurchaseOrder?.vendorId,
        vendorName: selectedDoc.supplierName || selectedDoc.vendorName || selectedDoc.financePurchaseOrder?.vendorName || '',
        originalVendorInvoiceId: sourceType === 'invoice' ? selectedDocId : null,
        originalFinancePurchaseOrderReceiptId: sourceType === 'grv' ? selectedDocId : null,
        returnDate: new Date(returnDate).toISOString(),
        reason: reason,
        currencyCode: currencyCode,
        exchangeRate: exchangeRate,
        lines: selectedLines.map(l => ({
          originalVendorInvoiceLineItemId: sourceType === 'invoice' ? l.id : null,
          originalFinancePurchaseOrderItemId: sourceType === 'grv' ? l.originalPoItemId : null,
          description: l.description,
          quantityReturned: Number(l.quantityReturned),
          unitPrice: Number(l.unitPrice),
          taxGroupId: l.taxGroupId,
          taxRate: l.taxRate,
          taxAmount: Number(l.taxAmount),
          lineTotal: Number(l.lineTotal)
        }))
      };

      const result = await accountsPayableService.createSupplierReturn(payload);
      toast({ title: 'Success', description: `Supplier Return ${result.returnNumber} created successfully` });
      router.push(`/finance/ap/returns/${result.id}`);
    } catch (error: any) {
      toast({ title: 'Error', description: error.message || 'Failed to create return', variant: 'destructive' });
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="space-y-6 p-8 max-w-[1600px] mx-auto">
      <div className="flex justify-between items-center">
        <div className="flex items-center space-x-4">
          <Button variant="outline" size="icon" onClick={() => router.push('/finance/ap/returns')}>
            <ArrowLeft className="h-4 w-4" />
          </Button>
          <div>
            <h1 className="text-3xl font-bold tracking-tight flex items-center gap-2">
              <RotateCcw className="h-8 w-8 text-primary" /> Record Supplier Return
            </h1>
            <p className="text-muted-foreground mt-1">Initiate a physical return slip and AP/GRV reversal wizard.</p>
          </div>
        </div>

        <Button 
          onClick={handleSubmit} 
          disabled={submitting || !selectedDocId || selectedLines.length === 0} 
          className="bg-primary hover:bg-primary/95 text-primary-foreground font-semibold px-6 shadow-md"
        >
          <Save className="h-4 w-4 mr-2" />
          {submitting ? 'Creating Draft Return...' : 'Save Draft Return'}
        </Button>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        
        {/* Left column: Context & Wizard */}
        <div className="lg:col-span-1 space-y-6">
          <Card className="glassmorphism border-slate-200/50 dark:border-slate-800/50">
            <CardHeader>
              <CardTitle>Return Source & Details</CardTitle>
              <CardDescription>Configure return source document and header parameters</CardDescription>
            </CardHeader>
            <CardContent className="space-y-4">
              
              {/* Source Type Toggle */}
              <div className="space-y-2">
                <label className="text-sm font-medium text-muted-foreground">Source Document Type</label>
                <div className="flex gap-2">
                  <Button 
                    type="button"
                    variant={sourceType === 'invoice' ? 'default' : 'outline'}
                    className="flex-1 font-semibold"
                    onClick={() => setSourceType('invoice')}
                  >
                    Posted Invoice
                  </Button>
                  <Button 
                    type="button"
                    variant={sourceType === 'grv' ? 'default' : 'outline'}
                    className="flex-1 font-semibold"
                    onClick={() => setSourceType('grv')}
                  >
                    GRV (Uninvoiced)
                  </Button>
                </div>
              </div>

              {/* Selector */}
              <div className="space-y-2">
                <label className="text-sm font-medium text-muted-foreground">Select Source Document</label>
                <select
                  value={selectedDocId}
                  onChange={(e) => handleDocumentChange(e.target.value)}
                  className="w-full p-2.5 rounded-md border bg-background text-sm focus:outline-none focus:ring-2 focus:ring-primary/40 transition"
                  disabled={loading}
                >
                  <option value="">
                    {loading ? 'Loading documents...' : `-- Select ${sourceType === 'invoice' ? 'Posted Invoice' : 'Uninvoiced GRV'} --`}
                  </option>
                  {sourceType === 'invoice' ? (
                    invoices.map(inv => (
                      <option key={inv.id} value={inv.id}>
                        {inv.invoiceNumber} - {inv.supplierName} ({formatCurrency(inv.totalAmount, inv.currencyCode || 'GHS')})
                      </option>
                    ))
                  ) : (
                    grvs.map(grv => (
                      <option key={grv.id} value={grv.id}>
                        {grv.receiptNumber} - PO: {grv.financePurchaseOrder?.orderNumber} ({grv.financePurchaseOrder?.currencyCode})
                      </option>
                    ))
                  )}
                </select>
              </div>

              {/* Readonly locked exchange rate card */}
              {selectedDoc && (
                <div className="rounded-lg border border-primary/20 bg-primary/5 p-4 space-y-2">
                  <div className="flex justify-between items-center text-sm font-semibold text-primary">
                    <span>Locked Exchange Rate</span>
                    <Badge variant="outline" className="border-primary/30 text-primary bg-primary/10">Immutability Enforced</Badge>
                  </div>
                  <div className="text-2xl font-bold text-slate-800 dark:text-slate-200">
                    {exchangeRate} <span className="text-xs font-normal text-muted-foreground">GHS per 1 {currencyCode}</span>
                  </div>
                  <p className="text-xs text-muted-foreground">
                    The exchange rate and currency are strictly locked to the original source document to prevent foreign exchange rate manipulation.
                  </p>
                </div>
              )}

              <div className="space-y-2">
                <label className="text-sm font-medium text-muted-foreground">Return Slip Number (Auto)</label>
                <Input 
                  value={returnSequence.allowManualEntry ? returnNumber : returnSequence.sampleNumber} 
                  onChange={(e) => setReturnNumber(e.target.value)}
                  placeholder={returnSequence.allowManualEntry ? `Auto: ${returnSequence.sampleNumber}` : undefined}
                  disabled={!selectedDocId || !returnSequence.allowManualEntry || returnSequence.loading}
                  className="font-mono"
                />
                <p className="text-xs text-muted-foreground">Assigned by the configured Supplier Return sequence when saved.</p>
              </div>

              <div className="space-y-2">
                <label className="text-sm font-medium text-muted-foreground">Return Date</label>
                <Input 
                  type="date"
                  value={returnDate} 
                  onChange={(e) => setReturnDate(e.target.value)} 
                  disabled={!selectedDocId}
                />
              </div>

              <div className="space-y-2">
                <label className="text-sm font-medium text-muted-foreground">Reason for Return</label>
                <Textarea 
                  value={reason} 
                  onChange={(e) => setReason(e.target.value)} 
                  placeholder="Explain why these goods are being returned..."
                  rows={3}
                  disabled={!selectedDocId}
                />
              </div>

            </CardContent>
          </Card>

          {/* Return Preview Totals */}
          {selectedDoc && (
            <Card className="glassmorphism border-slate-200/50 dark:border-slate-800/50">
              <CardHeader>
                <CardTitle>Return Summary Preview</CardTitle>
              </CardHeader>
              <CardContent className="space-y-3 font-semibold text-sm">
                <div className="flex justify-between py-1 border-b border-dashed">
                  <span className="text-muted-foreground">Subtotal Reclaimed:</span>
                  <span>{formatCurrency(returnSubTotal, currencyCode || 'GHS')}</span>
                </div>
                {sourceType === 'invoice' && (
                  <div className="flex justify-between py-1 border-b border-dashed text-emerald-600">
                    <span>Tax Reclaimed (Proportional):</span>
                    <span>{formatCurrency(returnTaxAmount, currencyCode || 'GHS')}</span>
                  </div>
                )}
                <div className="flex justify-between py-2 border-b text-lg font-bold text-slate-800 dark:text-slate-200">
                  <span>Reclaimed Total:</span>
                  <span>{formatCurrency(returnTotal, currencyCode || 'GHS')}</span>
                </div>
                {currencyCode !== 'GHS' && (
                  <div className="flex justify-between py-1 text-xs text-muted-foreground">
                    <span>Base Currency Reclaimed:</span>
                    <span>{formatCurrency(returnBaseTotal, 'GHS')}</span>
                  </div>
                )}
              </CardContent>
            </Card>
          )}

        </div>

        {/* Right column: Lines list grid */}
        <div className="lg:col-span-2 space-y-6">
          <Card className="glassmorphism border-slate-200/50 dark:border-slate-800/50">
            <CardHeader className="flex flex-row items-center justify-between pb-2">
              <div>
                <CardTitle>Select Items to Return</CardTitle>
                <CardDescription>Specify return quantities within remaining balances</CardDescription>
              </div>
              {selectedDoc && (
                <Badge className="bg-primary/20 text-primary border-primary/30">
                  {selectedLines.length} of {lines.length} Lines Selected
                </Badge>
              )}
            </CardHeader>
            <CardContent>
              {!selectedDocId ? (
                <div className="flex flex-col items-center justify-center p-12 text-center text-muted-foreground">
                  <AlertCircle className="h-12 w-12 text-muted-foreground/50 mb-3" />
                  <p className="font-semibold text-slate-700 dark:text-slate-300">No Source Document Selected</p>
                  <p className="text-sm text-muted-foreground mt-1 max-w-[300px]">
                    Choose Posted Invoice or GRV to load remaining items.
                  </p>
                </div>
              ) : (
                <div className="rounded-lg border border-slate-200 dark:border-slate-800 overflow-hidden">
                  <table className="w-full text-sm border-collapse text-left">
                    <thead className="bg-slate-50 dark:bg-slate-900/50 border-b">
                      <tr>
                        <th className="p-3 w-[45px]"></th>
                        <th className="p-3">Description</th>
                        <th className="p-3 text-right">Orig Qty</th>
                        <th className="p-3 text-right">Remaining</th>
                        <th className="p-3 text-center w-[120px]">Qty to Return</th>
                        <th className="p-3 text-right">Unit Price</th>
                        {sourceType === 'invoice' && <th className="p-3 text-right">Prop. Tax</th>}
                        <th className="p-3 text-right">Line Total</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-slate-200 dark:divide-slate-800">
                      {lines.map((line) => (
                        <tr 
                          key={line.id} 
                          className={`hover:bg-slate-50/50 dark:hover:bg-slate-900/10 transition-colors ${line.selected ? 'bg-primary/5' : ''}`}
                        >
                          <td className="p-3 text-center">
                            <button
                              type="button"
                              onClick={() => toggleLine(line.id)}
                              disabled={line.remainingQuantity <= 0}
                              className="text-muted-foreground focus:outline-none"
                            >
                              {line.selected ? (
                                <CheckSquare className="h-4.5 w-4.5 text-primary" />
                              ) : (
                                <Square className="h-4.5 w-4.5" />
                              )}
                            </button>
                          </td>
                          <td className="p-3">
                            <span className="font-medium block">{line.description}</span>
                            {line.alreadyReturnedQuantity > 0 && (
                              <span className="text-[10px] text-indigo-600 block mt-0.5">
                                {line.alreadyReturnedQuantity} already returned
                              </span>
                            )}
                          </td>
                          <td className="p-3 text-right">{line.originalQuantity}</td>
                          <td className="p-3 text-right font-semibold text-slate-700 dark:text-slate-300">
                            {line.remainingQuantity}
                          </td>
                          <td className="p-3">
                            <Input
                              type="number"
                              value={line.quantityReturned}
                              onChange={(e) => handleQtyChange(line.id, parseFloat(e.target.value))}
                              disabled={!line.selected}
                              className="h-8 py-0.5 text-center font-bold"
                              max={line.remainingQuantity}
                              min={0}
                            />
                          </td>
                          <td className="p-3 text-right">{formatCurrency(line.unitPrice, currencyCode || 'GHS')}</td>
                          {sourceType === 'invoice' && (
                            <td className="p-3 text-right text-emerald-600">
                              {formatCurrency(line.taxAmount, currencyCode || 'GHS')}
                            </td>
                          )}
                          <td className="p-3 text-right font-bold">
                            {formatCurrency(line.lineTotal, currencyCode || 'GHS')}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </CardContent>
          </Card>
        </div>

      </div>
    </div>
  );
}
