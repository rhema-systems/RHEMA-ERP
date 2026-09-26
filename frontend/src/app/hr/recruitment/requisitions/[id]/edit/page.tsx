'use client';

import { useEffect, useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2, Save } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { useToast } from '@/hooks/use-toast';
import { employeePositionService } from '@/services/hr/employee-position.service';
import { staffRequisitionService } from '@/services/hr/recruitment.service';
import {
  RequisitionFormFields,
  emptyRequisitionForm,
  type RequisitionFormState,
} from '@/components/hr/recruitment/RequisitionFormFields';

const dateInput = (v?: string | null) => (v ? v.slice(0, 10) : '');

/**
 * Editing a requisition. The server allows this only while it is **Draft or Rejected** — a
 * requisition out for approval, or already approved, is fixed. A rejected one is editable on
 * purpose: rework and resubmit is the loop that status exists for.
 */
export default function EditRequisitionPage() {
  const router = useRouter();
  const params = useParams();
  const id = (params?.id as string) ?? '';
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [form, setForm] = useState<RequisitionFormState>(emptyRequisitionForm);

  const { data, isLoading, isError } = useQuery({
    queryKey: ['hr', 'requisitions', id],
    queryFn: () => staffRequisitionService.getById(id),
    enabled: !!id,
  });

  const positions = useQuery({
    queryKey: ['hr', 'positions', 'all'],
    queryFn: () => employeePositionService.getAll(),
  });

  useEffect(() => {
    if (!data) return;
    setForm({
      positionId: data.positionId,
      jobDescriptionId: data.jobDescriptionId ?? '',
      locationId: data.locationId ?? '',
      type: data.type,
      priority: data.priority,
      requisitionTitle: data.requisitionTitle,
      description: data.description ?? '',
      numberOfPositions: data.numberOfPositions,
      replacementForEmployeeId: data.replacementForEmployeeId ?? null,
      replacementForEmployeeName: data.replacementForEmployeeName ?? null,
      replacementReason: data.replacementReason ?? '',
      employeeDepartureDate: dateInput(data.employeeDepartureDate),
      desiredStartDate: dateInput(data.desiredStartDate),
      latestAcceptableStartDate: dateInput(data.latestAcceptableStartDate),
      targetFillDate: dateInput(data.targetFillDate),
      businessJustification: data.businessJustification,
      impactIfNotFilled: data.impactIfNotFilled ?? '',
      manpowerBudgetLineId: data.manpowerBudgetLineId ?? '',
      exceptionJustification: data.exceptionJustification ?? '',
      allowInternalCandidates: data.allowInternalCandidates,
      allowExternalCandidates: data.allowExternalCandidates,
      notes: data.notes ?? '',
    });
  }, [data]);

  const position = (positions.data ?? []).find((p) => p.id === form.positionId);

  const save = useMutation({
    mutationFn: () =>
      staffRequisitionService.update(id, {
        id,
        positionId: form.positionId,
        jobDescriptionId: form.jobDescriptionId || null,
        organizationUnitId: position?.organizationUnitId ?? null,
        organizationLevelId: position?.organizationLevelId ?? null,
        locationId: form.locationId || null,
        type: form.type,
        priority: form.priority,
        requisitionTitle: form.requisitionTitle.trim(),
        description: form.description.trim() || null,
        numberOfPositions: form.numberOfPositions,
        replacementForEmployeeId: form.type === 'Replacement' ? form.replacementForEmployeeId : null,
        replacementReason: form.type === 'Replacement' ? form.replacementReason.trim() || null : null,
        employeeDepartureDate: form.type === 'Replacement' ? form.employeeDepartureDate || null : null,
        desiredStartDate: form.desiredStartDate,
        latestAcceptableStartDate: form.latestAcceptableStartDate || null,
        targetFillDate: form.targetFillDate || null,
        businessJustification: form.businessJustification.trim(),
        impactIfNotFilled: form.impactIfNotFilled.trim() || null,
        manpowerBudgetLineId: form.manpowerBudgetLineId || null,
        exceptionJustification: form.exceptionJustification.trim() || null,
        allowInternalCandidates: form.allowInternalCandidates,
        allowExternalCandidates: form.allowExternalCandidates,
        notes: form.notes.trim() || null,
      }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['hr', 'requisitions'] });
      toast({ title: 'Saved' });
      router.push(`/hr/recruitment/requisitions/${id}`);
    },
    onError: (e: any) =>
      toast({ title: 'Could not save', description: e?.message || 'Something went wrong.', variant: 'destructive' }),
  });

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-24">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (isError || !data) {
    return (
      <div className="p-6">
        <EmptyState title="Requisition not found" description="It may have been removed." />
      </div>
    );
  }

  const editable = data.status === 'Draft' || data.status === 'Rejected';
  const audienceChosen = form.allowInternalCandidates || form.allowExternalCandidates;
  const canSave =
    editable &&
    !!form.positionId &&
    !!form.requisitionTitle.trim() &&
    !!form.businessJustification.trim() &&
    !!form.desiredStartDate &&
    // G-4.7 (2026-09-15): the *new* page has always required this and *edit* did not. The server
    // carries [Range(1, 100)] on both DTOs so the write was refused either way — but on edit it
    // surfaced as a raw ModelState 400 instead of a disabled button.
    form.numberOfPositions >= 1 &&
    audienceChosen;

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={`Edit ${data.requisitionNumber}`}
        description={data.requisitionTitle}
        backHref={`/hr/recruitment/requisitions/${id}`}
        actions={
          <Button onClick={() => save.mutate()} disabled={!canSave || save.isPending}>
            <Save className="mr-2 h-4 w-4" />
            {save.isPending ? 'Saving…' : 'Save'}
          </Button>
        }
      />

      {/* ⚠ Until 2026-09-15 this sentence was advice for a control that did not exist (G-4.3):
          recall was built server-side, had a client method, and had no button anywhere in the
          frontend. It is now on the requisition's own page, for the person who raised it. */}
      {!editable && (
        <EmptyState
          title={`A ${data.status} requisition cannot be edited`}
          description="Only drafts and rejected requisitions can be changed. If it is still awaiting approval, open it and use Recall to take it back."
        />
      )}

      {editable && (
        <>
          <RequisitionFormFields
            value={form}
            onChange={setForm}
            positionLocked={!!data.jobVacancyId}
            requisitionId={id}
          />
          <div className="flex justify-end gap-2">
            <Button variant="outline" onClick={() => router.push(`/hr/recruitment/requisitions/${id}`)}>
              Cancel
            </Button>
            <Button onClick={() => save.mutate()} disabled={!canSave || save.isPending}>
              <Save className="mr-2 h-4 w-4" />
              {save.isPending ? 'Saving…' : 'Save'}
            </Button>
          </div>
        </>
      )}
    </div>
  );
}
