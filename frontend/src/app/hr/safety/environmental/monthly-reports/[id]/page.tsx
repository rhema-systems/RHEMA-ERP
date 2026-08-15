'use client';

import { useEffect, useState } from 'react';
import { useParams } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2, RefreshCw, Send } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Textarea } from '@/components/ui/textarea';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { safetyEnvironmentalComplianceService } from '@/services/hr/safety-environmental-compliance.service';

/**
 * One monthly environmental report (FR-ENV-033/034). Figures are computed at
 * generation time; regeneration recomputes an unsubmitted report in place;
 * submission freezes it as retained history.
 */
const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const MONTHS = [
  'January', 'February', 'March', 'April', 'May', 'June',
  'July', 'August', 'September', 'October', 'November', 'December',
];
const monthName = (m: number) => MONTHS[m - 1] ?? String(m);
const fmtGhs = (v: number) => `GHS ${v.toLocaleString(undefined, { maximumFractionDigits: 0 })}`;

function StatRow({ label, value, danger }: { label: string; value: string | number; danger?: boolean }) {
  return (
    <div className="flex items-baseline justify-between gap-4 py-1">
      <span className="text-muted-foreground text-sm">{label}</span>
      <span className={`font-medium ${danger ? 'text-destructive' : ''}`}>{value}</span>
    </div>
  );
}

export default function MonthlyEnvironmentalReportDetailPage() {
  const params = useParams();
  const id = params.id as string;
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [busy, setBusy] = useState(false);
  const [confirmSubmit, setConfirmSubmit] = useState(false);
  const [summaryDraft, setSummaryDraft] = useState('');

  const { data: report, isLoading } = useQuery({
    queryKey: ['hr', 'safety-env-compliance', 'monthly-reports', id],
    queryFn: () => safetyEnvironmentalComplianceService.getMonthlyReport(id),
  });

  useEffect(() => {
    setSummaryDraft(report?.officerSummary ?? '');
  }, [report?.officerSummary]);

  const invalidate = () =>
    queryClient.invalidateQueries({ queryKey: ['hr', 'safety-env-compliance'] });
  const fail = (fallback: string) => (error: any) =>
    toast({ title: 'Error', description: error?.message || fallback, variant: 'destructive' });

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-24">
        <Loader2 className="text-muted-foreground h-6 w-6 animate-spin" />
      </div>
    );
  }
  if (!report) {
    return (
      <div className="p-6">
        <EmptyState title="Report not found" description="It may have been removed." />
      </div>
    );
  }

  const submitted = !!report.submittedToManagementAt;

  const regenerate = async () => {
    setBusy(true);
    try {
      await safetyEnvironmentalComplianceService.generateMonthlyReport(report.year, report.month);
      await invalidate();
      toast({ title: 'Report regenerated', description: report.reportNumber });
    } catch (error: any) {
      fail('Regenerating failed.')(error);
    } finally {
      setBusy(false);
    }
  };

  const saveSummary = async () => {
    setBusy(true);
    try {
      await safetyEnvironmentalComplianceService.updateMonthlyReportSummary(
        report.id,
        summaryDraft.trim() || null,
      );
      await invalidate();
      toast({ title: 'Officer summary saved', description: report.reportNumber });
    } catch (error: any) {
      fail('Saving the summary failed.')(error);
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={report.reportNumber}
        description={`${monthName(report.month)} ${report.year} — generated ${fmtDate(report.generatedAt)} by ${report.generatedByName ?? 'the reminder engine'}.`}
        backHref="/hr/safety/environmental/monthly-reports"
        actions={
          !submitted ? (
            <div className="flex gap-2">
              <Button variant="outline" disabled={busy} onClick={() => void regenerate()}>
                <RefreshCw className="mr-2 h-4 w-4" /> Regenerate
              </Button>
              <Button disabled={busy} onClick={() => setConfirmSubmit(true)}>
                <Send className="mr-2 h-4 w-4" /> Submit to management
              </Button>
            </div>
          ) : undefined
        }
      />

      {submitted && (
        <Card className="border-green-600/40">
          <CardContent className="flex items-center gap-3 py-4">
            <Badge>Submitted</Badge>
            <span className="text-sm">
              Submitted to management on {fmtDate(report.submittedToManagementAt)} by{' '}
              {report.submittedByName ?? '—'} — this report is retained history and can no longer
              change (FR-ENV-034).
            </span>
          </CardContent>
        </Card>
      )}

      <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-4">
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">Compliance</CardTitle>
          </CardHeader>
          <CardContent>
            <StatRow
              label="Obligations compliant"
              value={`${report.obligationsCompliant} / ${report.obligationsTotal}`}
            />
            <StatRow
              label="Compliance percentage"
              value={report.compliancePercentage != null ? `${report.compliancePercentage}%` : '—'}
            />
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">Permits</CardTitle>
          </CardHeader>
          <CardContent>
            <StatRow label="Active" value={report.permitsActive} />
            <StatRow label="Renewals due (90 days)" value={report.permitsExpiringIn90Days} />
            <StatRow label="Expired" value={report.permitsExpired} danger={report.permitsExpired > 0} />
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">Reviews</CardTitle>
          </CardHeader>
          <CardContent>
            <StatRow label="Projects reviewed" value={report.projectsReviewed} />
            <StatRow label="Clearances issued" value={report.clearancesIssued} />
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">Waste</CardTitle>
          </CardHeader>
          <CardContent>
            <StatRow label="Generated (kg)" value={report.wasteGeneratedKg.toLocaleString()} />
            <StatRow label="Recycled (kg)" value={report.wasteRecycledKg.toLocaleString()} />
            <StatRow
              label="Recycling rate"
              value={report.wasteRecyclingRate != null ? `${report.wasteRecyclingRate}%` : '—'}
            />
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">Incidents</CardTitle>
          </CardHeader>
          <CardContent>
            <StatRow label="Reported" value={report.environmentalIncidents} />
            <StatRow label="Closed" value={report.environmentalIncidentsClosed} />
            <StatRow
              label="Monitoring exceedances"
              value={report.monitoringExceedances}
              danger={report.monitoringExceedances > 0}
            />
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">
              Audits & corrective actions
            </CardTitle>
          </CardHeader>
          <CardContent>
            <StatRow label="Audit findings raised" value={report.auditFindingsRaised} />
            <StatRow label="Corrective actions open" value={report.correctiveActionsOpen} />
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">Regulations</CardTitle>
          </CardHeader>
          <CardContent>
            <StatRow label="New regulatory updates" value={report.newRegulatoryUpdates} />
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">Sustainability</CardTitle>
          </CardHeader>
          <CardContent>
            <StatRow label="Active initiatives" value={report.sustainabilityInitiativesActive} />
            <StatRow label="Completed this period" value={report.sustainabilityInitiativesCompleted} />
            <StatRow label="Estimated savings" value={fmtGhs(report.sustainabilityCostSavings)} />
          </CardContent>
        </Card>
      </div>

      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="text-sm font-medium text-muted-foreground">
            Officer summary
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          {submitted ? (
            <p className="text-sm whitespace-pre-wrap">
              {report.officerSummary || 'No narrative was recorded.'}
            </p>
          ) : (
            <>
              <Textarea
                rows={4}
                value={summaryDraft}
                onChange={(e) => setSummaryDraft(e.target.value)}
                placeholder="The officer's narrative for the period — context behind the figures."
              />
              <div className="flex justify-end">
                <Button disabled={busy} onClick={() => void saveSummary()}>
                  {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                  Save summary
                </Button>
              </div>
            </>
          )}
        </CardContent>
      </Card>

      <ConfirmationDialog
        open={confirmSubmit}
        onOpenChange={setConfirmSubmit}
        title={`Submit ${report.reportNumber} to management?`}
        description="Sends the FR-ENV-034 electronic submission through the escalated notification topic and freezes the report — it can no longer be regenerated or edited."
        confirmText="Submit"
        onConfirm={async () => {
          try {
            await safetyEnvironmentalComplianceService.submitMonthlyReport(report.id);
            await invalidate();
            toast({ title: 'Report submitted', description: report.reportNumber });
          } catch (error: any) {
            fail('Submitting failed.')(error);
          } finally {
            setConfirmSubmit(false);
          }
        }}
      />
    </div>
  );
}
