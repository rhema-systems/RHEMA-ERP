'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { CalendarRange, Check, Loader2, MoreHorizontal, Plus, Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
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
import { Badge } from '@/components/ui/badge';
import { Skeleton } from '@/components/ui/skeleton';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { DateField, FieldRow, NumberField, SwitchField, TextField } from '@/components/hr/employee/tabs/fields';
import { fiscalYearService } from '@/services/hr/company-schedule.service';
import type { FiscalYear } from '@/types/hr/company-schedule';

const schema = z
  .object({
    year: z.coerce.number().int().min(2000).max(2100),
    fiscalYearName: z.string().min(1, 'Name is required').max(200),
    startDate: z.string().min(1, 'Start date is required'),
    endDate: z.string().min(1, 'End date is required'),
    isCurrent: z.boolean(),
  })
  .refine((v) => v.endDate > v.startDate, {
    message: 'The end must be after the start',
    path: ['endDate'],
  });

type FormValues = z.infer<typeof schema>;

/**
 * Fiscal years. Creating one only defines the window — the periods inside it are added on the
 * year's own page, because a year with no periods is still a valid thing to have created.
 */
export default function FiscalYearsPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [open, setOpen] = useState(false);
  const [saving, setSaving] = useState(false);
  const [deleteTarget, setDeleteTarget] = useState<FiscalYear | null>(null);

  const key = ['hr', 'company-schedule', 'fiscal-years'];
  const { data, isLoading } = useQuery({ queryKey: key, queryFn: () => fiscalYearService.getAll() });

  const thisYear = new Date().getFullYear();
  const form = useForm<FormValues>({
    resolver: zodResolver(schema) as any,
    defaultValues: {
      year: thisYear,
      fiscalYearName: `FY ${thisYear}`,
      startDate: `${thisYear}-01-01`,
      endDate: `${thisYear}-12-31`,
      isCurrent: false,
    },
  });

  const fail = (title: string) => (error: any) =>
    toast({
      title,
      description: error?.response?.data?.detail ?? error?.message ?? 'Please try again.',
      variant: 'destructive',
    });

  const onSubmit = form.handleSubmit(async (v) => {
    setSaving(true);
    try {
      await fiscalYearService.create({
        year: v.year,
        fiscalYearName: v.fiscalYearName.trim(),
        startDate: v.startDate,
        endDate: v.endDate,
        isCurrent: v.isCurrent,
      });
      await queryClient.invalidateQueries({ queryKey: key });
      toast({ title: 'Fiscal year created' });
      setOpen(false);
      form.reset();
    } catch (e) {
      fail('Could not create the fiscal year')(e);
    } finally {
      setSaving(false);
    }
  });

  const setCurrent = async (fy: FiscalYear) => {
    try {
      await fiscalYearService.setCurrent(fy.id);
      await queryClient.invalidateQueries({ queryKey: key });
      toast({ title: `${fy.fiscalYearName} is now the current year` });
    } catch (e) {
      fail('Could not set the current year')(e);
    }
  };

  const doDelete = async () => {
    if (!deleteTarget) return false;
    try {
      await fiscalYearService.remove(deleteTarget.id);
      await queryClient.invalidateQueries({ queryKey: key });
      toast({ title: 'Fiscal year deleted' });
      setDeleteTarget(null);
      return true;
    } catch (e) {
      fail('Could not delete the fiscal year')(e);
      return false;
    }
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Fiscal years"
        description="The reporting windows the organisation plans against, and the periods inside them."
        backHref="/administration/hr/company-schedule"
        actions={
          <Button onClick={() => setOpen(true)}>
            <Plus className="mr-2 h-4 w-4" /> New fiscal year
          </Button>
        }
      />

      <Card>
        <CardHeader><CardTitle>Years</CardTitle></CardHeader>
        <CardContent>
          <div className="rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Year</TableHead>
                  <TableHead>Name</TableHead>
                  <TableHead>From</TableHead>
                  <TableHead>To</TableHead>
                  <TableHead>Periods</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead className="w-[70px]" />
                </TableRow>
              </TableHeader>
              <TableBody>
                {isLoading ? (
                  [...Array(4)].map((_, i) => (
                    <TableRow key={i}>
                      {[...Array(7)].map((__, j) => (
                        <TableCell key={j}><Skeleton className="h-4 w-full" /></TableCell>
                      ))}
                    </TableRow>
                  ))
                ) : (data ?? []).length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={7}>
                      <EmptyState
                        icon={CalendarRange}
                        title="No fiscal years yet"
                        description="Define the first reporting window, then add its periods."
                        action={
                          <Button size="sm" onClick={() => setOpen(true)}>
                            <Plus className="mr-2 h-4 w-4" /> New fiscal year
                          </Button>
                        }
                      />
                    </TableCell>
                  </TableRow>
                ) : (
                  [...(data ?? [])]
                    .sort((a, b) => b.year - a.year)
                    .map((fy) => (
                      <TableRow
                        key={fy.id}
                        className="cursor-pointer hover:bg-muted/50"
                        onClick={() =>
                          router.push(`/administration/hr/company-schedule/fiscal-years/${fy.id}`)
                        }
                      >
                        <TableCell className="font-medium">{fy.year}</TableCell>
                        <TableCell>
                          {fy.fiscalYearName}
                          {fy.isCurrent && <Badge className="ml-2">Current</Badge>}
                        </TableCell>
                        <TableCell>{fy.startDate.slice(0, 10)}</TableCell>
                        <TableCell>{fy.endDate.slice(0, 10)}</TableCell>
                        <TableCell>{fy.periodCount}</TableCell>
                        <TableCell><StatusBadge status={fy.status} /></TableCell>
                        <TableCell onClick={(e) => e.stopPropagation()}>
                          <DropdownMenu>
                            <DropdownMenuTrigger asChild>
                              <Button variant="ghost" size="icon">
                                <MoreHorizontal className="h-4 w-4" />
                                <span className="sr-only">Actions</span>
                              </Button>
                            </DropdownMenuTrigger>
                            <DropdownMenuContent align="end">
                              {!fy.isCurrent && (
                                <DropdownMenuItem onClick={() => setCurrent(fy)}>
                                  <Check className="mr-2 h-4 w-4" /> Set as current
                                </DropdownMenuItem>
                              )}
                              <DropdownMenuSeparator />
                              <DropdownMenuItem className="text-destructive" onClick={() => setDeleteTarget(fy)}>
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

      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent>
          <form onSubmit={onSubmit}>
            <DialogHeader>
              <DialogTitle>New fiscal year</DialogTitle>
              <DialogDescription>
                The year number cannot be changed afterwards — everything else can.
              </DialogDescription>
            </DialogHeader>
            <div className="space-y-4 py-4">
              <FieldRow>
                <NumberField form={form} name="year" label="Year" required />
                <TextField form={form} name="fiscalYearName" label="Name" required />
              </FieldRow>
              <FieldRow>
                <DateField form={form} name="startDate" label="From" required />
                <DateField form={form} name="endDate" label="To" required />
              </FieldRow>
              <SwitchField
                form={form}
                name="isCurrent"
                label="Make this the current year"
                description="Only one year is current at a time."
              />
            </div>
            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => setOpen(false)}>Cancel</Button>
              <Button type="submit" disabled={saving}>
                {saving && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                Create
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      <ConfirmationDialog
        open={!!deleteTarget}
        onOpenChange={(o) => !o && setDeleteTarget(null)}
        title="Delete this fiscal year?"
        description={`"${deleteTarget?.fiscalYearName}" and its periods will be removed.`}
        confirmText="Delete"
        variant="destructive"
        onConfirm={doDelete}
      />
    </div>
  );
}
