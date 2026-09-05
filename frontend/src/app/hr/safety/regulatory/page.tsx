'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { z } from 'zod';
import { Loader2, Plus, Scale } from 'lucide-react';
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
  DateField,
  SwitchField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { safetyRegulatoryService } from '@/services/hr/safety-regulatory.service';
import { safetyReferenceService } from '@/services/hr/safety-reference.service';
import { SHE_REGULATORY_DOMAIN_OPTIONS } from '@/types/hr/safety';
import type { SheRegulatoryDomain } from '@/types/hr/safety';
import { SHE_COMPLIANCE_STATUS_OPTIONS } from '@/types/hr/safety-governance';
import type {
  SheComplianceStatus,
  SheRegulatoryObligationSummary,
} from '@/types/hr/safety-governance';

/**
 * Regulatory obligations register (FR-SHE-180–182): every statutory duty with its body,
 * owner, compliance status and review date. GNFS liaison (FR-SHE-084) lives here — GNFS is
 * a regulatory body; its statutory inspections/certifications are FireSafety obligations
 * with evidence records. Due-date chasing is automatic — the reminder engine walks the
 * statutory 180/90/60/30/14/7 ladder on every active obligation's review date.
 */
const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const blank = (v?: string) => (v && v.length > 0 ? v : null);

const obligationSchema = z.object({
  obligationCode: z.string().min(1, 'A code is required').max(30),
  title: z.string().min(1, 'A title is required').max(300),
  description: z.string().max(2000).optional().or(z.literal('')),
  domain: z.string().min(1),
  legislationName: z.string().max(200).optional().or(z.literal('')),
  sectionOrClause: z.string().max(100).optional().or(z.literal('')),
  regulatoryBodyId: z.string().optional().or(z.literal('')),
  complianceStatus: z.string().min(1),
  complianceNotes: z.string().max(1000).optional().or(z.literal('')),
  obligationOwnerId: z.string().optional().or(z.literal('')),
  nextReviewDate: z.string().optional().or(z.literal('')),
  isActive: z.boolean(),
});
type ObligationForm = z.input<typeof obligationSchema>;

const statusVariant = (s: SheComplianceStatus) =>
  s === 'NonCompliant' ? 'destructive' : s === 'Compliant' ? 'default' : 'secondary';

function ObligationsTable({
  rows,
  highlightReview,
}: {
  rows: SheRegulatoryObligationSummary[];
  highlightReview?: boolean;
}) {
  if (rows.length === 0) {
    return (
      <EmptyState
        title="Nothing here"
        description="Obligations matching this view will appear here."
        icon={Scale}
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
              <TableHead>Obligation</TableHead>
              <TableHead>Domain</TableHead>
              <TableHead>Body</TableHead>
              <TableHead>Owner</TableHead>
              <TableHead>Compliance</TableHead>
              <TableHead>Next review</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {rows.map((o) => (
              <TableRow key={o.id}>
                <TableCell>
                  <Link
                    href={`/hr/safety/regulatory/${o.id}`}
                    className="font-mono text-primary hover:underline"
                  >
                    {o.obligationCode}
                  </Link>
                </TableCell>
                <TableCell className="max-w-[320px] truncate font-medium" title={o.title}>
                  {o.title}
                </TableCell>
                <TableCell>{o.domainName}</TableCell>
                <TableCell>{o.regulatoryBodyName ?? '—'}</TableCell>
                <TableCell>{o.obligationOwnerName ?? 'Unassigned'}</TableCell>
                <TableCell>
                  <Badge variant={statusVariant(o.complianceStatus)}>{o.complianceStatusName}</Badge>
                </TableCell>
                <TableCell className={highlightReview ? 'text-destructive font-medium' : undefined}>
                  {fmtDate(o.nextReviewDate)}
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </CardContent>
    </Card>
  );
}

export default function RegulatoryCompliancePage() {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [dialogOpen, setDialogOpen] = useState(false);
  const [domainFilter, setDomainFilter] = useState<string>('All');
  const [busy, setBusy] = useState(false);

  const { data: all = [], isLoading } = useQuery({
    queryKey: ['hr', 'safety-regulatory', 'obligations'],
    queryFn: () => safetyRegulatoryService.getAllObligations(),
  });
  const { data: nonCompliant = [] } = useQuery({
    queryKey: ['hr', 'safety-regulatory', 'non-compliant'],
    queryFn: () => safetyRegulatoryService.getNonCompliantObligations(),
  });
  const { data: dueForReview = [] } = useQuery({
    queryKey: ['hr', 'safety-regulatory', 'due-for-review'],
    queryFn: () => safetyRegulatoryService.getObligationsDueForReview(30),
  });
  const { data: bodies = [] } = useQuery({
    queryKey: ['hr', 'safety-reference', 'regulatory-bodies'],
    queryFn: () => safetyReferenceService.getRegulatoryBodies(true),
  });

  const filtered =
    domainFilter === 'All' ? all : all.filter((o) => o.domain === (domainFilter as SheRegulatoryDomain));

  const form = useForm<ObligationForm>({ resolver: zodResolver(obligationSchema) });

  const openCreate = () => {
    form.reset({
      obligationCode: '',
      title: '',
      description: '',
      domain: 'OccupationalSafety',
      legislationName: '',
      sectionOrClause: '',
      regulatoryBodyId: '',
      complianceStatus: 'NotAssessed',
      complianceNotes: '',
      obligationOwnerId: '',
      nextReviewDate: '',
      isActive: true,
    });
    setDialogOpen(true);
  };

  const submit = form.handleSubmit(async (values) => {
    setBusy(true);
    try {
      const v = obligationSchema.parse(values);
      await safetyRegulatoryService.createObligation({
        obligationCode: v.obligationCode,
        title: v.title,
        description: blank(v.description),
        domain: v.domain as SheRegulatoryDomain,
        legislationName: blank(v.legislationName),
        sectionOrClause: blank(v.sectionOrClause),
        regulatoryBodyId: blank(v.regulatoryBodyId),
        complianceStatus: v.complianceStatus as SheComplianceStatus,
        complianceNotes: blank(v.complianceNotes),
        obligationOwnerId: blank(v.obligationOwnerId),
        nextReviewDate: v.nextReviewDate ? new Date(v.nextReviewDate).toISOString() : null,
        isActive: v.isActive,
      });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'safety-regulatory'] });
      toast({ title: 'Obligation registered' });
      setDialogOpen(false);
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Registering the obligation failed.',
        variant: 'destructive',
      });
    } finally {
      setBusy(false);
    }
  });

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Regulatory Compliance"
        description="Statutory obligations with their regulatory body, owner and evidence trail — including GNFS fire certifications and EPA duties. Reviews are chased automatically on the statutory 180/90/60/30/14/7 reminder ladder, with tiered escalation once overdue."
        backHref="/hr/safety"
        actions={
          <Button onClick={openCreate}>
            <Plus className="mr-2 h-4 w-4" /> Register obligation
          </Button>
        }
      />

      <Tabs defaultValue="all">
        <TabsList>
          <TabsTrigger value="all">All ({all.length})</TabsTrigger>
          <TabsTrigger value="non-compliant">Non-compliant ({nonCompliant.length})</TabsTrigger>
          <TabsTrigger value="due">Due for review ({dueForReview.length})</TabsTrigger>
        </TabsList>

        <TabsContent value="all" className="mt-4 space-y-3">
          <div className="flex items-center gap-2">
            <span className="text-muted-foreground text-sm">Domain:</span>
            <select
              className="border-input bg-background rounded-md border px-2 py-1 text-sm"
              value={domainFilter}
              onChange={(e) => setDomainFilter(e.target.value)}
            >
              <option value="All">All domains</option>
              {SHE_REGULATORY_DOMAIN_OPTIONS.map((o) => (
                <option key={o.value} value={o.value}>
                  {o.label}
                </option>
              ))}
            </select>
          </div>
          {isLoading ? null : <ObligationsTable rows={filtered} />}
        </TabsContent>

        <TabsContent value="non-compliant" className="mt-4">
          <ObligationsTable rows={nonCompliant} />
        </TabsContent>

        <TabsContent value="due" className="mt-4 space-y-3">
          <p className="text-muted-foreground text-sm">
            Reviews falling due in the next 30 days (or already past). The reminder engine
            chases these automatically on the statutory ladder; this queue stays the work view.
          </p>
          <ObligationsTable rows={dueForReview} highlightReview />
        </TabsContent>
      </Tabs>

      <Dialog open={dialogOpen} onOpenChange={(o) => !busy && setDialogOpen(o)}>
        <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-[640px]">
          <DialogHeader>
            <DialogTitle>Register obligation</DialogTitle>
            <DialogDescription>
              The code is unique per tenant and fixed after creation. Evidence is recorded on the
              obligation page.
            </DialogDescription>
          </DialogHeader>
          <form onSubmit={submit} className="space-y-4">
            <FieldRow>
              <TextField
                form={form}
                name="obligationCode"
                label="Code (unique)"
                required
                placeholder="e.g. REG-GNFS-001"
              />
              <SelectField
                form={form}
                name="domain"
                label="Domain"
                required
                options={SHE_REGULATORY_DOMAIN_OPTIONS}
              />
            </FieldRow>
            <TextField form={form} name="title" label="Title" required />
            <TextareaField form={form} name="description" label="Description" rows={2} />
            <FieldRow>
              <TextField form={form} name="legislationName" label="Legislation" />
              <TextField form={form} name="sectionOrClause" label="Section / clause" />
            </FieldRow>
            <FieldRow>
              <SelectField
                form={form}
                name="regulatoryBodyId"
                label="Regulatory body"
                allowEmpty
                emptyLabel="Not set"
                options={bodies.map((b) => ({ value: b.id, label: b.shortName ?? b.name }))}
              />
              <SelectField
                form={form}
                name="complianceStatus"
                label="Compliance status"
                required
                options={SHE_COMPLIANCE_STATUS_OPTIONS}
              />
            </FieldRow>
            <TextareaField form={form} name="complianceNotes" label="Compliance notes" rows={2} />
            <FieldRow>
              <DateField form={form} name="nextReviewDate" label="Next review" />
              <div className="self-end pb-1">
                <SwitchField form={form} name="isActive" label="Active" />
              </div>
            </FieldRow>
            <EmployeePickerField form={form} name="obligationOwnerId" label="Obligation owner" />
            <DialogFooter>
              <Button type="button" variant="outline" disabled={busy} onClick={() => setDialogOpen(false)}>
                Cancel
              </Button>
              <Button type="submit" disabled={busy}>
                {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                Register
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>
    </div>
  );
}
