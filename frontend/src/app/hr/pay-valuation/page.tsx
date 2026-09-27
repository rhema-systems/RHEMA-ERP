'use client';

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Banknote, Loader2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { HR_ADMIN_ROLES, PAY_VALUER_ROLES } from '@/components/hr/common/PermissionGate';
import { useAuth } from '@/hooks/use-auth';
import { useToast } from '@/hooks/use-toast';
import { payValuationService } from '@/services/hr/pay-valuation.service';
import { leaveEncashmentService } from '@/services/hr/leave.service';
import type { PayToValueItem, SettlementLine } from '@/types/hr/separation';

const money = (amount: number | null | undefined, currency: string) =>
  amount == null
    ? '—'
    : `${currency} ${amount.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;

const errorOf = (e: any) => e?.body?.detail ?? e?.body?.message ?? e?.message;

/**
 * Pay to value — Finance's step (leave settings audit 2, decision P2).
 *
 * The stakeholders asked HR to leave the monetary aspects and the calculation to Finance, and submit
 * the details such as the days encashed. HR records the days on a leaver's settlement and on leave
 * cashed in; the holder of `HR.Pay.Value` puts the money on them here. A statement cannot be
 * finalised while one of its pay lines awaits a figure, so nothing HR priced reaches Finance's books.
 */
export default function PayValuationPage() {
  const { hasAnyPermission, hasAnyRole } = useAuth();
  const canValue = hasAnyPermission(['HR.Pay.Value']) || hasAnyRole([...PAY_VALUER_ROLES, ...HR_ADMIN_ROLES]);
  const [open, setOpen] = useState<PayToValueItem | null>(null);

  const { data: queue, isLoading } = useQuery({
    queryKey: ['hr', 'pay-valuation'],
    queryFn: () => payValuationService.getQueue(),
    enabled: canValue,
  });

  if (!canValue) {
    return (
      <div className="p-6">
        <EmptyState
          icon={Banknote}
          title="Finance's step"
          description="Valuing the pay HR records is Finance's. Ask an administrator for the “Value HR pay (Finance)” permission."
        />
      </div>
    );
  }

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Pay to value"
        description="HR has recorded the days; Finance puts the money on them. A leaver's statement cannot be finalised until every pay line has a figure."
      />

      <Card>
        <CardHeader>
          <CardTitle>Awaiting Finance</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>What</TableHead>
                  <TableHead>Reference</TableHead>
                  <TableHead>Employee</TableHead>
                  <TableHead>Last day</TableHead>
                  <TableHead>To value</TableHead>
                  <TableHead />
                </TableRow>
              </TableHeader>
              <TableBody>
                {isLoading ? (
                  <TableRow>
                    <TableCell colSpan={6}>
                      <Loader2 className="h-4 w-4 animate-spin" />
                    </TableCell>
                  </TableRow>
                ) : (queue ?? []).length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={6}>
                      <EmptyState
                        icon={Banknote}
                        title="Nothing to value"
                        description="Leavers' statements and cashed-in leave appear here once HR has recorded the days."
                      />
                    </TableCell>
                  </TableRow>
                ) : (
                  (queue ?? []).map((item) => (
                    <TableRow key={`${item.kind}-${item.id}`}>
                      <TableCell>
                        <Badge variant="outline">
                          {item.kind === 'Settlement' ? "Leaver's settlement" : 'Leave cashed in'}
                        </Badge>
                      </TableCell>
                      <TableCell className="font-medium">{item.reference}</TableCell>
                      <TableCell>
                        {item.employeeName}
                        {item.employeeNumber && (
                          <span className="ml-1 text-xs text-muted-foreground">{item.employeeNumber}</span>
                        )}
                      </TableCell>
                      <TableCell>{item.lastDay?.slice(0, 10) ?? '—'}</TableCell>
                      <TableCell className="text-sm">{item.summary}</TableCell>
                      <TableCell className="text-right">
                        <Button size="sm" variant="outline" onClick={() => setOpen(item)}>
                          {item.kind === 'Settlement' ? 'Value' : 'Mark as paid'}
                        </Button>
                      </TableCell>
                    </TableRow>
                  ))
                )}
              </TableBody>
            </Table>
          </div>
        </CardContent>
      </Card>

      {open?.kind === 'Settlement' && (
        <SettlementValuation separationId={open.id} onClose={() => setOpen(null)} />
      )}
      {open?.kind === 'Encashment' && <EncashmentPayment item={open} onClose={() => setOpen(null)} />}
    </div>
  );
}

/** A leaver's whole statement, with a figure to enter on each pay line awaiting one. */
function SettlementValuation({ separationId, onClose }: { separationId: string; onClose: () => void }) {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [editing, setEditing] = useState<string | null>(null);
  const [amount, setAmount] = useState('');
  const [source, setSource] = useState('');

  const { data: settlement, isLoading } = useQuery({
    queryKey: ['hr', 'pay-valuation', 'settlement', separationId],
    queryFn: () => payValuationService.getSettlement(separationId),
  });

  const value = useMutation({
    mutationFn: (line: SettlementLine) =>
      payValuationService.valueLine(line.id, { amount: Number(amount), sourceReference: source.trim() }),
    onSuccess: async () => {
      setEditing(null);
      setAmount('');
      setSource('');
      await queryClient.invalidateQueries({ queryKey: ['hr', 'pay-valuation'] });
      toast({ title: 'Valued', description: 'The figure is on the statement, with who entered it.' });
    },
    onError: (e: any) => toast({ variant: 'destructive', title: 'It could not be valued', description: errorOf(e) }),
  });

  if (isLoading || !settlement) {
    return <Loader2 className="h-4 w-4 animate-spin" />;
  }

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between">
        <CardTitle>
          {settlement.separationNumber} — {settlement.employeeName}
        </CardTitle>
        <Button variant="ghost" size="sm" onClick={onClose}>
          Close
        </Button>
      </CardHeader>
      <CardContent className="space-y-2">
        {settlement.lines.map((line) => (
          <div key={line.id} className="rounded border p-3">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <div className="min-w-[240px]">
                <div className="font-medium">{line.description}</div>
                <div className="text-xs text-muted-foreground">
                  {line.basis ?? line.categoryName}
                  {line.sourceReference ? ` · source: ${line.sourceReference}` : ''}
                </div>
              </div>
              <div className="flex items-center gap-3">
                {line.days != null && <span className="text-sm text-muted-foreground">{line.days} day(s)</span>}
                {line.computation === 'CannotCompute' || (line.awaitingFinance && line.amount == null) ? (
                  <Badge variant="outline" className="border-amber-400 text-amber-700 dark:text-amber-300">
                    {line.isPayLine ? 'Awaiting Finance' : 'Not computed'}
                  </Badge>
                ) : (
                  <span className="tabular-nums">
                    {line.isDeduction ? '−' : ''}
                    {money(line.amount, settlement.currencyCode)}
                  </span>
                )}
                {/* HR's figure from before pay was Finance's: shown for reference, not as Finance's. */}
                {line.awaitingFinance && line.amount != null && (
                  <Badge variant="outline" className="border-amber-400 text-xs text-amber-700 dark:text-amber-300">
                    HR&apos;s figure — to value
                  </Badge>
                )}
                {line.computation === 'ValuedByFinance' && (
                  <Badge variant="outline" className="text-xs">
                    Valued{line.valuedByName ? ` — ${line.valuedByName}` : ''}
                  </Badge>
                )}
                {line.isPayLine && settlement.separationStatus === 'SettlementPending' && (
                  <Button
                    size="sm"
                    variant="outline"
                    onClick={() => {
                      setEditing(editing === line.id ? null : line.id);
                      setAmount(line.amount == null ? '' : String(line.amount));
                      setSource(line.sourceReference ?? '');
                    }}
                  >
                    {line.awaitingFinance ? 'Enter the figure' : 'Revalue'}
                  </Button>
                )}
              </div>
            </div>
            {editing === line.id && (
              <div className="mt-3 flex flex-wrap items-end gap-3 rounded bg-muted/40 p-3">
                <div className="w-[160px] space-y-1">
                  <Label className="text-xs">Amount ({settlement.currencyCode})</Label>
                  <Input type="number" step="0.01" min={0} value={amount} onChange={(e) => setAmount(e.target.value)} />
                </div>
                <div className="min-w-[240px] flex-1 space-y-1">
                  <Label className="text-xs">Where the figure came from</Label>
                  <Input
                    value={source}
                    onChange={(e) => setSource(e.target.value)}
                    placeholder="e.g. final pay computation FP-2026-014"
                  />
                </div>
                <Button
                  size="sm"
                  disabled={value.isPending || amount === '' || !source.trim()}
                  onClick={() => value.mutate(line)}
                >
                  {value.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                  Save
                </Button>
              </div>
            )}
          </div>
        ))}
        <p className="pt-2 text-sm">
          Net payable as it stands: <strong>{money(settlement.netPayable, settlement.currencyCode)}</strong>
          {settlement.uncomputedLines > 0 && ` — incomplete: ${settlement.uncomputedLines} line(s) have no figure yet.`}
        </p>
      </CardContent>
    </Card>
  );
}

/** Leave cashed in while employed: Finance pays the days, entering the amount. */
function EncashmentPayment({ item, onClose }: { item: PayToValueItem; onClose: () => void }) {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [amount, setAmount] = useState('');
  const [basis, setBasis] = useState('');
  const [reference, setReference] = useState('');

  const pay = useMutation({
    mutationFn: () =>
      leaveEncashmentService.markAsProcessed(item.id, {
        amount: Number(amount),
        basis: basis.trim() || null,
        paymentReference: reference.trim(),
      }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['hr', 'pay-valuation'] });
      toast({ title: 'Paid', description: 'Recorded, and posted to Finance with the amount you entered.' });
      onClose();
    },
    onError: (e: any) => toast({ variant: 'destructive', title: 'It could not be marked paid', description: errorOf(e) }),
  });

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between">
        <CardTitle>
          {item.reference} — {item.employeeName}: {item.summary}
        </CardTitle>
        <Button variant="ghost" size="sm" onClick={onClose}>
          Close
        </Button>
      </CardHeader>
      <CardContent className="flex flex-wrap items-end gap-3">
        <div className="w-[160px] space-y-1">
          <Label className="text-xs">Amount paid</Label>
          <Input type="number" step="0.01" min={0} value={amount} onChange={(e) => setAmount(e.target.value)} />
        </div>
        <div className="min-w-[220px] flex-1 space-y-1">
          <Label className="text-xs">How it was worked out (optional)</Label>
          <Input value={basis} onChange={(e) => setBasis(e.target.value)} />
        </div>
        <div className="w-[200px] space-y-1">
          <Label className="text-xs">Payment reference</Label>
          <Input value={reference} onChange={(e) => setReference(e.target.value)} placeholder="Voucher or transaction id" />
        </div>
        <Button
          size="sm"
          disabled={pay.isPending || !(Number(amount) > 0) || !reference.trim()}
          onClick={() => pay.mutate()}
        >
          {pay.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
          Mark as paid
        </Button>
      </CardContent>
    </Card>
  );
}
