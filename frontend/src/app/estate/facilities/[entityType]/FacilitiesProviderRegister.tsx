'use client';

import Link from 'next/link';
import React from 'react';
import { Banknote, ExternalLink, FileText, RefreshCw, Search } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { useAuth } from '@/hooks/use-auth';
import { estateFacilitiesService, type FacilitiesProviderInvoice, type FacilitiesProviderOption } from '@/services/estate-facilities.service';
import { FacilitiesProviderRates } from './FacilitiesProviderRates';

const PAGE_SIZE = 20;

export function expiringProviderContracts(provider: FacilitiesProviderOption, today = new Date()) {
  const current = Date.UTC(today.getUTCFullYear(), today.getUTCMonth(), today.getUTCDate());
  return provider.contracts.filter((contract) => {
    if (!contract.endDate) return false;
    const end = new Date(`${contract.endDate.slice(0, 10)}T00:00:00Z`);
    const days = Math.round((end.getTime() - current) / 86_400_000);
    return Number.isFinite(days) && days >= 0 && days <= 30;
  });
}

export function FacilitiesProviderRegister() {
  const { hasAnyPermission, hasAnyRole } = useAuth();
  const canManageRates = hasAnyRole(['admin', 'Admin', 'SystemAdmin', 'SuperAdmin', 'TenantAdmin', 'Estate Manager', 'Facilities Manager']);
  const [providers, setProviders] = React.useState<FacilitiesProviderOption[]>([]);
  const [search, setSearch] = React.useState('');
  const [expiringOnly, setExpiringOnly] = React.useState(false);
  const [page, setPage] = React.useState(1);
  const [loading, setLoading] = React.useState(true);
  const [error, setError] = React.useState<string | null>(null);
  const [invoiceProvider, setInvoiceProvider] = React.useState<FacilitiesProviderOption | null>(null);
  const [rateProvider, setRateProvider] = React.useState<FacilitiesProviderOption | null>(null);
  const [invoices, setInvoices] = React.useState<FacilitiesProviderInvoice[]>([]);
  const [invoiceLoading, setInvoiceLoading] = React.useState(false);
  const [invoiceError, setInvoiceError] = React.useState<string | null>(null);

  const openInvoices = async (provider: FacilitiesProviderOption) => {
    setInvoiceProvider(provider);
    setInvoices([]);
    setInvoiceError(null);
    setInvoiceLoading(true);
    try {
      setInvoices(await estateFacilitiesService.getProviderInvoices(provider.id));
    } catch {
      setInvoiceError('Unable to load provider invoices.');
    } finally {
      setInvoiceLoading(false);
    }
  };

  const loadProviders = React.useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      setProviders(await estateFacilitiesService.getApprovedProviders());
    } catch {
      setError('Unable to load approved service providers.');
    } finally {
      setLoading(false);
    }
  }, []);

  React.useEffect(() => {
    void loadProviders();
  }, [loadProviders]);

  const query = search.trim().toLowerCase();
  const filtered = providers.filter((provider) =>
    (!expiringOnly || expiringProviderContracts(provider).length > 0) &&
    [provider.partnerCode, provider.partnerName,
      ...(provider.categories ?? [])].some((value) => value?.toLowerCase().includes(query))
  );
  const expiringCount = providers.filter((provider) => expiringProviderContracts(provider).length > 0).length;
  const totalPages = Math.max(1, Math.ceil(filtered.length / PAGE_SIZE));
  const currentPage = Math.min(page, totalPages);
  const visible = filtered.slice((currentPage - 1) * PAGE_SIZE, currentPage * PAGE_SIZE);

  return (
    <section className="space-y-3" aria-label="Approved service providers">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h2 className="text-lg font-semibold">Approved service providers</h2>
        {expiringCount > 0 ? <Badge variant="destructive">{expiringCount} contracts nearing expiry</Badge> : null}
        <Button size="sm" variant="outline" onClick={() => void loadProviders()} disabled={loading} title="Refresh providers">
          <RefreshCw className="h-4 w-4" />
          <span className="sr-only">Refresh providers</span>
        </Button>
      </div>
      <div className="relative max-w-sm">
        <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
        <Input aria-label="Search providers" placeholder="Search provider or category" value={search}
          onChange={(event) => { setSearch(event.target.value); setPage(1); }} className="pl-9" />
      </div>
      <Button size="sm" variant={expiringOnly ? 'default' : 'outline'} aria-pressed={expiringOnly}
        onClick={() => { setExpiringOnly(!expiringOnly); setPage(1); }}>Expiring contracts</Button>
      {error ? <p role="alert" className="text-sm text-destructive">{error}</p> : null}
      <div className="overflow-x-auto rounded-md border">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Provider</TableHead>
              <TableHead>Type</TableHead>
              <TableHead>Categories</TableHead>
              <TableHead>Active contracts</TableHead>
              <TableHead>Rating</TableHead>
              <TableHead>Contact</TableHead>
              <TableHead className="w-12"><span className="sr-only">Open</span></TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {loading ? (
              <TableRow><TableCell colSpan={7}>Loading providers...</TableCell></TableRow>
            ) : visible.length === 0 ? (
              <TableRow><TableCell colSpan={7}>No approved providers found.</TableCell></TableRow>
            ) : visible.map((provider) => (
              <TableRow key={provider.id}>
                <TableCell><div className="font-medium">{provider.partnerName}</div><div className="text-xs text-muted-foreground">{provider.partnerCode}</div></TableCell>
                <TableCell>{provider.partnerType}</TableCell>
                <TableCell>{provider.categories?.join(', ') || '-'}</TableCell>
                <TableCell>{provider.contracts.length ? provider.contracts.map((item) => (
                  <div key={item.id} className="flex flex-wrap items-center gap-1">
                    <span title={item.contractTitle}>{item.contractNumber}</span>
                    <span className="text-xs text-muted-foreground">{item.currency} {item.contractValue.toLocaleString(undefined, { minimumFractionDigits: 2 })}</span>
                    {item.paymentTerms ? <span className="text-xs text-muted-foreground" title="Payment terms">{item.paymentTerms}</span> : null}
                    {item.endDate ? <span className="text-xs text-muted-foreground">ends {new Date(item.endDate).toLocaleDateString()}</span> : null}
                    {expiringProviderContracts(provider).some((contract) => contract.id === item.id)
                      ? <Badge variant="destructive">Soon</Badge> : null}
                  </div>
                )) : '-'}</TableCell>
                <TableCell>{provider.performanceRating == null ? '-' : provider.performanceRating.toFixed(1)}</TableCell>
                <TableCell>{provider.phone || provider.email || '-'}</TableCell>
                <TableCell>
                  <Button size="icon" variant="ghost" title={`View rates for ${provider.partnerName}`}
                    aria-label={`View rates for ${provider.partnerName}`} onClick={() => setRateProvider(provider)}>
                    <Banknote className="h-4 w-4" />
                  </Button>
                  <Button size="icon" variant="ghost" title={`View invoices for ${provider.partnerName}`}
                    aria-label={`View invoices for ${provider.partnerName}`} onClick={() => void openInvoices(provider)}>
                    <FileText className="h-4 w-4" />
                  </Button>
                  {hasAnyPermission(['procurement.records.read']) ? (
                    <Button size="icon" variant="ghost" asChild title={`View ${provider.partnerName}`}>
                      <Link href={`/procurement/business-partners/${provider.id}`} aria-label={`View ${provider.partnerName}`}><ExternalLink className="h-4 w-4" /></Link>
                    </Button>
                  ) : null}
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>
      {totalPages > 1 ? (
        <div className="flex items-center justify-end gap-2 text-sm">
          <Button size="sm" variant="outline" disabled={currentPage === 1} onClick={() => setPage(currentPage - 1)}>Previous</Button>
          <span>{currentPage} of {totalPages}</span>
          <Button size="sm" variant="outline" disabled={currentPage === totalPages} onClick={() => setPage(currentPage + 1)}>Next</Button>
        </div>
      ) : null}
      <Dialog open={invoiceProvider !== null} onOpenChange={(open) => { if (!open) setInvoiceProvider(null); }}>
        <DialogContent className="max-w-4xl" aria-describedby={undefined}>
          <DialogHeader><DialogTitle>{invoiceProvider?.partnerName} invoices</DialogTitle></DialogHeader>
          {invoiceError ? <p role="alert" className="text-sm text-destructive">{invoiceError}</p> : null}
          <div className="max-h-[60vh] overflow-auto rounded-md border">
            <Table>
              <TableHeader><TableRow>
                <TableHead>Invoice</TableHead><TableHead>Date</TableHead><TableHead>Purchase order</TableHead>
                <TableHead>Status</TableHead><TableHead className="text-right">Amount</TableHead>
                <TableHead className="text-right">Outstanding</TableHead>
              </TableRow></TableHeader>
              <TableBody>
                {invoiceLoading ? <TableRow><TableCell colSpan={6}>Loading invoices...</TableCell></TableRow>
                  : invoices.length === 0 ? <TableRow><TableCell colSpan={6}>No supplier invoices are linked to this provider.</TableCell></TableRow>
                    : invoices.map((invoice) => (
                      <TableRow key={invoice.id}>
                        <TableCell>{hasAnyPermission(['procurement.records.read']) ? <Link className="underline" href={`/procurement/supplier-invoices/${invoice.id}`}>{invoice.invoiceNumber}</Link> : invoice.invoiceNumber}</TableCell>
                        <TableCell>{new Date(invoice.invoiceDate).toLocaleDateString()}</TableCell>
                        <TableCell>{invoice.purchaseOrderId ? 'Linked' : 'Direct invoice'}</TableCell>
                        <TableCell>{typeof invoice.status === 'number' ? ['Unknown', 'Draft', 'Pending approval', 'Approved', 'Partially paid', 'Paid', 'Overdue', 'Voided', 'Rejected', 'On hold'][invoice.status] ?? 'Unknown' : invoice.status}</TableCell>
                        <TableCell className="text-right">{invoice.currencyCode} {invoice.totalAmount.toLocaleString(undefined, { minimumFractionDigits: 2 })}</TableCell>
                        <TableCell className="text-right">{invoice.currencyCode} {Math.max(0, invoice.totalAmount - invoice.paidAmount).toLocaleString(undefined, { minimumFractionDigits: 2 })}</TableCell>
                      </TableRow>
                    ))}
              </TableBody>
            </Table>
          </div>
        </DialogContent>
      </Dialog>
      <FacilitiesProviderRates provider={rateProvider} canManage={canManageRates} onClose={() => setRateProvider(null)} />
    </section>
  );
}
