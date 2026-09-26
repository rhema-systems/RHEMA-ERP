'use client';

import React, { useState } from 'react';
import { useRouter } from 'next/navigation';
import { ExternalLink, Loader2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { useToast } from '@/components/ui/use-toast';
import { accountsPayableService } from '@/services/accountsPayableService';
import { getProcurementProblemMessage } from '@/lib/procurement-tender-header-actions';

/** Resolve persisted ownership so older Finance invoices retain their existing route. */
export function SupplierInvoiceWorkspaceButton({ invoiceId }: { invoiceId: string }) {
  const router = useRouter();
  const { toast } = useToast();
  const [loading, setLoading] = useState(false);

  async function openInvoice() {
    setLoading(true);
    try {
      const invoice = await accountsPayableService.getInvoice(invoiceId);
      const supplierWorkspace = !invoice.isOpeningBalance && Boolean(
        invoice.estateAcquisitionId || invoice.purchaseOrderId || invoice.acceptedSupplyKind ||
        invoice.isProcurementAutoInvoice || invoice.lineItems.some(line => line.landedCostItemId)
      );
      const base = supplierWorkspace ? '/procurement/supplier-invoices' : '/finance/ap/invoices';
      router.push(`${base}/${encodeURIComponent(invoice.id)}`);
    } catch (error) {
      toast({ title: 'Unable to open supplier invoice', description: getProcurementProblemMessage(error), variant: 'destructive' });
    } finally {
      setLoading(false);
    }
  }

  return <Button type="button" variant="outline" size="sm" disabled={loading} onClick={() => void openInvoice()}>
    {loading ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <ExternalLink className="mr-2 h-4 w-4" />}
    Open supplier invoice
  </Button>;
}
