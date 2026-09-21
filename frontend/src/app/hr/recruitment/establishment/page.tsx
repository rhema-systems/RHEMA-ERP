'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Building2, FilePlus2, Loader2, MessageSquare, RefreshCw, ShieldCheck, XCircle } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { Label } from '@/components/ui/label';
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
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
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { MetricTiles } from '@/components/hr/common/MetricTiles';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { useAuth } from '@/hooks/use-auth';
import { useToast } from '@/hooks/use-toast';
import { formatDate, humanizeEnum } from '@/lib/hr/attendance-format';
import { positionVacancyService } from '@/services/hr/recruitment.service';
import { OrganizationUnitPicker } from '@/components/hr/common/OrganizationUnitPicker';
import { PlanBudgetFromEstablishmentDialog } from '@/components/hr/manpower/PlanBudgetFromEstablishmentDialog';
import { Banknote } from 'lucide-react';

/**
 * Establishment vs. actual headcount — the front of the recruitment funnel.
 *
 * A "position vacancy" here is a *gap*, not an advert: a position whose filled count is below its
 * establishment. Reconcile opens one wherever that is true and closes any that have since been
 * filled, and "Raise a requisition" turns a gap into a draft request for headcount.
 */
export default function EstablishmentPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const { hasAnyPermission } = useAuth();
  // G-3.10 (2026-09-15): was hasAnyRole(['SuperAdmin', 'HR']). The reads on this page are not
  // role-gated, so TenantAdmin, Admin and the legacy HR User role saw both tables and had no way
  // to act on either — the inverse of the landing page's problem, from the same cause.
  const isHr = hasAnyPermission(['HR.Recruitment.Write', 'HR.Recruitment.Admin']);
  // Reconcile is the one action here on the Admin policy, and it is the ONLY writer of the
  // vacancy register anywhere in the solution (G-3.2). Drawing it for someone who cannot run it
  // is how G-3.1 presented: a button that 403s, and an empty register for ever.
  const canReconcile = hasAnyPermission(['HR.Recruitment.Admin']);

  const [onlyVacant, setOnlyVacant] = useState(true);
  const [includeClosed, setIncludeClosed] = useState(false);
  // Round 2b, R4a: filter by unit (Level → Unit), and start a budget from what the grid shows.
  const [unitId, setUnitId] = useState('');
  const [planning, setPlanning] = useState(false);
  const [reconciling, setReconciling] = useState(false);
  const [raiseFor, setRaiseFor] = useState<string | null>(null);
  /**
   * ⚠ Three writes the screen never had. Reconcile OPENS gaps and closes the ones the
   * establishment no longer implies; none of it covers the gap somebody decides not to fill, the
   * one that needs a note, or the one whose status is simply wrong. Until now a vacancy could only
   * be raised into a requisition or left alone for ever.
   */
  const [closing, setClosing] = useState<{ id: string; title: string } | null>(null);
  const [closeReason, setCloseReason] = useState('');
  const [noting, setNoting] = useState<{ id: string; title: string; notes: string } | null>(null);
  // `notes` added with G-3.8: closing through this override used to leave ClosedReason null, while
  // the dedicated close dialog insists on one. The server now asks for it either way.
  const [statusing, setStatusing] = useState<
    { id: string; title: string; status: string; notes: string } | null
  >(null);

  const stats = useQuery({
    queryKey: ['hr', 'position-vacancy-stats'],
    queryFn: () => positionVacancyService.getStats(),
  });

  const establishment = useQuery({
    queryKey: ['hr', 'establishment', onlyVacant, unitId],
    queryFn: () => positionVacancyService.getEstablishment(unitId || null, onlyVacant),
  });

  const vacancies = useQuery({
    queryKey: ['hr', 'position-vacancies', includeClosed],
    queryFn: () => positionVacancyService.getVacancies({ includeClosed }),
  });

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: ['hr', 'position-vacancy-stats'] });
    await queryClient.invalidateQueries({ queryKey: ['hr', 'establishment'] });
    await queryClient.invalidateQueries({ queryKey: ['hr', 'position-vacancies'] });
  };

  const reconcile = useMutation({
    mutationFn: () => positionVacancyService.reconcile(),
    onSuccess: async (r) => {
      await refresh();
      toast({
        title: 'Reconciled',
        description: `${r.scanned} positions scanned — ${r.opened} vacancies opened, ${r.closed} closed.`,
      });
    },
    onError: (e: any) =>
      toast({ title: 'Could not reconcile', description: e?.message, variant: 'destructive' }),
  });

  const raise = async () => {
    if (!raiseFor) return false;
    setReconciling(true);
    try {
      const result = await positionVacancyService.raiseRequisition(raiseFor, {});
      await refresh();
      toast({
        title: 'Requisition raised',
        description: `${result.requisitionNumber} saved as a draft.`,
      });
      router.push(`/hr/recruitment/requisitions/${result.requisitionId}`);
      return true;
    } catch (e: any) {
      toast({ title: 'Refused', description: e?.message, variant: 'destructive' });
      return false;
    } finally {
      setReconciling(false);
      setRaiseFor(null);
    }
  };

  const closeVacancy = useMutation({
    mutationFn: () => {
      if (!closing) throw new Error('Nothing to close.');
      return positionVacancyService.close(closing.id, closeReason.trim());
    },
    onSuccess: async () => {
      await refresh();
      toast({ title: 'Vacancy closed', description: 'It leaves the open list with the reason on record.' });
      setClosing(null);
      setCloseReason('');
    },
    onError: (e: any) =>
      toast({ title: 'Refused', description: e?.body?.message ?? e?.message, variant: 'destructive' }),
  });

  const saveNotes = useMutation({
    mutationFn: () => {
      if (!noting) throw new Error('Nothing to annotate.');
      return positionVacancyService.setNotes(noting.id, noting.notes.trim() || null);
    },
    onSuccess: async () => {
      await refresh();
      toast({ title: 'Note saved' });
      setNoting(null);
    },
    onError: (e: any) =>
      toast({ title: 'Refused', description: e?.body?.message ?? e?.message, variant: 'destructive' }),
  });

  const setStatus = useMutation({
    mutationFn: () => {
      if (!statusing) throw new Error('Nothing to set.');
      return positionVacancyService.setStatus(statusing.id, {
        newStatus: statusing.status,
        notes: statusing.notes.trim() || undefined,
      });
    },
    onSuccess: async () => {
      await refresh();
      toast({ title: 'Status set' });
      setStatusing(null);
    },
    onError: (e: any) =>
      toast({ title: 'Refused', description: e?.body?.message ?? e?.message, variant: 'destructive' }),
  });

  const s = stats.data;

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Establishment"
        description="Where headcount sits against the establishment, and the gaps that follow from it."
        backHref="/hr/recruitment"
        actions={
          isHr && (
            <div className="flex items-center gap-2">
              {/* The Admin-tier exception path, which used to carry this screen's exact name in
                  the other menu. Establishing a post there bypasses the manpower-budget chain,
                  so a gap seen here can be answered from there when no budget covers it. */}
              <Button
                variant="outline"
                onClick={() => router.push('/administration/hr/establishment')}
              >
                <ShieldCheck className="mr-2 h-4 w-4" />
                Manual Establishment
              </Button>
              {canReconcile && (
                <Button
                  variant="outline"
                  onClick={() => reconcile.mutate()}
                  disabled={reconcile.isPending}
                >
                  <RefreshCw className={`mr-2 h-4 w-4 ${reconcile.isPending ? 'animate-spin' : ''}`} />
                  Reconcile
                </Button>
              )}
              {/* Round 2b, R4a: the establishment is where a budget starts. */}
              <Button onClick={() => setPlanning(true)}>
                <Banknote className="mr-2 h-4 w-4" />
                Plan a budget
              </Button>
            </div>
          )
        }
      />

      <PlanBudgetFromEstablishmentDialog open={planning} onOpenChange={setPlanning} initialUnitId={unitId || null} />

      {s && (
        <MetricTiles
          tiles={[
            { label: 'Open gaps', value: s.totalOpen },
            { label: 'Anticipated', value: s.anticipated },
            {
              label: 'Requisition raised',
              value: s.requisitionRaised,
              hint: 'Already asked for',
            },
            {
              label: 'Positions affected',
              value: `${s.positionsWithVacancy} of ${s.totalPositions}`,
            },
          ]}
        />
      )}

      <Tabs defaultValue="gaps">
        <TabsList>
          <TabsTrigger value="gaps">Vacancies</TabsTrigger>
          <TabsTrigger value="establishment">Position establishment</TabsTrigger>
        </TabsList>

        <TabsContent value="gaps" className="space-y-4 pt-4">
          <div className="flex items-center gap-2">
            <Checkbox
              id="includeClosed"
              checked={includeClosed}
              onCheckedChange={(c) => setIncludeClosed(c === true)}
            />
            <Label htmlFor="includeClosed" className="font-normal">
              Include filled and closed
            </Label>
          </div>

          <Card>
            <CardContent className="p-0">
              {vacancies.isLoading ? (
                <div className="flex items-center justify-center py-16">
                  <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
                </div>
              ) : (vacancies.data?.length ?? 0) === 0 ? (
                <div className="py-10">
                  <EmptyState
                    icon={Building2}
                    title="No open gaps"
                    description="Every position is at establishment. Reconcile to re-check."
                  />
                </div>
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Position</TableHead>
                      <TableHead>Unit</TableHead>
                      <TableHead>Reason</TableHead>
                      <TableHead>Vacated by</TableHead>
                      <TableHead>Since</TableHead>
                      <TableHead>Classification</TableHead>
                      <TableHead>Status</TableHead>
                      {isHr && <TableHead className="w-40" />}
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {(vacancies.data ?? []).map((pv) => (
                      <TableRow key={pv.id}>
                        <TableCell className="font-medium">{pv.positionTitle}</TableCell>
                        <TableCell>{pv.organizationUnitName || '—'}</TableCell>
                        <TableCell>{humanizeEnum(pv.reason)}</TableCell>
                        <TableCell>{pv.vacatedByEmployeeName || '—'}</TableCell>
                        <TableCell>{formatDate(pv.vacatedDate)}</TableCell>
                        <TableCell className="text-sm text-muted-foreground">
                          {humanizeEnum(pv.classification)}
                        </TableCell>
                        <TableCell>
                          <StatusBadge status={pv.status} />
                        </TableCell>
                        {isHr && (
                          <TableCell>
                            {pv.staffRequisitionId ? (
                              <Button
                                variant="ghost"
                                size="sm"
                                onClick={() =>
                                  router.push(`/hr/recruitment/requisitions/${pv.staffRequisitionId}`)
                                }
                              >
                                View requisition
                              </Button>
                            ) : (
                              <Button variant="outline" size="sm" onClick={() => setRaiseFor(pv.id)}>
                                <FilePlus2 className="mr-2 h-4 w-4" /> Raise
                              </Button>
                            )}
                            <Button
                              variant="ghost"
                              size="sm"
                              title="Annotate this gap"
                              onClick={async () => {
                                // ⚠ The list read has no notes on it; only the by-id read does.
                                // Seeding from the row would open empty and save a blank over
                                // whatever was written.
                                try {
                                  const detail = await positionVacancyService.getVacancy(pv.id);
                                  setNoting({
                                    id: pv.id,
                                    title: pv.positionTitle,
                                    notes: detail.notes ?? '',
                                  });
                                } catch (e: any) {
                                  toast({
                                    title: 'Could not open the notes',
                                    description: e?.body?.message ?? e?.message,
                                    variant: 'destructive',
                                  });
                                }
                              }}
                            >
                              <MessageSquare className="h-4 w-4" />
                            </Button>
                            <Button
                              variant="ghost"
                              size="sm"
                              title="Set the status by hand"
                              onClick={() =>
                                setStatusing({
                                  id: pv.id,
                                  title: pv.positionTitle,
                                  status: pv.status,
                                  notes: '',
                                })
                              }
                            >
                              <RefreshCw className="h-4 w-4" />
                            </Button>
                            <Button
                              variant="ghost"
                              size="sm"
                              title="Close this gap"
                              onClick={() => {
                                setCloseReason('');
                                setClosing({ id: pv.id, title: pv.positionTitle });
                              }}
                            >
                              <XCircle className="h-4 w-4" />
                            </Button>
                          </TableCell>
                        )}
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="establishment" className="space-y-4 pt-4">
          <div className="grid gap-4 sm:grid-cols-[2fr_1fr] sm:items-end">
            <OrganizationUnitPicker
              value={unitId}
              onChange={(id) => setUnitId(id)}
              allowNone="Every unit"
              unitLabel="Unit"
              idPrefix="est-unit"
              showCode
            />
            <div className="flex items-center gap-2 pb-2">
              <Checkbox
                id="onlyVacant"
                checked={onlyVacant}
                onCheckedChange={(c) => setOnlyVacant(c === true)}
              />
              <Label htmlFor="onlyVacant" className="font-normal">
                Only positions below establishment
              </Label>
            </div>
          </div>

          <Card>
            <CardHeader className="pb-3">
              <CardTitle className="text-base">Expected against actual</CardTitle>
            </CardHeader>
            <CardContent className="p-0">
              {establishment.isLoading ? (
                <div className="flex items-center justify-center py-16">
                  <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
                </div>
              ) : (establishment.data?.length ?? 0) === 0 ? (
                <div className="py-10">
                  <EmptyState
                    title={onlyVacant ? 'Nothing below establishment' : 'No positions'}
                    description={
                      onlyVacant
                        ? 'Every established position is at or above its authorised headcount. A post nobody has established has no gap to show.'
                        : 'No active positions in this unit.'
                    }
                  />
                </div>
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Position</TableHead>
                      <TableHead>Unit</TableHead>
                      <TableHead className="text-right">Established</TableHead>
                      <TableHead className="text-right">Filled</TableHead>
                      <TableHead className="text-right">Gap</TableHead>
                      <TableHead>Source</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {/* ⚠ An unestablished post shows "not established", never a gap of 0 of 1:
                        its headcount is the column default and means nothing (R4a). Over-strength
                        posts are the ones refusing recruitment and movements right now. */}
                    {(establishment.data ?? []).map((p) => (
                      <TableRow key={p.positionId} className={p.isOverEstablishment ? 'bg-amber-50' : undefined}>
                        <TableCell className="font-medium">
                          {p.positionTitle}
                          {p.positionCode && <span className="ml-1 text-xs text-muted-foreground">{p.positionCode}</span>}
                        </TableCell>
                        <TableCell>{p.organizationUnitName || '—'}</TableCell>
                        <TableCell className="text-right tabular-nums">
                          {p.isEstablished ? p.expectedHeadcount : <span className="text-xs text-muted-foreground">not established</span>}
                        </TableCell>
                        <TableCell className={`text-right tabular-nums ${p.isOverEstablishment ? 'font-semibold text-amber-800' : ''}`}>{p.filledCount}</TableCell>
                        <TableCell className="text-right tabular-nums font-medium">
                          {p.gapKnown ? p.vacantCount : '—'}
                        </TableCell>
                        <TableCell className="text-xs text-muted-foreground">
                          {p.establishmentSourceBudgetNumber
                            ? `Budget ${p.establishmentSourceBudgetNumber}`
                            : p.isEstablished ? 'Set by HR' : '—'}
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

      <ConfirmationDialog
        open={raiseFor !== null}
        onOpenChange={(open) => !open && setRaiseFor(null)}
        title="Raise a requisition for this vacancy"
        description="A draft replacement requisition is created from the vacancy — the position, the outgoing employee and the reason are carried across. You can edit it before submitting."
        confirmText={reconciling ? 'Raising…' : 'Raise'}
        onConfirm={raise}
      />

      {/* Close a gap by hand. Reconcile cannot see a post the organisation has decided to leave
          unfilled, so without this it stays open for ever. */}
      <Dialog open={closing !== null} onOpenChange={(o) => !o && setClosing(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Close the gap for {closing?.title}</DialogTitle>
            <DialogDescription>
              For a post that will not be filled — a restructure, or a decision to leave it open. The
              reason is kept on the record, and reconcile will not reopen it.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-2">
            <Label htmlFor="closeReason">Reason</Label>
            <Textarea
              id="closeReason"
              rows={3}
              value={closeReason}
              onChange={(e) => setCloseReason(e.target.value)}
              placeholder="Why this gap is being closed rather than filled"
            />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setClosing(null)}>Cancel</Button>
            <Button
              variant="destructive"
              disabled={!closeReason.trim() || closeVacancy.isPending}
              onClick={() => closeVacancy.mutate()}
            >
              {closeVacancy.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Close the gap
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={noting !== null} onOpenChange={(o) => !o && setNoting(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Notes on {noting?.title}</DialogTitle>
            <DialogDescription>
              What anyone looking at this gap next should know.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-2">
            <Label htmlFor="vacancyNotes">Notes</Label>
            <Textarea
              id="vacancyNotes"
              rows={4}
              value={noting?.notes ?? ''}
              onChange={(e) => noting && setNoting({ ...noting, notes: e.target.value })}
            />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setNoting(null)}>Cancel</Button>
            <Button disabled={saveNotes.isPending} onClick={() => saveNotes.mutate()}>
              {saveNotes.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Save
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ⚠ The status is normally reconcile's to set. This is the override for when it is wrong:
          a gap filled outside the system, or one raised in error. */}
      <Dialog open={statusing !== null} onOpenChange={(o) => !o && setStatusing(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Set the status of {statusing?.title}</DialogTitle>
            <DialogDescription>
              Reconcile normally decides this. Setting it by hand is for when it is wrong.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label>Status</Label>
              <Select
                value={statusing?.status ?? ''}
                onValueChange={(v) => statusing && setStatusing({ ...statusing, status: v })}
              >
                <SelectTrigger><SelectValue placeholder="Choose a status" /></SelectTrigger>
                <SelectContent>
                  {/* G-3.7 (2026-09-15): 'RequisitionRaised' and 'Filled' used to be offered here
                      and the service refuses both — "Use 'Raise Requisition' or the hiring flow to
                      move a vacancy to that status." Picking either produced a toast reading
                      "Refused". Two of six options were always going to fail, and the only way to
                      find out was to click. They are system-driven statuses; the paths that write
                      them are elsewhere. */}
                  {['Anticipated', 'Open', 'UnderReview', 'Closed'].map((v) => (
                    <SelectItem key={v} value={v}>{humanizeEnum(v)}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label htmlFor="statusNotes">
                {statusing?.status === 'Closed' ? 'Why is this being closed?' : 'Note (optional)'}
              </Label>
              <Textarea
                id="statusNotes"
                value={statusing?.notes ?? ''}
                onChange={(e) => statusing && setStatusing({ ...statusing, notes: e.target.value })}
                placeholder={
                  statusing?.status === 'Closed'
                    ? 'e.g. post abolished in the restructure, or filled outside the system'
                    : 'Anything worth recording against this change'
                }
              />
              {statusing?.status === 'Closed' && (
                <p className="text-xs text-muted-foreground">
                  Closing ends the record, so it carries a reason — the same one the dedicated
                  Close action asks for.
                </p>
              )}
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setStatusing(null)}>Cancel</Button>
            <Button
              disabled={
                setStatus.isPending ||
                (statusing?.status === 'Closed' && !statusing.notes.trim())
              }
              onClick={() => setStatus.mutate()}
            >
              {setStatus.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Set
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
