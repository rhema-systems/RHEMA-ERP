'use client';

import { useState, type ReactNode } from 'react';
import { useForm, type UseFormReturn, type DefaultValues, type FieldValues } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import type { ZodType } from 'zod';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2, Plus, MoreHorizontal, Pencil, Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { useToast } from '@/components/ui/use-toast';
import { EmptyState } from '@/components/hr/common/EmptyState';

/** A rendered column on the collection's table. */
export interface CollectionColumn<TItem> {
  header: string;
  cell: (item: TItem) => ReactNode;
  className?: string;
}

/** An extra per-row action beyond edit/delete (set-primary, verify, activate, …). */
export interface CollectionAction<TItem> {
  label: string | ((item: TItem) => string);
  run: (item: TItem) => Promise<unknown>;
  /** Hide the action for rows it does not apply to. */
  visible?: (item: TItem) => boolean;
  destructive?: boolean;
  /** Ask for confirmation before running. */
  confirm?: { title: string; description?: string };
}

export interface ResourceCollectionTabProps<TItem, TForm extends FieldValues> {
  /** Id of the record this collection hangs off (employee, leave type, …). */
  parentId: string;
  /** Plural label used in headings and messages, e.g. "bank details". */
  title: string;
  /** Singular label used on buttons and dialogs, e.g. "bank account". */
  singular: string;
  /** Full react-query key for this collection. */
  queryKey: unknown[];
  /** Additional keys to invalidate after a change (parent detail, counts, …). */
  invalidateKeys?: unknown[][];

  list: (parentId: string) => Promise<TItem[]>;
  create: (parentId: string, values: TForm) => Promise<unknown>;
  update: (parentId: string, id: string, values: TForm) => Promise<unknown>;
  remove?: (parentId: string, id: string) => Promise<unknown>;
  /** Render with no add/edit/remove affordance at all. */
  readOnly?: boolean;
  /**
   * Set false when the resource has no update endpoint — rows can still be added and
   * removed, but not edited in place. `update` is then never called.
   */
  allowUpdate?: boolean;
  /**
   * Set false when rows cannot originate here — the add affordance disappears but editing
   * stays available. Needed by mirrored resources like pay components, where new rows come
   * from the owning module and a create would be refused with 409.
   */
  allowCreate?: boolean;

  columns: CollectionColumn<TItem>[];
  actions?: CollectionAction<TItem>[];

  schema: ZodType<TForm>;
  emptyForm: TForm;
  /** Map an existing row back into form values when editing. */
  toForm: (item: TItem) => TForm;
  /** Render the dialog body. Receives the live react-hook-form instance. */
  renderFields: (form: UseFormReturn<TForm>) => ReactNode;

  getId: (item: TItem) => string;
  dialogClassName?: string;
  emptyDescription?: string;
  /** Sentence under the dialog title, e.g. "Add a sub-type to this leave type." */
  dialogHint?: string;
}

/**
 * One table-with-dialog implementation shared by every nested HR collection.
 *
 * These collections are all the same shape — list under a parent record, add/edit through
 * a dialog, delete with confirmation, plus a handful of side actions. Each caller supplies
 * columns, a zod schema and its form fields; everything else lives here.
 */
export function ResourceCollectionTab<TItem, TForm extends FieldValues>({
  parentId,
  title,
  singular,
  queryKey,
  invalidateKeys = [],
  list,
  create,
  update,
  remove,
  readOnly = false,
  allowUpdate = true,
  allowCreate = true,
  columns,
  actions = [],
  schema,
  emptyForm,
  toForm,
  renderFields,
  getId,
  dialogClassName = 'sm:max-w-[560px]',
  emptyDescription,
  dialogHint,
}: ResourceCollectionTabProps<TItem, TForm>) {
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [dialogOpen, setDialogOpen] = useState(false);
  const [editing, setEditing] = useState<TItem | null>(null);
  const [pendingDelete, setPendingDelete] = useState<TItem | null>(null);
  const [pendingAction, setPendingAction] = useState<{
    action: CollectionAction<TItem>;
    item: TItem;
  } | null>(null);

  const { data, isLoading, isError, error } = useQuery({
    queryKey,
    queryFn: () => list(parentId),
    enabled: !!parentId,
  });

  const form = useForm<TForm>({
    resolver: zodResolver(schema as any) as any,
    defaultValues: emptyForm as DefaultValues<TForm>,
  });

  const invalidate = async () => {
    await queryClient.invalidateQueries({ queryKey });
    for (const key of invalidateKeys) {
      await queryClient.invalidateQueries({ queryKey: key });
    }
  };

  const failed = (verb: string) => (e: any) =>
    toast({
      title: 'Error',
      description: e?.message || `Failed to ${verb} ${singular}.`,
      variant: 'destructive',
    });

  const saveMutation = useMutation({
    mutationFn: async (values: TForm) =>
      editing ? update(parentId, getId(editing), values) : create(parentId, values),
    onSuccess: async () => {
      await invalidate();
      toast({ title: 'Saved', description: `${cap(singular)} ${editing ? 'updated' : 'added'}.` });
      setDialogOpen(false);
      setEditing(null);
    },
    onError: failed('save'),
  });

  const deleteMutation = useMutation({
    mutationFn: async (item: TItem) => remove?.(parentId, getId(item)),
    onSuccess: async () => {
      await invalidate();
      toast({ title: 'Removed', description: `${cap(singular)} removed.` });
      setPendingDelete(null);
    },
    onError: failed('remove'),
  });

  const actionMutation = useMutation({
    mutationFn: async ({ action, item }: { action: CollectionAction<TItem>; item: TItem }) =>
      action.run(item),
    onSuccess: async () => {
      await invalidate();
      setPendingAction(null);
    },
    onError: (e: any) => {
      failed('update')(e);
      setPendingAction(null);
    },
  });

  const openCreate = () => {
    setEditing(null);
    form.reset(emptyForm as DefaultValues<TForm>);
    setDialogOpen(true);
  };

  const openEdit = (item: TItem) => {
    setEditing(item);
    form.reset(toForm(item) as DefaultValues<TForm>);
    setDialogOpen(true);
  };

  const runAction = (action: CollectionAction<TItem>, item: TItem) => {
    if (action.confirm) {
      setPendingAction({ action, item });
      return;
    }
    actionMutation.mutate({ action, item });
  };

  const rows = data ?? [];
  const canAdd = !readOnly && allowCreate;
  const canEdit = !readOnly && allowUpdate;
  const canRemove = !readOnly && !!remove;
  const hasRowMenu = canEdit || canRemove || actions.length > 0;

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <p className="text-sm text-muted-foreground">
          {isLoading ? 'Loading…' : `${rows.length} ${rows.length === 1 ? singular : title}`}
        </p>
        {canAdd && (
          <Button size="sm" onClick={openCreate}>
            <Plus className="mr-2 h-4 w-4" />
            Add {singular}
          </Button>
        )}
      </div>

      <Card>
        <CardContent className="p-0">
          {isLoading ? (
            <div className="flex items-center justify-center py-12">
              <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
            </div>
          ) : isError ? (
            <EmptyState
              title={`Could not load ${title}`}
              description={(error as any)?.message || 'Please try again.'}
            />
          ) : rows.length === 0 ? (
            <EmptyState
              title={`No ${title} yet`}
              description={emptyDescription ?? `Add ${article(singular)} ${singular} to get started.`}
              action={
                canAdd ? (
                  <Button size="sm" variant="outline" onClick={openCreate}>
                    <Plus className="mr-2 h-4 w-4" />
                    Add {singular}
                  </Button>
                ) : undefined
              }
            />
          ) : (
            <div className="overflow-x-auto">
              <Table>
                <TableHeader>
                  <TableRow>
                    {columns.map((c) => (
                      <TableHead key={c.header} className={c.className}>
                        {c.header}
                      </TableHead>
                    ))}
                    {hasRowMenu && <TableHead className="w-[60px]" />}
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {rows.map((item) => (
                    <TableRow key={getId(item)}>
                      {columns.map((c) => (
                        <TableCell key={c.header} className={c.className}>
                          {c.cell(item)}
                        </TableCell>
                      ))}
                      {hasRowMenu && (
                        <TableCell>
                          <DropdownMenu>
                            <DropdownMenuTrigger asChild>
                              <Button variant="ghost" size="icon" className="h-8 w-8">
                                <MoreHorizontal className="h-4 w-4" />
                                <span className="sr-only">Actions</span>
                              </Button>
                            </DropdownMenuTrigger>
                            <DropdownMenuContent align="end">
                              {canEdit && (
                                <DropdownMenuItem onClick={() => openEdit(item)}>
                                  <Pencil className="mr-2 h-4 w-4" />
                                  Edit
                                </DropdownMenuItem>
                              )}
                              {actions
                                .filter((a) => (a.visible ? a.visible(item) : true))
                                .map((a) => {
                                  const label =
                                    typeof a.label === 'function' ? a.label(item) : a.label;
                                  return (
                                    <DropdownMenuItem
                                      key={label}
                                      onClick={() => runAction(a, item)}
                                      className={a.destructive ? 'text-red-600' : undefined}
                                    >
                                      {label}
                                    </DropdownMenuItem>
                                  );
                                })}
                              {canRemove && (
                                <>
                                  <DropdownMenuSeparator />
                                  <DropdownMenuItem
                                    className="text-red-600"
                                    onClick={() => setPendingDelete(item)}
                                  >
                                    <Trash2 className="mr-2 h-4 w-4" />
                                    Remove
                                  </DropdownMenuItem>
                                </>
                              )}
                            </DropdownMenuContent>
                          </DropdownMenu>
                        </TableCell>
                      )}
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>
          )}
        </CardContent>
      </Card>

      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
        <DialogContent className={dialogClassName}>
          <form onSubmit={form.handleSubmit((values) => saveMutation.mutate(values))}>
            <DialogHeader>
              <DialogTitle>{editing ? `Edit ${singular}` : `Add ${singular}`}</DialogTitle>
              <DialogDescription>
                {editing ? `Update this ${singular}.` : (dialogHint ?? `Add a new ${singular}.`)}
              </DialogDescription>
            </DialogHeader>

            <div className="max-h-[60vh] space-y-4 overflow-y-auto py-4">{renderFields(form)}</div>

            <DialogFooter>
              <Button
                type="button"
                variant="outline"
                onClick={() => setDialogOpen(false)}
                disabled={saveMutation.isPending}
              >
                Cancel
              </Button>
              <Button type="submit" disabled={saveMutation.isPending}>
                {saveMutation.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                {editing ? 'Save changes' : `Add ${singular}`}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      <ConfirmationDialog
        open={pendingDelete !== null}
        onOpenChange={(open) => !open && setPendingDelete(null)}
        title={`Remove ${singular}?`}
        description={`This will remove the ${singular}.`}
        confirmText="Remove"
        variant="destructive"
        isLoading={deleteMutation.isPending}
        onConfirm={async () => {
          if (pendingDelete) await deleteMutation.mutateAsync(pendingDelete);
        }}
      />

      <ConfirmationDialog
        open={pendingAction !== null}
        onOpenChange={(open) => !open && setPendingAction(null)}
        title={pendingAction?.action.confirm?.title ?? 'Confirm'}
        description={pendingAction?.action.confirm?.description}
        variant={pendingAction?.action.destructive ? 'destructive' : 'default'}
        isLoading={actionMutation.isPending}
        onConfirm={async () => {
          if (pendingAction) await actionMutation.mutateAsync(pendingAction);
        }}
      />
    </div>
  );
}

const cap = (s: string) => s.charAt(0).toUpperCase() + s.slice(1);

/** "a dependent" but "an address" — the singular labels are supplied per caller. */
const article = (s: string) => ('aeiou'.includes(s.charAt(0).toLowerCase()) ? 'an' : 'a');
