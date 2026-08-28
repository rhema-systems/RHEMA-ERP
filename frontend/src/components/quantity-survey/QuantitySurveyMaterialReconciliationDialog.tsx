'use client';

import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import {
  CheckCircle2,
  ClipboardCheck,
  Plus,
  RefreshCw,
  Save,
  Send,
  Trash2,
  XCircle,
} from 'lucide-react';
import { toast } from 'sonner';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
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
import { Textarea } from '@/components/ui/textarea';
import { useAuth } from '@/hooks/use-auth';
import {
  quantitySurveyMaterialReconciliationService as service,
  type MaterialLineType,
  type MaterialReconciliation,
  type MaterialReconciliationWorkspace,
  type SaveMaterialReconciliationLine,
} from '@/services/quantity-survey-material-reconciliation.service';

type Props = { projectId: string };
type DraftLine = SaveMaterialReconciliationLine & { key: string };
const emptyWorkspace: MaterialReconciliationWorkspace = {
  valuations: [],
  materialSources: [],
  inventoryIssues: [],
  evidence: [],
  reconciliations: [],
};
const newLine = (): DraftLine => ({
  key: crypto.randomUUID(),
  lineType: 'materialOnSite',
  inventoryItemId: '',
  quantity: 0,
  deliveredUnitCost: null,
  approvedRateId: null,
  inventoryIssueVoucherLineId: null,
  valuationEvidenceId: null,
});
const money = (value: number, currency = 'GHS') =>
  new Intl.NumberFormat(undefined, {
    style: 'currency',
    currency,
    maximumFractionDigits: 2,
  }).format(value || 0);

export function QuantitySurveyMaterialReconciliationDialog({
  projectId,
}: Props) {
  const { hasPermission } = useAuth();
  const canRead = hasPermission('quantity-survey.workspace.read');
  const canManage = hasPermission('quantity-survey.valuations.manage');
  const canApprove = hasPermission('quantity-survey.transactions.approve');
  const requests = useRef<Record<string, { fingerprint: string; id: string }>>(
    {}
  );
  const [open, setOpen] = useState(false);
  const [loading, setLoading] = useState(false);
  const [working, setWorking] = useState(false);
  const [workspace, setWorkspace] = useState(emptyWorkspace);
  const [selectedId, setSelectedId] = useState('');
  const [valuationId, setValuationId] = useState('');
  const [reason, setReason] = useState('');
  const [notes, setNotes] = useState('');
  const [lines, setLines] = useState<DraftLine[]>([newLine()]);
  const selected = useMemo(
    () => workspace.reconciliations.find((value) => value.id === selectedId),
    [selectedId, workspace.reconciliations]
  );
  const valuation = workspace.valuations.find(
    (value) => value.worksheetId === valuationId
  );

  const load = useCallback(
    async (preferredId?: string) => {
      setLoading(true);
      try {
        const value = await service.workspace(projectId);
        setWorkspace(value);
        const nextId =
          preferredId &&
          value.reconciliations.some((item) => item.id === preferredId)
            ? preferredId
            : value.reconciliations[0]?.id || '';
        setSelectedId(nextId);
        const current = value.reconciliations.find(
          (item) => item.id === nextId
        );
        if (current) apply(current);
        else setValuationId(value.valuations[0]?.worksheetId || '');
      } catch (error) {
        toast.error(
          error instanceof Error
            ? error.message
            : 'Failed to load material reconciliations'
        );
      } finally {
        setLoading(false);
      }
    },
    [projectId]
  );

  const apply = (value: MaterialReconciliation) => {
    setValuationId(value.valuationWorksheetId);
    setNotes(value.notes || '');
    setLines(
      value.lines.map((line) => ({
        key: line.id,
        lineType: line.lineType,
        inventoryItemId: line.inventoryItemId,
        quantity: line.quantity,
        deliveredUnitCost: line.deliveredUnitCost,
        approvedRateId: line.approvedRateId,
        inventoryIssueVoucherLineId: line.inventoryIssueVoucherLineId,
        valuationEvidenceId: line.valuationEvidenceId,
      }))
    );
  };
  const requestId = (action: string, payload: object) => {
    const fingerprint = JSON.stringify(payload);
    if (requests.current[action]?.fingerprint !== fingerprint)
      requests.current[action] = { fingerprint, id: crypto.randomUUID() };
    return requests.current[action].id;
  };
  const run = async (
    action: () => Promise<MaterialReconciliation>,
    message: string
  ) => {
    setWorking(true);
    try {
      const value = await action();
      requests.current = {};
      setReason('');
      await load(value.id);
      toast.success(message);
    } catch (error) {
      toast.error(
        error instanceof Error
          ? error.message
          : 'Material-reconciliation action failed'
      );
    } finally {
      setWorking(false);
    }
  };
  const save = () => {
    if (!valuationId) return toast.error('Select an interim valuation.');
    if (reason.trim().length < 5)
      return toast.error('Enter a reason of at least 5 characters.');
    if (lines.some((line) => !line.inventoryItemId || line.quantity <= 0))
      return toast.error(
        'Every line must have a controlled Inventory item and quantity.'
      );
    const payload = { valuationId, reason: reason.trim(), notes, lines };
    return run(
      () =>
        service.save(projectId, {
          clientRequestId: requestId('save', payload),
          valuationWorksheetId: valuationId,
          rowVersion: selected?.rowVersion || null,
          reason: reason.trim(),
          notes: notes || null,
          lines: lines.map(({ key: _key, ...line }) => line),
        }),
      'Material reconciliation saved as Draft'
    );
  };
  const lifecycle = (
    action: 'submit' | 'approve' | 'reject',
    message: string
  ) => {
    if (!selected) return;
    if (reason.trim().length < 5)
      return toast.error('Enter a reason of at least 5 characters.');
    const payload = {
      id: selected.id,
      rowVersion: selected.rowVersion,
      reason: reason.trim(),
    };
    return run(
      () =>
        service[action](selected.id, {
          clientRequestId: requestId(action, payload),
          rowVersion: selected.rowVersion,
          reason: reason.trim(),
        }),
      message
    );
  };
  const reset = () => {
    setSelectedId('');
    setValuationId(workspace.valuations[0]?.worksheetId || '');
    setReason('');
    setNotes('');
    setLines([newLine()]);
    requests.current = {};
  };

  if (!canRead) return null;
  return (
    <Dialog
      open={open}
      onOpenChange={(value) => {
        setOpen(value);
        if (value) void load();
      }}
    >
      <DialogTrigger asChild>
        <Button size="sm" variant="outline">
          <ClipboardCheck className="mr-2 h-4 w-4" />
          Material reconciliation
        </Button>
      </DialogTrigger>
      <DialogContent className="max-h-[94vh] max-w-7xl overflow-y-auto">
        <DialogHeader>
          <DialogTitle>Governed material reconciliation</DialogTitle>
        </DialogHeader>
        <div className="rounded-md border bg-muted/20 p-3 text-sm text-muted-foreground">
          Material items, approved rates, acknowledged Stores issues and
          central-DMS evidence are selected from their owning registers. The
          approved result supplies the certificate values.
        </div>
        <div className="grid gap-5 lg:grid-cols-[0.3fr,0.7fr]">
          <div className="space-y-3">
            <div className="flex items-center justify-between">
              <span className="font-medium">Register</span>
              <Button
                size="sm"
                variant="outline"
                disabled={loading}
                onClick={() => void load(selectedId)}
              >
                <RefreshCw className="mr-2 h-4 w-4" />
                Refresh
              </Button>
            </div>
            {workspace.reconciliations.map((value) => (
              <button
                key={value.id}
                type="button"
                className={`w-full rounded-md border p-3 text-left ${selectedId === value.id ? 'border-primary bg-primary/5' : 'hover:bg-muted/30'}`}
                onClick={() => {
                  setSelectedId(value.id);
                  apply(value);
                }}
              >
                <div className="flex justify-between gap-2">
                  <span className="font-medium">
                    {value.reconciliationNumber}
                  </span>
                  <Badge variant="outline">{value.status}</Badge>
                </div>
                <div className="mt-1 text-xs text-muted-foreground">
                  {value.contractorName} · Deduction{' '}
                  {money(value.tdcSuppliedDeductionAmount, value.currency)}
                </div>
              </button>
            ))}
            <Button size="sm" variant="ghost" onClick={reset}>
              <Plus className="mr-2 h-4 w-4" />
              Prepare another
            </Button>
          </div>
          <div className="space-y-4">
            <div className="grid gap-3 md:grid-cols-2">
              <div className="grid gap-2 md:col-span-2">
                <Label>Interim valuation</Label>
                <Select
                  disabled={Boolean(selected) || working}
                  value={valuationId}
                  onValueChange={setValuationId}
                >
                  <SelectTrigger>
                    <SelectValue placeholder="Select valuation and Works contract" />
                  </SelectTrigger>
                  <SelectContent>
                    {workspace.valuations.map((value) => (
                      <SelectItem
                        key={value.worksheetId}
                        value={value.worksheetId}
                      >
                        {value.label} · {value.contractNumber} ·{' '}
                        {value.contractorName}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              {selected ? (
                <>
                  <Metric
                    label="On-site materials"
                    value={money(
                      selected.materialOnSiteAmount,
                      selected.currency
                    )}
                  />
                  <Metric
                    label="Off-site materials"
                    value={money(
                      selected.materialOffSiteAmount,
                      selected.currency
                    )}
                  />
                  <Metric
                    label="TDC-supplied deduction"
                    value={money(
                      selected.tdcSuppliedDeductionAmount,
                      selected.currency
                    )}
                  />
                  <Metric
                    label="Valuation basis"
                    value={selected.valuationBasis}
                  />
                </>
              ) : null}
            </div>
            <div className="space-y-3">
              {lines.map((line, index) => (
                <MaterialLineEditor
                  key={line.key}
                  line={line}
                  index={index}
                  disabled={Boolean(
                    selected &&
                    selected.status !== 'Draft' &&
                    selected.status !== 'Rejected'
                  )}
                  workspace={workspace}
                  worksheetId={valuationId}
                  currency={valuation?.currency || selected?.currency || 'GHS'}
                  onChange={(next) =>
                    setLines((current) =>
                      current.map((value) =>
                        value.key === line.key ? next : value
                      )
                    )
                  }
                  onRemove={() =>
                    setLines((current) =>
                      current.filter((value) => value.key !== line.key)
                    )
                  }
                />
              ))}
              {!selected || ['Draft', 'Rejected'].includes(selected.status) ? (
                <Button
                  size="sm"
                  variant="outline"
                  onClick={() => setLines((current) => [...current, newLine()])}
                >
                  <Plus className="mr-2 h-4 w-4" />
                  Add material line
                </Button>
              ) : null}
            </div>
            <div className="grid gap-2">
              <Label>Notes</Label>
              <Textarea
                value={notes}
                disabled={Boolean(
                  selected && !['Draft', 'Rejected'].includes(selected.status)
                )}
                onChange={(event) => setNotes(event.target.value)}
              />
            </div>
            <div className="grid gap-2">
              <Label>Action reason</Label>
              <Textarea
                value={reason}
                onChange={(event) => setReason(event.target.value)}
                placeholder="Record why this reconciliation or decision is required"
              />
            </div>
            <div className="flex flex-wrap gap-2">
              {(!selected || ['Draft', 'Rejected'].includes(selected.status)) &&
              canManage ? (
                <Button disabled={working} onClick={save}>
                  <Save className="mr-2 h-4 w-4" />
                  Save Draft
                </Button>
              ) : null}
              {selected?.status === 'ContractorConfirmed' && canManage ? (
                <Button
                  disabled={working}
                  onClick={() =>
                    lifecycle('submit', 'Material reconciliation submitted')
                  }
                >
                  <Send className="mr-2 h-4 w-4" />
                  Submit
                </Button>
              ) : null}
              {selected?.status === 'PendingApproval' && canApprove ? (
                <>
                  <Button
                    disabled={working}
                    onClick={() =>
                      lifecycle('approve', 'Material reconciliation approved')
                    }
                  >
                    <CheckCircle2 className="mr-2 h-4 w-4" />
                    Approve
                  </Button>
                  <Button
                    variant="destructive"
                    disabled={working}
                    onClick={() =>
                      lifecycle('reject', 'Material reconciliation rejected')
                    }
                  >
                    <XCircle className="mr-2 h-4 w-4" />
                    Reject
                  </Button>
                </>
              ) : null}
              {selected?.status === 'Draft' ? (
                <span className="self-center text-sm text-muted-foreground">
                  Awaiting confirmation by {selected.contractorName} in the
                  external project portal.
                </span>
              ) : null}
            </div>
          </div>
        </div>
      </DialogContent>
    </Dialog>
  );
}

function MaterialLineEditor({
  line,
  index,
  workspace,
  worksheetId,
  currency,
  disabled,
  onChange,
  onRemove,
}: {
  line: DraftLine;
  index: number;
  workspace: MaterialReconciliationWorkspace;
  worksheetId: string;
  currency: string;
  disabled: boolean;
  onChange: (value: DraftLine) => void;
  onRemove: () => void;
}) {
  const issue = workspace.inventoryIssues.find(
    (value) => value.issueVoucherLineId === line.inventoryIssueVoucherLineId
  );
  const sources = workspace.materialSources.filter(
    (value) =>
      !currency || value.currency.toLowerCase() === currency.toLowerCase()
  );
  const selectedSource = sources.find(
    (value) => value.inventoryItemId === line.inventoryItemId
  );
  const setType = (lineType: MaterialLineType) =>
    onChange({ ...newLine(), key: line.key, lineType });
  return (
    <div className="grid gap-3 rounded-md border p-3 md:grid-cols-6">
      <div className="grid gap-2 md:col-span-2">
        <Label>Line {index + 1} type</Label>
        <Select
          disabled={disabled}
          value={line.lineType}
          onValueChange={(value) => setType(value as MaterialLineType)}
        >
          <SelectTrigger>
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="materialOnSite">Material on site</SelectItem>
            <SelectItem value="materialOffSite">Material off site</SelectItem>
            <SelectItem value="tdcSuppliedMaterial">
              TDC-supplied deduction
            </SelectItem>
          </SelectContent>
        </Select>
      </div>
      {line.lineType === 'tdcSuppliedMaterial' ? (
        <div className="grid gap-2 md:col-span-4">
          <Label>Acknowledged Stores issue</Label>
          <Select
            disabled={disabled}
            value={line.inventoryIssueVoucherLineId || ''}
            onValueChange={(id) => {
              const value = workspace.inventoryIssues.find(
                (item) => item.issueVoucherLineId === id
              );
              const rate = sources.find(
                (item) => item.inventoryItemId === value?.inventoryItemId
              );
              onChange({
                ...line,
                inventoryIssueVoucherLineId: id,
                inventoryItemId: value?.inventoryItemId || '',
                quantity: value?.quantity || 0,
                deliveredUnitCost: value?.unitCost || 0,
                approvedRateId: rate?.approvedRateId || null,
                valuationEvidenceId: null,
              });
            }}
          >
            <SelectTrigger>
              <SelectValue placeholder="Select acknowledged project issue" />
            </SelectTrigger>
            <SelectContent>
              {workspace.inventoryIssues.map((value) => (
                <SelectItem
                  key={value.issueVoucherLineId}
                  value={value.issueVoucherLineId}
                >
                  {value.voucherNumber} · {value.itemCode} · {value.quantity}{' '}
                  {value.unitOfMeasure} · {money(value.totalValue, currency)}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
      ) : (
        <>
          <div className="grid gap-2 md:col-span-2">
            <Label>Inventory material</Label>
            <Select
              disabled={disabled}
              value={line.inventoryItemId}
              onValueChange={(id) => {
                const value = sources.find(
                  (item) => item.inventoryItemId === id
                );
                onChange({
                  ...line,
                  inventoryItemId: id,
                  approvedRateId: value?.approvedRateId || null,
                });
              }}
            >
              <SelectTrigger>
                <SelectValue placeholder="Select item and approved rate" />
              </SelectTrigger>
              <SelectContent>
                {sources.map((value) => (
                  <SelectItem
                    key={`${value.inventoryItemId}-${value.currency}`}
                    value={value.inventoryItemId}
                  >
                    {value.itemCode} · {value.itemName} ·{' '}
                    {value.approvedUnitRate ?? 0} {value.currency}/
                    {value.unitOfMeasure}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="grid gap-2 md:col-span-2">
            <Label>Central-DMS evidence</Label>
            <Select
              disabled={disabled}
              value={line.valuationEvidenceId || ''}
              onValueChange={(id) =>
                onChange({ ...line, valuationEvidenceId: id })
              }
            >
              <SelectTrigger>
                <SelectValue placeholder="Select matching evidence" />
              </SelectTrigger>
              <SelectContent>
                {workspace.evidence
                  .filter(
                    (value) =>
                      value.worksheetId === worksheetId &&
                      value.evidenceType === line.lineType
                  )
                  .map((value) => (
                    <SelectItem key={value.evidenceId} value={value.evidenceId}>
                      {value.label} · {value.fileName}
                    </SelectItem>
                  ))}
              </SelectContent>
            </Select>
          </div>
        </>
      )}
      <div className="grid gap-2">
        <Label>Quantity</Label>
        <Input
          type="number"
          min="0.0001"
          step="0.0001"
          disabled={disabled || line.lineType === 'tdcSuppliedMaterial'}
          value={line.quantity}
          onChange={(event) =>
            onChange({
              ...line,
              quantity: Math.max(0, Number(event.target.value || 0)),
            })
          }
        />
      </div>
      <div className="grid gap-2">
        <Label>Delivered unit cost</Label>
        <Input
          type="number"
          min="0"
          step="0.000001"
          disabled={disabled || line.lineType === 'tdcSuppliedMaterial'}
          value={line.deliveredUnitCost ?? ''}
          onChange={(event) =>
            onChange({
              ...line,
              deliveredUnitCost:
                event.target.value === ''
                  ? null
                  : Math.max(0, Number(event.target.value)),
            })
          }
        />
      </div>
      <div className="flex items-end text-xs text-muted-foreground md:col-span-3">
        {issue
          ? `Frozen Stores issue: ${issue.integrityHash.slice(0, 12)}…`
          : selectedSource
            ? `Approved rate: ${selectedSource.approvedUnitRate ?? 0} ${selectedSource.currency}`
            : 'Select a governed source.'}
      </div>
      <div className="flex items-end justify-end md:col-span-3">
        <Button
          type="button"
          size="sm"
          variant="ghost"
          disabled={disabled}
          onClick={onRemove}
        >
          <Trash2 className="mr-2 h-4 w-4" />
          Remove
        </Button>
      </div>
    </div>
  );
}

function Metric({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-md border p-3">
      <div className="text-xs text-muted-foreground">{label}</div>
      <div className="font-medium">{value}</div>
    </div>
  );
}

export function QuantitySurveyMaterialContractorWorkspace({
  projectId,
}: Props) {
  const requests = useRef<Record<string, { fingerprint: string; id: string }>>(
    {}
  );
  const [workspace, setWorkspace] = useState(emptyWorkspace);
  const [loading, setLoading] = useState(false);
  const [working, setWorking] = useState(false);
  const [attestations, setAttestations] = useState<Record<string, string>>({});
  const [reasons, setReasons] = useState<Record<string, string>>({});
  const load = useCallback(async () => {
    setLoading(true);
    try {
      setWorkspace(await service.externalWorkspace(projectId));
    } catch (error) {
      toast.error(
        error instanceof Error
          ? error.message
          : 'Failed to load material reconciliations'
      );
    } finally {
      setLoading(false);
    }
  }, [projectId]);
  const confirm = async (value: MaterialReconciliation) => {
    const attestation = (attestations[value.id] || '').trim();
    const reason = (reasons[value.id] || '').trim();
    if (attestation.length < 10)
      return toast.error(
        'Enter a contractor attestation of at least 10 characters.'
      );
    if (reason.length < 5)
      return toast.error(
        'Enter a confirmation reason of at least 5 characters.'
      );
    const fingerprint = JSON.stringify({
      id: value.id,
      rowVersion: value.rowVersion,
      attestation,
      reason,
    });
    if (requests.current[value.id]?.fingerprint !== fingerprint)
      requests.current[value.id] = { fingerprint, id: crypto.randomUUID() };
    setWorking(true);
    try {
      await service.confirm(projectId, value.id, {
        clientRequestId: requests.current[value.id].id,
        rowVersion: value.rowVersion,
        reason,
        attestation,
      });
      delete requests.current[value.id];
      await load();
      toast.success('Material reconciliation confirmed');
    } catch (error) {
      toast.error(
        error instanceof Error ? error.message : 'Confirmation failed'
      );
    } finally {
      setWorking(false);
    }
  };
  useEffect(() => {
    void load();
  }, [load]);
  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <h3 className="font-semibold">Material reconciliation</h3>
          <p className="text-sm text-muted-foreground">
            Confirm measured materials, central-DMS evidence and TDC-issued
            deductions before internal approval.
          </p>
        </div>
        <Button
          size="sm"
          variant="outline"
          disabled={loading}
          onClick={() => void load()}
        >
          <RefreshCw className="mr-2 h-4 w-4" />
          Refresh
        </Button>
      </div>
      {workspace.reconciliations.length === 0 ? (
        <div className="rounded-md border border-dashed p-5 text-sm text-muted-foreground">
          No material reconciliation is assigned to your contractor account.
        </div>
      ) : (
        workspace.reconciliations.map((value) => (
          <div key={value.id} className="space-y-3 rounded-md border p-4">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <div>
                <div className="font-medium">
                  {value.reconciliationNumber} · {value.contractNumber}
                </div>
                <div className="text-sm text-muted-foreground">
                  {value.contractorName}
                </div>
              </div>
              <Badge variant="outline">{value.status}</Badge>
            </div>
            <div className="grid gap-3 sm:grid-cols-3">
              <Metric
                label="On-site materials"
                value={money(value.materialOnSiteAmount, value.currency)}
              />
              <Metric
                label="Off-site materials"
                value={money(value.materialOffSiteAmount, value.currency)}
              />
              <Metric
                label="TDC-supplied deduction"
                value={money(value.tdcSuppliedDeductionAmount, value.currency)}
              />
            </div>
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b text-left">
                    <th className="p-2">Material</th>
                    <th className="p-2">Source</th>
                    <th className="p-2 text-right">Quantity</th>
                    <th className="p-2 text-right">Applied rate</th>
                    <th className="p-2 text-right">Value</th>
                  </tr>
                </thead>
                <tbody>
                  {value.lines.map((line) => (
                    <tr key={line.id} className="border-b last:border-0">
                      <td className="p-2">
                        {line.itemCode} · {line.itemName}
                      </td>
                      <td className="p-2">
                        {line.issueVoucherNumber || line.lineType}
                      </td>
                      <td className="p-2 text-right">
                        {line.quantity} {line.unitOfMeasure}
                      </td>
                      <td className="p-2 text-right">
                        {money(line.appliedUnitRate, value.currency)}
                      </td>
                      <td className="p-2 text-right">
                        {money(line.totalValue, value.currency)}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            {value.status === 'Draft' ? (
              <div className="grid gap-3 md:grid-cols-2">
                <div className="grid gap-2">
                  <Label>Contractor attestation</Label>
                  <Textarea
                    value={attestations[value.id] || ''}
                    onChange={(event) =>
                      setAttestations((current) => ({
                        ...current,
                        [value.id]: event.target.value,
                      }))
                    }
                    placeholder="I confirm the quantities, evidence and deductions shown above."
                  />
                </div>
                <div className="grid gap-2">
                  <Label>Confirmation reason</Label>
                  <Textarea
                    value={reasons[value.id] || ''}
                    onChange={(event) =>
                      setReasons((current) => ({
                        ...current,
                        [value.id]: event.target.value,
                      }))
                    }
                    placeholder="Record the basis of confirmation"
                  />
                </div>
                <div className="md:col-span-2">
                  <Button
                    disabled={working}
                    onClick={() => void confirm(value)}
                  >
                    <CheckCircle2 className="mr-2 h-4 w-4" />
                    Confirm reconciliation
                  </Button>
                </div>
              </div>
            ) : null}
          </div>
        ))
      )}
    </div>
  );
}
