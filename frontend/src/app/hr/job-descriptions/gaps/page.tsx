'use client';

import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { CheckCircle2, ClipboardList, Loader2, Users } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
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
import { jobArchitectureService } from '@/services/hr/job-architecture.service';

/**
 * FR-HR-134 coverage: which positions still have no approved job description.
 *
 * ⚠ Ordered by how many people are doing the job, not alphabetically. A post with forty people and
 * no description is a different problem from an empty one, and a list sorted by name buries it.
 *
 * ⚠ The route is `/gaps`, not `/coverage`, and that is not a naming preference: the repository
 * `.gitignore` carries a bare `coverage/` rule for test-coverage output, which silently excluded
 * this file from every commit. Any route directory named `coverage`, `dist` or `build` disappears
 * the same way, without a warning.
 */
export default function JobDescriptionCoveragePage() {
  const { data: analytics } = useQuery({
    queryKey: ['job-descriptions', 'analytics'],
    queryFn: () => jobArchitectureService.getAnalytics(),
  });

  const { data: uncovered, isLoading } = useQuery({
    queryKey: ['job-descriptions', 'uncovered'],
    queryFn: () => jobArchitectureService.getUncoveredPositions(),
  });

  return (
    <div className="space-y-6">
      <PageHeader
        title="Job description coverage"
        description="Positions with no approved job description, and how many people hold them."
        backHref="/hr/job-descriptions"
      />

      {analytics && (
        <div className="grid gap-4 sm:grid-cols-3">
          <Card>
            <CardContent className="pt-6">
              <div className="flex items-center gap-2 text-sm text-muted-foreground">
                <CheckCircle2 className="h-4 w-4" />
                Covered
              </div>
              <div className="mt-2 text-2xl font-semibold">
                {analytics.positionsCovered} / {analytics.totalPositions}
              </div>
              <div className="mt-1 text-xs text-muted-foreground">
                positions with an approved description
              </div>
            </CardContent>
          </Card>
          <Card>
            <CardContent className="pt-6">
              <div className="flex items-center gap-2 text-sm text-muted-foreground">
                <ClipboardList className="h-4 w-4" />
                Outstanding
              </div>
              <div className="mt-2 text-2xl font-semibold">{analytics.positionsUncovered}</div>
              <div className="mt-1 text-xs text-muted-foreground">still to describe or approve</div>
            </CardContent>
          </Card>
          <Card>
            <CardContent className="pt-6">
              <div className="flex items-center gap-2 text-sm text-muted-foreground">
                <Users className="h-4 w-4" />
                People affected
              </div>
              <div className="mt-2 text-2xl font-semibold">
                {(uncovered ?? []).reduce((sum, p) => sum + p.currentlyFilled, 0)}
              </div>
              <div className="mt-1 text-xs text-muted-foreground">
                doing a job nobody has described
              </div>
            </CardContent>
          </Card>
        </div>
      )}

      <Card>
        <CardContent className="pt-6">
          {isLoading ? (
            <div className="flex items-center justify-center py-16 text-muted-foreground">
              <Loader2 className="mr-2 h-5 w-5 animate-spin" />
              Loading…
            </div>
          ) : (uncovered ?? []).length === 0 ? (
            <EmptyState
              icon={CheckCircle2}
              title="Every position is covered"
              description="All positions have an approved job description."
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Position</TableHead>
                  <TableHead>Organisation unit</TableHead>
                  <TableHead className="text-right">In post</TableHead>
                  <TableHead>What it needs</TableHead>
                  <TableHead />
                </TableRow>
              </TableHeader>
              <TableBody>
                {(uncovered ?? []).map((p) => (
                  <TableRow key={p.positionId}>
                    <TableCell className="font-medium">{p.positionTitle}</TableCell>
                    <TableCell>{p.organizationUnitName || '—'}</TableCell>
                    <TableCell className="text-right">{p.currentlyFilled}</TableCell>
                    <TableCell>
                      {/* ⚠ Two different problems needing two different people. */}
                      {p.hasUnapprovedDraft ? (
                        <Badge className="bg-amber-100 text-amber-800">An approval</Badge>
                      ) : (
                        <Badge variant="outline">An author</Badge>
                      )}
                    </TableCell>
                    <TableCell className="text-right">
                      {!p.hasUnapprovedDraft && (
                        <Link href={`/hr/job-descriptions/new?positionId=${p.positionId}`}>
                          <Button size="sm" variant="outline">
                            Describe it
                          </Button>
                        </Link>
                      )}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
