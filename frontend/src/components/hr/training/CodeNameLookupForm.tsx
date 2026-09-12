'use client';

import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Loader2, Save } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardFooter, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  TextField,
  NumberField,
  TextareaField,
  SwitchField,
  ColorField,
  FieldRow,
  hexColorSchema,
} from '@/components/hr/employee/tabs/fields';

/**
 * Shared form for Training Categories and Program Groups — both are the same shape
 * (code, name, colour, description, sort order, active) so one component serves both
 * triads instead of two near-duplicate files.
 */

export const codeNameLookupSchema = z.object({
  code: z.string().min(1, 'Code is required').max(50),
  name: z.string().min(1, 'Name is required').max(200),
  colorHex: hexColorSchema,
  description: z.string().max(500).optional().or(z.literal('')),
  sortOrder: z.coerce.number().min(0),
  isActive: z.boolean(),
});

export type CodeNameLookupFormValues = z.infer<typeof codeNameLookupSchema>;

export const emptyCodeNameLookup: CodeNameLookupFormValues = {
  code: '',
  name: '',
  colorHex: '',
  description: '',
  sortOrder: 0,
  isActive: true,
};

interface CodeNameLookupFormProps {
  entityLabel: string;
  defaultValues: CodeNameLookupFormValues;
  onSubmit: (values: CodeNameLookupFormValues) => Promise<void>;
  submitting: boolean;
  submitLabel: string;
  onCancel: () => void;
  /** Code is immutable after creation. */
  codeEditable?: boolean;
}

export function CodeNameLookupForm({
  entityLabel,
  defaultValues,
  onSubmit,
  submitting,
  submitLabel,
  onCancel,
  codeEditable = true,
}: CodeNameLookupFormProps) {
  const form = useForm<CodeNameLookupFormValues>({
    resolver: zodResolver(codeNameLookupSchema) as any,
    defaultValues,
  });

  return (
    <Card className="max-w-2xl">
      <form onSubmit={form.handleSubmit(onSubmit)}>
        <CardHeader>
          <CardTitle>{entityLabel} details</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <FieldRow>
            {codeEditable ? (
              <TextField form={form} name="code" label="Code" required placeholder="e.g. TECH" />
            ) : (
              <div className="space-y-2">
                <Label htmlFor="code">Code</Label>
                <Input id="code" value={form.watch('code')} disabled />
                <p className="text-xs text-muted-foreground">Cannot be changed after creation.</p>
              </div>
            )}
            <TextField form={form} name="name" label="Name" required />
          </FieldRow>
          <TextareaField form={form} name="description" label="Description" rows={2} />
          <FieldRow>
            <ColorField form={form} name="colorHex" label="Colour" />
            <NumberField form={form} name="sortOrder" label="Sort order" />
          </FieldRow>
          <SwitchField form={form} name="isActive" label="Active" />
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
