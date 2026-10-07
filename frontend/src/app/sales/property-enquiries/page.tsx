'use client';

import React, { Suspense, useEffect, useMemo, useRef, useState } from 'react';
import { useSearchParams } from 'next/navigation';
import { useQuery, useQueryClient, useMutation } from '@tanstack/react-query';
import { apiService } from '@/services/api.service';
import {
  canSearchOrLinkExistingCustomer,
  canSubmitPropertyEstateHandoff,
  propertyEnquiryService,
  type CreateProspectBusinessPartnerRequest,
  type ProspectActivityType,
} from '@/services/propertyEnquiryService';
import { PropertyEnquiryDetails } from '@/components/estate/PropertyEnquiryDetails';
import { Button } from '@/components/ui/button';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Textarea } from '@/components/ui/textarea';
import { Label } from '@/components/ui/label';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { useToast } from '@/hooks/use-toast';
import { useAuth } from '@/hooks/use-auth';
import { getPropertyEnquiryDepositAccess } from '@/lib/sales/property-enquiry-deposit-access';
import { SalesHandoffActions } from '@/app/crm/components/SalesHandoffActions';
import { salesReferenceService } from '@/services/salesReferenceService';
import {
  buildOpportunityCurrencyOptions,
  resolveOpportunityCurrency,
} from '@/lib/crm/opportunity-currency';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';

type Envelope<T> = { success: boolean; data: T; totalCount?: number };
type EstateHandoffState = {
  crmOpportunityId?: string | null;
  opportunity?: {
    id: string;
    referenceNumber?: string | null;
    stage: string;
    isWon: boolean;
    amount: number;
    currency: string;
    actualCloseDate?: string | null;
  } | null;
  salesOrder?: {
    id: string;
    reference: string;
    status: string;
    agreedAmount: number;
    amountPaid: number;
    currency: string;
    completedAt?: string | null;
    invoiceReference?: string | null;
    paymentReference?: string | null;
    paymentDate?: string | null;
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
  requestedLeaseTerm: string;
  salesAmountPaid: string;
  salesPaymentReference: string;
  currency: string;
  salesCompletedAt: string;
  notes: string;
};
const legacyEndpoint = '/ehc/internal/property-enquiries';

function PropertyEnquiries() {
  const params = useSearchParams();
  const [selectedId, setSelectedId] = useState(params.get('id') || '');
  const requestedId = params.get('id');
  useEffect(() => {
    if (requestedId) setSelectedId(requestedId);
  }, [requestedId]);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(25);
  const [searchInput, setSearchInput] = useState('');
  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState('all');
  const [crmFilter, setCrmFilter] = useState('all');
  const [dateFilter, setDateFilter] = useState('all');
  useEffect(() => {
    const timeout = window.setTimeout(() => {
      setSearch(searchInput.trim());
      setPage(1);
    }, 300);
    return () => window.clearTimeout(timeout);
  }, [searchInput]);
  const createdFrom = useMemo(() => {
    if (dateFilter === 'all') return undefined;
    return new Date(
      Date.now() - Number(dateFilter) * 24 * 60 * 60 * 1000
    ).toISOString();
  }, [dateFilter]);
  const [emailBody, setEmailBody] = useState('');
  const [activityType, setActivityType] =
    useState<ProspectActivityType>('Contact');
  const [activityNotes, setActivityNotes] = useState('');
  const [followUpAt, setFollowUpAt] = useState('');
  const [qualificationNotes, setQualificationNotes] = useState('');
  const [qualificationScore, setQualificationScore] = useState('40');
  const [agreedAmount, setAgreedAmount] = useState('');
  const [qualificationCurrency, setQualificationCurrency] = useState('GHS');
  const [opportunityDraft, setOpportunityDraft] = useState({
    amount: '',
    currency: 'GHS',
    expectedCloseDate: '',
    reserveProperty: true,
    reservationDays: '14',
    notes: '',
  });
  const activeCurrencies = useQuery({
    queryKey: ['finance-active-currencies'],
    queryFn: salesReferenceService.getActiveCurrencies,
    staleTime: 5 * 60 * 1000,
  });
  const opportunityCurrencyOptions = useMemo(
    () =>
      buildOpportunityCurrencyOptions(
        activeCurrencies.data ?? [],
        opportunityDraft.currency
      ),
    [activeCurrencies.data, opportunityDraft.currency]
  );
  const [depositDraft, setDepositDraft] = useState({
    amount: '',
    paymentMethod: 'BankTransfer',
    transactionReference: '',
    receivedAt: '',
  });
  const [depositAction, setDepositAction] = useState<{
    kind: 'clear' | 'reverse';
    receiptId: string;
    receiptNumber: string;
  } | null>(null);
  const [reversalReason, setReversalReason] = useState('');
  const [depositActionDate, setDepositActionDate] = useState('');
  const [showMatches, setShowMatches] = useState(false);
  const [selectedMatchId, setSelectedMatchId] = useState('');
  const [confirmLink, setConfirmLink] = useState(false);
  const [confirmOpportunity, setConfirmOpportunity] = useState(false);
  const [createPartnerOpen, setCreatePartnerOpen] = useState(false);
  const [partnerDraft, setPartnerDraft] =
    useState<CreateProspectBusinessPartnerRequest>({
      partnerName: '',
      email: '',
      phone: '',
      physicalAddress: '',
      city: '',
      country: 'Ghana',
      postalCode: '',
    });
  const [handoffDraft, setHandoffDraft] = useState<EstateHandoffDraft>({
    salesReference: '',
    agreedAmount: '',
    requestedLeaseTerm: '',
    salesAmountPaid: '',
    salesPaymentReference: '',
    currency: '',
    salesCompletedAt: '',
    notes: '',
  });
  const salesAmountPaidEditedRef = useRef(false);
  const [confirmHandoff, setConfirmHandoff] = useState(false);
  const client = useQueryClient();
  const { toast } = useToast();
  const { hasPermission } = useAuth();
  const { canClear, canReverse } =
    getPropertyEnquiryDepositAccess(hasPermission);
  const queue = useQuery({
    queryKey: [
      'property-enquiries', page, pageSize, search, statusFilter, crmFilter,
      dateFilter,
    ],
    queryFn: () =>
      propertyEnquiryService.list(page, {
        pageSize,
        search,
        status: statusFilter === 'all' ? undefined : statusFilter,
        crmLinked: crmFilter === 'all' ? undefined : crmFilter === 'linked',
        createdFrom,
      }),
  });
  useEffect(() => {
    if (!queue.data) return;
    const lastPage = Math.max(
      1,
      Math.ceil((queue.data.totalCount ?? 0) / pageSize)
    );
    if (page > lastPage) setPage(lastPage);
  }, [page, pageSize, queue.data]);
  const detail = useQuery({
    queryKey: ['property-enquiry', selectedId],
    enabled: Boolean(selectedId),
    queryFn: () => propertyEnquiryService.get(selectedId),
  });
  const handoff = useQuery({
    queryKey: ['property-enquiry-estate-handoff', selectedId],
    enabled: Boolean(selectedId),
    queryFn: async () => {
      const result = await apiService.request<Envelope<EstateHandoffState>>(
        `${legacyEndpoint}/${selectedId}/estate-handoff`,
        { method: 'GET' }
      );
      return result.data;
    },
  });
  const salesOrderSource = useQuery({
    queryKey: ['property-enquiry-sales-order-source', selectedId],
    enabled: Boolean(
      selectedId &&
        detail.data?.prospect?.status === 'Converted' &&
        detail.data.prospect.opportunityId &&
        detail.data.prospect.businessPartnerId &&
        !handoff.data?.salesOrder
    ),
    queryFn: () => propertyEnquiryService.getSalesOrderSource(selectedId),
    retry: false,
  });
  const sendEmail = useMutation({
    mutationFn: () =>
      propertyEnquiryService.sendEmail(selectedId, emailBody.trim()),
    onSuccess: async () => {
      setEmailBody('');
      await client.invalidateQueries({
        queryKey: ['property-enquiry', selectedId],
      });
      toast({
        title: 'Email sent',
        description:
          'The email was sent to the address captured on the original enquiry and recorded in the activity history.',
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
      >(`${legacyEndpoint}/${selectedId}/estate-handoff`, {
        method: 'POST',
        body: JSON.stringify({
          salesReference: handoffDraft.salesReference.trim(),
          agreedAmount: Number(handoffDraft.agreedAmount),
          requestedLeaseTerm: handoffDraft.requestedLeaseTerm.trim() || null,
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

  const refreshSelected = async () => {
    await Promise.all([
      client.invalidateQueries({ queryKey: ['property-enquiry', selectedId] }),
      client.invalidateQueries({ queryKey: ['property-enquiries'] }),
      client.invalidateQueries({
        queryKey: ['property-enquiry-estate-handoff', selectedId],
      }),
      client.invalidateQueries({
        queryKey: ['property-enquiry-deposits', selectedId],
      }),
      client.invalidateQueries({
        queryKey: ['property-enquiry-matches', selectedId],
      }),
    ]);
  };

  const partnerMatches = useQuery({
    queryKey: ['property-enquiry-matches', selectedId],
    enabled: Boolean(selectedId && showMatches),
    queryFn: () => propertyEnquiryService.getBusinessPartnerMatches(selectedId),
  });
  const depositReceipts = useQuery({
    queryKey: ['property-enquiry-deposits', selectedId],
    enabled: Boolean(selectedId && detail.data?.prospect?.opportunityId),
    queryFn: () => propertyEnquiryService.listDeposits(selectedId),
  });
  const depositCurrency = resolveOpportunityCurrency(
    detail.data?.prospect?.currency ||
      handoff.data?.opportunity?.currency ||
      detail.data?.propertyListing?.currency,
    activeCurrencies.data ?? []
  );

  const qualify = useMutation({
    mutationFn: () =>
      propertyEnquiryService.qualify(selectedId, {
        qualificationScore: Number(qualificationScore),
        agreedAmount: Number(agreedAmount),
        currency: qualificationCurrency.trim().toUpperCase(),
        notes: qualificationNotes.trim() || null,
      }),
    onSuccess: async () => {
      setQualificationNotes('');
      await refreshSelected();
      toast({
        title: 'Prospect qualified',
        description:
          'Create the opportunity explicitly when Sales is ready to proceed.',
        variant: 'success',
      });
    },
    onError: (mutationError) => {
      toast({
        title: 'Prospect could not be qualified',
        description:
          mutationError instanceof Error && mutationError.message.trim()
            ? mutationError.message
            : 'Record Sales contact, then try qualifying the prospect again.',
        variant: 'destructive',
      });
    },
  });

  const disqualify = useMutation({
    mutationFn: () =>
      propertyEnquiryService.disqualify(selectedId, qualificationNotes.trim()),
    onSuccess: async () => {
      setQualificationNotes('');
      await refreshSelected();
      toast({
        title: 'Prospect disqualified',
        description: 'The reason was added to the enquiry audit history.',
        variant: 'success',
      });
    },
  });

  const addActivity = useMutation({
    mutationFn: async () => {
      if (activityType === 'Contact') {
        await propertyEnquiryService.markContacted(
          selectedId,
          activityNotes.trim()
        );
        return;
      }
      await propertyEnquiryService.addActivity(selectedId, {
        activityType,
        notes: activityNotes.trim(),
        followUpAt:
          activityType === 'FollowUp' && followUpAt
            ? new Date(followUpAt).toISOString()
            : null,
      });
    },
    onSuccess: async () => {
      setActivityNotes('');
      setFollowUpAt('');
      await refreshSelected();
      toast({
        title: 'Activity recorded',
        description: 'The internal Sales activity was saved.',
        variant: 'success',
      });
    },
    onError: (mutationError) => {
      toast({
        title: 'Activity could not be recorded',
        description:
          mutationError instanceof Error && mutationError.message.trim()
            ? mutationError.message
            : 'The Sales activity could not be saved. Please try again.',
        variant: 'destructive',
      });
    },
  });

  const linkPartner = useMutation({
    mutationFn: () =>
      propertyEnquiryService.linkBusinessPartner(selectedId, selectedMatchId),
    onSuccess: async () => {
      setConfirmLink(false);
      setShowMatches(false);
      setSelectedMatchId('');
      await refreshSelected();
      toast({
        title: 'Business partner linked',
        description: 'The original public enquiry remains unchanged.',
        variant: 'success',
      });
    },
  });

  const createPartner = useMutation({
    mutationFn: () =>
      propertyEnquiryService.createBusinessPartner(selectedId, {
        ...partnerDraft,
        partnerName: partnerDraft.partnerName?.trim() || null,
        email: partnerDraft.email?.trim() || null,
        phone: partnerDraft.phone?.trim() || null,
        physicalAddress: partnerDraft.physicalAddress?.trim() || null,
        city: partnerDraft.city?.trim() || null,
        country: partnerDraft.country?.trim() || null,
        postalCode: partnerDraft.postalCode?.trim() || null,
      }),
    onSuccess: async () => {
      setCreatePartnerOpen(false);
      await refreshSelected();
      toast({
        title: 'Customer registration submitted',
        description:
          'The customer Business Partner remains governed by the existing approval process.',
        variant: 'success',
      });
    },
  });

  const createOpportunity = useMutation({
    mutationFn: () =>
      propertyEnquiryService.createOpportunity(selectedId, {
        amount: Number(opportunityDraft.amount),
        currency: opportunityDraft.currency.trim().toUpperCase(),
        expectedCloseDate: opportunityDraft.expectedCloseDate,
        reserveProperty: opportunityDraft.reserveProperty,
        reservationDays: Number(opportunityDraft.reservationDays),
        notes: opportunityDraft.notes.trim() || null,
      }),
    onSuccess: async () => {
      setConfirmOpportunity(false);
      await refreshSelected();
      toast({
        title: 'Opportunity created',
        description:
          'The opportunity is linked to this enquiry and can now proceed to reservation and deposit.',
        variant: 'success',
      });
    },
    onError: (mutationError) => {
      setConfirmOpportunity(false);
      toast({
        title: 'Opportunity could not be created',
        description:
          mutationError instanceof Error && mutationError.message.trim()
            ? mutationError.message
            : 'Review the enquiry and try creating the opportunity again.',
        variant: 'destructive',
      });
    },
  });
  const recordDeposit = useMutation({
    mutationFn: () =>
      propertyEnquiryService.recordDeposit(selectedId, {
        amount: Number(depositDraft.amount),
        currency: depositCurrency,
        paymentMethod: depositDraft.paymentMethod,
        transactionReference: depositDraft.transactionReference.trim() || null,
        receivedAt: depositDraft.receivedAt
          ? new Date(depositDraft.receivedAt).toISOString()
          : null,
      }),
    onSuccess: async () => {
      setDepositDraft((value) => ({
        ...value,
        amount: '',
        transactionReference: '',
        receivedAt: '',
      }));
      await Promise.all([
        refreshSelected(),
        client.invalidateQueries({
          queryKey: ['property-enquiry-deposits', selectedId],
        }),
      ]);
      toast({
        title: 'Deposit recorded',
        description:
          'The receipt is pending clearance and does not count toward the threshold yet.',
        variant: 'success',
      });
    },
    onError: (mutationError) => {
      toast({
        title: 'Deposit could not be recorded',
        description:
          mutationError instanceof Error && mutationError.message.trim()
            ? mutationError.message
            : 'The deposit could not be recorded. Please try again.',
        variant: 'destructive',
      });
    },
  });
  const decideDeposit = useMutation({
    mutationFn: async () => {
      if (!depositAction) throw new Error('Select a deposit receipt.');
      return depositAction.kind === 'clear'
        ? propertyEnquiryService.clearDeposit(
            selectedId,
            depositAction.receiptId,
            depositActionDate ? new Date(depositActionDate).toISOString() : null
          )
        : propertyEnquiryService.reverseDeposit(
            selectedId,
            depositAction.receiptId,
            reversalReason.trim(),
            depositActionDate ? new Date(depositActionDate).toISOString() : null
          );
    },
    onSuccess: async () => {
      const action = depositAction?.kind;
      setDepositAction(null);
      setReversalReason('');
      setDepositActionDate('');
      await Promise.all([
        refreshSelected(),
        client.invalidateQueries({
          queryKey: ['property-enquiry-deposits', selectedId],
        }),
      ]);
      toast({
        title: action === 'clear' ? 'Deposit cleared' : 'Deposit reversed',
        description:
          'The prospect threshold has been recalculated from posted, unreversed receipts.',
        variant: 'success',
      });
    },
    onError: (mutationError) => {
      toast({
        title:
          depositAction?.kind === 'clear'
            ? 'Deposit could not be cleared'
            : 'Deposit could not be reversed',
        description:
          mutationError instanceof Error && mutationError.message.trim()
            ? mutationError.message
            : 'The deposit action could not be completed. Please try again.',
        variant: 'destructive',
      });
    },
  });
  const finalizePartner = useMutation({
    mutationFn: () =>
      propertyEnquiryService.finalizeBusinessPartner(selectedId),
    onSuccess: async () => {
      await refreshSelected();
      toast({
        title: 'Customer conversion finalized',
        description:
          'The approved customer and cleared deposit were linked to the Sales records.',
        variant: 'success',
      });
    },
  });
  const error =
    queue.error ||
    detail.error ||
    handoff.error ||
    sendEmail.error ||
    qualify.error ||
    disqualify.error ||
    addActivity.error ||
    linkPartner.error ||
    createPartner.error ||
    depositReceipts.error ||
    finalizePartner.error ||
    partnerMatches.error ||
    submitHandoff.error;
  const ticket = detail.data;
  const leadStatus = ticket?.prospect?.status || 'New';
  const prospect = ticket?.prospect;
  const depositThresholdMet = Boolean(prospect?.depositThresholdMet);
  const canMatchExistingCustomer = canSearchOrLinkExistingCustomer(prospect);
  const handoffState = handoff.data;
  const opportunity = handoffState?.opportunity;
  const salesOrder = handoffState?.salesOrder;
  const listingType = ticket?.propertyListing?.listingType || '';
  const handoffRequiresDuration =
    listingType === 'Rent' ||
    listingType === 'Lease' ||
    listingType === 'SaleAndRent' ||
    listingType === 'SaleAndLease';
  useEffect(() => {
    salesAmountPaidEditedRef.current = false;
    setHandoffDraft({
      salesReference: '',
      agreedAmount: '',
      requestedLeaseTerm: '',
      salesAmountPaid: '',
      salesPaymentReference: '',
      currency: '',
      salesCompletedAt: '',
      notes: '',
    });
  }, [selectedId]);
  useEffect(() => {
    if (
      !selectedId ||
      handoffState?.estateCase ||
      (!opportunity && !salesOrder)
    )
      return;

    const defaultAmountPaid = salesOrder && salesOrder.amountPaid > 0
      ? String(salesOrder.amountPaid)
      : prospect &&
          prospect.clearedDeposit > 0 &&
          prospect.currency === (salesOrder?.currency || opportunity?.currency)
        ? String(prospect.clearedDeposit)
        : '';
    setHandoffDraft((current) => ({
      ...current,
      salesReference: current.salesReference || salesOrder?.reference || '',
      agreedAmount:
        current.agreedAmount ||
        (salesOrder && salesOrder.agreedAmount > 0
          ? String(salesOrder.agreedAmount)
          : opportunity && opportunity.amount > 0
            ? String(opportunity.amount)
            : ''),
      salesAmountPaid: salesAmountPaidEditedRef.current
        ? current.salesAmountPaid
        : defaultAmountPaid,
      salesPaymentReference:
        current.salesPaymentReference || salesOrder?.paymentReference || '',
      currency:
        current.currency || salesOrder?.currency || opportunity?.currency || '',
      salesCompletedAt:
        current.salesCompletedAt ||
        (salesOrder?.completedAt ? salesOrder.completedAt.slice(0, 10) : ''),
    }));
  }, [
    selectedId,
    opportunity?.id,
    opportunity?.amount,
    opportunity?.currency,
    salesOrder?.id,
    salesOrder?.reference,
    salesOrder?.agreedAmount,
    salesOrder?.amountPaid,
    salesOrder?.currency,
    prospect?.clearedDeposit,
    prospect?.currency,
    salesOrder?.paymentReference,
    salesOrder?.currency,
    salesOrder?.completedAt,
    handoffState?.estateCase?.id,
  ]);
  const handoffDraftIsValid = Boolean(
    canSubmitPropertyEstateHandoff(
      prospect,
      Boolean(handoffState?.canHandoff)
    ) &&
      handoffDraft.salesReference.trim() &&
      Number.isFinite(Number(handoffDraft.agreedAmount)) &&
      Number(handoffDraft.agreedAmount) > 0 &&
      (!handoffRequiresDuration || handoffDraft.requestedLeaseTerm.trim()) &&
      (!handoffDraft.salesAmountPaid ||
        (Number.isFinite(Number(handoffDraft.salesAmountPaid)) &&
          Number(handoffDraft.salesAmountPaid) >= 0 &&
          Number(handoffDraft.salesAmountPaid) <=
            Number(handoffDraft.agreedAmount))) &&
      /^[A-Za-z]{3}$/.test(handoffDraft.currency.trim())
  );
  useEffect(() => {
    if (
      leadStatus !== 'New' &&
      leadStatus !== 'Contacted' &&
      activityType === 'Contact'
    ) {
      setActivityType('Note');
    }
  }, [activityType, leadStatus]);
  useEffect(() => {
    if (!ticket) return;
    setPartnerDraft({
      partnerName: ticket.propertyListing?.contactName || '',
      email: ticket.propertyListing?.contactEmail || '',
      phone: ticket.propertyListing?.contactPhone || '',
      physicalAddress: '',
      city: ticket.propertyListing?.location || '',
      country: 'Ghana',
      postalCode: '',
    });
    const initialAmount = ticket.prospect?.agreedAmount
      ? String(ticket.prospect.agreedAmount)
      : ticket.propertyListing?.price
        ? String(ticket.propertyListing.price)
        : '';
    const initialCurrency = resolveOpportunityCurrency(
      ticket.propertyListing?.currency,
      activeCurrencies.data ?? []
    );
    setAgreedAmount(initialAmount);
    setQualificationCurrency(initialCurrency);
    setOpportunityDraft((value) => ({
      ...value,
      amount: initialAmount,
      currency: initialCurrency,
    }));
  }, [ticket?.id]);
  return (
    <div className="space-y-5">
      <div>
        <h1 className="text-2xl font-semibold">Property enquiries</h1>
        <p className="text-slate-600">
          Qualify public prospects, create opportunities, collect the required
          deposit and then register or link the customer.
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
      <div className="grid min-w-0 gap-5 lg:grid-cols-[minmax(0,1.25fr)_minmax(0,1.75fr)]">
        <section className="min-w-0 space-y-3" aria-label="Property enquiry queue">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <div>
              <h2 className="font-semibold">Enquiry register</h2>
              <p className="text-sm text-slate-600">
                {queue.data?.totalCount ?? 0} matching enquiries
              </p>
            </div>
            <Button variant="outline" size="sm" onClick={() => void queue.refetch()}>
              Refresh
            </Button>
          </div>
          <div className="space-y-2 rounded-lg border bg-white p-3">
            <label htmlFor="property-enquiry-search" className="text-sm font-medium">
              Search enquiries
            </label>
            <Input
              id="property-enquiry-search"
              type="search"
              maxLength={100}
              value={searchInput}
              onChange={(event) => setSearchInput(event.target.value)}
              placeholder="Reference, subject or enquirer"
            />
            <div className="grid gap-2 sm:grid-cols-2">
              <label className="space-y-1 text-xs font-medium">
                <span>Status</span>
                <select
                  aria-label="Filter by status"
                  className="h-9 w-full rounded-md border border-input bg-background px-2 text-sm"
                  value={statusFilter}
                  onChange={(event) => {
                    setStatusFilter(event.target.value);
                    setPage(1);
                  }}
                >
                  <option value="all">All statuses</option>
                  <option value="Acknowledged">Acknowledged</option>
                  <option value="InProgress">In progress</option>
                  <option value="PendingUser">Pending user</option>
                  <option value="PendingThirdParty">Pending third party</option>
                  <option value="Resolved">Resolved</option>
                  <option value="Closed">Closed</option>
                  <option value="Reopened">Reopened</option>
                </select>
              </label>
              <label className="space-y-1 text-xs font-medium">
                <span>CRM linkage</span>
                <select
                  aria-label="Filter by CRM linkage"
                  className="h-9 w-full rounded-md border border-input bg-background px-2 text-sm"
                  value={crmFilter}
                  onChange={(event) => {
                    setCrmFilter(event.target.value);
                    setPage(1);
                  }}
                >
                  <option value="all">All enquiries</option>
                  <option value="linked">Linked to CRM</option>
                  <option value="unlinked">Not linked to CRM</option>
                </select>
              </label>
              <label className="space-y-1 text-xs font-medium">
                <span>Received</span>
                <select
                  aria-label="Filter by received date"
                  className="h-9 w-full rounded-md border border-input bg-background px-2 text-sm"
                  value={dateFilter}
                  onChange={(event) => {
                    setDateFilter(event.target.value);
                    setPage(1);
                  }}
                >
                  <option value="all">Any time</option>
                  <option value="7">Last 7 days</option>
                  <option value="30">Last 30 days</option>
                  <option value="90">Last 90 days</option>
                </select>
              </label>
              <label className="space-y-1 text-xs font-medium">
                <span>Rows per page</span>
                <select
                  aria-label="Rows per page"
                  className="h-9 w-full rounded-md border border-input bg-background px-2 text-sm"
                  value={pageSize}
                  onChange={(event) => {
                    setPageSize(Number(event.target.value));
                    setPage(1);
                  }}
                >
                  <option value={10}>10</option>
                  <option value={25}>25</option>
                  <option value={50}>50</option>
                </select>
              </label>
            </div>
          </div>
          <div className="overflow-hidden rounded-lg border bg-white">
            <Table aria-label="Property enquiries">
              <TableHeader>
                <TableRow>
                  <TableHead>Enquiry</TableHead>
                  <TableHead>Enquirer</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead>Received</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {queue.data?.data.map((item) => (
                  <TableRow key={item.id} data-state={selectedId === item.id ? 'selected' : undefined}>
                    <TableCell>
                      <button
                        type="button"
                        disabled={sendEmail.isPending || submitHandoff.isPending}
                        aria-current={selectedId === item.id ? 'true' : undefined}
                        onClick={() => {
                          setSelectedId(item.id);
                          setEmailBody('');
                          setActivityType('Contact');
                          setActivityNotes('');
                          setQualificationNotes('');
                          setQualificationScore('40');
                          setAgreedAmount('');
                          setQualificationCurrency('GHS');
                          setOpportunityDraft({
                            amount: '',
                            currency: 'GHS',
                            expectedCloseDate: '',
                            reserveProperty: true,
                            reservationDays: '14',
                            notes: '',
                          });
                          setDepositDraft({
                            amount: '',
                            paymentMethod: 'BankTransfer',
                            transactionReference: '',
                            receivedAt: '',
                          });
                          setDepositAction(null);
                          setReversalReason('');
                          setDepositActionDate('');
                          setShowMatches(false);
                          setSelectedMatchId('');
                          setHandoffDraft({
                            salesReference: '',
                            agreedAmount: '',
                            requestedLeaseTerm: '',
                            salesAmountPaid: '',
                            salesPaymentReference: '',
                            currency: '',
                            salesCompletedAt: '',
                            notes: '',
                          });
                          sendEmail.reset();
                          submitHandoff.reset();
                        }}
                        className="max-w-[12rem] text-left font-semibold text-blue-700 hover:underline focus-visible:outline focus-visible:outline-2 focus-visible:outline-blue-600 disabled:opacity-50"
                      >
                        <span className="block">{item.ticketNumber}</span>
                        <span className="block truncate text-xs font-normal text-slate-600" title={item.subject}>
                          {item.subject}
                        </span>
                      </button>
                    </TableCell>
                    <TableCell className="max-w-[9rem] truncate" title={item.requesterName || undefined}>
                      {item.requesterName || 'Public prospect'}
                    </TableCell>
                    <TableCell>{item.status.replace(/([a-z])([A-Z])/g, '$1 $2')}</TableCell>
                    <TableCell className="whitespace-nowrap">
                      {new Date(item.createdAt).toLocaleDateString()}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
            {queue.isLoading && <p className="p-4 text-sm text-slate-600">Loading enquiries…</p>}
            {!queue.isLoading && !queue.isError && queue.data?.data.length === 0 && (
              <p className="p-5 text-sm text-slate-600">
                No enquiries match these filters.
              </p>
            )}
          </div>
          <div className="flex flex-wrap items-center justify-between gap-2 text-sm">
            <span className="text-slate-600">
              {queue.data?.totalCount
                ? `Showing ${(page - 1) * pageSize + 1}–${Math.min(page * pageSize, queue.data.totalCount)} of ${queue.data.totalCount}`
                : 'Showing 0 enquiries'}
            </span>
            <div className="flex items-center gap-2">
              <Button
                variant="outline"
                size="sm"
                disabled={page <= 1 || queue.isFetching || sendEmail.isPending}
                onClick={() => setPage((current) => current - 1)}
              >
                Previous
              </Button>
              <span>Page {page} of {Math.max(1, Math.ceil((queue.data?.totalCount ?? 0) / pageSize))}</span>
              <Button
                variant="outline"
                size="sm"
                disabled={page * pageSize >= (queue.data?.totalCount ?? 0) || queue.isFetching || sendEmail.isPending}
                onClick={() => setPage((current) => current + 1)}
              >
                Next
              </Button>
            </div>
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
              <PropertyEnquiryDetails
                property={ticket.propertyListing}
                prospect={ticket.prospect}
                createdAt={ticket.createdAt}
                assignedToName={ticket.assignedToName}
              />
              <div>
                <h3 className="font-semibold">Original enquiry</h3>
                <p className="mt-2 whitespace-pre-wrap">{ticket.description}</p>
              </div>
              <section
                className="space-y-3 rounded-lg border border-sky-200 bg-sky-50 p-4"
                aria-label="Prospect qualification"
              >
                <div className="flex flex-wrap items-start justify-between gap-2">
                  <div>
                    <h3 className="font-semibold">Sales qualification</h3>
                    <p className="text-sm text-slate-600">
                      {prospect?.businessPartnerId && !prospect.leadId
                        ? 'Qualify this enquiry against the linked customer account. A duplicate CRM lead will not be created.'
                        : 'Work the public enquiry as a prospect. This does not create an ERP user, employee or customer.'}
                    </p>
                  </div>
                  <span className="rounded-full border border-sky-300 bg-white px-3 py-1 text-sm font-medium">
                    {leadStatus}
                  </span>
                </div>
                {leadStatus === 'New' || leadStatus === 'Contacted' ? (
                  <>
                    <div className="grid gap-3 md:grid-cols-3">
                      <div className="space-y-1">
                        <Label htmlFor="qualification-score">
                          Qualification score
                        </Label>
                        <Input
                          id="qualification-score"
                          type="number"
                          min="1"
                          max="100"
                          value={qualificationScore}
                          onChange={(event) =>
                            setQualificationScore(event.target.value)
                          }
                        />
                      </div>
                      <div className="space-y-1">
                        <Label htmlFor="qualification-amount">
                          Agreed property amount
                        </Label>
                        <Input
                          id="qualification-amount"
                          type="number"
                          min="0.01"
                          step="0.01"
                          value={agreedAmount}
                          onChange={(event) =>
                            setAgreedAmount(event.target.value)
                          }
                        />
                      </div>
                      <div className="space-y-1">
                        <Label htmlFor="qualification-currency">Currency</Label>
                        <Input
                          id="qualification-currency"
                          maxLength={3}
                          value={qualificationCurrency}
                          readOnly
                          aria-readonly="true"
                        />
                        <p className="text-xs text-slate-600">
                          Inherited from the listed property.
                        </p>
                      </div>
                    </div>
                    <div className="space-y-1">
                      <Label htmlFor="qualification-notes">
                        Qualification notes
                      </Label>
                      <Textarea
                        id="qualification-notes"
                        rows={3}
                        maxLength={2000}
                        value={qualificationNotes}
                        placeholder="Record what Sales verified and why this prospect should proceed or stop."
                        onChange={(event) =>
                          setQualificationNotes(event.target.value)
                        }
                        disabled={qualify.isPending}
                      />
                    </div>
                    <div className="flex flex-wrap gap-2">
                      <Button
                        onClick={() => qualify.mutate()}
                        disabled={
                          !Number.isFinite(Number(qualificationScore)) ||
                          Number(qualificationScore) < 1 ||
                          Number(qualificationScore) > 100 ||
                          !Number.isFinite(Number(agreedAmount)) ||
                          Number(agreedAmount) <= 0 ||
                          !/^[A-Za-z]{3}$/.test(qualificationCurrency) ||
                          leadStatus !== 'Contacted' ||
                          qualify.isPending
                        }
                      >
                        Mark qualified
                      </Button>
                      <Button
                        variant="outline"
                        onClick={() => disqualify.mutate()}
                        disabled={
                          qualificationNotes.trim().length < 5 ||
                          disqualify.isPending
                        }
                      >
                        Mark disqualified
                      </Button>
                    </div>
                    {leadStatus === 'New' ? (
                      <p className="text-sm text-amber-700" role="status">
                        Before qualifying, go to Internal activity below, choose
                        Contact made, enter the contact notes, then select Record
                        contact.
                      </p>
                    ) : null}
                  </>
                ) : (
                  <p className="text-sm text-slate-700">
                    Qualification is complete. The original enquiry details
                    remain immutable.
                  </p>
                )}
              </section>

              <section
                className="space-y-3 rounded-lg border border-emerald-200 bg-emerald-50 p-4"
                aria-label="Opportunity, reservation and deposit"
              >
                <div>
                  <h3 className="font-semibold">
                    Opportunity, reservation and deposit
                  </h3>
                  <p className="text-sm text-slate-600">
                    Create the opportunity only after qualification. Customer
                    registration becomes available when the configured deposit
                    threshold is met.
                  </p>
                </div>
                {opportunity ? (
                  <div className="grid gap-2 rounded border border-emerald-200 bg-white p-3 text-sm md:grid-cols-2">
                    <p>
                      <span className="font-medium">Opportunity:</span>{' '}
                      {opportunity.referenceNumber || opportunity.id}
                    </p>
                    <p>
                      <span className="font-medium">Stage:</span>{' '}
                      {opportunity.stage}
                    </p>
                    <p>
                      <span className="font-medium">Value:</span>{' '}
                      {opportunity.currency}{' '}
                      {opportunity.amount.toLocaleString()}
                    </p>
                    <a
                      className="text-blue-700 underline"
                      href={`/crm/opportunities?opportunityId=${encodeURIComponent(opportunity.id)}`}
                    >
                      Open opportunity
                    </a>
                  </div>
                ) : (
                  <div className="space-y-2">
                    <p className="text-sm text-amber-800">
                      No opportunity has been created for this enquiry.
                    </p>
                    {leadStatus === 'Qualified' ? (
                      <div className="grid gap-3 rounded border border-emerald-200 bg-white p-3 md:grid-cols-2">
                        <div className="space-y-1">
                          <Label htmlFor="opportunity-amount">
                            Opportunity amount
                          </Label>
                          <Input
                            id="opportunity-amount"
                            type="number"
                            min="0.01"
                            step="0.01"
                            value={opportunityDraft.amount}
                            onChange={(event) =>
                              setOpportunityDraft((value) => ({
                                ...value,
                                amount: event.target.value,
                              }))
                            }
                          />
                        </div>
                        <div className="space-y-1">
                          <Label>Currency</Label>
                          <Select
                            value={opportunityDraft.currency}
                            disabled
                          >
                            <SelectTrigger aria-label="Opportunity Currency">
                              <SelectValue placeholder="Select currency" />
                            </SelectTrigger>
                            <SelectContent>
                              {opportunityCurrencyOptions.map((currency) => (
                                <SelectItem key={currency.code} value={currency.code}>
                                  {currency.code} — {currency.name}
                                </SelectItem>
                              ))}
                            </SelectContent>
                          </Select>
                          <p className="text-xs text-slate-600">
                            Inherited from the listed property.
                          </p>
                        </div>
                        <div className="space-y-1">
                          <Label htmlFor="opportunity-close-date">
                            Expected close date
                          </Label>
                          <Input
                            id="opportunity-close-date"
                            type="date"
                            min={new Date().toISOString().slice(0, 10)}
                            value={opportunityDraft.expectedCloseDate}
                            onChange={(event) =>
                              setOpportunityDraft((value) => ({
                                ...value,
                                expectedCloseDate: event.target.value,
                              }))
                            }
                          />
                        </div>
                        <div className="space-y-1">
                          <Label htmlFor="opportunity-reservation-days">
                            Reservation days
                          </Label>
                          <Input
                            id="opportunity-reservation-days"
                            type="number"
                            min="1"
                            max="365"
                            value={opportunityDraft.reservationDays}
                            disabled={!opportunityDraft.reserveProperty}
                            onChange={(event) =>
                              setOpportunityDraft((value) => ({
                                ...value,
                                reservationDays: event.target.value,
                              }))
                            }
                          />
                        </div>
                        <label className="flex items-center gap-2 text-sm md:col-span-2">
                          <input
                            type="checkbox"
                            checked={opportunityDraft.reserveProperty}
                            onChange={(event) =>
                              setOpportunityDraft((value) => ({
                                ...value,
                                reserveProperty: event.target.checked,
                              }))
                            }
                          />
                          Reserve this property when the opportunity is created
                        </label>
                        <div className="space-y-1 md:col-span-2">
                          <Label htmlFor="opportunity-notes">
                            Opportunity notes
                          </Label>
                          <Textarea
                            id="opportunity-notes"
                            rows={3}
                            maxLength={2000}
                            value={opportunityDraft.notes}
                            onChange={(event) =>
                              setOpportunityDraft((value) => ({
                                ...value,
                                notes: event.target.value,
                              }))
                            }
                          />
                        </div>
                      </div>
                    ) : null}
                    <Button
                      onClick={() => setConfirmOpportunity(true)}
                      disabled={
                        leadStatus !== 'Qualified' ||
                        !Number.isFinite(Number(opportunityDraft.amount)) ||
                        Number(opportunityDraft.amount) <= 0 ||
                        !/^[A-Za-z]{3}$/.test(opportunityDraft.currency) ||
                        !opportunityDraft.expectedCloseDate ||
                        (opportunityDraft.reserveProperty &&
                          (!Number.isFinite(
                            Number(opportunityDraft.reservationDays)
                          ) ||
                            Number(opportunityDraft.reservationDays) < 1 ||
                            Number(opportunityDraft.reservationDays) > 365)) ||
                        createOpportunity.isPending
                      }
                    >
                      Create Opportunity
                    </Button>
                    {leadStatus !== 'Qualified' ? (
                      <p className="text-xs text-slate-600">
                        Qualify the prospect before creating an opportunity.
                      </p>
                    ) : null}
                  </div>
                )}
                {prospect?.salesAllocationId ? (
                  <div className="rounded border border-emerald-200 bg-white p-3 text-sm">
                    <div className="flex flex-wrap items-center justify-between gap-2">
                      <p>
                        <span className="font-medium">Reservation status:</span>{' '}
                        <span className="rounded-full bg-amber-100 px-2 py-0.5 font-medium text-amber-800">
                          {prospect.salesAllocationStatus || 'Reserved'}
                        </span>
                      </p>
                      <Button asChild size="sm" variant="outline">
                        <a href={`/sales/allocations/${prospect.salesAllocationId}`}>
                          Open reservation
                        </a>
                      </Button>
                    </div>
                    {prospect.salesAllocationReservedUntil ? (
                      <p className="mt-2">
                        <span className="font-medium">Reserved until:</span>{' '}
                        {new Date(
                          prospect.salesAllocationReservedUntil
                        ).toLocaleString()}
                      </p>
                    ) : null}
                    <p>
                      <span className="font-medium">Allocation ID:</span>{' '}
                      {prospect.salesAllocationId}
                    </p>
                  </div>
                ) : opportunity ? (
                  <p className="text-sm text-slate-600">
                    No reservation is recorded yet.
                  </p>
                ) : null}
                {prospect && prospect.opportunityId ? (
                  <div className="grid gap-2 rounded border border-emerald-200 bg-white p-3 text-sm md:grid-cols-2">
                    <p>
                      <span className="font-medium">Deposit rule:</span>{' '}
                      {prospect.depositRequirementType}
                    </p>
                    <p>
                      <span className="font-medium">Threshold:</span>{' '}
                      {prospect.depositThresholdMet ? 'Met' : 'Not met'}
                    </p>
                    <p>
                      <span className="font-medium">Required:</span>{' '}
                      {prospect.currency}{' '}
                      {prospect.requiredDeposit.toLocaleString()}
                    </p>
                    <p>
                      <span className="font-medium">Cleared:</span>{' '}
                      {prospect.currency}{' '}
                      {prospect.clearedDeposit.toLocaleString()}
                    </p>
                    <p>
                      <span className="font-medium">Remaining:</span>{' '}
                      {prospect.currency}{' '}
                      {Math.max(
                        0,
                        prospect.requiredDeposit - prospect.clearedDeposit
                      ).toLocaleString()}
                    </p>
                  </div>
                ) : opportunity ? (
                  <p className="text-sm text-slate-600">
                    Deposit readiness is not available yet.
                  </p>
                ) : null}
                {prospect?.opportunityId ? (
                  <div className="space-y-3 rounded border border-emerald-200 bg-white p-3">
                    <div className="grid gap-2 md:grid-cols-6">
                      <Input
                        aria-label="Deposit amount"
                        type="number"
                        min="0.01"
                        step="0.01"
                        placeholder="Amount"
                        value={depositDraft.amount}
                        onChange={(event) =>
                          setDepositDraft((value) => ({
                            ...value,
                            amount: event.target.value,
                          }))
                        }
                      />
                      <Input
                        aria-label="Deposit currency"
                        maxLength={3}
                        placeholder="Currency"
                        value={depositCurrency}
                        disabled
                      />
                      <select
                        aria-label="Payment method"
                        className="rounded-md border bg-white p-2 text-sm"
                        value={depositDraft.paymentMethod}
                        onChange={(event) =>
                          setDepositDraft((value) => ({
                            ...value,
                            paymentMethod: event.target.value,
                          }))
                        }
                      >
                        <option value="BankTransfer">Bank transfer</option>
                        <option value="Cash">Cash</option>
                        <option value="Cheque">Cheque</option>
                        <option value="Card">Card</option>
                        <option value="MobileMoney">Mobile money</option>
                      </select>
                      <Input
                        aria-label="Transaction reference"
                        maxLength={100}
                        placeholder="Reference"
                        value={depositDraft.transactionReference}
                        onChange={(event) =>
                          setDepositDraft((value) => ({
                            ...value,
                            transactionReference: event.target.value,
                          }))
                        }
                      />
                      <Input
                        aria-label="Receipt date and time"
                        type="datetime-local"
                        value={depositDraft.receivedAt}
                        onChange={(event) =>
                          setDepositDraft((value) => ({
                            ...value,
                            receivedAt: event.target.value,
                          }))
                        }
                      />
                      <Button
                        onClick={() => recordDeposit.mutate()}
                        disabled={
                          recordDeposit.isPending ||
                          !Number.isFinite(Number(depositDraft.amount)) ||
                          Number(depositDraft.amount) <= 0 ||
                          !/^[A-Z]{3}$/.test(depositCurrency)
                        }
                      >
                        {recordDeposit.isPending
                          ? 'Recording…'
                          : 'Record deposit'}
                      </Button>
                    </div>
                    <p className="text-xs text-slate-600">
                      Recorded receipts remain pending until an authorized user
                      clears them. Only cleared, unreversed deposits count
                      toward customer registration.
                    </p>
                    {depositReceipts.isLoading ? (
                      <p className="text-sm">Loading deposit receipts…</p>
                    ) : null}
                    {depositReceipts.data?.map((receipt) => (
                      <div
                        key={receipt.id}
                        className="flex flex-wrap items-center justify-between gap-2 rounded border p-2 text-sm"
                      >
                        <div>
                          <p className="font-medium">
                            {receipt.receiptNumber} · {receipt.status}
                          </p>
                          <p>
                            {receipt.currency} {receipt.amount.toLocaleString()}{' '}
                            · {receipt.paymentMethod}
                            {receipt.transactionReference
                              ? ` · ${receipt.transactionReference}`
                              : ''}
                          </p>
                          <p className="text-xs text-slate-600">
                            Received{' '}
                            {new Date(receipt.receivedAt).toLocaleString()}
                            {receipt.clearedAt
                              ? ` · Cleared ${new Date(receipt.clearedAt).toLocaleString()}`
                              : ''}
                            {receipt.reversedAt
                              ? ` · Reversed ${new Date(receipt.reversedAt).toLocaleString()}`
                              : ''}
                          </p>
                        </div>
                        <div className="flex gap-2">
                          {receipt.status === 'Pending' && canClear ? (
                            <Button
                              size="sm"
                              variant="outline"
                              onClick={() =>
                                setDepositAction({
                                  kind: 'clear',
                                  receiptId: receipt.id,
                                  receiptNumber: receipt.receiptNumber,
                                })
                              }
                            >
                              Confirm and post deposit
                            </Button>
                          ) : null}
                          {receipt.status === 'Cleared' && canReverse ? (
                            <Button
                              size="sm"
                              variant="outline"
                              onClick={() =>
                                setDepositAction({
                                  kind: 'reverse',
                                  receiptId: receipt.id,
                                  receiptNumber: receipt.receiptNumber,
                                })
                              }
                            >
                              Reverse
                            </Button>
                          ) : null}
                        </div>
                      </div>
                    ))}
                  </div>
                ) : null}
              </section>

              <section
                className="space-y-3 rounded-lg border p-4"
                aria-label="Customer business partner"
              >
                <div>
                  <h3 className="font-semibold">Customer Business Partner</h3>
                  <p className="text-sm text-slate-600">
                    Match qualified prospects against approved existing
                    customers first. Creating a new customer remains locked
                    until the cleared deposit threshold is met.
                  </p>
                </div>
                {prospect?.businessPartnerId ? (
                  <div className="rounded border bg-slate-50 p-3 text-sm">
                    <p className="font-medium">
                      Customer Business Partner linked
                    </p>
                    <p>
                      {prospect.businessPartnerCode &&
                      prospect.businessPartnerName
                        ? `${prospect.businessPartnerCode} · ${prospect.businessPartnerName}`
                        : prospect.businessPartnerName ||
                          prospect.businessPartnerCode ||
                          prospect.businessPartnerId}
                    </p>
                    <p>{prospect.status}</p>
                    {prospect.status === 'CustomerPendingApproval' ? (
                      <Button
                        className="mt-2"
                        variant="outline"
                        onClick={() => finalizePartner.mutate()}
                        disabled={
                          !depositThresholdMet || finalizePartner.isPending
                        }
                      >
                        Finalize after approval
                      </Button>
                    ) : null}
                    {prospect.status === 'Opportunity' ? (
                      <div className="mt-3 space-y-2">
                        <p className="text-xs text-slate-600">
                          This approved existing customer is linked. Finalize
                          the conversion after the cleared deposit reaches the
                          configured threshold.
                        </p>
                        <Button
                          variant="outline"
                          onClick={() => finalizePartner.mutate()}
                          disabled={
                            !depositThresholdMet || finalizePartner.isPending
                          }
                        >
                          Finalize customer conversion
                        </Button>
                      </div>
                    ) : null}
                    {prospect.status === 'Converted' &&
                    prospect.opportunityId &&
                    !salesOrder ? (
                      <div className="mt-3 border-t pt-3">
                        <p className="mb-2 text-xs text-slate-600">
                          The customer account is approved and the prospect
                          deposit is now a customer advance. Create the Sales
                          Order for this opportunity before applying it to an
                          invoice.
                        </p>
                        {salesOrderSource.isLoading ? (
                          <p className="text-xs text-slate-600">
                            Resolving the exact Estate listing for this Sales Order...
                          </p>
                        ) : salesOrderSource.isError ? (
                          <Alert variant="destructive">
                            <AlertDescription>
                              {salesOrderSource.error instanceof Error
                                ? salesOrderSource.error.message
                                : 'The property listing could not be resolved for a Sales Order.'}
                              <Button
                                className="ml-2"
                                size="sm"
                                variant="outline"
                                onClick={() => salesOrderSource.refetch()}
                              >
                                Try again
                              </Button>
                            </AlertDescription>
                          </Alert>
                        ) : salesOrderSource.data ? (
                          <SalesHandoffActions
                            context={{
                              businessPartnerId: prospect.businessPartnerId,
                              businessPartnerName:
                                prospect.businessPartnerName || undefined,
                              leadId: prospect.leadId || undefined,
                              leadName:
                                ticket.propertyListing?.contactName ||
                                ticket.subject,
                              opportunityId: prospect.opportunityId,
                              opportunityName:
                                opportunity?.referenceNumber || ticket.subject,
                              currency: prospect.currency,
                              estimatedValue: prospect.agreedAmount,
                              propertyReference:
                                ticket.propertyListing?.listingReference,
                              propertyType:
                                ticket.propertyListing?.listingType,
                              contextLabel: `Public property enquiry ${ticket.ticketNumber}`,
                              lockedPropertyEnquirySalesOrderSource:
                                salesOrderSource.data,
                            }}
                            size="sm"
                            showUnavailableHint={false}
                            showSalesAgreement={false}
                          />
                        ) : null}
                      </div>
                    ) : null}
                  </div>
                ) : (
                  <>
                    {!depositThresholdMet && prospect?.businessPartnerId ? (
                      <p className="text-sm text-amber-800">
                        The configured deposit threshold must still be met
                        before finalizing this existing customer enquiry.
                      </p>
                    ) : !depositThresholdMet ? (
                      <p className="text-sm text-amber-800">
                        New customer registration is locked until a cleared
                        deposit meets the configured threshold. An already
                        approved customer can still be matched and linked.
                      </p>
                    ) : null}
                    {!canMatchExistingCustomer &&
                    (leadStatus === 'New' || leadStatus === 'Contacted') ? (
                      <p className="text-sm text-slate-600">
                        Qualify the prospect before searching for or linking an
                        existing customer.
                      </p>
                    ) : null}
                    <div className="flex flex-wrap gap-2">
                      <Button
                        variant="outline"
                        onClick={() => setShowMatches((value) => !value)}
                        disabled={!canMatchExistingCustomer}
                      >
                        {showMatches
                          ? 'Hide matches'
                          : 'Find existing customer'}
                      </Button>
                      <Button
                        onClick={() => setCreatePartnerOpen(true)}
                        disabled={
                          !depositThresholdMet ||
                          !showMatches ||
                          partnerMatches.isLoading ||
                          partnerMatches.data === undefined ||
                          Boolean(partnerMatches.data?.length)
                        }
                      >
                        Create customer
                      </Button>
                    </div>
                    {showMatches ? (
                      <div className="space-y-2">
                        {partnerMatches.isLoading ? (
                          <p className="text-sm">
                            Checking email and phone matches…
                          </p>
                        ) : null}
                        {partnerMatches.data?.length === 0 ? (
                          <p className="text-sm text-slate-600">
                            No likely existing customer was found. You may
                            create a new governed customer record.
                          </p>
                        ) : null}
                        {partnerMatches.data?.map((match) => (
                          <label
                            key={match.id}
                            className="flex cursor-pointer gap-3 rounded border bg-white p-3 text-sm"
                          >
                            <input
                              type="radio"
                              name="partner-match"
                              value={match.id}
                              checked={selectedMatchId === match.id}
                              onChange={() => setSelectedMatchId(match.id)}
                            />
                            <span className="space-y-1">
                              <span className="block font-medium">
                                {match.partnerName}
                              </span>
                              <span className="block">
                                {match.email || 'No email'} ·{' '}
                                {match.phone || 'No phone'}
                              </span>
                              <span className="block text-slate-600">
                                {match.matchedOn.join(', ')} ·{' '}
                                {match.approvalStatus ||
                                  'Unknown approval status'}
                              </span>
                            </span>
                          </label>
                        ))}
                        {partnerMatches.data?.length ? (
                          <Button
                            onClick={() => setConfirmLink(true)}
                            disabled={
                              !canMatchExistingCustomer ||
                              !selectedMatchId ||
                              linkPartner.isPending
                            }
                          >
                            Link selected customer
                          </Button>
                        ) : null}
                      </div>
                    ) : null}
                  </>
                )}
              </section>

              <section
                className="grid gap-4 rounded-lg border p-4 md:grid-cols-2"
                aria-label="Prospect communications"
              >
                <div className="space-y-2">
                  <h3 className="font-semibold">Internal activity</h3>
                  <Label htmlFor="prospect-activity-type">Activity type</Label>
                  <select
                    id="prospect-activity-type"
                    className="w-full rounded-md border bg-white p-2"
                    value={activityType}
                    onChange={(event) =>
                      setActivityType(
                        event.target.value as ProspectActivityType
                      )
                    }
                  >
                    {leadStatus === 'New' || leadStatus === 'Contacted' ? (
                      <option value="Contact">Contact made</option>
                    ) : null}
                    <option value="Note">Internal note</option>
                    <option value="FollowUp">Follow-up</option>
                  </select>
                  <Textarea
                    aria-label="Activity notes"
                    rows={4}
                    maxLength={2000}
                    value={activityNotes}
                    onChange={(event) => setActivityNotes(event.target.value)}
                    placeholder="Record the call, meeting, note or follow-up action."
                  />
                  {activityType === 'FollowUp' ? (
                    <Input
                      type="datetime-local"
                      value={followUpAt}
                      onChange={(event) => setFollowUpAt(event.target.value)}
                    />
                  ) : null}
                  <Button
                    variant="outline"
                    onClick={() => addActivity.mutate()}
                    disabled={
                      activityNotes.trim().length < 3 ||
                      (activityType === 'FollowUp' && !followUpAt) ||
                      addActivity.isPending
                    }
                  >
                    {activityType === 'Contact'
                      ? 'Record contact'
                      : 'Save activity'}
                  </Button>
                </div>
                <div className="space-y-2">
                  <h3 className="font-semibold">Email prospect</h3>
                  <p className="text-xs text-slate-600">
                    Sends a real email to{' '}
                    {ticket.propertyListing?.contactEmail ||
                      'the address captured on the enquiry'}{' '}
                    and records delivery status.
                  </p>
                  <p className="text-xs text-slate-600">
                    Subject: Re: {ticket.subject || ticket.ticketNumber}
                  </p>
                  <Label htmlFor="property-enquiry-email-body">Message</Label>
                  <Textarea
                    id="property-enquiry-email-body"
                    rows={5}
                    maxLength={4000}
                    value={emailBody}
                    onChange={(event) => setEmailBody(event.target.value)}
                    disabled={sendEmail.isPending}
                  />
                  <Button
                    onClick={() => sendEmail.mutate()}
                    disabled={
                      !emailBody.trim() ||
                      sendEmail.isPending ||
                      !ticket.propertyListing?.contactEmail
                    }
                  >
                    {sendEmail.isPending ? 'Sending…' : 'Send email'}
                  </Button>
                </div>
              </section>
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
                {salesOrder ? (
                  <div className="grid gap-2 rounded border border-indigo-200 bg-white p-3 text-sm md:grid-cols-2">
                    <p>
                      <span className="font-medium">Sales order:</span>{' '}
                      {salesOrder.reference}
                    </p>
                    <p>
                      <span className="font-medium">Status:</span>{' '}
                      {salesOrder.status}
                    </p>
                    <p>
                      <span className="font-medium">Amount paid:</span>{' '}
                      {salesOrder.currency}{' '}
                      {salesOrder.amountPaid.toLocaleString()}
                    </p>
                    <p>
                      <span className="font-medium">Payment reference:</span>{' '}
                      {salesOrder.paymentReference ||
                        'No allocated receipt yet'}
                    </p>
                  </div>
                ) : null}
                {handoffState?.canHandoff && !prospect?.businessPartnerId ? (
                  <p className="text-sm text-amber-800">
                    Link or finalize the Customer Business Partner before
                    handing this completed sale to Estate.
                  </p>
                ) : null}
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
                ) : !opportunity.isWon ? (
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
                          : 'Recorded as won'}
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
                      {handoffRequiresDuration ? (
                        <div className="space-y-1">
                          <Label htmlFor="estate-requested-term">
                            Agreed{' '}
                            {listingType.includes('Lease') ? 'lease' : 'rent'}{' '}
                            duration
                          </Label>
                          <Input
                            id="estate-requested-term"
                            value={handoffDraft.requestedLeaseTerm}
                            maxLength={120}
                            placeholder={
                              listingType.includes('Lease')
                                ? 'Example: 50 years'
                                : 'Example: 12 months'
                            }
                            onChange={(event) =>
                              setHandoffDraft((value) => ({
                                ...value,
                                requestedLeaseTerm: event.target.value,
                              }))
                            }
                          />
                        </div>
                      ) : null}
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
                          onChange={(event) => {
                            salesAmountPaidEditedRef.current = true;
                            setHandoffDraft((value) => ({
                              ...value,
                              salesAmountPaid: event.target.value,
                            }));
                          }}
                        />
                        {prospect && prospect.clearedDeposit > 0 ? (
                          <p className="text-xs text-slate-600">
                            Cleared prospect deposits: {prospect.currency}{' '}
                            {prospect.clearedDeposit.toLocaleString()}. Verify the
                            total against Sales payments before handover.
                          </p>
                        ) : null}
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
      <ConfirmationDialog
        open={confirmOpportunity}
        onOpenChange={setConfirmOpportunity}
        title="Create an opportunity?"
        description="This creates the explicit Sales opportunity for the qualified prospect and links it to the original enquiry. It does not register the prospect as a customer."
        confirmText="Create Opportunity"
        onConfirm={async () => {
          try {
            await createOpportunity.mutateAsync();
          } catch {
            // The mutation shows the server error in a toast and closes this prompt.
            return false;
          }
        }}
        isLoading={createOpportunity.isPending}
      />
      <ConfirmationDialog
        open={confirmLink}
        onOpenChange={setConfirmLink}
        title="Link the selected customer?"
        description="The prospect will be linked to the existing Business Partner. The original enquiry name, phones, email, message and property snapshot will remain unchanged."
        confirmText="Link customer"
        onConfirm={async () => {
          await linkPartner.mutateAsync();
        }}
        isLoading={linkPartner.isPending}
        confirmDisabled={!selectedMatchId}
      />
      <Dialog
        open={Boolean(depositAction)}
        onOpenChange={(open) => {
          if (!open && !decideDeposit.isPending) {
            setDepositAction(null);
            setReversalReason('');
            setDepositActionDate('');
          }
        }}
      >
        <DialogContent className="sm:max-w-lg">
          <DialogHeader>
            <DialogTitle>
              {depositAction?.kind === 'clear'
                ? 'Confirm deposit receipt?'
                : 'Reverse cleared deposit?'}
            </DialogTitle>
            <DialogDescription>
              {depositAction?.kind === 'clear'
                ? `Confirming ${depositAction?.receiptNumber || 'this receipt'} makes it count toward the customer-registration threshold and posts through the configured prospect-deposit accounts.`
                : `Reversing ${depositAction?.receiptNumber || 'this receipt'} removes it from the cleared threshold. The audit trail and original receipt remain.`}
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-1">
            <Label htmlFor="deposit-action-date">
              {depositAction?.kind === 'clear'
                ? 'Deposit confirmation date and time'
                : 'Reversal date and time'}
            </Label>
            <Input
              id="deposit-action-date"
              type="datetime-local"
              value={depositActionDate}
              onChange={(event) => setDepositActionDate(event.target.value)}
            />
            <p className="text-xs text-slate-600">
              Leave blank to use the current server time.
            </p>
          </div>
          {depositAction?.kind === 'reverse' ? (
            <div className="space-y-1">
              <Label htmlFor="deposit-reversal-reason">Reversal reason</Label>
              <Textarea
                id="deposit-reversal-reason"
                rows={3}
                maxLength={1000}
                value={reversalReason}
                onChange={(event) => setReversalReason(event.target.value)}
              />
            </div>
          ) : null}
          <DialogFooter>
            <Button
              variant="outline"
              onClick={() => {
                setDepositAction(null);
                setReversalReason('');
                setDepositActionDate('');
              }}
              disabled={decideDeposit.isPending}
            >
              Cancel
            </Button>
            <Button
              onClick={() => decideDeposit.mutate()}
              disabled={
                decideDeposit.isPending ||
                Boolean(
                  depositAction?.kind === 'reverse' &&
                    reversalReason.trim().length < 5
                )
              }
            >
              {decideDeposit.isPending
                ? 'Saving…'
                : depositAction?.kind === 'clear'
                  ? 'Confirm and post deposit'
                  : 'Reverse receipt'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
      <Dialog
        open={createPartnerOpen}
        onOpenChange={(open) => {
          if (!createPartner.isPending) setCreatePartnerOpen(open);
        }}
      >
        <DialogContent className="sm:max-w-xl">
          <DialogHeader>
            <DialogTitle>Create customer Business Partner</DialogTitle>
            <DialogDescription>
              The original public contact has been prefilled. This submits the
              customer through the existing governed approval process.
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-3 sm:grid-cols-2">
            <div className="space-y-1 sm:col-span-2">
              <Label htmlFor="prospect-partner-name">Customer name</Label>
              <Input
                id="prospect-partner-name"
                maxLength={200}
                value={partnerDraft.partnerName || ''}
                onChange={(event) =>
                  setPartnerDraft((value) => ({
                    ...value,
                    partnerName: event.target.value,
                  }))
                }
              />
            </div>
            <div className="space-y-1">
              <Label htmlFor="prospect-partner-email">Email</Label>
              <Input
                id="prospect-partner-email"
                type="email"
                maxLength={320}
                value={partnerDraft.email || ''}
                onChange={(event) =>
                  setPartnerDraft((value) => ({
                    ...value,
                    email: event.target.value,
                  }))
                }
              />
            </div>
            <div className="space-y-1">
              <Label htmlFor="prospect-partner-phone">Phone</Label>
              <Input
                id="prospect-partner-phone"
                type="tel"
                maxLength={50}
                value={partnerDraft.phone || ''}
                onChange={(event) =>
                  setPartnerDraft((value) => ({
                    ...value,
                    phone: event.target.value,
                  }))
                }
              />
            </div>
            <div className="space-y-1 sm:col-span-2">
              <Label htmlFor="prospect-partner-address">Physical address</Label>
              <Input
                id="prospect-partner-address"
                maxLength={500}
                value={partnerDraft.physicalAddress || ''}
                onChange={(event) =>
                  setPartnerDraft((value) => ({
                    ...value,
                    physicalAddress: event.target.value,
                  }))
                }
              />
            </div>
            <div className="space-y-1">
              <Label htmlFor="prospect-partner-city">City</Label>
              <Input
                id="prospect-partner-city"
                maxLength={100}
                value={partnerDraft.city || ''}
                onChange={(event) =>
                  setPartnerDraft((value) => ({
                    ...value,
                    city: event.target.value,
                  }))
                }
              />
            </div>
            <div className="space-y-1">
              <Label htmlFor="prospect-partner-country">Country</Label>
              <Input
                id="prospect-partner-country"
                maxLength={100}
                value={partnerDraft.country || ''}
                onChange={(event) =>
                  setPartnerDraft((value) => ({
                    ...value,
                    country: event.target.value,
                  }))
                }
              />
            </div>
            <div className="space-y-1">
              <Label htmlFor="prospect-partner-postal-code">Postal code</Label>
              <Input
                id="prospect-partner-postal-code"
                maxLength={20}
                value={partnerDraft.postalCode || ''}
                onChange={(event) =>
                  setPartnerDraft((value) => ({
                    ...value,
                    postalCode: event.target.value,
                  }))
                }
              />
            </div>
          </div>
          <DialogFooter>
            <Button
              variant="outline"
              onClick={() => setCreatePartnerOpen(false)}
              disabled={createPartner.isPending}
            >
              Cancel
            </Button>
            <Button
              onClick={() => createPartner.mutate()}
              disabled={
                createPartner.isPending ||
                !partnerDraft.partnerName?.trim() ||
                !partnerDraft.email?.trim() ||
                !partnerDraft.phone?.trim()
              }
            >
              {createPartner.isPending
                ? 'Submitting…'
                : 'Submit customer registration'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
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
