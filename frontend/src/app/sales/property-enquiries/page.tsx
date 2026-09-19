'use client';

import React, { Suspense, useEffect, useState } from 'react';
import { useSearchParams } from 'next/navigation';
import { useQuery, useQueryClient, useMutation } from '@tanstack/react-query';
import { apiService } from '@/services/api.service';
import type { EhcTicketDetail } from '@/services/ehcTicketService';
import { PropertyEnquiryDetails } from '@/components/estate/PropertyEnquiryDetails';
import { Button } from '@/components/ui/button';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import { Label } from '@/components/ui/label';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { useToast } from '@/hooks/use-toast';

type EnquiryRow = {
  id: string;
  ticketNumber: string;
  subject: string;
  status: string;
  createdAt: string;
  requesterName: string;
};
type Envelope<T> = { success: boolean; data: T; totalCount?: number };
type EstateHandoffState = {
  crmOpportunityId?: string | null;
  opportunity?: {
    id: string;
    referenceNumber?: string | null;
    stage: string;
    amount: number;
    currency: string;
    actualCloseDate?: string | null;
  } | null;
  estateCase?: {
    id: string;
    referenceNumber?: string | null;
    title: string;
    status: string;
    currentStageName: string;
    createdAt: string;
  } | null;
  estateHandoffReference?: string | null;
  estateListingApplicationHandedOffAt?: string | null;
  canHandoff: boolean;
};
type EstateHandoffDraft = {
  salesReference: string;
  agreedAmount: string;
  salesAmountPaid: string;
  salesPaymentReference: string;
  currency: string;
  salesCompletedAt: string;
  notes: string;
};
const endpoint = '/ehc/internal/property-enquiries';

function PropertyEnquiries() {
  const params = useSearchParams();
  const [selectedId, setSelectedId] = useState(params.get('id') || '');
  const [page, setPage] = useState(1);
  const [reply, setReply] = useState('');
  const [handoffDraft, setHandoffDraft] = useState<EstateHandoffDraft>({
    salesReference: '',
    agreedAmount: '',
    salesAmountPaid: '',
    salesPaymentReference: '',
    currency: '',
    salesCompletedAt: '',
    notes: '',
  });
  const [confirmHandoff, setConfirmHandoff] = useState(false);
  const client = useQueryClient();
  const { toast } = useToast();
  const queue = useQuery({
    queryKey: ['property-enquiries', page],
    queryFn: () =>
      apiService.request<Envelope<EnquiryRow[]>>(`${endpoint}?page=${page}`, {
        method: 'GET',
      }),
  });
  const detail = useQuery({
    queryKey: ['property-enquiry', selectedId],
    enabled: Boolean(selectedId),
    queryFn: async () => {
      const result = await apiService.request<Envelope<EhcTicketDetail>>(
        `${endpoint}/${selectedId}`,
        { method: 'GET' }
      );
      return result.data;
    },
  });
  const handoff = useQuery({
    queryKey: ['property-enquiry-estate-handoff', selectedId],
    enabled: Boolean(selectedId),
    queryFn: async () => {
      const result = await apiService.request<Envelope<EstateHandoffState>>(
        `${endpoint}/${selectedId}/estate-handoff`,
        { method: 'GET' }
      );
      return result.data;
    },
  });
  const send = useMutation({
    mutationFn: () =>
      apiService.request(`${endpoint}/${selectedId}/reply`, {
        method: 'POST',
        body: JSON.stringify({ body: reply.trim() }),
      }),
    onSuccess: async () => {
      setReply('');
      await client.invalidateQueries({
        queryKey: ['property-enquiry', selectedId],
      });
      toast({
        title: 'Reply sent',
        description: 'The business partner can view your reply in the portal.',
        variant: 'success',
      });
    },
  });
  const submitHandoff = useMutation({
    mutationFn: () =>
      apiService.request<
        Envelope<{
          procedureCaseId: string;
          referenceNumber?: string | null;
          alreadyExists: boolean;
        }>
      >(`${endpoint}/${selectedId}/estate-handoff`, {
        method: 'POST',
        body: JSON.stringify({
          salesReference: handoffDraft.salesReference.trim(),
          agreedAmount: Number(handoffDraft.agreedAmount),
          salesAmountPaid: handoffDraft.salesAmountPaid
            ? Number(handoffDraft.salesAmountPaid)
            : 0,
          salesPaymentReference:
            handoffDraft.salesPaymentReference.trim() || null,
          currency: handoffDraft.currency.trim().toUpperCase(),
          salesCompletedAt: handoffDraft.salesCompletedAt || null,
          notes: handoffDraft.notes.trim() || null,
        }),
      }),
    onSuccess: async (result) => {
      setConfirmHandoff(false);
      await Promise.all([
        client.invalidateQueries({
          queryKey: ['property-enquiry-estate-handoff', selectedId],
        }),
        client.invalidateQueries({
          queryKey: ['property-enquiry', selectedId],
        }),
      ]);
      toast({
        title: result.data.alreadyExists
          ? 'Estate handoff linked'
          : 'Handed to Estate',
        description:
          'The Estate listing-application workflow can now continue.',
        variant: 'success',
      });
    },
  });
  const error =
    queue.error ||
    detail.error ||
    handoff.error ||
    send.error ||
    submitHandoff.error;
  const ticket = detail.data;
  const handoffState = handoff.data;
  const opportunity = handoffState?.opportunity;
  useEffect(() => {
    if (!selectedId || !opportunity || handoffState?.estateCase) return;

    setHandoffDraft((current) => ({
      ...current,
      agreedAmount:
        current.agreedAmount ||
        (opportunity.amount > 0 ? String(opportunity.amount) : ''),
      currency: current.currency || opportunity.currency || '',
    }));
  }, [
    selectedId,
    opportunity?.id,
    opportunity?.amount,
    opportunity?.currency,
    handoffState?.estateCase?.id,
  ]);
  const handoffDraftIsValid = Boolean(
    handoffState?.canHandoff &&
      handoffDraft.salesReference.trim() &&
      Number.isFinite(Number(handoffDraft.agreedAmount)) &&
      Number(handoffDraft.agreedAmount) > 0 &&
      (!handoffDraft.salesAmountPaid ||
        (Number.isFinite(Number(handoffDraft.salesAmountPaid)) &&
          Number(handoffDraft.salesAmountPaid) >= 0 &&
          Number(handoffDraft.salesAmountPaid) <=
            Number(handoffDraft.agreedAmount))) &&
      /^[A-Za-z]{3}$/.test(handoffDraft.currency.trim())
  );
  return (
    <div className="space-y-5">
      <div>
        <h1 className="text-2xl font-semibold">Property enquiries</h1>
        <p className="text-slate-600">
          Sales and Marketing follow-up for interested business partners.
        </p>
      </div>
      {error && (
        <Alert variant="destructive">
          <AlertDescription>
            {error instanceof Error
              ? error.message
              : 'Could not load or update the enquiry.'}
          </AlertDescription>
        </Alert>
      )}
      <div className="grid gap-5 lg:grid-cols-[minmax(280px,1fr)_2fr]">
        <section className="space-y-3" aria-label="Property enquiry queue">
          <div className="flex items-center justify-between">
            <span>{queue.data?.totalCount ?? 0} enquiries</span>
            <Button variant="outline" onClick={() => void queue.refetch()}>
              Refresh
            </Button>
          </div>
          {queue.isLoading && <p>Loading enquiries…</p>}
          {queue.data?.data.length === 0 && (
            <p className="rounded-md border p-5 text-slate-500">
              No property enquiries yet.
            </p>
          )}
          {queue.data?.data.map((item) => (
            <button
              key={item.id}
              disabled={send.isPending || submitHandoff.isPending}
              onClick={() => {
                setSelectedId(item.id);
                setReply('');
                setHandoffDraft({
                  salesReference: '',
                  agreedAmount: '',
                  salesAmountPaid: '',
                  salesPaymentReference: '',
                  currency: '',
                  salesCompletedAt: '',
                  notes: '',
                });
                send.reset();
                submitHandoff.reset();
              }}
              className={`w-full rounded-md border p-4 text-left ${selectedId === item.id ? 'border-blue-500 bg-blue-50' : 'bg-white'}`}
            >
              <p className="font-semibold">{item.ticketNumber}</p>
              <p>{item.subject}</p>
              <p className="mt-1 text-sm text-slate-600">
                {item.requesterName} · {item.status}
              </p>
            </button>
          ))}
          <div className="flex gap-2">
            <Button
              variant="outline"
              disabled={page <= 1 || send.isPending}
              onClick={() => setPage((p) => p - 1)}
            >
              Previous
            </Button>
            <Button
              variant="outline"
              disabled={
                page * 25 >= (queue.data?.totalCount ?? 0) || send.isPending
              }
              onClick={() => setPage((p) => p + 1)}
            >
              Next
            </Button>
          </div>
        </section>
        <section
          className="space-y-4 rounded-lg border bg-white p-5"
          aria-label="Selected enquiry"
        >
          {detail.isLoading ? (
            <p>Loading enquiry…</p>
          ) : ticket ? (
            <>
              <div>
                <h2 className="text-xl font-semibold">{ticket.ticketNumber}</h2>
                <p className="text-sm text-slate-600">{ticket.status}</p>
              </div>
              <PropertyEnquiryDetails property={ticket.propertyListing} />
              <div>
                <h3 className="font-semibold">Original enquiry</h3>
                <p className="mt-2 whitespace-pre-wrap">{ticket.description}</p>
              </div>
              <section
                className="space-y-3 rounded-lg border border-indigo-200 bg-indigo-50 p-4"
                aria-label="Sales to Estate handoff"
              >
                <div>
                  <h3 className="font-semibold">Sales to Estate handoff</h3>
                  <p className="text-sm text-slate-600">
                    Estate receives the listing application only after Sales
                    closes this enquiry&apos;s CRM opportunity as won.
                  </p>
                </div>
                {handoff.isLoading ? (
                  <p className="text-sm text-slate-600">
                    Checking the linked Sales opportunity…
                  </p>
                ) : handoffState?.estateCase ? (
                  <div className="space-y-2 text-sm">
                    <p>
                      <span className="font-medium">Estate application:</span>{' '}
                      {handoffState.estateCase.referenceNumber ||
                        handoffState.estateHandoffReference ||
                        handoffState.estateCase.id}
                    </p>
                    <p>
                      {handoffState.estateCase.status} ·{' '}
                      {handoffState.estateCase.currentStageName}
                    </p>
                    <a
                      className="inline-flex text-blue-700 underline"
                      href={`/estate/property-management/EstatePropertyManagementListingApplication?caseId=${encodeURIComponent(handoffState.estateCase.id)}`}
                    >
                      Open Estate application
                    </a>
                  </div>
                ) : !opportunity ? (
                  <p className="text-sm text-amber-800">
                    The enquiry needs a linked CRM opportunity before it can be
                    handed to Estate.
                  </p>
                ) : opportunity.stage !== 'Closed Won' ? (
                  <div className="space-y-1 text-sm text-amber-800">
                    <p>
                      CRM opportunity:{' '}
                      {opportunity.referenceNumber || opportunity.id}
                    </p>
                    <p>
                      Current stage: {opportunity.stage}. Close it as Won before
                      the Estate handoff.
                    </p>
                  </div>
                ) : (
                  <div className="space-y-3">
                    <div className="rounded border border-indigo-200 bg-white p-3 text-sm">
                      <p>
                        <span className="font-medium">CRM opportunity:</span>{' '}
                        {opportunity.referenceNumber || opportunity.id}
                      </p>
                      <p>
                        <span className="font-medium">Closed:</span>{' '}
                        {opportunity.actualCloseDate
                          ? new Date(
                              opportunity.actualCloseDate
                            ).toLocaleDateString()
                          : 'Recorded as Closed Won'}
                      </p>
                      <p>
                        <span className="font-medium">Opportunity value:</span>{' '}
                        {opportunity.currency}{' '}
                        {opportunity.amount.toLocaleString()}
                      </p>
                    </div>
                    <div className="grid gap-3 md:grid-cols-2">
                      <div className="space-y-1">
                        <Label htmlFor="estate-sales-reference">
                          Completed Sales reference
                        </Label>
                        <Input
                          id="estate-sales-reference"
                          value={handoffDraft.salesReference}
                          maxLength={200}
                          placeholder="Agreement, allocation or contract reference"
                          onChange={(event) =>
                            setHandoffDraft((value) => ({
                              ...value,
                              salesReference: event.target.value,
                            }))
                          }
                        />
                      </div>
                      <div className="space-y-1">
                        <Label htmlFor="estate-agreed-amount">
                          Agreed amount
                        </Label>
                        <Input
                          id="estate-agreed-amount"
                          type="number"
                          min="0.01"
                          step="0.01"
                          value={handoffDraft.agreedAmount}
                          placeholder={String(opportunity.amount)}
                          onChange={(event) =>
                            setHandoffDraft((value) => ({
                              ...value,
                              agreedAmount: event.target.value,
                            }))
                          }
                        />
                      </div>
                      <div className="space-y-1">
                        <Label htmlFor="estate-currency">Currency</Label>
                        <Input
                          id="estate-currency"
                          value={handoffDraft.currency}
                          maxLength={3}
                          placeholder={opportunity.currency}
                          onChange={(event) =>
                            setHandoffDraft((value) => ({
                              ...value,
                              currency: event.target.value.toUpperCase(),
                            }))
                          }
                        />
                      </div>
                      <div className="space-y-1">
                        <Label htmlFor="estate-sales-paid">
                          Amount paid in Sales
                        </Label>
                        <Input
                          id="estate-sales-paid"
                          type="number"
                          min="0"
                          max={handoffDraft.agreedAmount || undefined}
                          step="0.01"
                          value={handoffDraft.salesAmountPaid}
                          placeholder="0.00"
                          onChange={(event) =>
                            setHandoffDraft((value) => ({
                              ...value,
                              salesAmountPaid: event.target.value,
                            }))
                          }
                        />
                      </div>
                      <div className="space-y-1">
                        <Label htmlFor="estate-sales-payment-reference">
                          Sales payment reference
                        </Label>
                        <Input
                          id="estate-sales-payment-reference"
                          value={handoffDraft.salesPaymentReference}
                          maxLength={200}
                          placeholder="Receipt or collection reference"
                          onChange={(event) =>
                            setHandoffDraft((value) => ({
                              ...value,
                              salesPaymentReference: event.target.value,
                            }))
                          }
                        />
                      </div>
                      <div className="space-y-1">
                        <Label htmlFor="estate-completed-date">
                          Sales completion date
                        </Label>
                        <Input
                          id="estate-completed-date"
                          type="date"
                          value={handoffDraft.salesCompletedAt}
                          onChange={(event) =>
                            setHandoffDraft((value) => ({
                              ...value,
                              salesCompletedAt: event.target.value,
                            }))
                          }
                        />
                      </div>
                    </div>
                    <div className="space-y-1">
                      <Label htmlFor="estate-handoff-notes">
                        Handoff notes
                      </Label>
                      <Textarea
                        id="estate-handoff-notes"
                        value={handoffDraft.notes}
                        maxLength={1000}
                        rows={3}
                        placeholder="Commercial terms or information Estate needs to continue."
                        onChange={(event) =>
                          setHandoffDraft((value) => ({
                            ...value,
                            notes: event.target.value,
                          }))
                        }
                      />
                    </div>
                    <Button
                      onClick={() => setConfirmHandoff(true)}
                      disabled={!handoffDraftIsValid || submitHandoff.isPending}
                    >
                      {submitHandoff.isPending
                        ? 'Handing off…'
                        : 'Hand off to Estate'}
                    </Button>
                  </div>
                )}
              </section>
              <div className="space-y-3">
                <h3 className="font-semibold">Conversation</h3>
                {ticket.messages
                  .filter((m) => !m.isInternal)
                  .map((m) => (
                    <article className="rounded-md border p-3" key={m.id}>
                      <p className="text-xs text-slate-500">
                        {m.authorName || 'Portal user'} ·{' '}
                        {new Date(m.createdAt).toLocaleString()}
                      </p>
                      <p className="mt-1 whitespace-pre-wrap">{m.body}</p>
                    </article>
                  ))}
              </div>
              <div className="space-y-2">
                <Label htmlFor="property-enquiry-reply">
                  Reply to business partner
                </Label>
                <Textarea
                  id="property-enquiry-reply"
                  rows={5}
                  value={reply}
                  maxLength={4000}
                  onChange={(e) => setReply(e.target.value)}
                  disabled={send.isPending}
                />
                <Button
                  onClick={() => send.mutate()}
                  disabled={!reply.trim() || send.isPending}
                >
                  {send.isPending ? 'Sending…' : 'Send reply'}
                </Button>
              </div>
            </>
          ) : (
            <p className="text-slate-500">
              Select an enquiry to view the property and start following up.
            </p>
          )}
        </section>
      </div>
      <ConfirmationDialog
        open={confirmHandoff}
        onOpenChange={setConfirmHandoff}
        title="Hand completed enquiry to Estate?"
        description="This creates or links the Estate listing-application workflow using the verified listing, business partner and closed CRM opportunity."
        confirmText="Hand off to Estate"
        onConfirm={async () => {
          await submitHandoff.mutateAsync();
        }}
        isLoading={submitHandoff.isPending}
        confirmDisabled={!handoffDraftIsValid}
      />
    </div>
  );
}

export default function PropertyEnquiriesPage() {
  return (
    <Suspense fallback={<p>Loading enquiries…</p>}>
      <PropertyEnquiries />
    </Suspense>
  );
}
