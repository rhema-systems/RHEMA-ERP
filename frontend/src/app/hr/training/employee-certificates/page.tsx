'use client';

import { useMemo, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import {
  Plus,
  Search,
  MoreHorizontal,
  Trash2,
  BadgeCheck,
  Pencil,
  IdCard,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Skeleton } from '@/components/ui/skeleton';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { TextField, TextareaField, DateField, FieldRow } from '@/components/hr/employee/tabs/fields';
import { EmployeePickerField } from '@/components/hr/attendance/EmployeePickerField';
import { employeeCertificateService } from '@/services/hr/training-certificate.service';
import { CERTIFICATE_STATUS_OPTIONS } from '@/types/hr/training-certificates';
import type { EmployeeCertificate } from '@/types/hr/training-certificates';

const statusLabel = (v: string) =>
  CERTIFICATE_STATUS_OPTIONS.find((o) => o.value === v)?.label ?? v;
const fmt = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const today = () => new Date().toISOString().slice(0, 10);

const schema = z
  .object({
    employeeId: z.string().min(1, 'Employee is required'),
    certificateName: z.string().min(1, 'Required').max(200),
    issuingBody: z.string().min(1, 'Required').max(200),
    certificateNumber: z.string().max(100).optional().or(z.literal('')),
    issuedDate: z.string().min(1, 'Required'),
    expiryDate: z.string().optional().or(z.literal('')),
    description: z.string().max(1000).optional().or(z.literal('')),
  })
  .refine((v) => !v.expiryDate || new Date(v.expiryDate) >= new Date(v.issuedDate), {
    message: 'A certificate cannot expire before it was issued.',
    path: ['expiryDate'],
  });
type FormValues = z.infer<typeof schema>;

const emptyForm: FormValues = {
  employeeId: '',
  certificateName: '',
  issuingBody: '',
  certificateNumber: '',
  issuedDate: today(),
  expiryDate: '',
  description: '',
};

/**
 * Certificates employees already hold from outside bodies — a PMP, a trade licence, a safety card.
 *
 * We did not issue these, so there is no verification code and nothing to revoke: HR either confirms
 * they have seen the evidence (verify) or corrects the record. That is why the actions here differ
 * from the issued-certificate register.
 */
export default function EmployeeCertificatesPage() {
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [view, setView] = useState<'unverified' | 'expiring' | 'employee' | 'mine'>('unverified');
  const [search, setSearch] = useState('');
  const [formOpen, setFormOpen] = useState(false);
  const [editing, setEditing] = useState<EmployeeCertificate | null>(null);
  const [verifyTarget, setVerifyTarget] = useState<{ id: string; name: string } | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<{ id: string; name: string } | null>(null);
  const [busy, setBusy] = useState(false);

  const pickerForm = useForm<{ employeeId: string }>({ defaultValues: { employeeId: '' } });
  const selectedEmployee = pickerForm.watch('employeeId');

  const form = useForm<FormValues>({ resolver: zodResolver(schema) as any, defaultValues: emptyForm });

  const queryKey = ['hr', 'training', 'employee-certificates', view, selectedEmployee];
  const { data, isLoading } = useQuery({
    queryKey,
    queryFn: () => {
      if (view === 'expiring') return employeeCertificateService.getExpiring(90);
      if (view === 'mine') return employeeCertificateService.getMine();
      if (view === 'employee' && selectedEmployee) {
        return employeeCertificateService.getForEmployee(selectedEmployee);
      }
      return employeeCertificateService.getUnverified();
    },
  });

  const invalidate = () =>
    queryClient.invalidateQueries({ queryKey: ['hr', 'training', 'employee-certificates'] });

  const rows = useMemo(() => {
    const term = search.trim().toLowerCase();
    const all = (data ?? []) as any[];
    if (!term) return all;
    return all.filter(
      (c) =>
        (c.certificateName ?? '').toLowerCase().includes(term) ||
        (c.issuingBody ?? '').toLowerCase().includes(term) ||
        (c.employeeName ?? '').toLowerCase().includes(term),
    );
  }, [data, search]);

  const openCreate = () => {
    setEditing(null);
    form.reset(emptyForm);
    setFormOpen(true);
  };

  const openEdit = async (id: string) => {
    try {
      const full = await employeeCertificateService.getById(id);
      setEditing(full);
      form.reset({
        employeeId: full.employeeId,
        certificateName: full.certificateName,
        issuingBody: full.issuingBody,
        certificateNumber: full.certificateNumber ?? '',
        issuedDate: full.issuedDate.slice(0, 10),
        expiryDate: full.expiryDate?.slice(0, 10) ?? '',
        description: full.description ?? '',
      });
      setFormOpen(true);
    } catch (error: any) {
      toast({ title: 'Error', description: error?.message || 'Could not load it.', variant: 'destructive' });
    }
  };

  const submit = form.handleSubmit(async (values) => {
    setBusy(true);
    try {
      const payload = {
        certificateName: values.certificateName,
        issuingBody: values.issuingBody,
        certificateNumber: values.certificateNumber || null,
        issuedDate: new Date(values.issuedDate).toISOString(),
        expiryDate: values.expiryDate ? new Date(values.expiryDate).toISOString() : null,
        description: values.description || null,
      };
      if (editing) {
        await employeeCertificateService.update(editing.id, payload);
        toast({ title: 'Saved' });
      } else {
        await employeeCertificateService.create({ ...payload, employeeId: values.employeeId });
        toast({ title: 'Recorded', description: 'It starts unverified until HR confirms the evidence.' });
      }
      await invalidate();
      setFormOpen(false);
    } catch (error: any) {
      toast({ title: 'Error', description: error?.message || 'Failed to save.', variant: 'destructive' });
    } finally {
      setBusy(false);
    }
  });

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Employee Certificates"
        description="Qualifications staff hold from outside bodies. Recorded here, then verified by HR against the evidence."
        backHref="/hr/training"
        actions={
          <Button onClick={openCreate}>
            <Plus className="mr-2 h-4 w-4" /> Record a certificate
          </Button>
        }
      />

      <Card>
        <CardHeader>
          <div className="flex flex-wrap items-center justify-between gap-3">
            <div>
              <CardTitle>Certificates</CardTitle>
              <CardDescription>
                Unverified is the queue that matters — an unverified certificate is a claim, not a record.
              </CardDescription>
            </div>
            <div className="flex items-center gap-2">
              <Tabs value={view} onValueChange={(v) => setView(v as typeof view)}>
                <TabsList>
                  <TabsTrigger value="unverified">Unverified</TabsTrigger>
                  <TabsTrigger value="expiring">Expiring</TabsTrigger>
                  <TabsTrigger value="employee">By employee</TabsTrigger>
                  <TabsTrigger value="mine">Mine</TabsTrigger>
                </TabsList>
              </Tabs>
              <div className="relative w-56">
                <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
                <Input
                  placeholder="Search…"
                  className="pl-8"
                  value={search}
                  onChange={(e) => setSearch(e.target.value)}
                />
              </div>
            </div>
          </div>
        </CardHeader>
        <CardContent className="space-y-4">
          {view === 'employee' && (
            <div className="max-w-sm">
              <EmployeePickerField form={pickerForm} name="employeeId" label="Employee" />
            </div>
          )}

          <div className="rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Certificate</TableHead>
                  <TableHead>Holder</TableHead>
                  <TableHead>Issuing body</TableHead>
                  <TableHead>Issued</TableHead>
                  <TableHead>Expires</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead className="w-[70px]"></TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {isLoading ? (
                  [...Array(4)].map((_, i) => (
                    <TableRow key={i}>
                      {[...Array(7)].map((__, j) => (
                        <TableCell key={j}>
                          <Skeleton className="h-4 w-[90px]" />
                        </TableCell>
                      ))}
                    </TableRow>
                  ))
                ) : view === 'employee' && !selectedEmployee ? (
                  <TableRow>
                    <TableCell colSpan={7}>
                      <EmptyState
                        icon={IdCard}
                        title="Pick an employee"
                        description="Choose someone above to see what they hold."
                      />
                    </TableCell>
                  </TableRow>
                ) : rows.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={7}>
                      <EmptyState
                        icon={IdCard}
                        title={view === 'unverified' ? 'Nothing awaiting verification' : 'No certificates'}
                        description={
                          view === 'unverified'
                            ? 'Newly recorded certificates land here until HR confirms them.'
                            : 'Record a qualification an employee holds from an outside body.'
                        }
                      />
                    </TableCell>
                  </TableRow>
                ) : (
                  rows.map((c) => (
                    <TableRow key={c.id}>
                      <TableCell>
                        <div className="font-medium">{c.certificateName}</div>
                        {c.certificateNumber && (
                          <div className="font-mono text-[11px] text-muted-foreground">
                            {c.certificateNumber}
                          </div>
                        )}
                      </TableCell>
                      <TableCell className="text-muted-foreground">{c.employeeName || '—'}</TableCell>
                      <TableCell className="text-muted-foreground">{c.issuingBody}</TableCell>
                      <TableCell className="text-muted-foreground">{fmt(c.issuedDate)}</TableCell>
                      <TableCell>
                        {c.expiryDate ? (
                          <span className={c.isExpired ? 'text-destructive' : undefined}>
                            {fmt(c.expiryDate)}
                          </span>
                        ) : (
                          <span className="text-muted-foreground">No expiry</span>
                        )}
                      </TableCell>
                      <TableCell>
                        <div className="flex items-center gap-1.5">
                          <StatusBadge status={statusLabel(c.status)} />
                          {c.isVerified ? (
                            <Badge variant="secondary" className="text-[10px]">
                              Verified
                            </Badge>
                          ) : (
                            <span className="text-[11px] text-amber-600">Unverified</span>
                          )}
                        </div>
                      </TableCell>
                      <TableCell>
                        <DropdownMenu>
                          <DropdownMenuTrigger asChild>
                            <Button variant="ghost" className="h-8 w-8 p-0">
                              <span className="sr-only">Open menu</span>
                              <MoreHorizontal className="h-4 w-4" />
                            </Button>
                          </DropdownMenuTrigger>
                          <DropdownMenuContent align="end">
                            <DropdownMenuLabel>Actions</DropdownMenuLabel>
                            <DropdownMenuItem onClick={() => openEdit(c.id)}>
                              <Pencil className="mr-2 h-4 w-4" /> Edit
                            </DropdownMenuItem>
                            {!c.isVerified && (
                              <DropdownMenuItem
                                onClick={() =>
                                  setVerifyTarget({ id: c.id, name: c.certificateName })
                                }
                              >
                                <BadgeCheck className="mr-2 h-4 w-4" /> Verify
                              </DropdownMenuItem>
                            )}
                            <DropdownMenuSeparator />
                            <DropdownMenuItem
                              className="text-destructive focus:text-destructive"
                              onClick={() => setDeleteTarget({ id: c.id, name: c.certificateName })}
                            >
                              <Trash2 className="mr-2 h-4 w-4" /> Delete
                            </DropdownMenuItem>
                          </DropdownMenuContent>
                        </DropdownMenu>
                      </TableCell>
                    </TableRow>
                  ))
                )}
              </TableBody>
            </Table>
          </div>
        </CardContent>
      </Card>

      <Dialog open={formOpen} onOpenChange={setFormOpen}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>{editing ? 'Edit certificate' : 'Record a certificate'}</DialogTitle>
            <DialogDescription>
              {editing
                ? 'Correct the record. Verification is a separate step.'
                : 'It is recorded as unverified until someone confirms the evidence.'}
            </DialogDescription>
          </DialogHeader>
          <div className="max-h-[60vh] space-y-4 overflow-y-auto py-2">
            {/* The holder cannot move: the update DTO has no employeeId, so it is create-only. */}
            {editing ? (
              <div className="rounded-md border bg-muted/50 px-3 py-2 text-sm">
                Holder: <span className="font-medium">{editing.employeeName}</span>
                <p className="mt-1 text-xs text-muted-foreground">
                  The holder cannot be changed — delete and re-record it against the right person.
                </p>
              </div>
            ) : (
              <EmployeePickerField form={form} name="employeeId" label="Employee" required />
            )}
            <TextField
              form={form}
              name="certificateName"
              label="Certificate"
              placeholder="Project Management Professional"
              required
            />
            <TextField form={form} name="issuingBody" label="Issuing body" placeholder="PMI" required />
            <TextField form={form} name="certificateNumber" label="Certificate number" />
            <FieldRow>
              <DateField form={form} name="issuedDate" label="Issued" required />
              <DateField form={form} name="expiryDate" label="Expires" />
            </FieldRow>
            <TextareaField form={form} name="description" label="Notes" rows={2} />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setFormOpen(false)} disabled={busy}>
              Cancel
            </Button>
            <Button onClick={submit} disabled={busy}>
              {editing ? 'Save changes' : 'Record'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <ConfirmationDialog
        open={verifyTarget !== null}
        onOpenChange={(o) => !o && setVerifyTarget(null)}
        title="Verify this certificate?"
        description={
          verifyTarget
            ? `You are confirming you have seen evidence for "${verifyTarget.name}". You will be recorded as the verifier, and it cannot be un-verified.`
            : ''
        }
        confirmText="Verify"
        isLoading={busy}
        onConfirm={async () => {
          if (!verifyTarget) return false;
          setBusy(true);
          try {
            await employeeCertificateService.verify(verifyTarget.id);
            await invalidate();
            toast({ title: 'Verified' });
            setVerifyTarget(null);
            return true;
          } catch (error: any) {
            toast({
              title: 'Error',
              description: error?.message || 'Failed to verify.',
              variant: 'destructive',
            });
            return false;
          } finally {
            setBusy(false);
          }
        }}
      />

      <ConfirmationDialog
        open={deleteTarget !== null}
        onOpenChange={(o) => !o && setDeleteTarget(null)}
        title="Delete certificate"
        description={deleteTarget ? `Delete the record of "${deleteTarget.name}"?` : ''}
        confirmText="Delete"
        variant="destructive"
        isLoading={busy}
        onConfirm={async () => {
          if (!deleteTarget) return false;
          setBusy(true);
          try {
            await employeeCertificateService.remove(deleteTarget.id);
            await invalidate();
            toast({ title: 'Deleted' });
            setDeleteTarget(null);
            return true;
          } catch (error: any) {
            toast({
              title: 'Error',
              description: error?.message || 'Failed to delete.',
              variant: 'destructive',
            });
            return false;
          } finally {
            setBusy(false);
          }
        }}
      />
    </div>
  );
}
