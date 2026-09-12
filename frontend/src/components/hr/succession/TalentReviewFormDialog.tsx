'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useRouter } from 'next/navigation';
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
  TextField,
  TextareaField,
} from '@/components/hr/employee/tabs/fields';
import { organizationUnitService } from '@/services/hr/organization-unit.service';
import { talentReviewService } from '@/services/hr/succession.service';

const orNull = (v?: string) => (v && v.trim() !== '' ? v : null);

const schema = z.object({
  sessionName: z.string().min(1, 'Required').max(150),
  reviewYear: z.coerce.number().int().min(2000).max(2100),
  sessionDate: z.string().min(1, 'Required'),
  location: z.string().max(200).optional(),
  facilitatedById: z.string().optional(),
  agenda: z.string().max(4000).optional(),
  organizationUnitId: z.string().optional(),
});

type FormValues = z.input<typeof schema>;

/**
 * Create a calibration session.
 *
 * ⚠ `facilitatedById` is on the payload deliberately — who chaired the meeting is a fact being
 * recorded, and an HR desk may enter a session the MD ran. Contrast the *finalizer*, which is
 * absent: closing a session is an act performed by whoever clicks the button.
 */
export function TalentReviewFormDialog({
  open,
  onOpenChange,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}) {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const { data: units } = useQuery({
    queryKey: ['organization-units'],
    queryFn: () => organizationUnitService.getAll(),
  });

  const form = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      sessionName: '',
      reviewYear: new Date().getFullYear(),
      sessionDate: new Date().toISOString().slice(0, 10),
      location: '',
      facilitatedById: '',
      agenda: '',
      organizationUnitId: '',
    },
  });

  const create = useMutation({
    mutationFn: (values: FormValues) =>
      talentReviewService.create({
        sessionName: values.sessionName,
        reviewYear: Number(values.reviewYear),
        sessionDate: values.sessionDate,
        location: orNull(values.location),
        facilitatedById: orNull(values.facilitatedById),
        agenda: orNull(values.agenda),
        organizationUnitId: orNull(values.organizationUnitId),
      }),
    onSuccess: async (created) => {
      await queryClient.invalidateQueries({ queryKey: ['talent-reviews'] });
      onOpenChange(false);
      form.reset();
      toast({ title: `${created.sessionName} created` });
      router.push(`/hr/succession/reviews/${created.id}`);
    },
    onError: (error: any) =>
      toast({
        variant: 'destructive',
        title: error?.response?.status === 403 ? 'That is not yours to do' : 'Could not create the session',
        description: error?.response?.data?.detail ?? error?.message,
      }),
  });

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-2xl">
        <DialogHeader>
          <DialogTitle>New talent review session</DialogTitle>
          <DialogDescription>
            Rate people into the grid, calibrate the placements, then finalize. Finalizing freezes
            the session for good, so leave it open until the meeting has settled.
          </DialogDescription>
        </DialogHeader>

        <form className="space-y-4" onSubmit={form.handleSubmit((v) => create.mutate(v))}>
          <FieldRow>
            <TextField form={form} name="sessionName" label="Session name" required />
            <NumberField form={form} name="reviewYear" label="Review year" required />
          </FieldRow>
          <FieldRow>
            <DateField form={form} name="sessionDate" label="Session date" required />
            <TextField form={form} name="location" label="Location" />
          </FieldRow>
          <FieldRow>
            <EmployeePickerField form={form} name="facilitatedById" label="Facilitated by" />
            <SelectField
              form={form}
              name="organizationUnitId"
              label="Scope"
              options={(units ?? []).map((u: any) => ({ value: u.id, label: u.name }))}
              allowEmpty
              emptyLabel="Whole organisation"
            />
          </FieldRow>
          <TextareaField form={form} name="agenda" label="Agenda" />

          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
              Cancel
            </Button>
            <Button type="submit" disabled={create.isPending}>
              {create.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Create session
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
