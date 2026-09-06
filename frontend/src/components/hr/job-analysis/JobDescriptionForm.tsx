'use client';

import { useEffect, useMemo, useRef, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Loader2, Save } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Switch } from '@/components/ui/switch';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { jobArchitectureService } from '@/services/hr/job-architecture.service';
import { employeePositionService } from '@/services/hr/employee-position.service';
import { salaryGradeService } from '@/services/hr/salary-grade.service';
import { staffLevelService } from '@/services/hr/staff-level.service';
import { unionService } from '@/services/hr/union.service';
import type {
  CreateJobDescription,
  DecisionAuthorityLevel,
  RoleCriticalityLevel,
} from '@/types/hr/job-architecture';

const CRITICALITY: RoleCriticalityLevel[] = ['Low', 'Medium', 'High', 'MissionCritical'];
/** ⚠ Three values, not five. Read off the enum, not off the word "level". */
const AUTONOMY: DecisionAuthorityLevel[] = ['Operational', 'Tactical', 'Strategic'];

/** A Radix `Select` cannot hold an empty string as a value, so "unset" is this sentinel. */
const NONE = '__none__';

const emptyForm: CreateJobDescription = {
  positionId: '',
  jobTitle: '',
  jobSummary: '',
  effectiveDate: new Date().toISOString().slice(0, 10),
  reviewCycleMonths: 24,
};

/** `input[type=date]` refuses anything but yyyy-MM-dd; the API answers with a full timestamp. */
const toDateInput = (v?: string | null) => (v ? v.slice(0, 10) : '');
const toMoney = (v: string) => (v.trim() === '' ? null : Number(v));

export interface JobDescriptionFormProps {
  mode: 'create' | 'edit';
  /** Prefill. On edit this is the record as the API returned it. */
  initial?: Partial<CreateJobDescription>;
  /** Shown instead of the position picker on edit — see the lock note below. */
  lockedPositionTitle?: string;
  title: string;
  description: string;
  backHref: string;
  saveLabel: string;
  onSubmit: (values: CreateJobDescription) => Promise<void>;
}

/**
 * The authoring form for a job description's OWN fields, shared by create and edit.
 *
 * ⚠ **There is no "prepared by" field, and that is deliberate.** The API takes the author from the
 * token — a value the client cannot know is a value the client must not send. The same rule removed
 * the caller-declared approver from the manpower budget in slice 7.
 *
 * ⚠ **The classification is on THIS form, not a second save.** Until slice 2 the create DTO dropped
 * job family, sub-family, level, staff level, valuation and occupation code, so a form had to save
 * and then immediately save again — and anything that skipped the second save left the taxonomy with
 * no consumer at all.
 *
 * ⚠ **The position is locked once the record exists.** `VersionNumber` is issued per position
 * (`GetNextVersionNumberAsync(positionId)`), so moving a saved description to another post would
 * carry a version number that means nothing there and leave a hole in the one it left. The API
 * permits it; the screen does not. A description filed against the wrong post is replaced, not moved.
 */
export function JobDescriptionForm({
  mode,
  initial,
  lockedPositionTitle,
  title,
  description,
  backHref,
  saveLabel,
  onSubmit,
}: JobDescriptionFormProps) {
  const [saving, setSaving] = useState(false);
  const [form, setForm] = useState<CreateJobDescription>({ ...emptyForm, ...initial });

  // ⚠ Active unions only. An inactive union is one nobody bargains with any more; offering it here
  // would let a role be filed under a dead agreement.
  const { data: unions } = useQuery({
    queryKey: ['hr', 'unions', 'active'],
    queryFn: () => unionService.getActive(),
  });

  const { data: positions } = useQuery({
    queryKey: ['positions', 'all'],
    queryFn: () => employeePositionService.getAll(),
    enabled: mode === 'create',
  });
  const { data: families } = useQuery({
    queryKey: ['job-architecture', 'families', 'active'],
    queryFn: () => jobArchitectureService.getActiveJobFamilies(),
  });
  const { data: levels } = useQuery({
    queryKey: ['job-architecture', 'levels', 'active'],
    queryFn: () => jobArchitectureService.getActiveJobLevels(),
  });
  const { data: subFamilies } = useQuery({
    queryKey: ['job-architecture', 'sub-families', form.jobFamilyId],
    queryFn: () => jobArchitectureService.getSubFamilies(form.jobFamilyId as string),
    enabled: !!form.jobFamilyId,
  });
  const { data: staffLevels } = useQuery({
    queryKey: ['hr', 'staff-levels', 'active'],
    queryFn: () => staffLevelService.getActive(),
  });
  const { data: grades } = useQuery({
    queryKey: ['hr', 'salary-grades', 'active'],
    queryFn: () => salaryGradeService.getActive(),
  });

  /**
   * Changing the family clears the sub-family. The API refuses a sub-family belonging to a
   * different family (400), and two independent dropdowns produce exactly that mismatch the first
   * time someone changes one and not the other.
   *
   * ⚠ The ref is not decoration. Without it this effect fires on MOUNT, which on the edit screen
   * throws away the sub-family the record was saved with before the author has touched anything.
   */
  const previousFamily = useRef(form.jobFamilyId);
  useEffect(() => {
    if (previousFamily.current === form.jobFamilyId) return;
    previousFamily.current = form.jobFamilyId;
    setForm((f) => (f.jobSubFamilyId ? { ...f, jobSubFamilyId: null } : f));
  }, [form.jobFamilyId]);

  const selectedPosition = useMemo(
    () => (positions ?? []).find((p) => p.id === form.positionId),
    [positions, form.positionId],
  );

  const set = <K extends keyof CreateJobDescription>(key: K, value: CreateJobDescription[K]) =>
    setForm((f) => ({ ...f, [key]: value }));

  const canSave = !!form.positionId && !!form.jobTitle.trim() && !!form.jobSummary.trim();

  const save = async () => {
    if (!canSave || saving) return;
    setSaving(true);
    try {
      await onSubmit(form);
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="space-y-6">
      <PageHeader
        title={title}
        description={description}
        backHref={backHref}
        actions={
          <Button onClick={save} disabled={!canSave || saving}>
            {saving ? (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            ) : (
              <Save className="mr-2 h-4 w-4" />
            )}
            {saveLabel}
          </Button>
        }
      />

      <Card>
        <CardHeader>
          <CardTitle>The role</CardTitle>
        </CardHeader>
        <CardContent className="grid gap-4 sm:grid-cols-2">
          <div className="space-y-2">
            <Label>Position *</Label>
            {mode === 'edit' ? (
              <>
                <Input value={lockedPositionTitle ?? ''} readOnly disabled />
                <p className="text-xs text-muted-foreground">
                  A saved description stays with its position — its version number is issued per
                  post. To describe a different post, create a description there.
                </p>
              </>
            ) : (
              <>
                <Select value={form.positionId} onValueChange={(v) => set('positionId', v)}>
                  <SelectTrigger>
                    <SelectValue placeholder="Choose the position" />
                  </SelectTrigger>
                  <SelectContent>
                    {(positions ?? []).map((p) => (
                      <SelectItem key={p.id} value={p.id}>
                        {p.title}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                {selectedPosition && (
                  <p className="text-xs text-muted-foreground">
                    A position may hold many versions; only the approved one is in force.
                  </p>
                )}
              </>
            )}
          </div>

          <div className="space-y-2">
            <Label>Job title *</Label>
            <Input
              value={form.jobTitle}
              onChange={(e) => set('jobTitle', e.target.value)}
              placeholder="e.g. Senior Quantity Surveyor"
            />
          </div>

          <div className="space-y-2 sm:col-span-2">
            <Label>Job summary *</Label>
            <Textarea
              rows={4}
              value={form.jobSummary}
              onChange={(e) => set('jobSummary', e.target.value)}
              placeholder="What the role exists to do."
            />
          </div>

          <div className="space-y-2">
            <Label>Effective date *</Label>
            <Input
              type="date"
              value={toDateInput(form.effectiveDate)}
              onChange={(e) => set('effectiveDate', e.target.value)}
            />
          </div>

          <div className="space-y-2">
            <Label>Review cycle (months)</Label>
            <Input
              type="number"
              min={1}
              value={form.reviewCycleMonths}
              onChange={(e) => set('reviewCycleMonths', Number(e.target.value))}
            />
          </div>

          {mode === 'edit' && (
            <div className="space-y-2 sm:col-span-2">
              <Label>Reason for this revision</Label>
              <Input
                value={form.revisionReason ?? ''}
                onChange={(e) => set('revisionReason', e.target.value)}
                placeholder="Why the description is being changed."
              />
            </div>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Classification</CardTitle>
        </CardHeader>
        <CardContent className="grid gap-4 sm:grid-cols-3">
          <div className="space-y-2">
            <Label>Job family</Label>
            <Select
              value={form.jobFamilyId ?? NONE}
              onValueChange={(v) => set('jobFamilyId', v === NONE ? null : v)}
            >
              <SelectTrigger>
                <SelectValue placeholder="Unclassified" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value={NONE}>Unclassified</SelectItem>
                {(families ?? []).map((f) => (
                  <SelectItem key={f.id} value={f.id}>
                    {f.code} — {f.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="space-y-2">
            <Label>Sub-family</Label>
            <Select
              value={form.jobSubFamilyId ?? NONE}
              onValueChange={(v) => set('jobSubFamilyId', v === NONE ? null : v)}
              disabled={!form.jobFamilyId}
            >
              <SelectTrigger>
                <SelectValue placeholder={form.jobFamilyId ? 'Optional' : 'Choose a family first'} />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value={NONE}>None</SelectItem>
                {(subFamilies ?? []).map((sf) => (
                  <SelectItem key={sf.id} value={sf.id}>
                    {sf.code} — {sf.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="space-y-2">
            <Label>Career level</Label>
            <Select
              value={form.jobLevelId ?? NONE}
              onValueChange={(v) => set('jobLevelId', v === NONE ? null : v)}
            >
              <SelectTrigger>
                <SelectValue placeholder="Unassigned" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value={NONE}>Unassigned</SelectItem>
                {(levels ?? [])
                  .slice()
                  .sort((a, b) => a.rank - b.rank)
                  .map((l) => (
                    <SelectItem key={l.id} value={l.id}>
                      {l.rank}. {l.name}
                    </SelectItem>
                  ))}
              </SelectContent>
            </Select>
          </div>

          <div className="space-y-2">
            <Label>Staff level</Label>
            <Select
              value={form.staffLevelId ?? NONE}
              onValueChange={(v) => set('staffLevelId', v === NONE ? null : v)}
            >
              <SelectTrigger>
                <SelectValue placeholder="Unassigned" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value={NONE}>Unassigned</SelectItem>
                {(staffLevels ?? []).map((sl) => (
                  <SelectItem key={sl.id} value={sl.id}>
                    {sl.rank}. {sl.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="space-y-2">
            <Label>Occupation code</Label>
            <Input
              value={form.occupationCode ?? ''}
              onChange={(e) => set('occupationCode', e.target.value)}
            />
          </div>

          <div className="space-y-2">
            <Label>Criticality</Label>
            <Select
              value={form.roleCriticality ?? NONE}
              onValueChange={(v) =>
                set('roleCriticality', v === NONE ? null : (v as RoleCriticalityLevel))
              }
            >
              <SelectTrigger>
                <SelectValue placeholder="Not assessed" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value={NONE}>Not assessed</SelectItem>
                {CRITICALITY.map((c) => (
                  <SelectItem key={c} value={c}>
                    {c === 'MissionCritical' ? 'Mission critical' : c}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          {/*
            ⚠ These two fields existed on the entity, the DTOs and the detail screen since the port,
            and NOTHING could set them: the job-description detail renders a "Union" row, no form
            carried a control for it, and the union register did not exist. Measured on DEFAULT
            2026-08-22: 355 job descriptions, 0 with a union, 0 flagged bargaining-unit.

            They are not decoration. `IsBargainingUnitRole` routes the approval
            (SimpleWorkflowService) and both feed the offer letter's bargaining-unit clause
            (OfferLetterService: ["IsBargainingUnit"], ["UnionName"]), so until now that clause could
            never fire for any role.
          */}
          <div className="space-y-2">
            <Label>Bargaining unit</Label>
            <div className="flex h-10 items-center justify-between rounded-md border px-3">
              <span className="text-sm text-muted-foreground">Role is covered by a CBA</span>
              <Switch
                checked={form.isBargainingUnitRole ?? false}
                onCheckedChange={(v) => {
                  set('isBargainingUnitRole', v);
                  // Clearing the union with the flag keeps the pair honest: a role that is not in a
                  // bargaining unit has no union, and a stale id would still print in the offer letter.
                  if (!v) set('unionId', null);
                }}
              />
            </div>
          </div>

          <div className="space-y-2 sm:col-span-2">
            <Label>Union</Label>
            <Select
              value={form.unionId ?? NONE}
              onValueChange={(v) => set('unionId', v === NONE ? null : v)}
              disabled={!form.isBargainingUnitRole}
            >
              <SelectTrigger>
                <SelectValue
                  placeholder={
                    form.isBargainingUnitRole
                      ? (unions ?? []).length
                        ? 'Choose the union'
                        : 'No active unions on the register yet'
                      : 'Not a bargaining-unit role'
                  }
                />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value={NONE}>None</SelectItem>
                {(unions ?? []).map((u) => (
                  <SelectItem key={u.id} value={u.id}>
                    {u.code ? `${u.code} — ${u.name}` : u.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            <p className="text-xs text-muted-foreground">
              Printed in the offer letter&rsquo;s bargaining-unit clause. Unions are maintained under
              Administration &rsaquo; HR &rsaquo; Unions.
            </p>
          </div>

          <div className="space-y-2 sm:col-span-3">
            <Label>Essential functions</Label>
            <Textarea
              rows={3}
              value={form.essentialFunctionsSummary ?? ''}
              onChange={(e) => set('essentialFunctionsSummary', e.target.value)}
              placeholder="The functions the role cannot be performed without."
            />
          </div>
        </CardContent>
      </Card>

      {/*
        The Valuation tab on the detail screen has rendered these seven fields since area 17 and
        nothing could set any of them — `PUT descriptions/{id}` had no caller in the whole frontend,
        and the create form never offered them. They sit on the create path too, so a role can be
        valued as it is written rather than in a second pass nobody comes back for.
      */}
      <Card>
        <CardHeader>
          <CardTitle>Valuation &amp; authority</CardTitle>
        </CardHeader>
        <CardContent className="grid gap-4 sm:grid-cols-3">
          <div className="space-y-2">
            <Label>Intrinsic value</Label>
            <Input
              type="number"
              min={0}
              step="0.01"
              value={form.roleIntrinsicValue ?? ''}
              onChange={(e) => set('roleIntrinsicValue', toMoney(e.target.value))}
            />
          </div>

          <div className="space-y-2">
            <Label>Benchmark salary</Label>
            <Input
              type="number"
              min={0}
              step="0.01"
              value={form.industryBenchmarkSalary ?? ''}
              onChange={(e) => set('industryBenchmarkSalary', toMoney(e.target.value))}
            />
          </div>

          <div className="space-y-2">
            <Label>Suggested salary grade</Label>
            <Select
              value={form.suggestedSalaryGradeId ?? NONE}
              onValueChange={(v) => set('suggestedSalaryGradeId', v === NONE ? null : v)}
            >
              <SelectTrigger>
                <SelectValue placeholder="None" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value={NONE}>None</SelectItem>
                {(grades ?? []).map((g) => (
                  <SelectItem key={g.id} value={g.id}>
                    {g.code} — {g.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            {/* Payroll owns the grade store; this is a suggestion, never an assignment. */}
            <p className="text-xs text-muted-foreground">A suggestion for payroll, not an assignment.</p>
          </div>

          <div className="space-y-2">
            <Label>Autonomy</Label>
            <Select
              value={form.autonomyLevel ?? NONE}
              onValueChange={(v) =>
                set('autonomyLevel', v === NONE ? null : (v as DecisionAuthorityLevel))
              }
            >
              <SelectTrigger>
                <SelectValue placeholder="Not assessed" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value={NONE}>Not assessed</SelectItem>
                {AUTONOMY.map((a) => (
                  <SelectItem key={a} value={a}>
                    {a}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="space-y-2">
            <Label>Financial authority limit</Label>
            <Input
              type="number"
              min={0}
              step="0.01"
              value={form.financialAuthorityLimit ?? ''}
              onChange={(e) => set('financialAuthorityLimit', toMoney(e.target.value))}
            />
          </div>

          <div className="space-y-2">
            <Label>Decision scope</Label>
            <Input
              value={form.decisionMakingScope ?? ''}
              onChange={(e) => set('decisionMakingScope', e.target.value)}
              placeholder="What the role decides on its own."
            />
          </div>

          <div className="space-y-2 sm:col-span-3">
            <Label>Valuation notes</Label>
            <Textarea
              rows={3}
              value={form.valuationNotes ?? ''}
              onChange={(e) => set('valuationNotes', e.target.value)}
            />
          </div>

          <div className="space-y-2 sm:col-span-3">
            <Label>Approval authority notes</Label>
            <Textarea
              rows={2}
              value={form.approvalAuthorityNotes ?? ''}
              onChange={(e) => set('approvalAuthorityNotes', e.target.value)}
            />
          </div>
        </CardContent>
      </Card>

      <p className="text-sm text-muted-foreground">
        Responsibilities, duties, qualifications, competencies and the rest are added on the job
        description itself, under its tabs.
      </p>
    </div>
  );
}
