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
import type { OrganizationStructureSummary } from '@/types/hr/organization';

export const organizationLevelSchema = z.object({
  name: z.string().min(1, 'Name is required').max(100),
  code: z.string().max(20).optional().or(z.literal('')),
  levelNumber: z.coerce
    .number()
    .int('Must be a whole number')
    .min(1, 'Must be between 1 and 100')
    .max(100, 'Must be between 1 and 100'),
  structureId: z.string().min(1, 'Structure is required'),
  description: z.string().max(1000).optional().or(z.literal('')),
  requiresHead: z.boolean(),
  allowsDirectEmployees: z.boolean(),
  isActive: z.boolean(),
});

export type OrganizationLevelFormValues = z.infer<typeof organizationLevelSchema>;

export const emptyOrganizationLevel: OrganizationLevelFormValues = {
  name: '',
  code: '',
  levelNumber: 1,
  structureId: '',
  description: '',
  requiresHead: true,
  allowsDirectEmployees: true,
  isActive: true,
};

interface OrganizationLevelFormProps {
  structures: OrganizationStructureSummary[];
  defaultValues: OrganizationLevelFormValues;
  onSubmit: (values: OrganizationLevelFormValues) => Promise<void>;
  submitting: boolean;
  submitLabel: string;
  onCancel: () => void;
}

export function OrganizationLevelForm({
  structures,
  defaultValues,
  onSubmit,
  submitting,
  submitLabel,
  onCancel,
}: OrganizationLevelFormProps) {
  const form = useForm<OrganizationLevelFormValues>({
    resolver: zodResolver(organizationLevelSchema) as any,
    defaultValues,
  });

  const requiresHead = form.watch('requiresHead');
  const allowsDirectEmployees = form.watch('allowsDirectEmployees');
  const isActive = form.watch('isActive');

  return (
    <Card>
      <form onSubmit={form.handleSubmit(onSubmit)}>
        <CardHeader>
          <CardTitle>Level Details</CardTitle>
          <CardDescription>
            A level defines a tier in the organization hierarchy (e.g. Division, Department, Unit).
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label htmlFor="name">Name</Label>
              <Input id="name" placeholder="Division" {...form.register('name')} />
              {form.formState.errors.name && (
                <p className="text-sm text-red-500">{form.formState.errors.name.message}</p>
              )}
            </div>
            <div className="space-y-2">
              <Label htmlFor="code">Code</Label>
              <Input id="code" placeholder="DIV" {...form.register('code')} />
              {form.formState.errors.code && (
                <p className="text-sm text-red-500">{form.formState.errors.code.message}</p>
              )}
            </div>
          </div>

          <div className="grid grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label htmlFor="levelNumber">Level Number</Label>
              <Input
                id="levelNumber"
                type="number"
                min={1}
                max={100}
                {...form.register('levelNumber')}
              />
              <p className="text-xs text-muted-foreground">1 = highest tier (e.g. Board).</p>
              {form.formState.errors.levelNumber && (
                <p className="text-sm text-red-500">{form.formState.errors.levelNumber.message}</p>
              )}
            </div>
            <div className="space-y-2">
              <Label htmlFor="structureId">Structure</Label>
              <Select
                value={form.watch('structureId') || undefined}
                onValueChange={(value) =>
                  form.setValue('structureId', value, { shouldValidate: true })
                }
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
            <Textarea
              id="description"
              placeholder="Optional description"
              rows={3}
              {...form.register('description')}
            />
            {form.formState.errors.description && (
              <p className="text-sm text-red-500">{form.formState.errors.description.message}</p>
            )}
          </div>

          <div className="space-y-4 rounded-md border p-4">
            <div className="flex items-center justify-between">
              <div className="space-y-0.5">
                <Label htmlFor="requiresHead">Requires Head</Label>
                <p className="text-xs text-muted-foreground">
                  Units at this level must have a designated head.
                </p>
              </div>
              <Switch
                id="requiresHead"
                checked={requiresHead}
                onCheckedChange={(v) => form.setValue('requiresHead', v)}
              />
            </div>
            <div className="flex items-center justify-between">
              <div className="space-y-0.5">
                <Label htmlFor="allowsDirectEmployees">Allows Direct Employees</Label>
                <p className="text-xs text-muted-foreground">
                  Employees can be assigned directly to units at this level.
                </p>
              </div>
              <Switch
                id="allowsDirectEmployees"
                checked={allowsDirectEmployees}
                onCheckedChange={(v) => form.setValue('allowsDirectEmployees', v)}
              />
            </div>
            <div className="flex items-center justify-between">
              <div className="space-y-0.5">
                <Label htmlFor="isActive">Active</Label>
                <p className="text-xs text-muted-foreground">Inactive levels are hidden from selection.</p>
              </div>
              <Switch
                id="isActive"
                checked={isActive}
                onCheckedChange={(v) => form.setValue('isActive', v)}
              />
            </div>
          </div>
        </CardContent>
        <CardFooter className="flex justify-end space-x-2">
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
