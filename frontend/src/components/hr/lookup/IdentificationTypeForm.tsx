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
import type { Country } from '@/types/hr/country';

const NONE = 'none';

export const identificationTypeSchema = z.object({
  name: z.string().min(1, 'Name is required').max(100),
  code: z.string().max(20).optional().or(z.literal('')),
  issuingAuthorityName: z.string().min(1, 'Issuing authority is required').max(200),
  issuingCountryId: z.string().optional().or(z.literal('')),
  description: z.string().max(1000).optional().or(z.literal('')),
  hasExpiryDate: z.boolean(),
  // Kept as a string so an empty box stays empty rather than coercing to 0 — 0 would mean "warn on
  // the day it expires", which is a different instruction from "do not warn at all".
  expiryNotificationLeadDays: z
    .string()
    .optional()
    .or(z.literal(''))
    .refine((v) => !v || (/^[0-9]+$/.test(v) && Number(v) <= 3650), {
      message: 'Enter a whole number of days, up to 3650.',
    }),
  isActive: z.boolean(),
});

export type IdentificationTypeFormValues = z.infer<typeof identificationTypeSchema>;

export const emptyIdentificationType: IdentificationTypeFormValues = {
  name: '',
  code: '',
  issuingAuthorityName: '',
  issuingCountryId: '',
  description: '',
  hasExpiryDate: true,
  expiryNotificationLeadDays: '',
  isActive: true,
};

interface IdentificationTypeFormProps {
  defaultValues: IdentificationTypeFormValues;
  countries: Country[];
  onSubmit: (values: IdentificationTypeFormValues) => Promise<void>;
  submitting: boolean;
  submitLabel: string;
  onCancel: () => void;
}

export function IdentificationTypeForm({
  defaultValues,
  countries,
  onSubmit,
  submitting,
  submitLabel,
  onCancel,
}: IdentificationTypeFormProps) {
  const form = useForm<IdentificationTypeFormValues>({
    resolver: zodResolver(identificationTypeSchema) as any,
    defaultValues,
  });

  const countryValue = form.watch('issuingCountryId') || NONE;

  return (
    <Card className="max-w-2xl">
      <form onSubmit={form.handleSubmit(onSubmit)}>
        <CardHeader>
          <CardTitle>Identification Type Details</CardTitle>
          <CardDescription>
            A type of identity document employees can record on their profile.
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <div className="space-y-2">
              <Label htmlFor="name">Name</Label>
              <Input id="name" placeholder="Ghana Card" {...form.register('name')} />
              {form.formState.errors.name && (
                <p className="text-sm text-red-500">{form.formState.errors.name.message}</p>
              )}
            </div>
            <div className="space-y-2">
              <Label htmlFor="code">Code</Label>
              <Input id="code" placeholder="GHC" {...form.register('code')} />
            </div>
          </div>

          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <div className="space-y-2">
              <Label htmlFor="issuingAuthorityName">Issuing authority</Label>
              <Input
                id="issuingAuthorityName"
                placeholder="National Identification Authority"
                {...form.register('issuingAuthorityName')}
              />
              {form.formState.errors.issuingAuthorityName && (
                <p className="text-sm text-red-500">
                  {form.formState.errors.issuingAuthorityName.message}
                </p>
              )}
            </div>
            <div className="space-y-2">
              <Label htmlFor="issuingCountryId">Issuing country</Label>
              <Select
                value={countryValue}
                onValueChange={(v) =>
                  form.setValue('issuingCountryId', v === NONE ? '' : v)
                }
              >
                <SelectTrigger id="issuingCountryId">
                  <SelectValue placeholder="None" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value={NONE}>None</SelectItem>
                  {countries.map((c) => (
                    <SelectItem key={c.id} value={c.id}>
                      {c.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
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

          {/*
            The warning time only means anything for a type that expires, so it is hidden when the
            switch is off. Leaving an editable box on a type with no expiry date would invite a value
            the sweep is required to ignore.
          */}
          {form.watch('hasExpiryDate') && (
            <div className="space-y-2 rounded-md border border-dashed p-3">
              <Label htmlFor="expiryNotificationLeadDays">Warn this many days ahead</Label>
              <Input
                id="expiryNotificationLeadDays"
                inputMode="numeric"
                placeholder="e.g. 90"
                className="sm:max-w-[12rem]"
                {...form.register('expiryNotificationLeadDays')}
              />
              <p className="text-xs text-muted-foreground">
                Leave this blank and the expiry sweep will never raise a reminder for this type,
                however close a card is to running out. A passport usually needs more notice than a
                site pass, which is why the setting lives here rather than on each card.
              </p>
              {form.formState.errors.expiryNotificationLeadDays && (
                <p className="text-sm text-destructive">
                  {form.formState.errors.expiryNotificationLeadDays.message}
                </p>
              )}
            </div>
          )}

          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <div className="flex items-center justify-between rounded-md border px-3 py-2.5">
              <Label htmlFor="hasExpiryDate" className="cursor-pointer">
                Has expiry date
              </Label>
              <Switch
                id="hasExpiryDate"
                checked={form.watch('hasExpiryDate')}
                onCheckedChange={(v) => form.setValue('hasExpiryDate', v)}
              />
            </div>
            <div className="flex items-center justify-between rounded-md border px-3 py-2.5">
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
