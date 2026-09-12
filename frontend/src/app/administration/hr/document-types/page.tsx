'use client';

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { FileText, Loader2, Pencil, Plus, Sparkles, Trash2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { useToast } from '@/hooks/use-toast';
import { employeeDocumentService } from '@/services/hr/employee-document.service';
import type { EmployeeDocumentType } from '@/types/hr/employee-documents';

const BLANK = {
  name: '', code: '', description: '', hasExpiry: false, isActive: true,
};

/**
 * The employee document vocabulary.
 *
 * ⚠ **A table rather than an enum, because two features have to speak it**: an employee HOLDS
 * documents and a position REQUIRES them. Every other HR document vocabulary in the codebase is a
 * compiled enum precisely because nothing else reads it — and a requirement naming a compiled value
 * would mean TDC could never add a document kind without a release.
 */
export default function EmployeeDocumentTypesPage() {
  const { toast } = useToast();
  const qc = useQueryClient();

  const [editing, setEditing] = useState<EmployeeDocumentType | null>(null);
  const [open, setOpen] = useState(false);
  const [form, setForm] = useState(BLANK);

  const { data: types, isLoading } = useQuery({
    queryKey: ['employee-document-types', 'all'],
    queryFn: () => employeeDocumentService.getTypes(true),
  });

  const refresh = () => void qc.invalidateQueries({ queryKey: ['employee-document-types'] });

  const seed = useMutation({
    mutationFn: () => employeeDocumentService.seedDefaultTypes(),
    onSuccess: (r) => {
      // Saying "0 added" plainly matters: a second run that changes nothing is the correct outcome
      // and a silent success would look identical to one that never ran.
      toast({
        title: r.added === 0 ? 'Nothing to add' : `${r.added} document type${r.added === 1 ? '' : 's'} added`,
        description: r.added === 0 ? 'Every default is already on the list.' : undefined,
      });
      refresh();
    },
    onError: (e: unknown) => toast({
      variant: 'destructive', title: 'Not seeded',
      description: e instanceof Error ? e.message : 'Refused',
    }),
  });

  const save = useMutation({
    mutationFn: () => {
      const payload = {
        name: form.name.trim(),
        code: form.code.trim() || null,
        description: form.description.trim() || null,
        hasExpiry: form.hasExpiry,
        isActive: form.isActive,
      };
      return editing
        ? employeeDocumentService.updateType(editing.id, payload)
        : employeeDocumentService.createType(payload);
    },
    onSuccess: () => {
      toast({ title: editing ? 'Document type updated' : 'Document type added' });
      setOpen(false); setEditing(null); setForm(BLANK);
      refresh();
    },
    onError: (e: unknown) => toast({
      variant: 'destructive', title: 'Not saved',
      // The server names the clash — "A document type named 'X' already exists" — so show it.
      description: e instanceof Error ? e.message : 'Refused',
    }),
  });

  const remove = useMutation({
    mutationFn: (id: string) => employeeDocumentService.deleteType(id),
    onSuccess: () => { toast({ title: 'Document type removed' }); refresh(); },
    onError: (e: unknown) => toast({
      variant: 'destructive',
      title: 'Not removed',
      // ⚠ The refusal explains the alternative — mark it inactive — so pass it through verbatim
      // rather than replacing it with "could not delete".
      description: e instanceof Error ? e.message : 'Refused',
    }),
  });

  const openNew = () => { setEditing(null); setForm(BLANK); setOpen(true); };
  const openEdit = (t: EmployeeDocumentType) => {
    setEditing(t);
    setForm({
      name: t.name,
      code: t.code ?? '',
      description: t.description ?? '',
      hasExpiry: t.hasExpiry,
      isActive: t.isActive,
    });
    setOpen(true);
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Employee document types"
        description="What can be filed on an employee's record, and what a position can require."
        backHref="/administration/hr"
        actions={
          <div className="flex gap-2">
            <Button variant="outline" onClick={() => seed.mutate()} disabled={seed.isPending}>
              {seed.isPending ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Sparkles className="mr-2 h-4 w-4" />}
              Add the defaults
            </Button>
            <Button onClick={openNew}>
              <Plus className="mr-2 h-4 w-4" />
              New type
            </Button>
          </div>
        }
      />

      <Card>
        <CardContent className="pt-6">
          {isLoading ? (
            <div className="flex justify-center p-8">
              <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
            </div>
          ) : !types || types.length === 0 ? (
            <EmptyState
              icon={FileText}
              title="No document types yet"
              description="Add the defaults to start with a sensible list, then rename and extend it."
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Name</TableHead>
                  <TableHead>Code</TableHead>
                  <TableHead>Expires</TableHead>
                  <TableHead className="text-right">On file</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead className="w-[100px]" />
                </TableRow>
              </TableHeader>
              <TableBody>
                {types.map((t) => (
                  <TableRow key={t.id}>
                    <TableCell>
                      <p className="font-medium">{t.name}</p>
                      {t.description && (
                        <p className="text-xs text-muted-foreground">{t.description}</p>
                      )}
                    </TableCell>
                    <TableCell className="text-muted-foreground">{t.code ?? '—'}</TableCell>
                    <TableCell>
                      {t.hasExpiry
                        ? <Badge variant="secondary">Asks for an expiry</Badge>
                        : <span className="text-muted-foreground">—</span>}
                    </TableCell>
                    <TableCell className="text-right tabular-nums">{t.documentCount}</TableCell>
                    <TableCell>
                      <Badge variant={t.isActive ? 'default' : 'secondary'}>
                        {t.isActive ? 'Active' : 'Inactive'}
                      </Badge>
                    </TableCell>
                    <TableCell>
                      <div className="flex justify-end gap-1">
                        <Button variant="ghost" size="icon" aria-label={`Edit ${t.name}`} onClick={() => openEdit(t)}>
                          <Pencil className="h-4 w-4" />
                        </Button>
                        {/* ⚠ Offered even when in use. The server refuses with the reason and the
                            alternative — mark it inactive — and hiding the button would leave the
                            user guessing why it cannot be done. */}
                        <Button
                          variant="ghost" size="icon"
                          aria-label={`Remove ${t.name}`}
                          onClick={() => remove.mutate(t.id)}
                          disabled={remove.isPending}
                        >
                          <Trash2 className="h-4 w-4 text-destructive" />
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

      <Dialog open={open} onOpenChange={(o) => { setOpen(o); if (!o) { setEditing(null); setForm(BLANK); } }}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{editing ? 'Edit document type' : 'New document type'}</DialogTitle>
            <DialogDescription>
              A kind of document an employee can have on file, and that a position can require.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label>Name</Label>
              <Input
                value={form.name}
                onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))}
                placeholder="Professional indemnity certificate"
              />
            </div>
            <div className="space-y-2">
              <Label>Code</Label>
              <Input
                value={form.code}
                onChange={(e) => setForm((f) => ({ ...f, code: e.target.value }))}
                placeholder="Optional short code"
              />
            </div>
            <div className="space-y-2">
              <Label>Description</Label>
              <Textarea
                rows={2} value={form.description}
                onChange={(e) => setForm((f) => ({ ...f, description: e.target.value }))}
              />
            </div>
            <div className="flex items-center gap-2">
              <Checkbox
                id="hasExpiry" checked={form.hasExpiry}
                onCheckedChange={(v) => setForm((f) => ({ ...f, hasExpiry: v === true }))}
              />
              <Label htmlFor="hasExpiry" className="cursor-pointer">
                Documents of this kind expire
              </Label>
            </div>
            <p className="pl-6 text-xs text-muted-foreground">
              Decides whether the upload form asks for an expiry date, and whether a lapsed copy
              stops satisfying a position&apos;s requirement.
            </p>
            <div className="flex items-center gap-2">
              <Checkbox
                id="isActive" checked={form.isActive}
                onCheckedChange={(v) => setForm((f) => ({ ...f, isActive: v === true }))}
              />
              <Label htmlFor="isActive" className="cursor-pointer">Offered on new uploads</Label>
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setOpen(false)}>Cancel</Button>
            <Button onClick={() => save.mutate()} disabled={save.isPending || !form.name.trim()}>
              {save.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Save
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
