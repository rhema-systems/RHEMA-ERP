'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2, Pencil, Plus, Send, ShieldAlert, Stamp, Stethoscope, Umbrella, Check, TriangleAlert } from 'lucide-react';
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
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { Badge } from '@/components/ui/badge';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import {
  DateField,
  FieldRow,
  NumberField,
  SelectField,
  SwitchField,
  TextField,
  TextareaField,
} from '@/components/hr/employee/tabs/fields';
import { CurrencyField } from '@/components/hr/common/CurrencyPicker';
import { useAuth } from '@/hooks/use-auth';
import { useToast } from '@/hooks/use-toast';
import { travelComplianceService } from '@/services/hr/travel-compliance.service';
import { travelBookingsService } from '@/services/hr/travel-bookings.service';
import { TravelQueryError } from './TravelQueryError';
import { fmtTravelMoney as fmtMoney } from './travel-format';
import { VISA_REQUIREMENT_TYPE_LABELS } from '@/types/hr/travel-compliance';
import type { StaffTravelTripHealthRequirement } from '@/types/hr/travel-compliance';
import { Textarea } from '@/components/ui/textarea';
import type { StaffTravelRequest } from '@/types/hr/travel';

const VISA_STATUSES = [
  'NotStarted', 'InPreparation', 'Submitted', 'Approved', 'Rejected', 'Expired', 'NotRequired',
] as const;
const RISK_LEVELS = ['Low', 'Medium', 'High', 'Critical', 'Prohibited'] as const;
const RISK_CATEGORIES = [
  'Security', 'Health', 'NaturalDisaster', 'PoliticalInstability', 'Infrastructure', 'Crime', 'Other',
] as const;
const INSURANCE_TYPES = ['CorporateGroup', 'Individual', 'TopUp', 'Statutory'] as const;
const COVERAGE_TYPES = [
  'Medical', 'TripCancellation', 'Baggage', 'PersonalLiability', 'EmergencyEvacuation',
  'Comprehensive',
] as const;

const humanize = (v: string) => v.replace(/([a-z])([A-Z])/g, '$1 $2');
const options = (values: readonly string[]) => values.map((v) => ({ value: v, label: humanize(v) }));
const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const fmtDateTime = (v?: string | null) => (v ? new Date(v).toLocaleString() : '—');

/** An untouched date field registers as '' — the server's DateOnly binder rejects that. */
const orNull = (v?: string | null) => (v && v.trim() ? v : null);

const visaSchema = z.object({
  visaType: z.string().max(100).optional(),
  status: z.enum(VISA_STATUSES),
  submittedDate: z.string().optional(),
  approvedDate: z.string().optional(),
  expiryDate: z.string().optional(),
  visaNumber: z.string().max(100).optional(),
  processingFee: z.coerce.number().min(0).optional(),
  currencyCode: z.string().optional(),
  notes: z.string().max(2000).optional(),
});

const riskSchema = z.object({
  destinationCity: z.string().max(100).optional(),
  riskLevel: z.enum(RISK_LEVELS),
  riskCategory: z.enum(RISK_CATEGORIES),
  assessmentSource: z.string().max(200).optional(),
  assessmentSummary: z.string().max(2000).optional(),
  mitigationRequired: z.boolean(),
  mitigationNotes: z.string().max(2000).optional(),
  dutyOfCareBriefingSent: z.boolean(),
  validUntil: z.string().optional(),
});

const insuranceSchema = z.object({
  policyNumber: z.string().max(100).optional(),
  insuranceType: z.enum(INSURANCE_TYPES),
  coverageType: z.enum(COVERAGE_TYPES),
  coverageStart: z.string().min(1, 'Required'),
  coverageEnd: z.string().min(1, 'Required'),
  sumInsured: z.coerce.number().min(0),
  premium: z.coerce.number().min(0),
  currencyCode: z.string().min(1, 'Select a currency'),
  emergencyContact: z.string().max(200).optional(),
});

/**
 * A trip's compliance: the visa, the risk assessment the traveller has to read, and the insurance.
 *
 * ⚠ **The acknowledgement is the traveller's alone.** The server answers 403 for anyone else,
 * because it records that a specific person read a security briefing about where they are going.
 * It was previously settable by any Write holder, which made the record assert something that had
 * not happened. Nothing here should become a desk-side "mark as briefed". The button now shows
 * only to the traveller — on this desk page, an HR officer looking at their own trip — because
 * for everyone else it could only fail. A traveller without desk access has no screen to
 * acknowledge from at all yet (travel final closure, finding E1 — lane 7 builds it).
 *
 * The reference data behind these — visa requirements between two countries, health requirements
 * per destination, the destination alert feed — lives under `/administration/hr/travel`, because it
 * outlives any one trip.
 */
export function TravelCompliancePanel({ request }: { request: StaffTravelRequest }) {
  const requestId = request.id;
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const { user } = useAuth();
  const isTraveller = !!user?.employeeId && user.employeeId === request.employeeId;
  const [dialog, setDialog] = useState<'visa' | 'risk' | 'insurance' | null>(null);

  // ⚠ The currency lists are read through `api/hr/currencies` inside each CurrencyField. This panel
  // read `api/finance/currencies`, which answers 403 without a Finance permission, so no visa fee
  // or insurance cover could be recorded by the HR desk (travel final closure, lane 0 — O-19).

  const { data: visas, isLoading, isError: visasFailed, error: visasError } = useQuery({
    queryKey: ['travel-visas', requestId],
    queryFn: () => travelComplianceService.getVisaApplicationsByRequest(requestId),
  });

  const {
    data: assessment, isError: assessmentFailed, error: assessmentError,
  } = useQuery({
    queryKey: ['travel-risk-assessment', requestId],
    queryFn: () => travelComplianceService.getCurrentRiskAssessment(requestId),
  });

  const { data: insurance, isError: insuranceFailed, error: insuranceError } = useQuery({
    queryKey: ['travel-insurance', requestId],
    queryFn: () => travelComplianceService.getInsuranceByRequest(requestId),
  });

  const { data: alerts, isError: alertsFailed, error: alertsError } = useQuery({
    queryKey: ['travel-alerts-country', request.destinationCountryId],
    queryFn: () => travelComplianceService.getCurrentAlertsForCountry(request.destinationCountryId),
  });

  // Lane 6 (D-29, FX-8's read half): what Fleet records against the trip's company vehicles on its fleet trips.
  const { data: incidents, isError: incidentsFailed, error: incidentsError } = useQuery({
    queryKey: ['travel-fleet-incidents', requestId],
    queryFn: () => travelBookingsService.getFleetIncidents(requestId),
  });

  // Lane 7 (D-36, T-25): the destination's health requirements over the trip, each cleared by the desk or not.
  const { data: health, isError: healthFailed, error: healthError } = useQuery({
    queryKey: ['travel-health-requirements', requestId],
    queryFn: () => travelComplianceService.getTripHealthRequirements(requestId),
  });
  const [clearing, setClearing] = useState<StaffTravelTripHealthRequirement | null>(null);
  const [clearNote, setClearNote] = useState('');
  const clearHealth = useMutation({
    mutationFn: ({ id, note }: { id: string; note: string | null }) =>
      travelComplianceService.clearHealthRequirement(requestId, id, note),
    onSuccess: async () => {
      toast({ title: 'Requirement cleared' });
      setClearing(null);
      await queryClient.invalidateQueries({ queryKey: ['travel-health-requirements', requestId] });
    },
    onError: (e: Error) => toast({ variant: 'destructive', title: 'Could not clear it', description: e.message }),
  });
  const unclearHealth = useMutation({
    mutationFn: (id: string) => travelComplianceService.unclearHealthRequirement(requestId, id),
    onSuccess: async () => {
      toast({ title: 'Tick taken off' });
      await queryClient.invalidateQueries({ queryKey: ['travel-health-requirements', requestId] });
    },
    onError: (e: Error) => toast({ variant: 'destructive', title: 'Could not take the tick off', description: e.message }),
  });

  // Who has already been told what, so the desk does not send the same alert twice.
  const { data: sentAlerts } = useQuery({
    queryKey: ['travel-alert-notifications', requestId],
    queryFn: () => travelComplianceService.getAlertNotifications(request.employeeId),
    enabled: !!request.employeeId,
  });

  // ── what this traveller's passport actually needs ─────────────────────────
  // The visa-requirement register is reference data, and reference data nobody consults does not
  // get maintained. This is where it earns its place: the question "what does this passport need
  // for this destination" is asked at exactly the moment a visa is about to be recorded.
  //
  // ⚠ The passport country is not on the employee record — it is the ISSUING COUNTRY of their
  // Passport travel document. A traveller with no passport on file cannot be looked up at all,
  // which is worth saying out loud here rather than showing an empty answer.
  const { data: travelDocs, isError: docsFailed } = useQuery({
    queryKey: ['travel-documents', request.employeeId],
    queryFn: () => travelComplianceService.getDocumentsByEmployee(request.employeeId),
    enabled: !!request.employeeId,
  });

  const passport = (travelDocs ?? [])
    .filter((d) => d.documentType === 'Passport')
    // Prefer the one marked primary, then the latest expiry — a traveller may hold two.
    .sort((a, b) =>
      a.isPrimary === b.isPrimary
        ? (b.expiryDate ?? '').localeCompare(a.expiryDate ?? '')
        : a.isPrimary
          ? -1
          : 1,
    )[0];

  const passportCountryId = passport?.issuingCountryId;

  const {
    data: requirement, isLoading: requirementLoading, isError: requirementFailed,
  } = useQuery({
    queryKey: ['travel-visa-requirement', passportCountryId, request.destinationCountryId],
    queryFn: () =>
      travelComplianceService.getVisaRequirement(
        passportCountryId as string,
        request.destinationCountryId,
      ),
    enabled: !!passportCountryId && !!request.destinationCountryId,
  });

  const notify = useMutation({
    mutationFn: (alertId: string) =>
      travelComplianceService.notifyTraveller({
        travelAlertId: alertId,
        staffTravelRequestId: requestId,
        employeeId: request.employeeId,
      }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['travel-alert-notifications', requestId] });
      toast({
        title: 'Alert sent to the traveller',
        description: 'They confirm they have read it from their own travel page.',
      });
    },
    onError: (e: Error) =>
      toast({
        variant: 'destructive',
        title: 'Could not send the alert',
        description: e?.message,
      }),
  });

  const visaForm = useForm<z.input<typeof visaSchema>>({
    resolver: zodResolver(visaSchema),
    defaultValues: { status: 'NotStarted', currencyCode: request.currencyCode },
  });

  const riskForm = useForm<z.input<typeof riskSchema>>({
    resolver: zodResolver(riskSchema),
    defaultValues: {
      riskLevel: 'Low', riskCategory: 'Security', mitigationRequired: false,
      dutyOfCareBriefingSent: false, destinationCity: request.destinationCity,
    },
  });

  const insuranceForm = useForm<z.input<typeof insuranceSchema>>({
    resolver: zodResolver(insuranceSchema),
    defaultValues: {
      insuranceType: 'CorporateGroup', coverageType: 'Comprehensive',
      coverageStart: '', coverageEnd: '', sumInsured: 0, premium: 0,
      currencyCode: request.currencyCode,
    },
  });

  const refused = (title: string) => (e: Error) =>
    toast({ variant: 'destructive', title, description: e.message });

  // Lane 7 (E3): a visa application was frozen at creation — the edit had no caller. The same dialog now changes one.
  const [editingVisaId, setEditingVisaId] = useState<string | null>(null);
  const openNewVisa = () => {
    setEditingVisaId(null);
    visaForm.reset({ status: 'NotStarted', currencyCode: request.currencyCode });
    setDialog('visa');
  };
  // The list shows the number masked; the edit reads the application's own detail, which is in full.
  const openVisaEdit = async (id: string) => {
    try {
      const full = await travelComplianceService.getVisaApplication(id);
      visaForm.reset({
        visaType: full.visaType ?? '',
        status: full.status,
        submittedDate: full.submittedDate?.slice(0, 10) ?? '',
        approvedDate: full.approvedDate?.slice(0, 10) ?? '',
        expiryDate: full.expiryDate?.slice(0, 10) ?? '',
        visaNumber: full.visaNumber ?? '',
        processingFee: full.processingFee ?? undefined,
        currencyCode: full.currencyCode ?? request.currencyCode,
        notes: full.notes ?? '',
      });
      setEditingVisaId(id);
      setDialog('visa');
    } catch (e) {
      refused('Could not open the visa application')(e as Error);
    }
  };

  const addVisa = useMutation({
    mutationFn: (values: z.input<typeof visaSchema>) => {
      const v = visaSchema.parse(values);
      const payload = {
        ...v,
        destinationCountryId: request.destinationCountryId,
        submittedDate: orNull(v.submittedDate),
        approvedDate: orNull(v.approvedDate),
        expiryDate: orNull(v.expiryDate),
        // A currency is only meaningful with a fee, and a present one must be real to Finance.
        currencyCode: v.processingFee ? v.currencyCode || null : null,
      };
      return editingVisaId
        ? travelComplianceService.updateVisaApplication({ ...payload, id: editingVisaId })
        : travelComplianceService.createVisaApplication({
          ...payload, staffTravelRequestId: requestId, employeeId: request.employeeId,
        });
    },
    onSuccess: async () => {
      toast({ title: editingVisaId ? 'Visa application changed' : 'Visa application recorded' });
      setDialog(null);
      setEditingVisaId(null);
      visaForm.reset();
      await queryClient.invalidateQueries({ queryKey: ['travel-visas', requestId] });
    },
    onError: refused(editingVisaId ? 'Could not change the visa application' : 'Could not record the visa application'),
  });

  const addRisk = useMutation({
    mutationFn: (values: z.input<typeof riskSchema>) => {
      const v = riskSchema.parse(values);
      return travelComplianceService.createRiskAssessment({
        ...v,
        staffTravelRequestId: requestId,
        destinationCountryId: request.destinationCountryId,
        validUntil: orNull(v.validUntil),
      });
    },
    onSuccess: async () => {
      toast({ title: 'Risk assessment recorded' });
      setDialog(null);
      await queryClient.invalidateQueries({ queryKey: ['travel-risk-assessment', requestId] });
    },
    onError: refused('Could not record the assessment'),
  });

  const addInsurance = useMutation({
    mutationFn: (values: z.input<typeof insuranceSchema>) => {
      const v = insuranceSchema.parse(values);
      return travelComplianceService.createInsurance({ ...v, staffTravelRequestId: requestId });
    },
    onSuccess: async () => {
      toast({ title: 'Insurance recorded' });
      setDialog(null);
      insuranceForm.reset();
      await queryClient.invalidateQueries({ queryKey: ['travel-insurance', requestId] });
    },
    onError: refused('Could not record the insurance'),
  });

  const acknowledge = useMutation({
    mutationFn: (id: string) => travelComplianceService.acknowledgeRiskAssessment(id),
    onSuccess: async () => {
      toast({ title: 'Acknowledged' });
      await queryClient.invalidateQueries({ queryKey: ['travel-risk-assessment', requestId] });
    },
    // A 403 here means "this is not your trip" — worth saying plainly rather than as a failure.
    onError: (e: Error) =>
      toast({
        variant: 'destructive',
        title: 'Only the traveller can acknowledge this',
        description: e.message,
      }),
  });

  if (isLoading) {
    return (
      <div className="flex items-center justify-center p-10">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  return (
    <div className="space-y-4">
      {/* A failed alert read must not look like "no alerts": this is a duty-of-care card. */}
      {alertsFailed && !alerts && <TravelQueryError error={alertsError} what="the destination alerts" />}
      {incidentsFailed && !incidents && <TravelQueryError error={incidentsError} what="the company vehicles' incidents" />}

      {/* Lane 6 (D-29): read-only — Fleet records and closes them; telling anyone of a new one is lane 8's. */}
      {(incidents ?? []).length > 0 && (
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="flex items-center gap-2 text-base">
              <ShieldAlert className="h-4 w-4 text-destructive" />
              Company vehicle incidents (from Fleet)
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-2">
            {(incidents ?? []).map((i) => (
              <div key={i.id} className="rounded-md border p-3">
                <p className="text-sm font-medium">
                  {i.title}
                  <span className="text-muted-foreground">
                    {' '}· {i.incidentType} · {i.severity} · {i.status}
                  </span>
                </p>
                <p className="mt-1 text-xs text-muted-foreground">
                  {new Date(i.occurredAtUtc).toLocaleString()} · {i.vehicleName}{i.vehiclePlate ? ` (${i.vehiclePlate})` : ''}
                  {i.driverName ? ` · driver ${i.driverName}` : ''}{i.location ? ` · ${i.location}` : ''}
                </p>
                {i.description && <p className="mt-1 text-sm text-muted-foreground">{i.description}</p>}
              </div>
            ))}
          </CardContent>
        </Card>
      )}

      {(alerts ?? []).length > 0 && (
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="flex items-center gap-2 text-base">
              <TriangleAlert className="h-4 w-4 text-destructive" />
              Active alerts for {request.destinationCountryName}
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-2">
            {/*
              ⚠ `body` renders now. This read returned a SUMMARY DTO with no body until
              2026-08-30 while the client typed it as the full record, so every traveller saw
              "Civil unrest · High" and never a word about what or where — since the day this
              shipped, silently, and type-checking clean.
            */}
            {(alerts ?? []).map((a) => {
              const sent = (sentAlerts ?? []).find(
                (n) => n.travelAlertId === a.id && n.staffTravelRequestId === requestId);
              return (
                <div key={a.id} className="rounded-md border p-3">
                  <div className="flex flex-wrap items-start justify-between gap-2">
                    <p className="text-sm font-medium">
                      {a.title}
                      <span className="text-muted-foreground">
                        {' '}· {humanize(a.severityName)} · {humanize(a.alertTypeName)}
                      </span>
                    </p>
                    {sent ? (
                      <Badge variant={sent.isAcknowledged ? 'secondary' : 'outline'}>
                        {sent.isAcknowledged ? 'Read by traveller' : 'Sent, not yet read'}
                      </Badge>
                    ) : (
                      <Button
                        variant="outline"
                        size="sm"
                        disabled={notify.isPending}
                        onClick={() => notify.mutate(a.id)}
                      >
                        <Send className="mr-2 h-3.5 w-3.5" />
                        Send to traveller
                      </Button>
                    )}
                  </div>
                  {a.body && <p className="mt-1 text-sm text-muted-foreground">{a.body}</p>}
                  {a.source && (
                    <p className="mt-1 text-xs text-muted-foreground">Source: {a.source}</p>
                  )}
                </div>
              );
            })}
          </CardContent>
        </Card>
      )}

      {/*
        ⚠ **No policy-exception surface here, deliberately.** A `StaffTravelPolicyException` is the
        artefact of the policy RULES mechanism, and rules are not enforced — nothing evaluates one,
        so nothing can breach one. Raising exceptions by hand would manufacture audit records
        implying a control was in force and consciously waived, which is a worse artefact than an
        empty queue. A breach of the policy's own caps is a different thing: the booking is saved
        awaiting authorisation and decided on Staff Travel → Policy breaches by an `HR.Travel.Admin`
        holder who did not book it (lane 4, D-8).
      */}
      <Card>
        <CardHeader className="flex flex-row items-center justify-between gap-4 pb-3">
          <CardTitle className="flex items-center gap-2 text-base">
            <ShieldAlert className="h-4 w-4" />
            Risk assessment
          </CardTitle>
          <Button variant="outline" size="sm" onClick={() => setDialog('risk')}>
            <Plus className="mr-2 h-4 w-4" /> {assessment ? 'New assessment' : 'Assess'}
          </Button>
        </CardHeader>
        <CardContent>
          {assessmentFailed && !assessment ? (
            <TravelQueryError error={assessmentError} what="the risk assessment" />
          ) : !assessment ? (
            <EmptyState
              title="Not assessed"
              description={request.riskLevel === 'Critical'
                ? 'No risk assessment has been recorded, and this trip is rated Critical: its flight is not ticketed until one is recorded and the traveller has acknowledged it.'
                : 'No risk assessment has been recorded for this destination.'}
            />
          ) : (
            <div className="space-y-3">
              <div className="grid grid-cols-2 gap-x-6 md:grid-cols-4">
                <div>
                  <p className="text-xs text-muted-foreground">Risk</p>
                  <p className="text-sm font-medium">
                    {humanize(assessment.riskLevelName)} · {humanize(assessment.riskCategoryName)}
                  </p>
                </div>
                <div>
                  <p className="text-xs text-muted-foreground">Assessed by</p>
                  <p className="text-sm">{assessment.assessedByName || '—'}</p>
                </div>
                <div>
                  <p className="text-xs text-muted-foreground">Valid until</p>
                  <p className="text-sm">{fmtDate(assessment.validUntil)}</p>
                </div>
                <div>
                  <p className="text-xs text-muted-foreground">Briefing sent</p>
                  <p className="text-sm">{assessment.dutyOfCareBriefingSent ? 'Yes' : 'No'}</p>
                </div>
              </div>

              {assessment.assessmentSummary && (
                <p className="text-sm whitespace-pre-wrap">{assessment.assessmentSummary}</p>
              )}
              {assessment.mitigationRequired && (
                <div className="rounded-md border border-dashed p-3">
                  <p className="text-sm font-medium">Mitigation required</p>
                  {assessment.mitigationNotes && (
                    <p className="mt-1 text-sm whitespace-pre-wrap text-muted-foreground">
                      {assessment.mitigationNotes}
                    </p>
                  )}
                </div>
              )}

              {/* Lane 7 (D-37): a Critical trip's flight waits for the traveller's acknowledgement of this assessment. */}
              {(request.riskLevel === 'Critical' || assessment.riskLevel === 'Critical') && !assessment.employeeAcknowledged && (
                <p className="rounded-md border border-amber-300 bg-amber-50 p-3 text-sm dark:border-amber-700 dark:bg-amber-950">
                  This trip is rated Critical: its flight is not ticketed until the traveller acknowledges this assessment.
                </p>
              )}
              <div className="flex flex-wrap items-center gap-3 border-t pt-3">
                {assessment.employeeAcknowledged ? (
                  <p className="flex items-center gap-2 text-sm text-muted-foreground">
                    <Check className="h-4 w-4" />
                    Acknowledged by the traveller · {fmtDateTime(assessment.acknowledgedAt)}
                  </p>
                ) : isTraveller ? (
                  <>
                    <Button
                      size="sm"
                      disabled={acknowledge.isPending}
                      onClick={() => acknowledge.mutate(assessment.id)}
                    >
                      I have read this
                    </Button>
                    <p className="text-xs text-muted-foreground">
                      This is your trip, so the acknowledgement is yours to record.
                    </p>
                  </>
                ) : (
                  <p className="text-xs text-muted-foreground">
                    Not yet acknowledged. Only {request.employeeName} can acknowledge their own
                    assessment, and there is no self-service screen for it yet.
                  </p>
                )}
              </div>
            </div>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader className="flex flex-row items-center justify-between gap-4 pb-3">
          <CardTitle className="flex items-center gap-2 text-base">
            <Stamp className="h-4 w-4" />
            Visas
          </CardTitle>
          <Button variant="outline" size="sm" onClick={openNewVisa}>
            <Plus className="mr-2 h-4 w-4" /> Record a visa
          </Button>
        </CardHeader>
        <CardContent className="p-0">
          {/*
            What the register says, before anything is recorded. Built 2026-09-01 (lane 5b) — the
            requirement table had a screen to fill it and nothing that read it.
          */}
          <div className="border-b px-6 pb-4">
            {docsFailed && !travelDocs ? (
              <p className="text-sm text-destructive">
                The traveller&apos;s travel documents could not be read, so the visa requirement
                cannot be looked up.
              </p>
            ) : !passport ? (
              <p className="text-sm text-muted-foreground">
                No passport is on file for this traveller, so the visa requirement cannot be looked
                up — it is keyed on the country that issued the passport. Record it under{' '}
                <Link href="/hr/travel/documents" className="text-primary hover:underline">travel documents</Link>.
              </p>
            ) : requirementLoading ? (
              <p className="text-sm text-muted-foreground">
                Checking what a {passport.issuingCountryName ?? 'that'} passport needs…
              </p>
            ) : requirementFailed && !requirement ? (
              <p className="text-sm text-destructive">
                The visa requirements register could not be read.
              </p>
            ) : !requirement ? (
              <p className="text-sm text-muted-foreground">
                Nothing is recorded for a {passport.issuingCountryName ?? 'that'} passport travelling
                to {request.destinationCountryName}. The travel desk has no answer to give until
                somebody adds it to the{' '}
                <Link href="/hr/travel/visa-requirements" className="text-primary hover:underline">
                  visa requirements register
                </Link>
                .
              </p>
            ) : (
              <div className="space-y-1">
                <p className="text-sm">
                  <Badge
                    variant={
                      requirement.visaRequirementType === 'Prohibited'
                        ? 'destructive'
                        : requirement.visaRequirementType === 'VisaFree'
                          ? 'secondary'
                          : 'outline'
                    }
                  >
                    {VISA_REQUIREMENT_TYPE_LABELS[requirement.visaRequirementType] ??
                      requirement.visaRequirementTypeName}
                  </Badge>{' '}
                  for a {requirement.passportCountryName} passport entering{' '}
                  {requirement.destinationCountryName}
                  {requirement.visaCategory ? ` · ${requirement.visaCategory}` : ''}
                </p>
                <p className="text-sm text-muted-foreground">
                  {[
                    requirement.processingDays != null
                      ? `Allow ${requirement.processingDays} days to process`
                      : null,
                    requirement.maxStayDays != null
                      ? `maximum stay ${requirement.maxStayDays} days`
                      : null,
                  ]
                    .filter(Boolean)
                    .join(' · ') || 'No processing time or stay limit recorded.'}
                </p>
                {/*
                  ⚠ Visa rules change without notice, so how stale the entry is matters as much as
                  what it says. An unverified entry is shown as such rather than presented as fact.
                */}
                <p className="text-xs text-muted-foreground">
                  {requirement.lastVerifiedAt ? (
                    requirement.isStale
                      ? (
                        <span className="text-amber-600">
                          Last checked {requirement.lastVerifiedAt.slice(0, 10)} — over a year ago; confirm it before relying on it.
                        </span>
                      )
                      : <>Last checked {requirement.lastVerifiedAt.slice(0, 10)}</>
                  ) : (
                    <span className="text-amber-600">
                      Never checked against an official source — confirm before relying on it.
                    </span>
                  )}
                  {requirement.officialSourceUrl && (
                    <>
                      {' · '}
                      <a
                        href={requirement.officialSourceUrl}
                        target="_blank"
                        rel="noreferrer"
                        className="text-primary hover:underline"
                      >
                        source
                      </a>
                    </>
                  )}
                </p>
              </div>
            )}
          </div>

          {visasFailed && !visas ? (
            <div className="p-4">
              <TravelQueryError error={visasError} what="the visa applications" />
            </div>
          ) : (visas ?? []).length === 0 ? (
            <EmptyState
              title="No visa recorded"
              description={
                request.requiresVisa
                  ? 'This trip was raised as needing a visa.'
                  : 'This trip was not raised as needing one.'
              }
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Type</TableHead>
                  <TableHead>Number</TableHead>
                  <TableHead>Submitted</TableHead>
                  <TableHead>Expires</TableHead>
                  <TableHead className="text-right">Fee</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead className="w-12" />
                </TableRow>
              </TableHeader>
              <TableBody>
                {(visas ?? []).map((v) => (
                  <TableRow key={v.id}>
                    <TableCell className="font-medium">{v.visaType || '—'}</TableCell>
                    <TableCell className="font-mono text-xs">{v.visaNumberMasked || '—'}</TableCell>
                    <TableCell className="whitespace-nowrap">{fmtDate(v.submittedDate)}</TableCell>
                    <TableCell className="whitespace-nowrap">{fmtDate(v.expiryDate)}</TableCell>
                    <TableCell className="text-right whitespace-nowrap">
                      {fmtMoney(v.processingFee, v.currencyCode)}
                    </TableCell>
                    <TableCell><StatusBadge status={humanize(v.statusName)} /></TableCell>
                    <TableCell>
                      <Button variant="ghost" size="icon" aria-label="Change this visa application"
                        onClick={() => openVisaEdit(v.id)}>
                        <Pencil className="h-4 w-4" />
                      </Button>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      {/* Lane 7 (D-36, T-25): the destination's health requirements over the trip, each ticked off by the desk. */}
      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="flex items-center gap-2 text-base">
            <Stethoscope className="h-4 w-4" />
            Health requirements for {request.destinationCountryName}
          </CardTitle>
        </CardHeader>
        <CardContent className="p-0">
          {healthFailed && !health ? (
            <div className="p-4"><TravelQueryError error={healthError} what="the health requirements" /></div>
          ) : (health ?? []).length === 0 ? (
            <EmptyState title="None recorded" description="No health requirement is recorded for this destination over the trip's dates." />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Requirement</TableHead>
                  <TableHead>Cleared</TableHead>
                  <TableHead className="w-28" />
                </TableRow>
              </TableHeader>
              <TableBody>
                {(health ?? []).map((h) => (
                  <TableRow key={h.healthRequirementId}>
                    <TableCell>
                      <span className="font-medium">{h.requirementName}</span>
                      <span className="text-muted-foreground"> · {humanize(h.requirementTypeName)}</span>
                      {h.isMandatory && <Badge variant="outline" className="ml-2">mandatory</Badge>}
                      {h.notes && <span className="block text-xs text-muted-foreground">{h.notes}</span>}
                    </TableCell>
                    <TableCell className="text-sm">
                      {h.cleared
                        ? (
                          <span>
                            <Check className="mr-1 inline h-4 w-4 text-green-600" />
                            {h.clearedByName} · {fmtDate(h.clearedAt)}
                            {h.clearanceNote && <span className="block text-xs text-muted-foreground">{h.clearanceNote}</span>}
                          </span>
                        )
                        : <span className="text-muted-foreground">Not yet</span>}
                    </TableCell>
                    <TableCell className="text-right">
                      {h.cleared
                        ? (
                          <Button variant="ghost" size="sm" disabled={unclearHealth.isPending}
                            onClick={() => unclearHealth.mutate(h.healthRequirementId)}>
                            Untick
                          </Button>
                        )
                        : (
                          <Button variant="outline" size="sm" onClick={() => { setClearing(h); setClearNote(''); }}>
                            Clear
                          </Button>
                        )}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      <Dialog open={!!clearing} onOpenChange={(v) => !v && setClearing(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Clear {clearing?.requirementName}</DialogTitle>
            <DialogDescription>
              You are recorded as having checked this for {request.employeeName}, today. Say what you saw — a certificate,
              its validity.
            </DialogDescription>
          </DialogHeader>
          <Textarea value={clearNote} maxLength={1000} onChange={(e) => setClearNote(e.target.value)}
            placeholder="e.g. Yellow-fever certificate seen, valid to 2034" />
          <DialogFooter>
            <Button variant="outline" onClick={() => setClearing(null)}>Cancel</Button>
            <Button disabled={clearHealth.isPending}
              onClick={() => clearing && clearHealth.mutate({ id: clearing.healthRequirementId, note: clearNote.trim() || null })}>
              {clearHealth.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Clear it
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Card>
        <CardHeader className="flex flex-row items-center justify-between gap-4 pb-3">
          <CardTitle className="flex items-center gap-2 text-base">
            <Umbrella className="h-4 w-4" />
            Insurance
          </CardTitle>
          <Button variant="outline" size="sm" onClick={() => setDialog('insurance')}>
            <Plus className="mr-2 h-4 w-4" /> Record cover
          </Button>
        </CardHeader>
        <CardContent className="p-0">
          {/* Lane 7 (O-16): an international trip's flight waits for cover across every day of it. */}
          {request.isInternational && !insuranceFailed && !(insurance ?? []).some((p) =>
            String(p.coverageStart).slice(0, 10) <= String(request.travelStartDate).slice(0, 10)
            && String(p.coverageEnd).slice(0, 10) >= String(request.travelEndDate).slice(0, 10)) && (
            <p className="m-4 rounded-md border border-amber-300 bg-amber-50 p-3 text-sm dark:border-amber-700 dark:bg-amber-950">
              This trip is international and no cover recorded here spans every day of it — its flight is not ticketed until one does.
            </p>
          )}
          {insuranceFailed && !insurance ? (
            <div className="p-4">
              <TravelQueryError error={insuranceError} what="the insurance cover" />
            </div>
          ) : (insurance ?? []).length === 0 ? (
            <EmptyState title="No cover recorded" description="No travel insurance for this trip." />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Policy</TableHead>
                  <TableHead>Type</TableHead>
                  <TableHead>Cover</TableHead>
                  <TableHead>Period</TableHead>
                  <TableHead className="text-right">Sum insured</TableHead>
                  <TableHead className="text-right">Premium</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {(insurance ?? []).map((i) => (
                  <TableRow key={i.id}>
                    <TableCell className="font-medium">{i.policyNumber || '—'}</TableCell>
                    <TableCell>{humanize(i.insuranceTypeName)}</TableCell>
                    <TableCell>{humanize(i.coverageTypeName)}</TableCell>
                    <TableCell className="whitespace-nowrap">
                      {fmtDate(i.coverageStart)} – {fmtDate(i.coverageEnd)}
                    </TableCell>
                    <TableCell className="text-right whitespace-nowrap">
                      {fmtMoney(i.sumInsured, i.currencyCode)}
                    </TableCell>
                    <TableCell className="text-right whitespace-nowrap">
                      {fmtMoney(i.premium, i.currencyCode)}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      <Dialog open={dialog === 'risk'} onOpenChange={(v) => setDialog(v ? 'risk' : null)}>
        <DialogContent className="max-h-[85vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Assess the destination</DialogTitle>
            <DialogDescription>
              You are recorded as the assessor. A new assessment replaces the current one.
            </DialogDescription>
          </DialogHeader>
          <form
            id="risk-form"
            className="space-y-4"
            onSubmit={riskForm.handleSubmit((v) => addRisk.mutate(v))}
          >
            <FieldRow>
              <SelectField
                form={riskForm} name="riskLevel" label="Risk level" required
                options={options(RISK_LEVELS)}
              />
              <SelectField
                form={riskForm} name="riskCategory" label="Category" required
                options={options(RISK_CATEGORIES)}
              />
            </FieldRow>
            <FieldRow>
              <TextField form={riskForm} name="destinationCity" label="City" />
              <DateField form={riskForm} name="validUntil" label="Valid until" />
            </FieldRow>
            <TextField form={riskForm} name="assessmentSource" label="Source" />
            <TextareaField form={riskForm} name="assessmentSummary" label="Summary" />
            <SwitchField
              form={riskForm}
              name="mitigationRequired"
              label="Mitigation required"
              description="The traveller must take specific precautions."
            />
            <TextareaField form={riskForm} name="mitigationNotes" label="Mitigation" />
            <SwitchField
              form={riskForm}
              name="dutyOfCareBriefingSent"
              label="Duty-of-care briefing sent"
            />
          </form>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDialog(null)}>Cancel</Button>
            <Button type="submit" form="risk-form" disabled={addRisk.isPending}>
              {addRisk.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Record
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={dialog === 'visa'} onOpenChange={(v) => { setDialog(v ? 'visa' : null); if (!v) setEditingVisaId(null); }}>
        <DialogContent className="max-h-[85vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle>{editingVisaId ? 'Change the visa application' : 'Record a visa application'}</DialogTitle>
            <DialogDescription>
              For {request.employeeName} travelling to {request.destinationCountryName}.
            </DialogDescription>
          </DialogHeader>
          <form
            id="visa-form"
            className="space-y-4"
            onSubmit={visaForm.handleSubmit((v) => addVisa.mutate(v))}
          >
            <FieldRow>
              <TextField
                form={visaForm}
                name="visaType"
                label="Visa type"
                placeholder="Business, single entry"
              />
              <SelectField
                form={visaForm} name="status" label="Status" required options={options(VISA_STATUSES)}
              />
            </FieldRow>
            <FieldRow>
              <TextField form={visaForm} name="visaNumber" label="Visa number" />
              <DateField form={visaForm} name="expiryDate" label="Expires" />
            </FieldRow>
            <FieldRow>
              <DateField form={visaForm} name="submittedDate" label="Submitted" />
              <DateField form={visaForm} name="approvedDate" label="Approved" />
            </FieldRow>
            <FieldRow>
              <NumberField form={visaForm} name="processingFee" label="Processing fee" />
              <CurrencyField
                form={visaForm}
                name="currencyCode"
                label="Currency"
                allowEmpty
                emptyLabel="No fee"
              />
            </FieldRow>
            <TextareaField form={visaForm} name="notes" label="Notes" />
          </form>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDialog(null)}>Cancel</Button>
            <Button type="submit" form="visa-form" disabled={addVisa.isPending}>
              {addVisa.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              {editingVisaId ? 'Save' : 'Record'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={dialog === 'insurance'} onOpenChange={(v) => setDialog(v ? 'insurance' : null)}>
        <DialogContent className="max-h-[85vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Record travel insurance</DialogTitle>
            <DialogDescription>Cover for this trip.</DialogDescription>
          </DialogHeader>
          <form
            id="insurance-form"
            className="space-y-4"
            onSubmit={insuranceForm.handleSubmit((v) => addInsurance.mutate(v))}
          >
            <FieldRow>
              <TextField form={insuranceForm} name="policyNumber" label="Policy number" />
              <SelectField
                form={insuranceForm} name="insuranceType" label="Type" required
                options={options(INSURANCE_TYPES)}
              />
            </FieldRow>
            <SelectField
              form={insuranceForm} name="coverageType" label="Cover" required
              options={options(COVERAGE_TYPES)}
            />
            <FieldRow>
              <DateField form={insuranceForm} name="coverageStart" label="Cover from" required />
              <DateField form={insuranceForm} name="coverageEnd" label="Cover to" required />
            </FieldRow>
            <FieldRow>
              <NumberField form={insuranceForm} name="sumInsured" label="Sum insured" required />
              <NumberField form={insuranceForm} name="premium" label="Premium" required />
            </FieldRow>
            <CurrencyField form={insuranceForm} name="currencyCode" label="Currency" required />
            <TextField
              form={insuranceForm}
              name="emergencyContact"
              label="Emergency contact"
              placeholder="24-hour assistance line"
            />
          </form>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDialog(null)}>Cancel</Button>
            <Button type="submit" form="insurance-form" disabled={addInsurance.isPending}>
              {addInsurance.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Record
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
