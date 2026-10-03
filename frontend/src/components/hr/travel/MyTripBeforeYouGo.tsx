'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { AlertTriangle, BellRing, Check, IdCard, Loader2, ShieldAlert, Stethoscope, Umbrella } from 'lucide-react';
import Link from 'next/link';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { useToast } from '@/hooks/use-toast';
import { travelService } from '@/services/hr/travel.service';
import type { StaffTravelRequest } from '@/types/hr/travel';
import { TRAVEL_RISK_LEVEL_LABELS, enumLabel } from './travel-enums';
import { TravelQueryError } from './TravelQueryError';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const fmtDateTime = (v?: string | null) => (v ? new Date(v).toLocaleString() : '—');
const humanize = (v?: string | null) => (v ? v.replace(/([a-z])([A-Z])/g, '$1 $2') : '—');

/**
 * What the traveller has to know and do before their trip (travel final closure, lane 7, slice 7c1 — E1, E7).
 *
 * ⚠ **The risk acknowledgement had no door a traveller could use** (E1): the desk's route sits on Travel WRITE, which the
 * Employee role never holds, and the service accepts only the traveller. It is recorded here, through the token-scoped
 * `/me` route — nobody can record it on the traveller's behalf. A Critical trip's flight is not ticketed until it is
 * (D-37), so the card says so while it is outstanding.
 *
 * The rest is read-only: the destination's alerts in force over the trip (all of them, not only those sent to the
 * traveller), its health requirements and which the desk has cleared (D-36), the visa applications and the insurance.
 */
export function MyTripBeforeYouGo({ request }: { request: StaffTravelRequest }) {
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const alerts = useQuery({
    queryKey: ['my-travel-destination-alerts', request.id],
    queryFn: () => travelService.getMyDestinationAlerts(request.id),
  });
  const health = useQuery({
    queryKey: ['my-travel-health', request.id],
    queryFn: () => travelService.getMyHealthRequirements(request.id),
  });

  const acknowledge = useMutation({
    mutationFn: (id: string) => travelService.acknowledgeMyRiskAssessment(id),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['my-travel-request', request.id] });
      toast({ title: 'Thank you — that is recorded against your trip' });
    },
    onError: (e: Error) =>
      toast({ variant: 'destructive', title: 'Could not record that', description: e.message }),
  });

  // Newest first: the latest assessment is the one in force.
  const assessments = (request.riskAssessments ?? [])
    .slice()
    .sort((a, b) => (b.assessedAt ?? b.createdAt ?? '').localeCompare(a.assessedAt ?? a.createdAt ?? ''));
  const visas = request.visaApplications ?? [];
  const insurance = request.insurancePolicies ?? [];
  const start = request.travelStartDate.slice(0, 10);
  const end = request.travelEndDate.slice(0, 10);
  const covered = insurance.some((p) => p.coverageStart.slice(0, 10) <= start && p.coverageEnd.slice(0, 10) >= end);

  return (
    <div className="space-y-4">
      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="flex items-center gap-2 text-base">
            <ShieldAlert className="h-4 w-4" /> Risk assessment
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          {assessments.length === 0 ? (
            <p className="text-sm text-muted-foreground">
              The travel desk has not recorded a risk assessment for this trip.
            </p>
          ) : (
            assessments.map((a, i) => {
              const critical = request.riskLevel === 'Critical' || a.riskLevel === 'Critical';
              return (
                <div key={a.id} className={`rounded-md border p-3 ${i > 0 ? 'opacity-80' : ''}`}>
                  <div className="flex flex-wrap items-center gap-2">
                    <Badge variant={['High', 'Critical', 'Prohibited'].includes(a.riskLevel) ? 'destructive' : 'secondary'}>
                      {enumLabel(TRAVEL_RISK_LEVEL_LABELS, a.riskLevel)} risk
                    </Badge>
                    <span className="text-sm">{humanize(a.riskCategoryName)}</span>
                    <span className="text-xs text-muted-foreground">
                      {[a.destinationCity, a.destinationCountryName].filter(Boolean).join(', ')}
                      {a.validUntil && ` · valid until ${fmtDate(a.validUntil)}`}
                    </span>
                    {i > 0 && <span className="text-xs text-muted-foreground">(an earlier assessment)</span>}
                  </div>
                  {a.assessmentSummary && <p className="mt-2 whitespace-pre-line text-sm">{a.assessmentSummary}</p>}
                  {a.mitigationNotes && (
                    <div className="mt-2">
                      <p className="text-xs font-medium text-muted-foreground">What to do</p>
                      <p className="whitespace-pre-line text-sm">{a.mitigationNotes}</p>
                    </div>
                  )}
                  {critical && !a.employeeAcknowledged && i === 0 && (
                    <p className="mt-3 rounded-md border border-amber-300 bg-amber-50 p-2 text-sm dark:border-amber-700 dark:bg-amber-950">
                      This trip is rated Critical: your flight is not ticketed until you confirm you have read this.
                    </p>
                  )}
                  <div className="mt-3 flex flex-wrap items-center gap-3 border-t pt-3">
                    {a.employeeAcknowledged ? (
                      <p className="flex items-center gap-2 text-sm text-muted-foreground">
                        <Check className="h-4 w-4" /> You confirmed reading this · {fmtDateTime(a.acknowledgedAt)}
                      </p>
                    ) : (
                      <Button size="sm" disabled={acknowledge.isPending} onClick={() => acknowledge.mutate(a.id)}>
                        {acknowledge.isPending && <Loader2 className="mr-2 h-3 w-3 animate-spin" />}
                        I have read this
                      </Button>
                    )}
                    {a.assessedByName && (
                      <span className="text-xs text-muted-foreground">
                        Assessed by {a.assessedByName}{a.assessedAt && `, ${fmtDate(a.assessedAt)}`}
                      </span>
                    )}
                  </div>
                </div>
              );
            })
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="flex items-center gap-2 text-base">
            <BellRing className="h-4 w-4" /> Alerts for your destination
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          {alerts.isError && !alerts.data ? (
            <TravelQueryError error={alerts.error} what="the destination's alerts" />
          ) : alerts.isLoading ? (
            <Loader2 className="h-4 w-4 animate-spin text-muted-foreground" />
          ) : (alerts.data ?? []).length === 0 ? (
            <p className="text-sm text-muted-foreground">No alert is in force for your destination over these dates.</p>
          ) : (
            (alerts.data ?? []).map((a) => (
              <div key={a.id} className="rounded-md border p-3">
                <div className="flex flex-wrap items-center gap-2">
                  <p className="text-sm font-medium">{a.title}</p>
                  <Badge variant={a.severity === 'Critical' || a.severity === 'Emergency' ? 'destructive' : 'secondary'}>
                    {a.severityName}
                  </Badge>
                  <span className="text-xs text-muted-foreground">
                    {humanize(a.alertTypeName)}{a.city ? ` · ${a.city}` : ''} · from {fmtDate(a.effectiveFrom)}
                    {a.effectiveTo && ` to ${fmtDate(a.effectiveTo)}`}
                  </span>
                </div>
                {a.body && <p className="mt-2 whitespace-pre-line text-sm">{a.body}</p>}
              </div>
            ))
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="flex items-center gap-2 text-base">
            <Stethoscope className="h-4 w-4" /> Health requirements
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-2">
          {health.isError && !health.data ? (
            <TravelQueryError error={health.error} what="the health requirements" />
          ) : health.isLoading ? (
            <Loader2 className="h-4 w-4 animate-spin text-muted-foreground" />
          ) : (health.data ?? []).length === 0 ? (
            <p className="text-sm text-muted-foreground">None recorded for your destination.</p>
          ) : (
            (health.data ?? []).map((h) => (
              <div key={h.healthRequirementId} className="flex flex-wrap items-start justify-between gap-2 rounded-md border p-3">
                <div className="min-w-0">
                  <p className="text-sm font-medium">
                    {h.requirementName}
                    {h.isMandatory && <Badge variant="outline" className="ml-2">required</Badge>}
                  </p>
                  <p className="text-xs text-muted-foreground">
                    {humanize(h.requirementTypeName)}
                    {h.validityDays ? ` · valid ${h.validityDays} days` : ''}
                  </p>
                  {h.notes && <p className="mt-1 text-sm">{h.notes}</p>}
                </div>
                {h.cleared ? (
                  <Badge variant="secondary" className="gap-1">
                    <Check className="h-3 w-3" /> Cleared by the travel desk {fmtDate(h.clearedAt)}
                  </Badge>
                ) : (
                  <span className="text-xs text-muted-foreground">Not yet cleared — show the travel desk your proof</span>
                )}
              </div>
            ))
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="flex items-center gap-2 text-base">
            <IdCard className="h-4 w-4" /> Visas
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-2">
          {request.requiresVisa && visas.length === 0 && (
            <p className="flex items-center gap-2 text-sm">
              <AlertTriangle className="h-4 w-4 text-amber-600" />
              This trip needs a visa, and no application is recorded yet — the travel desk arranges it with you.
            </p>
          )}
          {!request.requiresVisa && visas.length === 0 && (
            <p className="text-sm text-muted-foreground">No visa is needed for this trip.</p>
          )}
          {visas.map((v) => (
            <div key={v.id} className="flex flex-wrap items-center justify-between gap-2 rounded-md border p-3">
              <div>
                <p className="text-sm font-medium">{v.visaType || 'Visa'} — {v.destinationCountryName ?? '—'}</p>
                <p className="text-xs text-muted-foreground">
                  {v.visaNumberMasked ? `No. ${v.visaNumberMasked} · ` : ''}
                  {v.expiryDate ? `expires ${fmtDate(v.expiryDate)}` : 'no expiry recorded'}
                </p>
              </div>
              <StatusBadge status={humanize(v.statusName)} />
            </div>
          ))}
          <p className="text-xs text-muted-foreground">
            Whether a trip needs a visa is read from your primary passport —{' '}
            <Link href="/me/travel/documents" className="underline">keep your travel documents up to date</Link>.
          </p>
        </CardContent>
      </Card>

      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="flex items-center gap-2 text-base">
            <Umbrella className="h-4 w-4" /> Travel insurance
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-2">
          {request.isInternational && !covered && (
            <p className="flex items-center gap-2 text-sm">
              <AlertTriangle className="h-4 w-4 text-amber-600" />
              No cover spanning every day of this trip is recorded yet — your flight is not ticketed without it.
            </p>
          )}
          {insurance.length === 0 && !request.isInternational && (
            <p className="text-sm text-muted-foreground">No travel insurance is recorded for this trip.</p>
          )}
          {insurance.map((p) => (
            <div key={p.id} className="rounded-md border p-3">
              <p className="text-sm font-medium">
                {p.vendorName ?? 'Insurer'}{p.policyNumber ? ` · policy ${p.policyNumber}` : ''}
              </p>
              <p className="text-xs text-muted-foreground">
                {humanize(p.insuranceTypeName)}, {humanize(p.coverageTypeName)} · {fmtDate(p.coverageStart)} – {fmtDate(p.coverageEnd)}
              </p>
              {p.emergencyContact && <p className="mt-1 text-sm">In an emergency: {p.emergencyContact}</p>}
            </div>
          ))}
        </CardContent>
      </Card>
    </div>
  );
}
