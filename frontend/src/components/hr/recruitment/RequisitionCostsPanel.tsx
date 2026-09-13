'use client';

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { CheckCircle2, Loader2, Pencil, Plus, Trash2, Wallet, XCircle } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Dialog,
  DialogContent,
  DialogDescription,
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
import { Textarea } from '@/components/ui/textarea';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { CurrencyPicker } from '@/components/hr/common/CurrencyPicker';
import { SupplierPicker } from '@/components/hr/common/SupplierPicker';
import { useToast } from '@/hooks/use-toast';
import { formatDate, formatMoney, humanizeEnum } from '@/lib/hr/attendance-format';
import { hrCurrencyService } from '@/services/hr/hr-currency.service';
import { jobArchitectureService } from '@/services/hr/job-architecture.service';
import { staffRequisitionService } from '@/services/hr/recruitment.service';
import {
  REQUISITION_COST_CATEGORIES,
  type RequisitionCost,
  type RequisitionCostCategory,
  type RequisitionCostForm,
} from '@/types/hr/recruitment';

const today = () => new Date().toISOString().slice(0, 10);

const blank = (): RequisitionCostForm => ({
  category: 'JobAdvertising',
  purpose: '',
  amount: 0,
  currency: 'GHS',
  costDate: today(),
  supplierId: null,
  payeeName: '',
  description: '',
  paymentVoucherNumber: '',
});

const fromCost = (c: RequisitionCost): RequisitionCostForm => ({
  category: c.category,
  purpose: c.purpose,
  amount: c.amount,
  currency: c.currency,
  costDate: c.costDate?.slice(0, 10) ?? today(),
  supplierId: c.supplierId ?? null,
  payeeName: c.supplierId ? '' : c.payeeName ?? '',
  description: c.description ?? '',
  paymentVoucherNumber: c.paymentVoucherNumber ?? '',
});

const STATUS_TONE: Record<string, string> = {
  Recorded: 'bg-slate-100 text-slate-700',
  Approved: 'bg-emerald-100 text-emerald-800',
  Rejected: 'bg-rose-100 text-rose-800',
};

/**
 * What it cost to fill this requisition — advertising, agency fees, assessments, travel.
 *
 * Round 2b, R7: the money reads Finance's masters and HR approves its own cost.
 * - The currency is one Finance holds (`api/hr/currencies`), not a three-letter box.
 * - The rate is **not typed**: the server reads Finance's rate for the cost date and stores the
 *   base-currency amount, so the total does not move when the rate does. Before R7 this was the
 *   last caller-supplied exchange rate in the HR module.
 * - The payee is a Procurement supplier, or a named person when there is none.
 * - A cost is Recorded, then Approved or Rejected by HR — never by the person who recorded it.
 *   An approved cost's money is fixed; only the voucher and the note may follow it.
 * ⚠ This is HR's own approval, not a payment status: whether Finance has paid it is Finance's to
 * say, and the voucher number stays a typed record until the AP hand-off (R8) is agreed.
 */
export function RequisitionCostsPanel({
  requisitionId,
  canManage,
}: {
  requisitionId: string;
  canManage: boolean;
}) {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [open, setOpen] = useState(false);
  const [editing, setEditing] = useState<RequisitionCost | null>(null);
  const [form, setForm] = useState<RequisitionCostForm>(blank);
  const [deciding, setDeciding] = useState<{ cost: RequisitionCost; approve: boolean; note: string } | null>(null);

  const costs = useQuery({
    queryKey: ['hr', 'requisition-costs', requisitionId],
    queryFn: () => staffRequisitionService.getCosts(requisitionId),
    enabled: !!requisitionId,
  });
  const total = useQuery({
    queryKey: ['hr', 'requisition-costs-total', requisitionId],
    queryFn: () => staffRequisitionService.getTotalCost(requisitionId),
    enabled: !!requisitionId,
  });
  const approvedTotal = useQuery({
    queryKey: ['hr', 'requisition-costs-total', requisitionId, 'Approved'],
    queryFn: () => staffRequisitionService.getTotalCost(requisitionId, 'Approved'),
    enabled: !!requisitionId,
  });
  // Round 2b, R6: the envelope this requisition's costs count against — the linked budget's
  // recruitment budget, shared with every other requisition on that budget. Read through the
  // same budget-check the panel above uses (same query key, so one fetch), then the spend.
  const check = useQuery({
    queryKey: ['hr', 'requisition-budget', requisitionId],
    queryFn: () => staffRequisitionService.checkBudget(requisitionId),
    staleTime: 30 * 1000,
  });
  const linkedBudgetId = check.data?.linkedBudgetId ?? null;
  const spend = useQuery({
    queryKey: ['manpower-budget-spend', linkedBudgetId],
    queryFn: () => jobArchitectureService.getRecruitmentSpend(linkedBudgetId!),
    enabled: !!linkedBudgetId,
    staleTime: 30 * 1000,
  });

  const { data: currencies } = useQuery({
    queryKey: ['hr', 'currencies'],
    queryFn: () => hrCurrencyService.getActive(),
    staleTime: 5 * 60 * 1000,
  });

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: ['hr', 'requisition-costs', requisitionId] });
    await queryClient.invalidateQueries({ queryKey: ['hr', 'requisition-costs-total', requisitionId] });
    await queryClient.invalidateQueries({ queryKey: ['manpower-budget-spend'] });
  };

  const payload = (): RequisitionCostForm => ({
    ...form,
    purpose: form.purpose.trim(),
    costDate: form.costDate || null,
    supplierId: form.supplierId || null,
    payeeName: form.supplierId ? null : form.payeeName?.trim() || null,
    description: form.description?.trim() || null,
    paymentVoucherNumber: form.paymentVoucherNumber?.trim() || null,
  });

  const save = useMutation({
    mutationFn: () =>
      editing
        ? staffRequisitionService.updateCost(editing.id, payload())
        : staffRequisitionService.addCost(requisitionId, payload()),
    onSuccess: async (saved) => {
      await refresh();
      setOpen(false);
      setEditing(null);
      setForm(blank());
      toast({ title: editing ? 'Cost updated' : 'Cost recorded' });
      if (saved?.budgetWarning) toast({ title: `Over ${saved.budgetNumber ?? 'the budget'}'s recruitment envelope`, description: saved.budgetWarning, variant: 'destructive' });
    },
    onError: (e: any) =>
      toast({ title: 'Refused', description: e?.body?.message ?? e?.message, variant: 'destructive' }),
  });

  const decide = useMutation({
    mutationFn: () => {
      if (!deciding) throw new Error('Nothing to decide.');
      return deciding.approve
        ? staffRequisitionService.approveCost(deciding.cost.id, deciding.note.trim() || null)
        : staffRequisitionService.rejectCost(deciding.cost.id, deciding.note.trim() || null);
    },
    onSuccess: async (saved) => {
      await refresh();
      toast({ title: deciding?.approve ? 'Cost approved' : 'Cost rejected' });
      if (saved?.budgetWarning) toast({ title: `Over ${saved.budgetNumber ?? 'the budget'}'s recruitment envelope`, description: saved.budgetWarning, variant: 'destructive' });
      setDeciding(null);
    },
    onError: (e: any) =>
      toast({ title: 'Refused', description: e?.body?.message ?? e?.message, variant: 'destructive' }),
  });

  const remove = useMutation({
    mutationFn: (costId: string) => staffRequisitionService.deleteCost(costId),
    onSuccess: async () => {
      await refresh();
      toast({ title: 'Cost removed' });
    },
    onError: (e: any) =>
      toast({ title: 'Could not remove it', description: e?.body?.message ?? e?.message, variant: 'destructive' }),
  });

  const rows = costs.data ?? [];
  const isApprovedEdit = !!editing && editing.status === 'Approved';
  const canSave =
    !!form.purpose.trim() && form.amount > 0 && !!form.currency && (!!form.supplierId || !!form.payeeName?.trim());

  const openNew = () => { setEditing(null); setForm(blank()); setOpen(true); };
  const openEdit = (c: RequisitionCost) => { setEditing(c); setForm(fromCost(c)); setOpen(true); };

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-3">
        <div>
          <CardTitle className="text-base">Recruitment costs</CardTitle>
          {total.data !== undefined && (
            <p className="mt-1 text-sm text-muted-foreground">
              Total {formatMoney(total.data)} in base currency, at Finance&apos;s rate on each cost date
              {approvedTotal.data !== undefined && <> · approved by HR {formatMoney(approvedTotal.data)}</>}
            </p>
          )}
          {spend.data && (
            <p className={`mt-1 text-sm ${spend.data.envelopeSet && spend.data.remaining < 0 ? 'text-destructive' : 'text-muted-foreground'}`}>
              {spend.data.envelopeSet ? (
                <>
                  Against {spend.data.budgetNumber}&apos;s recruitment envelope: {formatMoney(spend.data.approved)} of {formatMoney(spend.data.recruitmentBudget)} approved across every requisition on it
                  {spend.data.pending > 0 && <>, {formatMoney(spend.data.pending)} pending</>}
                  {' · '}
                  {spend.data.remaining < 0 ? `${formatMoney(-spend.data.remaining)} over` : `${formatMoney(spend.data.remaining)} left`}
                  {spend.data.modeName === 'Block' && <> · approval over the envelope is refused</>}
                  {spend.data.modeName === 'Warn' && <> · approval over the envelope is warned</>}
                </>
              ) : (
                <>Linked to {spend.data.budgetNumber}, which sets no recruitment envelope.</>
              )}
            </p>
          )}
        </div>
        {canManage && (
          <Button size="sm" onClick={openNew}>
            <Plus className="mr-2 h-4 w-4" /> Record a cost
          </Button>
        )}
      </CardHeader>
      <CardContent className="p-0">
        {costs.isLoading ? (
          <div className="flex items-center justify-center py-10">
            <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
          </div>
        ) : rows.length === 0 ? (
          <div className="py-8">
            <EmptyState
              icon={Wallet}
              title="Nothing recorded yet"
              description="Advertising, agency fees, assessments and travel spent filling this requisition."
            />
          </div>
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Category</TableHead>
                <TableHead>Purpose</TableHead>
                <TableHead>Payee</TableHead>
                <TableHead className="text-right">Amount</TableHead>
                <TableHead className="text-right">In base currency</TableHead>
                <TableHead>Status</TableHead>
                <TableHead>Voucher</TableHead>
                <TableHead>Recorded</TableHead>
                {canManage && <TableHead className="w-32" />}
              </TableRow>
            </TableHeader>
            <TableBody>
              {rows.map((c) => (
                <TableRow key={c.id}>
                  <TableCell>{humanizeEnum(c.category)}</TableCell>
                  <TableCell>{c.purpose}</TableCell>
                  <TableCell>
                    {c.payeeName || '—'}
                    {c.supplierId && <div className="text-xs text-muted-foreground">supplier</div>}
                  </TableCell>
                  <TableCell className="text-right tabular-nums">
                    {formatMoney(c.amount, c.currency)}
                    <div className="text-xs text-muted-foreground">{formatDate(c.costDate)} · rate {c.exchangeRate}</div>
                  </TableCell>
                  <TableCell className="text-right tabular-nums">{formatMoney(c.amountBaseCurrency)}</TableCell>
                  <TableCell>
                    <Badge className={STATUS_TONE[c.status] ?? ''}>{c.status}</Badge>
                    {c.approvedByName && (
                      <div className="text-xs text-muted-foreground">{c.approvedByName} · {formatDate(c.approvedOn)}</div>
                    )}
                    {c.approvalNote && <div className="text-xs text-muted-foreground">{c.approvalNote}</div>}
                  </TableCell>
                  <TableCell>{c.paymentVoucherNumber || '—'}</TableCell>
                  <TableCell className="text-sm text-muted-foreground">
                    {formatDate(c.recordedDate)} · {c.recordedByName}
                  </TableCell>
                  {canManage && (
                    <TableCell className="whitespace-nowrap">
                      {c.status === 'Recorded' && (
                        <>
                          <Button variant="ghost" size="icon" title="Approve" onClick={() => setDeciding({ cost: c, approve: true, note: '' })}>
                            <CheckCircle2 className="h-4 w-4" />
                          </Button>
                          <Button variant="ghost" size="icon" title="Reject" onClick={() => setDeciding({ cost: c, approve: false, note: '' })}>
                            <XCircle className="h-4 w-4" />
                          </Button>
                        </>
                      )}
                      <Button variant="ghost" size="icon" title={c.status === 'Approved' ? 'Voucher and note only' : 'Edit'} onClick={() => openEdit(c)}>
                        <Pencil className="h-4 w-4" />
                      </Button>
                      <Button variant="ghost" size="icon" title="Remove (Admin)" onClick={() => remove.mutate(c.id)} disabled={remove.isPending}>
                        <Trash2 className="h-4 w-4" />
                      </Button>
                    </TableCell>
                  )}
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </CardContent>

      <Dialog open={open} onOpenChange={(o) => { setOpen(o); if (!o) setEditing(null); }}>
        <DialogContent className="max-w-xl">
          <DialogHeader>
            <DialogTitle>{editing ? 'Edit the cost' : 'Record a recruitment cost'}</DialogTitle>
            <DialogDescription>
              {isApprovedEdit
                ? 'This cost has been approved: its money is fixed. Only the payment voucher and the note can change.'
                : 'Enter the amount in the currency it was paid in. The rate is Finance’s for the cost date, read on save.'}
            </DialogDescription>
          </DialogHeader>

          <div className="grid gap-4 py-2">
            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-1.5">
                <Label>Category</Label>
                <Select value={form.category} onValueChange={(v) => setForm({ ...form, category: v as RequisitionCostCategory })} disabled={isApprovedEdit}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    {REQUISITION_COST_CATEGORIES.map((c) => (
                      <SelectItem key={c} value={c}>{humanizeEnum(c)}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="costDate">Cost date</Label>
                <Input id="costDate" type="date" value={form.costDate ?? ''} onChange={(e) => setForm({ ...form, costDate: e.target.value })} disabled={isApprovedEdit} />
              </div>
            </div>

            <div className="space-y-1.5">
              <Label htmlFor="purpose">Purpose</Label>
              <Input id="purpose" value={form.purpose} onChange={(e) => setForm({ ...form, purpose: e.target.value })} placeholder="e.g. Two Sunday adverts in the Daily Graphic" disabled={isApprovedEdit} />
            </div>

            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-1.5">
                <Label htmlFor="amount">Amount</Label>
                <Input id="amount" type="number" step="0.01" value={form.amount} onChange={(e) => setForm({ ...form, amount: Number(e.target.value) || 0 })} disabled={isApprovedEdit} />
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="currency">Currency</Label>
                <CurrencyPicker id="currency" value={form.currency} onChange={(v) => setForm({ ...form, currency: v })} options={currencies} disabled={isApprovedEdit} />
                <p className="text-xs text-muted-foreground">Finance&apos;s currencies. The rate is read for the cost date; nobody types one.</p>
              </div>
            </div>

            <SupplierPicker
              id="costSupplier"
              label="Paid to"
              value={form.supplierId}
              onChange={(id) => setForm({ ...form, supplierId: id, payeeName: id ? '' : form.payeeName })}
              disabled={isApprovedEdit}
            />
            {!form.supplierId && (
              <div className="space-y-1.5">
                <Label htmlFor="payeeName">Payee name *</Label>
                <Input id="payeeName" value={form.payeeName ?? ''} onChange={(e) => setForm({ ...form, payeeName: e.target.value })} placeholder="Who was paid, when they are not a supplier — a reimbursed candidate, say" disabled={isApprovedEdit} />
              </div>
            )}

            <div className="space-y-1.5">
              <Label htmlFor="voucher">Payment voucher</Label>
              <Input id="voucher" value={form.paymentVoucherNumber ?? ''} onChange={(e) => setForm({ ...form, paymentVoucherNumber: e.target.value })} />
              <p className="text-xs text-muted-foreground">Finance&apos;s voucher number, as a record. Payment itself is Finance&apos;s to confirm.</p>
            </div>

            <div className="space-y-1.5">
              <Label htmlFor="costDescription">Notes</Label>
              <Textarea id="costDescription" rows={2} value={form.description ?? ''} onChange={(e) => setForm({ ...form, description: e.target.value })} />
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setOpen(false)}>Cancel</Button>
            <Button onClick={() => save.mutate()} disabled={(!isApprovedEdit && !canSave) || save.isPending}>
              {save.isPending ? 'Saving…' : editing ? 'Save' : 'Record'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={!!deciding} onOpenChange={(o) => !o && setDeciding(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{deciding?.approve ? 'Approve this cost' : 'Reject this cost'}</DialogTitle>
            <DialogDescription>
              {deciding && `${humanizeEnum(deciding.cost.category)} · ${formatMoney(deciding.cost.amount, deciding.cost.currency)} to ${deciding.cost.payeeName ?? '—'}. `}
              HR&apos;s own decision on the spend; not the person who recorded it.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-1.5">
            <Label htmlFor="decisionNote">Note</Label>
            <Textarea id="decisionNote" rows={2} value={deciding?.note ?? ''} onChange={(e) => deciding && setDeciding({ ...deciding, note: e.target.value })} />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDeciding(null)}>Cancel</Button>
            <Button variant={deciding?.approve ? 'default' : 'destructive'} onClick={() => decide.mutate()} disabled={decide.isPending}>
              {decide.isPending ? 'Saving…' : deciding?.approve ? 'Approve' : 'Reject'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </Card>
  );
}
