'use client';

import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  AlertTriangle,
  CheckCircle2,
  RefreshCw,
  Search,
  ShieldAlert,
  TrendingUp,
} from 'lucide-react';
import { toast } from 'sonner';

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Textarea } from '@/components/ui/textarea';
import { useAuth } from '@/hooks/use-auth';
import { procurementSupplierRiskService as service } from '@/services/procurement-supplier-risk.service';
import type { SupplierRiskAlert } from '@/types/procurement-supplier-risk';

const formatDate = (value?: string) =>
  value ? new Date(value).toLocaleString() : '—';
const shortHash = (value?: string) =>
  value ? `${value.slice(0, 12)}…${value.slice(-8)}` : '—';
const formatAmount = (value: number, currency: string) =>
  new Intl.NumberFormat(undefined, {
    style: currency === 'UNSPECIFIED' ? 'decimal' : 'currency',
    currency: currency === 'UNSPECIFIED' ? undefined : currency,
    maximumFractionDigits: 2,
  }).format(value);

export default function SupplierRiskPage() {
  const queryClient = useQueryClient();
  const { hasPermission } = useAuth();
  const canRead =
    hasPermission('procurement.supplier.review') ||
    hasPermission('procurement.supplier.manage') ||
    hasPermission('procurement.supplier.approve');
  const canAssess =
    hasPermission('procurement.supplier.review') ||
    hasPermission('procurement.supplier.manage');
  const canApprove = hasPermission('procurement.supplier.approve');

  const [search, setSearch] = useState('');
  const [selectedId, setSelectedId] = useState<string>();
  const [assessmentOpen, setAssessmentOpen] = useState(false);
  const [supplierId, setSupplierId] = useState('');
  const [alertAction, setAlertAction] = useState<{
    action: 'escalate' | 'resolve';
    alert: SupplierRiskAlert;
  }>();
  const [workflowDefinitionId, setWorkflowDefinitionId] = useState('');
  const [reason, setReason] = useState('');
  const [evidenceReference, setEvidenceReference] = useState('');

  const summary = useQuery({
    queryKey: ['supplier-risk-summary'],
    queryFn: service.summary,
    enabled: canRead,
  });
  const history = useQuery({
    queryKey: ['supplier-risk-history', search],
    queryFn: () =>
      service.search({ search: search || undefined, page: 1, pageSize: 100 }),
    enabled: canRead,
  });
  const detail = useQuery({
    queryKey: ['supplier-risk-detail', selectedId],
    queryFn: () => service.get(selectedId ?? ''),
    enabled: canRead && Boolean(selectedId),
  });
  const suppliers = useQuery({
    queryKey: ['supplier-risk-suppliers'],
    queryFn: service.supplierOptions,
    enabled: canRead,
  });
  const workflows = useQuery({
    queryKey: ['supplier-risk-workflows'],
    queryFn: service.workflowOptions,
    enabled: canRead,
  });

  const invalidate = async () => {
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: ['supplier-risk-summary'] }),
      queryClient.invalidateQueries({ queryKey: ['supplier-risk-history'] }),
      queryClient.invalidateQueries({ queryKey: ['supplier-risk-detail'] }),
    ]);
  };

  const assessMutation = useMutation({
    mutationFn: () =>
      service.evaluate({
        businessPartnerId: supplierId,
        idempotencyKey: crypto.randomUUID(),
        sourceType: 'SupplierRiskAdministration',
        sourceReference: 'Manual current-state assessment',
      }),
    onSuccess: async (result) => {
      setSelectedId(result.id);
      setAssessmentOpen(false);
      setSupplierId('');
      await invalidate();
      toast.success('Current supplier risk and concentration assessed');
    },
    onError: (error: Error) => toast.error(error.message),
  });

  const evidence = useMemo(
    () =>
      evidenceReference.trim()
        ? [
            {
              referenceKind: 'ExternalReference' as const,
              reference: evidenceReference.trim(),
              label: 'Supplier risk escalation evidence',
              requirementKey: 'SUPPLIER-RISK',
            },
          ]
        : [],
    [evidenceReference]
  );

  const alertMutation = useMutation({
    mutationFn: async () => {
      if (!alertAction) throw new Error('Select an alert action.');
      if (alertAction.action === 'escalate') {
        return service.escalate(alertAction.alert.id, {
          workflowDefinitionId,
          reason,
          rowVersion: alertAction.alert.rowVersion,
          evidence,
        });
      }
      return service.resolve(alertAction.alert.id, {
        reason,
        rowVersion: alertAction.alert.rowVersion,
        evidence,
      });
    },
    onSuccess: async () => {
      setAlertAction(undefined);
      setWorkflowDefinitionId('');
      setReason('');
      setEvidenceReference('');
      await invalidate();
      toast.success('Supplier risk alert lifecycle updated');
    },
    onError: (error: Error) => toast.error(error.message),
  });

  const refresh = async () => {
    await invalidate();
    toast.success('Supplier risk register refreshed');
  };

  const cards = [
    ['Suppliers', summary.data?.supplierCount ?? 0],
    ['Current assessments', summary.data?.currentAssessmentCount ?? 0],
    ['Overdue', summary.data?.overdueAssessmentCount ?? 0],
    ['Open alerts', summary.data?.openAlertCount ?? 0],
    ['Escalated', summary.data?.escalatedAlertCount ?? 0],
    ['Award blocked', summary.data?.awardBlockedSupplierCount ?? 0],
  ];

  if (!canRead) {
    return (
      <Alert variant="destructive">
        <ShieldAlert className="h-4 w-4" />
        <AlertTitle>Supplier risk access required</AlertTitle>
        <AlertDescription>
          Supplier review, management, or approval permission is required.
        </AlertDescription>
      </Alert>
    );
  }

  const selected = detail.data;

  return (
    <div className="space-y-4" data-testid="supplier-risk-page">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">
            Supplier Risk &amp; Concentration
          </h1>
          <p className="text-sm text-muted-foreground">
            Policy-bound risk bands, currency-safe spend exposure, single-source
            dependency, escalation, and award controls.
          </p>
        </div>
        <div className="flex gap-2">
          <Button variant="outline" onClick={refresh}>
            <RefreshCw className="mr-2 h-4 w-4" />
            Refresh
          </Button>
          {canAssess && (
            <Button
              onClick={() => setAssessmentOpen(true)}
              disabled={!summary.data?.policyAvailable}
            >
              <TrendingUp className="mr-2 h-4 w-4" />
              Assess supplier
            </Button>
          )}
        </div>
      </div>

      <Alert>
        <ShieldAlert className="h-4 w-4" />
        <AlertTitle>Authoritative shared control</AlertTitle>
        <AlertDescription>
          The server resolves one evidenced effective DEC-011, keeps unlike
          currencies separate, derives spend and category dependency from
          controlled purchase orders, composes AVL/due diligence/eligibility,
          and applies the approved alert, escalation, or award-stop action.
        </AlertDescription>
      </Alert>

      {summary.data && !summary.data.policyAvailable && (
        <Alert variant="destructive" data-testid="supplier-risk-policy-gate">
          <AlertTriangle className="h-4 w-4" />
          <AlertTitle>DEC-011 release configuration required</AlertTitle>
          <AlertDescription>{summary.data.policyReleaseGate}</AlertDescription>
        </Alert>
      )}

      <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-6">
        {cards.map(([label, value]) => (
          <Card key={label}>
            <CardContent className="p-4">
              <p className="text-xs text-muted-foreground">{label}</p>
              <p className="mt-1 text-2xl font-semibold">{value}</p>
            </CardContent>
          </Card>
        ))}
      </div>

      {summary.data?.policyAvailable && (
        <Card>
          <CardContent className="grid gap-3 p-4 text-sm md:grid-cols-5">
            <p>
              Profile:{' '}
              <strong>
                {summary.data.policyProfileCode} v
                {summary.data.policyProfileVersion}
              </strong>
            </p>
            <p>
              Window: <strong>{summary.data.exposureWindowMonths} months</strong>
            </p>
            <p>
              Concentration:{' '}
              <strong>{summary.data.concentrationLimitPercent}%</strong>
            </p>
            <p>
              Minimum score: <strong>{summary.data.minimumScore}</strong>
            </p>
            <p>
              Action: <strong>{summary.data.eligibilityAction}</strong>
            </p>
          </CardContent>
        </Card>
      )}

      <Card>
        <CardHeader className="pb-3">
          <div className="flex flex-wrap items-center justify-between gap-3">
            <CardTitle className="text-base">Current assessment register</CardTitle>
            <div className="relative w-full sm:w-80">
              <Search className="absolute left-3 top-2.5 h-4 w-4 text-muted-foreground" />
              <Input
                value={search}
                onChange={(event) => setSearch(event.target.value)}
                placeholder="Search supplier or assessment"
                className="pl-9"
              />
            </div>
          </div>
        </CardHeader>
        <CardContent>
          {history.isLoading && (
            <p className="py-8 text-center text-sm text-muted-foreground">
              Loading current assessments…
            </p>
          )}
          {history.isError && (
            <Alert variant="destructive">
              <AlertTriangle className="h-4 w-4" />
              <AlertDescription>
                {(history.error as Error).message}
              </AlertDescription>
            </Alert>
          )}
          {!history.isLoading && !history.isError && !history.data?.items.length && (
            <div className="py-10 text-center text-sm text-muted-foreground">
              No supplier-risk assessment has been recorded.
            </div>
          )}
          {!!history.data?.items.length && (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Supplier</TableHead>
                  <TableHead>Assessment</TableHead>
                  <TableHead>Score / band</TableHead>
                  <TableHead>Max spend share</TableHead>
                  <TableHead>Single source</TableHead>
                  <TableHead>Alerts</TableHead>
                  <TableHead>Award</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {history.data.items.map((item) => (
                  <TableRow
                    key={item.id}
                    className="cursor-pointer"
                    data-state={selectedId === item.id ? 'selected' : undefined}
                    onClick={() => setSelectedId(item.id)}
                  >
                    <TableCell>
                      <p className="font-medium">{item.partnerName}</p>
                      <p className="text-xs text-muted-foreground">
                        {item.partnerCode}
                      </p>
                    </TableCell>
                    <TableCell>
                      <p>{item.assessmentReference}</p>
                      <p className="text-xs text-muted-foreground">
                        {formatDate(item.assessedAtUtc)}
                      </p>
                    </TableCell>
                    <TableCell>
                      {item.riskScore?.toFixed(2) ?? 'Incomplete'} /{' '}
                      {item.riskBand ?? '—'}
                    </TableCell>
                    <TableCell>{item.maximumSpendSharePercent.toFixed(2)}%</TableCell>
                    <TableCell>{item.singleSourceCategoryCount}</TableCell>
                    <TableCell>
                      {item.openAlertCount} open · {item.escalatedAlertCount}{' '}
                      escalated
                    </TableCell>
                    <TableCell>
                      <Badge variant={item.awardBlocked ? 'destructive' : 'outline'}>
                        {item.awardBlocked ? 'Blocked' : 'Allowed'}
                      </Badge>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      {detail.isLoading && (
        <Card>
          <CardContent className="py-8 text-center text-sm text-muted-foreground">
            Loading assessment evidence…
          </CardContent>
        </Card>
      )}

      {selected && (
        <div className="space-y-4" data-testid="supplier-risk-detail">
          <Card>
            <CardHeader>
              <div className="flex flex-wrap items-start justify-between gap-3">
                <div>
                  <CardTitle className="text-base">
                    {selected.assessmentReference} · {selected.partnerName}
                  </CardTitle>
                  <p className="mt-1 text-xs text-muted-foreground">
                    DEC-011 {shortHash(selected.policyValueHash)} · eligibility{' '}
                    {shortHash(selected.eligibilityDecisionHash)} · integrity{' '}
                    {shortHash(selected.integrityHash)}
                  </p>
                </div>
                <Badge variant={selected.awardBlocked ? 'destructive' : 'outline'}>
                  {selected.awardBlocked ? 'Award blocked' : 'Award allowed'}
                </Badge>
              </div>
            </CardHeader>
            <CardContent className="grid gap-3 text-sm md:grid-cols-4">
              <p>
                Score: <strong>{selected.riskScore?.toFixed(2) ?? 'Incomplete'}</strong>
              </p>
              <p>
                Band: <strong>{selected.riskBand ?? 'Unresolved'}</strong>
              </p>
              <p>
                Period:{' '}
                <strong>
                  {formatDate(selected.periodStartUtc)} –{' '}
                  {formatDate(selected.periodEndUtc)}
                </strong>
              </p>
              <p>
                Review due: <strong>{formatDate(selected.nextReviewDueAtUtc)}</strong>
              </p>
            </CardContent>
          </Card>

          <div className="grid gap-4 xl:grid-cols-2">
            <Card>
              <CardHeader>
                <CardTitle className="text-base">Risk dimensions</CardTitle>
              </CardHeader>
              <CardContent>
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Dimension</TableHead>
                      <TableHead>Weight</TableHead>
                      <TableHead>Score</TableHead>
                      <TableHead>Source</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {selected.dimensions.map((dimension) => (
                      <TableRow key={dimension.dimension}>
                        <TableCell>{dimension.dimension}</TableCell>
                        <TableCell>{dimension.weightPercent}%</TableCell>
                        <TableCell>
                          {dimension.score?.toFixed(2) ?? 'Missing'}
                        </TableCell>
                        <TableCell className="max-w-56 truncate" title={dimension.source}>
                          {dimension.source}
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </CardContent>
            </Card>

            <Card>
              <CardHeader>
                <CardTitle className="text-base">Spend exposure by currency</CardTitle>
              </CardHeader>
              <CardContent>
                {!selected.spendExposure.length ? (
                  <p className="text-sm text-muted-foreground">
                    No controlled purchase-order spend falls in the policy window.
                  </p>
                ) : (
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead>Currency</TableHead>
                        <TableHead>Supplier</TableHead>
                        <TableHead>Tenant</TableHead>
                        <TableHead>Share</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {selected.spendExposure.map((exposure) => (
                        <TableRow key={exposure.currencyCode}>
                          <TableCell>{exposure.currencyCode}</TableCell>
                          <TableCell>
                            {formatAmount(
                              exposure.supplierAmount,
                              exposure.currencyCode
                            )}
                          </TableCell>
                          <TableCell>
                            {formatAmount(exposure.tenantAmount, exposure.currencyCode)}
                          </TableCell>
                          <TableCell>{exposure.spendSharePercent.toFixed(2)}%</TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                )}
              </CardContent>
            </Card>
          </div>

          <Card>
            <CardHeader>
              <CardTitle className="text-base">
                Category concentration and single-source dependency
              </CardTitle>
            </CardHeader>
            <CardContent>
              {!selected.categoryExposure.length ? (
                <p className="text-sm text-muted-foreground">
                  No supplier category exposure falls in the policy window.
                </p>
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Category</TableHead>
                      <TableHead>Currency</TableHead>
                      <TableHead>Supplier share</TableHead>
                      <TableHead>Suppliers</TableHead>
                      <TableHead>Dependency</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {selected.categoryExposure.map((exposure) => (
                      <TableRow
                        key={`${exposure.categoryId ?? 'none'}-${exposure.currencyCode}`}
                      >
                        <TableCell>
                          {exposure.categoryCode} · {exposure.categoryName}
                        </TableCell>
                        <TableCell>{exposure.currencyCode}</TableCell>
                        <TableCell>{exposure.spendSharePercent.toFixed(2)}%</TableCell>
                        <TableCell>{exposure.distinctSupplierCount}</TableCell>
                        <TableCell>
                          {exposure.isSingleSource ? (
                            <Badge variant="destructive">Single source</Badge>
                          ) : (
                            <Badge variant="outline">Diversified</Badge>
                          )}
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle className="text-base">Alerts &amp; escalation</CardTitle>
            </CardHeader>
            <CardContent className="space-y-3">
              {!selected.alerts.length && (
                <div className="flex items-center gap-2 text-sm text-muted-foreground">
                  <CheckCircle2 className="h-4 w-4 text-emerald-600" />
                  No risk alert was generated.
                </div>
              )}
              {selected.alerts.map((alert) => (
                <div
                  key={alert.id}
                  className="flex flex-wrap items-start justify-between gap-3 rounded-md border p-3"
                >
                  <div className="min-w-0 flex-1">
                    <div className="flex flex-wrap items-center gap-2">
                      <Badge variant={alert.status === 'Resolved' ? 'outline' : 'destructive'}>
                        {alert.status}
                      </Badge>
                      <span className="font-medium">{alert.alertType}</span>
                      <span className="text-xs text-muted-foreground">
                        {alert.ruleCode}
                      </span>
                    </div>
                    <p className="mt-2 text-sm">{alert.message}</p>
                    {alert.workflowInstanceId && (
                      <p className="mt-1 font-mono text-xs text-muted-foreground">
                        Workflow {alert.workflowInstanceId}
                      </p>
                    )}
                  </div>
                  <div className="flex gap-2">
                    {canAssess && alert.status === 'Open' && (
                      <Button
                        size="sm"
                        variant="outline"
                        onClick={() => setAlertAction({ action: 'escalate', alert })}
                      >
                        Escalate
                      </Button>
                    )}
                    {canApprove && alert.status === 'Escalated' && (
                      <Button
                        size="sm"
                        onClick={() => setAlertAction({ action: 'resolve', alert })}
                      >
                        Resolve
                      </Button>
                    )}
                  </div>
                </div>
              ))}
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle className="text-base">DEC-001 through DEC-014 lineage</CardTitle>
            </CardHeader>
            <CardContent className="flex flex-wrap gap-2">
              {selected.decisionKeys.map((key) => (
                <Badge key={key} variant="outline">
                  {key}
                </Badge>
              ))}
            </CardContent>
          </Card>
        </div>
      )}

      <Dialog open={assessmentOpen} onOpenChange={setAssessmentOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Assess current supplier risk</DialogTitle>
          </DialogHeader>
          <Alert>
            <TrendingUp className="h-4 w-4" />
            <AlertDescription>
              The server selects the exact policy window and weights. This action
              cannot enter or override a risk score.
            </AlertDescription>
          </Alert>
          <div>
            <Label>Supplier</Label>
            <Select value={supplierId} onValueChange={setSupplierId}>
              <SelectTrigger>
                <SelectValue placeholder="Select supplier" />
              </SelectTrigger>
              <SelectContent>
                {suppliers.data?.map((supplier) => (
                  <SelectItem key={supplier.id} value={supplier.id}>
                    {supplier.code} · {supplier.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setAssessmentOpen(false)}>
              Cancel
            </Button>
            <Button
              disabled={!supplierId || assessMutation.isPending}
              onClick={() => assessMutation.mutate()}
            >
              Assess
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog
        open={Boolean(alertAction)}
        onOpenChange={(open) => !open && setAlertAction(undefined)}
      >
        <DialogContent>
          <DialogHeader>
            <DialogTitle>
              {alertAction?.action === 'escalate'
                ? 'Escalate supplier risk alert'
                : 'Resolve approved escalation'}
            </DialogTitle>
          </DialogHeader>
          {alertAction?.action === 'escalate' && (
            <div>
              <Label>Published shared workflow</Label>
              <Select
                value={workflowDefinitionId}
                onValueChange={setWorkflowDefinitionId}
              >
                <SelectTrigger>
                  <SelectValue placeholder="Select workflow" />
                </SelectTrigger>
                <SelectContent>
                  {workflows.data?.map((workflow) => (
                    <SelectItem key={workflow.id} value={workflow.id}>
                      {workflow.name} · v{workflow.version}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          )}
          <div>
            <Label>Reason</Label>
            <Textarea
              value={reason}
              onChange={(event) => setReason(event.target.value)}
              placeholder="Explain the escalation or approved resolution"
            />
          </div>
          <div>
            <Label>Evidence reference</Label>
            <Input
              value={evidenceReference}
              onChange={(event) => setEvidenceReference(event.target.value)}
              placeholder="Minute, report, workflow, or approval reference"
            />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setAlertAction(undefined)}>
              Cancel
            </Button>
            <Button
              disabled={
                !reason.trim() ||
                !evidenceReference.trim() ||
                (alertAction?.action === 'escalate' && !workflowDefinitionId) ||
                alertMutation.isPending
              }
              onClick={() => alertMutation.mutate()}
            >
              Confirm
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
