'use client';

import { useState } from 'react';
import { useParams } from 'next/navigation';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2, Pencil, Plus, Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Switch } from '@/components/ui/switch';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Textarea } from '@/components/ui/textarea';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { CheckProviderPicker } from '@/components/hr/recruitment/CheckProviderPicker';
import { useToast } from '@/hooks/use-toast';
import { humanizeEnum } from '@/lib/hr/attendance-format';
import { preEmploymentCheckTemplateService } from '@/services/hr/offers.service';
import {
  PRE_EMPLOYMENT_CHECK_TYPES,
  type CreatePreEmploymentCheckTemplateItem,
  type PreEmploymentCheckType,
} from '@/types/hr/offers';

const blankItem = (): CreatePreEmploymentCheckTemplateItem => ({
  checkType: 'BackgroundCheck',
  defaultServiceProvider: '',
  defaultServiceProviderSupplierId: null,
  instructions: '',
  isMandatory: true,
  isBlockingOnFail: true,
  expectedDays: null,
});

export default function PreEmploymentCheckTemplateDetailPage() {
  const params = useParams();
  const id = (params?.id as string) ?? '';
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [editingHeader, setEditingHeader] = useState(false);
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [isActive, setIsActive] = useState(true);

  const [itemDialog, setItemDialog] = useState<{ id?: string } | null>(null);
  const [itemForm, setItemForm] = useState<CreatePreEmploymentCheckTemplateItem>(blankItem);
  const [deletingItemId, setDeletingItemId] = useState<string | null>(null);

  const { data: template, isLoading, isError } = useQuery({
    queryKey: ['hr', 'pre-employment-check-template', id],
    queryFn: () => preEmploymentCheckTemplateService.getById(id),
    enabled: !!id,
  });

  const refresh = () => queryClient.invalidateQueries({ queryKey: ['hr', 'pre-employment-check-template', id] });

  const openHeaderEdit = () => {
    if (!template) return;
    setName(template.name);
    setDescription(template.description ?? '');
    setIsActive(template.isActive);
    setEditingHeader(true);
  };

  const saveHeader = useMutation({
    mutationFn: () =>
      preEmploymentCheckTemplateService.update(id, {
        name: name.trim(),
        description: description.trim() || null,
        isActive,
      }),
    onSuccess: async () => {
      await refresh();
      setEditingHeader(false);
      toast({ title: 'Saved' });
    },
    onError: (e: any) => toast({ title: 'Could not save', description: e?.message, variant: 'destructive' }),
  });

  const openAddItem = () => {
    setItemForm(blankItem());
    setItemDialog({});
  };

  const openEditItem = (itemId: string) => {
    const item = template?.items.find((i) => i.id === itemId);
    if (!item) return;
    setItemForm({
      checkType: item.checkType,
      defaultServiceProvider: item.defaultServiceProvider ?? '',
      defaultServiceProviderSupplierId: item.defaultServiceProviderSupplierId ?? null,
      instructions: item.instructions ?? '',
      isMandatory: item.isMandatory,
      isBlockingOnFail: item.isBlockingOnFail,
      expectedDays: item.expectedDays ?? null,
    });
    setItemDialog({ id: itemId });
  };

  const saveItem = useMutation({
    mutationFn: () => {
      if (itemDialog?.id) {
        // CheckType is excluded on update — delete and re-add to change it.
        return preEmploymentCheckTemplateService.updateItem(id, itemDialog.id, {
          defaultServiceProvider: itemForm.defaultServiceProvider?.trim() || null,
          defaultServiceProviderSupplierId: itemForm.defaultServiceProviderSupplierId || null,
          instructions: itemForm.instructions?.trim() || null,
          isMandatory: itemForm.isMandatory,
          isBlockingOnFail: itemForm.isBlockingOnFail,
          expectedDays: itemForm.expectedDays,
        });
      }
      return preEmploymentCheckTemplateService.addItem(id, {
        ...itemForm,
        defaultServiceProvider: itemForm.defaultServiceProvider?.trim() || null,
        instructions: itemForm.instructions?.trim() || null,
      });
    },
    onSuccess: async () => {
      await refresh();
      setItemDialog(null);
      toast({ title: itemDialog?.id ? 'Check updated' : 'Check added' });
    },
    onError: (e: any) => toast({ title: 'Could not save', description: e?.message, variant: 'destructive' }),
  });

  const removeItem = useMutation({
    mutationFn: (itemId: string) => preEmploymentCheckTemplateService.removeItem(id, itemId),
    onSuccess: async () => {
      await refresh();
      setDeletingItemId(null);
      toast({ title: 'Check removed' });
    },
    onError: (e: any) => toast({ title: 'Could not remove it', description: e?.message, variant: 'destructive' }),
  });

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-24">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (isError || !template) {
    return <EmptyState title="Template not found" description="It may have been removed." />;
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title={template.name}
        description={template.description ?? undefined}
        backHref="/administration/hr/recruitment/check-templates"
        actions={
          <Button variant="outline" onClick={openHeaderEdit}>
            <Pencil className="mr-2 h-4 w-4" /> Edit
          </Button>
        }
      />

      <Card>
        <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-3">
          <CardTitle className="text-base">Checks</CardTitle>
          <Button size="sm" onClick={openAddItem}>
            <Plus className="mr-2 h-4 w-4" /> Add a check
          </Button>
        </CardHeader>
        <CardContent className="p-0">
          {template.items.length === 0 ? (
            <div className="py-8">
              <EmptyState title="No checks yet" description="Add each check this template should seed." />
            </div>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Type</TableHead>
                  <TableHead>Service provider</TableHead>
                  <TableHead className="text-right">Expected days</TableHead>
                  <TableHead>Mandatory</TableHead>
                  <TableHead>Blocking</TableHead>
                  <TableHead className="w-24" />
                </TableRow>
              </TableHeader>
              <TableBody>
                {template.items.map((item) => (
                  <TableRow key={item.id}>
                    <TableCell className="font-medium">{humanizeEnum(item.checkType)}</TableCell>
                    <TableCell className="text-sm text-muted-foreground">
                      {item.defaultServiceProvider || '—'}
                    </TableCell>
                    <TableCell className="text-right tabular-nums">{item.expectedDays ?? '—'}</TableCell>
                    <TableCell>{item.isMandatory ? 'Yes' : 'No'}</TableCell>
                    <TableCell>{item.isBlockingOnFail ? 'Yes' : 'No'}</TableCell>
                    <TableCell>
                      <div className="flex justify-end gap-1">
                        <Button variant="ghost" size="icon" onClick={() => openEditItem(item.id)}>
                          <Pencil className="h-4 w-4" />
                        </Button>
                        <Button variant="ghost" size="icon" onClick={() => setDeletingItemId(item.id)}>
                          <Trash2 className="h-4 w-4" />
                        </Button>
                      </div>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      {/* ── header edit ───────────────────────────────────────────────────── */}
      <Dialog open={editingHeader} onOpenChange={setEditingHeader}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Edit template</DialogTitle>
          </DialogHeader>
          <div className="space-y-4 py-2">
            <div className="space-y-1.5">
              <Label>Name</Label>
              <Input value={name} onChange={(e) => setName(e.target.value)} />
            </div>
            <div className="space-y-1.5">
              <Label>Description</Label>
              <Textarea rows={3} value={description} onChange={(e) => setDescription(e.target.value)} />
            </div>
            <div className="flex items-center justify-between rounded-md border p-3">
              <Label>Active</Label>
              <Switch checked={isActive} onCheckedChange={setIsActive} />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setEditingHeader(false)}>
              Cancel
            </Button>
            <Button disabled={!name.trim() || saveHeader.isPending} onClick={() => saveHeader.mutate()}>
              {saveHeader.isPending ? 'Saving…' : 'Save'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ── item add/edit ─────────────────────────────────────────────────── */}
      <Dialog open={!!itemDialog} onOpenChange={(o) => !o && setItemDialog(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{itemDialog?.id ? 'Edit check' : 'Add a check'}</DialogTitle>
            {itemDialog?.id && (
              <DialogDescription>
                The type cannot be changed here — delete and re-add to change it.
              </DialogDescription>
            )}
          </DialogHeader>
          <div className="grid gap-4 py-2">
            <div className="space-y-1.5">
              <Label>Type</Label>
              <Select
                value={itemForm.checkType}
                disabled={!!itemDialog?.id}
                onValueChange={(v) => setItemForm({ ...itemForm, checkType: v as PreEmploymentCheckType })}
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {PRE_EMPLOYMENT_CHECK_TYPES.map((t) => (
                    <SelectItem key={t} value={t}>
                      {humanizeEnum(t)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            {/* Round 3, lane G (D-14): the default provider from the suppliers set up for this check type. */}
            <CheckProviderPicker
              idPrefix="defaultProvider"
              checkType={itemForm.checkType}
              supplierId={itemForm.defaultServiceProviderSupplierId ?? null}
              name={itemForm.defaultServiceProvider ?? ''}
              onChange={({ supplierId, name }) =>
                setItemForm({ ...itemForm, defaultServiceProviderSupplierId: supplierId || null, defaultServiceProvider: name })
              }
            />
            <div className="space-y-1.5">
              <Label htmlFor="templateItemInstructions">Instructions</Label>
              <Textarea
                id="templateItemInstructions"
                rows={2}
                value={itemForm.instructions ?? ''}
                onChange={(e) => setItemForm({ ...itemForm, instructions: e.target.value })}
              />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="templateExpectedDays">Expected turnaround (days)</Label>
              <Input
                id="templateExpectedDays"
                type="number"
                min={0}
                value={itemForm.expectedDays ?? ''}
                onChange={(e) =>
                  setItemForm({
                    ...itemForm,
                    expectedDays: e.target.value === '' ? null : Number(e.target.value),
                  })
                }
                className="w-32"
              />
            </div>
            <div className="flex items-center gap-2">
              <Checkbox
                id="templateItemMandatory"
                checked={itemForm.isMandatory}
                onCheckedChange={(c) => setItemForm({ ...itemForm, isMandatory: c === true })}
              />
              <Label htmlFor="templateItemMandatory" className="font-normal">
                Mandatory
              </Label>
            </div>
            <div className="flex items-center gap-2">
              <Checkbox
                id="templateItemBlocking"
                checked={itemForm.isBlockingOnFail}
                onCheckedChange={(c) => setItemForm({ ...itemForm, isBlockingOnFail: c === true })}
              />
              <Label htmlFor="templateItemBlocking" className="font-normal">
                Blocks clearance on failure
              </Label>
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setItemDialog(null)}>
              Cancel
            </Button>
            <Button onClick={() => saveItem.mutate()} disabled={saveItem.isPending}>
              {saveItem.isPending ? 'Saving…' : 'Save'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <ConfirmationDialog
        open={!!deletingItemId}
        onOpenChange={(o) => !o && setDeletingItemId(null)}
        title="Remove this check?"
        description="It only affects the template — checks already applied to an offer are unaffected."
        confirmText={removeItem.isPending ? 'Removing…' : 'Remove'}
        variant="destructive"
        onConfirm={async () => {
          if (deletingItemId) await removeItem.mutateAsync(deletingItemId);
          return true;
        }}
      />
    </div>
  );
}
