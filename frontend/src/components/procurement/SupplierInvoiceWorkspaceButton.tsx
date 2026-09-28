'use client';

import React, { useState } from 'react';
import { useRouter } from 'next/navigation';
import { ExternalLink, Loader2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { useToast } from '@/components/ui/use-toast';
import { accountsPayableService } from '@/services/accountsPayableService';
import { getProcurementProblemMessage } from '@/lib/procurement-tender-header-actions';

export function SupplierInvoiceWorkspaceButton({ invoiceId }: { invoiceId: string }) {
  const router = useRouter();
  const { toast } = useToast();
  const [loading, setLoading] = useState(false);

  async function openInvoice() {
    setLoading(true);
    try {
      const invoice = await accountsPayableService.getInvoice(invoiceId);
      router.push(`/procurement/supplier-invoices/${encodeURIComponent(invoice.id)}`);
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
