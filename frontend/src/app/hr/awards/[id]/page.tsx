'use client';

import { useState } from 'react';
import { useParams } from 'next/navigation';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Banknote, CalendarCheck, CheckCircle2, Info, Loader2, Trophy } from 'lucide-react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { awardsService } from '@/services/hr/awards.service';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const fmtMoney = (v?: number | null) =>
  v === null || v === undefined
    ? '—'
    : v.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });

/**
 * One conferred award: what was promised, what was paid, and when it was presented.
 *
 * ⚠ **Payment releases the promise and books the payment, and the two can differ.** An award
 * promised 1,000 and paid 900 releases 1,000 of budget reservation and records 900 of spend. The
 * form therefore offers an amount separate from the award value rather than assuming they match.
 *
 * ⚠ **An award can be paid once.** A second attempt is refused, so the form disappears once paid
 * rather than offering a button that will be rejected.
 */
export default function AwardDetailPage() {
  const { id } = useParams<{ id: string }>();
  const queryClient = useQueryClient();

  const [reference, setReference] = useState('');
  const [amountPaid, setAmountPaid] = useState('');
  const [presentationDate, setPresentationDate] = useState(() =>
    new Date().toISOString().slice(0, 10),
  );
  const [venue, setVenue] = useState('');
  const [notes, setNotes] = useState('');

  const { data: award, isLoading } = useQuery({
    queryKey: ['award', id],
    queryFn: () => awardsService.getAward(id),
  });

  const invalidate = () => {
    queryClient.invalidateQueries({ queryKey: ['award', id] });
    queryClient.invalidateQueries({ queryKey: ['awards-conferred'] });
  };

  const pay = useMutation({
    mutationFn: () =>
      awardsService.recordPayment(id, {
        awardId: id,
        paymentReference: reference.trim(),
        amountPaid: amountPaid === '' ? null : Number(amountPaid),
      }),
    onSuccess: () => { toast.success('Payment recorded.'); invalidate(); },
    onError: (e: any) => toast.error(e?.body?.detail || e?.message || 'The payment was refused.'),
  });

  const present = useMutation({
    mutationFn: () =>
      awardsService.recordPresentation(id, {
        awardId: id,
        presentationDate: new Date(presentationDate).toISOString().slice(0, 19),
        presentationVenue: venue.trim() || null,
        presentationNotes: notes.trim() || null,
      }),
    onSuccess: () => { toast.success('Presentation recorded.'); invalidate(); },
    onError: (e: any) => toast.error(e?.body?.detail || e?.message || 'The record was refused.'),
  });

  if (isLoading || !award) {
    return (
      <div className="flex items-center justify-center p-12">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={award.awardNumber}
        description={`${award.awardTypeName}${award.awardLevelName ? ` — ${award.awardLevelName}` : ''}`}
        backHref="/hr/awards"
      />

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <Trophy className="h-4 w-4" />
            The award
          </CardTitle>
        </CardHeader>
        <CardContent className="grid gap-4 sm:grid-cols-2">
          <div>
            <p className="text-xs text-muted-foreground">Employee</p>
            <p className="text-sm">
              {award.employeeName}
              <span className="ml-2 text-xs text-muted-foreground">{award.employeeNumber}</span>
            </p>
          </div>
          <div>
            <p className="text-xs text-muted-foreground">Awarded</p>
            <p className="text-sm">{fmtDate(award.awardDate)}</p>
          </div>
          <div>
            <p className="text-xs text-muted-foreground">Value</p>
            <p className="text-sm">{fmtMoney(award.monetaryAmount)}</p>
          </div>
          <div>
            <p className="text-xs text-muted-foreground">Cycle</p>
            <p className="text-sm">{award.awardCycleName ?? '—'}</p>
          </div>
          {award.citation && (
            <div className="sm:col-span-2">
              <p className="text-xs text-muted-foreground">Citation</p>
              <p className="whitespace-pre-wrap text-sm">{award.citation}</p>
            </div>
          )}
          {award.reason && (
            <div className="sm:col-span-2">
              <p className="text-xs text-muted-foreground">Reason</p>
              <p className="whitespace-pre-wrap text-sm">{award.reason}</p>
            </div>
          )}
        </CardContent>
      </Card>

      {/* ── payment ────────────────────────────────────────────────────────── */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <Banknote className="h-4 w-4" />
            Payment
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          {award.paymentProcessed ? (
            <Alert>
              <CheckCircle2 className="h-4 w-4" />
              <AlertDescription>
                Paid {fmtDate(award.paymentDate)} — reference {award.paymentReference}
                {award.amountPaid !== null && award.amountPaid !== award.monetaryAmount && (
                  <> ({fmtMoney(award.amountPaid)} against a promised {fmtMoney(award.monetaryAmount)})</>
                )}
                .
              </AlertDescription>
            </Alert>
          ) : (
            <>
              <Alert>
                <Info className="h-4 w-4" />
                <AlertDescription>
                  Recording the payment releases the whole committed amount from the budget and books
                  what was actually paid. Leave the amount blank to pay what was promised.
                </AlertDescription>
              </Alert>
              <div className="grid gap-4 sm:grid-cols-2">
                <div className="space-y-2">
                  <Label htmlFor="reference">Payment reference</Label>
                  <Input
                    id="reference"
                    value={reference}
                    onChange={(e) => setReference(e.target.value)}
                    placeholder="Voucher or transfer reference"
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="amountPaid">Amount paid</Label>
                  <Input
                    id="amountPaid"
                    type="number"
                    min={0}
                    value={amountPaid}
                    onChange={(e) => setAmountPaid(e.target.value)}
                    placeholder={fmtMoney(award.monetaryAmount)}
                  />
                </div>
              </div>
              <div className="flex justify-end">
                <Button
                  disabled={reference.trim().length === 0 || pay.isPending}
                  onClick={() => pay.mutate()}
                >
                  {pay.isPending ? (
                    <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                  ) : (
                    <Banknote className="mr-2 h-4 w-4" />
                  )}
                  Record payment
                </Button>
              </div>
            </>
          )}
        </CardContent>
      </Card>

      {/* ── presentation ───────────────────────────────────────────────────── */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <CalendarCheck className="h-4 w-4" />
            Presentation
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          {award.presentationDate ? (
            <Alert>
              <CheckCircle2 className="h-4 w-4" />
              <AlertDescription>
                Presented {fmtDate(award.presentationDate)}
                {award.presentationVenue ? ` at ${award.presentationVenue}` : ''}.
              </AlertDescription>
            </Alert>
          ) : (
            <>
              <div className="grid gap-4 sm:grid-cols-2">
                <div className="space-y-2">
                  <Label htmlFor="presentationDate">Date</Label>
                  <Input
                    id="presentationDate"
                    type="date"
                    value={presentationDate}
                    onChange={(e) => setPresentationDate(e.target.value)}
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="venue">Venue</Label>
                  <Input id="venue" value={venue} onChange={(e) => setVenue(e.target.value)} />
                </div>
                <div className="space-y-2 sm:col-span-2">
                  <Label htmlFor="notes">Notes</Label>
                  <Textarea
                    id="notes"
                    rows={3}
                    value={notes}
                    onChange={(e) => setNotes(e.target.value)}
                  />
                </div>
              </div>
              <div className="flex justify-end">
                <Button disabled={!presentationDate || present.isPending} onClick={() => present.mutate()}>
                  {present.isPending ? (
                    <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                  ) : (
                    <CalendarCheck className="mr-2 h-4 w-4" />
                  )}
                  Record presentation
                </Button>
              </div>
            </>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
