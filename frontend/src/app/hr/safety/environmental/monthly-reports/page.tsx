'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { z } from 'zod';
import { FileBarChart, Loader2, Plus } from 'lucide-react';
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
import { NumberField, SelectField } from '@/components/hr/employee/tabs/fields';
import { safetyEnvironmentalComplianceService } from '@/services/hr/safety-environmental-compliance.service';

/**
 * Monthly environmental reports (FR-ENV-033/034): computed from the live
 * registers at generation time. The reminder engine generates the previous
 * month automatically when missing; submission to management freezes a report
 * as retained history.
 */
const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

const MONTHS = [
  'January', 'February', 'March', 'April', 'May', 'June',
  'July', 'August', 'September', 'October', 'November', 'December',
];
const monthName = (m: number) => MONTHS[m - 1] ?? String(m);

const MONTH_OPTIONS = MONTHS.map((label, i) => ({ value: String(i + 1), label }));

const generateSchema = z.object({
  year: z.coerce.number().int().min(2000).max(2200),
  month: z.string().min(1),
});
type GenerateForm = z.input<typeof generateSchema>;

export default function MonthlyEnvironmentalReportsPage() {
  const queryClient = useQueryClient();
  const router = useRouter();
  const { toast } = useToast();
  const [dialogOpen, setDialogOpen] = useState(false);
  const [busy, setBusy] = useState(false);

  const { data: reports = [] } = useQuery({
    queryKey: ['hr', 'safety-env-compliance', 'monthly-reports'],
    queryFn: () => safetyEnvironmentalComplianceService.getMonthlyReports(),
  });

  const generateForm = useForm<GenerateForm>({ resolver: zodResolver(generateSchema) });

  const openGenerate = () => {
    const now = new Date();
    generateForm.reset({ year: now.getFullYear(), month: String(now.getMonth() + 1) });
    setDialogOpen(true);
  };

  const submitGenerate = generateForm.handleSubmit(async (values) => {
    setBusy(true);
    try {
      const v = generateSchema.parse(values);
      const report = await safetyEnvironmentalComplianceService.generateMonthlyReport(
        v.year,
        Number(v.month),
      );
      await queryClient.invalidateQueries({ queryKey: ['hr', 'safety-env-compliance'] });
      toast({ title: 'Report generated', description: report.reportNumber });
      setDialogOpen(false);
      router.push(`/hr/safety/environmental/monthly-reports/${report.id}`);
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Generating the report failed.',
        variant: 'destructive',
      });
    } finally {
      setBusy(false);
    }
  });

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Monthly Environmental Reports"
        description="Every figure is computed from the live registers at generation time (FR-ENV-033). The reminder engine generates the previous month automatically; submitting a report to management freezes it as retained history (FR-ENV-034)."
        backHref="/hr/safety"
        actions={
          <Button onClick={openGenerate}>
            <Plus className="mr-2 h-4 w-4" /> Generate…
          </Button>
        }
      />

      {reports.length === 0 ? (
        <EmptyState
          title="No reports yet"
          description="Generate a period's report, or wait for the reminder engine to generate last month's."
          icon={FileBarChart}
        />
      ) : (
        <Card>
          <CardContent className="p-0">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Report #</TableHead>
                  <TableHead>Period</TableHead>
                  <TableHead>Generated</TableHead>
                  <TableHead>Compliance %</TableHead>
                  <TableHead>Expired permits</TableHead>
                  <TableHead>Incidents</TableHead>
                  <TableHead>Status</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {reports.map((r) => (
                  <TableRow key={r.id}>
                    <TableCell className="font-mono">
                      <Link
                        href={`/hr/safety/environmental/monthly-reports/${r.id}`}
                        className="text-primary hover:underline"
                      >
                        {r.reportNumber}
                      </Link>
                    </TableCell>
                    <TableCell className="font-medium">
                      {monthName(r.month)} {r.year}
                    </TableCell>
                    <TableCell>{fmtDate(r.generatedAt)}</TableCell>
                    <TableCell>
                      {r.compliancePercentage != null ? `${r.compliancePercentage}%` : '—'}
                    </TableCell>
                    <TableCell
                      className={r.permitsExpired > 0 ? 'text-destructive font-medium' : undefined}
                    >
                      {r.permitsExpired}
                    </TableCell>
                    <TableCell>{r.environmentalIncidents}</TableCell>
                    <TableCell>
                      {r.submittedToManagementAt ? (
                        <Badge>Submitted</Badge>
                      ) : (
                        <Badge variant="secondary">Draft</Badge>
                      )}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </CardContent>
        </Card>
      )}

      <Dialog open={dialogOpen} onOpenChange={(o) => !busy && setDialogOpen(o)}>
        <DialogContent className="max-w-md">
          <DialogHeader>
            <DialogTitle>Generate monthly report</DialogTitle>
            <DialogDescription>
              Regenerating an unsubmitted period recomputes it in place; a submitted period is
              retained history and refuses regeneration.
            </DialogDescription>
          </DialogHeader>
          <form onSubmit={submitGenerate} className="space-y-4">
            <NumberField form={generateForm} name="year" label="Year" required />
            <SelectField
              form={generateForm}
              name="month"
              label="Month"
              required
              options={MONTH_OPTIONS}
            />
            <DialogFooter>
              <Button type="button" variant="outline" disabled={busy} onClick={() => setDialogOpen(false)}>
                Cancel
              </Button>
              <Button type="submit" disabled={busy}>
                {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                Generate
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>
    </div>
  );
}
