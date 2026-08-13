'use client';

import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Loader2, Save } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardFooter, CardHeader, CardTitle } from '@/components/ui/card';
import { Label } from '@/components/ui/label';
import { TextField, TextareaField, SelectField, SwitchField, FieldRow } from '@/components/hr/employee/tabs/fields';
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import type { TrainingVendorSummary } from '@/types/hr/training';

export const trainerSchema = z.object({
  name: z.string().min(1, 'Name is required').max(200),
  employeeId: z.string().optional().or(z.literal('')),
  vendorId: z.string().optional().or(z.literal('')),
  bio: z.string().max(2000).optional().or(z.literal('')),
  contact: z.string().max(200).optional().or(z.literal('')),
  expertiseAreas: z.string().max(1000).optional().or(z.literal('')),
  isActive: z.boolean(),
});

export type TrainerFormValues = z.infer<typeof trainerSchema>;

export const emptyTrainer: TrainerFormValues = {
  name: '',
  employeeId: '',
  vendorId: '',
  bio: '',
  contact: '',
  expertiseAreas: '',
  isActive: true,
};

interface TrainerFormProps {
  defaultValues: TrainerFormValues;
  defaultEmployeeLabel?: string | null;
  vendors: TrainingVendorSummary[];
  onSubmit: (values: TrainerFormValues) => Promise<void>;
  submitting: boolean;
  submitLabel: string;
  onCancel: () => void;
  showActive?: boolean;
}

export function TrainerForm({
  defaultValues,
  defaultEmployeeLabel,
  vendors,
  onSubmit,
  submitting,
  submitLabel,
  onCancel,
  showActive = true,
}: TrainerFormProps) {
  const form = useForm<TrainerFormValues>({
    resolver: zodResolver(trainerSchema) as any,
    defaultValues,
  });

  const vendorOptions = vendors.map((v) => ({ value: v.id, label: v.name }));

  return (
    <Card className="max-w-2xl">
      <form onSubmit={form.handleSubmit(onSubmit)}>
        <CardHeader>
          <CardTitle>Trainer details</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <TextField form={form} name="name" label="Name" required />

          <div className="space-y-2">
            <Label>Linked employee (internal trainer)</Label>
            <EmployeePicker
              value={form.watch('employeeId') || null}
              initialLabel={defaultEmployeeLabel}
              onChange={(id) => form.setValue('employeeId', id ?? '')}
              placeholder="Search employees…"
            />
            <p className="text-xs text-muted-foreground">
              Leave blank for a purely external trainer.
            </p>
          </div>

          <SelectField
            form={form}
            name="vendorId"
            label="Vendor (external trainer)"
            options={vendorOptions}
            allowEmpty
            emptyLabel="No vendor — internal trainer"
          />

          <TextareaField form={form} name="bio" label="Bio" rows={3} />
          <FieldRow>
            <TextField form={form} name="contact" label="Direct contact" placeholder="Phone or personal email" />
            <TextField form={form} name="expertiseAreas" label="Expertise areas" />
          </FieldRow>

          {showActive && <SwitchField form={form} name="isActive" label="Active" />}
        </CardContent>
        <CardFooter className="flex justify-end gap-2">
          <Button variant="outline" type="button" onClick={onCancel} disabled={submitting}>
            Cancel
          </Button>
          <Button type="submit" disabled={submitting}>
            {submitting ? (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            ) : (
              <Save className="mr-2 h-4 w-4" />
            )}
            {submitLabel}
          </Button>
        </CardFooter>
      </form>
    </Card>
  );
}
