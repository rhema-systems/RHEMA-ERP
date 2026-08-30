'use client';

import { useEffect, useMemo, useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import { Separator } from '@/components/ui/separator';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { rfqService, type RfqDetailDto } from '@/services/rfqService';
import { getQuoteSubmitLabel, isQuoteSubmissionOpen } from '@/lib/rfq-quote-state';
import { toast } from 'sonner';
import { ArrowLeft, CheckCircle2, Clock3, FileText, History, Loader2, MailCheck, Send } from 'lucide-react';

export default function SupplierRfqDetailPage() {
  const params = useParams();
  const router = useRouter();
  const rfqId = Array.isArray(params?.id) ? params.id[0] : params?.id ?? '';

  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [confirmSubmitOpen, setConfirmSubmitOpen] = useState(false);
  const [rfq, setRfq] = useState<RfqDetailDto | null>(null);

  const [notes, setNotes] = useState('');
  const [unitPriceByItemId, setUnitPriceByItemId] = useState<Record<string, string>>({});

  const myQuote = useMemo(() => (rfq?.quotes || [])[0] || null, [rfq]);
  const awardedItemIds = useMemo(() => {
    const ids = new Set<string>();
    for (const qi of myQuote?.items || []) {
      if (qi.isAwarded) ids.add(qi.rfqItemId);
    }
    return ids;
  }, [myQuote]);

  const awardSummary = useMemo(() => {
    if (!rfq || rfq.status !== 'Awarded') return null;
    const total = (rfq.items || []).length;
    const awarded = awardedItemIds.size;
    return { total, awarded };
  }, [rfq, awardedItemIds]);

  useEffect(() => {
    const load = async () => {
      try {
        setLoading(true);
        const data = await rfqService.openMyRfq(rfqId);
        setRfq(data);

        // If supplier already submitted a quote, prefill their last values (supplier portal endpoint returns only their quote).
        const existingQuote = (data.quotes || [])[0];
        setNotes(existingQuote?.notes || '');

        const priceMap: Record<string, string> = {};
        for (const item of data.items || []) {
          const existing = existingQuote?.items?.find((qi) => qi.rfqItemId === item.id);
          priceMap[item.id] = existing ? String(existing.unitPrice) : '';
        }
        setUnitPriceByItemId(priceMap);
      } catch (e: any) {
        console.error(e);
        toast.error(e.message || 'Failed to load RFQ');
      } finally {
        setLoading(false);
      }
    };

    if (rfqId) load();
  }, [rfqId]);

  const totals = useMemo(() => {
    if (!rfq) return { total: 0, missing: 0 };
    let total = 0;
    let missing = 0;

    for (const item of rfq.items || []) {
      const raw = (unitPriceByItemId[item.id] ?? '').trim();
      const price = raw === '' ? NaN : Number(raw);
      if (!Number.isFinite(price) || price < 0) {
        missing++;
        continue;
      }
      total += price * Number(item.quantity);
    }

    return { total, missing };
  }, [rfq, unitPriceByItemId]);

  const submissionOpen = isQuoteSubmissionOpen(rfq?.status, rfq?.submissionDeadline);
  const hasSubmittedQuote = !!myQuote && myQuote.status.toLowerCase() === 'submitted';
  const canSubmit = submissionOpen && myQuote?.status.toLowerCase() !== 'laterejected';
  const submitLabel = getQuoteSubmitLabel(hasSubmittedQuote, canSubmit);

  const handleSubmit = async () => {
    if (!rfq) return;

    try {
      setSubmitting(true);

      const items = (rfq.items || []).map((i) => ({
        rfqItemId: i.id,
        unitPrice: Number((unitPriceByItemId[i.id] ?? '').trim()),
      }));

      if (items.some((x) => !Number.isFinite(x.unitPrice) || x.unitPrice < 0)) {
        toast.error('Please enter a valid unit price for every line item.');
        return;
      }

      await rfqService.submitQuote(rfq.id, { notes: notes || undefined, items });
      toast.success(hasSubmittedQuote ? 'Quote updated and resubmitted successfully' : 'Quote submitted successfully');

      const refreshed = await rfqService.getMyRfqDetail(rfq.id);
      setRfq(refreshed);
    } catch (e: any) {
      console.error(e);
      toast.error(e.message || 'Failed to submit quote');
    } finally {
      setSubmitting(false);
      setConfirmSubmitOpen(false);
    }
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center h-96">
        <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (!rfq) {
    return (
      <div className="space-y-6">
        <Button variant="outline" onClick={() => router.push('/external-portal/rfqs')}>
          <ArrowLeft className="w-4 h-4 mr-2" />
          Back
        </Button>
        <Card>
          <CardContent className="pt-6">
            <p className="text-sm text-muted-foreground">RFQ not found.</p>
          </CardContent>
        </Card>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between gap-4">
        <div className="flex items-center gap-3 min-w-0">
          <Button variant="outline" onClick={() => router.push('/external-portal/rfqs')}>
            <ArrowLeft className="w-4 h-4 mr-2" />
            Back
          </Button>
          <div className="min-w-0">
            <div className="flex items-center gap-2">
              <FileText className="h-4 w-4 text-muted-foreground" />
              <h1 className="text-xl font-semibold truncate">{rfq.rfqNumber}</h1>
              <Badge variant="outline">{rfq.supplierStatus || rfq.status}</Badge>
            </div>
            <div className="text-sm text-muted-foreground truncate">{rfq.title}</div>
          </div>
        </div>

        <div className="flex items-center gap-2">
          <Button
            variant="outline"
            onClick={() =>
              router.push(`/external-portal/rfqs/${rfqId}/award-status`)
            }
          >
            <MailCheck className="h-4 w-4 mr-2" />
            Award status
          </Button>
          <Button
            onClick={() => setConfirmSubmitOpen(true)}
            disabled={!canSubmit || submitting}
          >
            {submitting ? <Loader2 className="h-4 w-4 mr-2 animate-spin" /> : <Send className="h-4 w-4 mr-2" />}
            {submitLabel}
          </Button>
        </div>
      </div>

      {awardSummary && (
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-start justify-between gap-4">
              <div className="space-y-1">
                <div className="font-medium">Award Result</div>
                {awardSummary.awarded === awardSummary.total ? (
                  <div className="text-sm text-muted-foreground">All RFQ line(s) were awarded to you.</div>
                ) : awardSummary.awarded === 0 ? (
                  <div className="text-sm text-muted-foreground">None of the RFQ line(s) were awarded to you.</div>
                ) : (
                  <div className="text-sm text-muted-foreground">
                    {awardSummary.awarded} of {awardSummary.total} RFQ line(s) were awarded to you.
                  </div>
                )}
              </div>
              <Badge className={
                awardSummary.awarded === awardSummary.total
                  ? 'bg-green-100 text-green-800'
                  : awardSummary.awarded === 0
                    ? 'bg-gray-100 text-gray-800'
                    : 'bg-amber-100 text-amber-800'
              }>
                {awardSummary.awarded === awardSummary.total ? 'Awarded' : awardSummary.awarded === 0 ? 'Not Awarded' : 'Partially Awarded'}
              </Badge>
            </div>
          </CardContent>
        </Card>
      )}

      {myQuote && (
        <Card className={myQuote.status === 'LateRejected' ? 'border-red-200' : 'border-green-200'}>
          <CardContent className="pt-6">
            <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
              <div className="flex items-start gap-3">
                {myQuote.status === 'LateRejected' ? (
                  <Clock3 className="mt-0.5 h-5 w-5 text-red-600" />
                ) : (
                  <CheckCircle2 className="mt-0.5 h-5 w-5 text-green-600" />
                )}
                <div className="space-y-1">
                  <div className="font-medium">Your quotation has been recorded</div>
                  <div className="text-sm text-muted-foreground">
                    Reference {myQuote.id} · Revision {Math.max(1, myQuote.revisionNumber || 1)}
                  </div>
                  <div className="text-sm text-muted-foreground">
                    Latest submission: {myQuote.submittedAt ? new Date(myQuote.submittedAt).toLocaleString() : 'Not available'}
                  </div>
                  {canSubmit ? (
                    <div className="text-sm text-muted-foreground">
                      You may update and resubmit this same quotation before the deadline. Earlier revisions remain in History.
                    </div>
                  ) : (
                    <div className="text-sm text-muted-foreground">
                      Submission is closed. This quotation is retained as the final recorded version.
                    </div>
                  )}
                </div>
              </div>
              <Badge variant={myQuote.status === 'LateRejected' ? 'destructive' : 'default'}>{myQuote.status}</Badge>
            </div>
          </CardContent>
        </Card>
      )}

      <Tabs defaultValue="quote" className="space-y-4">
        <TabsList>
          <TabsTrigger value="quote">
            <FileText className="mr-2 h-4 w-4" />
            Quote
          </TabsTrigger>
          <TabsTrigger value="history">
            <History className="mr-2 h-4 w-4" />
            History ({myQuote?.history?.length || 0})
          </TabsTrigger>
        </TabsList>

        <TabsContent value="quote" className="mt-0">
          <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <Card className="lg:col-span-2">
          <CardHeader>
            <CardTitle className="text-base">Items</CardTitle>
            <CardDescription>Enter your unit price for each item.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="overflow-auto rounded-md border">
              <table className="w-full text-sm">
                <thead className="bg-muted/40">
                  <tr>
                    <th className="text-left p-2 w-12">#</th>
                    <th className="text-left p-2 w-28">Code</th>
                    <th className="text-left p-2">Description</th>
                    <th className="text-right p-2 w-24">Qty</th>
                    <th className="text-left p-2 w-20">UOM</th>
                    <th className="text-right p-2 w-40">Unit Price</th>
                    <th className="text-right p-2 w-40">Line Total</th>
                    {rfq.status === 'Awarded' && <th className="text-left p-2 w-32">Award</th>}
                  </tr>
                </thead>
                <tbody>
                  {rfq.items.map((i) => {
                    const raw = (unitPriceByItemId[i.id] ?? '').trim();
                    const price = raw === '' ? NaN : Number(raw);
                    const lineTotal = Number.isFinite(price) ? price * Number(i.quantity) : NaN;
                    return (
                      <tr key={i.id} className="border-t">
                        <td className="p-2">{i.lineNumber}</td>
                        <td className="p-2">{i.itemCode || ''}</td>
                        <td className="p-2">{i.description}</td>
                        <td className="p-2 text-right">{Number(i.quantity).toLocaleString()}</td>
                        <td className="p-2">{i.unitOfMeasure}</td>
                        <td className="p-2">
                          <Input
                            inputMode="decimal"
                            className="text-right"
                            placeholder="0.00"
                            value={unitPriceByItemId[i.id] ?? ''}
                            onChange={(e) => setUnitPriceByItemId((prev) => ({ ...prev, [i.id]: e.target.value }))}
                            disabled={!canSubmit || submitting}
                          />
                        </td>
                        <td className="p-2 text-right">
                          {Number.isFinite(lineTotal) ? lineTotal.toLocaleString(undefined, { maximumFractionDigits: 2 }) : '—'}
                        </td>
                        {rfq.status === 'Awarded' && (
                          <td className="p-2">
                            {awardedItemIds.has(i.id) ? (
                              <Badge className="bg-green-100 text-green-800">Awarded</Badge>
                            ) : (
                              <Badge variant="outline">Not Awarded</Badge>
                            )}
                          </td>
                        )}
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>

            <div className="flex items-center justify-between text-sm">
              <span className="text-muted-foreground">
                {totals.missing > 0 ? `${totals.missing} line(s) missing a unit price` : 'All lines priced'}
              </span>
              <span className="font-medium">
                Total: {totals.total.toLocaleString(undefined, { maximumFractionDigits: 2 })}
              </span>
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-base">Notes (Optional)</CardTitle>
            <CardDescription>Any notes or assumptions for your quotation.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-2">
            <Textarea
              rows={10}
              value={notes}
              onChange={(e) => setNotes(e.target.value)}
              disabled={!canSubmit || submitting}
              placeholder="Delivery lead time, warranty, payment terms, etc."
            />
            <Separator />
            <div className="text-xs text-muted-foreground">
              Deadline: {rfq.submissionDeadline ? new Date(rfq.submissionDeadline).toLocaleString() : 'N/A'}
            </div>
          </CardContent>
        </Card>
          </div>
        </TabsContent>

        <TabsContent value="history" className="mt-0">
          <Card>
            <CardHeader>
              <CardTitle className="text-base">Quotation history</CardTitle>
              <CardDescription>
                Each submission and pre-deadline revision is retained with its actor, date, time, status, and value.
              </CardDescription>
            </CardHeader>
            <CardContent>
              {!myQuote || !myQuote.history?.length ? (
                <div className="rounded-md border border-dashed p-8 text-center text-sm text-muted-foreground">
                  No quotation has been submitted yet.
                </div>
              ) : (
                <div className="space-y-3">
                  {myQuote.history.map((entry) => (
                    <div key={entry.id} className="rounded-md border p-4">
                      <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
                        <div className="space-y-1">
                          <div className="flex flex-wrap items-center gap-2">
                            <span className="font-medium">Revision {entry.revisionNumber}</span>
                            <Badge variant="outline">{entry.action}</Badge>
                            <Badge variant={entry.status === 'LateRejected' ? 'destructive' : 'secondary'}>
                              {entry.status}
                            </Badge>
                          </div>
                          <div className="text-sm text-muted-foreground">
                            {entry.performedBy} · {new Date(entry.timestamp).toLocaleString()}
                          </div>
                          {entry.description && (
                            <div className="text-sm text-muted-foreground">{entry.description}</div>
                          )}
                        </div>
                        <div className="font-medium">
                          {rfq.currency} {Number(entry.totalAmount).toLocaleString(undefined, {
                            minimumFractionDigits: 2,
                            maximumFractionDigits: 2,
                          })}
                        </div>
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>

      <ConfirmationDialog
        open={confirmSubmitOpen}
        onOpenChange={setConfirmSubmitOpen}
        title={hasSubmittedQuote ? 'Update and resubmit quotation?' : 'Submit quotation?'}
        description={hasSubmittedQuote
          ? 'This updates the same quotation and line records. The previous version remains available in History. Revisions are accepted only before the RFQ deadline.'
          : 'This records your quotation. You may revise the same quotation before the RFQ deadline, and every revision will be retained in History.'}
        confirmText={hasSubmittedQuote ? 'Update & Resubmit' : 'Submit'}
        onConfirm={handleSubmit}
      />
    </div>
  );
}
