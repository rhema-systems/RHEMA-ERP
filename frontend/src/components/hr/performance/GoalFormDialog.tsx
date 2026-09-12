'use client';

import { useEffect, type ReactNode } from 'react';
import { useForm, type DefaultValues, type FieldValues, type UseFormReturn } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import type { ZodType } from 'zod';
import { Loader2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';

/**
 * The add/edit dialog shared by the company, unit and employee goal screens.
 *
 * `ResourceListPanel` already does this for the simple setup lists, but it owns the table too
 * and those three screens have their own — paged, filtered, with metric tiles above. This is
 * just the dialog half: schema, fields and submit, with the caller keeping its own list.
 */
export interface GoalFormDialogProps<TForm extends FieldValues> {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /** Present when editing; the title and button text follow from it. */
  editingId: string | null;
  title: string;
  hint?: string;
  schema: ZodType<TForm>;
  /** Re-applied every time the dialog opens, so a cancelled edit leaves nothing behind. */
  values: TForm;
  renderFields: (form: UseFormReturn<TForm>) => ReactNode;
  onSubmit: (values: TForm) => Promise<unknown>;
  submitting?: boolean;
  className?: string;
  /** Rendered above the fields — a library picker button, an alignment hint, a warning. */
  header?: ReactNode;
}

export function GoalFormDialog<TForm extends FieldValues>({
  open,
  onOpenChange,
  editingId,
  title,
  hint,
  schema,
  values,
  renderFields,
  onSubmit,
  submitting = false,
  className = 'sm:max-w-[640px]',
  header,
}: GoalFormDialogProps<TForm>) {
  const form = useForm<TForm>({
    resolver: zodResolver(schema as any) as any,
    defaultValues: values as DefaultValues<TForm>,
  });

  // The caller swaps `values` when it switches between create and edit, and the dialog is
  // kept mounted, so the reset has to happen on open rather than on mount. `values` is
  // deliberately not a dependency — it is a fresh object every render, and re-running the
  // reset would wipe whatever the user has typed.
  useEffect(() => {
    if (open) form.reset(values as DefaultValues<TForm>);
  }, [open, editingId]);

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className={className}>
        <form onSubmit={form.handleSubmit(async (v) => onSubmit(v))}>
          <DialogHeader>
            <DialogTitle>
              {editingId ? `Edit ${title}` : `New ${title}`}
            </DialogTitle>
            {hint && <DialogDescription>{hint}</DialogDescription>}
          </DialogHeader>

          <div className="max-h-[62vh] space-y-4 overflow-y-auto py-4">
            {header}
            {renderFields(form)}
          </div>

          <DialogFooter>
            <Button
              type="button"
              variant="outline"
              onClick={() => onOpenChange(false)}
              disabled={submitting}
            >
              Cancel
            </Button>
            <Button type="submit" disabled={submitting}>
              {submitting && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              {editingId ? 'Save changes' : `Create ${title}`}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
