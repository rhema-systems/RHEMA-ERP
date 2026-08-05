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

export const organizationStructureSchema = z.object({
  name: z.string().min(1, 'Name is required').max(100),
  code: z.string().max(50).optional().or(z.literal('')),
  description: z.string().max(1000).optional().or(z.literal('')),
  isDefault: z.boolean(),
  isActive: z.boolean(),
});

export type OrganizationStructureFormValues = z.infer<typeof organizationStructureSchema>;

export const emptyOrganizationStructure: OrganizationStructureFormValues = {
  name: '',
  code: '',
  description: '',
  isDefault: false,
  isActive: true,
};

interface OrganizationStructureFormProps {
  defaultValues: OrganizationStructureFormValues;
  onSubmit: (values: OrganizationStructureFormValues) => Promise<void>;
  submitting: boolean;
  submitLabel: string;
  onCancel: () => void;
  showActive?: boolean;
}

export function OrganizationStructureForm({
  defaultValues,
  onSubmit,
  submitting,
  submitLabel,
  onCancel,
  showActive = true,
}: OrganizationStructureFormProps) {
  const form = useForm<OrganizationStructureFormValues>({
    resolver: zodResolver(organizationStructureSchema) as any,
    defaultValues,
  });

  return (
    <Card className="max-w-2xl">
      <form onSubmit={form.handleSubmit(onSubmit)}>
        <CardHeader>
          <CardTitle>Structure Details</CardTitle>
          <CardDescription>
            An organization structure is the template that holds levels and units.
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <div className="space-y-2">
              <Label htmlFor="name">Name</Label>
              <Input id="name" placeholder="TDC Organisational Structure" {...form.register('name')} />
              {form.formState.errors.name && (
                <p className="text-sm text-red-500">{form.formState.errors.name.message}</p>
              )}
            </div>
            <div className="space-y-2">
              <Label htmlFor="code">Code</Label>
              <Input id="code" placeholder="TDC-ORG" {...form.register('code')} />
              {form.formState.errors.code && (
                <p className="text-sm text-red-500">{form.formState.errors.code.message}</p>
              )}
            </div>
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

          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <div className="flex items-center justify-between rounded-md border px-3 py-2.5">
              <div className="space-y-0.5">
                <Label htmlFor="isDefault" className="cursor-pointer">Default structure</Label>
                <p className="text-xs text-muted-foreground">Used as the default for new levels/units.</p>
              </div>
              <Switch
                id="isDefault"
                checked={form.watch('isDefault')}
                onCheckedChange={(v) => form.setValue('isDefault', v)}
              />
            </div>
            {showActive && (
              <div className="flex items-center justify-between rounded-md border px-3 py-2.5">
                <Label htmlFor="isActive" className="cursor-pointer">Active</Label>
                <Switch
                  id="isActive"
                  checked={form.watch('isActive')}
                  onCheckedChange={(v) => form.setValue('isActive', v)}
                />
              </div>
            )}
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
