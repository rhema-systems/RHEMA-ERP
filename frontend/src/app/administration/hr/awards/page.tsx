'use client';

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import Link from 'next/link';
import {
  AlertTriangle,
  Award,
  CalendarRange,
  CheckCircle2,
  Loader2,
  Medal,
  PlayCircle,
  Plus,
  Sparkles,
  Trash2,
  Users,
} from 'lucide-react';
import { toast } from 'sonner';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Switch } from '@/components/ui/switch';
import { NavCardGrid } from '@/components/hr/common/NavCardGrid';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Badge } from '@/components/ui/badge';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { MetricTiles } from '@/components/hr/common/MetricTiles';
import { awardsService } from '@/services/hr/awards.service';

const fmtMoney = (v?: number | null) =>
  v === null || v === undefined
    ? null
    : v.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });

/**
 * Award setup — the catalogue, the long-service ladder, and the sweep that grants against it.
 *
 * ⚠ Everything on this page is `HR.Awards.Admin`. Nominating and voting are not: those live on the
 * employee's own surface, behind no HR permission at all.
 */
export default function AwardsAdministrationPage() {
  const queryClient = useQueryClient();
  const [selectedTypeId, setSelectedTypeId] = useState<string>('');
  const [rungForm, setRungForm] = useState<null | {
    id?: string;
    years: number;
    name: string;
    monetaryAmount: string;
    leaveDaysBonus: string;
    benefits: string;
    isActive: boolean;
  }>(null);

  const { data: types, isLoading: loadingTypes } = useQuery({
    queryKey: ['award-types'],
    queryFn: () => awardsService.getTypes(),
  });

  // Long service is the only category with a ladder, so the picker offers only those — an
  // Employee-of-the-Month award has no milestones to configure and offering it would invite a
  // question the screen cannot answer.
  const longServiceTypes = (types ?? []).filter((t) => t.category === 'LongService' && t.isActive);
  const activeTypeId = selectedTypeId || longServiceTypes[0]?.id || '';

  const { data: ladder, isLoading: loadingLadder } = useQuery({
    queryKey: ['long-service-ladder', activeTypeId],
    queryFn: () => awardsService.getLadder(activeTypeId),
    enabled: Boolean(activeTypeId),
  });

  const { data: preview, isLoading: loadingPreview } = useQuery({
    queryKey: ['long-service-preview', activeTypeId],
    queryFn: () => awardsService.previewLongServiceSweep(activeTypeId),
    enabled: Boolean(activeTypeId),
  });

  const seed = useMutation({
    mutationFn: () => awardsService.seedLadder(activeTypeId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['long-service-ladder', activeTypeId] }),
  });

  const saveRung = useMutation({
    mutationFn: () => {
      if (!rungForm) throw new Error('Nothing to save.');
      const body = {
        years: Number(rungForm.years),
        name: rungForm.name.trim() || null,
        // ⚠ Blank stays NULL rather than becoming 0. A rung with no amount is a question TDC has
        // not answered; a zero would answer it for them.
        monetaryAmount: rungForm.monetaryAmount === '' ? null : Number(rungForm.monetaryAmount),
        leaveDaysBonus: rungForm.leaveDaysBonus === '' ? null : Number(rungForm.leaveDaysBonus),
        benefits: rungForm.benefits.trim() || null,
        isActive: rungForm.isActive,
      };
      return rungForm.id
        ? awardsService.updateMilestone(rungForm.id, { id: rungForm.id, ...body })
        : awardsService.createMilestone({ awardTypeId: activeTypeId, ...body });
    },
    onSuccess: () => {
      toast.success('Saved.');
      setRungForm(null);
      queryClient.invalidateQueries({ queryKey: ['long-service-ladder', activeTypeId] });
      queryClient.invalidateQueries({ queryKey: ['long-service-preview', activeTypeId] });
    },
    onError: (e: any) => toast.error(e?.body?.detail || e?.message || 'The rung was refused.'),
  });

  const removeRung = useMutation({
    mutationFn: (rungId: string) => awardsService.deleteMilestone(rungId),
    onSuccess: () => {
      toast.success('Removed. Its year is free for a replacement.');
      queryClient.invalidateQueries({ queryKey: ['long-service-ladder', activeTypeId] });
      queryClient.invalidateQueries({ queryKey: ['long-service-preview', activeTypeId] });
    },
    onError: (e: any) => toast.error(e?.body?.detail || e?.message || 'The removal was refused.'),
  });

  const runSweep = useMutation({
    mutationFn: () => awardsService.runLongServiceSweep(activeTypeId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['long-service-preview', activeTypeId] });
      queryClient.invalidateQueries({ queryKey: ['awards-conferred'] });
    },
  });

  const rungs = ladder ?? [];
  const unpriced = rungs.filter((m) => m.isActive && m.monetaryAmount === null).length;

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Award setup"
        description="The award catalogue, the long-service ladder, and the sweep that grants against it."
        backHref="/administration/hr"
      />

      <NavCardGrid
        items={[
          {
            title: 'Award catalogue',
            description: 'The awards themselves, how they are nominated and how a winner is chosen.',
            href: '/administration/hr/awards/types',
            icon: Award,
          },
          {
            title: 'Cycles',
            description: 'Nomination and voting windows, publishing, and generated candidates.',
            href: '/administration/hr/awards/cycles',
            icon: CalendarRange,
          },
          {
            title: 'Committees',
            description: 'Who scores nominations. Membership is the entitlement.',
            href: '/administration/hr/awards/committees',
            icon: Users,
          },
        ]}
      />

      {loadingTypes ? (
        <div className="flex items-center justify-center p-12">
          <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
        </div>
      ) : longServiceTypes.length === 0 ? (
        <EmptyState
          icon={Medal}
          title="No long-service award"
          description="Create an active award in the Long Service category before a ladder can be configured."
        />
      ) : (
        <>
          <Card>
            <CardContent className="flex flex-wrap items-center gap-3 p-4">
              <Award className="h-4 w-4 text-muted-foreground" />
              <span className="text-sm text-muted-foreground">Long-service award</span>
              <Select value={activeTypeId} onValueChange={setSelectedTypeId}>
                <SelectTrigger className="w-80">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {longServiceTypes.map((t) => (
                    <SelectItem key={t.id} value={t.id}>
                      {t.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </CardContent>
          </Card>

          {/* ── the ladder ──────────────────────────────────────────────────── */}
          <Card>
            <CardHeader className="flex flex-row items-center justify-between">
              <CardTitle className="flex items-center gap-2 text-base">
                <Medal className="h-4 w-4" />
                Long-service milestones
              </CardTitle>
              <div className="flex gap-2">
              <Button
                variant="outline"
                size="sm"
                disabled={!activeTypeId}
                onClick={() =>
                  setRungForm({
                    years: 0, name: '', monetaryAmount: '', leaveDaysBonus: '',
                    benefits: '', isActive: true,
                  })
                }
              >
                <Plus className="mr-2 h-4 w-4" />
                Add a rung
              </Button>
              <Button
                variant="outline"
                size="sm"
                disabled={seed.isPending || !activeTypeId}
                onClick={() => seed.mutate()}
              >
                {seed.isPending ? (
                  <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                ) : (
                  <Sparkles className="mr-2 h-4 w-4" />
                )}
                Seed the default ladder
              </Button>
              </div>
            </CardHeader>
            <CardContent className="space-y-4">
              {seed.data && (
                <div className="rounded-md border border-emerald-200 bg-emerald-50 p-3 text-sm dark:border-emerald-900 dark:bg-emerald-950/40">
                  {/* Seeding is additive and never repriced — reporting both lists is what makes a
                      second press legible rather than looking like a failed first one. */}
                  <p>
                    Added {seed.data.created.length === 0 ? 'nothing' : seed.data.created.join(', ')}
                    {seed.data.alreadyPresent.length > 0 &&
                      `; already present: ${seed.data.alreadyPresent.join(', ')}`}
                    . Source: {seed.data.source}.
                  </p>
                  {seed.data.companyPolicyYears.length > 0 && (
                    <p className="mt-1 text-xs text-muted-foreground">
                      The company-wide setting lists {seed.data.companyPolicyYears.join(', ')}. It is
                      offered, not applied — its default cannot be told apart from a value someone chose.
                    </p>
                  )}
                </div>
              )}

              {/* ⚠ An unpriced rung is an unanswered question, not an award worth nothing. */}
              {unpriced > 0 && (
                <div className="flex items-start gap-2 rounded-md border border-amber-200 bg-amber-50 p-3 text-sm dark:border-amber-900 dark:bg-amber-950/40">
                  <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0 text-amber-600" />
                  <p>
                    {unpriced} of {rungs.filter((m) => m.isActive).length} milestones carry no value.
                    Awards granted against them will record no amount — that is an unanswered
                    question rather than an award worth nothing.
                  </p>
                </div>
              )}

              {loadingLadder ? (
                <div className="flex items-center justify-center p-8">
                  <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
                </div>
              ) : rungs.length === 0 ? (
                <EmptyState
                  icon={Medal}
                  title="No milestones yet"
                  description="Seed the default ladder (10 / 15 / 20 / 25 / 30 years) and then set what each rung carries."
                />
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Years</TableHead>
                      <TableHead>Name</TableHead>
                      <TableHead className="text-right">Value</TableHead>
                      <TableHead className="text-right">Leave days</TableHead>
                      <TableHead>Benefits</TableHead>
                      <TableHead>Active</TableHead>
                      <TableHead />
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {rungs.map((m) => (
                      <TableRow key={m.id}>
                        <TableCell className="font-medium">{m.years}</TableCell>
                        <TableCell>{m.name ?? '—'}</TableCell>
                        <TableCell className="text-right">
                          {fmtMoney(m.monetaryAmount) ?? (
                            <span className="text-muted-foreground">not set</span>
                          )}
                        </TableCell>
                        <TableCell className="text-right">{m.leaveDaysBonus ?? '—'}</TableCell>
                        <TableCell>{m.benefits ?? '—'}</TableCell>
                        <TableCell>
                          {m.isActive ? (
                            <CheckCircle2 className="h-4 w-4 text-emerald-600" />
                          ) : (
                            <span className="text-muted-foreground">—</span>
                          )}
                        </TableCell>
                        <TableCell className="space-x-1 text-right">
                          <Button
                            size="sm"
                            variant="ghost"
                            onClick={() =>
                              setRungForm({
                                id: m.id,
                                years: m.years,
                                name: m.name ?? '',
                                monetaryAmount: m.monetaryAmount === null ? '' : String(m.monetaryAmount),
                                leaveDaysBonus: m.leaveDaysBonus === null ? '' : String(m.leaveDaysBonus),
                                benefits: m.benefits ?? '',
                                isActive: m.isActive,
                              })
                            }
                          >
                            Edit
                          </Button>
                          <Button
                            size="sm"
                            variant="ghost"
                            disabled={removeRung.isPending}
                            onClick={() => removeRung.mutate(m.id)}
                          >
                            <Trash2 className="h-4 w-4" />
                          </Button>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>

          {/* ── the sweep ───────────────────────────────────────────────────── */}
          <Card>
            <CardHeader className="flex flex-row items-center justify-between">
              <CardTitle className="flex items-center gap-2 text-base">
                <Users className="h-4 w-4" />
                Who has reached a milestone
              </CardTitle>
              <Button
                size="sm"
                disabled={runSweep.isPending || !preview || preview.qualified.length === 0}
                onClick={() => runSweep.mutate()}
              >
                {runSweep.isPending ? (
                  <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                ) : (
                  <PlayCircle className="mr-2 h-4 w-4" />
                )}
                Grant {preview?.qualified.length ?? 0} award
                {preview?.qualified.length === 1 ? '' : 's'}
              </Button>
            </CardHeader>
            <CardContent className="space-y-4">
              {loadingPreview ? (
                <div className="flex items-center justify-center p-8">
                  <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
                </div>
              ) : !preview ? null : (
                <>
                  <MetricTiles
                    tiles={[
                      {
                        label: 'Eligible now',
                        value: preview.qualified.length,
                        icon: Medal,
                        tone: preview.qualified.length > 0 ? 'success' : 'default',
                      },
                      {
                        label: 'Exempt',
                        value: preview.disqualified.length,
                        hint: preview.disciplinaryCheckApplied
                          ? 'disciplinary rule applied'
                          : 'rule switched off for this award',
                        icon: AlertTriangle,
                      },
                      {
                        label: 'Employees measured',
                        value: preview.employeesConsidered - preview.withoutEmploymentDate,
                        hint: `of ${preview.employeesConsidered} serving`,
                        icon: Users,
                      },
                      {
                        // ⚠ The number that stops this screen lying. 3,476 of 5,579 employees have
                        // no employment date on the live tenant; without this beside it, "1
                        // eligible" reads as a statement about staff when it is a statement about
                        // the employee records.
                        label: 'Service unknown',
                        value: preview.withoutEmploymentDate,
                        hint: 'no employment date on record',
                        icon: AlertTriangle,
                        tone: preview.withoutEmploymentDate > 0 ? 'warning' : 'default',
                      },
                    ]}
                  />

                  {runSweep.data && (
                    <div className="rounded-md border border-emerald-200 bg-emerald-50 p-3 text-sm dark:border-emerald-900 dark:bg-emerald-950/40">
                      Granted {runSweep.data.awardsCreated} award
                      {runSweep.data.awardsCreated === 1 ? '' : 's'}.
                    </div>
                  )}

                  {preview.qualified.length === 0 && preview.disqualified.length === 0 ? (
                    <EmptyState
                      icon={Medal}
                      title="Nobody has reached a milestone"
                      description={
                        preview.withoutEmploymentDate > 0
                          ? `Note that ${preview.withoutEmploymentDate} employees have no employment date on record and could not be assessed at all.`
                          : 'Everyone measured is short of the lowest rung, or already holds the highest one their service has reached.'
                      }
                    />
                  ) : (
                    <Table>
                      <TableHeader>
                        <TableRow>
                          <TableHead>Employee</TableHead>
                          <TableHead className="text-right">Years served</TableHead>
                          <TableHead className="text-right">Milestone</TableHead>
                          <TableHead>Standing</TableHead>
                        </TableRow>
                      </TableHeader>
                      <TableBody>
                        {[...preview.qualified, ...preview.disqualified].map((c) => (
                          <TableRow key={`${c.employeeId}-${c.milestoneYears}`}>
                            <TableCell>
                              {c.employeeName}
                              <span className="ml-2 text-xs text-muted-foreground">
                                {c.employeeNumber}
                              </span>
                            </TableCell>
                            <TableCell className="text-right">{c.yearsOfService}</TableCell>
                            {/* ⚠ The rung reached, which is NOT the years served — somebody at 22
                                years reaches the 20-year milestone. */}
                            <TableCell className="text-right">{c.milestoneYears} years</TableCell>
                            <TableCell>
                              {c.reason ? (
                                <Badge
                                  variant="secondary"
                                  className="bg-rose-100 text-rose-800 dark:bg-rose-900/40 dark:text-rose-200"
                                  title={c.reason}
                                >
                                  Exempt
                                </Badge>
                              ) : (
                                <Badge
                                  variant="secondary"
                                  className="bg-emerald-100 text-emerald-800 dark:bg-emerald-900/40 dark:text-emerald-200"
                                >
                                  Eligible
                                </Badge>
                              )}
                            </TableCell>
                          </TableRow>
                        ))}
                      </TableBody>
                    </Table>
                  )}
                </>
              )}
            </CardContent>
          </Card>
        </>
      )}

      <Dialog open={Boolean(rungForm)} onOpenChange={(o) => !o && setRungForm(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{rungForm?.id ? 'Edit rung' : 'Add a rung'}</DialogTitle>
            <DialogDescription>
              One rung per number of years. Leaving a value blank means &quot;not decided&quot;, not zero.
            </DialogDescription>
          </DialogHeader>
          {rungForm && (
            <div className="space-y-4">
              <div className="grid gap-4 sm:grid-cols-2">
                <div className="space-y-2">
                  <Label htmlFor="years">Years of service</Label>
                  <Input
                    id="years"
                    type="number"
                    min={1}
                    max={100}
                    value={rungForm.years || ''}
                    onChange={(e) => setRungForm({ ...rungForm, years: Number(e.target.value) })}
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="rungName">Name</Label>
                  <Input
                    id="rungName"
                    value={rungForm.name}
                    onChange={(e) => setRungForm({ ...rungForm, name: e.target.value })}
                    placeholder="Decade of Service"
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="rungAmount">Value</Label>
                  <Input
                    id="rungAmount"
                    type="number"
                    min={0}
                    value={rungForm.monetaryAmount}
                    onChange={(e) => setRungForm({ ...rungForm, monetaryAmount: e.target.value })}
                    placeholder="Not decided"
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="rungLeave">Leave days</Label>
                  <Input
                    id="rungLeave"
                    type="number"
                    min={0}
                    value={rungForm.leaveDaysBonus}
                    onChange={(e) => setRungForm({ ...rungForm, leaveDaysBonus: e.target.value })}
                    placeholder="Not decided"
                  />
                </div>
                <div className="space-y-2 sm:col-span-2">
                  <Label htmlFor="rungBenefits">Other benefits</Label>
                  <Input
                    id="rungBenefits"
                    value={rungForm.benefits}
                    onChange={(e) => setRungForm({ ...rungForm, benefits: e.target.value })}
                  />
                </div>
              </div>
              <label className="flex items-center gap-2 text-sm">
                <Switch
                  checked={rungForm.isActive}
                  onCheckedChange={(v) => setRungForm({ ...rungForm, isActive: v })}
                />
                Active — an inactive rung is not swept for
              </label>
              <div className="flex justify-end gap-2">
                <Button variant="outline" onClick={() => setRungForm(null)}>Cancel</Button>
                <Button
                  disabled={!rungForm.years || rungForm.years < 1 || saveRung.isPending}
                  onClick={() => saveRung.mutate()}
                >
                  {saveRung.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                  Save
                </Button>
              </div>
            </div>
          )}
        </DialogContent>
      </Dialog>
    </div>
  );
}
