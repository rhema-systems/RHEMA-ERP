'use client';

import * as React from 'react';
import {
  AlertCircle,
  CheckCircle2,
  Loader2,
  RefreshCw,
  ShieldAlert,
  XCircle,
} from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import {
  ProcurementPurchaseOrderSodReadinessDto,
  purchasingService,
} from '@/services/purchasingService';

export interface PurchaseOrderSodControlProps {
  purchaseOrderId: string;
  status: string;
  onReadinessChange?: (
    readiness: ProcurementPurchaseOrderSodReadinessDto | null
  ) => void;
}

export function PurchaseOrderSodControl({
  purchaseOrderId,
  status,
  onReadinessChange,
}: PurchaseOrderSodControlProps) {
  const [readiness, setReadiness] =
    React.useState<ProcurementPurchaseOrderSodReadinessDto | null>(null);
  const [loading, setLoading] = React.useState(true);
  const [error, setError] = React.useState<string | null>(null);

  const load = React.useCallback(async () => {
    if (!purchaseOrderId) return;
    setLoading(true);
    setError(null);
    try {
      const result =
        await purchasingService.getPurchaseOrderSodReadiness(purchaseOrderId);
      setReadiness(result);
      onReadinessChange?.(result);
    } catch (loadError) {
      const message =
        loadError instanceof Error
          ? loadError.message
          : 'Failed to evaluate purchase-order role independence.';
      setReadiness(null);
      setError(message);
      onReadinessChange?.(null);
    } finally {
      setLoading(false);
    }
  }, [onReadinessChange, purchaseOrderId]);

  React.useEffect(() => {
    void load();
  }, [load, status]);

  if (loading) {
    return (
      <Card>
        <CardContent className="flex items-center gap-3 py-8 text-sm text-muted-foreground">
          <Loader2 className="h-5 w-5 animate-spin" />
          Checking requester, creator, approver, and receiver independence…
        </CardContent>
      </Card>
    );
  }

  if (error || !readiness) {
    return (
      <Card className="border-red-300 bg-red-50/60">
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-red-950">
            <AlertCircle className="h-5 w-5" />
            PO role-separation control unavailable
          </CardTitle>
          <CardDescription className="text-red-800">
            Positive approval and goods receipt fail closed until the server can
            evaluate this control.
          </CardDescription>
        </CardHeader>
        <CardContent className="flex items-center justify-between gap-4">
          <p className="text-sm text-red-900">{error}</p>
          <Button variant="outline" size="sm" onClick={() => void load()}>
            <RefreshCw className="mr-2 h-4 w-4" />
            Retry
          </Button>
        </CardContent>
      </Card>
    );
  }

  const allAllowed = readiness.checks.every((check) => check.allowed);
  return (
    <Card
      data-testid="purchase-order-sod-control"
      className={
        allAllowed
          ? 'border-emerald-300 bg-emerald-50/40'
          : 'border-amber-300 bg-amber-50/50'
      }
    >
      <CardHeader className="gap-3">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div>
            <CardTitle className="flex items-center gap-2">
              <ShieldAlert className="h-5 w-5" />
              PO segregation of duties
            </CardTitle>
            <CardDescription className="mt-1">
              The source requester and PO creator cannot approve; the PO creator
              cannot confirm the primary goods receipt.
            </CardDescription>
          </div>
          <div className="flex items-center gap-2">
            <Badge
              className={
                allAllowed
                  ? 'bg-emerald-100 text-emerald-900'
                  : 'bg-amber-100 text-amber-950'
              }
            >
              {allAllowed ? 'Independent actor' : 'Restricted action(s)'}
            </Badge>
            <Button
              variant="ghost"
              size="sm"
              aria-label="Refresh PO role-separation readiness"
              onClick={() => void load()}
            >
              <RefreshCw className="h-4 w-4" />
            </Button>
          </div>
        </div>
        <p className="text-xs text-muted-foreground">
          Current actor · evaluated{' '}
          {new Date(readiness.evaluatedAtUtc).toLocaleString()}
        </p>
      </CardHeader>
      <CardContent className="grid gap-3 md:grid-cols-2">
        {readiness.checks.map((check) => {
          const Icon = check.allowed ? CheckCircle2 : XCircle;
          return (
            <div
              key={check.key}
              data-testid="purchase-order-sod-check"
              className={`rounded-md border p-3 ${
                check.allowed
                  ? 'border-emerald-200 bg-white/80'
                  : 'border-amber-300 bg-white/90'
              }`}
            >
              <div className="flex items-start gap-2">
                <Icon
                  className={`mt-0.5 h-4 w-4 shrink-0 ${
                    check.allowed ? 'text-emerald-700' : 'text-amber-700'
                  }`}
                />
                <div className="min-w-0">
                  <div className="flex flex-wrap items-center gap-2">
                    <p className="text-sm font-semibold">{check.label}</p>
                    <Badge variant="outline" className="font-mono text-[10px]">
                      {check.controlCode}
                    </Badge>
                  </div>
                  <p className="mt-1 text-xs text-muted-foreground">
                    {check.message}
                  </p>
                  <p className="mt-2 text-[10px] text-muted-foreground">
                    Protected lineage: {check.participantRoles.join(', ')}
                  </p>
                </div>
              </div>
            </div>
          );
        })}
      </CardContent>
    </Card>
  );
}
