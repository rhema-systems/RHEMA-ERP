'use client';

import { useCallback, useEffect, useState } from 'react';
import { AlertTriangle, Loader2, RefreshCw } from 'lucide-react';

import { ReceiptInspectionControl } from '@/components/procurement/ReceiptInspectionControl';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import {
  purchasingService,
  type ProcurementReceiptInspectionOverviewDto,
} from '@/services/purchasingService';

const messageOf = (error: unknown) =>
  error instanceof Error ? error.message : 'The supplier inspection queue could not be loaded.';

export default function SupplierReceiptInspectionsPage() {
  const pageSize = 20;
  const [rows, setRows] = useState<ProcurementReceiptInspectionOverviewDto[]>([]);
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);
      const result = await purchasingService.getSupplierReceiptInspectionControls(page, pageSize);
      setRows(result.items);
      setTotalCount(result.totalCount);
    } catch (loadError) {
      setError(messageOf(loadError));
    } finally {
      setLoading(false);
    }
  }, [page]);

  useEffect(() => { void load(); }, [load]);

  return (
    <div className="space-y-6" data-testid="supplier-receipt-inspections">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div><h1 className="text-2xl font-semibold">Receipt inspections</h1><p className="text-sm text-muted-foreground">Track accepted and rejected deliveries, acknowledge formal rejection notes, and follow return or replacement closure.</p></div>
        <Button variant="outline" disabled={loading} onClick={() => void load()}><RefreshCw className="mr-2 h-4 w-4" />Refresh</Button>
      </div>
      {loading ? <Card><CardContent className="flex items-center gap-2 py-12 text-sm text-muted-foreground"><Loader2 className="h-4 w-4 animate-spin" />Loading supplier-scoped inspections…</CardContent></Card>
        : error ? <Alert variant="destructive"><AlertTriangle className="h-4 w-4" /><AlertTitle>Inspection queue unavailable</AlertTitle><AlertDescription>{error}</AlertDescription></Alert>
        : rows.length === 0 ? <Card><CardContent className="py-12 text-center text-sm text-muted-foreground">No governed receipt inspection is linked to your supplier account.</CardContent></Card>
        : <div className="space-y-4">
          {rows.map((row) => <ReceiptInspectionControl key={row.purchaseOrderReceiptId} receiptId={row.purchaseOrderReceiptId} initialOverview={row} external onChanged={() => void load()} />)}
          <div className="flex items-center justify-between text-sm text-muted-foreground">
            <span>Page {page} of {Math.max(1, Math.ceil(totalCount / pageSize))} · {totalCount} inspection{totalCount === 1 ? '' : 's'}</span>
            <div className="flex gap-2">
              <Button variant="outline" size="sm" disabled={loading || page <= 1} onClick={() => setPage((value) => Math.max(1, value - 1))}>Previous</Button>
              <Button variant="outline" size="sm" disabled={loading || page * pageSize >= totalCount} onClick={() => setPage((value) => value + 1)}>Next</Button>
            </div>
          </div>
        </div>}
    </div>
  );
}
