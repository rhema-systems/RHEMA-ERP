'use client';

import { useMemo, useState } from 'react';
import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { GraduationCap, Loader2, Search, TrendingDown, Users } from 'lucide-react';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
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
 * Where the organisation is short against what its positions require — the training-needs view.
 *
 * ⚠ **"Below requirement" and "not assessed" are separate columns and must stay that way.** An
 * unknown is not a shortfall: folding them together would report a training need the organisation
 * has no evidence for, on the screen that decides training spend. The two call for different
 * actions — one for a course, the other for an assessment.
 */
export default function CompetencyGapsPage() {
  const [search, setSearch] = useState('');

  const { data: gaps, isLoading } = useQuery({
    queryKey: ['competencies', 'organisation-gaps'],
    queryFn: () => jobArchitectureService.getOrganisationGaps(),
  });

  const rows = useMemo(() => {
    const term = search.trim().toLowerCase();
    return (gaps ?? []).filter(
      (g) => !term || g.competencyName.toLowerCase().includes(term) || g.competencyCode.toLowerCase().includes(term),
    );
  }, [gaps, search]);

  const totals = useMemo(
    () =>
      (gaps ?? []).reduce(
        (acc, g) => ({
          below: acc.below + g.belowRequirement,
          unassessed: acc.unassessed + g.notAssessed,
          requiring: acc.requiring + g.employeesRequiring,
        }),
        { below: 0, unassessed: 0, requiring: 0 },
      ),
    [gaps],
  );

  return (
    <div className="space-y-6">
      <PageHeader
        title="Competency gaps"
        description="What positions require, measured against what people have been assessed at."
        actions={
          <Link href="/hr/competencies/me">
            <Button variant="outline">My competencies</Button>
          </Link>
        }
      />

      <div className="grid gap-4 sm:grid-cols-3">
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center gap-2 text-sm text-muted-foreground">
              <TrendingDown className="h-4 w-4" />
              Below requirement
            </div>
            <div className="mt-2 text-2xl font-semibold text-amber-700">{totals.below}</div>
            <div className="mt-1 text-xs text-muted-foreground">assessed and short — a training need</div>
          </CardContent>
        </Card>
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center gap-2 text-sm text-muted-foreground">
              <Search className="h-4 w-4" />
              Never assessed
            </div>
            <div className="mt-2 text-2xl font-semibold">{totals.unassessed}</div>
            {/* ⚠ Deliberately not added to the figure above. */}
            <div className="mt-1 text-xs text-muted-foreground">unknown, not short — needs assessing</div>
          </CardContent>
        </Card>
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center gap-2 text-sm text-muted-foreground">
              <Users className="h-4 w-4" />
              Requirements in force
            </div>
            <div className="mt-2 text-2xl font-semibold">{totals.requiring}</div>
            <div className="mt-1 text-xs text-muted-foreground">employee–competency pairs</div>
          </CardContent>
        </Card>
      </div>

      <Card>
        <CardContent className="space-y-4 pt-6">
          <div className="relative">
            <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
            <Input
              placeholder="Search competencies"
              className="pl-9"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
          </div>

          {isLoading ? (
            <div className="flex items-center justify-center py-16 text-muted-foreground">
              <Loader2 className="mr-2 h-5 w-5 animate-spin" />
              Loading…
            </div>
          ) : rows.length === 0 ? (
            <EmptyState
              icon={GraduationCap}
              title="No requirements to measure against"
              description="Set competency requirements on positions to see where the organisation stands."
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Competency</TableHead>
                  <TableHead>Category</TableHead>
                  <TableHead className="text-right">Requiring</TableHead>
                  <TableHead className="text-right">Meets</TableHead>
                  <TableHead className="text-right">Below</TableHead>
                  <TableHead className="text-right">Not assessed</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {rows.map((g) => (
                  <TableRow key={g.competencyId}>
                    <TableCell>
                      <div className="font-medium">{g.competencyName}</div>
                      <div className="font-mono text-xs text-muted-foreground">{g.competencyCode}</div>
                    </TableCell>
                    <TableCell>
                      <Badge variant="outline">{g.competencyCategory}</Badge>
                    </TableCell>
                    <TableCell className="text-right">{g.employeesRequiring}</TableCell>
                    <TableCell className="text-right text-emerald-700">{g.meetingRequirement}</TableCell>
                    <TableCell className="text-right font-medium text-amber-700">
                      {g.belowRequirement}
                    </TableCell>
                    <TableCell className="text-right text-muted-foreground">{g.notAssessed}</TableCell>
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
