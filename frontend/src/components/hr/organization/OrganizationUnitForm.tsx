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
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import type { OrganizationLevel, OrganizationUnitSummary } from '@/types/hr/organization';

const NO_PARENT = 'none';

export const organizationUnitSchema = z.object({
  name: z.string().min(1, 'Name is required').max(200),
  code: z.string().max(50).optional().or(z.literal('')),
  accountCode: z.string().max(100).optional().or(z.literal('')),
  description: z.string().max(1000).optional().or(z.literal('')),
  organizationLevelId: z.string().min(1, 'Level is required'),
  parentUnitId: z.string().optional().or(z.literal('')),
  headEmployeeId: z.string().optional().or(z.literal('')),
  sequence: z.coerce.number().int('Must be a whole number').min(1, 'Must be at least 1'),
  isActive: z.boolean(),
});

export type OrganizationUnitFormValues = z.infer<typeof organizationUnitSchema>;

export const emptyOrganizationUnit: OrganizationUnitFormValues = {
  name: '',
  code: '',
  accountCode: '',
  description: '',
  organizationLevelId: '',
  parentUnitId: '',
  headEmployeeId: '',
  sequence: 1,
  isActive: true,
};

interface OrganizationUnitFormProps {
  levels: OrganizationLevel[];
  units: OrganizationUnitSummary[];
  defaultValues: OrganizationUnitFormValues;
  onSubmit: (values: OrganizationUnitFormValues) => Promise<void>;
  submitting: boolean;
  submitLabel: string;
  onCancel: () => void;
  /** Level cannot be changed once a unit exists (enforced server-side). */
  isEdit?: boolean;
  /** Name of the currently-assigned head, to seed the picker on edit. */
  initialHeadLabel?: string | null;
}

export function OrganizationUnitForm({
  levels,
  units,
  defaultValues,
  onSubmit,
  submitting,
  submitLabel,
  onCancel,
  isEdit,
  initialHeadLabel,
}: OrganizationUnitFormProps) {
  const form = useForm<OrganizationUnitFormValues>({
    resolver: zodResolver(organizationUnitSchema) as any,
    defaultValues,
  });

  const isActive = form.watch('isActive');
  const selectedLevelId = form.watch('organizationLevelId');
  const parentValue = form.watch('parentUnitId') || NO_PARENT;
  const headEmployeeId = form.watch('headEmployeeId') || null;

  const selectedLevel = levels.find((l) => l.id === selectedLevelId);
  const levelRequiresHead = selectedLevel?.requiresHead ?? false;

  return (
    <Card>
      <form onSubmit={form.handleSubmit(onSubmit)}>
        <CardHeader>
          <CardTitle>Unit Details</CardTitle>
          <CardDescription>
            A unit is an actual node in the hierarchy (e.g. &quot;Finance Division&quot;), placed at a level and
            optionally under a parent unit.
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label htmlFor="name">Name</Label>
              <Input id="name" placeholder="Finance Division" {...form.register('name')} />
              {form.formState.errors.name && (
                <p className="text-sm text-red-500">{form.formState.errors.name.message}</p>
              )}
            </div>
            <div className="space-y-2">
              <Label htmlFor="code">Code</Label>
              <Input id="code" placeholder="FIN" {...form.register('code')} />
              {form.formState.errors.code && (
                <p className="text-sm text-red-500">{form.formState.errors.code.message}</p>
              )}
            </div>
          </div>

          <div className="grid grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label htmlFor="organizationLevelId">Level</Label>
              <Select
                value={selectedLevelId || undefined}
                disabled={isEdit}
                onValueChange={(value) =>
                  form.setValue('organizationLevelId', value, { shouldValidate: true })
                }
              >
                <SelectTrigger id="organizationLevelId">
                  <SelectValue placeholder="Select a level" />
                </SelectTrigger>
                <SelectContent>
                  {levels.map((l) => (
                    <SelectItem key={l.id} value={l.id}>
                      {l.name} (L{l.levelNumber})
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              {isEdit && (
                <p className="text-xs text-muted-foreground">Level cannot be changed after creation.</p>
              )}
              {form.formState.errors.organizationLevelId && (
                <p className="text-sm text-red-500">
                  {form.formState.errors.organizationLevelId.message}
                </p>
              )}
            </div>
            <div className="space-y-2">
              <Label htmlFor="parentUnitId">Parent Unit</Label>
              <Select
                value={parentValue}
                onValueChange={(value) =>
                  form.setValue('parentUnitId', value === NO_PARENT ? '' : value)
                }
              >
                <SelectTrigger id="parentUnitId">
                  <SelectValue placeholder="None (root unit)" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value={NO_PARENT}>None (root unit)</SelectItem>
                  {units.map((u) => (
                    <SelectItem key={u.id} value={u.id}>
                      {u.name}
                      {u.levelName ? ` · ${u.levelName}` : ''}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              <p className="text-xs text-muted-foreground">
                Must be exactly one level above this unit. Leave empty for the root unit.
              </p>
            </div>
          </div>

          <div className="grid grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label htmlFor="accountCode">Account Code</Label>
              <Input id="accountCode" placeholder="Optional" {...form.register('accountCode')} />
            </div>
            <div className="space-y-2">
              <Label htmlFor="sequence">Sequence</Label>
              <Input id="sequence" type="number" min={1} {...form.register('sequence')} />
              <p className="text-xs text-muted-foreground">Display order among siblings.</p>
              {form.formState.errors.sequence && (
                <p className="text-sm text-red-500">{form.formState.errors.sequence.message}</p>
              )}
            </div>
          </div>

          <div className="space-y-2">
            <Label>Head Employee</Label>
            <EmployeePicker
              value={headEmployeeId}
              initialLabel={initialHeadLabel}
              onChange={(id) => form.setValue('headEmployeeId', id ?? '')}
            />
            <p className="text-xs text-muted-foreground">
              {levelRequiresHead
                ? 'This level is configured to have a head — optional now, assign one later once employees exist.'
                : 'Optional. Can be assigned later.'}
            </p>
          </div>

          <div className="space-y-2">
            <Label htmlFor="description">Description</Label>
            <Textarea
              id="description"
              placeholder="Optional description"
              rows={3}
              {...form.register('description')}
            />
          </div>

          <div className="flex items-center justify-between rounded-md border p-4">
            <div className="space-y-0.5">
              <Label htmlFor="isActive">Active</Label>
              <p className="text-xs text-muted-foreground">
                Inactive units are hidden from assignment.
              </p>
            </div>
            <Switch
              id="isActive"
              checked={isActive}
              onCheckedChange={(v) => form.setValue('isActive', v)}
            />
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
