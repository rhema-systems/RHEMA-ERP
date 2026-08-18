'use client';

import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Download, FileUp, RefreshCw, Send } from 'lucide-react';
import { toast } from 'sonner';
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
import { Textarea } from '@/components/ui/textarea';
import { useAuth } from '@/hooks/use-auth';
import {
  quantitySurveySubcontractChargeService as service,
  type SubcontractCharge,
} from '@/services/quantity-survey-subcontract-charge.service';

type Props = {
  projectId: string;
  subcontractId: string;
  currency: string;
  external?: boolean;
  valuationStatus?: string;
  selectedChargeIds: string[];
  onSelectionChange: (ids: string[]) => void;
  onChargesChange?: (charges: SubcontractCharge[]) => void;
};

const today = () => new Date().toISOString().slice(0, 10);
const inFourteenDays = () => {
  const value = new Date();
  value.setUTCDate(value.getUTCDate() + 14);
  return value.toISOString().slice(0, 10);
};
const money = (value: number, currency: string) =>
  new Intl.NumberFormat(undefined, { style: 'currency', currency }).format(
    value || 0
  );
const label = (value: string) => value.replace(/([a-z])([A-Z])/g, '$1 $2');

export function QuantitySurveySubcontractChargesPanel({
  projectId,
  subcontractId,
  currency,
  external = false,
  valuationStatus,
  selectedChargeIds,
  onSelectionChange,
  onChargesChange,
}: Props) {
  const { hasPermission } = useAuth();
  const canManage =
    !external && hasPermission('quantity-survey.certificates.manage');
  const canApprove =
    !external && hasPermission('quantity-survey.transactions.approve');
  const requests = useRef<Record<string, string>>({});
  const [charges, setCharges] = useState<SubcontractCharge[]>([]);
  const [selectedId, setSelectedId] = useState('');
  const [busy, setBusy] = useState(false);
  const [chargeType, setChargeType] = useState<'BackCharge' | 'ContraCharge'>(
    'BackCharge'
  );
  const [title, setTitle] = useState('');
  const [chargeReason, setChargeReason] = useState('');
  const [noticeDate, setNoticeDate] = useState(today());
  const [responseDueDate, setResponseDueDate] = useState(inFourteenDays());
  const [amount, setAmount] = useState(0);
  const [actionReason, setActionReason] = useState('');
  const [approvedAmount, setApprovedAmount] = useState(0);
  const [responseStatus, setResponseStatus] = useState<'Accepted' | 'Disputed'>(
    'Accepted'
  );
  const [evidenceTitle, setEvidenceTitle] = useState('');
  const [file, setFile] = useState<File | null>(null);
  const selected = useMemo(
    () => charges.find((item) => item.id === selectedId),
    [charges, selectedId]
  );
  const allocatable = charges.filter(
    (item) => item.status === 'Approved' && !item.appliedValuationId
  );
  const requestId = (key: string) =>
    requests.current[key] ?? (requests.current[key] = crypto.randomUUID());

  const load = useCallback(
    async (preferred?: string) => {
      try {
        const values = await service.list(projectId, subcontractId, external);
        setCharges(values);
        onChargesChange?.(values);
        const next =
          preferred && values.some((item) => item.id === preferred)
            ? preferred
            : (values[0]?.id ?? '');
        setSelectedId(next);
      } catch (error) {
        toast.error(
          error instanceof Error
            ? error.message
            : 'Charge notices could not be loaded.'
        );
      }
    },
    [external, onChargesChange, projectId, subcontractId]
  );

  useEffect(() => {
    void load();
  }, [load]);
  useEffect(() => {
    if (!selected) return;
    setChargeType(selected.chargeType);
    setTitle(selected.title);
    setChargeReason(selected.reason);
    setNoticeDate(selected.noticeDate.slice(0, 10));
    setResponseDueDate(selected.responseDueDate.slice(0, 10));
    setAmount(selected.proposedAmount);
    setApprovedAmount(selected.approvedAmount ?? selected.proposedAmount);
  }, [selected]);

  const run = async (
    key: string,
    operation: () => Promise<unknown>,
    message: string
  ) => {
    setBusy(true);
    try {
      await operation();
      delete requests.current[key];
      setActionReason('');
      toast.success(message);
      await load(selectedId);
    } catch (error) {
      toast.error(
        error instanceof Error
          ? error.message
          : 'The charge-notice action failed.'
      );
    } finally {
      setBusy(false);
    }
  };

  const actionRequest = (key: string) => ({
    clientRequestId: requestId(key),
    rowVersion: selected?.rowVersion ?? '',
    reason: actionReason.trim(),
  });

  const save = async () => {
    if (
      !canManage ||
      title.trim().length < 3 ||
      chargeReason.trim().length < 10 ||
      amount <= 0
    ) {
      toast.error('Enter a title, detailed charge reason and positive amount.');
      return;
    }
    const key = `save:${selected?.id ?? 'new'}:${chargeType}:${title}:${chargeReason}:${noticeDate}:${responseDueDate}:${amount}`;
    await run(
      key,
      () =>
        service.save(subcontractId, {
          id:
            selected && ['Draft', 'Rejected'].includes(selected.status)
              ? selected.id
              : null,
          clientRequestId: requestId(key),
          chargeType,
          title: title.trim(),
          reason: chargeReason.trim(),
          noticeDate: `${noticeDate}T00:00:00Z`,
          responseDueDate: `${responseDueDate}T00:00:00Z`,
          proposedAmount: amount,
          rowVersion: selected?.rowVersion ?? null,
        }),
      'Charge notice Draft saved.'
    );
  };

  const upload = async () => {
    if (!selected || !file || evidenceTitle.trim().length < 3) {
      toast.error('Select a charge, evidence title and evidence file.');
      return;
    }
    const key = `evidence:${selected.id}:${file.name}:${file.size}`;
    await run(
      key,
      () =>
        service.uploadEvidence(
          projectId,
          subcontractId,
          selected.id,
          requestId(key),
          evidenceTitle.trim(),
          file,
          external
        ),
      'Evidence retained in the central DMS.'
    );
    setEvidenceTitle('');
    setFile(null);
  };

  const act = async (
    kind: 'issue' | 'submit' | 'approve' | 'reject' | 'respond'
  ) => {
    if (!selected || actionReason.trim().length < 5) {
      toast.error(
        'Select a charge notice and enter an action reason of at least 5 characters.'
      );
      return;
    }
    const key = `${kind}:${selected.id}:${selected.rowVersion}:${actionReason}:${approvedAmount}:${responseStatus}`;
    const request = actionRequest(key);
    const operations = {
      issue: () => service.issue(selected.id, request),
      submit: () => service.submit(selected.id, request),
      approve: () =>
        service.decide(selected.id, true, { ...request, approvedAmount }),
      reject: () =>
        service.decide(selected.id, false, { ...request, approvedAmount: 0 }),
      respond: () =>
        service.respond(projectId, subcontractId, selected.id, {
          ...request,
          responseStatus,
        }),
    };
    await run(key, operations[kind], `${label(kind)} charge notice completed.`);
  };

  const download = async (evidenceId: string, fileName: string) => {
    if (!selected) return;
    try {
      const blob = await service.evidenceContent(
        projectId,
        subcontractId,
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

  return (
    <Card>
      <CardHeader className="pb-3">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <CardTitle className="text-base">
            Back charges and contra charges
          </CardTitle>
          <Button
            size="sm"
            variant="outline"
            onClick={() => void load(selectedId)}
            disabled={busy}
          >
            <RefreshCw className="mr-2 h-4 w-4" /> Refresh
          </Button>
        </div>
      </CardHeader>
      <CardContent className="space-y-4">
        <div className="flex flex-wrap gap-2">
          {charges.map((item) => (
            <Button
              key={item.id}
              size="sm"
              variant={item.id === selectedId ? 'default' : 'outline'}
              onClick={() => setSelectedId(item.id)}
            >
              {item.noticeNumber} · {label(item.chargeType)} ·{' '}
              {label(item.status)}
            </Button>
          ))}
          {canManage && (
            <Button
              size="sm"
              variant={!selectedId ? 'default' : 'outline'}
              onClick={() => {
                setSelectedId('');
                setTitle('');
                setChargeReason('');
                setAmount(0);
                setNoticeDate(today());
                setResponseDueDate(inFourteenDays());
              }}
            >
              New notice
            </Button>
          )}
        </div>

        {canManage &&
          (!selected || ['Draft', 'Rejected'].includes(selected.status)) && (
            <div className="grid gap-3 md:grid-cols-6">
              <div>
                <Label>Charge type</Label>
                <Select
                  value={chargeType}
                  onValueChange={(value) =>
                    setChargeType(value as 'BackCharge' | 'ContraCharge')
                  }
                >
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="BackCharge">Back charge</SelectItem>
                    <SelectItem value="ContraCharge">Contra charge</SelectItem>
                  </SelectContent>
                </Select>
              </div>
              <div className="md:col-span-2">
                <Label>Title</Label>
                <Input
                  value={title}
                  onChange={(event) => setTitle(event.target.value)}
                />
              </div>
              <div>
                <Label>Notice date</Label>
                <Input
                  type="date"
                  value={noticeDate}
                  onChange={(event) => setNoticeDate(event.target.value)}
                />
              </div>
              <div>
                <Label>Response due</Label>
                <Input
                  type="date"
                  value={responseDueDate}
                  onChange={(event) => setResponseDueDate(event.target.value)}
                />
              </div>
              <div>
                <Label>Amount ({currency})</Label>
                <Input
                  type="number"
                  min="0.01"
                  value={amount || ''}
                  onChange={(event) => setAmount(Number(event.target.value))}
                />
              </div>
              <div className="md:col-span-5">
                <Label>Charge reason</Label>
                <Textarea
                  value={chargeReason}
                  onChange={(event) => setChargeReason(event.target.value)}
                />
              </div>
              <div className="flex items-end">
                <Button onClick={() => void save()} disabled={busy}>
                  Save Draft
                </Button>
              </div>
            </div>
          )}

        {selected && (
          <div className="space-y-3 rounded-md border p-3">
            <div className="flex flex-wrap items-center gap-2">
              <strong>{selected.title}</strong>
              <Badge variant="outline">{label(selected.status)}</Badge>
              <span>
                {money(
                  selected.approvedAmount ?? selected.proposedAmount,
                  selected.currency
                )}
              </span>
              <span>Response: {label(selected.responseStatus)}</span>
            </div>
            <p className="text-sm text-muted-foreground">{selected.reason}</p>
            {selected.evidence.map((item) => (
              <div
                key={item.id}
                className="flex items-center justify-between rounded border px-3 py-2 text-sm"
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
            {((external && selected.status === 'Issued') ||
              (canManage &&
                ['Draft', 'Rejected'].includes(selected.status))) && (
              <div className="grid gap-2 md:grid-cols-4">
                <Input
                  placeholder="Evidence title"
                  value={evidenceTitle}
                  onChange={(event) => setEvidenceTitle(event.target.value)}
                />
                <Input
                  type="file"
                  onChange={(event) => setFile(event.target.files?.[0] ?? null)}
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
            <div className="grid gap-2 md:grid-cols-4">
              {external && selected.status === 'Issued' && (
                <div>
                  <Label>Response</Label>
                  <Select
                    value={responseStatus}
                    onValueChange={(value) =>
                      setResponseStatus(value as 'Accepted' | 'Disputed')
                    }
                  >
                    <SelectTrigger>
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="Accepted">Accept</SelectItem>
                      <SelectItem value="Disputed">Dispute</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
              )}
              {canApprove && selected.status === 'PendingApproval' && (
                <div>
                  <Label>Approved amount</Label>
                  <Input
                    type="number"
                    min="0.01"
                    max={selected.proposedAmount}
                    value={approvedAmount || ''}
                    onChange={(event) =>
                      setApprovedAmount(Number(event.target.value))
                    }
                  />
                </div>
              )}
              <div className="md:col-span-2">
                <Label>Action reason / response note</Label>
                <Input
                  value={actionReason}
                  onChange={(event) => setActionReason(event.target.value)}
                />
              </div>
              <div className="flex flex-wrap items-end gap-2">
                {canManage && selected.status === 'Draft' && (
                  <Button onClick={() => void act('issue')}>
                    <Send className="mr-2 h-4 w-4" />
                    Issue
                  </Button>
                )}
                {canManage &&
                  ['Issued', 'Responded'].includes(selected.status) && (
                    <Button onClick={() => void act('submit')}>
                      Submit approval
                    </Button>
                  )}
                {canApprove && selected.status === 'PendingApproval' && (
                  <>
                    <Button onClick={() => void act('approve')}>Approve</Button>
                    <Button
                      variant="destructive"
                      onClick={() => void act('reject')}
                    >
                      Reject
                    </Button>
                  </>
                )}
                {external && selected.status === 'Issued' && (
                  <Button onClick={() => void act('respond')}>
                    Send response
                  </Button>
                )}
                {!external &&
                  !['Draft', 'PendingApproval'].includes(selected.status) && (
                    <Button
                      variant="outline"
                      onClick={() =>
                        void run(
                          `retry:${selected.id}`,
                          () => service.retryCommunication(selected.id),
                          'Communication requested again.'
                        )
                      }
                    >
                      Retry communication
                    </Button>
                  )}
              </div>
            </div>
          </div>
        )}

        {!external &&
          valuationStatus === 'Submitted' &&
          allocatable.length > 0 && (
            <div className="space-y-2 rounded-md border p-3">
              <Label>Approved charges to deduct in this valuation</Label>
              {allocatable.map((item) => (
                <label
                  key={item.id}
                  className="flex items-center gap-2 text-sm"
                >
                  <Checkbox
                    checked={selectedChargeIds.includes(item.id)}
                    onCheckedChange={(checked) =>
                      onSelectionChange(
                        checked
                          ? [...selectedChargeIds, item.id]
                          : selectedChargeIds.filter((id) => id !== item.id)
                      )
                    }
                  />
                  <span>
                    {item.noticeNumber} · {label(item.chargeType)} ·{' '}
                    {money(item.approvedAmount ?? 0, item.currency)}
                  </span>
                </label>
              ))}
            </div>
          )}
      </CardContent>
    </Card>
  );
}
