'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useParams, useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2, Pencil, Trash2, CheckCircle2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Textarea } from '@/components/ui/textarea';
import { Label } from '@/components/ui/label';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import { safetyPerformanceService } from '@/services/hr/safety-performance.service';
import type { ShePerformanceSnapshot } from '@/types/hr/safety';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const num = (v: number | null | undefined, suffix = '') =>
  v === null || v === undefined ? '—' : `${v}${suffix}`;

function FigureGroup({ title, rows }: { title: string; rows: [string, string | number][] }) {
  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">{title}</CardTitle>
      </CardHeader>
      <CardContent>
        <dl className="grid grid-cols-2 gap-x-6 gap-y-2 text-sm md:grid-cols-3">
          {rows.map(([label, value]) => (
            <div key={label} className="flex items-baseline justify-between gap-2 border-b py-1">
              <dt className="text-muted-foreground">{label}</dt>
              <dd className="font-medium tabular-nums">{value}</dd>
            </div>
          ))}
        </dl>
      </CardContent>
    </Card>
  );
}

/**
 * One snapshot, in full. Every figure is REPORTED (the badge says so). Editing is figures-only
 * and disappears once management reviews — the server locks the record at that point, so the
 * button not being there is honesty, not decoration.
 */
export default function PerformanceSnapshotDetailPage() {
  const { id } = useParams<{ id: string }>();
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [reviewOpen, setReviewOpen] = useState(false);
  const [reviewerId, setReviewerId] = useState<string | null>(null);
  const [reviewComments, setReviewComments] = useState('');
  const [busy, setBusy] = useState(false);

  const {
    data: snapshot,
    isLoading,
    isError,
  } = useQuery({
    queryKey: ['hr', 'safety-performance', 'detail', id],
    queryFn: () => safetyPerformanceService.getById(id),
    enabled: !!id,
  });

  const invalidate = () =>
    queryClient.invalidateQueries({ queryKey: ['hr', 'safety-performance'] });

  const handleReview = async () => {
    if (!reviewerId) return;
    setBusy(true);
    try {
      await safetyPerformanceService.review(id, {
        snapshotId: id,
        reviewedById: reviewerId,
        managementComments: reviewComments || null,
      });
      await invalidate();
      await queryClient.invalidateQueries({
        queryKey: ['hr', 'safety-performance', 'detail', id],
      });
      toast({ title: 'Snapshot reviewed', description: 'The figures are now locked.' });
      setReviewOpen(false);
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to record the review.',
        variant: 'destructive',
      });
    } finally {
      setBusy(false);
    }
  };

  const handleDelete = async () => {
    if (!window.confirm('Delete this snapshot? The period can then be re-entered from scratch.'))
      return;
    setBusy(true);
    try {
      await safetyPerformanceService.remove(id);
      await invalidate();
      toast({ title: 'Snapshot deleted' });
      router.push('/hr/safety/performance');
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to delete the snapshot.',
        variant: 'destructive',
      });
      setBusy(false);
    }
  };

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-24">
        <Loader2 className="text-muted-foreground h-6 w-6 animate-spin" />
      </div>
    );
  }

  if (isError || !snapshot) {
    return (
      <div className="p-6">
        <EmptyState title="Snapshot not found" description="It may have been deleted." />
      </div>
    );
  }

  const s: ShePerformanceSnapshot = snapshot;
  const reviewed = !!s.reviewedById;
  const period =
    s.periodType === 'Annual'
      ? `${s.year}`
      : s.periodType === 'Quarterly'
        ? `Q${s.periodNumber} ${s.year}`
        : `${s.year}-${String(s.periodNumber).padStart(2, '0')}`;

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={s.snapshotNumber}
        description={`${s.periodTypeName} snapshot for ${period} — ${s.locationName ?? 'whole organisation'}.`}
        backHref="/hr/safety/performance"
        actions={
          <div className="flex gap-2">
            {!reviewed && (
              <>
                <Button asChild variant="outline">
                  <Link href={`/hr/safety/performance/${s.id}/edit`}>
                    <Pencil className="mr-2 h-4 w-4" />
                    Edit figures
                  </Link>
                </Button>
                <Button onClick={() => setReviewOpen(true)}>
                  <CheckCircle2 className="mr-2 h-4 w-4" />
                  Record review
                </Button>
              </>
            )}
            <Button variant="destructive" onClick={handleDelete} disabled={busy}>
              <Trash2 className="mr-2 h-4 w-4" />
              Delete
            </Button>
          </div>
        }
      />

      <div className="flex flex-wrap items-center gap-2">
        <Badge variant="outline">Reported figures — not computed</Badge>
        {reviewed ? (
          <Badge variant="secondary">
            Reviewed by {s.reviewedByName ?? '—'} on {fmtDate(s.reviewedDate)} — locked
          </Badge>
        ) : (
          <Badge variant="outline">Awaiting management review</Badge>
        )}
        <Badge variant="outline">
          Prepared by {s.preparedByName} on {fmtDate(s.preparedDate)}
        </Badge>
      </div>

      <FigureGroup
        title="Incidents & injuries"
        rows={[
          ['Accidents', s.totalAccidents],
          ['All incidents', s.totalIncidents],
          ['Near misses', s.totalNearMisses],
          ['Dangerous occurrences', s.totalDangerousOccurrences],
          ['Fatalities', s.totalFatalities],
          ['Lost-time injuries', s.totalLostTimeInjuries],
          ['LTIFR (reported)', num(s.lostTimeInjuryFrequencyRate)],
          ['Man-hours worked', s.totalManHoursWorked],
          ['Lost days', s.totalLostDays],
        ]}
      />

      <FigureGroup
        title="Inspections & corrective actions"
        rows={[
          ['Inspections planned', s.inspectionsPlanned],
          ['Conducted', s.inspectionsConducted],
          ['Overdue', s.inspectionsOverdue],
          ['CAs issued', s.correctiveActionsIssued],
          ['CAs completed', s.correctiveActionsCompleted],
          ['CAs overdue', s.correctiveActionsOverdue],
          ['CA closure rate (reported)', num(s.correctiveActionClosureRate, '%')],
        ]}
      />

      <FigureGroup
        title="Training & contractors"
        rows={[
          ['Training planned', s.trainingProgramsPlanned],
          ['Training conducted', s.trainingProgramsConducted],
          ['Training hours', s.totalTrainingHours],
          ['Contractors on site', s.contractorsOnSite],
          ['Contractor inspections', s.contractorInspectionsConducted],
          ['Non-compliance notices', s.contractorNonComplianceNoticesIssued],
          ['Contractor compliance (reported)', num(s.contractorComplianceRate, '%')],
        ]}
      />

      <FigureGroup
        title="Environment, drills & compliance"
        rows={[
          ['Environmental incidents', s.environmentalIncidents],
          ['Reported to EPA', s.environmentalIncidentsReportedToEpa],
          ['Drills planned', s.emergencyDrillsPlanned],
          ['Drills conducted', s.emergencyDrillsConducted],
          ['PPE compliance (reported)', num(s.ppeComplianceRate, '%')],
          ['Housekeeping rating (reported)', num(s.housekeepingComplianceRating, '%')],
          ['Obligations total', s.regulatoryObligationsTotal],
          ['Compliant', s.regulatoryObligationsCompliant],
          ['Non-compliant', s.regulatoryObligationsNonCompliant],
          ['Expiring soon', s.regulatoryObligationsExpiringSoon],
        ]}
      />

      {s.managementComments && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Management comments</CardTitle>
          </CardHeader>
          <CardContent>
            <p className="whitespace-pre-wrap text-sm">{s.managementComments}</p>
          </CardContent>
        </Card>
      )}

      <Dialog open={reviewOpen} onOpenChange={setReviewOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Record management review</DialogTitle>
            <DialogDescription>
              Reviewing locks the snapshot — the figures can no longer be edited afterwards.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label>Reviewed by</Label>
              <EmployeePicker value={reviewerId} onChange={(id) => setReviewerId(id)} />
            </div>
            <div className="space-y-2">
              <Label htmlFor="review-comments">Comments</Label>
              <Textarea
                id="review-comments"
                rows={3}
                value={reviewComments}
                onChange={(e) => setReviewComments(e.target.value)}
                placeholder="Management's reading of the period."
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setReviewOpen(false)} disabled={busy}>
              Cancel
            </Button>
            <Button onClick={handleReview} disabled={busy || !reviewerId}>
              {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Record review
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
