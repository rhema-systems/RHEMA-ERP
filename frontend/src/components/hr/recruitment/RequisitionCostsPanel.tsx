'use client';

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2, Plus, Trash2, Wallet } from 'lucide-react';
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
import { useToast } from '@/hooks/use-toast';
import { formatDate, formatMoney, humanizeEnum } from '@/lib/hr/attendance-format';
import { staffRequisitionService } from '@/services/hr/recruitment.service';
import {
  REQUISITION_COST_CATEGORIES,
  type RequisitionCostCategory,
  type RequisitionCostForm,
} from '@/types/hr/recruitment';

const blank = (): RequisitionCostForm => ({
  category: 'Advertising',
  purpose: '',
  amount: 0,
  currency: 'GHS',
  exchangeRate: 1,
  description: '',
  paymentVoucherNumber: '',
});

/**
 * What it cost to fill this requisition — advertising, agency fees, assessments, travel.
 *
 * ⚠ The total the server reports is the sum of `amount × exchangeRate`, so a cost recorded in a
 * foreign currency contributes its converted value. The rate is per-row and entered here rather
 * than looked up, because the figure that belongs on the record is the one that was actually used
 * when the money moved.
 *
 * Recording costs is HR's — the buttons are hidden for anyone else, and the server refuses anyway.
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
  const [form, setForm] = useState<RequisitionCostForm>(blank);

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

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: ['hr', 'requisition-costs', requisitionId] });
    await queryClient.invalidateQueries({ queryKey: ['hr', 'requisition-costs-total', requisitionId] });
  };

  const add = useMutation({
    mutationFn: () =>
      staffRequisitionService.addCost(requisitionId, {
        ...form,
        purpose: form.purpose.trim(),
        description: form.description?.trim() || null,
        paymentVoucherNumber: form.paymentVoucherNumber?.trim() || null,
      }),
    onSuccess: async () => {
      await refresh();
      setOpen(false);
      setForm(blank());
      toast({ title: 'Cost recorded' });
    },
    onError: (e: any) =>
      toast({ title: 'Could not record it', description: e?.message, variant: 'destructive' }),
  });

  const remove = useMutation({
    mutationFn: (costId: string) => staffRequisitionService.deleteCost(costId),
    onSuccess: async () => {
      await refresh();
      toast({ title: 'Cost removed' });
    },
    onError: (e: any) =>
      toast({ title: 'Could not remove it', description: e?.message, variant: 'destructive' }),
  });

  const rows = costs.data ?? [];

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-3">
        <div>
          <CardTitle className="text-base">Recruitment costs</CardTitle>
          {total.data !== undefined && (
            <p className="mt-1 text-sm text-muted-foreground">
              Total {formatMoney(total.data)} — converted at each row&apos;s own rate.
            </p>
          )}
        </div>
        {canManage && (
          <Button size="sm" onClick={() => setOpen(true)}>
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
                <TableHead className="text-right">Amount</TableHead>
                <TableHead className="text-right">Rate</TableHead>
                <TableHead>Voucher</TableHead>
                <TableHead>Recorded</TableHead>
                {canManage && <TableHead className="w-10" />}
              </TableRow>
            </TableHeader>
            <TableBody>
              {rows.map((c) => (
                <TableRow key={c.id}>
                  <TableCell>{humanizeEnum(c.category)}</TableCell>
                  <TableCell>{c.purpose}</TableCell>
                  <TableCell className="text-right tabular-nums">
                    {formatMoney(c.amount, c.currency)}
                  </TableCell>
                  <TableCell className="text-right tabular-nums">{c.exchangeRate}</TableCell>
                  <TableCell>{c.paymentVoucherNumber || '—'}</TableCell>
                  <TableCell className="text-sm text-muted-foreground">
                    {formatDate(c.recordedDate)} · {c.recordedByName}
                  </TableCell>
                  {canManage && (
                    <TableCell>
                      <Button
                        variant="ghost"
                        size="icon"
                        onClick={() => remove.mutate(c.id)}
                        disabled={remove.isPending}
                      >
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

      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Record a recruitment cost</DialogTitle>
            <DialogDescription>
              Enter the amount in the currency it was paid in, with the rate that was used.
            </DialogDescription>
          </DialogHeader>

          <div className="grid gap-4 py-2">
            <div className="space-y-1.5">
              <Label>Category</Label>
              <Select
                value={form.category}
                onValueChange={(v) => setForm({ ...form, category: v as RequisitionCostCategory })}
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {REQUISITION_COST_CATEGORIES.map((c) => (
                    <SelectItem key={c} value={c}>
                      {humanizeEnum(c)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="space-y-1.5">
              <Label htmlFor="purpose">Purpose</Label>
              <Input
                id="purpose"
                value={form.purpose}
                onChange={(e) => setForm({ ...form, purpose: e.target.value })}
                placeholder="e.g. Two Sunday adverts in the Daily Graphic"
              />
            </div>

            <div className="grid grid-cols-3 gap-3">
              <div className="space-y-1.5">
                <Label htmlFor="amount">Amount</Label>
                <Input
                  id="amount"
                  type="number"
                  step="0.01"
                  value={form.amount}
                  onChange={(e) => setForm({ ...form, amount: Number(e.target.value) || 0 })}
                />
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="currency">Currency</Label>
                <Input
                  id="currency"
                  value={form.currency}
                  onChange={(e) => setForm({ ...form, currency: e.target.value.toUpperCase() })}
                  maxLength={3}
                />
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="exchangeRate">Rate</Label>
                <Input
                  id="exchangeRate"
                  type="number"
                  step="0.0001"
                  value={form.exchangeRate}
                  onChange={(e) => setForm({ ...form, exchangeRate: Number(e.target.value) || 1 })}
                />
              </div>
            </div>

            <div className="space-y-1.5">
              <Label htmlFor="voucher">Payment voucher</Label>
              <Input
                id="voucher"
                value={form.paymentVoucherNumber ?? ''}
                onChange={(e) => setForm({ ...form, paymentVoucherNumber: e.target.value })}
              />
            </div>

            <div className="space-y-1.5">
              <Label htmlFor="costDescription">Notes</Label>
              <Textarea
                id="costDescription"
                rows={2}
                value={form.description ?? ''}
                onChange={(e) => setForm({ ...form, description: e.target.value })}
              />
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setOpen(false)}>
              Cancel
            </Button>
            <Button
              onClick={() => add.mutate()}
              disabled={!form.purpose.trim() || form.amount <= 0 || add.isPending}
            >
              {add.isPending ? 'Saving…' : 'Record'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </Card>
  );
}
