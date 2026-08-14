'use client';

import { useState, type ReactNode } from 'react';
import { useParams } from 'next/navigation';
import { z } from 'zod';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2, Pencil } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { ResourceCollectionTab } from '@/components/hr/common/ResourceCollectionTab';
import { EmployeePickerField } from '@/components/hr/attendance/EmployeePickerField';
import {
  TextField,
  NumberField,
  TextareaField,
  SelectField,
  SwitchField,
  DateField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { safetyReturnToWorkService } from '@/services/hr/safety-return-to-work.service';
import { SHE_RETURN_TO_WORK_STATUS_OPTIONS } from '@/types/hr/safety-health';
import type {
  SheReturnToWorkStatus,
  SheReturnToWorkPhase,
  SheReturnToWorkReview,
} from '@/types/hr/safety-health';

/**
 * Return-to-work plan detail: medical clearance and restrictions, the edit dialog, phased
 * duties (numbers are server-assigned — a deleted phase keeps its number) and add-only
 * periodic reviews. Completion is recorded through the edit dialog (status + completion
 * fields); there is no separate close flow.
 */
const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const blank = (v?: string) => (v && v.length > 0 ? v : null);
const dateOrNull = (v?: string) => (v && v.length > 0 ? new Date(v).toISOString() : null);

const editSchema = z.object({
  planDate: z.string().min(1),
  plannedReturnDate: z.string().optional().or(z.literal('')),
  actualReturnDate: z.string().optional().or(z.literal('')),
  medicalRestrictions: z.string().max(1000).optional().or(z.literal('')),
  medicalClearanceDate: z.string().optional().or(z.literal('')),
  medicalClearanceNotes: z.string().max(500).optional().or(z.literal('')),
  requiresWorkplaceModifications: z.boolean(),
  workplaceModificationsDescription: z.string().max(1000).optional().or(z.literal('')),
  status: z.string().min(1),
  coordinatorId: z.string().optional().or(z.literal('')),
  supervisorId: z.string().optional().or(z.literal('')),
  completionDate: z.string().optional().or(z.literal('')),
  successfullyCompleted: z.boolean(),
  completionNotes: z.string().max(500).optional().or(z.literal('')),
});
type EditForm = z.input<typeof editSchema>;

const phaseSchema = z.object({
  phaseName: z.string().min(1, 'A phase name is required').max(100),
  startDate: z.string().min(1, 'A start date is required'),
  endDate: z.string().optional().or(z.literal('')),
  requiresReducedHours: z.boolean(),
  hoursPerDay: z.coerce.number().min(1).max(24).optional(),
  daysPerWeek: z.coerce.number().min(1).max(7).optional(),
  duties: z.string().min(1, 'Duties are required').max(1000),
  restrictions: z.string().min(1, 'Restrictions are required').max(1000),
  assessmentDate: z.string().min(1, 'An assessment date is required'),
  assessedById: z.string().min(1, 'An assessor is required'),
  // Debrief fields — captured after the phase, on edit.
  employeeProgress: z.string().max(1000).optional().or(z.literal('')),
  challengesFaced: z.string().max(500).optional().or(z.literal('')),
  accommodationsEffectiveness: z.string().max(500).optional().or(z.literal('')),
  recommendedAdjustments: z.string().max(500).optional().or(z.literal('')),
  employeeFeedback: z.string().max(500).optional().or(z.literal('')),
  phaseCompleted: z.boolean(),
  actualEndDate: z.string().optional().or(z.literal('')),
  canContinuePlan: z.boolean(),
  completionNotes: z.string().max(500).optional().or(z.literal('')),
});
type PhaseForm = z.input<typeof phaseSchema>;

const reviewSchema = z.object({
  reviewDate: z.string().min(1, 'A review date is required'),
  employeeCondition: z.string().max(500).optional().or(z.literal('')),
  workProgress: z.string().max(500).optional().or(z.literal('')),
  issuesIdentified: z.string().max(500).optional().or(z.literal('')),
  recommendedActions: z.string().max(500).optional().or(z.literal('')),
  reviewedById: z.string().min(1, 'A reviewer is required'),
  nextReviewDate: z.string().optional().or(z.literal('')),
});
type ReviewForm = z.input<typeof reviewSchema>;

const statusLabel = (v: string) =>
  SHE_RETURN_TO_WORK_STATUS_OPTIONS.find((o) => o.value === v)?.label ?? v;

function Fact({ label, value }: { label: string; value: ReactNode }) {
  return (
    <div>
      <div className="text-muted-foreground text-xs">{label}</div>
      <div className="text-sm font-medium">{value ?? '—'}</div>
    </div>
  );
}

export default function ReturnToWorkPlanDetailPage() {
  const params = useParams<{ id: string }>();
  const id = params.id;
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [editOpen, setEditOpen] = useState(false);
  const [busy, setBusy] = useState(false);

  const planKey = ['hr', 'safety-rtw', id];

  const { data: plan } = useQuery({
    queryKey: planKey,
    queryFn: () => safetyReturnToWorkService.getById(id),
  });

  const editForm = useForm<EditForm>({ resolver: zodResolver(editSchema) });

  const openEdit = () => {
    if (!plan) return;
    editForm.reset({
      planDate: plan.planDate.slice(0, 10),
      plannedReturnDate: plan.plannedReturnDate?.slice(0, 10) ?? '',
      actualReturnDate: plan.actualReturnDate?.slice(0, 10) ?? '',
      medicalRestrictions: plan.medicalRestrictions ?? '',
      medicalClearanceDate: plan.medicalClearanceDate?.slice(0, 10) ?? '',
      medicalClearanceNotes: plan.medicalClearanceNotes ?? '',
      requiresWorkplaceModifications: plan.requiresWorkplaceModifications,
      workplaceModificationsDescription: plan.workplaceModificationsDescription ?? '',
      status: plan.status,
      coordinatorId: plan.coordinatorId ?? '',
      supervisorId: plan.supervisorId ?? '',
      completionDate: plan.completionDate?.slice(0, 10) ?? '',
      successfullyCompleted: plan.successfullyCompleted,
      completionNotes: plan.completionNotes ?? '',
    });
    setEditOpen(true);
  };

  const submitEdit = editForm.handleSubmit(async (values) => {
    setBusy(true);
    try {
      const v = editSchema.parse(values);
      await safetyReturnToWorkService.update(id, {
        id,
        planDate: new Date(v.planDate).toISOString(),
        plannedReturnDate: dateOrNull(v.plannedReturnDate),
        actualReturnDate: dateOrNull(v.actualReturnDate),
        medicalRestrictions: blank(v.medicalRestrictions),
        medicalClearanceDate: dateOrNull(v.medicalClearanceDate),
        medicalClearanceNotes: blank(v.medicalClearanceNotes),
        requiresWorkplaceModifications: v.requiresWorkplaceModifications,
        workplaceModificationsDescription: blank(v.workplaceModificationsDescription),
        status: v.status as SheReturnToWorkStatus,
        coordinatorId: blank(v.coordinatorId),
        supervisorId: blank(v.supervisorId),
        completionDate: dateOrNull(v.completionDate),
        successfullyCompleted: v.successfullyCompleted,
        completionNotes: blank(v.completionNotes),
      });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'safety-rtw'] });
      toast({ title: 'Plan updated' });
      setEditOpen(false);
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Updating failed.',
        variant: 'destructive',
      });
    } finally {
      setBusy(false);
    }
  });

  if (!plan) return null;

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={`${plan.planNumber} — ${plan.employeeName}`}
        description={
          plan.safetyIncidentNumber
            ? `Following incident ${plan.safetyIncidentNumber}`
            : 'Not linked to a recorded incident'
        }
        backHref="/hr/safety/return-to-work"
        actions={
          <Button variant="outline" onClick={openEdit}>
            <Pencil className="mr-2 h-4 w-4" /> Edit / progress
          </Button>
        }
      />

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-3 text-base">
            <StatusBadge status={statusLabel(plan.status)} />
            {plan.status === 'PendingMedicalClearance' && (
              <Badge variant="outline">Awaiting clearance</Badge>
            )}
            {plan.status === 'Completed' && (
              <Badge variant={plan.successfullyCompleted ? 'secondary' : 'destructive'}>
                {plan.successfullyCompleted ? 'Successful' : 'Unsuccessful'}
              </Badge>
            )}
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid grid-cols-2 gap-4 md:grid-cols-4">
            <Fact label="Plan date" value={fmtDate(plan.planDate)} />
            <Fact label="Planned return" value={fmtDate(plan.plannedReturnDate)} />
            <Fact label="Actual return" value={fmtDate(plan.actualReturnDate)} />
            <Fact label="Medical clearance" value={fmtDate(plan.medicalClearanceDate)} />
            <Fact label="Coordinator" value={plan.coordinatorName} />
            <Fact label="Supervisor" value={plan.supervisorName} />
            <Fact
              label="Workplace modifications"
              value={plan.requiresWorkplaceModifications ? 'Required' : 'Not required'}
            />
            <Fact label="Completed" value={fmtDate(plan.completionDate)} />
          </div>
          {plan.medicalRestrictions && (
            <div>
              <div className="text-muted-foreground mb-1 text-xs">Medical restrictions</div>
              <p className="whitespace-pre-wrap text-sm">{plan.medicalRestrictions}</p>
            </div>
          )}
          {plan.medicalClearanceNotes && (
            <div>
              <div className="text-muted-foreground mb-1 text-xs">Clearance notes</div>
              <p className="whitespace-pre-wrap text-sm">{plan.medicalClearanceNotes}</p>
            </div>
          )}
          {plan.workplaceModificationsDescription && (
            <div>
              <div className="text-muted-foreground mb-1 text-xs">Modifications</div>
              <p className="whitespace-pre-wrap text-sm">{plan.workplaceModificationsDescription}</p>
            </div>
          )}
        </CardContent>
      </Card>

      <Tabs defaultValue="phases">
        <TabsList>
          <TabsTrigger value="phases">Phases ({plan.phases.length})</TabsTrigger>
          <TabsTrigger value="reviews">Reviews ({plan.reviews.length})</TabsTrigger>
        </TabsList>

        {/* ── Phases ── */}
        <TabsContent value="phases" className="mt-4">
          <ResourceCollectionTab<SheReturnToWorkPhase, PhaseForm>
            parentId={id}
            title="phases"
            singular="phase"
            queryKey={[...planKey, 'phases']}
            invalidateKeys={[planKey]}
            list={async () =>
              (await safetyReturnToWorkService.getById(id)).phases.sort(
                (a, b) => a.phaseNumber - b.phaseNumber,
              )
            }
            create={(planId, values) => {
              const v = phaseSchema.parse(values);
              return safetyReturnToWorkService.addPhase(planId, {
                returnToWorkPlanId: planId,
                phaseName: v.phaseName,
                startDate: new Date(v.startDate).toISOString(),
                endDate: dateOrNull(v.endDate),
                requiresReducedHours: v.requiresReducedHours,
                hoursPerDay: v.hoursPerDay ?? null,
                daysPerWeek: v.daysPerWeek ?? null,
                duties: v.duties,
                restrictions: v.restrictions,
                assessmentDate: new Date(v.assessmentDate).toISOString(),
                assessedById: v.assessedById,
              });
            }}
            update={(_planId, phaseId, values) => {
              const v = phaseSchema.parse(values);
              return safetyReturnToWorkService.updatePhase(phaseId, {
                id: phaseId,
                phaseName: v.phaseName,
                startDate: new Date(v.startDate).toISOString(),
                endDate: dateOrNull(v.endDate),
                requiresReducedHours: v.requiresReducedHours,
                hoursPerDay: v.hoursPerDay ?? null,
                daysPerWeek: v.daysPerWeek ?? null,
                duties: v.duties,
                restrictions: v.restrictions,
                assessmentDate: new Date(v.assessmentDate).toISOString(),
                employeeProgress: blank(v.employeeProgress),
                challengesFaced: blank(v.challengesFaced),
                accommodationsEffectiveness: blank(v.accommodationsEffectiveness),
                recommendedAdjustments: blank(v.recommendedAdjustments),
                employeeFeedback: blank(v.employeeFeedback),
                phaseCompleted: v.phaseCompleted,
                actualEndDate: dateOrNull(v.actualEndDate),
                canContinuePlan: v.canContinuePlan,
                completionNotes: blank(v.completionNotes),
              });
            }}
            remove={(_planId, phaseId) => safetyReturnToWorkService.removePhase(phaseId)}
            columns={[
              { header: '#', cell: (p) => p.phaseNumber },
              { header: 'Phase', cell: (p) => <span className="font-medium">{p.phaseName}</span> },
              {
                header: 'Runs',
                cell: (p) => `${fmtDate(p.startDate)} – ${fmtDate(p.endDate)}`,
              },
              {
                header: 'Hours',
                cell: (p) =>
                  p.requiresReducedHours
                    ? `${p.hoursPerDay ?? '—'} h × ${p.daysPerWeek ?? '—'} d`
                    : 'Full time',
              },
              { header: 'Assessed by', cell: (p) => p.assessedByName },
              {
                header: 'Progress',
                cell: (p) =>
                  p.phaseCompleted ? (
                    <Badge variant="secondary">Completed</Badge>
                  ) : (
                    <Badge variant="outline">In progress</Badge>
                  ),
              },
            ]}
            schema={phaseSchema}
            emptyForm={{
              phaseName: '',
              startDate: new Date().toISOString().slice(0, 10),
              endDate: '',
              requiresReducedHours: false,
              hoursPerDay: undefined,
              daysPerWeek: undefined,
              duties: '',
              restrictions: '',
              assessmentDate: new Date().toISOString().slice(0, 10),
              assessedById: '',
              employeeProgress: '',
              challengesFaced: '',
              accommodationsEffectiveness: '',
              recommendedAdjustments: '',
              employeeFeedback: '',
              phaseCompleted: false,
              actualEndDate: '',
              canContinuePlan: true,
              completionNotes: '',
            }}
            toForm={(p) => ({
              phaseName: p.phaseName,
              startDate: p.startDate.slice(0, 10),
              endDate: p.endDate?.slice(0, 10) ?? '',
              requiresReducedHours: p.requiresReducedHours,
              hoursPerDay: p.hoursPerDay ?? undefined,
              daysPerWeek: p.daysPerWeek ?? undefined,
              duties: p.duties,
              restrictions: p.restrictions,
              assessmentDate: p.assessmentDate.slice(0, 10),
              assessedById: p.assessedById,
              employeeProgress: p.employeeProgress ?? '',
              challengesFaced: p.challengesFaced ?? '',
              accommodationsEffectiveness: p.accommodationsEffectiveness ?? '',
              recommendedAdjustments: p.recommendedAdjustments ?? '',
              employeeFeedback: p.employeeFeedback ?? '',
              phaseCompleted: p.phaseCompleted,
              actualEndDate: p.actualEndDate?.slice(0, 10) ?? '',
              canContinuePlan: p.canContinuePlan,
              completionNotes: p.completionNotes ?? '',
            })}
            renderFields={(f, editing) => (
              <>
                <FieldRow>
                  <TextField form={f} name="phaseName" label="Phase name" required />
                  <DateField form={f} name="assessmentDate" label="Assessment date" required />
                </FieldRow>
                {!editing ? (
                  <EmployeePickerField form={f} name="assessedById" label="Assessed by" required />
                ) : (
                  <div className="text-muted-foreground text-sm">
                    The phase number and assessor are fixed once recorded.
                  </div>
                )}
                <FieldRow>
                  <DateField form={f} name="startDate" label="Starts" required />
                  <DateField form={f} name="endDate" label="Planned end" />
                </FieldRow>
                <SwitchField form={f} name="requiresReducedHours" label="Reduced hours" />
                <FieldRow>
                  <NumberField form={f} name="hoursPerDay" label="Hours per day" />
                  <NumberField form={f} name="daysPerWeek" label="Days per week" />
                </FieldRow>
                <TextareaField form={f} name="duties" label="Duties" rows={2} required />
                <TextareaField form={f} name="restrictions" label="Restrictions" rows={2} required />
                {editing && (
                  <>
                    <TextareaField form={f} name="employeeProgress" label="Employee progress" rows={2} />
                    <TextareaField form={f} name="challengesFaced" label="Challenges faced" rows={2} />
                    <TextareaField
                      form={f}
                      name="accommodationsEffectiveness"
                      label="Accommodations effectiveness"
                      rows={2}
                    />
                    <TextareaField
                      form={f}
                      name="recommendedAdjustments"
                      label="Recommended adjustments"
                      rows={2}
                    />
                    <TextareaField form={f} name="employeeFeedback" label="Employee feedback" rows={2} />
                    <FieldRow>
                      <SwitchField form={f} name="phaseCompleted" label="Phase completed" />
                      <SwitchField form={f} name="canContinuePlan" label="Can continue plan" />
                    </FieldRow>
                    <FieldRow>
                      <DateField form={f} name="actualEndDate" label="Actual end" />
                      <div />
                    </FieldRow>
                    <TextareaField form={f} name="completionNotes" label="Completion notes" rows={2} />
                  </>
                )}
              </>
            )}
            getId={(p) => p.id}
            dialogClassName="sm:max-w-[640px]"
            emptyDescription="Phased duties on the way back to full work. Numbers are assigned in order; a deleted phase keeps its number."
          />
        </TabsContent>

        {/* ── Reviews (add-only) ── */}
        <TabsContent value="reviews" className="mt-4">
          <ResourceCollectionTab<SheReturnToWorkReview, ReviewForm>
            parentId={id}
            title="reviews"
            singular="review"
            queryKey={[...planKey, 'reviews']}
            invalidateKeys={[planKey]}
            list={() => safetyReturnToWorkService.getReviews(id)}
            create={(planId, values) => {
              const v = reviewSchema.parse(values);
              return safetyReturnToWorkService.addReview(planId, {
                returnToWorkPlanId: planId,
                reviewDate: new Date(v.reviewDate).toISOString(),
                employeeCondition: blank(v.employeeCondition),
                workProgress: blank(v.workProgress),
                issuesIdentified: blank(v.issuesIdentified),
                recommendedActions: blank(v.recommendedActions),
                reviewedById: v.reviewedById,
                nextReviewDate: dateOrNull(v.nextReviewDate),
              });
            }}
            update={() => Promise.reject(new Error('Reviews are add-only.'))}
            allowUpdate={false}
            columns={[
              { header: '#', cell: (r) => r.reviewNumber },
              { header: 'Date', cell: (r) => fmtDate(r.reviewDate) },
              { header: 'Reviewed by', cell: (r) => r.reviewedByName },
              { header: 'Condition', cell: (r) => r.employeeCondition ?? '—' },
              { header: 'Progress', cell: (r) => r.workProgress ?? '—' },
              { header: 'Issues', cell: (r) => r.issuesIdentified ?? '—' },
              { header: 'Next review', cell: (r) => fmtDate(r.nextReviewDate) },
            ]}
            schema={reviewSchema}
            emptyForm={{
              reviewDate: new Date().toISOString().slice(0, 10),
              employeeCondition: '',
              workProgress: '',
              issuesIdentified: '',
              recommendedActions: '',
              reviewedById: '',
              nextReviewDate: '',
            }}
            toForm={(r) => ({
              reviewDate: r.reviewDate.slice(0, 10),
              employeeCondition: r.employeeCondition ?? '',
              workProgress: r.workProgress ?? '',
              issuesIdentified: r.issuesIdentified ?? '',
              recommendedActions: r.recommendedActions ?? '',
              reviewedById: r.reviewedById,
              nextReviewDate: r.nextReviewDate?.slice(0, 10) ?? '',
            })}
            renderFields={(f) => (
              <>
                <FieldRow>
                  <DateField form={f} name="reviewDate" label="Review date" required />
                  <DateField form={f} name="nextReviewDate" label="Next review" />
                </FieldRow>
                <EmployeePickerField form={f} name="reviewedById" label="Reviewed by" required />
                <TextareaField form={f} name="employeeCondition" label="Employee condition" rows={2} />
                <TextareaField form={f} name="workProgress" label="Work progress" rows={2} />
                <TextareaField form={f} name="issuesIdentified" label="Issues identified" rows={2} />
                <TextareaField
                  form={f}
                  name="recommendedActions"
                  label="Recommended actions"
                  rows={2}
                />
              </>
            )}
            getId={(r) => r.id}
            emptyDescription="Periodic check-ins on the plan. Reviews are numbered in order and cannot be edited once recorded."
          />
        </TabsContent>
      </Tabs>

      {/* ── Edit dialog ── */}
      <Dialog open={editOpen} onOpenChange={(o) => !busy && setEditOpen(o)}>
        <DialogContent className="max-h-[90vh] max-w-2xl overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Edit plan</DialogTitle>
            <DialogDescription>
              {plan.planNumber} — the number and employee cannot change. Completion is recorded
              here (status, dates and outcome).
            </DialogDescription>
          </DialogHeader>
          <form onSubmit={submitEdit} className="space-y-4">
            <FieldRow>
              <DateField form={editForm} name="planDate" label="Plan date" required />
              <SelectField
                form={editForm}
                name="status"
                label="Status"
                required
                options={SHE_RETURN_TO_WORK_STATUS_OPTIONS}
              />
            </FieldRow>
            <FieldRow>
              <DateField form={editForm} name="plannedReturnDate" label="Planned return" />
              <DateField form={editForm} name="actualReturnDate" label="Actual return" />
            </FieldRow>
            <TextareaField
              form={editForm}
              name="medicalRestrictions"
              label="Medical restrictions"
              rows={2}
            />
            <FieldRow>
              <DateField form={editForm} name="medicalClearanceDate" label="Medical clearance" />
              <div />
            </FieldRow>
            <TextareaField
              form={editForm}
              name="medicalClearanceNotes"
              label="Clearance notes"
              rows={2}
            />
            <SwitchField
              form={editForm}
              name="requiresWorkplaceModifications"
              label="Requires workplace modifications"
            />
            <TextareaField
              form={editForm}
              name="workplaceModificationsDescription"
              label="Modifications description"
              rows={2}
            />
            <FieldRow>
              <EmployeePickerField
                form={editForm}
                name="coordinatorId"
                label="Coordinator"
                initialLabel={plan.coordinatorName ?? undefined}
              />
              <EmployeePickerField
                form={editForm}
                name="supervisorId"
                label="Supervisor"
                initialLabel={plan.supervisorName ?? undefined}
              />
            </FieldRow>
            <FieldRow>
              <DateField form={editForm} name="completionDate" label="Completion date" />
              <SwitchField form={editForm} name="successfullyCompleted" label="Successful" />
            </FieldRow>
            <TextareaField form={editForm} name="completionNotes" label="Completion notes" rows={2} />
            <DialogFooter>
              <Button
                type="button"
                variant="outline"
                disabled={busy}
                onClick={() => setEditOpen(false)}
              >
                Cancel
              </Button>
              <Button type="submit" disabled={busy}>
                {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                Save
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>
    </div>
  );
}
