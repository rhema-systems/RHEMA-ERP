import * as React from 'react';
import { Input } from '@/components/ui/input';

export function ApprovedPurchaseOrderUnit({ unit }: { unit: string }) {
  return (
    <Input
      aria-label="Approved source unit"
      value={unit || 'EA'}
      readOnly
      title="Unit retained from the approved source"
    />
  );
}
