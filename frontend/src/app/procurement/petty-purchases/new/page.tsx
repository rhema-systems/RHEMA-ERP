'use client';

import React, { Suspense, useEffect, useRef, useState } from 'react';
import Link from 'next/link';
import { useRouter, useSearchParams } from 'next/navigation';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { purchasingService, type PurchaseRequisitionDetailDto } from '@/services/purchasingService';
import { tenderService } from '@/services/tenderService';

function PettyPurchasePreparation() {
  const id = useSearchParams().get('fromRequisitionId') ?? '';
  const router = useRouter();
  const [pr, setPr] = useState<PurchaseRequisitionDetailDto | null>(null);
  const [error, setError] = useState('');
  const [confirm, setConfirm] = useState(false);
  const [busy, setBusy] = useState(false);
  const createdId = useRef<string | null>(null);
  useEffect(() => {
    if (!id) { setError('Open this page from an approved purchase requisition.'); return; }
    let active = true;
    purchasingService.getPurchaseRequisitionById(id).then(value => {
      if (active) setPr(value);
    }).catch(reason => { if (active) setError(reason instanceof Error ? reason.message : 'Unable to load requisition.'); });
    return () => { active = false; };
  }, [id]);
  const prepare = async () => {
    if (!pr || busy) return false;
    setBusy(true);
    try {
      // If navigation is delayed after success, never repeat source creation.
      if (!createdId.current) {
        const source = await tenderService.createTender({
          sourcePurchaseRequisitionId: id,
          title: `Petty Purchase - ${pr.requisitionNumber}`,
          description: pr.justification,
          notes: pr.notes,
          tenderType: 'PettyPurchase', currency: pr.currency || 'GHS',
          estimatedValue: pr.totalAmount,
        });
        createdId.current = source.id;
      }
      router.push(`/procurement/tenders/${createdId.current}/exception-controls`);
      return true;
    } catch (reason) {
      const message = reason instanceof Error ? reason.message : 'Unable to prepare Petty Purchase.';
      setError(message); toast.error(message); return false;
    } finally { setBusy(false); }
  };
  return <div className="mx-auto max-w-3xl space-y-5 p-6">
    <Button variant="outline" asChild><Link href={`/procurement/purchase-requisitions/${id}`}>Back to requisition</Link></Button>
    <h1 className="text-2xl font-bold">Prepare Petty Purchase</h1>
    <p className="text-sm text-muted-foreground">Record one supplier quotation and the configured evidence, then send it for independent approval. A purchase order follows the approved recommendation.</p>
    {error && !confirm && <p role="alert" className="rounded-lg border border-red-200 p-4 text-red-700">{error}</p>}
    {pr && <Card><CardHeader><CardTitle>{pr.requisitionNumber}</CardTitle></CardHeader><CardContent className="space-y-4">
      <p>{pr.justification}</p>
      <p className="font-semibold">Approved limit: {pr.currency || 'GHS'} {pr.totalAmount.toLocaleString(undefined, { minimumFractionDigits: 2 })}</p>
      <ul className="space-y-2">{pr.items.map(item => <li key={item.id}>{item.itemDescription} — {item.quantity} {item.unitOfMeasure}</li>)}</ul>
      <p className="text-sm text-muted-foreground">Items and quantities are copied from the approved PR. Public bidding dates, invitations and scoring templates are not required for this route.</p>
      <Button disabled={busy || pr.status !== 'Approved'} onClick={() => setConfirm(true)}>Continue to supplier and quotation</Button>
    </CardContent></Card>}
    <ConfirmationDialog open={confirm} onOpenChange={setConfirm} title="Prepare this Petty Purchase?"
      description="Create a draft against the locked sourcing case. No supplier invitation, award, order or payment is made at this step."
      confirmText="Create preparation draft" onConfirm={prepare} isLoading={busy}>
      {error && <p role="alert" className="text-sm text-red-700">{error}</p>}
    </ConfirmationDialog>
  </div>;
}

export default function Page() { return <Suspense fallback={<p className="p-6">Loading requisition…</p>}><PettyPurchasePreparation/></Suspense>; }
