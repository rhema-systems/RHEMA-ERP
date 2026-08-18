'use client';

import { useEffect } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
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
  SwitchField,
  TextField,
  TextareaField,
} from '@/components/hr/employee/tabs/fields';
import { employeePositionService } from '@/services/hr/employee-position.service';
import { talentPoolService, talentPoolTypeService } from '@/services/hr/succession.service';
import type { TalentPool } from '@/types/hr/succession';

const orNull = (v?: string) => (v && v.trim() !== '' ? v : null);

const schema = z
  .object({
    name: z.string().min(1, 'Required').max(150),
    description: z.string().max(2000).optional(),
    poolTypeId: z.string().min(1, 'Choose a pool type'),
    targetPositionId: z.string().optional(),
    targetSize: z.coerce.number().int().min(0).max(1000),
    validFrom: z.string().optional(),
    validTo: z.string().optional(),
    isActive: z.boolean(),
    ownerId: z.string().min(1, 'A pool needs an owner'),
  })
  .refine((v) => !v.validTo || !v.validFrom || v.validTo >= v.validFrom, {
    message: 'The end date cannot be before the start',
    path: ['validTo'],
  });

type FormValues = z.input<typeof schema>;

/**
 * Create or edit a talent pool.
 *
 * ⚠ `ownerId` is on the payload deliberately, unlike the actor fields removed elsewhere in this
 * area. The owner is a *business assignment* — which manager is accountable for this pool — not a
 * claim about who is acting, so the caller is the right source for it.
 */
export function TalentPoolFormDialog({
  open,
  onOpenChange,
  pool,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  pool?: TalentPool;
}) {
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const { data: types } = useQuery({
    queryKey: ['talent-pool-types'],
    queryFn: () => talentPoolTypeService.getAll(),
  });

  const { data: positions } = useQuery({
    queryKey: ['employee-positions', 'active'],
    queryFn: () => employeePositionService.getActive(),
  });

  const form = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      name: '',
      description: '',
      poolTypeId: '',
      targetPositionId: '',
      targetSize: 0,
      validFrom: '',
      validTo: '',
      isActive: true,
      ownerId: '',
    },
  });

  useEffect(() => {
    if (!open) return;
    form.reset({
      name: pool?.name ?? '',
      description: pool?.description ?? '',
      poolTypeId: pool?.poolTypeId ?? '',
      targetPositionId: pool?.targetPositionId ?? '',
      targetSize: pool?.targetSize ?? 0,
      validFrom: pool?.validFrom?.slice(0, 10) ?? '',
      validTo: pool?.validTo?.slice(0, 10) ?? '',
      isActive: pool?.isActive ?? true,
      ownerId: pool?.ownerId ?? '',
    });
  }, [open, pool, form]);

  const save = useMutation({
    mutationFn: (values: FormValues) => {
      const payload = {
        name: values.name,
        description: orNull(values.description),
        poolTypeId: values.poolTypeId,
        targetPositionId: orNull(values.targetPositionId),
        targetSize: Number(values.targetSize),
        validFrom: orNull(values.validFrom),
        validTo: orNull(values.validTo),
        isActive: values.isActive,
        ownerId: values.ownerId,
      };
      return pool
        ? talentPoolService.update(pool.id, { ...payload, id: pool.id })
        : talentPoolService.create(payload);
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['talent-pools'] });
      onOpenChange(false);
      toast({ title: pool ? 'Pool updated' : 'Pool created' });
    },
    onError: (error: any) => {
      toast({
        variant: 'destructive',
        title: error?.response?.status === 403 ? 'That is not yours to do' : 'Could not save the pool',
        description: error?.response?.data?.detail ?? error?.message,
      });
    },
  });

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[85vh] max-w-2xl overflow-y-auto">
        <DialogHeader>
          <DialogTitle>{pool ? 'Edit pool' : 'New talent pool'}</DialogTitle>
          <DialogDescription>
            A pool groups people being grown for something. Unlike a succession plan it is not tied
            to a single post — though it can name one it is feeding.
          </DialogDescription>
        </DialogHeader>

        <form className="space-y-4" onSubmit={form.handleSubmit((v) => save.mutate(v))}>
          <FieldRow>
            <TextField form={form} name="name" label="Pool name" required />
            <SelectField
              form={form}
              name="poolTypeId"
              label="Pool type"
              required
              options={(types ?? []).map((t) => ({ value: t.id, label: t.name }))}
            />
          </FieldRow>
          <FieldRow>
            <EmployeePickerField
              form={form}
              name="ownerId"
              label="Owner"
              required
              initialLabel={pool?.ownerName}
            />
            <NumberField form={form} name="targetSize" label="Target size" required />
          </FieldRow>
          <SelectField
            form={form}
            name="targetPositionId"
            label="Feeding which position"
            options={(positions ?? []).map((p) => ({ value: p.id, label: p.title }))}
            allowEmpty
            emptyLabel="Not tied to a position"
          />
          <FieldRow>
            <DateField form={form} name="validFrom" label="Valid from" />
            <DateField form={form} name="validTo" label="Valid to" />
          </FieldRow>
          <TextareaField form={form} name="description" label="Description" />
          <SwitchField form={form} name="isActive" label="Active" />

          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
              Cancel
            </Button>
            <Button type="submit" disabled={save.isPending}>
              {save.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              {pool ? 'Save changes' : 'Create pool'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
