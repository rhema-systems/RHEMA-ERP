'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ClipboardCheck, Loader2, Plus, Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
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
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Textarea } from '@/components/ui/textarea';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { CheckProvidersPanel } from '@/components/hr/recruitment/CheckProvidersPanel';
import { useAuth } from '@/hooks/use-auth';
import { useToast } from '@/hooks/use-toast';
import { preEmploymentCheckTemplateService } from '@/services/hr/offers.service';
import type { PreEmploymentCheckTemplate } from '@/types/hr/offers';

/**
 * Reusable pre-employment check templates — "which checks a hire of this kind needs". Setup, so it
 * lives under Administration; applying one is done from an offer's Pre-employment checks tab.
 */
export default function PreEmploymentCheckTemplatesPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const { hasAnyRole } = useAuth();
  const isHr = hasAnyRole(['SuperAdmin', 'HR']);

  const [creating, setCreating] = useState(false);
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [deleting, setDeleting] = useState<PreEmploymentCheckTemplate | null>(null);

  const { data, isLoading, isError } = useQuery({
    queryKey: ['hr', 'pre-employment-check-templates'],
    queryFn: () => preEmploymentCheckTemplateService.getAll(),
  });

  const refresh = () => queryClient.invalidateQueries({ queryKey: ['hr', 'pre-employment-check-templates'] });

  const create = useMutation({
    mutationFn: () =>
      preEmploymentCheckTemplateService.create({
        name: name.trim(),
        description: description.trim() || null,
        // Items are added on the detail page — a template with no items yet is legal.
        items: [],
      }),
    onSuccess: async (created) => {
      await refresh();
      setCreating(false);
      setName('');
      setDescription('');
      toast({ title: 'Template created', description: 'Add its checks next.' });
      router.push(`/administration/hr/recruitment/check-templates/${created.id}`);
    },
    onError: (e: any) => toast({ title: 'Could not create', description: e?.message, variant: 'destructive' }),
  });

  const remove = useMutation({
    mutationFn: (id: string) => preEmploymentCheckTemplateService.remove(id),
    onSuccess: async () => {
      await refresh();
      setDeleting(null);
      toast({ title: 'Template removed' });
    },
    onError: (e: any) => toast({ title: 'Could not remove it', description: e?.message, variant: 'destructive' }),
  });

  const rows = data ?? [];

  return (
    <div className="space-y-6">
      <PageHeader
        title="Pre-employment check templates"
        description="Standard sets of checks — medical, police clearance, references — applied to an offer in one step."
        backHref="/administration/hr"
        actions={
          isHr ? (
            <Button onClick={() => setCreating(true)}>
              <Plus className="mr-2 h-4 w-4" />
              New template
            </Button>
          ) : undefined
        }
      />

      {/* Round 3, lane G (D-14): the suppliers behind the checks, set up once and offered by type. */}
      <CheckProvidersPanel canManage={isHr} />

      <Card>
        <CardContent className="p-0">
          {isLoading ? (
            <div className="flex items-center justify-center py-16">
              <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
            </div>
          ) : isError ? (
            <EmptyState title="Could not load templates" description="Try again in a moment." />
          ) : rows.length === 0 ? (
            <EmptyState
              icon={ClipboardCheck}
              title="No templates yet"
              description="Without one, each offer's checks are added individually."
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Name</TableHead>
                  <TableHead>Description</TableHead>
                  <TableHead className="w-24 text-right">Checks</TableHead>
                  <TableHead className="w-28">Status</TableHead>
                  {isHr && <TableHead className="w-16" />}
                </TableRow>
              </TableHeader>
              <TableBody>
                {rows.map((t) => (
                  <TableRow key={t.id}>
                    <TableCell className="font-medium">
                      <Link
                        href={`/administration/hr/recruitment/check-templates/${t.id}`}
                        className="hover:underline"
                      >
                        {t.name}
                      </Link>
                    </TableCell>
                    <TableCell className="text-sm text-muted-foreground">{t.description ?? '—'}</TableCell>
                    <TableCell className="text-right tabular-nums">{t.itemCount}</TableCell>
                    <TableCell>
                      <StatusBadge active={t.isActive} />
                    </TableCell>
                    {isHr && (
                      <TableCell>
                        <Button variant="ghost" size="icon" onClick={() => setDeleting(t)}>
                          <Trash2 className="h-4 w-4" />
                        </Button>
                      </TableCell>
                    )}
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      <Dialog open={creating} onOpenChange={(o) => !o && setCreating(false)}>
        <DialogContent className="sm:max-w-[480px]">
          <DialogHeader>
            <DialogTitle>New template</DialogTitle>
            <DialogDescription>Create the template, then add its checks.</DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-1.5">
              <Label>Name</Label>
              <Input value={name} onChange={(e) => setName(e.target.value)} placeholder="Standard external hire" />
            </div>
            <div className="space-y-1.5">
              <Label>Description</Label>
              <Textarea value={description} onChange={(e) => setDescription(e.target.value)} rows={3} />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setCreating(false)}>
              Cancel
            </Button>
            <Button disabled={!name.trim() || create.isPending} onClick={() => create.mutate()}>
              {create.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Create
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <ConfirmationDialog
        open={!!deleting}
        onOpenChange={(o) => !o && setDeleting(null)}
        title={`Delete "${deleting?.name}"?`}
        description="This cannot be undone. Checks already applied from it to an offer are unaffected."
        confirmText={remove.isPending ? 'Deleting…' : 'Delete'}
        variant="destructive"
        onConfirm={async () => {
          if (deleting) await remove.mutateAsync(deleting.id);
          return true;
        }}
      />
    </div>
  );
}
