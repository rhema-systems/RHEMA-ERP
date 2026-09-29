'use client';

import React from 'react';
import Link from 'next/link';
import { ExternalLink, RefreshCw } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { useAuth } from '@/hooks/use-auth';
import { estateFacilitiesService, type FacilitiesPropertyInvoice } from '@/services/estate-facilities.service';

export function FacilitiesPropertyInvoices({ propertyUnit, refreshKey }: { propertyUnit?: string; refreshKey?: string }) {
  const { hasAnyRole } = useAuth();
  const canRelease = hasAnyRole([
    'admin', 'Admin', 'SystemAdmin', 'SuperAdmin', 'TenantAdmin', 'Finance Officer',
    'Finance Manager', 'Accounts Officer', 'Senior Accountant', 'Financial Controller',
  ]);
  const [invoices, setInvoices] = React.useState<FacilitiesPropertyInvoice[]>([]);
  const [loading, setLoading] = React.useState(false);
  const [releasing, setReleasing] = React.useState<string | null>(null);
  const [error, setError] = React.useState<string | null>(null);

  const load = React.useCallback(async () => {
    if (!propertyUnit) {
      setInvoices([]);
      return;
    }
    setLoading(true);
    setError(null);
    try {
      setInvoices(await estateFacilitiesService.getPropertyArInvoices(propertyUnit));
    } catch {
      setError('Unable to load this property\'s Facilities invoices.');
    } finally {
      setLoading(false);
    }
  }, [propertyUnit]);

  React.useEffect(() => { void load(); }, [load, refreshKey]);

  const release = async (id: string) => {
    if (!propertyUnit) return;
    setReleasing(id);
    setError(null);
    try {
      await estateFacilitiesService.releaseArInvoice(id, propertyUnit);
      await load();
    } catch {
      setError('Finance could not release this invoice. Check its approval and posting requirements.');
    } finally {
      setReleasing(null);
    }
  };

  return (
    <section className="space-y-2" aria-label="Facilities invoices for property">
      <div className="flex items-center justify-between gap-2">
        <h3 className="text-sm font-semibold">Property invoices</h3>
        <Button size="icon" variant="ghost" onClick={() => void load()} disabled={!propertyUnit || loading} title="Refresh invoices" aria-label="Refresh invoices">
          <RefreshCw className="h-4 w-4" />
        </Button>
      </div>
      {error ? <p className="text-sm text-destructive" role="alert">{error}</p> : null}
      <div className="overflow-x-auto rounded-md border">
        <Table>
          <TableHeader><TableRow>
            <TableHead>Invoice</TableHead><TableHead>Date</TableHead><TableHead>Status</TableHead>
            <TableHead className="text-right">Amount</TableHead><TableHead className="text-right">Outstanding</TableHead>
            <TableHead className="w-28"><span className="sr-only">Actions</span></TableHead>
          </TableRow></TableHeader>
          <TableBody>
            {loading ? <TableRow><TableCell colSpan={6}>Loading invoices...</TableCell></TableRow>
              : invoices.length === 0 ? <TableRow><TableCell colSpan={6}>{propertyUnit ? 'No Facilities invoices for this property.' : 'Select a property to see its invoices.'}</TableCell></TableRow>
                : invoices.map((invoice) => (
                  <TableRow key={invoice.id}>
                    <TableCell>{invoice.invoiceNumber}</TableCell>
                    <TableCell>{new Date(invoice.invoiceDate).toLocaleDateString()}</TableCell>
                    <TableCell>{invoice.status}</TableCell>
                    <TableCell className="text-right">{invoice.currencyCode} {invoice.totalAmount.toLocaleString(undefined, { minimumFractionDigits: 2 })}</TableCell>
                    <TableCell className="text-right">{invoice.currencyCode} {Math.max(0, invoice.totalAmount - invoice.paidAmount).toLocaleString(undefined, { minimumFractionDigits: 2 })}</TableCell>
                    <TableCell><div className="flex justify-end gap-1">
                      {canRelease && invoice.canRelease ? <Button size="sm" onClick={() => void release(invoice.id)} disabled={releasing === invoice.id}>
                        {releasing === invoice.id ? 'Releasing...' : 'Release'}
                      </Button> : null}
                      <Button size="icon" variant="ghost" asChild title={`Open ${invoice.invoiceNumber} in Finance`}>
                        <Link href={`/finance/ar/invoices/${invoice.id}`} aria-label={`Open ${invoice.invoiceNumber} in Finance`}><ExternalLink className="h-4 w-4" /></Link>
                      </Button>
                    </div></TableCell>
                  </TableRow>
                ))}
          </TableBody>
        </Table>
      </div>
    </section>
  );
}
