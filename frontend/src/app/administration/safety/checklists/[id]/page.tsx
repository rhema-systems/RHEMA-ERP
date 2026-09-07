'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useParams, useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { CheckCircle2, Copy, Loader2, Archive, Trash2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { ChecklistFormTab } from '@/components/hr/safety/checklist/ChecklistFormTab';
import { ChecklistFieldsTab } from '@/components/hr/safety/checklist/ChecklistFieldsTab';
import { ChecklistSectionsTab } from '@/components/hr/safety/checklist/ChecklistSectionsTab';
import { ChecklistOutcomesTab } from '@/components/hr/safety/checklist/ChecklistOutcomesTab';
import { ChecklistSignatoriesTab } from '@/components/hr/safety/checklist/ChecklistSignatoriesTab';
import { ChecklistPrintForm } from '@/components/hr/safety/checklist/ChecklistPrintForm';
import { safetyChecklistService } from '@/services/hr/safety-checklist.service';

type Action = 'publish' | 'retire' | 'newVersion' | 'delete';

/**
 * The checklist builder (docs/HR/HR-SHE-INSPECTION-CHECKLIST-BUILDER-DESIGN.md §5): one template,
 * shaped across six tabs. Draft → Publish freezes the structure; New version clones it under the
 * same number; Retire withdraws it from the scheduling picker. Past inspections keep their version.
 */
export default function ChecklistDetailPage() {
  const params = useParams<{ id: string }>();
  const id = params.id;
  const router = useRouter();
  const qc = useQueryClient();
  const { toast } = useToast();
  const [confirm, setConfirm] = useState<Action | null>(null);
  const [busy, setBusy] = useState(false);

  const { data: checklist, isLoading } = useQuery({
    queryKey: ['hr', 'safety-checklist', id],
    queryFn: () => safetyChecklistService.getById(id),
    enabled: !!id,
  });

  const refresh = () => qc.invalidateQueries({ queryKey: ['hr', 'safety-checklist', id] });
  const refreshList = () => qc.invalidateQueries({ queryKey: ['hr', 'safety-checklists'] });

  const run = async () => {
    if (!confirm || !checklist) return;
    setBusy(true);
    try {
      switch (confirm) {
        case 'publish': {
          const c = await safetyChecklistService.publish(id);
          toast({ title: 'Published', description: `${c.checklistNumber} v${c.version} is now offered for new inspections.` });
          break;
        }
        case 'retire': {
          const c = await safetyChecklistService.retire(id);
          toast({ title: 'Retired', description: `${c.checklistNumber} v${c.version} stays on past inspections but is no longer offered.` });
          break;
        }
        case 'newVersion': {
          const c = await safetyChecklistService.createNewVersion(id);
          toast({ title: 'New version created', description: `${c.checklistNumber} v${c.version} is a draft — edit it, then publish.` });
          await refreshList();
          setConfirm(null);
          router.push(`/administration/safety/checklists/${c.id}`);
          return;
        }
        case 'delete': {
          await safetyChecklistService.remove(id);
          toast({ title: 'Deleted', description: `${checklist.checklistNumber} v${checklist.version} removed.` });
          await refreshList();
          setConfirm(null);
          router.push('/administration/safety/checklists');
          return;
        }
      }
      setConfirm(null);
      await Promise.all([refresh(), refreshList()]);
    } catch (e: any) {
      toast({ title: 'Refused', description: e?.message || 'The action failed.', variant: 'destructive' });
    } finally {
      setBusy(false);
    }
  };

  if (isLoading || !checklist) {
    return (
      <div className="flex items-center justify-center py-24">
        <Loader2 className="text-muted-foreground h-6 w-6 animate-spin" />
      </div>
    );
  }

  const isDraft = checklist.status === 'Draft';
  const dialogs: Record<Action, { title: string; description: string; confirmText: string; variant?: 'destructive' }> = {
    publish: {
      title: `Publish ${checklist.checklistNumber} v${checklist.version}?`,
      description:
        'The structure freezes: fields, sections, items, outcomes and signatories can no longer change. Any earlier published version of this number retires. The server checks the form is complete first.',
      confirmText: 'Publish',
    },
    retire: {
      title: `Retire ${checklist.checklistNumber} v${checklist.version}?`,
      description: 'It stops being offered for new inspections. Inspections already run against it keep it.',
      confirmText: 'Retire',
    },
    newVersion: {
      title: `Create v${checklist.version + 1} of ${checklist.checklistNumber}?`,
      description: 'A draft copy of this form. Edit it freely, then publish it to replace this version.',
      confirmText: 'Create draft',
    },
    delete: {
      title: `Delete ${checklist.checklistNumber} v${checklist.version}?`,
      description: 'Refused if any inspection references it — retire instead in that case.',
      confirmText: 'Delete',
      variant: 'destructive',
    },
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={`${checklist.checklistNumber} v${checklist.version} — ${checklist.name}`}
        description={checklist.description ?? undefined}
        backHref="/administration/safety/checklists"
        actions={
          <div className="flex flex-wrap items-center gap-2">
            <Badge variant="outline">{checklist.typeName}</Badge>
            <StatusBadge status={checklist.statusName} />
            <StatusBadge status={checklist.isActive ? 'Active' : 'Inactive'} />
            {isDraft ? (
              <>
                <Button onClick={() => setConfirm('publish')}>
                  <CheckCircle2 className="mr-2 h-4 w-4" />
                  Publish
                </Button>
                <Button variant="outline" className="text-red-600" onClick={() => setConfirm('delete')}>
                  <Trash2 className="mr-2 h-4 w-4" />
                  Delete
                </Button>
              </>
            ) : (
              <>
                <Button onClick={() => setConfirm('newVersion')}>
                  <Copy className="mr-2 h-4 w-4" />
                  New version
                </Button>
                {checklist.status === 'Published' && (
                  <Button variant="outline" onClick={() => setConfirm('retire')}>
                    <Archive className="mr-2 h-4 w-4" />
                    Retire
                  </Button>
                )}
              </>
            )}
          </div>
        }
      />

      {!isDraft && (
        <p className="rounded-md bg-muted p-3 text-sm">
          This version is <b>{checklist.statusName.toLowerCase()}</b>
          {checklist.publishedAt ? ` (published ${new Date(checklist.publishedAt).toLocaleDateString()}${checklist.publishedByName ? ` by ${checklist.publishedByName}` : ''})` : ''}
          . Its structure is locked so past inspections keep the form they were done on — create a new version to change it.
          {checklist.previousVersionId ? (
            <>
              {' '}
              <Link href={`/administration/safety/checklists/${checklist.previousVersionId}`} className="underline">
                Previous version
              </Link>
            </>
          ) : null}
        </p>
      )}

      <Tabs defaultValue={isDraft ? 'sections' : 'preview'}>
        <TabsList>
          <TabsTrigger value="form">Form</TabsTrigger>
          <TabsTrigger value="fields">Header fields ({checklist.fields.length})</TabsTrigger>
          <TabsTrigger value="sections">Sections &amp; items ({checklist.itemCount})</TabsTrigger>
          <TabsTrigger value="outcomes">Outcomes ({checklist.outcomes.length})</TabsTrigger>
          <TabsTrigger value="signatories">Signatories ({checklist.signatories.length})</TabsTrigger>
          <TabsTrigger value="preview">Preview</TabsTrigger>
        </TabsList>
        <TabsContent value="form" className="mt-4">
          <ChecklistFormTab checklist={checklist} onSaved={() => Promise.all([refresh(), refreshList()])} />
        </TabsContent>
        <TabsContent value="fields" className="mt-4">
          <ChecklistFieldsTab checklist={checklist} />
        </TabsContent>
        <TabsContent value="sections" className="mt-4">
          <ChecklistSectionsTab checklist={checklist} />
        </TabsContent>
        <TabsContent value="outcomes" className="mt-4">
          <ChecklistOutcomesTab checklist={checklist} />
        </TabsContent>
        <TabsContent value="signatories" className="mt-4">
          <ChecklistSignatoriesTab checklist={checklist} />
        </TabsContent>
        <TabsContent value="preview" className="mt-4">
          <div className="overflow-x-auto rounded-md border">
            <ChecklistPrintForm checklist={checklist} className="mx-auto max-w-[210mm]" />
          </div>
        </TabsContent>
      </Tabs>

      {confirm && (
        <ConfirmationDialog
          open
          onOpenChange={(open) => !open && !busy && setConfirm(null)}
          title={dialogs[confirm].title}
          description={dialogs[confirm].description}
          confirmText={dialogs[confirm].confirmText}
          variant={dialogs[confirm].variant}
          isLoading={busy}
          onConfirm={run}
        />
      )}
    </div>
  );
}
