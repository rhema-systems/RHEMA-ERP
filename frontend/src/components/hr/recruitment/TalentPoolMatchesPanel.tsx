'use client';

import { useMemo } from 'react';
import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { Loader2 } from 'lucide-react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { formatDate, humanizeEnum } from '@/lib/hr/attendance-format';
import { talentPoolService } from '@/services/hr/talent-pool.service';

/**
 * Active talent-pool candidates weighed against this vacancy — the sourcing shortlist before anyone
 * has applied.
 *
 * Two scores, side by side, and deliberately **not** merged (round 4, decision D-7):
 *
 * - **Fit**, out of 90 — the blind 40/30/20 rubric over experience, work-mode preference and
 *   availability. It knows nothing about what the vacancy actually asks for.
 * - **Criteria**, out of 100 — the vacancy's own shortlisting criteria, run through the same engine
 *   that scores applications.
 *
 * Averaging them would answer neither question. A candidate can be a perfect fit on paper and miss
 * a mandatory qualification, and the recruiter needs to see both facts, not their mean.
 *
 * ⚠ Both denominators are rendered. The fit score was served with `matchScoreMax` from G-13.2
 * onward and shown bare until lane B4, so nobody could tell whether 65 was good.
 */
export function TalentPoolMatchesPanel({ vacancyId }: { vacancyId: string }) {
  const matches = useQuery({
    queryKey: ['hr', 'vacancy-pool-matches', vacancyId],
    queryFn: () => talentPoolService.matchToVacancy(vacancyId),
    enabled: !!vacancyId,
  });

  // ⚠ Its own query, allowed to fail on its own. The screen REFUSES when the vacancy states no
  // criteria, which is the common case on a vacancy nobody has finished setting up — and that
  // refusal must not take the fit column down with it.
  const screen = useQuery({
    queryKey: ['hr', 'vacancy-pool-screen', vacancyId],
    queryFn: () => talentPoolService.screenAgainstVacancy(vacancyId, { topN: 500 }),
    enabled: !!vacancyId,
    retry: false,
  });

  const criteriaScores = useMemo(() => {
    const map = new Map<string, { score: number | null; max: number; allMandatoryPassed: boolean }>();
    for (const row of screen.data?.rows ?? []) {
      map.set(row.candidateId, {
        score: row.criteriaScore,
        max: row.criteriaScoreMax,
        allMandatoryPassed: row.allMandatoryPassed,
      });
    }
    return map;
  }, [screen.data]);

  const rows = matches.data ?? [];

  return (
    <Card>
      <CardHeader className="pb-3">
        <CardTitle className="text-base">Talent pool matches</CardTitle>
        <CardDescription>
          Active pool candidates worth approaching for this role — before advertising it. Screen and
          invite them from the{' '}
          <Link href="/hr/recruitment/talent-pool" className="underline">
            talent pool
          </Link>
          .
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-3 p-0">
        {screen.isError && (
          <div className="px-6 pt-4">
            <Alert>
              <AlertDescription className="text-sm">
                {/* The server's sentence, not a paraphrase: it names the tab that fixes it. */}
                {(screen.error as any)?.message ?? 'The criteria score could not be worked out for this vacancy.'}
              </AlertDescription>
            </Alert>
          </div>
        )}

        {matches.isLoading ? (
          <div className="flex items-center justify-center py-10">
            <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
          </div>
        ) : rows.length === 0 ? (
          <div className="py-8">
            <EmptyState
              title="No active pool candidates"
              description="The talent pool has nobody with Active status to match."
            />
          </div>
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Candidate</TableHead>
                <TableHead>Headline</TableHead>
                <TableHead className="w-24">Experience</TableHead>
                <TableHead className="w-28">Available</TableHead>
                <TableHead className="w-24 text-right" title="Experience, work mode and availability">
                  Fit
                </TableHead>
                <TableHead className="w-32 text-right" title="This vacancy's own shortlisting criteria">
                  Criteria
                </TableHead>
                <TableHead>Why</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {rows.map((m) => {
                const criteria = criteriaScores.get(m.candidateId);
                return (
                  <TableRow key={m.candidateId}>
                    <TableCell>
                      <Link
                        href={`/hr/recruitment/candidates/${m.candidateId}`}
                        className="font-medium hover:underline"
                      >
                        {m.candidateName}
                      </Link>
                      <div className="text-xs text-muted-foreground">{m.candidateNumber}</div>
                    </TableCell>
                    <TableCell className="text-sm">{m.headline ?? '—'}</TableCell>
                    <TableCell className="text-sm">
                      {m.totalYearsExperience != null ? `${m.totalYearsExperience} yrs` : '—'}
                    </TableCell>
                    <TableCell className="text-sm">
                      {m.availableFrom ? formatDate(m.availableFrom) : 'Not on file'}
                    </TableCell>
                    <TableCell className="text-right tabular-nums">
                      {m.matchScore}
                      <span className="text-xs text-muted-foreground"> / {m.matchScoreMax ?? 90}</span>
                    </TableCell>
                    <TableCell className="text-right">
                      {screen.isLoading ? (
                        <Loader2 className="ml-auto h-3 w-3 animate-spin text-muted-foreground" />
                      ) : !criteria ? (
                        <span className="text-xs text-muted-foreground">—</span>
                      ) : !criteria.allMandatoryPassed ? (
                        <Badge variant="destructive" className="text-[10px]">
                          Missed a mandatory
                        </Badge>
                      ) : criteria.score === null ? (
                        // ⚠ Not "0". Null means the criteria asked questions this record cannot
                        // answer; rendering it as zero would rank an unknown beside a demonstrated
                        // miss, which is the fault this module keeps having to remove.
                        <span className="text-xs text-muted-foreground">Not measurable</span>
                      ) : (
                        <span className="tabular-nums">
                          {criteria.score}
                          <span className="text-xs text-muted-foreground"> / {criteria.max}</span>
                        </span>
                      )}
                    </TableCell>
                    <TableCell className="text-xs text-muted-foreground">
                      {m.matchReasons.join(' · ')}
                      {m.preferredWorkArrangementName
                        ? ` · Prefers ${humanizeEnum(m.preferredWorkArrangementName)}`
                        : ''}
                    </TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
        )}
      </CardContent>
    </Card>
  );
}
