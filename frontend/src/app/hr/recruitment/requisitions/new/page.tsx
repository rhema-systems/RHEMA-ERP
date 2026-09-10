'use client';

import { useState } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import { useMutation, useQuery } from '@tanstack/react-query';
import { Info, Save } from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { useToast } from '@/hooks/use-toast';
import { dateOffset } from '@/lib/hr/attendance-format';
import { employeePositionService } from '@/services/hr/employee-position.service';
import { staffRequisitionService } from '@/services/hr/recruitment.service';
import {
  RequisitionFormFields,
  emptyRequisitionForm,
  type RequisitionFormState,
} from '@/components/hr/recruitment/RequisitionFormFields';

/**
 * Raising a requisition. It is saved as a **draft** — nothing goes to an approver until it is
 * submitted from the detail page, which is also where the budget check is shown.
 */
export default function NewRequisitionPage() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const { toast } = useToast();

  const [form, setForm] = useState<RequisitionFormState>(() => ({
    ...emptyRequisitionForm(),
    positionId: searchParams.get('positionId') ?? '',
    desiredStartDate: dateOffset(30),
  }));

  const positions = useQuery({
    queryKey: ['hr', 'positions', 'all'],
    queryFn: () => employeePositionService.getAll(),
  });

  // Org placement is taken from the chosen position rather than picked separately: the two must
  // agree, and the position is the record that already knows.
  const position = (positions.data ?? []).find((p) => p.id === form.positionId);

  const create = useMutation({
    mutationFn: () =>
      staffRequisitionService.create({
        positionId: form.positionId,
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
    onSuccess: (created) => {
      toast({ title: 'Requisition raised', description: `${created.requisitionNumber} saved as a draft.` });
      router.push(`/hr/recruitment/requisitions/${created.id}`);
    },
    onError: (e: any) =>
      toast({ title: 'Could not save', description: e?.message || 'Something went wrong.', variant: 'destructive' }),
  });

  const audienceChosen = form.allowInternalCandidates || form.allowExternalCandidates;
  const canSave =
    !!form.positionId &&
    !!form.requisitionTitle.trim() &&
    !!form.businessJustification.trim() &&
    !!form.desiredStartDate &&
    form.numberOfPositions >= 1 &&
    audienceChosen;

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Raise a requisition"
        description="Ask for headcount. It is saved as a draft until you send it for approval."
        backHref="/hr/recruitment/requisitions"
        actions={
          <Button onClick={() => create.mutate()} disabled={!canSave || create.isPending}>
            <Save className="mr-2 h-4 w-4" />
            {create.isPending ? 'Saving…' : 'Save draft'}
          </Button>
        }
      />

      <Alert>
        <Info className="h-4 w-4" />
        <AlertTitle>The organisation unit comes from the position</AlertTitle>
        <AlertDescription>
          Pick the position and the unit and level follow from it. Raise it against an approved
          manpower budget line where one covers the post; the check on the form says what the
          server will say at submit, against the budget and the establishment.
        </AlertDescription>
      </Alert>

      <RequisitionFormFields value={form} onChange={setForm} />

      <div className="flex justify-end gap-2">
        <Button variant="outline" onClick={() => router.push('/hr/recruitment/requisitions')}>
          Cancel
        </Button>
        <Button onClick={() => create.mutate()} disabled={!canSave || create.isPending}>
          <Save className="mr-2 h-4 w-4" />
          {create.isPending ? 'Saving…' : 'Save draft'}
        </Button>
      </div>
    </div>
  );
}
