'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  AlertTriangle,
  CheckCircle2,
  Clock3,
  FileCheck2,
  HardHat,
  Loader2,
  RefreshCw,
  ShieldAlert,
  XCircle,
} from 'lucide-react';
import { toast } from 'sonner';
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
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import type { ContractDocumentDto } from '@/services/contractService';
import {
  procurementWorksCloseoutService,
  type WorksCloseoutAction,
  type WorksCloseoutActionType,
  type WorksCloseoutCheck,
  type WorksCloseoutOverview,
  type RetentionReleaseStage,
} from '@/services/procurement-works-closeout.service';

interface WorksCloseoutWorkspaceProps {
  contractId: string;
  documents: ContractDocumentDto[];
  onContractChanged: () => void | Promise<void>;
}

const actionTypes: WorksCloseoutActionType[] = [
  'InitialTakeover',
  'DefectRectification',
  'FinalTakeover',
  'WarrantyRelease',
  'PerformanceSecurityRelease',
  'RetentionRelease',
  'DisputeOpen',
  'DisputeResolve',
  'Termination',
  'FinalAccount',
  'Closeout',
];
const actionTypeNames = actionTypes;
const actionStatusNames = [
  'PendingApproval',
  'Approved',
  'Rejected',
  'RevalidationFailed',
];
const checkStatusNames = ['Passed', 'Failed', 'NotRequired', 'Pending'];
const retentionReleaseStages: Exclude<RetentionReleaseStage, number>[] = [
  'PracticalCompletion',
  'SectionalTakeover',
  'DefectsLiability',
  'FinalRelease',
];

function displayName(value: string): string {
  return value.replace(/([a-z])([A-Z])/g, '$1 $2');
}

function actionType(
  value: WorksCloseoutAction['actionType']
): WorksCloseoutActionType {
  return typeof value === 'number'
    ? (actionTypeNames[value] ?? 'InitialTakeover')
    : value;
}

function actionStatus(value: WorksCloseoutAction['status']): string {
  return typeof value === 'number'
    ? (actionStatusNames[value] ?? String(value))
    : value;
}

function checkStatus(value: WorksCloseoutCheck['status']): string {
  return typeof value === 'number'
    ? (checkStatusNames[value] ?? String(value))
    : value;
}

function retentionStage(value?: RetentionReleaseStage): string | undefined {
  if (value == null) return undefined;
  return typeof value === 'number' ? retentionReleaseStages[value] : value;
}

function CheckIcon({ status }: { status: WorksCloseoutCheck['status'] }) {
  const current = checkStatus(status);
  if (current === 'Passed' || current === 'NotRequired') {
    return <CheckCircle2 className="h-4 w-4 text-emerald-600" />;
  }
  if (current === 'Failed') return <XCircle className="h-4 w-4 text-red-600" />;
  return <Clock3 className="h-4 w-4 text-amber-600" />;
}

function formatDate(value?: string) {
  return value ? new Date(value).toLocaleString() : '—';
}

export function WorksCloseoutWorkspace({
  contractId,
  documents,
  onContractChanged,
}: WorksCloseoutWorkspaceProps) {
  const [overview, setOverview] = useState<WorksCloseoutOverview | null>(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [selectedType, setSelectedType] =
    useState<WorksCloseoutActionType>('InitialTakeover');
  const [retentionReleaseStage, setRetentionReleaseStage] = useState<
    Exclude<RetentionReleaseStage, number>
  >('PracticalCompletion');
  const [usesRetentionBond, setUsesRetentionBond] = useState(false);
  const [sourceId, setSourceId] = useState('');
  const [certificateId, setCertificateId] = useState('');
  const [effectiveAt, setEffectiveAt] = useState('');
  const [amount, setAmount] = useState('');
  const [currency, setCurrency] = useState('');
  const [reason, setReason] = useState('');
  const [decisionComment, setDecisionComment] = useState('');
  const [selectedDocuments, setSelectedDocuments] = useState<
    Record<string, string>
  >({});

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const value =
        await procurementWorksCloseoutService.getOverview(contractId);
      setOverview(value);
      setCurrency((current) => current || value.project?.currency || '');
    } catch (loadError) {
      setError(
        loadError instanceof Error
          ? loadError.message
          : 'Works closeout controls could not be loaded.'
      );
    } finally {
      setLoading(false);
    }
  }, [contractId]);

  useEffect(() => {
    void load();
  }, [load]);

  useEffect(() => {
    setSourceId('');
    setCertificateId('');
    setSelectedDocuments({});
    setUsesRetentionBond(false);
    setAmount(
      selectedType === 'FinalAccount' &&
        overview?.project?.finalAccountValue != null
        ? String(overview.project.finalAccountValue)
        : ''
    );
  }, [selectedType, overview?.project?.finalAccountValue]);

  const dmsDocuments = useMemo(
    () =>
      documents.filter(
        (
          document
        ): document is ContractDocumentDto & {
          fileUploadRecordId: string;
          centralDocumentRecordId: string;
          centralDocumentVersionId: string;
        } =>
          Boolean(
            document.fileUploadRecordId &&
            document.centralDocumentRecordId &&
            document.centralDocumentVersionId
          )
      ),
    [documents]
  );
  const requirements = overview?.requiredEvidence?.[selectedType] ?? [];
  const allEvidenceSelected =
    requirements.length > 0 &&
    requirements.every((key) => selectedDocuments[key]);
  const openAction = overview?.history.find((item) =>
    ['PendingApproval', 'RevalidationFailed'].includes(
      actionStatus(item.status)
    )
  );
  const isMonetary = [
    'RetentionRelease',
    'FinalAccount',
    'Termination',
  ].includes(selectedType);
  const needsHandover = ['InitialTakeover', 'FinalTakeover'].includes(
    selectedType
  );
  const needsRetentionHandover =
    selectedType === 'RetentionRelease' &&
    ['PracticalCompletion', 'SectionalTakeover'].includes(
      retentionReleaseStage
    );
  const needsDefect = selectedType === 'DefectRectification';
  const needsFinalAccount = selectedType === 'FinalAccount';
  const needsSecurity = selectedType === 'PerformanceSecurityRelease';
  const needsRetentionFinalAccount =
    selectedType === 'RetentionRelease' &&
    retentionReleaseStage === 'FinalRelease';
  const retentionBondReady =
    overview?.performanceSecurity?.status?.toLowerCase() === 'approved';
  const contractAllowsAction =
    overview?.contractStatus === 'Active' ||
    (overview?.contractStatus === 'Suspended' &&
      ['DisputeResolve', 'Termination'].includes(selectedType));
  const retentionStagePercent =
    retentionReleaseStage === 'PracticalCompletion'
      ? (overview?.retentionPolicy?.practicalCompletionReleasePercent ?? 0)
      : retentionReleaseStage === 'SectionalTakeover'
        ? (overview?.retentionPolicy?.sectionalTakeoverReleasePercent ?? 0)
        : retentionReleaseStage === 'DefectsLiability'
          ? (overview?.retentionPolicy?.defectsReleasePercent ?? 0)
          : 100;
  const approvedForRetentionStage =
    overview?.history
      .filter((item) => actionType(item.actionType) === 'RetentionRelease')
      .filter((item) => actionStatus(item.status) === 'Approved')
      .filter(
        (item) =>
          retentionStage(item.retentionReleaseStage) === retentionReleaseStage
      )
      .reduce((total, item) => total + (item.amount ?? 0), 0) ?? 0;
  const retentionAvailable = Math.max(
    (overview?.project?.retentionHeld ?? 0) -
      (overview?.project?.retentionReleased ?? 0),
    0
  );
  const retentionStageLimit =
    retentionReleaseStage === 'FinalRelease'
      ? retentionAvailable
      : Math.min(
          retentionAvailable,
          Math.max(
            ((overview?.project?.retentionHeld ?? 0) * retentionStagePercent) /
              100 -
              approvedForRetentionStage,
            0
          )
        );
  const canSubmit = Boolean(
    overview?.isWorksContract &&
    overview.project &&
    contractAllowsAction &&
    !openAction &&
    reason.trim() &&
    allEvidenceSelected &&
    (!needsHandover || sourceId) &&
    (!needsRetentionHandover || sourceId) &&
    (!needsDefect || sourceId) &&
    (!needsFinalAccount || overview.project.finalAccountId) &&
    (!needsRetentionFinalAccount || overview.project.finalAccountId) &&
    (!needsSecurity || sourceId) &&
    (!usesRetentionBond || retentionBondReady) &&
    (!isMonetary || (amount && currency)) &&
    (selectedType !== 'RetentionRelease' ||
      (overview?.retentionPolicy &&
        Number(amount) > 0 &&
        Number(amount) <= retentionStageLimit + 0.01))
  );

  const refreshAfterMutation = async () => {
    await Promise.all([load(), Promise.resolve(onContractChanged())]);
  };

  const submit = async () => {
    if (!overview || !canSubmit) return;
    setBusy(true);
    try {
      await procurementWorksCloseoutService.submit(contractId, {
        actionType: selectedType,
        projectHandoverItemId: needsHandover ? sourceId : undefined,
        projectDefectLiabilityCaseId: needsDefect ? sourceId : undefined,
        projectFinalAccountId:
          needsFinalAccount || needsRetentionFinalAccount
            ? overview.project?.finalAccountId
            : undefined,
        projectPaymentCertificateId: certificateId || undefined,
        performanceBondRequestId: needsSecurity
          ? sourceId || undefined
          : selectedType === 'RetentionRelease' && usesRetentionBond
            ? overview.performanceSecurity?.id
            : undefined,
        retentionReleaseStage:
          selectedType === 'RetentionRelease'
            ? retentionReleaseStage
            : undefined,
        usesRetentionBond:
          selectedType === 'RetentionRelease' && usesRetentionBond,
        effectiveAtUtc: effectiveAt
          ? new Date(effectiveAt).toISOString()
          : undefined,
        amount: amount ? Number(amount) : undefined,
        currency: amount ? currency.toUpperCase() : undefined,
        reason: reason.trim(),
        idempotencyKey: crypto.randomUUID(),
        contractRowVersion: overview.contractRowVersion,
        evidence: requirements.map((requirementKey) => {
          const document = dmsDocuments.find(
            (item) =>
              item.fileUploadRecordId === selectedDocuments[requirementKey]
          );
          if (!document)
            throw new Error(
              `Select central-DMS evidence for ${requirementKey}.`
            );
          return {
            requirementKey,
            referenceKind: 'CentralDocumentUpload',
            fileUploadRecordId: document.fileUploadRecordId,
            evidenceReference:
              document.filePath || `dms:${document.centralDocumentRecordId}`,
          };
        }),
      });
      toast.success(
        `${displayName(selectedType)} submitted to the configured workflow.`
      );
      setReason('');
      setSourceId('');
      setCertificateId('');
      setEffectiveAt('');
      setAmount('');
      setUsesRetentionBond(false);
      setSelectedDocuments({});
      await refreshAfterMutation();
    } catch (submitError) {
      toast.error(
        submitError instanceof Error
          ? submitError.message
          : 'Works closeout submission failed.'
      );
    } finally {
      setBusy(false);
    }
  };

  const decide = async (approved: boolean) => {
    if (!openAction || !decisionComment.trim()) return;
    setBusy(true);
    try {
      await procurementWorksCloseoutService.decide(
        openAction.id,
        approved,
        decisionComment.trim(),
        openAction.rowVersion
      );
      toast.success(
        approved ? 'Approval step recorded.' : 'Works closeout action rejected.'
      );
      setDecisionComment('');
      await refreshAfterMutation();
    } catch (decisionError) {
      toast.error(
        decisionError instanceof Error
          ? decisionError.message
          : 'Works closeout decision failed.'
      );
      await load();
    } finally {
      setBusy(false);
    }
  };

  if (loading) {
    return (
      <Card>
        <CardContent className="flex items-center gap-3 py-10 text-sm text-muted-foreground">
          <Loader2 className="h-5 w-5 animate-spin" />
          Revalidating Works takeover, defects, releases, final account, and
          closeout…
        </CardContent>
      </Card>
    );
  }

  if (error || !overview) {
    return (
      <Card className="border-red-200">
        <CardContent className="space-y-4 py-8">
          <div className="flex items-start gap-3 text-red-700">
            <AlertTriangle className="mt-0.5 h-5 w-5" />
            <div>
              <p className="font-semibold">
                Works closeout controls unavailable
              </p>
              <p className="text-sm">
                {error || 'The tenant-safe closeout state could not be loaded.'}
              </p>
            </div>
          </div>
          <Button variant="outline" onClick={() => void load()}>
            <RefreshCw className="mr-2 h-4 w-4" />
            Retry
          </Button>
        </CardContent>
      </Card>
    );
  }

  if (!overview.isWorksContract) {
    return (
      <Card className="border-amber-200 bg-amber-50/40">
        <CardContent className="flex gap-3 py-8">
          <ShieldAlert className="mt-0.5 h-5 w-5 text-amber-700" />
          <div>
            <p className="font-semibold">Not a Works contract</p>
            <p className="text-sm text-muted-foreground">
              TDC-0409 takeover and defects-liability controls are intentionally
              unavailable for supply, service, and consultancy contracts.
            </p>
          </div>
        </CardContent>
      </Card>
    );
  }

  return (
    <div className="space-y-4" data-testid="works-closeout-workspace">
      <Card>
        <CardHeader className="flex flex-row items-start justify-between gap-4">
          <div>
            <CardTitle className="flex items-center gap-2">
              <HardHat className="h-5 w-5 text-orange-600" />
              Works takeover and closeout
            </CardTitle>
            <CardDescription>
              Controlled initial/final takeover, defects rectification,
              warranty/security/retention release, dispute or termination, final
              account, and closeout.
            </CardDescription>
          </div>
          <Button
            variant="ghost"
            size="sm"
            onClick={() => void load()}
            disabled={busy}
          >
            <RefreshCw className="mr-2 h-4 w-4" />
            Refresh
          </Button>
        </CardHeader>
        <CardContent className="space-y-4">
          {overview.project ? (
            <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
              <div className="rounded-lg border p-3">
                <p className="text-xs text-muted-foreground">Linked project</p>
                <p className="font-semibold">{overview.project.projectCode}</p>
                <p className="truncate text-xs text-muted-foreground">
                  {overview.project.projectTitle}
                </p>
                <p className="text-xs text-muted-foreground">
                  Project closure:{' '}
                  {overview.project.projectClosureStatus || 'Not submitted'}
                </p>
              </div>
              <div className="rounded-lg border p-3">
                <p className="text-xs text-muted-foreground">
                  Takeover evidence
                </p>
                <p className="font-semibold">
                  {overview.project.completedPracticalTakeovers} practical /{' '}
                  {overview.project.completedFinalTakeovers} final
                </p>
              </div>
              <div className="rounded-lg border p-3">
                <p className="text-xs text-muted-foreground">Defects</p>
                <p className="font-semibold">
                  {overview.project.openDefects} open /{' '}
                  {overview.project.closedDefects} closed
                </p>
              </div>
              <div className="rounded-lg border p-3">
                <p className="text-xs text-muted-foreground">Retention</p>
                <p className="font-semibold">
                  {overview.project.currency}{' '}
                  {overview.project.retentionHeld.toLocaleString()} held
                </p>
                <p className="text-xs text-muted-foreground">
                  {overview.project.retentionReleased.toLocaleString()} released
                </p>
              </div>
            </div>
          ) : (
            <div className="rounded-lg border border-red-200 bg-red-50 p-3 text-sm text-red-700">
              Exactly one current-tenant project must be linked before any Works
              closeout action.
            </div>
          )}

          <div className="grid gap-3 md:grid-cols-3">
            {overview.checks.map((check) => (
              <div key={check.key} className="rounded-lg border p-3">
                <div className="flex items-start gap-2">
                  <CheckIcon status={check.status} />
                  <div className="min-w-0">
                    <div className="flex flex-wrap items-center gap-2">
                      <p className="font-medium">{check.label}</p>
                      <Badge variant="outline">
                        {checkStatus(check.status)}
                      </Badge>
                    </div>
                    <p className="mt-1 text-xs text-muted-foreground">
                      {check.message}
                    </p>
                    <p className="mt-1 truncate font-mono text-[10px] text-muted-foreground">
                      {check.code}
                    </p>
                  </div>
                </div>
              </div>
            ))}
          </div>
          <div className="flex flex-wrap gap-1">
            {overview.decisionKeys.map((key) => (
              <Badge
                key={key}
                variant="secondary"
                className="font-mono text-[10px]"
              >
                {key}
              </Badge>
            ))}
          </div>
        </CardContent>
      </Card>

      <Card data-testid="qs-retention-ledger">
        <CardHeader>
          <CardTitle className="text-base">Retention ledger</CardTitle>
          <CardDescription>
            Non-duplicating view of approved payment-certificate holds/releases
            and controlled Works release approvals.
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-3">
          {overview.retentionPolicy ? (
            <div className="grid gap-2 rounded-lg border bg-muted/30 p-3 text-xs sm:grid-cols-2 lg:grid-cols-5">
              <div>
                <span className="text-muted-foreground">Maximum</span>
                <p className="font-medium">
                  {overview.retentionPolicy.maximumRetentionPercent}%
                </p>
              </div>
              <div>
                <span className="text-muted-foreground">
                  Practical completion
                </span>
                <p className="font-medium">
                  {overview.retentionPolicy.practicalCompletionReleasePercent}%
                </p>
              </div>
              <div>
                <span className="text-muted-foreground">
                  Sectional takeover
                </span>
                <p className="font-medium">
                  {overview.retentionPolicy.sectionalTakeoverReleasePercent}%
                </p>
              </div>
              <div>
                <span className="text-muted-foreground">Defects release</span>
                <p className="font-medium">
                  {overview.retentionPolicy.defectsReleasePercent}%
                </p>
              </div>
              <div>
                <span className="text-muted-foreground">Defects period</span>
                <p className="font-medium">
                  {overview.retentionPolicy.defectsLiabilityDays} days
                </p>
              </div>
            </div>
          ) : (
            <div className="rounded-lg border border-amber-200 bg-amber-50 p-3 text-sm text-amber-800">
              Publish and evidence-verify QS-DEC-009 before requesting a
              retention release.
            </div>
          )}
          <div className="overflow-x-auto rounded-lg border">
            <table className="w-full min-w-[760px] text-left text-xs">
              <thead className="bg-muted/60 text-muted-foreground">
                <tr>
                  <th className="px-3 py-2">Date</th>
                  <th className="px-3 py-2">Source</th>
                  <th className="px-3 py-2">Stage</th>
                  <th className="px-3 py-2 text-right">Held</th>
                  <th className="px-3 py-2 text-right">Released</th>
                  <th className="px-3 py-2 text-right">Outstanding</th>
                </tr>
              </thead>
              <tbody>
                {overview.retentionLedger.map((entry) => (
                  <tr
                    key={`${entry.sourceType}-${entry.sourceId}`}
                    className="border-t"
                  >
                    <td className="px-3 py-2">
                      {formatDate(entry.effectiveAtUtc)}
                    </td>
                    <td className="px-3 py-2">
                      <p className="font-medium">{entry.sourceReference}</p>
                      <p className="text-muted-foreground">
                        {displayName(entry.sourceType)}
                      </p>
                    </td>
                    <td className="px-3 py-2">
                      {displayName(retentionStage(entry.releaseStage) || '—')}
                    </td>
                    <td className="px-3 py-2 text-right">
                      {entry.currency} {entry.heldAmount.toLocaleString()}
                    </td>
                    <td className="px-3 py-2 text-right">
                      {entry.currency} {entry.releasedAmount.toLocaleString()}
                    </td>
                    <td className="px-3 py-2 text-right font-medium">
                      {entry.currency}{' '}
                      {entry.outstandingAmount.toLocaleString()}
                    </td>
                  </tr>
                ))}
                {overview.retentionLedger.length === 0 && (
                  <tr>
                    <td
                      colSpan={6}
                      className="px-3 py-8 text-center text-muted-foreground"
                    >
                      No approved certificate retention movement exists for this
                      contract.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </CardContent>
      </Card>

      {!openAction && overview.project && !overview.isClosed && (
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-base">
              <FileCheck2 className="h-4 w-4" />
              Submit controlled Works action
            </CardTitle>
            <CardDescription>
              Source records are reused from Projects. Documents must already be
              malware-clean, current central-DMS versions attached to this
              contract.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="grid gap-4 md:grid-cols-2">
              <div className="space-y-2">
                <Label>Action</Label>
                <Select
                  value={selectedType}
                  onValueChange={(value) =>
                    setSelectedType(value as WorksCloseoutActionType)
                  }
                >
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {actionTypes.map((value) => (
                      <SelectItem key={value} value={value}>
                        {displayName(value)}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>

              {selectedType === 'RetentionRelease' && (
                <div className="space-y-2">
                  <Label>Release stage</Label>
                  <Select
                    value={retentionReleaseStage}
                    onValueChange={(value) => {
                      setRetentionReleaseStage(
                        value as Exclude<RetentionReleaseStage, number>
                      );
                      setSourceId('');
                      setAmount('');
                    }}
                  >
                    <SelectTrigger>
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {retentionReleaseStages.map((value) => (
                        <SelectItem key={value} value={value}>
                          {displayName(value)}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
              )}

              {needsHandover && (
                <div className="space-y-2">
                  <Label>
                    {selectedType === 'InitialTakeover'
                      ? 'Practical completion item'
                      : 'Final completion item'}
                  </Label>
                  <Select value={sourceId} onValueChange={setSourceId}>
                    <SelectTrigger>
                      <SelectValue placeholder="Select completed handover item" />
                    </SelectTrigger>
                    <SelectContent>
                      {overview.handoverItems
                        .filter((item) => item.status === 'Completed')
                        .filter((item) =>
                          selectedType === 'InitialTakeover'
                            ? item.handoverType === 'PracticalCompletion'
                            : item.handoverType === 'FinalCompletion'
                        )
                        .map((item) => (
                          <SelectItem key={item.id} value={item.id}>
                            {item.referenceNumber || item.title}
                          </SelectItem>
                        ))}
                    </SelectContent>
                  </Select>
                </div>
              )}

              {needsRetentionHandover && (
                <div className="space-y-2">
                  <Label>
                    {retentionReleaseStage === 'SectionalTakeover'
                      ? 'Completed sectional handover'
                      : 'Completed practical handover'}
                  </Label>
                  <Select value={sourceId} onValueChange={setSourceId}>
                    <SelectTrigger>
                      <SelectValue placeholder="Select controlled handover item" />
                    </SelectTrigger>
                    <SelectContent>
                      {overview.handoverItems
                        .filter(
                          (item) =>
                            item.status === 'Completed' &&
                            item.handoverType === 'PracticalCompletion'
                        )
                        .filter(
                          (item) =>
                            retentionReleaseStage !== 'SectionalTakeover' ||
                            item.projectUnitId
                        )
                        .map((item) => (
                          <SelectItem key={item.id} value={item.id}>
                            {item.referenceNumber || item.title}
                          </SelectItem>
                        ))}
                    </SelectContent>
                  </Select>
                </div>
              )}

              {needsDefect && (
                <div className="space-y-2">
                  <Label>Resolved defect</Label>
                  <Select value={sourceId} onValueChange={setSourceId}>
                    <SelectTrigger>
                      <SelectValue placeholder="Select resolved defect" />
                    </SelectTrigger>
                    <SelectContent>
                      {overview.defects
                        .filter((item) =>
                          ['Resolved', 'Closed'].includes(item.status)
                        )
                        .map((item) => (
                          <SelectItem key={item.id} value={item.id}>
                            {item.title} · {item.status}
                          </SelectItem>
                        ))}
                    </SelectContent>
                  </Select>
                </div>
              )}

              {needsSecurity && overview.performanceSecurity && (
                <div className="space-y-2">
                  <Label>Performance security</Label>
                  <Select value={sourceId} onValueChange={setSourceId}>
                    <SelectTrigger>
                      <SelectValue placeholder="Select approved security" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value={overview.performanceSecurity.id}>
                        {overview.performanceSecurity.id.slice(0, 8)} ·{' '}
                        {overview.performanceSecurity.status}
                      </SelectItem>
                    </SelectContent>
                  </Select>
                </div>
              )}

              {selectedType === 'RetentionRelease' &&
                overview.paymentCertificates.length > 0 && (
                  <div className="space-y-2">
                    <Label>Retention certificate (optional source)</Label>
                    <Select
                      value={certificateId}
                      onValueChange={setCertificateId}
                    >
                      <SelectTrigger>
                        <SelectValue placeholder="Select certificate" />
                      </SelectTrigger>
                      <SelectContent>
                        {overview.paymentCertificates.map((item) => (
                          <SelectItem key={item.id} value={item.id}>
                            {item.certificateNumber || item.title} ·{' '}
                            {item.status}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </div>
                )}

              <div className="space-y-2">
                <Label>Effective date/time</Label>
                <Input
                  type="datetime-local"
                  value={effectiveAt}
                  onChange={(event) => setEffectiveAt(event.target.value)}
                />
              </div>

              {isMonetary && (
                <>
                  <div className="space-y-2">
                    <Label>Controlled amount</Label>
                    <Input
                      type="number"
                      min="0"
                      step="0.01"
                      value={amount}
                      onChange={(event) => setAmount(event.target.value)}
                    />
                    {selectedType === 'RetentionRelease' && (
                      <div className="flex items-center justify-between gap-2 text-xs text-muted-foreground">
                        <span>
                          Available for this stage: {currency}{' '}
                          {retentionStageLimit.toLocaleString(undefined, {
                            maximumFractionDigits: 2,
                          })}
                        </span>
                        <Button
                          type="button"
                          size="sm"
                          variant="ghost"
                          className="h-7 px-2 text-xs"
                          onClick={() =>
                            setAmount(retentionStageLimit.toFixed(2))
                          }
                          disabled={retentionStageLimit <= 0}
                        >
                          Use limit
                        </Button>
                      </div>
                    )}
                  </div>
                  <div className="space-y-2">
                    <Label>Currency</Label>
                    <Input
                      maxLength={3}
                      value={currency}
                      readOnly
                      aria-readonly="true"
                    />
                    <p className="text-xs text-muted-foreground">
                      Controlled by the linked contract.
                    </p>
                  </div>
                </>
              )}
            </div>

            <div className="space-y-2">
              <Label>Documented reason</Label>
              <Textarea
                value={reason}
                onChange={(event) => setReason(event.target.value)}
                rows={3}
              />
            </div>

            {selectedType === 'RetentionRelease' &&
              overview.retentionPolicy?.allowRetentionBond && (
                <div className="flex items-start gap-3 rounded-lg border p-3">
                  <Checkbox
                    id="uses-retention-bond"
                    checked={usesRetentionBond}
                    disabled={!retentionBondReady}
                    onCheckedChange={(value) =>
                      setUsesRetentionBond(value === true)
                    }
                  />
                  <div>
                    <Label htmlFor="uses-retention-bond">
                      Retention-bond substitution
                    </Label>
                    <p className="text-xs text-muted-foreground">
                      {retentionBondReady
                        ? 'Uses the approved controlled performance-security record. The configured workflow and Finance approval still apply; this does not post or release cash automatically.'
                        : 'No approved controlled performance-security record is available. Bond substitution cannot be selected.'}
                    </p>
                  </div>
                </div>
              )}

            <div className="grid gap-3 md:grid-cols-2">
              {requirements.map((key) => (
                <div key={key} className="space-y-2 rounded-lg border p-3">
                  <Label>{displayName(key.replace(/-/g, ' '))}</Label>
                  <Select
                    value={selectedDocuments[key] || ''}
                    onValueChange={(value) =>
                      setSelectedDocuments((current) => ({
                        ...current,
                        [key]: value,
                      }))
                    }
                  >
                    <SelectTrigger>
                      <SelectValue placeholder="Select central-DMS document" />
                    </SelectTrigger>
                    <SelectContent>
                      {dmsDocuments.map((document) => (
                        <SelectItem
                          key={`${key}-${document.id}`}
                          value={document.fileUploadRecordId}
                        >
                          {document.fileName}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
              ))}
            </div>

            {isMonetary && (
              <div className="rounded-lg border border-blue-200 bg-blue-50 p-3 text-sm text-blue-800">
                This action records an independently approved
                release/final-account instruction only. AmountAutoPosted is
                permanently false; the existing Finance approval and posting
                path remains mandatory.
              </div>
            )}

            <Button onClick={() => void submit()} disabled={busy || !canSubmit}>
              {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Submit {displayName(selectedType)}
            </Button>
          </CardContent>
        </Card>
      )}

      {openAction && (
        <Card className="border-blue-200">
          <CardHeader>
            <CardTitle className="text-base">
              Pending #{openAction.sequence}:{' '}
              {displayName(actionType(openAction.actionType))}
            </CardTitle>
            <CardDescription>
              {openAction.authorityName} · workflow{' '}
              {openAction.workflowInstanceId || 'starting'}
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="grid gap-3 text-sm md:grid-cols-3">
              <div>
                <span className="text-muted-foreground">Submitted by</span>
                <p className="font-medium">{openAction.submittedByName}</p>
              </div>
              <div>
                <span className="text-muted-foreground">Submitted</span>
                <p className="font-medium">
                  {formatDate(openAction.submittedAtUtc)}
                </p>
              </div>
              <div>
                <span className="text-muted-foreground">Status</span>
                <p>
                  <Badge>{actionStatus(openAction.status)}</Badge>
                </p>
              </div>
            </div>
            <p className="rounded-lg bg-muted p-3 text-sm">
              {openAction.reason}
            </p>
            <div className="space-y-2">
              <Label>Independent decision comment</Label>
              <Textarea
                value={decisionComment}
                onChange={(event) => setDecisionComment(event.target.value)}
                rows={3}
              />
            </div>
            <div className="flex gap-2">
              <Button
                onClick={() => void decide(true)}
                disabled={busy || !decisionComment.trim()}
              >
                {actionStatus(openAction.status) === 'RevalidationFailed'
                  ? 'Retry approved application'
                  : 'Approve / progress workflow'}
              </Button>
              {actionStatus(openAction.status) !== 'RevalidationFailed' && (
                <Button
                  variant="destructive"
                  onClick={() => void decide(false)}
                  disabled={busy || !decisionComment.trim()}
                >
                  Reject
                </Button>
              )}
            </div>
          </CardContent>
        </Card>
      )}

      <Card>
        <CardHeader>
          <CardTitle className="text-base">
            Immutable Works closeout history
          </CardTitle>
          <CardDescription>
            Configuration, authority, workflow, source hashes, central evidence,
            actors, and DEC-001 through DEC-014 are retained.
          </CardDescription>
        </CardHeader>
        <CardContent>
          {overview.history.length === 0 ? (
            <p className="py-8 text-center text-sm text-muted-foreground">
              No controlled Works closeout actions yet.
            </p>
          ) : (
            <div className="space-y-3">
              {overview.history.map((item) => (
                <div key={item.id} className="rounded-lg border p-3">
                  <div className="flex flex-wrap items-center justify-between gap-2">
                    <div className="flex items-center gap-2">
                      <span className="font-semibold">
                        #{item.sequence}{' '}
                        {displayName(actionType(item.actionType))}
                      </span>
                      <Badge variant="outline">
                        {actionStatus(item.status)}
                      </Badge>
                    </div>
                    <span className="text-xs text-muted-foreground">
                      {formatDate(item.submittedAtUtc)}
                    </span>
                  </div>
                  <div className="mt-2 grid gap-2 text-xs text-muted-foreground md:grid-cols-3">
                    <span>{item.authorityName}</span>
                    <span>
                      Profile v{item.configurationProfileVersion} / policy v
                      {item.policyVersion}
                    </span>
                    <span>
                      {item.requiresIndependentFinanceApproval
                        ? 'Finance approval required'
                        : 'Non-monetary'}{' '}
                      · no auto-post
                    </span>
                  </div>
                  <p className="mt-2 font-mono text-[10px] text-muted-foreground">
                    Integrity {item.integrityHash}
                  </p>
                </div>
              ))}
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
