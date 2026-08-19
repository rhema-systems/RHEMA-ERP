'use client';

import { useEffect, useMemo, useState } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import { Loader2, Save } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
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
import { toast } from 'sonner';
import type {
  CreateJobDescription,
  DecisionAuthorityLevel,
  RoleCriticalityLevel,
} from '@/types/hr/job-architecture';

const CRITICALITY: RoleCriticalityLevel[] = ['Low', 'Medium', 'High', 'MissionCritical'];
/** ⚠ Three values, not five. Read off the enum, not off the word "level". */
const AUTONOMY: DecisionAuthorityLevel[] = ['Operational', 'Tactical', 'Strategic'];

/**
 * Authoring a job description.
 *
 * ⚠ **There is no "prepared by" field, and that is deliberate.** The API takes the author from the
 * token — a value the client cannot know is a value the client must not send. The same rule removed
 * the caller-declared approver from the manpower budget in slice 7.
 *
 * ⚠ **The classification is on THIS form, not a second save.** Until slice 2 the create DTO dropped
 * job family, sub-family, level, staff level, valuation and occupation code, so a form had to save
 * and then immediately save again — and anything that skipped the second save left the taxonomy with
 * no consumer at all.
 */
export default function NewJobDescriptionPage() {
  const router = useRouter();
  const params = useSearchParams();
  const presetPosition = params.get('positionId');

  const [saving, setSaving] = useState(false);
  const [form, setForm] = useState<CreateJobDescription>({
    positionId: presetPosition ?? '',
    jobTitle: '',
    jobSummary: '',
    effectiveDate: new Date().toISOString().slice(0, 10),
    reviewCycleMonths: 24,
  });

  const { data: positions } = useQuery({
    queryKey: ['positions', 'all'],
    queryFn: () => employeePositionService.getAll(),
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

  // ⚠ Changing the family clears the sub-family. The API refuses a sub-family belonging to a
  // different family (400), and two independent dropdowns produce exactly that mismatch the first
  // time someone changes one and not the other.
  useEffect(() => {
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
    if (!canSave) return;
    setSaving(true);
    try {
      // ⚠ Undefined optionals are DROPPED by JSON.stringify, which is what the API wants: an empty
      // string on a Guid or a DateOnly is a 400, not a null. Never send '' for an unset picker.
      const payload: CreateJobDescription = {
        ...form,
        jobFamilyId: form.jobFamilyId || undefined,
        jobSubFamilyId: form.jobSubFamilyId || undefined,
        jobLevelId: form.jobLevelId || undefined,
        occupationCode: form.occupationCode || undefined,
        essentialFunctionsSummary: form.essentialFunctionsSummary || undefined,
        roleCriticality: form.roleCriticality || undefined,
        autonomyLevel: form.autonomyLevel || undefined,
        decisionMakingScope: form.decisionMakingScope || undefined,
        valuationNotes: form.valuationNotes || undefined,
      };
      const created = await jobArchitectureService.createJobDescription(payload);
      toast.success(`${created.jobDescriptionNumber} created`);
      router.push(`/hr/job-descriptions/${created.id}`);
    } catch (e: any) {
      toast.error(e instanceof Error ? e.message : 'Could not save the job description');
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="space-y-6">
      <PageHeader
        title="New job description"
        description="Describe what the position is accountable for. It takes effect once approved."
        backHref="/hr/job-descriptions"
        actions={
          <Button onClick={save} disabled={!canSave || saving}>
            {saving ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Save className="mr-2 h-4 w-4" />}
            Save draft
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
              value={form.effectiveDate}
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
              value={form.jobFamilyId ?? ''}
              onValueChange={(v) => set('jobFamilyId', v || null)}
            >
              <SelectTrigger>
                <SelectValue placeholder="Unclassified" />
              </SelectTrigger>
              <SelectContent>
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
              value={form.jobSubFamilyId ?? ''}
              onValueChange={(v) => set('jobSubFamilyId', v || null)}
              disabled={!form.jobFamilyId}
            >
              <SelectTrigger>
                <SelectValue placeholder={form.jobFamilyId ? 'Optional' : 'Choose a family first'} />
              </SelectTrigger>
              <SelectContent>
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
            <Select value={form.jobLevelId ?? ''} onValueChange={(v) => set('jobLevelId', v || null)}>
              <SelectTrigger>
                <SelectValue placeholder="Unassigned" />
              </SelectTrigger>
              <SelectContent>
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
            <Label>Occupation code</Label>
            <Input
              value={form.occupationCode ?? ''}
              onChange={(e) => set('occupationCode', e.target.value)}
            />
          </div>

          <div className="space-y-2">
            <Label>Criticality</Label>
            <Select
              value={form.roleCriticality ?? ''}
              onValueChange={(v) => set('roleCriticality', (v || null) as RoleCriticalityLevel | null)}
            >
              <SelectTrigger>
                <SelectValue placeholder="Not assessed" />
              </SelectTrigger>
              <SelectContent>
                {CRITICALITY.map((c) => (
                  <SelectItem key={c} value={c}>
                    {c === 'MissionCritical' ? 'Mission critical' : c}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="space-y-2">
            <Label>Autonomy</Label>
            <Select
              value={form.autonomyLevel ?? ''}
              onValueChange={(v) => set('autonomyLevel', (v || null) as DecisionAuthorityLevel | null)}
            >
              <SelectTrigger>
                <SelectValue placeholder="Not assessed" />
              </SelectTrigger>
              <SelectContent>
                {AUTONOMY.map((a) => (
                  <SelectItem key={a} value={a}>
                    {a}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
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

      <p className="text-sm text-muted-foreground">
        Responsibilities, qualifications, competencies and the rest are added on the job description
        once it is saved.
      </p>
    </div>
  );
}
