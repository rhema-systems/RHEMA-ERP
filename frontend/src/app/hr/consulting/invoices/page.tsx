'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Receipt, Loader2, Send, BadgeDollarSign, Ban, Landmark } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { Skeleton } from '@/components/ui/skeleton';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { FinancePostingCard, FinancePostingInlineStatus } from '@/components/hr/common/FinancePostingCard';
import { timesheetInvoiceService } from '@/services/hr/consultant.service';
import { formatDate, formatHours, formatMoney, today } from '@/lib/hr/attendance-format';
import { INVOICE_STATUS_OPTIONS } from '@/types/hr/consultant';
import type { TimesheetInvoiceStatus, TimesheetInvoiceSummary } from '@/types/hr/consultant';

const ALL = '__all__';
const OVERDUE = '__overdue__';

type RowAction = 'send' | 'markPaid' | 'void';

/**
 * Invoices raised from client-confirmed timesheets.
 *
 * Generation happens from the timesheets side (pick the confirmed sheets, then invoice), so
 * this screen is the register plus the post-issue actions: send, record payment, void.
 */
export default function TimesheetInvoicesPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [filter, setFilter] = useState<string>(ALL);
  const [action, setAction] = useState<{ kind: RowAction; row: TimesheetInvoiceSummary } | null>(
    null,
  );
  const [busy, setBusy] = useState(false);
  // The invoice's Finance posting rows (the AR hand-off): this page has no detail screen, so the
  // register card — with its Refresh, which is how the desk pulls Finance's receipt — opens here.
  const [financeRow, setFinanceRow] = useState<TimesheetInvoiceSummary | null>(null);
  const [paidDate, setPaidDate] = useState(today());
  const [paidAmount, setPaidAmount] = useState('');
  const [voidReason, setVoidReason] = useState('');

  const { data, isLoading, isFetching } = useQuery({
    queryKey: ['hr', 'timesheet-invoices', 'list', filter],
    queryFn: () => {
      if (filter === OVERDUE) return timesheetInvoiceService.getOverdue();
      if (filter === ALL) return timesheetInvoiceService.getPaged(1, 100).then((p) => p.items);
      return timesheetInvoiceService.getByStatus(filter as TimesheetInvoiceStatus);
    },
  });

  const rows: TimesheetInvoiceSummary[] = data ?? [];

  const openAction = (kind: RowAction, row: TimesheetInvoiceSummary) => {
    if (kind === 'markPaid') {
      setPaidDate(today());
      setPaidAmount(String(row.totalAmount));
    }
    if (kind === 'void') setVoidReason('');
    setAction({ kind, row });
  };

  const runAction = async () => {
    if (!action) return false;
    setBusy(true);
    try {
      const { kind, row } = action;
      if (kind === 'send') {
        await timesheetInvoiceService.send(row.id);
      } else if (kind === 'markPaid') {
        const amount = Number(paidAmount);
        if (!Number.isFinite(amount) || amount <= 0) {
          toast({ title: 'Enter the amount paid', variant: 'destructive' });
          return false;
        }
        await timesheetInvoiceService.markPaid(row.id, paidDate, amount);
      } else {
        if (!voidReason.trim()) {
          toast({ title: 'A reason is required to void', variant: 'destructive' });
          return false;
        }
        await timesheetInvoiceService.void(row.id, voidReason.trim());
      }
      await queryClient.invalidateQueries({ queryKey: ['hr', 'timesheet-invoices'] });
      toast({ title: 'Done', description: 'The invoice was updated.' });
      setAction(null);
      return true;
    } catch (e: any) {
      toast({ title: 'Error', description: e?.message || 'Action failed.', variant: 'destructive' });
      return false;
    } finally {
      setBusy(false);
    }
  };

  const dialogCopy: Record<RowAction, { title: string; description: string; confirm: string }> = {
    send: {
      title: 'Send this invoice?',
      description: 'Marks the invoice as issued to the client and starts the payment clock.',
      confirm: 'Send',
    },
    markPaid: {
      title: 'Record a payment',
      description: 'A payment below the invoice total leaves it partially paid.',
      confirm: 'Record payment',
    },
    void: {
      title: 'Void this invoice?',
      description:
        'Voiding cancels the invoice. The timesheets on it become available to invoice again.',
      confirm: 'Void',
    },
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Timesheet Invoices"
        description="Invoices raised from client-confirmed consultant timesheets."
        backHref="/hr/consulting"
        actions={
          <Button variant="outline" onClick={() => router.push('/hr/consulting/timesheets')}>
            Invoice from timesheets
          </Button>
        }
      />

      <Card>
        <CardHeader>
          <CardTitle>Filter</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="max-w-xs space-y-2">
            <label className="text-sm font-medium">Show</label>
            <Select value={filter} onValueChange={setFilter}>
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value={ALL}>All invoices</SelectItem>
                <SelectItem value={OVERDUE}>Overdue</SelectItem>
                {INVOICE_STATUS_OPTIONS.map((o) => (
                  <SelectItem key={o.value} value={o.value}>
                    {o.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <div className="flex items-center justify-between">
            <CardTitle>
              {rows.length} {rows.length === 1 ? 'invoice' : 'invoices'}
            </CardTitle>
            {isFetching && <Loader2 className="h-4 w-4 animate-spin text-muted-foreground" />}
          </div>
        </CardHeader>
        <CardContent>
          <div className="overflow-x-auto rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Invoice</TableHead>
                  <TableHead>Client</TableHead>
                  <TableHead>Consultant</TableHead>
                  <TableHead>Billing period</TableHead>
                  <TableHead className="text-right">Hours</TableHead>
                  <TableHead className="text-right">Total</TableHead>
                  <TableHead>Due</TableHead>
                  <TableHead>Status</TableHead>
                  {/* Lane 8, slice 6: what Finance holds for this invoice — the AR row, once raised. */}
                  <TableHead>Finance</TableHead>
                  <TableHead className="w-[60px]" />
                </TableRow>
              </TableHeader>
              <TableBody>
                {isLoading ? (
                  [...Array(5)].map((_, i) => (
                    <TableRow key={i}>
                      {[...Array(10)].map((__, j) => (
                        <TableCell key={j}>
                          <Skeleton className="h-4 w-[70px]" />
                        </TableCell>
                      ))}
                    </TableRow>
                  ))
                ) : rows.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={10}>
                      <EmptyState
                        icon={Receipt}
                        title="No invoices"
                        description="Confirm some timesheets with the client, then raise an invoice from them."
                      />
                    </TableCell>
                  </TableRow>
                ) : (
                  rows.map((inv) => (
                    <TableRow key={inv.id}>
                      <TableCell className="font-medium">{inv.invoiceNumber}</TableCell>
                      <TableCell>{inv.clientName}</TableCell>
                      <TableCell>{inv.consultantName}</TableCell>
                      <TableCell>
                        {formatDate(inv.billingPeriodStart)} – {formatDate(inv.billingPeriodEnd)}
                      </TableCell>
                      <TableCell className="text-right">{formatHours(inv.totalHours)}</TableCell>
                      <TableCell className="text-right">
                        {formatMoney(inv.totalAmount, inv.currency)}
                      </TableCell>
                      <TableCell>{formatDate(inv.dueDate)}</TableCell>
                      <TableCell>
                        <StatusBadge status={inv.status} />
                      </TableCell>
                      <TableCell>
                        <FinancePostingInlineStatus sourceDocumentId={inv.id} />
                      </TableCell>
                      <TableCell>
                        <DropdownMenu>
                          <DropdownMenuTrigger asChild>
                            <Button variant="ghost" size="icon" className="h-8 w-8">
                              <span className="sr-only">Actions</span>⋯
                            </Button>
                          </DropdownMenuTrigger>
                          <DropdownMenuContent align="end">
                            <DropdownMenuItem
                              disabled={inv.status !== 'Draft'}
                              onClick={() => openAction('send', inv)}
                            >
                              <Send className="mr-2 h-4 w-4" /> Send
                            </DropdownMenuItem>
                            <DropdownMenuItem
                              disabled={
                                inv.status === 'Paid' ||
                                inv.status === 'Voided' ||
                                inv.status === 'Draft'
                              }
                              onClick={() => openAction('markPaid', inv)}
                            >
                              <BadgeDollarSign className="mr-2 h-4 w-4" /> Record payment
                            </DropdownMenuItem>
                            <DropdownMenuItem onClick={() => setFinanceRow(inv)}>
                              <Landmark className="mr-2 h-4 w-4" /> Finance posting
                            </DropdownMenuItem>
                            <DropdownMenuItem
                              className="text-destructive focus:text-destructive"
                              disabled={inv.status === 'Voided' || inv.status === 'Paid'}
                              onClick={() => openAction('void', inv)}
                            >
                              <Ban className="mr-2 h-4 w-4" /> Void
                            </DropdownMenuItem>
                          </DropdownMenuContent>
                        </DropdownMenu>
                      </TableCell>
                    </TableRow>
                  ))
                )}
              </TableBody>
            </Table>
          </div>
        </CardContent>
      </Card>

      <ConfirmationDialog
        open={action !== null}
        onOpenChange={(open) => !open && setAction(null)}
        title={action ? dialogCopy[action.kind].title : ''}
        description={action ? dialogCopy[action.kind].description : ''}
        confirmText={action ? dialogCopy[action.kind].confirm : 'Confirm'}
        variant={action?.kind === 'void' ? 'destructive' : 'default'}
        isLoading={busy}
        onConfirm={runAction}
      >
        {action?.kind === 'markPaid' && (
          <div className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="paidDate">Payment date</Label>
              <Input
                id="paidDate"
                type="date"
                value={paidDate}
                onChange={(e) => setPaidDate(e.target.value)}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="paidAmount">Amount paid</Label>
              <Input
                id="paidAmount"
                type="number"
                step="0.01"
                value={paidAmount}
                onChange={(e) => setPaidAmount(e.target.value)}
              />
              <p className="text-xs text-muted-foreground">
                Invoice total: {formatMoney(action.row.totalAmount, action.row.currency)}
              </p>
            </div>
          </div>
        )}
        {action?.kind === 'void' && (
          <div className="space-y-2">
            <Label htmlFor="voidReason">Reason</Label>
            <Textarea
              id="voidReason"
              rows={3}
              value={voidReason}
              onChange={(e) => setVoidReason(e.target.value)}
              placeholder="Why is this invoice being voided?"
            />
          </div>
        )}
      </ConfirmationDialog>

      <Dialog open={financeRow !== null} onOpenChange={(open) => { if (!open) setFinanceRow(null); }}>
        <DialogContent className="max-w-2xl">
          <DialogHeader>
            <DialogTitle>Finance posting · {financeRow?.invoiceNumber}</DialogTitle>
            <DialogDescription>
              The AR invoice Finance holds for this consulting invoice. Refresh pulls Finance's status and, once paid, the receipt onto the invoice.
            </DialogDescription>
          </DialogHeader>
          {financeRow && (
            <FinancePostingCard sourceDocumentId={financeRow.id} invalidateKeys={[['hr', 'timesheet-invoices']]} />
          )}
        </DialogContent>
      </Dialog>
    </div>
  );
}
