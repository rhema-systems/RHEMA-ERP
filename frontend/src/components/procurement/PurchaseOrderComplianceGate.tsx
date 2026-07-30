'use client';

import * as React from 'react';
import {
  AlertCircle,
  CheckCircle2,
  Loader2,
  RefreshCw,
  ShieldCheck,
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
  ProcurementPurchaseOrderComplianceDto,
  purchasingService,
} from '@/services/purchasingService';

export interface PurchaseOrderComplianceGateProps {
  purchaseOrderId: string;
  status: string;
  onReadinessChange?: (
    readiness: ProcurementPurchaseOrderComplianceDto | null
  ) => void;
}

export function PurchaseOrderComplianceGate({
  purchaseOrderId,
  status,
  onReadinessChange,
}: PurchaseOrderComplianceGateProps) {
  const [readiness, setReadiness] =
    React.useState<ProcurementPurchaseOrderComplianceDto | null>(null);
  const [loading, setLoading] = React.useState(true);
  const [error, setError] = React.useState<string | null>(null);

  const load = React.useCallback(async () => {
    if (!purchaseOrderId) return;
    setLoading(true);
    setError(null);
    try {
      const result =
        await purchasingService.getPurchaseOrderComplianceReadiness(
          purchaseOrderId
        );
      setReadiness(result);
      onReadinessChange?.(result);
    } catch (loadError) {
      const message =
        loadError instanceof Error
          ? loadError.message
          : 'Failed to evaluate purchase-order compliance.';
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
          Evaluating the pre-submit and pre-approval controls…
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
            Compliance readiness unavailable
          </CardTitle>
          <CardDescription className="text-red-800">
            The server will fail closed until this control can be evaluated.
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

  const passed = readiness.checks.filter((check) => check.passed).length;
  return (
    <Card
      data-testid="purchase-order-compliance-gate"
      className={
        readiness.isCompliant
          ? 'border-emerald-300 bg-emerald-50/40'
          : 'border-amber-300 bg-amber-50/50'
      }
    >
      <CardHeader className="gap-3">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div>
            <CardTitle className="flex items-center gap-2">
              <ShieldCheck className="h-5 w-5" />
              PO compliance gate
            </CardTitle>
            <CardDescription className="mt-1">
              Shared server control for supplier, budget, source, award,
              GHANEPS, contract, and signature readiness.
            </CardDescription>
          </div>
          <div className="flex items-center gap-2">
            <Badge
              className={
                readiness.isCompliant
                  ? 'bg-emerald-100 text-emerald-900'
                  : 'bg-amber-100 text-amber-950'
              }
            >
              {readiness.isCompliant
                ? 'Ready to progress'
                : `${readiness.blockedReasons.length} blocker(s)`}
            </Badge>
            <Button
              variant="ghost"
              size="sm"
              aria-label="Refresh compliance readiness"
              onClick={() => void load()}
            >
              <RefreshCw className="h-4 w-4" />
            </Button>
          </div>
        </div>
        <p className="text-sm">
          {passed}/{readiness.checks.length} checks pass · evaluated{' '}
          {new Date(readiness.evaluatedAtUtc).toLocaleString()}
        </p>
      </CardHeader>
      <CardContent className="grid gap-3 md:grid-cols-2">
        {readiness.checks.map((check) => {
          const Icon = check.passed ? CheckCircle2 : XCircle;
          return (
            <div
              key={check.key}
              data-testid="purchase-order-compliance-check"
              className={`rounded-md border p-3 ${
                check.passed
                  ? 'border-emerald-200 bg-white/80'
                  : 'border-amber-300 bg-white/90'
              }`}
            >
              <div className="flex items-start gap-2">
                <Icon
                  className={`mt-0.5 h-4 w-4 shrink-0 ${
                    check.passed ? 'text-emerald-700' : 'text-amber-700'
                  }`}
                />
                <div className="min-w-0">
                  <div className="flex flex-wrap items-center gap-2">
                    <p className="text-sm font-semibold">{check.label}</p>
                    {!check.required && (
                      <Badge variant="outline" className="text-[10px]">
                        Not required
                      </Badge>
                    )}
                  </div>
                  <p className="mt-1 text-xs text-muted-foreground">
                    {check.message}
                  </p>
                  {(check.reference || check.referenceId) && (
                    <p className="mt-2 truncate font-mono text-[10px] text-muted-foreground">
                      {check.reference || check.referenceId}
                    </p>
                  )}
                </div>
              </div>
            </div>
          );
        })}
      </CardContent>
    </Card>
  );
}
