'use client';

import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import {
  CheckCircle2,
  Download,
  FileUp,
  RefreshCw,
  Save,
  Send,
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
import { QuantitySurveySubcontractChargesPanel } from '@/components/quantity-survey/QuantitySurveySubcontractChargesPanel';
import { useAuth } from '@/hooks/use-auth';
import {
  quantitySurveySubcontractService as service,
  type Subcontract,
  type SubcontractValuation,
  type SubcontractWorkspace,
} from '@/services/quantity-survey-subcontract.service';

type Props = { projectId: string; external?: boolean };
const empty: SubcontractWorkspace = {
  contracts: [],
  subcontractors: [],
  paymentTerms: [],
  subcontracts: [],
};
const today = () => new Date().toISOString().slice(0, 10);
const label = (value: string) => value.replace(/([a-z])([A-Z])/g, '$1 $2');
const money = (value: number, currency = 'GHS') =>
  new Intl.NumberFormat(undefined, {
    style: 'currency',
    currency,
    maximumFractionDigits: 2,
  }).format(value || 0);

export function QuantitySurveySubcontractWorkspace({
  projectId,
  external = false,
}: Props) {
  const { hasPermission } = useAuth();
  const canRead = external || hasPermission('quantity-survey.workspace.read');
  const canManage =
    !external && hasPermission('quantity-survey.final-accounts.manage');
  const canCertify =
    !external && hasPermission('quantity-survey.certificates.manage');
  const canApprove =
    !external && hasPermission('quantity-survey.transactions.approve');
  const requests = useRef<Record<string, string>>({});
  const [workspace, setWorkspace] = useState(empty);
  const [selectedId, setSelectedId] = useState('');
  const [valuationId, setValuationId] = useState('');
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [reason, setReason] = useState('');
  const [contractId, setContractId] = useState('');
  const [partnerId, setPartnerId] = useState('');
  const [paymentTermId, setPaymentTermId] = useState('');
  const [title, setTitle] = useState('');
  const [scope, setScope] = useState('');
  const [value, setValue] = useState(0);
  const [retention, setRetention] = useState(0);
  const [startDate, setStartDate] = useState(today());
  const [endDate, setEndDate] = useState('');
  const [valuationDate, setValuationDate] = useState(today());
  const [periodEndDate, setPeriodEndDate] = useState(today());
  const [claimToDate, setClaimToDate] = useState(0);
  const [isFinal, setIsFinal] = useState(false);
  const [submissionNote, setSubmissionNote] = useState('');
  const [assessedToDate, setAssessedToDate] = useState(0);
  const [retentionReleased, setRetentionReleased] = useState(0);
  const [selectedChargeIds, setSelectedChargeIds] = useState<string[]>([]);
  const [evidenceTitle, setEvidenceTitle] = useState('');
  const [evidenceFile, setEvidenceFile] = useState<File | null>(null);
  const selected = useMemo(
    () => workspace.subcontracts.find((item) => item.id === selectedId),
    [selectedId, workspace.subcontracts]
  );
  const valuation = useMemo(
    () => selected?.valuations.find((item) => item.id === valuationId),
    [selected, valuationId]
  );
  const selectedContract = workspace.contracts.find(
    (item) => item.id === contractId
  );
  const requestId = (key: string) =>
    requests.current[key] ?? (requests.current[key] = crypto.randomUUID());
  const complete = (key: string) => {
    delete requests.current[key];
  };

  const load = useCallback(
    async (preferred?: string, preferredValuation?: string) => {
      setLoading(true);
      try {
        const data = await service.workspace(projectId, external);
        setWorkspace(data);
        const next = preferred ?? data.subcontracts[0]?.id ?? '';
        setSelectedId(next);
        const row = data.subcontracts.find((item) => item.id === next);
        setValuationId(preferredValuation ?? row?.valuations[0]?.id ?? '');
      } catch (error) {
        toast.error(
          error instanceof Error
            ? error.message
            : 'Subcontracts could not be loaded.'
        );
      } finally {
        setLoading(false);
      }
    },
    [external, projectId]
  );
  useEffect(() => {
    if (canRead) void load();
  }, [canRead, load]);

  const choose = (item: Subcontract) => {
    setSelectedId(item.id);
    setValuationId(item.valuations[0]?.id ?? '');
    setContractId(item.contractId);
    setPartnerId(item.subcontractorBusinessPartnerId);
    setPaymentTermId(item.paymentTermId);
    setTitle(item.title);
    setScope(item.scope);
    setValue(item.subcontractValue);
    setRetention(item.retentionPercentage);
    setStartDate(item.startDate.slice(0, 10));
    setEndDate(item.endDate?.slice(0, 10) ?? '');
    setSelectedChargeIds([]);
  };
  const chooseValuation = (item: SubcontractValuation) => {
    setValuationId(item.id);
    setValuationDate(item.valuationDate.slice(0, 10));
    setPeriodEndDate(item.periodEndDate.slice(0, 10));
    setClaimToDate(item.claimedToDateAmount);
    setIsFinal(item.isFinal);
    setSubmissionNote(item.submissionNote ?? '');
    setAssessedToDate(item.assessedToDateAmount ?? item.claimedToDateAmount);
    setRetentionReleased(item.retentionReleasedAmount);
    setSelectedChargeIds([]);
  };
  const run = async (
    key: string,
    operation: () => Promise<unknown>,
    message: string,
    preferredValuation?: string
  ) => {
    setBusy(true);
    try {
      await operation();
      complete(key);
      setReason('');
      toast.success(message);
      await load(selectedId, preferredValuation ?? valuationId);
    } catch (error) {
      toast.error(
        error instanceof Error
          ? error.message
          : 'The subcontract action failed.'
      );
    } finally {
      setBusy(false);
    }
  };

  const saveSubcontract = async () => {
    if (
      !contractId ||
      !partnerId ||
      !paymentTermId ||
      title.trim().length < 3 ||
      scope.trim().length < 10 ||
      value <= 0
    ) {
      toast.error(
        'Select the controlled contract, subcontractor and payment term, then complete the commercial terms.'
      );
      return;
    }
    const key = `save-sub:${selected?.id ?? 'new'}:${contractId}:${partnerId}:${paymentTermId}:${title}:${scope}:${value}:${retention}:${startDate}:${endDate}`;
    await run(
      key,
      () =>
        service.save(projectId, {
          id: selected?.id ?? null,
          clientRequestId: requestId(key),
          contractId,
          subcontractorBusinessPartnerId: partnerId,
          paymentTermId,
          title: title.trim(),
          scope: scope.trim(),
          subcontractValue: value,
          retentionPercentage: retention,
          startDate: `${startDate}T00:00:00Z`,
          endDate: endDate ? `${endDate}T00:00:00Z` : null,
          rowVersion: selected?.rowVersion ?? null,
        }),
      'Subcontract Draft saved.'
    );
  };
  const saveValuation = async () => {
    if (!selected || claimToDate <= 0) {
      toast.error(
        'Select an approved subcontract and enter a positive claimed-to-date amount.'
      );
      return;
    }
    const key = `save-val:${valuation?.id ?? 'new'}:${valuationDate}:${periodEndDate}:${claimToDate}:${isFinal}:${submissionNote}`;
    await run(
      key,
      () =>
        service.saveValuation(
          projectId,
          selected.id,
          {
            id: valuation?.id ?? null,
            clientRequestId: requestId(key),
            valuationDate: `${valuationDate}T00:00:00Z`,
            periodEndDate: `${periodEndDate}T00:00:00Z`,
            claimedToDateAmount: claimToDate,
            isFinal,
            submissionNote: submissionNote.trim() || null,
            rowVersion: valuation?.rowVersion ?? null,
          },
          external
        ),
      'Subcontract valuation Draft saved.'
    );
  };
  const upload = async (targetOverride?: string | null) => {
    const target =
      targetOverride === undefined ? (valuation?.id ?? null) : targetOverride;
    if (
      !selected ||
      !evidenceFile ||
      evidenceTitle.trim().length < 3 ||
      (external && !target)
    ) {
      toast.error('Select the target record, evidence title and file.');
      return;
    }
    const key = `evidence:${selected.id}:${target}:${evidenceFile.name}:${evidenceFile.size}:${evidenceTitle}`;
    await run(
      key,
      () =>
        service.uploadEvidence(
          projectId,
          selected.id,
          target,
          requestId(key),
          evidenceTitle.trim(),
          evidenceFile,
          external
        ),
      'Clean evidence retained in the central DMS.',
      target ?? undefined
    );
    setEvidenceTitle('');
    setEvidenceFile(null);
  };
  const action = async (
    kind:
      | 'submitSubcontract'
      | 'approveSubcontract'
      | 'rejectSubcontract'
      | 'submitValuation'
      | 'assess'
      | 'approveValuation'
      | 'rejectValuation'
      | 'handoff'
      | 'close'
  ) => {
    if (
      !selected ||
      reason.trim().length < 5 ||
      ((kind.includes('Valuation') ||
        kind === 'assess' ||
        kind === 'handoff') &&
        !valuation)
    ) {
      toast.error(
        'Select the record and enter an action reason of at least 5 characters.'
      );
      return;
    }
    const key = `${kind}:${selected.id}:${valuation?.id ?? ''}:${selected.rowVersion}:${valuation?.rowVersion ?? ''}:${reason}:${assessedToDate}:${retentionReleased}:${selectedChargeIds.slice().sort().join(',')}`;
    const subcontractRequest = {
      clientRequestId: requestId(key),
      rowVersion: selected.rowVersion,
      reason: reason.trim(),
    };
    const valuationRequest = {
      clientRequestId: requestId(key),
      rowVersion: valuation?.rowVersion ?? '',
      reason: reason.trim(),
    };
    const valuationId = valuation?.id ?? '';
    if (
      [
        'submitValuation',
        'assess',
        'approveValuation',
        'rejectValuation',
        'handoff',
      ].includes(kind) &&
      !valuationId
    ) {
      toast.error(
        'Select a subcontract valuation before performing this action.'
      );
      return;
    }
    const operations = {
      submitSubcontract: () =>
        service.submitSubcontract(selected.id, subcontractRequest),
      approveSubcontract: () =>
        service.decideSubcontract(selected.id, true, subcontractRequest),
      rejectSubcontract: () =>
        service.decideSubcontract(selected.id, false, subcontractRequest),
      submitValuation: () =>
        service.submitValuation(
          projectId,
          selected.id,
          valuationId,
          valuationRequest,
          external
        ),
      assess: () =>
        service.assess(valuationId, {
          ...valuationRequest,
          assessedToDateAmount: assessedToDate,
          retentionReleasedAmount: retentionReleased,
          chargeNoticeIds: selectedChargeIds,
        }),
      approveValuation: () =>
        service.decideValuation(valuationId, true, valuationRequest),
      rejectValuation: () =>
        service.decideValuation(valuationId, false, valuationRequest),
      handoff: () => service.handoff(valuationId, valuationRequest),
      close: () => service.close(selected.id, subcontractRequest),
    };
    await run(
      key,
      operations[kind],
      `${label(kind)} completed.`,
      valuation?.id
    );
  };
  const download = async (evidenceId: string, fileName: string) => {
    if (!selected) return;
    try {
      const blob = await service.evidenceContent(
        projectId,
        selected.id,
        evidenceId,
        external
      );
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = fileName;
      anchor.click();
      URL.revokeObjectURL(url);
    } catch (error) {
      toast.error(
        error instanceof Error ? error.message : 'Evidence could not be opened.'
      );
    }
  };

  if (!canRead) return null;
  const editableSubcontract =
    canManage && (!selected || ['Draft', 'Rejected'].includes(selected.status));
  const editableValuation =
    (external || canCertify) &&
    selected?.status === 'Approved' &&
    (!valuation || ['Draft', 'Rejected'].includes(valuation.status));
  return (
    <Card
      data-testid={
        external ? 'qs-subcontract-portal' : 'qs-subcontract-workspace'
      }
    >
      <CardHeader className="pb-3">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <div>
            <CardTitle>Subcontract valuations and certificates</CardTitle>
            <CardDescription>
              {external
                ? 'Submit evidenced valuations and track certification and Finance-owned payment status.'
                : 'Govern subcontracts, certify assessed work, hand approved certificates to Finance AP and close final settlement.'}
            </CardDescription>
          </div>
          <Button
            variant="outline"
            size="sm"
            onClick={() => void load(selectedId, valuationId)}
            disabled={loading}
          >
            <RefreshCw className="mr-2 h-4 w-4" />
            Refresh
          </Button>
        </div>
      </CardHeader>
      <CardContent className="space-y-4">
        <div className="flex flex-wrap gap-2">
          {workspace.subcontracts.map((item) => (
            <Button
              key={item.id}
              size="sm"
              variant={item.id === selectedId ? 'default' : 'outline'}
              onClick={() => choose(item)}
            >
              {item.subcontractNumber}
              <Badge className="ml-2" variant="secondary">
                {label(item.status)}
              </Badge>
            </Button>
          ))}
          {canManage && (
            <Button
              size="sm"
              variant={!selectedId ? 'default' : 'outline'}
              onClick={() => {
                setSelectedId('');
                setValuationId('');
                setContractId('');
                setPartnerId('');
                setPaymentTermId('');
                setTitle('');
                setScope('');
                setValue(0);
                setRetention(0);
              }}
            >
              New subcontract
            </Button>
          )}
        </div>
        {canManage && (
          <div className="grid gap-3 rounded-md border p-4 md:grid-cols-4">
            <div className="grid gap-2 md:col-span-2">
              <Label>Active Works contract</Label>
              <Select
                disabled={!editableSubcontract}
                value={contractId}
                onValueChange={(id) => {
                  setContractId(id);
                  const contract = workspace.contracts.find(
                    (item) => item.id === id
                  );
                  if (contract) {
                    setPaymentTermId(contract.paymentTermId);
                    setRetention(contract.retentionPercentage);
                  }
                }}
              >
                <SelectTrigger>
                  <SelectValue placeholder="Select governed contract" />
                </SelectTrigger>
                <SelectContent>
                  {workspace.contracts.map((item) => (
                    <SelectItem key={item.id} value={item.id}>
                      {item.number} · {item.title}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="grid gap-2">
              <Label>Approved supplier / contractor</Label>
              <Select
                disabled={!editableSubcontract}
                value={partnerId}
                onValueChange={setPartnerId}
              >
                <SelectTrigger>
                  <SelectValue placeholder="Select subcontractor" />
                </SelectTrigger>
                <SelectContent>
                  {workspace.subcontractors.map((item) => (
                    <SelectItem key={item.id} value={item.id}>
                      {item.code} · {item.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="grid gap-2">
              <Label>Payment term</Label>
              <Select
                disabled={!editableSubcontract}
                value={paymentTermId}
                onValueChange={setPaymentTermId}
              >
                <SelectTrigger>
                  <SelectValue placeholder="Select Finance term" />
                </SelectTrigger>
                <SelectContent>
                  {workspace.paymentTerms
                    .filter(
                      (item) =>
                        !selectedContract ||
                        item.id === selectedContract.paymentTermId
                    )
                    .map((item) => (
                      <SelectItem key={item.id} value={item.id}>
                        {item.code} · {item.name}
                      </SelectItem>
                    ))}
                </SelectContent>
              </Select>
            </div>
            <div className="grid gap-2 md:col-span-2">
              <Label>Title</Label>
              <Input
                disabled={!editableSubcontract}
                value={title}
                onChange={(event) => setTitle(event.target.value)}
              />
            </div>
            <div className="grid gap-2">
              <Label>
                Value (
                {selectedContract?.currency ??
                  selected?.currency ??
                  'contract currency'}
                )
              </Label>
              <Input
                disabled={!editableSubcontract}
                type="number"
                min="0.01"
                step="0.01"
                value={value || ''}
                onChange={(event) => setValue(Number(event.target.value))}
              />
            </div>
            <div className="grid gap-2">
              <Label>Retention %</Label>
              <Input
                disabled={!editableSubcontract}
                type="number"
                min="0"
                max="100"
                step="0.01"
                value={retention}
                onChange={(event) => setRetention(Number(event.target.value))}
              />
            </div>
            <div className="grid gap-2">
              <Label>Start date</Label>
              <Input
                disabled={!editableSubcontract}
                type="date"
                value={startDate}
                onChange={(event) => setStartDate(event.target.value)}
              />
            </div>
            <div className="grid gap-2">
              <Label>End date</Label>
              <Input
                disabled={!editableSubcontract}
                type="date"
                value={endDate}
                onChange={(event) => setEndDate(event.target.value)}
              />
            </div>
            <div className="grid gap-2 md:col-span-4">
              <Label>Controlled scope</Label>
              <Textarea
                disabled={!editableSubcontract}
                rows={3}
                value={scope}
                onChange={(event) => setScope(event.target.value)}
              />
            </div>
            {editableSubcontract && (
              <div className="md:col-span-4 flex justify-end">
                <Button onClick={() => void saveSubcontract()} disabled={busy}>
                  <Save className="mr-2 h-4 w-4" />
                  Save Draft
                </Button>
              </div>
            )}
          </div>
        )}
        {selected && (
          <div className="space-y-4 rounded-md border p-4">
            <div className="grid gap-3 md:grid-cols-5">
              <div>
                <Label>Subcontractor</Label>
                <p>{selected.subcontractorName}</p>
              </div>
              <div>
                <Label>Subcontract value</Label>
                <p>{money(selected.subcontractValue, selected.currency)}</p>
              </div>
              <div>
                <Label>Certified to date</Label>
                <p>
                  {money(selected.certifiedToDateAmount, selected.currency)}
                </p>
              </div>
              <div>
                <Label>Retention balance</Label>
                <p>{money(selected.retentionBalance, selected.currency)}</p>
              </div>
              <div>
                <Label>Outstanding</Label>
                <p>{money(selected.outstandingBalance, selected.currency)}</p>
              </div>
            </div>
            <div className="space-y-2">
              <Label>Subcontract agreement evidence</Label>
              {selected.evidence.map((item) => (
                <div
                  key={item.id}
                  className="flex items-center justify-between rounded border p-2 text-sm"
                >
                  <span>
                    {item.title} · {item.fileName}
                  </span>
                  <Button
                    size="sm"
                    variant="ghost"
                    onClick={() => void download(item.id, item.fileName)}
                  >
                    <Download className="mr-2 h-4 w-4" />
                    Open
                  </Button>
                </div>
              ))}
            </div>
            <div className="flex flex-wrap gap-2">
              {selected.valuations.map((item) => (
                <Button
                  key={item.id}
                  size="sm"
                  variant={item.id === valuationId ? 'default' : 'outline'}
                  onClick={() => chooseValuation(item)}
                >
                  {item.valuationNumber}
                  <Badge className="ml-2" variant="secondary">
                    {label(item.status)}
                  </Badge>
                </Button>
              ))}
              {(external || canCertify) && selected.status === 'Approved' && (
                <Button
                  size="sm"
                  variant={!valuationId ? 'default' : 'outline'}
                  onClick={() => {
                    setValuationId('');
                    setValuationDate(today());
                    setPeriodEndDate(today());
                    setClaimToDate(0);
                    setIsFinal(false);
                    setSubmissionNote('');
                  }}
                >
                  New valuation
                </Button>
              )}
            </div>
            {editableValuation && (
              <div className="grid gap-3 rounded border p-3 md:grid-cols-4">
                <div className="grid gap-2">
                  <Label>Valuation date</Label>
                  <Input
                    type="date"
                    value={valuationDate}
                    onChange={(event) => setValuationDate(event.target.value)}
                  />
                </div>
                <div className="grid gap-2">
                  <Label>Period end date</Label>
                  <Input
                    type="date"
                    value={periodEndDate}
                    onChange={(event) => setPeriodEndDate(event.target.value)}
                  />
                </div>
                <div className="grid gap-2">
                  <Label>Claimed to date</Label>
                  <Input
                    type="number"
                    min="0.01"
                    max={selected.subcontractValue}
                    value={claimToDate || ''}
                    onChange={(event) =>
                      setClaimToDate(Number(event.target.value))
                    }
                  />
                </div>
                <div className="flex items-end gap-2 pb-2">
                  <Checkbox
                    checked={isFinal}
                    onCheckedChange={(checked) => setIsFinal(checked === true)}
                  />
                  <Label>Final valuation</Label>
                </div>
                <div className="grid gap-2 md:col-span-4">
                  <Label>Submission note</Label>
                  <Textarea
                    value={submissionNote}
                    onChange={(event) => setSubmissionNote(event.target.value)}
                  />
                </div>
                <div className="md:col-span-4 flex justify-end">
                  <Button onClick={() => void saveValuation()} disabled={busy}>
                    <Save className="mr-2 h-4 w-4" />
                    Save valuation Draft
                  </Button>
                </div>
              </div>
            )}
            {valuation && (
              <div className="space-y-3 rounded border p-3">
                <div className="grid gap-3 md:grid-cols-6">
                  <div>
                    <Label>Claimed to date</Label>
                    <p>
                      {money(valuation.claimedToDateAmount, selected.currency)}
                    </p>
                  </div>
                  <div>
                    <Label>Assessed to date</Label>
                    <p>
                      {valuation.assessedToDateAmount == null
                        ? 'Pending'
                        : money(
                            valuation.assessedToDateAmount,
                            selected.currency
                          )}
                    </p>
                  </div>
                  <div>
                    <Label>Current certified</Label>
                    <p>
                      {money(
                        valuation.currentCertifiedAmount,
                        selected.currency
                      )}
                    </p>
                  </div>
                  <div>
                    <Label>Retention</Label>
                    <p>
                      {money(
                        valuation.retentionHeldAmount -
                          valuation.retentionReleasedAmount,
                        selected.currency
                      )}
                    </p>
                  </div>
                  <div>
                    <Label>Approved charges</Label>
                    <p>
                      {money(
                        valuation.approvedBackChargeAmount +
                          valuation.approvedContraChargeAmount,
                        selected.currency
                      )}
                    </p>
                  </div>
                  <div>
                    <Label>Net / payment</Label>
                    <p>
                      {money(valuation.netCertifiedAmount, selected.currency)} ·{' '}
                      {label(valuation.paymentStatus)}
                    </p>
                  </div>
                </div>
                <div className="space-y-2">
                  <Label>Valuation evidence</Label>
                  {valuation.evidence.map((item) => (
                    <div
                      key={item.id}
                      className="flex items-center justify-between rounded border p-2 text-sm"
                    >
                      <span>
                        {item.title} · {item.fileName}
                      </span>
                      <Button
                        size="sm"
                        variant="ghost"
                        onClick={() => void download(item.id, item.fileName)}
                      >
                        <Download className="mr-2 h-4 w-4" />
                        Open
                      </Button>
                    </div>
                  ))}
                </div>
                {['Draft', 'Rejected'].includes(valuation.status) && (
                  <div className="grid gap-2 md:grid-cols-4">
                    <Input
                      placeholder="Evidence title"
                      value={evidenceTitle}
                      onChange={(event) => setEvidenceTitle(event.target.value)}
                    />
                    <Input
                      type="file"
                      onChange={(event) =>
                        setEvidenceFile(event.target.files?.[0] ?? null)
                      }
                    />
                    <Button
                      variant="outline"
                      onClick={() => void upload()}
                      disabled={busy}
                    >
                      <FileUp className="mr-2 h-4 w-4" />
                      Upload evidence
                    </Button>
                  </div>
                )}
                {canCertify && valuation.status === 'Submitted' && (
                  <div className="grid gap-3 md:grid-cols-2">
                    <div className="grid gap-2">
                      <Label>QS assessed to date</Label>
                      <Input
                        type="number"
                        min={valuation.previouslyCertifiedAmount}
                        max={valuation.claimedToDateAmount}
                        value={assessedToDate || ''}
                        onChange={(event) =>
                          setAssessedToDate(Number(event.target.value))
                        }
                      />
                    </div>
                    <div className="grid gap-2">
                      <Label>Retention released</Label>
                      <Input
                        type="number"
                        min="0"
                        value={retentionReleased || ''}
                        onChange={(event) =>
                          setRetentionReleased(Number(event.target.value))
                        }
                      />
                    </div>
                  </div>
                )}
              </div>
            )}
            {!external &&
              canManage &&
              ['Draft', 'Rejected'].includes(selected.status) && (
                <div className="grid gap-2 md:grid-cols-4">
                  <Input
                    placeholder="Agreement evidence title"
                    value={evidenceTitle}
                    onChange={(event) => setEvidenceTitle(event.target.value)}
                  />
                  <Input
                    type="file"
                    onChange={(event) =>
                      setEvidenceFile(event.target.files?.[0] ?? null)
                    }
                  />
                  <Button
                    variant="outline"
                    onClick={() => void upload(null)}
                    disabled={busy}
                  >
                    <FileUp className="mr-2 h-4 w-4" />
                    Upload agreement
                  </Button>
                </div>
              )}
            <QuantitySurveySubcontractChargesPanel
              projectId={projectId}
              subcontractId={selected.id}
              currency={selected.currency}
              external={external}
              valuationStatus={valuation?.status}
              selectedChargeIds={selectedChargeIds}
              onSelectionChange={setSelectedChargeIds}
            />
            <div className="grid gap-2">
              <Label>Action reason / assessment note</Label>
              <Textarea
                value={reason}
                onChange={(event) => setReason(event.target.value)}
              />
            </div>
            <div className="flex flex-wrap justify-end gap-2">
              {canManage && ['Draft', 'Rejected'].includes(selected.status) && (
                <Button
                  onClick={() => void action('submitSubcontract')}
                  disabled={busy}
                >
                  <Send className="mr-2 h-4 w-4" />
                  Submit subcontract
                </Button>
              )}
              {canApprove && selected.status === 'PendingApproval' && (
                <>
                  <Button onClick={() => void action('approveSubcontract')}>
                    <CheckCircle2 className="mr-2 h-4 w-4" />
                    Approve subcontract
                  </Button>
                  <Button
                    variant="destructive"
                    onClick={() => void action('rejectSubcontract')}
                  >
                    <XCircle className="mr-2 h-4 w-4" />
                    Reject
                  </Button>
                </>
              )}
              {valuation &&
                (external || canCertify) &&
                ['Draft', 'Rejected'].includes(valuation.status) && (
                  <Button onClick={() => void action('submitValuation')}>
                    <Send className="mr-2 h-4 w-4" />
                    Submit valuation
                  </Button>
                )}
              {canCertify && valuation?.status === 'Submitted' && (
                <Button onClick={() => void action('assess')}>
                  Assess and route approval
                </Button>
              )}
              {canApprove && valuation?.status === 'PendingApproval' && (
                <>
                  <Button onClick={() => void action('approveValuation')}>
                    <CheckCircle2 className="mr-2 h-4 w-4" />
                    Approve certificate
                  </Button>
                  <Button
                    variant="destructive"
                    onClick={() => void action('rejectValuation')}
                  >
                    <XCircle className="mr-2 h-4 w-4" />
                    Reject
                  </Button>
                </>
              )}
              {canCertify &&
                valuation?.status === 'Approved' &&
                !valuation.vendorInvoiceId && (
                  <Button onClick={() => void action('handoff')}>
                    Handoff to Finance AP
                  </Button>
                )}
              {canCertify && valuation?.vendorInvoiceId && (
                <Button
                  variant="outline"
                  onClick={() =>
                    void run(
                      `refresh:${valuation.id}`,
                      () => service.refreshPayment(valuation.id),
                      'Payment status refreshed.',
                      valuation.id
                    )
                  }
                >
                  Refresh payment
                </Button>
              )}
              {canManage &&
                selected.status === 'Approved' &&
                selected.valuations.some(
                  (item) =>
                    item.isFinal && ['Approved', 'Paid'].includes(item.status)
                ) && (
                  <Button onClick={() => void action('close')}>
                    Close final settlement
                  </Button>
                )}
            </div>
          </div>
        )}
        {!loading && !workspace.subcontracts.length && (
          <p className="text-sm text-muted-foreground">
            No governed subcontract is available for this project. Internal
            users must first select an active Works contract whose QS commercial
            terms permit subcontracting.
          </p>
        )}
      </CardContent>
    </Card>
  );
}
