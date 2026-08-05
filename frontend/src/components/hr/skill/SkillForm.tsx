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

export const skillSchema = z.object({
  name: z.string().min(1, 'Name is required').max(100),
  category: z.string().max(100).optional().or(z.literal('')),
  description: z.string().max(1000).optional().or(z.literal('')),
  requiresCertification: z.boolean(),
  isActive: z.boolean(),
});

export type SkillFormValues = z.infer<typeof skillSchema>;

export const emptySkill: SkillFormValues = {
  name: '',
  category: '',
  description: '',
  requiresCertification: false,
  isActive: true,
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
