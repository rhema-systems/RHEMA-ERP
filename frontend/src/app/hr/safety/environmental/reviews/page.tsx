'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { z } from 'zod';
import { ClipboardCheck, Loader2, Plus } from 'lucide-react';
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
import { EmptyState } from '@/components/hr/common/EmptyState';
import { EmployeePickerField } from '@/components/hr/attendance/EmployeePickerField';
import {
  TextField,
  TextareaField,
  SelectField,
  SwitchField,
  DateField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { safetyEnvironmentalComplianceService } from '@/services/hr/safety-environmental-compliance.service';
import { SHE_ENV_WORK_CLASSIFICATION_OPTIONS } from '@/types/hr/safety-environment-compliance';
import type {
  SheEnvironmentalReviewSummary,
  SheEnvironmentalReviewStatus,
  SheEnvironmentalWorkClassification,
} from '@/types/hr/safety-environment-compliance';

/**
 * Environmental compliance reviews (FR-ENV-001–016): every planned project, upgrade or
 * infrastructure change is screened, approved and cleared before work proceeds. FR-ENV-010's
 * hard block awaits the Project module — reviews are recorded against a free-text project
 * reference for now.
 */
const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const blank = (v?: string) => (v && v.length > 0 ? v : null);

const reviewSchema = z.object({
  projectName: z.string().min(1, 'A project name is required').max(300),
  workClassification: z.string().min(1),
  projectReference: z.string().max(200).optional().or(z.literal('')),
  responsibleManagerId: z.string().optional().or(z.literal('')),
  plannedStartDate: z.string().optional().or(z.literal('')),
  description: z.string().min(1, 'A description is required').max(4000),
  applicableLaws: z.string().max(2000).optional().or(z.literal('')),
  permitRequired: z.boolean(),
  complianceChecklist: z.string().max(4000).optional().or(z.literal('')),
  notes: z.string().max(2000).optional().or(z.literal('')),
});
type ReviewForm = z.input<typeof reviewSchema>;

function ReviewStatusBadge({
  status,
  statusName,
}: {
  status: SheEnvironmentalReviewStatus;
  statusName: string;
}) {
  switch (status) {
    case 'Submitted':
      return <Badge variant="secondary">{statusName}</Badge>;
    case 'CorrectionsRequested':
      return (
        <Badge variant="outline" className="border-amber-500 text-amber-600">
          {statusName}
        </Badge>
      );
    case 'Rejected':
      return <Badge variant="destructive">{statusName}</Badge>;
    case 'ClearanceIssued':
      return <Badge className="bg-green-600 text-white hover:bg-green-600">{statusName}</Badge>;
    default:
      return <Badge>{statusName}</Badge>;
  }
}

function ReviewTable({
  items,
  emptyText,
}: {
  items: SheEnvironmentalReviewSummary[];
  emptyText: string;
}) {
  const router = useRouter();
  if (items.length === 0) {
    return <EmptyState title="Nothing here" description={emptyText} icon={ClipboardCheck} />;
  }
  return (
    <Card>
      <CardContent className="p-0">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Review #</TableHead>
              <TableHead>Project</TableHead>
              <TableHead>Classification</TableHead>
              <TableHead>Unit</TableHead>
              <TableHead>Submitted</TableHead>
              <TableHead>Planned start</TableHead>
              <TableHead>Flags</TableHead>
              <TableHead>Status</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {items.map((r) => (
              <TableRow
                key={r.id}
                className="cursor-pointer"
                onClick={() => router.push(`/hr/safety/environmental/reviews/${r.id}`)}
              >
                <TableCell className="font-mono">
                  <Link
                    href={`/hr/safety/environmental/reviews/${r.id}`}
                    className="hover:underline"
                    onClick={(e) => e.stopPropagation()}
                  >
                    {r.reviewNumber}
                  </Link>
                </TableCell>
                <TableCell className="max-w-xs font-medium">
                  <span className="line-clamp-1">{r.projectName}</span>
                </TableCell>
                <TableCell>{r.workClassificationName}</TableCell>
                <TableCell>{r.organizationUnitName ?? '—'}</TableCell>
                <TableCell className="tabular-nums">{fmtDate(r.submittedDate)}</TableCell>
                <TableCell className="tabular-nums">{fmtDate(r.plannedStartDate)}</TableCell>
                <TableCell>
                  <div className="flex flex-wrap gap-1">
                    {r.permitRequired && (
                      <Badge variant="outline" className="text-xs">
                        Permit required
                      </Badge>
                    )}
                    {r.requiresManagementApproval && (
                      <Badge variant="outline" className="text-xs">
                        Mgmt approval
                      </Badge>
                    )}
                    {!r.permitRequired && !r.requiresManagementApproval && (
                      <span className="text-muted-foreground text-sm">—</span>
                    )}
                  </div>
                </TableCell>
                <TableCell>
                  <ReviewStatusBadge status={r.status} statusName={r.statusName} />
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </CardContent>
    </Card>
  );
}

export default function EnvironmentalReviewsPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [createOpen, setCreateOpen] = useState(false);
  const [busy, setBusy] = useState(false);

  const { data: reviews = [] } = useQuery({
    queryKey: ['hr', 'safety-env-compliance', 'reviews'],
    queryFn: () => safetyEnvironmentalComplianceService.getReviews(),
  });

  const open = reviews.filter(
    (r) => r.status === 'Submitted' || r.status === 'CorrectionsRequested',
  );
  const approved = reviews.filter((r) => r.status === 'Approved');
  const cleared = reviews.filter((r) => r.status === 'ClearanceIssued');
  const rejected = reviews.filter((r) => r.status === 'Rejected');

  const form = useForm<ReviewForm>({ resolver: zodResolver(reviewSchema) });

  const openCreate = () => {
    form.reset({
      projectName: '',
      workClassification: 'PlannedProject',
      projectReference: '',
      responsibleManagerId: '',
      plannedStartDate: '',
      description: '',
      applicableLaws: '',
      permitRequired: false,
      complianceChecklist: '',
      notes: '',
    });
    setCreateOpen(true);
  };

  const submitCreate = form.handleSubmit(async (values) => {
    setBusy(true);
    try {
      const v = reviewSchema.parse(values);
      const created = await safetyEnvironmentalComplianceService.createReview({
        projectName: v.projectName,
        workClassification: v.workClassification as SheEnvironmentalWorkClassification,
        projectReference: blank(v.projectReference),
        responsibleManagerId: blank(v.responsibleManagerId),
        plannedStartDate: blank(v.plannedStartDate),
        description: v.description,
        applicableLaws: blank(v.applicableLaws),
        permitRequired: v.permitRequired,
        complianceChecklist: blank(v.complianceChecklist),
        notes: blank(v.notes),
      });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'safety-env-compliance'] });
      toast({ title: 'Review submitted', description: created.reviewNumber });
      setCreateOpen(false);
      router.push(`/hr/safety/environmental/reviews/${created.id}`);
    } catch (err) {
      toast({
        title: 'Error',
        description: (err as Error).message || 'Submitting the review failed.',
        variant: 'destructive',
      });
    } finally {
      setBusy(false);
    }
  });

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Environmental Compliance Reviews"
        description="Screening → approval → clearance before work proceeds. FR-ENV-010's hard block awaits the Project module — reviews are recorded against a free-text project reference for now."
        backHref="/hr/safety"
        actions={
          <Button onClick={openCreate}>
            <Plus className="mr-2 h-4 w-4" /> New review
          </Button>
        }
      />

      <Tabs defaultValue="open">
        <TabsList>
          <TabsTrigger value="open">Open ({open.length})</TabsTrigger>
          <TabsTrigger value="approved">Approved ({approved.length})</TabsTrigger>
          <TabsTrigger value="cleared">Cleared ({cleared.length})</TabsTrigger>
          <TabsTrigger value="rejected">Rejected ({rejected.length})</TabsTrigger>
          <TabsTrigger value="all">All ({reviews.length})</TabsTrigger>
        </TabsList>
        <TabsContent value="open" className="mt-4">
          <ReviewTable items={open} emptyText="No review is awaiting a decision." />
        </TabsContent>
        <TabsContent value="approved" className="mt-4">
          <ReviewTable items={approved} emptyText="No approved review yet." />
        </TabsContent>
        <TabsContent value="cleared" className="mt-4">
          <ReviewTable items={cleared} emptyText="No clearance has been issued yet." />
        </TabsContent>
        <TabsContent value="rejected" className="mt-4">
          <ReviewTable items={rejected} emptyText="No review has been rejected." />
        </TabsContent>
        <TabsContent value="all" className="mt-4">
          <ReviewTable items={reviews} emptyText="No environmental compliance review recorded yet." />
        </TabsContent>
      </Tabs>

      <Dialog open={createOpen} onOpenChange={(o) => !busy && setCreateOpen(o)}>
        <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-[680px]">
          <DialogHeader>
            <DialogTitle>New environmental compliance review</DialogTitle>
            <DialogDescription>
              Leave the number to the server — the review starts as Submitted and goes through
              screening before it can be approved.
            </DialogDescription>
          </DialogHeader>
          <form onSubmit={submitCreate} className="space-y-4">
            <TextField form={form} name="projectName" label="Project name" required />
            <FieldRow>
              <SelectField
                form={form}
                name="workClassification"
                label="Work classification"
                required
                options={SHE_ENV_WORK_CLASSIFICATION_OPTIONS}
              />
              <div className="space-y-2">
                <TextField form={form} name="projectReference" label="Project reference" />
                <p className="text-muted-foreground text-xs">
                  Future Project-module link — free text for now.
                </p>
              </div>
            </FieldRow>
            <EmployeePickerField form={form} name="responsibleManagerId" label="Responsible manager" />
            <div className="space-y-2">
              <DateField form={form} name="plannedStartDate" label="Planned start date" />
              <p className="text-muted-foreground text-xs">
                Drives the 90/60/30-day project notifications.
              </p>
            </div>
            <TextareaField form={form} name="description" label="Description" rows={3} required />
            <TextareaField form={form} name="applicableLaws" label="Applicable laws" rows={2} />
            <SwitchField form={form} name="permitRequired" label="Permit required" />
            <TextareaField
              form={form}
              name="complianceChecklist"
              label="Compliance checklist"
              rows={2}
            />
            <TextareaField form={form} name="notes" label="Notes" rows={2} />
            <DialogFooter>
              <Button
                type="button"
                variant="outline"
                disabled={busy}
                onClick={() => setCreateOpen(false)}
              >
                Cancel
              </Button>
              <Button type="submit" disabled={busy}>
                {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                Submit review
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>
    </div>
  );
}
