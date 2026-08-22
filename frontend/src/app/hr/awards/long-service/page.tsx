'use client';

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { CalendarCheck, CheckCircle2, Info, Loader2, Medal } from 'lucide-react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Badge } from '@/components/ui/badge';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { MetricTiles } from '@/components/hr/common/MetricTiles';
import { awardsService } from '@/services/hr/awards.service';
import type { LongServiceAwardSummary } from '@/types/hr/awards';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

/**
 * ⚠ Null is **not** zero. A rung TDC has not priced grants an award with no amount, and printing a
 * currency zero would answer a question they have not.
 */
const fmtMoney = (v?: number | null) =>
  v === null || v === undefined
    ? <span className="text-muted-foreground">not set</span>
    : v.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });

/**
 * Long-service awards the sweep has granted, and what is still owed on them.
 *
 * ⚠ **Granting is not the end of it.** The sweep creates the record; somebody still has to hold the
 * presentation and mark it done. Until this screen existed those awards were created and then had
 * nowhere to go — `long-service/{id}/process` was reachable by nothing.
 *
 * ⚠ **`yearsOfService` on the award is the MILESTONE reached, not the service given.** Somebody
 * swept at twenty-two years holds the twenty-year award, and that is what the certificate prints.
 */
export default function LongServiceAwardsPage() {
  const queryClient = useQueryClient();
  const [processing, setProcessing] = useState<LongServiceAwardSummary | null>(null);
  const [presentationDate, setPresentationDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [notes, setNotes] = useState('');

  const { data: all, isLoading } = useQuery({
    queryKey: ['long-service-awards'],
    queryFn: () => awardsService.getLongServiceAwards(),
  });

  const { data: pending } = useQuery({
    queryKey: ['long-service-pending'],
    queryFn: () => awardsService.getLongServicePendingProcessing(),
  });

  // ⚠ Granted awards whose milestone date is still ahead — what has been COMMITTED TO and is
  // coming up. Not the same question as "who will qualify next", which is the sweep preview.
  const { data: upcoming } = useQuery({
    queryKey: ['long-service-upcoming'],
    queryFn: () => awardsService.getUpcomingMilestones(180),
  });

  const process = useMutation({
    mutationFn: () => {
      if (!processing) throw new Error('No award selected.');
      return awardsService.processLongServiceAward(processing.id, {
        awardId: processing.id,
        presentationDate: new Date(presentationDate).toISOString().slice(0, 19),
        presentationNotes: notes.trim() || null,
      });
    },
    onSuccess: () => {
      toast.success('Marked as processed.');
      setProcessing(null);
      setNotes('');
      queryClient.invalidateQueries({ queryKey: ['long-service-awards'] });
      queryClient.invalidateQueries({ queryKey: ['long-service-pending'] });
    },
    onError: (e: any) => toast.error(e?.body?.detail || e?.message || 'The change was refused.'),
  });

  const rows = all ?? [];
  const owed = pending ?? [];
  const unpriced = rows.filter((r) => r.monetaryAmount === null).length;

  const table = (data: LongServiceAwardSummary[], withAction: boolean) => (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>Employee</TableHead>
          <TableHead className="text-right">Milestone</TableHead>
          <TableHead>Reached</TableHead>
          <TableHead className="text-right">Value</TableHead>
          <TableHead>Presented</TableHead>
          {withAction && <TableHead />}
        </TableRow>
      </TableHeader>
      <TableBody>
        {data.map((r) => (
          <TableRow key={r.id}>
            <TableCell>
              {r.employeeName}
              <span className="ml-2 text-xs text-muted-foreground">{r.employeeNumber}</span>
            </TableCell>
            <TableCell className="text-right">{r.yearsOfService} years</TableCell>
            <TableCell>{fmtDate(r.milestoneDate)}</TableCell>
            <TableCell className="text-right">{fmtMoney(r.monetaryAmount)}</TableCell>
            <TableCell>
              {r.presentationDate ? (
                fmtDate(r.presentationDate)
              ) : r.isProcessed ? (
                <Badge variant="secondary">Processed</Badge>
              ) : (
                <span className="text-muted-foreground">—</span>
              )}
            </TableCell>
            {withAction && (
              <TableCell className="text-right">
                <Button size="sm" variant="outline" onClick={() => setProcessing(r)}>
                  <CalendarCheck className="mr-2 h-4 w-4" />
                  Process
                </Button>
              </TableCell>
            )}
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Long-service awards"
        description="Milestones the sweep has granted, and the presentations still owed."
        backHref="/hr/awards"
      />

      <MetricTiles
        tiles={[
          { label: 'Granted', value: rows.length, icon: Medal },
          {
            label: 'Awaiting presentation',
            value: owed.length,
            icon: CalendarCheck,
            tone: owed.length > 0 ? 'warning' : 'default',
          },
          {
            label: 'Processed',
            value: rows.filter((r) => r.isProcessed).length,
            icon: CheckCircle2,
          },
        ]}
      />

      {/* ⚠ Worth saying once, loudly: an unpriced award is an unanswered question rather than an
          award worth nothing. */}
      {unpriced > 0 && (
        <Alert>
          <Info className="h-4 w-4" />
          <AlertDescription>
            {unpriced} of {rows.length} granted awards carry no amount, because the milestone they
            were granted against has not been priced. That is an unanswered question, not an award
            worth nothing — see the long-service ladder in Award setup.
          </AlertDescription>
        </Alert>
      )}

      <Tabs defaultValue="pending">
        <TabsList>
          <TabsTrigger value="pending">Awaiting presentation ({owed.length})</TabsTrigger>
          <TabsTrigger value="upcoming">Coming up ({(upcoming ?? []).length})</TabsTrigger>
          <TabsTrigger value="all">All ({rows.length})</TabsTrigger>
        </TabsList>

        <TabsContent value="pending">
          <Card>
            <CardContent className="p-0">
              {isLoading ? (
                <div className="flex items-center justify-center p-12">
                  <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
                </div>
              ) : owed.length === 0 ? (
                <EmptyState
                  icon={CheckCircle2}
                  title="Nothing owed"
                  description="Every granted milestone has been processed. New ones appear here after a sweep."
                />
              ) : (
                table(owed, true)
              )}
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="upcoming">
          <Card>
            <CardContent className="p-0">
              {(upcoming ?? []).length === 0 ? (
                <EmptyState
                  icon={CalendarCheck}
                  title="Nothing in the next six months"
                  description="Granted milestones with a date still ahead appear here, so a presentation can be planned before it is due."
                />
              ) : (
                table(upcoming ?? [], false)
              )}
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="all">
          <Card>
            <CardContent className="p-0">
              {rows.length === 0 ? (
                <EmptyState
                  icon={Medal}
                  title="None granted"
                  description="Run the long-service sweep from Award setup to grant the milestones that have fallen due."
                />
              ) : (
                table(rows, false)
              )}
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>

      <Dialog open={Boolean(processing)} onOpenChange={(o) => !o && setProcessing(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Process this award</DialogTitle>
            <DialogDescription>
              {processing?.employeeName} — {processing?.yearsOfService} years
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="lsDate">Presentation date</Label>
              <Input
                id="lsDate"
                type="date"
                value={presentationDate}
                onChange={(e) => setPresentationDate(e.target.value)}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="lsNotes">Notes</Label>
              <Textarea id="lsNotes" rows={3} value={notes} onChange={(e) => setNotes(e.target.value)} />
            </div>
            <div className="flex justify-end gap-2">
              <Button variant="outline" onClick={() => setProcessing(null)}>Cancel</Button>
              <Button disabled={!presentationDate || process.isPending} onClick={() => process.mutate()}>
                {process.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                Mark processed
              </Button>
            </div>
          </div>
        </DialogContent>
      </Dialog>
    </div>
  );
}
