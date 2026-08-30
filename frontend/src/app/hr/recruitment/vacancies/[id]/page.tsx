'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useParams, useRouter } from 'next/navigation';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Ban, ListFilter, Loader2, Megaphone, Send, SlidersHorizontal, Workflow } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Textarea } from '@/components/ui/textarea';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { AttachmentsPanel } from '@/components/hr/common/AttachmentsPanel';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { MetricTiles } from '@/components/hr/common/MetricTiles';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { VacancyPostingsPanel } from '@/components/hr/recruitment/VacancyPostingsPanel';
import { VacancyCriteriaPanel } from '@/components/hr/recruitment/VacancyCriteriaPanel';
import { VacancyStageOwnersPanel } from '@/components/hr/recruitment/VacancyStageOwnersPanel';
import { TalentPoolMatchesPanel } from '@/components/hr/recruitment/TalentPoolMatchesPanel';
import { EeoReportPanel } from '@/components/hr/recruitment/EeoReportPanel';
import { useAuth } from '@/hooks/use-auth';
import { useToast } from '@/hooks/use-toast';
import { formatDate, formatDateTime, formatMoney, humanizeEnum } from '@/lib/hr/attendance-format';
import { jobVacancyService } from '@/services/hr/recruitment.service';
import {
  VACANCY_CLOSURE_REASONS,
  type JobVacancyStatus,
  type VacancyClosureReason,
} from '@/types/hr/recruitment';

function InfoRow({ label, value }: { label: string; value?: React.ReactNode }) {
  return (
    <div className="flex flex-col gap-0.5 py-1.5">
      <span className="text-xs text-muted-foreground">{label}</span>
      <span className="text-sm">{value ?? '—'}</span>
    </div>
  );
}

function InfoCard({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <Card>
      <CardHeader className="pb-2">
        <CardTitle className="text-base">{title}</CardTitle>
      </CardHeader>
      <CardContent className="grid grid-cols-2 gap-x-6 md:grid-cols-3">{children}</CardContent>
    </Card>
  );
}

/**
 * The hiring stages a vacancy walks through after publication, in order.
 *
 * ⚠ This is only what the "Advance to" picker offers. **The server owns the real state machine**
 * and refuses an illegal move with a message naming the legal next states — so a mismatch here
 * shows up as an explained refusal rather than a wrong write. Do not try to mirror the full map.
 */
const HIRING_STAGES: JobVacancyStatus[] = [
  'ClosedForApplications',
  'Shortlisting',
  'Interviewing',
  'OfferStage',
  'Filled',
];

export default function VacancyDetailPage() {
  const router = useRouter();
  const params = useParams();
  const id = (params?.id as string) ?? '';
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const { hasAnyRole } = useAuth();

  const isHr = hasAnyRole(['SuperAdmin', 'HR']);

  const [closing, setClosing] = useState<null | 'cancel' | 'applications'>(null);
  const [closureReason, setClosureReason] = useState<VacancyClosureReason>('Other');
  const [closureNotes, setClosureNotes] = useState('');
  const [busy, setBusy] = useState(false);

  const { data: v, isLoading, isError } = useQuery({
    queryKey: ['hr', 'vacancies', id],
    queryFn: () => jobVacancyService.getById(id),
    enabled: !!id,
  });

  const history = useQuery({
    queryKey: ['hr', 'vacancy-history', id],
    queryFn: () => jobVacancyService.getStatusHistory(id),
    enabled: !!id,
  });

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: ['hr', 'vacancies'] });
    await queryClient.invalidateQueries({ queryKey: ['hr', 'vacancy-history', id] });
    await queryClient.invalidateQueries({ queryKey: ['hr', 'vacancy-postings', id] });
  };

  const changeStatus = useMutation({
    mutationFn: (next: JobVacancyStatus) => jobVacancyService.changeStatus(id, next),
    onSuccess: async () => {
      await refresh();
      toast({ title: 'Status updated' });
    },
    // The server's refusal names the legal next states — that is the message worth showing.
    onError: (e: any) =>
      toast({ title: 'Refused', description: e?.message, variant: 'destructive' }),
  });

  const runClose = async () => {
    if (!closing) return false;
    setBusy(true);
    try {
      if (closing === 'cancel') {
        await jobVacancyService.close(id, closureReason, closureNotes.trim() || null);
      } else {
        await jobVacancyService.closeForApplications(id, closureReason, closureNotes.trim() || null);
      }
      await refresh();
      toast({
        title: closing === 'cancel' ? 'Vacancy cancelled' : 'Closed for applications',
        description: 'Its live adverts have been expired.',
      });
      setClosing(null);
      setClosureNotes('');
      return true;
    } catch (e: any) {
      toast({ title: 'Refused', description: e?.message, variant: 'destructive' });
      return false;
    } finally {
      setBusy(false);
    }
  };

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-24">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (isError || !v) {
    return (
      <div className="p-6">
        <EmptyState title="Vacancy not found" description="It may have been removed." />
      </div>
    );
  }

  const terminal = v.vacancyStatus === 'Cancelled' || v.vacancyStatus === 'Filled';
  const canPublish = v.vacancyStatus === 'Approved';
  const canApprove = v.vacancyStatus === 'Draft' || v.vacancyStatus === 'PendingApproval';

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={v.vacancyNumber}
        description={`${v.jobTitle || v.positionTitle} · ${v.orgUnitName ?? '—'}`}
        backHref="/hr/recruitment/vacancies"
        actions={
          <div className="flex flex-wrap items-center gap-2">
            <StatusBadge status={v.vacancyStatus} />

            {/*
              The two per-vacancy workspaces. They live here rather than in the sidebar because
              neither means anything without a vacancy to scope them to.
            */}
            <Button variant="outline" asChild>
              <Link href={`/hr/recruitment/vacancies/${id}/pipeline`}>
                <Workflow className="mr-2 h-4 w-4" /> Pipeline
              </Link>
            </Button>
            <Button variant="outline" asChild>
              <Link href={`/hr/recruitment/vacancies/${id}/screening`}>
                <ListFilter className="mr-2 h-4 w-4" /> Screening
              </Link>
            </Button>

            {isHr && canApprove && (
              <Button
                variant="outline"
                onClick={() => changeStatus.mutate('Approved')}
                disabled={changeStatus.isPending}
              >
                Approve
              </Button>
            )}

            {isHr && canPublish && (
              <Button onClick={() => changeStatus.mutate('Published')} disabled={changeStatus.isPending}>
                <Megaphone className="mr-2 h-4 w-4" /> Publish
              </Button>
            )}

            {isHr && v.vacancyStatus === 'Published' && (
              <Button
                variant="outline"
                onClick={() => {
                  setClosureReason('ApplicationDeadlinePassed');
                  setClosing('applications');
                }}
              >
                <Send className="mr-2 h-4 w-4" /> Close for applications
              </Button>
            )}

            {isHr && !terminal && (
              <Select
                value=""
                onValueChange={(next) => changeStatus.mutate(next as JobVacancyStatus)}
              >
                <SelectTrigger className="w-[180px]">
                  <SlidersHorizontal className="mr-2 h-4 w-4" />
                  <SelectValue placeholder="Advance to…" />
                </SelectTrigger>
                <SelectContent>
                  {HIRING_STAGES.filter((s) => s !== v.vacancyStatus).map((s) => (
                    <SelectItem key={s} value={s}>
                      {humanizeEnum(s)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            )}

            {isHr && !terminal && (
              <Button
                variant="destructive"
                onClick={() => {
                  setClosureReason('PositionEliminated');
                  setClosing('cancel');
                }}
              >
                <Ban className="mr-2 h-4 w-4" /> Cancel
              </Button>
            )}
          </div>
        }
      />

      <MetricTiles
        tiles={[
          { label: 'Applications', value: v.applicationCount },
          { label: 'Shortlisted', value: v.shortlistedCount },
          { label: 'Offers', value: v.offerCount },
          { label: 'Hired', value: `${v.hireCount} of ${v.numberOfPositions}` },
        ]}
      />

      <Tabs defaultValue="overview">
        <TabsList>
          <TabsTrigger value="overview">Overview</TabsTrigger>
          <TabsTrigger value="adverts">Adverts</TabsTrigger>
          <TabsTrigger value="criteria">Shortlisting criteria</TabsTrigger>
          <TabsTrigger value="stage-owners">Stage owners</TabsTrigger>
          <TabsTrigger value="pool-matches">Pool matches</TabsTrigger>
          <TabsTrigger value="attachments">Attachments</TabsTrigger>
          {isHr && <TabsTrigger value="eeo">EEO report</TabsTrigger>}
          <TabsTrigger value="history">History</TabsTrigger>
        </TabsList>

        <TabsContent value="overview" className="space-y-4 pt-4">
          <InfoCard title="The role">
            <InfoRow label="Job title" value={v.jobTitle} />
            <InfoRow label="Position" value={v.positionTitle} />
            <InfoRow label="Organisation unit" value={v.orgUnitName} />
            <InfoRow
              label="Requisition"
              value={
                <Link
                  href={`/hr/recruitment/requisitions/${v.staffRequisitionId}`}
                  className="text-primary hover:underline"
                >
                  {v.requisitionNumber}
                </Link>
              }
            />
            <InfoRow label="Employment type" value={humanizeEnum(v.employmentType)} />
            <InfoRow label="Work mode" value={humanizeEnum(v.workMode)} />
            <InfoRow label="Heads" value={v.numberOfPositions} />
            <InfoRow
              label="Audience"
              value={[v.allowInternalCandidates && 'Internal', v.allowExternalCandidates && 'External']
                .filter(Boolean)
                .join(' and ') || '—'}
            />
            <InfoRow label="Pipeline" value={v.pipelineName} />
          </InfoCard>

          <InfoCard title="Dates and people">
            <InfoRow label="Planned publish" value={formatDate(v.publishDate)} />
            <InfoRow label="Actually published" value={formatDate(v.actualPublishDate)} />
            <InfoRow label="Application deadline" value={formatDate(v.applicationDeadline)} />
            <InfoRow label="Shortlisting deadline" value={formatDate(v.shortlistingDeadline)} />
            <InfoRow label="Target start" value={formatDate(v.targetStartDate)} />
            <InfoRow label="Interview rounds" value={v.numberOfInterviewRounds ?? '—'} />
            <InfoRow label="Hiring manager" value={v.hiringManagerName} />
            <InfoRow label="Recruiter" value={v.recruiterName} />
            <InfoRow label="Closed" value={formatDate(v.closedDate)} />
          </InfoCard>

          <InfoCard title="Package and assessment">
            <InfoRow
              label="Salary range"
              value={
                v.salaryRangeMin != null || v.salaryRangeMax != null
                  ? `${formatMoney(v.salaryRangeMin ?? 0, v.salaryCurrencyCode ?? 'GHS')} – ${formatMoney(
                      v.salaryRangeMax ?? 0,
                      v.salaryCurrencyCode ?? 'GHS',
                    )}`
                  : '—'
              }
            />
            <InfoRow label="Shown on the advert" value={v.isSalaryVisible ? 'Yes' : 'No'} />
            <InfoRow label="Minimum experience" value={v.requiredMinExperienceYears ?? '—'} />
            <InfoRow label="Written test" value={v.requiresWrittenTest ? 'Required' : 'No'} />
            <InfoRow label="Practical test" value={v.requiresPracticalTest ? 'Required' : 'No'} />
          </InfoCard>

          {v.keyBenefitsSummary && (
            <Card>
              <CardHeader className="pb-2">
                <CardTitle className="text-base">Key benefits</CardTitle>
              </CardHeader>
              <CardContent>
                <p className="whitespace-pre-wrap text-sm">{v.keyBenefitsSummary}</p>
              </CardContent>
            </Card>
          )}

          {(v.closureReason || v.closureNotes) && (
            <InfoCard title="Closure">
              <InfoRow label="Reason" value={humanizeEnum(v.closureReason)} />
              <InfoRow label="Notes" value={v.closureNotes} />
            </InfoCard>
          )}
        </TabsContent>

        <TabsContent value="adverts" className="pt-4">
          <VacancyPostingsPanel
            vacancyId={id}
            vacancyStatus={v.vacancyStatus}
            canManage={isHr}
          />
        </TabsContent>

        <TabsContent value="criteria" className="pt-4">
          <VacancyCriteriaPanel vacancyId={id} canManage={isHr} />
        </TabsContent>

        {/* ⚠ Stage OWNERS, not the application board — /pipeline moves applications between
            stages; this assigns who is responsible for each stage of this vacancy. */}
        <TabsContent value="stage-owners" className="pt-4">
          <VacancyStageOwnersPanel
            vacancyId={id}
            pipelineId={v.recruitmentPipelineId}
            canManage={isHr}
          />
        </TabsContent>

        <TabsContent value="pool-matches" className="pt-4">
          <TalentPoolMatchesPanel vacancyId={id} />
        </TabsContent>

        <TabsContent value="attachments" className="pt-4">
          <AttachmentsPanel
            title="Vacancy attachments"
            queryKey={['hr', 'vacancy-attachments', id]}
            list={() => jobVacancyService.getAttachments(id) as any}
            upload={(file, description) =>
              jobVacancyService.uploadAttachment(id, file, description) as any
            }
            download={async (attachment: any) => {
              const blob = await jobVacancyService.downloadAttachment(id, attachment.id);
              const url = URL.createObjectURL(blob);
              const a = document.createElement('a');
              a.href = url;
              a.download = attachment.fileName;
              a.click();
              URL.revokeObjectURL(url);
            }}
            remove={isHr ? (attachmentId) => jobVacancyService.deleteAttachment(attachmentId) : undefined}
            readOnly={!isHr}
            emptyDescription="The signed job description, the advert artwork, agency terms."
            note="Files are scanned and stored in the document repository; they are never public links."
          />
        </TabsContent>

        {isHr && (
          <TabsContent value="eeo" className="pt-4">
            <EeoReportPanel vacancyId={id} />
          </TabsContent>
        )}

        <TabsContent value="history" className="pt-4">
          <Card>
            <CardHeader className="pb-3">
              <CardTitle className="text-base">Status history</CardTitle>
            </CardHeader>
            <CardContent className="p-0">
              {history.isLoading ? (
                <div className="flex items-center justify-center py-10">
                  <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
                </div>
              ) : (history.data?.length ?? 0) === 0 ? (
                <div className="py-8">
                  <EmptyState title="Nothing yet" description="Status changes appear here." />
                </div>
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>When</TableHead>
                      <TableHead>From</TableHead>
                      <TableHead>To</TableHead>
                      <TableHead>By</TableHead>
                      <TableHead>Reason</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {(history.data ?? []).map((h) => (
                      <TableRow key={h.id}>
                        <TableCell className="whitespace-nowrap text-sm">
                          {formatDateTime(h.changedDate)}
                        </TableCell>
                        <TableCell>
                          <StatusBadge status={h.fromStatus} />
                        </TableCell>
                        <TableCell>
                          <StatusBadge status={h.toStatus} />
                        </TableCell>
                        <TableCell>{h.changedByName || '—'}</TableCell>
                        <TableCell className="text-sm text-muted-foreground">
                          {humanizeEnum(h.reason) || '—'}
                          {h.comments ? ` — ${h.comments}` : ''}
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>

      <ConfirmationDialog
        open={closing !== null}
        onOpenChange={(open) => {
          if (!open) {
            setClosing(null);
            setClosureNotes('');
          }
        }}
        title={closing === 'cancel' ? 'Cancel this vacancy' : 'Close this vacancy for applications'}
        description={
          closing === 'cancel'
            ? 'Cancelling is final — the vacancy cannot be reopened, and its live adverts will be expired.'
            : 'The vacancy stays open for shortlisting, but stops accepting applications and its live adverts are expired.'
        }
        confirmText={busy ? 'Working…' : 'Confirm'}
        onConfirm={runClose}
      >
        <div className="space-y-3">
          <div className="space-y-1.5">
            <Label>Reason</Label>
            <Select
              value={closureReason}
              onValueChange={(val) => setClosureReason(val as VacancyClosureReason)}
            >
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {VACANCY_CLOSURE_REASONS.map((reason) => (
                  <SelectItem key={reason} value={reason}>
                    {humanizeEnum(reason)}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="closureNotes">Notes</Label>
            <Textarea
              id="closureNotes"
              rows={3}
              value={closureNotes}
              onChange={(e) => setClosureNotes(e.target.value)}
            />
          </div>
        </div>
      </ConfirmationDialog>
    </div>
  );
}
