'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2, Plus } from 'lucide-react';
import { useToast } from '@/hooks/use-toast';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
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
import { CloneSettingsProfileDialog } from '@/components/hr/performance/CloneSettingsProfileDialog';
import { appraisalSettingsService } from '@/services/hr/appraisal.service';
import { evaluationWeightTotal } from '@/types/hr/appraisal';
import type { AppraisalSettings } from '@/types/hr/appraisal';

/**
 * Appraisal settings profiles — the policy a cycle runs under.
 *
 * A cycle names exactly one profile, and the profile decides who evaluates, how their scores
 * combine, what has to happen before a result is final, and the thresholds the dashboards
 * read. Most organisations need two or three: a standard annual policy, a lighter probation
 * one, perhaps a senior-management variant.
 *
 * One profile is the tenant's **default** — a flag HR moves with *Make default*, at most one per
 * tenant (closure B6). `GET /default` used to return the most recently created profile, so any new
 * or test profile silently became the default; the page marked the newest for the same reason.
 */
function weightSummary(s: AppraisalSettings) {
  const parts: string[] = [];
  if (s.requireSelfEvaluation) parts.push(`self ${pct(s.selfEvaluationWeight)}`);
  if (s.requirePeerReviews) parts.push(`peer ${pct(s.peerEvaluationWeight)}`);
  if (s.requireManagerEvaluation) parts.push(`manager ${pct(s.managerEvaluationWeight)}`);
  return parts.length ? parts.join(' · ') : 'No evaluator enabled';
}

const pct = (weight: number) => `${Math.round((Number(weight) || 0) * 100)}%`;

export default function AppraisalSettingsListPage() {
  const { data, isLoading, isError, error } = useQuery({
    queryKey: ['hr', 'appraisal-settings'],
    queryFn: () => appraisalSettingsService.getAll(),
  });

  const queryClient = useQueryClient();
  const { toast } = useToast();
  // A profile in use changes its rules on a copy (performance closure E-e, D-67).
  const [cloneOf, setCloneOf] = useState<AppraisalSettings | null>(null);
  const makeDefault = useMutation({
    mutationFn: (id: string) => appraisalSettingsService.makeDefault(id),
    onSuccess: (profile) => {
      toast({ title: 'Default profile changed', description: `${profile.settingsName} is now the default.` });
      queryClient.invalidateQueries({ queryKey: ['hr', 'appraisal-settings'] });
    },
    onError: (e: any) =>
      toast({ title: 'Could not change the default', description: e?.message ?? 'Please try again.', variant: 'destructive' }),
  });

  const rows = data ?? [];
  // The default first, then newest first.
  const sorted = [...rows].sort(
    (a, b) =>
      Number(b.isDefault) - Number(a.isDefault) || (b.createdAt ?? '').localeCompare(a.createdAt ?? ''),
  );

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Appraisal Settings"
        description="Named policy profiles a cycle runs under: evaluators and their weights, sign-off steps, appeals and deadline thresholds."
        backHref="/administration/hr/performance"
        actions={
          <Button asChild>
            <Link href="/administration/hr/performance/settings/new">
              <Plus className="mr-2 h-4 w-4" />
              New profile
            </Link>
          </Button>
        }
      />

      <Card>
        <CardContent className="p-0">
          {isLoading ? (
            <div className="flex items-center justify-center py-12">
              <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
            </div>
          ) : isError ? (
            <EmptyState
              title="Could not load appraisal settings"
              description={(error as any)?.message || 'Please try again.'}
            />
          ) : sorted.length === 0 ? (
            <EmptyState
              title="No settings profiles yet"
              description="A cycle cannot be created without one — start with a profile describing your standard annual appraisal."
              action={
                <Button size="sm" variant="outline" asChild>
                  <Link href="/administration/hr/performance/settings/new">
                    <Plus className="mr-2 h-4 w-4" />
                    New profile
                  </Link>
                </Button>
              }
            />
          ) : (
            <div className="overflow-x-auto">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Profile</TableHead>
                    <TableHead>Evaluation weights</TableHead>
                    <TableHead>Sign-off</TableHead>
                    <TableHead>Appeals</TableHead>
                    <TableHead>Interim reviews</TableHead>
                    <TableHead className="w-[60px]" />
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {sorted.map((s) => {
                    const total = evaluationWeightTotal(s);
                    return (
                      <TableRow key={s.id}>
                        <TableCell>
                          <Link
                            href={`/administration/hr/performance/settings/${s.id}`}
                            className="font-medium hover:underline"
                          >
                            {s.settingsName}
                          </Link>
                          {s.isDefault && (
                            <Badge variant="secondary" className="ml-2">
                              Default
                            </Badge>
                          )}
                          {s.isInUse && (
                            <Badge
                              variant="outline"
                              className="ml-2"
                              title={`Read by ${s.inUseAppraisalCount} appraisal(s): ${s.inUseCycleNames.join(', ')}. Its rules change on a copy.`}
                            >
                              In use
                            </Badge>
                          )}
                        </TableCell>
                        <TableCell>
                          <div className="flex items-center gap-2">
                            <span className="text-muted-foreground">{weightSummary(s)}</span>
                            {Math.abs(total - 1) > 0.005 && (
                              <Badge variant="destructive">
                                Totals {Math.round(total * 100)}%
                              </Badge>
                            )}
                          </div>
                        </TableCell>
                        <TableCell className="text-muted-foreground">
                          {[
                            s.requireCalibration ? 'Calibration' : null,
                            s.requireHRReview ? 'HR review' : null,
                            s.requireEmployeeAcknowledgment ? 'Acknowledgment' : null,
                          ]
                            .filter(Boolean)
                            .join(' → ') || 'None'}
                        </TableCell>
                        <TableCell className="text-muted-foreground">
                          {s.enableAppeals ? `${s.appealWindowDays} days` : 'Disabled'}
                        </TableCell>
                        <TableCell className="text-muted-foreground">
                          {s.reviewFrequency === 'None' ? 'Year-end only' : s.reviewFrequency}
                        </TableCell>
                        <TableCell className="whitespace-nowrap">
                          {!s.isDefault && (
                            <Button
                              variant="ghost"
                              size="sm"
                              disabled={makeDefault.isPending}
                              onClick={() => makeDefault.mutate(s.id)}
                            >
                              Make default
                            </Button>
                          )}
                          <Button variant="ghost" size="sm" asChild>
                            <Link href={`/administration/hr/performance/settings/${s.id}`}>
                              Edit
                            </Link>
                          </Button>
                          <Button variant="ghost" size="sm" onClick={() => setCloneOf(s)}>
                            Copy
                          </Button>
                        </TableCell>
                      </TableRow>
                    );
                  })}
                </TableBody>
              </Table>
            </div>
          )}
        </CardContent>
      </Card>

      <CloneSettingsProfileDialog profile={cloneOf} onOpenChange={(open) => !open && setCloneOf(null)} />
    </div>
  );
}
