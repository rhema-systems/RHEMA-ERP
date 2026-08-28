'use client';

import * as React from 'react';
import {
  AlertCircle,
  CheckCircle2,
  Loader2,
  RefreshCw,
  ShieldCheck,
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
  scope?: 'all' | 'receipt';
  initialReadiness?: ProcurementPurchaseOrderSodReadinessDto | null;
  onReadinessChange?: (
    readiness: ProcurementPurchaseOrderSodReadinessDto | null
  ) => void;
}

export function PurchaseOrderSodControl({
  purchaseOrderId,
  status,
  scope = 'all',
  initialReadiness = null,
  onReadinessChange,
}: PurchaseOrderSodControlProps) {
  const [readiness, setReadiness] =
    React.useState<ProcurementPurchaseOrderSodReadinessDto | null>(
      initialReadiness
    );
  const [loading, setLoading] = React.useState(initialReadiness === null);
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
    if (initialReadiness) {
      onReadinessChange?.(initialReadiness);
      return;
    }
    void load();
  }, [initialReadiness, load, onReadinessChange, status]);

  if (loading) {
    return (
      <Card>
        <CardContent className="flex items-center gap-3 py-8 text-sm text-muted-foreground">
          <Loader2 className="h-5 w-5 animate-spin" />
          Checking approval and receiving responsibilities…
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
            Approval and receipt controls unavailable
          </CardTitle>
          <CardDescription className="text-red-800">
            Approval and receiving actions are temporarily unavailable.
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

  const visibleChecks = scope === 'receipt'
    ? readiness.checks.filter((check) => check.key === 'receipt')
    : readiness.checks;
  const allAllowed = visibleChecks.length > 0 &&
    visibleChecks.every((check) => check.allowed);
  return (
    <Card
      data-testid="purchase-order-sod-control"
      className="border-slate-200 bg-white"
    >
      <CardHeader className="gap-3">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div>
            <CardTitle className="flex items-center gap-2">
              <ShieldCheck className="h-5 w-5 text-slate-700" />
              {scope === 'receipt'
                ? 'Receiving responsibility'
                : 'Approval and receiving responsibilities'}
            </CardTitle>
            <CardDescription className="mt-1">
              {scope === 'receipt'
                ? 'Receipt must be recorded by an authorised user other than the purchase-order creator.'
                : 'This purchase order must be approved and received by authorised users other than its creator.'}
            </CardDescription>
          </div>
          <div className="flex items-center gap-2">
            <Badge
              className={
                allAllowed
                  ? 'bg-emerald-100 text-emerald-900'
                  : 'bg-slate-100 text-slate-800'
              }
            >
              {allAllowed ? 'Available to you' : 'Independent user required'}
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
      </CardHeader>
      <CardContent
        className={scope === 'receipt' ? 'grid gap-3' : 'grid gap-3 md:grid-cols-2'}
      >
        {visibleChecks.map((check) => {
          const isApproval = check.key === 'approval';
          const label = isApproval ? 'Approval' : 'Goods receipt';
          const message = check.allowed
            ? isApproval
              ? 'You may approve this purchase order.'
              : 'You may record receipt for this purchase order.'
            : isApproval
              ? 'A different authorised user must approve this purchase order.'
              : 'A different authorised stores user must record receipt for this purchase order.';
          const Icon = check.allowed ? CheckCircle2 : ShieldCheck;
          return (
            <div
              key={check.key}
              data-testid="purchase-order-sod-check"
              className={`rounded-md border p-4 ${
                check.allowed
                  ? 'border-emerald-200 bg-white/80'
                  : 'border-slate-200 bg-slate-50/60'
              }`}
            >
              <div className="flex items-start gap-2">
                <Icon
                  className={`mt-0.5 h-4 w-4 shrink-0 ${
                    check.allowed ? 'text-emerald-700' : 'text-slate-600'
                  }`}
                />
                <div className="min-w-0">
                  <p className="text-sm font-semibold">{label}</p>
                  <p className="mt-1 text-xs text-muted-foreground">
                    {message}
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
