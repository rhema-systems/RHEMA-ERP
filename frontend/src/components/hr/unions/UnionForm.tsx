'use client';

import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Loader2, Save } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Switch } from '@/components/ui/switch';
import { Textarea } from '@/components/ui/textarea';
import {
  Card,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';

// Lengths mirror the entity's MaxLength attributes exactly — a form that allows more just moves the
// refusal to the server and loses the user's typing on the way.
export const unionSchema = z.object({
  code: z.string().max(50, 'Keep the code under 50 characters').optional().or(z.literal('')),
  name: z.string().min(1, 'Name is required').max(200),
  description: z.string().max(1000).optional().or(z.literal('')),
  contactPerson: z.string().max(150).optional().or(z.literal('')),
  contactEmail: z
    .string()
    .email('Enter a valid email address')
    .max(150)
    .optional()
    .or(z.literal('')),
  contactPhone: z.string().max(50).optional().or(z.literal('')),
  isActive: z.boolean(),
});

export type UnionFormValues = z.infer<typeof unionSchema>;

export const emptyUnion: UnionFormValues = {
  code: '',
  name: '',
  description: '',
  contactPerson: '',
  contactEmail: '',
  contactPhone: '',
  isActive: true,
};

interface UnionFormProps {
  defaultValues: UnionFormValues;
  onSubmit: (values: UnionFormValues) => Promise<void>;
  submitting: boolean;
  submitLabel: string;
  onCancel: () => void;
}

export function UnionForm({
  defaultValues,
  onSubmit,
  submitting,
  submitLabel,
  onCancel,
}: UnionFormProps) {
  const form = useForm<UnionFormValues>({
    resolver: zodResolver(unionSchema) as any,
    defaultValues,
  });

  const isActive = form.watch('isActive');

  return (
    <Card>
      <form onSubmit={form.handleSubmit(onSubmit)}>
        <CardHeader>
          <CardTitle>Union Details</CardTitle>
          <CardDescription>
            A trade union that represents a bargaining unit. Job descriptions point at one to say
            which agreement a role falls under, and the offer letter prints the union&apos;s name in
            its bargaining-unit clause.
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label htmlFor="name">Name</Label>
              <Input
                id="name"
                placeholder="Industrial &amp; Commercial Workers' Union"
                {...form.register('name')}
              />
              {form.formState.errors.name && (
                <p className="text-sm text-red-500">{form.formState.errors.name.message}</p>
              )}
            </div>
            <div className="space-y-2">
              <Label htmlFor="code">Code</Label>
              <Input id="code" placeholder="ICU" {...form.register('code')} />
              <p className="text-xs text-muted-foreground">
                Optional, but must be unique among your unions if given.
              </p>
              {form.formState.errors.code && (
                <p className="text-sm text-red-500">{form.formState.errors.code.message}</p>
              )}
            </div>
          </div>

          <div className="space-y-2">
            <Label htmlFor="description">Description</Label>
            <Textarea
              id="description"
              placeholder="Which staff this union represents, and any background worth recording."
              rows={3}
              {...form.register('description')}
            />
          </div>

          <div className="grid grid-cols-3 gap-4">
            <div className="space-y-2">
              <Label htmlFor="contactPerson">Contact person</Label>
              <Input id="contactPerson" placeholder="General Secretary" {...form.register('contactPerson')} />
            </div>
            <div className="space-y-2">
              <Label htmlFor="contactEmail">Contact email</Label>
              <Input id="contactEmail" type="email" placeholder="secretary@union.org" {...form.register('contactEmail')} />
              {form.formState.errors.contactEmail && (
                <p className="text-sm text-red-500">{form.formState.errors.contactEmail.message}</p>
              )}
            </div>
            <div className="space-y-2">
              <Label htmlFor="contactPhone">Contact phone</Label>
              <Input id="contactPhone" placeholder="+233 …" {...form.register('contactPhone')} />
            </div>
          </div>

          <div className="flex items-center justify-between rounded-md border p-4">
            <div className="space-y-0.5">
              <Label htmlFor="isActive">Active</Label>
              <p className="text-xs text-muted-foreground">
                Inactive unions stay on file with their agreements but drop out of the job-description
                picker.
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
