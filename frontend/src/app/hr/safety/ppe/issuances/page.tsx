'use client';

import { useMemo, useState } from 'react';
import { z } from 'zod';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { HardHat, Loader2, Undo2 } from 'lucide-react';
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
import { Label } from '@/components/ui/label';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import { EmptyState } from '@/components/hr/common/EmptyState';
import {
  TextField,
  NumberField,
  DateField,
  SelectField,
  TextareaField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { safetyPpeService } from '@/services/hr/safety-ppe.service';
import { SHE_PPE_CONDITION_OPTIONS } from '@/types/hr/safety-ppe';
import type { PpeIssuance, ShePpeCondition } from '@/types/hr/safety-ppe';

/**
 * The PPE issuance register (FR-SHE-132) — who holds what, what is due back and what has come
 * back. Issue and return both name their acting employees explicitly (issuer / receiver); a
 * return on an already-returned issuance is refused. Expiry is visible here but nothing alerts
 * on it yet (slice-13 job engine).
 */
const issueSchema = z.object({
  ppeTypeId: z.string().min(1, 'A PPE type is required'),
  quantity: z.coerce.number().min(1).max(100),
  size: z.string().max(20).optional().or(z.literal('')),
  serialNumber: z.string().max(100).optional().or(z.literal('')),
  issueDate: z.string().min(1, 'An issue date is required'),
  expiryDate: z.string().optional().or(z.literal('')),
  expectedReturnDate: z.string().optional().or(z.literal('')),
  conditionWhenIssued: z
    .enum(['New', 'Good', 'Fair', 'Worn', 'Damaged', 'Condemned'])
    .optional(),
  notes: z.string().max(500).optional().or(z.literal('')),
});

type IssueForm = z.input<typeof issueSchema>;

const emptyIssue: IssueForm = {
  ppeTypeId: '',
  quantity: 1,
  size: '',
  serialNumber: '',
  issueDate: new Date().toISOString().slice(0, 10),
  expiryDate: '',
  expectedReturnDate: '',
  conditionWhenIssued: 'New',
  notes: '',
};

const returnSchema = z.object({
  conditionWhenReturned: z
    .enum(['New', 'Good', 'Fair', 'Worn', 'Damaged', 'Condemned'])
    .optional(),
  notes: z.string().max(500).optional().or(z.literal('')),
});

type ReturnForm = z.input<typeof returnSchema>;

const blank = (v?: string) => (v && v.length > 0 ? v : null);
const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

function statusOf(i: PpeIssuance): { label: string; variant: 'secondary' | 'destructive' | 'outline' } {
  if (i.isReturned) return { label: 'Returned', variant: 'outline' };
  if (i.expectedReturnDate && new Date(i.expectedReturnDate) < new Date())
    return { label: 'Overdue return', variant: 'destructive' };
  return { label: 'Outstanding', variant: 'secondary' };
}

function IssuanceTable({
  items,
  emptyText,
  onReturn,
}: {
  items: PpeIssuance[];
  emptyText: string;
  onReturn: (issuance: PpeIssuance) => void;
}) {
  if (items.length === 0) {
    return <EmptyState title="Nothing here" description={emptyText} icon={HardHat} />;
  }
  return (
    <Card>
      <CardContent className="p-0">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Employee</TableHead>
              <TableHead>PPE type</TableHead>
              <TableHead>Issued</TableHead>
              <TableHead>Expiry</TableHead>
              <TableHead>Due back</TableHead>
              <TableHead>Status</TableHead>
              <TableHead>Issued by</TableHead>
              <TableHead className="w-28" />
            </TableRow>
          </TableHeader>
          <TableBody>
            {items.map((i) => {
              const s = statusOf(i);
              const expired = !!i.expiryDate && new Date(i.expiryDate) < new Date();
              return (
                <TableRow key={i.id}>
                  <TableCell>
                    <span className="font-medium">{i.employeeName}</span>
                    {i.employeeNumber && (
                      <span className="text-muted-foreground ml-1 font-mono text-xs">
                        {i.employeeNumber}
                      </span>
                    )}
                  </TableCell>
                  <TableCell>
                    {i.ppeTypeName}
                    {i.size ? ` · ${i.size}` : ''}
                    {i.serialNumber && (
                      <span className="text-muted-foreground ml-1 font-mono text-xs">
                        {i.serialNumber}
                      </span>
                    )}
                  </TableCell>
                  <TableCell className="text-sm">
                    {fmtDate(i.issueDate)} · qty {i.quantity}
                  </TableCell>
                  <TableCell className="text-sm">
                    {expired ? (
                      <Badge variant="destructive">Expired {fmtDate(i.expiryDate)}</Badge>
                    ) : (
                      fmtDate(i.expiryDate)
                    )}
                  </TableCell>
                  <TableCell className="text-sm">
                    {i.isReturned
                      ? `Returned ${fmtDate(i.actualReturnDate)}`
                      : fmtDate(i.expectedReturnDate)}
                  </TableCell>
                  <TableCell>
                    <Badge variant={s.variant}>{s.label}</Badge>
                  </TableCell>
                  <TableCell className="text-sm">{i.issuedByName}</TableCell>
                  <TableCell>
                    {!i.isReturned && (
                      <Button variant="outline" size="sm" onClick={() => onReturn(i)}>
                        <Undo2 className="mr-1 h-3 w-3" /> Return
                      </Button>
                    )}
                  </TableCell>
                </TableRow>
              );
            })}
          </TableBody>
        </Table>
      </CardContent>
    </Card>
  );
}

export default function SafetyPpeIssuancesPage() {
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [issueOpen, setIssueOpen] = useState(false);
  const [issueEmployeeId, setIssueEmployeeId] = useState<string | null>(null);
  const [issuedById, setIssuedById] = useState<string | null>(null);
  const [returning, setReturning] = useState<PpeIssuance | null>(null);
  const [returnedToId, setReturnedToId] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [lookupEmployeeId, setLookupEmployeeId] = useState<string | null>(null);

  const issueForm = useForm<IssueForm>({
    resolver: zodResolver(issueSchema),
    defaultValues: emptyIssue,
  });
  const returnForm = useForm<ReturnForm>({
    resolver: zodResolver(returnSchema),
    defaultValues: { conditionWhenReturned: 'Good', notes: '' },
  });

  const { data: types = [] } = useQuery({
    queryKey: ['hr', 'safety-ppe', 'types', 'active'],
    queryFn: () => safetyPpeService.getTypes(true),
  });
  const { data: outstanding = [] } = useQuery({
    queryKey: ['hr', 'safety-ppe', 'issuances', 'outstanding'],
    queryFn: () => safetyPpeService.getOutstandingIssuances(),
  });
  const { data: overdue = [] } = useQuery({
    queryKey: ['hr', 'safety-ppe', 'issuances', 'overdue'],
    queryFn: () => safetyPpeService.getOverdueReturns(),
  });
  const { data: byEmployee = [] } = useQuery({
    queryKey: ['hr', 'safety-ppe', 'issuances', 'by-employee', lookupEmployeeId],
    queryFn: () => safetyPpeService.getIssuancesByEmployee(lookupEmployeeId!),
    enabled: !!lookupEmployeeId,
  });

  const selectedType = useMemo(
    () => types.find((t) => t.id === issueForm.watch('ppeTypeId')),
    [types, issueForm.watch('ppeTypeId')],
  );

  const refresh = () =>
    queryClient.invalidateQueries({ queryKey: ['hr', 'safety-ppe', 'issuances'] });

  const submitIssue = issueForm.handleSubmit(async (values) => {
    if (!issueEmployeeId || !issuedById) {
      toast({
        title: 'Missing employee',
        description: 'Both the receiving employee and the issuer are required.',
        variant: 'destructive',
      });
      return;
    }
    setBusy(true);
    try {
      const v = issueSchema.parse(values);
      await safetyPpeService.issue({
        employeeId: issueEmployeeId,
        ppeTypeId: v.ppeTypeId,
        issueDate: new Date(v.issueDate).toISOString(),
        quantity: v.quantity,
        size: blank(v.size),
        serialNumber: blank(v.serialNumber),
        expiryDate: v.expiryDate ? new Date(v.expiryDate).toISOString() : null,
        expectedReturnDate: v.expectedReturnDate
          ? new Date(v.expectedReturnDate).toISOString()
          : null,
        conditionWhenIssued: (v.conditionWhenIssued as ShePpeCondition | undefined) ?? null,
        issuedById,
        notes: blank(v.notes),
      });
      await refresh();
      toast({ title: 'PPE issued' });
      setIssueOpen(false);
      issueForm.reset(emptyIssue);
      setIssueEmployeeId(null);
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Issuing failed.',
        variant: 'destructive',
      });
    } finally {
      setBusy(false);
    }
  });

  const submitReturn = returnForm.handleSubmit(async (values) => {
    if (!returning) return;
    if (!returnedToId) {
      toast({
        title: 'Missing receiver',
        description: 'Name the employee the PPE was returned to.',
        variant: 'destructive',
      });
      return;
    }
    setBusy(true);
    try {
      const v = returnSchema.parse(values);
      await safetyPpeService.recordReturn(returning.id, {
        issuanceId: returning.id,
        actualReturnDate: new Date().toISOString(),
        conditionWhenReturned: (v.conditionWhenReturned as ShePpeCondition | undefined) ?? null,
        returnedToId,
        notes: blank(v.notes),
      });
      await refresh();
      toast({ title: 'Return recorded' });
      setReturning(null);
      returnForm.reset({ conditionWhenReturned: 'Good', notes: '' });
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Recording the return failed.',
        variant: 'destructive',
      });
    } finally {
      setBusy(false);
    }
  });

  const openReturn = (issuance: PpeIssuance) => {
    setReturnedToId(null);
    returnForm.reset({ conditionWhenReturned: 'Good', notes: '' });
    setReturning(issuance);
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="PPE Issuance"
        description="Who holds what, what is due back, and what has come back. Stock levels live on the PPE stock register."
        backHref="/hr/safety/ppe"
        actions={
          <Button onClick={() => setIssueOpen(true)}>
            <HardHat className="mr-2 h-4 w-4" /> Issue PPE
          </Button>
        }
      />

      <Tabs defaultValue="outstanding">
        <TabsList>
          <TabsTrigger value="outstanding">Outstanding ({outstanding.length})</TabsTrigger>
          <TabsTrigger value="overdue">Overdue returns ({overdue.length})</TabsTrigger>
          <TabsTrigger value="by-employee">By employee</TabsTrigger>
        </TabsList>
        <TabsContent value="outstanding" className="mt-4">
          <IssuanceTable
            items={outstanding}
            emptyText="No PPE is currently out with employees."
            onReturn={openReturn}
          />
        </TabsContent>
        <TabsContent value="overdue" className="mt-4">
          <IssuanceTable
            items={overdue}
            emptyText="Nothing is overdue for return."
            onReturn={openReturn}
          />
        </TabsContent>
        <TabsContent value="by-employee" className="mt-4 space-y-4">
          <div className="max-w-md space-y-2">
            <Label>Employee</Label>
            <EmployeePicker value={lookupEmployeeId} onChange={(id) => setLookupEmployeeId(id)} />
          </div>
          {lookupEmployeeId && (
            <IssuanceTable
              items={byEmployee}
              emptyText="No PPE has ever been issued to this employee."
              onReturn={openReturn}
            />
          )}
        </TabsContent>
      </Tabs>

      {/* ── Issue dialog ── */}
      <Dialog open={issueOpen} onOpenChange={(o) => !busy && setIssueOpen(o)}>
        <DialogContent className="max-h-[90vh] max-w-2xl overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Issue PPE</DialogTitle>
            <DialogDescription>
              Records the hand-over to an employee. The issuer is named explicitly — issuing does
              not deduct stock automatically.
            </DialogDescription>
          </DialogHeader>
          <form onSubmit={submitIssue} className="space-y-4">
            <FieldRow>
              <div className="space-y-2">
                <Label>
                  Employee <span className="text-destructive">*</span>
                </Label>
                <EmployeePicker value={issueEmployeeId} onChange={(id) => setIssueEmployeeId(id)} />
              </div>
              <div className="space-y-2">
                <Label>
                  Issued by <span className="text-destructive">*</span>
                </Label>
                <EmployeePicker value={issuedById} onChange={(id) => setIssuedById(id)} />
              </div>
            </FieldRow>
            <FieldRow>
              <SelectField
                form={issueForm}
                name="ppeTypeId"
                label="PPE type"
                required
                options={types.map((t) => ({ value: t.id, label: `${t.code} — ${t.name}` }))}
              />
              <NumberField form={issueForm} name="quantity" label="Quantity" required />
            </FieldRow>
            <FieldRow>
              <TextField form={issueForm} name="size" label="Size" />
              <TextField
                form={issueForm}
                name="serialNumber"
                label={`Serial number${selectedType?.requiresSerialNumber ? ' (required by this type)' : ''}`}
              />
            </FieldRow>
            <FieldRow>
              <DateField form={issueForm} name="issueDate" label="Issue date" required />
              <SelectField
                form={issueForm}
                name="conditionWhenIssued"
                label="Condition when issued"
                options={SHE_PPE_CONDITION_OPTIONS}
              />
            </FieldRow>
            <FieldRow>
              <DateField
                form={issueForm}
                name="expiryDate"
                label={`Expiry date${selectedType?.hasExpiryDate ? ' (this type expires)' : ''}`}
              />
              <DateField form={issueForm} name="expectedReturnDate" label="Expected return" />
            </FieldRow>
            <TextareaField form={issueForm} name="notes" label="Notes" rows={2} />
            <DialogFooter>
              <Button type="button" variant="outline" disabled={busy} onClick={() => setIssueOpen(false)}>
                Cancel
              </Button>
              <Button type="submit" disabled={busy}>
                {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                Issue
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      {/* ── Return dialog ── */}
      <Dialog open={!!returning} onOpenChange={(o) => !busy && !o && setReturning(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Record a return</DialogTitle>
            <DialogDescription>
              {returning
                ? `${returning.ppeTypeName} issued to ${returning.employeeName} on ${fmtDate(returning.issueDate)}.`
                : ''}
            </DialogDescription>
          </DialogHeader>
          <form onSubmit={submitReturn} className="space-y-4">
            <div className="space-y-2">
              <Label>
                Returned to <span className="text-destructive">*</span>
              </Label>
              <EmployeePicker value={returnedToId} onChange={(id) => setReturnedToId(id)} />
            </div>
            <SelectField
              form={returnForm}
              name="conditionWhenReturned"
              label="Condition when returned"
              options={SHE_PPE_CONDITION_OPTIONS}
            />
            <TextareaField form={returnForm} name="notes" label="Notes" rows={2} />
            <DialogFooter>
              <Button type="button" variant="outline" disabled={busy} onClick={() => setReturning(null)}>
                Cancel
              </Button>
              <Button type="submit" disabled={busy}>
                {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                Record return
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>
    </div>
  );
}
