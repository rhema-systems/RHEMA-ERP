'use client';

import { useForm, useFieldArray } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Loader2, Plus, Save, Trash2 } from 'lucide-react';
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
import { CertificationPicker } from '@/components/hr/common/CertificationPicker';

export const skillSchema = z
  .object({
    name: z.string().min(1, 'Name is required').max(100),
    category: z.string().max(100).optional().or(z.literal('')),
    description: z.string().max(1000).optional().or(z.literal('')),
    requiresCertification: z.boolean(),
    isActive: z.boolean(),
    // The credentials that evidence the skill (round 2, lane C2 — S-1). Sent as the whole set.
    certifications: z.array(
      z.object({
        certificationId: z.string().min(1, 'Choose the certification'),
        isMandatory: z.boolean(),
        notes: z.string().max(500).optional().or(z.literal('')),
      }),
    ),
  })
  .superRefine((v, ctx) => {
    // The server refuses this too; saying it here saves the round trip. "Requires certification"
    // with nothing named was a bare flag nothing consumed.
    if (v.requiresCertification && v.certifications.length === 0) {
      ctx.addIssue({
        code: 'custom',
        path: ['certifications'],
        message: 'Say which credential evidences this skill — add at least one accepted certification.',
      });
    }
  });

export type SkillFormValues = z.infer<typeof skillSchema>;

export const emptySkill: SkillFormValues = {
  name: '',
  category: '',
  description: '',
  requiresCertification: false,
  isActive: true,
  certifications: [],
};

interface SkillFormProps {
  defaultValues: SkillFormValues;
  categories: string[];
  onSubmit: (values: SkillFormValues) => Promise<void>;
  submitting: boolean;
  submitLabel: string;
  onCancel: () => void;
  showActive?: boolean;
}

export function SkillForm({
  defaultValues,
  categories,
  onSubmit,
  submitting,
  submitLabel,
  onCancel,
  showActive = true,
}: SkillFormProps) {
  const form = useForm<SkillFormValues>({
    resolver: zodResolver(skillSchema) as any,
    defaultValues,
  });

  const requiresCertification = form.watch('requiresCertification');
  const { fields: certificationRows, append: appendCertification, remove: removeCertification } =
    useFieldArray({ control: form.control, name: 'certifications' });
  const chosenCertificationIds = form.watch('certifications').map((c) => c.certificationId).filter(Boolean);

  return (
    <Card className="max-w-2xl">
      <form onSubmit={form.handleSubmit(onSubmit)}>
        <CardHeader>
          <CardTitle>Skill Details</CardTitle>
          <CardDescription>A skill that positions can require and employees can hold.</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <div className="space-y-2">
              <Label htmlFor="name">Name</Label>
              <Input id="name" placeholder="Project Management" {...form.register('name')} />
              {form.formState.errors.name && (
                <p className="text-sm text-red-500">{form.formState.errors.name.message}</p>
              )}
            </div>
            <div className="space-y-2">
              <Label htmlFor="category">Category</Label>
              <Input
                id="category"
                list="skill-categories"
                placeholder="e.g. Technical, Leadership"
                {...form.register('category')}
              />
              <datalist id="skill-categories">
                {categories.map((c) => (
                  <option key={c} value={c} />
                ))}
              </datalist>
              <p className="text-xs text-muted-foreground">Pick an existing category or type a new one.</p>
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
              <Label htmlFor="requiresCertification" className="cursor-pointer">
                Requires Certification
              </Label>
              <Switch
                id="requiresCertification"
                checked={form.watch('requiresCertification')}
                onCheckedChange={(v) => form.setValue('requiresCertification', v)}
              />
            </div>
            {showActive && (
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
            )}
          </div>

          {requiresCertification && (
            <div className="space-y-3 rounded-md border p-4">
              <div className="flex items-center justify-between">
                <div>
                  <h4 className="text-sm font-semibold">Accepted certifications</h4>
                  <p className="text-xs text-muted-foreground">
                    Which credential evidences this skill. Pick the certifying body, then the certification it
                    issues. Mandatory means it is always needed; otherwise any one accepted credential will do.
                  </p>
                </div>
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={() => appendCertification({ certificationId: '', isMandatory: true, notes: '' })}
                >
                  <Plus className="mr-2 h-4 w-4" /> Add certification
                </Button>
              </div>

              {certificationRows.length === 0 && (
                <p className="text-sm text-destructive">
                  A skill that requires certification must name at least one accepted credential.
                </p>
              )}

              {certificationRows.map((row, index) => (
                <div key={row.id} className="space-y-3 rounded-md border bg-muted/30 p-3">
                  <CertificationPicker
                    idPrefix={`skill-certification-${index}`}
                    value={form.watch(`certifications.${index}.certificationId`)}
                    onChange={(id) =>
                      form.setValue(`certifications.${index}.certificationId`, id, {
                        shouldValidate: true,
                        shouldDirty: true,
                      })
                    }
                    excludeIds={chosenCertificationIds.filter(
                      (id) => id !== form.watch(`certifications.${index}.certificationId`),
                    )}
                    error={form.formState.errors.certifications?.[index]?.certificationId?.message as string | undefined}
                  />
                  <div className="grid grid-cols-1 gap-3 sm:grid-cols-[auto_1fr_auto] sm:items-center">
                    <label className="flex items-center gap-2 text-sm">
                      <Switch
                        checked={form.watch(`certifications.${index}.isMandatory`)}
                        onCheckedChange={(v) => form.setValue(`certifications.${index}.isMandatory`, v)}
                      />
                      Mandatory
                    </label>
                    <Input
                      placeholder="Notes (optional)"
                      {...form.register(`certifications.${index}.notes`)}
                    />
                    <Button type="button" variant="ghost" size="sm" onClick={() => removeCertification(index)}>
                      <Trash2 className="h-4 w-4" />
                    </Button>
                  </div>
                </div>
              ))}
              {(form.formState.errors.certifications as { message?: string } | undefined)?.message && (
                <p className="text-sm text-red-500">
                  {(form.formState.errors.certifications as { message?: string }).message}
                </p>
              )}
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
