'use client';

import { useCallback, useMemo, useRef, useState } from 'react';
import {
  CheckCircle2,
  History,
  Landmark,
  RefreshCw,
  Send,
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
  quantitySurveyAdvanceRecoveryService as service,
  type AdvanceRecoveryAgreement,
  type AdvanceRecoveryRevision,
  type AdvanceRecoveryWorkspace,
} from '@/services/quantity-survey-advance-recovery.service';

type Props = { projectId: string };
const money = (value: number, currency: string) =>
  new Intl.NumberFormat(undefined, {
    style: 'currency',
    currency: currency || 'GHS',
  }).format(value || 0);

export function QuantitySurveyAdvanceRecoveryDialog({ projectId }: Props) {
  const { hasPermission } = useAuth();
  const canRead = hasPermission('quantity-survey.workspace.read');
  const canManage = hasPermission('quantity-survey.valuations.manage');
  const canApprove = hasPermission('quantity-survey.transactions.approve');
  const canAudit = hasPermission('quantity-survey.audit.read');
  const requests = useRef<Record<string, string>>({});
  const [open, setOpen] = useState(false);
  const [loading, setLoading] = useState(false);
  const [working, setWorking] = useState(false);
  const [workspace, setWorkspace] = useState<AdvanceRecoveryWorkspace>({
    eligibleAdvances: [],
    agreements: [],
  });
  const [selectedId, setSelectedId] = useState('');
  const [advanceKey, setAdvanceKey] = useState('');
  const [percentage, setPercentage] = useState(10);
  const [reason, setReason] = useState('');
  const [history, setHistory] = useState<AdvanceRecoveryRevision[]>([]);
  const selected = useMemo(
    () => workspace.agreements.find((value) => value.id === selectedId),
    [workspace, selectedId]
  );
  const selectedAdvance = useMemo(
    () =>
      workspace.eligibleAdvances.find(
        (value) => `${value.contractId}:${value.vendorPaymentId}` === advanceKey
      ),
    [workspace, advanceKey]
  );

  const load = useCallback(
    async (preferredId?: string) => {
      setLoading(true);
      try {
        const value = await service.workspace(projectId);
        setWorkspace(value);
        const id =
          preferredId &&
          value.agreements.some((item) => item.id === preferredId)
            ? preferredId
            : value.agreements[0]?.id || '';
        setSelectedId(id);
        setAdvanceKey(
          value.eligibleAdvances[0]
            ? `${value.eligibleAdvances[0].contractId}:${value.eligibleAdvances[0].vendorPaymentId}`
            : ''
        );
        setHistory([]);
      } catch (error) {
        toast.error(
          error instanceof Error
            ? error.message
            : 'Failed to load advance recovery workspace'
        );
      } finally {
        setLoading(false);
      }
    },
    [projectId]
  );

  const requestId = (key: string) =>
    (requests.current[key] ||= crypto.randomUUID());
  const run = async (
    action: () => Promise<AdvanceRecoveryAgreement>,
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
          : 'Advance recovery action failed'
      );
    } finally {
      setWorking(false);
    }
  };
  const requireReason = () => {
    if (reason.trim().length < 5) {
      toast.error('Enter a clear reason of at least 5 characters.');
      return false;
    }
    return true;
  };
  const create = () => {
    if (!selectedAdvance)
      return toast.error('Select a posted Finance supplier advance.');
    if (!requireReason()) return;
    const key = `create:${advanceKey}:${percentage}:${reason.trim()}`;
    return run(
      () =>
        service.create(projectId, {
          clientRequestId: requestId(key),
          contractId: selectedAdvance.contractId,
          vendorPaymentId: selectedAdvance.vendorPaymentId,
          recoveryPercentage: percentage,
          reason: reason.trim(),
        }),
      'Advance recovery agreement prepared'
    );
  };
  const lifecycle = (
    action: 'submit' | 'approve' | 'reject',
    message: string
  ) => {
    if (!selected || !requireReason()) return;
    const key = `${action}:${selected.id}:${selected.rowVersion}:${reason.trim()}`;
    return run(
      () =>
        service[action](selected.id, {
          clientRequestId: requestId(key),
          rowVersion: selected.rowVersion,
          reason: reason.trim(),
        }),
      message
    );
  };
  const showHistory = async () => {
    if (!selected) return;
    try {
      setHistory(await service.history(selected.id));
    } catch (error) {
      toast.error(
        error instanceof Error
          ? error.message
          : 'Failed to load recovery history'
      );
    }
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
          <Landmark className="mr-2 h-4 w-4" />
          Advance recovery
        </Button>
      </DialogTrigger>
      <DialogContent className="max-h-[92vh] max-w-6xl overflow-y-auto">
        <DialogHeader>
          <DialogTitle>Governed advance recovery</DialogTitle>
        </DialogHeader>
        <div className="rounded-md border bg-muted/20 p-3 text-sm text-muted-foreground">
          Select a posted Finance supplier advance. QS stores only contract
          recovery terms and derives certificate and Finance reconciliation; it
          does not duplicate or repost the payment.
        </div>
        <div className="grid gap-5 lg:grid-cols-[0.36fr,0.64fr]">
          <div className="space-y-3">
            <div className="flex items-center justify-between">
              <span className="font-medium">Recovery agreements</span>
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
            {workspace.agreements.map((value) => (
              <button
                key={value.id}
                type="button"
                onClick={() => {
                  setSelectedId(value.id);
                  setHistory([]);
                }}
                className={`w-full rounded-md border p-3 text-left ${selectedId === value.id ? 'border-primary bg-primary/5' : 'hover:bg-muted/30'}`}
              >
                <div className="flex justify-between gap-2">
                  <span className="font-medium">{value.recoveryNumber}</span>
                  <Badge variant="outline">{value.status}</Badge>
                </div>
                <div className="mt-1 text-sm text-muted-foreground">
                  {value.contractNumber} ·{' '}
                  {money(value.remainingRecoveryBalance, value.currency)}{' '}
                  remaining
                </div>
              </button>
            ))}
            {workspace.agreements.length === 0 ? (
              <div className="rounded-md border border-dashed p-4 text-sm text-muted-foreground">
                No governed recovery agreement yet.
              </div>
            ) : null}
          </div>
          <div className="space-y-4">
            {!selected ? (
              <div className="space-y-4 rounded-md border p-4">
                <div className="font-medium">
                  Prepare from a posted supplier advance
                </div>
                <div className="grid gap-2">
                  <Label>Finance supplier advance and Works contract</Label>
                  <Select value={advanceKey} onValueChange={setAdvanceKey}>
                    <SelectTrigger>
                      <SelectValue placeholder="Select posted supplier advance" />
                    </SelectTrigger>
                    <SelectContent>
                      {workspace.eligibleAdvances.map((value) => (
                        <SelectItem
                          key={`${value.contractId}:${value.vendorPaymentId}`}
                          value={`${value.contractId}:${value.vendorPaymentId}`}
                        >
                          {value.contractNumber} · {value.paymentNumber} ·{' '}
                          {money(value.financeAvailableAmount, value.currency)}{' '}
                          available
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <div className="grid gap-2">
                  <Label>Recovery percentage per certificate</Label>
                  <Input
                    type="number"
                    min="0.0001"
                    max="100"
                    step="0.0001"
                    value={percentage}
                    onChange={(event) =>
                      setPercentage(
                        Math.max(0, Number(event.target.value || 0))
                      )
                    }
                  />
                </div>
                <div className="grid gap-2">
                  <Label>Preparation reason</Label>
                  <Textarea
                    value={reason}
                    onChange={(event) => setReason(event.target.value)}
                  />
                </div>
                <Button
                  disabled={!canManage || working || !selectedAdvance}
                  onClick={create}
                >
                  Prepare agreement
                </Button>
              </div>
            ) : (
              <>
                <div className="flex flex-wrap justify-between gap-2">
                  <div>
                    <div className="text-lg font-semibold">
                      {selected.recoveryNumber}
                    </div>
                    <div className="text-sm text-muted-foreground">
                      {selected.contractNumber} · {selected.contractorName}
                    </div>
                  </div>
                  <Badge>{selected.status}</Badge>
                </div>
                <div className="grid gap-3 rounded-md border p-3 text-sm md:grid-cols-4">
                  <Metric
                    label="Posted advance"
                    value={money(
                      selected.originalAdvanceAmount,
                      selected.currency
                    )}
                  />
                  <Metric
                    label="Recovery rate"
                    value={`${selected.recoveryPercentage}%`}
                  />
                  <Metric
                    label="QS recovered"
                    value={money(
                      selected.approvedRecoveryAmount,
                      selected.currency
                    )}
                  />
                  <Metric
                    label="Remaining"
                    value={money(
                      selected.remainingRecoveryBalance,
                      selected.currency
                    )}
                  />
                  <Metric
                    label="Pending certificates"
                    value={money(
                      selected.pendingRecoveryAmount,
                      selected.currency
                    )}
                  />
                  <Metric
                    label="Finance applied to QS"
                    value={money(
                      selected.financeAppliedToQsCertificates,
                      selected.currency
                    )}
                  />
                  <Metric
                    label="Finance available"
                    value={money(
                      selected.financeAvailableAmount,
                      selected.currency
                    )}
                  />
                  <Metric
                    label="Reconciliation difference"
                    value={money(
                      selected.reconciliationDifference,
                      selected.currency
                    )}
                  />
                </div>
                <div className="overflow-x-auto rounded-md border">
                  <table className="w-full text-sm">
                    <thead className="bg-muted/40">
                      <tr>
                        <th className="p-2 text-left">Certificate</th>
                        <th className="p-2 text-right">Recovery</th>
                        <th className="p-2 text-right">Finance applied</th>
                        <th className="p-2 text-right">Balance</th>
                      </tr>
                    </thead>
                    <tbody>
                      {selected.ledger.length === 0 ? (
                        <tr>
                          <td
                            colSpan={4}
                            className="p-5 text-center text-muted-foreground"
                          >
                            No certificate recovery entries yet.
                          </td>
                        </tr>
                      ) : (
                        selected.ledger.map((entry) => (
                          <tr
                            key={entry.paymentCertificateId}
                            className="border-t"
                          >
                            <td className="p-2">
                              {entry.certificateNumber}
                              <div className="text-xs text-muted-foreground">
                                {entry.status} ·{' '}
                                {new Date(entry.issueDate).toLocaleDateString()}
                              </div>
                            </td>
                            <td className="p-2 text-right">
                              {money(entry.recoveryAmount, selected.currency)}
                            </td>
                            <td className="p-2 text-right">
                              {money(
                                entry.financeAppliedAmount,
                                selected.currency
                              )}
                            </td>
                            <td className="p-2 text-right">
                              {money(
                                entry.runningRecoveryBalance,
                                selected.currency
                              )}
                            </td>
                          </tr>
                        ))
                      )}
                    </tbody>
                  </table>
                </div>
                <div className="grid gap-2">
                  <Label>Action reason</Label>
                  <Textarea
                    value={reason}
                    onChange={(event) => setReason(event.target.value)}
                  />
                </div>
                <div className="flex flex-wrap gap-2">
                  {selected.status === 'Draft' && canManage ? (
                    <Button
                      disabled={working}
                      onClick={() =>
                        lifecycle('submit', 'Recovery agreement submitted')
                      }
                    >
                      <Send className="mr-2 h-4 w-4" />
                      Submit
                    </Button>
                  ) : null}
                  {selected.status === 'PendingApproval' && canApprove ? (
                    <>
                      <Button
                        disabled={working}
                        onClick={() =>
                          lifecycle('approve', 'Recovery agreement approved')
                        }
                      >
                        <CheckCircle2 className="mr-2 h-4 w-4" />
                        Approve
                      </Button>
                      <Button
                        variant="destructive"
                        disabled={working}
                        onClick={() =>
                          lifecycle('reject', 'Recovery agreement rejected')
                        }
                      >
                        <XCircle className="mr-2 h-4 w-4" />
                        Reject
                      </Button>
                    </>
                  ) : null}
                  {canAudit ? (
                    <Button variant="outline" onClick={showHistory}>
                      <History className="mr-2 h-4 w-4" />
                      History
                    </Button>
                  ) : null}
                  <Button
                    variant="ghost"
                    onClick={() => {
                      setSelectedId('');
                      setHistory([]);
                    }}
                  >
                    Prepare another
                  </Button>
                </div>
                {history.length ? (
                  <div className="space-y-2 rounded-md border p-3">
                    <div className="font-medium">Immutable history</div>
                    {history.map((value) => (
                      <div
                        key={value.id}
                        className="border-b py-2 text-sm last:border-0"
                      >
                        <div className="font-medium">{value.action}</div>
                        <div className="text-muted-foreground">
                          {value.actorName} ·{' '}
                          {new Date(value.createdAt).toLocaleString()} ·{' '}
                          {value.correlationId}
                        </div>
                      </div>
                    ))}
                  </div>
                ) : null}
              </>
            )}
          </div>
        </div>
      </DialogContent>
    </Dialog>
  );
}

function Metric({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <div className="text-xs text-muted-foreground">{label}</div>
      <div className="font-medium">{value}</div>
    </div>
  );
}
