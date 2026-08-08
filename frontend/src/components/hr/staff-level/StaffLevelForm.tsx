'use client';

import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Loader2, Save } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Switch } from '@/components/ui/switch';
import {
  Card,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';

export const staffLevelSchema = z.object({
  name: z.string().min(1, 'Name is required').max(100),
  code: z.string().max(50).optional().or(z.literal('')),
  rank: z.coerce.number().int('Must be a whole number').min(1, 'Must be at least 1'),
  description: z.string().max(1000).optional().or(z.literal('')),
  isActive: z.boolean(),
});

export type StaffLevelFormValues = z.infer<typeof staffLevelSchema>;

export const emptyStaffLevel: StaffLevelFormValues = {
  name: '',
  code: '',
  rank: 1,
  description: '',
  isActive: true,
};

interface StaffLevelFormProps {
  defaultValues: StaffLevelFormValues;
  onSubmit: (values: StaffLevelFormValues) => Promise<void>;
  submitting: boolean;
  submitLabel: string;
  onCancel: () => void;
  /** Hide the Active toggle on create (the server sets active by default). */
  showActive?: boolean;
}

export function StaffLevelForm({
  defaultValues,
  onSubmit,
  submitting,
  submitLabel,
  onCancel,
  showActive = true,
}: StaffLevelFormProps) {
  const form = useForm<StaffLevelFormValues>({
    resolver: zodResolver(staffLevelSchema) as any,
    defaultValues,
  });

  return (
    <Card className="max-w-2xl">
      <form onSubmit={form.handleSubmit(onSubmit)}>
        <CardHeader>
          <CardTitle>Staff Level Details</CardTitle>
          <CardDescription>
            A ranked classification used to grade employees and positions.
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <div className="space-y-2">
              <Label htmlFor="name">Name</Label>
              <Input id="name" placeholder="Senior Manager" {...form.register('name')} />
              {form.formState.errors.name && (
                <p className="text-sm text-red-500">{form.formState.errors.name.message}</p>
              )}
            </div>
            <div className="space-y-2">
              <Label htmlFor="code">Code</Label>
              <Input id="code" placeholder="SM" {...form.register('code')} />
              {form.formState.errors.code && (
                <p className="text-sm text-red-500">{form.formState.errors.code.message}</p>
              )}
            </div>
          </div>

          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <div className="space-y-2">
              <Label htmlFor="rank">Rank</Label>
              <Input id="rank" type="number" min={1} {...form.register('rank')} />
              <p className="text-xs text-muted-foreground">
                Sort order — levels are listed by rank ascending. The seniority meaning is your own convention.
              </p>
              {form.formState.errors.rank && (
                <p className="text-sm text-red-500">{form.formState.errors.rank.message}</p>
              )}
            </div>
            {showActive && (
              <div className="flex items-end">
                <div className="flex w-full items-center justify-between rounded-md border px-3 py-2.5">
                  <Label htmlFor="isActive" className="cursor-pointer">
                    Active
                  </Label>
                  <Switch
                    id="isActive"
                    checked={form.watch('isActive')}
                    onCheckedChange={(v) => form.setValue('isActive', v)}
                  />
                </div>
              </div>
            )}
          </div>

          <div className="space-y-2">
            <Label htmlFor="description">Description</Label>
            <Textarea
              id="description"
              rows={3}
              placeholder="Optional description"
              {...form.register('description')}
            />
          </div>
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
