'use client';

import { useEffect, useMemo, useState } from 'react';
import Link from 'next/link';
import { useParams, useRouter, useSearchParams } from 'next/navigation';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import { Badge } from '@/components/ui/badge';
import { Separator } from '@/components/ui/separator';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { rfqService, type CreatePurchaseOrdersFromRfqResponseDto, type RfqDetailDto } from '@/services/rfqService';
import { businessPartnerService, type BusinessPartnerDto } from '@/services/businessPartnerService';
import { purchasingService, type SuggestedSupplierDto } from '@/services/purchasingService';
import { toast } from 'sonner';
import { ArrowLeft, CheckCircle2, FileText, Loader2, LockKeyhole, Printer, Send, Save, Users, Mail, Package } from 'lucide-react';

export default function EditRfqPage() {
  const params = useParams();
  const router = useRouter();
  const searchParams = useSearchParams();
  const rfqId = Array.isArray(params?.id) ? params.id[0] : params?.id ?? '';
  const fromRequisitionId = searchParams?.get('fromRequisitionId') ?? null;

  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [sending, setSending] = useState(false);
  const [confirmSendOpen, setConfirmSendOpen] = useState(false);

  const [rfq, setRfq] = useState<RfqDetailDto | null>(null);
  const [quoteOpen, setQuoteOpen] = useState(false);
  const [selectedQuoteId, setSelectedQuoteId] = useState<string | null>(null);

  const [awardMode, setAwardMode] = useState<'WinnerTakesAll' | 'SplitAward'>('WinnerTakesAll');
  const [winnerQuoteId, setWinnerQuoteId] = useState<string | null>(null);
  const [splitAwardByItemId, setSplitAwardByItemId] = useState<Record<string, string>>({});
  const [splitAwardReasonByItemId, setSplitAwardReasonByItemId] = useState<Record<string, string>>({});
  const [awarding, setAwarding] = useState(false);
  const [confirmAwardOpen, setConfirmAwardOpen] = useState(false);

  const [title, setTitle] = useState('');
  const [description, setDescription] = useState('');
  const [deadline, setDeadline] = useState('');
  const [currency, setCurrency] = useState('USD');
  const [estimatedValue, setEstimatedValue] = useState<string>('');

  const [externalEmails, setExternalEmails] = useState('');

  const [partnersLoading, setPartnersLoading] = useState(false);
  const [partners, setPartners] = useState<BusinessPartnerDto[]>([]);
  const [partnerSearch, setPartnerSearch] = useState('');
  const [selectedSupplierIds, setSelectedSupplierIds] = useState<string[]>([]);
  const [suggestedSuppliers, setSuggestedSuppliers] = useState<SuggestedSupplierDto[]>([]);

  const filteredPartners = useMemo(() => {
    const list = partners.filter((p) => p.partnerType === 'Supplier' || p.partnerType === 'Both');
    if (!partnerSearch.trim()) return list;
    const s = partnerSearch.toLowerCase();
    return list.filter((p) => (p.partnerName || '').toLowerCase().includes(s) || (p.partnerCode || '').toLowerCase().includes(s) || (p.email || '').toLowerCase().includes(s));
  }, [partners, partnerSearch]);

  const selectedQuote = useMemo(() => {
    if (!rfq || !selectedQuoteId) return null;
    return (rfq.quotes || []).find((q) => q.id === selectedQuoteId) || null;
  }, [rfq, selectedQuoteId]);

  useEffect(() => {
    const load = async () => {
      try {
        setLoading(true);
        const data = await rfqService.getRfqById(rfqId);
        setRfq(data);
        setTitle(data.title || '');
        setDescription(data.description || '');
        setDeadline(data.submissionDeadline ? new Date(data.submissionDeadline).toISOString().slice(0, 16) : '');
        setCurrency(data.currency || 'USD');
        setEstimatedValue(String(data.estimatedValue ?? ''));
        setExternalEmails(data.externalRecipientEmails || '');
        setSelectedSupplierIds((data.suppliers || []).map((s) => s.businessPartnerId));

        // Default award selections (for convenience):
        // - Winner takes all: pick lowest total quote
        // - Split award: pick lowest unit price per line
        if (data.quoteDetailsVisible && (data.quotes || []).length > 0) {
          const sortedByTotal = [...data.quotes].sort((a, b) => (a.totalAmount ?? 0) - (b.totalAmount ?? 0));
          setWinnerQuoteId(sortedByTotal[0]?.id ?? null);

          const byItem: Record<string, string> = {};
          for (const item of data.items || []) {
            let bestQuoteId: string | null = null;
            let bestUnitPrice = Number.POSITIVE_INFINITY;
            for (const q of data.quotes || []) {
              const qi = (q.items || []).find((x) => x.rfqItemId === item.id);
              if (!qi) continue;
              const price = Number(qi.unitPrice ?? 0);
              if (price < bestUnitPrice) {
                bestUnitPrice = price;
                bestQuoteId = q.id;
              }
            }
            if (bestQuoteId) byItem[item.id] = bestQuoteId;
          }
          setSplitAwardByItemId(byItem);
          setSplitAwardReasonByItemId({});
        } else {
          setWinnerQuoteId(null);
          setSplitAwardByItemId({});
          setSplitAwardReasonByItemId({});
        }
      } catch (e: any) {
        console.error(e);
        toast.error(e.message || 'Failed to load RFQ');
      } finally {
        setLoading(false);
      }
    };
    if (rfqId) load();
  }, [rfqId]);

  useEffect(() => {
    const loadPartners = async () => {
      try {
        setPartnersLoading(true);
        const partnerResult = await businessPartnerService.getPartners({ page: 1, pageSize: 1000 });
        const list = partnerResult.items || [];

        let suggested: SuggestedSupplierDto[] = [];
        if (fromRequisitionId) {
          try {
            suggested = await purchasingService.getSuggestedSuppliersForRequisition(fromRequisitionId);
          } catch (e) {
            console.warn('Failed to load suggested suppliers:', e);
          }
        }
        setSuggestedSuppliers(suggested);

        const suggestedMap = new Map(suggested.map((s) => [s.supplierId.toLowerCase(), s]));
        const sorted = [...list].sort((a, b) => {
          const sa = suggestedMap.get(a.id.toLowerCase());
          const sb = suggestedMap.get(b.id.toLowerCase());
          if (sa && !sb) return -1;
          if (!sa && sb) return 1;
          if (sa && sb) {
            if (sb.preferredItemCount !== sa.preferredItemCount) return sb.preferredItemCount - sa.preferredItemCount;
            if (sb.itemMatchCount !== sa.itemMatchCount) return sb.itemMatchCount - sa.itemMatchCount;
          }
          return (a.partnerName || '').localeCompare(b.partnerName || '');
        });

        setPartners(sorted);
      } catch (e: any) {
        console.error(e);
        toast.error(e.message || 'Failed to load suppliers');
      } finally {
        setPartnersLoading(false);
      }
    };
    loadPartners();
     
  }, [fromRequisitionId]);

  const toggleSupplier = (id: string) => {
    setSelectedSupplierIds((prev) => (prev.includes(id) ? prev.filter((x) => x !== id) : [...prev, id]));
  };

  const handleSave = async () => {
    if (!rfq) return;
    try {
      setSaving(true);
      const updated = await rfqService.updateRfq(rfq.id, {
        title,
        description,
        submissionDeadline: deadline ? new Date(deadline).toISOString() : undefined,
        currency,
        estimatedValue: estimatedValue ? Number(estimatedValue) : undefined,
        supplierIds: selectedSupplierIds,
        externalRecipientEmails: externalEmails,
      });
      setRfq(updated);
      toast.success('RFQ saved');
    } catch (e: any) {
      console.error(e);
      toast.error(e.message || 'Failed to save RFQ');
    } finally {
      setSaving(false);
    }
  };

  const handleSend = async () => {
    if (!rfq) return;
    try {
      setSending(true);
      await rfqService.sendRfq(rfq.id, {
        supplierIds: selectedSupplierIds,
        externalRecipientEmails: externalEmails || undefined,
      });
      const refreshed = await rfqService.getRfqById(rfq.id);
      setRfq(refreshed);
      toast.success('RFQ sent to suppliers');
    } catch (e: any) {
      console.error(e);
      toast.error(e.message || 'Failed to send RFQ');
    } finally {
      setSending(false);
      setConfirmSendOpen(false);
    }
  };

  const handlePrintPdf = async () => {
    try {
      const blob = await rfqService.getRfqPdf(rfqId);
      const url = window.URL.createObjectURL(blob);
      window.open(url, '_blank');
    } catch (e: any) {
      console.error(e);
      toast.error(e.message || 'Failed to generate RFQ PDF');
    }
  };

  const openQuote = (quoteId: string) => {
    if (!rfq?.quoteDetailsVisible) {
      toast.info('Quotation prices remain sealed until the submission deadline or controlled opening.');
      return;
    }
    setSelectedQuoteId(quoteId);
    setQuoteOpen(true);
  };

  const canAward = rfq?.quoteDetailsVisible === true && rfq?.status === 'Sent' && (rfq?.quotes || []).length > 0;

  const isSplitComplete = useMemo(() => {
    if (!rfq) return false;
    const items = rfq.items || [];
    if (items.length === 0) return false;
    return items.every((i) => Boolean(splitAwardByItemId[i.id]));
  }, [rfq, splitAwardByItemId]);

  const handleAward = async () => {
    if (!rfq) return;
    try {
      setAwarding(true);

      let result: CreatePurchaseOrdersFromRfqResponseDto;
      if (awardMode === 'WinnerTakesAll') {
        if (!winnerQuoteId) {
          toast.error('Please select a winning quote.');
          return;
        }
        result = await rfqService.awardAndCreatePurchaseOrders(rfq.id, {
          mode: 'WinnerTakesAll',
          quoteId: winnerQuoteId,
        });
      } else {
        if (!isSplitComplete) {
          toast.error('Please select a supplier quote for every item.');
          return;
        }
        result = await rfqService.awardAndCreatePurchaseOrders(rfq.id, {
          mode: 'SplitAward',
          lines: (rfq.items || []).map((i) => ({
            rfqItemId: i.id,
            quoteId: splitAwardByItemId[i.id],
            awardReason: splitAwardReasonByItemId[i.id] || undefined,
          })),
        });
      }

      const created = result.purchaseOrders || [];
      if (created.length === 0) {
        toast.success('Award saved.');
      } else {
        const nums = created.map((x) => x.orderNumber).filter(Boolean).join(', ');
        toast.success(created.length === 1 ? `PO created: ${nums}` : `POs created: ${nums}`);
      }

      const refreshed = await rfqService.getRfqById(rfq.id);
      setRfq(refreshed);
    } catch (e: any) {
      console.error(e);
      toast.error(e.message || 'Failed to award RFQ');
    } finally {
      setAwarding(false);
      setConfirmAwardOpen(false);
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
        <Button variant="outline" onClick={() => router.push('/procurement/rfqs')}>
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

  const isSent = rfq.status === 'Sent';

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between gap-4">
        <div className="flex items-center gap-3">
          <Button variant="outline" onClick={() => router.push('/procurement/rfqs')}>
            <ArrowLeft className="w-4 h-4 mr-2" />
            Back
          </Button>
          <div className="min-w-0">
            <div className="flex items-center gap-2">
              <h1 className="text-xl font-semibold truncate">{rfq.rfqNumber}</h1>
              <Badge variant="outline">{rfq.status}</Badge>
              {rfq.sourcePurchaseRequisitionId && (
                <Badge variant="secondary">
                  From PR:{' '}
                  <Link className="underline" href={`/procurement/purchase-requisitions/${rfq.sourcePurchaseRequisitionId}`}>
                    View
                  </Link>
                </Badge>
              )}
            </div>
            <div className="text-sm text-muted-foreground truncate">{rfq.title}</div>
          </div>
        </div>

        <div className="flex items-center gap-2">
          <Button asChild variant="outline">
            <Link href={`/procurement/rfqs/${rfqId}/document-controls`}>
              <FileText className="h-4 w-4 mr-2" />
              Document register
            </Link>
          </Button>
          <Button asChild variant="outline">
            <Link href={`/procurement/rfqs/${rfqId}/committee-controls`}>
              <Users className="h-4 w-4 mr-2" />
              Committee controls
            </Link>
          </Button>
          <Button onClick={handleSave} disabled={saving || sending || isSent} variant="outline">
            {saving ? <Loader2 className="h-4 w-4 mr-2 animate-spin" /> : <Save className="h-4 w-4 mr-2" />}
            Save
          </Button>
          <Button onClick={handlePrintPdf} disabled={saving || sending} variant="outline">
            <Printer className="h-4 w-4 mr-2" />
            Print PDF
          </Button>
          <Button onClick={() => setConfirmSendOpen(true)} disabled={sending || selectedSupplierIds.length === 0 && !externalEmails.trim()}>
            {sending ? <Loader2 className="h-4 w-4 mr-2 animate-spin" /> : <Send className="h-4 w-4 mr-2" />}
            Send RFQ
          </Button>
        </div>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <Card className="lg:col-span-2">
          <CardHeader>
            <CardTitle className="text-base">RFQ Details</CardTitle>
            <CardDescription>Internal details for this request.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              <div className="space-y-2">
                <label className="text-sm font-medium">Title</label>
                <Input value={title} onChange={(e) => setTitle(e.target.value)} disabled={isSent} />
              </div>
              <div className="space-y-2">
                <label className="text-sm font-medium">Submission Deadline</label>
                <Input type="datetime-local" value={deadline} onChange={(e) => setDeadline(e.target.value)} disabled={isSent} />
              </div>
              <div className="space-y-2">
                <label className="text-sm font-medium">Currency</label>
                <Input value={currency} onChange={(e) => setCurrency(e.target.value)} disabled={isSent} />
              </div>
              <div className="space-y-2">
                <label className="text-sm font-medium">Estimated Value</label>
                <Input value={estimatedValue} onChange={(e) => setEstimatedValue(e.target.value)} disabled={isSent} />
              </div>
            </div>
            <div className="space-y-2">
              <label className="text-sm font-medium">Description</label>
              <Textarea value={description} onChange={(e) => setDescription(e.target.value)} rows={4} disabled={isSent} />
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-base flex items-center gap-2">
              <Mail className="h-4 w-4" />
              External Recipients
            </CardTitle>
            <CardDescription>Optional email-only recipients (no portal access).</CardDescription>
          </CardHeader>
          <CardContent className="space-y-2">
            <Textarea
              value={externalEmails}
              onChange={(e) => setExternalEmails(e.target.value)}
              rows={6}
              placeholder="supplierA@example.com; supplierB@example.com"
              disabled={isSent}
            />
            <p className="text-xs text-muted-foreground">
              You can separate emails with comma, semicolon, or new line.
            </p>
          </CardContent>
        </Card>
      </div>

      <Card>
        <CardHeader>
          <CardTitle className="text-base flex items-center gap-2">
            <Users className="h-4 w-4" />
            Suppliers
          </CardTitle>
          <CardDescription>
            Select suppliers to invite. Suggested suppliers (from PR item mappings) appear first.
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="flex items-center gap-2">
            <Input value={partnerSearch} onChange={(e) => setPartnerSearch(e.target.value)} placeholder="Search suppliers..." />
            <Badge variant="secondary">Selected: {selectedSupplierIds.length}</Badge>
            {suggestedSuppliers.length > 0 && (
              <Badge variant="outline">Suggested: {suggestedSuppliers.length}</Badge>
            )}
          </div>

          {partnersLoading ? (
            <div className="flex items-center justify-center py-8">
              <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
            </div>
          ) : (
            <div className="max-h-[420px] overflow-auto rounded-md border">
              <div className="divide-y">
                {filteredPartners.map((p) => {
                  const checked = selectedSupplierIds.includes(p.id);
                  return (
                    <button
                      type="button"
                      key={p.id}
                      onClick={() => toggleSupplier(p.id)}
                      className={`w-full text-left px-4 py-3 hover:bg-muted/50 flex items-start justify-between gap-3 ${checked ? 'bg-muted' : ''}`}
                      disabled={isSent}
                    >
                      <div className="min-w-0">
                        <div className="font-medium truncate">
                          {p.partnerName} <span className="text-muted-foreground">({p.partnerCode})</span>
                        </div>
                        <div className="text-xs text-muted-foreground truncate">
                          {p.email || p.phone || ''}
                        </div>
                        <div className="text-xs text-muted-foreground truncate">
                          Status: {p.approvalStatus || p.status || '—'}
                        </div>
                      </div>
                      <div className="shrink-0">
                        <Badge variant={checked ? 'default' : 'outline'}>{checked ? 'Selected' : 'Select'}</Badge>
                      </div>
                    </button>
                  );
                })}
              </div>
            </div>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base flex items-center gap-2">
            <Package className="h-4 w-4" />
            Items
          </CardTitle>
          <CardDescription>These items were copied from the approved Purchase Requisition.</CardDescription>
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
                </tr>
              </thead>
              <tbody>
                {rfq.items.map((i) => (
                  <tr key={i.id} className="border-t">
                    <td className="p-2">{i.lineNumber}</td>
                    <td className="p-2">{i.itemCode || ''}</td>
                    <td className="p-2">{i.description}</td>
                    <td className="p-2 text-right">{Number(i.quantity).toLocaleString()}</td>
                    <td className="p-2">{i.unitOfMeasure}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {rfq.quotes?.length > 0 && (
            <>
              <Separator />
              <div className="space-y-3">
                <div className="text-sm font-medium">Quotes</div>
                {!rfq.quoteDetailsVisible && (
                  <div className="flex items-start gap-3 rounded-md border border-amber-200 bg-amber-50 p-4 text-amber-950">
                    <LockKeyhole className="mt-0.5 h-4 w-4 shrink-0" />
                    <div>
                      <div className="font-medium">Quotation prices are sealed</div>
                      <div className="mt-1 text-sm">
                        Supplier submissions are recorded, but commercial values remain hidden until the deadline or controlled opening. Selection and award are disabled until then.
                      </div>
                    </div>
                  </div>
                )}
                <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
                  {rfq.quotes.map((q) => (
                    <Card key={q.id}>
                      <CardHeader className="pb-2">
                        <CardTitle className="text-sm">{q.partnerName}</CardTitle>
                        <CardDescription>
                          {q.status}
                          {q.submittedAt ? ` · ${new Date(q.submittedAt).toLocaleString()}` : ''}
                        </CardDescription>
                      </CardHeader>
                      <CardContent className="text-sm">
                        <div className="flex items-center justify-between">
                          <span>Total</span>
                          <span className="font-medium">
                            {rfq.quoteDetailsVisible
                              ? `${q.totalAmount.toLocaleString(undefined, { maximumFractionDigits: 2 })} ${rfq.currency}`
                              : 'Sealed until opening'}
                          </span>
                        </div>
                        <div className="mt-3 flex items-center justify-between gap-2">
                          {canAward && awardMode === 'WinnerTakesAll' ? (
                            <Button
                              type="button"
                              size="sm"
                              variant={winnerQuoteId === q.id ? 'default' : 'outline'}
                              onClick={() => setWinnerQuoteId(q.id)}
                              className="shrink-0"
                            >
                              <CheckCircle2 className="h-4 w-4 mr-2" />
                              {winnerQuoteId === q.id ? 'Selected' : 'Select Winner'}
                            </Button>
                          ) : (
                            <span />
                          )}

                          <Button
                            type="button"
                            variant="outline"
                            size="sm"
                            onClick={() => openQuote(q.id)}
                            disabled={!rfq.quoteDetailsVisible}
                          >
                            View Quote
                          </Button>
                        </div>
                      </CardContent>
                    </Card>
                  ))}
                </div>
              </div>

              <Separator />

              <Card className="border-dashed">
                <CardHeader className="pb-3">
                  <CardTitle className="text-base">Award RFQ &amp; Create Purchase Order(s)</CardTitle>
                  <CardDescription>
                    Choose <span className="font-medium">Winner Takes All</span> to create one PO, or <span className="font-medium">Split Award</span> to create one PO per supplier based on selected lines.
                  </CardDescription>
                </CardHeader>
                <CardContent className="space-y-4">
                  {!canAward ? (
                    <div className="text-sm text-muted-foreground">
                      {rfq.status === 'Awarded'
                        ? 'This RFQ has already been awarded.'
                        : !rfq.quoteDetailsVisible
                          ? 'Quotation prices are sealed until the deadline or controlled opening.'
                        : rfq.status !== 'Sent'
                          ? 'RFQ must be Sent before you can award it.'
                          : 'No submitted quotes available.'}
                    </div>
                  ) : (
                    <>
                      <div className="flex flex-col md:flex-row md:items-center md:justify-between gap-3">
                        <div className="flex items-center gap-2">
                          <Button
                            type="button"
                            variant={awardMode === 'WinnerTakesAll' ? 'default' : 'outline'}
                            size="sm"
                            onClick={() => setAwardMode('WinnerTakesAll')}
                          >
                            Winner Takes All
                          </Button>
                          <Button
                            type="button"
                            variant={awardMode === 'SplitAward' ? 'default' : 'outline'}
                            size="sm"
                            onClick={() => setAwardMode('SplitAward')}
                          >
                            Split Award
                          </Button>
                        </div>

                        <Button
                          type="button"
                          onClick={() => setConfirmAwardOpen(true)}
                          disabled={awarding || (awardMode === 'WinnerTakesAll' ? !winnerQuoteId : !isSplitComplete)}
                        >
                          {awarding ? <Loader2 className="h-4 w-4 mr-2 animate-spin" /> : null}
                          Create PO(s)
                        </Button>
                      </div>

                      {awardMode === 'WinnerTakesAll' ? (
                        <p className="text-sm text-muted-foreground">
                          Select the winning supplier using the <span className="font-medium">Select Winner</span> button on a quote card.
                        </p>
                      ) : (
                        <div className="overflow-auto rounded-md border">
                          <table className="w-full text-sm">
                            <thead className="bg-muted/40">
                              <tr>
                                <th className="text-left p-2 w-12">#</th>
                                <th className="text-left p-2">Item</th>
                                <th className="text-right p-2 w-24">Qty</th>
                                <th className="text-left p-2 w-20">UOM</th>
                                <th className="text-left p-2 w-64">Award To</th>
                                <th className="text-left p-2 w-72">Reason (optional)</th>
                                <th className="text-right p-2 w-32">Unit Cost</th>
                                <th className="text-right p-2 w-32">Line Total</th>
                              </tr>
                            </thead>
                            <tbody>
                              {(rfq.items || []).map((item) => {
                                const selectedQuote = (rfq.quotes || []).find((q) => q.id === splitAwardByItemId[item.id]);
                                const selectedQuoteItem = selectedQuote?.items?.find((x) => x.rfqItemId === item.id);
                                const unitPrice = Number(selectedQuoteItem?.unitPrice ?? 0);
                                const lineTotal = unitPrice * Number(item.quantity ?? 0);

                                return (
                                  <tr key={item.id} className="border-t">
                                    <td className="p-2">{item.lineNumber}</td>
                                    <td className="p-2">
                                      <div className="font-medium">{item.description}</div>
                                      <div className="text-xs text-muted-foreground">{item.itemCode || ''}</div>
                                    </td>
                                    <td className="p-2 text-right">{Number(item.quantity).toLocaleString()}</td>
                                    <td className="p-2">{item.unitOfMeasure}</td>
                                    <td className="p-2">
                                      <Select
                                        value={splitAwardByItemId[item.id] || ''}
                                        onValueChange={(v) => setSplitAwardByItemId((prev) => ({ ...prev, [item.id]: v }))}
                                      >
                                        <SelectTrigger className="h-9">
                                          <SelectValue placeholder="Select supplier..." />
                                        </SelectTrigger>
                                        <SelectContent>
                                          {(rfq.quotes || []).map((q) => {
                                            const qi = (q.items || []).find((x) => x.rfqItemId === item.id);
                                            const price = qi ? Number(qi.unitPrice ?? 0) : 0;
                                            return (
                                              <SelectItem key={q.id} value={q.id}>
                                                {q.partnerName} · {price.toLocaleString(undefined, { maximumFractionDigits: 2 })} {rfq.currency}
                                              </SelectItem>
                                            );
                                          })}
                                        </SelectContent>
                                      </Select>
                                    </td>
                                    <td className="p-2">
                                      <Input
                                        value={splitAwardReasonByItemId[item.id] || ''}
                                        onChange={(e) =>
                                          setSplitAwardReasonByItemId((prev) => ({
                                            ...prev,
                                            [item.id]: e.target.value,
                                          }))
                                        }
                                        disabled={!splitAwardByItemId[item.id]}
                                        placeholder={splitAwardByItemId[item.id] ? 'Type a reason…' : 'Select supplier first'}
                                        className="h-9"
                                      />
                                    </td>
                                    <td className="p-2 text-right">{unitPrice.toLocaleString(undefined, { maximumFractionDigits: 2 })}</td>
                                    <td className="p-2 text-right">{lineTotal.toLocaleString(undefined, { maximumFractionDigits: 2 })}</td>
                                  </tr>
                                );
                              })}
                            </tbody>
                          </table>
                        </div>
                      )}
                    </>
                  )}
                </CardContent>
              </Card>
            </>
          )}
        </CardContent>
      </Card>

      <ConfirmationDialog
        open={confirmSendOpen}
        onOpenChange={setConfirmSendOpen}
        title="Send RFQ?"
        description="This will notify selected suppliers (and external recipients) and set the RFQ status to Sent."
        confirmText="Send"
        onConfirm={handleSend}
      />

      <ConfirmationDialog
        open={confirmAwardOpen}
        onOpenChange={setConfirmAwardOpen}
        title="Create Purchase Order(s)?"
        description={
          awardMode === 'WinnerTakesAll'
            ? 'This will award the RFQ to the selected supplier and create a Purchase Order from their quote.'
            : 'This will award each item to its selected supplier and create one Purchase Order per supplier.'
        }
        confirmText="Create PO(s)"
        onConfirm={handleAward}
      />

      <Dialog open={quoteOpen} onOpenChange={setQuoteOpen}>
        <DialogContent className="max-w-4xl max-h-[85vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Supplier Quote</DialogTitle>
            <DialogDescription>
              {selectedQuote ? (
                <span>
                  {selectedQuote.partnerName} ({selectedQuote.partnerCode}) · {selectedQuote.status}
                  {selectedQuote.submittedAt ? ` · ${new Date(selectedQuote.submittedAt).toLocaleString()}` : ''}
                </span>
              ) : (
                'Quote details'
              )}
            </DialogDescription>
          </DialogHeader>

          {!selectedQuote ? (
            <div className="text-sm text-muted-foreground">Quote not found.</div>
          ) : (
            <div className="space-y-4">
              {selectedQuote.notes && (
                <Card>
                  <CardHeader className="pb-2">
                    <CardTitle className="text-sm">Notes</CardTitle>
                  </CardHeader>
                  <CardContent className="text-sm whitespace-pre-wrap">
                    {selectedQuote.notes}
                  </CardContent>
                </Card>
              )}

              <div className="overflow-auto rounded-md border">
                <table className="w-full text-sm">
                  <thead className="bg-muted/40">
                    <tr>
                      <th className="text-left p-2 w-12">#</th>
                      <th className="text-left p-2">Description</th>
                      <th className="text-right p-2 w-24">Qty</th>
                      <th className="text-left p-2 w-20">UOM</th>
                      <th className="text-right p-2 w-32">Unit Price</th>
                      <th className="text-right p-2 w-32">Line Total</th>
                    </tr>
                  </thead>
                  <tbody>
                    {(selectedQuote.items || []).map((i) => (
                      <tr key={i.rfqItemId} className="border-t">
                        <td className="p-2">{i.lineNumber}</td>
                        <td className="p-2">{i.description}</td>
                        <td className="p-2 text-right">{Number(i.quantity).toLocaleString()}</td>
                        <td className="p-2">{i.unitOfMeasure}</td>
                        <td className="p-2 text-right">
                          {Number(i.unitPrice).toLocaleString(undefined, { maximumFractionDigits: 2 })}
                        </td>
                        <td className="p-2 text-right">
                          {Number(i.lineTotal).toLocaleString(undefined, { maximumFractionDigits: 2 })}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>

              <div className="flex justify-end text-sm">
                <div className="flex items-center gap-3">
                  <span className="text-muted-foreground">Total:</span>
                  <span className="font-medium">
                    {selectedQuote.totalAmount.toLocaleString(undefined, { maximumFractionDigits: 2 })} {rfq.currency}
                  </span>
                </div>
              </div>
            </div>
          )}

          <DialogFooter>
            <Button variant="outline" onClick={() => setQuoteOpen(false)}>
              Close
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
