'use client';

import { useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { Loader2, CalendarClock, Award, Stamp } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { useToast } from '@/components/ui/use-toast';
import { TextField, DateField, FieldRow } from '@/components/hr/employee/tabs/fields';
import { trainingCertificateService } from '@/services/hr/training-certificate.service';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { trainingNominationService } from '@/services/hr/training-nomination.service';
import {
  NOMINATION_STATUS_OPTIONS,
  NOMINATION_TYPE_OPTIONS,
  TRAINING_COMPLETION_STATUS_OPTIONS,
} from '@/types/hr/training-delivery';

const statusLabel = (v: string) => NOMINATION_STATUS_OPTIONS.find((o) => o.value === v)?.label ?? v;
const typeLabel = (v: string) => NOMINATION_TYPE_OPTIONS.find((o) => o.value === v)?.label ?? v;
const completionLabel = (v: string) =>
  TRAINING_COMPLETION_STATUS_OPTIONS.find((o) => o.value === v)?.label ?? v;
const fmt = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

export default function NominationDetailPage() {
  const router = useRouter();
  const params = useParams();
  const id = (params?.id as string) ?? '';

  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [issueOpen, setIssueOpen] = useState(false);
  const [issuing, setIssuing] = useState(false);

  const { data: n, isLoading, isError } = useQuery({
    queryKey: ['hr', 'training', 'nominations', id],
    queryFn: () => trainingNominationService.getById(id),
    enabled: !!id,
  });

  const issueForm = useForm<{ certificateName: string; issuedDate: string; expiryDate: string }>({
    defaultValues: {
      certificateName: '',
      issuedDate: new Date().toISOString().slice(0, 10),
      expiryDate: '',
    },
  });

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-24">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (isError || !n) {
    return (
      <div className="p-6">
        <EmptyState title="Nomination not found" description="It may have been removed." />
      </div>
    );
  }

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={n.employeeName}
        description={`${n.nominationNumber} · ${n.programName}`}
        backHref="/hr/training/approvals"
        actions={
          <div className="flex items-center gap-2">
            <StatusBadge status={statusLabel(n.status)} />
            {/* A certificate belongs to a completed nomination, which is why it is issued here and
                not from the certificate register. */}
            {n.hasCompletionRecord && n.completionRecord?.isPassed && (
              <Button
                size="sm"
                onClick={() => {
                  issueForm.reset({
                    certificateName: `${n.programName} — Certificate of Completion`,
                    issuedDate: new Date().toISOString().slice(0, 10),
                    expiryDate: '',
                  });
                  setIssueOpen(true);
                }}
              >
                <Stamp className="mr-2 h-4 w-4" /> Issue certificate
              </Button>
            )}
            <Button
              variant="outline"
              size="sm"
              onClick={() => router.push(`/hr/training/schedules/${n.scheduleId}`)}
            >
              <CalendarClock className="mr-2 h-4 w-4" /> Open schedule
            </Button>
          </div>
        }
      />

      <Card>
        <CardHeader>
          <CardTitle>Nomination</CardTitle>
        </CardHeader>
        <CardContent className="grid gap-4 py-2 sm:grid-cols-2">
          <Detail label="Employee" value={`${n.employeeName} (${n.employeeNumber})`} />
          <Detail label="Department" value={n.employeeDepartment ?? '—'} />
          <Detail label="Position" value={n.employeePosition ?? '—'} />
          <Detail label="Type" value={typeLabel(n.type)} />
          <Detail label="Nominated by" value={n.nominatedByName ?? '—'} />
          <Detail label="Nominated on" value={fmt(n.nominationDate)} />
          <Detail label="Schedule" value={n.scheduleNumber} />
          <Detail label="Training dates" value={`${fmt(n.trainingStartDate)} – ${fmt(n.trainingEndDate)}`} />
          <div className="sm:col-span-2">
            <p className="text-sm text-muted-foreground">Justification</p>
            <p className="font-medium">{n.justification || '—'}</p>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Approval trail</CardTitle>
        </CardHeader>
        <CardContent className="grid gap-4 py-2 sm:grid-cols-2">
          <Detail label="Supervisor" value={n.supervisorApprovedByName ?? 'Not yet'} />
          <Detail label="Supervisor decided" value={fmt(n.supervisorApprovalDate)} />
          <div className="sm:col-span-2">
            <p className="text-sm text-muted-foreground">Supervisor comments</p>
            <p className="font-medium">{n.supervisorComments || '—'}</p>
          </div>
          <Detail label="HR" value={n.hrApprovedByName ?? 'Not yet'} />
          <Detail label="HR decided" value={fmt(n.hrApprovalDate)} />
          <div className="sm:col-span-2">
            <p className="text-sm text-muted-foreground">HR comments</p>
            <p className="font-medium">{n.hrComments || '—'}</p>
          </div>
          {n.rejectionReason && (
            <div className="sm:col-span-2">
              <p className="text-sm text-muted-foreground">Rejection reason</p>
              <p className="font-medium text-destructive">{n.rejectionReason}</p>
            </div>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Outcome</CardTitle>
        </CardHeader>
        <CardContent className="py-2">
          {n.hasCompletionRecord && n.completionRecord ? (
            <div className="grid gap-4 sm:grid-cols-4">
              <Detail label="Status" value={completionLabel(n.completionRecord.status)} />
              <Detail label="Completed" value={fmt(n.completionRecord.completionDate)} />
              <Detail
                label="Score"
                value={
                  typeof n.completionRecord.finalScore === 'number'
                    ? String(n.completionRecord.finalScore)
                    : '—'
                }
              />
              <div>
                <p className="text-sm text-muted-foreground">Result</p>
                <div className="mt-1 flex items-center gap-2">
                  <Badge variant={n.completionRecord.isPassed ? 'default' : 'destructive'}>
                    {n.completionRecord.isPassed ? 'Passed' : 'Not passed'}
                  </Badge>
                  {n.completionRecord.isVerifiedByManager ? (
                    <Badge variant="secondary">Verified</Badge>
                  ) : (
                    <span className="text-xs text-amber-600">Unverified</span>
                  )}
                </div>
              </div>
            </div>
          ) : (
            <EmptyState
              icon={Award}
              title="No completion recorded"
              description="Once the training has run, record the outcome from the Completions screen."
            />
          )}
        </CardContent>
      </Card>
      <Dialog open={issueOpen} onOpenChange={setIssueOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Issue certificate</DialogTitle>
            <DialogDescription>
              For {n.employeeName} against {n.programName}. A verification code is generated so third
              parties can check it.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4 py-2">
            <TextField form={issueForm} name="certificateName" label="Certificate name" required />
            <FieldRow>
              <DateField form={issueForm} name="issuedDate" label="Issued" required />
              <DateField form={issueForm} name="expiryDate" label="Expires (optional)" />
            </FieldRow>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setIssueOpen(false)} disabled={issuing}>
              Cancel
            </Button>
            <Button
              disabled={issuing}
              onClick={async () => {
                const values = issueForm.getValues();
                if (!values.certificateName.trim()) {
                  toast({ title: 'A certificate name is required', variant: 'destructive' });
                  return;
                }
                setIssuing(true);
                try {
                  const cert = await trainingCertificateService.issue({
                    nominationId: n.id,
                    employeeId: n.employeeId,
                    programId: n.programId,
                    certificateName: values.certificateName.trim(),
                    issuedDate: new Date(values.issuedDate).toISOString(),
                    expiryDate: values.expiryDate ? new Date(values.expiryDate).toISOString() : null,
                  });
                  await queryClient.invalidateQueries({ queryKey: ['hr', 'training', 'certificates'] });
                  toast({
                    title: 'Certificate issued',
                    description: `${cert.certificateNumber} — verification code ${cert.verificationCode}`,
                  });
                  setIssueOpen(false);
                } catch (error: any) {
                  toast({
                    title: 'Error',
                    description: error?.message || 'Failed to issue the certificate.',
                    variant: 'destructive',
                  });
                } finally {
                  setIssuing(false);
                }
              }}
            >
              Issue
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

function Detail({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <p className="text-sm text-muted-foreground">{label}</p>
      <p className="font-medium">{value}</p>
    </div>
  );
}
