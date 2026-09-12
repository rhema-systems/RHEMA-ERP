'use client';

import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { Loader2 } from 'lucide-react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { formatDate, humanizeEnum } from '@/lib/hr/attendance-format';
import { talentPoolService } from '@/services/hr/talent-pool.service';

/**
 * Active talent-pool candidates scored against this vacancy — the sourcing shortlist before
 * anyone has applied. Scored on experience, work-mode preference and availability (40/30/20,
 * the same rubric as the candidate-side match).
 */
export function TalentPoolMatchesPanel({ vacancyId }: { vacancyId: string }) {
  const matches = useQuery({
    queryKey: ['hr', 'vacancy-pool-matches', vacancyId],
    queryFn: () => talentPoolService.matchToVacancy(vacancyId),
    enabled: !!vacancyId,
  });

  return (
    <Card>
      <CardHeader className="pb-3">
        <CardTitle className="text-base">Talent pool matches</CardTitle>
        <CardDescription>
          Active pool candidates worth approaching for this role — before advertising it.
        </CardDescription>
      </CardHeader>
      <CardContent className="p-0">
        {matches.isLoading ? (
          <div className="flex items-center justify-center py-10">
            <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
          </div>
        ) : (matches.data ?? []).length === 0 ? (
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
                <TableHead className="w-20 text-right">Score</TableHead>
                <TableHead>Why</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {(matches.data ?? []).map((m) => (
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
                    {m.availableFrom ? formatDate(m.availableFrom) : 'Now'}
                  </TableCell>
                  <TableCell className="text-right tabular-nums">{m.matchScore}</TableCell>
                  <TableCell className="text-xs text-muted-foreground">
                    {m.matchReasons.join(' · ')}
                    {m.preferredWorkArrangementName
                      ? ` · Prefers ${humanizeEnum(m.preferredWorkArrangementName)}`
                      : ''}
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
