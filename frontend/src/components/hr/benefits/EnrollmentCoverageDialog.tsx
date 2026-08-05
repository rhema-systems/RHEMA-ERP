'use client';

import { useEffect, useMemo, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2, Plus, Trash2, UserPlus, Undo2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
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
import { useToast } from '@/components/ui/use-toast';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { employeeBenefitEnrollmentService } from '@/services/hr/benefits.service';
import { benefitPolicyService } from '@/services/hr/benefits.service';
import { employeeService } from '@/services/hr/employee.service';
import { formatDate, formatMoney, humanizeEnum } from '@/lib/hr/attendance-format';
import type {
  EmployeeBenefitEnrollmentListItem,
  EnrollmentDependent,
  CreateBenefitBeneficiary,
} from '@/types/hr/benefits';

/**
 * Who an enrolment covers, and who receives it.
 *
 * The two halves behave differently on purpose, because the API does:
 *
 * - **Dependants** are edited a row at a time. Each has its own coverage window and its own
 *   consumed amount, and nothing ties one row to another beyond the policy's cap, so a row-level
 *   API is honest about the model.
 * - **Beneficiaries** are saved as a whole set. Their shares must total 100, and no sequence of
 *   single-row saves gets from one valid split to another without passing through a state that
 *   doesn't add up. So this tab edits a local draft and commits it in one call.
 *
 * Removing a dependant who has claims does not delete them — the server ends their cover instead,
 * keeping the claim ledger attributable. The toast says which of the two happened.
 */

interface Props {
  enrollment: EmployeeBenefitEnrollmentListItem | null;
  onOpenChange: (open: boolean) => void;
}

/** A beneficiary row being edited. `key` is local only — the API has no notion of row identity here. */
interface BeneficiaryDraft extends CreateBenefitBeneficiary {
  key: string;
}

const newKey = () => Math.random().toString(36).slice(2);

export function EnrollmentCoverageDialog({ enrollment, onOpenChange }: Props) {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  // Empty-string fallbacks rather than null: the queries below are gated on `enabled`, so the
  // queryFn never runs with one, and this keeps the service calls honestly typed as string.
  const enrollmentId = enrollment?.id ?? '';
  const employeeId = enrollment?.employeeId ?? '';
  const policyId = enrollment?.benefitPolicyId ?? '';

  const [busy, setBusy] = useState(false);

  // ── Dependants ────────────────────────────────────────────────────────────────
  const [addDependentId, setAddDependentId] = useState('');
  const [addFrom, setAddFrom] = useState('');
  const [addTo, setAddTo] = useState('');

  const { data: covered, isLoading: loadingCovered } = useQuery({
    queryKey: ['hr', 'enrollment-dependents', enrollmentId],
    queryFn: () => employeeBenefitEnrollmentService.getDependents(enrollmentId),
    enabled: !!enrollmentId,
  });

  // The employee's registered dependants are the only candidates — cover is extended to people
  // already on their profile, never typed in here.
  const { data: registered } = useQuery({
    queryKey: ['hr', 'employee-dependents', employeeId],
    queryFn: () => employeeService.getDependents(employeeId),
    enabled: !!employeeId,
  });

  const { data: policy } = useQuery({
    queryKey: ['hr', 'benefit-policy', policyId],
    queryFn: () => benefitPolicyService.getById(policyId),
    enabled: !!policyId,
  });

  const activeCount = (covered ?? []).filter((d) => d.isActive).length;
  const cap = policy?.maxDependents ?? null;
  const capReached = cap !== null && cap > 0 && activeCount >= cap;
  const staffOnly = policy?.recipient === 'Staff';

  const candidates = useMemo(() => {
    const takenIds = new Set(
      (covered ?? []).filter((d) => d.isActive).map((d) => d.employeeDependentId),
    );
    return (registered ?? [])
      .filter((d) => !takenIds.has(d.id) && !d.isDeceased)
      .map((d) => ({
        value: d.id,
        label: `${[d.firstName, d.middleName, d.lastName].filter(Boolean).join(' ')} — ${humanizeEnum(
          d.relationship,
        )}`,
      }));
  }, [registered, covered]);

  const refreshDependents = () =>
    queryClient.invalidateQueries({ queryKey: ['hr', 'enrollment-dependents', enrollmentId] });

  const run = async (fn: () => Promise<void>, fallback: string) => {
    setBusy(true);
    try {
      await fn();
    } catch (e: any) {
      toast({
        title: 'Error',
        description: e?.message || fallback,
        variant: 'destructive',
      });
    } finally {
      setBusy(false);
    }
  };

  const addDependent = () =>
    run(async () => {
      if (!enrollmentId || !addDependentId) return;
      await employeeBenefitEnrollmentService.addDependent(enrollmentId, {
        employeeDependentId: addDependentId,
        coverageStartDate: addFrom || null,
        coverageEndDate: addTo || null,
      });
      await refreshDependents();
      setAddDependentId('');
      setAddFrom('');
      setAddTo('');
      toast({ title: 'Cover added', description: 'The dependant is now covered by this enrolment.' });
    }, 'Failed to add the dependant.');

  const setDependentActive = (row: EnrollmentDependent, isActive: boolean) =>
    run(async () => {
      if (!enrollmentId) return;
      await employeeBenefitEnrollmentService.updateDependent(enrollmentId, row.id, {
        coverageStartDate: row.coverageStartDate ?? null,
        coverageEndDate: row.coverageEndDate ?? null,
        isActive,
      });
      await refreshDependents();
      toast({
        title: isActive ? 'Cover restored' : 'Cover ended',
        description: isActive
          ? `${row.dependentName} is covered again.`
          : `${row.dependentName} can no longer be claimed for.`,
      });
    }, 'Failed to change the cover.');

  const removeDependent = (row: EnrollmentDependent) =>
    run(async () => {
      if (!enrollmentId) return;
      const result = await employeeBenefitEnrollmentService.removeDependent(enrollmentId, row.id);
      await refreshDependents();
      // The server decides between deleting and ending cover; report what it actually did rather
      // than claiming a removal that did not happen.
      toast({
        title: result.deleted ? 'Cover removed' : 'Cover ended',
        description: result.message,
      });
    }, 'Failed to remove the dependant.');

  // ── Beneficiaries ─────────────────────────────────────────────────────────────
  const [drafts, setDrafts] = useState<BeneficiaryDraft[]>([]);
  const [loadedFor, setLoadedFor] = useState<string | null>(null);

  const { data: beneficiaries, isLoading: loadingBeneficiaries } = useQuery({
    queryKey: ['hr', 'enrollment-beneficiaries', enrollmentId],
    queryFn: () => employeeBenefitEnrollmentService.getBeneficiaries(enrollmentId),
    enabled: !!enrollmentId,
  });

  const { data: lookups } = useQuery({
    queryKey: ['hr', 'benefit-policy-lookups'],
    queryFn: () => benefitPolicyService.getLookups(),
  });

  // Seed the local draft once per enrolment. Re-seeding on every fetch would discard edits in
  // progress the moment any query refetched in the background.
  useEffect(() => {
    if (!enrollmentId || !beneficiaries || loadedFor === enrollmentId) return;
    setDrafts(
      beneficiaries.map((b) => ({
        key: newKey(),
        fullName: b.fullName,
        relationship: b.relationship,
        employeeDependentId: b.employeeDependentId ?? null,
        phoneNumber: b.phoneNumber ?? null,
        percentage: b.percentage,
      })),
    );
    setLoadedFor(enrollmentId);
  }, [enrollmentId, beneficiaries, loadedFor]);

  useEffect(() => {
    if (!enrollmentId) setLoadedFor(null);
  }, [enrollmentId]);

  const total = drafts.reduce((sum, d) => sum + (Number(d.percentage) || 0), 0);
  const totalIsValid = drafts.length === 0 || Math.abs(total - 100) < 0.001;

  const patchDraft = (key: string, patch: Partial<BeneficiaryDraft>) =>
    setDrafts((prev) => prev.map((d) => (d.key === key ? { ...d, ...patch } : d)));

  const saveBeneficiaries = () =>
    run(async () => {
      if (!enrollmentId) return;
      const saved = await employeeBenefitEnrollmentService.replaceBeneficiaries(
        enrollmentId,
        drafts.map(({ key: _key, ...b }) => ({
          ...b,
          percentage: Number(b.percentage) || 0,
        })),
      );
      queryClient.setQueryData(['hr', 'enrollment-beneficiaries', enrollmentId], saved);
      toast({
        title: 'Beneficiaries saved',
        description: drafts.length
          ? `${drafts.length} nomination(s) recorded.`
          : 'The nomination was cleared.',
      });
    }, 'Failed to save the beneficiaries.');

  const relationOptions = lookups?.relationTypes ?? [];

  return (
    <Dialog open={enrollment !== null} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-4xl">
        <DialogHeader>
          <DialogTitle>Cover and nominations</DialogTitle>
          <DialogDescription>
            {enrollment
              ? `${enrollment.benefitPolicyName} — ${enrollment.employeeName}`
              : ''}
          </DialogDescription>
        </DialogHeader>

        <Tabs defaultValue="dependants">
          <TabsList>
            <TabsTrigger value="dependants">
              Dependants {activeCount > 0 ? `(${activeCount})` : ''}
            </TabsTrigger>
            <TabsTrigger value="beneficiaries">
              Beneficiaries {drafts.length > 0 ? `(${drafts.length})` : ''}
            </TabsTrigger>
          </TabsList>

          {/* ── Dependants ────────────────────────────────────────────────────── */}
          <TabsContent value="dependants" className="space-y-4 pt-4">
            {staffOnly ? (
              <p className="rounded-md border border-amber-200 bg-amber-50 p-3 text-sm text-amber-900">
                This policy covers staff only, so dependants cannot be added to it. Change the
                policy&rsquo;s recipient if family cover is intended.
              </p>
            ) : (
              <div className="grid gap-3 rounded-md border p-3 md:grid-cols-[2fr_1fr_1fr_auto] md:items-end">
                <div className="space-y-2">
                  <Label htmlFor="addDependent">Dependant</Label>
                  <Select value={addDependentId} onValueChange={setAddDependentId}>
                    <SelectTrigger id="addDependent">
                      <SelectValue
                        placeholder={
                          candidates.length
                            ? 'Select a registered dependant…'
                            : 'No dependants available to add'
                        }
                      />
                    </SelectTrigger>
                    <SelectContent>
                      {candidates.map((c) => (
                        <SelectItem key={c.value} value={c.value}>
                          {c.label}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-2">
                  <Label htmlFor="addFrom">Cover from</Label>
                  <Input
                    id="addFrom"
                    type="date"
                    value={addFrom}
                    onChange={(e) => setAddFrom(e.target.value)}
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="addTo">Cover to</Label>
                  <Input
                    id="addTo"
                    type="date"
                    value={addTo}
                    onChange={(e) => setAddTo(e.target.value)}
                  />
                </div>
                <Button onClick={addDependent} disabled={busy || !addDependentId || capReached}>
                  {busy ? (
                    <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                  ) : (
                    <UserPlus className="mr-2 h-4 w-4" />
                  )}
                  Add
                </Button>
              </div>
            )}

            {cap !== null && cap > 0 && (
              <p className={`text-sm ${capReached ? 'text-amber-700' : 'text-muted-foreground'}`}>
                {activeCount} of {cap} dependant slot(s) used
                {capReached ? ' — end an existing cover to add another.' : '.'}
              </p>
            )}

            {loadingCovered ? (
              <div className="flex justify-center p-6">
                <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
              </div>
            ) : (covered ?? []).length === 0 ? (
              <EmptyState
                icon={UserPlus}
                title="No dependants covered"
                description="Add one of the employee's registered dependants to extend cover to them."
              />
            ) : (
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Dependant</TableHead>
                    <TableHead>Relationship</TableHead>
                    <TableHead>Cover</TableHead>
                    <TableHead className="text-right">Used</TableHead>
                    <TableHead>Status</TableHead>
                    <TableHead className="w-[1%]" />
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {(covered ?? []).map((row) => (
                    <TableRow key={row.id} className={row.isActive ? undefined : 'opacity-60'}>
                      <TableCell className="font-medium">{row.dependentName}</TableCell>
                      <TableCell>{humanizeEnum(row.relationship)}</TableCell>
                      <TableCell className="text-sm text-muted-foreground">
                        {row.coverageStartDate ? formatDate(row.coverageStartDate) : 'From enrolment'}
                        {row.coverageEndDate ? ` → ${formatDate(row.coverageEndDate)}` : ''}
                      </TableCell>
                      <TableCell className="text-right">
                        {formatMoney(row.benefitAmountUsed, enrollment?.currency)}
                      </TableCell>
                      <TableCell>
                        <Badge variant={row.isActive ? 'default' : 'secondary'}>
                          {row.isActive ? 'Covered' : 'Ended'}
                        </Badge>
                      </TableCell>
                      <TableCell className="whitespace-nowrap text-right">
                        {row.isActive ? (
                          <Button
                            variant="ghost"
                            size="sm"
                            disabled={busy}
                            onClick={() => removeDependent(row)}
                          >
                            <Trash2 className="h-4 w-4" />
                          </Button>
                        ) : (
                          <Button
                            variant="ghost"
                            size="sm"
                            disabled={busy || capReached}
                            onClick={() => setDependentActive(row, true)}
                          >
                            <Undo2 className="mr-1 h-4 w-4" />
                            Restore
                          </Button>
                        )}
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            )}
          </TabsContent>

          {/* ── Beneficiaries ─────────────────────────────────────────────────── */}
          <TabsContent value="beneficiaries" className="space-y-4 pt-4">
            <div className="flex items-center justify-between gap-3">
              <p className="text-sm text-muted-foreground">
                Shares must total 100. Saving commits the whole set at once.
              </p>
              <Badge variant={totalIsValid ? 'default' : 'destructive'}>
                Total {total.toFixed(2)}%
              </Badge>
            </div>

            {loadingBeneficiaries ? (
              <div className="flex justify-center p-6">
                <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
              </div>
            ) : drafts.length === 0 ? (
              <EmptyState
                icon={Plus}
                title="No beneficiaries nominated"
                description="Name who receives this benefit. Add rows and split the shares to 100%."
              />
            ) : (
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Full name</TableHead>
                    <TableHead>Relationship</TableHead>
                    <TableHead>Phone</TableHead>
                    <TableHead className="w-[110px] text-right">Share %</TableHead>
                    <TableHead className="w-[1%]" />
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {drafts.map((d) => (
                    <TableRow key={d.key}>
                      <TableCell>
                        <Input
                          value={d.fullName}
                          onChange={(e) => patchDraft(d.key, { fullName: e.target.value })}
                          placeholder="Full name"
                        />
                      </TableCell>
                      <TableCell>
                        <Select
                          value={d.relationship}
                          onValueChange={(v) => patchDraft(d.key, { relationship: v })}
                        >
                          <SelectTrigger>
                            <SelectValue />
                          </SelectTrigger>
                          <SelectContent>
                            {relationOptions.map((o) => (
                              <SelectItem key={o.name} value={o.name}>
                                {o.label}
                              </SelectItem>
                            ))}
                          </SelectContent>
                        </Select>
                      </TableCell>
                      <TableCell>
                        <Input
                          value={d.phoneNumber ?? ''}
                          onChange={(e) =>
                            patchDraft(d.key, { phoneNumber: e.target.value || null })
                          }
                          placeholder="Optional"
                        />
                      </TableCell>
                      <TableCell>
                        <Input
                          type="number"
                          min={0}
                          max={100}
                          step="0.01"
                          className="text-right"
                          value={d.percentage}
                          onChange={(e) =>
                            patchDraft(d.key, { percentage: e.target.valueAsNumber || 0 })
                          }
                        />
                      </TableCell>
                      <TableCell className="text-right">
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={() =>
                            setDrafts((prev) => prev.filter((x) => x.key !== d.key))
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

            <div className="flex items-center gap-2">
              <Button
                variant="outline"
                onClick={() =>
                  setDrafts((prev) => [
                    ...prev,
                    {
                      key: newKey(),
                      fullName: '',
                      // "Any" is the entity's own default and the safe pick when unspecified.
                      relationship: relationOptions[0]?.name ?? 'Any',
                      employeeDependentId: null,
                      phoneNumber: null,
                      // Offer the remaining share, so a first row lands on 100 and the split
                      // reaches a valid total without arithmetic by hand.
                      percentage: Math.max(0, 100 - total),
                    },
                  ])
                }
              >
                <Plus className="mr-2 h-4 w-4" />
                Add beneficiary
              </Button>
              {!totalIsValid && (
                <span className="text-sm text-destructive">
                  Adjust the shares to total 100% before saving.
                </span>
              )}
            </div>
          </TabsContent>
        </Tabs>

        <DialogFooter className="gap-2">
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={busy}>
            Close
          </Button>
          <Button
            onClick={saveBeneficiaries}
            disabled={busy || !totalIsValid || drafts.some((d) => !d.fullName.trim())}
          >
            {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            Save beneficiaries
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
