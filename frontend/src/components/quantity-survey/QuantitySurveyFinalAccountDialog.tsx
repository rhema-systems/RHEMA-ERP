'use client';

import { useCallback, useMemo, useRef, useState } from 'react';
import {
  CheckCircle2,
  FileCheck2,
  History,
  LockKeyhole,
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
  quantitySurveyFinalAccountService as service,
  type FinalAccount,
  type FinalAccountRevision,
  type FinalAccountWorkspace,
} from '@/services/quantity-survey-final-account.service';

type Props = { projectId: string };
const today = () => new Date().toISOString().slice(0, 10);
const label = (value: string) =>
  value.replace(/([a-z])([A-Z])/g, '$1 $2').replace(/_/g, ' ');
const money = (value: number, currency: string) =>
  new Intl.NumberFormat(undefined, {
    style: 'currency',
    currency: currency || 'GHS',
  }).format(value || 0);

export function QuantitySurveyFinalAccountDialog({ projectId }: Props) {
  const { hasPermission } = useAuth();
  const canRead = hasPermission('quantity-survey.workspace.read');
  const canManage = hasPermission('quantity-survey.final-accounts.manage');
  const canApprove = hasPermission('quantity-survey.transactions.approve');
  const canAudit = hasPermission('quantity-survey.audit.read');
  const requests = useRef<Record<string, string>>({});
  const [open, setOpen] = useState(false);
  const [loading, setLoading] = useState(false);
  const [working, setWorking] = useState(false);
  const [workspace, setWorkspace] = useState<FinalAccountWorkspace>({
    contracts: [],
  });
  const [contractId, setContractId] = useState('');
  const [settlementDate, setSettlementDate] = useState(today());
  const [notes, setNotes] = useState('');
  const [reason, setReason] = useState('');
  const [history, setHistory] = useState<FinalAccountRevision[]>([]);
  const current = workspace.finalAccount || null;
  const contract = useMemo(
    () => workspace.contracts.find((value) => value.id === contractId),
    [contractId, workspace.contracts]
  );

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const value = await service.workspace(projectId);
      setWorkspace(value);
      setContractId(
        value.finalAccount?.contractId || value.contracts[0]?.id || ''
      );
      setSettlementDate(
        value.finalAccount?.settlementDate?.slice(0, 10) || today()
      );
      setNotes(value.finalAccount?.notes || '');
      setHistory([]);
    } catch (error) {
      toast.error(
        error instanceof Error
          ? error.message
          : 'Failed to load the final-account workspace'
      );
    } finally {
      setLoading(false);
    }
  }, [projectId]);

  const requestId = (key: string) =>
    (requests.current[key] ||= crypto.randomUUID());
  const requireReason = () => {
    if (reason.trim().length < 5) {
      toast.error('Enter a clear reason of at least 5 characters.');
      return false;
    }
    return true;
  };
  const run = async (action: () => Promise<FinalAccount>, message: string) => {
    setWorking(true);
    try {
      await action();
      requests.current = {};
      setReason('');
      await load();
      toast.success(message);
    } catch (error) {
      toast.error(
        error instanceof Error ? error.message : 'Final-account action failed'
      );
    } finally {
      setWorking(false);
    }
  };
  const prepare = () => {
    if (!contractId) return toast.error('Select a controlled Works contract.');
    if (!requireReason()) return;
    const key = `prepare:${contractId}:${settlementDate}:${reason.trim()}`;
    return run(
      () =>
        service.prepare(projectId, {
          clientRequestId: requestId(key),
          contractId,
          settlementDate,
          rowVersion: current?.rowVersion,
          reason: reason.trim(),
          notes: notes.trim() || undefined,
        }),
      current ? 'Final account refreshed' : 'Final account prepared'
    );
  };
  const lifecycle = (
    action: 'submit' | 'approve' | 'reject' | 'close',
    message: string
  ) => {
    if (!current || !requireReason()) return;
    const key = `${action}:${current.id}:${current.rowVersion}:${reason.trim()}`;
    return run(
      () =>
        service[action](current.id, {
          clientRequestId: requestId(key),
          rowVersion: current.rowVersion,
          reason: reason.trim(),
        }),
      message
    );
  };
  const showHistory = async () => {
    if (!current) return;
    try {
      setHistory(await service.history(current.id));
    } catch (error) {
      toast.error(error instanceof Error ? error.message : 'History failed');
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
        <Button variant="outline">
          <FileCheck2 className="mr-2 h-4 w-4" /> Final account workspace
        </Button>
      </DialogTrigger>
      <DialogContent className="max-h-[92vh] max-w-6xl overflow-y-auto">
        <DialogHeader>
          <DialogTitle>Final account reconciliation</DialogTitle>
        </DialogHeader>
        <div className="flex flex-wrap items-center justify-between gap-2 rounded-lg border p-3">
          <div>
            <div className="font-medium">Governed close process</div>
            <div className="text-sm text-muted-foreground">
              Approved BoQ, contract changes, certificates, deductions,
              retention and Finance payments reconcile before closure.
            </div>
          </div>
          <Button
            variant="outline"
            size="sm"
            onClick={() => void load()}
            disabled={loading}
          >
            <RefreshCw className="mr-2 h-4 w-4" /> Refresh
          </Button>
        </div>

        {canManage &&
        (!current || ['Draft', 'Rejected'].includes(current.status)) ? (
          <div className="grid gap-3 rounded-lg border p-4 md:grid-cols-4">
            <div className="grid gap-2 md:col-span-2">
              <Label>Works contract</Label>
              <Select value={contractId} onValueChange={setContractId}>
                <SelectTrigger>
                  <SelectValue placeholder="Select contract" />
                </SelectTrigger>
                <SelectContent>
                  {workspace.contracts.map((value) => (
                    <SelectItem key={value.id} value={value.id}>
                      {value.contractNumber} · {value.contractor} ·{' '}
                      {money(value.contractValue, value.currency)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="grid gap-2">
              <Label>Settlement date</Label>
              <Input
                type="date"
                value={settlementDate}
                onChange={(event) => setSettlementDate(event.target.value)}
              />
            </div>
            <div className="grid gap-2">
              <Label>Contract currency</Label>
              <Input
                value={contract?.currency || current?.currency || ''}
                readOnly
              />
            </div>
            <div className="grid gap-2 md:col-span-2">
              <Label>Notes</Label>
              <Textarea
                rows={2}
                value={notes}
                onChange={(event) => setNotes(event.target.value)}
              />
            </div>
            <div className="grid gap-2 md:col-span-2">
              <Label>Reason</Label>
              <Textarea
                rows={2}
                value={reason}
                onChange={(event) => setReason(event.target.value)}
              />
            </div>
            <div className="md:col-span-4 flex justify-end">
              <Button onClick={prepare} disabled={working || !contractId}>
                <RefreshCw className="mr-2 h-4 w-4" />{' '}
                {current ? 'Refresh draft' : 'Prepare final account'}
              </Button>
            </div>
          </div>
        ) : null}

        {current ? (
          <div className="space-y-4">
            <div className="flex flex-wrap items-center gap-2">
              <Badge>{label(current.status)}</Badge>
              <span className="text-sm text-muted-foreground">
                {current.contractNumber} · {current.contractor} · Approved BoQ v
                {current.approvedBoqVersionNumber ?? '—'}
              </span>
            </div>
            <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-5">
              {[
                ['Gross account', current.grossFinalAccountValue],
                ['Deductions', current.totalDeductionAmount],
                ['Net final account', current.finalAccountValue],
                ['Finance paid', current.paidToDateAmount],
                ['Final payment', current.finalPaymentAmount],
                ['Certified to date', current.certifiedToDate],
                ['Retention outstanding', current.retentionOutstandingAmount],
                ['Approved variations', current.approvedVariationAmount],
                ['Approved claims', current.approvedClaimAmount],
                ['Approved escalation', current.approvedEscalationAmount],
              ].map(([title, amount]) => (
                <div key={String(title)} className="rounded-lg border p-3">
                  <div className="text-xs text-muted-foreground">{title}</div>
                  <div className="font-semibold">
                    {money(Number(amount), current.currency)}
                  </div>
                </div>
              ))}
            </div>
            <div className="overflow-x-auto rounded-lg border">
              <table className="w-full text-sm">
                <thead className="bg-muted/50 text-left text-xs uppercase text-muted-foreground">
                  <tr>
                    <th className="p-2">Source</th>
                    <th className="p-2">Reference</th>
                    <th className="p-2">Effect</th>
                    <th className="p-2 text-right">Amount</th>
                    <th className="p-2">Status</th>
                  </tr>
                </thead>
                <tbody>
                  {current.lines.map((line, index) => (
                    <tr
                      key={`${line.category}:${line.sourceId || index}`}
                      className="border-t"
                    >
                      <td className="p-2">
                        <div className="font-medium">{line.label}</div>
                        <div className="text-xs text-muted-foreground">
                          {label(line.category)}
                        </div>
                      </td>
                      <td className="p-2">{line.sourceReference || '—'}</td>
                      <td className="p-2">{line.effect}</td>
                      <td className="p-2 text-right">
                        {money(line.amount, current.currency)}
                      </td>
                      <td className="p-2">{label(line.status)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            {current.closureBlockers.length ? (
              <div className="rounded-lg border border-amber-300 bg-amber-50 p-3 text-sm text-amber-900 dark:bg-amber-950/30 dark:text-amber-100">
                <div className="font-medium">Closure blockers</div>
                <ul className="mt-1 list-disc pl-5">
                  {current.closureBlockers.map((value) => (
                    <li key={value}>{value}</li>
                  ))}
                </ul>
              </div>
            ) : null}
            <div className="grid gap-2">
              <Label>Lifecycle reason</Label>
              <Textarea
                rows={2}
                value={reason}
                onChange={(event) => setReason(event.target.value)}
              />
            </div>
            <div className="flex flex-wrap justify-end gap-2">
              {canAudit ? (
                <Button variant="outline" onClick={() => void showHistory()}>
                  <History className="mr-2 h-4 w-4" /> History
                </Button>
              ) : null}
              {canManage && current.status === 'Draft' ? (
                <Button
                  onClick={() =>
                    void lifecycle('submit', 'Final account submitted')
                  }
                  disabled={working}
                >
                  <Send className="mr-2 h-4 w-4" /> Submit
                </Button>
              ) : null}
              {canApprove && current.status === 'PendingApproval' ? (
                <>
                  <Button
                    variant="outline"
                    onClick={() =>
                      void lifecycle('reject', 'Final account rejected')
                    }
                    disabled={working}
                  >
                    <XCircle className="mr-2 h-4 w-4" /> Reject
                  </Button>
                  <Button
                    onClick={() =>
                      void lifecycle('approve', 'Final account approved')
                    }
                    disabled={working}
                  >
                    <CheckCircle2 className="mr-2 h-4 w-4" /> Approve
                  </Button>
                </>
              ) : null}
              {canApprove && current.status === 'Approved' ? (
                <Button
                  onClick={() =>
                    void lifecycle('close', 'Final account closed')
                  }
                  disabled={working || !current.canClose}
                >
                  <LockKeyhole className="mr-2 h-4 w-4" /> Close
                </Button>
              ) : null}
            </div>
            {history.length ? (
              <div className="space-y-2 rounded-lg border p-3">
                {history.map((item) => (
                  <div
                    key={item.id}
                    className="border-b pb-2 text-sm last:border-0"
                  >
                    <div className="font-medium">
                      {label(item.action)} · {item.actorName}
                    </div>
                    <div className="text-xs text-muted-foreground">
                      {new Date(item.createdAt).toLocaleString()} ·{' '}
                      {item.correlationId}
                    </div>
                    {item.reason ? <div>{item.reason}</div> : null}
                  </div>
                ))}
              </div>
            ) : null}
          </div>
        ) : loading ? (
          <div className="p-8 text-center text-sm text-muted-foreground">
            Loading final account…
          </div>
        ) : (
          <div className="rounded-lg border border-dashed p-8 text-center text-sm text-muted-foreground">
            No governed final account has been prepared.
          </div>
        )}
      </DialogContent>
    </Dialog>
  );
}
