'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  AlertTriangle,
  CheckCircle2,
  Clock3,
  FileCheck2,
  Loader2,
  RefreshCw,
  ShieldCheck,
  XCircle,
} from 'lucide-react';
import { toast } from 'sonner';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import {
  contractService,
  type ContractActivation,
  type ContractActivationCheck,
  type ContractActivationOverview,
  type ContractDocumentDto,
} from '@/services/contractService';
import { useAuth } from '@/hooks/use-auth';
import { getProcurementProblemMessage } from '@/lib/procurement-tender-header-actions';

interface ContractActivationGateProps {
  contractId: string;
  contractRowVersion: string;
  documents: ContractDocumentDto[];
  onContractChanged: () => void | Promise<void>;
}

const checkStatusNames = ['Passed', 'Failed', 'NotRequired', 'Pending'] as const;
const activationStatusNames = [
  'PendingApproval',
  'Approved',
  'Rejected',
  'Activated',
  'RevalidationFailed',
  'Cancelled',
] as const;

function checkStatus(value: ContractActivationCheck['status']): string {
  return typeof value === 'number' ? checkStatusNames[value] ?? String(value) : value;
}

function activationStatus(value: ContractActivation['status']): string {
  return typeof value === 'number' ? activationStatusNames[value] ?? String(value) : value;
}

function checkMessage(check: ContractActivationCheck): string {
  if (check.key === 'configuration' && checkStatus(check.status) === 'Failed') {
    return 'Contract activation setup is not available. Ask an administrator to publish the required configuration.';
  }

  return check.message;
}

function CheckIcon({ status }: { status: ContractActivationCheck['status'] }) {
  const value = checkStatus(status);
  if (value === 'Passed' || value === 'NotRequired') {
    return <CheckCircle2 className="h-4 w-4 text-emerald-600" />;
  }
  if (value === 'Failed') return <XCircle className="h-4 w-4 text-red-600" />;
  return <Clock3 className="h-4 w-4 text-amber-600" />;
}

export function ContractActivationGate({
  contractId,
  contractRowVersion,
  documents,
  onContractChanged,
}: ContractActivationGateProps) {
  const { hasPermission } = useAuth();
  const canManageContract = hasPermission('procurement.contract.manage');
  const canApproveContract = hasPermission('procurement.contract.approve');
  const [overview, setOverview] = useState<ContractActivationOverview | null>(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [reason, setReason] = useState('');
  const [decisionComment, setDecisionComment] = useState('');
  const [contractorSignatory, setContractorSignatory] = useState('');
  const [activationComment, setActivationComment] = useState('');
  const [selectedDocuments, setSelectedDocuments] = useState<Record<string, string>>({});

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      setOverview(await contractService.getActivationOverview(contractId));
    } catch (loadError) {
      setError(loadError instanceof Error ? loadError.message : 'Activation controls could not be loaded.');
    } finally {
      setLoading(false);
    }
  }, [contractId]);

  useEffect(() => {
    void load();
  }, [load]);

  const dmsDocuments = useMemo(
    () => documents.filter((document): document is ContractDocumentDto & {
      fileUploadRecordId: string;
      centralDocumentRecordId: string;
      centralDocumentVersionId: string;
    } => Boolean(
      document.fileUploadRecordId &&
      document.centralDocumentRecordId &&
      document.centralDocumentVersionId
    )),
    [documents]
  );
  const current = overview?.history[0];
  const activationComplete = Boolean(
    overview?.contractStatus === 'Active' &&
    overview.history.some((item) => activationStatus(item.status) === 'Activated')
  );
  const failedChecks = overview?.checks.filter((check) => checkStatus(check.status) === 'Failed') ?? [];
  const requiredEvidenceKeys = overview?.requiredEvidenceKeys ?? [];
  const allEvidenceSelected = requiredEvidenceKeys.length > 0 &&
    requiredEvidenceKeys.every((key) => selectedDocuments[key]);

  const refreshAfterMutation = async () => {
    await Promise.all([load(), Promise.resolve(onContractChanged())]);
  };

  const submit = async () => {
    if (!canManageContract) {
      toast.error('Contract management permission is required');
      return;
    }
    if (!overview || !reason.trim() || !allEvidenceSelected) return;
    setBusy(true);
    try {
      await contractService.submitActivation(contractId, {
        reason: reason.trim(),
        idempotencyKey: crypto.randomUUID(),
        contractRowVersion,
        evidence: overview.requiredEvidenceKeys.map((requirementKey) => {
          const document = dmsDocuments.find(
            (item) => item.fileUploadRecordId === selectedDocuments[requirementKey]
          );
          if (!document) {
            throw new Error(`Select central-DMS evidence for ${requirementKey}.`);
          }
          return {
            requirementKey,
            referenceKind: 'CentralDocumentUpload',
            fileUploadRecordId: document.fileUploadRecordId,
            evidenceReference: document.filePath || `dms:${document.centralDocumentRecordId}`,
          };
        }),
      });
      toast.success('Contract activation submitted to the configured workflow.');
      setReason('');
      setSelectedDocuments({});
      await refreshAfterMutation();
    } catch (submitError) {
      toast.error(getProcurementProblemMessage(submitError, 'Activation submission failed.'));
    } finally {
      setBusy(false);
    }
  };

  const decide = async (approved: boolean) => {
    if (!canApproveContract) {
      toast.error('Contract approval permission is required');
      return;
    }
    if (!current || !decisionComment.trim()) return;
    setBusy(true);
    try {
      await contractService.decideActivation(
        current.id,
        approved,
        decisionComment.trim(),
        current.rowVersion
      );
      toast.success(approved ? 'Approval step recorded.' : 'Contract activation rejected.');
      setDecisionComment('');
      await refreshAfterMutation();
    } catch (decisionError) {
      toast.error(getProcurementProblemMessage(decisionError, 'Activation decision failed.'));
    } finally {
      setBusy(false);
    }
  };

  const activate = async () => {
    if (!canApproveContract) {
      toast.error('Contract approval permission is required');
      return;
    }
    if (!current || !contractorSignatory.trim() || !activationComment.trim()) return;
    setBusy(true);
    try {
      await contractService.applyActivation(
        current.id,
        contractorSignatory.trim(),
        activationComment.trim(),
        current.rowVersion
      );
      toast.success('Contract activated through the controlled gate.');
      setContractorSignatory('');
      setActivationComment('');
      await refreshAfterMutation();
    } catch (activationError) {
      toast.error(getProcurementProblemMessage(activationError, 'Contract activation failed.'));
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
          Revalidating contract activation controls…
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
              <p className="font-semibold">Activation controls unavailable</p>
              <p className="text-sm">{error || 'The activation state could not be loaded.'}</p>
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

  return (
    <div className="space-y-4" data-testid="contract-activation-gate">
      <Card>
        <CardHeader className="flex flex-row items-start justify-between gap-4">
          <div>
            <CardTitle className="flex items-center gap-2">
              <ShieldCheck className="h-5 w-5 text-blue-600" />
              Contract approval and activation gate
            </CardTitle>
            <CardDescription>
              Legal, Internal Audit, authority, award, GHANEPS, signature, performance-security,
              and central-DMS evidence are revalidated before activation.
            </CardDescription>
          </div>
          <Button variant="ghost" size="sm" onClick={() => void load()} disabled={busy}>
            <RefreshCw className="mr-2 h-4 w-4" />
            Refresh
          </Button>
        </CardHeader>
        <CardContent className="space-y-3">
          {activationComplete ? (
            <div className="flex items-start gap-3 rounded-lg border border-emerald-200 bg-emerald-50 p-4 text-emerald-800">
              <CheckCircle2 className="mt-0.5 h-5 w-5" />
              <div>
                <p className="font-medium">Contract activated</p>
                <p className="text-sm">The completed approval and activation history is retained below.</p>
              </div>
            </div>
          ) : (
            <div className="grid gap-3 md:grid-cols-2">
            {overview.checks.map((check) => (
              <div key={check.key} className="rounded-lg border p-3">
                <div className="flex items-start gap-2">
                  <CheckIcon status={check.status} />
                  <div className="min-w-0">
                    <div className="flex flex-wrap items-center gap-2">
                      <p className="font-medium">{check.label}</p>
                      <Badge variant="outline">{checkStatus(check.status)}</Badge>
                    </div>
                    <p className="mt-1 text-xs text-muted-foreground">{checkMessage(check)}</p>
                  </div>
                </div>
              </div>
            ))}
            </div>
          )}
        </CardContent>
      </Card>

      {overview.canSubmit && canManageContract && (
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-base">
              <FileCheck2 className="h-4 w-4" />
              Submit controlled activation
            </CardTitle>
            <CardDescription>
              Select a clean document already registered against this contract in the central DMS
              for every configured requirement.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            {failedChecks.length > 0 && (
              <div className="rounded-md border border-red-200 bg-red-50 p-3 text-sm text-red-700">
                Resolve the failed server checks before submission.
              </div>
            )}
            {dmsDocuments.length === 0 && (
              <div className="rounded-md border border-amber-200 bg-amber-50 p-3 text-sm text-amber-800">
                Upload the required contract evidence in the Documents tab first. New uploads are
                malware-scanned and registered in the central DMS.
              </div>
            )}
            <div className="grid gap-3 md:grid-cols-2">
              {overview.requiredEvidenceKeys.map((key) => (
                <div key={key} className="space-y-1.5">
                  <Label htmlFor={`evidence-${key}`}>{key}</Label>
                  <Select
                    value={selectedDocuments[key] || ''}
                    onValueChange={(value) =>
                      setSelectedDocuments((currentSelections) => ({
                        ...currentSelections,
                        [key]: value,
                      }))
                    }
                  >
                    <SelectTrigger id={`evidence-${key}`}>
                      <SelectValue placeholder="Select central-DMS evidence" />
                    </SelectTrigger>
                    <SelectContent>
                      {dmsDocuments.map((document) => (
                        <SelectItem key={`${key}-${document.id}`} value={document.fileUploadRecordId}>
                          {document.fileName} · {document.documentType}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
              ))}
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="activation-reason">Submission reason</Label>
              <Textarea
                id="activation-reason"
                value={reason}
                onChange={(event) => setReason(event.target.value)}
                placeholder="Explain why the contract is ready for controlled activation."
              />
            </div>
            <Button
              onClick={() => void submit()}
              disabled={busy || failedChecks.length > 0 || !reason.trim() || !allEvidenceSelected}
            >
              {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Submit to shared workflow
            </Button>
          </CardContent>
        </Card>
      )}

      {current && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">
              Activation #{current.sequence} · {activationStatus(current.status)}
            </CardTitle>
            <CardDescription>
              Submitted by {current.submittedByName}. Authority: {current.authorityName}.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="grid gap-2 text-sm md:grid-cols-3">
              <p><span className="text-muted-foreground">Configuration:</span> v{current.configurationProfileVersion}</p>
              <p><span className="text-muted-foreground">Policy:</span> v{current.policyVersion}</p>
              <p><span className="text-muted-foreground">Readiness:</span> #{current.awardReadinessSequence}</p>
            </div>
            {overview.canDecide && canApproveContract && (
              <div className="space-y-3 rounded-lg border p-4">
                <Label htmlFor="activation-decision">Independent decision comment</Label>
                <Textarea
                  id="activation-decision"
                  value={decisionComment}
                  onChange={(event) => setDecisionComment(event.target.value)}
                  placeholder="Record the Legal/authority approval or rejection rationale."
                />
                <div className="flex gap-2">
                  <Button onClick={() => void decide(true)} disabled={busy || !decisionComment.trim()}>
                    Approve current workflow step
                  </Button>
                  <Button
                    variant="destructive"
                    onClick={() => void decide(false)}
                    disabled={busy || !decisionComment.trim()}
                  >
                    Reject
                  </Button>
                </div>
              </div>
            )}
            {overview.canActivate && canApproveContract && (
              <div className="grid gap-3 rounded-lg border border-emerald-200 p-4 md:grid-cols-2">
                <div className="space-y-1.5">
                  <Label htmlFor="contractor-signatory">Contractor signatory</Label>
                  <Input
                    id="contractor-signatory"
                    value={contractorSignatory}
                    onChange={(event) => setContractorSignatory(event.target.value)}
                    placeholder="Contractor representative"
                  />
                </div>
                <div className="space-y-1.5 md:col-span-2">
                  <Label htmlFor="activation-comment">Activation comment</Label>
                  <Textarea
                    id="activation-comment"
                    value={activationComment}
                    onChange={(event) => setActivationComment(event.target.value)}
                    placeholder="Confirm final revalidation and activation."
                  />
                </div>
                <Button
                  className="w-fit bg-emerald-600 hover:bg-emerald-700"
                  onClick={() => void activate()}
                  disabled={busy || !contractorSignatory.trim() || !activationComment.trim()}
                >
                  {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                  Revalidate and activate
                </Button>
              </div>
            )}
          </CardContent>
        </Card>
      )}

      {overview.history.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Immutable activation history</CardTitle>
          </CardHeader>
          <CardContent className="space-y-2">
            {overview.history.map((item) => (
              <div key={item.id} className="flex flex-wrap items-center justify-between gap-3 rounded-md border p-3 text-sm">
                <div>
                  <p className="font-medium">#{item.sequence} · {activationStatus(item.status)}</p>
                  <p className="text-xs text-muted-foreground">
                    {item.reason} · {item.evidence.length} evidence reference(s)
                  </p>
                </div>
                <Badge variant="outline" className="font-mono text-[10px]">
                  {item.integrityHash.slice(0, 12)}
                </Badge>
              </div>
            ))}
          </CardContent>
        </Card>
      )}
    </div>
  );
}
