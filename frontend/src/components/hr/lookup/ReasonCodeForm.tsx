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
import { REASON_CODE_CATEGORY_OPTIONS } from '@/types/hr/lookups';

export const reasonCodeSchema = z.object({
  code: z.string().min(1, 'Code is required').max(20),
  name: z.string().min(1, 'Name is required').max(100),
  category: z.enum([
    'General',
    'LeaveAdjustment',
    'LeaveEncashment',
    'LeaveCancellation',
    'LeaveRejection',
  ]),
  description: z.string().max(1000).optional().or(z.literal('')),
  isActive: z.boolean(),
});

export type ReasonCodeFormValues = z.infer<typeof reasonCodeSchema>;

export const emptyReasonCode: ReasonCodeFormValues = {
  code: '',
  name: '',
  category: 'General',
  description: '',
  isActive: true,
};

interface ReasonCodeFormProps {
  defaultValues: ReasonCodeFormValues;
  onSubmit: (values: ReasonCodeFormValues) => Promise<void>;
  submitting: boolean;
  submitLabel: string;
  onCancel: () => void;
}

export function ReasonCodeForm({
  defaultValues,
  onSubmit,
  submitting,
  submitLabel,
  onCancel,
}: ReasonCodeFormProps) {
  const form = useForm<ReasonCodeFormValues>({
    resolver: zodResolver(reasonCodeSchema) as any,
    defaultValues,
  });

  return (
    <Card className="max-w-2xl">
      <form onSubmit={form.handleSubmit(onSubmit)}>
        <CardHeader>
          <CardTitle>Reason Code Details</CardTitle>
          <CardDescription>
            A standard reason selectable on HR actions such as leave adjustments.
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <div className="space-y-2">
              <Label htmlFor="code">Code</Label>
              <Input id="code" placeholder="ADJ-01" {...form.register('code')} />
              {form.formState.errors.code && (
                <p className="text-sm text-red-500">{form.formState.errors.code.message}</p>
              )}
            </div>
            <div className="space-y-2">
              <Label htmlFor="name">Name</Label>
              <Input id="name" placeholder="Carry-over adjustment" {...form.register('name')} />
              {form.formState.errors.name && (
                <p className="text-sm text-red-500">{form.formState.errors.name.message}</p>
              )}
            </div>
          </div>

          <div className="space-y-2">
            <Label htmlFor="category">Category</Label>
            <Select
              value={form.watch('category')}
              onValueChange={(v) => form.setValue('category', v as ReasonCodeFormValues['category'])}
            >
              <SelectTrigger id="category">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {REASON_CODE_CATEGORY_OPTIONS.map((o) => (
                  <SelectItem key={o.value} value={o.value}>
                    {o.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            <p className="text-xs text-muted-foreground">
              Determines where the reason is offered.
            </p>
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
