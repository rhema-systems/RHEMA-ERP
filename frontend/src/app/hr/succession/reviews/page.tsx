'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { CalendarCheck, Grid3x3, Loader2, Lock, Plus } from 'lucide-react';
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
import { talentReviewService } from '@/services/hr/succession.service';
import { TalentReviewFormDialog } from '@/components/hr/succession/TalentReviewFormDialog';
import type { TalentReviewSessionSummary } from '@/types/hr/succession';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

type View = 'all' | 'pending' | 'finalized';

const VIEWS: { key: View; label: string; icon: typeof Grid3x3; hint: string }[] = [
  { key: 'all', label: 'All sessions', icon: Grid3x3, hint: 'Every calibration session on record.' },
  {
    key: 'pending',
    label: 'Still open',
    icon: CalendarCheck,
    hint: 'Sessions that can still be rated and calibrated.',
  },
  {
    key: 'finalized',
    label: 'Closed',
    icon: Lock,
    hint: 'Finalized sessions. Frozen — their ratings are a record of what the meeting decided.',
  },
];

export default function TalentReviewsPage() {
  const [view, setView] = useState<View>('all');
  const [creating, setCreating] = useState(false);

  const { data, isLoading } = useQuery({
    queryKey: ['talent-reviews', view],
    queryFn: () => {
      if (view === 'pending') return talentReviewService.getPending();
      if (view === 'finalized') return talentReviewService.getFinalized();
      return talentReviewService.getAll();
    },
  });

  const activeView = VIEWS.find((v) => v.key === view) ?? VIEWS[0];
  const rows: TalentReviewSessionSummary[] = data ?? [];

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Talent reviews"
        description="Calibration sessions and the nine-box placements they settle."
        backHref="/hr/succession"
        actions={
          <Button onClick={() => setCreating(true)}>
            <Plus className="mr-2 h-4 w-4" />
            New session
          </Button>
        }
      />

      <div className="flex flex-wrap gap-2">
        {VIEWS.map((v) => (
          <Button
            key={v.key}
            variant={view === v.key ? 'default' : 'outline'}
            size="sm"
            onClick={() => setView(v.key)}
          >
            <v.icon className="mr-2 h-4 w-4" />
            {v.label}
          </Button>
        ))}
      </div>

      <Card>
        <CardContent className="p-4">
          <p className="text-sm text-muted-foreground">{activeView.hint}</p>
        </CardContent>
      </Card>

      <Card>
        <CardContent className="p-0">
          {isLoading ? (
            <div className="flex items-center justify-center p-12">
              <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
            </div>
          ) : rows.length === 0 ? (
            <EmptyState
              icon={Grid3x3}
              title="No review sessions"
              description="A calibration session is where managers agree where people actually sit, rather than each rating in isolation."
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Session</TableHead>
                  <TableHead>Year</TableHead>
                  <TableHead>Date</TableHead>
                  <TableHead>Facilitator</TableHead>
                  <TableHead>Scope</TableHead>
                  <TableHead className="text-right">Rated</TableHead>
                  <TableHead>Status</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {rows.map((sn) => (
                  <TableRow key={sn.id}>
                    <TableCell>
                      <Link
                        href={`/hr/succession/reviews/${sn.id}`}
                        className="font-medium hover:underline"
                      >
                        {sn.sessionName}
                      </Link>
                    </TableCell>
                    <TableCell>{sn.reviewYear}</TableCell>
                    <TableCell>{fmtDate(sn.sessionDate)}</TableCell>
                    <TableCell>{sn.facilitatedByName ?? '—'}</TableCell>
                    <TableCell>{sn.organizationUnitName ?? 'Whole organisation'}</TableCell>
                    <TableCell className="text-right">{sn.ratingCount ?? 0}</TableCell>
                    <TableCell>
                      {sn.isFinalized ? (
                        <Badge variant="outline" className="gap-1">
                          <Lock className="h-3 w-3" />
                          Finalized
                        </Badge>
                      ) : (
                        <Badge>Open</Badge>
                      )}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      <TalentReviewFormDialog open={creating} onOpenChange={setCreating} />
    </div>
  );
}
