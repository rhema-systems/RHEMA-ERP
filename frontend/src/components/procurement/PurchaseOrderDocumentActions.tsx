'use client';

import React, { useRef, useState } from 'react';
import { Download, Loader2, Printer } from 'lucide-react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { CentralDocumentViewerDialog, type CentralDocumentViewerFile } from '@/components/document-management/CentralDocumentViewerDialog';
import { buildPurchaseOrderPdf, type PurchaseOrderDocumentCompany } from '@/lib/purchase-order-document';
import type { PurchaseOrderDetailDto } from '@/services/purchasingService';

export function PurchaseOrderDocumentActions({ order, getCompany }: {
  order: PurchaseOrderDetailDto;
  getCompany: () => Promise<PurchaseOrderDocumentCompany>;
}) {
  const [action, setAction] = useState<'print' | 'pdf' | null>(null);
  const [preview, setPreview] = useState<CentralDocumentViewerFile | null>(null);
  const busy = useRef(false);

  const openPreview = async (requestedAction: 'print' | 'pdf') => {
    if (busy.current) return;
    busy.current = true;
    setAction(requestedAction);
    try {
      const pdf = buildPurchaseOrderPdf(order, await getCompany());
      setPreview({
        title: order.orderNumber,
        fileName: `${order.orderNumber.replace(/[^a-z0-9._-]+/gi, '-')}.pdf`,
        contentType: 'application/pdf',
        pdfData: new Uint8Array(pdf.output('arraybuffer')),
        sourceLabel: 'Purchase order · Use the viewer toolbar to print or download.',
      });
    } catch (error) {
      toast.error(error instanceof Error ? error.message : 'Unable to prepare the purchase order PDF.');
    } finally {
      busy.current = false;
      setAction(null);
    }
  };

  return <>
    <Button variant="outline" onClick={() => void openPreview('print')} disabled={action !== null}>
      {action === 'print' ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Printer className="mr-2 h-4 w-4" />}Print
    </Button>
    <Button variant="outline" onClick={() => void openPreview('pdf')} disabled={action !== null}>
      {action === 'pdf' ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Download className="mr-2 h-4 w-4" />}Export PDF
    </Button>
    <CentralDocumentViewerDialog file={preview} open={Boolean(preview)}
      onOpenChange={(open) => { if (!open) setPreview(null); }} enableAnnotations={false} />
  </>;
}
