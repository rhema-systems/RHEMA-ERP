'use client';

import { useEffect } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Loader2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { useToast } from '@/hooks/use-toast';
import { EmployeePickerField } from '@/components/hr/attendance/EmployeePickerField';
import {
  DateField,
  FieldRow,
  NumberField,
  SelectField,
  TextareaField,
} from '@/components/hr/employee/tabs/fields';
import { talentPoolService } from '@/services/hr/succession.service';
import type { TalentPool } from '@/types/hr/succession';

const READINESS = [
  'ReadyNow',
  'ReadyIn12Months',
  'ReadyIn24Months',
  'ReadyIn36PlusMonths',
  'NotReady',
] as const;

const spaced = (v: string) => v.replace(/([a-z])([A-Z0-9])/g, '$1 $2');
const orNull = (v?: string) => (v && v.trim() !== '' ? v : null);

const schema = z.object({
  employeeId: z.string().min(1, 'Choose an employee'),
  rank: z.coerce.number().int().min(1),
  readiness: z.enum(READINESS),
  readyByDate: z.string().optional(),
  justification: z.string().max(2000).optional(),
  strengths: z.string().max(2000).optional(),
  developmentGaps: z.string().max(2000).optional(),
  enrolledDate: z.string().min(1, 'Required'),
  nominationNotes: z.string().max(2000).optional(),
});

type FormValues = z.input<typeof schema>;

/**
 * Add someone to a talent pool.
 *
 * ⚠ There is no "nominated by" field: the nominator is the signed-in user. It used to be on the
 * payload and was honoured, so a desk actor could record a nomination in a colleague's name.
 *
 * ⚠ Adding someone who was previously removed **revives their old membership** rather than creating
 * a second row, so their original enrolment date and removal history survive. The server does that;
 * this form does not need to check first.
 */
export function TalentPoolMemberDialog({
  open,
  onOpenChange,
  pool,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  pool: TalentPool;
}) {
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const form = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      employeeId: '',
      rank: pool.currentMemberCount + 1,
      readiness: 'ReadyIn12Months',
      readyByDate: '',
      justification: '',
      strengths: '',
      developmentGaps: '',
      enrolledDate: new Date().toISOString().slice(0, 10),
      nominationNotes: '',
    },
  });

  useEffect(() => {
    if (open) form.setValue('rank', pool.currentMemberCount + 1);
  }, [open, pool.currentMemberCount, form]);

  const add = useMutation({
    mutationFn: (values: FormValues) =>
      talentPoolService.addMember(pool.id, {
        talentPoolId: pool.id,
        employeeId: values.employeeId,
        rank: Number(values.rank),
        readiness: values.readiness,
        readyByDate: orNull(values.readyByDate),
        justification: orNull(values.justification),
        strengths: orNull(values.strengths),
        developmentGaps: orNull(values.developmentGaps),
        enrolledDate: values.enrolledDate,
        nominationNotes: orNull(values.nominationNotes),
      }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['talent-pools'] });
      onOpenChange(false);
      form.reset();
      toast({ title: 'Member added to the pool' });
    },
    onError: (error: any) => {
      const status = error?.response?.status;
      toast({
        variant: 'destructive',
        // The 409 names the pool, and is the only thing that tells the user what happened.
        title:
          status === 409
            ? 'Already in this pool'
            : status === 403
              ? 'That is not yours to do'
              : 'Could not add the member',
        description: error?.response?.data?.detail ?? error?.message,
      });
    },
  });

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[85vh] max-w-2xl overflow-y-auto">
        <DialogHeader>
          <DialogTitle>Add to {pool.name}</DialogTitle>
          <DialogDescription>
            Recorded against your sign-in as the nominator — there is no field to name someone else.
          </DialogDescription>
        </DialogHeader>

        <form className="space-y-4" onSubmit={form.handleSubmit((v) => add.mutate(v))}>
          <FieldRow>
            <EmployeePickerField form={form} name="employeeId" label="Employee" required />
            <NumberField form={form} name="rank" label="Rank" required />
          </FieldRow>
          <FieldRow>
            <SelectField
              form={form}
              name="readiness"
              label="Readiness"
              required
              options={READINESS.map((r) => ({ value: r, label: spaced(r) }))}
            />
            <DateField form={form} name="readyByDate" label="Ready by" />
          </FieldRow>
          <FieldRow>
            <DateField form={form} name="enrolledDate" label="Enrolled" required />
            <div />
          </FieldRow>
          <TextareaField form={form} name="justification" label="Why they belong in this pool" />
          <TextareaField form={form} name="strengths" label="Strengths" />
          <TextareaField form={form} name="developmentGaps" label="Development gaps" />
          <TextareaField form={form} name="nominationNotes" label="Nomination notes" />

          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
              Cancel
            </Button>
            <Button type="submit" disabled={add.isPending}>
              {add.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Add to pool
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
