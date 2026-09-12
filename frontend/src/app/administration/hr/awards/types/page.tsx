'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Award, Info, Loader2, Plus, Search, Trash2 } from 'lucide-react';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Badge } from '@/components/ui/badge';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Switch } from '@/components/ui/switch';
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
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { awardsService } from '@/services/hr/awards.service';
import type {
  AwardCategory,
  AwardFrequency,
  AwardNominationSource,
  AwardWinnerDecision,
  UpsertAwardType,
} from '@/types/hr/awards';

const CATEGORIES: AwardCategory[] = [
  'Performance', 'LongService', 'Innovation', 'CustomerService',
  'Safety', 'TeamPlayer', 'Leadership', 'SpecialRecognition', 'Other',
];
const FREQUENCIES: AwardFrequency[] = ['Monthly', 'Quarterly', 'Annually', 'AdHoc'];
const SOURCES: AwardNominationSource[] = ['OpenNomination', 'ManagementDirect', 'PerformanceTriggered'];
const DECISIONS: AwardWinnerDecision[] = ['StaffVote', 'CommitteeScore', 'ManagementDecision'];

const SOURCE_LABEL: Record<AwardNominationSource, string> = {
  OpenNomination: 'Anyone may nominate',
  ManagementDirect: 'Management picks directly',
  PerformanceTriggered: 'Derived from performance',
};

const DECISION_LABEL: Record<AwardWinnerDecision, string> = {
  StaffVote: 'Staff vote',
  CommitteeScore: 'Committee score',
  ManagementDecision: 'Management decides',
};

const blank = (): UpsertAwardType => ({
  code: '', name: '', description: '',
  category: 'Performance', frequency: 'Annually', isTeamAward: false,
  nominationSource: 'OpenNomination', winnerDecision: 'CommitteeScore',
  allowSelfNomination: false, disqualifyOnDisciplinaryRecord: false,
  disqualifyingDisciplineMonths: null,
  minServiceYears: null, maxServiceYears: null, minAge: null, maxAge: null,
  maxAwardsPerPeriod: null, maxAwardsPerEmployee: null,
  hasMonetaryReward: false, minMonetaryAmount: null, maxMonetaryAmount: null,
  hasCertificate: true, hasTrophy: false, leaveDaysBonus: null, hasLevels: false,
  requiresFormalReview: false, minRequiredReviewers: null,
  minPerformanceScore: null, minGoalsAchieved: null,
  notes: null, isActive: true,
});

const num = (v: string) => (v === '' ? null : Number(v));

/**
 * The award catalogue.
 *
 * ⚠ **Two independent axes, not one "method" (decision D-3).** Where candidates come from
 * (`nominationSource`) and how the winner is chosen (`winnerDecision`) are separate questions:
 * management can pick the shortlist and staff still vote on it. Collapsing them into a single enum
 * was the shape this area started with and could not express what TDC described.
 *
 * ⚠ **Self-nomination is off by default and set per award.** The enterprise norm is peer or manager
 * nomination; self-nomination suits innovation and improvement awards rather than behavioural ones.
 *
 * ⚠ **The disciplinary exemption is off by default too.** TDC stated that rule under Long Service
 * only, so turning it on elsewhere is a policy choice HR makes deliberately — not a default.
 */
export default function AwardTypesPage() {
  const queryClient = useQueryClient();
  const [search, setSearch] = useState('');
  const [editing, setEditing] = useState<{ id?: string; form: UpsertAwardType } | null>(null);
  const [removing, setRemoving] = useState<{ id: string; name: string } | null>(null);

  const { data: types, isLoading } = useQuery({
    queryKey: ['award-types'],
    queryFn: () => awardsService.getTypes(),
  });

  const rows = (types ?? []).filter((t) => {
    const term = search.trim().toLowerCase();
    if (!term) return true;
    return t.name.toLowerCase().includes(term) || t.code.toLowerCase().includes(term);
  });

  const save = useMutation({
    mutationFn: async () => {
      if (!editing) throw new Error('Nothing to save.');
      return editing.id
        ? awardsService.updateType(editing.id, { ...editing.form, id: editing.id })
        : awardsService.createType(editing.form);
    },
    onSuccess: () => {
      toast.success('Saved.');
      setEditing(null);
      queryClient.invalidateQueries({ queryKey: ['award-types'] });
    },
    onError: (e: any) => toast.error(e?.body?.detail || e?.message || 'The change was refused.'),
  });

  /**
   * ⚠ Asked of the server before offering the delete, not guessed from `awardCount`.
   * "In use" is more than conferred awards - nominations, cycles, levels, targets and budgets all
   * reference a type, and a screen that only counted awards would offer a delete the API refuses.
   */
  const remove = useMutation({
    mutationFn: async () => {
      if (!removing) throw new Error('Nothing selected.');
      const inUse = await awardsService.isTypeInUse(removing.id);
      if (inUse) throw new Error('This award is referenced by other records and cannot be removed.');
      return awardsService.deleteType(removing.id);
    },
    onSuccess: () => {
      toast.success('Removed.');
      setRemoving(null);
      queryClient.invalidateQueries({ queryKey: ['award-types'] });
    },
    onError: (e: any) =>
      toast.error(e?.body?.detail || e?.message || 'The removal was refused.'),
  });

  const openEdit = async (id: string) => {
    const full = await awardsService.getType(id);
    setEditing({ id, form: { ...(full as unknown as UpsertAwardType) } });
  };

  const f = editing?.form;
  const set = (patch: Partial<UpsertAwardType>) =>
    setEditing((e) => (e ? { ...e, form: { ...e.form, ...patch } } : e));

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Award catalogue"
        description="The awards themselves — how they are nominated, how a winner is chosen, and who qualifies."
        backHref="/administration/hr/awards"
        actions={
          <Button onClick={() => setEditing({ form: blank() })}>
            <Plus className="mr-2 h-4 w-4" />
            New award
          </Button>
        }
      />

      <Card>
        <CardContent className="p-4">
          <div className="relative">
            <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
            <Input
              className="pl-9"
              placeholder="Search by name or code"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardContent className="p-0">
          {isLoading ? (
            <div className="flex items-center justify-center p-12">
              <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
            </div>
          ) : rows.length === 0 ? (
            <EmptyState icon={Award} title="No awards" description="Create the first award to begin." />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Code</TableHead>
                  <TableHead>Name</TableHead>
                  <TableHead>Category</TableHead>
                  <TableHead>Nominated by</TableHead>
                  <TableHead>Winner decided by</TableHead>
                  <TableHead className="text-right">Conferred</TableHead>
                  <TableHead>Active</TableHead>
                  <TableHead />
                </TableRow>
              </TableHeader>
              <TableBody>
                {rows.map((t) => (
                  <TableRow key={t.id}>
                    <TableCell className="font-medium">{t.code}</TableCell>
                    <TableCell>
                      <Link className="underline" href={`/administration/hr/awards/types/${t.id}`}>
                        {t.name}
                      </Link>
                    </TableCell>
                    <TableCell>{t.categoryName}</TableCell>
                    <TableCell>{t.nominationSourceName}</TableCell>
                    <TableCell>{t.winnerDecisionName}</TableCell>
                    <TableCell className="text-right">{t.awardCount}</TableCell>
                    <TableCell>
                      {t.isActive ? (
                        <Badge variant="secondary">Active</Badge>
                      ) : (
                        <span className="text-muted-foreground">—</span>
                      )}
                    </TableCell>
                    <TableCell className="text-right">
                      <Button size="sm" variant="ghost" onClick={() => openEdit(t.id)}>
                        Edit
                      </Button>
                      <Button
                        size="sm"
                        variant="ghost"
                        onClick={() => setRemoving({ id: t.id, name: t.name })}
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

      <ConfirmationDialog
        open={Boolean(removing)}
        onOpenChange={(o) => !o && setRemoving(null)}
        title={`Remove ${removing?.name}?`}
        description="It is flagged as removed rather than erased, so anything already awarded under it still explains itself. If other records reference it, the removal will be refused."
        variant="destructive"
        confirmText="Remove"
        isLoading={remove.isPending}
        onConfirm={async () => {
          try { await remove.mutateAsync(); } catch { return false; }
        }}
      />

      <Dialog open={Boolean(editing)} onOpenChange={(o) => !o && setEditing(null)}>
        <DialogContent className="max-h-[90vh] max-w-3xl overflow-y-auto">
          <DialogHeader>
            <DialogTitle>{editing?.id ? 'Edit award' : 'New award'}</DialogTitle>
            <DialogDescription>
              How this award is nominated and decided are two separate questions.
            </DialogDescription>
          </DialogHeader>

          {f && (
            <div className="space-y-6">
              <div className="grid gap-4 sm:grid-cols-2">
                <div className="space-y-2">
                  <Label htmlFor="code">Code</Label>
                  <Input id="code" value={f.code} onChange={(e) => set({ code: e.target.value })} />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="name">Name</Label>
                  <Input id="name" value={f.name} onChange={(e) => set({ name: e.target.value })} />
                </div>
                <div className="space-y-2 sm:col-span-2">
                  <Label htmlFor="description">Description</Label>
                  <Textarea
                    id="description"
                    rows={2}
                    value={f.description}
                    onChange={(e) => set({ description: e.target.value })}
                  />
                </div>
                <div className="space-y-2">
                  <Label>Category</Label>
                  <Select value={f.category} onValueChange={(v) => set({ category: v as AwardCategory })}>
                    <SelectTrigger><SelectValue /></SelectTrigger>
                    <SelectContent>
                      {CATEGORIES.map((c) => <SelectItem key={c} value={c}>{c}</SelectItem>)}
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-2">
                  <Label>Frequency</Label>
                  <Select value={f.frequency} onValueChange={(v) => set({ frequency: v as AwardFrequency })}>
                    <SelectTrigger><SelectValue /></SelectTrigger>
                    <SelectContent>
                      {FREQUENCIES.map((c) => <SelectItem key={c} value={c}>{c}</SelectItem>)}
                    </SelectContent>
                  </Select>
                </div>
              </div>

              {/* ── D-3: the two axes ─────────────────────────────────────── */}
              <div className="space-y-4 rounded-md border p-4">
                <p className="text-sm font-medium">How it is decided</p>
                <Alert>
                  <Info className="h-4 w-4" />
                  <AlertDescription>
                    These are independent. Management can put the shortlist forward and staff can
                    still vote on it.
                  </AlertDescription>
                </Alert>
                <div className="grid gap-4 sm:grid-cols-2">
                  <div className="space-y-2">
                    <Label>Candidates come from</Label>
                    <Select
                      value={f.nominationSource}
                      onValueChange={(v) => set({ nominationSource: v as AwardNominationSource })}
                    >
                      <SelectTrigger><SelectValue /></SelectTrigger>
                      <SelectContent>
                        {SOURCES.map((c) => (
                          <SelectItem key={c} value={c}>{SOURCE_LABEL[c]}</SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </div>
                  <div className="space-y-2">
                    <Label>Winner decided by</Label>
                    <Select
                      value={f.winnerDecision}
                      onValueChange={(v) => set({ winnerDecision: v as AwardWinnerDecision })}
                    >
                      <SelectTrigger><SelectValue /></SelectTrigger>
                      <SelectContent>
                        {DECISIONS.map((c) => (
                          <SelectItem key={c} value={c}>{DECISION_LABEL[c]}</SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                    {f.winnerDecision === 'StaffVote' && (
                      <p className="text-xs text-muted-foreground">
                        A voted award needs a voting window on every cycle; other awards must not
                        have one.
                      </p>
                    )}
                  </div>
                </div>

                <div className="grid gap-3 sm:grid-cols-2">
                  <label className="flex items-center gap-2 text-sm">
                    <Switch
                      checked={f.isTeamAward}
                      onCheckedChange={(v) => set({ isTeamAward: v })}
                    />
                    Team award
                  </label>
                  <label className="flex items-center gap-2 text-sm">
                    <Switch
                      checked={f.allowSelfNomination}
                      onCheckedChange={(v) => set({ allowSelfNomination: v })}
                    />
                    Allow self-nomination
                  </label>
                  <label className="flex items-center gap-2 text-sm">
                    <Switch
                      checked={f.requiresFormalReview}
                      onCheckedChange={(v) => set({ requiresFormalReview: v })}
                    />
                    Requires a formal committee review
                  </label>
                  {f.requiresFormalReview && (
                    <div className="space-y-2">
                      <Label htmlFor="minReviewers">Minimum reviewers</Label>
                      <Input
                        id="minReviewers"
                        type="number"
                        min={1}
                        value={f.minRequiredReviewers ?? ''}
                        onChange={(e) => set({ minRequiredReviewers: num(e.target.value) })}
                      />
                    </div>
                  )}
                </div>
              </div>

              {/* ── eligibility ───────────────────────────────────────────── */}
              <div className="space-y-4 rounded-md border p-4">
                <p className="text-sm font-medium">Who qualifies</p>
                <div className="grid gap-4 sm:grid-cols-4">
                  <div className="space-y-2">
                    <Label htmlFor="minServiceYears">Min service (yrs)</Label>
                    <Input id="minServiceYears" type="number" min={0} value={f.minServiceYears ?? ''}
                      onChange={(e) => set({ minServiceYears: num(e.target.value) })} />
                  </div>
                  <div className="space-y-2">
                    <Label htmlFor="maxServiceYears">Max service (yrs)</Label>
                    <Input id="maxServiceYears" type="number" min={0} value={f.maxServiceYears ?? ''}
                      onChange={(e) => set({ maxServiceYears: num(e.target.value) })} />
                  </div>
                  <div className="space-y-2">
                    <Label htmlFor="minAge">Min age</Label>
                    <Input id="minAge" type="number" min={0} value={f.minAge ?? ''}
                      onChange={(e) => set({ minAge: num(e.target.value) })} />
                  </div>
                  <div className="space-y-2">
                    <Label htmlFor="maxAge">Max age</Label>
                    <Input id="maxAge" type="number" min={0} value={f.maxAge ?? ''}
                      onChange={(e) => set({ maxAge: num(e.target.value) })} />
                  </div>
                  <div className="space-y-2">
                    <Label htmlFor="maxPerEmployee">Max per employee</Label>
                    <Input id="maxPerEmployee" type="number" min={0} value={f.maxAwardsPerEmployee ?? ''}
                      onChange={(e) => set({ maxAwardsPerEmployee: num(e.target.value) })} />
                  </div>
                  <div className="space-y-2">
                    <Label htmlFor="maxPerPeriod">Max per period</Label>
                    <Input id="maxPerPeriod" type="number" min={0} value={f.maxAwardsPerPeriod ?? ''}
                      onChange={(e) => set({ maxAwardsPerPeriod: num(e.target.value) })} />
                  </div>
                </div>

                <label className="flex items-center gap-2 text-sm">
                  <Switch
                    checked={f.disqualifyOnDisciplinaryRecord}
                    onCheckedChange={(v) => set({ disqualifyOnDisciplinaryRecord: v })}
                  />
                  A disciplinary record exempts an employee
                </label>
                {f.disqualifyOnDisciplinaryRecord && (
                  <div className="space-y-2">
                    <Label htmlFor="discMonths">Look back (months)</Label>
                    <Input
                      id="discMonths"
                      type="number"
                      min={1}
                      value={f.disqualifyingDisciplineMonths ?? ''}
                      onChange={(e) => set({ disqualifyingDisciplineMonths: num(e.target.value) })}
                      placeholder="Blank = the whole service period"
                    />
                    {/* Null is the rule as TDC wrote it — "any negative records", no horizon. A
                        value relaxes it, which is why blank is the strict default rather than the
                        lenient one. */}
                    <p className="text-xs text-muted-foreground">
                      Leave blank to count any record ever. A value <em>relaxes</em> the rule.
                    </p>
                  </div>
                )}
              </div>

              {/* ── what it carries ───────────────────────────────────────── */}
              <div className="space-y-4 rounded-md border p-4">
                <p className="text-sm font-medium">What it carries</p>
                <div className="grid gap-3 sm:grid-cols-2">
                  <label className="flex items-center gap-2 text-sm">
                    <Switch checked={f.hasCertificate} onCheckedChange={(v) => set({ hasCertificate: v })} />
                    Certificate
                  </label>
                  <label className="flex items-center gap-2 text-sm">
                    <Switch checked={f.hasTrophy} onCheckedChange={(v) => set({ hasTrophy: v })} />
                    Trophy
                  </label>
                  <label className="flex items-center gap-2 text-sm">
                    <Switch checked={f.hasLevels} onCheckedChange={(v) => set({ hasLevels: v })} />
                    Has levels (Gold / Silver / Bronze)
                  </label>
                  <label className="flex items-center gap-2 text-sm">
                    <Switch
                      checked={f.hasMonetaryReward}
                      onCheckedChange={(v) => set({ hasMonetaryReward: v })}
                    />
                    Carries money
                  </label>
                  {f.hasMonetaryReward && (
                    <>
                      <div className="space-y-2">
                        <Label htmlFor="minAmount">Minimum amount</Label>
                        <Input id="minAmount" type="number" min={0} value={f.minMonetaryAmount ?? ''}
                          onChange={(e) => set({ minMonetaryAmount: num(e.target.value) })} />
                      </div>
                      <div className="space-y-2">
                        <Label htmlFor="maxAmount">Maximum amount</Label>
                        <Input id="maxAmount" type="number" min={0} value={f.maxMonetaryAmount ?? ''}
                          onChange={(e) => set({ maxMonetaryAmount: num(e.target.value) })} />
                      </div>
                    </>
                  )}
                </div>
                <label className="flex items-center gap-2 text-sm">
                  <Switch checked={f.isActive} onCheckedChange={(v) => set({ isActive: v })} />
                  Active
                </label>
              </div>

              <div className="flex justify-end gap-2">
                <Button variant="outline" onClick={() => setEditing(null)}>Cancel</Button>
                <Button
                  disabled={!f.code.trim() || !f.name.trim() || save.isPending}
                  onClick={() => save.mutate()}
                >
                  {save.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
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
