'use client';

import Link from 'next/link';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2, Plus, ShieldCheck, FileWarning } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent } from '@/components/ui/card';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { TravelQueryError } from '@/components/hr/travel/TravelQueryError';
import { travelPolicyState } from '@/components/hr/travel/travel-policy-state';
import { useToast } from '@/hooks/use-toast';
import { travelComplianceService } from '@/services/hr/travel-compliance.service';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

/**
 * Travel policies — the spend rules that actually refuse bookings.
 *
 * ⚠ **The two controls this screen lost have come back.** It shipped with a "Draft a policy"
 * button and a per-row link to a detail screen and neither route existed, so both were a 404;
 * slice 10 removed them rather than leave dead controls, recording that the editor was an area-12
 * job. `/new` and `/[id]` exist now, so the button and the link are real.
 *
 * The API never was the gap — `StaffTravelPoliciesController` has carried create, update,
 * approve, withdraw, rules CRUD and exceptions throughout. The client methods existed too, which
 * is why the coverage instrument counted them wired: a service method is not a screen.
 *
 * ⚠ **A policy is a draft until it is approved, and a draft caps nothing.** That distinction is the
 * whole point of this screen, so it is shown as a badge on every row rather than buried in a detail
 * page: a list where a draft looks like a policy in force is a list that tells you the organisation
 * is protected when it is not.
 *
 * Approving is `HR.Travel.Admin` and also puts the policy in force, superseding whichever policy
 * covered the same scope. The button is shown to everyone and answers 403 for anyone without the
 * permission — hiding it would turn "you may not do this" into "this cannot be done".
 */
export default function TravelPoliciesPage() {
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const { data, isLoading, isError, error } = useQuery({
    queryKey: ['travel-policies'],
    queryFn: () => travelComplianceService.getPolicies(),
  });

  const withdraw = useMutation({
    mutationFn: (id: string) => travelComplianceService.withdrawPolicy(id),
    onSuccess: async () => {
      toast({ title: 'Policy withdrawn — it no longer caps bookings' });
      await queryClient.invalidateQueries({ queryKey: ['travel-policies'] });
    },
    onError: (e: Error) =>
      toast({ variant: 'destructive', title: 'Could not withdraw', description: e.message }),
  });

  const approve = useMutation({
    mutationFn: (id: string) => travelComplianceService.approvePolicy(id),
    // Lane 4, O-4: a version approved to start later is in force from its own date, not today.
    onSuccess: async (approved) => {
      toast({ title: 'Policy approved', description: approved ? travelPolicyState(approved).label : undefined });
      await queryClient.invalidateQueries({ queryKey: ['travel-policies'] });
    },
    onError: (e: Error) =>
      toast({
        variant: 'destructive',
        title: 'Could not approve the policy',
        description: e.message,
      }),
  });

  const items = data ?? [];
  const drafts = items.filter((p) => !p.approvedById);

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Travel policies"
        description="What staff may spend on travel, and what the caps refuse."
        backHref="/administration/hr"
        actions={
          <div className="flex flex-wrap gap-2">
            {/*
              No exceptions queue. Exceptions belong to the rules mechanism, which is recorded and
              not enforced, so the queue could only ever hold records raised by hand against a rule
              that never fires. It ships with rule enforcement or not at all.
            */}
            <Button asChild>
              <Link href="/administration/hr/travel/policies/new">
                <Plus className="mr-2 h-4 w-4" />
                Draft a policy
              </Link>
            </Button>
          </div>
        }
      />

      {drafts.length > 0 && (
        <Card>
          <CardContent className="flex items-start gap-3 p-4">
            <FileWarning className="mt-0.5 h-4 w-4 shrink-0 text-muted-foreground" />
            <p className="text-sm">
              <span className="font-medium">
                {drafts.length} {drafts.length === 1 ? 'policy is' : 'policies are'} still a draft.
              </span>{' '}
              A draft enforces nothing — bookings are accepted without a cap until it is approved.
            </p>
          </CardContent>
        </Card>
      )}

      <Card>
        <CardContent className="p-0">
          {isLoading ? (
            <div className="flex items-center justify-center p-10">
              <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
            </div>
          ) : isError && !data ? (
            <div className="p-4">
              <TravelQueryError error={error} what="the travel policies" />
            </div>
          ) : items.length === 0 ? (
            <EmptyState
              icon={ShieldCheck}
              title="No travel policies"
              description="Without one, travel bookings are not capped at all."
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Policy</TableHead>
                  <TableHead className="w-20">Version</TableHead>
                  <TableHead>Effective</TableHead>
                  <TableHead className="w-20">Rules</TableHead>
                  <TableHead>State</TableHead>
                  <TableHead>Approved by</TableHead>
                  <TableHead className="w-28" />
                </TableRow>
              </TableHeader>
              <TableBody>
                {items.map((p) => (
                  <TableRow key={p.id}>
                    <TableCell className="font-medium">
                      <Link
                        href={`/administration/hr/travel/policies/${p.id}`}
                        className="hover:underline"
                      >
                        {p.policyName}
                      </Link>
                    </TableCell>
                    <TableCell>v{p.versionNumber}</TableCell>
                    <TableCell className="whitespace-nowrap">
                      {fmtDate(p.effectiveFrom)}
                      {p.effectiveTo ? ` – ${fmtDate(p.effectiveTo)}` : ''}
                    </TableCell>
                    <TableCell>{p.ruleCount}</TableCell>
                    <TableCell>
                      <Badge variant={travelPolicyState(p).variant}>
                        {travelPolicyState(p).label}
                      </Badge>
                    </TableCell>
                    <TableCell>{p.approvedByName || '—'}</TableCell>
                    <TableCell>
                      {p.approvedById && p.isCurrentVersion && (
                        <Button
                          variant="ghost"
                          size="sm"
                          disabled={withdraw.isPending}
                          onClick={() => withdraw.mutate(p.id)}
                        >
                          Withdraw
                        </Button>
                      )}
                      {!p.approvedById && (
                        <Button
                          variant="ghost"
                          size="sm"
                          disabled={approve.isPending}
                          onClick={() => approve.mutate(p.id)}
                        >
                          Approve
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

      <p className="text-xs text-muted-foreground">
        Approving is a travel administrator&apos;s act — the HR desk holds it — and never that of whoever
        drafted or last changed the policy: a second officer signs. The approver is stored against
        their employee profile, so an account with no profile cannot sign a policy even when it holds
        the permission. A version approved for the same staff levels and organisation unit takes over
        on its own start date, so exactly one is in force on any day. Withdrawing stands a policy down
        without unmaking the approval — the record that it governed spending for a period stays.
      </p>
    </div>
  );
}
