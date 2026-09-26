import React from 'react';
import Link from 'next/link';
import type { VendorInvoice } from '@/types/ap';

const payableLabels = {
  SurveyorFee: 'External surveyor fee',
  VendorConsideration: 'Land vendor payment',
  StampDuty: 'Stamp duty',
  OtherAcquisitionCosts: 'Other acquisition costs',
};

export function EstateInvoiceSource({ invoice }: { invoice?: Pick<VendorInvoice, 'estateAcquisitionId' | 'estatePayableKind'> }) {
  if (!invoice?.estateAcquisitionId || !invoice.estatePayableKind) return null;

  return (
    <div className="flex flex-wrap items-center justify-between gap-3 rounded-lg border bg-muted/30 px-4 py-3">
      <div>
        <p className="text-sm font-medium">Estate · {payableLabels[invoice.estatePayableKind]}</p>
        <p className="mt-1 text-xs text-muted-foreground">Supplier, charges and source accounts are controlled by the land acquisition.</p>
      </div>
      <Link className="text-sm font-medium text-primary underline-offset-4 hover:underline" href={`/estate/land-acquisition?acquisitionId=${encodeURIComponent(invoice.estateAcquisitionId)}`}>
        View land acquisition
      </Link>
    </div>
  );
}
