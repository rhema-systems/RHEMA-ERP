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
import { useQuery } from '@tanstack/react-query';
import { QUALIFICATION_TYPE_OPTIONS } from '@/types/hr/lookups';
import { referenceDimensionService } from '@/services/hr/lookup.service';

/** The select's stand-in for "unranked", which the wire carries as null. */
const NO_LEVEL = '__none__';

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
  /**
   * Which rung of the ladder this sits on. Empty means unranked.
   *
   * ⚠ Not a duplicate of `type`. That is a CATEGORY — Education, Certification, License,
   * Membership — and it cannot answer "is a Master's higher than a Diploma", which is the
   * question shortlisting and succession ask. Plenty of qualifications are legitimately
   * unranked, so this stays optional rather than inventing a comparison.
   */
  qualificationLevelId: z.string().optional().or(z.literal('')),
  isActive: z.boolean(),
});

export type QualificationFormValues = z.infer<typeof qualificationSchema>;

export const emptyQualification: QualificationFormValues = {
  name: '',
  shortCode: '',
  type: 'Education',
  issuingAuthority: '',
  description: '',
  qualificationLevelId: '',
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

  // Active rungs only: a retired one must stop being offered on new qualifications, which is what
  // retiring it means. A qualification already sitting on a retired rung keeps it — the value is
  // on the record, not derived from this list.
  const levels = useQuery({
    queryKey: ['hr', 'qualification-levels', 'active'],
    queryFn: () => referenceDimensionService.getQualificationLevels(true),
  });
  const levelOptions = [...(levels.data ?? [])].sort((a, b) => a.rank - b.rank || a.name.localeCompare(b.name));

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

          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <div className="space-y-2">
              <Label htmlFor="qualificationLevelId">Level</Label>
              <Select
                value={form.watch('qualificationLevelId') || NO_LEVEL}
                onValueChange={(v) =>
                  form.setValue('qualificationLevelId', v === NO_LEVEL ? '' : v)
                }
              >
                <SelectTrigger id="qualificationLevelId">
                  <SelectValue placeholder="Unranked" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value={NO_LEVEL}>Unranked</SelectItem>
                  {levelOptions.map((l) => (
                    <SelectItem key={l.id} value={l.id}>
                      {l.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              <p className="text-xs text-muted-foreground">
                {levelOptions.length === 0
                  ? 'No levels are set up yet — add them under Qualification Levels to rank this.'
                  : 'Where this sits on the ladder. Type says what kind it is; level says how far it goes.'}
              </p>
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
