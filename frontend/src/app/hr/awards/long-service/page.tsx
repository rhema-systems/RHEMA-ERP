'use client';

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { CalendarCheck, CheckCircle2, Info, Loader2, Medal, Pencil, Plus } from 'lucide-react';
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
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { MetricTiles } from '@/components/hr/common/MetricTiles';
import { FinancePostingInlineStatus } from '@/components/hr/common/FinancePostingCard';
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
  /**
   * The by-hand record and its correction.
   *
   * ⚠ `isProcessed`, `presentationDate` and `presentationNotes` are carried through UNCHANGED
   * rather than omitted. `UpdateLongServiceAwardDto` takes all three, so a form that left them out
   * would quietly un-present an award somebody had already presented — the replace-set trap this
   * codebase has met before. They are edited from the Process dialog, not this one.
   */
  const [award, setAward] = useState<null | {
    id?: string;
    employeeId: string; awardTypeId: string;
    yearsOfService: string; serviceStartDate: string; milestoneDate: string;
    awardDescription: string; monetaryAmount: string; leaveDaysBonus: string; otherBenefits: string;
    employeeAwardId: string | null;
    isProcessed: boolean; presentationDate: string | null; presentationNotes: string | null;
  }>(null);
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

  const { data: awardTypes } = useQuery({
    queryKey: ['award-types'],
    queryFn: () => awardsService.getTypes(),
    enabled: Boolean(award) && !award?.id,
  });

  /**
   * ⚠ Until this existed a long-service award could only come into being through the sweep, so
   * one granted with the wrong value — or owed to somebody the sweep could not see, because their
   * `DateEmployed` is missing — had no path at all. It is money, and the sweep is still the normal
   * way: this is the exception and the correction.
   */
  const saveAward = useMutation({
    mutationFn: () => {
      if (!award) throw new Error('Nothing to save.');
      const money = (v: string) => (v.trim() === '' ? null : Number(v));
      if (award.id) {
        return awardsService.updateLongServiceAward(award.id, {
          id: award.id,
          employeeAwardId: award.employeeAwardId,
          awardDescription: award.awardDescription.trim(),
          monetaryAmount: money(award.monetaryAmount),
          leaveDaysBonus: money(award.leaveDaysBonus),
          otherBenefits: award.otherBenefits.trim() || null,
          isProcessed: award.isProcessed,
          presentationDate: award.presentationDate,
          presentationNotes: award.presentationNotes,
        });
      }
      return awardsService.createLongServiceAward({
        employeeId: award.employeeId,
        awardTypeId: award.awardTypeId,
        employeeAwardId: null,
        yearsOfService: Number(award.yearsOfService),
        serviceStartDate: new Date(award.serviceStartDate).toISOString().slice(0, 19),
        milestoneDate: new Date(award.milestoneDate).toISOString().slice(0, 19),
        awardDescription: award.awardDescription.trim(),
        monetaryAmount: money(award.monetaryAmount),
        leaveDaysBonus: money(award.leaveDaysBonus),
        otherBenefits: award.otherBenefits.trim() || null,
      });
    },
    onSuccess: () => {
      toast.success(award?.id ? 'Saved.' : 'Recorded.');
      setAward(null);
      queryClient.invalidateQueries({ queryKey: ['long-service-awards'] });
      queryClient.invalidateQueries({ queryKey: ['long-service-pending'] });
    },
    onError: (e: any) => toast.error(e?.body?.detail || e?.message || 'The award was refused.'),
  });

  /**
   * Seeded from the BY-ID read, never from the row.
   *
   * ⚠ `LongServiceAwardSummary` carries five fields; the update takes eight. Seeding the form
   * from the list would blank the description, the leave bonus and the other benefits on save —
   * the summary-shaped-read trap that has cost this codebase four separate panels.
   */
  const openEdit = async (row: LongServiceAwardSummary) => {
    try {
      const detail = await awardsService.getLongServiceAward(row.id);
      setAward({
        id: detail.id,
        employeeId: '',
        awardTypeId: '',
        yearsOfService: String(detail.yearsOfService),
        serviceStartDate: detail.serviceStartDate?.slice(0, 10) ?? '',
        milestoneDate: detail.milestoneDate?.slice(0, 10) ?? '',
        awardDescription: detail.awardDescription ?? '',
        monetaryAmount: detail.monetaryAmount === null ? '' : String(detail.monetaryAmount),
        leaveDaysBonus: detail.leaveDaysBonus === null ? '' : String(detail.leaveDaysBonus),
        otherBenefits: detail.otherBenefits ?? '',
        employeeAwardId: null,
        isProcessed: detail.isProcessed,
        presentationDate: detail.presentationDate,
        presentationNotes: detail.presentationNotes,
      });
    } catch (e: any) {
      toast.error(e?.body?.detail || e?.message || 'That award could not be opened.');
    }
  };

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
          <TableHead>Finance</TableHead>
          <TableHead />
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
            <TableCell>
              <FinancePostingInlineStatus sourceDocumentId={r.id} />
            </TableCell>
            <TableCell className="text-right">
              <Button size="sm" variant="ghost" onClick={() => openEdit(r)}>
                <Pencil className="h-4 w-4" />
              </Button>
              {withAction && (
                <Button size="sm" variant="outline" onClick={() => setProcessing(r)}>
                  <CalendarCheck className="mr-2 h-4 w-4" />
                  Process
                </Button>
              )}
            </TableCell>
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
        actions={
          <Button onClick={() => setAward({ employeeId: '', awardTypeId: '', yearsOfService: '', serviceStartDate: '', milestoneDate: '', awardDescription: '', monetaryAmount: '', leaveDaysBonus: '', otherBenefits: '', employeeAwardId: null, isProcessed: false, presentationDate: null, presentationNotes: null })}>
            <Plus className="mr-2 h-4 w-4" />
            Record an award
          </Button>
        }
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

      <Dialog open={Boolean(award)} onOpenChange={(o) => !o && setAward(null)}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>{award?.id ? 'Correct this award' : 'Record a long-service award'}</DialogTitle>
            <DialogDescription>
              {award?.id
                ? 'The presentation details are edited from Process, not here.'
                : 'For an award the sweep cannot see — a missing service date, or one agreed by hand.'}
            </DialogDescription>
          </DialogHeader>
          {award && (
            <div className="space-y-4">
              {!award.id && (
                <>
                  <div className="space-y-2">
                    <Label>Employee</Label>
                    <EmployeePicker
                      value={award.employeeId || null}
                      onChange={(v) => setAward({ ...award, employeeId: v ?? '' })}
                    />
                  </div>
                  <div className="space-y-2">
                    <Label>Award type</Label>
                    <Select
                      value={award.awardTypeId}
                      onValueChange={(v) => setAward({ ...award, awardTypeId: v })}
                    >
                      <SelectTrigger><SelectValue placeholder="Choose an award type" /></SelectTrigger>
                      <SelectContent>
                        {(awardTypes ?? []).map((t) => (
                          <SelectItem key={t.id} value={t.id}>{t.name}</SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </div>
                  <div className="grid grid-cols-3 gap-4">
                    <div className="space-y-2">
                      <Label htmlFor="lsYears">Milestone</Label>
                      <Input id="lsYears" type="number" min={1} value={award.yearsOfService}
                        onChange={(e) => setAward({ ...award, yearsOfService: e.target.value })} />
                      <p className="text-xs text-muted-foreground">Years the rung is for.</p>
                    </div>
                    <div className="space-y-2">
                      <Label htmlFor="lsStart">Service from</Label>
                      <Input id="lsStart" type="date" value={award.serviceStartDate}
                        onChange={(e) => setAward({ ...award, serviceStartDate: e.target.value })} />
                    </div>
                    <div className="space-y-2">
                      <Label htmlFor="lsReached">Reached</Label>
                      <Input id="lsReached" type="date" value={award.milestoneDate}
                        onChange={(e) => setAward({ ...award, milestoneDate: e.target.value })} />
                    </div>
                  </div>
                </>
              )}
              <div className="space-y-2">
                <Label htmlFor="lsDesc">Description</Label>
                <Input id="lsDesc" value={award.awardDescription}
                  onChange={(e) => setAward({ ...award, awardDescription: e.target.value })}
                  placeholder="10 years of service" />
              </div>
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="lsAmount">Value</Label>
                  <Input id="lsAmount" type="number" min={0} value={award.monetaryAmount}
                    onChange={(e) => setAward({ ...award, monetaryAmount: e.target.value })} />
                  <p className="text-xs text-muted-foreground">Blank is not zero — it means unpriced.</p>
                </div>
                <div className="space-y-2">
                  <Label htmlFor="lsLeave">Leave days</Label>
                  <Input id="lsLeave" type="number" min={0} value={award.leaveDaysBonus}
                    onChange={(e) => setAward({ ...award, leaveDaysBonus: e.target.value })} />
                </div>
              </div>
              <div className="space-y-2">
                <Label htmlFor="lsOther">Other benefits</Label>
                <Textarea id="lsOther" rows={2} value={award.otherBenefits}
                  onChange={(e) => setAward({ ...award, otherBenefits: e.target.value })} />
              </div>
              <div className="flex justify-end gap-2">
                <Button variant="outline" onClick={() => setAward(null)}>Cancel</Button>
                <Button
                  disabled={
                    saveAward.isPending ||
                    !award.awardDescription.trim() ||
                    (!award.id && (!award.employeeId || !award.awardTypeId || !award.yearsOfService ||
                      !award.serviceStartDate || !award.milestoneDate))
                  }
                  onClick={() => saveAward.mutate()}
                >
                  {saveAward.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                  {award.id ? 'Save' : 'Record'}
                </Button>
              </div>
            </div>
          )}
        </DialogContent>
      </Dialog>

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
