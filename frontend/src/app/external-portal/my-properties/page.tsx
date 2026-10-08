'use client';

import { useEffect, useMemo, useState } from 'react';
import Link from 'next/link';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { AlertCircle, ArrowRight, Building2, Download, Eye, FileSignature, Loader2, ReceiptText, Upload } from 'lucide-react';
import { CentralDocumentViewerDialog } from '@/components/document-management/CentralDocumentViewerDialog';
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import { useToast } from '@/hooks/use-toast';
import {
  externalEstateServicesService,
  type ExternalEstateServiceRequest,
  type ExternalPropertyPortfolio,
} from '@/services/external-estate-services.service';

const SALES_PROPERTY_HANDOFF_SOURCE = 'Sales - Estate Enquiry';
const MY_PROPERTIES_TABS = new Set([
  'properties',
  'bills',
  'transfers',
  'requests',
]);

const assetStatusLabels: Record<number, string> = {
  3: 'Reserved',
  4: 'Leased',
  5: 'Occupied',
  6: 'Sold',
};

const invoiceStatusLabels: Record<number, string> = {
  0: 'Draft',
  1: 'Pending approval',
  2: 'Sent',
  3: 'Partially paid',
  4: 'Paid',
  5: 'Overdue',
  6: 'Cancelled',
};

function enumLabel(value: string | number, labels: Record<number, string>) {
  return typeof value === 'number' ? labels[value] || String(value) : value;
}

function formatDate(value?: string | null) {
  if (!value) return 'Not recorded';
  return new Intl.DateTimeFormat('en-GB', {
    day: 'numeric',
    month: 'short',
    year: 'numeric',
  }).format(new Date(value));
}

function formatMoney(value: number, currency = 'GHS') {
  return new Intl.NumberFormat('en-GH', {
    style: 'currency',
    currency,
    minimumFractionDigits: 2,
  }).format(value);
}

function dueDateLabel(value?: string | null, balance = 0) {
  if (!value || balance <= 0) return null;
  const due = new Date(value);
  due.setHours(0, 0, 0, 0);
  const today = new Date();
  today.setHours(0, 0, 0, 0);
  const days = Math.round((due.getTime() - today.getTime()) / 86_400_000);
  if (days < 0) return { label: `${Math.abs(days)} day${days === -1 ? '' : 's'} overdue`, className: 'text-red-700' };
  if (days === 0) return { label: 'Due today', className: 'text-amber-700' };
  if (days <= 30) return { label: `Due in ${days} day${days === 1 ? '' : 's'}`, className: 'text-amber-700' };
  return null;
}

function requestField(request: ExternalEstateServiceRequest, key: string) {
  return request.fieldValues?.[key] || '';
}

export default function MyPropertiesPage() {
  const { toast } = useToast();
  const [portfolio, setPortfolio] = useState<ExternalPropertyPortfolio | null>(null);
  const [requests, setRequests] = useState<ExternalEstateServiceRequest[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [selectedTransfer, setSelectedTransfer] = useState<ExternalPropertyPortfolio['legalTransfers'][number] | null>(null);
  const [selectedInvoice, setSelectedInvoice] = useState<ExternalPropertyPortfolio['invoices'][number] | null>(null);
  const [transferFiles, setTransferFiles] = useState<Record<string, File | null>>({});
  const [transferNotes, setTransferNotes] = useState<Record<string, string>>({});
  const [savingTransferId, setSavingTransferId] = useState<string | null>(null);
  const [activeTab, setActiveTab] = useState('properties');

  useEffect(() => {
    const requestedTab = new URLSearchParams(window.location.search).get('tab');
    if (requestedTab && MY_PROPERTIES_TABS.has(requestedTab)) {
      setActiveTab(requestedTab);
    }
    const load = async () => {
      try {
        setLoading(true);
        setError(null);
        const [portfolioResult, requestResult] = await Promise.all([
          externalEstateServicesService.getMyProperties(),
          externalEstateServicesService.getMyRequests(),
        ]);
        setPortfolio(portfolioResult);
        setRequests(
          requestResult.filter(
            (request) =>
              request.sourceDepartment === SALES_PROPERTY_HANDOFF_SOURCE
          )
        );
      } catch (loadError) {
        setError(loadError instanceof Error ? loadError.message : 'Unable to load your properties.');
      } finally {
        setLoading(false);
      }
    };
    void load();
  }, []);

  const totals = useMemo(() => {
    const invoices = portfolio?.invoices || [];
    return {
      outstanding: invoices.reduce((sum, invoice) => sum + Math.max(invoice.balanceAmount, 0), 0),
      paid: invoices.reduce((sum, invoice) => sum + invoice.paidAmount, 0),
    };
  }, [portfolio]);

  if (loading) {
    return <div className="flex min-h-64 items-center justify-center"><Loader2 className="h-7 w-7 animate-spin text-slate-500" /></div>;
  }

  if (error) {
    return (
      <div className="flex min-h-64 flex-col items-center justify-center gap-3 text-center">
        <AlertCircle className="h-8 w-8 text-red-600" />
        <p className="font-medium text-slate-900">My Properties could not be loaded</p>
        <p className="max-w-xl text-sm text-slate-600">{error}</p>
      </div>
    );
  }

  const properties = portfolio?.properties || [];
  const invoices = portfolio?.invoices || [];
  const legalTransfers = portfolio?.legalTransfers || [];

  const downloadInvoice = async (invoice: ExternalPropertyPortfolio['invoices'][number]) => {
    try {
      const blob = await externalEstateServicesService.downloadPropertyInvoice(invoice.id);
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = `${invoice.invoiceNumber}.pdf`;
      anchor.click();
      URL.revokeObjectURL(url);
    } catch (downloadError) {
      toast({
        title: 'Invoice download failed',
        description: downloadError instanceof Error ? downloadError.message : 'Could not download this invoice.',
        variant: 'destructive',
      });
    }
  };

  const downloadLegalTransferDraft = async (transfer: ExternalPropertyPortfolio['legalTransfers'][number]) => {
    const blob = await externalEstateServicesService.downloadLegalTransferDraft(transfer.id);
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = `${transfer.referenceNumber || 'legal-transfer'}-draft.pdf`;
    anchor.click();
    URL.revokeObjectURL(url);
  };

  const uploadExecutedTransferForm = async (transfer: ExternalPropertyPortfolio['legalTransfers'][number]) => {
    const file = transferFiles[transfer.id];
    if (!file) {
      setError('Select the signed transfer form before uploading.');
      return;
    }

    try {
      setSavingTransferId(transfer.id);
      setError(null);
      const updated = await externalEstateServicesService.uploadExecutedTransferForm(
        transfer.id,
        file,
        transferNotes[transfer.id]?.trim() || null
      );
      setPortfolio((current) => current
        ? {
            ...current,
            legalTransfers: current.legalTransfers.map((item) =>
              item.id === updated.id ? updated : item
            ),
          }
        : current);
      setTransferFiles((current) => ({ ...current, [transfer.id]: null }));
      setTransferNotes((current) => ({ ...current, [transfer.id]: '' }));
      toast({
        title: 'Signed transfer form submitted',
        description: 'Legal has been notified to continue the transfer.',
        variant: 'success',
      });
    } catch (uploadError) {
      setError(uploadError instanceof Error ? uploadError.message : 'Could not upload the signed transfer form.');
    } finally {
      setSavingTransferId(null);
    }
  };

  return (
    <div className="space-y-6">
      <CentralDocumentViewerDialog
        open={Boolean(selectedInvoice)}
        onOpenChange={(open) => { if (!open) setSelectedInvoice(null); }}
        enableAnnotations={false}
        file={selectedInvoice ? {
          title: `Invoice ${selectedInvoice.invoiceNumber}`,
          fileName: `${selectedInvoice.invoiceNumber}.pdf`,
          renditionPath: `/api/estate/external/invoices/${encodeURIComponent(selectedInvoice.id)}/pdf`,
          contentType: 'application/pdf',
          sourceLabel: 'Customer property invoice',
        } : null}
      />
      <CentralDocumentViewerDialog
        open={Boolean(selectedTransfer)}
        onOpenChange={(open) => { if (!open) setSelectedTransfer(null); }}
        enableAnnotations={false}
        file={selectedTransfer ? {
          title: `${selectedTransfer.referenceNumber || 'Legal transfer'} draft`,
          fileName: `${selectedTransfer.referenceNumber || 'legal-transfer'}-draft.pdf`,
          renditionPath: `/api/estate/external/legal-transfers/${encodeURIComponent(selectedTransfer.id)}/draft`,
          contentType: 'application/pdf',
          sourceLabel: 'Legal transfer draft',
        } : null}
      />
      <div>
        <h1 className="text-2xl font-semibold text-slate-950">My Properties</h1>
        <p className="mt-1 text-sm text-slate-600">Your rentals, purchases, bills, receipts, and property requests.</p>
      </div>

      <div className="grid gap-4 sm:grid-cols-3">
        <div className="border border-slate-200 border-l-4 border-l-blue-600 bg-slate-50 p-4 shadow-sm">
          <div className="text-sm font-medium text-slate-700">Properties</div>
          <div className="mt-1 text-2xl font-semibold text-slate-950">{properties.length}</div>
        </div>
        <div className="border border-emerald-200 border-l-4 border-l-emerald-700 bg-emerald-50 p-4 shadow-sm">
          <div className="text-sm font-medium text-emerald-900">Payments recorded</div>
          <div className="mt-1 text-2xl font-semibold text-emerald-950">{formatMoney(totals.paid)}</div>
        </div>
        <div className="border border-amber-200 border-l-4 border-l-amber-600 bg-amber-50 p-4 shadow-sm">
          <div className="text-sm font-medium text-amber-900">Outstanding bills</div>
          <div className="mt-1 text-2xl font-semibold text-amber-950">{formatMoney(totals.outstanding)}</div>
        </div>
      </div>

      <Tabs value={activeTab} onValueChange={setActiveTab} className="space-y-4">
        <TabsList>
          <TabsTrigger value="properties">Properties</TabsTrigger>
          <TabsTrigger value="bills">Bills &amp; Receipts</TabsTrigger>
          <TabsTrigger value="transfers">Transfers</TabsTrigger>
          <TabsTrigger value="requests">My Requests</TabsTrigger>
        </TabsList>

        <TabsContent value="properties" className="space-y-4">
          {properties.map((property) => (
            <Link
              key={property.id}
              href={`/external-portal/my-properties/${property.id}`}
              className="block rounded-md focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-600"
            >
              <Card className="cursor-pointer rounded-md transition-colors hover:border-blue-400 hover:bg-blue-50/30">
                <CardHeader className="pb-3">
                <div className="flex flex-wrap items-start justify-between gap-3">
                  <div className="flex items-start gap-3">
                    <Building2 className="mt-0.5 h-5 w-5 text-blue-700" />
                    <div>
                      <CardTitle className="text-base">{property.name}</CardTitle>
                      <p className="mt-1 text-sm text-slate-500">{property.projectUnitCode || property.assetCode}</p>
                    </div>
                  </div>
                  <div className="flex gap-2">
                    <Badge variant="outline">{property.transactionType}</Badge>
                    <Badge>{enumLabel(property.status, assetStatusLabels)}</Badge>
                  </div>
                </div>
                </CardHeader>
                <CardContent>
                <dl className="grid gap-x-6 gap-y-4 text-sm sm:grid-cols-2 lg:grid-cols-4">
                  <div><dt className="text-slate-500">Location</dt><dd className="mt-1 font-medium">{property.location || property.town || property.district || 'Not recorded'}</dd></div>
                  <div><dt className="text-slate-500">Agreement</dt><dd className="mt-1 font-medium">{property.agreementReference || 'Not recorded'}</dd></div>
                  <div><dt className="text-slate-500">Agreement date</dt><dd className="mt-1 font-medium">{formatDate(property.agreementDate)}</dd></div>
                  <div><dt className="text-slate-500">Actual possession</dt><dd className="mt-1 font-medium">{formatDate(property.actualPossessionDate)}</dd></div>
                  {property.transactionType === 'Rental' ? <div><dt className="text-slate-500">Monthly rent</dt><dd className="mt-1 font-medium">{property.monthlyRent ? formatMoney(property.monthlyRent, property.currencyCode) : 'Not recorded'}</dd></div> : null}
                  {property.transactionType === 'Lease' ? <div><dt className="text-slate-500">Listed full-term amount</dt><dd className="mt-1 font-medium">{property.fullTermLeaseAmount ? formatMoney(property.fullTermLeaseAmount, property.currencyCode) : 'Not recorded'}</dd></div> : null}
                  {property.transactionType === 'Rental' || property.transactionType === 'Lease' ? <div><dt className="text-slate-500">Lease term</dt><dd className="mt-1 font-medium">{property.leaseTermYears ? `${property.leaseTermYears} years` : 'Not recorded'}</dd></div> : null}
                  {property.transactionType === 'Rental' ? <div><dt className="text-slate-500">Next billing date</dt><dd className="mt-1 font-medium">{formatDate(property.nextRentBillingDate)}</dd></div> : null}
                </dl>
                  <div className="mt-4 flex justify-end">
                    <span className="inline-flex h-9 items-center px-3 text-sm font-medium text-blue-700">
                    View property
                    <ArrowRight className="ml-2 h-4 w-4" />
                    </span>
                  </div>
                </CardContent>
              </Card>
            </Link>
          ))}
          {properties.length === 0 ? <div className="border border-dashed p-8 text-center text-sm text-slate-500">No rented or purchased properties are linked to your customer account yet.</div> : null}
        </TabsContent>

        <TabsContent value="bills" className="space-y-5">
          <div className="overflow-x-auto border bg-white">
            <Table className="min-w-[960px] table-fixed">
              <TableHeader><TableRow><TableHead className="w-40 pl-4">Invoice</TableHead><TableHead>Description</TableHead><TableHead className="w-40">Due date</TableHead><TableHead className="w-36 text-right">Amount</TableHead><TableHead className="w-36 text-right">Balance</TableHead><TableHead className="w-36">Status</TableHead><TableHead className="w-24 text-right">Document</TableHead></TableRow></TableHeader>
              <TableBody>
                {invoices.map((invoice) => {
                  const dueTiming = dueDateLabel(invoice.dueDate, invoice.balanceAmount);
                  return (
                  <TableRow key={invoice.id}>
                    <TableCell className="whitespace-nowrap pl-4 font-medium">{invoice.invoiceNumber}</TableCell>
                    <TableCell>{invoice.description || invoice.reference || 'Property charge'}</TableCell>
                    <TableCell><div>{formatDate(invoice.dueDate)}</div>{dueTiming ? <div className={`mt-1 text-xs font-medium ${dueTiming.className}`}>{dueTiming.label}</div> : null}</TableCell>
                    <TableCell className="text-right">{formatMoney(invoice.totalAmount, invoice.currencyCode)}</TableCell>
                    <TableCell className="text-right font-medium">{formatMoney(invoice.balanceAmount, invoice.currencyCode)}</TableCell>
                    <TableCell><Badge variant={invoice.balanceAmount <= 0 ? 'secondary' : 'outline'}>{enumLabel(invoice.status, invoiceStatusLabels)}</Badge></TableCell>
                    <TableCell className="text-right">
                      <div className="flex justify-end gap-1">
                        <Button type="button" size="icon" variant="ghost" title="View invoice" aria-label={`View invoice ${invoice.invoiceNumber}`} onClick={() => setSelectedInvoice(invoice)}><Eye className="h-4 w-4" /></Button>
                        <Button type="button" size="icon" variant="ghost" title="Download invoice" aria-label={`Download invoice ${invoice.invoiceNumber}`} onClick={() => void downloadInvoice(invoice)}><Download className="h-4 w-4" /></Button>
                      </div>
                    </TableCell>
                  </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          </div>
          {invoices.flatMap((invoice) => invoice.receipts.map((receipt) => ({ invoice, receipt }))).map(({ invoice, receipt }) => (
            <div key={`${invoice.id}-${receipt.customerPaymentId}`} className="flex flex-wrap items-center justify-between gap-4 border-b bg-white px-4 py-3 text-sm">
              <div className="flex items-center gap-3"><ReceiptText className="h-4 w-4 text-emerald-700" /><div><div className="font-medium">{receipt.paymentNumber}</div><div className="text-slate-500">For {invoice.invoiceNumber} · {formatDate(receipt.paymentDate)}</div></div></div>
              <div className="text-right"><div className="font-medium">{formatMoney(receipt.amount, receipt.paymentCurrencyCode)}</div><div className="text-slate-500">{receipt.paymentMethod}{receipt.transactionReference ? ` · ${receipt.transactionReference}` : ''}</div></div>
            </div>
          ))}
          {invoices.length === 0 ? <div className="border border-dashed p-8 text-center text-sm text-slate-500">No Estate bills have been issued to your customer account.</div> : null}
        </TabsContent>

        <TabsContent value="transfers" className="space-y-3">
          {legalTransfers.map((transfer) => (
            <div key={transfer.id} className="border border-slate-200 bg-white p-4 text-slate-950">
              <div className="flex flex-wrap items-start justify-between gap-4">
                <div>
                  <div className="font-semibold">{transfer.referenceNumber || transfer.title}</div>
                  <div className="mt-1 text-sm text-slate-700">
                    {transfer.propertyNumber || 'Property not recorded'} · {transfer.currentStageName}
                  </div>
                  <div className="mt-1 text-xs text-slate-500">
                    {transfer.currentAssignedRole || 'Awaiting assignment'}
                  </div>
                </div>
                <div className="flex flex-wrap gap-2">
                  <Badge variant="outline">{transfer.paymentStatus || 'Payment pending'}</Badge>
                  <Badge variant="secondary">{transfer.status}</Badge>
                </div>
              </div>

              <div className="mt-4 grid gap-3 text-sm md:grid-cols-3">
                <div><span className="text-slate-500">Sale request</span><div className="mt-1 font-medium">{transfer.sourceRecordReference || 'Not recorded'}</div></div>
                <div><span className="text-slate-500">Transfer fee</span><div className="mt-1 font-medium">{transfer.transferFeePayable ? formatMoney(Number(transfer.transferFeePayable)) : 'Not recorded'}</div></div>
                <div><span className="text-slate-500">Draft reference</span><div className="mt-1 font-medium">{transfer.draftDocumentReference || 'Not generated'}</div></div>
              </div>

              <div className="mt-4 flex flex-wrap gap-2">
                <Button
                  size="sm"
                  variant="outline"
                  disabled={!transfer.canDownloadDraft}
                  onClick={() => setSelectedTransfer(transfer)}
                >
                  <Eye className="mr-2 h-4 w-4" />
                  View draft
                </Button>
                <Button
                  size="sm"
                  variant="outline"
                  disabled={!transfer.canDownloadDraft}
                  onClick={() => void downloadLegalTransferDraft(transfer)}
                >
                  <Download className="mr-2 h-4 w-4" />
                  Download PDF
                </Button>
              </div>

              {transfer.executedTransferFormFileName ? (
                <div className="mt-4 rounded-md border border-emerald-200 bg-emerald-50 p-3 text-sm text-emerald-900">
                  <FileSignature className="mr-2 inline h-4 w-4" />
                  Signed transfer form submitted: {transfer.executedTransferFormFileName}
                </div>
              ) : transfer.canUploadExecutedTransferForm ? (
                <div className="mt-4 rounded-md border border-blue-200 bg-blue-50 p-4">
                  <div className="flex items-center gap-2 font-medium text-blue-900">
                    <FileSignature className="h-4 w-4" />
                    Sign and return transfer form
                  </div>
                  <p className="mt-1 text-sm text-blue-800">
                    Download the approved transfer draft, sign it, then upload the signed copy for Legal.
                  </p>
                  <div className="mt-3 grid gap-3 md:grid-cols-[1fr_1fr_auto]">
                    <Input
                      type="file"
                      className="bg-white"
                      accept=".pdf,.doc,.docx,.jpg,.jpeg,.png"
                      onChange={(event) =>
                        setTransferFiles((current) => ({
                          ...current,
                          [transfer.id]: event.target.files?.[0] || null,
                        }))
                      }
                    />
                    <Textarea
                      className="min-h-10 bg-white"
                      value={transferNotes[transfer.id] || ''}
                      onChange={(event) =>
                        setTransferNotes((current) => ({
                          ...current,
                          [transfer.id]: event.target.value,
                        }))
                      }
                      placeholder="Optional notes"
                    />
                    <Button
                      type="button"
                      disabled={savingTransferId === transfer.id || !transferFiles[transfer.id]}
                      onClick={() => void uploadExecutedTransferForm(transfer)}
                    >
                      {savingTransferId === transfer.id ? (
                        <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                      ) : (
                        <Upload className="mr-2 h-4 w-4" />
                      )}
                      Upload
                    </Button>
                  </div>
                </div>
              ) : null}
            </div>
          ))}
          {legalTransfers.length === 0 ? <div className="border border-dashed bg-white p-8 text-center text-sm text-slate-600">No Legal transfers are linked to your properties yet.</div> : null}
        </TabsContent>

        <TabsContent value="requests" className="space-y-3">
          {requests.map((request) => (
            <div key={request.id} className="flex flex-wrap items-center justify-between gap-4 border border-slate-200 bg-white p-4 text-slate-950">
              <div>
                <div className="font-semibold">{request.referenceNumber || request.title}</div>
                <div className="mt-1 text-sm text-slate-700">{requestField(request, 'requestType') || request.title} · {requestField(request, 'propertyUnit') || requestField(request, 'propertyNumber') || 'Property not recorded'}</div>
              </div>
              <div className="flex items-center gap-3">
                <Badge variant="outline" className="border-slate-400 bg-white text-slate-900">{requestField(request, 'applicationStatus') || request.status}</Badge>
                <Button asChild size="sm" variant="outline"><Link href={`/external-portal/my-property-requests/${request.id}`}>Open</Link></Button>
              </div>
            </div>
          ))}
          {requests.length === 0 ? <div className="border border-dashed bg-white p-8 text-center text-sm text-slate-600">No property requests are awaiting action.</div> : null}
        </TabsContent>
      </Tabs>
    </div>
  );
}
