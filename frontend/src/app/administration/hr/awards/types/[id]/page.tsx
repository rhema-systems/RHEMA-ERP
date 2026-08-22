'use client';

import { useState } from 'react';
import { useParams } from 'next/navigation';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Banknote, Info, Layers, Loader2, Plus, Target, Trash2 } from 'lucide-react';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Badge } from '@/components/ui/badge';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Switch } from '@/components/ui/switch';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
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
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import { awardsService } from '@/services/hr/awards.service';
import { employeePositionService } from '@/services/hr/employee-position.service';
import { organizationUnitService } from '@/services/hr/organization-unit.service';
import { staffLevelService } from '@/services/hr/staff-level.service';

const fmtMoney = (v?: number | null) =>
  v === null || v === undefined ? '—' : v.toLocaleString(undefined, { minimumFractionDigits: 2 });

/**
 * What a scope row points at.
 *
 * ⚠⚠ **`AwardTargetType` and `AwardScope` hold the same four members in OPPOSITE order.**
 *
 * | value | `AwardTargetType` (this DTO field) | `AwardScope` (the route parameter) |
 * |---|---|---|
 * | 1 | OrganizationUnit | Employee |
 * | 2 | Position | Position |
 * | 3 | StaffLevel | StaffLevel |
 * | 4 | Employee | OrganizationUnit |
 *
 * The middle two coincide, so a mix-up is **right half the time** — which is the worst possible
 * shape for a bug: it survives casual testing and scopes an award to the wrong kind of thing at the
 * two ends. These constants exist so nobody writes the number inline again. `targetType` on
 * `CreateAwardTypeTargetDto` is `AwardTargetType`; the `targets/scope/{scope}` route takes the
 * other one.
 */
const TARGET_TYPE = {
  OrganizationUnit: 1,
  Position: 2,
  StaffLevel: 3,
  Employee: 4,
} as const;

type TargetKind = keyof typeof TARGET_TYPE;

const TARGET_KIND_LABEL: Record<TargetKind, string> = {
  Employee: 'A named employee',
  Position: 'Everyone in a position',
  StaffLevel: 'Everyone at a staff level',
  OrganizationUnit: 'Everyone in an organisation unit',
};

/**
 * One award's levels, scoping and budgets.
 *
 * ⚠ **Targets answer two different questions**, and the `purpose` is what separates them: who may
 * WIN the award (Eligibility) and who may VOTE in it (Electorate). An award with no electorate
 * target is voted on by *everyone* — so an empty electorate list is not an empty electorate, and the
 * screen says so rather than leaving it to be discovered.
 *
 * ⚠ **A budget is per year, and one per year.** Conferring reserves against it and payment spends;
 * an award whose year has no budget row simply draws against nothing rather than being refused.
 */
export default function AwardTypeDetailPage() {
  const { id } = useParams<{ id: string }>();
  const queryClient = useQueryClient();

  const [levelForm, setLevelForm] = useState<null | {
    id?: string;
    code: string; name: string; description: string; rank: number;
    monetaryAmount: string; isActive: boolean;
  }>(null);
  const [targetForm, setTargetForm] = useState<null | {
    kind: TargetKind; targetId: string; isExclusion: boolean; purpose: number; reason: string;
  }>(null);
  const [budgetForm, setBudgetForm] = useState<null | {
    id?: string; year: number; budgetAmount: string;
  }>(null);
  const [removing, setRemoving] = useState<null | {
    kind: 'level' | 'target' | 'budget'; id: string; label: string;
  }>(null);

  const { data: type, isLoading } = useQuery({
    queryKey: ['award-type', id],
    queryFn: () => awardsService.getType(id),
  });

  const { data: levels } = useQuery({
    queryKey: ['award-levels', id],
    queryFn: () => awardsService.getLevels(id),
  });

  const { data: targets } = useQuery({
    queryKey: ['award-targets', id],
    queryFn: () => awardsService.getTargets(id),
  });

  const { data: budgets } = useQuery({
    queryKey: ['award-budgets', id],
    queryFn: () => awardsService.getBudgets(id),
  });

  const refresh = (key: string) => queryClient.invalidateQueries({ queryKey: [key, id] });

  // Each scope kind draws its options from a different place. They are fetched only for the kind
  // the user has actually picked — loading all four to fill one dropdown would be three wasted
  // round trips on every open.
  const kind = targetForm?.kind;

  const { data: positions } = useQuery({
    queryKey: ['award-scope-positions'],
    queryFn: () => employeePositionService.getActive(),
    enabled: kind === 'Position',
  });

  const { data: staffLevels } = useQuery({
    queryKey: ['award-scope-staff-levels'],
    queryFn: () => staffLevelService.getActive(),
    enabled: kind === 'StaffLevel',
  });

  const { data: orgUnits } = useQuery({
    queryKey: ['award-scope-org-units'],
    queryFn: () => organizationUnitService.getAll(),
    enabled: kind === 'OrganizationUnit',
  });

  const addLevel = useMutation({
    mutationFn: () => {
      if (!levelForm) throw new Error('No level to add.');
      const body = {
        awardTypeId: id,
        code: levelForm.code.trim(),
        name: levelForm.name.trim(),
        description: levelForm.description.trim() || null,
        rank: Number(levelForm.rank),
        monetaryAmount: levelForm.monetaryAmount === '' ? null : Number(levelForm.monetaryAmount),
        isActive: levelForm.isActive,
      };
      return levelForm.id
        ? awardsService.updateLevel(levelForm.id, { ...body, id: levelForm.id })
        : awardsService.createLevel(body);
    },
    onSuccess: () => { toast.success('Saved.'); setLevelForm(null); refresh('award-levels'); },
    onError: (e: any) => toast.error(e?.body?.detail || e?.message || 'The level was refused.'),
  });

  const addTarget = useMutation({
    mutationFn: () => {
      if (!targetForm) throw new Error('No scope to add.');
      return awardsService.createTarget({
        awardTypeId: id,
        targetType: TARGET_TYPE[targetForm.kind],
        targetId: targetForm.targetId,
        isExclusion: targetForm.isExclusion,
        purpose: targetForm.purpose,
        reason: targetForm.reason.trim() || null,
      });
    },
    onSuccess: () => { toast.success('Scope added.'); setTargetForm(null); refresh('award-targets'); },
    onError: (e: any) => toast.error(e?.body?.detail || e?.message || 'The scope was refused.'),
  });

  const addBudget = useMutation({
    mutationFn: () => {
      if (!budgetForm) throw new Error('No budget to add.');
      const body = {
        awardTypeId: id,
        year: Number(budgetForm.year),
        budgetAmount: Number(budgetForm.budgetAmount),
      };
      return budgetForm.id
        ? awardsService.updateBudget(budgetForm.id, { ...body, id: budgetForm.id })
        : awardsService.createBudget(body);
    },
    onSuccess: () => { toast.success('Saved.'); setBudgetForm(null); refresh('award-budgets'); },
    onError: (e: any) => toast.error(e?.body?.detail || e?.message || 'The budget was refused.'),
  });

  const remove = useMutation({
    mutationFn: () => {
      if (!removing) throw new Error('Nothing selected.');
      if (removing.kind === 'level') return awardsService.deleteLevel(removing.id);
      if (removing.kind === 'target') return awardsService.deleteTarget(removing.id);
      return awardsService.deleteBudget(removing.id);
    },
    onSuccess: () => {
      toast.success('Removed.');
      const key = removing?.kind === 'level' ? 'award-levels'
        : removing?.kind === 'target' ? 'award-targets' : 'award-budgets';
      setRemoving(null);
      refresh(key);
    },
    onError: (e: any) => toast.error(e?.body?.detail || e?.message || 'The removal was refused.'),
  });

  if (isLoading || !type) {
    return (
      <div className="flex items-center justify-center p-12">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  const eligibilityTargets = (targets ?? []).filter((t) => t.purpose !== 2);
  const electorateTargets = (targets ?? []).filter((t) => t.purpose === 2);

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={type.name}
        description={`${type.code} — ${type.categoryName}, ${type.nominationSourceName}, decided by ${type.winnerDecisionName}`}
        backHref="/administration/hr/awards/types"
        actions={type.isActive ? <Badge variant="secondary">Active</Badge> : null}
      />

      <Tabs defaultValue="levels">
        <TabsList>
          <TabsTrigger value="levels">Levels</TabsTrigger>
          <TabsTrigger value="scope">Scope</TabsTrigger>
          <TabsTrigger value="budgets">Budgets</TabsTrigger>
        </TabsList>

        {/* ── levels ───────────────────────────────────────────────────────── */}
        <TabsContent value="levels" className="space-y-4">
          {!type.hasLevels && (
            <Alert>
              <Info className="h-4 w-4" />
              <AlertDescription>
                This award is not marked as having levels, so nothing here will be asked for when it
                is conferred. Turn on &quot;has levels&quot; on the award itself first.
              </AlertDescription>
            </Alert>
          )}
          <Card>
            <CardHeader className="flex flex-row items-center justify-between">
              <CardTitle className="flex items-center gap-2 text-base">
                <Layers className="h-4 w-4" /> Levels
              </CardTitle>
              <Button
                size="sm"
                variant="outline"
                onClick={() =>
                  setLevelForm({ code: '', name: '', description: '', rank: (levels?.length ?? 0) + 1, monetaryAmount: '', isActive: true })
                }
              >
                <Plus className="mr-2 h-4 w-4" /> Add level
              </Button>
            </CardHeader>
            <CardContent className="p-0">
              {(levels ?? []).length === 0 ? (
                <EmptyState icon={Layers} title="No levels" description="Gold, Silver, Bronze and so on." />
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Rank</TableHead>
                      <TableHead>Code</TableHead>
                      <TableHead>Name</TableHead>
                      <TableHead className="text-right">Value</TableHead>
                      <TableHead>Active</TableHead>
                      <TableHead />
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {(levels ?? []).map((l) => (
                      <TableRow key={l.id}>
                        <TableCell>{l.rank}</TableCell>
                        <TableCell className="font-medium">{l.code}</TableCell>
                        <TableCell>{l.name}</TableCell>
                        <TableCell className="text-right">{fmtMoney(l.monetaryAmount)}</TableCell>
                        <TableCell>{l.isActive ? <Badge variant="secondary">Active</Badge> : '—'}</TableCell>
                        <TableCell className="text-right">
                          <Button
                            size="sm"
                            variant="ghost"
                            onClick={() =>
                              setLevelForm({
                                id: l.id,
                                code: l.code,
                                name: l.name,
                                description: l.description ?? '',
                                rank: l.rank,
                                monetaryAmount: l.monetaryAmount === null ? '' : String(l.monetaryAmount),
                                isActive: l.isActive,
                              })
                            }
                          >
                            Edit
                          </Button>
                          <Button
                            size="sm"
                            variant="ghost"
                            onClick={() => setRemoving({ kind: 'level', id: l.id, label: l.name })}
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
        </TabsContent>

        {/* ── scope ────────────────────────────────────────────────────────── */}
        <TabsContent value="scope" className="space-y-4">
          <Alert>
            <Info className="h-4 w-4" />
            <AlertDescription>
              An award with <strong>no electorate scope is voted on by everyone</strong> — that is
              the &quot;or all of them&quot; half of the requirement, not an oversight. Eligibility
              scope works the other way: adding one narrows who can win.
            </AlertDescription>
          </Alert>

          <Card>
            <CardHeader className="flex flex-row items-center justify-between">
              <CardTitle className="flex items-center gap-2 text-base">
                <Target className="h-4 w-4" /> Scope
              </CardTitle>
              <Button
                size="sm"
                variant="outline"
                onClick={() =>
                  setTargetForm({
                    kind: 'Employee', targetId: '', isExclusion: false, purpose: 1, reason: '',
                  })
                }
              >
                <Plus className="mr-2 h-4 w-4" /> Add scope
              </Button>
            </CardHeader>
            <CardContent className="space-y-6">
              <div>
                <p className="mb-2 text-sm font-medium">Who may win ({eligibilityTargets.length})</p>
                {eligibilityTargets.length === 0 ? (
                  <p className="text-sm text-muted-foreground">
                    Unscoped — anyone meeting the award&apos;s other criteria qualifies.
                  </p>
                ) : (
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead>Kind</TableHead>
                        <TableHead>Who</TableHead>
                        <TableHead>Effect</TableHead>
                        <TableHead>Reason</TableHead>
                        <TableHead />
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {eligibilityTargets.map((t) => (
                        <TableRow key={t.id}>
                          <TableCell>{t.targetTypeName}</TableCell>
                          <TableCell>{t.targetName ?? '—'}</TableCell>
                          <TableCell>
                            {t.isExclusion ? (
                              <Badge variant="secondary" className="bg-rose-100 text-rose-800 dark:bg-rose-900/40 dark:text-rose-200">
                                Excluded
                              </Badge>
                            ) : (
                              <Badge variant="secondary">Included</Badge>
                            )}
                          </TableCell>
                          <TableCell className="text-sm text-muted-foreground">{t.reason ?? '—'}</TableCell>
                          <TableCell className="text-right">
                            <Button
                              size="sm"
                              variant="ghost"
                              onClick={() =>
                                setRemoving({ kind: 'target', id: t.id, label: t.targetName ?? 'this scope' })
                              }
                            >
                              <Trash2 className="h-4 w-4" />
                            </Button>
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                )}
              </div>

              <div>
                <p className="mb-2 text-sm font-medium">Who may vote ({electorateTargets.length})</p>
                {electorateTargets.length === 0 ? (
                  <p className="text-sm text-muted-foreground">
                    Unscoped — <strong>everyone</strong> votes in this award.
                  </p>
                ) : (
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead>Kind</TableHead>
                        <TableHead>Who</TableHead>
                        <TableHead>Effect</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {electorateTargets.map((t) => (
                        <TableRow key={t.id}>
                          <TableCell>{t.targetTypeName}</TableCell>
                          <TableCell>{t.targetName ?? '—'}</TableCell>
                          <TableCell>{t.isExclusion ? 'Excluded' : 'Included'}</TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                )}
              </div>
            </CardContent>
          </Card>
        </TabsContent>

        {/* ── budgets ──────────────────────────────────────────────────────── */}
        <TabsContent value="budgets" className="space-y-4">
          <Card>
            <CardHeader className="flex flex-row items-center justify-between">
              <CardTitle className="flex items-center gap-2 text-base">
                <Banknote className="h-4 w-4" /> Budgets
              </CardTitle>
              <Button
                size="sm"
                variant="outline"
                onClick={() => setBudgetForm({ year: new Date().getFullYear(), budgetAmount: '' })}
              >
                <Plus className="mr-2 h-4 w-4" /> Add budget
              </Button>
            </CardHeader>
            <CardContent className="p-0">
              {(budgets ?? []).length === 0 ? (
                <EmptyState
                  icon={Banknote}
                  title="No budget"
                  description="Awards can still be conferred — they simply draw against nothing."
                />
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Code</TableHead>
                      <TableHead>Year</TableHead>
                      <TableHead className="text-right">Budget</TableHead>
                      <TableHead className="text-right">Committed</TableHead>
                      <TableHead className="text-right">Spent</TableHead>
                      <TableHead className="text-right">Available</TableHead>
                      <TableHead />
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {(budgets ?? []).map((b) => (
                      <TableRow key={b.id}>
                        <TableCell className="font-medium">{b.budgetCode}</TableCell>
                        <TableCell>{b.year}</TableCell>
                        <TableCell className="text-right">{fmtMoney(b.budgetAmount)}</TableCell>
                        {/* Conferred but unpaid — a real commitment against the year. */}
                        <TableCell className="text-right">{fmtMoney(b.reservedAmount)}</TableCell>
                        <TableCell className="text-right">{fmtMoney(b.spentAmount)}</TableCell>
                        <TableCell className="text-right font-medium">{fmtMoney(b.availableAmount)}</TableCell>
                        <TableCell className="text-right">
                          <Button
                            size="sm"
                            variant="ghost"
                            onClick={() =>
                              setBudgetForm({ id: b.id, year: b.year, budgetAmount: String(b.budgetAmount) })
                            }
                          >
                            Edit
                          </Button>
                          <Button
                            size="sm"
                            variant="ghost"
                            onClick={() =>
                              setRemoving({ kind: 'budget', id: b.id, label: `the ${b.year} budget` })
                            }
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
        </TabsContent>
      </Tabs>

      <ConfirmationDialog
        open={Boolean(removing)}
        onOpenChange={(o) => !o && setRemoving(null)}
        title={`Remove ${removing?.label}?`}
        description="Removal is a soft delete — the row is flagged rather than erased, so anything already granted against it still explains itself."
        variant="destructive"
        confirmText="Remove"
        isLoading={remove.isPending}
        onConfirm={async () => {
          try { await remove.mutateAsync(); } catch { return false; }
        }}
      />

      {/* ── dialogs ────────────────────────────────────────────────────────── */}
      <Dialog open={Boolean(levelForm)} onOpenChange={(o) => !o && setLevelForm(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{levelForm?.id ? 'Edit level' : 'Add a level'}</DialogTitle>
            <DialogDescription>Rank 1 is the highest.</DialogDescription>
          </DialogHeader>
          {levelForm && (
            <div className="space-y-4">
              <div className="grid gap-4 sm:grid-cols-2">
                <div className="space-y-2">
                  <Label htmlFor="lcode">Code</Label>
                  <Input id="lcode" value={levelForm.code}
                    onChange={(e) => setLevelForm({ ...levelForm, code: e.target.value })} />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="lname">Name</Label>
                  <Input id="lname" value={levelForm.name}
                    onChange={(e) => setLevelForm({ ...levelForm, name: e.target.value })} />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="lrank">Rank</Label>
                  <Input id="lrank" type="number" min={1} value={levelForm.rank}
                    onChange={(e) => setLevelForm({ ...levelForm, rank: Number(e.target.value) })} />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="lamount">Value</Label>
                  <Input id="lamount" type="number" min={0} value={levelForm.monetaryAmount}
                    onChange={(e) => setLevelForm({ ...levelForm, monetaryAmount: e.target.value })} />
                </div>
                <div className="space-y-2 sm:col-span-2">
                  <Label htmlFor="ldesc">Description</Label>
                  <Textarea id="ldesc" rows={2} value={levelForm.description}
                    onChange={(e) => setLevelForm({ ...levelForm, description: e.target.value })} />
                </div>
              </div>
              <div className="flex justify-end gap-2">
                <Button variant="outline" onClick={() => setLevelForm(null)}>Cancel</Button>
                <Button
                  disabled={!levelForm.code.trim() || !levelForm.name.trim() || addLevel.isPending}
                  onClick={() => addLevel.mutate()}
                >
                  {addLevel.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                  Add
                </Button>
              </div>
            </div>
          )}
        </DialogContent>
      </Dialog>

      <Dialog open={Boolean(targetForm)} onOpenChange={(o) => !o && setTargetForm(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Add scope</DialogTitle>
            <DialogDescription>Narrow who may win, or who may vote.</DialogDescription>
          </DialogHeader>
          {targetForm && (
            <div className="space-y-4">
              <div className="space-y-2">
                <Label>Applies to</Label>
                <Select
                  value={String(targetForm.purpose)}
                  onValueChange={(v) => setTargetForm({ ...targetForm, purpose: Number(v) })}
                >
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="1">Who may win</SelectItem>
                    <SelectItem value="2">Who may vote</SelectItem>
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-2">
                <Label>Scope kind</Label>
                <Select
                  value={targetForm.kind}
                  onValueChange={(v) =>
                    setTargetForm({ ...targetForm, kind: v as TargetKind, targetId: '' })
                  }
                >
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    {(Object.keys(TARGET_TYPE) as TargetKind[]).map((k) => (
                      <SelectItem key={k} value={k}>{TARGET_KIND_LABEL[k]}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>

              <div className="space-y-2">
                <Label>
                  {targetForm.kind === 'Employee' ? 'Employee'
                    : targetForm.kind === 'Position' ? 'Position'
                    : targetForm.kind === 'StaffLevel' ? 'Staff level'
                    : 'Organisation unit'}
                </Label>

                {targetForm.kind === 'Employee' ? (
                  <EmployeePicker
                    value={targetForm.targetId || null}
                    onChange={(v) => setTargetForm({ ...targetForm, targetId: v ?? '' })}
                  />
                ) : (
                  <Select
                    value={targetForm.targetId}
                    onValueChange={(v) => setTargetForm({ ...targetForm, targetId: v })}
                  >
                    <SelectTrigger>
                      <SelectValue placeholder="Choose one" />
                    </SelectTrigger>
                    <SelectContent>
                      {targetForm.kind === 'Position' &&
                        (positions ?? []).map((o) => (
                          <SelectItem key={o.id} value={o.id}>{o.title}</SelectItem>
                        ))}
                      {targetForm.kind === 'StaffLevel' &&
                        (staffLevels ?? []).map((o) => (
                          <SelectItem key={o.id} value={o.id}>{o.name}</SelectItem>
                        ))}
                      {targetForm.kind === 'OrganizationUnit' &&
                        (orgUnits ?? []).map((o) => (
                          <SelectItem key={o.id} value={o.id}>{o.name}</SelectItem>
                        ))}
                    </SelectContent>
                  </Select>
                )}

                {targetForm.kind !== 'Employee' && (
                  <p className="text-xs text-muted-foreground">
                    Scoping by {TARGET_KIND_LABEL[targetForm.kind].toLowerCase()} covers everyone in
                    it, including people who join it later.
                  </p>
                )}
              </div>
              <label className="flex items-center gap-2 text-sm">
                <Switch
                  checked={targetForm.isExclusion}
                  onCheckedChange={(v) => setTargetForm({ ...targetForm, isExclusion: v })}
                />
                Exclude them instead of including them
              </label>
              {targetForm.isExclusion && (
                <p className="text-xs text-muted-foreground">
                  An exclusion beats an inclusion — somebody both included and excluded is out.
                </p>
              )}
              <div className="space-y-2">
                <Label htmlFor="treason">Reason</Label>
                <Input id="treason" value={targetForm.reason}
                  onChange={(e) => setTargetForm({ ...targetForm, reason: e.target.value })} />
              </div>
              <div className="flex justify-end gap-2">
                <Button variant="outline" onClick={() => setTargetForm(null)}>Cancel</Button>
                <Button disabled={!targetForm.targetId || addTarget.isPending} onClick={() => addTarget.mutate()}>
                  {addTarget.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                  Add
                </Button>
              </div>
            </div>
          )}
        </DialogContent>
      </Dialog>

      <Dialog open={Boolean(budgetForm)} onOpenChange={(o) => !o && setBudgetForm(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{budgetForm?.id ? 'Edit budget' : 'Add a budget'}</DialogTitle>
            <DialogDescription>One per year. A code is generated if you leave it out.</DialogDescription>
          </DialogHeader>
          {budgetForm && (
            <div className="space-y-4">
              <div className="grid gap-4 sm:grid-cols-2">
                <div className="space-y-2">
                  <Label htmlFor="byear">Year</Label>
                  <Input id="byear" type="number" value={budgetForm.year}
                    onChange={(e) => setBudgetForm({ ...budgetForm, year: Number(e.target.value) })} />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="bamount">Amount</Label>
                  <Input id="bamount" type="number" min={0} value={budgetForm.budgetAmount}
                    onChange={(e) => setBudgetForm({ ...budgetForm, budgetAmount: e.target.value })} />
                </div>
              </div>
              <div className="flex justify-end gap-2">
                <Button variant="outline" onClick={() => setBudgetForm(null)}>Cancel</Button>
                <Button
                  disabled={budgetForm.budgetAmount === '' || addBudget.isPending}
                  onClick={() => addBudget.mutate()}
                >
                  {addBudget.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                  Add
                </Button>
              </div>
            </div>
          )}
        </DialogContent>
      </Dialog>
    </div>
  );
}
