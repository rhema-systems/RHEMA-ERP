'use client';

import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { z } from 'zod';
import { Loader2, MoreHorizontal, Plus, Signpost } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
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
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
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
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import {
  TextField,
  TextareaField,
  SelectField,
  DateField,
  SwitchField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { safetySignageService } from '@/services/hr/safety-signage.service';
import { locationService } from '@/services/hr/location.service';
import {
  SHE_SIGN_TYPE_OPTIONS,
  SHE_SIGN_STATUS_OPTIONS,
} from '@/types/hr/safety-governance';
import type {
  SafetySign,
  SheSafetySignType,
  SheSafetySignStatus,
} from '@/types/hr/safety-governance';

/**
 * Safety signage register (SoW 15.0): every sign with its location, condition and
 * inspection dates. Inspection due dates are hand-kept — the due-for-inspection view
 * is the manual queue until the reminder engine lands.
 */
const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const blank = (v?: string) => (v && v.length > 0 ? v : null);

const signSchema = z.object({
  signCode: z.string().min(1, 'A code is required').max(30),
  description: z.string().min(1, 'A description is required').max(200),
  signType: z.string().min(1),
  locationId: z.string().min(1, 'A location is required'),
  specificPosition: z.string().max(300).optional().or(z.literal('')),
  installationDate: z.string().min(1, 'An installation date is required'),
  manufacturer: z.string().max(100).optional().or(z.literal('')),
  material: z.string().max(100).optional().or(z.literal('')),
  isPhotoluminescent: z.boolean(),
  status: z.string().min(1),
  lastInspectionDate: z.string().optional().or(z.literal('')),
  nextInspectionDate: z.string().optional().or(z.literal('')),
  inspectionNotes: z.string().max(500).optional().or(z.literal('')),
  isActive: z.boolean(),
});
type SignForm = z.input<typeof signSchema>;

const conditionVariant = (s: SheSafetySignStatus) =>
  s === 'Damaged' || s === 'Missing' ? 'destructive' : s === 'Good' ? 'default' : 'secondary';

function SignsTable({
  rows,
  onEdit,
  onDelete,
  highlightDue,
}: {
  rows: SafetySign[];
  onEdit: (s: SafetySign) => void;
  onDelete: (s: SafetySign) => void;
  highlightDue?: boolean;
}) {
  if (rows.length === 0) {
    return (
      <EmptyState
        title="No signs here"
        description="Signs matching this view will appear here."
        icon={Signpost}
      />
    );
  }
  return (
    <Card>
      <CardContent className="p-0">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Code</TableHead>
              <TableHead>Sign</TableHead>
              <TableHead>Type</TableHead>
              <TableHead>Location</TableHead>
              <TableHead>Condition</TableHead>
              <TableHead>Next inspection</TableHead>
              <TableHead>Status</TableHead>
              <TableHead className="w-[60px]" />
            </TableRow>
          </TableHeader>
          <TableBody>
            {rows.map((s) => (
              <TableRow key={s.id}>
                <TableCell className="font-mono">{s.signCode}</TableCell>
                <TableCell className="max-w-[260px]">
                  <div className="truncate font-medium" title={s.description}>
                    {s.description}
                  </div>
                  {s.specificPosition && (
                    <div className="text-muted-foreground truncate text-xs">{s.specificPosition}</div>
                  )}
                </TableCell>
                <TableCell>{s.signTypeName}</TableCell>
                <TableCell>{s.locationName}</TableCell>
                <TableCell>
                  <Badge variant={conditionVariant(s.status)}>{s.statusName}</Badge>
                </TableCell>
                <TableCell className={highlightDue ? 'text-destructive font-medium' : undefined}>
                  {fmtDate(s.nextInspectionDate)}
                </TableCell>
                <TableCell>
                  <StatusBadge status={s.isActive ? 'Active' : 'Inactive'} />
                </TableCell>
                <TableCell>
                  <DropdownMenu>
                    <DropdownMenuTrigger asChild>
                      <Button variant="ghost" size="icon" className="h-8 w-8">
                        <MoreHorizontal className="h-4 w-4" />
                        <span className="sr-only">Actions</span>
                      </Button>
                    </DropdownMenuTrigger>
                    <DropdownMenuContent align="end">
                      <DropdownMenuItem onClick={() => onEdit(s)}>Edit…</DropdownMenuItem>
                      <DropdownMenuItem className="text-red-600" onClick={() => onDelete(s)}>
                        Remove
                      </DropdownMenuItem>
                    </DropdownMenuContent>
                  </DropdownMenu>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </CardContent>
    </Card>
  );
}

export default function SafetySignsPage() {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [pendingDelete, setPendingDelete] = useState<SafetySign | null>(null);
  const [busy, setBusy] = useState(false);

  const { data: signs = [], isLoading } = useQuery({
    queryKey: ['hr', 'safety-signs', 'all'],
    queryFn: () => safetySignageService.getAll(),
  });
  const { data: dueForInspection = [] } = useQuery({
    queryKey: ['hr', 'safety-signs', 'due'],
    queryFn: () => safetySignageService.getDueForInspection(30),
  });
  const { data: locations = [] } = useQuery({
    queryKey: ['hr', 'locations'],
    queryFn: () => locationService.getAll(),
  });

  const attention = signs.filter(
    (s) => s.isActive && (s.status === 'Damaged' || s.status === 'Missing' || s.status === 'Faded'),
  );

  const form = useForm<SignForm>({ resolver: zodResolver(signSchema) });

  const openCreate = () => {
    setEditingId(null);
    form.reset({
      signCode: '',
      description: '',
      signType: 'Warning',
      locationId: '',
      specificPosition: '',
      installationDate: new Date().toISOString().slice(0, 10),
      manufacturer: '',
      material: '',
      isPhotoluminescent: false,
      status: 'Good',
      lastInspectionDate: '',
      nextInspectionDate: '',
      inspectionNotes: '',
      isActive: true,
    });
    setDialogOpen(true);
  };

  const openEdit = (s: SafetySign) => {
    setEditingId(s.id);
    form.reset({
      signCode: s.signCode,
      description: s.description,
      signType: s.signType,
      locationId: s.locationId,
      specificPosition: s.specificPosition ?? '',
      installationDate: s.installationDate.slice(0, 10),
      manufacturer: s.manufacturer ?? '',
      material: s.material ?? '',
      isPhotoluminescent: s.isPhotoluminescent,
      status: s.status,
      lastInspectionDate: s.lastInspectionDate ? s.lastInspectionDate.slice(0, 10) : '',
      nextInspectionDate: s.nextInspectionDate ? s.nextInspectionDate.slice(0, 10) : '',
      inspectionNotes: s.inspectionNotes ?? '',
      isActive: s.isActive,
    });
    setDialogOpen(true);
  };

  const submit = form.handleSubmit(async (values) => {
    setBusy(true);
    try {
      const v = signSchema.parse(values);
      const common = {
        description: v.description,
        signType: v.signType as SheSafetySignType,
        locationId: v.locationId,
        specificPosition: blank(v.specificPosition),
        installationDate: new Date(v.installationDate).toISOString(),
        manufacturer: blank(v.manufacturer),
        material: blank(v.material),
        isPhotoluminescent: v.isPhotoluminescent,
        status: v.status as SheSafetySignStatus,
        isActive: v.isActive,
      };
      if (editingId) {
        await safetySignageService.update(editingId, {
          id: editingId,
          ...common,
          lastInspectionDate: v.lastInspectionDate
            ? new Date(v.lastInspectionDate).toISOString()
            : null,
          nextInspectionDate: v.nextInspectionDate
            ? new Date(v.nextInspectionDate).toISOString()
            : null,
          inspectionNotes: blank(v.inspectionNotes),
        });
      } else {
        await safetySignageService.create({ signCode: v.signCode, ...common });
      }
      await queryClient.invalidateQueries({ queryKey: ['hr', 'safety-signs'] });
      toast({ title: editingId ? 'Sign updated' : 'Sign registered' });
      setDialogOpen(false);
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Saving the sign failed.',
        variant: 'destructive',
      });
    } finally {
      setBusy(false);
    }
  });

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Safety Signage"
        description="The signage register — location, condition and inspection cycle per sign. Inspection chasing is manual; no reminder job exists yet."
        backHref="/hr/safety"
        actions={
          <Button onClick={openCreate}>
            <Plus className="mr-2 h-4 w-4" /> Register sign
          </Button>
        }
      />

      <Tabs defaultValue="all">
        <TabsList>
          <TabsTrigger value="all">All signs ({signs.length})</TabsTrigger>
          <TabsTrigger value="due">Due for inspection ({dueForInspection.length})</TabsTrigger>
          <TabsTrigger value="attention">Needs attention ({attention.length})</TabsTrigger>
        </TabsList>

        <TabsContent value="all" className="mt-4">
          {isLoading ? null : (
            <SignsTable rows={signs} onEdit={openEdit} onDelete={setPendingDelete} />
          )}
        </TabsContent>

        <TabsContent value="due" className="mt-4 space-y-3">
          <p className="text-muted-foreground text-sm">
            Inspections falling due in the next 30 days (or already past).
          </p>
          <SignsTable rows={dueForInspection} onEdit={openEdit} onDelete={setPendingDelete} highlightDue />
        </TabsContent>

        <TabsContent value="attention" className="mt-4 space-y-3">
          <p className="text-muted-foreground text-sm">
            Active signs recorded as faded, damaged or missing — replace and update the register.
          </p>
          <SignsTable rows={attention} onEdit={openEdit} onDelete={setPendingDelete} />
        </TabsContent>
      </Tabs>

      <Dialog open={dialogOpen} onOpenChange={(o) => !busy && setDialogOpen(o)}>
        <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-[640px]">
          <DialogHeader>
            <DialogTitle>{editingId ? 'Edit sign' : 'Register sign'}</DialogTitle>
            <DialogDescription>
              {editingId
                ? 'The sign code is fixed at creation. Record inspections here — set the next due date.'
                : 'The code is unique per tenant and fixed after creation.'}
            </DialogDescription>
          </DialogHeader>
          <form onSubmit={submit} className="space-y-4">
            <FieldRow>
              {!editingId ? (
                <TextField
                  form={form}
                  name="signCode"
                  label="Sign code (unique)"
                  required
                  placeholder="e.g. SGN-014"
                />
              ) : (
                <div className="text-muted-foreground self-end pb-2 text-sm">
                  <span className="font-mono">{form.getValues('signCode')}</span> — fixed at creation.
                </div>
              )}
              <SelectField
                form={form}
                name="signType"
                label="Type"
                required
                options={SHE_SIGN_TYPE_OPTIONS}
              />
            </FieldRow>
            <TextField form={form} name="description" label="Description" required />
            <FieldRow>
              <SelectField
                form={form}
                name="locationId"
                label="Location"
                required
                options={locations.map((l) => ({ value: l.id, label: l.name }))}
              />
              <TextField form={form} name="specificPosition" label="Specific position" />
            </FieldRow>
            <FieldRow>
              <DateField form={form} name="installationDate" label="Installed" required />
              <SelectField
                form={form}
                name="status"
                label="Condition"
                required
                options={SHE_SIGN_STATUS_OPTIONS}
              />
            </FieldRow>
            <FieldRow>
              <TextField form={form} name="manufacturer" label="Manufacturer" />
              <TextField form={form} name="material" label="Material" />
            </FieldRow>
            <SwitchField
              form={form}
              name="isPhotoluminescent"
              label="Photoluminescent"
              description="Glows in the dark — required for escape-route signage in unlit areas."
            />
            {editingId && (
              <>
                <FieldRow>
                  <DateField form={form} name="lastInspectionDate" label="Last inspection" />
                  <DateField form={form} name="nextInspectionDate" label="Next inspection due" />
                </FieldRow>
                <TextareaField form={form} name="inspectionNotes" label="Inspection notes" rows={2} />
              </>
            )}
            <SwitchField form={form} name="isActive" label="Active" />
            <DialogFooter>
              <Button type="button" variant="outline" disabled={busy} onClick={() => setDialogOpen(false)}>
                Cancel
              </Button>
              <Button type="submit" disabled={busy}>
                {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                {editingId ? 'Save' : 'Register sign'}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      <ConfirmationDialog
        open={pendingDelete !== null}
        onOpenChange={(open) => !open && setPendingDelete(null)}
        title={`Remove ${pendingDelete?.signCode}?`}
        description="This removes the sign from the register. A decommissioned sign can instead keep its history with condition 'Decommissioned' and Active off."
        confirmText="Remove"
        variant="destructive"
        onConfirm={async () => {
          if (!pendingDelete) return;
          try {
            await safetySignageService.remove(pendingDelete.id);
            await queryClient.invalidateQueries({ queryKey: ['hr', 'safety-signs'] });
            toast({ title: 'Removed', description: pendingDelete.signCode });
          } catch (error: any) {
            toast({
              title: 'Error',
              description: error?.message || 'Removing failed.',
              variant: 'destructive',
            });
          } finally {
            setPendingDelete(null);
          }
        }}
      />
    </div>
  );
}
