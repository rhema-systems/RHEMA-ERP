'use client';

import { use, useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2, Power, PowerOff, Trash2, Users } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { Skeleton } from '@/components/ui/skeleton';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import {
  ExternalAssociateForm,
  type ExternalAssociateFormValues,
} from '@/components/hr/external-associates/ExternalAssociateForm';
import { formatDate } from '@/lib/hr/attendance-format';
import { externalAssociateService } from '@/services/hr/external-associate.service';

/**
 * One external associate.
 *
 * ⚠ The panel count is read from the server (`interviewPanelCount`) and drives what this screen
 * lets you do. Delete is disabled while it is nonzero, and the reason is on screen before the
 * button is pressed rather than only in the refusal afterwards: the delete is a soft delete, the
 * panel's `OnDelete.Restrict` therefore never fires, and until slice 8 deleting an associate simply
 * removed them from every interview panel that carried them.
 */
export default function ExternalAssociateDetailPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = use(params);
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [submitting, setSubmitting] = useState(false);
  const [toggling, setToggling] = useState(false);
  const [deleting, setDeleting] = useState(false);
  const [confirmDelete, setConfirmDelete] = useState(false);

  const { data: associate, isLoading, isError } = useQuery({
    queryKey: ['hr', 'external-associates', id],
    queryFn: () => externalAssociateService.getById(id),
    enabled: !!id,
  });

  const refresh = () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: ['hr', 'external-associates'] }),
      queryClient.invalidateQueries({ queryKey: ['hr', 'external-associates', id] }),
    ]);

  const handleSave = async (values: ExternalAssociateFormValues) => {
    if (!associate) return;
    setSubmitting(true);
    try {
      await externalAssociateService.update(id, {
        id,
        title: values.title || null,
        firstName: values.firstName,
        middleName: values.middleName || '',
        lastName: values.lastName,
        email: values.email,
        phoneNumber: values.phoneNumber,
        companyName: values.companyName || null,
        role: values.role || null,
        isActive: values.isActive,
        // ⚠ Carried back untouched. Both are dormant and off the form; `hasFixedModule` is a
        // non-nullable bool on the server DTO, so omitting it would write `false` over whatever
        // was there.
        hasFixedModule: associate.hasFixedModule,
        moduleId: associate.moduleId ?? null,
      });
      await refresh();
      toast({ title: 'Saved', description: 'The associate was updated.' });
    } catch (error) {
      toast({
        title: 'Could not save',
        description: (error as Error)?.message || 'The changes were not saved.',
        variant: 'destructive',
      });
    } finally {
      setSubmitting(false);
    }
  };

  const handleToggle = async () => {
    if (!associate) return;
    setToggling(true);
    try {
      if (associate.isActive) {
        await externalAssociateService.deactivate(id);
        toast({
          title: 'Deactivated',
          description: `${associate.fullName} will no longer appear in the interview panel picker.`,
        });
      } else {
        await externalAssociateService.activate(id);
        toast({ title: 'Activated', description: `${associate.fullName} is back in the picker.` });
      }
      await refresh();
    } catch (error) {
      toast({
        title: 'Could not change their status',
        description: (error as Error)?.message || 'The change was not saved.',
        variant: 'destructive',
      });
    } finally {
      setToggling(false);
    }
  };

  const handleDelete = async () => {
    if (!associate) return false;
    setDeleting(true);
    try {
      await externalAssociateService.remove(id);
      await queryClient.invalidateQueries({ queryKey: ['hr', 'external-associates'] });
      toast({ title: 'Deleted', description: `${associate.fullName} was removed.` });
      router.push('/administration/hr/external-associates');
      return true;
    } catch (error) {
      toast({
        title: 'Could not delete this associate',
        description: (error as Error)?.message || 'Failed to delete the associate.',
        variant: 'destructive',
      });
      return false;
    } finally {
      setDeleting(false);
    }
  };

  if (isLoading) {
    return (
      <div className="space-y-6 p-6 max-w-4xl mx-auto">
        <Skeleton className="h-10 w-72" />
        <Skeleton className="h-96 w-full" />
      </div>
    );
  }

  if (isError || !associate) {
    return (
      <div className="space-y-6 p-6 max-w-4xl mx-auto">
        <PageHeader
          title="External Associate"
          backHref="/administration/hr/external-associates"
        />
        <EmptyState
          icon={Users}
          title="Associate not found"
          description="This associate no longer exists, or it belongs to another tenant."
        />
      </div>
    );
  }

  const onPanels = associate.interviewPanelCount > 0;

  return (
    <div className="space-y-6 p-6 max-w-4xl mx-auto">
      <PageHeader
        title={associate.fullName}
        description={`${associate.associateNumber} · added ${formatDate(associate.dateAdded)}`}
        backHref="/administration/hr/external-associates"
        actions={
          <div className="flex items-center gap-2">
            <Button variant="outline" onClick={handleToggle} disabled={toggling}>
              {toggling ? (
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              ) : associate.isActive ? (
                <PowerOff className="mr-2 h-4 w-4" />
              ) : (
                <Power className="mr-2 h-4 w-4" />
              )}
              {associate.isActive ? 'Deactivate' : 'Activate'}
            </Button>
            <Button
              variant="destructive"
              disabled={onPanels || deleting}
              title={
                onPanels
                  ? 'They sit on an interview panel — deactivate them instead.'
                  : undefined
              }
              onClick={() => setConfirmDelete(true)}
            >
              <Trash2 className="mr-2 h-4 w-4" /> Delete
            </Button>
          </div>
        }
      />

      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="text-base">Use</CardTitle>
          <CardDescription>
            Where this associate is relied on today. It is the reason a delete may be refused.
          </CardDescription>
        </CardHeader>
        <CardContent className="flex flex-wrap items-center gap-6">
          <div className="flex items-center gap-2">
            <Users className="h-4 w-4 text-muted-foreground" />
            <span className="text-sm">
              <strong>{associate.interviewPanelCount}</strong> interview{' '}
              {associate.interviewPanelCount === 1 ? 'panel' : 'panels'}
            </span>
          </div>
          <Badge
            className={
              associate.isActive
                ? 'bg-emerald-100 text-emerald-800 hover:bg-emerald-100'
                : 'bg-muted text-muted-foreground hover:bg-muted'
            }
          >
            {associate.isActive ? 'In the panel picker' : 'Not in the panel picker'}
          </Badge>
          {onPanels && (
            <span className="text-xs text-muted-foreground">
              Cannot be deleted while they sit on a panel — deactivating takes them out of the
              picker and leaves every interview&apos;s record intact.
            </span>
          )}
        </CardContent>
      </Card>

      <ExternalAssociateForm
        defaultValues={{
          title: associate.title ?? '',
          firstName: associate.firstName,
          middleName: associate.middleName ?? '',
          lastName: associate.lastName,
          email: associate.email,
          phoneNumber: associate.phoneNumber,
          companyName: associate.companyName ?? '',
          role: associate.role ?? '',
          isActive: associate.isActive,
        }}
        onSubmit={handleSave}
        submitting={submitting}
        submitLabel="Save Changes"
        onCancel={() => router.push('/administration/hr/external-associates')}
        associateNumber={associate.associateNumber}
      />

      <ConfirmationDialog
        open={confirmDelete}
        onOpenChange={setConfirmDelete}
        title="Delete associate"
        description={`Are you sure you want to delete ${associate.fullName}? Their EXT number is retired with them and is never issued again.`}
        confirmText="Delete"
        variant="destructive"
        isLoading={deleting}
        onConfirm={handleDelete}
      />
    </div>
  );
}
