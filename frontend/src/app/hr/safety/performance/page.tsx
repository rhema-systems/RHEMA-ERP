'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { Loader2, Plus, ChevronLeft, ChevronRight } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Card, CardContent } from '@/components/ui/card';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { safetyPerformanceService } from '@/services/hr/safety-performance.service';
import type { ShePerformanceSnapshotSummary } from '@/types/hr/safety';

const periodLabel = (s: ShePerformanceSnapshotSummary) =>
  s.periodType === 'Annual'
    ? `${s.year}`
    : s.periodType === 'Quarterly'
      ? `Q${s.periodNumber} ${s.year}`
      : `${s.year}-${String(s.periodNumber).padStart(2, '0')}`;

/**
 * The KPI snapshot register, one year at a time. All figures shown are REPORTED — typed in by
 * the SHE officer, not computed from the registers — hence the badge in the header. The KPI
 * computation slice replaces that badge, not this screen.
 */
export default function PerformanceSnapshotsPage() {
  const [year, setYear] = useState(() => new Date().getFullYear());

  const { data: snapshots = [], isLoading } = useQuery({
    queryKey: ['hr', 'safety-performance', year],
    queryFn: () => safetyPerformanceService.getByYear(year),
  });

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="SHE Performance Snapshots"
        description="Hand-reported KPI figures per period. Management reviews a snapshot to lock it."
        backHref="/hr/safety"
        actions={
          <Button asChild>
            <Link href="/hr/safety/performance/new">
              <Plus className="mr-2 h-4 w-4" />
              New snapshot
            </Link>
          </Button>
        }
      />

      <div className="flex items-center gap-2">
        <Button variant="outline" size="icon" onClick={() => setYear((y) => y - 1)}>
          <ChevronLeft className="h-4 w-4" />
        </Button>
        <span className="min-w-16 text-center text-lg font-medium tabular-nums">{year}</span>
        <Button variant="outline" size="icon" onClick={() => setYear((y) => y + 1)}>
          <ChevronRight className="h-4 w-4" />
        </Button>
        <Badge variant="outline" className="ml-2">
          Reported figures — not computed
        </Badge>
      </div>

      <Card>
        <CardContent className="p-0">
          {isLoading ? (
            <div className="flex items-center justify-center py-16">
              <Loader2 className="text-muted-foreground h-6 w-6 animate-spin" />
            </div>
          ) : snapshots.length === 0 ? (
            <EmptyState
              title={`No snapshots for ${year}`}
              description="Enter the period's reported figures to start the register."
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Number</TableHead>
                  <TableHead>Period</TableHead>
                  <TableHead>Location</TableHead>
                  <TableHead className="text-right">Incidents</TableHead>
                  <TableHead className="text-right">Lost-time injuries</TableHead>
                  <TableHead className="text-right">LTIFR</TableHead>
                  <TableHead>Prepared</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {snapshots.map((s) => (
                  <TableRow key={s.id}>
                    <TableCell className="font-medium">
                      <Link href={`/hr/safety/performance/${s.id}`} className="hover:underline">
                        <span className="font-mono">{s.snapshotNumber}</span>
                      </Link>
                    </TableCell>
                    <TableCell>
                      {periodLabel(s)}
                      <span className="text-muted-foreground ml-2 text-xs">{s.periodTypeName}</span>
                    </TableCell>
                    <TableCell>{s.locationName ?? 'Whole organisation'}</TableCell>
                    <TableCell className="text-right tabular-nums">{s.totalIncidents}</TableCell>
                    <TableCell className="text-right tabular-nums">
                      {s.totalLostTimeInjuries}
                    </TableCell>
                    <TableCell className="text-right tabular-nums">
                      {s.lostTimeInjuryFrequencyRate ?? '—'}
                    </TableCell>
                    <TableCell>{new Date(s.preparedDate).toLocaleDateString()}</TableCell>
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
