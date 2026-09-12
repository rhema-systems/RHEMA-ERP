'use client';

import { use, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { CheckCircle2, Grid3x3, Loader2, Lock, Plus } from 'lucide-react';
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
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
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
import { useToast } from '@/hooks/use-toast';
import { talentReviewService } from '@/services/hr/succession.service';
import { NineBoxGrid } from '@/components/hr/succession/NineBoxGrid';
import { TalentRatingDialog } from '@/components/hr/succession/TalentRatingDialog';
import type { TalentReviewRatingSummary } from '@/types/hr/succession';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const spaced = (v?: string | null) => (v ? v.replace(/([a-z])([A-Z0-9])/g, '$1 $2') : '—');

export default function TalentReviewDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [rating, setRating] = useState(false);
  const [confirming, setConfirming] = useState<TalentReviewRatingSummary | null>(null);
  const [calibrationNotes, setCalibrationNotes] = useState('');
  const [finalizing, setFinalizing] = useState(false);
  const [sessionNotes, setSessionNotes] = useState('');

  const { data: session, isLoading } = useQuery({
    queryKey: ['talent-reviews', id],
    queryFn: () => talentReviewService.getById(id),
  });

  const refresh = () => queryClient.invalidateQueries({ queryKey: ['talent-reviews'] });

  const fail = (fallback: string) => (error: any) => {
    const status = error?.response?.status;
    toast({
      variant: 'destructive',
      title: status === 403 ? 'That is not yours to do' : status === 409 ? 'Not allowed now' : fallback,
      description: error?.response?.data?.detail ?? error?.message,
    });
  };

  const confirm = useMutation({
    mutationFn: (r: TalentReviewRatingSummary) =>
      talentReviewService.confirmCalibration(r.id, {
        ratingId: r.id,
        calibrationNotes: calibrationNotes || null,
      }),
    onSuccess: async () => {
      await refresh();
      setConfirming(null);
      setCalibrationNotes('');
      toast({
        title: 'Calibration confirmed',
        // Worth saying: this is the step that leaves the session and reaches the employee's record.
        description: "The placement is now the employee's latest, and is published to their talent pool record.",
      });
    },
    onError: fail('Could not confirm the calibration'),
  });

  const finalize = useMutation({
    mutationFn: () =>
      talentReviewService.finalize(id, { sessionId: id, sessionNotes: sessionNotes || null }),
    onSuccess: async () => {
      await refresh();
      setFinalizing(false);
      toast({ title: 'Session finalized', description: 'It is now frozen and cannot be changed.' });
    },
    onError: fail('Could not finalize the session'),
  });

  if (isLoading) {
    return (
      <div className="flex items-center justify-center p-12">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (!session) return null;

  const ratings = session.ratings ?? [];
  const pending = ratings.filter((r) => !r.calibrationConfirmed);

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={session.sessionName}
        description={`${session.reviewYear} · ${fmtDate(session.sessionDate)}${
          session.organizationUnitName ? ` · ${session.organizationUnitName}` : ''
        }`}
        backHref="/hr/succession/reviews"
        actions={
          session.isFinalized ? (
            <Badge variant="outline" className="gap-1 px-3 py-1.5">
              <Lock className="h-3.5 w-3.5" />
              Finalized {fmtDate(session.finalizedDate)}
              {session.finalizedByName && ` by ${session.finalizedByName}`}
            </Badge>
          ) : (
            <div className="flex gap-2">
              <Button variant="outline" onClick={() => setRating(true)}>
                <Plus className="mr-2 h-4 w-4" />
                Rate someone
              </Button>
              <Button onClick={() => setFinalizing(true)} disabled={ratings.length === 0}>
                <Lock className="mr-2 h-4 w-4" />
                Finalize
              </Button>
            </div>
          )
        }
      />

      {session.isFinalized && (
        <Card>
          <CardContent className="p-4 text-sm text-muted-foreground">
            This session is closed. Its ratings are the record of what the meeting decided and can
            no longer be added to, edited or removed — run a new session to reassess anyone.
          </CardContent>
        </Card>
      )}

      <Tabs defaultValue="grid">
        <TabsList>
          <TabsTrigger value="grid">Nine box</TabsTrigger>
          <TabsTrigger value="ratings">Ratings ({ratings.length})</TabsTrigger>
        </TabsList>

        <TabsContent value="grid" className="pt-4">
          {ratings.length === 0 ? (
            <EmptyState
              icon={Grid3x3}
              title="Nobody rated yet"
              description="Add ratings and they appear in the grid as you go."
            />
          ) : (
            <NineBoxGrid
              ratings={ratings}
              onSelect={(r) => !session.isFinalized && !r.calibrationConfirmed && setConfirming(r)}
            />
          )}
        </TabsContent>

        <TabsContent value="ratings" className="pt-4">
          <Card>
            <CardContent className="p-0">
              {ratings.length === 0 ? (
                <EmptyState icon={Grid3x3} title="No ratings" description="Nobody has been placed yet." />
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Employee</TableHead>
                      <TableHead>Performance</TableHead>
                      <TableHead>Potential</TableHead>
                      <TableHead>Movement</TableHead>
                      <TableHead>Calibration</TableHead>
                      <TableHead className="text-right">Actions</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {ratings.map((r) => (
                      <TableRow key={r.id}>
                        <TableCell>
                          <div className="font-medium">{r.employeeName}</div>
                          <div className="text-xs text-muted-foreground">{r.employeePosition}</div>
                        </TableCell>
                        <TableCell>{spaced(r.performance)}</TableCell>
                        <TableCell>{spaced(r.potential)}</TableCell>
                        <TableCell className="text-sm text-muted-foreground">
                          {r.previousPerformance || r.previousPotential
                            ? `was ${spaced(r.previousPerformance)} / ${spaced(r.previousPotential)}`
                            : 'first placement'}
                        </TableCell>
                        <TableCell>
                          {r.calibrationConfirmed ? (
                            <Badge variant="outline" className="gap-1">
                              <CheckCircle2 className="h-3 w-3" />
                              Confirmed
                            </Badge>
                          ) : (
                            <Badge variant="outline">Draft</Badge>
                          )}
                        </TableCell>
                        <TableCell className="text-right">
                          {!r.calibrationConfirmed && !session.isFinalized && (
                            <Button variant="ghost" size="sm" onClick={() => setConfirming(r)}>
                              Confirm
                            </Button>
                          )}
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>

      <TalentRatingDialog open={rating} onOpenChange={setRating} session={session} />

      <Dialog open={!!confirming} onOpenChange={(o) => !o && setConfirming(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Confirm {confirming?.employeeName}&apos;s placement</DialogTitle>
            <DialogDescription>
              Confirming publishes {spaced(confirming?.performance)} / {spaced(confirming?.potential)}{' '}
              to their talent pool record, and the rating can no longer be edited. Recorded against
              your sign-in.
            </DialogDescription>
          </DialogHeader>
          <Textarea
            value={calibrationNotes}
            onChange={(e) => setCalibrationNotes(e.target.value)}
            placeholder="What the session agreed, and why"
            rows={3}
          />
          <DialogFooter>
            <Button variant="outline" onClick={() => setConfirming(null)}>
              Cancel
            </Button>
            <Button disabled={confirm.isPending} onClick={() => confirming && confirm.mutate(confirming)}>
              {confirm.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Confirm calibration
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={finalizing} onOpenChange={setFinalizing}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Finalize {session.sessionName}</DialogTitle>
            <DialogDescription>
              This cannot be undone. Afterwards no rating can be added, changed or removed —
              including the {pending.length} still awaiting calibration, which will stay as drafts
              permanently.
            </DialogDescription>
          </DialogHeader>
          <Textarea
            value={sessionNotes}
            onChange={(e) => setSessionNotes(e.target.value)}
            placeholder="Closing notes for the session"
            rows={3}
          />
          <DialogFooter>
            <Button variant="outline" onClick={() => setFinalizing(false)}>
              Cancel
            </Button>
            <Button disabled={finalize.isPending} onClick={() => finalize.mutate()}>
              {finalize.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Finalize session
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
