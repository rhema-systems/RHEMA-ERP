'use client';

import { useEffect, useMemo, useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { ArrowLeft, Building2, Download, Eye, FileCheck2, FileSignature, Loader2, ReceiptText } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { CentralDocumentViewerDialog } from '@/components/document-management/CentralDocumentViewerDialog';
import {
  externalEstateServicesService,
  type ExternalEstateServiceRequest,
  type ExternalPropertyPortfolio,
} from '@/services/external-estate-services.service';

const PROPERTY_LISTING_SOURCE = 'External Portal - Estate Listings';

const assetStatusLabels: Record<number, string> = {
  3: 'Reserved',
  4: 'Leased',
  5: 'Occupied',
  6: 'Sold',
};

function assetStatusLabel(value: string | number) {
  return typeof value === 'number' ? assetStatusLabels[value] || String(value) : value;
}

function formatDate(value?: string | null) {
  if (!value) return 'Not recorded';
  return new Intl.DateTimeFormat('en-GB', { day: 'numeric', month: 'short', year: 'numeric' }).format(new Date(value));
}

function formatMoney(value: number, currency = 'GHS') {
  return new Intl.NumberFormat('en-GH', { style: 'currency', currency, minimumFractionDigits: 2 }).format(value);
}

function requestField(request: ExternalEstateServiceRequest, key: string) {
  return request.fieldValues?.[key] || '';
}

function matchesProperty(request: ExternalEstateServiceRequest, references: string[]) {
  const requestReferences = [
    requestField(request, 'propertyUnit'),
    requestField(request, 'listingReference'),
    requestField(request, 'propertyNumber'),
  ].filter(Boolean);
  return requestReferences.some((value) => references.some((reference) => value.toLowerCase() === reference.toLowerCase()));
}

function matchesTransfer(
  transfer: ExternalPropertyPortfolio['legalTransfers'][number],
  references: string[]
) {
  const transferReferences = [
    transfer.propertyNumber,
    transfer.sourceRecordReference,
    transfer.fieldValues?.propertyNumber,
    transfer.fieldValues?.propertyFileReference,
  ].filter((value): value is string => Boolean(value));

  return transferReferences.some((value) =>
    references.some((reference) =>
      value.toLowerCase().includes(reference.toLowerCase())
    )
  );
}

export default function MyPropertyDetailPage() {
  const router = useRouter();
  const params = useParams<{ id: string }>();
  const [portfolio, setPortfolio] = useState<ExternalPropertyPortfolio | null>(null);
  const [requests, setRequests] = useState<ExternalEstateServiceRequest[]>([]);
  const [loading, setLoading] = useState(true);
  const [agreementOpen, setAgreementOpen] = useState(false);
  const [selectedInvoice, setSelectedInvoice] = useState<ExternalPropertyPortfolio['invoices'][number] | null>(null);
  const [selectedSignedDocument, setSelectedSignedDocument] = useState<{
    transfer: ExternalPropertyPortfolio['legalTransfers'][number];
    document: NonNullable<ExternalPropertyPortfolio['legalTransfers'][number]['signedDocuments']>[number];
  } | null>(null);

  useEffect(() => {
    const load = async () => {
      try {
        const [portfolioResult, requestResult] = await Promise.all([
          externalEstateServicesService.getMyProperties(),
          externalEstateServicesService.getMyRequests(),
        ]);
        setPortfolio(portfolioResult);
        setRequests(
          requestResult.filter(
            (request) => request.sourceDepartment === PROPERTY_LISTING_SOURCE
          )
        );
      } finally {
        setLoading(false);
      }
    };
    void load();
  }, []);

  const property = portfolio?.properties.find((item) => item.id === params.id);
  const references = useMemo(
    () => property ? [property.assetCode, property.projectUnitCode].filter((value): value is string => Boolean(value)) : [],
    [property]
  );
  const propertyRequests = useMemo(
    () => requests.filter((request) => matchesProperty(request, references)),
    [references, requests]
  );
  const propertyInvoices = useMemo(
    () => (portfolio?.invoices || []).filter((invoice) => {
      const source = `${invoice.reference || ''} ${invoice.description || ''}`.toLowerCase();
      return references.some((reference) => source.includes(reference.toLowerCase()));
    }),
    [portfolio, references]
  );
  const propertyTransfers = useMemo(
    () =>
      (portfolio?.legalTransfers || []).filter((transfer) =>
        matchesTransfer(transfer, references)
      ),
    [portfolio, references]
  );
  const signedPropertyDocuments = useMemo(
    () =>
      propertyTransfers.flatMap((transfer) =>
        (transfer.signedDocuments || []).map((document) => ({
          transfer,
          document,
        }))
      ),
    [propertyTransfers]
  );
  const agreementRequest = propertyRequests[0];

  const goBack = () => {
    if (window.history.length > 1) {
      router.back();
      return;
    }
    router.push('/external-portal/my-properties');
  };

  const downloadInvoice = async (invoice: ExternalPropertyPortfolio['invoices'][number]) => {
    const blob = await externalEstateServicesService.downloadPropertyInvoice(invoice.id);
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = `${invoice.invoiceNumber}.pdf`;
    anchor.click();
    URL.revokeObjectURL(url);
  };

  const downloadSignedDocument = async (
    transfer: ExternalPropertyPortfolio['legalTransfers'][number],
    signedDocument: NonNullable<ExternalPropertyPortfolio['legalTransfers'][number]['signedDocuments']>[number]
  ) => {
    const blob = await externalEstateServicesService.downloadLegalTransferDocument(
      transfer.id,
      signedDocument.id
    );
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = signedDocument.fileName || `${transfer.referenceNumber || 'signed-transfer'}.pdf`;
    anchor.click();
    URL.revokeObjectURL(url);
  };

  if (loading) return <div className="flex min-h-64 items-center justify-center"><Loader2 className="h-7 w-7 animate-spin text-slate-500" /></div>;

  if (!property) {
    return (
      <div className="space-y-5">
        <Button variant="ghost" onClick={goBack}><ArrowLeft className="mr-2 h-4 w-4" />Back</Button>
        <div className="border border-dashed p-8 text-center text-slate-600">This property was not found on your customer account.</div>
      </div>
    );
  }

  return (
    <div className="space-y-6 text-slate-950">
      <CentralDocumentViewerDialog
        open={agreementOpen}
        onOpenChange={setAgreementOpen}
        enableAnnotations={false}
        file={agreementRequest ? {
          title: `${property.name} final agreement`,
          fileName: `${property.agreementReference || 'property-agreement'}.pdf`,
          renditionPath: `/api/estate/external/requests/${encodeURIComponent(agreementRequest.id)}/agreement`,
          contentType: 'application/pdf',
          sourceLabel: 'Final signed property agreement',
        } : null}
      />
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
        open={Boolean(selectedSignedDocument)}
        onOpenChange={(open) => {
          if (!open) setSelectedSignedDocument(null);
        }}
        enableAnnotations={false}
        file={selectedSignedDocument ? {
          title: selectedSignedDocument.document.name,
          fileName: selectedSignedDocument.document.fileName || 'signed-property-file.pdf',
          renditionPath: `/api/estate/external/legal-transfers/${encodeURIComponent(selectedSignedDocument.transfer.id)}/documents/${encodeURIComponent(selectedSignedDocument.document.id)}/content`,
          contentType: 'application/pdf',
          sourceLabel: 'Signed property transfer file',
        } : null}
      />
      <Button variant="ghost" className="bg-white text-slate-800 hover:bg-slate-100 hover:text-slate-950" onClick={goBack}><ArrowLeft className="mr-2 h-4 w-4" />Back</Button>

      <div className="flex flex-wrap items-start justify-between gap-4">
        <div className="flex items-start gap-3">
          <Building2 className="mt-1 h-6 w-6 text-blue-700" />
          <div><h1 className="text-2xl font-semibold text-slate-950">{property.name}</h1><p className="mt-1 text-sm text-slate-600">{property.projectUnitCode || property.assetCode}</p></div>
        </div>
        <div className="flex gap-2">
          <Badge variant="outline" className="border-slate-300 bg-white text-slate-900">{property.transactionType}</Badge>
          <Badge className="bg-blue-700 text-white">{assetStatusLabel(property.status)}</Badge>
        </div>
      </div>

      <Card className="rounded-md border-slate-200 bg-white text-slate-950 shadow-sm">
        <CardHeader className="border-b border-slate-200 bg-slate-50"><CardTitle className="text-base text-slate-950">Property details</CardTitle></CardHeader>
        <CardContent className="pt-6 text-slate-950">
          <dl className="grid gap-x-6 gap-y-5 text-sm sm:grid-cols-2 lg:grid-cols-4">
            <div><dt className="text-slate-500">Location</dt><dd className="mt-1 font-medium text-slate-950">{property.location || property.town || property.district || 'Not recorded'}</dd></div>
            <div><dt className="text-slate-500">Agreement</dt><dd className="mt-1 font-medium text-slate-950">{property.agreementReference || 'Not recorded'}</dd></div>
            <div><dt className="text-slate-500">Agreement date</dt><dd className="mt-1 font-medium text-slate-950">{formatDate(property.agreementDate)}</dd></div>
            <div><dt className="text-slate-500">Actual possession</dt><dd className="mt-1 font-medium text-slate-950">{formatDate(property.actualPossessionDate)}</dd></div>
            {property.transactionType === 'Rental' ? <div><dt className="text-slate-500">Monthly rent</dt><dd className="mt-1 font-medium text-slate-950">{property.monthlyRent ? formatMoney(property.monthlyRent, property.currencyCode) : 'Not recorded'}</dd></div> : null}
            {property.transactionType === 'Lease' ? <div><dt className="text-slate-500">Listed full-term amount</dt><dd className="mt-1 font-medium text-slate-950">{property.fullTermLeaseAmount ? formatMoney(property.fullTermLeaseAmount, property.currencyCode) : 'Not recorded'}</dd></div> : null}
            {property.transactionType === 'Rental' || property.transactionType === 'Lease' ? <div><dt className="text-slate-500">Lease term</dt><dd className="mt-1 font-medium text-slate-950">{property.leaseTermYears ? `${property.leaseTermYears} years` : 'Not recorded'}</dd></div> : null}
            {property.transactionType === 'Rental' ? <div><dt className="text-slate-500">Next billing date</dt><dd className="mt-1 font-medium text-slate-950">{formatDate(property.nextRentBillingDate)}</dd></div> : null}
            {property.transactionType === 'Rental' ? <div><dt className="text-slate-500">Late-payment terms</dt><dd className="mt-1 font-medium text-slate-950">{property.rentPenaltyMethod && property.rentPenaltyMethod !== 'None' ? `${property.rentPenaltyMethod} ${property.rentPenaltyValue}${property.rentPenaltyMethod === 'Percentage' ? '%' : ` ${property.currencyCode}`} after ${property.rentGracePeriodDays} day${property.rentGracePeriodDays === 1 ? '' : 's'}` : 'No penalty configured'}</dd></div> : null}
          </dl>
          <div className="mt-5"><Button variant="outline" disabled={!agreementRequest} onClick={() => setAgreementOpen(true)}><FileCheck2 className="mr-2 h-4 w-4" />Open agreement</Button></div>
        </CardContent>
      </Card>

      {signedPropertyDocuments.length > 0 ? (
        <section className="space-y-4">
          <h2 className="text-lg font-semibold text-slate-950">
            Signed Property Files
          </h2>
          <div className="space-y-3">
            {signedPropertyDocuments.map(({ transfer, document }) => (
              <div
                key={`${transfer.id}-${document.id}`}
                className="border border-slate-200 bg-white p-4 text-slate-950"
              >
                <div className="flex flex-wrap items-start justify-between gap-4">
                  <div className="flex items-start gap-3">
                    <FileSignature className="mt-0.5 h-5 w-5 text-blue-700" />
                    <div>
                      <div className="font-semibold">{document.name}</div>
                      <div className="mt-1 text-sm text-slate-600">
                        {document.fileName || transfer.referenceNumber || 'Signed transfer document'}
                      </div>
                      <div className="mt-1 text-xs text-slate-500">
                        {transfer.referenceNumber || transfer.title}
                      </div>
                    </div>
                  </div>
                  <Badge variant="outline">Signed</Badge>
                </div>
                <div className="mt-3 flex flex-wrap gap-2">
                  <Button
                    size="sm"
                    variant="outline"
                    onClick={() => setSelectedSignedDocument({ transfer, document })}
                  >
                    <Eye className="mr-2 h-4 w-4" />
                    View file
                  </Button>
                  <Button
                    size="sm"
                    variant="outline"
                    onClick={() => void downloadSignedDocument(transfer, document)}
                  >
                    <Download className="mr-2 h-4 w-4" />
                    Download PDF
                  </Button>
                </div>
              </div>
            ))}
          </div>
        </section>
      ) : null}

      <section className="space-y-4">
        <h2 className="text-lg font-semibold text-slate-950">Bills &amp; Receipts</h2>
        <div className="space-y-3">
          {propertyInvoices.map((invoice) => (
            <div key={invoice.id} className="border border-slate-200 bg-white p-4 text-slate-950">
              <div className="flex flex-wrap justify-between gap-4"><div><div className="font-semibold">{invoice.invoiceNumber}</div><div className="mt-1 text-sm text-slate-600">{invoice.description || invoice.reference}</div></div><div className="text-right"><div className="font-semibold">{formatMoney(invoice.totalAmount, invoice.currencyCode)}</div><div className="text-sm text-slate-600">Balance {formatMoney(invoice.balanceAmount, invoice.currencyCode)}</div></div></div>
              <div className="mt-3 flex flex-wrap gap-2">
                <Button size="sm" variant="outline" onClick={() => setSelectedInvoice(invoice)}><Eye className="mr-2 h-4 w-4" />View invoice</Button>
                <Button size="sm" variant="outline" onClick={() => void downloadInvoice(invoice)}><Download className="mr-2 h-4 w-4" />Download PDF</Button>
              </div>
              {invoice.receipts.map((receipt) => <div key={receipt.customerPaymentId} className="mt-3 flex flex-wrap justify-between gap-3 border-t pt-3 text-sm"><div className="flex items-center gap-2"><ReceiptText className="h-4 w-4 text-emerald-700" /><span>{receipt.paymentNumber} · {formatDate(receipt.paymentDate)}</span></div><span className="font-medium">{formatMoney(receipt.amount, receipt.paymentCurrencyCode)}</span></div>)}
            </div>
          ))}
          {propertyInvoices.length === 0 ? <div className="border border-dashed p-8 text-center text-sm text-slate-500">No bills have been linked to this property.</div> : null}
        </div>
      </section>
    </div>
  );
}
