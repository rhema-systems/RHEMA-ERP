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
import { QUALIFICATION_TYPE_OPTIONS } from '@/types/hr/lookups';

export const qualificationSchema = z.object({
  name: z.string().min(1, 'Name is required').max(200),
  shortCode: z.string().max(20).optional().or(z.literal('')),
  type: z.enum([
    'Education',
    'Experience',
    'Certification',
    'License',
    'TechnicalSkills',
    'Language',
    'Membership',
    'Other',
  ]),
  issuingAuthority: z.string().max(200).optional().or(z.literal('')),
  description: z.string().max(1000).optional().or(z.literal('')),
  isActive: z.boolean(),
});

export type QualificationFormValues = z.infer<typeof qualificationSchema>;

export const emptyQualification: QualificationFormValues = {
  name: '',
  shortCode: '',
  type: 'Education',
  issuingAuthority: '',
  description: '',
  isActive: true,
};

interface QualificationFormProps {
  defaultValues: QualificationFormValues;
  onSubmit: (values: QualificationFormValues) => Promise<void>;
  submitting: boolean;
  submitLabel: string;
  onCancel: () => void;
}

export function QualificationForm({
  defaultValues,
  onSubmit,
  submitting,
  submitLabel,
  onCancel,
}: QualificationFormProps) {
  const form = useForm<QualificationFormValues>({
    resolver: zodResolver(qualificationSchema) as any,
    defaultValues,
  });

  return (
    <Card className="max-w-2xl">
      <form onSubmit={form.handleSubmit(onSubmit)}>
        <CardHeader>
          <CardTitle>Qualification Details</CardTitle>
          <CardDescription>
            A qualification employees can hold, selected on their profile.
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <div className="space-y-2">
              <Label htmlFor="name">Name</Label>
              <Input id="name" placeholder="BSc Accounting" {...form.register('name')} />
              {form.formState.errors.name && (
                <p className="text-sm text-red-500">{form.formState.errors.name.message}</p>
              )}
            </div>
            <div className="space-y-2">
              <Label htmlFor="shortCode">Short code</Label>
              <Input id="shortCode" placeholder="BSC-ACC" {...form.register('shortCode')} />
            </div>
          </div>

          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <div className="space-y-2">
              <Label htmlFor="type">Type</Label>
              <Select
                value={form.watch('type')}
                onValueChange={(v) => form.setValue('type', v as QualificationFormValues['type'])}
              >
                <SelectTrigger id="type">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {QUALIFICATION_TYPE_OPTIONS.map((o) => (
                    <SelectItem key={o.value} value={o.value}>
                      {o.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label htmlFor="issuingAuthority">Issuing authority</Label>
              <Input
                id="issuingAuthority"
                placeholder="University of Ghana"
                {...form.register('issuingAuthority')}
              />
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

          <div className="flex items-center justify-between rounded-md border px-3 py-2.5 sm:max-w-[50%]">
            <Label htmlFor="isActive" className="cursor-pointer">
              Active
            </Label>
            <Switch
              id="isActive"
              checked={form.watch('isActive')}
              onCheckedChange={(v) => form.setValue('isActive', v)}
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
