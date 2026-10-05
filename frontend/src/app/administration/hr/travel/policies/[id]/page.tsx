'use client';

import { use, useState } from 'react';
import { useRouter } from 'next/navigation';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2, Pencil, ShieldCheck, Trash2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { HR_ROLES } from '@/components/hr/common/PermissionGate';
import { PolicyRulesPanel } from '@/components/hr/travel/PolicyRulesPanel';
import { TravelPolicyForm } from '@/components/hr/travel/TravelPolicyForm';
import { TravelQueryError } from '@/components/hr/travel/TravelQueryError';
import { travelPolicyState } from '@/components/hr/travel/travel-policy-state';
import { useAuth } from '@/hooks/use-auth';
import { useToast } from '@/hooks/use-toast';
import { travelComplianceService } from '@/services/hr/travel-compliance.service';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const spaced = (v?: string | null) => (v ? v.replace(/([a-z])([A-Z])/g, '$1 $2') : '—');
const money = (v: number) => v.toLocaleString(undefined, { minimumFractionDigits: 2 });

function Detail({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div>
      <dt className="text-xs uppercase tracking-wide text-muted-foreground">{label}</dt>
      <dd className="mt-0.5 text-sm">{children}</dd>
    </div>
  );
}

/**
 * One travel policy: its caps, its scope and its rules.
 *
 * The register linked here from the day it shipped and this route did not exist, so the link was
 * a 404 and was removed rather than left dead. It exists now, and it is where a policy is drafted,
 * corrected and given rules.
 *
 * ⚠ **Approved means frozen.** Editing is offered only on a draft, because changing what everyone
 * may spend without anyone approving the change is what approval exists to prevent — the API
 * refuses it either way. Deleting is likewise refused on an approved policy: the verb for standing
 * one down is Withdraw, on the register, which keeps the record that it once governed spending.
 */
export default function TravelPolicyDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const { hasAnyPermission, hasAnyRole } = useAuth();
  const [editing, setEditing] = useState(false);
  const [confirmingDelete, setConfirmingDelete] = useState(false);

  const canWrite =
    hasAnyPermission(['HR.Travel.Write', 'HR.Travel.Admin']) || hasAnyRole(HR_ROLES);
  // Lane 4, D-3: the permission alone — see useTravelAccess.
  const canAdmin = hasAnyPermission(['HR.Travel.Admin']);

  const { data: policy, isLoading, isError, error } = useQuery({
    queryKey: ['travel-policies', id],
    queryFn: () => travelComplianceService.getPolicy(id),
  });

  const remove = useMutation({
    mutationFn: () => travelComplianceService.deletePolicy(id),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['travel-policies'] });
      toast({ title: 'Policy deleted' });
      router.push('/administration/hr/travel/policies');
    },
    onError: (e: Error) =>
      toast({
        variant: 'destructive',
        title: 'Could not delete the policy',
        description: e?.message,
      }),
  });

  if (isLoading) {
    return (
      <div className="flex items-center justify-center p-12">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }
  if (isError && !policy) {
    return (
      <div className="p-6">
        <TravelQueryError error={error} what="this travel policy" />
      </div>
    );
  }
  if (!policy) return null;

  const isDraft = !policy.approvedById;

  if (editing) {
    return (
      <div className="space-y-6 p-6">
        <PageHeader
          title={`Edit ${policy.policyName}`}
          description="Only a draft can be corrected — an approved policy needs a new version."
          backHref={`/administration/hr/travel/policies/${id}`}
        />
        <TravelPolicyForm policy={policy} onSaved={() => setEditing(false)} />
      </div>
    );
  }

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={`${policy.policyName} · v${policy.versionNumber}`}
        description="What staff may spend on travel under this policy."
        backHref="/administration/hr/travel/policies"
        actions={
          <div className="flex flex-wrap gap-2">
            {canWrite && isDraft && (
              <Button variant="outline" onClick={() => setEditing(true)}>
                <Pencil className="mr-2 h-4 w-4" />
                Edit
              </Button>
            )}
            {canAdmin && isDraft && (
              <Button variant="outline" onClick={() => setConfirmingDelete(true)}>
                <Trash2 className="mr-2 h-4 w-4" />
                Delete
              </Button>
            )}
          </div>
        }
      />

      <div className="flex flex-wrap items-center gap-2">
        <Badge variant={travelPolicyState(policy).variant}>{travelPolicyState(policy).label}</Badge>
        <Badge variant="outline">
          {fmtDate(policy.effectiveFrom)}
          {policy.effectiveTo ? ` – ${fmtDate(policy.effectiveTo)}` : ' onwards'}
        </Badge>
      </div>

      {!isDraft && (
        <Card>
          <CardContent className="p-4 text-sm">
            Approved by {policy.approvedByName ?? 'an administrator'} on{' '}
            {fmtDate(policy.approvedAt)}. An approved policy cannot be edited or deleted — raise a
            new version to change what it allows, or withdraw it from the register to stop it
            capping bookings. A new version approved for the same scope takes over from its own
            start date; this one stays in force until the day before.
          </CardContent>
        </Card>
      )}

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <ShieldCheck className="h-4 w-4" />
            Scope
          </CardTitle>
        </CardHeader>
        <CardContent>
          <dl className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            <Detail label="Organisation unit">
              {policy.appliesToOrganizationUnitName ?? 'The whole organisation'}
            </Detail>
            <Detail label="From staff level">
              {policy.appliesToLevelFromName ?? 'Any level'}
            </Detail>
            <Detail label="To staff level">{policy.appliesToLevelToName ?? 'Any level'}</Detail>
          </dl>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">The caps that refuse a booking</CardTitle>
        </CardHeader>
        <CardContent>
          <dl className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            <Detail label="Max cabin — domestic">
              {spaced(policy.maxFlightClassDomestic)}
            </Detail>
            <Detail label="Max cabin — international">
              {spaced(policy.maxFlightClassInternational)}
            </Detail>
            <Detail label="Hotel — domestic, per night">
              {money(policy.maxHotelRateDomestic)}
            </Detail>
            <Detail label="Hotel — international, per night">
              {money(policy.maxHotelRateInternational)}
            </Detail>
            <Detail label="Currency of the limits">{policy.currencyCode ?? 'The base currency'}</Detail>
            <Detail label="Max per trip">
              {policy.maxSingleTripBudget > 0 ? money(policy.maxSingleTripBudget) : 'No limit'}
            </Detail>
            <Detail label="Receipt required above">{money(policy.receiptRequiredAbove)}</Detail>
            <Detail label="Days to submit expenses">{policy.expenseSubmissionDays}</Detail>
            <Detail label="Book flights ahead">{policy.advanceBookingDaysFlight} days</Detail>
            <Detail label="Book hotels ahead">{policy.advanceBookingDaysHotel} days</Detail>
            <Detail label="Preferred vendors">
              {policy.preferredVendorMandatory ? 'Mandatory' : 'Not mandatory'}
            </Detail>
          </dl>
        </CardContent>
      </Card>

      {/*
        ⚠ Read-only. The rules register is not in service — nothing evaluates a rule — and an
        editable control that does nothing creates false assurance, which is worse than no control:
        a rule set to `Block` is a promise. When enforcement lands, rule writes also need the
        approval guard `UpdatePolicyAsync` has, on the service rather than here.
      */}
      <PolicyRulesPanel policyId={id} />

      <ConfirmationDialog
        open={confirmingDelete}
        onOpenChange={setConfirmingDelete}
        title="Delete this policy?"
        description={`"${policy.policyName}" will be removed. Only a draft can be deleted — once a policy has been approved, withdrawing it is the way to stop it capping bookings.`}
        confirmText="Delete"
        variant="destructive"
        isLoading={remove.isPending}
        onConfirm={async () => {
          await remove.mutateAsync();
        }}
      />
    </div>
  );
}
