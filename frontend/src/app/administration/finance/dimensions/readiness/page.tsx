'use client';

import { useEffect, useMemo, useState } from 'react';
import Link from 'next/link';
import {
  ArrowLeft,
  Download,
  Loader2,
  RefreshCw,
  ShieldCheck,
} from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { useToast } from '@/hooks/use-toast';
import { financeDataService } from '@/services/finance/finance-data.service';
import type {
  FinanceDimensionCertificationState,
  FinanceDimensionReadinessAssessment,
  FinanceDimensionRouteCertification,
} from '@/types/finance';

const nextState = (
  state: FinanceDimensionCertificationState
): FinanceDimensionCertificationState | null => {
  if (state === 'LegacyReadOnly') return 'CaptureOptional';
  if (state === 'CaptureOptional') return 'Enforced';
  return null;
};

const stateVariant = (state: FinanceDimensionCertificationState) =>
  state === 'Enforced'
    ? 'default'
    : state === 'CaptureOptional'
      ? 'secondary'
      : 'outline';

export default function FinanceDimensionReadinessPage() {
  const { toast } = useToast();
  const [routes, setRoutes] = useState<FinanceDimensionRouteCertification[]>(
    []
  );
  const [assessment, setAssessment] =
    useState<FinanceDimensionReadinessAssessment | null>(null);
  const [selectedRouteId, setSelectedRouteId] = useState<
    FinanceDimensionRouteCertification['routeId'] | null
  >(null);
  const [reason, setReason] = useState('');
  const [confirmed, setConfirmed] = useState(false);
  const [loading, setLoading] = useState(true);
  const [working, setWorking] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const selectedRoute = useMemo(
    () => routes.find((route) => route.routeId === selectedRouteId) ?? null,
    [routes, selectedRouteId]
  );

  const loadRoutes = async () => {
    setLoading(true);
    setError(null);
    try {
      const rows =
        await financeDataService.getFinanceDimensionCertificationRoutes();
      setRoutes(rows);
      if (!selectedRouteId && rows.length > 0)
        setSelectedRouteId(rows[0].routeId);
    } catch (cause) {
      setError(
        cause instanceof Error
          ? cause.message
          : 'Unable to load route certification state.'
      );
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void loadRoutes();
  }, []);

  const assess = async (route: FinanceDimensionRouteCertification) => {
    const target = nextState(route.state);
    if (!target) return;
    setWorking(true);
    setError(null);
    try {
      const result = await financeDataService.assessFinanceDimensionRoute(
        route.routeId,
        target
      );
      setAssessment(result);
      setSelectedRouteId(route.routeId);
      setConfirmed(false);
      setReason('');
      await loadRoutes();
    } catch (cause) {
      setError(
        cause instanceof Error ? cause.message : 'Readiness assessment failed.'
      );
    } finally {
      setWorking(false);
    }
  };

  const openLatest = async (route: FinanceDimensionRouteCertification) => {
    if (!route.latestAssessmentId) return;
    setWorking(true);
    try {
      setAssessment(
        await financeDataService.getFinanceDimensionReadinessAssessment(
          route.latestAssessmentId
        )
      );
      setSelectedRouteId(route.routeId);
      setConfirmed(false);
      setReason('');
    } catch (cause) {
      setError(
        cause instanceof Error
          ? cause.message
          : 'Unable to load readiness evidence.'
      );
    } finally {
      setWorking(false);
    }
  };

  const download = async () => {
    if (!assessment) return;
    const blob = await financeDataService.downloadFinanceDimensionReadinessCsv(
      assessment.id
    );
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = `finance-dimension-readiness-${assessment.id}.csv`;
    anchor.click();
    URL.revokeObjectURL(url);
  };

  const promote = async () => {
    if (!selectedRoute || !assessment) return;
    setWorking(true);
    setError(null);
    try {
      await financeDataService.promoteFinanceDimensionRoute(
        selectedRoute,
        assessment.id,
        reason.trim(),
        assessment.targetState
      );
      toast({
        title: 'Route promoted',
        description: `${selectedRoute.sourceRoute} is now ${assessment.targetState}.`,
      });
      setAssessment(null);
      setConfirmed(false);
      setReason('');
      await loadRoutes();
    } catch (cause) {
      setError(
        cause instanceof Error ? cause.message : 'Route promotion failed.'
      );
    } finally {
      setWorking(false);
    }
  };

  const promotable = Boolean(
    selectedRoute &&
      assessment &&
      assessment.routeId === selectedRoute.routeId &&
      assessment.blockerCount === 0 &&
      !assessment.isExpired &&
      confirmed &&
      reason.trim().length >= 10
  );

  return (
    <div className="container mx-auto space-y-6 p-6">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <Button asChild variant="ghost" className="mb-2 -ml-3">
            <Link href="/administration/finance/dimensions">
              <ArrowLeft className="mr-2 h-4 w-4" />
              Coding dimensions
            </Link>
          </Button>
          <h1 className="flex items-center gap-2 text-3xl font-bold">
            <ShieldCheck className="h-7 w-7" />
            Dimension Route Readiness
          </h1>
          <p className="text-muted-foreground">
            Assess and promote only code-recognized, tenant-scoped producer
            routes.
          </p>
        </div>
        <Button
          variant="outline"
          onClick={() => void loadRoutes()}
          disabled={loading || working}
        >
          <RefreshCw className="mr-2 h-4 w-4" />
          Refresh
        </Button>
      </div>

      <Alert>
        <AlertTitle>Governed promotion</AlertTitle>
        <AlertDescription>
          Promotion uses immutable, expiring evidence and reruns authoritative
          blocker checks in the certification transaction. Enforced routes
          cannot be downgraded here.
        </AlertDescription>
      </Alert>
      {error && (
        <Alert variant="destructive">
          <AlertTitle>Request failed</AlertTitle>
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      )}

      {loading ? (
        <div className="flex items-center gap-2">
          <Loader2 className="h-4 w-4 animate-spin" />
          Loading routes…
        </div>
      ) : (
        <div className="grid gap-4 xl:grid-cols-2">
          {routes.map((route) => {
            const target = nextState(route.state);
            return (
              <Card
                key={route.routeId}
                className={
                  selectedRouteId === route.routeId ? 'border-primary' : ''
                }
              >
                <CardHeader>
                  <div className="flex items-start justify-between gap-3">
                    <div>
                      <CardTitle className="text-lg">
                        {route.sourceRoute}
                      </CardTitle>
                      <CardDescription>
                        {route.producerModule} · {route.documentType} · contract{' '}
                        {route.contractVersion}
                      </CardDescription>
                    </div>
                    <Badge variant={stateVariant(route.state)}>
                      {route.state}
                    </Badge>
                  </div>
                </CardHeader>
                <CardContent className="space-y-3 text-sm">
                  <p>{route.notes}</p>
                  <p className="text-muted-foreground">
                    Owner: {route.owner} · Grain: {route.grain}
                  </p>
                  {route.latestAssessmentId && (
                    <p>
                      Latest evidence: {route.latestBlockerCount ?? 0}{' '}
                      blocker(s)
                      {route.latestAssessmentExpiresAt
                        ? `, expires ${new Date(route.latestAssessmentExpiresAt).toLocaleString()}`
                        : ''}
                    </p>
                  )}
                  <div className="flex flex-wrap gap-2">
                    {target && (
                      <Button
                        size="sm"
                        onClick={() => void assess(route)}
                        disabled={working}
                      >
                        Assess for {target}
                      </Button>
                    )}
                    {route.latestAssessmentId && (
                      <Button
                        size="sm"
                        variant="outline"
                        onClick={() => void openLatest(route)}
                        disabled={working}
                      >
                        View latest evidence
                      </Button>
                    )}
                  </div>
                </CardContent>
              </Card>
            );
          })}
        </div>
      )}

      {assessment && selectedRoute && (
        <Card>
          <CardHeader>
            <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
              <div>
                <CardTitle>Readiness evidence</CardTitle>
                <CardDescription>
                  {assessment.sourceRoute} · {assessment.currentState} →{' '}
                  {assessment.targetState} · assessed{' '}
                  {new Date(assessment.assessedAt).toLocaleString()}
                </CardDescription>
              </div>
              <Button variant="outline" onClick={() => void download()}>
                <Download className="mr-2 h-4 w-4" />
                Download protected CSV
              </Button>
            </div>
          </CardHeader>
          <CardContent className="space-y-5">
            <div className="flex flex-wrap gap-2">
              <Badge
                variant={
                  assessment.blockerCount === 0 ? 'default' : 'destructive'
                }
              >
                {assessment.blockerCount} blocker(s)
              </Badge>
              <Badge variant={assessment.isExpired ? 'destructive' : 'outline'}>
                {assessment.isExpired
                  ? 'Expired'
                  : `Expires ${new Date(assessment.expiresAt).toLocaleString()}`}
              </Badge>
              <Badge variant="outline">
                Watermark {assessment.dataVersionWatermark}
              </Badge>
            </div>
            {assessment.blockers.length > 0 && (
              <div className="overflow-x-auto rounded-md border">
                <table className="w-full text-left text-sm">
                  <thead className="bg-muted">
                    <tr>
                      <th className="p-3">Document</th>
                      <th className="p-3">Lifecycle</th>
                      <th className="p-3">Issue</th>
                      <th className="p-3">Budget / reservation</th>
                      <th className="p-3">Remediation</th>
                    </tr>
                  </thead>
                  <tbody>
                    {assessment.blockers.map((blocker, index) => (
                      <tr
                        key={`${blocker.code}-${blocker.documentId ?? index}`}
                        className="border-t"
                      >
                        <td className="p-3">
                          {blocker.documentLink ? (
                            <Link
                              className="text-primary underline"
                              href={blocker.documentLink}
                            >
                              {blocker.documentReference ?? blocker.documentId}
                            </Link>
                          ) : (
                            (blocker.documentReference ??
                            blocker.documentId ??
                            'Route')
                          )}
                        </td>
                        <td className="p-3">{blocker.lifecycleState ?? '—'}</td>
                        <td className="p-3">
                          <div className="font-medium">{blocker.code}</div>
                          <div>{blocker.dimensionIssue ?? blocker.message}</div>
                          {blocker.fixedRuleDrift && (
                            <Badge variant="destructive" className="mt-1">
                              Fixed-rule drift
                            </Badge>
                          )}
                        </td>
                        <td className="p-3">
                          {blocker.staleBudgetEvidence
                            ? 'Stale evidence'
                            : 'Current'}
                          {blocker.activeReservationState
                            ? ` · ${blocker.activeReservationState}`
                            : ''}
                        </td>
                        <td className="p-3">
                          {blocker.remediationStatus ?? 'Required'}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}

            {assessment.blockerCount === 0 && !assessment.isExpired && (
              <div className="max-w-2xl space-y-4 rounded-md border p-4">
                <div className="space-y-2">
                  <Label htmlFor="promotion-reason">
                    Promotion reason and evidence reference
                  </Label>
                  <Textarea
                    id="promotion-reason"
                    value={reason}
                    onChange={(event) => setReason(event.target.value)}
                    placeholder="Record the UAT/readiness decision and evidence reference (minimum 10 characters)."
                  />
                </div>
                <div className="flex items-start gap-2">
                  <Checkbox
                    id="promotion-confirmation"
                    checked={confirmed}
                    onCheckedChange={(value) => setConfirmed(value === true)}
                  />
                  <Label
                    htmlFor="promotion-confirmation"
                    className="font-normal"
                  >
                    I confirm this governed transition. New or re-entered
                    documents will fail closed when required dimension evidence
                    is missing.
                  </Label>
                </div>
                <Button
                  onClick={() => void promote()}
                  disabled={!promotable || working}
                >
                  {working && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                  Promote to {assessment.targetState}
                </Button>
              </div>
            )}
          </CardContent>
        </Card>
      )}
    </div>
  );
}
