'use client';

import { useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import {
  AlertTriangle,
  CheckCircle2,
  ClipboardCheck,
  RefreshCw,
  ShieldAlert,
} from 'lucide-react';
import { toast } from 'sonner';

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { useAuth } from '@/hooks/use-auth';
import { procurementSupplierEligibilityService as service } from '@/services/procurement-supplier-eligibility.service';
import type {
  SupplierEligibilityBoundary,
  SupplierEligibilityResult,
} from '@/types/procurement-supplier-eligibility';

const fallbackBoundaries: Array<{
  value: SupplierEligibilityBoundary;
  label: string;
}> = [
  { value: 'StatusReview', label: 'Status review' },
  { value: 'Invitation', label: 'Invitation' },
  { value: 'Award', label: 'Award' },
  { value: 'Contract', label: 'Contract' },
  { value: 'ManualPurchaseOrder', label: 'Manual purchase order' },
  { value: 'FrameworkCallOff', label: 'Framework call-off' },
];

const shortHash = (value?: string) =>
  value ? `${value.slice(0, 14)}…${value.slice(-8)}` : '—';

export default function SupplierEligibilityPage() {
  const { hasPermission } = useAuth();
  const canRead =
    hasPermission('procurement.supplier.review') ||
    hasPermission('procurement.supplier.manage');
  const [businessPartnerId, setBusinessPartnerId] = useState('');
  const [boundary, setBoundary] =
    useState<SupplierEligibilityBoundary>('StatusReview');
  const [categoryIds, setCategoryIds] = useState('');
  const [requiresPrequalification, setRequiresPrequalification] =
    useState(false);
  const [requiresLicenses, setRequiresLicenses] = useState(false);
  const [minimumRating, setMinimumRating] = useState('');
  const [result, setResult] = useState<SupplierEligibilityResult>();
  const [busy, setBusy] = useState(false);

  const boundaries = useQuery({
    queryKey: ['supplier-eligibility-boundaries'],
    queryFn: service.boundaries,
  });
  const boundaryOptions = boundaries.data ?? fallbackBoundaries;
  const parsedCategories = useMemo(
    () =>
      categoryIds
        .split(',')
        .map((value) => value.trim())
        .filter(Boolean),
    [categoryIds]
  );

  const evaluate = async () => {
    if (!businessPartnerId.trim()) {
      toast.error('Enter a supplier business-partner ID.');
      return;
    }
    try {
      setBusy(true);
      setResult(
        await service.evaluate({
          businessPartnerId: businessPartnerId.trim(),
          boundary,
          categoryIds: parsedCategories,
          requiresPrequalification,
          requiresLicenses,
          includeFinancialWarnings: true,
          minimumPerformanceRating: minimumRating
            ? Number(minimumRating)
            : undefined,
        })
      );
    } catch {
      setResult(undefined);
      toast.error('Supplier eligibility could not be evaluated.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="space-y-6 p-6" data-testid="supplier-eligibility-page">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">
          Supplier eligibility
        </h1>
        <p className="text-sm text-muted-foreground">
          One read-only compliance decision shared by invitation, award,
          contract, manual PO, and framework call-off controls.
        </p>
      </div>

      <Alert>
        <ClipboardCheck className="h-4 w-4" />
        <AlertTitle>Authoritative current-state check</AlertTitle>
        <AlertDescription>
          The server derives tenant, supplier status, blacklist, categories,
          evidence-pack, qualified-list, exact DEC-011 policy, current
          due-diligence/reassessment, and policy lineage. This page cannot
          approve a supplier or change a transaction.
        </AlertDescription>
      </Alert>

      {!canRead && (
        <Alert variant="destructive">
          <ShieldAlert className="h-4 w-4" />
          <AlertTitle>Permission required</AlertTitle>
          <AlertDescription>
            Supplier review or supplier management permission is required.
          </AlertDescription>
        </Alert>
      )}

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Evaluation inputs</CardTitle>
        </CardHeader>
        <CardContent className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
          <div className="space-y-2">
            <Label htmlFor="supplier-id">Business-partner ID</Label>
            <Input
              id="supplier-id"
              value={businessPartnerId}
              onChange={(event) => setBusinessPartnerId(event.target.value)}
              placeholder="00000000-0000-0000-0000-000000000000"
            />
          </div>
          <div className="space-y-2">
            <Label>Enforcement boundary</Label>
            <Select
              value={boundary}
              onValueChange={(value) =>
                setBoundary(value as SupplierEligibilityBoundary)
              }
            >
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {boundaryOptions.map((option) => (
                  <SelectItem key={option.value} value={option.value}>
                    {option.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-2">
            <Label htmlFor="category-ids">Required category IDs</Label>
            <Input
              id="category-ids"
              value={categoryIds}
              onChange={(event) => setCategoryIds(event.target.value)}
              placeholder="Comma-separated; optional"
            />
          </div>
          <div className="space-y-2">
            <Label htmlFor="minimum-rating">Minimum performance rating</Label>
            <Input
              id="minimum-rating"
              type="number"
              min="0"
              max="5"
              step="0.01"
              value={minimumRating}
              onChange={(event) => setMinimumRating(event.target.value)}
              placeholder="Optional"
            />
          </div>
          <div className="flex items-end gap-5 pb-2">
            <label className="flex items-center gap-2 text-sm">
              <Checkbox
                checked={requiresPrequalification}
                onCheckedChange={(checked) =>
                  setRequiresPrequalification(checked === true)
                }
              />
              Require qualified list
            </label>
            <label className="flex items-center gap-2 text-sm">
              <Checkbox
                checked={requiresLicenses}
                onCheckedChange={(checked) =>
                  setRequiresLicenses(checked === true)
                }
              />
              Require licences
            </label>
          </div>
          <div className="flex items-end">
            <Button
              className="w-full"
              disabled={!canRead || busy}
              onClick={evaluate}
            >
              <RefreshCw className={`mr-2 h-4 w-4 ${busy ? 'animate-spin' : ''}`} />
              Evaluate current eligibility
            </Button>
          </div>
        </CardContent>
      </Card>

      {!result ? (
        <Card>
          <CardContent className="flex min-h-40 flex-col items-center justify-center gap-2 text-center text-muted-foreground">
            <ClipboardCheck className="h-8 w-8" />
            <p>No eligibility decision has been evaluated.</p>
            <p className="text-xs">
              Enter a supplier and choose the transaction boundary.
            </p>
          </CardContent>
        </Card>
      ) : (
        <div className="space-y-4" data-testid="supplier-eligibility-result">
          <Alert variant={result.isValid ? 'default' : 'destructive'}>
            {result.isValid ? (
              <CheckCircle2 className="h-4 w-4" />
            ) : (
              <AlertTriangle className="h-4 w-4" />
            )}
            <AlertTitle>
              {result.isValid ? 'Eligible' : 'Not eligible'} ·{' '}
              {result.validationCode}
            </AlertTitle>
            <AlertDescription>
              {result.partnerCode} · {result.partnerName} ·{' '}
              {new Date(result.evaluatedAtUtc).toLocaleString()}
            </AlertDescription>
          </Alert>

          <div className="grid gap-4 lg:grid-cols-2 xl:grid-cols-3">
            <Card>
              <CardHeader>
                <CardTitle className="text-base">Supplier status</CardTitle>
              </CardHeader>
              <CardContent className="space-y-2 text-sm">
                <p>Approval: {result.approvalStatus ?? 'Pending'}</p>
                <p>Registration: {result.registrationStatus}</p>
                <p>Active: {result.isActive ? 'Yes' : 'No'}</p>
                <p>Blacklisted: {result.isBlacklisted ? 'Yes' : 'No'}</p>
                <p>
                  Categories:{' '}
                  {result.categories.length
                    ? result.categories
                        .map(
                          (item) =>
                            item.categoryCode ??
                            item.categoryName ??
                            item.categoryId
                        )
                        .join(', ')
                    : 'None'}
                </p>
              </CardContent>
            </Card>
            <Card>
              <CardHeader>
                <CardTitle className="text-base">Risk &amp; concentration</CardTitle>
              </CardHeader>
              <CardContent className="space-y-2 text-sm">
                <p>
                  Assessment:{' '}
                  {result.riskAssessmentReference ??
                    (result.riskPolicyAvailable ? 'Required' : 'DEC-011 release gate')}
                </p>
                <p>
                  Score / band:{' '}
                  {result.riskScore !== undefined
                    ? `${result.riskScore.toFixed(2)} / ${result.riskBand ?? 'Unresolved'}`
                    : 'Incomplete'}
                </p>
                <p>
                  Spend share:{' '}
                  {result.maximumSpendSharePercent !== undefined
                    ? `${result.maximumSpendSharePercent.toFixed(2)}% / ${result.concentrationLimitPercent?.toFixed(2) ?? '—'}% limit`
                    : '—'}
                </p>
                <p>Single-source categories: {result.singleSourceCategoryCount}</p>
                <p>
                  Alerts: {result.openRiskAlertCount} open ·{' '}
                  {result.escalatedRiskAlertCount} escalated
                </p>
                <p>
                  Award: {result.riskAwardBlocked ? 'Blocked' : 'No risk stop'}
                </p>
                <p className="font-mono" title={result.riskAssessmentIntegrityHash}>
                  Integrity: {shortHash(result.riskAssessmentIntegrityHash)}
                </p>
              </CardContent>
            </Card>
            <Card>
              <CardHeader>
                <CardTitle className="text-base">Supplier performance</CardTitle>
              </CardHeader>
              <CardContent className="space-y-2 text-sm">
                <p>
                  Scorecard:{' '}
                  {result.performanceScorecardReference ??
                    (result.performanceScorecardPolicyAvailable
                      ? 'Required'
                      : 'DEC-011 release gate')}
                </p>
                <p>
                  Score / band:{' '}
                  {result.performanceOverallScore !== undefined
                    ? `${result.performanceOverallScore.toFixed(2)} / ${result.performanceBand ?? 'Unresolved'}`
                    : 'Incomplete'}
                </p>
                <p>
                  Coverage:{' '}
                  {result.performanceDataCoveragePercent !== undefined
                    ? `${result.performanceDataCoveragePercent.toFixed(2)}%`
                    : '—'}
                </p>
                <p>
                  Minimum:{' '}
                  {result.performanceMinimumScore !== undefined
                    ? result.performanceMinimumScore.toFixed(2)
                    : '—'}
                </p>
                <p>
                  Current: {result.performanceScorecardCurrent ? 'Yes' : 'No'}
                </p>
                <p>
                  Award:{' '}
                  {result.performanceAwardBlocked
                    ? 'Blocked'
                    : 'No performance stop'}
                </p>
                <p
                  className="font-mono"
                  title={result.performanceScorecardIntegrityHash}
                >
                  Integrity: {shortHash(result.performanceScorecardIntegrityHash)}
                </p>
              </CardContent>
            </Card>
            <Card>
              <CardHeader>
                <CardTitle className="text-base">Due diligence</CardTitle>
              </CardHeader>
              <CardContent className="space-y-2 text-sm">
                <p>
                  Review:{' '}
                  {result.dueDiligenceReviewReference ??
                    (result.dueDiligencePolicyAvailable
                      ? 'Required'
                      : 'DEC-011 release gate')}
                </p>
                <p>
                  State:{' '}
                  {result.dueDiligenceCurrent
                    ? 'Current and clear'
                    : result.dueDiligenceOutcome ?? 'Not available'}
                </p>
                <p>
                  Cycle:{' '}
                  {result.dueDiligenceCycleNumber
                    ? `${result.dueDiligenceReviewType} #${result.dueDiligenceCycleNumber}`
                    : '—'}
                </p>
                <p>
                  Review due:{' '}
                  {result.dueDiligenceReviewPeriodEndUtc
                    ? new Date(
                        result.dueDiligenceReviewPeriodEndUtc
                      ).toLocaleDateString()
                    : '—'}
                </p>
                <p>
                  Checks: {result.dueDiligenceChecks?.length ?? 0} / 6
                </p>
                <p
                  className="font-mono"
                  title={result.dueDiligenceIntegrityHash}
                >
                  Integrity: {shortHash(result.dueDiligenceIntegrityHash)}
                </p>
              </CardContent>
            </Card>
            <Card>
              <CardHeader>
                <CardTitle className="text-base">Evidence and AVL</CardTitle>
              </CardHeader>
              <CardContent className="space-y-2 text-sm">
                <p>
                  Evidence pack:{' '}
                  {result.evidencePackCode
                    ? `${result.evidencePackCode} v${result.evidencePackVersion}`
                    : 'Legacy / not bound'}
                </p>
                <p>
                  Evidence ready:{' '}
                  {result.evidenceReady === undefined
                    ? 'Not applicable'
                    : result.evidenceReady
                      ? 'Yes'
                      : 'No'}
                </p>
                <p className="font-mono" title={result.evidencePackSnapshotHash}>
                  Evidence hash: {shortHash(result.evidencePackSnapshotHash)}
                </p>
                <p>
                  DEC-011 AVL policy:{' '}
                  {result.avlPolicyAvailable
                    ? `${result.avlProfileCode} v${result.avlProfileVersion}`
                    : 'Release configuration pending'}
                </p>
                <p className="font-mono" title={result.avlPolicyValueHash}>
                  AVL value hash: {shortHash(result.avlPolicyValueHash)}
                </p>
                <p>
                  Published AVL:{' '}
                  {result.avlRegisterAvailable
                    ? `${result.avlRegisterCode} v${result.avlRegisterVersion}`
                    : result.avlPolicyAvailable
                      ? 'Current publication required'
                      : 'Awaiting DEC-011'}
                </p>
                <p>
                  AVL membership:{' '}
                  {result.formalAvlCurrent
                    ? 'Active'
                    : result.avlEntryStatus ?? 'Not listed'}
                </p>
                <p className="font-mono" title={result.avlEntryIntegrityHash}>
                  Entry integrity: {shortHash(result.avlEntryIntegrityHash)}
                </p>
                <p>Qualified-list records: {result.qualifiedListEntries.length}</p>
              </CardContent>
            </Card>
            <Card>
              <CardHeader>
                <CardTitle className="text-base">Immutable decision</CardTitle>
              </CardHeader>
              <CardContent className="space-y-2 text-sm">
                <p>Boundary: {result.boundary}</p>
                <p className="font-mono" title={result.decisionHash}>
                  Hash: {shortHash(result.decisionHash)}
                </p>
                <div className="flex flex-wrap gap-1">
                  {result.decisionKeys.map((key) => (
                    <Badge key={key} variant="outline">
                      {key}
                    </Badge>
                  ))}
                </div>
              </CardContent>
            </Card>
          </div>

          <Card>
            <CardHeader>
              <CardTitle className="text-base">Findings</CardTitle>
            </CardHeader>
            <CardContent className="space-y-2">
              {result.findings.length === 0 ? (
                <p className="text-sm text-muted-foreground">
                  No blocking findings or warnings.
                </p>
              ) : (
                result.findings.map((finding) => (
                  <div
                    key={`${finding.code}-${finding.message}`}
                    className="flex items-start justify-between gap-3 rounded-md border p-3"
                  >
                    <div>
                      <p className="text-sm font-medium">{finding.code}</p>
                      <p className="text-sm text-muted-foreground">
                        {finding.message}
                      </p>
                    </div>
                    <Badge variant={finding.blocking ? 'destructive' : 'secondary'}>
                      {finding.blocking ? 'Blocking' : 'Review'}
                    </Badge>
                  </div>
                ))
              )}
            </CardContent>
          </Card>
        </div>
      )}
    </div>
  );
}
