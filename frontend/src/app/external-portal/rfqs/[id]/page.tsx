'use client';

import { useEffect, useMemo, useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import { Separator } from '@/components/ui/separator';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { rfqService, type RfqDetailDto } from '@/services/rfqService';
import { toast } from 'sonner';
import { ArrowLeft, Loader2, Send, FileText } from 'lucide-react';

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
        const data = await rfqService.getMyRfqDetail(rfqId);
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

  const canSubmit = !!rfq && rfq.status === 'Sent';

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
      toast.success('Quote submitted successfully');

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
              <Badge variant="outline">{rfq.status}</Badge>
            </div>
            <div className="text-sm text-muted-foreground truncate">{rfq.title}</div>
          </div>
        </div>

        <div className="flex items-center gap-2">
          <Button
            onClick={() => setConfirmSubmitOpen(true)}
            disabled={!canSubmit || submitting}
          >
            {submitting ? <Loader2 className="h-4 w-4 mr-2 animate-spin" /> : <Send className="h-4 w-4 mr-2" />}
            Submit Quote
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

      <ConfirmationDialog
        open={confirmSubmitOpen}
        onOpenChange={setConfirmSubmitOpen}
        title="Submit Quote?"
        description="Once submitted, your quote will be recorded. You can submit again to replace your previous quote if the RFQ is still open."
        confirmText="Submit"
        onConfirm={handleSubmit}
      />
    </div>
  );
}
