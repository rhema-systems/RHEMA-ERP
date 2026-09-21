'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { z } from 'zod';
import { GraduationCap, Loader2, MoreHorizontal, Plus } from 'lucide-react';
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
import { EmployeePickerField } from '@/components/hr/attendance/EmployeePickerField';
import {
  TextField,
  NumberField,
  TextareaField,
  SelectField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { safetyTrainingService } from '@/services/hr/safety-training.service';
import type { SheTrainingPlan } from '@/types/hr/safety-training';
import { OrganizationUnitPickerField } from '@/components/hr/common/OrganizationUnitPickerField';

/**
 * SHE training workspace: the annual/quarterly plans, the 30-day upcoming-program strip
 * and the expiring-certificates queue (renewal notices also fire automatically via the
 * reminder engine). This is the SHE record — separate from corporate Training (area 7).
 */
const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const blank = (v?: string) => (v && v.length > 0 ? v : null);

const planSchema = z.object({
  planNumber: z.string().min(1, 'A plan number is required').max(30),
  title: z.string().min(1, 'A title is required').max(200),
  year: z.coerce.number().int().min(2000).max(2100),
  quarter: z.coerce.number().int().min(1).max(4).optional().or(z.literal('')),
  organizationUnitId: z.string().optional().or(z.literal('')),
  preparedById: z.string().min(1, 'A preparer is required'),
  notes: z.string().max(500).optional().or(z.literal('')),
});
type PlanForm = z.input<typeof planSchema>;

export default function SafetyTrainingPage() {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [year, setYear] = useState(new Date().getFullYear());
  const [dialogOpen, setDialogOpen] = useState(false);
  const [pendingDelete, setPendingDelete] = useState<SheTrainingPlan | null>(null);
  const [busy, setBusy] = useState(false);

  const { data: plans = [], isLoading } = useQuery({
    queryKey: ['hr', 'safety-training', 'plans', year],
    queryFn: () => safetyTrainingService.getPlansByYear(year),
  });
  const { data: upcoming = [] } = useQuery({
    queryKey: ['hr', 'safety-training', 'upcoming'],
    queryFn: () => safetyTrainingService.getUpcomingPrograms(30),
  });
  const { data: expiring = [] } = useQuery({
    queryKey: ['hr', 'safety-training', 'expiring-certs'],
    queryFn: () => safetyTrainingService.getExpiringCertificates(30),
  });

  const form = useForm<PlanForm>({ resolver: zodResolver(planSchema) });

  const openCreate = () => {
    form.reset({
      planNumber: '',
      title: '',
      year,
      quarter: '',
      organizationUnitId: '',
      preparedById: '',
      notes: '',
    });
    setDialogOpen(true);
  };

  const submit = form.handleSubmit(async (values) => {
    setBusy(true);
    try {
      const v = planSchema.parse(values);
      await safetyTrainingService.createPlan({
        planNumber: v.planNumber,
        title: v.title,
        year: v.year,
        quarter: typeof v.quarter === 'number' ? v.quarter : null,
        organizationUnitId: blank(v.organizationUnitId),
        preparedById: v.preparedById,
        notes: blank(v.notes),
      });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'safety-training'] });
      toast({ title: 'Training plan created', description: 'It starts as Draft.' });
      setDialogOpen(false);
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Creating the plan failed.',
        variant: 'destructive',
      });
    } finally {
      setBusy(false);
    }
  });

  const yearOptions = Array.from({ length: 5 }, (_, i) => new Date().getFullYear() - 2 + i);

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Safety Training"
        description="SHE training plans, delivery programs and attendance — including contractor and visitor sign-ins. This is the SHE record, separate from corporate Training. Certificate renewals are chased automatically — expiring certificates raise reminders on a 90/60/30/14/7 ladder."
        backHref="/hr/safety"
        actions={
          <Button onClick={openCreate}>
            <Plus className="mr-2 h-4 w-4" /> New plan
          </Button>
        }
      />

      <Tabs defaultValue="plans">
        <TabsList>
          <TabsTrigger value="plans">Plans ({plans.length})</TabsTrigger>
          <TabsTrigger value="upcoming">Upcoming programs ({upcoming.length})</TabsTrigger>
          <TabsTrigger value="expiring">Expiring certificates ({expiring.length})</TabsTrigger>
        </TabsList>

        <TabsContent value="plans" className="mt-4 space-y-3">
          <div className="flex items-center gap-2">
            <span className="text-muted-foreground text-sm">Year:</span>
            <select
              className="border-input bg-background rounded-md border px-2 py-1 text-sm"
              value={year}
              onChange={(e) => setYear(Number(e.target.value))}
            >
              {yearOptions.map((y) => (
                <option key={y} value={y}>
                  {y}
                </option>
              ))}
            </select>
          </div>
          {isLoading ? null : plans.length === 0 ? (
            <EmptyState
              title={`No training plans for ${year}`}
              description="Create the annual or quarterly plan, then schedule its programs."
              icon={GraduationCap}
            />
          ) : (
            <Card>
              <CardContent className="p-0">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Plan</TableHead>
                      <TableHead>Title</TableHead>
                      <TableHead>Period</TableHead>
                      <TableHead>Org unit</TableHead>
                      <TableHead>Prepared by</TableHead>
                      <TableHead>Programs</TableHead>
                      <TableHead>Status</TableHead>
                      <TableHead className="w-[60px]" />
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {plans.map((p) => (
                      <TableRow key={p.id}>
                        <TableCell>
                          <Link
                            href={`/hr/safety/training/plans/${p.id}`}
                            className="font-mono text-primary hover:underline"
                          >
                            {p.planNumber}
                          </Link>
                        </TableCell>
                        <TableCell className="max-w-[280px] truncate font-medium" title={p.title}>
                          {p.title}
                        </TableCell>
                        <TableCell>
                          {p.year}
                          {p.quarter ? ` Q${p.quarter}` : ''}
                        </TableCell>
                        <TableCell>{p.organizationUnitName ?? 'Company-wide'}</TableCell>
                        <TableCell>{p.preparedByName}</TableCell>
                        <TableCell>{p.programs.length}</TableCell>
                        <TableCell>
                          <StatusBadge status={p.statusName} />
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
                              <DropdownMenuItem asChild>
                                <Link href={`/hr/safety/training/plans/${p.id}`}>Open</Link>
                              </DropdownMenuItem>
                              <DropdownMenuItem
                                className="text-red-600"
                                onClick={() => setPendingDelete(p)}
                              >
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
          )}
        </TabsContent>

        <TabsContent value="upcoming" className="mt-4">
          {upcoming.length === 0 ? (
            <EmptyState
              title="Nothing scheduled in the next 30 days"
              description="Planned and scheduled programs with a date in the next 30 days appear here."
              icon={GraduationCap}
            />
          ) : (
            <Card>
              <CardContent className="p-0">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Code</TableHead>
                      <TableHead>Program</TableHead>
                      <TableHead>Category</TableHead>
                      <TableHead>Delivery</TableHead>
                      <TableHead>Scheduled</TableHead>
                      <TableHead>Status</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {upcoming.map((p) => (
                      <TableRow key={p.id}>
                        <TableCell>
                          <Link
                            href={`/hr/safety/training/programs/${p.id}`}
                            className="font-mono text-primary hover:underline"
                          >
                            {p.programCode}
                          </Link>
                        </TableCell>
                        <TableCell className="font-medium">{p.title}</TableCell>
                        <TableCell>{p.categoryName}</TableCell>
                        <TableCell>{p.deliveryMethodName}</TableCell>
                        <TableCell>{fmtDate(p.scheduledDate)}</TableCell>
                        <TableCell>
                          <StatusBadge status={p.statusName} />
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </CardContent>
            </Card>
          )}
        </TabsContent>

        <TabsContent value="expiring" className="mt-4 space-y-3">
          <p className="text-muted-foreground text-sm">
            Certificates expiring within 30 days (or already lapsed) — schedule refreshers and
            update the attendance record.
          </p>
          {expiring.length === 0 ? (
            <EmptyState
              title="No certificates expiring"
              description="Attendance rows with a certificate expiry inside 30 days appear here."
              icon={GraduationCap}
            />
          ) : (
            <Card>
              <CardContent className="p-0">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Holder</TableHead>
                      <TableHead>Program</TableHead>
                      <TableHead>Category</TableHead>
                      <TableHead>Expires</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {expiring.map((a) => {
                      const lapsed =
                        !!a.certificateExpiryDate &&
                        new Date(a.certificateExpiryDate).getTime() < Date.now();
                      return (
                        <TableRow key={a.id}>
                          <TableCell className="font-medium">
                            {a.attendanceName}
                            {!a.isEmployee && (
                              <span className="text-muted-foreground text-xs">
                                {' '}
                                ({a.companyName ?? 'visitor'})
                              </span>
                            )}
                          </TableCell>
                          <TableCell>
                            <Link
                              href={`/hr/safety/training/programs/${a.programId}`}
                              className="text-primary hover:underline"
                            >
                              {a.programCode ? `${a.programCode} — ${a.programTitle}` : a.programTitle}
                            </Link>
                          </TableCell>
                          <TableCell>{a.programCategoryName ?? '—'}</TableCell>
                          <TableCell>
                            <span className={lapsed ? 'text-destructive font-medium' : ''}>
                              {fmtDate(a.certificateExpiryDate)}
                              {lapsed && ' (lapsed)'}
                            </span>
                          </TableCell>
                        </TableRow>
                      );
                    })}
                  </TableBody>
                </Table>
              </CardContent>
            </Card>
          )}
        </TabsContent>
      </Tabs>

      <Dialog open={dialogOpen} onOpenChange={(o) => !busy && setDialogOpen(o)}>
        <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-[560px]">
          <DialogHeader>
            <DialogTitle>New training plan</DialogTitle>
            <DialogDescription>
              The plan number is unique per tenant and fixed after creation. Plans start as Draft;
              approval happens on the plan page and requires naming the approver.
            </DialogDescription>
          </DialogHeader>
          <form onSubmit={submit} className="space-y-4">
            <FieldRow>
              <TextField
                form={form}
                name="planNumber"
                label="Plan number (unique)"
                required
                placeholder="e.g. STP-2026-Q3"
              />
              <TextField form={form} name="title" label="Title" required />
            </FieldRow>
            <FieldRow>
              <NumberField form={form} name="year" label="Year" required />
              <NumberField form={form} name="quarter" label="Quarter (1–4, optional)" />
            </FieldRow>
            <OrganizationUnitPickerField form={form} name="organizationUnitId" label="Organization unit" allowEmpty emptyLabel="Company-wide" />
            <EmployeePickerField form={form} name="preparedById" label="Prepared by" required />
            <TextareaField form={form} name="notes" label="Notes" rows={2} />
            <DialogFooter>
              <Button type="button" variant="outline" disabled={busy} onClick={() => setDialogOpen(false)}>
                Cancel
              </Button>
              <Button type="submit" disabled={busy}>
                {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                Create plan
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      <ConfirmationDialog
        open={pendingDelete !== null}
        onOpenChange={(open) => !open && setPendingDelete(null)}
        title={`Remove ${pendingDelete?.planNumber}?`}
        description="This removes the plan from the register. Its programs remain reachable from the program views."
        confirmText="Remove"
        variant="destructive"
        onConfirm={async () => {
          if (!pendingDelete) return;
          try {
            await safetyTrainingService.removePlan(pendingDelete.id);
            await queryClient.invalidateQueries({ queryKey: ['hr', 'safety-training'] });
            toast({ title: 'Removed', description: pendingDelete.planNumber });
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
