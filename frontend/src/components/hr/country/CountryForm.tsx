'use client';

import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Loader2, Save } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Switch } from '@/components/ui/switch';
import {
  Card,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';

export const countrySchema = z.object({
  name: z.string().min(1, 'Name is required').max(100),
  code: z.string().min(2, 'Use the ISO 3-letter code').max(3, 'Max 3 characters'),
  alpha2Code: z.string().max(2, 'Max 2 characters').optional().or(z.literal('')),
  isActive: z.boolean(),
});

export type CountryFormValues = z.infer<typeof countrySchema>;

export const emptyCountry: CountryFormValues = {
  name: '',
  code: '',
  alpha2Code: '',
  isActive: true,
};

interface CountryFormProps {
  defaultValues: CountryFormValues;
  onSubmit: (values: CountryFormValues) => Promise<void>;
  submitting: boolean;
  submitLabel: string;
  onCancel: () => void;
  showActive?: boolean;
}

export function CountryForm({
  defaultValues,
  onSubmit,
  submitting,
  submitLabel,
  onCancel,
  showActive = true,
}: CountryFormProps) {
  const form = useForm<CountryFormValues>({
    resolver: zodResolver(countrySchema) as any,
    defaultValues,
  });

  return (
    <Card className="max-w-2xl">
      <form onSubmit={form.handleSubmit(onSubmit)}>
        <CardHeader>
          <CardTitle>Country Details</CardTitle>
          <CardDescription>A country reference used by locations and employees.</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="space-y-2">
            <Label htmlFor="name">Name</Label>
            <Input id="name" placeholder="Ghana" {...form.register('name')} />
            {form.formState.errors.name && (
              <p className="text-sm text-red-500">{form.formState.errors.name.message}</p>
            )}
          </div>

          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <div className="space-y-2">
              <Label htmlFor="code">ISO Code (Alpha-3)</Label>
              <Input
                id="code"
                placeholder="GHA"
                maxLength={3}
                className="uppercase"
                {...form.register('code', {
                  setValueAs: (v) => (typeof v === 'string' ? v.toUpperCase() : v),
                })}
              />
              {form.formState.errors.code && (
                <p className="text-sm text-red-500">{form.formState.errors.code.message}</p>
              )}
            </div>
            <div className="space-y-2">
              <Label htmlFor="alpha2Code">ISO Code (Alpha-2)</Label>
              <Input
                id="alpha2Code"
                placeholder="GH"
                maxLength={2}
                className="uppercase"
                {...form.register('alpha2Code', {
                  setValueAs: (v) => (typeof v === 'string' ? v.toUpperCase() : v),
                })}
              />
              {form.formState.errors.alpha2Code && (
                <p className="text-sm text-red-500">{form.formState.errors.alpha2Code.message}</p>
              )}
            </div>
          </div>

          {showActive && (
            <div className="flex items-center justify-between rounded-md border px-3 py-2.5 sm:max-w-xs">
              <Label htmlFor="isActive" className="cursor-pointer">Active</Label>
              <Switch
                id="isActive"
                checked={form.watch('isActive')}
                onCheckedChange={(v) => form.setValue('isActive', v)}
              />
            </div>
          )}
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
