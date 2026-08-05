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
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import type { LocationStructureSummary } from '@/types/hr/location';

export const locationLevelSchema = z.object({
  name: z.string().min(1, 'Name is required').max(100),
  code: z.string().max(20).optional().or(z.literal('')),
  description: z.string().max(1000).optional().or(z.literal('')),
  levelNumber: z.coerce.number().int('Must be a whole number').min(1, 'Must be at least 1'),
  structureId: z.string().min(1, 'Structure is required'),
  requiresAddress: z.boolean(),
  requiresContactInfo: z.boolean(),
  allowsEmployeeAssignment: z.boolean(),
  isActive: z.boolean(),
});

export type LocationLevelFormValues = z.infer<typeof locationLevelSchema>;

export const emptyLocationLevel: LocationLevelFormValues = {
  name: '',
  code: '',
  description: '',
  levelNumber: 1,
  structureId: '',
  requiresAddress: false,
  requiresContactInfo: false,
  allowsEmployeeAssignment: false,
  isActive: true,
};

interface LocationLevelFormProps {
  structures: LocationStructureSummary[];
  defaultValues: LocationLevelFormValues;
  onSubmit: (values: LocationLevelFormValues) => Promise<void>;
  submitting: boolean;
  submitLabel: string;
  onCancel: () => void;
}

export function LocationLevelForm({
  structures,
  defaultValues,
  onSubmit,
  submitting,
  submitLabel,
  onCancel,
}: LocationLevelFormProps) {
  const form = useForm<LocationLevelFormValues>({
    resolver: zodResolver(locationLevelSchema) as any,
    defaultValues,
  });

  const requiresAddress = form.watch('requiresAddress');
  const requiresContactInfo = form.watch('requiresContactInfo');
  const allowsEmployeeAssignment = form.watch('allowsEmployeeAssignment');
  const isActive = form.watch('isActive');

  return (
    <Card className="max-w-3xl">
      <form onSubmit={form.handleSubmit(onSubmit)}>
        <CardHeader>
          <CardTitle>Level Details</CardTitle>
          <CardDescription>
            A level defines a tier in the location hierarchy (e.g. Region, Site, Building).
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label htmlFor="name">Name</Label>
              <Input id="name" placeholder="Site / Office" {...form.register('name')} />
              {form.formState.errors.name && (
                <p className="text-sm text-red-500">{form.formState.errors.name.message}</p>
              )}
            </div>
            <div className="space-y-2">
              <Label htmlFor="code">Code</Label>
              <Input id="code" placeholder="SITE" {...form.register('code')} />
              {form.formState.errors.code && (
                <p className="text-sm text-red-500">{form.formState.errors.code.message}</p>
              )}
            </div>
          </div>

          <div className="grid grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label htmlFor="levelNumber">Level Number</Label>
              <Input id="levelNumber" type="number" min={1} max={100} {...form.register('levelNumber')} />
              <p className="text-xs text-muted-foreground">1 = highest tier.</p>
              {form.formState.errors.levelNumber && (
                <p className="text-sm text-red-500">{form.formState.errors.levelNumber.message}</p>
              )}
            </div>
            <div className="space-y-2">
              <Label htmlFor="structureId">Structure</Label>
              <Select
                value={form.watch('structureId') || undefined}
                onValueChange={(value) => form.setValue('structureId', value, { shouldValidate: true })}
              >
                <SelectTrigger id="structureId">
                  <SelectValue placeholder="Select a structure" />
                </SelectTrigger>
                <SelectContent>
                  {structures.map((s) => (
                    <SelectItem key={s.id} value={s.id}>
                      {s.name}
                      {s.isDefault ? ' (Default)' : ''}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              {form.formState.errors.structureId && (
                <p className="text-sm text-red-500">{form.formState.errors.structureId.message}</p>
              )}
            </div>
          </div>

          <div className="space-y-2">
            <Label htmlFor="description">Description</Label>
            <Textarea id="description" rows={3} placeholder="Optional description" {...form.register('description')} />
          </div>

          <div className="space-y-4 rounded-md border p-4">
            <div className="flex items-center justify-between">
              <div className="space-y-0.5">
                <Label htmlFor="requiresAddress">Requires Address</Label>
                <p className="text-xs text-muted-foreground">Locations at this level must have an address.</p>
              </div>
              <Switch
                id="requiresAddress"
                checked={requiresAddress}
                onCheckedChange={(v) => form.setValue('requiresAddress', v)}
              />
            </div>
            <div className="flex items-center justify-between">
              <div className="space-y-0.5">
                <Label htmlFor="requiresContactInfo">Requires Contact Info</Label>
                <p className="text-xs text-muted-foreground">Locations at this level must have contact details.</p>
              </div>
              <Switch
                id="requiresContactInfo"
                checked={requiresContactInfo}
                onCheckedChange={(v) => form.setValue('requiresContactInfo', v)}
              />
            </div>
            <div className="flex items-center justify-between">
              <div className="space-y-0.5">
                <Label htmlFor="allowsEmployeeAssignment">Allows Employee Assignment</Label>
                <p className="text-xs text-muted-foreground">Employees can be based at locations of this level.</p>
              </div>
              <Switch
                id="allowsEmployeeAssignment"
                checked={allowsEmployeeAssignment}
                onCheckedChange={(v) => form.setValue('allowsEmployeeAssignment', v)}
              />
            </div>
            <div className="flex items-center justify-between">
              <Label htmlFor="isActive">Active</Label>
              <Switch
                id="isActive"
                checked={isActive}
                onCheckedChange={(v) => form.setValue('isActive', v)}
              />
            </div>
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
