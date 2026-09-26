'use client';

import { useEffect, useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useRouter } from 'next/navigation';
import { ArrowLeft, Loader2, Plus, Save, Trash2 } from 'lucide-react';

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { useToast } from '@/components/ui/use-toast';
import { calculateSupplierDebitNoteLine } from '@/lib/finance/supplier-debit-note';
import { loadApprovedInvoiceRate } from '@/lib/finance/invoice-exchange-rate';
import { accountsPayableService } from '@/services/accountsPayableService';
import { financeService } from '@/services/finance.service';
import { financeDataService } from '@/services/finance/finance-data.service';
import { taxDataService } from '@/services/finance/tax-data.service';
import type { SupplierDebitNoteLineRequest, VendorInvoice } from '@/types/ap';
import { SourceDocumentDimensionPanel } from '@/components/finance/dimensions/source-document-dimension-panel';
import {
  toFinanceDimensionValueRecord,
  toFinancePostingDimensionValues,
} from '@/lib/finance/source-document-dimensions';

interface DraftLine extends SupplierDebitNoteLineRequest {
  key: string;
}

const today = () => {
  const date = new Date();
  const offset = date.getTimezoneOffset();
  return new Date(date.getTime() - offset * 60_000).toISOString().slice(0, 10);
};

const blankLine = (): DraftLine => ({
  key: crypto.randomUUID(),
  description: '',
  quantity: 1,
  unitPrice: 0,
  discountPercentage: 0,
  taxRate: 0,
  taxAmount: 0,
  discountAmount: 0,
  lineTotal: 0,
});

const roundMoney = (value: number) =>
  Math.round((value + Number.EPSILON) * 100) / 100;
const roundRate = (value: number) =>
  Math.round((value + Number.EPSILON) * 10_000) / 10_000;

const linkedDraftLine = (
  source: VendorInvoice['lineItems'][number],
  quantity = source.quantity,
  key = crypto.randomUUID(),
  description = source.description
): DraftLine => {
  const safeQuantity = Math.max(Number(quantity) || 0, 0);
  const ratio = source.quantity > 0 ? safeQuantity / source.quantity : 0;
  const gross = roundMoney(safeQuantity * source.unitPrice);
  const discountAmount = roundMoney(source.discountAmount * ratio);
  const taxAmount = roundMoney(source.taxAmount * ratio);
  const net = roundMoney(gross - discountAmount);
  return {
    key,
    originalVendorInvoiceLineItemId: source.id,
    description,
    quantity: safeQuantity,
    unitPrice: source.unitPrice,
    taxGroupId: source.taxGroupId ?? undefined,
    taxRate: net > 0 ? roundRate((taxAmount / net) * 100) : 0,
    taxAmount,
    discountPercentage: source.discountPercentage,
    discountAmount,
    lineTotal: roundMoney(net + taxAmount),
  };
};

export function SupplierDebitNoteForm({ noteId }: { noteId?: string }) {
  const router = useRouter();
  const { toast } = useToast();
  const [vendorId, setVendorId] = useState('');
  const [invoiceId, setInvoiceId] = useState('standalone');
  const [debitNoteDate, setDebitNoteDate] = useState(today());
  const [supplierReference, setSupplierReference] = useState('');
  const [reason, setReason] = useState('');
  const [notes, setNotes] = useState('');
  const [currencyCode, setCurrencyCode] = useState('GHS');
  const [exchangeRate, setExchangeRate] = useState(1);
  const [lines, setLines] = useState<DraftLine[]>([blankLine()]);
  const [rowVersion, setRowVersion] = useState('');
  const [isSaving, setIsSaving] = useState(false);
  const [loadedNoteId, setLoadedNoteId] = useState<string>();
  const [defaultDimensionValues, setDefaultDimensionValues] = useState<Record<string, string>>({});
  const [lineDimensionValues, setLineDimensionValues] = useState<Record<string, Record<string, string>>>({});
  const [applyDefaultToAll, setApplyDefaultToAll] = useState(false);

  const { data: existing, isLoading: isLoadingExisting } = useQuery({
    queryKey: ['supplier-debit-note', noteId],
    queryFn: () =>
      accountsPayableService.getSupplierDebitNote(noteId as string),
    enabled: Boolean(noteId),
  });
  const { data: suppliers = [] } = useQuery({
    queryKey: ['finance', 'ap', 'entry-suppliers', 'debit-notes'],
    queryFn: async () =>
      (await accountsPayableService.getInvoiceSupplierEntryOptions()).filter(
        (supplier) => Boolean(supplier.businessPartnerId)
      ),
  });
  const { data: invoicePage } = useQuery({
    queryKey: ['posted-supplier-invoices', vendorId],
    queryFn: () =>
      accountsPayableService.getInvoices({
        businessPartnerId: vendorId,
        page: 1,
        pageSize: 200,
      }),
    enabled: Boolean(vendorId),
  });
  const { data: accounts = [] } = useQuery({
    queryKey: ['supplier-debit-note-posting-accounts'],
    queryFn: () =>
      financeDataService.getAccounts({ status: 'Active', take: 1000 }),
  });
  const { data: taxGroups = [] } = useQuery({
    queryKey: ['active-purchase-tax-groups'],
    queryFn: () => taxDataService.getActiveTaxGroups('Purchases'),
  });
  const { data: currencies = [] } = useQuery({
    queryKey: ['active-currencies'],
    queryFn: () => financeDataService.getCurrencies({ isActive: true }),
  });
  const { data: financeSettings } = useQuery({
    queryKey: ['finance-settings'],
    queryFn: () => financeDataService.getFinanceSettings(),
  });

  const postedInvoices = (invoicePage?.items ?? []).filter((invoice) =>
    Boolean(invoice.journalEntryId)
  );
  const selectedInvoice = postedInvoices.find(
    (invoice) => invoice.id === invoiceId
  );
  const isLinkedNote = invoiceId !== 'standalone';
  const postingAccounts = accounts.filter(
    (account) =>
      account.allowDirectPosting &&
      !account.isControlAccount &&
      account.status === 'Active'
  );

  useEffect(() => {
    if (!existing || loadedNoteId === existing.id) return;
    setLoadedNoteId(existing.id);
    setVendorId(existing.vendorId);
    setInvoiceId(existing.originalVendorInvoiceId ?? 'standalone');
    setDebitNoteDate(existing.debitNoteDate.slice(0, 10));
    setSupplierReference(existing.supplierCreditNoteReference ?? '');
    setReason(existing.reason ?? '');
    setNotes(existing.notes ?? '');
    setCurrencyCode(existing.currencyCode);
    setExchangeRate(existing.exchangeRate);
    setRowVersion(existing.rowVersion);
    setDefaultDimensionValues(
      toFinanceDimensionValueRecord(existing.financeDimensions?.defaultValues ?? [])
    );
    setLineDimensionValues(
      Object.fromEntries(
        (existing.financeDimensions?.lines ?? []).map((line) => [
          line.sourceLineId,
          toFinanceDimensionValueRecord(line.values),
        ])
      )
    );
    setLines(
      existing.lineItems.map((line) => ({
        key: line.id,
        lineItemType: line.lineItemType,
        originalVendorInvoiceLineItemId: line.originalVendorInvoiceLineItemId,
        glAccountId: line.glAccountId,
        description: line.description,
        quantity: line.quantity,
        unitPrice: line.unitPrice,
        taxGroupId: line.taxGroupId,
        taxRate: line.taxRate,
        taxAmount: line.taxAmount,
        discountPercentage: line.discountPercentage,
        discountAmount: line.discountAmount,
        lineTotal: line.lineTotal,
      }))
    );
  }, [existing, loadedNoteId]);

  const totals = useMemo(
    () =>
      lines.reduce(
        (result, line) => ({
          subTotal:
            result.subTotal +
            Math.max((line.lineTotal ?? 0) - (line.taxAmount ?? 0), 0),
          tax: result.tax + (line.taxAmount ?? 0),
          total: result.total + (line.lineTotal ?? 0),
        }),
        { subTotal: 0, tax: 0, total: 0 }
      ),
    [lines]
  );

  const recalculate = (line: DraftLine): DraftLine => ({
    ...calculateSupplierDebitNoteLine(
      line,
      taxGroups.find((group) => group.id === line.taxGroupId)
    ),
    key: line.key,
  });

  const updateLine = (key: string, patch: Partial<DraftLine>) => {
    setLines((current) =>
      current.map((line) => {
        if (line.key !== key) return line;
        if (!isLinkedNote) {
          return recalculate({ ...line, ...patch });
        }
        if (!selectedInvoice || !line.originalVendorInvoiceLineItemId)
          return line;
        const source = selectedInvoice.lineItems.find(
          (item) => item.id === line.originalVendorInvoiceLineItemId
        );
        if (!source) return line;
        const next = { ...line, ...patch };
        // Linked documents expose only description and credited quantity. Amount, tax and coding
        // values always derive from the immutable posted invoice evidence.
        return linkedDraftLine(
          source,
          Number(next.quantity),
          line.key,
          next.description
        );
      })
    );
  };

  const handleInvoiceChange = (value: string) => {
    setInvoiceId(value);
    if (value === 'standalone') {
      setLines([blankLine()]);
      return;
    }
    const invoice = postedInvoices.find((item) => item.id === value);
    if (!invoice) return;
    setCurrencyCode(invoice.currencyCode);
    setExchangeRate(invoice.exchangeRate);
    setLines(invoice.lineItems.map((line) => linkedDraftLine(line)));
  };

  const resolveApprovedRate = async () => {
    if (!financeSettings)
      throw new Error('Finance settings are still loading.');
    const snapshot = await loadApprovedInvoiceRate(
      {
        module: 'AP',
        transactionCurrency: currencyCode,
        functionalCurrency: financeSettings.baseCurrency,
        invoiceDate: new Date(`${debitNoteDate}T12:00:00`),
        settings: financeSettings,
      },
      (code, query) => financeService.getCurrentExchangeRate(code, query)
    );
    setExchangeRate(snapshot.rate);
    return snapshot.rate;
  };

  const handleSave = async () => {
    if (!vendorId || !debitNoteDate || !reason.trim()) {
      toast({
        title: 'Required fields missing',
        description: 'Select a supplier, date and commercial reason.',
        variant: 'destructive',
      });
      return;
    }
    if (
      lines.length === 0 ||
      lines.some(
        (line) =>
          !line.description.trim() ||
          (!isLinkedNote && !line.lineItemType) ||
          Number(line.quantity) <= 0 ||
          Number(line.unitPrice) <= 0
      )
    ) {
      toast({
        title: 'Invalid lines',
        description:
          'Every line requires a type, description, positive quantity and positive price.',
        variant: 'destructive',
      });
      return;
    }

    setIsSaving(true);
    try {
      const linkedInvoice =
        invoiceId === 'standalone' ? undefined : selectedInvoice;
      if (invoiceId !== 'standalone' && !linkedInvoice) {
        throw new Error(
          'The selected posted invoice is still loading or is no longer available.'
        );
      }
      // Linked notes reverse the source invoice's original functional measurement. Only a
      // standalone commercial adjustment obtains a new approved Daily rate on its own date.
      const approvedRate =
        linkedInvoice?.exchangeRate ?? (await resolveApprovedRate());
      const payload = {
        vendorId,
        originalVendorInvoiceId:
          invoiceId === 'standalone' ? undefined : invoiceId,
        supplierCreditNoteReference: supplierReference.trim() || undefined,
        debitNoteDate,
        reason: reason.trim(),
        notes: notes.trim() || undefined,
        currencyCode,
        exchangeRate: approvedRate,
        lines: lines.map(({ key, ...line }) => ({ id: key, ...line })),
        financeDimensions: {
          defaultDimensions: toFinancePostingDimensionValues(defaultDimensionValues),
          lines: isLinkedNote
            ? []
            : lines.flatMap((line) => line.glAccountId ? [{
                sourceLineId: line.key,
                accountId: line.glAccountId,
                dimensions: toFinancePostingDimensionValues(lineDimensionValues[line.key] || {}),
              }] : []),
          applyDefaultToEligibleLines: applyDefaultToAll,
        },
      };
      const saved = noteId
        ? await accountsPayableService.updateSupplierDebitNote(noteId, {
            ...payload,
            rowVersion,
          })
        : await accountsPayableService.createSupplierDebitNote(payload);
      toast({
        title: noteId ? 'Debit note updated' : 'Debit note created',
        description: 'The document remains a draft until submitted.',
      });
      router.push(`/finance/ap/supplier-debit-notes/${saved.id}`);
    } catch (error) {
      const raw =
        error instanceof Error
          ? error.message
          : 'Unable to save supplier debit note.';
      toast({
        title: 'Save failed',
        description: raw.replaceAll('invoice', 'supplier debit note'),
        variant: 'destructive',
      });
    } finally {
      setIsSaving(false);
    }
  };

  if (noteId && isLoadingExisting)
    return (
      <div className="p-8 text-muted-foreground">
        Loading supplier debit note...
      </div>
    );

  return (
    <div className="mx-auto max-w-[1400px] space-y-6 p-8">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-4">
          <Button variant="outline" size="icon" onClick={() => router.back()}>
            <ArrowLeft className="h-4 w-4" />
          </Button>
          <div>
            <h1 className="text-3xl font-bold">
              {noteId ? 'Edit Supplier Debit Note' : 'New Supplier Debit Note'}
            </h1>
            <p className="text-muted-foreground">
              A Finance-owned supplier credit document; it does not prove a
              physical Return-to-Vendor event.
            </p>
          </div>
        </div>
        <Button onClick={handleSave} disabled={isSaving}>
          {isSaving ? (
            <Loader2 className="mr-2 h-4 w-4 animate-spin" />
          ) : (
            <Save className="mr-2 h-4 w-4" />
          )}
          Save draft
        </Button>
      </div>

      <Alert>
        <AlertTitle>
          Commercial credit and physical return are separate controls
        </AlertTitle>
        <AlertDescription>
          Link a posted AP invoice when the supplier credit reverses that
          invoice. Use standalone coding only for a genuine Finance-owned
          commercial adjustment. FIN-INT-012/013 remain planned and quarantined.
        </AlertDescription>
      </Alert>

      <Card>
        <CardHeader>
          <CardTitle>Document</CardTitle>
        </CardHeader>
        <CardContent className="grid gap-5 md:grid-cols-2 lg:grid-cols-3">
          <div className="space-y-2">
            <Label>Supplier *</Label>
            <Select
              value={vendorId}
              onValueChange={(value) => {
                setVendorId(value);
                setInvoiceId('standalone');
                setLines([blankLine()]);
              }}
              disabled={Boolean(noteId)}
            >
              <SelectTrigger>
                <SelectValue placeholder="Select supplier" />
              </SelectTrigger>
              <SelectContent>
                {suppliers.map((supplier) => (
                  <SelectItem
                    key={supplier.id}
                    value={supplier.businessPartnerId as string}
                  >
                    {supplier.code} — {supplier.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-2">
            <Label>Original posted invoice</Label>
            <Select
              value={invoiceId}
              onValueChange={handleInvoiceChange}
              disabled={
                !vendorId ||
                Boolean(noteId)
              }
            >
              <SelectTrigger>
                <SelectValue placeholder="Standalone debit note" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="standalone">
                  Standalone debit note
                </SelectItem>
                {postedInvoices.map((invoice) => (
                  <SelectItem key={invoice.id} value={invoice.id}>
                    {invoice.invoiceNumber} — {invoice.currencyCode}{' '}
                    {invoice.totalAmount.toFixed(2)}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-2">
            <Label>Debit-note date *</Label>
            <Input
              type="date"
              value={debitNoteDate}
              onChange={(event) => setDebitNoteDate(event.target.value)}
            />
          </div>
          <div className="space-y-2">
            <Label>Supplier credit-note reference</Label>
            <Input
              value={supplierReference}
              onChange={(event) => setSupplierReference(event.target.value)}
              placeholder="Supplier's document number"
            />
          </div>
          <div className="space-y-2">
            <Label>Currency *</Label>
            <Select
              value={currencyCode}
              onValueChange={setCurrencyCode}
              disabled={isLinkedNote}
            >
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {currencies.map((currency) => (
                  <SelectItem
                    key={currency.currencyCode}
                    value={currency.currencyCode}
                  >
                    {currency.currencyCode} — {currency.currencyName}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            <p className="text-xs text-muted-foreground">
              {isLinkedNote
                ? 'Frozen source-invoice rate'
                : 'Approved Daily rate snapshot'}
              : {exchangeRate.toFixed(6)}
            </p>
          </div>
          <div className="space-y-2">
            <Label>Commercial reason *</Label>
            <Input
              value={reason}
              onChange={(event) => setReason(event.target.value)}
              placeholder="Pricing credit, rebate, invoice correction..."
            />
          </div>
          <div className="space-y-2 md:col-span-2 lg:col-span-3">
            <Label>Notes</Label>
            <Textarea
              value={notes}
              onChange={(event) => setNotes(event.target.value)}
            />
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Finance coding dimensions</CardTitle>
        </CardHeader>
        <CardContent>
          {isLinkedNote ? (
            <Alert>
              <AlertTitle>Inherited source evidence</AlertTitle>
              <AlertDescription>
                Each line inherits the exact dimensions of its originating
                posted invoice line. They cannot be cleared or overridden here.
              </AlertDescription>
            </Alert>
          ) : (
            <SourceDocumentDimensionPanel
              context={{
                sourceModule: 'AP',
                sourceDocumentType: 'SupplierDebitNote',
                postingAction: 'Post',
                sourceRoute: 'finance.ap.supplier-debit-notes.manual',
                contractVersion: '1.0',
              }}
              effectiveDate={debitNoteDate}
              lines={lines.map((line) => ({
                id: line.key,
                accountId: line.glAccountId,
                accountLabel: line.description || undefined,
              }))}
              defaultValues={defaultDimensionValues}
              lineValues={lineDimensionValues}
              onDefaultValuesChange={(values) => {
                setDefaultDimensionValues(values);
                setApplyDefaultToAll(false);
              }}
              onLineValuesChange={setLineDimensionValues}
              onApplyDefaultToAll={() => setApplyDefaultToAll(true)}
            />
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader className="flex-row items-center justify-between">
          <CardTitle>Credit lines</CardTitle>
          {invoiceId === 'standalone' && (
            <Button
              variant="outline"
              size="sm"
              onClick={() => setLines((current) => [...current, blankLine()])}
            >
              <Plus className="mr-2 h-4 w-4" />
              Add line
            </Button>
          )}
        </CardHeader>
        <CardContent className="space-y-4">
          {lines.map((line, index) => (
            <div
              key={line.key}
              className="grid gap-3 rounded-md border p-4 lg:grid-cols-12"
            >
              {!isLinkedNote && (
                <div className="space-y-2 lg:col-span-12">
                  <Label htmlFor={`credit-type-${line.key}`}>Credit type *</Label>
                  <Select value={line.lineItemType ?? ''} onValueChange={(value) =>
                    updateLine(line.key, value === 'Writeoff' ? {
                      lineItemType: value, glAccountId: undefined, taxGroupId: undefined,
                      taxRate: 0, taxAmount: 0, discountPercentage: 0, discountAmount: 0,
                    } : { lineItemType: value })}>
                    <SelectTrigger id={`credit-type-${line.key}`}><SelectValue placeholder="Select credit type" /></SelectTrigger>
                    <SelectContent>
                      <SelectItem value="Expense">Expense credit</SelectItem>
                      <SelectItem value="Service">Service credit</SelectItem>
                      <SelectItem value="Inventory">Inventory credit</SelectItem>
                      <SelectItem value="FixedAsset">Fixed asset credit</SelectItem>
                      <SelectItem value="Writeoff">Supplier liability write-off</SelectItem>
                    </SelectContent>
                  </Select>
                  {line.lineItemType === 'Writeoff' && <p className="text-sm text-muted-foreground">
                    Records an agreed reduction in the amount owed to the supplier. Uses the supplier's Writeoffs account
                    unless you select an override. Tax and discounts do not apply. Approval and posting are required;
                    applying the credit to outstanding invoices is a separate step.
                  </p>}
                </div>
              )}
              <div className="space-y-2 lg:col-span-3">
                <Label>Description *</Label>
                <Input
                  value={line.description}
                  onChange={(event) =>
                    updateLine(line.key, { description: event.target.value })
                  }
                />
              </div>
              <div className="space-y-2">
                <Label>Quantity *</Label>
                <Input
                  type="number"
                  min="0.0001"
                  step="0.0001"
                  value={line.quantity}
                  disabled={isLinkedNote && !selectedInvoice}
                  onChange={(event) =>
                    updateLine(line.key, {
                      quantity: Number(event.target.value),
                    })
                  }
                />
              </div>
              <div className="space-y-2 lg:col-span-2">
                <Label>Unit price *</Label>
                <Input
                  type="number"
                  min="0.01"
                  step="0.01"
                  value={line.unitPrice}
                  disabled={isLinkedNote}
                  onChange={(event) =>
                    updateLine(line.key, {
                      unitPrice: Number(event.target.value),
                    })
                  }
                />
              </div>
              <div className="space-y-2">
                <Label>Discount %</Label>
                <Input
                  type="number"
                  min="0"
                  max="99.99"
                  step="0.01"
                  value={line.discountPercentage ?? 0}
                  disabled={isLinkedNote || line.lineItemType === 'Writeoff'}
                  onChange={(event) =>
                    updateLine(line.key, {
                      discountPercentage: Number(event.target.value),
                    })
                  }
                />
              </div>
              <div className="space-y-2 lg:col-span-2">
                <Label>Purchase tax</Label>
                {isLinkedNote ? (
                  <Input
                    disabled
                    value={
                      line.taxGroupId
                        ? `${taxGroups.find((group) => group.id === line.taxGroupId)?.code ?? 'Frozen source tax'} · ${(line.taxRate ?? 0).toFixed(4)}% · ${currencyCode} ${(line.taxAmount ?? 0).toFixed(2)}`
                        : 'No tax'
                    }
                  />
                ) : (
                  <Select
                    value={line.taxGroupId ?? 'none'}
                    disabled={line.lineItemType === 'Writeoff'}
                    onValueChange={(value) =>
                      updateLine(line.key, {
                        taxGroupId: value === 'none' ? undefined : value,
                      })
                    }
                  >
                    <SelectTrigger>
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="none">No tax</SelectItem>
                      {taxGroups.map((group) => (
                        <SelectItem key={group.id} value={group.id}>
                          {group.code} — {group.name}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                )}
              </div>
              <div className="space-y-2 lg:col-span-2">
                <Label>{isLinkedNote ? 'Source coding' : line.lineItemType === 'Writeoff' ? 'Write-off account' : 'GL account *'}</Label>
                {isLinkedNote ? (
                  <Input disabled value="Reverses original invoice line" />
                ) : (
                  <Select
                    value={line.glAccountId ?? (line.lineItemType === 'Writeoff' ? 'supplier-default' : '')}
                    onValueChange={(value) =>
                      updateLine(line.key, { glAccountId: value === 'supplier-default' ? undefined : value })
                    }
                  >
                    <SelectTrigger>
                      <SelectValue placeholder="Select account" />
                    </SelectTrigger>
                    <SelectContent>
                      {line.lineItemType === 'Writeoff' && <SelectItem value="supplier-default">Use supplier Writeoffs account</SelectItem>}
                      {postingAccounts.filter(account => line.lineItemType !== 'Writeoff' || ['Revenue', 'Expense'].includes(account.accountType)).map((account) => (
                        <SelectItem key={account.id} value={account.id}>
                          {account.accountCode} — {account.accountName}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                )}
              </div>
              <div className="flex items-end justify-between lg:col-span-12">
                <span className="text-sm text-muted-foreground">
                  Line {index + 1}: tax {currencyCode}{' '}
                  {(line.taxAmount ?? 0).toFixed(2)} · total {currencyCode}{' '}
                  {(line.lineTotal ?? 0).toFixed(2)}
                </span>
                {lines.length > 1 && (
                  <Button
                    variant="ghost"
                    size="sm"
                    onClick={() =>
                      setLines((current) =>
                        current.filter((item) => item.key !== line.key)
                      )
                    }
                  >
                    <Trash2 className="mr-2 h-4 w-4" />
                    Remove
                  </Button>
                )}
              </div>
            </div>
          ))}
          <div className="ml-auto grid max-w-sm gap-2 rounded-md bg-muted/50 p-4 text-sm">
            <div className="flex justify-between">
              <span>Net amount</span>
              <strong>
                {currencyCode} {totals.subTotal.toFixed(2)}
              </strong>
            </div>
            <div className="flex justify-between">
              <span>Tax reversal</span>
              <strong>
                {currencyCode} {totals.tax.toFixed(2)}
              </strong>
            </div>
            <div className="flex justify-between border-t pt-2 text-base">
              <span>Total supplier credit</span>
              <strong>
                {currencyCode} {totals.total.toFixed(2)}
              </strong>
            </div>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
