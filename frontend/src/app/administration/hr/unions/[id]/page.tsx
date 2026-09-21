'use client';

import { use, useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Camera, FileText, Loader2, Paperclip, Pencil, Plus, Trash2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { useToast } from '@/components/ui/use-toast';
import { useAuth } from '@/hooks/use-auth';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { GatedPhoto, PhotoDialog } from '@/components/hr/common/PhotoDialog';
import { UnionForm, type UnionFormValues } from '@/components/hr/unions/UnionForm';
import { AgreementDialog } from '@/components/hr/unions/AgreementDialog';
import { UnionContactsPanel } from '@/components/hr/unions/UnionContactsPanel';
import { UnionDocumentsPanel } from '@/components/hr/unions/UnionDocumentsPanel';
import { formatDate } from '@/lib/hr/attendance-format';
import { unionService } from '@/services/hr/union.service';
import type {
  CollectiveBargainingAgreement,
  CollectiveBargainingAgreementStatus,
} from '@/types/hr/union';

const STATUS_STYLES: Record<CollectiveBargainingAgreementStatus, string> = {
  Active: 'bg-emerald-100 text-emerald-800 hover:bg-emerald-100',
  Pending: 'bg-blue-100 text-blue-800 hover:bg-blue-100',
  Expired: 'bg-amber-100 text-amber-800 hover:bg-amber-100',
  Inactive: 'bg-muted text-muted-foreground hover:bg-muted',
};

/**
 * A union: its collective agreements, the people we deal with there, its files, its logo.
 *
 * ⚠ The status badge reads `agreement.status` from the server and never re-derives it. `isActive` is
 * a flag somebody sets and nothing clears — an agreement that ran 2019-2021 still reads
 * `isActive: true` today — so "in force" is a question about two dates as well, answered once on
 * the server so every screen answers it the same way.
 *
 * Round 3, lane U: the Contacts tab holds `UnionContact` rows (an employee or an external person,
 * with a role; one primary) and the union's contact trio on the Details tab becomes a read-only
 * mirror of the primary once any contact exists (D-8). The Documents tab holds the files — the
 * signed copy of an agreement is a document of kind CollectiveAgreement naming that agreement.
 * The logo is a gated image on the header, fetched with the bearer token, never a public URL.
 */
export default function UnionDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const { hasAnyRole } = useAuth();
  // Writes are HR.Employee.Write (SuperAdmin / TenantAdmin / HR); document delete is HR.Employee.Admin.
  const canWrite = hasAnyRole(['SuperAdmin', 'TenantAdmin', 'HR']);
  const canDeleteDocuments = hasAnyRole(['SuperAdmin', 'TenantAdmin']);

  const [submitting, setSubmitting] = useState(false);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editing, setEditing] = useState<CollectiveBargainingAgreement | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<CollectiveBargainingAgreement | null>(null);
  const [deleting, setDeleting] = useState(false);
  const [logoOpen, setLogoOpen] = useState(false);
  const [logoVersion, setLogoVersion] = useState(0);

  const { data: union, isLoading, isError } = useQuery({
    queryKey: ['hr', 'unions', id],
    queryFn: () => unionService.getById(id),
    enabled: !!id,
  });

  const refresh = () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: ['hr', 'unions'] }),
      queryClient.invalidateQueries({ queryKey: ['hr', 'unions', id] }),
    ]);

  const handleSave = async (values: UnionFormValues) => {
    setSubmitting(true);
    try {
      await unionService.update(id, {
        id,
        code: values.code ?? '',
        name: values.name,
        description: values.description || null,
        contactPerson: values.contactPerson || null,
        contactEmail: values.contactEmail || null,
        contactPhone: values.contactPhone || null,
        isActive: values.isActive,
      });
      await refresh();
      toast({ title: 'Saved', description: 'The union was updated.' });
    } catch (error) {
      toast({
        title: 'Could not save the union',
        description: (error as Error)?.message || 'Failed to update the union.',
        variant: 'destructive',
      });
    } finally {
      setSubmitting(false);
    }
  };

  const handleDeleteAgreement = async () => {
    if (!deleteTarget) return false;
    setDeleting(true);
    try {
      await unionService.removeAgreement(deleteTarget.id);
      await refresh();
      toast({ title: 'Agreement removed', description: `“${deleteTarget.title}” was deleted.` });
      setDeleteTarget(null);
      return true;
    } catch (error) {
      toast({
        title: 'Could not remove the agreement',
        description: (error as Error)?.message || 'Failed to delete the agreement.',
        variant: 'destructive',
      });
      return false;
    } finally {
      setDeleting(false);
    }
  };

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-16">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (isError || !union) {
    return (
      <div className="p-6">
        <EmptyState title="Union not found" description="This union may have been deleted." />
      </div>
    );
  }

  const agreements = union.agreements ?? [];
  const contacts = union.contacts ?? [];
  const documents = union.documents ?? [];
  const primary = union.primaryContact ?? null;
  const logoEndpoint = unionService.logoEndpoint(id);

  return (
    <div className="space-y-6 p-6 max-w-5xl mx-auto">
      <PageHeader
        title={union.name}
        description={
          union.inForceAgreementCount > 0
            ? `${union.inForceAgreementCount} of ${union.agreementCount} agreement(s) in force today.`
            : union.agreementCount > 0
              ? `${union.agreementCount} agreement(s) on file, none in force today.`
              : 'No collective agreements recorded yet.'
        }
        backHref="/administration/hr/unions"
      />

      {/* The face of the union: logo, code, the primary contact. */}
      <Card>
        <CardContent className="flex flex-wrap items-center gap-4 p-4">
          <GatedPhoto
            endpoint={logoEndpoint}
            enabled={union.hasLogo}
            version={logoVersion}
            alt={`${union.name} logo`}
            className="h-16 w-16"
          />
          <div className="min-w-0 flex-1">
            <div className="flex flex-wrap items-center gap-2">
              <span className="font-medium">{union.name}</span>
              {union.code && <Badge variant="outline">{union.code}</Badge>}
              {!union.isActive && <Badge variant="secondary">Inactive</Badge>}
            </div>
            <p className="text-sm text-muted-foreground" data-testid="union-primary-contact">
              {primary
                ? `${primary.displayName} · ${primary.role}${primary.email ? ` · ${primary.email}` : ''}${!primary.email && primary.phone ? ` · ${primary.phone}` : ''}`
                : union.contactPerson || union.contactEmail || union.contactPhone
                  ? [union.contactPerson, union.contactEmail, union.contactPhone].filter(Boolean).join(' · ')
                  : 'No contact recorded yet.'}
            </p>
          </div>
          {canWrite && (
            <Button variant="outline" size="sm" onClick={() => setLogoOpen(true)}>
              <Camera className="mr-2 h-4 w-4" />
              {union.hasLogo ? 'Change logo' : 'Add logo'}
            </Button>
          )}
        </CardContent>
      </Card>

      <Tabs defaultValue="agreements">
        <TabsList>
          <TabsTrigger value="agreements">
            Agreements{agreements.length ? ` (${agreements.length})` : ''}
          </TabsTrigger>
          <TabsTrigger value="contacts">
            Contacts{contacts.length ? ` (${contacts.length})` : ''}
          </TabsTrigger>
          <TabsTrigger value="documents">
            Documents{documents.length ? ` (${documents.length})` : ''}
          </TabsTrigger>
          <TabsTrigger value="details">Details</TabsTrigger>
        </TabsList>

        <TabsContent value="agreements" className="mt-4">
          <Card>
            <CardHeader className="flex flex-row items-start justify-between space-y-0">
              <div>
                <CardTitle>Collective bargaining agreements</CardTitle>
                <CardDescription>
                  Newest first. An agreement with no expiry date is open-ended and never lapses on
                  its own. The signed copy is filed on the Documents tab against its agreement.
                </CardDescription>
              </div>
              {canWrite && (
                <Button
                  size="sm"
                  onClick={() => {
                    setEditing(null);
                    setDialogOpen(true);
                  }}
                >
                  <Plus className="mr-2 h-4 w-4" /> Add agreement
                </Button>
              )}
            </CardHeader>
            <CardContent>
              <div className="rounded-md border">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Agreement</TableHead>
                      <TableHead className="w-[200px]">Period</TableHead>
                      <TableHead className="w-[110px]">Status</TableHead>
                      <TableHead className="w-[110px]"></TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {agreements.length === 0 ? (
                      <TableRow>
                        <TableCell colSpan={4}>
                          <EmptyState
                            icon={FileText}
                            title="No agreements on file"
                            description="Record the collective agreement negotiated with this union so job descriptions and offer letters can refer to it."
                          />
                        </TableCell>
                      </TableRow>
                    ) : (
                      agreements.map((agreement) => (
                        <TableRow key={agreement.id}>
                          <TableCell>
                            <div className="flex flex-col">
                              <span className="font-medium">{agreement.title}</span>
                              {agreement.referenceNumber && (
                                <span className="text-xs text-muted-foreground">
                                  {agreement.referenceNumber}
                                </span>
                              )}
                              {agreement.summary && (
                                <span className="mt-1 text-xs text-muted-foreground">
                                  {agreement.summary}
                                </span>
                              )}
                              {agreement.documentCount > 0 && (
                                <span className="mt-1 flex items-center gap-1 text-xs text-muted-foreground">
                                  <Paperclip className="h-3 w-3" />
                                  {agreement.documentCount} file{agreement.documentCount === 1 ? '' : 's'} on the Documents tab
                                </span>
                              )}
                            </div>
                          </TableCell>
                          <TableCell className="text-sm text-muted-foreground">
                            {formatDate(agreement.effectiveDate)} —{' '}
                            {agreement.expiryDate ? formatDate(agreement.expiryDate) : 'open-ended'}
                          </TableCell>
                          <TableCell>
                            <Badge className={STATUS_STYLES[agreement.status] ?? STATUS_STYLES.Inactive}>
                              {agreement.status}
                            </Badge>
                          </TableCell>
                          <TableCell>
                            {canWrite && (
                              <div className="flex justify-end gap-1">
                                <Button
                                  variant="ghost"
                                  size="sm"
                                  onClick={() => {
                                    setEditing(agreement);
                                    setDialogOpen(true);
                                  }}
                                >
                                  <Pencil className="h-4 w-4" />
                                  <span className="sr-only">Edit</span>
                                </Button>
                                <Button
                                  variant="ghost"
                                  size="sm"
                                  className="text-destructive hover:text-destructive"
                                  onClick={() => setDeleteTarget(agreement)}
                                >
                                  <Trash2 className="h-4 w-4" />
                                  <span className="sr-only">Delete</span>
                                </Button>
                              </div>
                            )}
                          </TableCell>
                        </TableRow>
                      ))
                    )}
                  </TableBody>
                </Table>
              </div>
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="contacts" className="mt-4">
          <UnionContactsPanel
            unionId={id}
            unionName={union.name}
            canWrite={canWrite}
            onChanged={refresh}
          />
        </TabsContent>

        <TabsContent value="documents" className="mt-4">
          <UnionDocumentsPanel
            unionId={id}
            unionName={union.name}
            agreements={agreements}
            canWrite={canWrite}
            canDelete={canDeleteDocuments}
            onChanged={refresh}
          />
        </TabsContent>

        <TabsContent value="details" className="mt-4">
          <UnionForm
            defaultValues={{
              code: union.code ?? '',
              name: union.name,
              description: union.description ?? '',
              contactPerson: union.contactPerson ?? '',
              contactEmail: union.contactEmail ?? '',
              contactPhone: union.contactPhone ?? '',
              isActive: union.isActive,
            }}
            contactsMirrored={contacts.length > 0}
            onSubmit={handleSave}
            submitting={submitting}
            submitLabel="Save Changes"
            onCancel={() => router.push('/administration/hr/unions')}
          />
        </TabsContent>
      </Tabs>

      <AgreementDialog
        unionId={id}
        unionName={union.name}
        agreement={editing}
        open={dialogOpen}
        onOpenChange={setDialogOpen}
        onSaved={refresh}
      />

      <PhotoDialog
        open={logoOpen}
        onOpenChange={setLogoOpen}
        title="Union logo"
        description="Shown on the register and the union's record. Fetched through the gate, never a public link."
        endpoint={logoEndpoint}
        hasPhoto={union.hasLogo}
        upload={(file) => unionService.uploadLogo(id, file)}
        onUploaded={async () => {
          setLogoVersion((v) => v + 1);
          await refresh();
        }}
        subjectLabel={`${union.name} logo`}
        canWrite={canWrite}
      />

      <ConfirmationDialog
        open={deleteTarget !== null}
        onOpenChange={(open) => !open && setDeleteTarget(null)}
        title="Delete agreement"
        description={
          deleteTarget
            ? deleteTarget.documentCount > 0
              ? `“${deleteTarget.title}” has ${deleteTarget.documentCount} file(s) filed against it on the Documents tab. Remove those first.`
              : `Delete “${deleteTarget.title}”? The record of what was agreed will be gone.`
            : ''
        }
        confirmText="Delete"
        variant="destructive"
        isLoading={deleting}
        onConfirm={handleDeleteAgreement}
      />
    </div>
  );
}
