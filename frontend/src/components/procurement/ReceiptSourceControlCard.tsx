'use client';
import { ProcurementControlAccordion } from '@/components/procurement/ProcurementControlAccordion';

import React from 'react';
import { AlertTriangle, CheckCircle2, RefreshCw } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { CardContent } from '@/components/ui/card';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import type { ProcurementReceiptSourceReadinessDto } from '@/services/purchasingService';

interface ReceiptSourceControlCardProps {
  readiness: ProcurementReceiptSourceReadinessDto | null;
  loading: boolean;
  error?: string | null;
  onRetry?: () => void;
}

const quantity = (value: number) =>
  new Intl.NumberFormat(undefined, { maximumFractionDigits: 4 }).format(value);

export function ReceiptSourceControlCard({
  readiness,
  loading,
  error,
  onRetry,
}: ReceiptSourceControlCardProps) {
  const allowed = readiness?.canReceive === true;

  return (
    <ProcurementControlAccordion
        title="Governed receipt source"
        summary={readiness?.sourceReference || 'Approved PO source and receipt capacity'}
        status={<Badge variant="outline">{loading ? 'Checking' : error || !readiness ? 'Blocked — unavailable' : allowed ? 'Ready' : 'Blocked'}</Badge>}
        notice={!loading && (error || (!allowed && (readiness?.message || 'Receipt controls are unavailable. Retry before continuing.')))}
        actions={onRetry && <Button type="button" variant="ghost" size="sm" aria-label="Refresh receipt source readiness" onClick={onRetry} disabled={loading}><RefreshCw className="h-4 w-4" /></Button>}
      >
        <p className="mb-4 text-sm text-muted-foreground">
              Server-derived PO source, remaining quantity, and receipt tolerance.
            </p>
      <CardContent className="space-y-4">
        {loading && (
          <div className="flex items-center gap-2 rounded-md border p-3 text-sm text-muted-foreground">
            <RefreshCw className="h-4 w-4 animate-spin" />
            Revalidating the approved source and receipt capacity…
          </div>
        )}

        {!loading && error && (
          <div className="flex flex-wrap items-center justify-between gap-3 rounded-md border border-red-200 bg-red-50 p-3 text-sm text-red-900">
            <span className="flex items-center gap-2">
              <AlertTriangle className="h-4 w-4" />
              {error}
            </span>
            {onRetry && (
              <Button type="button" variant="outline" size="sm" onClick={onRetry}>
                <RefreshCw className="mr-2 h-4 w-4" />
                Retry
              </Button>
            )}
          </div>
        )}

        {!loading && readiness && (
          <>
            <div className={`rounded-md border p-3 text-sm ${allowed ? 'border-emerald-200 bg-emerald-50 text-emerald-950' : 'border-amber-200 bg-amber-50 text-amber-950'}`}>
              <div className="flex items-start gap-2">
                {allowed ? (
                  <CheckCircle2 className="mt-0.5 h-4 w-4 shrink-0" />
                ) : (
                  <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
                )}
                <div>
                  <p className="font-medium">{readiness.message}</p>
                  <p className="mt-1 text-xs">
                    {readiness.sourceReference || 'No source reference'} · PO status{' '}
                    {readiness.purchaseOrderStatus} · tolerance {quantity(readiness.tolerancePercent)}%
                  </p>
                </div>
              </div>
            </div>

            <div className="overflow-x-auto rounded-md border">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>PO line</TableHead>
                    <TableHead className="text-right">Ordered</TableHead>
                    <TableHead className="text-right">Receipted</TableHead>
                    <TableHead className="text-right">Tolerance</TableHead>
                    <TableHead className="text-right">Remaining</TableHead>
                    <TableHead>Status</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {readiness.lines.map((line) => (
                    <TableRow key={line.purchaseOrderItemId}>
                      <TableCell>
                        <p className="font-medium">{line.itemCode || 'Uncoded item'}</p>
                        <p className="text-xs text-muted-foreground">
                          {line.itemName || line.purchaseOrderItemId} · {line.unitOfMeasure}
                        </p>
                      </TableCell>
                      <TableCell className="text-right">{quantity(line.orderedQuantity)}</TableCell>
                      <TableCell className="text-right">{quantity(line.previouslyReceiptedQuantity)}</TableCell>
                      <TableCell className="text-right">{quantity(line.toleranceQuantity)}</TableCell>
                      <TableCell className="text-right font-medium">{quantity(line.remainingQuantity)}</TableCell>
                      <TableCell>
                        <Badge variant={line.remainingQuantity > 0 ? 'outline' : 'secondary'}>
                          {line.remainingQuantity > 0 ? 'Available' : 'Full'}
                        </Badge>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>

            <div className="flex flex-wrap gap-1.5" aria-label="Decision register coverage">
              {readiness.decisionKeys.map((key) => (
                <Badge key={key} variant="outline" className="font-mono text-[10px]">
                  {key}
                </Badge>
              ))}
            </div>
          </>
        )}
      </CardContent>
    </ProcurementControlAccordion>
  );
}
