'use client';

import { useQuery } from '@tanstack/react-query';
import { Info, Loader2, ScrollText } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { travelComplianceService } from '@/services/hr/travel-compliance.service';

const spaced = (v?: string | null) => (v ? v.replace(/([a-z])([A-Z0-9])/g, '$1 $2') : '—');

/**
 * The policy's rule register — **read-only, deliberately.**
 *
 * ⚠ **These rules are enforced by nothing.** `StaffTravelPolicyService` is the only consumer of
 * the rules table anywhere in the codebase: `StaffTravelPolicyGuard` refuses a booking on the
 * policy's own scalar caps (`maxFlightClass*`, `maxHotelRate*`) and never reads a rule. So
 * `ruleType`, `limitValue` and `violationAction` describe an enforcement mechanism that does not
 * run.
 *
 * ⚠ **Which is why authoring is not offered here.** An editable control that does nothing creates
 * false assurance, and that is worse than no control: a rule set to `Block` is a promise to the
 * person configuring it, and a warning banner is a weak defence to an auditor looking at a
 * screenshot. The API carries create, update and delete — `addPolicyRule`, `updatePolicyRule` and
 * `deletePolicyRule` are all present and harness-covered — and turning the affordances back on is
 * a small change **on the day rule evaluation lands**, not before.
 *
 * ⚠ **Two things that must happen in the same change as enforcement**, recorded so they are not
 * rediscovered: rule writes need the approval guard `UpdatePolicyAsync` has (an approved policy's
 * rules are part of the approved document), and `(policy, ruleCode)` uniqueness should become a
 * filtered index rather than the revive-on-re-add the service does today — a rule code is a label,
 * not an identity, so reviving keeps the wrong row's audit stamps.
 */
export function PolicyRulesPanel({ policyId }: { policyId: string }) {
  const { data: rules, isLoading } = useQuery({
    queryKey: ['travel-policies', policyId, 'rules'],
    queryFn: () => travelComplianceService.getPolicyRules(policyId),
    enabled: !!policyId,
  });

  const rows = rules ?? [];

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">Rules</CardTitle>
      </CardHeader>
      <CardContent className="space-y-4 p-0">
        <div className="mx-6 flex items-start gap-3 rounded-md border border-amber-300 bg-amber-50 p-3 dark:border-amber-900 dark:bg-amber-950/40">
          <Info className="mt-0.5 h-4 w-4 shrink-0 text-amber-700 dark:text-amber-300" />
          <p className="text-sm text-amber-900 dark:text-amber-100">
            <span className="font-medium">Rules are not in service.</span> What refuses a booking
            is the policy&apos;s own caps above — cabin class, hotel rate and the budgets. This
            register is shown for reference and cannot be edited until rule enforcement is built.
          </p>
        </div>

        {isLoading ? (
          <div className="flex justify-center py-10">
            <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
          </div>
        ) : rows.length === 0 ? (
          <div className="px-6 pb-6">
            <EmptyState
              icon={ScrollText}
              title="No rules"
              description="Nothing is written down beyond the caps above."
            />
          </div>
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Rule</TableHead>
                <TableHead>Applies to</TableHead>
                <TableHead className="text-right">Limit</TableHead>
                <TableHead>On breach</TableHead>
                <TableHead>State</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {rows.map((r) => (
                <TableRow key={r.id}>
                  <TableCell>
                    <div className="font-medium">{r.ruleName}</div>
                    <div className="text-xs text-muted-foreground">
                      {r.ruleCode} · {spaced(r.ruleTypeName || r.ruleType)}
                    </div>
                  </TableCell>
                  <TableCell className="text-sm">
                    <div>{r.expenseCategoryName ?? spaced(r.expenseCategory) ?? 'Any category'}</div>
                    <div className="text-xs text-muted-foreground">
                      {r.travelTypeName ?? spaced(r.travelType) ?? 'Any trip'}
                    </div>
                  </TableCell>
                  <TableCell className="text-right tabular-nums">
                    {r.limitValue == null ? '—' : r.limitValue}
                    {r.limitUnit && (
                      <span className="ml-1 text-xs text-muted-foreground">{r.limitUnit}</span>
                    )}
                  </TableCell>
                  <TableCell>
                    {/*
                      Shown as plain text rather than a badge: a badge reads as a live state, and
                      this action is not taken by anything.
                    */}
                    <span className="text-sm text-muted-foreground">
                      {spaced(r.violationActionName || r.violationAction)}
                      <span className="ml-1 text-xs">(not applied)</span>
                    </span>
                  </TableCell>
                  <TableCell>
                    {r.isActive ? (
                      <Badge variant="secondary">Active</Badge>
                    ) : (
                      <Badge variant="outline">Inactive</Badge>
                    )}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </CardContent>
    </Card>
  );
}
